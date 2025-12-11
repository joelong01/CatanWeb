<#
.SYNOPSIS
VC++ Debug Redistributable management script (Windows only).
.DESCRIPTION
Provides management for Visual C++ Debug Runtime with Install, Clean, Doctor operations.
Required for debugging the Desktop app on Windows.

This script follows the standardized template patterns for resource management.
.PARAMETER Install
Installs VC++ Debug Redistributable
.PARAMETER Clean
Removes VC++ Debug Redistributable installation
.PARAMETER Doctor
Verifies VC++ Debug Redistributable installation status
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
.\vcpp-debug.ps1 -Doctor -TraceLevel INFO
Verifies VC++ Debug Redistributable installation
.EXAMPLE
.\vcpp-debug.ps1 -Doctor -Json
Returns status as JSON
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
    Checks if running on Windows.
.OUTPUTS
    [bool] True if running on Windows
#>
function Test-WindowsPlatform {
    return $IsWindows -eq $true
}

<#
.SYNOPSIS
    Checks if Visual Studio is installed.
.OUTPUTS
    [hashtable] Information about VS installation
#>
function Get-VisualStudioInfo {
    param(
        [ValidateSet("ERROR", "WARN", "INFO", "DEBUG")]
        [string]$TraceLevel = "ERROR"
    )

    $result = @{
        Installed = $false
        Path = $null
        Edition = $null
        HasDebugRuntime = $false
    }

    if (-not (Test-WindowsPlatform)) {
        return $result
    }

    try {
        # Check common VS installation paths
        $vsPaths = @(
            "${env:ProgramFiles}\Microsoft Visual Studio\2022\Enterprise",
            "${env:ProgramFiles}\Microsoft Visual Studio\2022\Professional",
            "${env:ProgramFiles}\Microsoft Visual Studio\2022\Community",
            "${env:ProgramFiles(x86)}\Microsoft Visual Studio\2022\BuildTools"
        )

        foreach ($path in $vsPaths) {
            if (Test-Path $path) {
                $result.Installed = $true
                $result.Path = $path
                $result.Edition = Split-Path $path -Leaf
                Write-Log -Level "DEBUG" -Message "Found Visual Studio at: $path" -TraceLevel $TraceLevel
                break
            }
        }

        # Check for debug runtime DLLs
        $debugDllPaths = @(
            "${env:SystemRoot}\System32\vcruntime140d.dll",
            "${env:SystemRoot}\System32\msvcp140d.dll"
        )

        $allDebugDllsExist = $true
        foreach ($dll in $debugDllPaths) {
            if (-not (Test-Path $dll)) {
                Write-Log -Level "DEBUG" -Message "Debug DLL not found: $dll" -TraceLevel $TraceLevel
                $allDebugDllsExist = $false
            }
        }

        $result.HasDebugRuntime = $allDebugDllsExist

    } catch {
        Write-Log -Level "DEBUG" -Message "Error checking Visual Studio: $_" -TraceLevel $TraceLevel
    }

    return $result
}

<#
.SYNOPSIS
    Runs Doctor operation to verify VC++ Debug Runtime installation.
.DESCRIPTION
    Returns a hashtable with installation status. No output except DEBUG level.
    Status values:
    - Installed: VC++ Debug Runtime is installed
    - NotInstalled: VC++ Debug Runtime is not installed
    - NotApplicable: Not running on Windows
    - Error: An error occurred during detection
.OUTPUTS
    [hashtable] Status information
