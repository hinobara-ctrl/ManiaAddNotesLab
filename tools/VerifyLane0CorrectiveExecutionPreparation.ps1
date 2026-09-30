param(
    [Parameter(Mandatory = $true)][string]$Root,
    [Parameter(Mandatory = $true)][string]$Contract
)

$ErrorActionPreference = 'Stop'
$Root = (Resolve-Path $Root).Path
$Contract = (Resolve-Path $Contract).Path

Add-Type -TypeDefinition @'
using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text.Json;
public static class Lane0ExecutionPreparationCanonicalJson
{
    public static string HashContract(string raw)
    {
        using var document = JsonDocument.Parse(raw);
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream)) Write(writer, document.RootElement.GetProperty("contract"));
        return Convert.ToHexString(SHA256.HashData(stream.ToArray()));
    }
    private static void Write(Utf8JsonWriter writer, JsonElement value)
    {
        if (value.ValueKind == JsonValueKind.Object) {
            writer.WriteStartObject();
            foreach (var property in value.EnumerateObject().OrderBy(x => x.Name, StringComparer.Ordinal)) {
                writer.WritePropertyName(property.Name); Write(writer, property.Value);
            }
            writer.WriteEndObject();
        } else if (value.ValueKind == JsonValueKind.Array) {
            writer.WriteStartArray(); foreach (var item in value.EnumerateArray()) Write(writer, item); writer.WriteEndArray();
        } else value.WriteTo(writer);
    }
}
'@

function Get-Sha256([byte[]]$Bytes) {
    [Convert]::ToHexString([Security.Cryptography.SHA256]::HashData($Bytes))
}
function ConvertTo-NormalizedBytes([byte[]]$Bytes) {
    $text = [Text.UTF8Encoding]::new($false, $true).GetString($Bytes)
    if ($text.StartsWith([char]0xFEFF)) { $text = $text.Substring(1) }
    $text = $text.Replace("`r`n", "`n").Replace("`r", "`n")
    return ,[Text.UTF8Encoding]::new($false).GetBytes($text)
}
function Get-GitBlob([string]$Revision, [string]$Path) {
    $start = [Diagnostics.ProcessStartInfo]::new('git')
    $start.WorkingDirectory = $Root
    $start.ArgumentList.Add('cat-file'); $start.ArgumentList.Add('blob'); $start.ArgumentList.Add("$Revision`:$Path")
    $start.RedirectStandardOutput = $true; $start.RedirectStandardError = $true; $start.UseShellExecute = $false
    $process = [Diagnostics.Process]::Start($start)
    $memory = [IO.MemoryStream]::new(); $process.StandardOutput.BaseStream.CopyTo($memory); $process.WaitForExit()
    if ($process.ExitCode -ne 0) { throw "git cat-file failed for $Revision`:$Path" }
    $result = $memory.ToArray(); $memory.Dispose(); $process.Dispose(); return ,$result
}
function Get-NormalizedBytes([string]$Path, [string]$Revision = 'WORKING') {
    [byte[]]$raw = if ($Revision -eq 'WORKING') { [IO.File]::ReadAllBytes((Join-Path $Root $Path)) } else { Get-GitBlob $Revision $Path }
    return ,(ConvertTo-NormalizedBytes $raw)
}
function Get-NormalizedHash([string]$Path, [string]$Revision = 'WORKING') {
    Get-Sha256 (Get-NormalizedBytes $Path $Revision)
}
function Get-TreeIdentity([string[]]$Paths, [string]$Revision = 'WORKING') {
    $rows = foreach ($path in ($Paths | Sort-Object -CaseSensitive)) {
        "$($path.Replace('\', '/'))|$(Get-NormalizedHash $path $Revision)"
    }
    Get-Sha256 ([Text.UTF8Encoding]::new($false).GetBytes(($rows -join "`n")))
}

$raw = [IO.File]::ReadAllText($Contract)
$artifact = $raw | ConvertFrom-Json
$body = $artifact.contract
$canonical = [Lane0ExecutionPreparationCanonicalJson]::HashContract($raw)
$implementation = Get-TreeIdentity @($body.executionPreparationImplementationFiles)
$harness = Get-TreeIdentity @($body.successorHarnessFiles)
$verifier = Get-NormalizedHash $body.verifierPath
$solution = Get-NormalizedHash $body.solutionPath
$failed = $false

Write-Output "successor contract declared=$($artifact.canonicalSha256) calculated=$canonical"
Write-Output "implementation declared=$($body.executionPreparationImplementationSha256) working=$implementation"
Write-Output "harness declared=$($body.successorHarnessSha256) working=$harness"
Write-Output "verifier declared=$($body.verifierNormalizedSha256) working=$verifier"
Write-Output "solution declared=$($body.solutionNormalizedSha256) working=$solution"

if ($canonical -ne $artifact.canonicalSha256 `
    -or $implementation -ne $body.executionPreparationImplementationSha256 `
    -or $harness -ne $body.successorHarnessSha256 `
    -or $verifier -ne $body.verifierNormalizedSha256 `
    -or $solution -ne $body.solutionNormalizedSha256) { $failed = $true }

