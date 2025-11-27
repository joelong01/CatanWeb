<#
.SYNOPSIS
    Build, run, and debug the WebUI project with GameService.

.DESCRIPTION
    Provides convenient commands for working with the Blazor WebUI project.

.PARAMETER Verb
    The action to perform: build, run, or debug

.EXAMPLE
    ./webui.ps1 build    # Build all projects
    ./webui.ps1 run      # Initialize database, run GameService and WebUI, launch browser
    ./webui.ps1 debug    # Instructions for debugging
    ./webui.ps1 clean    # Delete database and clean build artifacts
#>

param(
    [Parameter(Position = 0)]
    [ValidateSet("build", "run", "debug", "clean", "stop", "restart", "update", "database", "help")]
    [string]$Verb = "run",

    [Parameter(Position = 1)]
    [string]$SubCommand
)

$ErrorActionPreference = "Stop"

$GameServicePort = 8080
$WebUIPort = 5296
$GameServiceUrl = "http://localhost:$GameServicePort"
$WebUIUrl = "http://localhost:$WebUIPort"
$DatabasePath = Join-Path $PSScriptRoot "Catan3.GameService\Data\catan.db"
$PidFile = Join-Path $PSScriptRoot ".webui-pids.json"

function Test-PortInUse {
    param([int]$Port)
    $connection = Get-NetTCPConnection -LocalPort $Port -ErrorAction SilentlyContinue
    return $null -ne $connection
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
        $pids = Get-Content $PidFile | ConvertFrom-Json -AsHashtable
    }

    if ($GameServicePid -gt 0) { $pids.GameService = $GameServicePid }
    if ($WebUIPid -gt 0) { $pids.WebUI = $WebUIPid }

    $pids | ConvertTo-Json | Set-Content $PidFile
}

function Get-SavedPids {
    if (Test-Path $PidFile) {
        return Get-Content $PidFile | ConvertFrom-Json -AsHashtable
    }
    return @{}
}

function Start-GameService {
    Write-Host "Starting GameService on port $GameServicePort..." -ForegroundColor Cyan

    $gameServicePath = Join-Path $PSScriptRoot "Catan3.GameService"

    # Start GameService in a new window and track the process
    $process = Start-Process pwsh -ArgumentList "-NoExit", "-Command", "cd '$gameServicePath'; dotnet run" -WindowStyle Normal -PassThru
    Save-Pids -GameServicePid $process.Id

    Write-Host "Waiting for GameService to be ready..." -ForegroundColor Yellow
    if (Wait-ForService -Url "$GameServiceUrl/health" -TimeoutSeconds 30) {
        Write-Host "GameService is running at $GameServiceUrl" -ForegroundColor Green
        return $true
    }
    else {
        Write-Host "Warning: GameService may not be fully ready" -ForegroundColor Yellow
        return $true  # Continue anyway
    }
}

