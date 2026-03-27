<#
.SYNOPSIS
Master dependency management script for Catan3 development environment.
.DESCRIPTION
Orchestrates installation, verification, and cleanup of all development dependencies:
- .NET 9 SDK
- Docker (for CosmosDB emulator)
- Node.js and npm (for Claude Code and other tools)
- Claude CLI
- VC++ Debug redistributable (Windows only, for Desktop app)

This script follows the standardized template patterns for resource management.
.PARAMETER Install
Installs all development dependencies
.PARAMETER Clean
Removes/resets all dependencies
.PARAMETER Doctor
Verifies installation status of all dependencies and displays version information
.PARAMETER HashTable
Returns results as a PowerShell hashtable (for programmatic use)
.PARAMETER Json
Returns results as JSON (for programmatic use)
.PARAMETER Yes
Automatically confirms operations without prompting
.PARAMETER Force
Forces operations without additional safety checks
.PARAMETER TraceLevel
Sets output detail level (ERROR, WARN, INFO, DEBUG)
.PARAMETER Help
Displays help information
.EXAMPLE
.\install-dependencies.ps1 -Doctor -TraceLevel INFO
Verifies all dependency installations with standard logging
.EXAMPLE
.\install-dependencies.ps1 -Install -Yes
Installs all dependencies without prompting
.EXAMPLE
.\install-dependencies.ps1 -Doctor -Json
Returns dependency status as JSON
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

# Define all dependency scripts and their order
$script:DependencyScripts = @(
    @{
        Name = "dotnet"
        Script = "dotnet.ps1"
        Description = ".NET 9 SDK"
        Required = $true
        Platforms = @("Windows", "macOS", "Linux")
    },
    @{
        Name = "docker"
        Script = "docker.ps1"
        Description = "Docker (Cosmos emulator)"
        Required = $false
        Platforms = @("Windows", "macOS", "Linux")
    },
    @{
        Name = "node"
        Script = "node.ps1"
        Description = "Node.js and npm"
        Required = $false
        Platforms = @("Windows", "macOS", "Linux")
    },
    @{
        Name = "claude-cli"
        Script = "claude-cli.ps1"
        Description = "Claude CLI"
        Required = $false
        Platforms = @("Windows", "macOS", "Linux")
    },
    @{
        Name = "vcpp-debug"
        Script = "vcpp-debug.ps1"
        Description = "VC++ Debug Redistributable"
        Required = $false
        Platforms = @("Windows")
    }
)

<#
.SYNOPSIS
    Gets the current platform name.
.OUTPUTS
    [string] "Windows", "macOS", or "Linux"
#>
function Get-CurrentPlatform {
    if ($IsWindows) { return "Windows" }
    elseif ($IsMacOS) { return "macOS" }
    else { return "Linux" }
}

<#
.SYNOPSIS
    Checks if a dependency script exists.
.PARAMETER ScriptName
    Name of the script file
.OUTPUTS
    [bool] True if script exists
#>
function Test-DependencyScript {
    param([string]$ScriptName)
    $fullPath = Join-Path $scriptPath $ScriptName
    return Test-Path $fullPath
}

<#
.SYNOPSIS
    Runs Doctor on a dependency script and returns the result hashtable.
.PARAMETER Dependency
    Dependency configuration hashtable
.OUTPUTS
    [hashtable] Result from Doctor-* function with Status, Version, Message, etc.
#>
function Get-DependencyDoctorResult {
    param(
        [hashtable]$Dependency
    )

    $result = @{
        Name = $Dependency.Name
        Description = $Dependency.Description
        Status = "Unknown"
        Version = $null
        Message = ""
        Error = $null
    }

    $scriptFile = Join-Path $scriptPath $Dependency.Script

    if (-not (Test-Path $scriptFile)) {
        $result.Status = "NotImplemented"
        $result.Message = "Script not yet implemented"
        $result.Error = "Script not found: $($Dependency.Script)"
        return $result
    }

    try {
        # Run Doctor with -HashTable to get raw result
        $doctorResult = & $scriptFile -Doctor -HashTable -TraceLevel $TraceLevel

        if ($doctorResult -is [hashtable]) {
            # Merge the doctor result into our result
            $result.Status = $doctorResult.Status
            $result.Version = $doctorResult.Version
            $result.Message = $doctorResult.Message
            # Copy any additional properties
            foreach ($key in $doctorResult.Keys) {
                if (-not $result.ContainsKey($key)) {
                    $result[$key] = $doctorResult[$key]
                }
            }
        } else {
            $result.Status = "Error"
            $result.Error = "Doctor did not return a hashtable"
        }
    } catch {
        $result.Status = "Error"
        $result.Error = $_.Exception.Message
        $result.Message = "Error running Doctor: $_"
    }

    return $result
}

