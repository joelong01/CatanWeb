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

.EXAMPLE
    ./catan.ps1 run              # Build GameService + React UI, start services, launch browser
    ./catan.ps1 run -Network     # Same, but accessible from other devices
    ./catan.ps1 stop             # Stop running services
    ./catan.ps1 build            # Build GameService + React UI
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
    [Alias("LogLevel")]
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
    [switch]$All,

    [Parameter()]
    [string]$Slot,

    [Parameter()]
    [string]$AzureGameServiceUrl,

    [Parameter()]
    [switch]$Staging,

    [Parameter(ValueFromRemainingArguments = $true)]
    [string[]]$RemainingArgs
)

$ErrorActionPreference = "Stop"

# Import shared utility module
Import-Module "$PSScriptRoot/.scripts/utility-scripts.psm1" -Force

$PSDefaultParameterValues = @{ 'Write-Log:TraceLevel' = $TraceLevel }

# Platform detection (use built-in automatic variables in PS Core, fallback for PS 5.1)
if (-not (Test-Path variable:IsMacOS)) { $script:IsMacOS = $false }
if (-not (Test-Path variable:IsLinux)) { $script:IsLinux = $false }
if (-not (Test-Path variable:IsWindows)) { $script:IsWindows = $true }

# Check for unknown arguments (typos, etc.)
if ($RemainingArgs) {
    Write-Log -Level ERROR -Message "Unknown argument(s): $($RemainingArgs -join ', ')" -NoLabel
    Write-Log -Level INFO -Message "" -NoLabel
    $Verb = "help"
}

$GameServicePort = 8080
$ReactUIPort = 3000
$GameServiceUrl = "http://localhost:$GameServicePort"
$ReactUIUrl = "http://localhost:$ReactUIPort"
# All database operations use CosmosDB via .scripts/database.ps1
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

    Write-Log -Level INFO -Message "Starting GameService..." -NoLabel -ForegroundColor Cyan

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

    Write-Log -Level WARN -Message "Waiting for GameService..." -NoLabel
    Wait-ForService -Url "$GameServiceUrl/health" -TimeoutSeconds 30 | Out-Null
    Write-Log -Level INFO -Message "GameService running at $GameServiceUrl" -NoLabel -ForegroundColor Green
}