$expected = @{
    schemaVersion = 'lane-0-corrective-execution-preparation.1'
    baselinePublishedHead = '9b065535248e7dd7581822af80cf6534b16922eb'
    baselineParentHead = 'f3037260f3f5d7e74e906885b6b7ed1c3f2d1510'
    preregistrationCanonicalSha256 = '6392C579B87EC318C1DE381201D797B004650AB961398E3E2AB23F7A49A83D33'
    evaluatorHardeningCanonicalSha256 = 'A61F0933A49534857096D3A568E9C07F37805F9468CC94001546767073CD2E18'
    evaluatorImplementationSha256 = '20D0AC6BB0AF197ACC6BE1B5E71D1DF65BD2CDC0FCAB0E49FD6F036D03A0D8F4'
    evaluatorHarnessSha256 = '724837E42F2308DA3159BAE00F3339656C03EE936B9E350F416124A7E92AA173'
    solutionNormalizedSha256 = 'BFEB1689414A2FB712170A967B9BA6DAD70BFE656D307A25821A976D81942A37'
    expectedC11ManifestSha256 = 'AC28C73F65B7FC896E02046E9715C8A24156FBB9B200439657B6A6F0F3E81445'
    bindingSchema = 'lane-0-corrective-evaluation-publication-binding.1'
    bindingCanonicalPath = 'docs/lane_0_corrective_evaluation_publication_binding.json'
    officialOutputPath = '.artifacts/lane_0_corrective'
    currentState = 'READY FOR MANUAL PUBLICATION AND INDEPENDENT AUDIT / BINDING NOT YET ISSUED / C11 EXECUTION BLOCKED'
}
foreach ($pair in $expected.GetEnumerator()) {
    if ($body.($pair.Key) -ne $pair.Value) { Write-Error "Frozen declaration mismatch: $($pair.Key)"; $failed = $true }
}

if ($body.bindingPresent -or $body.humanAuthorization -or $body.corpusAccessPerformed `
    -or $body.correctiveEvaluationExecuted -or $body.successorAuthorization `
    -or -not $body.corpusAdapterPresent -or $body.authorizedExecutionCount -ne 1 `
    -or $body.behaviorChange -or $body.rngChange -or $body.defaultChange) {
    Write-Error 'Closed preparation state or one-shot declaration drifted.'; $failed = $true
}
if (Test-Path -LiteralPath (Join-Path $Root $body.bindingCanonicalPath)) {
    Write-Error 'The authoritative publication binding must remain absent.'; $failed = $true
}

$frozenFiles = @(
    'ManiaAddNotesLab.sln',
    'tools/ManiaAddNotesLab.Experiments/Lane0CorrectiveEvaluationRunner.cs',
    'tools/ManiaAddNotesLab.Experiments/Lane0FeasibilityRunner.cs',
    'tools/ManiaAddNotesLab.Experiments/Program.cs',
    'tests/ManiaAddNotesLab.CorrectiveEvaluator.Tests/Lane0CorrectiveEvaluatorDesignTests.cs',
    'tests/ManiaAddNotesLab.CorrectiveEvaluator.Tests/Lane0CorrectiveEvaluatorHardeningTests.cs',
    'tests/ManiaAddNotesLab.CorrectiveEvaluator.Tests/ManiaAddNotesLab.CorrectiveEvaluator.Tests.csproj',
    'tools/VerifyLane0CorrectiveEvaluatorDesign.ps1',
    'tools/VerifyLane0CorrectiveEvaluatorHardening.ps1',
    'tools/IndependentLane0IdentityVerifier.ps1',
    'tools/DocConsistency/Program.cs',
    'src/ManiaAddNotesLab.Core/InteriorRelationFeasibilityResearch.cs',
    'src/ManiaAddNotesLab.Core/InteriorRelationHoldoutAuditor.cs',
    'src/ManiaAddNotesLab.Core/InteriorRelationMembershipResearch.cs',
    'src/ManiaAddNotesLab.Core/FrozenC11ManifestResearch.cs',
    'src/ManiaAddNotesLab.Core/C11CorpusDiscovery.cs',
    'docs/lane_0_corrective_evaluation_preregistration_contract.json',
    'docs/lane_0_corrective_evaluator_design_contract.json',
    'docs/lane_0_corrective_evaluator_hardening_contract.json',
    'docs/PHASE_LANE_0_CORRECTIVE_EVALUATOR_DESIGN.md',
    'docs/PHASE_LANE_0_CORRECTIVE_EVALUATOR_HARDENING.md',
    'docs/PHASE_LANE_0_CORRECTIVE_EVALUATOR_POST_PUBLICATION_AUDIT.md',
    'docs/lane_0_future_held_counter_closure_contract.json'
)
foreach ($path in $frozenFiles) {
    $working = Get-NormalizedHash $path
    $baseline = Get-NormalizedHash $path $body.baselinePublishedHead
    if ($working -ne $baseline) { Write-Error "Frozen file drifted from baseline: $path"; $failed = $true }
}

$forbiddenRouteFiles = @(
    'tools/ManiaAddNotesLab.Experiments/Program.cs',
    'src/ManiaAddNotesLab.Cli/Program.cs',
    'src/ManiaAddNotesLab.Web/Program.cs'
) | Where-Object { Test-Path -LiteralPath (Join-Path $Root $_) }
foreach ($path in $forbiddenRouteFiles) {
    $text = [Text.UTF8Encoding]::new($false).GetString((Get-NormalizedBytes $path))
    if ($text.Contains('Lane0CorrectiveExecutionPreparation', [StringComparison]::Ordinal)) {
        Write-Error "Productive route exposes corrective preparation: $path"; $failed = $true
    }
}

if ($failed) { exit 1 }
Write-Output 'LANE.0 corrective execution preparation verification PASS'