<#
.SYNOPSIS
    Runs Install or Clean on a dependency script.
.PARAMETER Dependency
    Dependency configuration hashtable
.PARAMETER Operation
    Operation to perform (Install, Clean)
.OUTPUTS
    [bool] True if operation succeeded
#>
function Invoke-DependencyOperation {
    param(
        [hashtable]$Dependency,
        [ValidateSet("Install", "Clean")]
        [string]$Operation
    )

    $scriptFile = Join-Path $scriptPath $Dependency.Script

    if (-not (Test-Path $scriptFile)) {
        Write-Log -Level "WARN" -Message "Script not found: $($Dependency.Script)" -TraceLevel $TraceLevel
        return $false
    }

    try {
        $params = @{
            TraceLevel = $TraceLevel
        }

        switch ($Operation) {
            "Install" { $params.Install = $true; $params.Yes = $Yes; $params.Force = $Force }
            "Clean" { $params.Clean = $true; $params.Yes = $Yes; $params.Force = $Force }
        }

        & $scriptFile @params

        return ($LASTEXITCODE -eq 0)
    } catch {
        Write-Log -Level "ERROR" -Message "Error running $Operation on $($Dependency.Name): $_" -TraceLevel $TraceLevel
        return $false
    }
}

<#
.SYNOPSIS
    Runs Doctor on all dependencies and collects status.
.OUTPUTS
    [array] Array of dependency status hashtables
#>
function Get-AllDependencyStatus {
    $platform = Get-CurrentPlatform
    $results = @()

    foreach ($dep in $script:DependencyScripts) {
        # Skip if not applicable to current platform
        if ($dep.Platforms -notcontains $platform) {
            continue
        }

        Write-Log -Level "DEBUG" -Message "Checking $($dep.Description)..." -TraceLevel $TraceLevel
        $result = Get-DependencyDoctorResult -Dependency $dep
        $results += $result
    }

    return $results
}

<#
.SYNOPSIS
    Displays a formatted table of dependency status.
.PARAMETER Results
    Array of dependency status hashtables
#>
function Show-DependencyTable {
    param([array]$Results)

    Write-Log -Level INFO -Message "" -NoLabel
    Write-Log -Level INFO -Message "Dependency Status" -NoLabel -ForegroundColor Cyan
    Write-Log -Level INFO -Message "=================" -NoLabel -ForegroundColor Cyan
    Write-Log -Level INFO -Message "" -NoLabel

    # Calculate column widths
    $nameWidth = ($Results | ForEach-Object { $_.Description.Length } | Measure-Object -Maximum).Maximum
    $nameWidth = [Math]::Max($nameWidth, 12)

    # Header
    $header = "{0,-$nameWidth} {1,-15} {2,-15}" -f "Component", "Status", "Version"
    Write-Log -Level INFO -Message $header -NoLabel -ForegroundColor White
    Write-Log -Level DEBUG -Message ("-" * ($nameWidth + 32)) -NoLabel

    foreach ($result in $Results) {
        $statusColor = switch ($result.Status) {
            "OK" { "Green" }
            "NotImplemented" { "Yellow" }
            "Failed" { "Red" }
            "Error" { "Red" }
            default { "Gray" }
        }

        $version = if ($result.Version) { $result.Version } else { "-" }
        $line = "{0,-$nameWidth} {1,-15} {2,-15}" -f $result.Description, $result.Status, $version

        Write-Log -Level INFO -Message $line -ForegroundColor $statusColor -NoLabel

        if ($result.Error -and $TraceLevel -eq "DEBUG") {
            Write-Log -Level ERROR -Message "     Error: $($result.Error)"
        }
    }

    Write-Log -Level INFO -Message "" -NoLabel
}

<#
.SYNOPSIS
    Displays help information.
