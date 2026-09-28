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

public static class Lane0PreparationCanonicalJson
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

function Get-NormalizedHash([string]$Path, [string]$Revision) {
    [byte[]]$raw = if ($Revision -eq 'WORKING') {
        [IO.File]::ReadAllBytes((Join-Path $Root $Path))
    } else {
        Get-GitBlob $Revision $Path
    }
    Get-Sha256 (ConvertTo-NormalizedBytes $raw)
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
$canonical = [Lane0PreparationCanonicalJson]::HashContract($raw)
$instrumentWorking = Get-TreeIdentity @($body.instrumentImplementationFiles) 'WORKING'
$instrumentHead = Get-TreeIdentity @($body.instrumentImplementationFiles) 'HEAD'
$v3HarnessWorking = Get-TreeIdentity @($body.instrumentHarnessFiles) 'WORKING'
$v3HarnessHead = Get-TreeIdentity @($body.instrumentHarnessFiles) 'HEAD'
$preparationHarness = Get-TreeIdentity @($body.preparationHarnessFiles) 'WORKING'

Write-Output "contract declared=$($artifact.canonicalSha256) calculated=$canonical"
Write-Output "v3 implementation declared=$($body.repairImplementationSha256) working=$instrumentWorking head=$instrumentHead"
Write-Output "v3 harness declared=$($body.instrumentHarnessSha256) working=$v3HarnessWorking head=$v3HarnessHead"
Write-Output "preparation harness declared=$($body.preparationHarnessSha256) working=$preparationHarness head=AWAITING_MANUAL_PUBLICATION"

$failed = $canonical -ne $artifact.canonicalSha256 `
    -or $instrumentWorking -ne $body.repairImplementationSha256 `
    -or $instrumentHead -ne $body.repairImplementationSha256 `
    -or $v3HarnessWorking -ne $body.instrumentHarnessSha256 `
    -or $v3HarnessHead -ne $body.instrumentHarnessSha256 `
    -or $preparationHarness -ne $body.preparationHarnessSha256

foreach ($pair in $body.reusedDependencies.PSObject.Properties) {
    $working = Get-NormalizedHash $pair.Name 'WORKING'
    $head = Get-NormalizedHash $pair.Name 'HEAD'
    Write-Output "dependency $($pair.Name) declared=$($pair.Value) working=$working head=$head"
    if ($working -ne $pair.Value -or $head -ne $pair.Value) { $failed = $true }
}

foreach ($pair in $body.preparedArtifactIdentities.PSObject.Properties) {
    $working = Get-NormalizedHash $pair.Name 'WORKING'
    Write-Output "prepared artifact $($pair.Name) declared=$($pair.Value) working=$working"
    if ($working -ne $pair.Value) { $failed = $true }
}

if ($body.executionRoute.evaluationBodyPresent `
    -or $body.executionRoute.authorizationBindingPresent `
    -or $body.executionRoute.humanAuthorizationGranted `
    -or $body.executionRoute.currentState -ne 'BLOCKED') {
    Write-Error 'Corrective execution route is not safely blocked.'
    $failed = $true
}

if ($failed) { exit 1 }
Write-Output 'LANE.0 corrective preparation verification PASS'