#>
function Doctor-VcppDebug {
    param(
        [ValidateSet("ERROR", "WARN", "INFO", "DEBUG")]
        [string]$TraceLevel = "ERROR"
    )

    $result = @{
        Name = "vcpp-debug"
        Status = "Error"
        IsWindows = Test-WindowsPlatform
        VisualStudioInstalled = $false
        VisualStudioEdition = $null
        DebugRuntimeInstalled = $false
        Message = ""
    }

    Write-Log -Level "DEBUG" -Message "Checking VC++ Debug Runtime installation..." -TraceLevel $TraceLevel

    try {
        if (-not $result.IsWindows) {
            $result.Status = "NotApplicable"
            $result.Message = "VC++ Debug Runtime is only applicable on Windows"
            return $result
        }

        $vsInfo = Get-VisualStudioInfo -TraceLevel $TraceLevel
        $result.VisualStudioInstalled = $vsInfo.Installed
        $result.VisualStudioEdition = $vsInfo.Edition
        $result.DebugRuntimeInstalled = $vsInfo.HasDebugRuntime

        if ($vsInfo.HasDebugRuntime) {
            $result.Status = "Installed"
            $result.Message = "VC++ Debug Runtime is installed"
            if ($vsInfo.Installed) {
                $result.Message += " (Visual Studio $($vsInfo.Edition) detected)"
            }
        } elseif ($vsInfo.Installed) {
            $result.Status = "PartialInstall"
            $result.Message = "Visual Studio $($vsInfo.Edition) is installed but debug runtime DLLs are missing"
        } else {
            $result.Status = "NotInstalled"
            $result.Message = "VC++ Debug Runtime is not installed (Visual Studio not found)"
        }

    } catch {
        $result.Status = "Error"
        $result.Message = "Error checking VC++ Debug Runtime: $_"
        Write-Log -Level "DEBUG" -Message $result.Message -TraceLevel $TraceLevel
    }

    Write-Log -Level "DEBUG" -Message "Doctor-VcppDebug result: Status=$($result.Status)" -TraceLevel $TraceLevel
    return $result
}

<#
.SYNOPSIS
    Displays formatted Doctor output.
.PARAMETER Result
    Hashtable from Doctor-VcppDebug
