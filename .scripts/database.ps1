<#
.SYNOPSIS
    CosmosDB database management for Catan3 — local emulator and Azure.
.DESCRIPTION
    Manages the CosmosDB database for both local development (Docker emulator)
    and Azure production (CosmosDB for NoSQL account).

    Local mode (default / -Local)
        install  Pull emulator image, start container, seed DB + containers
        seed     Create/verify Catan database and all required containers
        start    Start the emulator container (no image pull)
        stop     Stop the emulator container
        clean    Stop container and remove the emulator Docker image
        doctor   Report emulator health + verify all containers exist
        status   Alias for doctor
        test     Seed, run CatanDb contract tests, clean up params file
        write-test-params  Write .cosmos-test-params.json for dotnet test

    Azure mode (-Azure):
        install  Create CosmosDB account, database, and all containers
        deploy   Configure App Service: set COSMOS_ENDPOINT + grant RBAC
        seed     Create/verify database and containers on existing account
        clean    Delete the CosmosDB account (destructive)
        doctor   Check account, database, containers, App Service wiring
        status   Alias for doctor
        test     Run CatanDb contract tests against Azure endpoint
        write-test-params  Write .cosmos-test-params.json for Azure endpoint

    The emulator endpoint is: http://localhost:8081 (HTTP, no TLS, NoAuth mode).
    Azure endpoint:           https://<accountName>.documents.azure.com:443/

.PARAMETER Verb
    Operation to perform (see above)
.PARAMETER Local
    Target local CosmosDB Emulator (default when -Azure is not set)
.PARAMETER Azure
    Target Azure CosmosDB account
.PARAMETER Yes
    Auto-confirm destructive operations
.PARAMETER Force
    Force re-pull image / re-create resources
.PARAMETER Json
    Return doctor/status as JSON
.PARAMETER HashTable
    Return doctor/status as PowerShell hashtable
.PARAMETER TraceLevel
    ERROR | WARN | INFO | DEBUG (default: INFO)
.PARAMETER Help
    Show help

.EXAMPLE
    ./database.ps1 install
.EXAMPLE
    ./database.ps1 install -Azure
.EXAMPLE
    ./database.ps1 deploy -Azure
.EXAMPLE
    ./database.ps1 doctor -Azure -Json
.EXAMPLE
    ./database.ps1 test -Azure
#>

param(
    [Parameter(Position = 0)]
    [ValidateSet("install", "deploy", "seed", "seed-data", "start", "stop", "clean",
                 "doctor", "status", "test", "write-test-params",
                 "nuke-containers", "help")]
    [string]$Verb = "help",

    [Parameter()]
    [switch]$Local,

    [Parameter()]
    [switch]$Azure,

    [Parameter()]
    [switch]$Yes,

    [Parameter()]
    [switch]$Force,

    [Parameter()]
    [switch]$Json,

    [Parameter()]
    [switch]$HashTable,

    [Parameter()]
    [ValidateSet("ERROR", "WARN", "INFO", "DEBUG")]
    [string]$TraceLevel = "INFO",

    [Parameter()]
    [switch]$Help
)

$ErrorActionPreference = "Stop"

# ── Module imports ──────────────────────────────────────────────────────────────

$ScriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
Import-Module "$ScriptDir/utility-scripts.psm1" -Force

$PSDefaultParameterValues = @{
    'Write-Log:TraceLevel' = $TraceLevel
}

# ── Azure CLI wrapper ────────────────────────────────────────────────────────────

<#
.SYNOPSIS
    Wrapper around the az CLI that logs every invocation at DEBUG level,
    captures stdout and stderr separately, and returns stdout as a string.
    Usage: Call-Azure cosmosdb show --name foo ...
    The function restores $LASTEXITCODE so callers can check it normally.
#>
function Invoke-Azure {
    [CmdletBinding()]
    param(
        [Parameter(ValueFromRemainingArguments = $true)]
        [string[]]$Arguments
    )

    Write-Log -Level "DEBUG" -Message "az $($Arguments -join ' ')"

    $tmpErr = [System.IO.Path]::GetTempFileName()
    try {
        $stdout = & az @Arguments 2>$tmpErr
        $exitCode = $LASTEXITCODE
        $stderr = Get-Content $tmpErr -Raw -ErrorAction SilentlyContinue
        if (-not [string]::IsNullOrWhiteSpace($stderr)) {
            Write-Log -Level "DEBUG" -Message "az stderr: $($stderr.Trim())"
        }
        if (-not [string]::IsNullOrWhiteSpace($stdout)) {
            Write-Log -Level DEBUG "Success"
            # Write-Log -Level "DEBUG" -Message "az stdout: $($stdout -join "`n")"
        }
    } finally {
        Remove-Item $tmpErr -Force -ErrorAction SilentlyContinue
    }

    $global:LASTEXITCODE = $exitCode
    return $stdout
}

# ── Shared constants ────────────────────────────────────────────────────────────

$ProjectRoot    = Split-Path -Parent $ScriptDir
$DatabaseName   = "catan"
$Containers     = [ordered]@{
    "players"         = "/id"
    "games"           = "/id"
    "completed-games" = "/id"
    "templates"       = "/id"
    "recordings"      = "/id"
}

# Minimal indexing policies — only index fields used in WHERE clauses.
# Default (exclude /*) avoids indexing large blobs (profileJson, imageData, compressedData).
# Returns a hashtable; callers convert to JSON as needed.
function New-IndexPolicy {
    param([string[]]$Include = @())
    return [ordered]@{
        indexingMode  = "consistent"
        automatic     = $true
        includedPaths = @($Include | ForEach-Object { @{ path = $_ } })
        excludedPaths = @(@{ path = "/*" })
    }
}

$ContainerIndexPolicies = @{
    # players: only id lookups (partition key) + list-all — no secondary index
    "players"         = New-IndexPolicy

    # games: primary filter is gameState (active lobby); also by startedBy and savedAt for sorting
    "games"           = New-IndexPolicy -Include "/gameState/?", "/startedBy/?", "/savedAt/?"

    # completed-games: gameId for Open (cross-container lookup), completedAt for history sort,
    # winnerId for player stats ("games won by X")
    "completed-games" = New-IndexPolicy -Include "/gameId/?", "/completedAt/?", "/winnerId/?"

    # templates: few docs; category is the only meaningful filter
    "templates"       = New-IndexPolicy -Include "/category/?"

    # recordings: few docs; gameId is the only query (FindRecordingByGameIdAsync)
    "recordings"      = New-IndexPolicy -Include "/gameId/?"
}
$TestParamsFile = Join-Path $ProjectRoot "Tests/GameService/CatanDb/.cosmos-test-params.json"
$TestProject    = Join-Path $ProjectRoot "Tests/GameService"

# ── Cosmos .NET SDK helpers ────────────────────────────────────────────────────
# Uses the same DLLs the GameService uses — avoids REST API partition-key issues.

$script:CosmosSDKLoaded = $false

function Initialize-CosmosSDK {
    <#
    .SYNOPSIS
        Loads the Cosmos .NET SDK assemblies from the GameService build output.
        Idempotent — safe to call multiple times.
    #>
    if ($script:CosmosSDKLoaded) { return $true }

    $binDir = Join-Path $ProjectRoot "Catan3.GameService/bin/Debug/net9.0"
    $cosmosDll   = Join-Path $binDir "Microsoft.Azure.Cosmos.Client.dll"
    $identityDll = Join-Path $binDir "Azure.Identity.dll"
    $coreDll     = Join-Path $binDir "Azure.Core.dll"

    if (-not (Test-Path $cosmosDll)) {
        # Try Release if Debug not available
        $binDir = Join-Path $ProjectRoot "Catan3.GameService/bin/Release/net9.0"
        $cosmosDll   = Join-Path $binDir "Microsoft.Azure.Cosmos.Client.dll"
        $identityDll = Join-Path $binDir "Azure.Identity.dll"
        $coreDll     = Join-Path $binDir "Azure.Core.dll"
    }

    if (-not (Test-Path $cosmosDll)) {
        Write-Log -Level "ERROR" -Message "Cosmos SDK DLL not found. Run: pwsh ./catan.ps1 build"
        return $false
    }

    try {
        Add-Type -Path $cosmosDll   -ErrorAction Stop
        Add-Type -Path $identityDll -ErrorAction Stop
        Add-Type -Path $coreDll     -ErrorAction Stop
        $script:CosmosSDKLoaded = $true
        Write-Log -Level "DEBUG" -Message "Cosmos .NET SDK loaded from $binDir"
        return $true
    } catch {
        Write-Log -Level "ERROR" -Message "Failed to load Cosmos SDK: $_"
        return $false
    }
}

function New-CosmosClient {
    <#
    .SYNOPSIS
        Creates a CosmosClient. For Azure, uses DefaultAzureCredential (AAD).
        For local emulator, uses master key. Caller must Dispose().
    #>
    param([switch]$IsAzure, [string]$Endpoint, [string]$Key = "")

    if (-not (Initialize-CosmosSDK)) { return $null }

    if ($IsAzure) {
        $cred = [Azure.Identity.DefaultAzureCredential]::new()
        return [Microsoft.Azure.Cosmos.CosmosClient]::new($Endpoint, $cred)
    } else {
        # Local emulator requires Gateway mode (vnext-preview doesn't support Direct)
        $opts = [Microsoft.Azure.Cosmos.CosmosClientOptions]::new()
        $opts.ConnectionMode = [Microsoft.Azure.Cosmos.ConnectionMode]::Gateway
        return [Microsoft.Azure.Cosmos.CosmosClient]::new($Endpoint, $Key, $opts)
    }
}

