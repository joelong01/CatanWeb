# PowerShell script to run tests with completely suppressed logging
param(
    [string]$Filter = "",
    [string]$Verbosity = "minimal",
    [switch]$Quiet
)

# Set environment variables to suppress ALL logging
$env:ASPNETCORE_ENVIRONMENT = "Testing"
$env:DOTNET_ENVIRONMENT = "Testing"

# Suppress all ASP.NET Core logging
$env:Logging__LogLevel__Default = "None"
$env:Logging__LogLevel__Microsoft = "None"
$env:Logging__LogLevel__Microsoft_AspNetCore = "None"
$env:Logging__LogLevel__Microsoft_Hosting_Lifetime = "None"
$env:Logging__LogLevel__Catan3_GameService = "None"

# Change to test directory
Set-Location "Tests.GameService"

# Build command based on parameters
$command = "dotnet test"

if ($Quiet) {
    $command += " --verbosity quiet --nologo --no-build --logger `"console;verbosity=quiet`""
} else {
    $command += " --verbosity $Verbosity --nologo"
}

if ($Filter) {
    $command += " --filter `"$Filter`""
}

Write-Host "Running tests with suppressed logging..." -ForegroundColor Green

if ($Quiet) {
    # Completely silent execution - redirect output
    $output = Invoke-Expression $command 2>&1
    
    # Only show test results and errors
    $output | Where-Object { 
        $_ -match "Passed|Failed|Skipped|Total tests|Test run" -or 
        $_ -match "?|?|Failed|Error|Exception" 
    } | ForEach-Object { Write-Host $_ }
} else {
    # Normal execution with minimal logging
    Invoke-Expression $command
}

# Examples:
# .\run-tests-clean.ps1 -Quiet
# .\run-tests-clean.ps1 -Filter "StateProgression" -Quiet
# .\run-tests-clean.ps1 -Filter "AllocationHelper" -Verbosity "quiet"