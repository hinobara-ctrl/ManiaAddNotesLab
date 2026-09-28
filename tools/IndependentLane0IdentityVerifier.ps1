param(
    [Parameter(Mandatory = $true)][string]$Root,
    [Parameter(Mandatory = $true)][string]$Contract
)

$ErrorActionPreference = 'Stop'
$Root = (Resolve-Path $Root).Path
$Contract = (Resolve-Path $Contract).Path

Add-Type -TypeDefinition @'
using System.Collections.Generic;
public sealed class IndependentLane0CounterClosureContract {
 public string SchemaVersion {get;set;} public string AuditedBaselineHead {get;set;}
 public string ParentHardeningContractSha256 {get;set;} public string IdentityAlgorithm {get;set;}
 public string RequiredPublicationBinding {get;set;} public string RepairImplementationSha256 {get;set;}
 public List<string> RepairImplementationFiles {get;set;} public string CorrectiveHarnessSha256 {get;set;}
 public List<string> CorrectiveHarnessFiles {get;set;} public string CorpusManifestSha256 {get;set;}
 public string MemoryLimit {get;set;} public string CounterSemantics {get;set;}
 public List<string> RequiredControls {get;set;} public SortedDictionary<string,string> OutcomeRules {get;set;}
 public SortedDictionary<string,string> ReusedDependencies {get;set;}
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

function Get-TreeIdentity([string[]]$Paths, [string]$Revision) {
    $rows = foreach ($path in ($Paths | Sort-Object -CaseSensitive)) {
        [byte[]]$raw = if ($Revision -eq 'WORKING') {
            [IO.File]::ReadAllBytes((Join-Path $Root $path))
        } else {
            Get-GitBlob $Revision $path
        }
        $hash = Get-Sha256 (ConvertTo-NormalizedBytes $raw)
        "$($path.Replace('\', '/'))|$hash"
    }
    Get-Sha256 ([Text.UTF8Encoding]::new($false).GetBytes(($rows -join "`n")))
}

$rawContract = [IO.File]::ReadAllText($Contract)
$document = [System.Text.Json.JsonDocument]::Parse($rawContract)
$options = [System.Text.Json.JsonSerializerOptions]::new()
$options.PropertyNamingPolicy = [System.Text.Json.JsonNamingPolicy]::CamelCase
$options.WriteIndented = $false
$typed = [System.Text.Json.JsonSerializer]::Deserialize(
    $document.RootElement.GetProperty('contract').GetRawText(),
    [IndependentLane0CounterClosureContract], $options)
$canonicalBytes = [System.Text.Json.JsonSerializer]::SerializeToUtf8Bytes(
    [object]$typed, [IndependentLane0CounterClosureContract], $options)
$canonical = Get-Sha256 $canonicalBytes
$artifact = $rawContract | ConvertFrom-Json
$implementation = Get-TreeIdentity @($artifact.contract.repairImplementationFiles) 'WORKING'
$harness = Get-TreeIdentity @($artifact.contract.correctiveHarnessFiles) 'WORKING'
$implementationCommit = try {
    Get-TreeIdentity @($artifact.contract.repairImplementationFiles) 'HEAD'
} catch { 'NOT_PRESENT_AT_HEAD' }
$harnessCommit = try {
    Get-TreeIdentity @($artifact.contract.correctiveHarnessFiles) 'HEAD'
} catch { 'NOT_PRESENT_AT_HEAD' }

Write-Output "contract declared=$($artifact.canonicalSha256) calculated=$canonical"
Write-Output "implementation declared=$($artifact.contract.repairImplementationSha256) working=$implementation commit=$implementationCommit"
Write-Output "harness declared=$($artifact.contract.correctiveHarnessSha256) working=$harness commit=$harnessCommit"

$failed = $canonical -ne $artifact.canonicalSha256 `
    -or $implementation -ne $artifact.contract.repairImplementationSha256 `
    -or $harness -ne $artifact.contract.correctiveHarnessSha256
foreach ($pair in $artifact.contract.reusedDependencies.PSObject.Properties) {
    $actual = Get-Sha256 (ConvertTo-NormalizedBytes ([IO.File]::ReadAllBytes((Join-Path $Root $pair.Name))))
    Write-Output "dependency $($pair.Name) declared=$($pair.Value) calculated=$actual"
    if ($actual -ne $pair.Value) { $failed = $true }
}
$document.Dispose()
if ($failed) { exit 1 }