function Invoke-CosmosUpsert {
    <#
    .SYNOPSIS
        Upserts a document (PSObject or hashtable) into a Cosmos container using the SDK.
        Returns $true on success.
    #>
    param(
        [Microsoft.Azure.Cosmos.Container]$Container,
        [object]$Document,
        [string]$PartitionKeyValue
    )

    $json   = $Document | ConvertTo-Json -Depth 20 -Compress
    $stream = [System.IO.MemoryStream]::new([System.Text.Encoding]::UTF8.GetBytes($json))
    $pk     = [Microsoft.Azure.Cosmos.PartitionKey]::new($PartitionKeyValue)

    try {
        $resp = $Container.UpsertItemStreamAsync($stream, $pk).GetAwaiter().GetResult()
        if ([int]$resp.StatusCode -ge 200 -and [int]$resp.StatusCode -lt 300) {
            return $true
        } else {
            $errBody = ""
            try {
                $reader = [System.IO.StreamReader]::new($resp.Content)
                $errBody = $reader.ReadToEnd()
                $reader.Dispose()
            } catch {}
            Write-Log -Level "ERROR" -Message "Cosmos upsert failed: HTTP $($resp.StatusCode)"
            if ($errBody) { Write-Log -Level "ERROR" -Message "  response: $(($errBody -replace '\s+', ' ').Trim().Substring(0, [Math]::Min(500, $errBody.Length)))" }
            return $false
        }
    } catch {
        Write-Log -Level "ERROR" -Message "Cosmos upsert exception: $_"
        return $false
    } finally {
        $stream.Dispose()
    }
}

# ── Local-only constants ────────────────────────────────────────────────────────

$ComposeFile      = Join-Path $ProjectRoot ".docker/cosmos-emulator.yml"
$ContainerName    = "catan-cosmos-emulator"
$CosmosImage      = "mcr.microsoft.com/cosmosdb/linux/azure-cosmos-emulator:vnext-preview"
$EmulatorEndpoint = "http://localhost:8081"
$EmulatorKey      = "C2y6yDjf5/R+ob0N8A7Cgv30VRDJIWEHLM+4QDU5DE2nQ9nDuVTqobD4b8mGGyPMbIZnqyMsEcaGQy67XIw="
$HealthEndpoint   = "http://localhost:8081"

# ── Azure config path ───────────────────────────────────────────────────────────

$AzureConfigFile = Join-Path $ProjectRoot ".azure/catan-azure.json"

# CosmosDB built-in data-plane RBAC role (Cosmos DB Built-in Data Contributor)
$CosmosDataContributorRoleId = "00000000-0000-0000-0000-000000000002"

# ── Default mode ────────────────────────────────────────────────────────────────

if (-not $Local -and -not $Azure) { $Local = $true }

# ════════════════════════════════════════════════════════════════════════════════
# LOCAL HELPERS
# ════════════════════════════════════════════════════════════════════════════════

function Test-DockerCli {
    try { $null = & docker --version 2>&1; return ($LASTEXITCODE -eq 0) }
    catch { return $false }
}

function Test-DockerRunning {
    try { $null = & docker info 2>&1; return ($LASTEXITCODE -eq 0) }
    catch { return $false }
}

<#
.SYNOPSIS
    Ensures Docker CLI is installed and the daemon is running.
    On macOS: starts Docker Desktop automatically if needed.
    Returns $true when Docker is ready to accept commands.
#>
function Assert-Docker {
    param([switch]$Yes, [switch]$Force)

    if (-not (Test-DockerCli)) {
        Write-Log -Level "WARN" -Message "Docker CLI not found."
        if (-not $Yes) {
            $answer = Read-Host "Install Docker Desktop? [y/N]"
            if ($answer -notmatch '^[Yy]') {
                Write-Log -Level "ERROR" -Message "Docker is required. Aborting."
                return $false
            }
        }
        if ($IsMacOS) {
            if (-not (Get-Command brew -ErrorAction SilentlyContinue)) {
                Write-Log -Level "ERROR" -Message "Homebrew required to install Docker. See https://brew.sh"
                return $false
            }
            Write-Log -Level "INFO" -Message "Installing Docker Desktop via Homebrew..."
            & brew install --cask docker 2>&1 | ForEach-Object { Write-Log -Level "DEBUG" -Message $_ }
            Write-Log -Level "INFO" -Message "Open Docker.app then re-run this script."
            return $false
        } elseif ($IsWindows) {
            if (-not (Get-Command winget -ErrorAction SilentlyContinue)) {
                Write-Log -Level "ERROR" -Message "winget required to install Docker."
                return $false
            }
            Write-Log -Level "INFO" -Message "Installing Docker Desktop via winget..."
            $wArgs = @("install","Docker.DockerDesktop","--accept-source-agreements","--accept-package-agreements")
            if ($Yes) { $wArgs += "--silent" }
            & winget @wArgs 2>&1 | ForEach-Object { Write-Log -Level "DEBUG" -Message $_ }
            Write-Log -Level "INFO" -Message "Start Docker Desktop from the Start Menu then re-run."
            return $false
        } else {
            Write-Log -Level "ERROR" -Message "Install Docker Engine: https://docs.docker.com/engine/install/"
            return $false
        }
    }

    if (-not (Test-DockerRunning)) {
        Write-Log -Level "WARN" -Message "Docker daemon is not running."
        if ($IsMacOS) {
            Write-Log -Level "INFO" -Message "Starting Docker Desktop..."
            & open -a Docker 2>&1 | Out-Null
            Write-Log -Level "INFO" -Message "Waiting for daemon (up to 60s)..."
            $deadline = (Get-Date).AddSeconds(60)
            while ((Get-Date) -lt $deadline) {
                Start-Sleep -Seconds 3
                if (Test-DockerRunning) {
                    Write-Log -Level "INFO" -Message "Docker daemon ready."
                    return $true
                }
            }
        }
        Write-Log -Level "ERROR" -Message "Docker daemon not running. Start Docker Desktop and retry."
        return $false
    }

    Write-Log -Level "DEBUG" -Message "Docker ready."
    return $true
}

<#
.SYNOPSIS
    Returns the running state of the CosmosDB emulator container.
    Values: "running" | "stopped" | "absent"
#>
function Get-EmulatorState {
    try {
        $state = (& docker inspect --format '{{.State.Status}}' $ContainerName 2>&1 | Out-String).Trim()
        if ($LASTEXITCODE -ne 0) { return "absent" }
        if ($state -eq "running") { return "running" }
        return "stopped"
    } catch { return "absent" }
}

<#
.SYNOPSIS
    Polls the emulator health endpoint until it responds or the timeout expires.
    Returns $true when healthy.
#>
function Wait-ForEmulatorHealth {
    param([int]$TimeoutSeconds = 120, [int]$PollIntervalSeconds = 5)

    Write-Log -Level "INFO" -Message "Waiting for emulator health (up to ${TimeoutSeconds}s)..."
    $deadline = (Get-Date).AddSeconds($TimeoutSeconds)
    $attempt  = 0

    while ((Get-Date) -lt $deadline) {
        $attempt++
        try {
            $r = Invoke-WebRequest -Uri $HealthEndpoint -TimeoutSec 4 -ErrorAction Stop
            if ($r.StatusCode -lt 300) {
                Write-Log -Level "INFO" -Message "Emulator healthy after ~$($attempt * $PollIntervalSeconds)s."
                return $true
            }
        } catch { }
        Write-Log -Level "DEBUG" -Message "Attempt $attempt — not ready yet, waiting ${PollIntervalSeconds}s..."
        Start-Sleep -Seconds $PollIntervalSeconds
    }

    Write-Log -Level "ERROR" -Message "Emulator did not become healthy within ${TimeoutSeconds}s."
    return $false
}

<#
.SYNOPSIS
    Pulls the emulator image via docker compose (or docker pull when -Force).
    Returns $true on success.
#>
function Invoke-PullImage {
    param([switch]$Force)

    if ($Force) {
        Write-Log -Level "INFO" -Message "Force-pulling latest emulator image..."
        & docker pull $CosmosImage 2>&1 | ForEach-Object { Write-Log -Level "DEBUG" -Message $_ }
    } else {
        Write-Log -Level "INFO" -Message "Pulling emulator image (skips if already cached)..."
        & docker compose -f $ComposeFile pull 2>&1 | ForEach-Object { Write-Log -Level "DEBUG" -Message $_ }
    }
    return ($LASTEXITCODE -eq 0)
}

<#
.SYNOPSIS
    Starts the emulator container, waiting for it to become healthy.
    Returns $true when running and healthy.
#>
function Start-Emulator {
    param([switch]$Force)

    $state = Get-EmulatorState
    if ($state -eq "running" -and -not $Force) {
        Write-Log -Level "INFO" -Message "Emulator already running."
        return $true
    }

    if ($state -eq "stopped") {
        Write-Log -Level "INFO" -Message "Restarting stopped emulator container..."
        & docker start $ContainerName 2>&1 | ForEach-Object { Write-Log -Level "DEBUG" -Message $_ }
        if ($LASTEXITCODE -eq 0) { return Wait-ForEmulatorHealth }
        Write-Log -Level "WARN" -Message "docker start failed; re-creating via compose..."
    }

    Write-Log -Level "INFO" -Message "Starting emulator via docker compose..."
    & docker compose -f $ComposeFile up -d 2>&1 | ForEach-Object { Write-Log -Level "DEBUG" -Message $_ }
    if ($LASTEXITCODE -ne 0) {
        Write-Log -Level "ERROR" -Message "docker compose up failed (exit $LASTEXITCODE)."
        return $false
    }
    return Wait-ForEmulatorHealth
}