#>
function Show-Help {
    $help = @"
Catan3 Development Dependencies Management Script
=================================================

Manages all development dependencies required for building and running Catan3.

Usage:
    install-dependencies.ps1 [-Install] [-Clean] [-Doctor] [-HashTable] [-Json]
                             [-Yes] [-Force] [-TraceLevel <level>] [-Help]

Operations:
    -Install    Installs all development dependencies
    -Clean      Removes/resets all dependencies
    -Doctor     Verifies installation status with version table
    -Help       Shows this help message

Output Formats:
    -HashTable  Returns results as PowerShell hashtable
    -Json       Returns results as JSON string

Options:
    -Yes        Automatically answers yes to prompts
    -Force      Skips verification checks
    -TraceLevel Sets output detail level (ERROR, WARN, INFO, DEBUG)

Dependencies Managed:
    - .NET 9 SDK          Required for building all projects
    - Docker              For CosmosDB local emulator
    - Node.js/npm         Optional, for Claude Code and dev tools
    - Claude CLI          Optional, for AI-assisted development
    - VC++ Debug          Windows only, for Desktop app debugging

Examples:
    # Check all dependencies
    install-dependencies.ps1 -Doctor

    # Install all dependencies without prompts
    install-dependencies.ps1 -Install -Yes

    # Get status as JSON for scripting
    install-dependencies.ps1 -Doctor -Json

    # Detailed diagnostic output
    install-dependencies.ps1 -Doctor -TraceLevel DEBUG
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

    $platform = Get-CurrentPlatform
    Write-Log -Level "DEBUG" -Message "Platform: $platform" -TraceLevel $TraceLevel

    if ($Doctor) {
        $results = Get-AllDependencyStatus

        if ($HashTable) {
            return $results
        } elseif ($Json) {
            return ($results | ConvertTo-Json -Depth 5)
        } else {
            Show-DependencyTable -Results $results
        }
    }

    if ($Install) {
        Write-Log -Level "HEADER" -Message "INSTALLING DEVELOPMENT DEPENDENCIES" -TraceLevel $TraceLevel

        foreach ($dep in $script:DependencyScripts) {
            # Skip if not applicable to current platform
            if ($dep.Platforms -notcontains $platform) {
                Write-Log -Level "DEBUG" -Message "Skipping $($dep.Description) (not applicable to $platform)" -TraceLevel $TraceLevel
                continue
            }

            # Check current status first
            $doctorResult = Get-DependencyDoctorResult -Dependency $dep

            if ($doctorResult.Status -eq "NotImplemented") {
                Write-Log -Level "WARN" -Message "$($dep.Description): Script not yet implemented" -TraceLevel $TraceLevel
                continue
            }

            # Skip if already installed (unless Force)
            if ($doctorResult.Status -eq "Installed" -and -not $Force) {
                $versionInfo = if ($doctorResult.Version) { " v$($doctorResult.Version)" } else { "" }
                Write-Log -Level "INFO" -Message "$($dep.Description)$versionInfo is already installed" -TraceLevel $TraceLevel
                continue
            }

            Write-Log -Level "INFO" -Message "Installing $($dep.Description)..." -TraceLevel $TraceLevel
            $success = Invoke-DependencyOperation -Dependency $dep -Operation "Install"

            if (-not $success) {
                if ($dep.Required) {
                    Write-Log -Level "ERROR" -Message "Failed to install required dependency: $($dep.Description)" -TraceLevel $TraceLevel
                    exit 1
                } else {
                    Write-Log -Level "WARN" -Message "Failed to install optional dependency: $($dep.Description)" -TraceLevel $TraceLevel
                }
            }
        }

        Write-Log -Level "INFO" -Message "Dependency installation complete" -TraceLevel $TraceLevel
    }

    if ($Clean) {
        Write-Log -Level "HEADER" -Message "CLEANING DEVELOPMENT DEPENDENCIES" -TraceLevel $TraceLevel

        foreach ($dep in $script:DependencyScripts) {
            # Skip if not applicable to current platform
            if ($dep.Platforms -notcontains $platform) {
                continue
            }

            # Check current status first
            $doctorResult = Get-DependencyDoctorResult -Dependency $dep

            if ($doctorResult.Status -eq "NotImplemented") {
                Write-Log -Level "DEBUG" -Message "$($dep.Description): Script not yet implemented" -TraceLevel $TraceLevel
                continue
            }

            if ($doctorResult.Status -eq "NotInstalled") {
                Write-Log -Level "DEBUG" -Message "$($dep.Description) is not installed, skipping clean" -TraceLevel $TraceLevel
                continue
            }

            Write-Log -Level "INFO" -Message "Cleaning $($dep.Description)..." -TraceLevel $TraceLevel
            Invoke-DependencyOperation -Dependency $dep -Operation "Clean"
        }

        Write-Log -Level "INFO" -Message "Dependency cleanup complete" -TraceLevel $TraceLevel
    }

    exit 0
} catch {
    Write-Log -Level "ERROR" -Message "An error occurred: $_" -TraceLevel $TraceLevel
    exit 1
}
