<#
.SYNOPSIS
    Unified CI/CD orchestrator for Catan Azure deployments.
    Ensures infrastructure, database access, and app deployment happen in the correct order.

.DESCRIPTION
    Solves the circular dependency in deploy pipelines: apps need database access to be healthy,
    but the old pipeline granted access AFTER deploying. This script runs infrastructure first.

    Order of operations:
    1. Infrastructure: firewall, app settings, RBAC (so apps CAN connect to CosmosDB)
    2. Build & deploy: GameService and/or React UI
    3. Seed: ensure default data exists
    4. Verify: health checks (now the app has database access)

    All steps are idempotent — safe to re-run.

.PARAMETER Action
    What to deploy: "all", "infrastructure", "gameservice", "react", "verify"

.PARAMETER Slot
    Deployment slot: "production" (default) or "staging"

.PARAMETER TraceLevel
    Logging verbosity: "INFO" (default) or "DEBUG"

.EXAMPLE
    # Full staging deploy (CI/CD pipeline)
    pwsh .scripts/catan-cicd.ps1 all -Slot staging

    # Just fix infrastructure (firewall, RBAC, app settings)
    pwsh .scripts/catan-cicd.ps1 infrastructure -Slot staging

    # Deploy GameService only (infrastructure runs first automatically)
    pwsh .scripts/catan-cicd.ps1 gameservice -Slot staging
#>
param(
    [Parameter(Position = 0)]
    [ValidateSet("all", "infrastructure", "gameservice", "react", "verify", "help")]
    [string]$Action = "help",

    [ValidateSet("production", "staging")]
    [string]$Slot = "production",

    [ValidateSet("INFO", "DEBUG")]
    [string]$TraceLevel = "INFO",

    [switch]$Force
)

$ErrorActionPreference = "Stop"
$scriptDir = $PSScriptRoot
$repoRoot  = Split-Path $scriptDir

# ─── Helpers ────────────────────────────────────────────────────────────────

function Write-Step {
    param([int]$Number, [string]$Title)
    Write-Host ""
    Write-Host "--- Step $Number`: $Title ---" -ForegroundColor Cyan
}

function Write-Ok   { param([string]$Msg) Write-Host "  [OK] $Msg" -ForegroundColor Green }
function Write-Fail { param([string]$Msg) Write-Host "  [FAIL] $Msg" -ForegroundColor Red }
function Write-Info { param([string]$Msg) Write-Host "  [..] $Msg" -ForegroundColor Gray }

function Invoke-Step {
    param([scriptblock]$Block, [string]$Label)
    try {
        & $Block
        if ($LASTEXITCODE -and $LASTEXITCODE -ne 0) { throw "Exit code $LASTEXITCODE" }
        Write-Ok $Label
        return $true
    } catch {
        Write-Fail "$Label — $_"
        return $false
    }
}

# ─── Load config ────────────────────────────────────────────────────────────

$configPath = Join-Path $repoRoot ".azure/catan-azure.json"
if (-not (Test-Path $configPath)) {
    Write-Fail "Config not found: $configPath"
    exit 1
}
$config = Get-Content $configPath -Raw | ConvertFrom-Json
$appName     = $config.gameService.appName
$rg          = $config.resourceGroup
$cosmosAcct  = $config.cosmosDb.accountName
$cosmosDb    = $config.cosmosDb.databaseName
$cosmosEp    = $config.cosmosDb.endpoint
$dbScript    = Join-Path $scriptDir "database.ps1"

$slotArgs = if ($Slot -eq "staging") { @("--slot", "staging") } else { @() }
$slotLabel = if ($Slot -eq "staging") { " (staging)" } else { "" }

# ─── Actions ────────────────────────────────────────────────────────────────

