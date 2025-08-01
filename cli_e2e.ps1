#!/usr/bin/env pwsh
<#
.SYNOPSIS
    End-to-end CLI testing script for Catan3
    
.DESCRIPTION
    This script:
    1. Builds the entire solution
    2. Checks if GameService is running on port 8080
    3. Starts GameService if not running
    4. Runs the CLI with the provided arguments
    
.PARAMETER GameType
    The type of game to run (regular or expansion)
    
.PARAMETER Arguments
    Additional arguments to pass to the CLI
    
.EXAMPLE
    .\cli_e2e.ps1 regular --complete --log-level INFO
    
.EXAMPLE
    .\cli_e2e.ps1 expansion --run-to WaitingForRoll --no-exit
    
.EXAMPLE
    .\cli_e2e.ps1 regular --player-count 4 --uri http://localhost:8080
#>

param(
    [Parameter(Position=0)]
    [string]$GameType = "regular",
    
    [Parameter(Position=1, ValueFromRemainingArguments=$true)]
    [string[]]$Arguments = @()
)

# Color output functions
function Write-ColorOutput {
    param(
        [string]$Message,
        [string]$Color = "White"
    )
    
    $timestamp = Get-Date -Format "HH:mm:ss.fff"
    Write-Host "[$timestamp] " -NoNewline -ForegroundColor Gray
    Write-Host $Message -ForegroundColor $Color
}

function Write-Success { param([string]$Message) Write-ColorOutput "? $Message" "Green" }
function Write-Info { param([string]$Message) Write-ColorOutput "?? $Message" "Cyan" }
function Write-Warning { param([string]$Message) Write-ColorOutput "??  $Message" "Yellow" }
function Write-Error { param([string]$Message) Write-ColorOutput "? $Message" "Red" }
function Write-Header { param([string]$Message) Write-ColorOutput "?? $Message" "Magenta" }

# Configuration
$GameServicePort = 8080
$GameServiceUrl = "http://localhost:$GameServicePort"
$SolutionFile = "Catan3.sln"
$GameServiceProject = "Catan3.GameService\Catan3.GameService.csproj"
$CLIProject = "Catan3.CLI\Catan3.CLI.csproj"

# Global variables for cleanup
$GameServiceProcess = $null
$StartedGameService = $false

# Cleanup function
function Cleanup {
    if ($StartedGameService -and $GameServiceProcess -and !$GameServiceProcess.HasExited) {
        Write-Info "Stopping GameService that was started by this script..."
        try {
            $GameServiceProcess.Kill()
            $GameServiceProcess.WaitForExit(5000)
            Write-Success "GameService stopped successfully"
        }
        catch {
            Write-Warning "Failed to stop GameService gracefully: $_"
        }
    }
}

# Register cleanup on script exit
Register-EngineEvent PowerShell.Exiting -Action { Cleanup }
$null = Register-ObjectEvent -InputObject ([System.Console]) -EventName CancelKeyPress -Action {
    Write-Info "Ctrl+C detected, cleaning up..."
    Cleanup
    [System.Environment]::Exit(0)
}

