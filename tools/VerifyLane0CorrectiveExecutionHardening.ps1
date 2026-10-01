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
public static class Lane0ExecutionHardeningCanonicalJson
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
function Get-CanonicalContractHash([string]$Path, [string]$Revision = 'WORKING') {
    $raw = [Text.UTF8Encoding]::new($false, $true).GetString((Get-NormalizedBytes $Path $Revision))
    [Lane0ExecutionHardeningCanonicalJson]::HashContract($raw)
}

$raw = [IO.File]::ReadAllText($Contract)
$artifact = $raw | ConvertFrom-Json
$body = $artifact.contract
$canonical = [Lane0ExecutionHardeningCanonicalJson]::HashContract($raw)
$implementation = Get-TreeIdentity @($body.hardeningImplementationFiles)
$runner = Get-TreeIdentity @($body.officialRunnerFiles)
$launcher = Get-NormalizedHash $body.isolatedLauncherPath
$harness = Get-TreeIdentity @($body.hardeningHarnessFiles)
$runtime = Get-TreeIdentity @($body.runtimeScientificFiles)
$verifier = Get-NormalizedHash $body.verifierPath
$failed = $false

Write-Output "contract declared=$($artifact.canonicalSha256) calculated=$canonical"
Write-Output "hardening implementation tree declared=$($body.hardeningImplementationSha256) working=$implementation"
Write-Output "official runner tree declared=$($body.officialRunnerTreeSha256) working=$runner"
Write-Output "isolated launcher normalized file declared=$($body.isolatedLauncherNormalizedSha256) working=$launcher"
Write-Output "hardening harness tree declared=$($body.hardeningHarnessSha256) working=$harness"
Write-Output "runtime scientific tree declared=$($body.runtimeScientificTreeSha256) working=$runtime"
Write-Output "verifier normalized file declared=$($body.verifierNormalizedSha256) working=$verifier"