function Start-WebUI {
    Write-Host "Starting WebUI on port $WebUIPort..." -ForegroundColor Cyan

    $webUIPath = Join-Path $PSScriptRoot "WebUI"

    # Start WebUI with hot reload using dotnet watch and track the process
    $process = Start-Process pwsh -ArgumentList "-NoExit", "-Command", "cd '$webUIPath'; dotnet watch run" -WindowStyle Normal -PassThru
    Save-Pids -WebUIPid $process.Id

    Write-Host "Waiting for WebUI to be ready..." -ForegroundColor Yellow
    if (Wait-ForService -Url $WebUIUrl -TimeoutSeconds 30) {
        Write-Host "WebUI is running at $WebUIUrl (with hot reload)" -ForegroundColor Green
    }
    else {
        Write-Host "Warning: WebUI may not be fully ready" -ForegroundColor Yellow
    }
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

function Test-Database {
    Write-Host "Validating database schema..." -ForegroundColor Cyan

    if (-not (Test-Path $DatabasePath)) {
        Write-Host "Database not found at $DatabasePath" -ForegroundColor Red
        Write-Host "Run './webui.ps1 database install' to create the database" -ForegroundColor Yellow
        return $false
    }

    # Run GameService tests that validate the database schema
    Write-Host "Running database schema validation tests..." -ForegroundColor Yellow

    $testProject = Join-Path $PSScriptRoot "Tests\GameService\Tests.GameService.csproj"

    if (-not (Test-Path $testProject)) {
        Write-Host "Test project not found: $testProject" -ForegroundColor Red
        return $false
    }

    # Run tests with a filter for database-related tests
    $result = & dotnet test $testProject --filter "Category=Database" --verbosity quiet --nologo 2>&1

    if ($LASTEXITCODE -eq 0) {
        Write-Host "Database schema validation passed!" -ForegroundColor Green
        return $true
    }
    else {
        Write-Host "Database schema validation failed!" -ForegroundColor Red
        Write-Host "The database may need to be rebuilt:" -ForegroundColor Yellow
        Write-Host "  1. ./webui.ps1 database clean" -ForegroundColor White
        Write-Host "  2. ./webui.ps1 database install" -ForegroundColor White
        Write-Host ""
        Write-Host "Test output:" -ForegroundColor Yellow
        Write-Host $result -ForegroundColor Gray
        return $false
    }
}

function Stop-ChildProcesses {
    param([int]$ParentPid)

    $children = Get-CimInstance Win32_Process -Filter "ParentProcessId=$ParentPid" -ErrorAction SilentlyContinue
    foreach ($child in $children) {
        Stop-ChildProcesses -ParentPid $child.ProcessId  # Recursive
        Stop-Process -Id $child.ProcessId -Force -ErrorAction SilentlyContinue
    }
}

function Stop-Services {
    Write-Host "Stopping services..." -ForegroundColor Yellow

    # Kill ALL PowerShell processes running GameService or WebUI (and their children)
    $allProcesses = Get-CimInstance Win32_Process -Filter "Name='pwsh.exe' OR Name='powershell.exe' OR Name='dotnet.exe'" -ErrorAction SilentlyContinue

    $killedCount = 0
    foreach ($proc in $allProcesses) {
        $cmdLine = $proc.CommandLine
        if ($cmdLine) {
            # Check if this process is running GameService or WebUI
            if ($cmdLine -match "Catan3\.GameService" -or
                $cmdLine -match "WebUI.*dotnet.*watch.*run" -or
                $cmdLine -match "dotnet.*run.*GameService" -or
                $cmdLine -match "dotnet.*watch.*run.*WebUI") {

                try {
                    # Kill all child processes first
                    Stop-ChildProcesses -ParentPid $proc.ProcessId

                    # Then kill the parent
                    Stop-Process -Id $proc.ProcessId -Force -ErrorAction SilentlyContinue
                    Write-Host "  Killed process $($proc.ProcessId): $(($cmdLine -split ' ')[0..5] -join ' ')..." -ForegroundColor Gray
                    $killedCount++
                }
                catch {
                    Write-Host "  Failed to kill process $($proc.ProcessId)" -ForegroundColor Yellow
                }
            }
        }
    }

    if ($killedCount -gt 0) {
        Write-Host "  Killed $killedCount remnant process(es)" -ForegroundColor Gray
    }

    # Fallback: kill any remaining processes on ports
    $gameServicePids = (Get-NetTCPConnection -LocalPort $GameServicePort -ErrorAction SilentlyContinue).OwningProcess | Select-Object -Unique
    if ($gameServicePids) {
        foreach ($processId in $gameServicePids) {
            Stop-Process -Id $processId -Force -ErrorAction SilentlyContinue
            Write-Host "  Killed process $processId (GameService port)" -ForegroundColor Gray
        }
    }

    $webUIPids = (Get-NetTCPConnection -LocalPort $WebUIPort -ErrorAction SilentlyContinue).OwningProcess | Select-Object -Unique
    if ($webUIPids) {
        foreach ($processId in $webUIPids) {
            Stop-Process -Id $processId -Force -ErrorAction SilentlyContinue
            Write-Host "  Killed process $processId (WebUI port)" -ForegroundColor Gray
        }
    }

    # Clean up PID file
    if (Test-Path $PidFile) {
        Remove-Item $PidFile -Force
    }

    # Wait for ports to be released
    Start-Sleep -Milliseconds 500

    # Verify ports are free
    $gameStillRunning = Test-PortInUse -Port $GameServicePort
    $webUIStillRunning = Test-PortInUse -Port $WebUIPort

    if ($gameStillRunning -or $webUIStillRunning) {
        Write-Host "  Waiting for ports to be released..." -ForegroundColor Yellow
        Start-Sleep -Seconds 2
    }

    Write-Host "Services stopped." -ForegroundColor Green
}

switch ($Verb) {
    "build" {
        Write-Host "Building solution..." -ForegroundColor Cyan
        & "$PSScriptRoot\build.ps1" -NoTest
        if ($LASTEXITCODE -ne 0) {
            Write-Host "Build failed!" -ForegroundColor Red
            exit 1
        }
        Write-Host "Build completed successfully!" -ForegroundColor Green
    }

    "run" {
        # Build solution first
        Write-Host "Building solution..." -ForegroundColor Cyan
        & "$PSScriptRoot\build.ps1" -NoTest
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

        # Check if GameService is already running
        if (Test-PortInUse -Port $GameServicePort) {
            Write-Host "GameService already running on port $GameServicePort" -ForegroundColor Green
        }
        else {
            Start-GameService
        }

        # Check if WebUI is already running
        if (Test-PortInUse -Port $WebUIPort) {
            Write-Host "WebUI already running on port $WebUIPort" -ForegroundColor Green
        }
        else {
            Start-WebUI
        }

        # Wait a moment then launch browser
        Start-Sleep -Seconds 2

        $browserUrl = "$WebUIUrl/newgame"
        Write-Host "Launching browser to $browserUrl..." -ForegroundColor Cyan
        Start-Process $browserUrl

        Write-Host ""
        Write-Host "Services running:" -ForegroundColor Green
        Write-Host "  GameService: $GameServiceUrl" -ForegroundColor White
        Write-Host "  WebUI:       $WebUIUrl" -ForegroundColor White
        Write-Host ""
        Write-Host "Press Ctrl+C in service windows to stop them." -ForegroundColor Yellow
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
        Write-Host "Cleaning project..." -ForegroundColor Cyan

        # Stop any running services first
        Stop-Services

        # Clean database
        Clear-Database

        # Clean build artifacts
        Write-Host "Cleaning build artifacts..." -ForegroundColor Yellow
        & dotnet clean Catan.sln --verbosity quiet
        if ($LASTEXITCODE -eq 0) {
            Write-Host "Build artifacts cleaned." -ForegroundColor Green
        }
        else {
            Write-Host "Warning: dotnet clean returned non-zero exit code" -ForegroundColor Yellow
        }

        # Remove bin/obj directories for thorough clean
        Write-Host "Removing bin/obj directories..." -ForegroundColor Yellow
        Get-ChildItem -Path $PSScriptRoot -Include bin, obj -Recurse -Directory |
            ForEach-Object {
                Remove-Item $_.FullName -Recurse -Force -ErrorAction SilentlyContinue
            }

        Write-Host ""
        Write-Host "Clean completed!" -ForegroundColor Green
        Write-Host "Run './webui.ps1 build' to rebuild, or './webui.ps1 run' to rebuild and run." -ForegroundColor White
    }

    "stop" {
        Stop-Services
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
        Write-Host "Rebuilding WebUI and GameService..." -ForegroundColor Cyan

        # Build GameService first
        $gameServicePath = Join-Path $PSScriptRoot "Catan3.GameService"
        & dotnet build $gameServicePath --verbosity quiet
        if ($LASTEXITCODE -ne 0) {
            Write-Host "GameService build failed!" -ForegroundColor Red
            exit 1
        }
        Write-Host "GameService rebuilt." -ForegroundColor Green

        # Build WebUI
        $webUIPath = Join-Path $PSScriptRoot "WebUI"
        & dotnet build $webUIPath --verbosity quiet
        if ($LASTEXITCODE -ne 0) {
            Write-Host "WebUI build failed!" -ForegroundColor Red
            exit 1
        }
        Write-Host "WebUI rebuilt." -ForegroundColor Green

        # Restart both services if running
        $gameRunning = Test-PortInUse -Port $GameServicePort
        $webRunning = Test-PortInUse -Port $WebUIPort

        if ($gameRunning -or $webRunning) {
            Write-Host "Restarting services..." -ForegroundColor Yellow
            Stop-Services
            Start-Sleep -Milliseconds 500

            if ($gameRunning) {
                Start-GameService
            }
            if ($webRunning) {
                Start-WebUI
            }
            Write-Host "Services rebuilt and restarted! Refresh browser to load changes." -ForegroundColor Green
        }
        else {
            Write-Host "Projects rebuilt successfully! Run './webui.ps1 run' to start." -ForegroundColor Green
        }
    }

    "database" {
        switch ($SubCommand) {
            "check" {
                $valid = Test-Database
                if (-not $valid) {
                    exit 1
                }
            }
            "clean" {
                Clear-Database
            }
            "install" {
                # Clean first, then install fresh
                Clear-Database
                $installed = Install-Database
                if (-not $installed) {
                    exit 1
                }
            }
            default {
                Write-Host ""
                Write-Host "Database Commands" -ForegroundColor Cyan
                Write-Host "=================" -ForegroundColor Cyan
                Write-Host ""
                Write-Host "Usage: ./webui.ps1 database <subcommand>" -ForegroundColor Yellow
                Write-Host ""
                Write-Host "Subcommands:" -ForegroundColor Yellow
                Write-Host "  check    - Validate database schema matches app requirements"
                Write-Host "  clean    - Delete the database (wipes all data)"
                Write-Host "  install  - Clean and reinstall database with default data"
                Write-Host ""
                Write-Host "Examples:" -ForegroundColor Yellow
                Write-Host "  ./webui.ps1 database check     - Check if database is valid"
                Write-Host "  ./webui.ps1 database clean     - Delete database"
                Write-Host "  ./webui.ps1 database install   - Fresh install with default players"
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
        Write-Host "WebUI Development Script" -ForegroundColor Cyan
        Write-Host "========================" -ForegroundColor Cyan
        Write-Host ""
        Write-Host "Commands:" -ForegroundColor Yellow
        Write-Host "  ./webui.ps1 run              - Start GameService + WebUI, launch browser"
        Write-Host "  ./webui.ps1 stop             - Stop running services"
        Write-Host "  ./webui.ps1 restart          - Stop and restart services"
        Write-Host "  ./webui.ps1 update           - Rebuild projects and restart services"
        Write-Host "  ./webui.ps1 build            - Build all projects (full solution)"
        Write-Host "  ./webui.ps1 clean            - Stop services, delete database, clean build"
        Write-Host "  ./webui.ps1 database <cmd>   - Database management (check/clean/install)"
        Write-Host "  ./webui.ps1 debug            - Instructions for VS Code debugging"
        Write-Host "  ./webui.ps1 help             - Show this help"
        Write-Host ""
        Write-Host "Database Commands:" -ForegroundColor Yellow
        Write-Host "  ./webui.ps1 database check   - Validate database schema"
        Write-Host "  ./webui.ps1 database clean   - Delete database"
        Write-Host "  ./webui.ps1 database install - Fresh install with default data"
        Write-Host ""
        Write-Host "Typical workflow:" -ForegroundColor Yellow
        Write-Host "  1. ./webui.ps1 run           - Start services (hot reload enabled)"
        Write-Host "  2. Make code changes         - Browser auto-refreshes on save"
        Write-Host ""
        Write-Host "  If hot reload fails (e.g., rude edits):"
        Write-Host "  3. ./webui.ps1 update        - Rebuild and restart services"
        Write-Host ""
        Write-Host "  If database schema changed:"
        Write-Host "  4. ./webui.ps1 database install - Rebuild database"
        Write-Host ""
        Write-Host "URLs:" -ForegroundColor Yellow
        Write-Host "  GameService: $GameServiceUrl"
        Write-Host "  WebUI:       $WebUIUrl"
        Write-Host ""
    }
}
