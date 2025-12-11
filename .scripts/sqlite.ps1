<#
.SYNOPSIS
SQLite development environment management script.
.DESCRIPTION
Provides management for SQLite with Install, Clean, Doctor, and Update operations.
Manages SQLite installation and verification for local database support.

This script follows the standardized template patterns for resource management.
.PARAMETER Install
Installs SQLite
.PARAMETER Clean
Removes SQLite installation
.PARAMETER Doctor
Verifies SQLite installation status and displays version information
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
.\sqlite.ps1 -Doctor -TraceLevel INFO
Verifies SQLite installation with standard logging
.EXAMPLE
.\sqlite.ps1 -Doctor -Json
Returns SQLite status as JSON
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

<#
.SYNOPSIS
    Gets the installed SQLite version.
.OUTPUTS
    [string] Version string or $null if not installed
#>
function Get-InstalledSqliteVersion {
    param(
        [ValidateSet("ERROR", "WARN", "INFO", "DEBUG")]
        [string]$TraceLevel = "ERROR"
    )

    try {
        $output = & sqlite3 --version 2>&1
        if ($LASTEXITCODE -eq 0) {
            # SQLite outputs: "3.43.2 2023-10-10 12:14:04 ..."
            if ($output -match '^(\d+\.\d+\.\d+)') {
                $version = $matches[1]
                Write-Log -Level "DEBUG" -Message "sqlite3 --version output: $version" -TraceLevel $TraceLevel
                return $version
            }
        }
    } catch {
        Write-Log -Level "DEBUG" -Message "sqlite3 not found: $_" -TraceLevel $TraceLevel
    }

    return $null
}

<#
.SYNOPSIS
    Checks if SQLite is available via .NET (Microsoft.Data.Sqlite).
.DESCRIPTION
    Entity Framework Core uses Microsoft.Data.Sqlite which bundles its own SQLite.
    This check verifies that the NuGet package system will work.
.OUTPUTS
    [bool] True if .NET SQLite support is available
#>
function Test-DotNetSqliteSupport {
    param(
        [ValidateSet("ERROR", "WARN", "INFO", "DEBUG")]
        [string]$TraceLevel = "ERROR"
    )

    # .NET SQLite support comes via NuGet packages, not a system install
    # If dotnet is available, SQLite support will work via packages
    try {
        $output = & dotnet --version 2>&1
        if ($LASTEXITCODE -eq 0) {
            Write-Log -Level "DEBUG" -Message ".NET available, SQLite NuGet packages will work" -TraceLevel $TraceLevel
            return $true
        }
    } catch {
        Write-Log -Level "DEBUG" -Message ".NET not available: $_" -TraceLevel $TraceLevel
    }

    return $false
}

<#
.SYNOPSIS
    Runs Doctor operation to verify SQLite installation.
.DESCRIPTION
    Returns a hashtable with installation status. No output except DEBUG level.
    Status values:
    - Installed: SQLite is installed and working
    - DotNetOnly: SQLite CLI not found, but .NET SQLite support available
    - NotInstalled: SQLite is not installed
    - Error: An error occurred during detection
.OUTPUTS
    [hashtable] Status information
#>
function Doctor-Sqlite {
    param(
        [ValidateSet("ERROR", "WARN", "INFO", "DEBUG")]
        [string]$TraceLevel = "ERROR"
    )

    $result = @{
        Name = "sqlite"
        Status = "Error"
        Version = $null
        HasCliTool = $false
        HasDotNetSupport = $false
        Message = ""
    }

    Write-Log -Level "DEBUG" -Message "Checking SQLite installation..." -TraceLevel $TraceLevel

    try {
        # Check for sqlite3 CLI tool
        $version = Get-InstalledSqliteVersion -TraceLevel $TraceLevel
        if ($version) {
            $result.Version = $version
            $result.HasCliTool = $true
        }

        # Check for .NET SQLite support
        $result.HasDotNetSupport = Test-DotNetSqliteSupport -TraceLevel $TraceLevel

        # Determine overall status
        if ($result.HasCliTool) {
            $result.Status = "Installed"
            $result.Message = "SQLite $version installed (CLI and .NET support available)"
        } elseif ($result.HasDotNetSupport) {
            $result.Status = "DotNetOnly"
            $result.Message = "SQLite CLI not found, but .NET SQLite support available via NuGet packages"
        } else {
            $result.Status = "NotInstalled"
            $result.Message = "SQLite is not installed"
        }
    } catch {
        $result.Status = "Error"
        $result.Message = "Error checking SQLite: $_"
        Write-Log -Level "DEBUG" -Message $result.Message -TraceLevel $TraceLevel
    }

    Write-Log -Level "DEBUG" -Message "Doctor-Sqlite result: Status=$($result.Status), Version=$($result.Version)" -TraceLevel $TraceLevel
    return $result
}

<#
.SYNOPSIS
    Installs SQLite.
.OUTPUTS
    [bool] True if installation succeeded
