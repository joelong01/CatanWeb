<#
.SYNOPSIS
Node.js development environment management script.
.DESCRIPTION
Provides management for Node.js development environment with Install, Clean,
Doctor, and Update operations. Manages Node.js runtime, npm package manager,
and global npm packages like markdownlint-cli.

This script follows the standardized template patterns for resource management.
.PARAMETER Install
Installs and configures Node.js runtime and npm packages
.PARAMETER Clean
Removes Node.js runtime, npm packages, and cleans environment
.PARAMETER Doctor
Verifies Node.js installation status and displays version information
.PARAMETER Update
Updates Node.js runtime and npm packages to latest versions
.PARAMETER Yes
Automatically confirms operations without prompting
.PARAMETER Force
Forces operations without additional safety checks
.PARAMETER TraceLevel
Sets output detail level (ERROR, WARN, INFO, DEBUG)
.PARAMETER Help
Displays help information
.EXAMPLE
.\node-new.ps1 -Install -TraceLevel INFO
Installs Node.js with standard logging
.EXAMPLE
.\node-new.ps1 -Doctor -TraceLevel DEBUG
Verifies Node.js installation with detailed debug output
.EXAMPLE
.\node-new.ps1 -Update -Yes
Updates Node.js to latest versions without prompting
#>

param(
    [Parameter()]
    [switch]$Install,

    [Parameter()]
    [switch]$Clean,

    [Parameter()]
    [switch]$Doctor,

    [Parameter()]
    [switch]$Update,

    [Parameter()]
    [switch]$LTS,

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

# Debug platform detection
Write-Log -Level "DEBUG" -Message "Platform detection: IsWindows=$IsWindows, IsMacOS=$IsMacOS, IsLinux=$IsLinux" -TraceLevel $TraceLevel

# Load version configuration for reproducible builds
$versionsPath = Join-Path (Split-Path $scriptPath -Parent) "pinned-versions.json"
if (Test-Path $versionsPath) {
    $VERSIONS_CONFIG = Get-Content $versionsPath -Raw | ConvertFrom-Json
    Write-Log -Level "DEBUG" -Message "Loaded versions configuration from: $versionsPath" -TraceLevel $TraceLevel
} else {
    # Version pinning is optional - not using it is normal, so only log at DEBUG level
    Write-Log -Level "DEBUG" -Message "pinned-versions.json not found (version pinning disabled)" -TraceLevel $TraceLevel
    $VERSIONS_CONFIG = $null
}

# Configuration constants
# Status update interval for long-running operations (in seconds)
# PURPOSE: Controls how frequently progress messages are displayed during installations
# USAGE: Used by Invoke-BackgroundInstaller for Node.js and npm package installations
# MODIFY: Node.js might take a while to install, but seeing this tick every second is a
# good user experience and doesn't impact performance in any discernable way.
$STATUS_UPDATE_INTERVAL = 1

Write-Log -Level "DEBUG" -Message "Configuration: STATUS_UPDATE_INTERVAL=$STATUS_UPDATE_INTERVAL seconds" -TraceLevel $TraceLevel

# Platform-specific Node.js tools configuration hashtable
# This manages cross-platform Node.js with different installation methods,
# verification commands, and platform-specific settings
$script:NodeResources = if ($IsWindows) {
    @{
        # Main tool configuration - Node.js runtime
        NodeJS = @{
            Name = "nodejs"
            DisplayName = "Node.js JavaScript runtime"
            InstallMethod = "winget"
            PackageId = "OpenJS.NodeJS"
            ExecutableName = "node.exe"
            VerifyCommand = "node"
            VerifyArgs = @("--version")
            VerifyOutputFilter = "v([0-9.]+).*"
            InstallArgs = @("install", "OpenJS.NodeJS")
            # Version properties filled by Doctor-Resource
            InstalledVersion = $null
            PinnedVersion = $null
            LatestVersion = $null
            LTSVersion = $null
        }
        # NPM comes bundled with Node.js but we track it separately
        NPM = @{
            Name = "npm"
            DisplayName = "Node Package Manager"
            InstallMethod = "bundled"  # Comes with Node.js
            ExecutableName = "npm.cmd"
            VerifyCommand = "npm"
            VerifyArgs = @("--version")
            VerifyOutputFilter = "([0-9.]+).*"
            # Version properties filled by Doctor-Resource
            InstalledVersion = $null
            PinnedVersion = $null
            LatestVersion = $null
            LTSVersion = $null
        }
        # Global npm packages (utilities like markdownlint are handled by utilities.ps1)
        GlobalPackages = @(
            # Add other global npm packages here as needed
            # markdownlint-cli is managed by utilities.ps1 script
        )
        # Cleanup configuration for generic Clean-Resources function
        Cleanup = @{
            ProcessNames = @("node", "npm", "npx", "markdownlint")
            CleanupCommand = "winget"
            CleanupArgs = @("uninstall", "--id", "OpenJS.NodeJS", "--silent")
            DirectoriesToRemove = @("AppData\Roaming\npm", "AppData\Roaming\.npm", ".npm")
            EnvironmentVars = @("NODE_PATH", "npm_config_prefix")
            PathEntries = @()  # Node.js manages its own PATH entries
        }
    }
} elseif ($IsMacOS) {
    @{
        NodeJS = @{
            Name = "nodejs"
            DisplayName = "Node.js JavaScript runtime"
            InstallMethod = "homebrew"
            PackageId = "node"
            ExecutableName = "node"
            VerifyCommand = "node"
            VerifyArgs = @("--version")
            VerifyOutputFilter = "v([0-9.]+).*"
            InstallArgs = @("install", "node")
            # Version properties filled by Doctor-Resource
            InstalledVersion = $null
            PinnedVersion = $null
            LatestVersion = $null
        }
        NPM = @{
            Name = "npm"
            DisplayName = "Node Package Manager"
            InstallMethod = "bundled"
            ExecutableName = "npm"
            VerifyCommand = "npm"
            VerifyArgs = @("--version")
            VerifyOutputFilter = "([0-9.]+).*"
            # Version properties filled by Doctor-Resource
            InstalledVersion = $null
            PinnedVersion = $null
            LatestVersion = $null
        }
        GlobalPackages = @(
            # Add other global npm packages here as needed
            # markdownlint-cli is managed by utilities.ps1 script
        )
        # Cleanup configuration for generic Clean-Resources function
        Cleanup = @{
            ProcessNames = @("node", "npm", "npx", "markdownlint")
            CleanupCommand = $null  # Will be determined dynamically based on installation method
            CleanupArgs = @()
            DirectoriesToRemove = @(".npm", ".nvm")  # Include .nvm directory for NVM installations
            EnvironmentVars = @("NODE_PATH", "npm_config_prefix", "NVM_DIR", "NVM_BIN", "NVM_INC")
            PathEntries = @()
        }
    }
} else {
    @{
        NodeJS = @{
            Name = "nodejs"
            DisplayName = "Node.js JavaScript runtime"
            InstallMethod = "script-download"
            DownloadUrl = "https://deb.nodesource.com/setup_lts.x"
            ExecutableName = "setup_lts.sh"
            VerifyCommand = "node"
            VerifyArgs = @("--version")
            VerifyOutputFilter = "v([0-9.]+).*"
            InstallArgs = @("-y")
            ShellScript = $true
            # Version properties filled by Doctor-Resource
            InstalledVersion = $null
            PinnedVersion = $null
            LatestVersion = $null
        }
        NPM = @{
            Name = "npm"
            DisplayName = "Node Package Manager"
            InstallMethod = "bundled"
            ExecutableName = "npm"
            VerifyCommand = "npm"
            VerifyArgs = @("--version")
            VerifyOutputFilter = "([0-9.]+).*"
            # Version properties filled by Doctor-Resource
            InstalledVersion = $null
            PinnedVersion = $null
            LatestVersion = $null
        }
        GlobalPackages = @(
            # Add other global npm packages here as needed
            # markdownlint-cli is managed by utilities.ps1 script
        )
        # Cleanup configuration for generic Clean-Resources function
        Cleanup = @{
            ProcessNames = @("node", "npm", "npx", "markdownlint")
            CleanupCommand = "apt"
            CleanupArgs = @("remove", "-y", "nodejs", "npm")
            DirectoriesToRemove = @(".npm")
            EnvironmentVars = @("NODE_PATH", "npm_config_prefix")
            PathEntries = @()
        }
    }
}

<#
.SYNOPSIS
    Gets the latest Node.js version from nodejs.org API.
.DESCRIPTION
    Queries Node.js releases API to determine the latest stable version.
    Returns the actual latest version (not just LTS) so users can see
    when they have a newer version than what we consider "recommended".

    HYBRID APPROACH: This follows the rust.ps1 pattern of component-specific version functions.
    Node.js has complex versioning (latest vs LTS, multiple channels) that benefits from
    dedicated functions rather than forcing it into a generic pattern.
.PARAMETER TraceLevel
    Sets output detail level (ERROR, WARN, INFO, DEBUG)
.OUTPUTS
    [string] Latest version or "Unknown" if unavailable
.EXAMPLE
    $version = Get-LatestNodeVersion -TraceLevel "DEBUG"
#>
function Get-LatestNodeVersion {
    param(
        [ValidateSet("ERROR", "WARN", "INFO", "DEBUG")]
        [string]$TraceLevel = "ERROR"
    )

    try {
        Write-Log -Level "DEBUG" -Message "Querying latest Node.js version..." -TraceLevel $TraceLevel

        # Query Node.js API for all releases
        $response = Invoke-RestMethod -Uri "https://nodejs.org/dist/index.json" -Headers @{ "User-Agent" = "PowerShell" } -TimeoutSec 10
        if ($response -and $response.Count -gt 0) {
            # Return the latest version (first in the list)
            $latestVersion = $response[0].version -replace "^v", ""
            Write-Log -Level "DEBUG" -Message "Found Node.js latest version: $latestVersion" -TraceLevel $TraceLevel
            return $latestVersion
        } else {
            throw "No releases found in Node.js API response"
        }
    } catch {
        Write-Log -Level "DEBUG" -Message "Node.js API failed: $_" -TraceLevel $TraceLevel
        return "24.8.0"  # Fallback version
    }
}

<#
.SYNOPSIS
    Gets the latest npm version from npm registry.
.DESCRIPTION
    Queries npm registry for the latest npm version. npm is bundled with Node.js
    but can be updated independently, so we track its version separately.

    COMPONENT-SPECIFIC FUNCTION: npm has its own release cycle and registry,
    so it benefits from dedicated version checking logic.
.PARAMETER TraceLevel
    Sets output detail level (ERROR, WARN, INFO, DEBUG)
.OUTPUTS
    [string] Latest version or "Unknown" if unavailable
.EXAMPLE
    $version = Get-LatestNpmVersion -TraceLevel "DEBUG"
#>
function Get-LatestNpmVersion {
    param(
        [ValidateSet("ERROR", "WARN", "INFO", "DEBUG")]
        [string]$TraceLevel = "ERROR"
    )

    try {
        Write-Log -Level "DEBUG" -Message "Querying latest npm version..." -TraceLevel $TraceLevel

        # Query npm registry for npm package itself
        $response = Invoke-RestMethod -Uri "https://registry.npmjs.org/npm/latest" -Headers @{ "User-Agent" = "PowerShell" } -TimeoutSec 10
        if ($response -and $response.version) {
            Write-Log -Level "DEBUG" -Message "Found npm version: $($response.version)" -TraceLevel $TraceLevel
            return $response.version
        } else {
            throw "No version found in npm registry response"
        }
    } catch {
        Write-Log -Level "DEBUG" -Message "npm registry query failed: $_" -TraceLevel $TraceLevel
        return "10.8.2"  # Fallback version
    }
}

<#
.SYNOPSIS
    Gets the latest version for npm packages from registry.
.DESCRIPTION
    Queries npm registry for the latest version of a specific npm package.
    This function handles the common pattern for npm package version checking.

    COMPONENT-SPECIFIC FUNCTION: npm packages have consistent API patterns
    that benefit from dedicated handling rather than generic approaches.
.PARAMETER PackageName
    Name of the npm package to query
.PARAMETER TraceLevel
    Sets output detail level (ERROR, WARN, INFO, DEBUG)
.OUTPUTS
    [string] Latest version or "Unknown" if unavailable
.EXAMPLE
    $version = Get-LatestNpmPackageVersion -PackageName "markdownlint-cli" -TraceLevel "DEBUG"
#>
function Get-LatestNpmPackageVersion {
    param(
        [Parameter(Mandatory = $true)]
        [string]$PackageName,
        [ValidateSet("ERROR", "WARN", "INFO", "DEBUG")]
        [string]$TraceLevel = "ERROR"
    )

    try {
        Write-Log -Level "DEBUG" -Message "Querying latest version for npm package: $PackageName" -TraceLevel $TraceLevel

        # Query npm registry for package
        $response = Invoke-RestMethod -Uri "https://registry.npmjs.org/$PackageName/latest" -Headers @{ "User-Agent" = "PowerShell" } -TimeoutSec 10
        if ($response -and $response.version) {
            Write-Log -Level "DEBUG" -Message "Found $PackageName version: $($response.version)" -TraceLevel $TraceLevel
            return $response.version
        } else {
            throw "No version found in registry response for $PackageName"
        }
    } catch {
        Write-Log -Level "DEBUG" -Message "npm registry query failed for $PackageName`: $_" -TraceLevel $TraceLevel

        # Fallback versions for known packages
        switch ($PackageName) {
            "markdownlint-cli" { return "0.41.0" }
            default { return "Unknown" }
        }
    }
}

<#
.SYNOPSIS
    Gets the latest LTS Node.js version from nodejs.org API.
.DESCRIPTION
    Queries Node.js releases API to find the latest LTS (Long Term Support) version.
    This is separate from the latest version and provides the recommended production version.
.PARAMETER ToolName
    Name of the tool to query (nodejs, npm, packages, etc.)
.PARAMETER TraceLevel
    Sets output detail level (ERROR, WARN, INFO, DEBUG)
.OUTPUTS
    [string] Latest LTS version or "Unknown" if unavailable
.EXAMPLE
    $ltsVersion = Get-LatestLTSNodeVersion -ToolName "nodejs" -TraceLevel "DEBUG"
#>
function Get-LatestLTSNodeVersion {
    param(
        [string]$ToolName,
        [ValidateSet("ERROR", "WARN", "INFO", "DEBUG")]
        [string]$TraceLevel = "ERROR"
    )

    try {
        Write-Log -Level "DEBUG" -Message "Querying latest LTS version for: $ToolName" -TraceLevel $TraceLevel

        # Handle Node.js runtime - return the latest LTS version
        if ($ToolName -eq "nodejs") {
            try {
                # Query Node.js API for all releases
                $response = Invoke-RestMethod -Uri "https://nodejs.org/dist/index.json" -Headers @{ "User-Agent" = "PowerShell" } -TimeoutSec 10
                if ($response -and $response.Count -gt 0) {
                    # Find the latest LTS version (first entry where lts is not false)
                    $ltsRelease = $response | Where-Object { $_.lts -ne $false } | Select-Object -First 1
                    if ($ltsRelease) {
                        $ltsVersion = $ltsRelease.version -replace "^v", ""
                        Write-Log -Level "DEBUG" -Message "Found Node.js latest LTS version: $ltsVersion ($($ltsRelease.lts))" -TraceLevel $TraceLevel
                        return $ltsVersion
                    }
                }
            } catch {
                Write-Log -Level "DEBUG" -Message "Node.js LTS API failed: $_" -TraceLevel $TraceLevel
            }
        }

        # For npm and other tools, LTS doesn't apply - return the same as latest
        if ($ToolName -eq "npm" -or $ToolName -eq "markdownlint-cli") {
            return Get-LatestNodeVersion -ToolName $ToolName -TraceLevel $TraceLevel
        }

        # Fallback LTS versions for known tools
        switch ($ToolName) {
            "nodejs" { return "22.20.0" }  # Current LTS as of late 2024
            "npm" { return "10.8.2" }      # Same as latest
            "markdownlint-cli" { return "0.41.0" }  # Same as latest
            default { return "Unknown" }
        }
    } catch {
        Write-Log -Level "DEBUG" -Message "Failed to get latest LTS version for $ToolName`: $_" -TraceLevel $TraceLevel
        return "Unknown"
    }
}

<#
.SYNOPSIS
    Gets the path to the Node.js executable.
.DESCRIPTION
    Finds the Node.js executable using platform-specific discovery logic.
    Handles winget, homebrew, system installations, and PATH-based discovery.
.PARAMETER TraceLevel
    Sets output detail level (ERROR, WARN, INFO, DEBUG)
.OUTPUTS
    [string] Path to Node.js executable, or "node" as fallback
.EXAMPLE
    $nodePath = Get-NodeExecutablePath -TraceLevel "DEBUG"
#>
function Get-NodeExecutablePath {
    param(
        [ValidateSet("ERROR", "WARN", "INFO", "DEBUG")]
        [string]$TraceLevel = "ERROR"
    )

    Write-Log -Level "DEBUG" -Message "Discovering Node.js executable path..." -TraceLevel $TraceLevel

    # Default fallback
    $nodePath = "node"
    if ($IsWindows) { $nodePath = "node.exe" }

    try {
        if ($IsWindows) {
            # Try winget user-scope installations first
            $localAppData = $env:LOCALAPPDATA
            $wingetPackagesPath = Join-Path $localAppData "Microsoft\WinGet\Packages"

            $packagePatterns = @(
                "OpenJS.NodeJS_*",
                "OpenJS.NodeJS.LTS_*"
            )

            $nodeCandidates = @()
            foreach ($pattern in $packagePatterns) {
                $packageDirs = Get-ChildItem -Path (Join-Path $wingetPackagesPath $pattern) -Directory -ErrorAction SilentlyContinue
                foreach ($packageDir in $packageDirs) {
                    $nodeDirs = Get-ChildItem -Path (Join-Path $packageDir.FullName "node-v*") -Directory -ErrorAction SilentlyContinue
                    foreach ($nodeDir in $nodeDirs) {
                        $nodeExe = Join-Path $nodeDir.FullName "node.exe"
                        if (Test-Path $nodeExe) {
                            $nodeCandidates += $nodeExe
                            Write-Log -Level "DEBUG" -Message "Found Node.js candidate: $nodeExe" -TraceLevel $TraceLevel
                        }
                    }
                }
            }

            if ($nodeCandidates.Count -gt 0) {
                # Sort by version to get the most recent
                $sortedCandidates = $nodeCandidates | Sort-Object {
                    if ($_ -match 'node-v(\d+)\.(\d+)\.(\d+)') {
                        [int]$matches[1] * 10000 + [int]$matches[2] * 100 + [int]$matches[3]
                    } else { 0 }
                } -Descending

                $nodePath = $sortedCandidates[0]
                Write-Log -Level "DEBUG" -Message "Using Node.js at: $nodePath" -TraceLevel $TraceLevel
                return $nodePath
            }

            # Try system installation path as fallback
            $systemNodePath = Join-Path ${env:ProgramFiles} "nodejs\node.exe"
            if (Test-Path $systemNodePath) {
                Write-Log -Level "DEBUG" -Message "Using system Node.js at: $systemNodePath" -TraceLevel $TraceLevel
                return $systemNodePath
            }
        }

        # For non-Windows or if no specific installation found, try PATH
        $nodeCommand = Get-Command $nodePath -ErrorAction SilentlyContinue
        if ($nodeCommand) {
            Write-Log -Level "DEBUG" -Message "Using Node.js from PATH at: $($nodeCommand.Source)" -TraceLevel $TraceLevel
            return $nodeCommand.Source
        }

        Write-Log -Level "DEBUG" -Message "Using Node.js fallback: $nodePath" -TraceLevel $TraceLevel
        return $nodePath

    } catch {
        Write-Log -Level "DEBUG" -Message "Node.js path discovery failed: $_" -TraceLevel $TraceLevel
        return $nodePath
    }
}

<#
.SYNOPSIS
    Gets the path to the npm executable.
.DESCRIPTION
    Finds the npm executable using platform-specific discovery logic.
    Handles winget, homebrew, system installations, and PATH-based discovery.
.PARAMETER TraceLevel
    Sets output detail level (ERROR, WARN, INFO, DEBUG)
.OUTPUTS
    [string] Path to npm executable, or "npm" as fallback
.EXAMPLE
    $npmPath = Get-NpmExecutablePath -TraceLevel "DEBUG"
#>
function Get-NpmExecutablePath {
    param(
        [ValidateSet("ERROR", "WARN", "INFO", "DEBUG")]
        [string]$TraceLevel = "ERROR"
    )

    Write-Log -Level "DEBUG" -Message "Discovering npm executable path..." -TraceLevel $TraceLevel

    # Default fallback
    $npmPath = "npm"
    if ($IsWindows) { $npmPath = "npm.cmd" }

    try {
        if ($IsWindows) {
            # Try winget user-scope installations first using explicit path construction
            $localAppData = $env:LOCALAPPDATA
            $wingetPackagesPath = Join-Path $localAppData "Microsoft\WinGet\Packages"

            $packagePatterns = @(
                "OpenJS.NodeJS_*",
                "OpenJS.NodeJS.LTS_*"
            )

            $npmCandidates = @()
            foreach ($pattern in $packagePatterns) {
                $packageDirs = Get-ChildItem -Path (Join-Path $wingetPackagesPath $pattern) -Directory -ErrorAction SilentlyContinue
                foreach ($packageDir in $packageDirs) {
                    $nodeDirs = Get-ChildItem -Path (Join-Path $packageDir.FullName "node-v*") -Directory -ErrorAction SilentlyContinue
                    foreach ($nodeDir in $nodeDirs) {
                        $npmCmd = Join-Path $nodeDir.FullName "npm.cmd"
                        if (Test-Path $npmCmd) {
                            $npmCandidates += $npmCmd
                            Write-Log -Level "DEBUG" -Message "Found npm candidate: $npmCmd" -TraceLevel $TraceLevel
                        }
                    }
                }
            }

            if ($npmCandidates.Count -gt 0) {
                # Sort by version to get the most recent
                $sortedCandidates = $npmCandidates | Sort-Object {
                    if ($_ -match 'node-v(\d+)\.(\d+)\.(\d+)') {
                        [int]$matches[1] * 10000 + [int]$matches[2] * 100 + [int]$matches[3]
                    } else { 0 }
                } -Descending

                # Use Select-Object -First 1 to avoid array indexing issues
                $npmPath = $sortedCandidates | Select-Object -First 1
                Write-Log -Level "DEBUG" -Message "Using npm at: $npmPath" -TraceLevel $TraceLevel
                return $npmPath
            }

            # Try system installation path as fallback
            $systemNpmPath = Join-Path ${env:ProgramFiles} "nodejs\npm.cmd"
            if (Test-Path $systemNpmPath) {
                Write-Log -Level "DEBUG" -Message "Using system npm at: $systemNpmPath" -TraceLevel $TraceLevel
                return $systemNpmPath
            }
        }

        # For non-Windows or if no specific installation found, try PATH
        $npmCommand = Get-Command $npmPath -ErrorAction SilentlyContinue
        if ($npmCommand) {
            Write-Log -Level "DEBUG" -Message "Using npm from PATH at: $($npmCommand.Source)" -TraceLevel $TraceLevel
            return $npmCommand.Source
        }

        Write-Log -Level "DEBUG" -Message "Using npm fallback: $npmPath" -TraceLevel $TraceLevel
        return $npmPath

    } catch {
        Write-Log -Level "DEBUG" -Message "npm path discovery failed: $_" -TraceLevel $TraceLevel
        return $npmPath
    }
}

<#
.SYNOPSIS
    Stops Node.js-related processes.
.DESCRIPTION
    Terminates Node.js processes that might interfere with installation or cleanup,
    including node, npm, and related processes.
.PARAMETER TraceLevel
    Sets output detail level (ERROR, WARN, INFO, DEBUG)
#>
function Stop-NodeProcesses {
    param(
        [ValidateSet("ERROR", "WARN", "INFO", "DEBUG")]
        [string]$TraceLevel = "INFO"
    )

    Write-Log -Level "DEBUG" -Message "Stopping Node.js processes..." -TraceLevel $TraceLevel

    # Define Node.js-related process names (without .exe extension)
    $nodeProcesses = @(
        "node",
        "npm",
        "npx",
        "markdownlint"
    )

    Stop-RunningProcesses -ProcessNames $nodeProcesses -TraceLevel $TraceLevel

    # Give processes time to fully terminate
    Start-Sleep -Seconds 2

    Write-Log -Level "DEBUG" -Message "Node.js process cleanup completed" -TraceLevel $TraceLevel
}

<#
.SYNOPSIS
    Detects all Node.js installation methods on the system.
.DESCRIPTION
    Scans for different Node.js installations including NVM, Homebrew, system packages, etc.
    Returns information about detected installations for proper cleanup.
.PARAMETER TraceLevel
    Sets output detail level (ERROR, WARN, INFO, DEBUG)
.OUTPUTS
    [hashtable] Information about detected Node.js installations
#>
function Get-NodeInstallations {
    param(
        [ValidateSet("ERROR", "WARN", "INFO", "DEBUG")]
        [string]$TraceLevel = "ERROR"
    )

    $installations = @{
        NVM = $false
        Homebrew = $false
        System = $false
        NodePaths = @()
    }

    # Check for NVM installation
    $homeDir = if ($IsWindows) { $env:USERPROFILE } else { $env:HOME }
    $nvmDir = Join-Path $homeDir ".nvm"

    if (Test-Path $nvmDir) {
        $installations.NVM = $true
        Write-Log -Level "DEBUG" -Message "Detected NVM installation at: $nvmDir" -TraceLevel $TraceLevel
    }

    # Check for all Node.js executables in PATH
    $nodeCommands = @()
    if ($IsWindows) {
        $nodeCommands = @(Get-Command node.exe -All -ErrorAction SilentlyContinue)
    } else {
        $nodeCommands = @(Get-Command node -All -ErrorAction SilentlyContinue)
    }

    foreach ($nodeCmd in $nodeCommands) {
        $nodePath = $nodeCmd.Source
        $installations.NodePaths += $nodePath
        Write-Log -Level "DEBUG" -Message "Found Node.js at: $nodePath" -TraceLevel $TraceLevel

        # Check if it's actually a Homebrew installation by verifying with brew
        if ($nodePath -like "*/usr/local/bin/node*" -or $nodePath -like "*/opt/homebrew/bin/node*") {
            if (Get-Command brew -ErrorAction SilentlyContinue) {
                & brew list node 2>&1 | Out-Null
                if ($LASTEXITCODE -eq 0) {
                    $installations.Homebrew = $true
                    Write-Log -Level "DEBUG" -Message "Detected Homebrew-managed Node.js (verified)" -TraceLevel $TraceLevel
                } else {
                    # In /usr/local/bin but not Homebrew-managed - treat as system installation
                    $installations.System = $true
                    Write-Log -Level "DEBUG" -Message "Detected system-managed Node.js in /usr/local/bin (not Homebrew)" -TraceLevel $TraceLevel
                }
            } else {
                # No brew command available, treat as system installation
                $installations.System = $true
                Write-Log -Level "DEBUG" -Message "Detected system-managed Node.js (no Homebrew available)" -TraceLevel $TraceLevel
            }
        }
        # Check if it's in a system location
        elseif ($nodePath -like "*/usr/bin/node*" -or $nodePath -like "*/bin/node*") {
            $installations.System = $true
            Write-Log -Level "DEBUG" -Message "Detected system-managed Node.js" -TraceLevel $TraceLevel
        }
        # Check if it's NVM-managed
        elseif ($nodePath -like "*/.nvm/*" -or $nodePath -like "*\.nvm\*") {
            # NVM is already detected by directory check, but good to be explicit
            Write-Log -Level "DEBUG" -Message "Confirmed NVM-managed Node.js" -TraceLevel $TraceLevel
        }
    }

    return $installations
}

<#
.SYNOPSIS
    Removes global npm packages.
.DESCRIPTION
    Uninstalls global npm packages before removing Node.js to prevent orphaned packages.
.PARAMETER TraceLevel
    Sets output detail level (ERROR, WARN, INFO, DEBUG)
.OUTPUTS
    [bool] True if successful, False otherwise
#>
function Remove-GlobalNpmPackages {
    param(
        [ValidateSet("ERROR", "WARN", "INFO", "DEBUG")]
        [string]$TraceLevel = "ERROR"
    )

    Write-Log -Level "INFO" -Message "Removing global npm packages..." -TraceLevel $TraceLevel

    try {
        foreach ($package in $script:NodeResources.GlobalPackages) {
            if (Get-Command $package.VerifyCommand -ErrorAction SilentlyContinue) {
                Write-Log -Level "DEBUG" -Message "Removing $($package.DisplayName)..." -TraceLevel $TraceLevel
                try {
                    & npm uninstall -g $package.PackageName 2>&1 | Out-Null
                    if ($LASTEXITCODE -eq 0) {
                        Write-Log -Level "DEBUG" -Message "Successfully removed $($package.DisplayName)" -TraceLevel $TraceLevel
                    } else {
                        Write-Log -Level "DEBUG" -Message "$($package.DisplayName) was not installed" -TraceLevel $TraceLevel
                    }
                } catch {
                    Write-Log -Level "DEBUG" -Message "Failed to remove $($package.DisplayName): $_" -TraceLevel $TraceLevel
                }
            }
        }
        return $true
    } catch {
        Write-Log -Level "WARN" -Message "Global package removal failed: $_" -TraceLevel $TraceLevel
        return $false
    }
}

<#
.SYNOPSIS
    Removes NVM-managed Node.js installations.
.DESCRIPTION
    Handles cleanup of Node.js versions managed by NVM (Node Version Manager).
.PARAMETER TraceLevel
    Sets output detail level (ERROR, WARN, INFO, DEBUG)
.OUTPUTS
    [bool] True if successful, False otherwise
#>
function Remove-NvmNodeInstallation {
    param(
        [ValidateSet("ERROR", "WARN", "INFO", "DEBUG")]
        [string]$TraceLevel = "ERROR"
    )

    Write-Log -Level "INFO" -Message "Removing NVM-managed Node.js versions..." -TraceLevel $TraceLevel

    try {
        $homeDir = if ($IsWindows) { $env:USERPROFILE } else { $env:HOME }
        $nvmDir = Join-Path $homeDir ".nvm"

        if (Test-Path "$nvmDir/nvm.sh") {
            # Try to uninstall current Node.js version via NVM
            $currentVersion = & node --version 2>&1
            if ($LASTEXITCODE -eq 0) {
                $version = $currentVersion -replace "^v", ""
                Write-Log -Level "DEBUG" -Message "Attempting to uninstall Node.js version: $version" -TraceLevel $TraceLevel

                # Try to run nvm uninstall
                $uninstallResult = Invoke-BackgroundInstaller -FilePath "bash" -ArgumentList @("-c", "source ~/.nvm/nvm.sh && nvm uninstall $version") -OperationName "NVM Node.js uninstall" -ProgressInterval $STATUS_UPDATE_INTERVAL -TraceLevel $TraceLevel

                if ($uninstallResult) {
                    Write-Log -Level "INFO" -Message "Successfully uninstalled Node.js version $version via NVM" -TraceLevel $TraceLevel
                } else {
                    Write-Log -Level "DEBUG" -Message "NVM uninstall command failed, will clean directories manually" -TraceLevel $TraceLevel
                }
            }
        }
        return $true
    } catch {
        Write-Log -Level "DEBUG" -Message "NVM cleanup failed: $_" -TraceLevel $TraceLevel
        return $false
    }
}
<#
.SYNOPSIS
    Detects system-wide Node.js installations.
.DESCRIPTION
    Scans for system-wide Node.js installations that are not managed by NVM, Homebrew,
    or other package managers. These are typically direct installations from nodejs.org
    or system package managers.
.PARAMETER TraceLevel
    Sets output detail level (ERROR, WARN, INFO, DEBUG)
.OUTPUTS
    [bool] True if system-wide Node.js installation is detected, False otherwise
.EXAMPLE
    $detected = Detect-SystemNode -TraceLevel "DEBUG"
    Detects system-wide Node.js with detailed logging
#>
function Detect-SystemNode {
    param(
        [ValidateSet("ERROR", "WARN", "INFO", "DEBUG")]
        [string]$TraceLevel = "ERROR"
    )

    Write-Log -Level "DEBUG" -Message "Detecting system-wide Node.js installations..." -TraceLevel $TraceLevel

    try {
        if ($IsMacOS -or $IsLinux) {
            $nodePath = (Get-Command node -ErrorAction SilentlyContinue)?.Source

            if ($null -eq $nodePath) {
                Write-Log -Level "DEBUG" -Message "Node.js not found on system" -TraceLevel $TraceLevel
                return $false
            }

            if ($nodePath -eq "/usr/local/bin/node") {
                Write-Log -Level "INFO" -Message "System-wide Node.js installation detected at $nodePath" -TraceLevel $TraceLevel
                return $true
            } else {
                Write-Log -Level "DEBUG" -Message "Node.js found at $nodePath (not a system-wide install)" -TraceLevel $TraceLevel
                return $false
            }

        } elseif ($IsWindows) {
            $nodePath = (Get-Command node.exe -ErrorAction SilentlyContinue)?.Source

            if ($null -eq $nodePath) {
                Write-Log -Level "DEBUG" -Message "Node.js not found on system" -TraceLevel $TraceLevel
                return $false
            }

            if ($nodePath -like "C:\Program Files\nodejs*") {
                Write-Log -Level "INFO" -Message "System-wide Node.js installation detected at $nodePath" -TraceLevel $TraceLevel
                return $true
            } else {
                Write-Log -Level "DEBUG" -Message "Node.js found at $nodePath (not a system-wide install)" -TraceLevel $TraceLevel
                return $false
            }
        } else {
            Write-Log -Level "WARN" -Message "Unsupported platform for system Node.js detection" -TraceLevel $TraceLevel
            return $false
        }
    } catch {
        Write-Log -Level "ERROR" -Message "Failed to detect system Node.js: $_" -TraceLevel $TraceLevel
        return $false
    }
}

<#
.SYNOPSIS
    Removes system-wide Node.js installations.
.DESCRIPTION
    Removes system-wide Node.js installations that are not managed by package managers.
    On macOS/Linux: removes Node binaries from /usr/local/bin and node_modules from /usr/local/lib.
    On Windows: removes Node from "C:\Program Files\nodejs" if found.

    This function requires elevated privileges (sudo on macOS/Linux, admin on Windows).
.PARAMETER TraceLevel
    Sets output detail level (ERROR, WARN, INFO, DEBUG)
.OUTPUTS
    [bool] True if removal succeeds, False otherwise
.EXAMPLE
    Clean-SystemNode -TraceLevel "INFO"
    Removes system-wide Node.js with standard logging
#>
function Clean-SystemNode {
    param(
        [ValidateSet("ERROR", "WARN", "INFO", "DEBUG")]
        [string]$TraceLevel = "ERROR"
    )

    Write-Log -Level "DEBUG" -Message "Starting system-wide Node.js removal..." -TraceLevel $TraceLevel

    try {
        if ($IsMacOS -or $IsLinux) {
            $nodePath = (Get-Command node -ErrorAction SilentlyContinue)?.Source

            if ($null -eq $nodePath) {
                Write-Log -Level "DEBUG" -Message "Node.js not found on system" -TraceLevel $TraceLevel
                return $true
            }

            if ($nodePath -eq "/usr/local/bin/node") {
                Write-Log -Level "INFO" -Message "Removing system-wide Node.js installation at $nodePath" -TraceLevel $TraceLevel
                Write-Log -Level "INFO" -Message "Removing Node.js binaries and global modules..." -TraceLevel $TraceLevel

                # Pre-authenticate sudo to avoid password prompt interfering with status messages
                Write-Log -Level "DEBUG" -Message "Pre-authenticating sudo for system Node.js removal..." -TraceLevel $TraceLevel
                & sudo -v 2>&1 | Out-Null

                # Remove Node.js binaries
                $removeResult = Invoke-BackgroundInstaller -FilePath "sudo" -ArgumentList @("rm", "-f", "/usr/local/bin/node", "/usr/local/bin/npm", "/usr/local/bin/npx") -OperationName "Remove Node.js binaries" -ProgressInterval $STATUS_UPDATE_INTERVAL -TraceLevel $TraceLevel

                if ($removeResult) {
                    Write-Log -Level "DEBUG" -Message "Successfully removed Node.js binaries" -TraceLevel $TraceLevel
                } else {
                    Write-Log -Level "WARN" -Message "Failed to remove some Node.js binaries" -TraceLevel $TraceLevel
                }

                # Remove global modules
                $moduleResult = Invoke-BackgroundInstaller -FilePath "sudo" -ArgumentList @("rm", "-rf", "/usr/local/lib/node_modules") -OperationName "Remove Node.js modules" -ProgressInterval $STATUS_UPDATE_INTERVAL -TraceLevel $TraceLevel

                if ($moduleResult) {
                    Write-Log -Level "DEBUG" -Message "Successfully removed Node.js global modules" -TraceLevel $TraceLevel
                } else {
                    Write-Log -Level "WARN" -Message "Failed to remove Node.js global modules" -TraceLevel $TraceLevel
                }

                # Forget Node package receipt on macOS
                if ($IsMacOS) {
                    try {
                        $pkgOutput = & pkgutil --pkgs 2>&1 | Where-Object { $_ -like "*node*" }
                        if ($pkgOutput) {
                            Write-Log -Level "INFO" -Message "Forgetting Node.js package receipt: $pkgOutput" -TraceLevel $TraceLevel
                            & sudo pkgutil --forget $pkgOutput 2>&1 | Out-Null
                            if ($LASTEXITCODE -eq 0) {
                                Write-Log -Level "DEBUG" -Message "Successfully forgot Node.js package receipt" -TraceLevel $TraceLevel
                            } else {
                                Write-Log -Level "WARN" -Message "Failed to forget Node.js package receipt" -TraceLevel $TraceLevel
                            }
                        }
                    } catch {
                        Write-Log -Level "DEBUG" -Message "No Node.js package receipt found to forget" -TraceLevel $TraceLevel
                    }
                }

                Write-Log -Level "INFO" -Message "System-wide Node.js removal completed" -TraceLevel $TraceLevel
                return $true
            } else {
                Write-Log -Level "DEBUG" -Message "Node.js found at $nodePath (not a system-wide install). No action taken." -TraceLevel $TraceLevel
                return $true
            }

        } elseif ($IsWindows) {
            $nodePath = (Get-Command node.exe -ErrorAction SilentlyContinue)?.Source

            if ($null -eq $nodePath) {
                Write-Log -Level "DEBUG" -Message "Node.js not found on system" -TraceLevel $TraceLevel
                return $true
            }

            if ($nodePath -like "C:\Program Files\nodejs*") {
                Write-Log -Level "INFO" -Message "Removing system-wide Node.js installation at $nodePath" -TraceLevel $TraceLevel
                Write-Log -Level "INFO" -Message "Removing Node.js directory..." -TraceLevel $TraceLevel

                try {
                    Remove-Item "C:\Program Files\nodejs" -Recurse -Force -ErrorAction Stop
                    Write-Log -Level "INFO" -Message "System-wide Node.js removal completed" -TraceLevel $TraceLevel
                    return $true
                } catch {
                    Write-Log -Level "ERROR" -Message "Failed to remove Node.js directory: $_" -TraceLevel $TraceLevel
                    return $false
                }
            } else {
                Write-Log -Level "DEBUG" -Message "Node.js found at $nodePath (not a system-wide install). No action taken." -TraceLevel $TraceLevel
                return $true
            }
        } else {
            Write-Log -Level "WARN" -Message "Unsupported platform for system Node.js removal" -TraceLevel $TraceLevel
            return $false
        }
    } catch {
        Write-Log -Level "ERROR" -Message "System Node.js removal failed: $_" -TraceLevel $TraceLevel
        return $false
    }
}
<#
.SYNOPSIS
    Removes winget-managed Node.js installations on Windows.
.DESCRIPTION
    Handles cleanup of Node.js packages installed via winget, including both
    OpenJS.NodeJS and OpenJS.NodeJS.LTS packages that might be present.
.PARAMETER TraceLevel
    Sets output detail level (ERROR, WARN, INFO, DEBUG)
.OUTPUTS
    [bool] True if successful, False otherwise
#>
function Clean-WingetNodeInstallation {
    param(
        [ValidateSet("ERROR", "WARN", "INFO", "DEBUG")]
        [string]$TraceLevel = "ERROR"
    )

    Write-Log -Level "INFO" -Message "Removing winget-managed Node.js..." -TraceLevel $TraceLevel

    try {
        $uninstallSuccess = $true

        # Check and uninstall each possible winget Node.js package
        $wingetPackages = @("OpenJS.NodeJS", "OpenJS.NodeJS.LTS")

        foreach ($packageId in $wingetPackages) {
            # Check if package is installed
            $listOutput = & winget list --id $packageId 2>&1
            if ($LASTEXITCODE -eq 0 -and $listOutput -notlike "*No installed package found*") {
                Write-Log -Level "INFO" -Message "Uninstalling winget package: $packageId" -TraceLevel $TraceLevel

                $uninstallArgs = @("uninstall", "--id", $packageId, "--silent")
                $uninstallResult = Invoke-BackgroundInstaller -FilePath "winget" -ArgumentList $uninstallArgs -OperationName "$packageId uninstall" -ProgressInterval $STATUS_UPDATE_INTERVAL -TraceLevel $TraceLevel

                if ($uninstallResult) {
                    Write-Log -Level "INFO" -Message "Successfully uninstalled $packageId" -TraceLevel $TraceLevel
                } else {
                    Write-Log -Level "WARN" -Message "Failed to uninstall $packageId" -TraceLevel $TraceLevel
                    $uninstallSuccess = $false
                }

                # Also manually clean up winget package directories (winget sometimes leaves these behind)
                $packagePattern = "$env:LOCALAPPDATA\Microsoft\WinGet\Packages\$packageId*"
                $packageDirs = Get-ChildItem -Path $packagePattern -Directory -ErrorAction SilentlyContinue
                foreach ($packageDir in $packageDirs) {
                    try {
                        Write-Log -Level "DEBUG" -Message "Removing winget package directory: $($packageDir.FullName)" -TraceLevel $TraceLevel
                        Remove-Item -Path $packageDir.FullName -Recurse -Force -ErrorAction Stop
                        Write-Log -Level "DEBUG" -Message "Successfully removed package directory: $($packageDir.FullName)" -TraceLevel $TraceLevel
                    } catch {
                        Write-Log -Level "WARN" -Message "Failed to remove package directory $($packageDir.FullName): $_" -TraceLevel $TraceLevel
                        $uninstallSuccess = $false
                    }
                }
            } else {
                Write-Log -Level "DEBUG" -Message "$packageId not installed via winget" -TraceLevel $TraceLevel
            }
        }

        return $uninstallSuccess
    } catch {
        Write-Log -Level "WARN" -Message "winget cleanup failed: $_" -TraceLevel $TraceLevel
        return $false
    }
}

<#
.SYNOPSIS
    Removes Homebrew-managed Node.js installation.
.DESCRIPTION
    Handles cleanup of Node.js installed via Homebrew package manager.
.PARAMETER TraceLevel
    Sets output detail level (ERROR, WARN, INFO, DEBUG)
.OUTPUTS
    [bool] True if successful, False otherwise
#>
function Clean-HomebrewNodeIntallation {
    param(
        [ValidateSet("ERROR", "WARN", "INFO", "DEBUG")]
        [string]$TraceLevel = "ERROR"
    )

    Write-Log -Level "INFO" -Message "Removing Homebrew-managed Node.js..." -TraceLevel $TraceLevel

    try {
        if (Get-Command brew -ErrorAction SilentlyContinue) {
            # Check if Node.js is actually installed via Homebrew
            & brew list node 2>&1 | Out-Null
            if ($LASTEXITCODE -eq 0) {
                Write-Log -Level "DEBUG" -Message "Confirmed Homebrew Node.js installation" -TraceLevel $TraceLevel
                $uninstallResult = Invoke-BackgroundInstaller -FilePath "brew" -ArgumentList @("uninstall", "--ignore-dependencies", "node") -OperationName "Homebrew Node.js uninstall" -ProgressInterval $STATUS_UPDATE_INTERVAL -TraceLevel $TraceLevel

                if ($uninstallResult) {
                    Write-Log -Level "INFO" -Message "Successfully uninstalled Node.js via Homebrew" -TraceLevel $TraceLevel
                    return $true
                } else {
                    Write-Log -Level "WARN" -Message "Homebrew Node.js uninstall failed" -TraceLevel $TraceLevel
                    return $false
                }
            } else {
                Write-Log -Level "DEBUG" -Message "Node.js not installed via Homebrew" -TraceLevel $TraceLevel
                return $true
            }
        } else {
            Write-Log -Level "DEBUG" -Message "Homebrew not available" -TraceLevel $TraceLevel
            return $true
        }
    } catch {
        Write-Log -Level "WARN" -Message "Homebrew cleanup failed: $_" -TraceLevel $TraceLevel
        return $false
    }
}

<#
.SYNOPSIS
    Cleans up Node.js directories and environment variables.
.DESCRIPTION
    Removes Node.js-related directories and environment variables using hashtable configuration.
.PARAMETER TraceLevel
    Sets output detail level (ERROR, WARN, INFO, DEBUG)
.OUTPUTS
    [bool] True if successful, False otherwise
#>
function Clean-NodeDirectoriesAndEnvironment {
    param(
        [ValidateSet("ERROR", "WARN", "INFO", "DEBUG")]
        [string]$TraceLevel = "ERROR"
    )

    try {
        $cleanup = $script:NodeResources.Cleanup

        # Clean up directories (generic pattern using hashtable configuration)
        if ($cleanup.DirectoriesToRemove -and $cleanup.DirectoriesToRemove.Count -gt 0) {
            Write-Log -Level "INFO" -Message "Cleaning up Node.js directories..." -TraceLevel $TraceLevel
            $homeDir = if ($IsWindows) { $env:USERPROFILE } else { $env:HOME }

            foreach ($relativeDir in $cleanup.DirectoriesToRemove) {
                $fullPath = Join-Path $homeDir $relativeDir
                if (Test-Path $fullPath) {
                    Write-Log -Level "INFO" -Message "Removing directory: $fullPath" -TraceLevel $TraceLevel
                    try {
                        Remove-Item $fullPath -Recurse -Force -ErrorAction Stop
                        Write-Log -Level "DEBUG" -Message "Successfully removed: $fullPath" -TraceLevel $TraceLevel
                    } catch {
                        Write-Log -Level "WARN" -Message "Failed to remove directory $fullPath`: $_" -TraceLevel $TraceLevel
                    }
                } else {
                    Write-Log -Level "DEBUG" -Message "Directory does not exist: $fullPath" -TraceLevel $TraceLevel
                }
            }
        }

        # Clean up environment variables (generic pattern using hashtable configuration)
        if ($IsWindows -and $cleanup.EnvironmentVars -and $cleanup.EnvironmentVars.Count -gt 0) {
            Write-Log -Level "INFO" -Message "Cleaning up environment variables..." -TraceLevel $TraceLevel
            foreach ($envVar in $cleanup.EnvironmentVars) {
                try {
                    [Environment]::SetEnvironmentVariable($envVar, $null, 'User')
                    [Environment]::SetEnvironmentVariable($envVar, $null, 'Machine')
                    Remove-Item "env:$envVar" -ErrorAction SilentlyContinue
                    Write-Log -Level "DEBUG" -Message "Removed environment variable: $envVar" -TraceLevel $TraceLevel
                } catch {
                    Write-Log -Level "DEBUG" -Message "Environment variable $envVar was not set" -TraceLevel $TraceLevel
                }
            }
        }

        return $true
    } catch {
        Write-Log -Level "ERROR" -Message "Directory and environment cleanup failed: $_" -TraceLevel $TraceLevel
        return $false
    }
}

<#
.SYNOPSIS
    Ensures Node.js is installed and properly configured.
.DESCRIPTION
    Downloads and installs Node.js if not present using platform-specific methods.
    Handles cross-platform installation and updates existing installations.
.PARAMETER TraceLevel
    Sets output detail level (ERROR, WARN, INFO, DEBUG)
.OUTPUTS
    [bool] True if Node.js is available and properly configured
#>
function Ensure-NodeJS {
    param(
        [ValidateSet("ERROR", "WARN", "INFO", "DEBUG")]
        [string]$TraceLevel = "ERROR",
        [switch]$UseLTS
    )

    $nodeConfig = $script:NodeResources.NodeJS

    # Check if Node.js is already installed
    if (Get-Command node -ErrorAction SilentlyContinue) {
        Write-Log -Level "INFO" -Message "Node.js already installed. Checking version..." -TraceLevel $TraceLevel

        try {
            $nodeOutput = & node --version 2>&1
            if ($LASTEXITCODE -eq 0) {
                $currentVersion = $nodeOutput -replace "^v", ""
                Write-Log -Level "INFO" -Message "Current Node.js version: $currentVersion" -TraceLevel $TraceLevel

                # Check if we need to update to a pinned version
                if ($nodeConfig.PinnedVersion -and $currentVersion -ne $nodeConfig.PinnedVersion) {
                    Write-Log -Level "INFO" -Message "Node.js version mismatch. Current: $currentVersion, Expected: $($nodeConfig.PinnedVersion)" -TraceLevel $TraceLevel
                    Write-Log -Level "INFO" -Message "Proceeding with Node.js upgrade..." -TraceLevel $TraceLevel
                    # Continue with installation to upgrade
                } else {
                    # Version matches or no pinned version, installation is complete
                    return $true
                }
            }
        } catch {
            Write-Log -Level "WARN" -Message "Node.js found but not working properly: $_" -TraceLevel $TraceLevel
            # Continue with installation to fix
        }
    }

    try {
        Write-Log -Level "DEBUG" -Message "Starting $($nodeConfig.DisplayName) installation..." -TraceLevel $TraceLevel

        # Platform-specific installation using InstallMethod from hashtable
        if ($nodeConfig.InstallMethod -eq "winget") {
            # Windows: Use winget - choose correct package based on version type
            $packageId = "OpenJS.NodeJS"

            if ($nodeConfig.PinnedVersion) {
                # Check if pinned version is LTS by comparing with actual LTS version
                $pinnedMajor = [int]($nodeConfig.PinnedVersion -split '\.')[0]
                $ltsMajor = if ($nodeConfig.LTSVersion) { [int]($nodeConfig.LTSVersion -split '\.')[0] } else { 22 }

                if ($pinnedMajor -eq $ltsMajor) {
                    # Use LTS package for current LTS versions
                    $packageId = "OpenJS.NodeJS.LTS"
                    Write-Log -Level "DEBUG" -Message "Using LTS package for LTS version $($nodeConfig.PinnedVersion)" -TraceLevel $TraceLevel
                } else {
                    # Use regular package for non-LTS versions (like 25.x)
                    Write-Log -Level "DEBUG" -Message "Using regular package for non-LTS version $($nodeConfig.PinnedVersion)" -TraceLevel $TraceLevel
                }

                # Try to install exact version first - use user scope to avoid elevation
                $installArgs = @("install", $packageId, "--version", $nodeConfig.PinnedVersion, "--scope", "user", "--silent", "--accept-package-agreements", "--accept-source-agreements")
                Write-Log -Level "DEBUG" -Message "Attempting to install pinned Node.js version: $($nodeConfig.PinnedVersion) using package: $packageId (user scope)" -TraceLevel $TraceLevel
            } else {
                # No pinned version - install latest - use user scope to avoid elevation
                $installArgs = @("install", $packageId, "--scope", "user", "--silent", "--accept-package-agreements", "--accept-source-agreements")
                Write-Log -Level "DEBUG" -Message "Installing latest Node.js using package: $packageId (user scope)" -TraceLevel $TraceLevel
            }

            $installResult = Invoke-BackgroundInstaller -FilePath "winget" -ArgumentList $installArgs -OperationName "$($nodeConfig.DisplayName) installation" -ProgressInterval $STATUS_UPDATE_INTERVAL -TraceLevel $TraceLevel

            # Check if the exact version wasn't available (for LTS package)
            if (-not $installResult -and $packageId -eq "OpenJS.NodeJS.LTS" -and $nodeConfig.PinnedVersion) {
                Write-Log -Level "WARN" -Message "Exact version $($nodeConfig.PinnedVersion) not available in $packageId package" -TraceLevel $TraceLevel
                Write-Log -Level "INFO" -Message "Attempting to install latest LTS version instead..." -TraceLevel $TraceLevel

                # Try installing without specific version (will get latest LTS) - use user scope to avoid elevation
                $fallbackArgs = @("install", $packageId, "--scope", "user", "--silent", "--accept-package-agreements", "--accept-source-agreements")
                $installResult = Invoke-BackgroundInstaller -FilePath "winget" -ArgumentList $fallbackArgs -OperationName "$($nodeConfig.DisplayName) LTS installation" -ProgressInterval $STATUS_UPDATE_INTERVAL -TraceLevel $TraceLevel

                if ($installResult) {
                    Write-Log -Level "INFO" -Message "Successfully installed latest LTS version from $packageId package" -TraceLevel $TraceLevel
                }
            }

            if ($installResult) {
                Write-Log -Level "INFO" -Message "$($nodeConfig.DisplayName) installation completed successfully" -TraceLevel $TraceLevel

                # Update environment after installation
                Update-EnvironmentVariables -TraceLevel $TraceLevel

                # On Windows, also explicitly add Node.js to PATH for current session
                $nodejsPath = "${env:ProgramFiles}\nodejs"
                if (Test-Path $nodejsPath) {
                    if ($env:Path -notlike "*$nodejsPath*") {
                        $env:Path = "$nodejsPath;$env:Path"
                        Write-Log -Level "DEBUG" -Message "Added Node.js to current session PATH: $nodejsPath" -TraceLevel $TraceLevel
                    }

                    # Also add npm global packages path
                    $npmPath = "${env:APPDATA}\npm"
                    if (-not (Test-Path $npmPath)) {
                        New-Item -ItemType Directory -Path $npmPath -Force | Out-Null
                        Write-Log -Level "DEBUG" -Message "Created npm global directory: $npmPath" -TraceLevel $TraceLevel
                    }
                    if ($env:Path -notlike "*$npmPath*") {
                        $env:Path = "$npmPath;$env:Path"
                        Write-Log -Level "DEBUG" -Message "Added npm to current session PATH: $npmPath" -TraceLevel $TraceLevel
                    }
                }

                # Verify npm is now available - check multiple locations on Windows
                Write-Log -Level "DEBUG" -Message "Verifying npm availability..." -TraceLevel $TraceLevel
                $npmFound = $false

                # Try different methods to find npm
                $npmLocations = @(
                    "${env:ProgramFiles}\nodejs\npm.cmd",
                    "${env:LOCALAPPDATA}\Microsoft\WindowsApps\npm.cmd",
                    "${env:APPDATA}\npm\npm.cmd"
                )

                foreach ($npmPath in $npmLocations) {
                    if (Test-Path $npmPath) {
                        Write-Log -Level "DEBUG" -Message "Found npm at: $npmPath" -TraceLevel $TraceLevel
                        try {
                            $npmVersion = & $npmPath --version 2>&1
                            if ($LASTEXITCODE -eq 0) {
                                Write-Log -Level "DEBUG" -Message "npm verified at $npmPath - version: $npmVersion" -TraceLevel $TraceLevel
                                $npmFound = $true
                                break
                            }
                        } catch {
                            Write-Log -Level "DEBUG" -Message "Failed to run npm from $npmPath`: $_" -TraceLevel $TraceLevel
                        }
                    }
                }

                # Also try via PATH
                if (-not $npmFound) {
                    try {
                        $npmTest = & cmd /c "where npm" 2>&1
                        if ($LASTEXITCODE -eq 0) {
                            Write-Log -Level "DEBUG" -Message "npm found in PATH at: $npmTest" -TraceLevel $TraceLevel
                            $npmFound = $true
                        }
                    } catch {
                        Write-Log -Level "DEBUG" -Message "Could not find npm via where command: $_" -TraceLevel $TraceLevel
                    }
                }

                if (-not $npmFound) {
                    Write-Log -Level "WARN" -Message "npm not found after Node.js installation - may need PATH refresh" -TraceLevel $TraceLevel
                }

                return $true
            } else {
                Write-Log -Level "ERROR" -Message "$($nodeConfig.DisplayName) installation failed" -TraceLevel $TraceLevel
                return $false
            }

        } elseif ($nodeConfig.InstallMethod -eq "homebrew") {
            # macOS: Use Homebrew with dynamic LTS detection
            if (-not (Get-Command brew -ErrorAction SilentlyContinue)) {
                Write-Log -Level "ERROR" -Message "Homebrew not found. Please install Homebrew first." -TraceLevel $TraceLevel
                return $false
            }

            # Determine installation package based on Update operation semantics
            if ($UseLTS) {
                # Update -LTS was called: install LTS version using dynamic detection
                $ltsVersion = Get-LatestLTSNodeVersion -ToolName "nodejs" -TraceLevel $TraceLevel
                $ltsMajorVersion = ($ltsVersion -split '\.')[0]
                $installArgs = @("install", "node@$ltsMajorVersion")
                Write-Log -Level "DEBUG" -Message "Installing LTS Node.js (v$ltsVersion) using node@$ltsMajorVersion" -TraceLevel $TraceLevel
            } elseif ($nodeConfig.PinnedVersion) {
                # Legacy support: pinned version specified in config
                $majorVersion = ($nodeConfig.PinnedVersion -split '\.')[0]

                # Check if this is the current/latest version (25.x+) - use 'node' package
                if ([int]$majorVersion -ge 25) {
                    $installArgs = @("install", "node")
                    Write-Log -Level "DEBUG" -Message "Installing pinned Node.js version: $($nodeConfig.PinnedVersion) using 'node' package (current/latest)" -TraceLevel $TraceLevel
                } else {
                    # For older versions, use versioned package node@major
                    $installArgs = @("install", "node@$majorVersion")
                    Write-Log -Level "DEBUG" -Message "Installing pinned Node.js version: $($nodeConfig.PinnedVersion) using node@$majorVersion" -TraceLevel $TraceLevel
                }
            } else {
                # Update (without LTS) was called: install latest version
                $installArgs = @("install", "node")
                Write-Log -Level "DEBUG" -Message "Installing latest Node.js using 'node' package" -TraceLevel $TraceLevel
            }
            $installResult = Invoke-BackgroundInstaller -FilePath "brew" -ArgumentList $installArgs -OperationName "$($nodeConfig.DisplayName) installation" -ProgressInterval $STATUS_UPDATE_INTERVAL -TraceLevel $TraceLevel

            if ($installResult) {
                # For versioned Node.js installations, we need to link them to make them accessible
                if ($UseLTS -or ($nodeConfig.PinnedVersion -and [int]($nodeConfig.PinnedVersion -split '\.')[0] -lt 25)) {
                    # Determine which version to link
                    $majorVersionToLink = if ($UseLTS) {
                        $ltsMajorVersion
                    } else {
                        ($nodeConfig.PinnedVersion -split '\.')[0]
                    }

                    Write-Log -Level "DEBUG" -Message "Linking node@$majorVersionToLink to make it accessible in PATH" -TraceLevel $TraceLevel

                    # First, unlink any existing node installation to avoid conflicts
                    & brew unlink node 2>&1 | Out-Null
                    Write-Log -Level "DEBUG" -Message "Unlinked existing node installation" -TraceLevel $TraceLevel

                    # Now link the specific version we want
                    $linkOutput = & brew link "node@$majorVersionToLink" --force --overwrite 2>&1
                    if ($LASTEXITCODE -eq 0) {
                        Write-Log -Level "DEBUG" -Message "Successfully linked node@$majorVersionToLink" -TraceLevel $TraceLevel
                    } else {
                        Write-Log -Level "WARN" -Message "Failed to link node@$majorVersionToLink, may not be accessible in PATH" -TraceLevel $TraceLevel
                        Write-Log -Level "DEBUG" -Message "Link output: $linkOutput" -TraceLevel $TraceLevel

                        # Try alternative approach: manually create symlinks
                        Write-Log -Level "DEBUG" -Message "Attempting manual symlink creation..." -TraceLevel $TraceLevel
                        try {
                            $brewPrefix = (& brew --prefix 2>&1).Trim()
                            $nodeVersionPath = "$brewPrefix/opt/node@$majorVersionToLink/bin"
                            $binPath = "$brewPrefix/bin"

                            if (Test-Path "$nodeVersionPath/node") {
                                # Remove existing symlinks if they exist
                                if (Test-Path "$binPath/node") {
                                    & rm "$binPath/node" 2>&1 | Out-Null
                                }
                                if (Test-Path "$binPath/npm") {
                                    & rm "$binPath/npm" 2>&1 | Out-Null
                                }
                                if (Test-Path "$binPath/npx") {
                                    & rm "$binPath/npx" 2>&1 | Out-Null
                                }

                                # Create new symlinks
                                & ln -sf "$nodeVersionPath/node" "$binPath/node" 2>&1 | Out-Null
                                & ln -sf "$nodeVersionPath/npm" "$binPath/npm" 2>&1 | Out-Null
                                & ln -sf "$nodeVersionPath/npx" "$binPath/npx" 2>&1 | Out-Null

                                Write-Log -Level "DEBUG" -Message "Created manual symlinks for node@$majorVersionToLink" -TraceLevel $TraceLevel
                            } else {
                                Write-Log -Level "WARN" -Message "node@$majorVersionToLink binaries not found at expected path: $nodeVersionPath" -TraceLevel $TraceLevel
                            }
                        } catch {
                            Write-Log -Level "WARN" -Message "Manual symlink creation failed: $_" -TraceLevel $TraceLevel
                        }
                    }
                } else {
                    Write-Log -Level "DEBUG" -Message "Using 'node' package - no linking required" -TraceLevel $TraceLevel
                }
                Write-Log -Level "INFO" -Message "$($nodeConfig.DisplayName) installation completed successfully" -TraceLevel $TraceLevel
                return $true
            } else {
                Write-Log -Level "ERROR" -Message "$($nodeConfig.DisplayName) installation failed" -TraceLevel $TraceLevel
                return $false
            }

        } elseif ($nodeConfig.InstallMethod -eq "script-download") {
            # Linux: Use NodeSource repository script with version-specific setup
            if ($nodeConfig.PinnedVersion) {
                # For specific versions, we need to use the major version to select the right repository
                $majorVersion = ($nodeConfig.PinnedVersion -split '\.')[0]
                $versionSpecificUrl = "https://deb.nodesource.com/setup_$majorVersion.x"
                Write-Log -Level "INFO" -Message "Setting up NodeSource repository for Node.js $majorVersion.x..." -TraceLevel $TraceLevel
                Write-Log -Level "DEBUG" -Message "Installing pinned Node.js version: $($nodeConfig.PinnedVersion)" -TraceLevel $TraceLevel

                $setupResult = Invoke-BackgroundInstaller -FilePath "bash" -ArgumentList @("-c", "curl -fsSL $versionSpecificUrl | sudo -E bash -") -OperationName "NodeSource repository setup" -ProgressInterval $STATUS_UPDATE_INTERVAL -TraceLevel $TraceLevel
            } else {
                Write-Log -Level "INFO" -Message "Setting up NodeSource repository..." -TraceLevel $TraceLevel
                $setupResult = Invoke-BackgroundInstaller -FilePath "bash" -ArgumentList @("-c", "curl -fsSL $($nodeConfig.DownloadUrl) | sudo -E bash -") -OperationName "NodeSource repository setup" -ProgressInterval $STATUS_UPDATE_INTERVAL -TraceLevel $TraceLevel
            }

            if ($setupResult) {
                Write-Log -Level "INFO" -Message "Installing Node.js via apt..." -TraceLevel $TraceLevel
                $installResult = Invoke-BackgroundInstaller -FilePath "sudo" -ArgumentList @("apt-get", "install", "-y", "nodejs") -OperationName "$($nodeConfig.DisplayName) installation" -ProgressInterval $STATUS_UPDATE_INTERVAL -TraceLevel $TraceLevel

                if ($installResult) {
                    Write-Log -Level "INFO" -Message "$($nodeConfig.DisplayName) installation completed successfully" -TraceLevel $TraceLevel
                    return $true
                } else {
                    Write-Log -Level "ERROR" -Message "$($nodeConfig.DisplayName) installation failed" -TraceLevel $TraceLevel
                    return $false
                }
            } else {
                Write-Log -Level "ERROR" -Message "NodeSource repository setup failed" -TraceLevel $TraceLevel
                return $false
            }
        }

        Write-Log -Level "ERROR" -Message "Unsupported installation method: $($nodeConfig.InstallMethod)" -TraceLevel $TraceLevel
        return $false

    } catch {
        Write-Log -Level "ERROR" -Message "$($nodeConfig.DisplayName) installation failed: $_" -TraceLevel $TraceLevel
        return $false
    }
}

<#
.SYNOPSIS
    Verifies Node.js installation and displays comprehensive status information.
.DESCRIPTION
    This function performs comprehensive verification of Node.js installation by:
    1. Checking if Node.js and npm are installed and getting their versions
    2. Checking global npm packages installation and versions
    3. Comparing against pinned versions from configuration
    4. Fetching latest available versions from external sources
    5. Displaying results in a formatted table showing Component, Installed, Pinned, and Latest columns

    This is the "Doctor" function that provides visibility into the current Node.js state.
.PARAMETER Force
    Forces operations without additional safety checks
.PARAMETER TraceLevel
    Sets output detail level (ERROR, WARN, INFO, DEBUG)
.PARAMETER Silent
    Suppresses console output during verification (for internal calls)
.OUTPUTS
    [Hashtable] Node.js resource hashtable with version information populated
.EXAMPLE
    $nodeTools = Doctor-Resource -TraceLevel "INFO"
    Verifies Node.js installation and displays status table with standard logging
#>
function Doctor-Resource {
    param(
        [switch]$Force,
        [ValidateSet("ERROR", "WARN", "INFO", "DEBUG")]
        [string]$TraceLevel = "ERROR",
        [switch]$Silent
    )

    Write-Log -Level "DEBUG" -Message "Doctor-Resource started for Node.js" -TraceLevel $TraceLevel -Silent:$Silent

    try {
        # Refresh environment variables to pick up newly installed tools
        Update-EnvironmentVariables -TraceLevel $TraceLevel

        # Update current session PATH to include newly installed Node.js
        Update-CurrentSessionPath -TraceLevel $TraceLevel

        # Work directly with the Node.js resource configuration hashtable
        $nodeTools = $script:NodeResources

        # Get latest versions for Node.js components using component-specific functions
        $latestNodeVersion = Get-LatestNodeVersion -TraceLevel $TraceLevel
        $ltsNodeVersion = Get-LatestLTSNodeVersion -ToolName "nodejs" -TraceLevel $TraceLevel
        $latestNpmVersion = Get-LatestNpmVersion -TraceLevel $TraceLevel

        # Fill in Node.js version information
        $nodejs = $nodeTools.NodeJS

        # Get installed Node.js version - ensure we get fresh results after Clean operations
        try {
            # First check if the command exists at all
            $nodeCommand = Get-Command $nodejs.VerifyCommand -ErrorAction SilentlyContinue
            if (-not $nodeCommand) {
                Write-Log -Level "DEBUG" -Message "Node.js command not found in PATH" -TraceLevel $TraceLevel
                $nodejs.InstalledVersion = $null
            } else {
                # Command exists, try to get version
                $output = & $nodejs.VerifyCommand @($nodejs.VerifyArgs) 2>&1
                if ($LASTEXITCODE -eq 0) {
                    # Apply VerifyOutputFilter to extract version (data-driven approach)
                    if ($nodejs.VerifyOutputFilter) {
                        $nodejs.InstalledVersion = ($output | Select-Object -First 1) -replace $nodejs.VerifyOutputFilter, '$1'
                    }
                    Write-Log -Level "DEBUG" -Message "Node.js found with version: $($nodejs.InstalledVersion)" -TraceLevel $TraceLevel
                } else {
                    Write-Log -Level "DEBUG" -Message "Node.js command failed with exit code: $LASTEXITCODE" -TraceLevel $TraceLevel
                    $nodejs.InstalledVersion = $null
                }
            }
        } catch {
            Write-Log -Level "DEBUG" -Message "Node.js not available: $_" -TraceLevel $TraceLevel
            $nodejs.InstalledVersion = $null
        }

        # Get pinned version from configuration
        $nodejs.PinnedVersion = if ($VERSIONS_CONFIG -and $VERSIONS_CONFIG.tools.nodejs.runtime.version) {
            $VERSIONS_CONFIG.tools.nodejs.runtime.version
        } else { $null }

        # Set latest and LTS versions
        $nodejs.LatestVersion = $latestNodeVersion
        $nodejs.LTSVersion = $ltsNodeVersion

        # Fill in npm version information
        $npm = $nodeTools.NPM

        # Get installed npm version
        try {
            $output = & $npm.VerifyCommand @($npm.VerifyArgs) 2>&1
            if ($LASTEXITCODE -eq 0) {
                # Apply VerifyOutputFilter to extract version
                if ($npm.VerifyOutputFilter) {
                    $npm.InstalledVersion = ($output | Select-Object -First 1) -replace $npm.VerifyOutputFilter, '$1'
                }
            }
        } catch {
            Write-Log -Level "DEBUG" -Message "npm not available: $_" -TraceLevel $TraceLevel
            $npm.InstalledVersion = $null
        }

        # Get pinned version from configuration
        $npm.PinnedVersion = if ($VERSIONS_CONFIG -and $VERSIONS_CONFIG.tools.nodejs.npm.version) {
            $VERSIONS_CONFIG.tools.nodejs.npm.version
        } else { $null }

        # Set latest and LTS versions (npm doesn't have LTS, so use same as latest)
        $npm.LatestVersion = $latestNpmVersion
        $npm.LTSVersion = $latestNpmVersion

        # Process global npm packages
        foreach ($package in $nodeTools.GlobalPackages) {
            Write-Log -Level "STATUS" -Message "Checking $($package.DisplayName)..." -TraceLevel $TraceLevel -Silent:$Silent

            # Get installed version
            try {
                if ($package.ExecutableName) {
                    # For executable packages, check if the executable exists and get version
                    if (Get-Command $package.VerifyCommand -ErrorAction SilentlyContinue) {
                        $versionOutput = & $package.VerifyCommand @($package.VerifyArgs) 2>&1
                        if ($LASTEXITCODE -eq 0) {
                            if ($package.VerifyOutputFilter) {
                                $package.InstalledVersion = ($versionOutput | Select-Object -First 1) -replace $package.VerifyOutputFilter, '$1'
                            } else {
                                $package.InstalledVersion = ($versionOutput | Select-Object -First 1) -replace ".*?([0-9]+\.[0-9]+\.[0-9]+).*", '$1'
                            }
                        }
                    }
                }
            } catch {
                Write-Log -Level "DEBUG" -Message "Error checking $($package.Name): $_" -TraceLevel $TraceLevel
                $package.InstalledVersion = $null
            }

            # Get pinned version from configuration
            $package.PinnedVersion = if ($VERSIONS_CONFIG -and $VERSIONS_CONFIG.tools.nodejs.globalPackages.($package.Name).version) {
                $VERSIONS_CONFIG.tools.nodejs.globalPackages.($package.Name).version
            } else { $null }

            # Get latest and LTS versions using component-specific function for npm packages
            $package.LatestVersion = Get-LatestNpmPackageVersion -PackageName $package.PackageName -TraceLevel $TraceLevel
            $package.LTSVersion = Get-LatestNpmPackageVersion -PackageName $package.PackageName -TraceLevel $TraceLevel

            Complete-StatusMessage
        }

        # Display results as a formatted table
        if (-not $Silent) {
            Display-NodeToolStatus -Tools $nodeTools -TraceLevel $TraceLevel
        }

        Write-Log -Level "DEBUG" -Message "Doctor-Resource completed for Node.js" -TraceLevel $TraceLevel -Silent:$Silent

        # Return standardized result hashtable
        $result = @{
            Name = "node"
            Status = "Error"
            Version = $null
            Message = ""
            NodeVersion = $nodeTools.NodeJS.InstalledVersion
            NpmVersion = $nodeTools.NPM.InstalledVersion
            NodeTools = $nodeTools
        }

        if ($nodeTools.NodeJS.InstalledVersion) {
            $result.Status = "Installed"
            $result.Version = $nodeTools.NodeJS.InstalledVersion
            $result.Message = "Node.js $($nodeTools.NodeJS.InstalledVersion) installed"
        } else {
            $result.Status = "NotInstalled"
            $result.Message = "Node.js is not installed"
        }

        return $result
    } catch {
        Write-Log -Level "ERROR" -Message "Node.js verification failed: $_" -TraceLevel $TraceLevel -Silent:$Silent
        return @{
            Name = "node"
            Status = "Error"
            Version = $null
            Message = "Node.js verification failed: $_"
        }
    }
}

<#
.SYNOPSIS
    Displays Node.js tool status in a formatted table.
.DESCRIPTION
    Takes the Node.js resource configuration and displays component versions
    in a table format showing Component, Installed, Pinned, and Latest columns.
    This provides a clear visual representation of the current Node.js state vs desired state.
.PARAMETER Tools
    Node.js resource hashtable with version information
.PARAMETER TraceLevel
    Sets output detail level (ERROR, WARN, INFO, DEBUG)
.EXAMPLE
    Display-NodeToolStatus -Tools $nodeTools -TraceLevel "INFO"
#>
function Display-NodeToolStatus {
    param(
        [Parameter(Mandatory = $true)]
        [hashtable]$Tools,
        [ValidateSet("ERROR", "WARN", "INFO", "DEBUG")]
        [string]$TraceLevel = "ERROR"
    )

    # Create component status array for display
    $componentStatuses = @()

    # Add Node.js to status table (always show, even if not installed)
    $componentStatuses += [PSCustomObject]@{
        Component = "nodejs"
        Installed = if ($Tools.NodeJS.InstalledVersion) { $Tools.NodeJS.InstalledVersion } else { "(not installed)" }
        Pinned = if ($Tools.NodeJS.PinnedVersion) { $Tools.NodeJS.PinnedVersion } else { "None" }
        LTS = $Tools.NodeJS.LTSVersion
        Latest = $Tools.NodeJS.LatestVersion
    }

    # Add npm to status table (always show, even if not installed)
    $componentStatuses += [PSCustomObject]@{
        Component = "npm"
        Installed = if ($Tools.NPM.InstalledVersion) { $Tools.NPM.InstalledVersion } else { "(not installed)" }
        Pinned = if ($Tools.NPM.PinnedVersion) { $Tools.NPM.PinnedVersion } else { "None" }
        LTS = $Tools.NPM.LTSVersion
        Latest = $Tools.NPM.LatestVersion
    }

    # Add global packages to status table
    foreach ($package in $Tools.GlobalPackages) {
        $componentStatuses += [PSCustomObject]@{
            Component = $package.Name
            Installed = if ($package.InstalledVersion) { $package.InstalledVersion } else { "(not installed)" }
            Pinned = if ($package.PinnedVersion) { $package.PinnedVersion } else { "None" }
            LTS = $package.LTSVersion
            Latest = $package.LatestVersion
        }
    }

    # Display table
    Write-Log -Level "INFO" -Message "`n" -TraceLevel $TraceLevel -NoLabel

    # Calculate column widths for proper alignment
    $maxComponent = ($componentStatuses | ForEach-Object { $_.Component.Length } | Measure-Object -Maximum).Maximum
    $maxInstalled = ($componentStatuses | ForEach-Object { $_.Installed.Length } | Measure-Object -Maximum).Maximum
    $maxPinned = ($componentStatuses | ForEach-Object { $_.Pinned.Length } | Measure-Object -Maximum).Maximum
    $maxLTS = ($componentStatuses | ForEach-Object { $_.LTS.Length } | Measure-Object -Maximum).Maximum

    # Ensure minimum widths for headers
    $componentWidth = [Math]::Max($maxComponent, "Component".Length)
    $installedWidth = [Math]::Max($maxInstalled, "Installed".Length)
    $pinnedWidth = [Math]::Max($maxPinned, "Pinned".Length)
    $ltsWidth = [Math]::Max($maxLTS, "LTS".Length)

    # Create header with consistent spacing
    $spacing = "     "  # 5 spaces
    $header = "Component".PadRight($componentWidth) + $spacing + "Installed".PadRight($installedWidth) + $spacing + "Pinned".PadRight($pinnedWidth) + $spacing + "LTS".PadRight($ltsWidth) + $spacing + "Latest"
    $separator = "-" * "Component".Length + ("-" * ($componentWidth - "Component".Length)) + $spacing + "-" * "Installed".Length + ("-" * ($installedWidth - "Installed".Length)) + $spacing + "-" * "Pinned".Length + ("-" * ($pinnedWidth - "Pinned".Length)) + $spacing + "-" * "LTS".Length + ("-" * ($ltsWidth - "LTS".Length)) + $spacing + "-" * "Latest".Length

    # Display header
    Write-Log -Level "INFO" -Message $header -TraceLevel $TraceLevel -NoLabel
    Write-Log -Level "INFO" -Message $separator -TraceLevel $TraceLevel -NoLabel

    # Display each row with appropriate coloring
    foreach ($status in $componentStatuses) {
        $row = $status.Component.PadRight($componentWidth) + $spacing + $status.Installed.PadRight($installedWidth) + $spacing + $status.Pinned.PadRight($pinnedWidth) + $spacing + $status.LTS.PadRight($ltsWidth) + $spacing + $status.Latest

        # Determine log level based on installation and version status
        if ($status.Installed.Contains("(not installed)")) {
            # Component not installed = ERROR
            Write-Log -Level "ERROR" -Message $row -TraceLevel $TraceLevel -NoLabel
        } elseif ($status.Pinned -ne "None" -and $status.Installed -ne $status.Pinned -and $status.Installed -ne "installed") {
            # Component installed but wrong version = WARN
            Write-Log -Level "WARN" -Message $row -TraceLevel $TraceLevel -NoLabel
        } else {
            # Component installed and correct version = INFO
            Write-Log -Level "INFO" -Message $row -TraceLevel $TraceLevel -NoLabel
        }
    }
}

<#
.SYNOPSIS
    Installs and configures Node.js runtime and global packages.
.DESCRIPTION
    This function performs Node.js installation by:
    1. Getting current installation status from Doctor-Resource
    2. Installing Node.js runtime if needed
    3. Installing global npm packages that aren't present
    4. Handling version mismatches and updates

    The function demonstrates the pattern for orchestrating installation of multiple
    Node.js components with proper error handling and user confirmation.
.PARAMETER Yes
    Automatically confirms operations without prompting
.PARAMETER Force
    Forces operations without additional safety checks
.PARAMETER TraceLevel
    Sets output detail level (ERROR, WARN, INFO, DEBUG)
.OUTPUTS
    [bool] True if installation succeeds, False otherwise
.EXAMPLE
    Install-Resource -Yes -TraceLevel "DEBUG"
    Installs Node.js with automatic confirmation and detailed logging
#>
function Install-Resource {
    param(
        [switch]$Yes,
        [switch]$Force,
        [ValidateSet("ERROR", "WARN", "INFO", "DEBUG")]
        [string]$TraceLevel = "ERROR"
    )

    Write-Log -Level "DEBUG" -Message "Starting Node.js installation..." -TraceLevel $TraceLevel

    # Stop any running Node.js processes that might interfere
    Stop-NodeProcesses -TraceLevel $TraceLevel

    # Get user confirmation unless Yes is specified
    if (-not $Yes) {
        $response = Get-UserConfirmation -Question "Continue with Node.js installation?" -TraceLevel $TraceLevel
        if ($response -ne 'Yes') {
            Write-Log -Level "INFO" -Message "Installation cancelled by user" -TraceLevel $TraceLevel
            return $false
        }
    }

    # Get current installation status
    $nodeTools = Doctor-Resource -Silent -TraceLevel $TraceLevel

    if (-not $nodeTools) {
        Write-Log -Level "ERROR" -Message "Failed to get current Node.js installation status" -TraceLevel $TraceLevel
        return $false
    }

    # Install main tool using correct semantics:
    # 1. If not installed, install it
    # 2. If pinned version is HIGHER than installed, upgrade to pinned version
    # 3. If pinned version is LOWER than installed, ERROR and require clean first (downgrade not supported)
    # 4. If not pinned, upgrade to latest version
    $mainTool = $nodeTools.NodeJS
    $needsInstall = $false
    $needsUpdate = $false

    if (-not $mainTool.InstalledVersion) {
        # Not installed at all
        $needsInstall = $true
        Write-Log -Level "INFO" -Message "Installing $($mainTool.DisplayName)..." -TraceLevel $TraceLevel
    } elseif ($mainTool.PinnedVersion -and $mainTool.InstalledVersion -ne $mainTool.PinnedVersion) {
        # Check if this is an upgrade or downgrade
        try {
            # Parse version numbers for comparison (basic semantic version comparison)
            $installedParts = $mainTool.InstalledVersion -split '\.'
            $pinnedParts = $mainTool.PinnedVersion -split '\.'

            $installedMajor = [int]$installedParts[0]
            $installedMinor = [int]$installedParts[1]
            $installedPatch = [int]$installedParts[2]

            $pinnedMajor = [int]$pinnedParts[0]
            $pinnedMinor = [int]$pinnedParts[1]
            $pinnedPatch = [int]$pinnedParts[2]

            $isDowngrade = ($pinnedMajor -lt $installedMajor) -or
            ($pinnedMajor -eq $installedMajor -and $pinnedMinor -lt $installedMinor) -or
            ($pinnedMajor -eq $installedMajor -and $pinnedMinor -eq $installedMinor -and $pinnedPatch -lt $installedPatch)

            if ($isDowngrade) {
                # Downgrade not supported - ERROR but continue with npm/package updates
                Write-Log -Level "ERROR" -Message "Cannot downgrade Node.js from $($mainTool.InstalledVersion) to $($mainTool.PinnedVersion)" -TraceLevel $TraceLevel
                Write-Log -Level "ERROR" -Message "To downgrade Node.js, run: .\node.ps1 -Clean -Install -Yes" -TraceLevel $TraceLevel
                Write-Log -Level "INFO" -Message "Continuing with npm and package updates..." -TraceLevel $TraceLevel
                # Don't return false - continue with npm updates
            } else {
                # Upgrade to pinned version
                $needsUpdate = $true
                Write-Log -Level "INFO" -Message "Upgrading $($mainTool.DisplayName) from $($mainTool.InstalledVersion) to pinned version $($mainTool.PinnedVersion)..." -TraceLevel $TraceLevel
            }
        } catch {
            Write-Log -Level "WARN" -Message "Version comparison failed, skipping Node.js update: $_" -TraceLevel $TraceLevel
        }
    } elseif (-not $mainTool.PinnedVersion -and $mainTool.InstalledVersion -ne $mainTool.LatestVersion) {
        # Not pinned, but not latest - should upgrade
        $needsUpdate = $true
        Write-Log -Level "INFO" -Message "Upgrading $($mainTool.DisplayName) from $($mainTool.InstalledVersion) to latest version $($mainTool.LatestVersion)..." -TraceLevel $TraceLevel
    }

    if ($needsInstall -or $needsUpdate) {
        # Pass LTS flag if this is an Update operation with LTS specified
        $success = Ensure-NodeJS -TraceLevel $TraceLevel -UseLTS:$script:LTS
        if (-not $success) {
            Write-Log -Level "ERROR" -Message "Failed to install/update $($mainTool.DisplayName)" -TraceLevel $TraceLevel
            return $false
        }

        # Refresh environment after Node.js installation/update
        Update-EnvironmentVariables -TraceLevel $TraceLevel
    }

    # Check and update npm independently of Node.js installation status
    Write-Log -Level "DEBUG" -Message "Checking npm version and updating if needed..." -TraceLevel $TraceLevel

    # Use the centralized npm path discovery function
    $npmPath = Get-NpmExecutablePath -TraceLevel $TraceLevel
    try {
        $npmTest = & $npmPath --version 2>&1
        if ($LASTEXITCODE -eq 0) {
            Write-Log -Level "DEBUG" -Message "npm verified successfully at: $npmPath - version: $npmTest" -TraceLevel $TraceLevel

            # Use the npm version already detected by Doctor-Resource (don't overwrite it)
            $npm = $nodeTools.NPM
            $currentNpmVersion = $npm.InstalledVersion

            # Check if npm needs updating
            $needsNpmUpdate = $false

            if ($npm.PinnedVersion -and $npm.InstalledVersion -ne $npm.PinnedVersion) {
                # npm pinned but wrong version
                $needsNpmUpdate = $true
                Write-Log -Level "INFO" -Message "Updating npm from $($npm.InstalledVersion) to pinned version $($npm.PinnedVersion)..." -TraceLevel $TraceLevel
            } elseif (-not $npm.PinnedVersion -and $npm.InstalledVersion -ne $npm.LatestVersion) {
                # npm not pinned but not latest - should upgrade
                $needsNpmUpdate = $true
                Write-Log -Level "INFO" -Message "Updating npm from $($npm.InstalledVersion) to latest version $($npm.LatestVersion)..." -TraceLevel $TraceLevel
            }

            # Perform npm update if needed
            if ($needsNpmUpdate) {
                try {
                    $targetNpmVersion = if ($npm.PinnedVersion) { $npm.PinnedVersion } else { $npm.LatestVersion }

                    Write-Log -Level "DEBUG" -Message "Starting npm update using: $npmPath" -TraceLevel $TraceLevel

                    $npmUpdateResult = Invoke-BackgroundInstaller -FilePath $npmPath -ArgumentList @("install", "-g", "npm@$targetNpmVersion") -OperationName "npm update to $targetNpmVersion" -ProgressInterval $STATUS_UPDATE_INTERVAL -TraceLevel $TraceLevel

                    if ($npmUpdateResult) {
                        Write-Log -Level "INFO" -Message "Successfully updated npm to $targetNpmVersion" -TraceLevel $TraceLevel
                    } else {
                        Write-Log -Level "WARN" -Message "Failed to update npm to $targetNpmVersion" -TraceLevel $TraceLevel
                    }
                } catch {
                    Write-Log -Level "WARN" -Message "npm update failed: $_" -TraceLevel $TraceLevel
                }
            } else {
                Write-Log -Level "DEBUG" -Message "npm version $currentNpmVersion is already correct, no update needed" -TraceLevel $TraceLevel
            }
        } else {
            Write-Log -Level "WARN" -Message "npm verification failed with exit code: $LASTEXITCODE" -TraceLevel $TraceLevel
        }
    } catch {
        Write-Log -Level "WARN" -Message "Failed to verify npm at $npmPath`: $_" -TraceLevel $TraceLevel
    }


    # Install global packages that aren't present
    foreach ($package in $nodeTools.GlobalPackages) {
        if (-not $package.InstalledVersion) {
            Write-Log -Level "INFO" -Message "Installing $($package.DisplayName)..." -TraceLevel $TraceLevel

            # Generic installation using InstallMethod from hashtable
            if ($package.InstallMethod -eq "npm") {
                $installArgs = @("install", "-g", $package.PackageName)
                if ($package.PinnedVersion) {
                    $installArgs = @("install", "-g", "$($package.PackageName)@$($package.PinnedVersion)")
                }

                # Add --silent flag unless we're in DEBUG mode
                if ($TraceLevel -ne "DEBUG") {
                    $installArgs += @("--silent")
                }

                $installResult = Invoke-BackgroundInstaller -FilePath "npm" -ArgumentList $installArgs -OperationName "$($package.DisplayName) installation" -ProgressInterval $STATUS_UPDATE_INTERVAL -TraceLevel $TraceLevel

                if (-not $installResult) {
                    Write-Log -Level "WARN" -Message "Failed to install $($package.DisplayName)" -TraceLevel $TraceLevel
                }
            } else {
                Write-Log -Level "INFO" -Message "Unsupported package installation method: $($package.InstallMethod) for $($package.DisplayName)" -TraceLevel $TraceLevel
            }
        } elseif ($package.PinnedVersion -and $package.InstalledVersion -ne $package.PinnedVersion) {
            Write-Log -Level "WARN" -Message "$($package.DisplayName) version mismatch: installed=$($package.InstalledVersion), expected=$($package.PinnedVersion)" -TraceLevel $TraceLevel
        }
    }

    Write-Log -Level "INFO" -Message "Node.js installation completed successfully" -TraceLevel $TraceLevel
    return $true
}

<#
.SYNOPSIS
    Removes existing Node.js runtime and cleans environment.
.DESCRIPTION
    This function orchestrates the complete removal of Node.js by calling
    smaller, focused functions to handle different aspects of cleanup:
    1. Stopping Node.js processes
    2. Removing global npm packages
    3. Detecting and removing different types of Node.js installations
    4. Cleaning up directories and environment variables

    The function is now modular and easier to maintain, with each step
    handled by a dedicated function.
.PARAMETER Yes
    Automatically confirms operations without prompting
.PARAMETER Force
    Forces operations without additional safety checks
.PARAMETER TraceLevel
    Sets output detail level (ERROR, WARN, INFO, DEBUG)
.OUTPUTS
    [bool] True if removal succeeds, False otherwise
.EXAMPLE
    Clean-Resources -Force -TraceLevel "INFO"
    Forces Node.js removal with standard logging
#>
function Clean-Resources {
    param(
        [switch]$Yes,
        [switch]$Force,
        [ValidateSet("ERROR", "WARN", "INFO", "DEBUG")]
        [string]$TraceLevel = "ERROR"
    )

    Write-Log -Level "DEBUG" -Message "Starting Node.js removal..." -TraceLevel $TraceLevel

    # Get user confirmation unless Yes is specified
    if (-not $Yes) {
        $response = Get-UserConfirmation -Question "Continue with Node.js removal? This will remove all Node.js tools and data." -TraceLevel $TraceLevel
        if ($response -ne 'Yes') {
            Write-Log -Level "INFO" -Message "Removal cancelled by user" -TraceLevel $TraceLevel
            return $false
        }
    }

    try {
        # Step 1: Stop any running Node.js processes
        Stop-NodeProcesses -TraceLevel $TraceLevel

        # Step 2: Remove global npm packages first
        $packageRemovalResult = Remove-GlobalNpmPackages -TraceLevel $TraceLevel
        if (-not $packageRemovalResult) {
            Write-Log -Level "WARN" -Message "Global package removal had issues, continuing with Node.js removal" -TraceLevel $TraceLevel
        }

        # Step 3: Detect all Node.js installations and remove them
        Write-Log -Level "INFO" -Message "Detecting Node.js installations..." -TraceLevel $TraceLevel
        $installations = Get-NodeInstallations -TraceLevel $TraceLevel

        $removalSuccess = $true

        # Remove NVM installation if detected
        if ($installations.NVM) {
            Write-Log -Level "INFO" -Message "Detected NVM-managed Node.js installation" -TraceLevel $TraceLevel
            $nvmResult = Remove-NvmNodeInstallation -TraceLevel $TraceLevel
            if (-not $nvmResult) {
                $removalSuccess = $false
            }
        }

        # Remove winget installation if on Windows
        if ($IsWindows) {
            $wingetResult = Clean-WingetNodeInstallation -TraceLevel $TraceLevel
            if (-not $wingetResult) {
                $removalSuccess = $false
            }
        }

        # Remove Homebrew installation if detected
        if ($installations.Homebrew) {
            $homebrewResult = Clean-HomebrewNodeIntallation -TraceLevel $TraceLevel
            if (-not $homebrewResult) {
                $removalSuccess = $false
            }
        }

        # Handle system installation with user confirmation
        $systemNodeDetected = Detect-SystemNode -TraceLevel $TraceLevel
        if ($systemNodeDetected) {
            # Ask for confirmation unless -Force -Yes is passed
            if (-not ($Force -and $Yes)) {
                $systemResponse = Get-UserConfirmation -Question "System-wide Node.js installation detected. Remove system Node.js?" -TraceLevel $TraceLevel
                if ($systemResponse -eq 'Yes') {
                    $systemResult = Clean-SystemNode -TraceLevel $TraceLevel
                    if (-not $systemResult) {
                        $removalSuccess = $false
                    }
                } else {
                    Write-Log -Level "INFO" -Message "System Node.js removal skipped by user" -TraceLevel $TraceLevel
                }
            } else {
                # Force mode - remove without asking
                $systemResult = Clean-SystemNode -TraceLevel $TraceLevel
                if (-not $systemResult) {
                    $removalSuccess = $false
                }
            }
        }

        # If no specific installation type was detected but Node.js paths exist, try generic cleanup
        if (-not ($installations.NVM -or $installations.Homebrew -or $systemNodeDetected) -and $installations.NodePaths.Count -gt 0) {
            Write-Log -Level "INFO" -Message "Detected unrecognized Node.js installation, trying generic cleanup..." -TraceLevel $TraceLevel
            $genericResult = Clean-SystemNode -TraceLevel $TraceLevel
            if (-not $genericResult) {
                $removalSuccess = $false
            }
        }

        # Step 4: Clean up directories and environment variables
        $cleanupResult = Clean-NodeDirectoriesAndEnvironment -TraceLevel $TraceLevel
        if (-not $cleanupResult) {
            $removalSuccess = $false
        }

        if ($removalSuccess) {
            Write-Log -Level "INFO" -Message "Node.js removal completed successfully" -TraceLevel $TraceLevel
        } else {
            Write-Log -Level "WARN" -Message "Node.js removal completed with some issues" -TraceLevel $TraceLevel
        }

        return $removalSuccess

    } catch {
        Write-Log -Level "ERROR" -Message "Node.js removal failed: $_" -TraceLevel $TraceLevel
        return $false
    }
}

<#
.SYNOPSIS
    Updates existing Node.js runtime and global packages to latest versions.
.DESCRIPTION
    This function performs Node.js updates by:
    1. Getting current status and latest versions from Doctor-Resource
    2. Updating the version configuration file with latest versions
    3. Calling Install-Resource to perform the actual updates

    This demonstrates the pattern for version management and controlled updates
    used across resource management scripts.
.PARAMETER Yes
    Automatically confirms operations without prompting
.PARAMETER Force
    Forces operations without additional safety checks
.PARAMETER TraceLevel
    Sets output detail level (ERROR, WARN, INFO, DEBUG)
.OUTPUTS
    [bool] True if update succeeds, False otherwise
.EXAMPLE
    Update-Resource -Yes -TraceLevel "INFO"
    Updates Node.js to latest versions with standard logging
#>
function Update-Resource {
    param(
        [switch]$Yes,
        [switch]$Force,
        [ValidateSet("ERROR", "WARN", "INFO", "DEBUG")]
        [string]$TraceLevel = "ERROR"
    )

    # Access the LTS parameter from the parent scope (script level)
    $UseLTS = $script:LTS

    # Get current status first to access resource information (silent to avoid displaying table)
    $resourceTools = Doctor-Resource -Silent -TraceLevel $TraceLevel

    if (-not $resourceTools) {
        Write-Log -Level "ERROR" -Message "Failed to get current resource status" -TraceLevel $TraceLevel
        return $false
    }

    # Use resource name from hashtable for data-driven messaging
    $resourceName = $resourceTools.NodeJS.DisplayName -replace " JavaScript runtime", "" -replace " runtime", ""

    Write-Log -Level "DEBUG" -Message "Starting $resourceName update..." -TraceLevel $TraceLevel

    # Determine target version based on -LTS parameter
    $targetVersionType = if ($LTS) { "LTS" } else { "latest" }
    $targetNodeVersion = if ($LTS) { $resourceTools.NodeJS.LTSVersion } else { $resourceTools.NodeJS.LatestVersion }

    # Check for downgrade scenario
    if ($resourceTools.NodeJS.InstalledVersion) {
        try {
            # Parse version numbers for comparison (basic semantic version comparison)
            $installedParts = $resourceTools.NodeJS.InstalledVersion -split '\.'
            $targetParts = $targetNodeVersion -split '\.'

            $installedMajor = [int]$installedParts[0]
            $installedMinor = [int]$installedParts[1]
            $installedPatch = [int]$installedParts[2]

            $targetMajor = [int]$targetParts[0]
            $targetMinor = [int]$targetParts[1]
            $targetPatch = [int]$targetParts[2]

            $isDowngrade = ($targetMajor -lt $installedMajor) -or
            ($targetMajor -eq $installedMajor -and $targetMinor -lt $installedMinor) -or
            ($targetMajor -eq $installedMajor -and $targetMinor -eq $installedMinor -and $targetPatch -lt $installedPatch)

            if ($isDowngrade) {
                Write-Log -Level "ERROR" -Message "Cannot downgrade from $($resourceTools.NodeJS.InstalledVersion) to $targetNodeVersion" -TraceLevel $TraceLevel
                Write-Log -Level "ERROR" -Message "To downgrade Node.js, run: .\node.ps1 -Clean -Update $($LTS ? '-LTS' : '') -Yes" -TraceLevel $TraceLevel
                return $false
            }
        } catch {
            Write-Log -Level "DEBUG" -Message "Version comparison failed, proceeding with update: $_" -TraceLevel $TraceLevel
        }
    }

    # Skip confirmation if -Yes is specified (passed from main script parameters)
    if (-not $Yes) {
        $response = Get-UserConfirmation -Question "Continue with $resourceName update to $targetVersionType versions?" -TraceLevel $TraceLevel
        if ($response -ne 'Yes') {
            Write-Log -Level "INFO" -Message "Update cancelled by user" -TraceLevel $TraceLevel
            return $false
        }
    }

    # Update the pinned-versions.json file with target versions using generic data-driven approach
    $versionsPath = Join-Path (Split-Path $scriptPath -Parent) "pinned-versions.json"
    if ($VERSIONS_CONFIG) {
        # Ensure config structure exists using proper PowerShell object creation
        if (-not $VERSIONS_CONFIG.tools) {
            $VERSIONS_CONFIG | Add-Member -NotePropertyName "tools" -NotePropertyValue @{} -Force
        }
        if (-not $VERSIONS_CONFIG.tools.PSObject.Properties['nodejs']) {
            $VERSIONS_CONFIG.tools | Add-Member -NotePropertyName "nodejs" -NotePropertyValue @{} -Force
        }

        # Generic approach: Loop through main components using hashtable structure
        # Update Node.js runtime - use LTSVersion if -LTS specified, otherwise LatestVersion
        $targetNodeVersion = if ($UseLTS) { $resourceTools.NodeJS.LTSVersion } else { $resourceTools.NodeJS.LatestVersion }
        if (-not $VERSIONS_CONFIG.tools.nodejs.runtime) {
            $VERSIONS_CONFIG.tools.nodejs.runtime = @{}
        }
        $VERSIONS_CONFIG.tools.nodejs.runtime.version = $targetNodeVersion

        # Update npm - always use LatestVersion (npm shows same value in both LTS and Latest columns)
        if (-not $VERSIONS_CONFIG.tools.nodejs.npm) {
            $VERSIONS_CONFIG.tools.nodejs.npm = @{}
        }
        $VERSIONS_CONFIG.tools.nodejs.npm.version = $resourceTools.NPM.LatestVersion

        # Update global packages - loop through array using hashtable properties
        if (-not $VERSIONS_CONFIG.tools.nodejs.globalPackages) {
            $VERSIONS_CONFIG.tools.nodejs.globalPackages = @{}
        }
        foreach ($package in $resourceTools.GlobalPackages) {
            if (-not $VERSIONS_CONFIG.tools.nodejs.globalPackages.($package.Name)) {
                $VERSIONS_CONFIG.tools.nodejs.globalPackages.($package.Name) = @{}
            }
            # For packages, always use LatestVersion (they don't have meaningful LTS)
            $VERSIONS_CONFIG.tools.nodejs.globalPackages.($package.Name).version = $package.LatestVersion
        }

        # Save updated configuration
        $VERSIONS_CONFIG | ConvertTo-Json -Depth 10 | Set-Content $versionsPath -Encoding UTF8
        Write-Log -Level "INFO" -Message "Updated version configuration to $targetVersionType versions" -TraceLevel $TraceLevel
    } else {
        Write-Log -Level "WARN" -Message "No version configuration available for updates" -TraceLevel $TraceLevel
    }

    # Call Install-Resource to perform the actual update
    $installResult = Install-Resource -Yes:$Yes -Force:$Force -TraceLevel $TraceLevel
    return $installResult
}

<#
.SYNOPSIS
    Gets the target Node.js version from configuration or defaults to latest.
.DESCRIPTION
    Returns the target Node.js version to install, either from versions.json configuration
    or "latest" if no version is pinned. This demonstrates the pattern for
    version selection logic.
.OUTPUTS
    [string] Target version string for display
#>
function Get-TargetVersion {
    if ($VERSIONS_CONFIG -and $VERSIONS_CONFIG.tools.nodejs.runtime.version) {
        return $VERSIONS_CONFIG.tools.nodejs.runtime.version
    } else {
        return "latest"
    }
}

<#
.SYNOPSIS
    Gets the target Node.js version for Install operation.
.DESCRIPTION
    Returns the target Node.js version for installation:
    1. If pinned version exists, use that
    2. If no pinned version, default to LTS version (safer for new installs)
.OUTPUTS
    [string] Target version string for display
#>
function Get-TargetVersionForInstall {
    if ($VERSIONS_CONFIG -and $VERSIONS_CONFIG.tools.nodejs.runtime.version) {
        return $VERSIONS_CONFIG.tools.nodejs.runtime.version
    } else {
        return "LTS"
    }
}

<#
.SYNOPSIS
    Displays help information.
.DESCRIPTION
    Shows comprehensive usage information and examples for this script.
    This demonstrates the standard help pattern used across resource scripts.
#>
function Show-Help {
    $help = @"
Node.js Development Environment Management Script
===============================================

Manages Node.js development environment with Install, Clean, Doctor, and Update operations.
Handles Node.js runtime, npm package manager, and global npm packages.

Usage:
    node-new.ps1 [-Install] [-Clean] [-Doctor] [-Update] [-LTS] [-Yes] [-Force] [-TraceLevel <level>] [-Help]

Operations:
    -Install    Installs pinned version (if configured), otherwise defaults to LTS version
    -Clean      Removes Node.js runtime and cleans environment
    -Doctor     Verifies Node.js installation status with version table
    -Update     Updates pinned versions in configuration (use with -LTS for LTS, otherwise latest)
    -Help       Shows this help message

Options:
    -LTS        Used with -Update to pin to LTS versions (not available with -Install)
    -Yes        Automatically answers yes to prompts
    -Force      Skips verification checks
    -TraceLevel Sets output detail level (ERROR, WARN, INFO, DEBUG)

Examples:
    # Install Node.js with detailed output
    node-new.ps1 -Install -TraceLevel DEBUG

    # Check Node.js installation showing version table
    node-new.ps1 -Doctor -TraceLevel INFO

    # Remove Node.js without prompts
    node-new.ps1 -Clean -Yes

    # Update Node.js and global packages
    node-new.ps1 -Update -Yes

Notes:
    - Cross-platform support: Windows, macOS, and Linux
    - Install and Clean operations may require elevated privileges on some platforms
    - Doctor shows Component, Installed, Pinned, and Latest columns in a table
    - TraceLevel determines which messages are displayed:
      DEBUG: Shows all messages including detailed diagnostics
      INFO:  Shows INFO, WARN, and ERROR messages
      WARN:  Shows WARN and ERROR messages only
      ERROR: Shows only ERROR messages (minimal output)
    - The script manages:
      * Node.js JavaScript runtime
      * npm package manager (bundled with Node.js)
      * Global npm packages (markdownlint-cli, etc.)
      * Environment variables and PATH entries
      * Configuration files for reproducible builds
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

    # Validate parameter combinations
    if ($LTS -and -not $Update) {
        Write-Log -Level "ERROR" -Message "-LTS parameter can only be used with -Update operation" -TraceLevel $TraceLevel
        Write-Log -Level "INFO" -Message "Use: node.ps1 -Update -LTS to pin to LTS versions" -TraceLevel $TraceLevel
        exit 1
    }

    # Process operations in specific order: Clean -> Install -> Update -> Doctor
    if ($Clean) {
        Write-Log -Level "HEADER" -Message "CLEANING NODE.JS" -TraceLevel $TraceLevel
        if (-not (Clean-Resources -Yes:$Yes -Force:$Force -TraceLevel $TraceLevel)) {
            exit 1
        }
    }

    if ($Install) {
        $targetVersion = Get-TargetVersionForInstall
        Write-Log -Level "HEADER" -Message "INSTALLING NODE.JS $targetVersion" -TraceLevel $TraceLevel
        if (-not (Install-Resource -Yes:$Yes -Force:$Force -TraceLevel $TraceLevel)) {
            exit 1
        }
    }

    if ($Update) {
        $targetVersionType = if ($LTS) { "LTS" } else { "latest" }
        Write-Log -Level "HEADER" -Message "UPDATING NODE.JS to $targetVersionType" -TraceLevel $TraceLevel
        if (-not (Update-Resource -Yes:$Yes -Force:$Force -TraceLevel $TraceLevel)) {
            exit 1
        }
    }

    if ($Doctor) {
        # Run silently when returning structured data
        $silent = $HashTable -or $Json

        $result = Doctor-Resource -Force:$Force -TraceLevel $(if ($silent) { "ERROR" } else { $TraceLevel }) -Silent:$silent

        if ($HashTable) {
            return $result
        } elseif ($Json) {
            return ($result | ConvertTo-Json -Depth 5)
        }

        if ($result.Status -eq "Error") {
            exit 1
        }
    }

    # If no operation specified, show help
    if (-not ($Install -or $Clean -or $Doctor -or $Update -or $Help)) {
        Show-Help
        exit 0
    }

    exit 0
} catch {
    Write-Log -Level "ERROR" -Message "An error occurred: $_" -TraceLevel $TraceLevel
    exit 1
}
