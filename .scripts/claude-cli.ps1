<#
.SYNOPSIS
Claude CLI development environment management script.
.DESCRIPTION
Provides management for Claude CLI with Install, Clean, Doctor operations.
Claude CLI is installed via npm and requires Node.js.

This script follows the standardized template patterns for resource management.
.PARAMETER Install
Installs Claude CLI via npm
.PARAMETER Clean
Removes Claude CLI installation
.PARAMETER Doctor
Verifies Claude CLI installation status and displays version information
.PARAMETER HashTable
Returns Doctor results as a PowerShell hashtable
.PARAMETER Json
Returns Doctor results as JSON
.PARAMETER Yes
Automatically confirms operations without prompting
.PARAMETER Force
Forces operations without additional safety checks
.PARAMETER TraceLevel
Sets output detail level (ERROR, WARN, INFO, DEBUG)
.PARAMETER Help
Displays help information
.EXAMPLE
.\claude-cli.ps1 -Doctor -TraceLevel INFO
Verifies Claude CLI installation with standard logging
.EXAMPLE
.\claude-cli.ps1 -Doctor -Json
Returns Claude CLI status as JSON
#>

param(
    [Parameter()]
    [switch]$Install,

    [Parameter()]
    [switch]$Clean,

    [Parameter()]
    [switch]$Doctor,

    [Parameter()]
    [switch]$HashTable,

    [Parameter()]
    [switch]$Json,

    [Parameter()]
    [switch]$Yes,

    [Parameter()]
    [switch]$Force,

    [Parameter()]
    [ValidateSet("ERROR", "WARN", "INFO", "DEBUG")]
    [string]$TraceLevel = "INFO",

    [Parameter()]
    [switch]$Help
)

# Import utility module
$scriptPath = Split-Path -Parent $MyInvocation.MyCommand.Path
Import-Module "$scriptPath\utility-scripts.psm1" -Force

$PSDefaultParameterValues = @{ 'Write-Log:TraceLevel' = $TraceLevel }

# NPM package name for Claude CLI
$script:PackageName = "@anthropic-ai/claude-code"

<#
.SYNOPSIS
    Gets the installed Claude CLI version.
.OUTPUTS
    [string] Version string or $null if not installed
#>
function Get-InstalledClaudeVersion {
    param(
        [ValidateSet("ERROR", "WARN", "INFO", "DEBUG")]
        [string]$TraceLevel = "ERROR"
    )

    try {
        $output = & claude --version 2>&1
        if ($LASTEXITCODE -eq 0) {
            # Parse version from output
            if ($output -match '(\d+\.\d+\.\d+)') {
                $version = $matches[1]
                Write-Log -Level "DEBUG" -Message "claude --version output: $version" -TraceLevel $TraceLevel
                return $version
            }
            # Return raw output if no version pattern found
            return $output.Trim()
        }
    } catch {
        Write-Log -Level "DEBUG" -Message "claude not found: $_" -TraceLevel $TraceLevel
    }

    return $null
}

<#
.SYNOPSIS
    Checks if npm is available.
.OUTPUTS
    [bool] True if npm is available
#>
function Test-NpmAvailable {
    param(
        [ValidateSet("ERROR", "WARN", "INFO", "DEBUG")]
        [string]$TraceLevel = "ERROR"
    )

    try {
        $output = & npm --version 2>&1
        if ($LASTEXITCODE -eq 0) {
            Write-Log -Level "DEBUG" -Message "npm version: $output" -TraceLevel $TraceLevel
            return $true
        }
    } catch {
        Write-Log -Level "DEBUG" -Message "npm not found: $_" -TraceLevel $TraceLevel
    }

    return $false
}

<#
.SYNOPSIS
    Runs Doctor operation to verify Claude CLI installation.
.DESCRIPTION
    Returns a hashtable with installation status. No output except DEBUG level.
    Status values:
    - Installed: Claude CLI is installed and working
    - NotInstalled: Claude CLI is not installed
    - NpmMissing: npm is not available (required for installation)
    - Error: An error occurred during detection
.OUTPUTS
    [hashtable] Status information