<#
.SYNOPSIS
    Stops the emulator container without removing it.
#>
function Stop-Emulator {
    $state = Get-EmulatorState
    if ($state -eq "absent")  { Write-Log -Level "INFO" -Message "No emulator container to stop.";  return }
    if ($state -eq "stopped") { Write-Log -Level "INFO" -Message "Emulator already stopped."; return }
    Write-Log -Level "INFO" -Message "Stopping emulator..."
    & docker compose -f $ComposeFile stop 2>&1 | ForEach-Object { Write-Log -Level "DEBUG" -Message $_ }
}

<#
.SYNOPSIS
    Stops the container and removes the emulator Docker image from local storage.
    Docker Desktop itself is left intact.
#>
function Invoke-LocalClean {
    param([switch]$Yes)

    if (-not $Yes) {
        $answer = Read-Host "Stop container and remove CosmosDB Emulator image? [y/N]"
        if ($answer -notmatch '^[Yy]') {
            Write-Log -Level "INFO" -Message "Clean cancelled."
            return $false
        }
    }

    $state = Get-EmulatorState
    if ($state -ne "absent") {
        Write-Log -Level "INFO" -Message "Stopping and removing container..."
        & docker compose -f $ComposeFile down -v 2>&1 | ForEach-Object { Write-Log -Level "DEBUG" -Message $_ }
    }

    $imgId = (& docker images -q $CosmosImage 2>&1 | Out-String).Trim()
    if ($imgId) {
        Write-Log -Level "INFO" -Message "Removing emulator image $CosmosImage..."
        & docker rmi $CosmosImage 2>&1 | ForEach-Object { Write-Log -Level "DEBUG" -Message $_ }
    } else {
        Write-Log -Level "INFO" -Message "Emulator image not found locally — nothing to remove."
    }
    return $true
}

# ── CosmosDB REST helpers (shared by local emulator and Azure seeding) ──────────

<#
.SYNOPSIS
    Makes an authenticated REST call to any CosmosDB endpoint.
    Supports two auth modes (mutually exclusive):
      -Key       HMAC master-key auth  (local emulator; disableLocalAuth must be false)
      -AadToken  AAD token auth        (Azure; required when disableLocalAuth = true)
    -PartitionKey sets x-ms-partitionkey (required by Azure for document operations).
    -Upsert adds x-ms-documentdb-is-upsert so the call is idempotent.
#>
function Invoke-CosmosDocRest {
    param(
        [string]$Endpoint,
        [string]$Key      = "",   # master key (local emulator)
        [string]$AadToken = "",   # AAD access token (Azure)
        [string]$Method,
        [string]$Path,
        [string]$ResourceType,
        [string]$ResourceId   = "",
        [string]$PartitionKey = "",  # JSON-array string e.g. '["Joe-001"]'
        [object]$Body         = $null,
        [switch]$Upsert
    )

    $date = [DateTime]::UtcNow.ToString("R")

    if ($AadToken) {
        # AAD auth — URL-encode same as master key path; & must become %26
        $authValue = [Uri]::EscapeDataString("type=aad&ver=1.0&sig=$AadToken")
    } else {
        # HMAC master-key auth
        $keyBytes  = [Convert]::FromBase64String($Key)
        $payload   = "$($Method.ToLower())`n$($ResourceType.ToLower())`n$ResourceId`n$($date.ToLower())`n`n"
        $hmac      = [System.Security.Cryptography.HMACSHA256]::new($keyBytes)
        $hash      = $hmac.ComputeHash([Text.Encoding]::UTF8.GetBytes($payload))
        $signature = [Convert]::ToBase64String($hash)
        $authValue = [Uri]::EscapeDataString("type=master&ver=1.0&sig=$signature")
    }

    $headers = @{
        Authorization  = $authValue
        "x-ms-date"    = $date
        "x-ms-version" = "2018-12-31"
        "Content-Type" = "application/json"
    }
    if ($Upsert)       { $headers["x-ms-documentdb-is-upsert"] = "true" }
    if ($PartitionKey) { $headers["x-ms-partitionkey"] = $PartitionKey }

    $uri  = "$($Endpoint.TrimEnd('/'))$Path"
    $bodyJson = if ($null -ne $Body) { $Body | ConvertTo-Json -Depth 20 -Compress } else { $null }

    $reqParams = @{
        Uri         = $uri
        Method      = $Method
        Headers     = $headers
        TimeoutSec  = 30
        ErrorAction = "Stop"
    }
    if ($null -ne $bodyJson) { $reqParams.Body = $bodyJson }

    # Log full request at DEBUG so -TraceLevel DEBUG shows exactly what is sent
    Write-Log -Level "DEBUG" -Message "$Method $uri"
    Write-Log -Level "DEBUG" -Message "  x-ms-version:            $($headers['x-ms-version'])"
    Write-Log -Level "DEBUG" -Message "  x-ms-date:               $date"
    Write-Log -Level "DEBUG" -Message "  x-ms-partitionkey:       $($headers['x-ms-partitionkey'])"
    Write-Log -Level "DEBUG" -Message "  x-ms-documentdb-upsert:  $($headers['x-ms-documentdb-is-upsert'])"
    Write-Log -Level "DEBUG" -Message "  auth-type:               $(if ($AadToken) { 'aad' } else { 'master' })"
    Write-Log -Level "DEBUG" -Message "  resourceType:            $ResourceType"
    Write-Log -Level "DEBUG" -Message "  resourceId:              $ResourceId"
    if ($null -ne $bodyJson) {
        $preview = if ($bodyJson.Length -gt 200) { $bodyJson.Substring(0,200) + "..." } else { $bodyJson }
        Write-Log -Level "DEBUG" -Message "  body:                    $preview"
    }

    try {
        return Invoke-RestMethod @reqParams
    } catch [Microsoft.PowerShell.Commands.HttpResponseException] {
        $status   = [int]$_.Exception.Response.StatusCode
        $errBody  = $null
        try { $errBody = $_.ErrorDetails.Message } catch {}
        if ($status -eq 409) {
            Write-Log -Level "DEBUG" -Message "Already exists (409) — OK."
            return $null
        }
        Write-Log -Level "ERROR" -Message "CosmosDB REST $Method $Path → HTTP $status"
        if ($errBody) {
            $compact = ($errBody -replace '\s+', ' ').Trim()
            Write-Log -Level "ERROR" -Message "  response body: $compact"
        }
        throw
    }
}

<#
.SYNOPSIS
    Thin wrapper: HMAC REST call to the local emulator using module-level constants.
#>
function Invoke-CosmosRest {
    param(
        [string]$Method,
        [string]$Path,
        [string]$ResourceType,
        [string]$ResourceId = "",
        [object]$Body = $null
    )
    Invoke-CosmosDocRest -Endpoint $EmulatorEndpoint -Key $EmulatorKey `
        -Method $Method -Path $Path -ResourceType $ResourceType `
        -ResourceId $ResourceId -Body $Body
}

<#
.SYNOPSIS
    Returns $true if the named container exists in the local emulator database.
