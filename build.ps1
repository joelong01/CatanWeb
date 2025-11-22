#!/usr/bin/env pwsh
# Thin wrapper that executes build_worker.ps1 and tees output to a timestamped log,
# preserving the worker's exit code.

# Create BuildLogs directory if it doesn't exist
$LogDir = Join-Path $PSScriptRoot "BuildLogs"
if (-not (Test-Path $LogDir)) {
    New-Item -ItemType Directory -Path $LogDir -Force | Out-Null
}

# Create timestamped log file
$timestamp = Get-Date -Format "yyyy-MM-dd_HH-mm-ss"
$LogFile = Join-Path $LogDir "build_$timestamp.log"

Write-Host "Build log: $LogFile" -ForegroundColor Cyan

# Join arguments as typed to forward to worker
$argLine = ($args | ForEach-Object {
    if ($_ -is [string]) {
        $s = $_
        if ($s -match '"') { $s = $s -replace '"', '\"' }
        if ($s -match '\s|"') { '"' + $s + '"' } else { $s }
    } else { $_.ToString() }
}) -join ' '

$pwsh = (Get-Command pwsh).Source

# Ensure child knows it's under tee (disables transcript inside worker)
$prev = $env:BUILD_TEE
$env:BUILD_TEE = '1'

& $pwsh -NoLogo -NoProfile -ExecutionPolicy Bypass -File "$PSScriptRoot/build_worker.ps1" @args 2>&1 |
    Tee-Object -FilePath $LogFile

$code = $LASTEXITCODE
$env:BUILD_TEE = $prev
exit $code
