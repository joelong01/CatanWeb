<#
.SYNOPSIS
    GitHub Actions OIDC setup for Azure deployments.
.DESCRIPTION
    Manages the Azure AD app registration, service principal, and federated
    credentials needed for GitHub Actions to deploy to Azure without secrets.

    Azure only — no local equivalent.
.EXAMPLE
    ./github.ps1 install -Azure
.EXAMPLE
    ./github.ps1 doctor -Azure
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
$ScriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
Import-Module "$ScriptDir\utility-scripts.psm1" -Force
Set-ModuleTraceLevel -TraceLevel $TraceLevel
$PSDefaultParameterValues = @{ 'Write-Log:TraceLevel' = $TraceLevel }

if ($Help) { $Verb = "help" }

# ─── Main ────────────────────────────────────────────────────────────────────

switch ($Verb) {
    "doctor" {
        if (-not $Azure) {
            Write-Log -Level INFO -Message "GitHub OIDC is Azure-only. Use: github.ps1 doctor -Azure" -NoLabel
            exit 0
        }
        $azureScript = Join-Path $ScriptDir "catan-azure.ps1"
        & $azureScript github doctor -TraceLevel $TraceLevel -Json:$Json -HashTable:$HashTable
        exit $LASTEXITCODE
    }

    "install" {
        if (-not $Azure) {
            Write-Log -Level ERROR -Message "GitHub OIDC requires -Azure." -NoLabel
            exit 1
        }
        $azureScript = Join-Path $ScriptDir "catan-azure.ps1"
        & $azureScript github install -TraceLevel $TraceLevel -Yes:$Yes
        exit $LASTEXITCODE
    }

    "clean" {
        Write-Log -Level WARN -Message "GitHub OIDC clean not yet implemented. Delete the app registration manually in Azure Portal." -NoLabel
        exit 0
    }

    "help" {
        $name = Split-Path -Leaf $MyInvocation.MyCommand.Path
        Write-Host @"

GitHub Actions OIDC Setup
=========================

Usage: $name <verb> -Azure [options]

Verbs:
  doctor     Check OIDC configuration (app registration, credentials, roles)
  install    Create app registration, service principal, federated credentials
  help       Show this message

Options:
  -Azure         Required for all operations
  -Yes           Skip confirmation prompts
  -HashTable     Return doctor result as hashtable
  -Json          Return doctor result as JSON
  -TraceLevel    Output: ERROR, WARN, INFO (default), DEBUG

Examples:
  $name doctor -Azure     # Check OIDC health
  $name install -Azure    # Set up GitHub Actions OIDC

"@
        exit 0
    }
}
