<#
.SYNOPSIS
    Template for resource management scripts. Copy this to create a new noun script.
.DESCRIPTION
    Demonstrates the standard patterns for CatanWeb resource scripts:
    - Positional Verb parameter (doctor/install/clean/help)
    - Doctor returns standard hashtable via -HashTable or -Json
    - Install is idempotent (calls doctor first, only creates what's missing)
    - Clean is destructive (requires -Yes or prompts)
    - TraceLevel propagated via $PSDefaultParameterValues
    - Azure/Local parity via -Azure switch

    To create a new noun script:
    1. Copy this file: cp _template.ps1 my-resource.ps1
    2. Replace "Template" with your resource name in function names
    3. Implement Doctor-Template, Install-Template, Clean-Template
    4. Update the ValidateSet for Verb if you need "deploy"
    5. Delete these instructions

    This script actually runs: ./_template.ps1 doctor returns a valid result.

    COMPOSITION NOTE: When catan.ps1 calls noun scripts for data (e.g., doctor results),
    it should use -HashTable mode and inspect the returned hashtable. When calling for
    side effects (install/clean), it should call via & pwsh $script so that exit codes
    don't terminate the parent process.
.EXAMPLE
    ./_template.ps1 doctor
.EXAMPLE
    ./_template.ps1 doctor -HashTable
.EXAMPLE
    ./_template.ps1 doctor -Json
.EXAMPLE
    ./_template.ps1 install -Yes
.EXAMPLE
    ./_template.ps1 clean -Yes
.EXAMPLE
    ./_template.ps1 doctor -Azure
#>

param(
    [Parameter(Position = 0)]
    [ValidateSet("doctor", "install", "clean", "help")]
    [string]$Verb = "help",

    [ValidateSet("ERROR", "WARN", "INFO", "DEBUG")]
    [Alias("LogLevel")]
    [string]$TraceLevel = "INFO",

    [switch]$Azure,
    [switch]$Yes,
    [switch]$Force,
    [switch]$HashTable,
    [switch]$Json,
    [switch]$Help
)

$ErrorActionPreference = "Stop"
$scriptPath = Split-Path -Parent $MyInvocation.MyCommand.Path
Import-Module "$scriptPath\utility-scripts.psm1" -Force
$PSDefaultParameterValues = @{ 'Write-Log:TraceLevel' = $TraceLevel }

# ─── Doctor ──────────────────────────────────────────────────────────────────

function Doctor-Template {
    <#
    .SYNOPSIS
        Checks resource health. Read-only — never modifies state.
    .OUTPUTS
        [hashtable] Standard doctor result: Name, Target, Status, Version, Message, Details
    #>

    $target = if ($Azure) { "Azure" } else { "Local" }

    $result = @{
        Name    = "template"
        Target  = $target               # "Local" | "Azure" — self-describing
        Status  = "Error"               # Installed | NotInstalled | NeedsFix | Error
        Version = $null
        Message = ""
        Details = @{}                   # Resource-specific extra data (optional)
    }

    Write-Log -Level DEBUG -Message "Checking template resource ($target)..."

    try {
        if ($Azure) {
            # Azure health check — use Get-AzureResourceNames for canonical naming
            # $az = Get-AzureResourceNames -ProjectRoot (Split-Path $scriptPath -Parent)
            # $existing = Invoke-AzCommand "webapp show --name $($az.GameServiceAppName) -g $($az.ResourceGroup)" -Check
            # if ($existing) {
            #     $result.Status = "Installed"
            #     $result.Message = "Azure resource is healthy"
            # }
            $result.Status = "NotInstalled"
            $result.Message = "Template Azure resource not provisioned"
        }
        else {
            # Local health check
            # Example: check if a process is running, a port is listening, a file exists
            $result.Status = "Installed"
            $result.Version = "1.0.0"
            $result.Message = "Template resource is healthy"
        }
    }
    catch {
        $result.Status = "Error"
        $result.Message = "Health check failed: $_"
    }

    return $result
}

# ─── Install ─────────────────────────────────────────────────────────────────

function Install-Template {
    <#
    .SYNOPSIS
        Idempotent install. Checks state first, only creates what's missing.
    .PARAMETER DoctorResult
        Result from Doctor-Template (avoids redundant health check)
    .OUTPUTS
        [bool] True if resource is healthy after install
    #>
    param(
        [Parameter(Mandatory)]
        [hashtable]$DoctorResult
    )

    if ($DoctorResult.Status -eq "Installed") {
        Write-Log -Level INFO -Message "Template resource already installed ($($DoctorResult.Version))" -NoLabel
        return $true
    }

    $target = if ($Azure) { "Azure" } else { "Local" }
    Write-Log -Level INFO -Message "Installing template resource ($target)..." -NoLabel

    if ($Azure) {
        # Example Azure install — use Get-AzureResourceNames for canonical naming:
        # $az = Get-AzureResourceNames -ProjectRoot (Split-Path $scriptPath -Parent)
        # Invoke-AzCommand "webapp create --name $($az.GameServiceAppName) -g $($az.ResourceGroup) --plan $($az.GameServicePlan)"
        Write-Log -Level INFO -Message "  (simulated) Azure resource created" -NoLabel
    }
    else {
        # Example local install:
        # Download, configure, start service, etc.
        Write-Log -Level INFO -Message "  (simulated) Local resource installed" -NoLabel
    }

    # Verify
    $verify = Doctor-Template
    if ($verify.Status -ne "Installed") {
        Write-Log -Level ERROR -Message "Install completed but verification failed: $($verify.Message)"
        return $false
    }

    Write-Log -Level INFO -Message "Template resource installed successfully" -NoLabel
    return $true
}

# ─── Clean ───────────────────────────────────────────────────────────────────

function Clean-Template {
    <#
    .SYNOPSIS
        Removes the resource. Destructive — prompts for confirmation.
    .PARAMETER DoctorResult
        Result from Doctor-Template
    .OUTPUTS
        [bool] True if resource was removed
    #>
    param(
        [Parameter(Mandatory)]
        [hashtable]$DoctorResult
    )

    if ($DoctorResult.Status -eq "NotInstalled") {
        Write-Log -Level INFO -Message "Template resource not installed, nothing to clean" -NoLabel
        return $true
    }

    if (-not $Yes) {
        $target = if ($Azure) { "Azure" } else { "Local" }
        $response = Get-UserConfirmation -Question "Remove template resource ($target)?"
        if ($response -ne 'Yes') {
            Write-Log -Level INFO -Message "Clean cancelled" -NoLabel
            return $false
        }
    }

    $target = if ($Azure) { "Azure" } else { "Local" }
    Write-Log -Level INFO -Message "Removing template resource ($target)..." -NoLabel

    if ($Azure) {
        # Example: Invoke-AzCommand "webapp delete --name $($az.GameServiceAppName) -g $($az.ResourceGroup) --yes"
        Write-Log -Level INFO -Message "  (simulated) Azure resource deleted" -NoLabel
    }
    else {
        # Example: Stop process, delete files, etc.
        Write-Log -Level INFO -Message "  (simulated) Local resource removed" -NoLabel
    }

    return $true
}

# ─── Output helpers ──────────────────────────────────────────────────────────

function Show-DoctorOutput {
    <#
    .SYNOPSIS
        Human-readable display of doctor result.
    #>
    param([hashtable]$Result)

    $color = switch ($Result.Status) {
        "Installed"    { "Green" }
        "NeedsFix"     { "Yellow" }
        "NotInstalled" { "Yellow" }
        default        { "Red" }
    }

    $targetLabel = if ($Result.Target -eq "Azure") { " (Azure)" } else { "" }
    Write-Log -Level INFO -Message "" -NoLabel
    Write-Log -Level INFO -Message "Template Resource$targetLabel" -NoLabel -ForegroundColor Cyan
    Write-Log -Level INFO -Message ("=" * (18 + $targetLabel.Length)) -NoLabel -ForegroundColor Cyan
    Write-Log -Level INFO -Message "" -NoLabel
    Write-Log -Level INFO -Message "Status:  " -NoLabel -NoNewline
    Write-Log -Level INFO -Message $Result.Status -NoLabel -ForegroundColor $color
    if ($Result.Version) {
        Write-Log -Level INFO -Message "Version: $($Result.Version)" -NoLabel
    }
    Write-Log -Level INFO -Message "" -NoLabel
    Write-Log -Level INFO -Message $Result.Message -NoLabel -ForegroundColor $color
    Write-Log -Level INFO -Message "" -NoLabel
}

function Show-Help {
    # Write-Host is intentional here — help text is UI display, not operational logging.
    # Write-Log would add [INFO HH:mm:ss] prefix and 5-space indent, which looks wrong
    # for formatted help output.
    $name = Split-Path -Leaf $MyInvocation.ScriptName
    $help = @"

Template Resource Management
============================

Usage: $name <verb> [options]

Verbs:
  doctor     Check resource health (read-only)
  install    Create/repair resource (idempotent)
  clean      Remove resource (destructive, prompts unless -Yes)
  help       Show this message

Options:
  -Azure         Target Azure instead of local (default)
  -Yes           Skip confirmation prompts
  -Force         Override safety checks
  -HashTable     Return doctor result as hashtable (for script composition)
  -Json          Return doctor result as JSON
  -TraceLevel    Output verbosity: ERROR, WARN, INFO (default), DEBUG

Examples:
  $name doctor                  # Check local health
  $name doctor -Azure           # Check Azure health
  $name doctor -HashTable       # Get result for script composition
  $name install -Yes            # Install without prompting
  $name install -Azure -Yes     # Provision in Azure
  $name clean -Yes              # Remove local resource

"@
    Write-Host $help
}

# ─── Main ────────────────────────────────────────────────────────────────────
#
# NOTE ON EXIT VS RETURN:
# - "return $result" is used for -HashTable/-Json so parent scripts get the object
# - "exit N" is used for standalone/CLI invocation to set the process exit code
# - When catan.ps1 needs data: call with -HashTable, inspect the returned hashtable
# - When catan.ps1 needs side effects: call via "& pwsh $script" for exit code isolation

if ($Help) { $Verb = "help" }

switch ($Verb) {
    "doctor" {
        $result = Doctor-Template

        if ($HashTable) { return $result }
        if ($Json) { return ($result | ConvertTo-Json -Depth 5) }

        Show-DoctorOutput -Result $result
        exit $(if ($result.Status -eq "Installed") { 0 } else { 1 })
    }

    "install" {
        $result = Doctor-Template
        $success = Install-Template -DoctorResult $result
        exit $(if ($success) { 0 } else { 1 })
    }

    "clean" {
        $result = Doctor-Template
        $success = Clean-Template -DoctorResult $result
        exit $(if ($success) { 0 } else { 1 })
    }

    "help" {
        Show-Help
        exit 0
    }
}
