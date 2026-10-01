param(
    [Parameter(Mandatory = $true)][string]$RepositoryRoot,
    [Parameter(Mandatory = $true)][string]$ExecutionRoot,
    [Parameter(Mandatory = $true)][string]$CorpusRoot
)

$ErrorActionPreference = 'Stop'
$BindingRelativePath = 'docs/lane_0_corrective_evaluation_publication_binding.json'
$RunnerProject = 'tools/ManiaAddNotesLab.CorrectiveExecution/ManiaAddNotesLab.CorrectiveExecution.csproj'

function Invoke-CapturedProcess {
    param(
        [Parameter(Mandatory = $true)][string]$FileName,
        [Parameter(Mandatory = $true)][string[]]$Arguments,
        [Parameter(Mandatory = $true)][string]$WorkingDirectory
    )
    $start = [Diagnostics.ProcessStartInfo]::new($FileName)
    $start.WorkingDirectory = $WorkingDirectory
    $start.UseShellExecute = $false
    $start.RedirectStandardOutput = $true
    $start.RedirectStandardError = $true
    foreach ($argument in $Arguments) { $start.ArgumentList.Add($argument) }
    $process = [Diagnostics.Process]::Start($start)
    if ($null -eq $process) { throw "Unable to start $FileName." }
    $stdout = $process.StandardOutput.ReadToEnd()
    $stderr = $process.StandardError.ReadToEnd()
    $process.WaitForExit()
    $result = [pscustomobject]@{ ExitCode = $process.ExitCode; Stdout = $stdout; Stderr = $stderr }
    $process.Dispose()
    return $result
}

function Invoke-Git {
    param(
        [Parameter(Mandatory = $true)][string]$WorkingDirectory,
        [Parameter(Mandatory = $true)][string[]]$Arguments,
        [switch]$AllowFailure
    )
    $result = Invoke-CapturedProcess -FileName 'git' -Arguments $Arguments -WorkingDirectory $WorkingDirectory
    if (-not $AllowFailure -and $result.ExitCode -ne 0) {
        throw "git $($Arguments -join ' ') failed: $($result.Stderr.Trim())"
    }
    return $result
}

function Get-Sha256 {
    param([Parameter(Mandatory = $true)][byte[]]$Bytes)
    return [Convert]::ToHexString([Security.Cryptography.SHA256]::HashData($Bytes))
}

$repository = (Resolve-Path -LiteralPath $RepositoryRoot).Path
$headResult = Invoke-Git -WorkingDirectory $repository -Arguments @('rev-parse', '--verify', 'HEAD')
$sourceHead = $headResult.Stdout.Trim()
if ($sourceHead -notmatch '^[0-9a-fA-F]{40}$') { throw 'INVALID: source HEAD is not a full Git object identity.' }

$unstaged = Invoke-Git -WorkingDirectory $repository -Arguments @('diff', '--quiet', 'HEAD', '--') -AllowFailure
$staged = Invoke-Git -WorkingDirectory $repository -Arguments @('diff', '--cached', '--quiet') -AllowFailure
if ($unstaged.ExitCode -gt 1 -or $staged.ExitCode -gt 1) {
    throw 'INVALID: source tracked-clean state could not be determined.'
}
if ($unstaged.ExitCode -ne 0 -or $staged.ExitCode -ne 0) {
    throw 'INVALID: source repository contains tracked staged or unstaged changes.'
}

$bindingSource = Join-Path $repository $BindingRelativePath
if (-not [IO.File]::Exists($bindingSource)) {
    throw "BLOCKED: canonical publication binding is absent at $BindingRelativePath."
}
$trackedBinding = Invoke-Git -WorkingDirectory $repository -Arguments @(
    'cat-file', '-e', "$sourceHead`:$BindingRelativePath"
) -AllowFailure
if ($trackedBinding.ExitCode -eq 0) { throw 'INVALID: canonical publication binding is tracked at source HEAD.' }
if ($trackedBinding.ExitCode -ne 128) {
    throw 'INVALID: canonical publication binding tracked state could not be determined.'
}
$bindingBytes = [IO.File]::ReadAllBytes($bindingSource)
$bindingSourceSha256 = Get-Sha256 -Bytes $bindingBytes
$bindingDocument = $null
try {
    $bindingDocument = [Text.Json.JsonDocument]::Parse(
        [ReadOnlyMemory[byte]]::new($bindingBytes))
    $bindingRoot = $bindingDocument.RootElement
    if ($bindingRoot.ValueKind -ne [Text.Json.JsonValueKind]::Object) {
        throw 'binding root must be an object'
    }
    $bindingSchema = $bindingRoot.GetProperty('schemaVersion')
    $bindingHead = $bindingRoot.GetProperty('approvedPublishedHead')
    $bindingCount = $bindingRoot.GetProperty('authorizedExecutionCount')
    if ($bindingSchema.ValueKind -ne [Text.Json.JsonValueKind]::String -or
        $bindingSchema.GetString() -cne 'lane-0-corrective-evaluation-publication-binding.2') {
        throw 'binding schema must be publication binding v2'
    }
    if ($bindingHead.ValueKind -ne [Text.Json.JsonValueKind]::String) {
        throw 'approvedPublishedHead must be a string'
    }
    $approvedPublishedHead = $bindingHead.GetString()
    if ($approvedPublishedHead -notmatch '^[0-9a-fA-F]{40}$') {
        throw 'approvedPublishedHead must be exactly 40 hexadecimal characters'
    }
    if (-not [string]::Equals($approvedPublishedHead, $sourceHead,
            [StringComparison]::OrdinalIgnoreCase)) {
        throw 'approvedPublishedHead does not equal source HEAD'
    }
    [int]$authorizedExecutionCount = 0
    if ($bindingCount.ValueKind -ne [Text.Json.JsonValueKind]::Number -or
        -not $bindingCount.TryGetInt32([ref]$authorizedExecutionCount)) {
        throw 'authorizedExecutionCount must be a valid integer'
    }
}
catch {
    throw "INVALID: pre-build publication binding validation failed: $($_.Exception.Message)"
}
finally {
    if ($null -ne $bindingDocument) { $bindingDocument.Dispose() }
}

