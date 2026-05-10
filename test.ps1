# Ensures Docker Desktop is running, then executes the test suite.
# Testcontainers manages container lifecycle (pull, start, stop) automatically.

$ErrorActionPreference = "Stop"

$dockerDesktop = "$env:ProgramFiles\Docker\Docker\Docker Desktop.exe"

if (-not (Get-Process "Docker Desktop" -ErrorAction SilentlyContinue)) {
    if (-not (Test-Path $dockerDesktop)) {
        Write-Error "Docker Desktop not found at '$dockerDesktop'. Please install Docker Desktop."
        exit 1
    }
    Write-Host "Starting Docker Desktop..."
    Start-Process $dockerDesktop
}

Write-Host "Waiting for Docker daemon to be ready..."
$deadline = (Get-Date).AddSeconds(120)
$ready = $false
while ((Get-Date) -lt $deadline) {
    $result = docker info 2>$null
    if ($?) { $ready = $true; break }
    Start-Sleep 3
}

if (-not $ready) {
    Write-Error "Docker daemon did not become ready within 120 seconds."
    exit 1
}

Write-Host "Docker is ready. Running tests..."
dotnet test Orchestrator.slnx --logger "console;verbosity=normal" @args
exit $LASTEXITCODE
