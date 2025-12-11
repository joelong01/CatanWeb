<#
.SYNOPSIS
.NET SDK development environment management script.
.DESCRIPTION
Provides management for .NET SDK with Install, Clean, Doctor, and Update operations.
Manages .NET 9 SDK installation and verification.

This script follows the standardized template patterns for resource management.
.PARAMETER Install
Installs .NET 9 SDK
.PARAMETER Clean
Removes .NET SDK installation
.PARAMETER Doctor
Verifies .NET SDK installation status and displays version information
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
.\dotnet.ps1 -Doctor -TraceLevel INFO
Verifies .NET SDK installation with standard logging
.EXAMPLE
.\dotnet.ps1 -Doctor -Json
Returns .NET SDK status as JSON
.EXAMPLE
.\dotnet.ps1 -Install -Yes
Installs .NET 9 SDK without prompting
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

# Required .NET version from global.json
$script:RequiredDotNetVersion = "9.0"

<#
.SYNOPSIS
    Gets the installed .NET SDK version.
.OUTPUTS
    [string] Version string or $null if not installed
#>
function Get-InstalledDotNetVersion {
    param(
        [ValidateSet("ERROR", "WARN", "INFO", "DEBUG")]
        [string]$TraceLevel = "ERROR"
    )

    try {
        $output = & dotnet --version 2>&1
        if ($LASTEXITCODE -eq 0) {
            Write-Log -Level "DEBUG" -Message "dotnet --version output: $output" -TraceLevel $TraceLevel
            return $output.Trim()
        }
    } catch {
        Write-Log -Level "DEBUG" -Message "dotnet not found: $_" -TraceLevel $TraceLevel
    }

    return $null
}

<#
.SYNOPSIS
    Gets the list of installed .NET SDKs.
.OUTPUTS
    [array] Array of SDK version strings
#>
function Get-InstalledDotNetSdks {
    param(
        [ValidateSet("ERROR", "WARN", "INFO", "DEBUG")]
        [string]$TraceLevel = "ERROR"
    )

    try {
        $output = & dotnet --list-sdks 2>&1
        if ($LASTEXITCODE -eq 0) {
            $sdks = $output | ForEach-Object {
                if ($_ -match '^(\d+\.\d+\.\d+)') {
                    $matches[1]
                }
            } | Where-Object { $_ }
            return $sdks
        }
    } catch {
        Write-Log -Level "DEBUG" -Message "Failed to list SDKs: $_" -TraceLevel $TraceLevel
    }

    return @()
}

<#
.SYNOPSIS
    Runs Doctor operation to verify .NET SDK installation.
.DESCRIPTION
    Returns a hashtable with installation status. No output except DEBUG level.
    Status values:
    - Installed: Required .NET version is installed and working
    - WrongVersion: .NET is installed but not the required version
    - NotInstalled: .NET SDK is not installed
    - Error: An error occurred during detection
.OUTPUTS
    [hashtable] Status information with keys:
    - Name: "dotnet"
    - Status: Installation status (Installed, WrongVersion, NotInstalled, Error)
    - Version: Installed SDK version or $null
    - InstalledSdks: Array of all installed SDK versions
    - RequiredVersion: Required version string
    - Message: Human-readable status message
