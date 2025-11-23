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
    [ValidateSet("build", "run", "debug", "clean", "stop", "update", "help")]
    [string]$Verb = "run"
)

$ErrorActionPreference = "Stop"

$GameServicePort = 8080
$WebUIPort = 5296
$GameServiceUrl = "http://localhost:$GameServicePort"
$WebUIUrl = "http://localhost:$WebUIPort"
$DatabasePath = Join-Path $PSScriptRoot "Catan3.GameService\Data\catan.db"
$PlayerImagesSource = Join-Path $PSScriptRoot "DesktopApp\Assets\DefaultPlayers"

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

function Start-GameService {
    Write-Host "Starting GameService on port $GameServicePort..." -ForegroundColor Cyan

    $gameServicePath = Join-Path $PSScriptRoot "Catan3.GameService"

    # Start GameService in a new window
    Start-Process pwsh -ArgumentList "-NoExit", "-Command", "cd '$gameServicePath'; dotnet run" -WindowStyle Normal

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

    # Start WebUI
    Start-Process pwsh -ArgumentList "-NoExit", "-Command", "cd '$webUIPath'; dotnet run" -WindowStyle Normal

    Write-Host "Waiting for WebUI to be ready..." -ForegroundColor Yellow
    Start-Sleep -Seconds 3  # Give it a moment to start

    Write-Host "WebUI is starting at $WebUIUrl (with hot reload)" -ForegroundColor Green
}

function Initialize-Database {
    Write-Host "Checking database..." -ForegroundColor Cyan

    $dataDir = Split-Path $DatabasePath -Parent

    # Create Data directory if it doesn't exist
    if (-not (Test-Path $dataDir)) {
        Write-Host "Creating Data directory..." -ForegroundColor Yellow
        New-Item -ItemType Directory -Path $dataDir -Force | Out-Null
    }

    # Check if database exists
    if (Test-Path $DatabasePath) {
        Write-Host "Database exists at $DatabasePath" -ForegroundColor Green
        return $true
    }

    Write-Host "Database not found. Initializing..." -ForegroundColor Yellow

    # Run the database seed tool
    $gameServicePath = Join-Path $PSScriptRoot "Catan3.GameService"

    Push-Location $gameServicePath
    try {
        # Use dotnet run with a seed argument to initialize the database
        $result = & dotnet run -- --seed-database 2>&1
        if ($LASTEXITCODE -ne 0) {
            Write-Host "Database initialization failed!" -ForegroundColor Red
            Write-Host $result -ForegroundColor Red
            return $false
        }
        Write-Host "Database initialized successfully!" -ForegroundColor Green
        return $true
    }
    finally {
        Pop-Location
    }
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

function Stop-Services {
    Write-Host "Stopping services..." -ForegroundColor Yellow

    # Kill processes on GameService port
    $gameServicePids = (Get-NetTCPConnection -LocalPort $GameServicePort -ErrorAction SilentlyContinue).OwningProcess | Select-Object -Unique
    if ($gameServicePids) {
        foreach ($processId in $gameServicePids) {
            Stop-Process -Id $processId -Force -ErrorAction SilentlyContinue
            Write-Host "  Killed process $processId (GameService port)" -ForegroundColor Gray
        }
    }

    # Kill processes on WebUI port
    $webUIPids = (Get-NetTCPConnection -LocalPort $WebUIPort -ErrorAction SilentlyContinue).OwningProcess | Select-Object -Unique
    if ($webUIPids) {
        foreach ($processId in $webUIPids) {
            Stop-Process -Id $processId -Force -ErrorAction SilentlyContinue
            Write-Host "  Killed process $processId (WebUI port)" -ForegroundColor Gray
        }
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

    "update" {
        Write-Host "Rebuilding WebUI..." -ForegroundColor Cyan
        $webUIPath = Join-Path $PSScriptRoot "WebUI"
        & dotnet build $webUIPath --verbosity quiet
        if ($LASTEXITCODE -ne 0) {
            Write-Host "WebUI build failed!" -ForegroundColor Red
            exit 1
        }

        # Restart WebUI server if running to serve fresh files
        if (Test-PortInUse -Port $WebUIPort) {
            Write-Host "Restarting WebUI server..." -ForegroundColor Yellow
            $webUIPids = (Get-NetTCPConnection -LocalPort $WebUIPort -ErrorAction SilentlyContinue).OwningProcess | Select-Object -Unique
            foreach ($processId in $webUIPids) {
                Stop-Process -Id $processId -Force -ErrorAction SilentlyContinue
            }
            Start-Sleep -Milliseconds 500
            Start-WebUI
            Write-Host "WebUI rebuilt and restarted! Refresh browser to load changes." -ForegroundColor Green
        }
        else {
            Write-Host "WebUI rebuilt successfully! Run './webui.ps1 run' to start." -ForegroundColor Green
        }
    }

    "help" {
        Write-Host ""
        Write-Host "WebUI Development Script" -ForegroundColor Cyan
        Write-Host "========================" -ForegroundColor Cyan
        Write-Host ""
        Write-Host "Commands:" -ForegroundColor Yellow
        Write-Host "  ./webui.ps1 run      - Start GameService + WebUI, launch browser"
        Write-Host "  ./webui.ps1 stop     - Stop running services"
        Write-Host "  ./webui.ps1 update   - Rebuild WebUI and restart (for code changes)"
        Write-Host "  ./webui.ps1 build    - Build all projects (full solution)"
        Write-Host "  ./webui.ps1 clean    - Stop services, delete database, clean build"
        Write-Host "  ./webui.ps1 debug    - Instructions for VS Code debugging"
        Write-Host "  ./webui.ps1 help     - Show this help"
        Write-Host ""
        Write-Host "Typical workflow:" -ForegroundColor Yellow
        Write-Host "  1. ./webui.ps1 run           - Start services"
        Write-Host "  2. Make code changes"
        Write-Host "  3. ./webui.ps1 update        - Rebuild and restart WebUI"
        Write-Host "  4. Refresh browser"
        Write-Host ""
        Write-Host "URLs:" -ForegroundColor Yellow
        Write-Host "  GameService: $GameServiceUrl"
        Write-Host "  WebUI:       $WebUIUrl"
        Write-Host ""
    }
}