#>
function Install-Sqlite {
    param(
        [switch]$Yes,
        [switch]$Force,
        [ValidateSet("ERROR", "WARN", "INFO", "DEBUG")]
        [string]$TraceLevel = "ERROR"
    )

    Write-Log -Level "INFO" -Message "Installing SQLite..." -TraceLevel $TraceLevel

    if ($IsWindows) {
        # Use winget on Windows
        Write-Log -Level "INFO" -Message "Using winget to install SQLite..." -TraceLevel $TraceLevel
        $args = @("install", "SQLite.SQLite", "--accept-source-agreements", "--accept-package-agreements")
        if ($Yes) { $args += "--silent" }

        & winget @args 2>&1 | ForEach-Object { Write-Log -Level "DEBUG" -Message $_ -TraceLevel $TraceLevel }

        if ($LASTEXITCODE -ne 0) {
            Write-Log -Level "WARN" -Message "winget install returned non-zero exit code" -TraceLevel $TraceLevel
            # Don't fail - .NET SQLite support may still work
        }
    } elseif ($IsMacOS) {
        # SQLite comes pre-installed on macOS, but we can update via homebrew
        Write-Log -Level "INFO" -Message "SQLite is typically pre-installed on macOS" -TraceLevel $TraceLevel
        Write-Log -Level "INFO" -Message "Checking if update is needed via Homebrew..." -TraceLevel $TraceLevel

        & brew install sqlite 2>&1 | ForEach-Object { Write-Log -Level "DEBUG" -Message $_ -TraceLevel $TraceLevel }
    } else {
        # Linux - use package manager
        Write-Log -Level "INFO" -Message "Installing SQLite via apt..." -TraceLevel $TraceLevel
        & sudo apt-get install -y sqlite3 2>&1 | ForEach-Object { Write-Log -Level "DEBUG" -Message $_ -TraceLevel $TraceLevel }
    }

    # Verify installation
    Update-EnvironmentVariables -TraceLevel $TraceLevel
    $result = Doctor-Sqlite -TraceLevel $TraceLevel
    return ($result.Status -eq "Installed" -or $result.Status -eq "DotNetOnly")
}

<#
.SYNOPSIS
    Displays formatted Doctor output.
.PARAMETER Result
    Hashtable from Doctor-Sqlite
#>
function Show-DoctorOutput {
    param([hashtable]$Result)

    Write-Host ""
    Write-Host "SQLite Status" -ForegroundColor Cyan
    Write-Host "=============" -ForegroundColor Cyan
    Write-Host ""

    $statusColor = switch ($Result.Status) {
        "Installed" { "Green" }
        "DotNetOnly" { "Yellow" }
        "NotInstalled" { "Red" }
        default { "Red" }
    }

    Write-Host "Status:          " -NoNewline
    Write-Host $Result.Status -ForegroundColor $statusColor

    if ($Result.Version) {
        Write-Host "CLI Version:     $($Result.Version)"
    }

    Write-Host "CLI Tool:        $(if ($Result.HasCliTool) { 'Yes' } else { 'No' })"
    Write-Host ".NET Support:    $(if ($Result.HasDotNetSupport) { 'Yes' } else { 'No' })"

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
SQLite Development Environment Management Script
=================================================

Manages SQLite installation and verification.

Usage:
    sqlite.ps1 [-Install] [-Clean] [-Doctor] [-HashTable] [-Json]
               [-Yes] [-Force] [-TraceLevel <level>] [-Help]

Operations:
    -Install    Installs SQLite CLI tool
    -Clean      Removes SQLite (not recommended)
    -Doctor     Verifies SQLite installation status
    -Help       Shows this help message

Output Formats:
    -HashTable  Returns Doctor results as PowerShell hashtable
    -Json       Returns Doctor results as JSON string

Options:
    -Yes        Automatically answers yes to prompts
    -Force      Skips verification checks
    -TraceLevel Sets output detail level (ERROR, WARN, INFO, DEBUG)

Status Values:
    Installed     - SQLite CLI is installed and working
    DotNetOnly    - CLI not found, but .NET SQLite support available
    NotInstalled  - SQLite is not available
    Error         - An error occurred during detection

Notes:
    - Entity Framework Core uses Microsoft.Data.Sqlite which bundles SQLite
    - The CLI tool (sqlite3) is optional but useful for debugging
    - macOS typically has SQLite pre-installed
    - On Windows, the CLI is optional if using .NET SQLite packages

Examples:
    # Check SQLite installation
    sqlite.ps1 -Doctor

    # Get status as JSON
    sqlite.ps1 -Doctor -Json

    # Install SQLite CLI
    sqlite.ps1 -Install -Yes
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
    $doctorResult = Doctor-Sqlite -TraceLevel $TraceLevel

    if ($Doctor) {
        if ($HashTable) {
            return $doctorResult
        } elseif ($Json) {
            return ($doctorResult | ConvertTo-Json -Depth 5)
        } else {
            Show-DoctorOutput -Result $doctorResult
            # Consider DotNetOnly as success since the app will work
            exit $(if ($doctorResult.Status -eq "Installed" -or $doctorResult.Status -eq "DotNetOnly") { 0 } else { 1 })
        }
    }

    if ($Install) {
        Write-Log -Level "HEADER" -Message "INSTALLING SQLITE" -TraceLevel $TraceLevel

        # Check if already installed
        if ($doctorResult.Status -eq "Installed" -and -not $Force) {
            Write-Log -Level "INFO" -Message "SQLite $($doctorResult.Version) is already installed" -TraceLevel $TraceLevel
            exit 0
        }

        if (Install-Sqlite -Yes:$Yes -Force:$Force -TraceLevel $TraceLevel) {
            Write-Log -Level "INFO" -Message "SQLite installation complete" -TraceLevel $TraceLevel
            exit 0
        } else {
            Write-Log -Level "ERROR" -Message "SQLite installation failed" -TraceLevel $TraceLevel
            exit 1
        }
    }

    if ($Clean) {
        Write-Log -Level "HEADER" -Message "CLEANING SQLITE" -TraceLevel $TraceLevel
        Write-Log -Level "WARN" -Message "Removing SQLite must be done via your system's package manager" -TraceLevel $TraceLevel
        exit 0
    }

    exit 0
} catch {
    Write-Log -Level "ERROR" -Message "An error occurred: $_" -TraceLevel $TraceLevel
    exit 1
}
