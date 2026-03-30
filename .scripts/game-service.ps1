<#
.SYNOPSIS
    GameService management — local dev server and Azure App Service.
.DESCRIPTION
    Manages the GameService (.NET API + SignalR) for local and Azure environments.

    Verbs:
        doctor   Check health (local port or Azure health endpoint)
        install  Create Azure App Service, enable managed identity, configure Always On
        deploy   Build and push code to Azure (supports -Slot staging)
        clean    Delete Azure App Service
        help     Show usage
.EXAMPLE
    ./game-service.ps1 doctor
.EXAMPLE
    ./game-service.ps1 doctor -Azure
.EXAMPLE
    ./game-service.ps1 install -Azure
.EXAMPLE
    ./game-service.ps1 deploy -Azure -Force
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
    [string]$Slot,
    [switch]$Perf,
    [switch]$Help
)

$ErrorActionPreference = "Stop"
$ScriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$ProjectRoot = Split-Path -Parent $ScriptDir
Import-Module "$ScriptDir\utility-scripts.psm1" -Force
Set-ModuleTraceLevel -TraceLevel $TraceLevel
$PSDefaultParameterValues = @{ 'Write-Log:TraceLevel' = $TraceLevel }

$GameServicePort = 8080

if ($Help) { $Verb = "help" }
if ($Staging) { $Slot = "staging" }

# ─── Helpers ─────────────────────────────────────────────────────────────────

function Get-Az {
    # Lazy-load Azure resource names (only needed for -Azure operations)
    if (-not $script:_az) {
        $script:_az = Get-AzureResourceNames -ProjectRoot $ProjectRoot
    }
    return $script:_az
}

# ─── Doctor ──────────────────────────────────────────────────────────────────

function Doctor-Local {
    $result = @{
        Name    = "game-service"
        Target  = "Local"
        Status  = "NotInstalled"
        Version = $null
        Message = ""
        Details = @{}
    }

    $listening = Test-NetConnection -ComputerName localhost -Port $GameServicePort -WarningAction SilentlyContinue -ErrorAction SilentlyContinue
    if ($listening -and $listening.TcpTestSucceeded) {
        try {
            $health = Invoke-RestMethod -Uri "http://localhost:$GameServicePort/health" -TimeoutSec 5
            $result.Status = "Installed"
            $result.Version = $health.version.commit
            $result.Message = "GameService running on port $GameServicePort"
            $result.Details = @{ status = $health.status; database = $health.database.provider }
        }
        catch {
            $result.Status = "NeedsFix"
            $result.Message = "Port $GameServicePort is in use but /health failed: $_"
        }
    }
    else {
        $result.Message = "GameService not running on port $GameServicePort. Run: ./catan.ps1 run"
    }

    return $result
}

# ─── Main ────────────────────────────────────────────────────────────────────

switch ($Verb) {
    "doctor" {
        if ($Azure) {
            # Delegate to catan-azure.ps1 for now (until full migration)
            $azureScript = Join-Path $ScriptDir "catan-azure.ps1"
            & $azureScript game-service doctor -TraceLevel $TraceLevel -Staging:$Staging -Json:$Json -HashTable:$HashTable -Perf:$Perf
            exit $LASTEXITCODE
        }

        $result = Doctor-Local
        if ($HashTable) { return $result }
        if ($Json) { return ($result | ConvertTo-Json -Depth 5) }

        # Human-readable
        $color = if ($result.Status -eq "Installed") { "Green" } elseif ($result.Status -eq "NeedsFix") { "Yellow" } else { "Red" }
        Write-Log -Level INFO -Message "" -NoLabel
        Write-Log -Level INFO -Message "GameService ($($result.Target))" -NoLabel -ForegroundColor Cyan
        Write-Log -Level INFO -Message ("=" * 25) -NoLabel -ForegroundColor Cyan
        Write-Log -Level INFO -Message "Status:  $($result.Status)" -NoLabel -ForegroundColor $color
        if ($result.Version) { Write-Log -Level INFO -Message "Version: $($result.Version)" -NoLabel }
        Write-Log -Level INFO -Message $result.Message -NoLabel -ForegroundColor $color
        Write-Log -Level INFO -Message "" -NoLabel
        exit $(if ($result.Status -eq "Installed") { 0 } else { 1 })
    }

    "install" {
        if (-not $Azure) {
            Write-Log -Level INFO -Message "GameService install is only needed for Azure. Locally, use './catan.ps1 run'." -NoLabel
            exit 0
        }
        $azureScript = Join-Path $ScriptDir "catan-azure.ps1"
        & $azureScript game-service install -TraceLevel $TraceLevel
        exit $LASTEXITCODE
    }

    "deploy" {
        if (-not $Azure) {
            Write-Log -Level ERROR -Message "deploy requires -Azure. Locally, use './catan.ps1 run'." -NoLabel
            exit 1
        }
        $azureScript = Join-Path $ScriptDir "catan-azure.ps1"
        $slotArg = if ($Slot) { @("-Slot", $Slot) } else { @() }
        & $azureScript game-service deploy -TraceLevel $TraceLevel -Force:$Force -NoBuild:$NoBuild @slotArg
        exit $LASTEXITCODE
    }

    "clean" {
        if (-not $Azure) {
            Write-Log -Level INFO -Message "Nothing to clean locally for GameService." -NoLabel
            exit 0
        }
        $azureScript = Join-Path $ScriptDir "catan-azure.ps1"
        & $azureScript game-service clean -TraceLevel $TraceLevel -Yes:$Yes
        exit $LASTEXITCODE
    }

    "help" {
        $name = Split-Path -Leaf $MyInvocation.MyCommand.Path
        Write-Host @"

GameService Management
======================

Usage: $name <verb> [options]

Verbs:
  doctor     Check health (local port $GameServicePort or Azure endpoint)
  install    Create Azure App Service (-Azure required)
  deploy     Build and push code to Azure (-Azure required, supports -Slot)
  clean      Delete Azure App Service (-Azure required)
  help       Show this message

Options:
  -Azure         Target Azure (default: local)
  -Staging       Shortcut for -Slot staging
  -Slot <name>   Target a specific deployment slot
  -Force         Redeploy even if commit matches
  -NoBuild       Skip dotnet publish (use existing build)
  -Yes           Skip confirmation prompts
  -HashTable     Return doctor result as hashtable
  -Json          Return doctor result as JSON
  -Perf          Run performance test (doctor only)
  -TraceLevel    Output: ERROR, WARN, INFO (default), DEBUG

Examples:
  $name doctor                    # Check local port 8080
  $name doctor -Azure             # Check Azure production
  $name doctor -Azure -Staging    # Check Azure staging slot
  $name install -Azure            # Create App Service
  $name deploy -Azure -Force      # Deploy to production
  $name deploy -Azure -Staging    # Deploy to staging slot

"@
        exit 0
    }
}