#>
function Test-LocalContainerExists {
    param([string]$Name)
    try {
        $r = Invoke-CosmosRest -Method "GET" `
            -Path "/dbs/$DatabaseName/colls/$Name" `
            -ResourceType "colls" `
            -ResourceId "dbs/$DatabaseName/colls/$Name"
        return ($null -ne $r)
    } catch [Microsoft.PowerShell.Commands.HttpResponseException] {
        if ($_.Exception.Response.StatusCode -eq 404) { return $false }
        throw
    } catch { return $false }
}

<#
.SYNOPSIS
    Creates the Catan database and all required containers in the local emulator.
    Idempotent — skips resources that already exist (409 Conflict).
#>
function Invoke-LocalSeed {
    Write-Log -Level "INFO" -Message "Seeding: ensuring database '$DatabaseName' exists..."
    Invoke-CosmosRest -Method "POST" -Path "/dbs" -ResourceType "dbs" `
        -Body @{ id = $DatabaseName } | Out-Null

    foreach ($name in $Containers.Keys) {
        $partKey = $Containers[$name]
        Write-Log -Level "INFO" -Message "  Container '$name' ($partKey)..."
        $body = @{
            id             = $name
            partitionKey   = @{ paths = @($partKey); kind = "Hash"; version = 2 }
            indexingPolicy = $ContainerIndexPolicies[$name]
        }
        Invoke-CosmosRest -Method "POST" -Path "/dbs/$DatabaseName/colls" `
            -ResourceType "colls" -ResourceId "dbs/$DatabaseName" -Body $body | Out-Null
    }
    Write-Log -Level "INFO" -Message "Seed complete."
}

# ── Local doctor ────────────────────────────────────────────────────────────────

<#
.SYNOPSIS
    Returns an ordered hashtable describing the health of the local emulator.
#>
function Get-LocalDoctorResult {
    $result = [ordered]@{
        Name            = "cosmos-local"
        DockerInstalled = $false
        DockerRunning   = $false
        EmulatorState   = "absent"
        EmulatorHealthy = $false
        DatabaseExists  = $false
        Containers      = [ordered]@{}
        AllContainersOk = $false
        Status          = "NotReady"
        Message         = ""
    }

    $result.DockerInstalled = Test-DockerCli
    if (-not $result.DockerInstalled) {
        $result.Status  = "NoDocker"
        $result.Message = "Docker CLI not found. Run: database.ps1 install"
        return $result
    }

    $result.DockerRunning = Test-DockerRunning
    if (-not $result.DockerRunning) {
        $result.Status  = "DockerStopped"
        $result.Message = "Docker daemon not running. Start Docker Desktop and retry."
        return $result
    }

    $result.EmulatorState = Get-EmulatorState
    if ($result.EmulatorState -ne "running") {
        $result.Status  = "EmulatorStopped"
        $result.Message = "CosmosDB Emulator not running. Run: database.ps1 start"
        return $result
    }

    try {
        $null = Invoke-WebRequest -Uri $HealthEndpoint -TimeoutSec 5 -ErrorAction Stop
        $result.EmulatorHealthy = $true
    } catch {
        $result.Status  = "EmulatorUnhealthy"
        $result.Message = "Emulator running but health probe failed at $HealthEndpoint"
        return $result
    }

    try {
        $null = Invoke-CosmosRest -Method "GET" -Path "/dbs/$DatabaseName" `
            -ResourceType "dbs" -ResourceId "dbs/$DatabaseName"
        $result.DatabaseExists = $true
    } catch {
        $result.Status  = "NeedsSeeding"
        $result.Message = "Emulator healthy but database '$DatabaseName' missing. Run: database.ps1 seed"
        return $result
    }

    $allOk = $true
    foreach ($name in $Containers.Keys) {
        $exists = Test-LocalContainerExists -Name $name
        $result.Containers[$name] = $exists
        if (-not $exists) { $allOk = $false }
    }
    $result.AllContainersOk = $allOk

    if (-not $allOk) {
        $missing = ($result.Containers.Keys | Where-Object { -not $result.Containers[$_] }) -join ", "
        $result.Status  = "NeedsSeeding"
        $result.Message = "Missing containers: $missing. Run: database.ps1 seed"
    } else {
        $result.Status  = "Ready"
        $result.Message = "CosmosDB Emulator is running and all containers are present."
    }
    return $result
}

<#
.SYNOPSIS
    Prints a formatted local doctor report to the console.
#>
function Show-LocalDoctorOutput {
    param([System.Collections.Specialized.OrderedDictionary]$Result)

    Write-Host ""
    Write-Host "CosmosDB Local Emulator Status" -ForegroundColor Cyan
    Write-Host "==============================" -ForegroundColor Cyan
    Write-Host ""

    $color = switch ($Result.Status) {
        "Ready"        { "Green"  }
        "NeedsSeeding" { "Yellow" }
        default        { "Red"    }
    }

    Write-Host ("Docker CLI:       " + ($Result.DockerInstalled ? "OK" : "MISSING"))
    Write-Host ("Docker Daemon:    " + ($Result.DockerRunning   ? "Running" : "Stopped"))
    Write-Host ("Emulator:         " + $Result.EmulatorState)
    Write-Host ("Health Probe:     " + ($Result.EmulatorHealthy ? "OK" : "FAIL"))
    Write-Host ("Database:         " + ($Result.DatabaseExists  ? "OK ($DatabaseName)" : "Missing"))

    if ($Result.Containers.Count -gt 0) {
        Write-Host "Containers:"
        foreach ($name in $Result.Containers.Keys) {
            Write-Host ("  $name".PadRight(24) + ($Result.Containers[$name] ? "OK" : "MISSING"))
        }
    }

    Write-Host ""
    Write-Host ("Status: " + $Result.Status) -ForegroundColor $color
    Write-Host $Result.Message -ForegroundColor $color
    Write-Host ""
}

# ════════════════════════════════════════════════════════════════════════════════
# AZURE HELPERS
# ════════════════════════════════════════════════════════════════════════════════

<#
.SYNOPSIS
    Reads .azure/catan-azure.json and returns it as a PSCustomObject.
    Throws if the file does not exist.
#>
function Get-AzureConfig {
    if (-not (Test-Path $AzureConfigFile)) {
        throw "Azure config not found at $AzureConfigFile. Run catan-azure.ps1 install first."
    }
    return (Get-Content $AzureConfigFile -Raw | ConvertFrom-Json)
}

<#
.SYNOPSIS
    Saves updated config back to .azure/catan-azure.json, preserving existing fields.
#>
function Save-AzureConfig {
    param([psobject]$Config)
    $Config | ConvertTo-Json -Depth 10 | Set-Content $AzureConfigFile -Encoding UTF8
    Write-Log -Level "DEBUG" -Message "Saved config to $AzureConfigFile"
}

<#
.SYNOPSIS
    Returns the CosmosDB account name from config, or derives it as "cosmos-{baseName}".
#>
function Get-CosmosAccountName {
    param([psobject]$Config)
    if ($Config.cosmosDb -and $Config.cosmosDb.accountName) {
        return $Config.cosmosDb.accountName
    }
    return "cosmos-$($Config.baseName)"
}

<#
.SYNOPSIS
    Returns the CosmosDB endpoint URL for the given account name.
#>
function Get-CosmosEndpointUrl {
    param([string]$AccountName)
    return "https://$AccountName.documents.azure.com:443/"
}

<#
.SYNOPSIS
    Verifies the az CLI is installed and the current session is logged in.
    Returns $true when ready.
#>
function Assert-AzCli {
    if (-not (Get-Command az -ErrorAction SilentlyContinue)) {
        Write-Log -Level "ERROR" -Message "Azure CLI (az) not found. Install from https://aka.ms/installazurecli"
        return $false
    }
    $null = Invoke-Azure account show
    if ($LASTEXITCODE -ne 0) {
        Write-Log -Level "ERROR" -Message "Not logged in to Azure. Run: az login"
        return $false
    }
    Write-Log -Level "DEBUG" -Message "Azure CLI ready."
    return $true
}

<#
.SYNOPSIS
    Creates the CosmosDB account if it does not already exist.
    Attempts free tier first; falls back to standard if the subscription's
    free-tier quota is already used by another account.
    Returns $true on success.
#>
function Install-AzureCosmosAccount {
    param([psobject]$Config, [string]$AccountName, [switch]$Force)

    $rg       = $Config.resourceGroup
    $location = $Config.location

    $null = Invoke-Azure cosmosdb show --name $AccountName --resource-group $rg
    if ($LASTEXITCODE -eq 0 -and -not $Force) {
        Write-Log -Level "INFO" -Message "Account '$AccountName' already exists — skipping creation."
        return $true
    }

    Write-Log -Level "INFO" -Message "Creating CosmosDB account '$AccountName' with 400 RU/s provisioned throughput (this takes 1-3 minutes)..."

    $null = Invoke-Azure cosmosdb create `
        --name $AccountName `
        --resource-group $rg `
        --kind GlobalDocumentDB `
        --locations regionName=$location failoverPriority=0 isZoneRedundant=false `
        --default-consistency-level Session `
        --output none

    if ($LASTEXITCODE -ne 0) {
        Write-Log -Level "ERROR" -Message "Failed to create CosmosDB account."
        return $false
    }

    Write-Log -Level "INFO" -Message "Account '$AccountName' created."
    return $true
}

<#
.SYNOPSIS
    Creates the Catan database and all required containers on an Azure CosmosDB account.
    Idempotent — skips resources that already exist.
    Returns $true on success.
#>
function Invoke-AzureSeed {
    param([psobject]$Config, [string]$AccountName)

    $rg = $Config.resourceGroup

    # Database: check first (~1s), create only if missing (~30s)
    $null = Invoke-Azure cosmosdb sql database show `
        --account-name $AccountName --resource-group $rg --name $DatabaseName
    if ($LASTEXITCODE -ne 0) {
        Write-Log -Level "INFO" -Message "Creating database '$DatabaseName'..."
        $null = Invoke-Azure cosmosdb sql database create `
            --account-name $AccountName --resource-group $rg --name $DatabaseName --output none
        if ($LASTEXITCODE -ne 0) {
            Write-Log -Level "ERROR" -Message "Failed to create database '$DatabaseName'."
            return $false
        }
    } else {
        Write-Log -Level "DEBUG" -Message "Database '$DatabaseName' already exists."
    }

    # Containers: check first (~1s each), create only if missing (~30s each).
    # --idx JSON written to a temp file to avoid shell quoting issues with nested arrays.
    $anyCreated = $false
    foreach ($name in $Containers.Keys) {
        $null = Invoke-Azure cosmosdb sql container show `
            --account-name $AccountName --resource-group $rg `
            --database-name $DatabaseName --name $name
        if ($LASTEXITCODE -eq 0) {
            Write-Log -Level "DEBUG" -Message "  Container '$name' already exists — skip."
            continue
        }

        $partKey = $Containers[$name]
        Write-Log -Level "INFO" -Message "  Creating container '$name' ($partKey) — ~30s..."
        $tmpIdx = [System.IO.Path]::GetTempFileName() + ".json"
        $ContainerIndexPolicies[$name] | ConvertTo-Json -Depth 5 -Compress |
            Set-Content -Path $tmpIdx -Encoding UTF8 -NoNewline
        $null = Invoke-Azure cosmosdb sql container create `
            --account-name $AccountName --resource-group $rg `
            --database-name $DatabaseName --name $name `
            --partition-key-path $partKey --partition-key-version 1 `
            --idx "@$tmpIdx" --output none
        Remove-Item $tmpIdx -Force -ErrorAction SilentlyContinue
        if ($LASTEXITCODE -ne 0) {
            Write-Log -Level "ERROR" -Message "  Failed to create container '$name'."
            return $false
        }
        Write-Log -Level "INFO" -Message "  Container '$name' created."
        $anyCreated = $true
    }

    # Log partition keys for any containers just created (verify version=1)
    if ($anyCreated) {
        foreach ($name in $Containers.Keys) {
            $pk = Invoke-Azure cosmosdb sql container show `
                --account-name $AccountName --resource-group $rg `
                --database-name $DatabaseName --name $name `
                --query "resource.partitionKey" --output json
            Write-Log -Level "DEBUG" -Message "  Container '$name' partitionKey: $($pk -replace '\s+', ' ')"
        }
    }

    Write-Log -Level "INFO" -Message "Azure infrastructure ready (database + containers)."
    return $true
}

<#
.SYNOPSIS
    Configures the GameService App Service to use the CosmosDB account:
      1. Sets the COSMOS_ENDPOINT app setting.
      2. Grants the App Service system-assigned managed identity the
         "Cosmos DB Built-in Data Contributor" data-plane RBAC role.
    Returns $true on success.
#>
function Deploy-AzureDatabase {
    param([psobject]$Config, [string]$AccountName, [string]$Endpoint)

    $rg      = $Config.resourceGroup
    $appName = $Config.gameService.appName

    # 1. Set COSMOS_ENDPOINT app setting
    Write-Log -Level "INFO" -Message "Setting COSMOS_ENDPOINT on App Service '$appName'..."
    $null = Invoke-Azure webapp config appsettings set `
        --name $appName `
        --resource-group $rg `
        --settings "COSMOS_ENDPOINT=$Endpoint" `
        --output none
    if ($LASTEXITCODE -ne 0) {
        Write-Log -Level "ERROR" -Message "Failed to set COSMOS_ENDPOINT app setting."
        return $false
    }

    # 3. Get managed identity principal ID
    Write-Log -Level "INFO" -Message "Retrieving managed identity for '$appName'..."
    $principalId = Invoke-Azure webapp identity show `
        --name $appName `
        --resource-group $rg `
        --query principalId `
        --output tsv
    if ($LASTEXITCODE -ne 0 -or [string]::IsNullOrWhiteSpace($principalId)) {
        Write-Log -Level "WARN" -Message "Managed identity not found — assigning one..."
        $null = Invoke-Azure webapp identity assign --name $appName --resource-group $rg --output none
        $principalId = Invoke-Azure webapp identity show `
            --name $appName `
            --resource-group $rg `
            --query principalId `
            --output tsv
        if ($LASTEXITCODE -ne 0) {
            Write-Log -Level "ERROR" -Message "Failed to get managed identity principal ID."
            return $false
        }
    }
    $principalId = $principalId.Trim()
    Write-Log -Level "DEBUG" -Message "Principal ID: $principalId"

    # 4. Get Cosmos account resource ID (scope for all role assignments)
    $accountId = Invoke-Azure cosmosdb show `
        --name $AccountName `
        --resource-group $rg `
        --query id `
        --output tsv
    if ($LASTEXITCODE -ne 0) {
        Write-Log -Level "ERROR" -Message "Failed to get CosmosDB account resource ID."
        return $false
    }
    $accountId = $accountId.Trim()

    # 5+6. Grant role to App Service MI and signed-in developer
    $developerOid = (Invoke-Azure ad signed-in-user show --query id --output tsv).Trim()

    $principals = @( @{ Id = $principalId; Label = "App Service managed identity" } )
    if (-not [string]::IsNullOrWhiteSpace($developerOid)) {
        $principals += @{ Id = $developerOid; Label = "signed-in developer ($developerOid)" }
    }

    Write-Log -Level "INFO" -Message "Granting data contributor role..."
    foreach ($p in $principals) {
        Write-Log -Level "INFO" -Message "  Granting to $($p.Label)..."
        $ok = Grant-CosmosRbac -AccountName $AccountName -ResourceGroup $rg `
                               -PrincipalId $p.Id -Scope $accountId
        if (-not $ok) {
            Write-Log -Level "ERROR" -Message "  RBAC grant failed: $($p.Label)"
            return $false
        }
        Write-Log -Level "INFO" -Message "  RBAC granted: $($p.Label)"
    }

    Write-Log -Level "INFO" -Message "App Service '$appName' configured for CosmosDB."
    return $true
}

<#
.SYNOPSIS
    Grants Cosmos DB Built-in Data Contributor role to a principal (idempotent).
    Returns $true on success or if already assigned.
#>
function Grant-CosmosRbac {
    param([string]$AccountName, [string]$ResourceGroup, [string]$PrincipalId, [string]$Scope)

    $null = Invoke-Azure cosmosdb sql role assignment create `
        --account-name $AccountName `
        --resource-group $ResourceGroup `
        --role-definition-id $CosmosDataContributorRoleId `
        --principal-id $PrincipalId `
        --scope $Scope `
        --output none
    if ($LASTEXITCODE -eq 0) { return $true }

    # Non-zero exit — check if the assignment already exists
    $existing = Invoke-Azure cosmosdb sql role assignment list `
        --account-name $AccountName `
        --resource-group $ResourceGroup `
        --query "[?principalId=='$PrincipalId']" `
        --output tsv
    if ($LASTEXITCODE -eq 0 -and -not [string]::IsNullOrWhiteSpace($existing)) {
        Write-Log -Level "DEBUG" -Message "Role already assigned to $PrincipalId — OK."
        return $true
    }

    Write-Log -Level "ERROR" -Message "Failed to grant Cosmos RBAC role to $PrincipalId."
    return $false
}

<#
.SYNOPSIS
    Adds the current developer's public IP (and 0.0.0.0 for Azure services) to the
    CosmosDB account IP allow-list. Idempotent — skips IPs already present.
    Returns $true on success.
#>
function Grant-CosmosFirewallAccess {
    param([string]$AccountName, [string]$ResourceGroup)

    # Check current state — need publicNetworkAccess=Enabled and empty ipRules.
    # Empty ipRules + Enabled = allow all public IPs. We do NOT add specific IPs because
    # corporate networks egress from unpredictable IPs that differ from what ipify.org reports.
    $acctJson = Invoke-Azure cosmosdb show `
        --name $AccountName `
        --resource-group $ResourceGroup `
        --query "{pna:publicNetworkAccess, ruleCount:length(ipRules)}" `
        --output json
    $acctInfo = if ($LASTEXITCODE -eq 0 -and -not [string]::IsNullOrWhiteSpace($acctJson)) {
        try { $acctJson | ConvertFrom-Json } catch { $null }
    } else { $null }

    $alreadyOpen = ($null -ne $acctInfo -and $acctInfo.pna -eq "Enabled" -and $acctInfo.ruleCount -eq 0)
    if ($alreadyOpen) {
        Write-Log -Level "INFO" -Message "Firewall already open (publicNetworkAccess=Enabled, no IP restrictions)."
        return $true
    }

    Write-Log -Level "INFO" -Message "Opening CosmosDB to all public IPs (clearing ipRules, enabling public access)..."
    $null = Invoke-Azure cosmosdb update `
        --name $AccountName `
        --resource-group $ResourceGroup `
        --ip-range-filter "" `
        --public-network-access Enabled `
        --output none
    if ($LASTEXITCODE -ne 0) {
        Write-Log -Level "ERROR" -Message "Failed to update CosmosDB firewall (exit $LASTEXITCODE)."
        return $false
    }
    Write-Log -Level "INFO" -Message "Firewall open — all public IPs allowed."
    Write-Log -Level "INFO" -Message "NOTE: Firewall changes can take 1-2 min to propagate."
    return $true
}

<#
.SYNOPSIS
    Returns an ordered hashtable describing the health of the Azure CosmosDB database.
#>
function Get-AzureDoctorResult {
    param([psobject]$Config, [string]$AccountName)

    $rg      = $Config.resourceGroup
    $appName = $Config.gameService.appName

    # Discover current public IP once — used for firewall check
    $currentIp = ""
    try { $currentIp = (Invoke-RestMethod -Uri "https://api.ipify.org" -TimeoutSec 5 -ErrorAction Stop).Trim() } catch {}

    $result = [ordered]@{
        Name             = "cosmos-azure"
        AccountName      = $AccountName
        AccountExists    = $false
        AccountState     = "Unknown"
        DatabaseExists   = $false
        Containers       = [ordered]@{}
        AllContainersOk  = $false
        FirewallOk       = $false
        CurrentIp        = $currentIp
        AppSettingSet    = $false
        RbacGranted      = $false
        Status           = "NotReady"
        Message          = ""
    }

    # 1. Cosmos account
    $acctJson = Invoke-Azure cosmosdb show --name $AccountName --resource-group $rg --output json
    if ($LASTEXITCODE -ne 0 -or [string]::IsNullOrWhiteSpace($acctJson)) {
        $result.Status  = "NoAccount"
        $result.Message = "CosmosDB account '$AccountName' not found in '$rg'. Run: database.ps1 install -Azure"
        return $result
    }
    $acct = $acctJson | ConvertFrom-Json
    $result.AccountExists = $true
    $result.AccountState  = $acct.provisioningState

    if ($acct.provisioningState -ne "Succeeded") {
        $result.Status  = "AccountNotReady"
        $result.Message = "Account '$AccountName' provisioningState is '$($acct.provisioningState)' — not yet ready."
        return $result
    }

    # 2. Firewall — publicNetworkAccess must be Enabled AND ipRules must be empty.
    # Empty ipRules + Enabled = allow all public IPs (the correct open config).
    # Non-empty ipRules restricts to specific IPs, which breaks on corporate networks
    # where the actual egress IP differs from what the developer's machine reports.
    $publicNetworkEnabled = ($acct.publicNetworkAccess -eq "Enabled")
    $ipRulesEmpty         = ($acct.ipRules.Count -eq 0)
    $result.FirewallOk    = $publicNetworkEnabled -and $ipRulesEmpty

    # 3. Database
    $null = Invoke-Azure cosmosdb sql database show `
        --account-name $AccountName `
        --resource-group $rg `
        --name $DatabaseName
    $result.DatabaseExists = ($LASTEXITCODE -eq 0)

    # 4. Containers (only checkable if DB exists)
    $allOk = $result.DatabaseExists
    if ($result.DatabaseExists) {
        foreach ($name in $Containers.Keys) {
            $null = Invoke-Azure cosmosdb sql container show `
                --account-name $AccountName `
                --resource-group $rg `
                --database-name $DatabaseName `
                --name $name
            $exists = ($LASTEXITCODE -eq 0)
            $result.Containers[$name] = $exists
            if (-not $exists) { $allOk = $false }
        }
    } else {
        foreach ($name in $Containers.Keys) { $result.Containers[$name] = $false }
    }
    $result.AllContainersOk = $allOk

    # 5. App Service COSMOS_ENDPOINT setting
    $settingsJson = Invoke-Azure webapp config appsettings list `
        --name $appName `
        --resource-group $rg `
        --output json
    $settings = if ($LASTEXITCODE -eq 0 -and -not [string]::IsNullOrWhiteSpace($settingsJson)) {
        $settingsJson | ConvertFrom-Json
    } else { @() }
    $cosmosSettingEntry = $settings | Where-Object { $_.name -eq "COSMOS_ENDPOINT" }
    $result.AppSettingSet = ($null -ne $cosmosSettingEntry -and -not [string]::IsNullOrWhiteSpace($cosmosSettingEntry.value))

    # 6. RBAC: managed identity has data contributor role
    $principalId = Invoke-Azure webapp identity show `
        --name $appName `
        --resource-group $rg `
        --query principalId `
        --output tsv
    if ($LASTEXITCODE -eq 0 -and -not [string]::IsNullOrWhiteSpace($principalId)) {
        $principalId = $principalId.Trim()
        $assignmentsJson = Invoke-Azure cosmosdb sql role assignment list `
            --account-name $AccountName `
            --resource-group $rg `
            --query "[?principalId=='$principalId' && contains(roleDefinitionId, '$CosmosDataContributorRoleId')]" `
            --output json
        $assignments = if ($LASTEXITCODE -eq 0 -and -not [string]::IsNullOrWhiteSpace($assignmentsJson)) {
            $assignmentsJson | ConvertFrom-Json
        } else { @() }
        $result.RbacGranted = ($assignments.Count -gt 0)
    }

    # Derive final status from all accumulated checks
    if (-not $result.DatabaseExists -or -not $result.AllContainersOk) {
        $result.Status  = "NeedsSeeding"
        $result.Message = if (-not $result.DatabaseExists) {
            "Database '$DatabaseName' missing. Run: database.ps1 install -Azure"
        } else {
            $missing = ($result.Containers.Keys | Where-Object { -not $result.Containers[$_] }) -join ", "
            "Missing containers: $missing. Run: database.ps1 install -Azure"
        }
    } elseif (-not $result.AppSettingSet -or -not $result.RbacGranted) {
        $missing = @()
        if (-not $result.AppSettingSet) { $missing += "COSMOS_ENDPOINT app setting" }
        if (-not $result.RbacGranted)   { $missing += "RBAC role assignment" }
        $result.Status  = "NeedsDeploy"
        $result.Message = "Missing: $($missing -join ', '). Run: database.ps1 deploy -Azure"
    } elseif (-not $result.FirewallOk) {
        $result.Status  = "FirewallBlocked"
        $result.Message = "IP $currentIp is not in the CosmosDB allow-list. Run: database.ps1 install -Azure"
    } else {
        $result.Status  = "Ready"
        $result.Message = "Azure CosmosDB is configured and the App Service is wired up."
    }
    return $result
}

<#
.SYNOPSIS
    Prints a formatted Azure doctor report to the console.
#>
function Show-AzureDoctorOutput {
    param([System.Collections.Specialized.OrderedDictionary]$Result)

    Write-Host ""
    Write-Host "CosmosDB Azure Status" -ForegroundColor Cyan
    Write-Host "=====================" -ForegroundColor Cyan
    Write-Host ""

    $color = switch ($Result.Status) {
        "Ready"           { "Green"  }
        "NeedsDeploy"     { "Yellow" }
        "NeedsSeeding"    { "Yellow" }
        "FirewallBlocked" { "Red"    }
        default           { "Red"    }
    }

    Write-Host ("Account:          " + ($Result.AccountExists ? "OK ($($Result.AccountName))" : "Missing"))
    Write-Host ("Account State:    " + $Result.AccountState)
    Write-Host ("Database:         " + ($Result.DatabaseExists ? "OK ($DatabaseName)" : "Missing"))

    if ($Result.Containers.Count -gt 0) {
        Write-Host "Containers:"
        foreach ($name in $Result.Containers.Keys) {
            Write-Host ("  $name".PadRight(24) + ($Result.Containers[$name] ? "OK" : "MISSING"))
        }
    }

    $fwMsg = if ($Result.FirewallOk) { "OK (publicNetworkAccess=Enabled, no IP restrictions)" } else { "BLOCKED (publicNetworkAccess=Disabled or ipRules restricting access)" }
    Write-Host ("Firewall:         " + $fwMsg)
    Write-Host ("App Setting:      " + ($Result.AppSettingSet ? "OK (COSMOS_ENDPOINT)" : "Missing"))
    Write-Host ("RBAC:             " + ($Result.RbacGranted   ? "OK (Data Contributor)" : "Missing"))
    Write-Host ""
    Write-Host ("Status: " + $Result.Status) -ForegroundColor $color
    Write-Host $Result.Message -ForegroundColor $color
    Write-Host ""
}

# ════════════════════════════════════════════════════════════════════════════════
# DEFAULT DATA SEEDING  (control plane — writes directly to Cosmos, no app needed)
# ════════════════════════════════════════════════════════════════════════════════

<#
.SYNOPSIS
    Seeds *.json files from a subdirectory into a Cosmos container using the .NET SDK.
    Each JSON file IS the exact CosmosDB document — inserted verbatim.
    Idempotent: uses upsert, so re-running is safe.
#>
function Invoke-SeedFromJsonFiles {
    param(
        [Microsoft.Azure.Cosmos.Container]$Container,
        [string]$Directory,
        [string]$Label = "document"
    )

    if (-not (Test-Path $Directory)) {
        Write-Log -Level "WARN" -Message "No $Label directory at $Directory — skipping."
        return 0
    }

    $jsonFiles = Get-ChildItem -Path $Directory -Filter "*.json" -ErrorAction SilentlyContinue
    if (-not $jsonFiles -or $jsonFiles.Count -eq 0) {
        Write-Log -Level "WARN" -Message "No $Label JSON files found in $Directory"
        return 0
    }

    $ok = 0
    foreach ($file in $jsonFiles) {
        $doc = Get-Content -Raw $file.FullName | ConvertFrom-Json
        $id  = $doc.id
        if (-not $id) {
            Write-Log -Level "WARN" -Message "  Skipping $($file.Name): no 'id' field"
            continue
        }

        $name = if ($doc.name) { $doc.name } else { $id }
        Write-Log -Level "INFO" -Message "  $Label '$name' ($id)..."

        if (Invoke-CosmosUpsert -Container $Container -Document $doc -PartitionKeyValue $id) {
            $ok++
        }
    }

    Write-Log -Level "INFO" -Message "$Label seeded ($ok/$($jsonFiles.Count) files)."
    return $ok
}

<#
.SYNOPSIS
    Seeds all default data into the Cosmos database using the .NET SDK.
    For Azure: uses DefaultAzureCredential (AAD). For local: uses emulator key.
    Returns $true on success.
#>
function Invoke-SeedDefaultData {
    param([switch]$IsAzure, [psobject]$Config = $null, [string]$AccountName = "")

    if ($IsAzure) {
        $endpoint = Get-CosmosEndpointUrl -AccountName $AccountName
    } else {
        $endpoint = $EmulatorEndpoint
    }

    # Create SDK client (handles AAD or master-key auth)
    $client = New-CosmosClient -IsAzure:$IsAzure -Endpoint $endpoint -Key $EmulatorKey
    if (-not $client) { return $false }

    try {
        $db = $client.GetDatabase($DatabaseName)

        # Probe RBAC with a quick write/delete
        if ($IsAzure) {
            Write-Log -Level "INFO" -Message "Probing RBAC write access via SDK..."
            $playersContainer = $db.GetContainer("players")
            $probeDoc = @{ id = "__rbac-probe__"; name = "probe" } | ConvertTo-Json -Compress
            $probeStream = [System.IO.MemoryStream]::new([System.Text.Encoding]::UTF8.GetBytes($probeDoc))
            $probePk = [Microsoft.Azure.Cosmos.PartitionKey]::new("__rbac-probe__")

            $probeOk = $false
            $deadline = (Get-Date).AddMinutes(5)
            while ((Get-Date) -lt $deadline) {
                try {
                    $probeStream.Position = 0
                    $r = $playersContainer.UpsertItemStreamAsync($probeStream, $probePk).GetAwaiter().GetResult()
                    if ([int]$r.StatusCode -ge 200 -and [int]$r.StatusCode -lt 300) {
                        $probeOk = $true
                        # Clean up
                        try { $playersContainer.DeleteItemStreamAsync("__rbac-probe__", $probePk).GetAwaiter().GetResult() | Out-Null } catch {}
                        break
                    }
                    Write-Log -Level "DEBUG" -Message "  Probe → HTTP $($r.StatusCode), waiting..."
                } catch {
                    Write-Log -Level "DEBUG" -Message "  Probe → $($_.Exception.Message), waiting..."
                }
                Start-Sleep -Seconds 10
            }
            $probeStream.Dispose()

            if ($probeOk) {
                Write-Log -Level "INFO" -Message "RBAC write confirmed."
            } else {
                Write-Log -Level "WARN" -Message "RBAC write not confirmed — attempting seed anyway."
            }
        }

        $defaultDataPath = Join-Path $ProjectRoot "Catan3.GameService" "Default Data"
        if (-not (Test-Path $defaultDataPath)) {
            Write-Log -Level "ERROR" -Message "Default Data path not found: $defaultDataPath"
            return $false
        }

        # Seed each collection from its JSON directory
        Write-Log -Level "INFO" -Message "Seeding players..."
        $null = Invoke-SeedFromJsonFiles -Container $db.GetContainer("players") `
            -Directory (Join-Path $defaultDataPath "Players") -Label "Player"

        Write-Log -Level "INFO" -Message "Seeding recordings..."
        $null = Invoke-SeedFromJsonFiles -Container $db.GetContainer("recordings") `
            -Directory (Join-Path $defaultDataPath "Recordings") -Label "Recording"

        Write-Log -Level "INFO" -Message "Default data seeding complete."
        return $true
    } finally {
        $client.Dispose()
    }
}

# ════════════════════════════════════════════════════════════════════════════════
# SHARED: TEST PARAMS
# ════════════════════════════════════════════════════════════════════════════════

<#
.SYNOPSIS
    Writes .cosmos-test-params.json for the local emulator (key + HTTP endpoint).
    Called by catan.ps1 test to avoid duplicating the endpoint/key constants.
#>
function Write-LocalTestParams {
    $params = @{ endpoint = $EmulatorEndpoint; key = $EmulatorKey }
    $dir    = Split-Path -Parent $TestParamsFile
    if (-not (Test-Path $dir)) { New-Item -ItemType Directory -Path $dir | Out-Null }
    Set-Content -Path $TestParamsFile -Value ($params | ConvertTo-Json -Depth 5) -Encoding UTF8
    Write-Log -Level "DEBUG" -Message "Wrote local test params to $TestParamsFile"
}

<#
.SYNOPSIS
    Writes .cosmos-test-params.json for Azure (HTTPS endpoint only, no key).
    CosmosCatanDbTests uses DefaultAzureCredential when no key is present.
#>
function Write-AzureTestParams {
    param([string]$Endpoint)
    $params = @{ endpoint = $Endpoint }
    $dir    = Split-Path -Parent $TestParamsFile
    if (-not (Test-Path $dir)) { New-Item -ItemType Directory -Path $dir | Out-Null }
    Set-Content -Path $TestParamsFile -Value ($params | ConvertTo-Json -Depth 5) -Encoding UTF8
    Write-Log -Level "DEBUG" -Message "Wrote Azure test params to $TestParamsFile"
}

<#
.SYNOPSIS
    Removes the test params file if it exists.
#>
function Remove-TestParams {
    if (Test-Path $TestParamsFile) {
        Remove-Item $TestParamsFile -Force
        Write-Log -Level "DEBUG" -Message "Removed test params file."
    }
}

<#
.SYNOPSIS
    Starts the local emulator, seeds it, runs the CatanDb contract tests,
    and cleans up the params file.  Returns the dotnet test exit code.
#>
function Invoke-LocalTests {
    if (-not (Assert-Docker -Yes:$Yes -Force:$Force)) { return 1 }
    if (-not (Start-Emulator -Force:$Force)) {
        Write-Log -Level "ERROR" -Message "Emulator failed to start. Aborting tests."
        return 1
    }
    Invoke-LocalSeed
    try {
        Write-LocalTestParams
        Write-Log -Level "INFO" -Message "Running CatanDb contract tests..."
        & dotnet test $TestProject `
            --filter "FullyQualifiedName~CatanDb" `
            --logger "console;verbosity=normal" `
            2>&1 | ForEach-Object { Write-Host $_ }
        return $LASTEXITCODE
    } finally {
        Remove-TestParams
    }
}