if (-not [IO.Path]::IsPathFullyQualified($ExecutionRoot)) {
    throw 'BLOCKED: ExecutionRoot must be an absolute path.'
}
$execution = [IO.Path]::GetFullPath($ExecutionRoot)
if ([IO.File]::Exists($execution) -or [IO.Directory]::Exists($execution)) {
    throw 'BLOCKED: ExecutionRoot must be absolutely nonexistent.'
}
$relativeExecution = [IO.Path]::GetRelativePath($repository, $execution)
if (-not [IO.Path]::IsPathRooted($relativeExecution) -and
    $relativeExecution -ne '..' -and
    -not $relativeExecution.StartsWith("..$([IO.Path]::DirectorySeparatorChar)", [StringComparison]::Ordinal) -and
    -not $relativeExecution.StartsWith("..$([IO.Path]::AltDirectorySeparatorChar)", [StringComparison]::Ordinal)) {
    throw 'BLOCKED: ExecutionRoot must be outside RepositoryRoot.'
}

Write-Output "ExecutionRoot (preserved): $execution"
$executionParent = [IO.Path]::GetDirectoryName($execution)
if ([string]::IsNullOrEmpty($executionParent) -or -not [IO.Directory]::Exists($executionParent)) {
    throw 'BLOCKED: ExecutionRoot parent directory must already exist.'
}

$clone = Invoke-Git -WorkingDirectory $executionParent -Arguments @(
    'clone', '--no-hardlinks', '--no-checkout', '--', $repository, $execution
)
$null = Invoke-Git -WorkingDirectory $execution -Arguments @('checkout', '--detach', $sourceHead)

$isolatedHead = (Invoke-Git -WorkingDirectory $execution -Arguments @('rev-parse', '--verify', 'HEAD')).Stdout.Trim()
if ($isolatedHead -cne $sourceHead) { throw 'INVALID: isolated checkout HEAD differs from source HEAD.' }
$symbolic = Invoke-Git -WorkingDirectory $execution -Arguments @('symbolic-ref', '-q', 'HEAD') -AllowFailure
if ($symbolic.ExitCode -eq 0) { throw 'INVALID: isolated checkout is not detached.' }
if ($symbolic.ExitCode -ne 1) { throw 'INVALID: detached-HEAD state could not be determined.' }
$isolatedUnstaged = Invoke-Git -WorkingDirectory $execution -Arguments @('diff', '--quiet', 'HEAD', '--') -AllowFailure
$isolatedStaged = Invoke-Git -WorkingDirectory $execution -Arguments @('diff', '--cached', '--quiet') -AllowFailure
if ($isolatedUnstaged.ExitCode -ne 0 -or $isolatedStaged.ExitCode -ne 0) {
    throw 'INVALID: isolated checkout is not tracked-clean before binding copy.'
}
$preCopyStatus = (Invoke-Git -WorkingDirectory $execution -Arguments @(
    'status', '--porcelain=v1', '--untracked-files=all', '--ignored'
)).Stdout
if (-not [string]::IsNullOrWhiteSpace($preCopyStatus)) {
    throw 'INVALID: isolated checkout contains untracked, ignored, or build-output entries before binding copy.'
}

$bindingDestination = Join-Path $execution $BindingRelativePath
[IO.File]::WriteAllBytes($bindingDestination, $bindingBytes)
$postCopyStatus = (Invoke-Git -WorkingDirectory $execution -Arguments @(
    'status', '--porcelain=v1', '--untracked-files=all', '--ignored'
)).Stdout.Trim()
$expectedStatus = "?? $BindingRelativePath"
if ($postCopyStatus -cne $expectedStatus) {
    throw "INVALID: isolated checkout must contain exactly one untracked canonical binding; observed '$postCopyStatus'."
}
$bindingDestinationSha256 = Get-Sha256 -Bytes ([IO.File]::ReadAllBytes($bindingDestination))
if ($bindingDestinationSha256 -cne $bindingSourceSha256) {
    throw 'INVALID: copied publication binding bytes differ from source binding bytes.'
}

$dotnet = [Diagnostics.ProcessStartInfo]::new('dotnet')
$dotnet.WorkingDirectory = $execution
$dotnet.UseShellExecute = $false
foreach ($argument in @(
    'run', '--project', $RunnerProject, '-c', 'Release', '--',
    '--repo-root', $execution, '--corpus-root', $CorpusRoot
)) { $dotnet.ArgumentList.Add($argument) }
$runner = [Diagnostics.Process]::Start($dotnet)
if ($null -eq $runner) { throw 'Unable to start the official corrective runner.' }
$runner.WaitForExit()
$runnerExitCode = $runner.ExitCode
$runner.Dispose()
exit $runnerExitCode