if ($canonical -ne $artifact.canonicalSha256 -or $implementation -ne $body.hardeningImplementationSha256 `
    -or $runner -ne $body.officialRunnerTreeSha256 -or $harness -ne $body.hardeningHarnessSha256 `
    -or $launcher -ne $body.isolatedLauncherNormalizedSha256 `
    -or $runtime -ne $body.runtimeScientificTreeSha256 -or $verifier -ne $body.verifierNormalizedSha256) {
    $failed = $true
}

$expected = @{
    schemaVersion = 'lane-0-corrective-execution-hardening.1'
    baselinePublishedHead = 'b3a87c5420371af212ff5f0512e5137dffb1e2bc'
    parentPreparationContractCanonicalSha256 = '61C05856AC8BB48ABFC5BCFD3AA47599D2D5F567CBA6E39EC7AF27DD235B8F1E'
    parentPreparationImplementationSha256 = 'A518278E7B0CCBEA8C7916720035C598073382CFF2616E790340DD6F45F8D219'
    parentPreparationHarnessSha256 = '0501B1AA2625259AB66249A749C0963CE725C6678E2591CEF5D3CC3D7ADDC170'
    parentPreparationVerifierSha256 = '182CBAEFCD3B010EBA7C4505DA8F105BF3732E9D5D2BCD4D042531B920B201E0'
    preregistrationCanonicalSha256 = '6392C579B87EC318C1DE381201D797B004650AB961398E3E2AB23F7A49A83D33'
    evaluatorHardeningCanonicalSha256 = 'A61F0933A49534857096D3A568E9C07F37805F9468CC94001546767073CD2E18'
    evaluatorImplementationSha256 = '20D0AC6BB0AF197ACC6BE1B5E71D1DF65BD2CDC0FCAB0E49FD6F036D03A0D8F4'
    evaluatorHarnessSha256 = '724837E42F2308DA3159BAE00F3339656C03EE936B9E350F416124A7E92AA173'
    solutionNormalizedSha256 = 'BFEB1689414A2FB712170A967B9BA6DAD70BFE656D307A25821A976D81942A37'
    expectedC11ManifestSha256 = 'AC28C73F65B7FC896E02046E9715C8A24156FBB9B200439657B6A6F0F3E81445'
    bindingSchema = 'lane-0-corrective-evaluation-publication-binding.2'
    officialExecutionMode = 'LOCAL_ISOLATED_CLONE_OF_PUBLISHED_HEAD'
    officialBootstrapMode = 'HOST_POWERSHELL_GIT_PRECHECK_BEFORE_REPO_CODE'
    externalPreflightRequired = $true
    approvedHeadInvariant = 'BINDING_HEAD_EQUALS_SOURCE_HEAD_EQUALS_ISOLATED_HEAD_EQUALS_RUNTIME_HEAD'
    launcherDefenseInDepthBindingHeadCheck = $true
    authorizationRootRequired = $true
    authorizationRootPolicy = 'SOURCE_REPOSITORY_MATCHING_APPROVED_HEAD_TRACKED_CLEAN_WITH_BYTE_IDENTICAL_CANONICAL_BINDING'
    attemptReceiptRoot = 'AUTHORIZATION_ROOT'
    attemptReceiptDurability = 'SHARED_ACROSS_ISOLATED_EXECUTION_ROOTS_FOR_CANONICAL_AUTHORIZATION_ROOT'
    executionOutputRoot = 'ISOLATED_EXECUTION_ROOT'
    durableAttemptReceiptPreflightRequired = $true
    oneShotScope = 'CANONICAL_AUTHORIZATION_ROOT_AND_BINDING_CEREMONY'
    officialCommandShape = 'HOST_PRECHECK_APPROVED_HEAD_AND_TRACKED_CLEAN -> pwsh -NoProfile -File tools/InvokeLane0CorrectiveExecutionIsolated.ps1 -RepositoryRoot . -ExecutionRoot <DEDICATED_NONEXISTENT_PATH> -CorpusRoot <EXPLICIT_FROZEN_C11_ROOT>'
    currentState = 'READY FOR MANUAL PUBLICATION AND FINAL INDEPENDENT AUDIT / NEW BINDING V2 NOT YET ISSUED / C11 EXECUTION BLOCKED'
}
foreach ($pair in $expected.GetEnumerator()) {
    if ($body.($pair.Key) -ne $pair.Value) { Write-Error "Frozen declaration mismatch: $($pair.Key)"; $failed = $true }
}
$expectedExternalPreflight = @(
    'READ_CANONICAL_BINDING_AS_DATA',
    'REQUIRE_BINDING_SCHEMA_V2',
    'READ_SOURCE_HEAD_WITH_GIT',
    'REQUIRE_APPROVED_HEAD_EQUALS_SOURCE_HEAD',
    'REQUIRE_SOURCE_TRACKED_UNSTAGED_CLEAN',
    'REQUIRE_SOURCE_TRACKED_STAGED_CLEAN',
    'REQUIRE_CANONICAL_BINDING_UNTRACKED_AT_HEAD',
    'REQUIRE_DURABLE_ATTEMPT_RECEIPT_ABSENT',
    'ONLY_THEN_INVOKE_TRACKED_ISOLATED_LAUNCHER'
)
if (@($body.externalPreflightSteps).Count -ne $expectedExternalPreflight.Count) {
    Write-Error 'External host preflight step count drifted.'; $failed = $true
} else {
    for ($index = 0; $index -lt $expectedExternalPreflight.Count; $index++) {
        if ($body.externalPreflightSteps[$index] -cne $expectedExternalPreflight[$index]) {
            Write-Error "External host preflight step drifted at index $index."; $failed = $true
        }
    }
}
$expectedRunnerArguments = @('--repo-root', '--authorization-root', '--corpus-root')
if (@($body.officialRunnerArgumentSet).Count -ne $expectedRunnerArguments.Count) {
    Write-Error 'Official runner argument count drifted.'; $failed = $true
} else {
    for ($index = 0; $index -lt $expectedRunnerArguments.Count; $index++) {
        if ($body.officialRunnerArgumentSet[$index] -cne $expectedRunnerArguments[$index]) {
            Write-Error "Official runner argument drifted at index $index."; $failed = $true
        }
    }
}

$parentCanonical = Get-CanonicalContractHash $body.parentPreparationContractPath
$parentImplementation = Get-TreeIdentity @($body.parentPreparationImplementationFiles)
$parentHarness = Get-TreeIdentity @($body.parentPreparationHarnessFiles)
$parentVerifier = Get-NormalizedHash $body.parentPreparationVerifierPath
$evaluatorCanonical = Get-CanonicalContractHash $body.evaluatorHardeningContractPath
$evaluatorImplementation = Get-TreeIdentity @($body.evaluatorImplementationFiles)
$evaluatorHarness = Get-TreeIdentity @($body.evaluatorHarnessFiles)
$solution = Get-NormalizedHash $body.solutionPath
$flatEvaluator = Get-NormalizedHash $body.evaluatorImplementationFiles[0]
Write-Output "evaluator normalized single-file hash=$flatEvaluator"
Write-Output "evaluator path|hash tree identity=$evaluatorImplementation"
if ($parentCanonical -ne $body.parentPreparationContractCanonicalSha256 `
    -or $parentImplementation -ne $body.parentPreparationImplementationSha256 `
    -or $parentHarness -ne $body.parentPreparationHarnessSha256 `
    -or $parentVerifier -ne $body.parentPreparationVerifierSha256 `
    -or $evaluatorCanonical -ne $body.evaluatorHardeningCanonicalSha256 `
    -or $evaluatorImplementation -ne $body.evaluatorImplementationSha256 `
    -or $evaluatorHarness -ne $body.evaluatorHarnessSha256 `
    -or $solution -ne $body.solutionNormalizedSha256 `
    -or $flatEvaluator -eq $evaluatorImplementation) { $failed = $true }

if ($body.bindingPresent -or $body.humanAuthorization -or $body.attemptReceiptPresent `
    -or $body.corpusAccessPerformed -or $body.correctiveEvaluationExecuted `
    -or $body.behaviorChange -or $body.rngChange -or $body.defaultChange `
    -or $body.scientificSemanticsChanged -or $body.successorAuthorization) {
    Write-Error 'Closed safety state drifted.'; $failed = $true
}
foreach ($path in @($body.bindingCanonicalPath, $body.attemptReceiptPath, $body.stagingPath, $body.finalOutputPath)) {
    if (Test-Path -LiteralPath (Join-Path $Root $path)) { Write-Error "Forbidden live execution state exists: $path"; $failed = $true }
}

