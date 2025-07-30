# PowerShell script to run tests with suppressed logging
param(
    [string]$Filter = "",
    [string]$Verbosity = "minimal"
)

# Set environment variables to suppress logging
$env:ASPNETCORE_ENVIRONMENT = "Testing"
$env:Logging__LogLevel__Default = "Warning"
$env:Logging__LogLevel__Microsoft = "Warning"
$env:Logging__LogLevel__Microsoft_AspNetCore = "Warning"

# Change to test directory
Set-Location "Tests.GameService"

# Build command
$command = "dotnet test --verbosity $Verbosity --logger `"console;verbosity=quiet`""

if ($Filter) {
    $command += " --filter `"$Filter`""
}

Write-Host "Running tests with suppressed logging..." -ForegroundColor Green
Write-Host "Command: $command" -ForegroundColor Gray

# Execute the command
Invoke-Expression $command

# Examples:
# .\run-tests-quiet.ps1
# .\run-tests-quiet.ps1 -Filter "PickingBoard"
# .\run-tests-quiet.ps1 -Filter "StateProgression" -Verbosity "quiet"