#>
function Doctor-ClaudeCli {
    param(
        [ValidateSet("ERROR", "WARN", "INFO", "DEBUG")]
        [string]$TraceLevel = "ERROR"
    )

    $result = @{
        Name = "claude-cli"
        Status = "Error"
        Version = $null
        HasNpm = $false
        PackageName = $script:PackageName
        Message = ""
    }

    Write-Log -Level "DEBUG" -Message "Checking Claude CLI installation..." -TraceLevel $TraceLevel

    try {
        # Check for npm first
        $result.HasNpm = Test-NpmAvailable -TraceLevel $TraceLevel

        # Check for Claude CLI
        $version = Get-InstalledClaudeVersion -TraceLevel $TraceLevel

        if ($version) {
            $result.Version = $version
            $result.Status = "Installed"
            $result.Message = "Claude CLI $version installed"
        } elseif ($result.HasNpm) {
            $result.Status = "NotInstalled"
            $result.Message = "Claude CLI is not installed (npm available for installation)"
        } else {
            $result.Status = "NpmMissing"
            $result.Message = "Claude CLI is not installed and npm is not available"
        }
    } catch {
        $result.Status = "Error"
        $result.Message = "Error checking Claude CLI: $_"
        Write-Log -Level "DEBUG" -Message $result.Message -TraceLevel $TraceLevel
    }

    Write-Log -Level "DEBUG" -Message "Doctor-ClaudeCli result: Status=$($result.Status), Version=$($result.Version)" -TraceLevel $TraceLevel
    return $result
}

<#
.SYNOPSIS
    Installs Claude CLI via npm.
.OUTPUTS
    [bool] True if installation succeeded
#>
function Install-ClaudeCli {
    param(
        [switch]$Yes,
        [switch]$Force,
        [ValidateSet("ERROR", "WARN", "INFO", "DEBUG")]
        [string]$TraceLevel = "ERROR"
    )

    Write-Log -Level "INFO" -Message "Installing Claude CLI ($($script:PackageName))..." -TraceLevel $TraceLevel

    # Check npm availability
    if (-not (Test-NpmAvailable -TraceLevel $TraceLevel)) {
        Write-Log -Level "ERROR" -Message "npm is required to install Claude CLI. Install Node.js first." -TraceLevel $TraceLevel
        return $false
    }

    # Install globally via npm
    Write-Log -Level "INFO" -Message "Running: npm install -g $($script:PackageName)" -TraceLevel $TraceLevel

    & npm install -g $script:PackageName 2>&1 | ForEach-Object { Write-Log -Level "DEBUG" -Message $_ -TraceLevel $TraceLevel }

    if ($LASTEXITCODE -ne 0) {
        Write-Log -Level "ERROR" -Message "npm install failed with exit code $LASTEXITCODE" -TraceLevel $TraceLevel
        return $false
    }

    # Verify installation
    Update-EnvironmentVariables -TraceLevel $TraceLevel
    $result = Doctor-ClaudeCli -TraceLevel $TraceLevel
    return ($result.Status -eq "Installed")
}

<#
.SYNOPSIS
    Uninstalls Claude CLI via npm.
.OUTPUTS
    [bool] True if uninstallation succeeded
#>
function Uninstall-ClaudeCli {
    param(
        [switch]$Yes,
        [ValidateSet("ERROR", "WARN", "INFO", "DEBUG")]
        [string]$TraceLevel = "ERROR"
    )

    Write-Log -Level "INFO" -Message "Uninstalling Claude CLI..." -TraceLevel $TraceLevel

    if (-not (Test-NpmAvailable -TraceLevel $TraceLevel)) {
        Write-Log -Level "WARN" -Message "npm is not available, cannot uninstall via npm" -TraceLevel $TraceLevel
        return $false
    }

    & npm uninstall -g $script:PackageName 2>&1 | ForEach-Object { Write-Log -Level "DEBUG" -Message $_ -TraceLevel $TraceLevel }

    return ($LASTEXITCODE -eq 0)
}

<#
.SYNOPSIS
    Displays formatted Doctor output.
.PARAMETER Result
    Hashtable from Doctor-ClaudeCli
#>
function Show-DoctorOutput {
    param([hashtable]$Result)

    Write-Log -Level INFO -Message "" -NoLabel
    Write-Log -Level INFO -Message "Claude CLI Status" -NoLabel -ForegroundColor Cyan
    Write-Log -Level INFO -Message "=================" -NoLabel -ForegroundColor Cyan
    Write-Log -Level INFO -Message "" -NoLabel

    $statusColor = switch ($Result.Status) {
        "Installed" { "Green" }
        "NotInstalled" { "Yellow" }
        "NpmMissing" { "Red" }
        default { "Red" }
    }

    Write-Log -Level INFO -Message "Status:       " -NoLabel -NoNewline
    Write-Log -Level INFO -Message $Result.Status -ForegroundColor $statusColor -NoLabel

    if ($Result.Version) {
        Write-Log -Level INFO -Message "Version:      $($Result.Version)" -NoLabel
    }

    Write-Log -Level INFO -Message "npm Available: $(if ($Result.HasNpm) { 'Yes' } else { 'No' })" -NoLabel
    Write-Log -Level INFO -Message "Package:       $($Result.PackageName)" -NoLabel

    Write-Log -Level INFO -Message "" -NoLabel
    Write-Log -Level INFO -Message $Result.Message -ForegroundColor $statusColor -NoLabel
    Write-Log -Level INFO -Message "" -NoLabel
}