try {
    Write-Header "CATAN3 END-TO-END CLI TESTING"
    Write-Info "Arguments: $GameType $($Arguments -join ' ')"
    Write-Info ""

    # Step 0: Build the project
    Write-Header "STEP 1: Building Required Projects"
    
    # Build only the projects needed for CLI (not the full solution)
    $projectsToBuild = @(
        "Catan3.Shared\Catan3.Shared.csproj",
        "Catan3.CLI\Catan3.CLI.csproj"
    )
    
    foreach ($project in $projectsToBuild) {
        if (!(Test-Path $project)) {
            Write-Error "Project file '$project' not found. Are you in the project root?"
            exit 1
        }
        
        Write-Info "Building project: $project"
        $buildResult = dotnet build $project --configuration Debug
        if ($LASTEXITCODE -ne 0) {
            Write-Error "Build failed for $project. Please fix compilation errors and try again."
            exit 1
        }
        Write-Success "Project $project built successfully"
    }
    Write-Info ""

    # Step 1: Check if GameService is running
    Write-Header "STEP 2: Checking GameService Status"
    
    function Test-GameServiceRunning {
        try {
            $response = Invoke-WebRequest -Uri "$GameServiceUrl/api/companion/games" -TimeoutSec 3 -UseBasicParsing
            return $response.StatusCode -eq 200
        }
        catch {
            return $false
        }
    }

    $isGameServiceRunning = Test-GameServiceRunning
    
    if ($isGameServiceRunning) {
        Write-Success "GameService is already running at $GameServiceUrl"
    }
    else {
        Write-Info "GameService is not running, starting it now..."
        
        # Step 2: Start GameService if not running
        Write-Header "STEP 3: Starting GameService"
        
        if (!(Test-Path $GameServiceProject)) {
            Write-Error "GameService project '$GameServiceProject' not found"
            exit 1
        }

        Write-Info "Starting GameService: dotnet run --project $GameServiceProject"
        
        # Start GameService in background
        $processStartInfo = New-Object System.Diagnostics.ProcessStartInfo
        $processStartInfo.FileName = "dotnet"
        $processStartInfo.Arguments = "run --project `"$GameServiceProject`""
        $processStartInfo.UseShellExecute = $false
        $processStartInfo.RedirectStandardOutput = $true
        $processStartInfo.RedirectStandardError = $true
        $processStartInfo.CreateNoWindow = $true
        
        $GameServiceProcess = [System.Diagnostics.Process]::Start($processStartInfo)
        $StartedGameService = $true
        
        Write-Info "GameService process started (PID: $($GameServiceProcess.Id))"
        Write-Info "Waiting for GameService to become ready..."
        
        # Wait for GameService to start (up to 30 seconds)
        $timeout = 30
        $elapsed = 0
        $interval = 2
        
        while ($elapsed -lt $timeout) {
            Start-Sleep -Seconds $interval
            $elapsed += $interval
            
            if ($GameServiceProcess.HasExited) {
                Write-Error "GameService process exited unexpectedly"
                Write-Error "Exit Code: $($GameServiceProcess.ExitCode)"
                # Try to read error output
                if ($GameServiceProcess.StandardError) {
                    $errorOutput = $GameServiceProcess.StandardError.ReadToEnd()
                    Write-Error "Error Output: $errorOutput"
                }
                exit 1
            }
            
            if (Test-GameServiceRunning) {
                Write-Success "GameService is now running and responding at $GameServiceUrl"
                break
            }
            
            Write-Info "Still waiting for GameService... ($elapsed/$timeout seconds)"
        }
        
        if ($elapsed -ge $timeout) {
            Write-Error "GameService failed to start within $timeout seconds"
            Cleanup
            exit 1
        }
    }
    Write-Info ""

    # Step 3: Run the CLI test
    Write-Header "STEP 4: Running CLI Test"
    
    if (!(Test-Path $CLIProject)) {
        Write-Error "CLI project '$CLIProject' not found"
        exit 1
    }

    # Validate GameType
    if ($GameType -notin @("regular", "expansion")) {
        Write-Error "Invalid GameType '$GameType'. Must be 'regular' or 'expansion'"
        exit 1
    }

    # Build the CLI command
    $cliArgs = @("run", "--project", "`"$CLIProject`"", "--", $GameType)
    if ($Arguments.Count -gt 0) {
        $cliArgs += $Arguments
    }
    
    # Add default URI if not specified
    if ($Arguments -notcontains "--uri") {
        $cliArgs += @("--uri", $GameServiceUrl)
    }

    $commandLine = "dotnet $($cliArgs -join ' ')"
    Write-Info "Executing: $commandLine"
    Write-Info ""
    
    # Execute the CLI
    $cliResult = & dotnet @cliArgs
    $cliExitCode = $LASTEXITCODE
    
    Write-Info ""
    if ($cliExitCode -eq 0) {
        Write-Success "CLI test completed successfully"
    }
    else {
        Write-Error "CLI test failed with exit code $cliExitCode"
    }
    
    # Final status
    Write-Info ""
    Write-Header "EXECUTION SUMMARY"
    Write-Info "GameService URL: $GameServiceUrl"
    Write-Info "GameService Started by Script: $(if ($StartedGameService) { 'Yes' } else { 'No' })"
    Write-Info "CLI Exit Code: $cliExitCode"
    
    if ($StartedGameService) {
        Write-Info ""
        Write-Warning "GameService was started by this script and is still running"
        Write-Info "You can:"
        Write-Info "  • Access companion at: $GameServiceUrl/companion"
        Write-Info "  • View active games at: $GameServiceUrl/api/companion/games"
        Write-Info "  • Stop manually with Ctrl+C or by closing this terminal"
        
        # Keep the script running if GameService was started, unless CLI had --no-exit
        if ($Arguments -contains "--no-exit") {
            Write-Info ""
            Write-Info "CLI used --no-exit flag, keeping script alive..."
            Write-Info "Press Ctrl+C to stop GameService and exit"
            
            # Wait indefinitely
            try {
                while (!$GameServiceProcess.HasExited) {
                    Start-Sleep -Seconds 1
                }
            }
            catch {
                # Ctrl+C pressed
            }
        }
        else {
            Write-Info ""
            Write-Info "Leaving GameService running for further testing..."
            Write-Info "Use Ctrl+C to stop and exit, or run 'taskkill /PID $($GameServiceProcess.Id)' to stop manually"
        }
    }
    
    exit $cliExitCode
}
catch {
    Write-Error "Unexpected error: $_"
    Write-Error $_.ScriptStackTrace
    exit 1
}
finally {
    # Only cleanup if we're not keeping GameService running
    if ($StartedGameService -and $Arguments -notcontains "--no-exit") {
        # Don't cleanup here - let user manage GameService manually
    }
}