<#
.SYNOPSIS
    Runs the CatanDb contract tests against Azure CosmosDB.
    The caller must already be authenticated (DefaultAzureCredential).
    Returns the dotnet test exit code.
#>
function Invoke-AzureTests {
    param([string]$Endpoint)
    try {
        Write-AzureTestParams -Endpoint $Endpoint
        Write-Log -Level "INFO" -Message "Running CatanDb contract tests against Azure ($Endpoint)..."
        & dotnet test $TestProject `
            --filter "FullyQualifiedName~CatanDb" `
            --logger "console;verbosity=normal" `
            2>&1 | ForEach-Object { Write-Host $_ }
        return $LASTEXITCODE
    } finally {
        Remove-TestParams
    }
}

# ════════════════════════════════════════════════════════════════════════════════
# HELP
# ════════════════════════════════════════════════════════════════════════════════

function Show-Help {
    $text = @"
database.ps1 — CosmosDB database management (local emulator and Azure)

Usage:
    database.ps1 <verb> [-Local | -Azure] [-Yes] [-Force] [-Json] [-TraceLevel LEVEL]

Local verbs (default / -Local):
    install           Pull emulator image, start container, seed structure + default data
    seed              Create/verify Catan database and all required containers
    seed-data         Load default players and recordings into the emulator
    start             Start the emulator container (no image pull)
    stop              Stop the emulator container
    clean             Stop container and remove the emulator Docker image
    doctor / status   Emulator health + container check (-Json for JSON output)
    test              Start emulator, run CatanDb contract tests, clean up
    write-test-params Write .cosmos-test-params.json for dotnet test

Azure verbs (-Azure):
    install           Create CosmosDB account, containers, and seed default data
    deploy            Set COSMOS_ENDPOINT on App Service + grant RBAC
    seed              Create/verify database and containers (no account creation)
    seed-data         Load default players and recordings into Azure CosmosDB
    clean             Delete the CosmosDB account (destructive)
    doctor / status   Account + containers + App Service health check
    test              Run CatanDb contract tests against Azure endpoint
    write-test-params Write .cosmos-test-params.json for Azure endpoint

Flags:
    -Local        Target local emulator (default when -Azure is not set)
    -Azure        Target Azure CosmosDB account
    -Yes          Auto-confirm destructive operations
    -Force        Force re-pull / re-create
    -Json         Output doctor/status as JSON
    -HashTable    Output doctor/status as PowerShell hashtable
    -TraceLevel   ERROR | WARN | INFO | DEBUG (default: INFO)

Examples:
    ./database.ps1 install
    ./database.ps1 doctor
    ./database.ps1 test
    ./database.ps1 install -Azure
    ./database.ps1 deploy -Azure
    ./database.ps1 doctor -Azure -Json
    ./database.ps1 test -Azure
    ./database.ps1 clean -Azure -Yes
"@
    Write-Host $text
}

# ════════════════════════════════════════════════════════════════════════════════
# MAIN DISPATCH
# ════════════════════════════════════════════════════════════════════════════════

try {
    if ($Help -or $Verb -eq "help") { Show-Help; exit 0 }

    # ── AZURE ───────────────────────────────────────────────────────────────────
    if ($Azure) {
        if (-not (Assert-AzCli)) { exit 1 }

        $config      = Get-AzureConfig
        $accountName = Get-CosmosAccountName -Config $config
        $endpoint    = Get-CosmosEndpointUrl  -AccountName $accountName

        switch ($Verb) {

            "install" {
                Write-Log -Level "HEADER" -Message "INSTALLING AZURE COSMOSDB DATABASE"

                # Run doctor first — only do what's actually missing.
                Write-Log -Level "INFO" -Message "Checking current state..."
                $dr = Get-AzureDoctorResult -Config $config -AccountName $accountName

                # 1. Account
                if (-not $dr.AccountExists -or $dr.AccountState -ne "Succeeded") {
                    if (-not (Install-AzureCosmosAccount -Config $config -AccountName $accountName -Force:$Force)) { exit 1 }
                } else {
                    Write-Log -Level "INFO" -Message "Account '$accountName' exists and is ready."
                }

                # 2. Database + containers (Invoke-AzureSeed checks each before creating)
                if (-not $dr.DatabaseExists -or -not $dr.AllContainersOk) {
                    if (-not (Invoke-AzureSeed -Config $config -AccountName $accountName)) { exit 1 }
                } else {
                    Write-Log -Level "INFO" -Message "Database and all containers already exist."
                }

                # Persist account name and endpoint to config for future commands
                if (-not $config.cosmosDb) {
                    $config | Add-Member -NotePropertyName cosmosDb -NotePropertyValue ([pscustomobject]@{}) -Force
                }
                $config.cosmosDb | Add-Member -NotePropertyName accountName -NotePropertyValue $accountName -Force
                $config.cosmosDb | Add-Member -NotePropertyName databaseName -NotePropertyValue $DatabaseName -Force
                $config.cosmosDb | Add-Member -NotePropertyName endpoint -NotePropertyValue $endpoint -Force
                Save-AzureConfig -Config $config

                # 3. App Service wiring (COSMOS_ENDPOINT + RBAC for App Service MI AND signed-in developer).
                # Always run — the doctor only checks App Service RBAC; the developer role is verified here.
                # Deploy-AzureDatabase is fully idempotent (check-then-set for all operations).
                Write-Log -Level "INFO" -Message "Verifying App Service wiring and RBAC (idempotent)..."
                if (-not (Deploy-AzureDatabase -Config $config -AccountName $accountName -Endpoint $endpoint)) {
                    Write-Log -Level "WARN" -Message "App Service wiring failed — run 'database.ps1 deploy -Azure' manually."
                }

                # 4. Firewall — ensure current developer IP is in the CosmosDB allow-list.
                # Re-read FirewallOk from a fresh doctor call after RBAC/deploy may have changed things.
                if (-not $dr.FirewallOk) {
                    Write-Log -Level "INFO" -Message "CosmosDB firewall does not allow $($dr.CurrentIp) — fixing..."
                    if (-not (Grant-CosmosFirewallAccess -AccountName $accountName -ResourceGroup $config.resourceGroup)) {
                        Write-Log -Level "WARN" -Message "Firewall update failed — seeding may fail with HTTP 403."
                    }
                } else {
                    Write-Log -Level "INFO" -Message "Firewall already allows $($dr.CurrentIp)."
                }

                # 5. Seed data (always run — upserts are idempotent).
                # Invoke-SeedDefaultData probes RBAC write access internally and waits up to 10 min.
                if (-not (Invoke-SeedDefaultData -IsAzure -Config $config -AccountName $accountName)) { exit 1 }

                Write-Log -Level "INFO" -Message "Azure CosmosDB installed. Endpoint: $endpoint"
                $result = Get-AzureDoctorResult -Config $config -AccountName $accountName
                Show-AzureDoctorOutput -Result $result
                exit $(if ($result.Status -eq "Ready") { 0 } else { 1 })
            }

            "seed-data" {
                Write-Log -Level "HEADER" -Message "SEEDING DEFAULT DATA (AZURE)"
                $dr = Get-AzureDoctorResult -Config $config -AccountName $accountName
                if (-not $dr.AccountExists -or -not $dr.DatabaseExists -or -not $dr.AllContainersOk) {
                    Write-Log -Level "ERROR" -Message "CosmosDB is not fully installed. Run: database.ps1 install -Azure"
                    Show-AzureDoctorOutput -Result $dr
                    exit 1
                }
                if (-not $dr.FirewallOk) {
                    Write-Log -Level "ERROR" -Message "Firewall blocks $($dr.CurrentIp). Run: database.ps1 install -Azure (it will fix the firewall)."
                    exit 1
                }
                if (-not (Invoke-SeedDefaultData -IsAzure -Config $config -AccountName $accountName)) { exit 1 }
                exit 0
            }

            "nuke-containers" {
                # Debug verb: delete all containers then recreate them, without touching the account or RBAC.
                # Useful for testing different container creation options without a full clean+reinstall.
                Write-Log -Level "HEADER" -Message "NUKING AND RECREATING CONTAINERS"
                $rg = $config.resourceGroup
                foreach ($name in $Containers.Keys) {
                    Write-Log -Level "INFO" -Message "  Deleting container '$name'..."
                    $null = Invoke-Azure cosmosdb sql container delete `
                        --account-name $accountName --resource-group $rg `
                        --database-name $DatabaseName --name $name --yes
                    if ($LASTEXITCODE -ne 0) {
                        Write-Log -Level "WARN" -Message "  Delete failed (may not exist) — continuing."
                    }
                }
                Write-Log -Level "INFO" -Message "Recreating containers (no --partition-key-version flag)..."
                foreach ($name in $Containers.Keys) {
                    $partKey = $Containers[$name]
                    Write-Log -Level "INFO" -Message "  Creating '$name' ($partKey) — ~30s..."
                    $null = Invoke-Azure cosmosdb sql container create `
                        --account-name $accountName --resource-group $rg `
                        --database-name $DatabaseName --name $name `
                        --partition-key-path $partKey --output none
                    if ($LASTEXITCODE -ne 0) {
                        Write-Log -Level "ERROR" -Message "  Failed to create '$name'."
                        exit 1
                    }
                    # Dump full partition key from management plane
                    $full = Invoke-Azure cosmosdb sql container show `
                        --account-name $accountName --resource-group $rg `
                        --database-name $DatabaseName --name $name `
                        --query "resource.partitionKey" --output json
                    Write-Log -Level "INFO" -Message "  '$name' partitionKey: $($full -replace '\s+', ' ')"
                }
                Write-Log -Level "INFO" -Message "Done. Run 'seed-data -Azure' to test writing."
                exit 0
            }

            "deploy" {
                Write-Log -Level "HEADER" -Message "DEPLOYING AZURE COSMOSDB TO APP SERVICE"
                if (-not (Deploy-AzureDatabase -Config $config -AccountName $accountName -Endpoint $endpoint)) { exit 1 }
                exit 0
            }

            "seed" {
                Write-Log -Level "HEADER" -Message "SEEDING AZURE COSMOSDB DATABASE"
                if (-not (Invoke-AzureSeed -Config $config -AccountName $accountName)) { exit 1 }
                exit 0
            }

            "clean" {
                Write-Log -Level "HEADER" -Message "CLEANING AZURE COSMOSDB DATABASE"
                if (-not $Yes) {
                    $answer = Read-Host "Delete CosmosDB account '$accountName' and ALL data? [y/N]"
                    if ($answer -notmatch '^[Yy]') {
                        Write-Log -Level "INFO" -Message "Clean cancelled."
                        exit 0
                    }
                }
                Write-Log -Level "INFO" -Message "Deleting CosmosDB account '$accountName' (this takes 2-5 minutes)..."
                $null = Invoke-Azure cosmosdb delete `
                    --name $accountName `
                    --resource-group $config.resourceGroup `
                    --yes `
                    --output none
                if ($LASTEXITCODE -ne 0) {
                    Write-Log -Level "ERROR" -Message "Failed to delete account (exit $LASTEXITCODE)."
                    exit 1
                }
                Write-Log -Level "INFO" -Message "Account '$accountName' deleted."
                exit 0
            }

            { $_ -in "doctor", "status" } {
                $result = Get-AzureDoctorResult -Config $config -AccountName $accountName
                if ($HashTable) { return $result }
                if ($Json)      { return ($result | ConvertTo-Json -Depth 5) }
                Show-AzureDoctorOutput -Result $result
                exit $(if ($result.Status -eq "Ready") { 0 } else { 1 })
            }

            "test" {
                Write-Log -Level "HEADER" -Message "RUNNING CATANDB CONTRACT TESTS (AZURE)"
                exit (Invoke-AzureTests -Endpoint $endpoint)
            }

            "write-test-params" {
                Write-AzureTestParams -Endpoint $endpoint
                exit 0
            }

            "start" {
                Write-Log -Level "INFO" -Message "Azure CosmosDB is a managed service — no start/stop needed."
                exit 0
            }

            "stop" {
                Write-Log -Level "INFO" -Message "Azure CosmosDB is a managed service — no start/stop needed."
                exit 0
            }
        }
        exit 0
    }

    # ── LOCAL ───────────────────────────────────────────────────────────────────
    switch ($Verb) {

        "install" {
            Write-Log -Level "HEADER" -Message "INSTALLING COSMOSDB EMULATOR"
            if (-not (Assert-Docker -Yes:$Yes -Force:$Force)) { exit 1 }
            if (-not (Invoke-PullImage -Force:$Force))        { exit 1 }
            if (-not (Start-Emulator -Force:$Force))          { exit 1 }
            Invoke-LocalSeed
            if (-not (Invoke-SeedDefaultData)) { exit 1 }
            Write-Log -Level "INFO" -Message "Install complete. Emulator ready at $EmulatorEndpoint"
            exit 0
        }

        "seed-data" {
            Write-Log -Level "HEADER" -Message "SEEDING DEFAULT DATA (LOCAL)"
            if ((Get-EmulatorState) -ne "running") {
                Write-Log -Level "ERROR" -Message "Emulator is not running. Run: database.ps1 start"
                exit 1
            }
            if (-not (Invoke-SeedDefaultData)) { exit 1 }
            exit 0
        }

        "seed" {
            Write-Log -Level "HEADER" -Message "SEEDING COSMOSDB DATABASE"
            if ((Get-EmulatorState) -ne "running") {
                Write-Log -Level "ERROR" -Message "Emulator is not running. Run: database.ps1 start"
                exit 1
            }
            Invoke-LocalSeed
            exit 0
        }

        "start" {
            Write-Log -Level "HEADER" -Message "STARTING COSMOSDB EMULATOR"
            if (-not (Assert-Docker -Yes:$Yes -Force:$Force)) { exit 1 }
            if (-not (Start-Emulator -Force:$Force))          { exit 1 }
            exit 0
        }

        "stop" {
            Write-Log -Level "HEADER" -Message "STOPPING COSMOSDB EMULATOR"
            if (Test-DockerCli) { Stop-Emulator } else { Write-Log -Level "WARN" -Message "Docker not found." }
            exit 0
        }

        "clean" {
            Write-Log -Level "HEADER" -Message "CLEANING COSMOSDB EMULATOR"
            if (-not (Test-DockerCli)) { Write-Log -Level "WARN" -Message "Docker not found."; exit 0 }
            if (-not (Invoke-LocalClean -Yes:$Yes)) { exit 1 }
            exit 0
        }

        { $_ -in "doctor", "status" } {
            $result = Get-LocalDoctorResult
            if ($HashTable) { return $result }
            if ($Json)      { return ($result | ConvertTo-Json -Depth 5) }
            Show-LocalDoctorOutput -Result $result
            exit $(if ($result.Status -eq "Ready") { 0 } else { 1 })
        }

        "test" {
            Write-Log -Level "HEADER" -Message "RUNNING CATANDB CONTRACT TESTS"
            exit (Invoke-LocalTests)
        }

        "write-test-params" {
            # Single source of emulator endpoint/key constants — called by catan.ps1 test
            Write-LocalTestParams
            exit 0
        }

        "deploy" {
            Write-Log -Level "INFO" -Message "'deploy' only applies to Azure. Use: database.ps1 deploy -Azure"
            exit 1
        }
    }
} catch {
    Write-Log -Level "ERROR" -Message "Fatal: $_"
    Write-Log -Level "DEBUG" -Message $_.ScriptStackTrace
    exit 1
}
