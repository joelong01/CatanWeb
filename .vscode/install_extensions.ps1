#!/usr/bin/env pwsh
<#
.SYNOPSIS
    Scenario-based VS Code extension installer for development workflows

.DESCRIPTION
    This script installs VS Code extensions based on development scenarios (C#, Rust, Web, etc.).
    It reads configuration from extension-scenarios.json and can install extensions for specific
    scenarios or present an interactive menu for selection.

.PARAMETER Scenario
    Install extensions for specific scenario(s). Use comma-separated values for multiple scenarios.
    Available scenarios: csharp-winui, web-frontend, rust, go, devops-containers, scripting-automation, documentation, testing-qa, utilities

.PARAMETER Clean
    Remove all installed extensions except core/platform extensions (preserves AI and essential tools)

.PARAMETER ListScenarios
    List all available scenarios and exit

.PARAMETER Force
    Force reinstall extensions even if they're already installed

.PARAMETER SkipCore
    Skip installing core extensions (always-install list)

.PARAMETER ConfigFile
    Path to the extension configuration JSON file. Default: "extension-scenarios.json"

.EXAMPLE
    .\install_extensions.ps1
    # Shows interactive scenario selection menu

.EXAMPLE
    .\install_extensions.ps1 -Scenario "csharp-winui"
    # Installs C# and WinUI development extensions

.EXAMPLE
    .\install_extensions.ps1 -Scenario "csharp-winui,web-frontend" -Force
    # Installs extensions for both C# and web development, forcing reinstall

.EXAMPLE
    .\install_extensions.ps1 -Clean
    # Removes all extensions except core/platform ones

.EXAMPLE
    .\install_extensions.ps1 -ListScenarios
    # Lists all available scenarios

.NOTES
    Requires VS Code to be installed and 'code' command to be available in PATH
#>

param(
    [string[]]$Scenario = @(),
    [switch]$Clean = $false,
    [switch]$ListScenarios = $false,
    [switch]$Force = $false,
    [switch]$SkipCore = $false,
    [string]$ConfigFile = "extension-scenarios.json"
)

# Colors for output
$Red = "`e[31m"
$Green = "`e[32m"
$Yellow = "`e[33m"
$Blue = "`e[34m"
$Magenta = "`e[35m"
$Cyan = "`e[36m"
$White = "`e[37m"
$Bold = "`e[1m"
$Reset = "`e[0m"

function Write-ColorOutput {
    param(
        [string]$Message,
        [string]$Color = $Reset
    )
    Write-Host "${Color}${Message}${Reset}"
}

function Test-VSCodeInstalled {
    try {
        $null = Get-Command code -ErrorAction Stop
        return $true
    }
    catch {
        return $false
    }
}

function Get-InstalledExtensions {
    try {
        Write-ColorOutput "  Running: code --list-extensions" $Cyan
        $output = & code --list-extensions 2>&1
        Write-ColorOutput "  Exit code: $LASTEXITCODE" $Cyan
        if ($LASTEXITCODE -eq 0) {
            Write-ColorOutput "  Found $($output.Count) extensions" $Cyan
            return $output
        }
        Write-ColorOutput "  Command failed with exit code: $LASTEXITCODE" $Red
        Write-ColorOutput "  Output: $output" $Red
        return @()
    }
    catch {
        Write-ColorOutput "  Exception in Get-InstalledExtensions: $($_.Exception.Message)" $Red
        return @()
    }
}

function Install-Extension {
    param(
        [string]$ExtensionId,
        [string]$ExtensionName,
        [bool]$ForceInstall = $false
    )
    
    Write-ColorOutput "  Installing: $ExtensionName ($ExtensionId)" $Cyan
    
    $installArgs = @("--install-extension", $ExtensionId)
    if ($ForceInstall) {
        $installArgs += "--force"
    }
    
    try {
        $output = & code @installArgs 2>&1
        if ($LASTEXITCODE -eq 0) {
            Write-ColorOutput "  ✓ Successfully installed: $ExtensionName" $Green
            return $true
        }
        else {
            Write-ColorOutput "  ✗ Failed to install: $ExtensionName" $Red
            Write-ColorOutput "    Error: $output" $Red
            return $false
        }
    }
    catch {
        Write-ColorOutput "  ✗ Exception installing: $ExtensionName" $Red
        Write-ColorOutput "    Error: $($_.Exception.Message)" $Red
        return $false
    }
}

function Uninstall-Extension {
    param(
        [string]$ExtensionId,
        [string]$ExtensionName
    )
    
    Write-ColorOutput "  Removing: $ExtensionName ($ExtensionId)" $Yellow
    
    try {
        $output = & code --uninstall-extension $ExtensionId 2>&1
        if ($LASTEXITCODE -eq 0) {
            Write-ColorOutput "  ✓ Successfully removed: $ExtensionName" $Green
            return $true
        }
        else {
            Write-ColorOutput "  ✗ Failed to remove: $ExtensionName" $Red
            Write-ColorOutput "    Error: $output" $Red
            return $false
        }
    }
    catch {
        Write-ColorOutput "  ✗ Exception removing: $ExtensionName" $Red
        Write-ColorOutput "    Error: $($_.Exception.Message)" $Red
        return $false
    }
}

function Get-CoreExtensionIds {
    param($config)
    
    $coreIds = @()
    
    # Always preserve extensions from alwaysInstall
    foreach ($ext in $config.alwaysInstall) {
        $coreIds += $ext.id
    }
    
    # Additional platform/core extensions that should never be removed
    $platformCore = @(
        # VS Code built-in language extensions
        "ms-vscode.vscode-typescript-next",
        "ms-vscode.vscode-json",
        "ms-vscode.references-view",
        "ms-vscode.search-result",
        
        # Core Microsoft extensions
        "ms-vscode.powershell",
        "ms-vscode.remote-explorer",
        "ms-vscode.remote-server",
        
        # Source control (essential)
        "eamodio.gitlens",
        "github.vscode-pull-request-github",
        
        # AI assistants (never remove these!)
        "github.copilot",
        "github.copilot-chat",
        "ms-dotnettools.vscodeintellicode-csharp"
    )
    
    $coreIds += $platformCore
    return $coreIds | Sort-Object -Unique
}

function Invoke-CleanExtensions {
    param($config, $installedExtensions)
    
    Write-ColorOutput "${Bold}${Red}🧹 Extension Cleanup Mode${Reset}"
    Write-ColorOutput "${Red}════════════════════════════════════════════════════════════════════════════════${Reset}"
    
    $coreExtensions = Get-CoreExtensionIds $config
    $extensionsToRemove = @()
    
    Write-ColorOutput "📋 Analyzing installed extensions..." $Blue
    Write-ColorOutput "Total installed: $($installedExtensions.Count)" $Blue
    Write-ColorOutput "Core extensions (will be preserved): $($coreExtensions.Count)" $Blue
    
    foreach ($extId in $installedExtensions) {
        if ($coreExtensions -notcontains $extId) {
            $extensionsToRemove += $extId
        }
    }
    
    Write-ColorOutput ""
    Write-ColorOutput "📊 Cleanup Summary:" $Bold$Blue
    Write-ColorOutput "  Extensions to remove: $($extensionsToRemove.Count)" $Red
    Write-ColorOutput "  Extensions to preserve: $($coreExtensions.Count)" $Green
    
    if ($extensionsToRemove.Count -eq 0) {
        Write-ColorOutput "✅ No extensions to remove - only core extensions are installed!" $Green
        return
    }
    
    Write-ColorOutput ""
    Write-ColorOutput "Extensions that will be REMOVED:" $Red
    foreach ($extId in $extensionsToRemove) {
        Write-ColorOutput "  • $extId" $Red
    }
    
    Write-ColorOutput ""
    Write-ColorOutput "Extensions that will be PRESERVED (Core):" $Green
    foreach ($extId in $coreExtensions) {
        if ($installedExtensions -contains $extId) {
            Write-ColorOutput "  • $extId" $Green
        }
    }
    
    Write-ColorOutput ""
    Write-ColorOutput "${Bold}${Red}⚠️  WARNING: This will remove $($extensionsToRemove.Count) extensions!${Reset}" 
    Write-ColorOutput "This action cannot be undone easily. You can reinstall using scenarios later." $Yellow
    
    $confirmation = Read-Host "Are you sure you want to proceed? Type 'YES' to confirm"
    if ($confirmation -ne 'YES') {
        Write-ColorOutput "Cleanup cancelled by user" $Yellow
        return
    }
    
    Write-ColorOutput ""
    Write-ColorOutput "🧹 Removing Extensions..." $Bold$Red
    $successful = 0
    $failed = 0
    
    foreach ($extId in $extensionsToRemove) {
        if (Uninstall-Extension -ExtensionId $extId -ExtensionName $extId) {
            $successful++
        }
        else {
            $failed++
        }
        Start-Sleep -Milliseconds 300  # Brief pause between removals
    }
    
    Write-ColorOutput ""
    Write-ColorOutput "${Bold}${Green}🧹 Cleanup Complete!${Reset}"
    Write-ColorOutput "${Green}════════════════════════════════════════════════════════════════════════════════${Reset}"
    Write-ColorOutput "✅ Successfully removed: $successful" $Green
    Write-ColorOutput "❌ Failed to remove: $failed" $(if ($failed -gt 0) { $Red } else { $Green })
    Write-ColorOutput "🛡️  Core extensions preserved: $($coreExtensions.Count)" $Blue
    
    Write-ColorOutput ""
    Write-ColorOutput "💡 Next steps:" $Cyan
    Write-ColorOutput "   1. Restart VS Code to complete the cleanup" $Cyan
    Write-ColorOutput "   2. Run this script with scenarios to reinstall what you need" $Cyan
    Write-ColorOutput "   3. Example: .\install_extensions.ps1 -Scenario 'csharp-winui'" $Cyan
}

function Show-ScenarioTable {
    param($scenarios)
    
    Write-ColorOutput ""
    Write-ColorOutput "� Available Development Scenarios:" $Bold$Blue
    Write-ColorOutput "════════════════════════════════════════════════════════════════════════════════" $Blue
    
    $table = @()
    $index = 1
    foreach ($scenarioKey in $scenarios.PSObject.Properties.Name) {
        $scenario = $scenarios.$scenarioKey
        $extensionCount = $scenario.extensions.Count
        $requiredCount = ($scenario.extensions | Where-Object { $_.required -eq $true }).Count
        
        $table += [PSCustomObject]@{
            "#" = $index
            "Scenario" = $scenarioKey
            "Name" = $scenario.name
            "Extensions" = "$extensionCount ($requiredCount required)"
            "Description" = $scenario.description
        }
        $index++
    }
    
    # Display table
    $table | Format-Table -Property "#", "Scenario", "Name", "Extensions", "Description" -Wrap | Out-String | Write-Host
    
    return $table
}

function Get-UserScenarioSelection {
    param($scenarioTable, $scenarios)
    
    Write-ColorOutput "Selection Options:" $Yellow
    Write-ColorOutput "  • Enter scenario numbers (e.g., 1,3,5)" $Yellow
    Write-ColorOutput "  • Enter scenario names (e.g., csharp-winui,web-frontend)" $Yellow
    Write-ColorOutput "  • Enter 'all' to install all scenarios" $Yellow
    Write-ColorOutput "  • Enter 'q' to quit" $Yellow
    Write-ColorOutput ""
    
    $selection = Read-Host "Select scenarios"
    
    if ($selection -eq 'q') {
        Write-ColorOutput "Installation cancelled by user" $Yellow
        exit 0
    }
    
    if ($selection -eq 'all') {
        return $scenarios.PSObject.Properties.Name
    }
    
    $selectedScenarios = @()
    $parts = $selection -split '[,\s]' | Where-Object { $_ -ne '' }
    
    foreach ($part in $parts) {
        if ($part -match '^\d+$') {
            # Numeric selection
            $index = [int]$part
            if ($index -ge 1 -and $index -le $scenarioTable.Count) {
                $selectedScenarios += $scenarioTable[$index - 1].Scenario
            }
            else {
                Write-ColorOutput "⚠️  Invalid scenario number: $part (valid range: 1-$($scenarioTable.Count))" $Yellow
            }
        }
        else {
            # Name selection
            if ($scenarios.PSObject.Properties.Name -contains $part) {
                $selectedScenarios += $part
            }
            else {
                Write-ColorOutput "⚠️  Unknown scenario: $part" $Yellow
            }
        }
    }
    
    return $selectedScenarios
}

# Main script
Write-ColorOutput "${Bold}${Magenta}🚀 VS Code Scenario-Based Extension Installer${Reset}" 
Write-ColorOutput "${Magenta}════════════════════════════════════════════════════════════════════════════════${Reset}"

# Check if VS Code is installed
if (-not (Test-VSCodeInstalled)) {
    Write-ColorOutput "❌ VS Code is not installed or 'code' command is not in PATH" $Red
    Write-ColorOutput "Please install VS Code and ensure it's added to your PATH" $Yellow
    exit 1
}

# Check if config file exists
$configPath = Join-Path $PSScriptRoot $ConfigFile
if (-not (Test-Path $configPath)) {
    Write-ColorOutput "❌ Configuration file not found: $configPath" $Red
    exit 1
}

# Read configuration
try {
    $config = Get-Content $configPath -Raw | ConvertFrom-Json
    Write-ColorOutput "✓ Configuration loaded from: $ConfigFile" $Green
}
catch {
    Write-ColorOutput "❌ Failed to parse configuration file: $($_.Exception.Message)" $Red
    exit 1
}

# Handle list scenarios
if ($ListScenarios) {
    Show-ScenarioTable $config.scenarios | Out-Null
    exit 0
}

# Get installed extensions
Write-ColorOutput "📋 Checking currently installed extensions..." $Blue
$installedExtensions = Get-InstalledExtensions
Write-ColorOutput "Found $($installedExtensions.Count) installed extensions" $Blue

# Handle clean mode
if ($Clean) {
    Invoke-CleanExtensions $config $installedExtensions
    exit 0
}

# Determine scenarios to install
if ($Scenario.Count -eq 0) {
    # Interactive mode
    $scenarioTable = Show-ScenarioTable $config.scenarios
    $selectedScenarios = Get-UserScenarioSelection $scenarioTable $config.scenarios
}
else {
    # Command line mode
    $selectedScenarios = $Scenario -split ',' | ForEach-Object { $_.Trim() }
    
    # Validate scenarios
    $invalidScenarios = $selectedScenarios | Where-Object { $config.scenarios.PSObject.Properties.Name -notcontains $_ }
    if ($invalidScenarios) {
        Write-ColorOutput "❌ Invalid scenarios: $($invalidScenarios -join ', ')" $Red
        Write-ColorOutput "Available scenarios: $($config.scenarios.PSObject.Properties.Name -join ', ')" $Yellow
        exit 1
    }
}

if ($selectedScenarios.Count -eq 0) {
    Write-ColorOutput "No scenarios selected. Exiting." $Yellow
    exit 0
}

Write-ColorOutput ""
Write-ColorOutput "🎯 Selected Scenarios: $($selectedScenarios -join ', ')" $Bold$Cyan

# Collect extensions to install
$extensionsToInstall = @()
$alreadyInstalled = 0

# Always install core extensions (unless skipped)
if (-not $SkipCore) {
    Write-ColorOutput ""
    Write-ColorOutput "📦 Processing Core Extensions (Always Install)..." $Blue
    foreach ($ext in $config.alwaysInstall) {
        $isInstalled = $installedExtensions -contains $ext.id
        
        if ($isInstalled -and -not $Force) {
            Write-ColorOutput "⏭️  Already installed: $($ext.name) ($($ext.id))" $Yellow
            $alreadyInstalled++
        }
        else {
            $extensionsToInstall += [PSCustomObject]@{
                id = $ext.id
                name = $ext.name
                description = $ext.description
                scenario = "Core"
                required = $true
            }
        }
    }
}

# Process selected scenarios
foreach ($scenarioKey in $selectedScenarios) {
    $scenarioObj = $config.scenarios.PSObject.Properties.Where({$_.Name -eq $scenarioKey}).Value
    Write-ColorOutput ""
    Write-ColorOutput "📦 Processing Scenario: $($scenarioObj.name)" $Blue
    
    foreach ($ext in $scenarioObj.extensions) {
        $isInstalled = $installedExtensions -contains $ext.id
        
        if ($isInstalled -and -not $Force) {
            Write-ColorOutput "⏭️  Already installed: $($ext.name) ($($ext.id))" $Yellow
            $alreadyInstalled++
        }
        else {
            $extensionsToInstall += [PSCustomObject]@{
                id = $ext.id
                name = $ext.name
                description = $ext.description
                scenario = $scenarioObj.name
                required = $ext.required
            }
        }
    }
}

# Remove duplicates (in case extension appears in multiple scenarios)
$extensionsToInstall = $extensionsToInstall | Sort-Object id -Unique

# Summary
Write-ColorOutput ""
Write-ColorOutput "📊 Installation Summary:" $Bold$Blue
Write-ColorOutput "  Scenarios selected: $($selectedScenarios.Count)" $Blue
Write-ColorOutput "  Extensions to install: $($extensionsToInstall.Count)" $Blue
Write-ColorOutput "  Already installed: $alreadyInstalled" $Blue

if ($extensionsToInstall.Count -eq 0) {
    Write-ColorOutput "✅ All selected extensions are already installed!" $Green
    exit 0
}

# Show what will be installed
Write-ColorOutput ""
Write-ColorOutput "Extensions to be installed:" $Cyan
foreach ($ext in $extensionsToInstall) {
    $requiredText = if ($ext.required) { "(Required)" } else { "(Optional)" }
    Write-ColorOutput "  • $($ext.name) - $($ext.scenario) $requiredText" $Cyan
}

# Confirm installation
if (-not $Force) {
    Write-ColorOutput ""
    $response = Read-Host "Do you want to proceed with installation? (y/N)"
    if ($response -notmatch "^[Yy]") {
        Write-ColorOutput "Installation cancelled by user" $Yellow
        exit 0
    }
}

# Install extensions
Write-ColorOutput ""
Write-ColorOutput "🔧 Installing Extensions..." $Bold$Blue
$successful = 0
$failed = 0

foreach ($ext in $extensionsToInstall) {
    Write-ColorOutput ""
    Write-ColorOutput "🔧 [$($ext.scenario)] Installing $($ext.name)..." $Blue
    if (Install-Extension -ExtensionId $ext.id -ExtensionName $ext.name -ForceInstall $Force) {
        $successful++
    }
    else {
        $failed++
    }
    Start-Sleep -Milliseconds 500  # Brief pause between installations
}

# Final summary
Write-ColorOutput ""
Write-ColorOutput "${Bold}${Magenta}📋 Installation Complete!${Reset}"
Write-ColorOutput "${Magenta}════════════════════════════════════════════════════════════════════════════════${Reset}"
Write-ColorOutput "✅ Successfully installed: $successful" $Green
Write-ColorOutput "❌ Failed: $failed" $(if ($failed -gt 0) { $Red } else { $Green })

if ($failed -gt 0) {
    Write-ColorOutput ""
    Write-ColorOutput "⚠️  Some extensions failed to install. You can:" $Yellow
    Write-ColorOutput "   1. Try running the script again with -Force" $Yellow
    Write-ColorOutput "   2. Install failed extensions manually in VS Code" $Yellow
    Write-ColorOutput "   3. Check the VS Code output for detailed error messages" $Yellow
}

Write-ColorOutput ""
Write-ColorOutput "💡 Next steps:" $Cyan
Write-ColorOutput "   1. Restart VS Code to ensure all extensions are loaded" $Cyan
Write-ColorOutput "   2. Open your project files to verify extension functionality" $Cyan
Write-ColorOutput "   3. Configure extension settings as needed" $Cyan

Write-ColorOutput ""
Write-ColorOutput "📖 Installed scenarios: $($selectedScenarios -join ', ')" $Blue

exit $(if ($failed -gt 0) { 1 } else { 0 })
