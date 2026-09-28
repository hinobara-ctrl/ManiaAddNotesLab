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

public static class Lane0CorrectiveEvaluatorHardeningCanonicalJson
{
    public static string HashContract(string raw)
    {
        using var document = JsonDocument.Parse(raw);
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream))
            Write(writer, document.RootElement.GetProperty("contract"));
        return Convert.ToHexString(SHA256.HashData(stream.ToArray()));
    }

    private static void Write(Utf8JsonWriter writer, JsonElement element)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.Object:
                writer.WriteStartObject();
                foreach (var property in element.EnumerateObject().OrderBy(x => x.Name, StringComparer.Ordinal))
                {
                    writer.WritePropertyName(property.Name);
                    Write(writer, property.Value);
                }
                writer.WriteEndObject();
                break;
            case JsonValueKind.Array:
                writer.WriteStartArray();
                foreach (var item in element.EnumerateArray()) Write(writer, item);
                writer.WriteEndArray();
                break;
            default:
                element.WriteTo(writer);
                break;
        }
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
    [Text.UTF8Encoding]::new($false).GetBytes($text)
}

function Get-GitBlob([string]$Revision, [string]$Path) {
    $start = [Diagnostics.ProcessStartInfo]::new('git')
    $start.WorkingDirectory = $Root
    $start.ArgumentList.Add('cat-file')
    $start.ArgumentList.Add('blob')
    $start.ArgumentList.Add("$Revision`:$Path")
    $start.RedirectStandardOutput = $true
    $start.RedirectStandardError = $true
    $start.UseShellExecute = $false
    $process = [Diagnostics.Process]::Start($start)
    $memory = [IO.MemoryStream]::new()
    $process.StandardOutput.BaseStream.CopyTo($memory)
    $process.WaitForExit()
    if ($process.ExitCode -ne 0) { throw "git cat-file failed for $Revision`:$Path" }
    $result = $memory.ToArray()
    $memory.Dispose()
    $process.Dispose()
    return ,$result
}

function Get-NormalizedBytes([string]$Path, [string]$Revision) {
    [byte[]]$raw = if ($Revision -eq 'WORKING') {
        [IO.File]::ReadAllBytes((Join-Path $Root $Path))
    } else {
        Get-GitBlob $Revision $Path
    }
    return ,(ConvertTo-NormalizedBytes $raw)
}

function Get-NormalizedHash([string]$Path, [string]$Revision) {
    Get-Sha256 (Get-NormalizedBytes $Path $Revision)
}

function Get-TreeIdentity([string[]]$Paths, [string]$Revision) {
    $rows = foreach ($path in ($Paths | Sort-Object -CaseSensitive)) {
        "$($path.Replace('\', '/'))|$(Get-NormalizedHash $path $Revision)"
    }
    Get-Sha256 ([Text.UTF8Encoding]::new($false).GetBytes(($rows -join "`n")))
}

$raw = [IO.File]::ReadAllText($Contract)
$artifact = $raw | ConvertFrom-Json
$body = $artifact.contract
$canonical = [Lane0CorrectiveEvaluatorHardeningCanonicalJson]::HashContract($raw)
$implementation = Get-TreeIdentity @($body.evaluatorImplementationFiles) 'WORKING'
$harness = Get-TreeIdentity @($body.evaluatorHarnessFiles) 'WORKING'
$solutionHash = Get-NormalizedHash $body.solutionPath 'WORKING'
$solutionText = [Text.UTF8Encoding]::new($false).GetString(
    (Get-NormalizedBytes $body.solutionPath 'WORKING'))
$projectReferenceCount = [regex]::Matches($solutionText,
    [regex]::Escape($body.correctiveTestProjectReference)).Count

[byte[]]$parentContractBytes = Get-GitBlob $body.parentPublishedHead $body.parentDesignContractPath
$parentContractRaw = [Text.UTF8Encoding]::new($false, $true).GetString($parentContractBytes)
$parentArtifact = $parentContractRaw | ConvertFrom-Json
$parentCanonical = [Lane0CorrectiveEvaluatorHardeningCanonicalJson]::HashContract($parentContractRaw)
$parentImplementation = Get-TreeIdentity @($body.parentEvaluatorImplementationFiles) $body.parentPublishedHead
$parentHarness = Get-TreeIdentity @($body.parentEvaluatorHarnessFiles) $body.parentPublishedHead

