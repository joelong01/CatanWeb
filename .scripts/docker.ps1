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

$PSDefaultParameterValues = @{ 'Write-Log:TraceLevel' = $TraceLevel }

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
    Write-Log -Level INFO -Message "" -NoLabel
    Write-Log -Level INFO -Message "Docker Status" -NoLabel -ForegroundColor Cyan
    Write-Log -Level INFO -Message "=============" -NoLabel -ForegroundColor Cyan
    Write-Log -Level INFO -Message "Status:  " -NoLabel -NoNewline
    Write-Log -Level INFO -Message $Result.Status -ForegroundColor $color -NoLabel
    if ($Result.Version) { Write-Log -Level INFO -Message "Version: $($Result.Version)" -NoLabel }
    Write-Log -Level INFO -Message $Result.Message -ForegroundColor $color -NoLabel
    Write-Log -Level INFO -Message "" -NoLabel
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
    Write-Log -Level WARN -Message "Docker must be installed manually from https://www.docker.com/products/docker-desktop/"
    exit $(if ($doctorResult.Status -eq "Installed") { 0 } else { 1 })
}

if ($Help -or (-not $Install -and -not $Clean -and -not $Doctor)) {
    Write-Log -Level INFO -Message "Docker dependency check for CosmosDB emulator." -NoLabel
    Write-Log -Level INFO -Message "Usage: docker.ps1 -Doctor | -Install" -NoLabel
    exit 0
}
