$ErrorActionPreference = 'Stop'
$labRoot = Split-Path -Parent $PSScriptRoot
$url = 'http://127.0.0.1:5178/'

function Test-LabServer {
    try {
        $response = Invoke-WebRequest -UseBasicParsing -Uri $url -TimeoutSec 1
        return $response.StatusCode -eq 200
    }
    catch {
        return $false
    }
}

if (-not (Test-LabServer)) {
    $project = Join-Path $labRoot 'src\ManiaAddNotesLab.Web\ManiaAddNotesLab.Web.csproj'
    Start-Process -FilePath 'dotnet' -ArgumentList @('run', '--project', $project, '-c', 'Release') `
        -WorkingDirectory $labRoot -WindowStyle Hidden

    $ready = $false
    for ($attempt = 0; $attempt -lt 80; $attempt++) {
        Start-Sleep -Milliseconds 250
        if (Test-LabServer) {
            $ready = $true
            break
        }
    }
    if (-not $ready) {
        throw 'No se pudo iniciar ManiaAddNotesLab en el puerto 5178.'
    }
}

Start-Process $url