$failed = $false
Write-Output "successor contract declared=$($artifact.canonicalSha256) calculated=$canonical"
Write-Output "successor implementation declared=$($body.evaluatorImplementationSha256) working=$implementation"
Write-Output "successor harness declared=$($body.evaluatorHarnessSha256) working=$harness"
Write-Output "solution declared=$($body.solutionNormalizedSha256) working=$solutionHash projectReferences=$projectReferenceCount"
Write-Output "parent contract declared=$($body.parentDesignContractCanonicalSha256) blobCalculated=$parentCanonical"
Write-Output "parent implementation declared=$($body.parentEvaluatorImplementationSha256) blobCalculated=$parentImplementation"
Write-Output "parent harness declared=$($body.parentEvaluatorHarnessSha256) blobCalculated=$parentHarness"

if ($canonical -ne $artifact.canonicalSha256 `
    -or $implementation -ne $body.evaluatorImplementationSha256 `
    -or $harness -ne $body.evaluatorHarnessSha256 `
    -or $solutionHash -ne $body.solutionNormalizedSha256 `
    -or $projectReferenceCount -ne 1 `
    -or $parentCanonical -ne $body.parentDesignContractCanonicalSha256 `
    -or $parentArtifact.canonicalSha256 -ne $body.parentDesignContractCanonicalSha256 `
    -or $parentImplementation -ne $body.parentEvaluatorImplementationSha256 `
    -or $parentHarness -ne $body.parentEvaluatorHarnessSha256) { $failed = $true }

$expected = @{
    parentPublishedHead = 'd005534fc9b27827ebd3951368c3eb5eb714ee87'
    parentDesignContractCanonicalSha256 = '074B84F57D0ED3870C744ABCD1F00E2BA9B9C587F917A9823C76F46D06DF01EB'
    parentEvaluatorImplementationSha256 = '9CA35582CCB70B5D85434373709D511A3B896BCDF1353137F8FE6724DBA53128'
    parentEvaluatorHarnessSha256 = 'F00289DF36021C0211F3EE888A1EFF0D332390F1219EFC3391EEF487D9CC63D0'
    parentPreregistrationCanonicalSha256 = '6392C579B87EC318C1DE381201D797B004650AB961398E3E2AB23F7A49A83D33'
    instrumentV3CanonicalSha256 = '221D5133D8058FEBEAEA0A13F83058219D900261B033282EE389A82BA20FAAF7'
    instrumentImplementationSha256 = '2F938F18D98C6299214C080195633A68B68C688CABF49DC0F4DEFFCA9C1897D9'
    instrumentHarnessSha256 = '1EFC2B824FAA6F0798C3FFA679FCA073939D88B2C751612C06BB75C240E96309'
    preparationHarnessSha256 = 'AC1DD7469AC2876B1A31226DDB311CDF74F5D64ABEACC6C442C461F5F87964E3'
    expectedC11Manifest = 'AC28C73F65B7FC896E02046E9715C8A24156FBB9B200439657B6A6F0F3E81445'
}
foreach ($pair in $expected.GetEnumerator()) {
    if ($body.($pair.Key) -ne $pair.Value) {
        Write-Error "Frozen identity mismatch: $($pair.Key)"
        $failed = $true
    }
}

if ($parentArtifact.contract.evaluatorImplementationSha256 -ne $body.parentEvaluatorImplementationSha256 `
    -or $parentArtifact.contract.evaluatorHarnessSha256 -ne $body.parentEvaluatorHarnessSha256 `
    -or $parentArtifact.contract.parentPreregistrationCanonicalSha256 -ne $body.parentPreregistrationCanonicalSha256 `
    -or $parentArtifact.contract.instrumentV3CanonicalSha256 -ne $body.instrumentV3CanonicalSha256) {
    Write-Error 'Parent d005 contract declarations do not match successor references.'
    $failed = $true
}

$requiredInputs = @($body.scientificRequiredInputs)
if ($requiredInputs.Count -ne 2 `
    -or $requiredInputs -notcontains 'historicalSnapshot' `
    -or $requiredInputs -notcontains 'historicalG1CaseRule' `
    -or $body.missingRequiredScientificInputOutcome -ne 'BLOCKED' `
    -or $body.suppliedHistoricalFactMismatchOutcome -ne 'INVALID' `
    -or $body.outputDirectoryPolicy -ne 'NONEXISTENT_OR_EMPTY_ONLY' `
    -or $body.corpusLoaderPresent `
    -or $body.cliExecutionExposed `
    -or $body.authorizationBindingPresent `
    -or $body.humanAuthorizationGranted `
    -or $body.c11AccessAuthorized `
    -or $body.currentState -ne 'READY_FOR_PUBLICATION_AND_AUDIT / EXECUTION_BLOCKED') {
    Write-Error 'Successor safety state or scientific-input policy is invalid.'
    $failed = $true
}

if ($failed) { exit 1 }
Write-Output 'LANE.0 corrective evaluator hardening verification PASS'