function Invoke-Infrastructure {
    Write-Step 1 "CosmosDB Firewall"
    Invoke-Step -Label "Firewall open (publicNetworkAccess=Enabled, no IP restrictions)" -Block {
        & pwsh $dbScript deploy -Azure -TraceLevel $TraceLevel 2>&1 | Out-Null
        # Verify firewall specifically
        $acctJson = az cosmosdb show --name $cosmosAcct --resource-group $rg `
            --query "{pna:publicNetworkAccess, rules:length(ipRules)}" --output json 2>$null
        $acct = $acctJson | ConvertFrom-Json
        if ($acct.pna -ne "Enabled" -or $acct.rules -ne 0) {
            # Force fix firewall
            az cosmosdb update --name $cosmosAcct --resource-group $rg `
                --public-network-access Enabled --ip-range-filter "" --output none 2>$null
        }
    }

    Write-Step 2 "App Settings$slotLabel"
    Invoke-Step -Label "COSMOS_ENDPOINT and COSMOS_DATABASE set" -Block {
        az webapp config appsettings set `
            --name $appName --resource-group $rg @slotArgs `
            --settings "COSMOS_ENDPOINT=$cosmosEp" "COSMOS_DATABASE=$cosmosDb" `
            --output none 2>$null
    }

    Write-Step 3 "Managed Identity & RBAC$slotLabel"
    # Get or assign managed identity for the target slot
    $principalId = (az webapp identity show --name $appName --resource-group $rg @slotArgs `
        --query principalId --output tsv 2>$null)
    if ([string]::IsNullOrWhiteSpace($principalId)) {
        Write-Info "Assigning managed identity..."
        az webapp identity assign --name $appName --resource-group $rg @slotArgs --output none 2>$null
        $principalId = (az webapp identity show --name $appName --resource-group $rg @slotArgs `
            --query principalId --output tsv 2>$null)
    }

    if (-not [string]::IsNullOrWhiteSpace($principalId)) {
        $principalId = $principalId.Trim()
        Invoke-Step -Label "RBAC for $Slot identity ($principalId)" -Block {
            & pwsh $dbScript deploy -Azure -TraceLevel $TraceLevel
        }
    } else {
        Write-Fail "Could not get managed identity for $Slot"
    }
}

function Invoke-DeployGameService {
    Write-Step 4 "Deploy GameService$slotLabel"
    $deployScript = Join-Path $scriptDir "catan-azure.ps1"
    if ($Slot -eq "staging") {
        Invoke-Step -Label "GameService deployed to staging slot" -Block {
            & pwsh $deployScript game-service deploy -Slot staging -Force -TraceLevel $TraceLevel
        }
    } else {
        Invoke-Step -Label "GameService deployed to production" -Block {
            & pwsh $deployScript game-service deploy -Force -TraceLevel $TraceLevel
        }
    }
}

function Invoke-DeployReact {
    Write-Step 5 "Deploy React UI$slotLabel"
    $deployScript = Join-Path $scriptDir "catan-azure.ps1"
    if ($Slot -eq "staging") {
        Invoke-Step -Label "React UI deployed to staging" -Block {
            & pwsh $deployScript ui deploy-staging -Force -TraceLevel $TraceLevel
        }
    } else {
        Invoke-Step -Label "React UI deployed (blue-green swap)" -Block {
            & pwsh $deployScript ui deploy-staging -Force -TraceLevel $TraceLevel `
                -GameServiceUrl $config.gameService.url
        }
    }
}

function Invoke-Verify {
    Write-Step 6 "Verify$slotLabel"
    $apiBase = if ($Slot -eq "staging") {
        "https://catan-api-staging.azurewebsites.net"
    } else {
        $config.gameService.url
    }

    $uiBase = if ($Slot -eq "staging") {
        "https://catan-staging.azurewebsites.net"
    } else {
        $config.ui.url
    }

    # Health check with retry
    $healthy = $false
    for ($i = 1; $i -le 30; $i++) {
        try {
            $resp = Invoke-RestMethod -Uri "$apiBase/health" -TimeoutSec 10 -ErrorAction Stop
            $status = $resp.status
            if ($status -eq "healthy") {
                Write-Ok "GameService healthy after $($i * 10)s"
                $healthy = $true
                break
            }
            Write-Info "GameService status: $status ($($i * 10)s)..."
        } catch {
            Write-Info "GameService not responding ($($i * 10)s)..."
        }
        Start-Sleep -Seconds 10
    }
    if (-not $healthy) {
        Write-Fail "GameService did not become healthy within 5 minutes"
        # Show last health response for debugging
        try {
            $resp = Invoke-RestMethod -Uri "$apiBase/health" -TimeoutSec 10 -ErrorAction Stop
            Write-Info "Last health response: $($resp | ConvertTo-Json -Compress)"
        } catch {}
    }

    # React check
    try {
        $code = (Invoke-WebRequest -Uri $uiBase -TimeoutSec 10 -ErrorAction Stop).StatusCode
        if ($code -eq 200) { Write-Ok "React UI responding (HTTP $code)" }
        else { Write-Fail "React UI HTTP $code" }
    } catch {
        Write-Fail "React UI not responding"
    }
}

function Show-Help {
    Write-Host ""
    Write-Host "Catan CI/CD Orchestrator" -ForegroundColor Cyan
    Write-Host ""
    Write-Host "Usage: pwsh .scripts/catan-cicd.ps1 <action> [-Slot staging|production]"
    Write-Host ""
    Write-Host "Actions:"
    Write-Host "  all              Full deploy: infrastructure → gameservice → verify"
    Write-Host "  infrastructure   Firewall, app settings, RBAC only"
    Write-Host "  gameservice      Deploy GameService (runs infrastructure first)"
    Write-Host "  react            Deploy React UI"
    Write-Host "  verify           Health checks only"
    Write-Host "  help             Show this help"
    Write-Host ""
    Write-Host "Examples:"
    Write-Host "  pwsh .scripts/catan-cicd.ps1 all -Slot staging      # Full staging deploy"
    Write-Host "  pwsh .scripts/catan-cicd.ps1 infrastructure         # Fix prod infra"
    Write-Host "  pwsh .scripts/catan-cicd.ps1 verify -Slot staging   # Check staging health"
    Write-Host ""
}

# ─── Main ───────────────────────────────────────────────────────────────────

Write-Host ""
Write-Host "==========================================" -ForegroundColor Cyan
Write-Host "  Catan CI/CD Deploy -- $($Slot.ToUpper())" -ForegroundColor Cyan
Write-Host "==========================================" -ForegroundColor Cyan

switch ($Action) {
    "help" {
        Show-Help
    }
    "infrastructure" {
        Invoke-Infrastructure
    }
    "gameservice" {
        Invoke-Infrastructure
        Invoke-DeployGameService
        Invoke-Verify
    }
    "react" {
        Invoke-DeployReact
        Invoke-Verify
    }
    "verify" {
        Invoke-Verify
    }
    "all" {
        Invoke-Infrastructure
        Invoke-DeployGameService
        Invoke-DeployReact
        Invoke-Verify
    }
}

Write-Host ""