$frozen = @($body.parentPreparationFiles) + @($body.runtimeScientificFiles) + @(
    'ManiaAddNotesLab.sln',
    'tools/ManiaAddNotesLab.Experiments/Program.cs',
    'src/ManiaAddNotesLab.Core/AddNotesEngine.cs'
) | Sort-Object -Unique
foreach ($path in $frozen) {
    if ((Get-NormalizedHash $path) -ne (Get-NormalizedHash $path $body.baselinePublishedHead)) {
        Write-Error "Historical/scientific file drifted from b3a87c: $path"; $failed = $true
    }
}

$runnerSource = [Text.UTF8Encoding]::new($false).GetString((Get-NormalizedBytes $body.officialRunnerProgramPath))
$launcherSource = [Text.UTF8Encoding]::new($false).GetString((Get-NormalizedBytes $body.isolatedLauncherPath))
$hardeningSource = [Text.UTF8Encoding]::new($false).GetString((Get-NormalizedBytes $body.hardeningImplementationFiles[0]))
$solutionText = [Text.UTF8Encoding]::new($false).GetString((Get-NormalizedBytes $body.solutionPath))
$bindingParseIndex = $launcherSource.IndexOf('[Text.Json.JsonDocument]::Parse', [StringComparison]::Ordinal)
$bindingSchemaIndex = $launcherSource.IndexOf("bindingSchema.GetString() -cne 'lane-0-corrective-evaluation-publication-binding.2'", [StringComparison]::Ordinal)
$bindingHeadIndex = $launcherSource.IndexOf('[string]::Equals($approvedPublishedHead, $sourceHead', [StringComparison]::Ordinal)
$executionRootIndex = $launcherSource.IndexOf('[IO.Path]::IsPathFullyQualified($ExecutionRoot)', [StringComparison]::Ordinal)
$cloneIndex = $launcherSource.IndexOf("'clone', '--no-hardlinks', '--no-checkout'", [StringComparison]::Ordinal)
$dotnetIndex = $launcherSource.IndexOf("'run', '--project', `$RunnerProject, '-c', 'Release'", [StringComparison]::Ordinal)
$receiptPrecheckIndex = $launcherSource.IndexOf("`$AttemptReceiptSource = Join-Path `$repository '.artifacts/lane_0_corrective.attempt.json'", [StringComparison]::Ordinal)
if (-not $runnerSource.Contains('Lane0CorrectiveExecutionHardening.Execute', [StringComparison]::Ordinal) `
    -or $runnerSource.Contains('Lane0CorrectiveExecutionPreparation.Execute', [StringComparison]::Ordinal) `
    -or $solutionText.Contains('ManiaAddNotesLab.CorrectiveExecution.csproj', [StringComparison]::Ordinal) `
    -or $body.bindingSchema -eq 'lane-0-corrective-evaluation-publication-binding.1') {
    Write-Error 'Official runner exposure or binding schema is invalid.'; $failed = $true
}
if (@($body.preBuildAllowedUntracked).Count -ne 1 `
    -or $body.preBuildAllowedUntracked[0] -ne $body.bindingCanonicalPath `
    -or $body.executionRootPolicy -ne 'NONEXISTENT / PRESERVE_AFTER_ATTEMPT' `
    -or $body.sourceWorkingTreeUntrackedPolicy -ne 'NOT_COPIED_TO_EXECUTION_ROOT' `
    -or $body.isolatedExecutionTreePolicy -ne 'TRACKED_HEAD_PLUS_CANONICAL_BINDING_ONLY_BEFORE_BUILD' `
    -or $bindingParseIndex -lt 0 -or $bindingSchemaIndex -le $bindingParseIndex `
    -or $bindingHeadIndex -le $bindingSchemaIndex -or $executionRootIndex -le $bindingHeadIndex `
    -or $receiptPrecheckIndex -le $bindingHeadIndex -or $cloneIndex -le $receiptPrecheckIndex `
    -or $dotnetIndex -le $cloneIndex `
    -or -not $launcherSource.Contains('TryGetInt32([ref]$authorizedExecutionCount)', [StringComparison]::Ordinal) `
    -or -not $launcherSource.Contains("'clone', '--no-hardlinks', '--no-checkout'", [StringComparison]::Ordinal) `
    -or -not $launcherSource.Contains("'checkout', '--detach'", [StringComparison]::Ordinal) `
    -or -not $launcherSource.Contains("'status', '--porcelain=v1', '--untracked-files=all', '--ignored'", [StringComparison]::Ordinal) `
    -or -not $launcherSource.Contains('[IO.File]::Exists($execution)', [StringComparison]::Ordinal) `
    -or -not $launcherSource.Contains('[IO.Directory]::Exists($execution)', [StringComparison]::Ordinal) `
    -or -not $launcherSource.Contains('[IO.File]::WriteAllBytes($bindingDestination, $bindingBytes)', [StringComparison]::Ordinal) `
    -or -not $launcherSource.Contains("'run', '--project', `$RunnerProject, '-c', 'Release'", [StringComparison]::Ordinal) `
    -or -not $launcherSource.Contains("'--repo-root', `$execution, '--authorization-root', `$repository", [StringComparison]::Ordinal) `
    -or -not $launcherSource.Contains('[IO.File]::Exists($AttemptReceiptSource)', [StringComparison]::Ordinal) `
    -or -not $launcherSource.Contains('[IO.Directory]::Exists($AttemptReceiptSource)', [StringComparison]::Ordinal) `
    -or $launcherSource.Contains("'fetch'", [StringComparison]::OrdinalIgnoreCase) `
    -or $launcherSource.Contains("'pull'", [StringComparison]::OrdinalIgnoreCase) `
    -or $launcherSource.Contains('retry', [StringComparison]::OrdinalIgnoreCase) `
    -or $launcherSource.Contains('--no-build', [StringComparison]::OrdinalIgnoreCase) `
    -or $launcherSource.Contains('Remove-Item', [StringComparison]::OrdinalIgnoreCase) `
    -or $launcherSource.Contains('Directory]::Delete', [StringComparison]::OrdinalIgnoreCase) `
    -or $launcherSource.Contains('File]::Delete', [StringComparison]::OrdinalIgnoreCase) `
    -or $launcherSource.Contains('Copy-Item', [StringComparison]::OrdinalIgnoreCase) `
    -or $launcherSource -match '(Resolve-Path|Test-Path|GetFullPath|EnumerateFiles|ReadAllBytes)[^\r\n]*CorpusRoot' `
    -or -not $runnerSource.Contains('out var authorizationRoot', [StringComparison]::Ordinal) `
    -or -not $runnerSource.Contains('new Lane0HardenedExecutionRequest(repositoryRoot!, authorizationRoot!, corpusRoot!)', [StringComparison]::Ordinal) `
    -or -not $hardeningSource.Contains('Path.Combine(source, AttemptReceiptPath)', [StringComparison]::Ordinal) `
    -or $hardeningSource.Contains('Path.Combine(root, AttemptReceiptPath)', [StringComparison]::Ordinal) `
    -or -not $hardeningSource.Contains('sourceBindingBytes.AsSpan().SequenceEqual(executionBindingBytes)', [StringComparison]::Ordinal) `
    -or -not $hardeningSource.Contains('FileMode.CreateNew, FileAccess.Write, FileShare.None', [StringComparison]::Ordinal) `
    -or -not $hardeningSource.Contains('Directory.EnumerateFileSystemEntries(staging)', [StringComparison]::Ordinal) `
    -or -not $hardeningSource.Contains('actualEntries.Any(path => !File.Exists(path))', [StringComparison]::Ordinal)) {
    Write-Error 'Isolated launcher or all-entry staging semantics are invalid.'; $failed = $true
}
foreach ($path in @('tools/ManiaAddNotesLab.Experiments/Program.cs','src/ManiaAddNotesLab.Cli/Program.cs','src/ManiaAddNotesLab.Web/Program.cs')) {
    if (Test-Path -LiteralPath (Join-Path $Root $path)) {
        $text = [Text.UTF8Encoding]::new($false).GetString((Get-NormalizedBytes $path))
        if ($text.Contains('Lane0CorrectiveExecutionHardening', [StringComparison]::Ordinal)) {
            Write-Error "Product surface exposes hardened execution: $path"; $failed = $true
        }
    }
}

if ($failed) { exit 1 }
Write-Output 'LANE.0 corrective execution hardening verification PASS'