#>
function Doctor-DotNet {
    param(
        [ValidateSet("ERROR", "WARN", "INFO", "DEBUG")]
        [string]$TraceLevel = "ERROR"
    )

    $result = @{
        Name = "dotnet"
        Status = "Error"
        Version = $null
        InstalledSdks = @()
        RequiredVersion = $script:RequiredDotNetVersion
        Message = ""
    }

    Write-Log -Level "DEBUG" -Message "Checking .NET SDK installation..." -TraceLevel $TraceLevel

    try {
        $version = Get-InstalledDotNetVersion -TraceLevel $TraceLevel

        if ($version) {
            $result.Version = $version
            $result.InstalledSdks = Get-InstalledDotNetSdks -TraceLevel $TraceLevel

            # Check if required version is among installed SDKs
            $hasRequiredVersion = $false
            foreach ($sdk in $result.InstalledSdks) {
                if ($sdk -match "^$($script:RequiredDotNetVersion)\.") {
                    $hasRequiredVersion = $true
                    Write-Log -Level "DEBUG" -Message "Found required SDK version: $sdk" -TraceLevel $TraceLevel
                    break
                }
            }

            if ($hasRequiredVersion) {
                $result.Status = "Installed"
                $result.Message = ".NET SDK $version installed (required: $($script:RequiredDotNetVersion).x)"
            } else {
                $result.Status = "WrongVersion"
                $result.Message = ".NET SDK $version installed, but $($script:RequiredDotNetVersion).x is required"
            }
        } else {
            $result.Status = "NotInstalled"
            $result.Message = ".NET SDK is not installed"
        }
    } catch {
        $result.Status = "Error"
        $result.Message = "Error checking .NET SDK: $_"
        Write-Log -Level "DEBUG" -Message $result.Message -TraceLevel $TraceLevel
    }

    Write-Log -Level "DEBUG" -Message "Doctor-DotNet result: Status=$($result.Status), Version=$($result.Version)" -TraceLevel $TraceLevel
    return $result
}

<#
.SYNOPSIS
    Installs .NET SDK.
.OUTPUTS
    [bool] True if installation succeeded
#>
function Install-DotNet {
    param(
        [switch]$Yes,
        [switch]$Force,
        [ValidateSet("ERROR", "WARN", "INFO", "DEBUG")]
        [string]$TraceLevel = "ERROR"
    )

    Write-Log -Level "INFO" -Message "Installing .NET $($script:RequiredDotNetVersion) SDK..." -TraceLevel $TraceLevel

    if ($IsWindows) {
        # Use winget on Windows
        Write-Log -Level "INFO" -Message "Using winget to install .NET SDK..." -TraceLevel $TraceLevel
        $args = @("install", "Microsoft.DotNet.SDK.9", "--accept-source-agreements", "--accept-package-agreements")
        if ($Yes) { $args += "--silent" }

        & winget @args 2>&1 | ForEach-Object { Write-Log -Level "DEBUG" -Message $_ -TraceLevel $TraceLevel }

        if ($LASTEXITCODE -ne 0) {
            Write-Log -Level "ERROR" -Message "Failed to install .NET SDK via winget" -TraceLevel $TraceLevel
            return $false
        }
    } elseif ($IsMacOS) {
        # Use homebrew on macOS
        Write-Log -Level "INFO" -Message "Using Homebrew to install .NET SDK..." -TraceLevel $TraceLevel

        & brew install dotnet@9 2>&1 | ForEach-Object { Write-Log -Level "DEBUG" -Message $_ -TraceLevel $TraceLevel }

        if ($LASTEXITCODE -ne 0) {
            # Try the dotnet-sdk cask as fallback
            Write-Log -Level "INFO" -Message "Trying dotnet-sdk cask..." -TraceLevel $TraceLevel
            & brew install --cask dotnet-sdk 2>&1 | ForEach-Object { Write-Log -Level "DEBUG" -Message $_ -TraceLevel $TraceLevel }
        }
    } else {
        # Linux - use Microsoft's script
        Write-Log -Level "INFO" -Message "Using Microsoft install script for Linux..." -TraceLevel $TraceLevel
        Write-Log -Level "INFO" -Message "Please visit: https://dotnet.microsoft.com/download/dotnet/9.0" -TraceLevel $TraceLevel
        Write-Log -Level "INFO" -Message "Or use your package manager (apt, dnf, etc.)" -TraceLevel $TraceLevel
        return $false
    }

    # Verify installation
    Update-EnvironmentVariables -TraceLevel $TraceLevel
    $result = Doctor-DotNet -TraceLevel $TraceLevel
    return ($result.Status -eq "Installed")
}

<#
.SYNOPSIS
    Displays formatted Doctor output.
.PARAMETER Result
    Hashtable from Doctor-DotNet