function Start-ReactUI {
    Write-Log -Level INFO -Message "Starting React UI..." -NoLabel -ForegroundColor Cyan

    $reactUIPath = Join-Path $PSScriptRoot "react-ui"

    # Check if react-ui directory exists
    if (-not (Test-Path $reactUIPath)) {
        Write-Log -Level ERROR -Message "React UI directory not found at $reactUIPath" -NoLabel
        return
    }

    # Check if npm install is needed
    $nodeModulesPath = Join-Path $reactUIPath "node_modules"
    $packageJsonPath = Join-Path $reactUIPath "package.json"
    $nextBinPath = Join-Path $reactUIPath "node_modules" ".bin" "next"
    $needsInstall = $false
    if (-not (Test-Path $nodeModulesPath)) {
        Write-Log -Level WARN -Message "node_modules not found. Running npm ci..." -NoLabel
        $needsInstall = $true
    } elseif (-not (Test-Path $nextBinPath)) {
        Write-Log -Level WARN -Message "next binary missing from node_modules. Running npm ci..." -NoLabel
        $needsInstall = $true
    } elseif ((Get-Item $packageJsonPath).LastWriteTime -gt (Get-Item $nodeModulesPath).LastWriteTime) {
        Write-Log -Level WARN -Message "package.json is newer than node_modules. Running npm ci..." -NoLabel
        $needsInstall = $true
    }
    if ($needsInstall) {
        Push-Location $reactUIPath
        try {
            & npm ci
            if ($LASTEXITCODE -ne 0) {
                Write-Log -Level ERROR -Message "npm ci failed!" -NoLabel
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

    Write-Log -Level WARN -Message "Waiting for React UI..." -NoLabel
    Wait-ForService -Url $ReactUIUrl -TimeoutSeconds 30 | Out-Null
    Write-Log -Level INFO -Message "React UI running at $ReactUIUrl" -NoLabel -ForegroundColor Green

    # Open browser to React UI
    Start-Process $ReactUIUrl
}

function Initialize-Database {
    Write-Log -Level INFO -Message "Checking Cosmos emulator..." -NoLabel -ForegroundColor Cyan
    $dbScript = Join-Path $PSScriptRoot ".scripts/database.ps1"
    & pwsh $dbScript install
    return ($LASTEXITCODE -eq 0)
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
    Write-Log -Level WARN -Message "Stopping services..." -NoLabel

    $killedCount = 0

    if ($IsWindows) {
        # Use netstat instead of Get-NetTCPConnection (which can hang)
        # netstat -ano returns: Proto  LocalAddress  ForeignAddress  State  PID
        $netstatOutput = netstat -ano 2>$null | Select-String "LISTENING"

        foreach ($port in @($GameServicePort, $ReactUIPort)) {
            $portMatches = $netstatOutput | Where-Object { $_ -match ":$port\s" }
            foreach ($match in $portMatches) {
                # Extract PID from the last column
                if ($match -match '\s+(\d+)\s*$') {
                    $processId = [int]$Matches[1]
                    if ($processId -and $processId -ne 0) {
                        try {
                            Stop-Process -Id $processId -Force -ErrorAction Stop
                            Write-Log -Level DEBUG -Message "  Killed process $processId (port $port)" -NoLabel
                            $killedCount++
                        } catch {
                            # Process may have already exited
                        }
                    }
                }
            }
        }

        if ($killedCount -eq 0) {
            Write-Log -Level DEBUG -Message "  No services running" -NoLabel
        }
    } else {
        # macOS: Kill processes and close Terminal windows

        # Kill processes by port
        $gameServicePids = lsof -ti ":$GameServicePort" 2>$null
        foreach ($procId in $gameServicePids) {
            if ($procId) {
                Stop-ChildProcesses -ParentPid $procId
                Stop-Process -Id $procId -Force -ErrorAction SilentlyContinue
                Write-Log -Level DEBUG -Message "  Killed process $procId (GameService port)" -NoLabel
                $killedCount++
            }
        }

        $reactUIPids = lsof -ti ":$ReactUIPort" 2>$null
        foreach ($procId in $reactUIPids) {
            if ($procId) {
                Stop-ChildProcesses -ParentPid $procId
                Stop-Process -Id $procId -Force -ErrorAction SilentlyContinue
                Write-Log -Level DEBUG -Message "  Killed process $procId (React UI port)" -NoLabel
                $killedCount++
            }
        }

        # Kill any dotnet processes running GameService
        $procIds = pgrep -f "Catan3.GameService|dotnet.*watch.*run" 2>$null
        foreach ($procId in $procIds) {
            if ($procId) {
                Stop-Process -Id $procId -Force -ErrorAction SilentlyContinue
                Write-Log -Level DEBUG -Message "  Killed process $procId" -NoLabel
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
            Write-Log -Level DEBUG -Message "  Killed $killedCount process(es)" -NoLabel
        }
    }

    # Clean up PID files
    if (Test-Path $PidFile) {
        Remove-Item $PidFile -Force
    }
    # Clean up macOS pid files
    $gameServicePidFile = Join-Path $PSScriptRoot ".gameservice.pid"
    if (Test-Path $gameServicePidFile) { Remove-Item $gameServicePidFile -Force }

    # Wait for ports to be released
    Start-Sleep -Milliseconds 500

    # Verify ports are free
    $gameStillRunning = Test-PortInUse -Port $GameServicePort
    $reactUIStillRunning = Test-PortInUse -Port $ReactUIPort

    if ($gameStillRunning -or $reactUIStillRunning) {
        Write-Log -Level WARN -Message "  Waiting for ports to be released..." -NoLabel
        Start-Sleep -Seconds 2
    }

    Write-Log -Level INFO -Message "Services stopped." -NoLabel -ForegroundColor Green
}

# Handle -Help switch (redirect to help verb)
if ($Help) {
    $Verb = "help"
}

switch ($Verb) {
    "build" {
        Write-Log -Level INFO -Message "Building solution..." -NoLabel -ForegroundColor Cyan
        & "$PSScriptRoot\.scripts\build.ps1" -NoTest
        if ($LASTEXITCODE -ne 0) {
            Write-Log -Level ERROR -Message "Build failed!" -NoLabel
            exit 1
        }
        Write-Log -Level INFO -Message "Build completed successfully!" -NoLabel -ForegroundColor Green

        # Generate TypeScript types (if react-ui exists)
        $reactUiPath = Join-Path $PSScriptRoot "react-ui"
        if (Test-Path $reactUiPath) {
            Write-Log -Level INFO -Message "" -NoLabel
            Write-Log -Level WARN -Message "Generating TypeScript types..." -NoLabel

            # Generate model types from C# using TypeGenRunner (TypeGen 7.0.0)
            # Note: do NOT use --no-build here; TypeGenRunner is not in the solution so
            # its copy of Catan3.Shared.dll can go stale and drop newly-added types.
            $typegenRunnerProject = Join-Path $PSScriptRoot "Catan3.Shared\TypeScript\TypeGenRunner\TypeGenRunner.csproj"
            $null = & dotnet run --project $typegenRunnerProject 2>&1
            if ($LASTEXITCODE -eq 0) {
                Write-Log -Level INFO -Message "  [OK] Model types updated (TypeGen 7.0.0)" -NoLabel -ForegroundColor Green
            } else {
                Write-Log -Level WARN -Message "  [WARN] TypeGen failed (non-blocking)" -NoLabel
            }
        }
    }

    "test" {
        Write-Log -Level INFO -Message "Running tests..." -NoLabel -ForegroundColor Cyan

        # Stop services first to avoid file locking issues during build
        Stop-Services

        # Start CosmosDB Emulator and seed if not already running
        Write-Log -Level INFO -Message "" -NoLabel
        Write-Log -Level WARN -Message "Starting CosmosDB Emulator..." -NoLabel
        $dbScript = Join-Path $PSScriptRoot ".scripts\database.ps1"
        & $dbScript start -Yes
        if ($LASTEXITCODE -ne 0) {
            Write-Log -Level ERROR -Message "  [FAIL] CosmosDB Emulator failed to start — CatanDb tests will fail" -NoLabel
            exit 1
        }
        & $dbScript seed
        # Write test params via database.ps1 (single source of emulator endpoint/key)
        $testParamsPath = Join-Path $PSScriptRoot "Tests\GameService\CatanDb\.cosmos-test-params.json"
        & $dbScript write-test-params
        Write-Log -Level INFO -Message "  [OK] CosmosDB Emulator running" -NoLabel -ForegroundColor Green

        # Run .NET tests (clean up params file on exit)
        Write-Log -Level INFO -Message "" -NoLabel
        Write-Log -Level WARN -Message "Running .NET tests..." -NoLabel
        try {
            & "$PSScriptRoot\.scripts\build.ps1"
            if ($LASTEXITCODE -ne 0) {
                Write-Log -Level ERROR -Message ".NET tests failed!" -NoLabel
                exit 1
            }
        } finally {
            if (Test-Path $testParamsPath) { Remove-Item $testParamsPath -Force }
        }
        Write-Log -Level INFO -Message "  [OK] .NET tests passed" -NoLabel -ForegroundColor Green

        # Run TypeScript tests (if react-ui exists)
        $reactUiPath = Join-Path $PSScriptRoot "react-ui"
        if (Test-Path $reactUiPath) {
            Write-Log -Level INFO -Message "" -NoLabel
            Write-Log -Level WARN -Message "Running TypeScript tests..." -NoLabel
            Push-Location $reactUiPath
            try {
                $tsTestResult = & npm run test:run 2>&1
                if ($LASTEXITCODE -ne 0) {
                    Write-Log -Level ERROR -Message "  [FAIL] TypeScript tests failed:" -NoLabel
                    Write-Log -Level DEBUG -Message $tsTestResult -NoLabel
                    exit 1
                }
                Write-Log -Level INFO -Message "  [OK] TypeScript tests passed" -NoLabel -ForegroundColor Green
            }
            finally {
                Pop-Location
            }
        }

        Write-Log -Level INFO -Message "" -NoLabel
        Write-Log -Level INFO -Message "All tests passed!" -NoLabel -ForegroundColor Green
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
            Write-Log -Level ERROR -Message "Unknown lint type: $SubCommand" -NoLabel
            Write-Log -Level WARN -Message "Valid: ./catan.ps1 lint [-All] [all|cs|ts|md|json|ps1|spell]" -NoLabel
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
            Write-Log -Level ERROR -Message "Unknown format type: $SubCommand" -NoLabel
            Write-Log -Level WARN -Message "Valid: ./catan.ps1 format [-All] [all|check|cs|ts]" -NoLabel
            exit 1
        }

        & $formatScript @formatArgs
        exit $LASTEXITCODE
    }

    "generate-types" {
        Write-Log -Level INFO -Message "Generating TypeScript types from C# models..." -NoLabel -ForegroundColor Cyan

        $reactUiPath = Join-Path $PSScriptRoot "react-ui"
        if (-not (Test-Path $reactUiPath)) {
            Write-Log -Level ERROR -Message "react-ui directory not found!" -NoLabel
            exit 1
        }

        # Generate model types from C# using TypeGenRunner (TypeGen 7.0.0)
        $typegenRunnerProject = Join-Path $PSScriptRoot "Catan3.Shared\TypeScript\TypeGenRunner\TypeGenRunner.csproj"
        Write-Log -Level INFO -Message "" -NoLabel
        Write-Log -Level WARN -Message "Running TypeGenRunner..." -NoLabel

        $typegenOutput = & dotnet run --project $typegenRunnerProject 2>&1
        if ($LASTEXITCODE -ne 0) {
            Write-Log -Level ERROR -Message "  [FAIL] TypeGen failed:" -NoLabel
            Write-Log -Level DEBUG -Message $typegenOutput -NoLabel
            exit 1
        }

        # Count generated files from output
        $fileCount = ($typegenOutput | Select-String "Generated \d+ files").Matches.Value -replace "Generated | files", ""
        Write-Log -Level INFO -Message "  [OK] Generated $fileCount TypeScript files to react-ui/types/generated/models/" -NoLabel -ForegroundColor Green

        Write-Log -Level INFO -Message "" -NoLabel
        Write-Log -Level INFO -Message "TypeScript types generated successfully!" -NoLabel -ForegroundColor Green
    }

    "replay" {
        # Redirect to recording replay for backward compatibility
        Write-Log -Level INFO -Message "Note: 'replay' command moved to 'recording replay'. Redirecting..." -NoLabel -ForegroundColor DarkYellow
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
            $azureConfig = Get-AzureConfig -ProjectRoot $PSScriptRoot
            $targetUrl = Get-AzureGameServiceUrl -AzureConfig $azureConfig
            $targetName = "Azure"
        }

        # Default location for stats export
        $defaultStatsDir = Join-Path $PSScriptRoot "."

        switch ($SubVerb) {
            "" {
                # Show stats help
                Write-Log -Level INFO -Message "" -NoLabel
                Write-Log -Level INFO -Message "Stats Management Commands" -NoLabel -ForegroundColor Cyan
                Write-Log -Level INFO -Message "=========================" -NoLabel -ForegroundColor Cyan
                Write-Log -Level INFO -Message "" -NoLabel
                Write-Log -Level WARN -Message "Usage: ./catan.ps1 stats <subcommand> [options]" -NoLabel
                Write-Log -Level INFO -Message "" -NoLabel
                Write-Log -Level INFO -Message "Subcommands:" -NoLabel
                Write-Log -Level INFO -Message "  list     - Show all player statistics" -NoLabel
                Write-Log -Level INFO -Message "  export   - Export stats to JSON file" -NoLabel
                Write-Log -Level INFO -Message "  import   - Import stats from JSON file" -NoLabel
                Write-Log -Level INFO -Message "  reset    - Delete all statistics" -NoLabel
                Write-Log -Level INFO -Message "" -NoLabel
                Write-Log -Level INFO -Message "Options:" -NoLabel
                Write-Log -Level INFO -Message "  -Local        Target local GameService (default)" -NoLabel
                Write-Log -Level INFO -Message "  -Azure        Target Azure GameService" -NoLabel
                Write-Log -Level INFO -Message "  -Json         Output as JSON (for list)" -NoLabel
                Write-Log -Level INFO -Message "  -Location     File path for export" -NoLabel
                Write-Log -Level INFO -Message "  -File         File path for import (required)" -NoLabel
                Write-Log -Level INFO -Message "  -Replace      Replace all stats with imported data" -NoLabel
                Write-Log -Level INFO -Message "  -Yes          Skip confirmation prompts" -NoLabel
                Write-Log -Level INFO -Message "" -NoLabel
                Write-Log -Level WARN -Message "Examples:" -NoLabel
                Write-Log -Level INFO -Message "  ./catan.ps1 stats list                       - List local player stats" -NoLabel
                Write-Log -Level INFO -Message "  ./catan.ps1 stats list -Azure                - List Azure player stats" -NoLabel
                Write-Log -Level INFO -Message "  ./catan.ps1 stats export                     - Export local stats" -NoLabel
                Write-Log -Level INFO -Message "  ./catan.ps1 stats export -Azure              - Export Azure stats" -NoLabel
                Write-Log -Level INFO -Message "  ./catan.ps1 stats import -File stats.json    - Import and merge" -NoLabel
                Write-Log -Level INFO -Message "  ./catan.ps1 stats import -File stats.json -Replace  - Replace all" -NoLabel
                Write-Log -Level INFO -Message "  ./catan.ps1 stats reset -Yes                 - Reset local stats" -NoLabel
                Write-Log -Level INFO -Message "" -NoLabel
            }

            "list" {
                Write-Log -Level INFO -Message "" -NoLabel
                Write-Log -Level INFO -Message "Player Statistics ($targetName)" -NoLabel -ForegroundColor Cyan
                Write-Log -Level INFO -Message "========================" -NoLabel -ForegroundColor Cyan
                Write-Log -Level INFO -Message "" -NoLabel

                # Check if service is reachable
                if (-not $Azure -and -not (Test-PortInUse $GameServicePort)) {
                    Write-Log -Level ERROR -Message "GameService is not running on port $GameServicePort" -NoLabel
                    Write-Log -Level WARN -Message "Start services first with: ./catan.ps1 run" -NoLabel
                    exit 1
                }

                try {
                    $stats = Invoke-RestMethod -Uri "$targetUrl/api/stats" -Method Get -TimeoutSec 30

                    if ($Json) {
                        $stats | ConvertTo-Json -Depth 10
                    } else {
                        if ($stats.Count -eq 0) {
                            Write-Log -Level WARN -Message "No player statistics found." -NoLabel
                        } else {
                            # Table header
                            Write-Log -Level INFO -Message ("{0,-15} {1,6} {2,5} {3,6} {4,6} {5,8}" -f "Player", "Games", "Wins", "Win%", "Best", "AvgStars") -NoLabel
                            Write-Log -Level INFO -Message ("{0,-15} {1,6} {2,5} {3,6} {4,6} {5,8}" -f "---------------", "------", "-----", "------", "------", "--------") -NoLabel

                            foreach ($player in $stats) {
                                $winRate = if ($player.gamesPlayed -gt 0) { "{0:N1}%" -f $player.winRate } else { "0.0%" }
                                $avgStars = "{0:N1}" -f $player.averageStars
                                Write-Log -Level INFO -Message ("{0,-15} {1,6} {2,5} {3,6} {4,6} {5,8}" -f $player.playerName, $player.gamesPlayed, $player.wins, $winRate, $player.highestScore, $avgStars) -NoLabel
                            }

                            Write-Log -Level INFO -Message "" -NoLabel
                            Write-Log -Level INFO -Message "Total: $($stats.Count) players" -NoLabel -ForegroundColor Green
                        }
                    }
                } catch {
                    Write-Log -Level ERROR -Message "Failed to fetch stats: $_" -NoLabel
                    exit 1
                }
            }

            "export" {
                Write-Log -Level INFO -Message "" -NoLabel
                Write-Log -Level INFO -Message "Exporting Player Statistics ($targetName)" -NoLabel -ForegroundColor Cyan
                Write-Log -Level INFO -Message "========================================" -NoLabel -ForegroundColor Cyan
                Write-Log -Level INFO -Message "" -NoLabel

                # Check if service is reachable
                if (-not $Azure -and -not (Test-PortInUse $GameServicePort)) {
                    Write-Log -Level ERROR -Message "GameService is not running on port $GameServicePort" -NoLabel
                    Write-Log -Level WARN -Message "Start services first with: ./catan.ps1 run" -NoLabel
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

                    Write-Log -Level INFO -Message "Exported $($export.players.Count) player(s) to:" -NoLabel -ForegroundColor Green
                    Write-Log -Level INFO -Message "  $outputPath" -NoLabel
                } catch {
                    Write-Log -Level ERROR -Message "Failed to export stats: $_" -NoLabel
                    exit 1
                }
            }

            "import" {
                Write-Log -Level INFO -Message "" -NoLabel
                Write-Log -Level INFO -Message "Importing Player Statistics to $targetName" -NoLabel -ForegroundColor Cyan
                Write-Log -Level INFO -Message "===========================================" -NoLabel -ForegroundColor Cyan
                Write-Log -Level INFO -Message "" -NoLabel

                # Check if service is reachable
                if (-not $Azure -and -not (Test-PortInUse $GameServicePort)) {
                    Write-Log -Level ERROR -Message "GameService is not running on port $GameServicePort" -NoLabel
                    Write-Log -Level WARN -Message "Start services first with: ./catan.ps1 run" -NoLabel
                    exit 1
                }

                if (-not $File) {
                    Write-Log -Level ERROR -Message "ERROR: -File parameter is required for import" -NoLabel
                    Write-Log -Level WARN -Message "Usage: ./catan.ps1 stats import -File <path>" -NoLabel
                    exit 1
                }

                if (-not (Test-Path $File)) {
                    Write-Log -Level ERROR -Message "ERROR: File not found: $File" -NoLabel
                    exit 1
                }

                try {
                    $document = Get-Content $File -Raw | ConvertFrom-Json
                    Write-Log -Level INFO -Message "Found $($document.players.Count) player(s) in file" -NoLabel -ForegroundColor Green

                    $mode = if ($Replace) { "replace" } else { "merge" }
                    Write-Log -Level DEBUG -Message "Import mode: $mode" -NoLabel
                    Write-Log -Level INFO -Message "" -NoLabel

                    $importRequest = @{
                        document = $document
                        replace = $Replace.IsPresent
                    }

                    $response = Invoke-RestMethod -Uri "$targetUrl/api/stats/import" `
                        -Method Post `
                        -ContentType "application/json" `
                        -Body ($importRequest | ConvertTo-Json -Depth 10) `
                        -TimeoutSec 30

                    Write-Log -Level INFO -Message "Import complete:" -NoLabel -ForegroundColor Green
                    Write-Log -Level INFO -Message "  Imported: $($response.imported)" -NoLabel
                    Write-Log -Level INFO -Message "  Merged:   $($response.merged)" -NoLabel
                    Write-Log -Level INFO -Message "  Skipped:  $($response.skipped)" -NoLabel
                } catch {
                    Write-Log -Level ERROR -Message "Failed to import stats: $_" -NoLabel
                    exit 1
                }
            }

            "reset" {
                Write-Log -Level INFO -Message "" -NoLabel
                Write-Log -Level INFO -Message "Reset Player Statistics ($targetName)" -NoLabel -ForegroundColor Cyan
                Write-Log -Level INFO -Message "=====================================" -NoLabel -ForegroundColor Cyan
                Write-Log -Level INFO -Message "" -NoLabel

                # Check if service is reachable
                if (-not $Azure -and -not (Test-PortInUse $GameServicePort)) {
                    Write-Log -Level ERROR -Message "GameService is not running on port $GameServicePort" -NoLabel
                    Write-Log -Level WARN -Message "Start services first with: ./catan.ps1 run" -NoLabel
                    exit 1
                }

                if (-not $Yes) {
                    Write-Log -Level ERROR -Message "WARNING: This will delete ALL player statistics on $targetName!" -NoLabel
                    $confirm = Read-Host "Type 'yes' to confirm"
                    if ($confirm -ne "yes") {
                        Write-Log -Level WARN -Message "Aborted." -NoLabel
                        exit 0
                    }
                }

                try {
                    $response = Invoke-RestMethod -Uri "$targetUrl/api/stats" -Method Delete -TimeoutSec 30
                    Write-Log -Level INFO -Message "Reset complete: $($response.playersReset) player(s) reset" -NoLabel -ForegroundColor Green
                } catch {
                    Write-Log -Level ERROR -Message "Failed to reset stats: $_" -NoLabel
                    exit 1
                }
            }

            default {
                Write-Log -Level ERROR -Message "Unknown stats subcommand: $SubVerb" -NoLabel
                Write-Log -Level WARN -Message "Run './catan.ps1 stats' for help" -NoLabel
                exit 1
            }
        }
    }

    "recording" {
        # Determine target URL (local or Azure)
        $targetUrl = $GameServiceUrl
        $targetName = "Local"

        if ($Azure) {
            $azureConfig = Get-AzureConfig -ProjectRoot $PSScriptRoot
            $targetUrl = Get-AzureGameServiceUrl -AzureConfig $azureConfig
            $targetName = "Azure"
        }

        # Default recordings directory
        $defaultRecordingsDir = Join-Path $PSScriptRoot "Catan3.GameService\Default Data\Recordings"
        $recordingsDir = if ($Location) { $Location } else { $defaultRecordingsDir }

        switch ($SubCommand) {
            "list" {
                Write-Log -Level INFO -Message "" -NoLabel
                Write-Log -Level INFO -Message "Recordings ($targetName)" -NoLabel -ForegroundColor Cyan
                Write-Log -Level INFO -Message "==================" -NoLabel -ForegroundColor Cyan
                Write-Log -Level INFO -Message "" -NoLabel

                # Check if service is reachable
                if (-not $Azure -and -not (Test-PortInUse $GameServicePort)) {
                    Write-Log -Level ERROR -Message "GameService is not running on port $GameServicePort" -NoLabel
                    Write-Log -Level WARN -Message "Start services first with: ./catan.ps1 run" -NoLabel
                    exit 1
                }

                try {
                    $recordings = Invoke-RestMethod -Uri "$targetUrl/api/recordings" -Method Get -TimeoutSec 30
                } catch {
                    Write-Log -Level ERROR -Message "Failed to fetch recordings from $targetUrl : $_" -NoLabel
                    exit 1
                }

                if ($recordings.Count -eq 0) {
                    Write-Log -Level WARN -Message "No recordings found." -NoLabel
                    exit 0
                }

                if ($Json) {
                    $recordings | ConvertTo-Json -Depth 10
                } else {
                    # Table format with RecordingID and GameID columns
                    # Show first 8 chars of each GUID for readability
                    Write-Log -Level INFO -Message ("{0,-10} {1,-10} {2,-22} {3,-8} {4,-8} {5}" -f "RecID", "GameID", "Name", "Actions", "Players", "Type") -NoLabel
                    Write-Log -Level INFO -Message ("{0,-10} {1,-10} {2,-22} {3,-8} {4,-8} {5}" -f "----------", "----------", "----------------------", "--------", "--------", "---------") -NoLabel
                    foreach ($r in $recordings) {
                        $displayName = if ($r.name.Length -gt 21) { $r.name.Substring(0, 18) + "..." } else { $r.name }
                        $shortRecId = if ($r.id.Length -ge 8) { $r.id.Substring(0, 8) } else { $r.id }
                        $shortGameId = if ($r.gameId -and $r.gameId.Length -ge 8) { $r.gameId.Substring(0, 8) } else { $r.gameId }
                        Write-Log -Level INFO -Message ("{0,-10} {1,-10} {2,-22} {3,-8} {4,-8} {5}" -f $shortRecId, $shortGameId, $displayName, $r.actionCount, $r.playerCount, $r.gameType) -NoLabel
                    }
                }
                Write-Log -Level INFO -Message "" -NoLabel
                Write-Log -Level INFO -Message "Total: $($recordings.Count) recording(s)" -NoLabel -ForegroundColor Green
            }

            "save" {
                Write-Log -Level INFO -Message "" -NoLabel
                Write-Log -Level INFO -Message "Saving Recordings ($targetName)" -NoLabel -ForegroundColor Cyan
                Write-Log -Level INFO -Message "========================" -NoLabel -ForegroundColor Cyan
                Write-Log -Level INFO -Message "" -NoLabel

                # Check if service is reachable
                if (-not $Azure -and -not (Test-PortInUse $GameServicePort)) {
                    Write-Log -Level ERROR -Message "GameService is not running on port $GameServicePort" -NoLabel
                    Write-Log -Level WARN -Message "Start services first with: ./catan.ps1 run" -NoLabel
                    exit 1
                }

                # Ensure output directory exists
                if (-not (Test-Path $recordingsDir)) {
                    New-Item -ItemType Directory -Path $recordingsDir -Force | Out-Null
                    Write-Log -Level WARN -Message "Created directory: $recordingsDir" -NoLabel
                }

                # Fetch recordings list
                try {
                    $recordings = Invoke-RestMethod -Uri "$targetUrl/api/recordings" -Method Get -TimeoutSec 30
                } catch {
                    Write-Log -Level ERROR -Message "Failed to fetch recordings: $_" -NoLabel
                    exit 1
                }

                # Filter by name if specified
                if ($Name) {
                    $recordings = $recordings | Where-Object { $_.name -like $Name }
                }

                if ($recordings.Count -eq 0) {
                    Write-Log -Level WARN -Message "No recordings found matching criteria." -NoLabel
                    exit 0
                }

                Write-Log -Level INFO -Message "Found $($recordings.Count) recording(s)" -NoLabel -ForegroundColor Green
                Write-Log -Level INFO -Message "Saving to: $recordingsDir" -NoLabel
                Write-Log -Level INFO -Message "" -NoLabel

                $saved = 0
                foreach ($recording in $recordings) {
                    $safeName = $recording.name -replace '[^\w\-\.]', '-'
                    $filePath = Join-Path $recordingsDir "$safeName.json"

                    Write-Log -Level INFO -Message "  Saving: $($recording.name)..." -NoLabel -NoNewline

                    # Fetch full recording data
                    try {
                        $fullRecording = Invoke-RestMethod -Uri "$targetUrl/api/recording/$($recording.id)" -Method Get -TimeoutSec 30
                    } catch {
                        Write-Log -Level ERROR -Message " failed to fetch: $_" -NoLabel
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
                    Write-Log -Level INFO -Message " saved" -NoLabel -ForegroundColor Green
                    $saved++
                }

                Write-Log -Level INFO -Message "" -NoLabel
                Write-Log -Level INFO -Message "Saved $saved recording(s) to: $recordingsDir" -NoLabel -ForegroundColor Green
            }

            "load" {
                Write-Log -Level INFO -Message "" -NoLabel
                Write-Log -Level INFO -Message "Loading Recordings to $targetName" -NoLabel -ForegroundColor Cyan
                Write-Log -Level INFO -Message "==========================" -NoLabel -ForegroundColor Cyan
                Write-Log -Level INFO -Message "" -NoLabel

                # Check if service is reachable
                if (-not $Azure -and -not (Test-PortInUse $GameServicePort)) {
                    Write-Log -Level ERROR -Message "GameService is not running on port $GameServicePort" -NoLabel
                    Write-Log -Level WARN -Message "Start services first with: ./catan.ps1 run" -NoLabel
                    exit 1
                }

                # For Azure, verify service is healthy and import endpoint exists
                if ($Azure) {
                    Write-Log -Level INFO -Message "Checking Azure service health..." -NoLabel -NoNewline
                    try {
                        $null = Invoke-RestMethod -Uri "$targetUrl/health" -TimeoutSec 10
                        Write-Log -Level INFO -Message " OK" -NoLabel -ForegroundColor Green
                    } catch {
                        Write-Log -Level ERROR -Message " FAILED" -NoLabel
                        Write-Log -Level ERROR -Message "Azure service is not responding at $targetUrl/health" -NoLabel
                        Write-Log -Level WARN -Message "Check deployment status or try again later." -NoLabel
                        exit 1
                    }

                    # Verify import endpoint exists by checking recordings list first
                    Write-Log -Level INFO -Message "Checking import endpoint..." -NoLabel -NoNewline
                    try {
                        # A simple GET to /api/recordings verifies the recording endpoints are deployed
                        $null = Invoke-RestMethod -Uri "$targetUrl/api/recordings" -TimeoutSec 10
                        Write-Log -Level INFO -Message " OK" -NoLabel -ForegroundColor Green
                    } catch {
                        Write-Log -Level ERROR -Message " FAILED" -NoLabel
                        Write-Log -Level ERROR -Message "Recording API not available. Deployment may be in progress." -NoLabel
                        Write-Log -Level WARN -Message "Wait for deployment to complete and try again." -NoLabel
                        exit 1
                    }
                    Write-Log -Level INFO -Message "" -NoLabel
                }

                if (-not (Test-Path $recordingsDir)) {
                    Write-Log -Level ERROR -Message "Recordings directory not found: $recordingsDir" -NoLabel
                    Write-Log -Level WARN -Message "Run './catan.ps1 recording save' first to export recordings" -NoLabel
                    exit 1
                }

                # Get JSON files
                $jsonFiles = Get-ChildItem -Path $recordingsDir -Filter "*.json" -ErrorAction SilentlyContinue

                # Filter by name if specified
                if ($Name) {
                    $jsonFiles = $jsonFiles | Where-Object { $_.Name -like $Name }
                }

                if ($jsonFiles.Count -eq 0) {
                    Write-Log -Level WARN -Message "No recording files found in $recordingsDir" -NoLabel
                    exit 0
                }

                Write-Log -Level INFO -Message "Found $($jsonFiles.Count) recording file(s)" -NoLabel -ForegroundColor Green
                Write-Log -Level INFO -Message "Loading to: $targetUrl" -NoLabel
                Write-Log -Level INFO -Message "" -NoLabel

                $imported = 0
                $skipped = 0
                $failed = 0

                foreach ($file in $jsonFiles) {
                    Write-Log -Level INFO -Message "  Loading: $($file.Name)..." -NoLabel -NoNewline

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

                        Write-Log -Level INFO -Message " imported" -NoLabel -ForegroundColor Green
                        $imported++
                    }
                    catch {
                        if ($_.Exception.Response.StatusCode -eq 409) {
                            Write-Log -Level INFO -Message " already exists" -NoLabel
                            $skipped++
                        } else {
                            Write-Log -Level ERROR -Message " failed: $_" -NoLabel
                            $failed++
                        }
                    }
                }

                Write-Log -Level INFO -Message "" -NoLabel
                Write-Log -Level INFO -Message "Results: $imported imported, $skipped skipped, $failed failed" -ForegroundColor $(if ($failed -eq 0) { "Green" } else { "Red" }) -NoLabel
            }

            "delete" {
                if (-not $Name) {
                    Write-Log -Level ERROR -Message "Error: -Name parameter is required for delete" -NoLabel
                    Write-Log -Level WARN -Message "Usage: ./catan.ps1 recording delete -Name <name-or-id>" -NoLabel
                    exit 1
                }

                Write-Log -Level INFO -Message "" -NoLabel
                Write-Log -Level INFO -Message "Deleting Recording from $targetName" -NoLabel -ForegroundColor Cyan
                Write-Log -Level INFO -Message "=============================" -NoLabel -ForegroundColor Cyan
                Write-Log -Level INFO -Message "" -NoLabel

                # Check if service is reachable
                if (-not $Azure -and -not (Test-PortInUse $GameServicePort)) {
                    Write-Log -Level ERROR -Message "GameService is not running on port $GameServicePort" -NoLabel
                    Write-Log -Level WARN -Message "Start services first with: ./catan.ps1 run" -NoLabel
                    exit 1
                }

                # Find recording by name or ID
                try {
                    $recordings = Invoke-RestMethod -Uri "$targetUrl/api/recordings" -Method Get -TimeoutSec 30
                } catch {
                    Write-Log -Level ERROR -Message "Failed to fetch recordings: $_" -NoLabel
                    exit 1
                }

                $toDelete = $recordings | Where-Object { $_.name -eq $Name -or $_.id -eq $Name }

                if ($toDelete.Count -eq 0) {
                    Write-Log -Level ERROR -Message "Recording not found: $Name" -NoLabel
                    exit 1
                }

                if ($toDelete.Count -gt 1) {
                    Write-Log -Level ERROR -Message "Multiple recordings match '$Name'. Please use the full ID:" -NoLabel
                    foreach ($r in $toDelete) {
                        Write-Log -Level INFO -Message "  $($r.id) - $($r.name)" -NoLabel
                    }
                    exit 1
                }

                $recording = $toDelete[0]

                if (-not $Yes) {
                    Write-Log -Level WARN -Message "About to delete: $($recording.name) ($($recording.id))" -NoLabel
                    $confirm = Read-Host "Are you sure? (y/N)"
                    if ($confirm -ne 'y' -and $confirm -ne 'Y') {
                        Write-Log -Level INFO -Message "Cancelled." -NoLabel
                        exit 0
                    }
                }

                try {
                    Invoke-RestMethod -Uri "$targetUrl/api/recording/$($recording.id)" -Method Delete -TimeoutSec 30 | Out-Null
                    Write-Log -Level INFO -Message "Deleted: $($recording.name)" -NoLabel -ForegroundColor Green
                } catch {
                    Write-Log -Level ERROR -Message "Failed to delete: $_" -NoLabel
                    exit 1
                }
            }

            "replay" {
                Write-Log -Level INFO -Message "" -NoLabel
                Write-Log -Level INFO -Message "Running Recording Replay Tests ($targetName)" -NoLabel -ForegroundColor Cyan
                Write-Log -Level INFO -Message "======================================" -NoLabel -ForegroundColor Cyan
                Write-Log -Level INFO -Message "" -NoLabel

                # Check if service is reachable
                if (-not $Azure -and -not (Test-PortInUse $GameServicePort)) {
                    Write-Log -Level ERROR -Message "GameService is not running on port $GameServicePort" -NoLabel
                    Write-Log -Level WARN -Message "Start services first with: ./catan.ps1 run" -NoLabel
                    exit 1
                }

                # For Azure, verify service is healthy first
                if ($Azure) {
                    Write-Log -Level INFO -Message "Checking Azure service health..." -NoLabel -NoNewline
                    try {
                        $null = Invoke-RestMethod -Uri "$targetUrl/health" -TimeoutSec 10
                        Write-Log -Level INFO -Message " OK" -NoLabel -ForegroundColor Green
                    } catch {
                        Write-Log -Level ERROR -Message " FAILED" -NoLabel
                        Write-Log -Level ERROR -Message "Azure service is not responding at $targetUrl/health" -NoLabel
                        Write-Log -Level WARN -Message "Check deployment status or try again later." -NoLabel
                        exit 1
                    }
                    Write-Log -Level INFO -Message "" -NoLabel
                }

                # Get all recordings
                try {
                    $recordings = Invoke-RestMethod -Uri "$targetUrl/api/recordings" -Method Get -TimeoutSec 30
                } catch {
                    Write-Log -Level ERROR -Message "Failed to fetch recordings: $_" -NoLabel
                    exit 1
                }

                # Filter by name if specified
                if ($Name) {
                    $recordings = $recordings | Where-Object { $_.name -like $Name }
                }

                if ($recordings.Count -eq 0) {
                    Write-Log -Level WARN -Message "No recordings found. Create some recordings first!" -NoLabel
                    exit 0
                }

                Write-Log -Level INFO -Message "Found $($recordings.Count) recording(s)" -NoLabel -ForegroundColor Green
                Write-Log -Level INFO -Message "" -NoLabel

                $passed = 0
                $failed = 0
                $failedTests = @()

                foreach ($recording in $recordings) {
                    Write-Log -Level INFO -Message "  Running: $($recording.name) ($($recording.actionCount) actions)... " -NoLabel -NoNewline

                    try {
                        $result = Invoke-RestMethod -Uri "$targetUrl/api/recording/$($recording.id)/replay" -Method Post -TimeoutSec 120

                        if ($result.success) {
                            Write-Log -Level INFO -Message "PASS" -NoLabel -ForegroundColor Green
                            $passed++
                        } else {
                            Write-Log -Level ERROR -Message "FAIL" -NoLabel
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
                        Write-Log -Level ERROR -Message "ERROR" -NoLabel
                        $failed++
                        $failedTests += @{
                            Name = $recording.name
                            Error = $_.Exception.Message
                        }
                    }
                }

                Write-Log -Level INFO -Message "" -NoLabel
                Write-Log -Level INFO -Message "Results: $passed passed, $failed failed" -ForegroundColor $(if ($failed -eq 0) { "Green" } else { "Red" }) -NoLabel

                if ($failedTests.Count -gt 0) {
                    Write-Log -Level INFO -Message "" -NoLabel
                    Write-Log -Level ERROR -Message "Failed Tests:" -NoLabel
                    foreach ($test in $failedTests) {
                        Write-Log -Level ERROR -Message "  - $($test.Name): $($test.Error)" -NoLabel
                        if ($test.Expected -and $test.Actual) {
                            Write-Log -Level INFO -Message "    Expected: $($test.Expected), Actual: $($test.Actual)" -NoLabel
                        }
                    }
                    exit 1
                }

                Write-Log -Level INFO -Message "" -NoLabel
                Write-Log -Level INFO -Message "All recording replay tests passed!" -NoLabel -ForegroundColor Green
            }

            default {
                Write-Log -Level INFO -Message "" -NoLabel
                Write-Log -Level INFO -Message "Recording Management Commands" -NoLabel -ForegroundColor Cyan
                Write-Log -Level INFO -Message "=============================" -NoLabel -ForegroundColor Cyan
                Write-Log -Level INFO -Message "" -NoLabel
                Write-Log -Level WARN -Message "Usage: ./catan.ps1 recording <subcommand> [options]" -NoLabel
                Write-Log -Level INFO -Message "" -NoLabel
                Write-Log -Level WARN -Message "Subcommands:" -NoLabel
                Write-Log -Level INFO -Message "  list     - List all recordings" -NoLabel
                Write-Log -Level INFO -Message "  save     - Save recordings to JSON files" -NoLabel
                Write-Log -Level INFO -Message "  load     - Load recordings from JSON files" -NoLabel
                Write-Log -Level INFO -Message "  delete   - Delete a recording" -NoLabel
                Write-Log -Level INFO -Message "  replay   - Run replay tests" -NoLabel
                Write-Log -Level INFO -Message "" -NoLabel
                Write-Log -Level WARN -Message "Options:" -NoLabel
                Write-Log -Level INFO -Message "  -Local        Target local GameService (default)" -NoLabel
                Write-Log -Level INFO -Message "  -Azure        Target Azure GameService" -NoLabel
                Write-Log -Level INFO -Message "  -Name <name>  Filter by recording name (supports wildcards)" -NoLabel
                Write-Log -Level INFO -Message "  -Location     Directory for save/load (default: Default Data/Recordings/)" -NoLabel
                Write-Log -Level INFO -Message "  -Json         Output as JSON (for list)" -NoLabel
                Write-Log -Level INFO -Message "  -Yes          Skip confirmation prompts" -NoLabel
                Write-Log -Level INFO -Message "" -NoLabel
                Write-Log -Level WARN -Message "Examples:" -NoLabel
                Write-Log -Level INFO -Message "  ./catan.ps1 recording list                    - List local recordings" -NoLabel
                Write-Log -Level INFO -Message "  ./catan.ps1 recording list -Azure             - List Azure recordings" -NoLabel
                Write-Log -Level INFO -Message "  ./catan.ps1 recording save                    - Save all local recordings" -NoLabel
                Write-Log -Level INFO -Message "  ./catan.ps1 recording save -Azure             - Save all Azure recordings" -NoLabel
                Write-Log -Level INFO -Message "  ./catan.ps1 recording load                    - Load recordings to local" -NoLabel
                Write-Log -Level INFO -Message "  ./catan.ps1 recording load -Azure             - Load recordings to Azure" -NoLabel
                Write-Log -Level INFO -Message "  ./catan.ps1 recording replay                  - Run all replay tests locally" -NoLabel
                Write-Log -Level INFO -Message "  ./catan.ps1 recording replay -Azure           - Run replay tests on Azure" -NoLabel
                Write-Log -Level INFO -Message "  ./catan.ps1 recording delete -Name 'Test*'    - Delete matching recording" -NoLabel
                Write-Log -Level INFO -Message "" -NoLabel

                if ($SubCommand) {
                    Write-Log -Level ERROR -Message "Unknown subcommand: $SubCommand" -NoLabel
                    exit 1
                }
            }
        }
    }

    "doctor" {
        Write-Log -Level INFO -Message "" -NoLabel
        Write-Log -Level INFO -Message "Catan3 Health Check" -NoLabel -ForegroundColor Cyan
        Write-Log -Level INFO -Message "===================" -NoLabel -ForegroundColor Cyan
        Write-Log -Level INFO -Message "" -NoLabel

        # Check dependencies
        Write-Log -Level WARN -Message "Checking dependencies..." -NoLabel
        & "$PSScriptRoot\.scripts\dependencies.ps1" -Doctor
        Write-Log -Level INFO -Message "" -NoLabel

        # Check react-ui npm packages (Prettier, ESLint, etc.)
        $reactUiCheck = Join-Path $PSScriptRoot "react-ui"
        if (Test-Path $reactUiCheck) {
            Write-Log -Level WARN -Message "Checking react-ui npm packages..." -NoLabel
            $nodeModules = Join-Path $reactUiCheck "node_modules"
            $prettierBin = Join-Path $nodeModules ".bin\prettier"
            $eslintBin = Join-Path $nodeModules ".bin\eslint"

            if (-not (Test-Path $nodeModules)) {
                Write-Log -Level WARN -Message "  [WARN] node_modules not found. Run: cd react-ui && npm install" -NoLabel
            }
            else {
                $allGood = $true
                if (Test-Path $prettierBin) {
                    $prettierVer = & npx --prefix $reactUiCheck prettier --version 2>$null
                    Write-Log -Level INFO -Message "  [OK] Prettier v$prettierVer" -NoLabel -ForegroundColor Green
                }
                else {
                    Write-Log -Level WARN -Message "  [WARN] Prettier not found. Run: cd react-ui && npm install" -NoLabel
                    $allGood = $false
                }
                if (Test-Path $eslintBin) {
                    $eslintVer = & npx --prefix $reactUiCheck eslint --version 2>$null
                    Write-Log -Level INFO -Message "  [OK] ESLint v$eslintVer" -NoLabel -ForegroundColor Green
                }
                else {
                    Write-Log -Level WARN -Message "  [WARN] ESLint not found. Run: cd react-ui && npm install" -NoLabel
                    $allGood = $false
                }
                if ($allGood) {
                    Write-Log -Level INFO -Message "  [OK] All react-ui npm packages installed" -NoLabel -ForegroundColor Green
                }
            }
            Write-Log -Level INFO -Message "" -NoLabel
        }

        # Check database (Cosmos emulator)
        Write-Log -Level WARN -Message "Checking database..." -NoLabel
        $dbScript = Join-Path $PSScriptRoot ".scripts/database.ps1"
        & pwsh $dbScript doctor
        if ($LASTEXITCODE -ne 0) {
            Write-Log -Level INFO -Message "" -NoLabel
            Write-Log -Level WARN -Message "Some issues found. Run './catan.ps1 database install' to fix." -NoLabel
            exit 1
        }
    }

    "install" {
        Write-Log -Level INFO -Message "" -NoLabel
        Write-Log -Level INFO -Message "Catan3 Installation" -NoLabel -ForegroundColor Cyan
        Write-Log -Level INFO -Message "===================" -NoLabel -ForegroundColor Cyan
        Write-Log -Level INFO -Message "" -NoLabel

        # Install dependencies (dependencies.ps1 already checks if installed)
        Write-Log -Level WARN -Message "Checking dependencies..." -NoLabel
        & "$PSScriptRoot\.scripts\dependencies.ps1" -Install -Yes:$Yes
        Write-Log -Level INFO -Message "" -NoLabel

        # Install database (Cosmos emulator)
        Write-Log -Level WARN -Message "Installing database..." -NoLabel
        $dbScript = Join-Path $PSScriptRoot ".scripts/database.ps1"
        & pwsh $dbScript install
        if ($LASTEXITCODE -ne 0) {
            Write-Log -Level ERROR -Message "Database installation failed!" -NoLabel
            exit 1
        }
        Write-Log -Level INFO -Message "" -NoLabel
        Write-Log -Level INFO -Message "Installation complete!" -NoLabel -ForegroundColor Green
    }

    "run" {
        Write-Log -Level INFO -Message "Building..." -NoLabel -ForegroundColor Cyan
        & "$PSScriptRoot\.scripts\build.ps1" -NoTest

        if ($LASTEXITCODE -ne 0) {
            Write-Log -Level ERROR -Message "Build failed! Cannot start services." -NoLabel
            exit 1
        }
        Write-Log -Level INFO -Message "Build completed successfully!" -NoLabel -ForegroundColor Green
        Write-Log -Level INFO -Message "" -NoLabel

        # Ensure database is initialized
        if (-not (Initialize-Database)) {
            Write-Log -Level ERROR -Message "Failed to initialize database. Cannot start services." -NoLabel
            exit 1
        }

        # Check if GameService is already running AND responding
        $gameServiceRunning = $false
        if (Test-PortInUse -Port $GameServicePort) {
            # Port is in use - verify service is actually responding
            $responding = Wait-ForService -Url "$GameServiceUrl/health" -TimeoutSeconds 3
            if ($responding) {
                Write-Log -Level INFO -Message "GameService already running on port $GameServicePort" -NoLabel -ForegroundColor Green
                $gameServiceRunning = $true
            }
            else {
                Write-Log -Level WARN -Message "Port $GameServicePort in use but service not responding. Killing stale process..." -NoLabel
                Stop-ProcessOnPort -Port $GameServicePort
                Start-Sleep -Milliseconds 500
            }
        }

        if (-not $gameServiceRunning) {
            Start-GameService -NetworkBinding:$Network
        }

        # Start React UI
        $reactUIRunning = $false
        if (Test-PortInUse -Port $ReactUIPort) {
            $responding = Wait-ForService -Url $ReactUIUrl -TimeoutSeconds 3
            if ($responding) {
                Write-Log -Level INFO -Message "React UI already running on port $ReactUIPort" -NoLabel -ForegroundColor Green
                $reactUIRunning = $true
            }
            else {
                Write-Log -Level WARN -Message "Port $ReactUIPort in use but service not responding. Killing stale process..." -NoLabel
                Stop-ProcessOnPort -Port $ReactUIPort
                Start-Sleep -Milliseconds 500
            }
        }

        if (-not $reactUIRunning) {
            Start-ReactUI
        }

        Write-Log -Level INFO -Message "" -NoLabel
        Write-Log -Level INFO -Message "Services running:" -NoLabel -ForegroundColor Green
        Write-Log -Level INFO -Message "  GameService: $GameServiceUrl" -NoLabel -ForegroundColor White
        Write-Log -Level INFO -Message "  React UI:    $ReactUIUrl" -NoLabel -ForegroundColor White

        if ($Network) {
            # Get the local IP address for network access
            if ($IsMacOS -or $IsLinux) {
                $localIp = (ifconfig | grep "inet " | grep -v "127.0.0.1" | head -1 | awk '{print $2}') 2>$null
            } else {
                $localIp = (Get-NetIPAddress -AddressFamily IPv4 | Where-Object { $_.IPAddress -notlike "127.*" -and $_.PrefixOrigin -ne "WellKnown" } | Select-Object -First 1).IPAddress
            }
            if ($localIp) {
                Write-Log -Level INFO -Message "" -NoLabel
                Write-Log -Level INFO -Message "Network access (for iPhone simulator, other devices):" -NoLabel -ForegroundColor Cyan
                Write-Log -Level INFO -Message "  React UI:    http://${localIp}:$ReactUIPort" -NoLabel -ForegroundColor White
            }
        }

        Write-Log -Level INFO -Message "" -NoLabel
        if ($IsWindows) {
            Write-Log -Level WARN -Message "Press Ctrl+C in service windows to stop them." -NoLabel
        } else {
            Write-Log -Level WARN -Message "Use './catan.ps1 stop' to stop services. Logs: .gameservice.log, .webui.log" -NoLabel
        }
    }

    "debug" {
        Write-Log -Level INFO -Message "Debug Mode" -NoLabel -ForegroundColor Cyan
        Write-Log -Level INFO -Message "" -NoLabel
        Write-Log -Level INFO -Message "For debugging, use VS Code with the configured launch profiles:" -NoLabel -ForegroundColor White
        Write-Log -Level INFO -Message "" -NoLabel
        Write-Log -Level WARN -Message "  1. Open VS Code in the Catan directory" -NoLabel
        Write-Log -Level WARN -Message "  2. Press F5 or use Run > Start Debugging" -NoLabel
        Write-Log -Level WARN -Message "  3. Select 'GameService' launch configuration" -NoLabel
        Write-Log -Level INFO -Message "" -NoLabel
        Write-Log -Level INFO -Message "This will launch GameService with debugger attached." -NoLabel -ForegroundColor White
        Write-Log -Level INFO -Message "" -NoLabel
        Write-Log -Level INFO -Message "Debug configurations:" -NoLabel -ForegroundColor White
        Write-Log -Level INFO -Message "  - 'Debug GameService' - GameService only" -NoLabel
        Write-Log -Level INFO -Message "" -NoLabel

        # Offer to open VS Code
        $openVSCode = Read-Host "Open VS Code now? (y/n)"
        if ($openVSCode -eq 'y') {
            Start-Process code -ArgumentList $PSScriptRoot
        }
    }

    "clean" {
        $cleanDatabase = ($SubCommand -eq "database")

        if ($cleanDatabase) {
            Write-Log -Level INFO -Message "Cleaning project (including database)..." -NoLabel -ForegroundColor Cyan
        } else {
            Write-Log -Level INFO -Message "Cleaning project (preserving database)..." -NoLabel -ForegroundColor Cyan
        }

        # Stop any running services first
        Stop-Services

        # Only clean database if explicitly requested
        if ($cleanDatabase) {
            $dbScript = Join-Path $PSScriptRoot ".scripts/database.ps1"
            & pwsh $dbScript nuke-containers
        }

        # Clean build artifacts
        Write-Log -Level WARN -Message "Cleaning build artifacts..." -NoLabel
        $projectsToClean = @(
            "Catan3.Shared/Catan3.Shared.csproj",
            "Catan3.GameService/Catan3.GameService.csproj",
            "Catan3.CLI/Catan3.CLI.csproj",
            "Tests/GameService/Catan3.Tests.GameService.csproj"
        )
        foreach ($proj in $projectsToClean) {
            $projPath = Join-Path $PSScriptRoot $proj
            if (Test-Path $projPath) {
                & dotnet clean $projPath --verbosity quiet 2>$null
            }
        }
        Write-Log -Level INFO -Message "Build artifacts cleaned." -NoLabel -ForegroundColor Green

        # Remove bin/obj directories for thorough clean
        Write-Log -Level WARN -Message "Removing bin/obj directories..." -NoLabel
        Get-ChildItem -Path $PSScriptRoot -Include bin, obj -Recurse -Directory |
            ForEach-Object {
                Remove-Item $_.FullName -Recurse -Force -ErrorAction SilentlyContinue
            }

        Write-Log -Level INFO -Message "" -NoLabel
        Write-Log -Level INFO -Message "Clean completed!" -NoLabel -ForegroundColor Green
        if (-not $cleanDatabase) {
            Write-Log -Level INFO -Message "Database preserved. Use './catan.ps1 clean database' to also clean database." -NoLabel
        }
        Write-Log -Level INFO -Message "Run './catan.ps1 build' to rebuild, or './catan.ps1 run' to rebuild and run." -NoLabel -ForegroundColor White
        exit 0
    }

    "stop" {
        Stop-Services
        exit 0
    }

    "restart" {
        Write-Log -Level INFO -Message "Restarting services..." -NoLabel -ForegroundColor Cyan
        Stop-Services
        Start-Sleep -Milliseconds 500

        # Ensure database is initialized
        if (-not (Initialize-Database)) {
            Write-Log -Level ERROR -Message "Failed to initialize database. Cannot start services." -NoLabel
            exit 1
        }

        Start-GameService
        Start-ReactUI

        Write-Log -Level INFO -Message "" -NoLabel
        Write-Log -Level INFO -Message "Services restarted:" -NoLabel -ForegroundColor Green
        Write-Log -Level INFO -Message "  GameService: $GameServiceUrl" -NoLabel -ForegroundColor White
        Write-Log -Level INFO -Message "  React UI:    $ReactUIUrl" -NoLabel -ForegroundColor White
    }

    "update" {
        # Terminate all Terminal windows on macOS if requested
        if ($Terminate -and $IsMacOS) {
            Write-Log -Level WARN -Message "Terminating all Terminal windows..." -NoLabel
            try {
                # Use killall to force-terminate Terminal without confirmation dialog
                & killall Terminal 2>$null
                Start-Sleep -Milliseconds 500
                Write-Log -Level INFO -Message "Terminal windows closed." -NoLabel -ForegroundColor Green
            }
            catch {
                Write-Log -Level INFO -Message "Note: Could not close Terminal windows (may not be running)" -NoLabel -ForegroundColor DarkYellow
            }
        }
        elseif ($Terminate -and -not $IsMacOS) {
            Write-Log -Level INFO -Message "Note: -Terminate switch is only supported on macOS" -NoLabel -ForegroundColor DarkYellow
        }

        # Build GameService (always needed)
        Write-Log -Level INFO -Message "Rebuilding GameService..." -NoLabel -ForegroundColor Cyan
        $gameServicePath = Join-Path $PSScriptRoot "Catan3.GameService"
        & dotnet build $gameServicePath --verbosity quiet
        if ($LASTEXITCODE -ne 0) {
            Write-Log -Level ERROR -Message "GameService build failed!" -NoLabel
            exit 1
        }
        Write-Log -Level INFO -Message "GameService rebuilt." -NoLabel -ForegroundColor Green

        # Check what services are running
        $gameRunning = Test-PortInUse -Port $GameServicePort
        $reactRunning = Test-PortInUse -Port $ReactUIPort

        if ($gameRunning -or $reactRunning) {
            Write-Log -Level WARN -Message "Restarting services..." -NoLabel
            Stop-Services
            Start-Sleep -Milliseconds 500

            if ($gameRunning) {
                Start-GameService
            }
            if ($reactRunning) {
                Start-ReactUI
            }
            Write-Log -Level INFO -Message "Services rebuilt and restarted! Refresh browser to load changes." -NoLabel -ForegroundColor Green
        }
        elseif ($Terminate) {
            # -Terminate killed the services, so start them fresh
            Write-Log -Level WARN -Message "Starting services..." -NoLabel
            Start-GameService
            Start-ReactUI
            Write-Log -Level INFO -Message "Services rebuilt and started! Refresh browser to load changes." -NoLabel -ForegroundColor Green
        }
        else {
            Write-Log -Level INFO -Message "Projects rebuilt successfully! Run './catan.ps1 run' to start." -NoLabel -ForegroundColor Green
        }
    }

    "database" {
        # All database commands route through .scripts/database.ps1 (CosmosDB).
        # Pass -Azure for Azure, omit for local emulator.
        $dbScript = Join-Path $PSScriptRoot ".scripts/database.ps1"
        $modeFlag = if ($Azure) { @("-Azure") } else { @() }

        switch ($SubCommand) {
            "clean" {
                & pwsh $dbScript nuke-containers @modeFlag -TraceLevel $TraceLevel
                exit $LASTEXITCODE
            }
            "install" {
                & pwsh $dbScript install @modeFlag -TraceLevel $TraceLevel
                exit $LASTEXITCODE
            }
            "doctor" {
                & pwsh $dbScript doctor @modeFlag -TraceLevel $TraceLevel
                exit $LASTEXITCODE
            }
            "seed-data" {
                & pwsh $dbScript seed-data @modeFlag -TraceLevel $TraceLevel
                exit $LASTEXITCODE
            }
            "test" {
                & pwsh $dbScript test @modeFlag -TraceLevel $TraceLevel
                exit $LASTEXITCODE
            }

            default {
                Write-Log -Level INFO -Message "" -NoLabel
                Write-Log -Level INFO -Message "Database Commands" -NoLabel -ForegroundColor Cyan
                Write-Log -Level INFO -Message "=================" -NoLabel -ForegroundColor Cyan
                Write-Log -Level INFO -Message "" -NoLabel
                Write-Log -Level WARN -Message "Usage: ./catan.ps1 database <subcommand>" -NoLabel
                Write-Log -Level INFO -Message "" -NoLabel
                Write-Log -Level WARN -Message "Subcommands:" -NoLabel
                Write-Log -Level INFO -Message "  doctor       - Check CosmosDB health (emulator or Azure)" -NoLabel
                Write-Log -Level INFO -Message "  install      - Install emulator, create containers, seed data" -NoLabel
                Write-Log -Level INFO -Message "  clean        - Delete containers (nuke-containers)" -NoLabel
                Write-Log -Level INFO -Message "  seed-data    - Seed players and recordings" -NoLabel
                Write-Log -Level INFO -Message "  test         - Run contract tests" -NoLabel
                Write-Log -Level INFO -Message "" -NoLabel
                Write-Log -Level WARN -Message "Flags:" -NoLabel
                Write-Log -Level INFO -Message "  -Azure       - Target Azure CosmosDB instead of local emulator" -NoLabel
                Write-Log -Level INFO -Message "" -NoLabel
                Write-Log -Level WARN -Message "Examples:" -NoLabel
                Write-Log -Level INFO -Message "  ./catan.ps1 database doctor                        - Check local emulator" -NoLabel
                Write-Log -Level INFO -Message "  ./catan.ps1 database doctor -Azure                 - Check Azure CosmosDB" -NoLabel
                Write-Log -Level INFO -Message "  ./catan.ps1 database install                       - Install local emulator + seed" -NoLabel
                Write-Log -Level INFO -Message "  ./catan.ps1 database install -Azure                - Install Azure account + seed" -NoLabel
                Write-Log -Level INFO -Message "  ./catan.ps1 database test                          - Run tests against emulator" -NoLabel
                Write-Log -Level INFO -Message "  ./catan.ps1 database test -Azure                   - Run tests against Azure" -NoLabel
                Write-Log -Level INFO -Message "" -NoLabel
                Write-Log -Level INFO -Message "Note: Recording management moved to './catan.ps1 recording'" -NoLabel -ForegroundColor DarkYellow
                Write-Log -Level INFO -Message "" -NoLabel

                if ($SubCommand) {
                    Write-Log -Level ERROR -Message "Unknown subcommand: $SubCommand" -NoLabel
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
                Write-Log -Level INFO -Message "" -NoLabel
                Write-Log -Level INFO -Message "Dependencies Commands" -NoLabel -ForegroundColor Cyan
                Write-Log -Level INFO -Message "=====================" -NoLabel -ForegroundColor Cyan
                Write-Log -Level INFO -Message "" -NoLabel
                Write-Log -Level WARN -Message "Usage: ./catan.ps1 dependencies <subcommand>" -NoLabel
                Write-Log -Level INFO -Message "" -NoLabel
                Write-Log -Level WARN -Message "Subcommands:" -NoLabel
                Write-Log -Level INFO -Message "  doctor   - Check status of all dependencies" -NoLabel
                Write-Log -Level INFO -Message "  install  - Install all dependencies" -NoLabel
                Write-Log -Level INFO -Message "  clean    - Remove/reset dependencies" -NoLabel
                Write-Log -Level INFO -Message "" -NoLabel
                Write-Log -Level WARN -Message "Options:" -NoLabel
                Write-Log -Level INFO -Message "  -Json        Output doctor as JSON" -NoLabel
                Write-Log -Level INFO -Message "  -HashTable   Output doctor as PowerShell hashtable" -NoLabel
                Write-Log -Level INFO -Message "  -Yes         Skip confirmation prompts" -NoLabel
                Write-Log -Level INFO -Message "  -Force       Force reinstall even if already installed" -NoLabel
                Write-Log -Level INFO -Message "  -TraceLevel  Output detail: ERROR, WARN, INFO (default), DEBUG" -NoLabel
                Write-Log -Level INFO -Message "" -NoLabel
                Write-Log -Level WARN -Message "Examples:" -NoLabel
                Write-Log -Level INFO -Message "  ./catan.ps1 dependencies doctor    - Check all dependencies" -NoLabel
                Write-Log -Level INFO -Message "  ./catan.ps1 dependencies install   - Install missing dependencies" -NoLabel
                Write-Log -Level INFO -Message "" -NoLabel

                if ($SubCommand) {
                    Write-Log -Level ERROR -Message "Unknown subcommand: $SubCommand" -NoLabel
                    exit 1
                }
            }
        }
    }

    "streamdeck" {
        $sdDir = Join-Path $PSScriptRoot "streamdeck"
        $sdPlugin = Join-Path $sdDir "com.catan.streamdeck.sdPlugin"
        $sdPackedFile = Join-Path $sdDir "com.catan.streamdeck.streamDeckPlugin"
        $sdDownloadDir = Join-Path $PSScriptRoot "Catan3.GameService" "wwwroot" "downloads"
        $sdManifest = Get-Content (Join-Path $sdPlugin "manifest.json") | ConvertFrom-Json
        $sdVersion = $sdManifest.Version
        $sdDownloadFile = Join-Path $sdDownloadDir "CatanStreamDeck-v${sdVersion}.streamDeckPlugin"

        switch ($SubCommand) {
            "build" {
                Write-Log -Level INFO -Message "Building Stream Deck plugin..." -NoLabel -ForegroundColor Cyan
                Push-Location $sdDir
                try {
                    npm run build
                    if ($LASTEXITCODE -ne 0) { throw "Build failed" }
                    Write-Log -Level INFO -Message "Stream Deck plugin built successfully" -NoLabel -ForegroundColor Green

                    # Build profile zip archives from JSON definitions
                    Write-Log -Level INFO -Message "Building profile archives..." -NoLabel -ForegroundColor Cyan
                    node scripts/build-profiles.mjs
                    if ($LASTEXITCODE -ne 0) { throw "Profile build failed" }

                    # Convert SVG icons to PNG (required by Elgato CLI)
                    Write-Log -Level INFO -Message "Converting SVG icons to PNG..." -NoLabel -ForegroundColor Cyan
                    Get-ChildItem -Path $sdPlugin -Filter "*.svg" -Recurse | ForEach-Object {
                        $png = $_.FullName -replace '\.svg$', '.png'
                        $png2x = $_.FullName -replace '\.svg$', '@2x.png'
                        if (-not (Test-Path $png)) {
                            sips -s format png $_.FullName --out $png --resampleWidth 72 --resampleHeight 72 2>$null | Out-Null
                        }
                        if (-not (Test-Path $png2x)) {
                            sips -s format png $_.FullName --out $png2x --resampleWidth 144 --resampleHeight 144 2>$null | Out-Null
                        }
                    }

                    # Pack and copy to wwwroot for download
                    Write-Log -Level INFO -Message "Packing plugin for download..." -NoLabel -ForegroundColor Cyan
                    npx streamdeck pack $sdPlugin --force --output $sdDir 2>$null
                    if (Test-Path $sdPackedFile) {
                        if (-not (Test-Path $sdDownloadDir)) {
                            New-Item -ItemType Directory -Path $sdDownloadDir -Force | Out-Null
                        }
                        Copy-Item $sdPackedFile $sdDownloadFile -Force
                        # Clean up old versioned downloads and write metadata
                        Get-ChildItem -Path $sdDownloadDir -Filter "CatanStreamDeck-v*.streamDeckPlugin" | Where-Object { $_.FullName -ne $sdDownloadFile } | Remove-Item -Force
                        @{ version = $sdVersion; filename = "CatanStreamDeck-v${sdVersion}.streamDeckPlugin" } | ConvertTo-Json | Set-Content (Join-Path $sdDownloadDir "streamdeck-latest.json")
                        Write-Log -Level INFO -Message "Plugin available at /downloads/CatanStreamDeck-v${sdVersion}.streamDeckPlugin" -NoLabel -ForegroundColor Green
                    } else {
                        Write-Log -Level WARN -Message "Warning: Pack succeeded but output file not found" -NoLabel
                    }
                } finally {
                    Pop-Location
                }
            }
            "watch" {
                Write-Log -Level INFO -Message "Watching Stream Deck plugin (Ctrl+C to stop)..." -NoLabel -ForegroundColor Cyan
                Push-Location $sdDir
                try {
                    npm run watch
                } finally {
                    Pop-Location
                }
            }
            "pack" {
                Write-Log -Level INFO -Message "Packing Stream Deck plugin..." -NoLabel -ForegroundColor Cyan
                Push-Location $sdDir
                try {
                    npx streamdeck pack $sdPlugin --force --output $sdDir
                    if ($LASTEXITCODE -ne 0) { throw "Pack failed" }
                    if (Test-Path $sdPackedFile) {
                        if (-not (Test-Path $sdDownloadDir)) {
                            New-Item -ItemType Directory -Path $sdDownloadDir -Force | Out-Null
                        }
                        Copy-Item $sdPackedFile $sdDownloadFile -Force
                        Write-Log -Level INFO -Message "Plugin packed and copied to wwwroot/downloads/" -NoLabel -ForegroundColor Green
                    }
                } finally {
                    Pop-Location
                }
            }
            "link" {
                Write-Log -Level INFO -Message "Linking Stream Deck plugin for development..." -NoLabel -ForegroundColor Cyan
                Push-Location $sdDir
                try {
                    npx streamdeck link $sdPlugin
                    Write-Log -Level INFO -Message "Plugin linked. Restart Stream Deck to load it." -NoLabel -ForegroundColor Green
                } finally {
                    Pop-Location
                }
            }
            "install" {
                Write-Log -Level INFO -Message "Installing Stream Deck plugin dependencies..." -NoLabel -ForegroundColor Cyan
                Push-Location $sdDir
                try {
                    npm install
                    Write-Log -Level INFO -Message "Dependencies installed" -NoLabel -ForegroundColor Green
                } finally {
                    Pop-Location
                }
            }
            default {
                Write-Log -Level WARN -Message "Stream Deck plugin commands:" -NoLabel
                Write-Log -Level INFO -Message "  ./catan.ps1 streamdeck install  - Install npm dependencies" -NoLabel
                Write-Log -Level INFO -Message "  ./catan.ps1 streamdeck build    - Build the plugin" -NoLabel
                Write-Log -Level INFO -Message "  ./catan.ps1 streamdeck watch    - Build and watch for changes" -NoLabel
                Write-Log -Level INFO -Message "  ./catan.ps1 streamdeck pack     - Package as .streamDeckPlugin" -NoLabel
                Write-Log -Level INFO -Message "  ./catan.ps1 streamdeck link     - Symlink for local development" -NoLabel
            }
        }
    }

    "azure" {
        $azureScript = Join-Path $PSScriptRoot ".scripts/catan-azure.ps1"

        switch ($SubCommand) {
            "install" {
                $dbScript = Join-Path $PSScriptRoot ".scripts/database.ps1"
                Write-Log -Level INFO -Message "Installing all Azure resources..." -NoLabel -ForegroundColor Cyan
                Write-Log -Level INFO -Message "" -NoLabel
                & $azureScript game-service install -TraceLevel $TraceLevel
                if ($LASTEXITCODE -ne 0) { exit 1 }
                & pwsh $dbScript install -Azure -TraceLevel $TraceLevel
                if ($LASTEXITCODE -ne 0) { exit 1 }
                & $azureScript ui install -TraceLevel $TraceLevel
                if ($LASTEXITCODE -ne 0) { exit 1 }
                # Deploy RBAC and app settings for CosmosDB
                & pwsh $dbScript deploy -Azure -TraceLevel $TraceLevel
                if ($LASTEXITCODE -ne 0) { exit 1 }
                Write-Log -Level INFO -Message "" -NoLabel
                Write-Log -Level INFO -Message "All Azure resources installed and configured!" -NoLabel -ForegroundColor Green
            }
            "deploy" {
                # Support targeted deploys: ./catan.ps1 azure deploy [ui|game-service|database]
                # Without a target, deploys everything.
                $deployTarget = if ($Target) { $Target.ToLower() } else { "all" }

                switch ($deployTarget) {
                    "ui" {
                        Write-Log -Level INFO -Message "Deploying UI to Azure..." -NoLabel -ForegroundColor Cyan
                        Write-Log -Level INFO -Message "" -NoLabel

                        $uiDoctor = & $azureScript ui doctor -HashTable -TraceLevel $TraceLevel
                        if ($uiDoctor.needsInstall) {
                            Write-Log -Level WARN -Message "  UI: Not installed - installing..." -NoLabel
                            & $azureScript ui install -TraceLevel $TraceLevel
                            if ($LASTEXITCODE -ne 0) { exit 1 }
                        }

                        if ($uiDoctor.needsDeploy -or $Force) {
                            Write-Log -Level WARN -Message "  UI: Deploying..." -NoLabel
                            & $azureScript ui deploy -Force:$Force -NoBuild:$NoBuild -TraceLevel $TraceLevel
                            if ($LASTEXITCODE -ne 0) { exit 1 }
                        } else {
                            Write-Log -Level INFO -Message "  UI: Up to date - skipping" -NoLabel -ForegroundColor Green
                        }

                        # React staging is deployed by the deploy-react-staging.yml workflow
                        # Use './catan.ps1 azure ui deploy-staging' to deploy manually

                        Write-Log -Level INFO -Message "" -NoLabel
                        Write-Log -Level INFO -Message "UI deployment complete!" -NoLabel -ForegroundColor Green
                    }
                    "game-service" {
                        $slotLabel = if ($Slot) { " to slot '$Slot'" } else { "" }
                        Write-Log -Level INFO -Message "Deploying GameService${slotLabel} to Azure..." -NoLabel -ForegroundColor Cyan
                        Write-Log -Level INFO -Message "" -NoLabel
                        $gsArgs = @{}
                        if ($Slot) { $gsArgs['Slot'] = $Slot }
                        & $azureScript game-service deploy -Force:$Force -NoBuild:$NoBuild -TraceLevel $TraceLevel @gsArgs
                        if ($LASTEXITCODE -ne 0) { exit 1 }
                        Write-Log -Level INFO -Message "" -NoLabel
                        Write-Log -Level INFO -Message "GameService deployment complete!" -NoLabel -ForegroundColor Green
                    }
                    "database" {
                        $dbScript = Join-Path $PSScriptRoot ".scripts/database.ps1"
                        Write-Log -Level INFO -Message "Deploying database configuration..." -NoLabel -ForegroundColor Cyan
                        Write-Log -Level INFO -Message "" -NoLabel
                        & pwsh $dbScript deploy -Azure -TraceLevel $TraceLevel
                        if ($LASTEXITCODE -ne 0) { exit 1 }
                        Write-Log -Level INFO -Message "" -NoLabel
                        Write-Log -Level INFO -Message "Database deployment complete!" -NoLabel -ForegroundColor Green
                    }
                    "all" {
                        Write-Log -Level INFO -Message "Deploying to Azure..." -NoLabel -ForegroundColor Cyan
                        Write-Log -Level INFO -Message "" -NoLabel

                        # Run all doctors to determine what needs to be done
                        Write-Log -Level INFO -Message "Checking deployment status..." -NoLabel

                        # GameService doctor
                        $gsDoctor = & $azureScript game-service doctor -HashTable -TraceLevel $TraceLevel
                        $gsNeedsInstall = $gsDoctor.needsInstall
                        $gsNeedsDeploy = $gsDoctor.needsDeploy
                        $gsHealthy = $gsDoctor.healthy

                        if ($gsNeedsInstall) {
                            Write-Log -Level WARN -Message "  GameService: Not installed - installing..." -NoLabel
                            & $azureScript game-service install -TraceLevel $TraceLevel
                            if ($LASTEXITCODE -ne 0) { exit 1 }
                            $gsNeedsDeploy = $true
                        }

                        if ($gsHealthy -and -not $gsNeedsDeploy -and -not $Force) {
                            Write-Log -Level INFO -Message "  GameService: Up to date - skipping" -NoLabel -ForegroundColor Green
                        }
                        elseif ($gsNeedsDeploy -or $Force) {
                            Write-Log -Level WARN -Message "  GameService: Needs deploy" -NoLabel
                            & $azureScript game-service deploy -Force:$Force -NoBuild:$NoBuild -TraceLevel $TraceLevel
                            if ($LASTEXITCODE -ne 0) { exit 1 }
                        }
                        else {
                            Write-Log -Level INFO -Message "  GameService: OK - skipping" -NoLabel -ForegroundColor Green
                        }

                        # Database: use database.ps1 for CosmosDB
                        $dbScript = Join-Path $PSScriptRoot ".scripts/database.ps1"
                        $dbDoctor = & pwsh $dbScript doctor -Azure -HashTable -TraceLevel $TraceLevel
                        if ($dbDoctor.Status -ne "Ready") {
                            Write-Log -Level WARN -Message "  Database: Needs setup" -NoLabel
                            & pwsh $dbScript install -Azure -TraceLevel $TraceLevel
                            if ($LASTEXITCODE -ne 0) { exit 1 }
                            & pwsh $dbScript deploy -Azure -TraceLevel $TraceLevel
                            if ($LASTEXITCODE -ne 0) { exit 1 }
                        } else {
                            Write-Log -Level INFO -Message "  Database: Ready - skipping" -NoLabel -ForegroundColor Green
                        }

                        # UI doctor
                        $uiDoctor = & $azureScript ui doctor -HashTable -TraceLevel $TraceLevel
                        $uiNeedsInstall = $uiDoctor.needsInstall
                        $uiNeedsDeploy = $uiDoctor.needsDeploy
                        $uiHealthy = $uiDoctor.healthy

                        if ($uiNeedsInstall) {
                            Write-Log -Level WARN -Message "  UI: Not installed - installing..." -NoLabel
                            & $azureScript ui install -TraceLevel $TraceLevel
                            if ($LASTEXITCODE -ne 0) { exit 1 }
                            $uiNeedsDeploy = $true
                        }

                        if ($uiHealthy -and -not $uiNeedsDeploy -and -not $Force) {
                            Write-Log -Level INFO -Message "  UI: Up to date - skipping" -NoLabel -ForegroundColor Green
                        }
                        elseif ($uiNeedsDeploy -or $Force) {
                            Write-Log -Level WARN -Message "  UI: Needs deploy" -NoLabel
                            & $azureScript ui deploy -Force:$Force -NoBuild:$NoBuild -TraceLevel $TraceLevel
                            if ($LASTEXITCODE -ne 0) { exit 1 }
                        }
                        else {
                            Write-Log -Level INFO -Message "  UI: OK - skipping" -NoLabel -ForegroundColor Green
                        }

                        # React UI staging is deployed by the deploy-react-staging.yml workflow
                        # (triggered on push to main when react-ui/** changes). Don't duplicate
                        # that here — it requires Node.js which the deploy-azure.yml workflow
                        # doesn't set up, and would hang the deploy pipeline.

                        Write-Log -Level INFO -Message "" -NoLabel
                        Write-Log -Level INFO -Message "All deployments complete!" -NoLabel -ForegroundColor Green
                    }
                    default {
                        Write-Log -Level ERROR -Message "Unknown deploy target: $deployTarget" -NoLabel
                        Write-Log -Level INFO -Message "" -NoLabel
                        Write-Log -Level WARN -Message "Usage: ./catan.ps1 azure deploy [target]" -NoLabel
                        Write-Log -Level INFO -Message "" -NoLabel
                        Write-Log -Level WARN -Message "Targets:" -NoLabel
                        Write-Log -Level INFO -Message "  ui            - Deploy UI" -NoLabel
                        Write-Log -Level INFO -Message "  game-service  - Deploy GameService only" -NoLabel
                        Write-Log -Level INFO -Message "  database      - Deploy database configuration only" -NoLabel
                        Write-Log -Level INFO -Message "  (no target)   - Deploy everything" -NoLabel
                        exit 1
                    }
                }
            }
            "doctor" {
                # Delegate to TypeScript Azure Doctor (single source of truth).
                # Config comes from .azure/catan-azure.json — no env vars needed.
                # Uses DefaultAzureCredential (picks up az login session).
                $tsArgs = @()
                if ($Staging)  { $tsArgs += "--staging" }
                if ($Json)     { $tsArgs += "--json" }
                if (-not $Yes) { $tsArgs += "--no-fix" }

                $tsScript = Join-Path $PSScriptRoot "react-ui/lib/azure/azureDoctor.cli.ts"
                & npx tsx $tsScript @tsArgs
                exit $LASTEXITCODE
            }
            "swap-slots" {
                Write-Log -Level INFO -Message "Swap Azure Deployment Slots" -NoLabel -ForegroundColor Cyan
                Write-Log -Level INFO -Message "==========================" -NoLabel -ForegroundColor Cyan
                Write-Log -Level INFO -Message "" -NoLabel

                # Load config for app name and resource group
                $azureConfigFile = Join-Path $PSScriptRoot ".azure/catan-azure.json"
                if (-not (Test-Path $azureConfigFile)) {
                    Write-Log -Level ERROR -Message "Azure configuration not found. Run './catan.ps1 azure install' first." -NoLabel
                    exit 1
                }
                $azureConfig = Get-Content $azureConfigFile -Raw | ConvertFrom-Json
                $appName = $azureConfig.ui.appName
                $rgName = $azureConfig.resourceGroup

                # Use doctor to get complete picture of the system
                Write-Log -Level INFO -Message "Running UI health check..." -NoLabel
                $uiDoctor = & $azureScript ui doctor -HashTable -TraceLevel $TraceLevel

                if ($uiDoctor.needsInstall) {
                    Write-Log -Level ERROR -Message "UI is not installed. Run './catan.ps1 azure install' first." -NoLabel
                    exit 1
                }

                # Show current state from doctor
                Write-Log -Level WARN -Message "This will swap the staging slot into production." -NoLabel
                Write-Log -Level INFO -Message "" -NoLabel
                Write-Log -Level INFO -Message "  App:         $appName" -NoLabel
                Write-Log -Level INFO -Message "  Production:  https://$appName.azurewebsites.net" -NoLabel
                Write-Log -Level INFO -Message "  Staging:     https://$appName-staging.azurewebsites.net" -NoLabel
                Write-Log -Level INFO -Message "" -NoLabel

                # Show what's currently in each slot
                if ($uiDoctor.prodRuntime -or $uiDoctor.stagingRuntime) {
                    function Get-SwapRuntimeLabel {
                        param([string]$Runtime)
                        if ([string]::IsNullOrWhiteSpace($Runtime)) { return "unknown" }
                        if ($Runtime -like "DOTNETCORE*") { return "Blazor ($Runtime)" }
                        if ($Runtime -like "NODE*") { return "React/Next.js ($Runtime)" }
                        return $Runtime
                    }
                    Write-Log -Level WARN -Message "Current configuration:" -NoLabel
                    Write-Log -Level INFO -Message "  Production: $(Get-SwapRuntimeLabel $uiDoctor.prodRuntime)" -NoLabel
                    Write-Log -Level INFO -Message "  Staging:    $(Get-SwapRuntimeLabel $uiDoctor.stagingRuntime)" -NoLabel
                    Write-Log -Level INFO -Message "" -NoLabel
                    Write-Log -Level WARN -Message "After swap:" -NoLabel
                    Write-Log -Level INFO -Message "  Production: $(Get-SwapRuntimeLabel $uiDoctor.stagingRuntime)" -NoLabel
                    Write-Log -Level INFO -Message "  Staging:    $(Get-SwapRuntimeLabel $uiDoctor.prodRuntime)" -NoLabel
                    Write-Log -Level INFO -Message "" -NoLabel
                }

                # Check staging slot exists
                if (-not $uiDoctor.checks.stagingSlot) {
                    Write-Log -Level ERROR -Message "Staging slot does not exist." -NoLabel
                    Write-Log -Level INFO -Message "  Run: ./catan.ps1 azure install" -NoLabel -ForegroundColor Cyan
                    exit 1
                }

                # Check staging has code deployed
                if (-not $uiDoctor.checks.stagingCodeDeployed) {
                    Write-Log -Level ERROR -Message "Staging slot has no code deployed." -NoLabel
                    Write-Log -Level INFO -Message "  Run: ./catan.ps1 azure deploy ui" -NoLabel -ForegroundColor Cyan
                    exit 1
                }

                # Show staging commit info
                if ($uiDoctor.stagingDeployedCommit) {
                    Write-Log -Level INFO -Message "  Staging commit: $($uiDoctor.stagingDeployedCommit)" -NoLabel
                }

                # Check staging runtime is correct (NODE for React)
                if (-not $uiDoctor.checks.stagingRuntime) {
                    Write-Log -Level ERROR -Message "Staging slot runtime is not configured for Node.js." -NoLabel
                    Write-Log -Level INFO -Message "  Current: $($uiDoctor.stagingRuntime)" -NoLabel
                    Write-Log -Level INFO -Message "  Run: ./catan.ps1 azure ui deploy-staging" -NoLabel -ForegroundColor Cyan
                    exit 1
                }

                # Check staging is responding (with retry for cold starts)
                if (-not $uiDoctor.checks.stagingResponding) {
                    Write-Log -Level WARN -Message "Staging slot is not responding. Warming up..." -NoLabel
                    $stagingUrl = "https://$appName-staging.azurewebsites.net"
                    $healthy = $false
                    for ($attempt = 1; $attempt -le 3; $attempt++) {
                        try {
                            $response = Invoke-WebRequest -Uri $stagingUrl -TimeoutSec 30 -UseBasicParsing -ErrorAction Stop
                            Write-Log -Level INFO -Message "  Staging slot is responding (HTTP $($response.StatusCode))" -NoLabel -ForegroundColor Green
                            $healthy = $true
                            break
                        }
                        catch {
                            if ($attempt -lt 3) {
                                Write-Log -Level WARN -Message "  Attempt $attempt/3: staging not ready, retrying..." -NoLabel
                                Start-Sleep -Seconds 5
                            }
                        }
                    }
                    if (-not $healthy) {
                        Write-Log -Level ERROR -Message "Staging slot is not responding after 3 attempts." -NoLabel
                        Write-Log -Level INFO -Message "" -NoLabel
                        Write-Log -Level WARN -Message "Try:" -NoLabel
                        Write-Log -Level INFO -Message "  1. Wait a minute and retry (cold start can be slow)" -NoLabel
                        Write-Log -Level INFO -Message "  2. Redeploy: ./catan.ps1 azure deploy ui -Force" -NoLabel
                        exit 1
                    }
                }

                # Confirm
                if (-not $Yes) {
                    $confirm = Read-Host "Proceed with swap? (y/N)"
                    if ($confirm -ne 'y' -and $confirm -ne 'Y') {
                        Write-Log -Level WARN -Message "Swap cancelled." -NoLabel
                        exit 0
                    }
                }

                Write-Log -Level INFO -Message "Swapping staging -> production..." -NoLabel -ForegroundColor Cyan
                az webapp deployment slot swap --name $appName --resource-group $rgName --slot staging
                if ($LASTEXITCODE -ne 0) {
                    Write-Log -Level ERROR -Message "Slot swap failed!" -NoLabel
                    exit 1
                }

                Write-Log -Level INFO -Message "" -NoLabel
                Write-Log -Level INFO -Message "Slot swap complete!" -NoLabel -ForegroundColor Green
                Write-Log -Level INFO -Message "  Production is now serving: $(Get-SwapRuntimeLabel $uiDoctor.stagingRuntime)" -NoLabel
                Write-Log -Level INFO -Message "  To swap back: ./catan.ps1 azure swap-slots" -NoLabel
            }
            "start" {
                Write-Log -Level INFO -Message "Starting Azure services..." -NoLabel -ForegroundColor Cyan
                Write-Log -Level INFO -Message "" -NoLabel

                $azureConfigFile = Join-Path $PSScriptRoot ".azure/catan-azure.json"
                if (-not (Test-Path $azureConfigFile)) {
                    Write-Log -Level ERROR -Message "Azure configuration not found. Run './catan.ps1 azure install' first." -NoLabel
                    exit 1
                }
                $azureConfig = Get-Content $azureConfigFile -Raw | ConvertFrom-Json
                $gsUrl = $azureConfig.gameService.url
                $uiUrl = $azureConfig.ui.url
                $sqlServer = $azureConfig.sqlServer.serverName
                $sqlDb = $azureConfig.sqlServer.databaseName
                $rgName = $azureConfig.resourceGroup

                # Step 1: Resume database if paused (must complete before GameService can connect)
                Write-Log -Level INFO -Message "  Database...   " -NoLabel -NoNewline
                $dbStatus = az sql db show --name $sqlDb --server $sqlServer --resource-group $rgName --query status -o tsv 2>$null
                if ($dbStatus -eq "Paused") {
                    Write-Log -Level WARN -Message "resuming... " -NoLabel -NoNewline
                    az sql db update --name $sqlDb --server $sqlServer --resource-group $rgName --auto-pause-delay 720 2>$null | Out-Null
                    # Wait for resume (polls every 5s, up to 2 minutes)
                    for ($i = 0; $i -lt 24; $i++) {
                        Start-Sleep -Seconds 5
                        $dbStatus = az sql db show --name $sqlDb --server $sqlServer --resource-group $rgName --query status -o tsv 2>$null
                        if ($dbStatus -eq "Online") { break }
                    }
                    if ($dbStatus -eq "Online") {
                        Write-Log -Level INFO -Message "online" -NoLabel -ForegroundColor Green
                    } else {
                        Write-Log -Level WARN -Message "status: $dbStatus (may still be resuming)" -NoLabel
                    }
                } elseif ($dbStatus -eq "Online") {
                    Write-Log -Level INFO -Message "already online" -NoLabel -ForegroundColor Green
                } else {
                    Write-Log -Level WARN -Message "status: $dbStatus" -NoLabel
                }

                # Step 2: Wake GameService and UI in parallel
                $gsJob = Start-Job -ScriptBlock {
                    try {
                        $r = Invoke-RestMethod -Uri "$using:gsUrl/health" -TimeoutSec 60 -ErrorAction Stop
                        return @{ ok = $true; status = $r.status }
                    } catch {
                        return @{ ok = $false; error = $_.Exception.Message }
                    }
                }
                $uiJob = Start-Job -ScriptBlock {
                    try {
                        $r = Invoke-WebRequest -Uri $using:uiUrl -TimeoutSec 60 -UseBasicParsing -ErrorAction Stop
                        return @{ ok = $true; status = $r.StatusCode }
                    } catch {
                        return @{ ok = $false; error = $_.Exception.Message }
                    }
                }

                # Wait for both with progress
                Write-Log -Level INFO -Message "  GameService..." -NoLabel -NoNewline
                $gsJob | Wait-Job | Out-Null
                $gsResult = Receive-Job $gsJob
                if ($gsResult.ok) {
                    Write-Log -Level INFO -Message "  $($gsResult.status)" -NoLabel -ForegroundColor Green
                } else {
                    Write-Log -Level ERROR -Message "  failed to wake" -NoLabel
                }
                $gsJob | Remove-Job -Force

                Write-Log -Level INFO -Message "  React UI...   " -NoLabel -NoNewline
                $uiJob | Wait-Job | Out-Null
                $uiResult = Receive-Job $uiJob
                if ($uiResult.ok) {
                    Write-Log -Level INFO -Message "  responding" -NoLabel -ForegroundColor Green
                } else {
                    Write-Log -Level ERROR -Message "  failed to wake" -NoLabel
                }
                $uiJob | Remove-Job -Force

                # Summary
                Write-Log -Level INFO -Message "" -NoLabel
                $allOk = $gsResult.ok -and $uiResult.ok -and ($dbStatus -eq "Online")
                if ($allOk) {
                    Write-Log -Level INFO -Message "All services running!" -NoLabel -ForegroundColor Green
                } else {
                    Write-Log -Level WARN -Message "Some services may still be starting. Run './catan.ps1 azure doctor' to check." -NoLabel
                }
                Write-Log -Level INFO -Message "" -NoLabel
                Write-Log -Level INFO -Message "  WebUI:       $uiUrl" -NoLabel
                Write-Log -Level INFO -Message "  GameService: $gsUrl" -NoLabel
            }
            "clean" {
                Write-Log -Level WARN -Message "Cleaning all Azure resources..." -NoLabel
                Write-Log -Level INFO -Message "" -NoLabel

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
                Write-Log -Level INFO -Message "" -NoLabel
                Write-Log -Level INFO -Message "All Azure resources cleaned!" -NoLabel -ForegroundColor Green
            }
            # Noun-first routing: ./catan.ps1 azure <noun> <verb>
            # Passes through directly to catan-azure.ps1
            { $_ -in @("ui", "game-service", "database", "github") } {
                if (-not $Target) {
                    Write-Log -Level WARN -Message "Usage: ./catan.ps1 azure $SubCommand <verb>" -NoLabel
                    Write-Log -Level INFO -Message "" -NoLabel
                    Write-Log -Level INFO -Message "Verbs: install, deploy, deploy-staging, doctor, clean, fix" -NoLabel
                    exit 1
                }
                $extraArgs = @{}
                if ($Slot) { $extraArgs['Slot'] = $Slot }
                if ($AzureGameServiceUrl) { $extraArgs['GameServiceUrl'] = $AzureGameServiceUrl }
                & $azureScript $SubCommand $Target -Force:$Force -NoBuild:$NoBuild -TraceLevel $TraceLevel -Json:$Json -HashTable:$HashTable @extraArgs
                if ($LASTEXITCODE -ne 0) { exit 1 }
            }
            default {
                Write-Log -Level INFO -Message "" -NoLabel
                Write-Log -Level INFO -Message "Azure Commands" -NoLabel -ForegroundColor Cyan
                Write-Log -Level INFO -Message "==============" -NoLabel -ForegroundColor Cyan
                Write-Log -Level INFO -Message "" -NoLabel
                Write-Log -Level WARN -Message "Usage: ./catan.ps1 azure <verb> [target]" -NoLabel
                Write-Log -Level WARN -Message "       ./catan.ps1 azure <target> <verb>" -NoLabel
                Write-Log -Level INFO -Message "" -NoLabel
                Write-Log -Level WARN -Message "Verbs (operate on all resources):" -NoLabel
                Write-Log -Level INFO -Message "  start       - Wake all services (resume DB, warm up apps)" -NoLabel
                Write-Log -Level INFO -Message "  install     - Create all Azure resources (idempotent)" -NoLabel
                Write-Log -Level INFO -Message "  deploy      - Deploy everything to Azure" -NoLabel
                Write-Log -Level INFO -Message "  doctor      - Check health of all Azure resources" -NoLabel
                Write-Log -Level INFO -Message "  clean       - Delete all Azure resources" -NoLabel
                Write-Log -Level INFO -Message "  swap-slots  - Swap staging and production slots" -NoLabel
                Write-Log -Level INFO -Message "" -NoLabel
                Write-Log -Level WARN -Message "Targeted (verb + target or target + verb):" -NoLabel
                Write-Log -Level INFO -Message "  deploy ui              - Deploy UI" -NoLabel
                Write-Log -Level INFO -Message "  deploy game-service    - Deploy GameService only" -NoLabel
                Write-Log -Level INFO -Message "  deploy database        - Deploy database config only" -NoLabel
                Write-Log -Level INFO -Message "  ui doctor              - Check UI health only" -NoLabel
                Write-Log -Level INFO -Message "  ui deploy-staging      - Deploy React to staging only" -NoLabel
                Write-Log -Level INFO -Message "  game-service deploy    - Deploy GameService only" -NoLabel
                Write-Log -Level INFO -Message "  github install         - Setup GitHub Actions OIDC" -NoLabel
                Write-Log -Level INFO -Message "" -NoLabel
                Write-Log -Level WARN -Message "Options:" -NoLabel
                Write-Log -Level INFO -Message "  -Force                  Force deploy even if up-to-date" -NoLabel
                Write-Log -Level INFO -Message "  -Slot <name>            Deploy to a specific slot (e.g., staging)" -NoLabel
                Write-Log -Level INFO -Message "  -AzureGameServiceUrl    GameService URL for React staging builds" -NoLabel
                Write-Log -Level INFO -Message "  -Json                   Output doctor as JSON" -NoLabel
                Write-Log -Level INFO -Message "  -HashTable              Output doctor as PowerShell hashtable" -NoLabel
                Write-Log -Level INFO -Message "  -TraceLevel             Output detail: ERROR, WARN, INFO (default), DEBUG" -NoLabel
                Write-Log -Level INFO -Message "" -NoLabel
                Write-Log -Level WARN -Message "Examples:" -NoLabel
                Write-Log -Level INFO -Message "  ./catan.ps1 azure deploy ui -Force    - Force deploy UI" -NoLabel
                Write-Log -Level INFO -Message "  ./catan.ps1 azure ui doctor            - Check UI health" -NoLabel
                Write-Log -Level INFO -Message "  ./catan.ps1 azure doctor               - Check all resources" -NoLabel
                Write-Log -Level INFO -Message "  ./catan.ps1 azure swap-slots           - Swap staging <-> production" -NoLabel
                Write-Log -Level INFO -Message "  ./catan.ps1 azure game-service deploy -Slot staging  - Deploy to staging slot" -NoLabel
                Write-Log -Level INFO -Message "  ./catan.ps1 azure ui deploy-staging -AzureGameServiceUrl https://catan-api-staging.azurewebsites.net" -NoLabel
                Write-Log -Level INFO -Message "" -NoLabel

                if ($SubCommand) {
                    Write-Log -Level ERROR -Message "Unknown subcommand: $SubCommand" -NoLabel
                    exit 1
                }
            }
        }
    }

    "help" {
        Write-Log -Level INFO -Message "" -NoLabel
        Write-Log -Level INFO -Message "Catan3 Development Script" -NoLabel -ForegroundColor Cyan
        Write-Log -Level INFO -Message "=========================" -NoLabel -ForegroundColor Cyan
        Write-Log -Level INFO -Message "" -NoLabel
        Write-Log -Level WARN -Message "Quick Start:" -NoLabel
        Write-Log -Level INFO -Message "  ./catan.ps1 doctor           - Check if everything is set up correctly" -NoLabel
        Write-Log -Level INFO -Message "  ./catan.ps1 install          - Install dependencies and database" -NoLabel
        Write-Log -Level INFO -Message "  ./catan.ps1 run              - Build, start services, launch browser" -NoLabel
        Write-Log -Level INFO -Message "" -NoLabel
        Write-Log -Level WARN -Message "Development:" -NoLabel
        Write-Log -Level INFO -Message "  ./catan.ps1 run              - Start GameService + React UI" -NoLabel
        Write-Log -Level INFO -Message "  ./catan.ps1 run -Network     - Same, but accessible from other devices" -NoLabel
        Write-Log -Level INFO -Message "  ./catan.ps1 stop             - Stop running services" -NoLabel
        Write-Log -Level INFO -Message "  ./catan.ps1 restart          - Stop and restart services" -NoLabel
        Write-Log -Level INFO -Message "  ./catan.ps1 update           - Rebuild and restart (when hot reload fails)" -NoLabel
        Write-Log -Level INFO -Message "  ./catan.ps1 update -Terminate - Same, but close all Terminal windows first (macOS)" -NoLabel
        Write-Log -Level INFO -Message "  ./catan.ps1 build            - Build GameService + React UI (no tests)" -NoLabel
        Write-Log -Level INFO -Message "  ./catan.ps1 test             - Build and run all tests" -NoLabel
        Write-Log -Level INFO -Message "  ./catan.ps1 lint             - Format, lint, and spell check (PS, TS, MD)" -NoLabel
        Write-Log -Level INFO -Message "  ./catan.ps1 generate-types   - Generate TypeScript types from C# models (TypeGen 7.0.0)" -NoLabel
        Write-Log -Level INFO -Message "  ./catan.ps1 clean            - Stop services, clean build (preserves database)" -NoLabel
        Write-Log -Level INFO -Message "  ./catan.ps1 debug            - Instructions for VS Code debugging" -NoLabel
        Write-Log -Level INFO -Message "  ./catan.ps1 help (or -Help)  - Show this help" -NoLabel
        Write-Log -Level INFO -Message "" -NoLabel
        Write-Log -Level WARN -Message "Setup:" -NoLabel
        Write-Log -Level INFO -Message "  ./catan.ps1 doctor           - Check dependencies and database health" -NoLabel
        Write-Log -Level INFO -Message "  ./catan.ps1 install          - Install all dependencies and database" -NoLabel
        Write-Log -Level INFO -Message "" -NoLabel
        Write-Log -Level WARN -Message "Recording:" -NoLabel
        Write-Log -Level INFO -Message "  ./catan.ps1 recording list   - List all recordings (add -Azure for Azure)" -NoLabel
        Write-Log -Level INFO -Message "  ./catan.ps1 recording save   - Save recordings to JSON files" -NoLabel
        Write-Log -Level INFO -Message "  ./catan.ps1 recording load   - Load recordings from JSON files" -NoLabel
        Write-Log -Level INFO -Message "  ./catan.ps1 recording replay - Run replay tests (requires running server)" -NoLabel
        Write-Log -Level INFO -Message "  ./catan.ps1 recording delete - Delete a recording" -NoLabel
        Write-Log -Level INFO -Message "  ./catan.ps1 recording        - Show detailed recording help" -NoLabel
        Write-Log -Level INFO -Message "" -NoLabel
        Write-Log -Level WARN -Message "Stats:" -NoLabel
        Write-Log -Level INFO -Message "  ./catan.ps1 stats list       - Show stats summary (add -Azure for Azure)" -NoLabel
        Write-Log -Level INFO -Message "  ./catan.ps1 stats export     - Export stats to JSON file" -NoLabel
        Write-Log -Level INFO -Message "  ./catan.ps1 stats import     - Import stats from JSON file" -NoLabel
        Write-Log -Level INFO -Message "  ./catan.ps1 stats reset      - Reset all lifetime statistics" -NoLabel
        Write-Log -Level INFO -Message "  ./catan.ps1 stats            - Show detailed stats help" -NoLabel
        Write-Log -Level INFO -Message "" -NoLabel
        Write-Log -Level WARN -Message "Database:" -NoLabel
        Write-Log -Level INFO -Message "  ./catan.ps1 database doctor  - Diagnose database health and contents" -NoLabel
        Write-Log -Level INFO -Message "  ./catan.ps1 database clean   - Delete database" -NoLabel
        Write-Log -Level INFO -Message "  ./catan.ps1 database install - Fresh install with default data" -NoLabel
        Write-Log -Level INFO -Message "" -NoLabel
        Write-Log -Level WARN -Message "Dependencies:" -NoLabel
        Write-Log -Level INFO -Message "  ./catan.ps1 dependencies doctor  - Check all dependency status" -NoLabel
        Write-Log -Level INFO -Message "  ./catan.ps1 dependencies install - Install missing dependencies" -NoLabel
        Write-Log -Level INFO -Message "  ./catan.ps1 dependencies clean   - Remove/reset dependencies" -NoLabel
        Write-Log -Level INFO -Message "" -NoLabel
        Write-Log -Level WARN -Message "Stream Deck:" -NoLabel
        Write-Log -Level INFO -Message "  ./catan.ps1 streamdeck install  - Install npm dependencies" -NoLabel
        Write-Log -Level INFO -Message "  ./catan.ps1 streamdeck build    - Build the plugin" -NoLabel
        Write-Log -Level INFO -Message "  ./catan.ps1 streamdeck watch    - Build and watch for changes" -NoLabel
        Write-Log -Level INFO -Message "  ./catan.ps1 streamdeck pack     - Package as .streamDeckPlugin" -NoLabel
        Write-Log -Level INFO -Message "  ./catan.ps1 streamdeck link     - Symlink for local development" -NoLabel
        Write-Log -Level INFO -Message "" -NoLabel
        Write-Log -Level WARN -Message "Azure:" -NoLabel
        Write-Log -Level INFO -Message "  ./catan.ps1 azure start      - Wake all services (resume DB, warm apps)" -NoLabel
        Write-Log -Level INFO -Message "  ./catan.ps1 azure doctor     - Check Azure deployment health" -NoLabel
        Write-Log -Level INFO -Message "  ./catan.ps1 azure install    - Create all Azure resources" -NoLabel
        Write-Log -Level INFO -Message "  ./catan.ps1 azure deploy     - Deploy everything to Azure" -NoLabel
        Write-Log -Level INFO -Message "  ./catan.ps1 azure swap-slots - Swap staging <-> production" -NoLabel
        Write-Log -Level INFO -Message "  ./catan.ps1 azure clean      - Delete all Azure resources" -NoLabel
        Write-Log -Level INFO -Message "" -NoLabel
        Write-Log -Level WARN -Message "Typical workflow:" -NoLabel
        Write-Log -Level INFO -Message "  1. ./catan.ps1 run           - Start services (hot reload enabled)" -NoLabel
        Write-Log -Level INFO -Message "  2. Make code changes         - Browser auto-refreshes on save" -NoLabel
        Write-Log -Level INFO -Message "  3. ./catan.ps1 update        - If hot reload fails, rebuild and restart" -NoLabel
        Write-Log -Level INFO -Message "" -NoLabel
        Write-Log -Level WARN -Message "URLs:" -NoLabel
        Write-Log -Level INFO -Message "  GameService: $GameServiceUrl" -NoLabel
        Write-Log -Level INFO -Message "  React UI:    $ReactUIUrl" -NoLabel
        Write-Log -Level INFO -Message "" -NoLabel
    }

    default {
        Write-Log -Level INFO -Message "" -NoLabel
        Write-Log -Level ERROR -Message "Invalid command: $Verb" -NoLabel
        Write-Log -Level INFO -Message "" -NoLabel
        & $PSCommandPath help
        exit 1
    }
}
