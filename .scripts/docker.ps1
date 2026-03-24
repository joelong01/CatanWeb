<#
.SYNOPSIS
    Docker dependency check for CosmosDB emulator.
.PARAMETER Doctor
    Verifies Docker installation status
.PARAMETER HashTable
    Returns Doctor results as a PowerShell hashtable
.PARAMETER Json
    Returns Doctor results as JSON
#>

param(
    [switch]$Install,
    [switch]$Clean,
    [switch]$Doctor,
    [switch]$HashTable,
    [switch]$Json,
    [switch]$Yes,
    [switch]$Force,
    [ValidateSet("ERROR", "WARN", "INFO", "DEBUG")]
    [string]$TraceLevel = "INFO",
    [switch]$Help
)

$scriptPath = Split-Path -Parent $MyInvocation.MyCommand.Path
Import-Module "$scriptPath\utility-scripts.psm1" -Force

function Doctor-Docker {
    param([ValidateSet("ERROR", "WARN", "INFO", "DEBUG")][string]$TraceLevel = "ERROR")

    $result = @{
        Name = "docker"
        Status = "NotInstalled"
        Version = $null
        Message = ""
    }

    try {
        $output = & docker --version 2>&1
        if ($LASTEXITCODE -eq 0 -and $output -match '(\d+\.\d+\.\d+)') {
            $result.Version = $matches[1]
            $result.Status = "Installed"
            $result.Message = "Docker $($result.Version) installed"
        }
    } catch {
        $result.Message = "Docker not found"
    }

    return $result
}

function Show-DoctorOutput {
    param([hashtable]$Result)
    $color = if ($Result.Status -eq "Installed") { "Green" } else { "Yellow" }
    Write-Host ""
    Write-Host "Docker Status" -ForegroundColor Cyan
    Write-Host "=============" -ForegroundColor Cyan
    Write-Host "Status:  " -NoNewline
    Write-Host $Result.Status -ForegroundColor $color
    if ($Result.Version) { Write-Host "Version: $($Result.Version)" }
    Write-Host $Result.Message -ForegroundColor $color
    Write-Host ""
}

$doctorResult = Doctor-Docker -TraceLevel $TraceLevel

if ($Doctor) {
    if ($HashTable) { return $doctorResult }
    elseif ($Json) { return ($doctorResult | ConvertTo-Json -Depth 5) }
    else {
        Show-DoctorOutput -Result $doctorResult
        exit $(if ($doctorResult.Status -eq "Installed") { 0 } else { 1 })
    }
}

if ($Install) {
    Write-Host "Docker must be installed manually from https://www.docker.com/products/docker-desktop/" -ForegroundColor Yellow
    exit $(if ($doctorResult.Status -eq "Installed") { 0 } else { 1 })
}

if ($Help -or (-not $Install -and -not $Clean -and -not $Doctor)) {
    Write-Host "Docker dependency check for CosmosDB emulator."
    Write-Host "Usage: docker.ps1 -Doctor | -Install"
    exit 0
}
