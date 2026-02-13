<#
.SYNOPSIS
    Unified entry point for Catan3 development and deployment.

.DESCRIPTION
    Manages local development (build, run, test), database, dependencies, and Azure deployment.
    By default, builds and runs the React UI (Next.js) with GameService.

.PARAMETER Verb
    The action to perform: run, stop, build, test, doctor, install, database, azure, etc.

.PARAMETER Network
    Bind services to all network interfaces (0.0.0.0) instead of localhost only.
    Use this to access services from iPhone simulator or other devices on the network.

.PARAMETER Razor
    Build and run the Blazor WebUI instead of React UI.

.PARAMETER Desktop
    Also build and deploy the Windows Desktop app (MSIX).

.EXAMPLE
    ./catan.ps1 run              # Build GameService + React UI, start services, launch browser
    ./catan.ps1 run -Network     # Same, but accessible from other devices
    ./catan.ps1 run -Razor       # Build GameService + Blazor WebUI instead of React
    ./catan.ps1 run -Desktop     # Also build/deploy Windows Desktop app
    ./catan.ps1 stop             # Stop running services
    ./catan.ps1 build            # Build GameService + React UI
    ./catan.ps1 build -Desktop   # Build everything including Desktop app
    ./catan.ps1 test             # Run all tests
    ./catan.ps1 doctor           # Check dependencies and database health
    ./catan.ps1 install          # Install dependencies and database
    ./catan.ps1 clean            # Clean build artifacts (preserves database)
    ./catan.ps1 database doctor  # Database diagnostics
    ./catan.ps1 azure deploy     # Deploy to Azure
#>

[CmdletBinding()]
param(
    [Parameter(Position = 0)]
    [string]$Verb = "run",

    [Parameter(Position = 1)]
    [string]$SubCommand,

    [Parameter(Position = 2)]
    [string]$Target,

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
    [switch]$Network,

    [Parameter()]
    [switch]$Local,

    [Parameter()]
    [switch]$Help,

    [Parameter()]
    [switch]$Terminate,

    [Parameter()]
    [switch]$Azure,

    [Parameter()]
    [string]$Name,

    [Parameter()]
    [string]$Location,

    [Parameter()]
    [string]$File,

    [Parameter()]
    [switch]$Replace,

    [Parameter()]
    [switch]$NoBuild,

    [Parameter()]
    [switch]$Razor,

    [Parameter()]
    [switch]$Desktop,

    [Parameter()]
    [switch]$All,

    [Parameter()]
    [string]$Slot,

    [Parameter()]
    [string]$AzureGameServiceUrl,

    [Parameter(ValueFromRemainingArguments = $true)]
    [string[]]$RemainingArgs
)

$ErrorActionPreference = "Stop"

# Platform detection (use built-in automatic variables in PS Core, fallback for PS 5.1)
if (-not (Test-Path variable:IsMacOS)) { $script:IsMacOS = $false }
if (-not (Test-Path variable:IsLinux)) { $script:IsLinux = $false }
if (-not (Test-Path variable:IsWindows)) { $script:IsWindows = $true }

# Check for unknown arguments (typos, etc.)
if ($RemainingArgs) {
    Write-Host "Unknown argument(s): $($RemainingArgs -join ', ')" -ForegroundColor Red
    Write-Host ""
    $Verb = "help"
}

$GameServicePort = 8080
$WebUIPort = 5296
$ReactUIPort = 3000
$GameServiceUrl = "http://localhost:$GameServicePort"
$WebUIUrl = "http://localhost:$WebUIPort"
$ReactUIUrl = "http://localhost:$ReactUIPort"
$DatabasePath = Join-Path $PSScriptRoot "Catan3.GameService\Data\catan.db"
$PidFile = Join-Path $PSScriptRoot ".webui-pids.json"

function Test-PortInUse {
    param([int]$Port)
    if ($IsWindows) {
        # Use netstat instead of Get-NetTCPConnection (which can hang)
        $result = netstat -ano 2>$null | Select-String ":$Port\s.*LISTENING"
        return $null -ne $result
    } else {
        # macOS/Linux: use lsof to check if port is in use
        $result = lsof -i ":$Port" 2>$null
        return $null -ne $result -and $result.Count -gt 0
    }
}

function Stop-ProcessOnPort {
    param([int]$Port)
    if ($IsWindows) {
        # Use netstat to find PID
        $netstatOutput = netstat -ano 2>$null | Select-String ":$Port\s.*LISTENING"
        foreach ($match in $netstatOutput) {
            if ($match -match '\s+(\d+)\s*$') {
                $processId = [int]$Matches[1]
                if ($processId -and $processId -ne 0) {
                    Stop-Process -Id $processId -Force -ErrorAction SilentlyContinue
                }
            }
        }
    } else {
        # macOS/Linux: use lsof
        $pids = lsof -ti ":$Port" 2>$null
        foreach ($procId in $pids) {
            if ($procId) {
                Stop-Process -Id $procId -Force -ErrorAction SilentlyContinue
            }
        }
    }
}

function Wait-ForService {
    param(
        [string]$Url,
        [int]$TimeoutSeconds = 30
    )

    $startTime = Get-Date
    while (((Get-Date) - $startTime).TotalSeconds -lt $TimeoutSeconds) {
        try {
            $response = Invoke-WebRequest -Uri $Url -Method Get -TimeoutSec 2 -ErrorAction SilentlyContinue
            if ($response.StatusCode -eq 200) {
                return $true
            }
        }
        catch {
            # Service not ready yet
        }
        Start-Sleep -Milliseconds 500
    }
    return $false
}

function Save-Pids {
    param(
        [int]$GameServicePid = 0,
        [int]$WebUIPid = 0
    )

    $pids = @{}
    if (Test-Path $PidFile) {
        try {
            $existing = Get-Content $PidFile -Raw | ConvertFrom-Json
            if ($existing.GameService) { $pids["GameService"] = $existing.GameService }
            if ($existing.WebUI) { $pids["WebUI"] = $existing.WebUI }
        } catch { }
    }

    if ($GameServicePid -gt 0) { $pids["GameService"] = $GameServicePid }
    if ($WebUIPid -gt 0) { $pids["WebUI"] = $WebUIPid }

    $pids | ConvertTo-Json | Set-Content $PidFile
}

function Get-SavedPids {
    if (Test-Path $PidFile) {
        return Get-Content $PidFile | ConvertFrom-Json -AsHashtable
    }
    return @{}
}

