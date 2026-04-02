<#
.SYNOPSIS
    Finds the closest Azure region to Seattle that supports B1 App Service Plans.
.DESCRIPTION
    Probes regions in order of proximity to Seattle by attempting to create a B1
    App Service Plan. Cleans up after itself. Outputs the best region found.
.PARAMETER BaseName
    Base name for the project (default: from .azure/catan-azure.json)
.PARAMETER Apply
    If set, updates .azure/catan-azure.json with the found region
.EXAMPLE
    ./.scripts/find-region.ps1
.EXAMPLE
    ./.scripts/find-region.ps1 -Apply
#>

param(
    [string]$BaseName,
    [switch]$Apply,
    [ValidateSet("ERROR", "WARN", "INFO", "DEBUG")]
    [string]$TraceLevel = "INFO",
    [switch]$Help
)

$ErrorActionPreference = "Stop"
$ScriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$ProjectRoot = Split-Path -Parent $ScriptDir
Import-Module "$ScriptDir\utility-scripts.psm1" -Force
Set-ModuleTraceLevel -TraceLevel $TraceLevel
$PSDefaultParameterValues = @{ 'Write-Log:TraceLevel' = $TraceLevel }

if ($Help) {
    Write-Host @"

Find Azure Region
=================

Probes Azure regions near Seattle for B1 App Service Plan availability.
Creates a temp resource group + plan, checks if it works, cleans up.

Usage: find-region.ps1 [-Apply] [-BaseName <name>]

Options:
  -Apply       Update .azure/catan-azure.json with the found region
  -BaseName    Override base name (default: from config)
  -TraceLevel  Output verbosity

"@
    exit 0
}

# Regions ordered by proximity to Seattle
$regions = @(
    "westus2"          # Washington
    "westus"           # California
    "westus3"          # Arizona
    "westcentralus"    # Wyoming
    "northcentralus"   # Illinois
    "centralus"        # Iowa
    "southcentralus"   # Texas
    "eastus"           # Virginia
    "eastus2"          # Virginia
    "canadacentral"    # Toronto
)

$testRg = "rg-region-probe-$(Get-Random -Maximum 9999)"
$testPlan = "asp-region-probe"

Write-Log -Level INFO -Message "" -NoLabel
Write-Log -Level INFO -Message "Finding closest Azure region with B1 quota..." -NoLabel -ForegroundColor Cyan
Write-Log -Level INFO -Message "" -NoLabel

$foundRegion = $null

foreach ($region in $regions) {
    Write-Host -NoNewline "  $($region.PadRight(20)) "

    # Create temp resource group
    $rg = Invoke-AzCommand "group create --name $testRg --location $region --output none" -Check -SuppressOutput
    if (-not $rg) {
        Write-Host "SKIP (can't create RG)" -ForegroundColor Gray
        continue
    }

    # Try B1 plan
    $result = Invoke-AzCommand "appservice plan create --name $testPlan --resource-group $testRg --location $region --sku B1 --is-linux --number-of-workers 1 --output none" -Check -SuppressOutput

    # Clean up (async — don't wait)
    Invoke-AzCommand "group delete --name $testRg --yes --no-wait --output none" -Check -SuppressOutput | Out-Null

    if ($result) {
        Write-Host "B1 available" -ForegroundColor Green
        $foundRegion = $region
        break
    }
    else {
        Write-Host "no quota" -ForegroundColor Yellow
    }
}

Write-Log -Level INFO -Message "" -NoLabel

if (-not $foundRegion) {
    Write-Log -Level ERROR -Message "No region found with B1 quota." -NoLabel
    Write-Log -Level INFO -Message "Request a quota increase at: https://portal.azure.com/#view/Microsoft_Azure_Capacity/QuotaMenuBlade" -NoLabel
    Write-Log -Level INFO -Message "Or run ./catan.ps1 install -Azure and it will fall back to F1 (Free) tier." -NoLabel
    exit 1
}

Write-Log -Level INFO -Message "Best region: $foundRegion" -NoLabel -ForegroundColor Green
Write-Log -Level INFO -Message "" -NoLabel

if ($Apply) {
    $configFile = Join-Path $ProjectRoot ".azure/catan-azure.json"
    $config = if (Test-Path $configFile) {
        Get-Content $configFile -Raw | ConvertFrom-Json -AsHashtable
    } else {
        @{}
    }
    $config.location = $foundRegion
    if (-not $config.baseName -and $BaseName) { $config.baseName = $BaseName }

    $configDir = Join-Path $ProjectRoot ".azure"
    if (-not (Test-Path $configDir)) { New-Item -ItemType Directory -Path $configDir -Force | Out-Null }
    $config | ConvertTo-Json -Depth 5 | Set-Content $configFile
    Write-Log -Level INFO -Message "Updated .azure/catan-azure.json with location: $foundRegion" -NoLabel -ForegroundColor Green
}
else {
    Write-Log -Level INFO -Message "To apply: ./.scripts/find-region.ps1 -Apply" -NoLabel
    Write-Log -Level INFO -Message "Or manually set location in .azure/catan-azure.json" -NoLabel
}
