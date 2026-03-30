<#
.SYNOPSIS
    React UI management — local dev server and Azure App Service.
.DESCRIPTION
    Manages the React/Next.js UI for local and Azure environments.

    Verbs:
        doctor   Check health (local port or Azure site)
        install  Create Azure App Service with staging slot
        deploy   Build and push React app to Azure (supports -Staging)
        clean    Delete Azure App Service
        help     Show usage
.EXAMPLE
    ./ui.ps1 doctor
.EXAMPLE
    ./ui.ps1 deploy -Azure -Staging
#>

param(
    [Parameter(Position = 0)]
    [ValidateSet("doctor", "install", "deploy", "clean", "help")]
    [string]$Verb = "help",

    [ValidateSet("ERROR", "WARN", "INFO", "DEBUG")]
    [Alias("LogLevel")]
    [string]$TraceLevel = "INFO",

    [switch]$Azure,
    [switch]$Staging,
    [switch]$Yes,
    [switch]$Force,
    [switch]$NoBuild,
    [switch]$HashTable,
    [switch]$Json,
    [string]$GameServiceUrl,
    [switch]$Help
)

$ErrorActionPreference = "Stop"
$ScriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$ProjectRoot = Split-Path -Parent $ScriptDir
Import-Module "$ScriptDir\utility-scripts.psm1" -Force
Set-ModuleTraceLevel -TraceLevel $TraceLevel
$PSDefaultParameterValues = @{ 'Write-Log:TraceLevel' = $TraceLevel }

$ReactUIPort = 3000

if ($Help) { $Verb = "help" }

# ─── Doctor ──────────────────────────────────────────────────────────────────

function Doctor-Local {
    $result = @{
        Name    = "ui"
        Target  = "Local"
        Status  = "NotInstalled"
        Version = $null
        Message = ""
        Details = @{}
    }

    try {
        $response = Invoke-WebRequest -Uri "http://localhost:$ReactUIPort" -TimeoutSec 5 -UseBasicParsing -ErrorAction Stop
        $result.Status = "Installed"
        $result.Message = "React UI running on port $ReactUIPort"
    }
    catch {
        $result.Message = "React UI not running on port $ReactUIPort. Run: ./catan.ps1 run"
    }

    return $result
}

# ─── Main ────────────────────────────────────────────────────────────────────

switch ($Verb) {
    "doctor" {
        if ($Azure) {
            $azureScript = Join-Path $ScriptDir "catan-azure.ps1"
            & $azureScript ui doctor -TraceLevel $TraceLevel -Staging:$Staging -Json:$Json -HashTable:$HashTable
            exit $LASTEXITCODE
        }

        $result = Doctor-Local
        if ($HashTable) { return $result }
        if ($Json) { return ($result | ConvertTo-Json -Depth 5) }

        $color = if ($result.Status -eq "Installed") { "Green" } else { "Red" }
        Write-Log -Level INFO -Message "" -NoLabel
        Write-Log -Level INFO -Message "React UI ($($result.Target))" -NoLabel -ForegroundColor Cyan
        Write-Log -Level INFO -Message ("=" * 20) -NoLabel -ForegroundColor Cyan
        Write-Log -Level INFO -Message "Status:  $($result.Status)" -NoLabel -ForegroundColor $color
        Write-Log -Level INFO -Message $result.Message -NoLabel -ForegroundColor $color
        Write-Log -Level INFO -Message "" -NoLabel
        exit $(if ($result.Status -eq "Installed") { 0 } else { 1 })
    }

    "install" {
        if (-not $Azure) {
            Write-Log -Level INFO -Message "UI install is only needed for Azure. Locally, use './catan.ps1 run'." -NoLabel
            exit 0
        }
        $azureScript = Join-Path $ScriptDir "catan-azure.ps1"
        & $azureScript ui install -TraceLevel $TraceLevel
        exit $LASTEXITCODE
    }

    "deploy" {
        if (-not $Azure) {
            Write-Log -Level ERROR -Message "deploy requires -Azure. Locally, use './catan.ps1 run'." -NoLabel
            exit 1
        }
        $azureScript = Join-Path $ScriptDir "catan-azure.ps1"
        if ($Staging) {
            $gsUrlArgs = if ($GameServiceUrl) { @("-GameServiceUrl", $GameServiceUrl) } else { @() }
            & $azureScript ui deploy-staging -TraceLevel $TraceLevel -Force:$Force @gsUrlArgs
        }
        else {
            & $azureScript ui deploy -TraceLevel $TraceLevel -Force:$Force -NoBuild:$NoBuild
        }
        exit $LASTEXITCODE
    }

    "clean" {
        if (-not $Azure) {
            Write-Log -Level INFO -Message "Nothing to clean locally for UI." -NoLabel
            exit 0
        }
        $azureScript = Join-Path $ScriptDir "catan-azure.ps1"
        & $azureScript ui clean -TraceLevel $TraceLevel -Yes:$Yes
        exit $LASTEXITCODE
    }

    "help" {
        $name = Split-Path -Leaf $MyInvocation.MyCommand.Path
        Write-Host @"

React UI Management
===================

Usage: $name <verb> [options]

Verbs:
  doctor     Check health (local port $ReactUIPort or Azure site)
  install    Create Azure App Service (-Azure required)
  deploy     Build and push React app to Azure (-Azure required)
  clean      Delete Azure App Service (-Azure required)
  help       Show this message

Options:
  -Azure              Target Azure (default: local)
  -Staging            Deploy to staging slot
  -Force              Redeploy even if commit matches
  -NoBuild            Skip build (production deploy only)
  -GameServiceUrl     Override GameService URL for staging builds
  -Yes                Skip confirmation prompts
  -HashTable          Return doctor result as hashtable
  -Json               Return doctor result as JSON
  -TraceLevel         Output: ERROR, WARN, INFO (default), DEBUG

Examples:
  $name doctor                    # Check local port 3000
  $name doctor -Azure             # Check Azure production
  $name doctor -Azure -Staging    # Check Azure staging
  $name deploy -Azure             # Deploy to production
  $name deploy -Azure -Staging    # Deploy to staging slot
  $name install -Azure            # Create App Service

"@
        exit 0
    }
}