#>
function Show-DoctorOutput {
    param([hashtable]$Result)

    Write-Host ""
    Write-Host "VC++ Debug Runtime Status" -ForegroundColor Cyan
    Write-Host "=========================" -ForegroundColor Cyan
    Write-Host ""

    $statusColor = switch ($Result.Status) {
        "Installed" { "Green" }
        "PartialInstall" { "Yellow" }
        "NotInstalled" { "Red" }
        "NotApplicable" { "Gray" }
        default { "Red" }
    }

    Write-Host "Status:              " -NoNewline
    Write-Host $Result.Status -ForegroundColor $statusColor

    Write-Host "Platform:            $(if ($Result.IsWindows) { 'Windows' } else { 'Non-Windows' })"

    if ($Result.IsWindows) {
        Write-Host "Visual Studio:       $(if ($Result.VisualStudioInstalled) { $Result.VisualStudioEdition } else { 'Not installed' })"
        Write-Host "Debug Runtime DLLs:  $(if ($Result.DebugRuntimeInstalled) { 'Yes' } else { 'No' })"
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
VC++ Debug Redistributable Management Script (Windows Only)
============================================================

Manages Visual C++ Debug Runtime installation and verification.
Required for debugging the Desktop app on Windows.

Usage:
    vcpp-debug.ps1 [-Install] [-Clean] [-Doctor] [-HashTable] [-Json]
                   [-Yes] [-Force] [-TraceLevel <level>] [-Help]

Operations:
    -Install    Opens Visual Studio Installer to install debug components
    -Clean      Provides guidance on removal
    -Doctor     Verifies VC++ Debug Runtime installation status
    -Help       Shows this help message

Output Formats:
    -HashTable  Returns Doctor results as PowerShell hashtable
    -Json       Returns Doctor results as JSON string

Options:
    -Yes        Automatically answers yes to prompts
    -Force      Skips verification checks
    -TraceLevel Sets output detail level (ERROR, WARN, INFO, DEBUG)

Status Values:
    Installed      - VC++ Debug Runtime is installed
    PartialInstall - Visual Studio installed but debug DLLs missing
    NotInstalled   - Not installed
    NotApplicable  - Not running on Windows
    Error          - An error occurred during detection

Notes:
    - VC++ Debug Runtime is only needed for debugging the Desktop app
    - Typically installed with Visual Studio
    - Debug DLLs (vcruntime140d.dll, msvcp140d.dll) must be present

Examples:
    # Check installation status
    vcpp-debug.ps1 -Doctor

    # Get status as JSON
    vcpp-debug.ps1 -Doctor -Json
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
    $doctorResult = Doctor-VcppDebug -TraceLevel $TraceLevel

    if ($Doctor) {
        if ($HashTable) {
            return $doctorResult
        } elseif ($Json) {
            return ($doctorResult | ConvertTo-Json -Depth 5)
        } else {
            Show-DoctorOutput -Result $doctorResult
            exit $(if ($doctorResult.Status -eq "Installed" -or $doctorResult.Status -eq "NotApplicable") { 0 } else { 1 })
        }
    }

    if ($Install) {
        Write-Log -Level "HEADER" -Message "INSTALLING VC++ DEBUG RUNTIME" -TraceLevel $TraceLevel

        if (-not $doctorResult.IsWindows) {
            Write-Log -Level "INFO" -Message "VC++ Debug Runtime is only applicable on Windows" -TraceLevel $TraceLevel
            exit 0
        }

        if ($doctorResult.Status -eq "Installed" -and -not $Force) {
            Write-Log -Level "INFO" -Message "VC++ Debug Runtime is already installed" -TraceLevel $TraceLevel
            exit 0
        }

        Write-Log -Level "INFO" -Message "To install VC++ Debug Runtime:" -TraceLevel $TraceLevel
        Write-Log -Level "INFO" -Message "1. Install Visual Studio 2022 (Community, Professional, or Enterprise)" -TraceLevel $TraceLevel
        Write-Log -Level "INFO" -Message "2. In Visual Studio Installer, select 'Desktop development with C++'" -TraceLevel $TraceLevel
        Write-Log -Level "INFO" -Message "3. Ensure 'MSVC v143 - VS 2022 C++ x64/x86 build tools' is checked" -TraceLevel $TraceLevel
        Write-Log -Level "INFO" -Message "" -TraceLevel $TraceLevel
        Write-Log -Level "INFO" -Message "Download Visual Studio: https://visualstudio.microsoft.com/downloads/" -TraceLevel $TraceLevel

        # Try to launch Visual Studio Installer if available
        $vsInstallerPath = "${env:ProgramFiles(x86)}\Microsoft Visual Studio\Installer\vs_installer.exe"
        if (Test-Path $vsInstallerPath) {
            if (-not $Yes) {
                $response = Read-Host "Launch Visual Studio Installer? (Y/N)"
                if ($response -ne 'Y' -and $response -ne 'y') {
                    exit 0
                }
            }
            Write-Log -Level "INFO" -Message "Launching Visual Studio Installer..." -TraceLevel $TraceLevel
            Start-Process $vsInstallerPath
        }

        exit 0
    }

    if ($Clean) {
        Write-Log -Level "HEADER" -Message "CLEANING VC++ DEBUG RUNTIME" -TraceLevel $TraceLevel

        if (-not $doctorResult.IsWindows) {
            Write-Log -Level "INFO" -Message "VC++ Debug Runtime is only applicable on Windows" -TraceLevel $TraceLevel
            exit 0
        }

        Write-Log -Level "INFO" -Message "To remove VC++ Debug Runtime:" -TraceLevel $TraceLevel
        Write-Log -Level "INFO" -Message "1. Open Visual Studio Installer" -TraceLevel $TraceLevel
        Write-Log -Level "INFO" -Message "2. Modify your Visual Studio installation" -TraceLevel $TraceLevel
        Write-Log -Level "INFO" -Message "3. Uncheck 'Desktop development with C++' workload" -TraceLevel $TraceLevel
        Write-Log -Level "INFO" -Message "" -TraceLevel $TraceLevel
        Write-Log -Level "WARN" -Message "Note: Removing debug runtime may break Desktop app debugging" -TraceLevel $TraceLevel

        exit 0
    }

    exit 0
} catch {
    Write-Log -Level "ERROR" -Message "An error occurred: $_" -TraceLevel $TraceLevel
    exit 1
}