function Start-GameService {
    param([switch]$NetworkBinding)

    Write-Host "Starting GameService..." -ForegroundColor Cyan

    $gameServicePath = Join-Path $PSScriptRoot "Catan3.GameService"

    if ($IsWindows) {
        $urlsArg = if ($NetworkBinding) { " --urls `"http://0.0.0.0:$GameServicePort`"" } else { "" }
        $process = Start-Process pwsh -ArgumentList "-NoExit", "-Command", "cd '$gameServicePath'; dotnet run$urlsArg" -WindowStyle Normal -PassThru
        Save-Pids -GameServicePid $process.Id
    } else {
        # macOS: Open new Terminal window - use single quotes for URL to avoid AppleScript escaping issues
        $urlsArg = if ($NetworkBinding) { " --urls 'http://0.0.0.0:$GameServicePort'" } else { "" }
        $pidFile = Join-Path $PSScriptRoot ".gameservice.pid"
        $script = "cd '$gameServicePath' && echo `$`$ > '$pidFile' && dotnet run$urlsArg"
        & osascript -e "tell application `"Terminal`" to do script `"$script`""
        # Wait for PID file to be created
        Start-Sleep -Milliseconds 500
        if (Test-Path $pidFile) {
            $procId = [int](Get-Content $pidFile).Trim()
            Save-Pids -GameServicePid $procId
        }
    }

    Write-Host "Waiting for GameService..." -ForegroundColor Yellow
    Wait-ForService -Url "$GameServiceUrl/health" -TimeoutSeconds 30 | Out-Null
    Write-Host "GameService running at $GameServiceUrl" -ForegroundColor Green
}

function Start-WebUI {
    param([switch]$NetworkBinding)

    Write-Host "Starting WebUI..." -ForegroundColor Cyan

    $webUIPath = Join-Path $PSScriptRoot "WebUI"

    if ($IsWindows) {
        $urlsArg = if ($NetworkBinding) { " --urls `"http://0.0.0.0:$WebUIPort`"" } else { "" }
        # When using network binding, suppress dotnet's browser launch (it tries to open 0.0.0.0 which fails)
        # We'll manually open localhost instead
        $envPrefix = if ($NetworkBinding) { "`$env:DOTNET_WATCH_SUPPRESS_LAUNCH_BROWSER='true'; " } else { "" }
        $process = Start-Process pwsh -ArgumentList "-NoExit", "-Command", "${envPrefix}cd '$webUIPath'; dotnet watch run$urlsArg" -WindowStyle Normal -PassThru
        Save-Pids -WebUIPid $process.Id
        if ($NetworkBinding) {
            # Open browser with localhost since 0.0.0.0 won't work in browser
            Start-Sleep -Seconds 2  # Give the service a moment to start
            Start-Process "http://localhost:$WebUIPort"
        }
    } else {
        # macOS: Open new Terminal window - use single quotes for URL to avoid AppleScript escaping issues
        $urlsArg = if ($NetworkBinding) { " --urls 'http://0.0.0.0:$WebUIPort'" } else { "" }
        $pidFile = Join-Path $PSScriptRoot ".webui.pid"
        $script = "cd '$webUIPath' && echo `$`$ > '$pidFile' && dotnet watch run$urlsArg"
        & osascript -e "tell application `"Terminal`" to do script `"$script`""
        # Wait for PID file to be created
        Start-Sleep -Milliseconds 500
        if (Test-Path $pidFile) {
            $procId = [int](Get-Content $pidFile).Trim()
            Save-Pids -WebUIPid $procId
        }
    }

    Write-Host "Waiting for WebUI..." -ForegroundColor Yellow
    Wait-ForService -Url $WebUIUrl -TimeoutSeconds 30 | Out-Null
    Write-Host "WebUI running at $WebUIUrl" -ForegroundColor Green
}

function Start-ReactUI {
    Write-Host "Starting React UI..." -ForegroundColor Cyan

    $reactUIPath = Join-Path $PSScriptRoot "react-ui"

    # Check if react-ui directory exists
    if (-not (Test-Path $reactUIPath)) {
        Write-Host "React UI directory not found at $reactUIPath" -ForegroundColor Red
        return
    }

    # Check if npm install is needed (missing node_modules or package.json changed)
    $nodeModulesPath = Join-Path $reactUIPath "node_modules"
    $packageJsonPath = Join-Path $reactUIPath "package.json"
    $needsInstall = $false
    if (-not (Test-Path $nodeModulesPath)) {
        Write-Host "node_modules not found. Running npm install..." -ForegroundColor Yellow
        $needsInstall = $true
    } elseif ((Get-Item $packageJsonPath).LastWriteTime -gt (Get-Item $nodeModulesPath).LastWriteTime) {
        Write-Host "package.json is newer than node_modules. Running npm install..." -ForegroundColor Yellow
        $needsInstall = $true
    }
    if ($needsInstall) {
        Push-Location $reactUIPath
        try {
            & npm install
            if ($LASTEXITCODE -ne 0) {
                Write-Host "npm install failed!" -ForegroundColor Red
                return
            }
        } finally {
            Pop-Location
        }
    }

    if ($IsWindows) {
        $null = Start-Process pwsh -ArgumentList "-NoExit", "-Command", "cd '$reactUIPath'; npm run dev" -WindowStyle Normal -PassThru
        # Note: Not saving PID to pidFile since React runs on different port
    } else {
        # macOS: Open new Terminal window
        $script = "cd '$reactUIPath' && npm run dev"
        & osascript -e "tell application `"Terminal`" to do script `"$script`""
    }

    Write-Host "Waiting for React UI..." -ForegroundColor Yellow
    Wait-ForService -Url $ReactUIUrl -TimeoutSeconds 30 | Out-Null
    Write-Host "React UI running at $ReactUIUrl" -ForegroundColor Green

    # Open browser to React UI
    Start-Process $ReactUIUrl
}

function Install-Database {
    Write-Host "Installing database..." -ForegroundColor Cyan

    $dataDir = Split-Path $DatabasePath -Parent

    # Create Data directory if it doesn't exist
    if (-not (Test-Path $dataDir)) {
        Write-Host "Creating Data directory..." -ForegroundColor Yellow
        New-Item -ItemType Directory -Path $dataDir -Force | Out-Null
    }

    # Run the database seed tool
    $gameServicePath = Join-Path $PSScriptRoot "Catan3.GameService"

    Push-Location $gameServicePath
    try {
        # Use dotnet run with a seed argument to initialize the database
        Write-Host "Seeding database with default data..." -ForegroundColor Yellow
        $result = & dotnet run -- --seed-database 2>&1
        if ($LASTEXITCODE -ne 0) {
            Write-Host "Database installation failed!" -ForegroundColor Red
            Write-Host $result -ForegroundColor Red
            return $false
        }
        Write-Host "Database installed successfully at $DatabasePath" -ForegroundColor Green
        return $true
    }
    finally {
        Pop-Location
    }
}

function Initialize-Database {
    Write-Host "Checking database..." -ForegroundColor Cyan

    # Check if database exists
    if (Test-Path $DatabasePath) {
        Write-Host "Database exists at $DatabasePath" -ForegroundColor Green
        return $true
    }

    Write-Host "Database not found. Installing..." -ForegroundColor Yellow
    return Install-Database
}

function Clear-Database {
    Write-Host "Cleaning database..." -ForegroundColor Yellow

    if (Test-Path $DatabasePath) {
        Remove-Item $DatabasePath -Force
        Write-Host "Database deleted: $DatabasePath" -ForegroundColor Green
    }
    else {
        Write-Host "Database not found (already clean)" -ForegroundColor Gray
    }

    # Also remove any SQLite journal/wal files
    $dbDir = Split-Path $DatabasePath -Parent
    if (Test-Path $dbDir) {
        Get-ChildItem $dbDir -Filter "*.db-*" | Remove-Item -Force
    }
}

function Test-DatabaseSchema {
    # Run GameService tests that validate the database schema
    # Returns $true if schema is valid, $false otherwise

    $testProject = Join-Path $PSScriptRoot "Tests\GameService\Tests.GameService.csproj"

    if (-not (Test-Path $testProject)) {
        return $null  # Can't verify - test project not found
    }

    # Run tests with a filter for database-related tests
    & dotnet test $testProject --filter "Category=Database" --verbosity quiet --nologo 2>&1 | Out-Null
    return ($LASTEXITCODE -eq 0)
}

function Invoke-DatabaseDoctor {
    param(
        [switch]$UseApi  # Force using API instead of sqlite3
    )

    Write-Host ""
    Write-Host "Database Doctor" -ForegroundColor Cyan
    Write-Host "===============" -ForegroundColor Cyan
    Write-Host ""

    $healthy = $true
    $needsGames = $false

    # Check if GameService is running - prefer API for authoritative results
    $serviceRunning = Test-PortInUse -Port $GameServicePort

    if ($serviceRunning -or $UseApi) {
        Write-Host "Checking via GameService API..." -ForegroundColor Yellow

        try {
            $healthResponse = Invoke-RestMethod -Uri "$GameServiceUrl/api/database/health" -Method Get -TimeoutSec 5

            if ($healthResponse.healthy) {
                Write-Host "  [OK] Database is healthy" -ForegroundColor Green
                Write-Host "  Players: $($healthResponse.playerCount)" -ForegroundColor Gray
                Write-Host "  Games:   $($healthResponse.gameCount)" -ForegroundColor Gray

                if ($healthResponse.needsSeeding) {
                    Write-Host "  [WARN] No players configured - run './catan.ps1 database install'" -ForegroundColor Yellow
                    $healthy = $false
                }

                if ($healthResponse.needsGames) {
                    Write-Host "  [INFO] No saved games in database" -ForegroundColor Cyan
                    $needsGames = $true
                }
            }
            else {
                Write-Host "  [FAIL] Database is unhealthy: $($healthResponse.error)" -ForegroundColor Red
                $healthy = $false
            }

            # Return object with status
            return @{
                Healthy = $healthy
                NeedsGames = $needsGames
                PlayerCount = $healthResponse.playerCount
                GameCount = $healthResponse.gameCount
            }
        }
        catch {
            Write-Host "  [FAIL] Could not reach health endpoint: $_" -ForegroundColor Red
            if (-not $serviceRunning) {
                Write-Host "  GameService is not running - start with './catan.ps1 run'" -ForegroundColor Yellow
            }
            return @{ Healthy = $false; NeedsGames = $true; PlayerCount = 0; GameCount = 0 }
        }
    }

    # Fallback: Check database file exists
    Write-Host "Checking database file..." -ForegroundColor Yellow
    if (Test-Path $DatabasePath) {
        $fileInfo = Get-Item $DatabasePath
        Write-Host "  [OK] Database exists: $DatabasePath ($([math]::Round($fileInfo.Length / 1024, 1)) KB)" -ForegroundColor Green
    }
    else {
        Write-Host "  [FAIL] Database not found: $DatabasePath" -ForegroundColor Red
        Write-Host "  Fix: Run './catan.ps1 database install'" -ForegroundColor Yellow
        return @{ Healthy = $false; NeedsGames = $true; PlayerCount = 0; GameCount = 0 }
    }

    # Try to find sqlite3 for offline inspection
    Write-Host ""
    Write-Host "Checking database tables..." -ForegroundColor Yellow

    $sqlite3 = $null
    if ($IsWindows) {
        $possiblePaths = @(
            "sqlite3.exe",
            "C:\ProgramData\chocolatey\bin\sqlite3.exe",
            "$env:LOCALAPPDATA\Microsoft\WinGet\Packages\*\sqlite3.exe"
        )
        foreach ($path in $possiblePaths) {
            if (Get-Command $path -ErrorAction SilentlyContinue) {
                $sqlite3 = $path
                break
            }
        }
    }
    else {
        if (Get-Command sqlite3 -ErrorAction SilentlyContinue) {
            $sqlite3 = "sqlite3"
        }
    }

    $playerCount = 0
    $gameCount = 0

    if ($sqlite3) {
        $tables = & $sqlite3 $DatabasePath ".tables" 2>&1
        $requiredTables = @("Players", "Images", "GameSaveData", "GameSaveMetadata")

        foreach ($table in $requiredTables) {
            if ($tables -match $table) {
                Write-Host "  [OK] Table exists: $table" -ForegroundColor Green
            }
            else {
                Write-Host "  [FAIL] Table missing: $table" -ForegroundColor Red
                $healthy = $false
            }
        }

        Write-Host ""
        Write-Host "Checking table contents..." -ForegroundColor Yellow

        $playerCountResult = & $sqlite3 $DatabasePath "SELECT COUNT(*) FROM Players;" 2>&1
        if ($playerCountResult -match '^\d+$') {
            $playerCount = [int]$playerCountResult
            if ($playerCount -gt 0) {
                Write-Host "  [OK] Players: $playerCount players configured" -ForegroundColor Green
            }
            else {
                Write-Host "  [WARN] Players: No players configured" -ForegroundColor Yellow
                $healthy = $false
            }
        }

        $metadataCountResult = & $sqlite3 $DatabasePath "SELECT COUNT(*) FROM GameSaveMetadata;" 2>&1
        if ($metadataCountResult -match '^\d+$') {
            $gameCount = [int]$metadataCountResult
            Write-Host "  [INFO] Saved games: $gameCount game(s) in database" -ForegroundColor Cyan
            $needsGames = ($gameCount -eq 0)

            if ($gameCount -gt 0) {
                Write-Host ""
                Write-Host "Recent saved games:" -ForegroundColor Yellow
                # Set column widths: GameId(36), GameState(25), PlayerNames(35), TurnCount(9), SavedAt(19)
                $recentGames = & $sqlite3 $DatabasePath -header -column -cmd ".width 36 25 35 9 19" "SELECT GameId, GameState, PlayerNames, TurnCount, datetime(SavedAt) as SavedAt FROM GameSaveMetadata ORDER BY SavedAt DESC LIMIT 5;" 2>&1
                Write-Host $recentGames -ForegroundColor Gray
            }
        }
    }
    else {
        Write-Host "  [SKIP] sqlite3 not found - cannot verify tables" -ForegroundColor Yellow
        Write-Host ""
        Write-Host "To check database health, either:" -ForegroundColor Yellow
        Write-Host "  1. Start GameService: ./catan.ps1 run" -ForegroundColor White
        Write-Host "  2. Install sqlite3 for offline checks" -ForegroundColor White
        Write-Host ""
        Write-Host "Database health: UNKNOWN (cannot verify)" -ForegroundColor Yellow
        return @{
            Healthy = $true  # Assume healthy if file exists, let service verify
            NeedsGames = $true  # Assume games needed
            PlayerCount = 0
            GameCount = 0
        }
    }

    # Schema validation
    Write-Host ""
    Write-Host "Checking database schema..." -ForegroundColor Yellow
    $schemaValid = Test-DatabaseSchema
    if ($null -eq $schemaValid) {
        Write-Host "  [SKIP] Test project not found - cannot validate schema" -ForegroundColor Yellow
    }
    elseif ($schemaValid) {
        Write-Host "  [OK] Schema validation passed" -ForegroundColor Green
    }
    else {
        Write-Host "  [FAIL] Schema validation failed" -ForegroundColor Red
        $healthy = $false
    }

    # Summary
    Write-Host ""
    if ($healthy) {
        Write-Host "Database health: GOOD" -ForegroundColor Green
    }
    else {
        Write-Host "Database health: ISSUES FOUND" -ForegroundColor Red
        Write-Host ""
        Write-Host "To fix, run:" -ForegroundColor Yellow
        Write-Host "  ./catan.ps1 database install" -ForegroundColor White
    }
    Write-Host ""

    return @{
        Healthy = $healthy
        NeedsGames = $needsGames
        PlayerCount = $playerCount
        GameCount = $gameCount
        SchemaValid = $schemaValid
    }
}

function Import-DefaultGames {
    $defaultGamesPath = Join-Path $PSScriptRoot "Catan3.GameService\Default Data\Games"

    if (-not (Test-Path $defaultGamesPath)) {
        return  # No games folder, nothing to import
    }

    $catanFiles = Get-ChildItem -Path $defaultGamesPath -Filter "*.catan" -ErrorAction SilentlyContinue

    if ($catanFiles.Count -eq 0) {
        return  # No .catan files, nothing to import
    }

    Write-Host ""
    Write-Host "Importing default games..." -ForegroundColor Cyan

    # Wait for service to be ready (in case we just started it)
    $ready = Wait-ForService -Url "$GameServiceUrl/health" -TimeoutSeconds 10
    if (-not $ready) {
        Write-Host "  [WARN] GameService not responding, skipping game import" -ForegroundColor Yellow
        return
    }

    Write-Host "  Found $($catanFiles.Count) game file(s)" -ForegroundColor Yellow

    foreach ($file in $catanFiles) {
        try {
            Write-Host "  Importing: $($file.Name)..." -ForegroundColor Gray -NoNewline

            # Use PowerShell 7's -Form parameter for proper multipart handling
            $form = @{
                file = Get-Item -Path $file.FullName
            }

            $response = Invoke-RestMethod -Uri "$GameServiceUrl/api/game/import" `
                -Method Post `
                -Form $form `
                -TimeoutSec 30

            if ($response.success) {
                if ($response.message -eq "Game already exists") {
                    Write-Host " already exists" -ForegroundColor Gray
                }
                else {
                    Write-Host " imported ($($response.playerNames), $($response.turnCount) turns)" -ForegroundColor Green
                }
            }
            else {
                Write-Host " failed: $($response.error)" -ForegroundColor Red
            }
        }
        catch {
            Write-Host " error: $_" -ForegroundColor Red
        }
    }

    Write-Host ""
}

function Stop-ChildProcesses {
    param([int]$ParentPid)

    if ($IsWindows) {
        $children = Get-CimInstance Win32_Process -Filter "ParentProcessId=$ParentPid" -ErrorAction SilentlyContinue
        foreach ($child in $children) {
            Stop-ChildProcesses -ParentPid $child.ProcessId  # Recursive
            Stop-Process -Id $child.ProcessId -Force -ErrorAction SilentlyContinue
        }
    } else {
        # macOS/Linux: use pgrep to find child processes
        $children = pgrep -P $ParentPid 2>$null
        foreach ($childPid in $children) {
            if ($childPid) {
                Stop-ChildProcesses -ParentPid $childPid  # Recursive
                Stop-Process -Id $childPid -Force -ErrorAction SilentlyContinue
            }
        }
    }
}

function Stop-Services {
    Write-Host "Stopping services..." -ForegroundColor Yellow

    $killedCount = 0

    if ($IsWindows) {
        # Use netstat instead of Get-NetTCPConnection (which can hang)
        # netstat -ano returns: Proto  LocalAddress  ForeignAddress  State  PID
        $netstatOutput = netstat -ano 2>$null | Select-String "LISTENING"

        foreach ($port in @($GameServicePort, $WebUIPort, $ReactUIPort)) {
            $portMatches = $netstatOutput | Where-Object { $_ -match ":$port\s" }
            foreach ($match in $portMatches) {
                # Extract PID from the last column
                if ($match -match '\s+(\d+)\s*$') {
                    $processId = [int]$Matches[1]
                    if ($processId -and $processId -ne 0) {
                        try {
                            Stop-Process -Id $processId -Force -ErrorAction Stop
                            Write-Host "  Killed process $processId (port $port)" -ForegroundColor Gray
                            $killedCount++
                        } catch {
                            # Process may have already exited
                        }
                    }
                }
            }
        }

        if ($killedCount -eq 0) {
            Write-Host "  No services running" -ForegroundColor Gray
        }
    } else {
        # macOS: Kill processes and close Terminal windows

        # Kill processes by port
        $gameServicePids = lsof -ti ":$GameServicePort" 2>$null
        foreach ($procId in $gameServicePids) {
            if ($procId) {
                Stop-ChildProcesses -ParentPid $procId
                Stop-Process -Id $procId -Force -ErrorAction SilentlyContinue
                Write-Host "  Killed process $procId (GameService port)" -ForegroundColor Gray
                $killedCount++
            }
        }

        $webUIPids = lsof -ti ":$WebUIPort" 2>$null
        foreach ($procId in $webUIPids) {
            if ($procId) {
                Stop-ChildProcesses -ParentPid $procId
                Stop-Process -Id $procId -Force -ErrorAction SilentlyContinue
                Write-Host "  Killed process $procId (WebUI port)" -ForegroundColor Gray
                $killedCount++
            }
        }

        $reactUIPids = lsof -ti ":$ReactUIPort" 2>$null
        foreach ($procId in $reactUIPids) {
            if ($procId) {
                Stop-ChildProcesses -ParentPid $procId
                Stop-Process -Id $procId -Force -ErrorAction SilentlyContinue
                Write-Host "  Killed process $procId (React UI port)" -ForegroundColor Gray
                $killedCount++
            }
        }

        # Kill any dotnet processes running GameService or WebUI
        $procIds = pgrep -f "Catan3.GameService|Catan3.WebUI|dotnet.*watch.*run" 2>$null
        foreach ($procId in $procIds) {
            if ($procId) {
                Stop-Process -Id $procId -Force -ErrorAction SilentlyContinue
                Write-Host "  Killed process $procId" -ForegroundColor Gray
                $killedCount++
            }
        }

        # Close Terminal windows that are running our commands
        $closeScript = @'
tell application "Terminal"
    set windowsToClose to {}
    repeat with w in windows
        repeat with t in tabs of w
            set tabProcs to processes of t
            repeat with p in tabProcs
                if p contains "dotnet" then
                    set end of windowsToClose to w
                    exit repeat
                end if
            end repeat
        end repeat
    end repeat
    repeat with w in windowsToClose
        close w
    end repeat
end tell
'@
        & osascript -e $closeScript 2>$null

        if ($killedCount -gt 0) {
            Write-Host "  Killed $killedCount process(es)" -ForegroundColor Gray
        }
    }

    # Clean up PID files
    if (Test-Path $PidFile) {
        Remove-Item $PidFile -Force
    }
    # Clean up macOS pid files
    $gameServicePidFile = Join-Path $PSScriptRoot ".gameservice.pid"
    $webUIPidFile = Join-Path $PSScriptRoot ".webui.pid"
    if (Test-Path $gameServicePidFile) { Remove-Item $gameServicePidFile -Force }
    if (Test-Path $webUIPidFile) { Remove-Item $webUIPidFile -Force }

    # Wait for ports to be released
    Start-Sleep -Milliseconds 500

    # Verify ports are free
    $gameStillRunning = Test-PortInUse -Port $GameServicePort
    $webUIStillRunning = Test-PortInUse -Port $WebUIPort
    $reactUIStillRunning = Test-PortInUse -Port $ReactUIPort

    if ($gameStillRunning -or $webUIStillRunning -or $reactUIStillRunning) {
        Write-Host "  Waiting for ports to be released..." -ForegroundColor Yellow
        Start-Sleep -Seconds 2
    }

    Write-Host "Services stopped." -ForegroundColor Green
}

# Handle -Help switch (redirect to help verb)
if ($Help) {
    $Verb = "help"
}

switch ($Verb) {
    "build" {
        Write-Host "Building solution..." -ForegroundColor Cyan
        if ($Desktop) {
            # Full build including Desktop app
            & "$PSScriptRoot\.scripts\build.ps1" -NoTest
        } else {
            # Skip Desktop app (default - faster for web development)
            & "$PSScriptRoot\.scripts\build.ps1" -NoTest -NoDesktop
        }
        if ($LASTEXITCODE -ne 0) {
            Write-Host "Build failed!" -ForegroundColor Red
            exit 1
        }
        Write-Host "Build completed successfully!" -ForegroundColor Green

        # Generate TypeScript types (if react-ui exists)
        $reactUiPath = Join-Path $PSScriptRoot "react-ui"
        if (Test-Path $reactUiPath) {
            Write-Host ""
            Write-Host "Generating TypeScript types..." -ForegroundColor Yellow

            # Generate model types from C# using TypeGenRunner (TypeGen 7.0.0)
            $typegenRunnerProject = Join-Path $PSScriptRoot "Catan3.Shared\TypeScript\TypeGenRunner\TypeGenRunner.csproj"
            $null = & dotnet run --project $typegenRunnerProject --no-build 2>&1
            if ($LASTEXITCODE -eq 0) {
                Write-Host "  [OK] Model types updated (TypeGen 7.0.0)" -ForegroundColor Green
            } else {
                Write-Host "  [WARN] TypeGen failed (non-blocking)" -ForegroundColor Yellow
            }
        }
    }

    "test" {
        Write-Host "Running tests..." -ForegroundColor Cyan

        # Stop services first to avoid file locking issues during build
        Stop-Services

        # Run .NET tests
        Write-Host ""
        Write-Host "Running .NET tests..." -ForegroundColor Yellow
        & "$PSScriptRoot\.scripts\build.ps1"
        if ($LASTEXITCODE -ne 0) {
            Write-Host ".NET tests failed!" -ForegroundColor Red
            exit 1
        }
        Write-Host "  [OK] .NET tests passed" -ForegroundColor Green

        # Run TypeScript tests (if react-ui exists)
        $reactUiPath = Join-Path $PSScriptRoot "react-ui"
        if (Test-Path $reactUiPath) {
            Write-Host ""
            Write-Host "Running TypeScript tests..." -ForegroundColor Yellow
            Push-Location $reactUiPath
            try {
                $tsTestResult = & npm run test:run 2>&1
                if ($LASTEXITCODE -ne 0) {
                    Write-Host "  [FAIL] TypeScript tests failed:" -ForegroundColor Red
                    Write-Host $tsTestResult -ForegroundColor Gray
                    exit 1
                }
                Write-Host "  [OK] TypeScript tests passed" -ForegroundColor Green
            }
            finally {
                Pop-Location
            }
        }

        Write-Host ""
        Write-Host "All tests passed!" -ForegroundColor Green
    }

    "lint" {
        # Smart linting - by default only lint changed files
        # Use 'lint -All' or 'lint all' to lint entire codebase
        # SubCommand can be: all (lint everything), or a type filter: cs, ts, md, json, ps1, spell
        $lintScript = Join-Path $PSScriptRoot ".scripts\lint.ps1"
        $lintArgs = @{}

        # -All switch or 'all' subcommand
        if ($All -or $SubCommand -eq "all") {
            $lintArgs.All = $true
        }

        # Type filter subcommand
        if ($SubCommand -in @("cs", "ts", "md", "json", "ps1", "spell")) {
            $lintArgs.Type = $SubCommand
        }
        elseif ($SubCommand -and $SubCommand -ne "" -and $SubCommand -ne "all") {
            Write-Host "Unknown lint type: $SubCommand" -ForegroundColor Red
            Write-Host "Valid: ./catan.ps1 lint [-All] [all|cs|ts|md|json|ps1|spell]" -ForegroundColor Yellow
            exit 1
        }

        & $lintScript @lintArgs
        exit $LASTEXITCODE
    }

    "format" {
        # Auto-format source files
        # Use 'format -All' or 'format all' to format entire codebase
        # SubCommand can be: all, check, cs, ts
        $formatScript = Join-Path $PSScriptRoot ".scripts\format.ps1"
        $formatArgs = @{}

        # -All switch or 'all' subcommand
        if ($All -or $SubCommand -eq "all") {
            $formatArgs.All = $true
        }

        # 'check' subcommand
        if ($SubCommand -eq "check") {
            $formatArgs.Check = $true
        }

        # Type filter subcommand
        if ($SubCommand -in @("cs", "ts")) {
            $formatArgs.Type = $SubCommand
        }
        elseif ($SubCommand -and $SubCommand -ne "" -and $SubCommand -notin @("all", "check")) {
            Write-Host "Unknown format type: $SubCommand" -ForegroundColor Red
            Write-Host "Valid: ./catan.ps1 format [-All] [all|check|cs|ts]" -ForegroundColor Yellow
            exit 1
        }

        & $formatScript @formatArgs
        exit $LASTEXITCODE
    }

    "generate-types" {
        Write-Host "Generating TypeScript types from C# models..." -ForegroundColor Cyan

        $reactUiPath = Join-Path $PSScriptRoot "react-ui"
        if (-not (Test-Path $reactUiPath)) {
            Write-Host "react-ui directory not found!" -ForegroundColor Red
            exit 1
        }

        # Generate model types from C# using TypeGenRunner (TypeGen 7.0.0)
        $typegenRunnerProject = Join-Path $PSScriptRoot "Catan3.Shared\TypeScript\TypeGenRunner\TypeGenRunner.csproj"
        Write-Host ""
        Write-Host "Running TypeGenRunner..." -ForegroundColor Yellow

        $typegenOutput = & dotnet run --project $typegenRunnerProject 2>&1
        if ($LASTEXITCODE -ne 0) {
            Write-Host "  [FAIL] TypeGen failed:" -ForegroundColor Red
            Write-Host $typegenOutput -ForegroundColor Gray
            exit 1
        }

        # Count generated files from output
        $fileCount = ($typegenOutput | Select-String "Generated \d+ files").Matches.Value -replace "Generated | files", ""
        Write-Host "  [OK] Generated $fileCount TypeScript files to react-ui/types/generated/models/" -ForegroundColor Green

        Write-Host ""
        Write-Host "TypeScript types generated successfully!" -ForegroundColor Green
    }

    "replay" {
        # Redirect to recording replay for backward compatibility
        Write-Host "Note: 'replay' command moved to 'recording replay'. Redirecting..." -ForegroundColor DarkYellow
        $Verb = "recording"
        $SubCommand = "replay"
        # Re-invoke with the new verb (fall through to recording handler won't work, so we call it directly)
        & $PSCommandPath recording replay -Name:$Name -Azure:$Azure -Local:$Local
        exit $LASTEXITCODE
    }

    # ==============================================================================
    # Stats Management Commands
    # ==============================================================================
    "stats" {
        # SubVerb is the subcommand (list, export, import, reset)
        $SubVerb = $SubCommand

        # Determine target URL (local or Azure)
        $targetUrl = $GameServiceUrl
        $targetName = "Local"

        if ($Azure) {
            $azureConfigFile = Join-Path $PSScriptRoot ".azure\catan-azure.json"
            if (-not (Test-Path $azureConfigFile)) {
                Write-Host "Azure configuration not found. Run './catan.ps1 azure install' first." -ForegroundColor Red
                exit 1
            }
            $azureConfig = Get-Content $azureConfigFile -Raw | ConvertFrom-Json
            $targetUrl = $azureConfig.gameService.url
            $targetName = "Azure"
        }

        # Default location for stats export
        $defaultStatsDir = Join-Path $PSScriptRoot "."

        switch ($SubVerb) {
            "" {
                # Show stats help
                Write-Host ""
                Write-Host "Stats Management Commands" -ForegroundColor Cyan
                Write-Host "=========================" -ForegroundColor Cyan
                Write-Host ""
                Write-Host "Usage: ./catan.ps1 stats <subcommand> [options]" -ForegroundColor Yellow
                Write-Host ""
                Write-Host "Subcommands:"
                Write-Host "  list     - Show all player statistics"
                Write-Host "  export   - Export stats to JSON file"
                Write-Host "  import   - Import stats from JSON file"
                Write-Host "  reset    - Delete all statistics"
                Write-Host ""
                Write-Host "Options:"
                Write-Host "  -Local        Target local GameService (default)"
                Write-Host "  -Azure        Target Azure GameService"
                Write-Host "  -Json         Output as JSON (for list)"
                Write-Host "  -Location     File path for export"
                Write-Host "  -File         File path for import (required)"
                Write-Host "  -Replace      Replace all stats with imported data"
                Write-Host "  -Yes          Skip confirmation prompts"
                Write-Host ""
                Write-Host "Examples:" -ForegroundColor Yellow
                Write-Host "  ./catan.ps1 stats list                       - List local player stats"
                Write-Host "  ./catan.ps1 stats list -Azure                - List Azure player stats"
                Write-Host "  ./catan.ps1 stats export                     - Export local stats"
                Write-Host "  ./catan.ps1 stats export -Azure              - Export Azure stats"
                Write-Host "  ./catan.ps1 stats import -File stats.json    - Import and merge"
                Write-Host "  ./catan.ps1 stats import -File stats.json -Replace  - Replace all"
                Write-Host "  ./catan.ps1 stats reset -Yes                 - Reset local stats"
                Write-Host ""
            }

            "list" {
                Write-Host ""
                Write-Host "Player Statistics ($targetName)" -ForegroundColor Cyan
                Write-Host "========================" -ForegroundColor Cyan
                Write-Host ""

                # Check if service is reachable
                if (-not $Azure -and -not (Test-PortInUse $GameServicePort)) {
                    Write-Host "GameService is not running on port $GameServicePort" -ForegroundColor Red
                    Write-Host "Start services first with: ./catan.ps1 run" -ForegroundColor Yellow
                    exit 1
                }

                try {
                    $stats = Invoke-RestMethod -Uri "$targetUrl/api/stats" -Method Get -TimeoutSec 30

                    if ($Json) {
                        $stats | ConvertTo-Json -Depth 10
                    } else {
                        if ($stats.Count -eq 0) {
                            Write-Host "No player statistics found." -ForegroundColor Yellow
                        } else {
                            # Table header
                            Write-Host ("{0,-15} {1,6} {2,5} {3,6} {4,6} {5,8}" -f "Player", "Games", "Wins", "Win%", "Best", "AvgStars") -ForegroundColor Gray
                            Write-Host ("{0,-15} {1,6} {2,5} {3,6} {4,6} {5,8}" -f "---------------", "------", "-----", "------", "------", "--------") -ForegroundColor Gray

                            foreach ($player in $stats) {
                                $winRate = if ($player.gamesPlayed -gt 0) { "{0:N1}%" -f $player.winRate } else { "0.0%" }
                                $avgStars = "{0:N1}" -f $player.averageStars
                                Write-Host ("{0,-15} {1,6} {2,5} {3,6} {4,6} {5,8}" -f $player.playerName, $player.gamesPlayed, $player.wins, $winRate, $player.highestScore, $avgStars)
                            }

                            Write-Host ""
                            Write-Host "Total: $($stats.Count) players" -ForegroundColor Green
                        }
                    }
                } catch {
                    Write-Host "Failed to fetch stats: $_" -ForegroundColor Red
                    exit 1
                }
            }

            "export" {
                Write-Host ""
                Write-Host "Exporting Player Statistics ($targetName)" -ForegroundColor Cyan
                Write-Host "========================================" -ForegroundColor Cyan
                Write-Host ""

                # Check if service is reachable
                if (-not $Azure -and -not (Test-PortInUse $GameServicePort)) {
                    Write-Host "GameService is not running on port $GameServicePort" -ForegroundColor Red
                    Write-Host "Start services first with: ./catan.ps1 run" -ForegroundColor Yellow
                    exit 1
                }

                # Determine output path
                $timestamp = Get-Date -Format "yyyy-MM-dd-HHmm"
                $outputPath = if ($Location) { $Location } else { Join-Path $defaultStatsDir "player-stats-$timestamp.json" }

                # If Location is a directory, add filename
                if (Test-Path $outputPath -PathType Container) {
                    $outputPath = Join-Path $outputPath "player-stats-$timestamp.json"
                }

                try {
                    $export = Invoke-RestMethod -Uri "$targetUrl/api/stats/export" -Method Get -TimeoutSec 30
                    $export | ConvertTo-Json -Depth 10 | Out-File -FilePath $outputPath -Encoding UTF8

                    Write-Host "Exported $($export.players.Count) player(s) to:" -ForegroundColor Green
                    Write-Host "  $outputPath" -ForegroundColor Gray
                } catch {
                    Write-Host "Failed to export stats: $_" -ForegroundColor Red
                    exit 1
                }
            }

            "import" {
                Write-Host ""
                Write-Host "Importing Player Statistics to $targetName" -ForegroundColor Cyan
                Write-Host "===========================================" -ForegroundColor Cyan
                Write-Host ""

                # Check if service is reachable
                if (-not $Azure -and -not (Test-PortInUse $GameServicePort)) {
                    Write-Host "GameService is not running on port $GameServicePort" -ForegroundColor Red
                    Write-Host "Start services first with: ./catan.ps1 run" -ForegroundColor Yellow
                    exit 1
                }

                if (-not $File) {
                    Write-Host "ERROR: -File parameter is required for import" -ForegroundColor Red
                    Write-Host "Usage: ./catan.ps1 stats import -File <path>" -ForegroundColor Yellow
                    exit 1
                }

                if (-not (Test-Path $File)) {
                    Write-Host "ERROR: File not found: $File" -ForegroundColor Red
                    exit 1
                }

                try {
                    $document = Get-Content $File -Raw | ConvertFrom-Json
                    Write-Host "Found $($document.players.Count) player(s) in file" -ForegroundColor Green

                    $mode = if ($Replace) { "replace" } else { "merge" }
                    Write-Host "Import mode: $mode" -ForegroundColor Gray
                    Write-Host ""

                    $importRequest = @{
                        document = $document
                        replace = $Replace.IsPresent
                    }

                    $response = Invoke-RestMethod -Uri "$targetUrl/api/stats/import" `
                        -Method Post `
                        -ContentType "application/json" `
                        -Body ($importRequest | ConvertTo-Json -Depth 10) `
                        -TimeoutSec 30

                    Write-Host "Import complete:" -ForegroundColor Green
                    Write-Host "  Imported: $($response.imported)" -ForegroundColor Gray
                    Write-Host "  Merged:   $($response.merged)" -ForegroundColor Gray
                    Write-Host "  Skipped:  $($response.skipped)" -ForegroundColor Gray
                } catch {
                    Write-Host "Failed to import stats: $_" -ForegroundColor Red
                    exit 1
                }
            }

            "reset" {
                Write-Host ""
                Write-Host "Reset Player Statistics ($targetName)" -ForegroundColor Cyan
                Write-Host "=====================================" -ForegroundColor Cyan
                Write-Host ""

                # Check if service is reachable
                if (-not $Azure -and -not (Test-PortInUse $GameServicePort)) {
                    Write-Host "GameService is not running on port $GameServicePort" -ForegroundColor Red
                    Write-Host "Start services first with: ./catan.ps1 run" -ForegroundColor Yellow
                    exit 1
                }

                if (-not $Yes) {
                    Write-Host "WARNING: This will delete ALL player statistics on $targetName!" -ForegroundColor Red
                    $confirm = Read-Host "Type 'yes' to confirm"
                    if ($confirm -ne "yes") {
                        Write-Host "Aborted." -ForegroundColor Yellow
                        exit 0
                    }
                }

                try {
                    $response = Invoke-RestMethod -Uri "$targetUrl/api/stats" -Method Delete -TimeoutSec 30
                    Write-Host "Reset complete: $($response.playersReset) player(s) reset" -ForegroundColor Green
                } catch {
                    Write-Host "Failed to reset stats: $_" -ForegroundColor Red
                    exit 1
                }
            }

            default {
                Write-Host "Unknown stats subcommand: $SubVerb" -ForegroundColor Red
                Write-Host "Run './catan.ps1 stats' for help" -ForegroundColor Yellow
                exit 1
            }
        }
    }

    "recording" {
        # Determine target URL (local or Azure)
        $targetUrl = $GameServiceUrl
        $targetName = "Local"

        if ($Azure) {
            $azureConfigFile = Join-Path $PSScriptRoot ".azure\catan-azure.json"
            if (-not (Test-Path $azureConfigFile)) {
                Write-Host "Azure configuration not found. Run './catan.ps1 azure install' first." -ForegroundColor Red
                exit 1
            }
            $azureConfig = Get-Content $azureConfigFile -Raw | ConvertFrom-Json
            $targetUrl = $azureConfig.gameService.url
            $targetName = "Azure"
        }

        # Default recordings directory
        $defaultRecordingsDir = Join-Path $PSScriptRoot "Catan3.GameService\Default Data\Recordings"
        $recordingsDir = if ($Location) { $Location } else { $defaultRecordingsDir }

        switch ($SubCommand) {
            "list" {
                Write-Host ""
                Write-Host "Recordings ($targetName)" -ForegroundColor Cyan
                Write-Host "==================" -ForegroundColor Cyan
                Write-Host ""

                # Check if service is reachable
                if (-not $Azure -and -not (Test-PortInUse $GameServicePort)) {
                    Write-Host "GameService is not running on port $GameServicePort" -ForegroundColor Red
                    Write-Host "Start services first with: ./catan.ps1 run" -ForegroundColor Yellow
                    exit 1
                }

                try {
                    $recordings = Invoke-RestMethod -Uri "$targetUrl/api/recordings" -Method Get -TimeoutSec 30
                } catch {
                    Write-Host "Failed to fetch recordings from $targetUrl : $_" -ForegroundColor Red
                    exit 1
                }

                if ($recordings.Count -eq 0) {
                    Write-Host "No recordings found." -ForegroundColor Yellow
                    exit 0
                }

                if ($Json) {
                    $recordings | ConvertTo-Json -Depth 10
                } else {
                    # Table format with RecordingID and GameID columns
                    # Show first 8 chars of each GUID for readability
                    Write-Host ("{0,-10} {1,-10} {2,-22} {3,-8} {4,-8} {5}" -f "RecID", "GameID", "Name", "Actions", "Players", "Type")
                    Write-Host ("{0,-10} {1,-10} {2,-22} {3,-8} {4,-8} {5}" -f "----------", "----------", "----------------------", "--------", "--------", "---------")
                    foreach ($r in $recordings) {
                        $displayName = if ($r.name.Length -gt 21) { $r.name.Substring(0, 18) + "..." } else { $r.name }
                        $shortRecId = if ($r.id.Length -ge 8) { $r.id.Substring(0, 8) } else { $r.id }
                        $shortGameId = if ($r.gameId -and $r.gameId.Length -ge 8) { $r.gameId.Substring(0, 8) } else { $r.gameId }
                        Write-Host ("{0,-10} {1,-10} {2,-22} {3,-8} {4,-8} {5}" -f $shortRecId, $shortGameId, $displayName, $r.actionCount, $r.playerCount, $r.gameType)
                    }
                }
                Write-Host ""
                Write-Host "Total: $($recordings.Count) recording(s)" -ForegroundColor Green
            }

            "save" {
                Write-Host ""
                Write-Host "Saving Recordings ($targetName)" -ForegroundColor Cyan
                Write-Host "========================" -ForegroundColor Cyan
                Write-Host ""

                # Check if service is reachable
                if (-not $Azure -and -not (Test-PortInUse $GameServicePort)) {
                    Write-Host "GameService is not running on port $GameServicePort" -ForegroundColor Red
                    Write-Host "Start services first with: ./catan.ps1 run" -ForegroundColor Yellow
                    exit 1
                }

                # Ensure output directory exists
                if (-not (Test-Path $recordingsDir)) {
                    New-Item -ItemType Directory -Path $recordingsDir -Force | Out-Null
                    Write-Host "Created directory: $recordingsDir" -ForegroundColor Yellow
                }

                # Fetch recordings list
                try {
                    $recordings = Invoke-RestMethod -Uri "$targetUrl/api/recordings" -Method Get -TimeoutSec 30
                } catch {
                    Write-Host "Failed to fetch recordings: $_" -ForegroundColor Red
                    exit 1
                }

                # Filter by name if specified
                if ($Name) {
                    $recordings = $recordings | Where-Object { $_.name -like $Name }
                }

                if ($recordings.Count -eq 0) {
                    Write-Host "No recordings found matching criteria." -ForegroundColor Yellow
                    exit 0
                }

                Write-Host "Found $($recordings.Count) recording(s)" -ForegroundColor Green
                Write-Host "Saving to: $recordingsDir" -ForegroundColor Gray
                Write-Host ""

                $saved = 0
                foreach ($recording in $recordings) {
                    $safeName = $recording.name -replace '[^\w\-\.]', '-'
                    $filePath = Join-Path $recordingsDir "$safeName.json"

                    Write-Host "  Saving: $($recording.name)..." -ForegroundColor Gray -NoNewline

                    # Fetch full recording data
                    try {
                        $fullRecording = Invoke-RestMethod -Uri "$targetUrl/api/recording/$($recording.id)" -Method Get -TimeoutSec 30
                    } catch {
                        Write-Host " failed to fetch: $_" -ForegroundColor Red
                        continue
                    }

                    $exportObj = @{
                        id = $fullRecording.id
                        name = $fullRecording.name
                        createdAt = $fullRecording.createdAt
                        gameType = $fullRecording.gameType
                        playerCount = $fullRecording.playerCount
                        playerIds = $fullRecording.playerIds
                        actionCount = $fullRecording.actionCount
                        data = $fullRecording.data
                    }

                    $exportObj | ConvertTo-Json -Depth 10 -Compress | Set-Content -Path $filePath -Encoding UTF8
                    Write-Host " saved" -ForegroundColor Green
                    $saved++
                }

                Write-Host ""
                Write-Host "Saved $saved recording(s) to: $recordingsDir" -ForegroundColor Green
            }

            "load" {
                Write-Host ""
                Write-Host "Loading Recordings to $targetName" -ForegroundColor Cyan
                Write-Host "==========================" -ForegroundColor Cyan
                Write-Host ""

                # Check if service is reachable
                if (-not $Azure -and -not (Test-PortInUse $GameServicePort)) {
                    Write-Host "GameService is not running on port $GameServicePort" -ForegroundColor Red
                    Write-Host "Start services first with: ./catan.ps1 run" -ForegroundColor Yellow
                    exit 1
                }

                # For Azure, verify service is healthy and import endpoint exists
                if ($Azure) {
                    Write-Host "Checking Azure service health..." -NoNewline
                    try {
                        $null = Invoke-RestMethod -Uri "$targetUrl/health" -TimeoutSec 10
                        Write-Host " OK" -ForegroundColor Green
                    } catch {
                        Write-Host " FAILED" -ForegroundColor Red
                        Write-Host "Azure service is not responding at $targetUrl/health" -ForegroundColor Red
                        Write-Host "Check deployment status or try again later." -ForegroundColor Yellow
                        exit 1
                    }

                    # Verify import endpoint exists by checking recordings list first
                    Write-Host "Checking import endpoint..." -NoNewline
                    try {
                        # A simple GET to /api/recordings verifies the recording endpoints are deployed
                        $null = Invoke-RestMethod -Uri "$targetUrl/api/recordings" -TimeoutSec 10
                        Write-Host " OK" -ForegroundColor Green
                    } catch {
                        Write-Host " FAILED" -ForegroundColor Red
                        Write-Host "Recording API not available. Deployment may be in progress." -ForegroundColor Red
                        Write-Host "Wait for deployment to complete and try again." -ForegroundColor Yellow
                        exit 1
                    }
                    Write-Host ""
                }

                if (-not (Test-Path $recordingsDir)) {
                    Write-Host "Recordings directory not found: $recordingsDir" -ForegroundColor Red
                    Write-Host "Run './catan.ps1 recording save' first to export recordings" -ForegroundColor Yellow
                    exit 1
                }

                # Get JSON files
                $jsonFiles = Get-ChildItem -Path $recordingsDir -Filter "*.json" -ErrorAction SilentlyContinue

                # Filter by name if specified
                if ($Name) {
                    $jsonFiles = $jsonFiles | Where-Object { $_.Name -like $Name }
                }

                if ($jsonFiles.Count -eq 0) {
                    Write-Host "No recording files found in $recordingsDir" -ForegroundColor Yellow
                    exit 0
                }

                Write-Host "Found $($jsonFiles.Count) recording file(s)" -ForegroundColor Green
                Write-Host "Loading to: $targetUrl" -ForegroundColor Gray
                Write-Host ""

                $imported = 0
                $skipped = 0
                $failed = 0

                foreach ($file in $jsonFiles) {
                    Write-Host "  Loading: $($file.Name)..." -NoNewline

                    try {
                        $recording = Get-Content $file.FullName -Raw | ConvertFrom-Json

                        $importRequest = @{
                            id = $recording.id
                            name = $recording.name
                            createdAt = $recording.createdAt
                            gameType = $recording.gameType
                            playerCount = $recording.playerCount
                            playerIds = $recording.playerIds
                            actionCount = $recording.actionCount
                            data = $recording.data
                        }

                        $response = Invoke-RestMethod -Uri "$targetUrl/api/recording/import" `
                            -Method Post `
                            -ContentType "application/json" `
                            -Body ($importRequest | ConvertTo-Json -Depth 10) `
                            -TimeoutSec 30

                        Write-Host " imported" -ForegroundColor Green
                        $imported++
                    }
                    catch {
                        if ($_.Exception.Response.StatusCode -eq 409) {
                            Write-Host " already exists" -ForegroundColor Gray
                            $skipped++
                        } else {
                            Write-Host " failed: $_" -ForegroundColor Red
                            $failed++
                        }
                    }
                }

                Write-Host ""
                Write-Host "Results: $imported imported, $skipped skipped, $failed failed" -ForegroundColor $(if ($failed -eq 0) { "Green" } else { "Red" })
            }

            "delete" {
                if (-not $Name) {
                    Write-Host "Error: -Name parameter is required for delete" -ForegroundColor Red
                    Write-Host "Usage: ./catan.ps1 recording delete -Name <name-or-id>" -ForegroundColor Yellow
                    exit 1
                }

                Write-Host ""
                Write-Host "Deleting Recording from $targetName" -ForegroundColor Cyan
                Write-Host "=============================" -ForegroundColor Cyan
                Write-Host ""

                # Check if service is reachable
                if (-not $Azure -and -not (Test-PortInUse $GameServicePort)) {
                    Write-Host "GameService is not running on port $GameServicePort" -ForegroundColor Red
                    Write-Host "Start services first with: ./catan.ps1 run" -ForegroundColor Yellow
                    exit 1
                }

                # Find recording by name or ID
                try {
                    $recordings = Invoke-RestMethod -Uri "$targetUrl/api/recordings" -Method Get -TimeoutSec 30
                } catch {
                    Write-Host "Failed to fetch recordings: $_" -ForegroundColor Red
                    exit 1
                }

                $toDelete = $recordings | Where-Object { $_.name -eq $Name -or $_.id -eq $Name }

                if ($toDelete.Count -eq 0) {
                    Write-Host "Recording not found: $Name" -ForegroundColor Red
                    exit 1
                }

                if ($toDelete.Count -gt 1) {
                    Write-Host "Multiple recordings match '$Name'. Please use the full ID:" -ForegroundColor Red
                    foreach ($r in $toDelete) {
                        Write-Host "  $($r.id) - $($r.name)" -ForegroundColor Gray
                    }
                    exit 1
                }

                $recording = $toDelete[0]

                if (-not $Yes) {
                    Write-Host "About to delete: $($recording.name) ($($recording.id))" -ForegroundColor Yellow
                    $confirm = Read-Host "Are you sure? (y/N)"
                    if ($confirm -ne 'y' -and $confirm -ne 'Y') {
                        Write-Host "Cancelled." -ForegroundColor Gray
                        exit 0
                    }
                }

                try {
                    Invoke-RestMethod -Uri "$targetUrl/api/recording/$($recording.id)" -Method Delete -TimeoutSec 30 | Out-Null
                    Write-Host "Deleted: $($recording.name)" -ForegroundColor Green
                } catch {
                    Write-Host "Failed to delete: $_" -ForegroundColor Red
                    exit 1
                }
            }

            "replay" {
                Write-Host ""
                Write-Host "Running Recording Replay Tests ($targetName)" -ForegroundColor Cyan
                Write-Host "======================================" -ForegroundColor Cyan
                Write-Host ""

                # Check if service is reachable
                if (-not $Azure -and -not (Test-PortInUse $GameServicePort)) {
                    Write-Host "GameService is not running on port $GameServicePort" -ForegroundColor Red
                    Write-Host "Start services first with: ./catan.ps1 run" -ForegroundColor Yellow
                    exit 1
                }

                # For Azure, verify service is healthy first
                if ($Azure) {
                    Write-Host "Checking Azure service health..." -NoNewline
                    try {
                        $null = Invoke-RestMethod -Uri "$targetUrl/health" -TimeoutSec 10
                        Write-Host " OK" -ForegroundColor Green
                    } catch {
                        Write-Host " FAILED" -ForegroundColor Red
                        Write-Host "Azure service is not responding at $targetUrl/health" -ForegroundColor Red
                        Write-Host "Check deployment status or try again later." -ForegroundColor Yellow
                        exit 1
                    }
                    Write-Host ""
                }

                # Get all recordings
                try {
                    $recordings = Invoke-RestMethod -Uri "$targetUrl/api/recordings" -Method Get -TimeoutSec 30
                } catch {
                    Write-Host "Failed to fetch recordings: $_" -ForegroundColor Red
                    exit 1
                }

                # Filter by name if specified
                if ($Name) {
                    $recordings = $recordings | Where-Object { $_.name -like $Name }
                }

                if ($recordings.Count -eq 0) {
                    Write-Host "No recordings found. Create some recordings first!" -ForegroundColor Yellow
                    exit 0
                }

                Write-Host "Found $($recordings.Count) recording(s)" -ForegroundColor Green
                Write-Host ""

                $passed = 0
                $failed = 0
                $failedTests = @()

                foreach ($recording in $recordings) {
                    Write-Host "  Running: $($recording.name) ($($recording.actionCount) actions)... " -NoNewline

                    try {
                        $result = Invoke-RestMethod -Uri "$targetUrl/api/recording/$($recording.id)/replay" -Method Post -TimeoutSec 120

                        if ($result.success) {
                            Write-Host "PASS" -ForegroundColor Green
                            $passed++
                        } else {
                            Write-Host "FAIL" -ForegroundColor Red
                            $failed++
                            $errorMsg = if ($result.failedAtAction) {
                                "Failed at action $($result.failedAtAction): $($result.errorMessage)"
                            } else {
                                $result.errorMessage
                            }
                            $failedTests += @{
                                Name = $recording.name
                                Error = $errorMsg
                                Expected = $result.expectedHash
                                Actual = $result.actualHash
                            }
                        }
                    } catch {
                        Write-Host "ERROR" -ForegroundColor Red
                        $failed++
                        $failedTests += @{
                            Name = $recording.name
                            Error = $_.Exception.Message
                        }
                    }
                }

                Write-Host ""
                Write-Host "Results: $passed passed, $failed failed" -ForegroundColor $(if ($failed -eq 0) { "Green" } else { "Red" })

                if ($failedTests.Count -gt 0) {
                    Write-Host ""
                    Write-Host "Failed Tests:" -ForegroundColor Red
                    foreach ($test in $failedTests) {
                        Write-Host "  - $($test.Name): $($test.Error)" -ForegroundColor Red
                        if ($test.Expected -and $test.Actual) {
                            Write-Host "    Expected: $($test.Expected), Actual: $($test.Actual)" -ForegroundColor DarkGray
                        }
                    }
                    exit 1
                }

                Write-Host ""
                Write-Host "All recording replay tests passed!" -ForegroundColor Green
            }

            default {
                Write-Host ""
                Write-Host "Recording Management Commands" -ForegroundColor Cyan
                Write-Host "=============================" -ForegroundColor Cyan
                Write-Host ""
                Write-Host "Usage: ./catan.ps1 recording <subcommand> [options]" -ForegroundColor Yellow
                Write-Host ""
                Write-Host "Subcommands:" -ForegroundColor Yellow
                Write-Host "  list     - List all recordings"
                Write-Host "  save     - Save recordings to JSON files"
                Write-Host "  load     - Load recordings from JSON files"
                Write-Host "  delete   - Delete a recording"
                Write-Host "  replay   - Run replay tests"
                Write-Host ""
                Write-Host "Options:" -ForegroundColor Yellow
                Write-Host "  -Local        Target local GameService (default)"
                Write-Host "  -Azure        Target Azure GameService"
                Write-Host "  -Name <name>  Filter by recording name (supports wildcards)"
                Write-Host "  -Location     Directory for save/load (default: Default Data/Recordings/)"
                Write-Host "  -Json         Output as JSON (for list)"
                Write-Host "  -Yes          Skip confirmation prompts"
                Write-Host ""
                Write-Host "Examples:" -ForegroundColor Yellow
                Write-Host "  ./catan.ps1 recording list                    - List local recordings"
                Write-Host "  ./catan.ps1 recording list -Azure             - List Azure recordings"
                Write-Host "  ./catan.ps1 recording save                    - Save all local recordings"
                Write-Host "  ./catan.ps1 recording save -Azure             - Save all Azure recordings"
                Write-Host "  ./catan.ps1 recording load                    - Load recordings to local"
                Write-Host "  ./catan.ps1 recording load -Azure             - Load recordings to Azure"
                Write-Host "  ./catan.ps1 recording replay                  - Run all replay tests locally"
                Write-Host "  ./catan.ps1 recording replay -Azure           - Run replay tests on Azure"
                Write-Host "  ./catan.ps1 recording delete -Name 'Test*'    - Delete matching recording"
                Write-Host ""

                if ($SubCommand) {
                    Write-Host "Unknown subcommand: $SubCommand" -ForegroundColor Red
                    exit 1
                }
            }
        }
    }

    "doctor" {
        Write-Host ""
        Write-Host "Catan3 Health Check" -ForegroundColor Cyan
        Write-Host "===================" -ForegroundColor Cyan
        Write-Host ""

        # Check dependencies
        Write-Host "Checking dependencies..." -ForegroundColor Yellow
        & "$PSScriptRoot\.scripts\dependencies.ps1" -Doctor
        Write-Host ""

        # Check react-ui npm packages (Prettier, ESLint, etc.)
        $reactUiCheck = Join-Path $PSScriptRoot "react-ui"
        if (Test-Path $reactUiCheck) {
            Write-Host "Checking react-ui npm packages..." -ForegroundColor Yellow
            $nodeModules = Join-Path $reactUiCheck "node_modules"
            $prettierBin = Join-Path $nodeModules ".bin\prettier"
            $eslintBin = Join-Path $nodeModules ".bin\eslint"

            if (-not (Test-Path $nodeModules)) {
                Write-Host "  [WARN] node_modules not found. Run: cd react-ui && npm install" -ForegroundColor Yellow
            }
            else {
                $allGood = $true
                if (Test-Path $prettierBin) {
                    $prettierVer = & npx --prefix $reactUiCheck prettier --version 2>$null
                    Write-Host "  [OK] Prettier v$prettierVer" -ForegroundColor Green
                }
                else {
                    Write-Host "  [WARN] Prettier not found. Run: cd react-ui && npm install" -ForegroundColor Yellow
                    $allGood = $false
                }
                if (Test-Path $eslintBin) {
                    $eslintVer = & npx --prefix $reactUiCheck eslint --version 2>$null
                    Write-Host "  [OK] ESLint v$eslintVer" -ForegroundColor Green
                }
                else {
                    Write-Host "  [WARN] ESLint not found. Run: cd react-ui && npm install" -ForegroundColor Yellow
                    $allGood = $false
                }
                if ($allGood) {
                    Write-Host "  [OK] All react-ui npm packages installed" -ForegroundColor Green
                }
            }
            Write-Host ""
        }

        # Check database
        Write-Host "Checking database..." -ForegroundColor Yellow
        $dbStatus = Invoke-DatabaseDoctor
        if (-not $dbStatus.Healthy) {
            Write-Host ""
            Write-Host "Some issues found. Run './catan.ps1 install' to fix." -ForegroundColor Yellow
            exit 1
        }
    }

    "install" {
        Write-Host ""
        Write-Host "Catan3 Installation" -ForegroundColor Cyan
        Write-Host "===================" -ForegroundColor Cyan
        Write-Host ""

        # Install dependencies (dependencies.ps1 already checks if installed)
        Write-Host "Checking dependencies..." -ForegroundColor Yellow
        & "$PSScriptRoot\.scripts\dependencies.ps1" -Install -Yes:$Yes
        Write-Host ""

        # Check database status first
        Write-Host "Checking database..." -ForegroundColor Yellow
        $dbStatus = Invoke-DatabaseDoctor

        if ($dbStatus.Healthy -and -not $Force) {
            Write-Host "Database is already installed and healthy." -ForegroundColor Green
        } else {
            if ($Force) {
                Write-Host "Force flag set, reinstalling database..." -ForegroundColor Yellow
            } else {
                Write-Host "Database needs installation..." -ForegroundColor Yellow
            }
            Clear-Database
            $installed = Install-Database
            if (-not $installed) {
                Write-Host "Database installation failed!" -ForegroundColor Red
                exit 1
            }
        }
        Write-Host ""
        Write-Host "Installation complete!" -ForegroundColor Green
    }

    "run" {
        # Build based on flags:
        # - Default: Build Shared + GameService + React UI (skip Desktop)
        # - With -Razor: Build Shared + GameService + Blazor WebUI (skip Desktop)
        # - With -Desktop: Also build Desktop app
        Write-Host "Building..." -ForegroundColor Cyan

        if ($Desktop) {
            # Full build including Desktop app
            & "$PSScriptRoot\.scripts\build.ps1" -NoTest
        } else {
            # Skip Desktop app build (faster)
            & "$PSScriptRoot\.scripts\build.ps1" -NoTest -NoDesktop
        }

        if ($LASTEXITCODE -ne 0) {
            Write-Host "Build failed! Cannot start services." -ForegroundColor Red
            exit 1
        }
        Write-Host "Build completed successfully!" -ForegroundColor Green
        Write-Host ""

        # Ensure database is initialized
        if (-not (Initialize-Database)) {
            Write-Host "Failed to initialize database. Cannot start services." -ForegroundColor Red
            exit 1
        }

        # Check if GameService is already running AND responding
        $gameServiceRunning = $false
        if (Test-PortInUse -Port $GameServicePort) {
            # Port is in use - verify service is actually responding
            $responding = Wait-ForService -Url "$GameServiceUrl/health" -TimeoutSeconds 3
            if ($responding) {
                Write-Host "GameService already running on port $GameServicePort" -ForegroundColor Green
                $gameServiceRunning = $true
            }
            else {
                Write-Host "Port $GameServicePort in use but service not responding. Killing stale process..." -ForegroundColor Yellow
                Stop-ProcessOnPort -Port $GameServicePort
                Start-Sleep -Milliseconds 500
            }
        }

        if (-not $gameServiceRunning) {
            Start-GameService -NetworkBinding:$Network
        }

        # Start UI based on flags:
        # - Default: React UI (Next.js on port 3000)
        # - With -Razor: Blazor WebUI (port 5296)
        if ($Razor) {
            # Start Blazor WebUI
            $webUIRunning = $false
            if (Test-PortInUse -Port $WebUIPort) {
                $responding = Wait-ForService -Url $WebUIUrl -TimeoutSeconds 3
                if ($responding) {
                    Write-Host "Blazor WebUI already running on port $WebUIPort" -ForegroundColor Green
                    $webUIRunning = $true
                }
                else {
                    Write-Host "Port $WebUIPort in use but service not responding. Killing stale process..." -ForegroundColor Yellow
                    Stop-ProcessOnPort -Port $WebUIPort
                    Start-Sleep -Milliseconds 500
                }
            }

            if (-not $webUIRunning) {
                Start-WebUI -NetworkBinding:$Network
            }
        } else {
            # Start React UI (default)
            $reactUIRunning = $false
            if (Test-PortInUse -Port $ReactUIPort) {
                $responding = Wait-ForService -Url $ReactUIUrl -TimeoutSeconds 3
                if ($responding) {
                    Write-Host "React UI already running on port $ReactUIPort" -ForegroundColor Green
                    $reactUIRunning = $true
                }
                else {
                    Write-Host "Port $ReactUIPort in use but service not responding. Killing stale process..." -ForegroundColor Yellow
                    Stop-ProcessOnPort -Port $ReactUIPort
                    Start-Sleep -Milliseconds 500
                }
            }

            if (-not $reactUIRunning) {
                Start-ReactUI
            }
        }

        Write-Host ""
        Write-Host "Services running:" -ForegroundColor Green
        Write-Host "  GameService: $GameServiceUrl" -ForegroundColor White
        if ($Razor) {
            Write-Host "  Blazor UI:   $WebUIUrl" -ForegroundColor White
        } else {
            Write-Host "  React UI:    $ReactUIUrl" -ForegroundColor White
        }

        if ($Network) {
            # Get the local IP address for network access
            if ($IsMacOS -or $IsLinux) {
                $localIp = (ifconfig | grep "inet " | grep -v "127.0.0.1" | head -1 | awk '{print $2}') 2>$null
            } else {
                $localIp = (Get-NetIPAddress -AddressFamily IPv4 | Where-Object { $_.IPAddress -notlike "127.*" -and $_.PrefixOrigin -ne "WellKnown" } | Select-Object -First 1).IPAddress
            }
            if ($localIp) {
                Write-Host ""
                Write-Host "Network access (for iPhone simulator, other devices):" -ForegroundColor Cyan
                Write-Host "  WebUI:       http://${localIp}:$WebUIPort" -ForegroundColor White
            }
        }

        Write-Host ""
        if ($IsWindows) {
            Write-Host "Press Ctrl+C in service windows to stop them." -ForegroundColor Yellow
        } else {
            Write-Host "Use './catan.ps1 stop' to stop services. Logs: .gameservice.log, .webui.log" -ForegroundColor Yellow
        }
    }

    "debug" {
        Write-Host "Debug Mode" -ForegroundColor Cyan
        Write-Host ""
        Write-Host "For debugging, use VS Code with the configured launch profiles:" -ForegroundColor White
        Write-Host ""
        Write-Host "  1. Open VS Code in the Catan directory" -ForegroundColor Yellow
        Write-Host "  2. Press F5 or use Run > Start Debugging" -ForegroundColor Yellow
        Write-Host "  3. Select 'WebUI + GameService' compound configuration" -ForegroundColor Yellow
        Write-Host ""
        Write-Host "This will launch both services with debuggers attached." -ForegroundColor White
        Write-Host ""
        Write-Host "Alternatively, debug individual services:" -ForegroundColor White
        Write-Host "  - 'Debug GameService' - GameService only" -ForegroundColor Gray
        Write-Host "  - 'Debug WebUI' - WebUI only" -ForegroundColor Gray
        Write-Host "  - 'Debug WebUI (with GameService)' - WebUI with GameService pre-started" -ForegroundColor Gray
        Write-Host ""

        # Offer to open VS Code
        $openVSCode = Read-Host "Open VS Code now? (y/n)"
        if ($openVSCode -eq 'y') {
            Start-Process code -ArgumentList $PSScriptRoot
        }
    }

    "clean" {
        $cleanDatabase = ($SubCommand -eq "database")

        if ($cleanDatabase) {
            Write-Host "Cleaning project (including database)..." -ForegroundColor Cyan
        } else {
            Write-Host "Cleaning project (preserving database)..." -ForegroundColor Cyan
        }

        # Stop any running services first
        Stop-Services

        # Only clean database if explicitly requested
        if ($cleanDatabase) {
            Clear-Database
        }

        # Clean build artifacts (skip DesktopApp on non-Windows - it's Windows-only)
        Write-Host "Cleaning build artifacts..." -ForegroundColor Yellow
        $projectsToClean = @(
            "Catan3.Shared/Catan3.Shared.csproj",
            "Catan3.GameService/Catan3.GameService.csproj",
            "WebUI/Catan3.WebUI.csproj",
            "Catan3.CLI/Catan3.CLI.csproj",
            "Tests/GameService/Catan3.Tests.GameService.csproj"
        )
        if ($IsWindows) {
            $projectsToClean += "DesktopApp/Catan Desktop.csproj"
        }
        foreach ($proj in $projectsToClean) {
            $projPath = Join-Path $PSScriptRoot $proj
            if (Test-Path $projPath) {
                & dotnet clean $projPath --verbosity quiet 2>$null
            }
        }
        Write-Host "Build artifacts cleaned." -ForegroundColor Green

        # Remove bin/obj directories for thorough clean
        Write-Host "Removing bin/obj directories..." -ForegroundColor Yellow
        Get-ChildItem -Path $PSScriptRoot -Include bin, obj -Recurse -Directory |
            ForEach-Object {
                Remove-Item $_.FullName -Recurse -Force -ErrorAction SilentlyContinue
            }

        Write-Host ""
        Write-Host "Clean completed!" -ForegroundColor Green
        if (-not $cleanDatabase) {
            Write-Host "Database preserved. Use './catan.ps1 clean database' to also clean database." -ForegroundColor Gray
        }
        Write-Host "Run './catan.ps1 build' to rebuild, or './catan.ps1 run' to rebuild and run." -ForegroundColor White
        exit 0
    }

    "stop" {
        Stop-Services
        exit 0
    }

    "restart" {
        Write-Host "Restarting services..." -ForegroundColor Cyan
        Stop-Services
        Start-Sleep -Milliseconds 500

        # Ensure database is initialized
        if (-not (Initialize-Database)) {
            Write-Host "Failed to initialize database. Cannot start services." -ForegroundColor Red
            exit 1
        }

        Start-GameService
        Start-WebUI

        Write-Host ""
        Write-Host "Services restarted:" -ForegroundColor Green
        Write-Host "  GameService: $GameServiceUrl" -ForegroundColor White
        Write-Host "  WebUI:       $WebUIUrl" -ForegroundColor White
    }

    "update" {
        # Terminate all Terminal windows on macOS if requested
        if ($Terminate -and $IsMacOS) {
            Write-Host "Terminating all Terminal windows..." -ForegroundColor Yellow
            try {
                # Use killall to force-terminate Terminal without confirmation dialog
                & killall Terminal 2>$null
                Start-Sleep -Milliseconds 500
                Write-Host "Terminal windows closed." -ForegroundColor Green
            }
            catch {
                Write-Host "Note: Could not close Terminal windows (may not be running)" -ForegroundColor DarkYellow
            }
        }
        elseif ($Terminate -and -not $IsMacOS) {
            Write-Host "Note: -Terminate switch is only supported on macOS" -ForegroundColor DarkYellow
        }

        # Build GameService (always needed)
        Write-Host "Rebuilding GameService..." -ForegroundColor Cyan
        $gameServicePath = Join-Path $PSScriptRoot "Catan3.GameService"
        & dotnet build $gameServicePath --verbosity quiet
        if ($LASTEXITCODE -ne 0) {
            Write-Host "GameService build failed!" -ForegroundColor Red
            exit 1
        }
        Write-Host "GameService rebuilt." -ForegroundColor Green

        # Build Blazor WebUI only if -Razor flag is set
        if ($Razor) {
            Write-Host "Rebuilding Blazor WebUI..." -ForegroundColor Cyan
            $webUIPath = Join-Path $PSScriptRoot "WebUI"
            & dotnet build $webUIPath --verbosity quiet
            if ($LASTEXITCODE -ne 0) {
                Write-Host "WebUI build failed!" -ForegroundColor Red
                exit 1
            }
            Write-Host "WebUI rebuilt." -ForegroundColor Green
        }

        # Check what services are running
        $gameRunning = Test-PortInUse -Port $GameServicePort
        $webRunning = Test-PortInUse -Port $WebUIPort
        $reactRunning = Test-PortInUse -Port $ReactUIPort

        if ($gameRunning -or $webRunning -or $reactRunning) {
            Write-Host "Restarting services..." -ForegroundColor Yellow
            Stop-Services
            Start-Sleep -Milliseconds 500

            if ($gameRunning) {
                Start-GameService
            }
            if ($Razor -and $webRunning) {
                Start-WebUI
            } elseif ($reactRunning) {
                Start-ReactUI
            }
            Write-Host "Services rebuilt and restarted! Refresh browser to load changes." -ForegroundColor Green
        }
        elseif ($Terminate) {
            # -Terminate killed the services, so start them fresh
            Write-Host "Starting services..." -ForegroundColor Yellow
            Start-GameService
            if ($Razor) {
                Start-WebUI
            } else {
                Start-ReactUI
            }
            Write-Host "Services rebuilt and started! Refresh browser to load changes." -ForegroundColor Green
        }
        else {
            Write-Host "Projects rebuilt successfully! Run './catan.ps1 run' to start." -ForegroundColor Green
        }
    }

    "database" {
        switch ($SubCommand) {
            "clean" {
                Clear-Database
            }
            "install" {
                # Check database status first
                $dbStatus = Invoke-DatabaseDoctor

                if ($dbStatus.Healthy -and -not $Force) {
                    Write-Host "Database is already installed and healthy." -ForegroundColor Green
                } else {
                    if ($Force) {
                        Write-Host "Force flag set, reinstalling database..." -ForegroundColor Yellow
                    }
                    Clear-Database
                    $installed = Install-Database
                    if (-not $installed) {
                        exit 1
                    }
                }
            }
            "doctor" {
                if ($Local) {
                    # Check local SQLite database
                    $status = Invoke-DatabaseDoctor
                    if (-not $status.Healthy) {
                        exit 1
                    }
                }
                else {
                    # Default: Check Azure SQL via catan-azure.ps1
                    $azureScript = Join-Path $PSScriptRoot ".scripts/catan-azure.ps1"
                    if (Test-Path $azureScript) {
                        & $azureScript database doctor -TraceLevel $TraceLevel -Json:$Json -HashTable:$HashTable
                    }
                    else {
                        Write-Host "Azure script not found. Use -Local for local database check." -ForegroundColor Red
                        exit 1
                    }
                }
            }
            default {
                Write-Host ""
                Write-Host "Database Commands" -ForegroundColor Cyan
                Write-Host "=================" -ForegroundColor Cyan
                Write-Host ""
                Write-Host "Usage: ./catan.ps1 database <subcommand>" -ForegroundColor Yellow
                Write-Host ""
                Write-Host "Subcommands:" -ForegroundColor Yellow
                Write-Host "  doctor   - Diagnose database health and show contents"
                Write-Host "  clean    - Delete the database (wipes all data)"
                Write-Host "  install  - Clean and reinstall database with default data"
                Write-Host ""
                Write-Host "Examples:" -ForegroundColor Yellow
                Write-Host "  ./catan.ps1 database doctor         - Check Azure SQL health (default)"
                Write-Host "  ./catan.ps1 database doctor -Local  - Check local SQLite database"
                Write-Host "  ./catan.ps1 database clean          - Delete local database"
                Write-Host "  ./catan.ps1 database install        - Fresh install with default players"
                Write-Host ""
                Write-Host "Note: Recording management moved to './catan.ps1 recording'" -ForegroundColor DarkYellow
                Write-Host ""

                if ($SubCommand) {
                    Write-Host "Unknown subcommand: $SubCommand" -ForegroundColor Red
                    exit 1
                }
            }
        }
    }

    "dependencies" {
        $depsScript = Join-Path $PSScriptRoot ".scripts/dependencies.ps1"

        switch ($SubCommand) {
            "doctor" {
                if ($Json) {
                    & $depsScript -Doctor -Json -TraceLevel $TraceLevel
                } elseif ($HashTable) {
                    & $depsScript -Doctor -HashTable -TraceLevel $TraceLevel
                } else {
                    & $depsScript -Doctor -TraceLevel $TraceLevel
                }
            }
            "install" {
                & $depsScript -Install -Yes:$Yes -Force:$Force -TraceLevel $TraceLevel
            }
            "clean" {
                & $depsScript -Clean -Yes:$Yes -Force:$Force -TraceLevel $TraceLevel
            }
            default {
                Write-Host ""
                Write-Host "Dependencies Commands" -ForegroundColor Cyan
                Write-Host "=====================" -ForegroundColor Cyan
                Write-Host ""
                Write-Host "Usage: ./catan.ps1 dependencies <subcommand>" -ForegroundColor Yellow
                Write-Host ""
                Write-Host "Subcommands:" -ForegroundColor Yellow
                Write-Host "  doctor   - Check status of all dependencies"
                Write-Host "  install  - Install all dependencies"
                Write-Host "  clean    - Remove/reset dependencies"
                Write-Host ""
                Write-Host "Options:" -ForegroundColor Yellow
                Write-Host "  -Json        Output doctor as JSON"
                Write-Host "  -HashTable   Output doctor as PowerShell hashtable"
                Write-Host "  -Yes         Skip confirmation prompts"
                Write-Host "  -Force       Force reinstall even if already installed"
                Write-Host "  -TraceLevel  Output detail: ERROR, WARN, INFO (default), DEBUG"
                Write-Host ""
                Write-Host "Examples:" -ForegroundColor Yellow
                Write-Host "  ./catan.ps1 dependencies doctor    - Check all dependencies"
                Write-Host "  ./catan.ps1 dependencies install   - Install missing dependencies"
                Write-Host ""

                if ($SubCommand) {
                    Write-Host "Unknown subcommand: $SubCommand" -ForegroundColor Red
                    exit 1
                }
            }
        }
    }

    "azure" {
        $azureScript = Join-Path $PSScriptRoot ".scripts/catan-azure.ps1"

        switch ($SubCommand) {
            "install" {
                Write-Host "Installing all Azure resources..." -ForegroundColor Cyan
                Write-Host ""
                & $azureScript game-service install -TraceLevel $TraceLevel
                if ($LASTEXITCODE -ne 0) { exit 1 }
                & $azureScript database install -TraceLevel $TraceLevel
                if ($LASTEXITCODE -ne 0) { exit 1 }
                & $azureScript ui install -TraceLevel $TraceLevel
                if ($LASTEXITCODE -ne 0) { exit 1 }
                # Configure database connection and grant managed identity access
                & $azureScript database deploy -TraceLevel $TraceLevel
                if ($LASTEXITCODE -ne 0) { exit 1 }
                Write-Host ""
                Write-Host "All Azure resources installed and configured!" -ForegroundColor Green
            }
            "deploy" {
                # Support targeted deploys: ./catan.ps1 azure deploy [ui|game-service|database]
                # Without a target, deploys everything.
                $deployTarget = if ($Target) { $Target.ToLower() } else { "all" }

                switch ($deployTarget) {
                    "ui" {
                        Write-Host "Deploying UI to Azure..." -ForegroundColor Cyan
                        Write-Host ""

                        $uiDoctor = & $azureScript ui doctor -HashTable -TraceLevel $TraceLevel
                        if ($uiDoctor.needsInstall) {
                            Write-Host "  UI: Not installed - installing..." -ForegroundColor Yellow
                            & $azureScript ui install -TraceLevel $TraceLevel
                            if ($LASTEXITCODE -ne 0) { exit 1 }
                        }

                        # Blazor → production
                        if ($uiDoctor.needsDeploy -or $Force) {
                            Write-Host "  UI (Blazor): Deploying to production..." -ForegroundColor Yellow
                            & $azureScript ui deploy -Force:$Force -NoBuild:$NoBuild -TraceLevel $TraceLevel
                            if ($LASTEXITCODE -ne 0) { exit 1 }
                        } else {
                            Write-Host "  UI (Blazor): Up to date - skipping" -ForegroundColor Green
                        }

                        # React staging is deployed by the deploy-react-staging.yml workflow
                        # Use './catan.ps1 azure ui deploy-staging' to deploy manually

                        Write-Host ""
                        Write-Host "UI deployment complete!" -ForegroundColor Green
                    }
                    "game-service" {
                        $slotLabel = if ($Slot) { " to slot '$Slot'" } else { "" }
                        Write-Host "Deploying GameService${slotLabel} to Azure..." -ForegroundColor Cyan
                        Write-Host ""
                        $gsArgs = @{}
                        if ($Slot) { $gsArgs['Slot'] = $Slot }
                        & $azureScript game-service deploy -Force:$Force -NoBuild:$NoBuild -TraceLevel $TraceLevel @gsArgs
                        if ($LASTEXITCODE -ne 0) { exit 1 }
                        Write-Host ""
                        Write-Host "GameService deployment complete!" -ForegroundColor Green
                    }
                    "database" {
                        Write-Host "Deploying database configuration..." -ForegroundColor Cyan
                        Write-Host ""
                        & $azureScript database deploy -Force:$Force -TraceLevel $TraceLevel
                        if ($LASTEXITCODE -ne 0) { exit 1 }
                        Write-Host ""
                        Write-Host "Database deployment complete!" -ForegroundColor Green
                    }
                    "all" {
                        Write-Host "Deploying to Azure..." -ForegroundColor Cyan
                        Write-Host ""

                        # Run all doctors to determine what needs to be done
                        Write-Host "Checking deployment status..." -ForegroundColor Gray

                        # GameService doctor
                        $gsDoctor = & $azureScript game-service doctor -HashTable -TraceLevel $TraceLevel
                        $gsNeedsInstall = $gsDoctor.needsInstall
                        $gsNeedsDeploy = $gsDoctor.needsDeploy
                        $gsHealthy = $gsDoctor.healthy

                        if ($gsNeedsInstall) {
                            Write-Host "  GameService: Not installed - installing..." -ForegroundColor Yellow
                            & $azureScript game-service install -TraceLevel $TraceLevel
                            if ($LASTEXITCODE -ne 0) { exit 1 }
                            $gsNeedsDeploy = $true
                        }

                        if ($gsHealthy -and -not $gsNeedsDeploy -and -not $Force) {
                            Write-Host "  GameService: Up to date - skipping" -ForegroundColor Green
                        }
                        elseif ($gsNeedsDeploy -or $Force) {
                            Write-Host "  GameService: Needs deploy" -ForegroundColor Yellow
                            & $azureScript game-service deploy -Force:$Force -NoBuild:$NoBuild -TraceLevel $TraceLevel
                            if ($LASTEXITCODE -ne 0) { exit 1 }
                        }
                        else {
                            Write-Host "  GameService: OK - skipping" -ForegroundColor Green
                        }

                        # Database doctor
                        $dbDoctor = & $azureScript database doctor -HashTable -TraceLevel $TraceLevel
                        $dbNeedsInstall = $dbDoctor.needsInstall
                        $dbConnected = $dbDoctor.checks.gameServiceConnected
                        $dbNeedsDeploy = $dbDoctor.needsDeploy
                        $dbSchemaValid = $dbDoctor.checks.schemaValid

                        if ($dbNeedsInstall) {
                            Write-Host "  Database: Not installed - installing..." -ForegroundColor Yellow
                            & $azureScript database install -TraceLevel $TraceLevel
                            if ($LASTEXITCODE -ne 0) { exit 1 }
                            $dbNeedsDeploy = $true
                        }

                        if ($dbConnected -and -not $Force) {
                            Write-Host "  Database: Connected - skipping" -ForegroundColor Green
                        }
                        elseif ($dbNeedsDeploy -or $Force) {
                            Write-Host "  Database: Needs configuration" -ForegroundColor Yellow
                            & $azureScript database deploy -Force:$Force -TraceLevel $TraceLevel
                            if ($LASTEXITCODE -ne 0) { exit 1 }
                        }
                        else {
                            Write-Host "  Database: OK - skipping" -ForegroundColor Green
                        }

                        # Check for schema issues
                        if (-not $dbSchemaValid -and $dbDoctor.missingTables) {
                            Write-Host "  Database: Schema missing tables - creating directly..." -ForegroundColor Yellow
                            Write-Host "  Database: Missing: $($dbDoctor.missingTables -join ', ')" -ForegroundColor Yellow
                            & $azureScript database fix -TraceLevel $TraceLevel
                            if ($LASTEXITCODE -ne 0) {
                                Write-Host "  Database: Failed to create missing tables" -ForegroundColor Red
                                exit 1
                            }
                            Write-Host "  Database: Schema repaired successfully" -ForegroundColor Green
                        }
                        elseif (-not $dbSchemaValid) {
                            Write-Host "  Database: Schema check incomplete - may need to run deploy again after database wakes" -ForegroundColor Yellow
                        }

                        # UI doctor
                        $uiDoctor = & $azureScript ui doctor -HashTable -TraceLevel $TraceLevel
                        $uiNeedsInstall = $uiDoctor.needsInstall
                        $uiNeedsDeploy = $uiDoctor.needsDeploy
                        $uiHealthy = $uiDoctor.healthy

                        if ($uiNeedsInstall) {
                            Write-Host "  UI: Not installed - installing..." -ForegroundColor Yellow
                            & $azureScript ui install -TraceLevel $TraceLevel
                            if ($LASTEXITCODE -ne 0) { exit 1 }
                            $uiNeedsDeploy = $true
                        }

                        if ($uiHealthy -and -not $uiNeedsDeploy -and -not $Force) {
                            Write-Host "  UI (Blazor): Up to date - skipping" -ForegroundColor Green
                        }
                        elseif ($uiNeedsDeploy -or $Force) {
                            Write-Host "  UI (Blazor): Needs deploy" -ForegroundColor Yellow
                            & $azureScript ui deploy -Force:$Force -NoBuild:$NoBuild -TraceLevel $TraceLevel
                            if ($LASTEXITCODE -ne 0) { exit 1 }
                        }
                        else {
                            Write-Host "  UI (Blazor): OK - skipping" -ForegroundColor Green
                        }

                        # React UI staging is deployed by the deploy-react-staging.yml workflow
                        # (triggered on push to main when react-ui/** changes). Don't duplicate
                        # that here — it requires Node.js which the deploy-azure.yml workflow
                        # doesn't set up, and would hang the deploy pipeline.

                        Write-Host ""
                        Write-Host "All deployments complete!" -ForegroundColor Green
                    }
                    default {
                        Write-Host "Unknown deploy target: $deployTarget" -ForegroundColor Red
                        Write-Host ""
                        Write-Host "Usage: ./catan.ps1 azure deploy [target]" -ForegroundColor Yellow
                        Write-Host ""
                        Write-Host "Targets:" -ForegroundColor Yellow
                        Write-Host "  ui            - Deploy Blazor (prod) + React (staging)"
                        Write-Host "  game-service  - Deploy GameService only"
                        Write-Host "  database      - Deploy database configuration only"
                        Write-Host "  (no target)   - Deploy everything"
                        exit 1
                    }
                }
            }
            "doctor" {
                Write-Host "Checking Azure health..." -ForegroundColor Cyan
                Write-Host ""

                # Pass through -Json and -HashTable to the individual doctor calls
                # The catan-azure.ps1 script now handles all formatting
                & $azureScript game-service doctor -TraceLevel $TraceLevel -Json:$Json -HashTable:$HashTable
                & $azureScript database doctor -TraceLevel $TraceLevel -Json:$Json -HashTable:$HashTable
                & $azureScript ui doctor -TraceLevel $TraceLevel -Json:$Json -HashTable:$HashTable

                # Show summary if not in JSON/HashTable mode
                if (-not $Json -and -not $HashTable) {
                    Write-Host ""
                    Write-Host "Service URLs:" -ForegroundColor Gray
                    Write-Host "  WebUI:       https://catan.azurewebsites.net" -ForegroundColor Gray
                    Write-Host "  GameService: https://catan-api.azurewebsites.net" -ForegroundColor Gray
                }
            }
            "swap-slots" {
                Write-Host "Swap Azure Deployment Slots" -ForegroundColor Cyan
                Write-Host "==========================" -ForegroundColor Cyan
                Write-Host ""

                # Load config for app name and resource group
                $azureConfigFile = Join-Path $PSScriptRoot ".azure/catan-azure.json"
                if (-not (Test-Path $azureConfigFile)) {
                    Write-Host "Azure configuration not found. Run './catan.ps1 azure install' first." -ForegroundColor Red
                    exit 1
                }
                $azureConfig = Get-Content $azureConfigFile -Raw | ConvertFrom-Json
                $appName = $azureConfig.ui.appName
                $rgName = $azureConfig.resourceGroup

                # Use doctor to get complete picture of the system
                Write-Host "Running UI health check..." -ForegroundColor Gray
                $uiDoctor = & $azureScript ui doctor -HashTable -TraceLevel $TraceLevel

                if ($uiDoctor.needsInstall) {
                    Write-Host "UI is not installed. Run './catan.ps1 azure install' first." -ForegroundColor Red
                    exit 1
                }

                # Show current state from doctor
                Write-Host "This will swap the staging slot into production." -ForegroundColor Yellow
                Write-Host ""
                Write-Host "  App:         $appName" -ForegroundColor Gray
                Write-Host "  Production:  https://$appName.azurewebsites.net" -ForegroundColor Gray
                Write-Host "  Staging:     https://$appName-staging.azurewebsites.net" -ForegroundColor Gray
                Write-Host ""

                # Show what's currently in each slot
                if ($uiDoctor.prodRuntime -or $uiDoctor.stagingRuntime) {
                    function Get-SwapRuntimeLabel {
                        param([string]$Runtime)
                        if ([string]::IsNullOrWhiteSpace($Runtime)) { return "unknown" }
                        if ($Runtime -like "DOTNETCORE*") { return "Blazor ($Runtime)" }
                        if ($Runtime -like "NODE*") { return "React/Next.js ($Runtime)" }
                        return $Runtime
                    }
                    Write-Host "Current configuration:" -ForegroundColor Yellow
                    Write-Host "  Production: $(Get-SwapRuntimeLabel $uiDoctor.prodRuntime)" -ForegroundColor Gray
                    Write-Host "  Staging:    $(Get-SwapRuntimeLabel $uiDoctor.stagingRuntime)" -ForegroundColor Gray
                    Write-Host ""
                    Write-Host "After swap:" -ForegroundColor Yellow
                    Write-Host "  Production: $(Get-SwapRuntimeLabel $uiDoctor.stagingRuntime)" -ForegroundColor Gray
                    Write-Host "  Staging:    $(Get-SwapRuntimeLabel $uiDoctor.prodRuntime)" -ForegroundColor Gray
                    Write-Host ""
                }

                # Check staging slot exists
                if (-not $uiDoctor.checks.stagingSlot) {
                    Write-Host "Staging slot does not exist." -ForegroundColor Red
                    Write-Host "  Run: ./catan.ps1 azure install" -ForegroundColor Cyan
                    exit 1
                }

                # Check staging has code deployed
                if (-not $uiDoctor.checks.stagingCodeDeployed) {
                    Write-Host "Staging slot has no code deployed." -ForegroundColor Red
                    Write-Host "  Run: ./catan.ps1 azure deploy ui" -ForegroundColor Cyan
                    exit 1
                }

                # Show staging commit info
                if ($uiDoctor.stagingDeployedCommit) {
                    Write-Host "  Staging commit: $($uiDoctor.stagingDeployedCommit)" -ForegroundColor Gray
                }

                # Check staging runtime is correct (NODE for React)
                if (-not $uiDoctor.checks.stagingRuntime) {
                    Write-Host "Staging slot runtime is not configured for Node.js." -ForegroundColor Red
                    Write-Host "  Current: $($uiDoctor.stagingRuntime)" -ForegroundColor Gray
                    Write-Host "  Run: ./catan.ps1 azure ui deploy-staging" -ForegroundColor Cyan
                    exit 1
                }

                # Check staging is responding (with retry for cold starts)
                if (-not $uiDoctor.checks.stagingResponding) {
                    Write-Host "Staging slot is not responding. Warming up..." -ForegroundColor Yellow
                    $stagingUrl = "https://$appName-staging.azurewebsites.net"
                    $healthy = $false
                    for ($attempt = 1; $attempt -le 3; $attempt++) {
                        try {
                            $response = Invoke-WebRequest -Uri $stagingUrl -TimeoutSec 30 -UseBasicParsing -ErrorAction Stop
                            Write-Host "  Staging slot is responding (HTTP $($response.StatusCode))" -ForegroundColor Green
                            $healthy = $true
                            break
                        }
                        catch {
                            if ($attempt -lt 3) {
                                Write-Host "  Attempt $attempt/3: staging not ready, retrying..." -ForegroundColor Yellow
                                Start-Sleep -Seconds 5
                            }
                        }
                    }
                    if (-not $healthy) {
                        Write-Host "Staging slot is not responding after 3 attempts." -ForegroundColor Red
                        Write-Host ""
                        Write-Host "Try:" -ForegroundColor Yellow
                        Write-Host "  1. Wait a minute and retry (cold start can be slow)" -ForegroundColor Gray
                        Write-Host "  2. Redeploy: ./catan.ps1 azure deploy ui -Force" -ForegroundColor Gray
                        exit 1
                    }
                }

                # Confirm
                if (-not $Yes) {
                    $confirm = Read-Host "Proceed with swap? (y/N)"
                    if ($confirm -ne 'y' -and $confirm -ne 'Y') {
                        Write-Host "Swap cancelled." -ForegroundColor Yellow
                        exit 0
                    }
                }

                Write-Host "Swapping staging -> production..." -ForegroundColor Cyan
                az webapp deployment slot swap --name $appName --resource-group $rgName --slot staging
                if ($LASTEXITCODE -ne 0) {
                    Write-Host "Slot swap failed!" -ForegroundColor Red
                    exit 1
                }

                Write-Host ""
                Write-Host "Slot swap complete!" -ForegroundColor Green
                Write-Host "  Production is now serving: $(Get-SwapRuntimeLabel $uiDoctor.stagingRuntime)" -ForegroundColor Gray
                Write-Host "  To swap back: ./catan.ps1 azure swap-slots" -ForegroundColor Gray
            }
            "clean" {
                Write-Host "Cleaning all Azure resources..." -ForegroundColor Yellow
                Write-Host ""

                if ($Yes) {
                    & $azureScript ui clean -Yes -TraceLevel $TraceLevel
                    if ($LASTEXITCODE -ne 0) { exit 1 }
                    & $azureScript database clean -Yes -TraceLevel $TraceLevel
                    if ($LASTEXITCODE -ne 0) { exit 1 }
                    & $azureScript game-service clean -Yes -TraceLevel $TraceLevel
                    if ($LASTEXITCODE -ne 0) { exit 1 }
                }
                else {
                    & $azureScript ui clean -TraceLevel $TraceLevel
                    if ($LASTEXITCODE -ne 0) { exit 1 }
                    & $azureScript database clean -TraceLevel $TraceLevel
                    if ($LASTEXITCODE -ne 0) { exit 1 }
                    & $azureScript game-service clean -TraceLevel $TraceLevel
                    if ($LASTEXITCODE -ne 0) { exit 1 }
                }
                Write-Host ""
                Write-Host "All Azure resources cleaned!" -ForegroundColor Green
            }
            # Noun-first routing: ./catan.ps1 azure <noun> <verb>
            # Passes through directly to catan-azure.ps1
            { $_ -in @("ui", "game-service", "database", "github") } {
                if (-not $Target) {
                    Write-Host "Usage: ./catan.ps1 azure $SubCommand <verb>" -ForegroundColor Yellow
                    Write-Host ""
                    Write-Host "Verbs: install, deploy, deploy-staging, doctor, clean, fix" -ForegroundColor Gray
                    exit 1
                }
                $extraArgs = @{}
                if ($Slot) { $extraArgs['Slot'] = $Slot }
                if ($AzureGameServiceUrl) { $extraArgs['GameServiceUrl'] = $AzureGameServiceUrl }
                & $azureScript $SubCommand $Target -Force:$Force -NoBuild:$NoBuild -TraceLevel $TraceLevel -Json:$Json -HashTable:$HashTable @extraArgs
                if ($LASTEXITCODE -ne 0) { exit 1 }
            }
            default {
                Write-Host ""
                Write-Host "Azure Commands" -ForegroundColor Cyan
                Write-Host "==============" -ForegroundColor Cyan
                Write-Host ""
                Write-Host "Usage: ./catan.ps1 azure <verb> [target]" -ForegroundColor Yellow
                Write-Host "       ./catan.ps1 azure <target> <verb>" -ForegroundColor Yellow
                Write-Host ""
                Write-Host "Verbs (operate on all resources):" -ForegroundColor Yellow
                Write-Host "  install     - Create all Azure resources (idempotent)"
                Write-Host "  deploy      - Deploy everything to Azure"
                Write-Host "  doctor      - Check health of all Azure resources"
                Write-Host "  clean       - Delete all Azure resources"
                Write-Host "  swap-slots  - Swap staging (React) and production (Blazor) slots"
                Write-Host ""
                Write-Host "Targeted (verb + target or target + verb):" -ForegroundColor Yellow
                Write-Host "  deploy ui              - Deploy Blazor (prod) + React (staging)"
                Write-Host "  deploy game-service    - Deploy GameService only"
                Write-Host "  deploy database        - Deploy database config only"
                Write-Host "  ui doctor              - Check UI health only"
                Write-Host "  ui deploy-staging      - Deploy React to staging only"
                Write-Host "  game-service deploy    - Deploy GameService only"
                Write-Host "  github install         - Setup GitHub Actions OIDC"
                Write-Host ""
                Write-Host "Options:" -ForegroundColor Yellow
                Write-Host "  -Force                  Force deploy even if up-to-date"
                Write-Host "  -Slot <name>            Deploy to a specific slot (e.g., staging)"
                Write-Host "  -AzureGameServiceUrl    GameService URL for React staging builds"
                Write-Host "  -Json                   Output doctor as JSON"
                Write-Host "  -HashTable              Output doctor as PowerShell hashtable"
                Write-Host "  -TraceLevel             Output detail: ERROR, WARN, INFO (default), DEBUG"
                Write-Host ""
                Write-Host "Examples:" -ForegroundColor Yellow
                Write-Host "  ./catan.ps1 azure deploy ui -Force    - Force deploy UI"
                Write-Host "  ./catan.ps1 azure ui doctor            - Check UI health"
                Write-Host "  ./catan.ps1 azure doctor               - Check all resources"
                Write-Host "  ./catan.ps1 azure swap-slots           - Swap staging <-> production"
                Write-Host "  ./catan.ps1 azure game-service deploy -Slot staging  - Deploy to staging slot"
                Write-Host "  ./catan.ps1 azure ui deploy-staging -AzureGameServiceUrl https://catan-api-staging.azurewebsites.net"
                Write-Host ""

                if ($SubCommand) {
                    Write-Host "Unknown subcommand: $SubCommand" -ForegroundColor Red
                    exit 1
                }
            }
        }
    }

    "help" {
        Write-Host ""
        Write-Host "Catan3 Development Script" -ForegroundColor Cyan
        Write-Host "=========================" -ForegroundColor Cyan
        Write-Host ""
        Write-Host "Quick Start:" -ForegroundColor Yellow
        Write-Host "  ./catan.ps1 doctor           - Check if everything is set up correctly"
        Write-Host "  ./catan.ps1 install          - Install dependencies and database"
        Write-Host "  ./catan.ps1 run              - Build, start services, launch browser"
        Write-Host ""
        Write-Host "Development:" -ForegroundColor Yellow
        Write-Host "  ./catan.ps1 run              - Start GameService + React UI (default)"
        Write-Host "  ./catan.ps1 run -Network     - Same, but accessible from other devices"
        Write-Host "  ./catan.ps1 run -Razor       - Use Blazor WebUI instead of React"
        Write-Host "  ./catan.ps1 run -Desktop     - Also build/deploy Desktop app (Windows)"
        Write-Host "  ./catan.ps1 stop             - Stop running services"
        Write-Host "  ./catan.ps1 restart          - Stop and restart services"
        Write-Host "  ./catan.ps1 update           - Rebuild and restart (when hot reload fails)"
        Write-Host "  ./catan.ps1 update -Terminate - Same, but close all Terminal windows first (macOS)"
        Write-Host "  ./catan.ps1 build            - Build GameService + React UI (no tests)"
        Write-Host "  ./catan.ps1 build -Desktop   - Build all including Desktop app"
        Write-Host "  ./catan.ps1 test             - Build and run all tests"
        Write-Host "  ./catan.ps1 lint             - Format, lint, and spell check (PS, TS, MD)"
        Write-Host "  ./catan.ps1 generate-types   - Generate TypeScript types from C# models (TypeGen 7.0.0)"
        Write-Host "  ./catan.ps1 clean            - Stop services, clean build (preserves database)"
        Write-Host "  ./catan.ps1 debug            - Instructions for VS Code debugging"
        Write-Host "  ./catan.ps1 help (or -Help)  - Show this help"
        Write-Host ""
        Write-Host "Setup:" -ForegroundColor Yellow
        Write-Host "  ./catan.ps1 doctor           - Check dependencies and database health"
        Write-Host "  ./catan.ps1 install          - Install all dependencies and database"
        Write-Host ""
        Write-Host "Recording:" -ForegroundColor Yellow
        Write-Host "  ./catan.ps1 recording list   - List all recordings (add -Azure for Azure)"
        Write-Host "  ./catan.ps1 recording save   - Save recordings to JSON files"
        Write-Host "  ./catan.ps1 recording load   - Load recordings from JSON files"
        Write-Host "  ./catan.ps1 recording replay - Run replay tests (requires running server)"
        Write-Host "  ./catan.ps1 recording delete - Delete a recording"
        Write-Host "  ./catan.ps1 recording        - Show detailed recording help"
        Write-Host ""
        Write-Host "Stats:" -ForegroundColor Yellow
        Write-Host "  ./catan.ps1 stats list       - Show stats summary (add -Azure for Azure)"
        Write-Host "  ./catan.ps1 stats export     - Export stats to JSON file"
        Write-Host "  ./catan.ps1 stats import     - Import stats from JSON file"
        Write-Host "  ./catan.ps1 stats reset      - Reset all lifetime statistics"
        Write-Host "  ./catan.ps1 stats            - Show detailed stats help"
        Write-Host ""
        Write-Host "Database:" -ForegroundColor Yellow
        Write-Host "  ./catan.ps1 database doctor  - Diagnose database health and contents"
        Write-Host "  ./catan.ps1 database clean   - Delete database"
        Write-Host "  ./catan.ps1 database install - Fresh install with default data"
        Write-Host ""
        Write-Host "Dependencies:" -ForegroundColor Yellow
        Write-Host "  ./catan.ps1 dependencies doctor  - Check all dependency status"
        Write-Host "  ./catan.ps1 dependencies install - Install missing dependencies"
        Write-Host "  ./catan.ps1 dependencies clean   - Remove/reset dependencies"
        Write-Host ""
        Write-Host "Azure:" -ForegroundColor Yellow
        Write-Host "  ./catan.ps1 azure doctor     - Check Azure deployment health"
        Write-Host "  ./catan.ps1 azure install    - Create all Azure resources"
        Write-Host "  ./catan.ps1 azure deploy     - Deploy everything to Azure"
        Write-Host "  ./catan.ps1 azure swap-slots - Swap staging <-> production"
        Write-Host "  ./catan.ps1 azure clean      - Delete all Azure resources"
        Write-Host ""
        Write-Host "Typical workflow:" -ForegroundColor Yellow
        Write-Host "  1. ./catan.ps1 run           - Start services (hot reload enabled)"
        Write-Host "  2. Make code changes         - Browser auto-refreshes on save"
        Write-Host "  3. ./catan.ps1 update        - If hot reload fails, rebuild and restart"
        Write-Host ""
        Write-Host "URLs:" -ForegroundColor Yellow
        Write-Host "  GameService: $GameServiceUrl"
        Write-Host "  WebUI:       $WebUIUrl"
        Write-Host ""
    }

    default {
        Write-Host ""
        Write-Host "Invalid command: $Verb" -ForegroundColor Red
        Write-Host ""
        & $PSCommandPath help
        exit 1
    }
}