<#
.SYNOPSIS
    Displays help information.
#>
function Show-Help {
    $help = @"
Claude CLI Development Environment Management Script
=====================================================

Manages Claude CLI installation and verification.
Claude CLI is installed globally via npm.

Usage:
    claude-cli.ps1 [-Install] [-Clean] [-Doctor] [-HashTable] [-Json]
                   [-Yes] [-Force] [-TraceLevel <level>] [-Help]

Operations:
    -Install    Installs Claude CLI via npm
    -Clean      Removes Claude CLI
    -Doctor     Verifies Claude CLI installation status
    -Help       Shows this help message

Output Formats:
    -HashTable  Returns Doctor results as PowerShell hashtable
    -Json       Returns Doctor results as JSON string

Options:
    -Yes        Automatically answers yes to prompts
    -Force      Skips verification checks
    -TraceLevel Sets output detail level (ERROR, WARN, INFO, DEBUG)

Status Values:
    Installed     - Claude CLI is installed and working
    NotInstalled  - Claude CLI is not installed (npm available)
    NpmMissing    - npm is not available (required for installation)
    Error         - An error occurred during detection

Prerequisites:
    - Node.js and npm must be installed first
    - Use node.ps1 -Install to set up Node.js

Examples:
    # Check Claude CLI installation
    claude-cli.ps1 -Doctor

    # Get status as JSON
    claude-cli.ps1 -Doctor -Json

    # Install Claude CLI
    claude-cli.ps1 -Install -Yes

    # Remove Claude CLI
    claude-cli.ps1 -Clean -Yes
"@
    Write-Log -Level INFO -Message $help -NoLabel
}

# Main execution block
try {
    # Handle Help first
    if ($Help) {
        Show-Help
        exit 0
    }

    # If no operation specified, show help
    if (-not ($Install -or $Clean -or $Doctor)) {
        Show-Help
        exit 0
    }

    # Always run Doctor first to get current status
    $doctorResult = Doctor-ClaudeCli -TraceLevel $TraceLevel

    if ($Doctor) {
        if ($HashTable) {
            return $doctorResult
        } elseif ($Json) {
            return ($doctorResult | ConvertTo-Json -Depth 5)
        } else {
            Show-DoctorOutput -Result $doctorResult
            exit $(if ($doctorResult.Status -eq "Installed") { 0 } else { 1 })
        }
    }

    if ($Install) {
        Write-Log -Level "HEADER" -Message "INSTALLING CLAUDE CLI" -TraceLevel $TraceLevel

        # Check if already installed
        if ($doctorResult.Status -eq "Installed" -and -not $Force) {
            Write-Log -Level "INFO" -Message "Claude CLI $($doctorResult.Version) is already installed" -TraceLevel $TraceLevel
            exit 0
        }

        # Check npm requirement
        if (-not $doctorResult.HasNpm) {
            Write-Log -Level "ERROR" -Message "npm is required. Run: .\.scripts\node.ps1 -Install" -TraceLevel $TraceLevel
            exit 1
        }

        if (Install-ClaudeCli -Yes:$Yes -Force:$Force -TraceLevel $TraceLevel) {
            Write-Log -Level "INFO" -Message "Claude CLI installation complete" -TraceLevel $TraceLevel
            exit 0
        } else {
            Write-Log -Level "ERROR" -Message "Claude CLI installation failed" -TraceLevel $TraceLevel
            exit 1
        }
    }

    if ($Clean) {
        Write-Log -Level "HEADER" -Message "CLEANING CLAUDE CLI" -TraceLevel $TraceLevel

        if ($doctorResult.Status -ne "Installed") {
            Write-Log -Level "INFO" -Message "Claude CLI is not installed" -TraceLevel $TraceLevel
            exit 0
        }

        if (Uninstall-ClaudeCli -Yes:$Yes -TraceLevel $TraceLevel) {
            Write-Log -Level "INFO" -Message "Claude CLI uninstalled successfully" -TraceLevel $TraceLevel
            exit 0
        } else {
            Write-Log -Level "ERROR" -Message "Claude CLI uninstall failed" -TraceLevel $TraceLevel
            exit 1
        }
    }

    exit 0
} catch {
    Write-Log -Level "ERROR" -Message "An error occurred: $_" -TraceLevel $TraceLevel
    exit 1
}