#>
function Show-DoctorOutput {
    param([hashtable]$Result)

    Write-Host ""
    Write-Host ".NET SDK Status" -ForegroundColor Cyan
    Write-Host "===============" -ForegroundColor Cyan
    Write-Host ""

    $statusColor = switch ($Result.Status) {
        "Installed" { "Green" }
        "WrongVersion" { "Yellow" }
        "NotInstalled" { "Red" }
        default { "Red" }
    }

    Write-Host "Status:           " -NoNewline
    Write-Host $Result.Status -ForegroundColor $statusColor

    Write-Host "Required Version: $($Result.RequiredVersion).x"

    if ($Result.Version) {
        Write-Host "Current Version:  $($Result.Version)"
    }

    if ($Result.InstalledSdks -and $Result.InstalledSdks.Count -gt 0) {
        Write-Host "Installed SDKs:   $($Result.InstalledSdks -join ', ')"
    }

    Write-Host ""
    Write-Host $Result.Message -ForegroundColor $statusColor
    Write-Host ""
}

<#
.SYNOPSIS
    Displays help information.
#>
function Show-Help {
    $help = @"
.NET SDK Development Environment Management Script
===================================================

Manages .NET SDK installation and verification.

Usage:
    dotnet.ps1 [-Install] [-Clean] [-Doctor] [-HashTable] [-Json]
               [-Yes] [-Force] [-TraceLevel <level>] [-Help]

Operations:
    -Install    Installs .NET 9 SDK
    -Clean      Removes .NET SDK (not recommended)
    -Doctor     Verifies .NET SDK installation status
    -Help       Shows this help message

Output Formats:
    -HashTable  Returns Doctor results as PowerShell hashtable
    -Json       Returns Doctor results as JSON string

Options:
    -Yes        Automatically answers yes to prompts
    -Force      Skips verification checks
    -TraceLevel Sets output detail level (ERROR, WARN, INFO, DEBUG)

Required Version: .NET 9.0 (as specified in global.json)

Status Values:
    Installed     - Required .NET version is installed and working
    WrongVersion  - .NET is installed but not the required version
    NotInstalled  - .NET SDK is not installed
    Error         - An error occurred during detection

Examples:
    # Check .NET SDK installation
    dotnet.ps1 -Doctor

    # Get status as JSON
    dotnet.ps1 -Doctor -Json

    # Install .NET 9 SDK
    dotnet.ps1 -Install -Yes
"@
    Write-Host $help
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
    $doctorResult = Doctor-DotNet -TraceLevel $TraceLevel

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
        Write-Log -Level "HEADER" -Message "INSTALLING .NET SDK" -TraceLevel $TraceLevel

        # Check if already installed
        if ($doctorResult.Status -eq "Installed" -and -not $Force) {
            Write-Log -Level "INFO" -Message ".NET SDK $($doctorResult.Version) is already installed" -TraceLevel $TraceLevel
            exit 0
        }

        if (Install-DotNet -Yes:$Yes -Force:$Force -TraceLevel $TraceLevel) {
            Write-Log -Level "INFO" -Message ".NET SDK installation complete" -TraceLevel $TraceLevel
            exit 0
        } else {
            Write-Log -Level "ERROR" -Message ".NET SDK installation failed" -TraceLevel $TraceLevel
            exit 1
        }
    }

    if ($Clean) {
        Write-Log -Level "HEADER" -Message "CLEANING .NET SDK" -TraceLevel $TraceLevel
        Write-Log -Level "WARN" -Message "Removing .NET SDK is not recommended and must be done manually" -TraceLevel $TraceLevel
        Write-Log -Level "INFO" -Message "Use your system's package manager or the .NET uninstall tool" -TraceLevel $TraceLevel
        Write-Log -Level "INFO" -Message "See: https://docs.microsoft.com/dotnet/core/additional-tools/uninstall-tool" -TraceLevel $TraceLevel
        exit 0
    }

    exit 0
} catch {
    Write-Log -Level "ERROR" -Message "An error occurred: $_" -TraceLevel $TraceLevel
    exit 1
}
