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

public static class Lane0CorrectiveEvaluatorCanonicalJson
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

function Get-NormalizedHash([string]$Path) {
    Get-Sha256 (ConvertTo-NormalizedBytes ([IO.File]::ReadAllBytes((Join-Path $Root $Path))) )
}

function Get-TreeIdentity([string[]]$Paths) {
    $rows = foreach ($path in ($Paths | Sort-Object -CaseSensitive)) {
        "$($path.Replace('\', '/'))|$(Get-NormalizedHash $path)"
    }
    Get-Sha256 ([Text.UTF8Encoding]::new($false).GetBytes(($rows -join "`n")))
}

$raw = [IO.File]::ReadAllText($Contract)
$artifact = $raw | ConvertFrom-Json
$body = $artifact.contract
$canonical = [Lane0CorrectiveEvaluatorCanonicalJson]::HashContract($raw)
$implementation = Get-TreeIdentity @($body.evaluatorImplementationFiles)
$harness = Get-TreeIdentity @($body.evaluatorHarnessFiles)
$failed = $false

Write-Output "contract declared=$($artifact.canonicalSha256) calculated=$canonical"
Write-Output "evaluator implementation declared=$($body.evaluatorImplementationSha256) working=$implementation head=AWAITING_MANUAL_PUBLICATION"
Write-Output "evaluator harness declared=$($body.evaluatorHarnessSha256) working=$harness head=AWAITING_MANUAL_PUBLICATION"

if ($canonical -ne $artifact.canonicalSha256 `
    -or $implementation -ne $body.evaluatorImplementationSha256 `
    -or $harness -ne $body.evaluatorHarnessSha256) { $failed = $true }

$expected = @{
    publishedPreparationHead = '3c89e0b42d4e26509381f40218e3d5304fb2c5ac'
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

foreach ($pair in $body.frozenDependencyHashes.PSObject.Properties) {
    $working = Get-NormalizedHash $pair.Name
    Write-Output "dependency $($pair.Name) declared=$($pair.Value) working=$working"
    if ($working -ne $pair.Value) { $failed = $true }
}

if (-not $body.evaluatorCorePresent `
    -or $body.corpusLoaderPresent `
    -or $body.cliExecutionExposed `
    -or $body.authorizationBindingPresent `
    -or $body.humanAuthorizationGranted `
    -or $body.c11AccessAuthorized `
    -or $body.currentState -ne 'READY_FOR_PUBLICATION_AND_AUDIT / EXECUTION_BLOCKED' `
    -or @($body.researchQuestions.PSObject.Properties).Count -ne 9 `
    -or $body.historicalCaseRule.total -ne 11 `
    -or $body.historicalCaseRule.keymode -ne 7) {
    Write-Error 'Evaluator state, RQ inventory or historical-case guard is invalid.'
    $failed = $true
}

if ($failed) { exit 1 }
Write-Output 'LANE.0 corrective evaluator design verification PASS'
