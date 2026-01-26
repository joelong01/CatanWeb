<#
.SYNOPSIS
VS Code development environment health check script.
.DESCRIPTION
Diagnoses VS Code installation, extensions, Claude Code credentials, and common
configuration problems. Provides automated fixes or manual instructions.

This script follows the standardized template patterns for resource management.
.PARAMETER Doctor
Runs health checks and displays status
.PARAMETER Fix
Automatically fixes problems that can be resolved programmatically
.PARAMETER HashTable
Returns Doctor results as a PowerShell hashtable
.PARAMETER Json
Returns Doctor results as JSON
.PARAMETER TraceLevel
Sets output detail level (ERROR, WARN, INFO, DEBUG)
.PARAMETER Help
Displays help information
.EXAMPLE
.\vs-code-health.ps1 -Doctor
Runs all health checks and displays results
.EXAMPLE
.\vs-code-health.ps1 -Doctor -Fix
Runs health checks and automatically fixes issues
.EXAMPLE
.\vs-code-health.ps1 -Doctor -Json
Returns health status as JSON
#>

param(
    [Parameter()]
    [switch]$Doctor,

    [Parameter()]
    [switch]$Fix,

    [Parameter()]
    [switch]$HashTable,

    [Parameter()]
    [switch]$Json,

    [Parameter()]
    [ValidateSet("ERROR", "WARN", "INFO", "DEBUG")]
    [string]$TraceLevel = "INFO",

    [Parameter()]
    [switch]$Help
)

# Import utility module
$scriptPath = Split-Path -Parent $MyInvocation.MyCommand.Path
Import-Module "$scriptPath\utility-scripts.psm1" -Force

# Known problematic/duplicate extensions
$script:DuplicateClaudeExtensions = @(
    @{
        Id = "saoudrizwan.claude-dev"
        Name = "Cline (formerly Claude Dev)"
        Reason = "Third-party extension that conflicts with official Claude Code"
    }
)

# Official/recommended extensions
$script:OfficialExtensions = @(
    @{
        Id = "anthropic.claude-code"
        Name = "Claude Code"
        Required = $true
    }
)

<#
.SYNOPSIS
    Gets VS Code version information.
.OUTPUTS
    [hashtable] Version info or null if not installed
#>
function Get-VSCodeVersion {
    param(
        [ValidateSet("ERROR", "WARN", "INFO", "DEBUG")]
        [string]$TraceLevel = "ERROR"
    )

    try {
        $output = & code --version 2>&1
        if ($LASTEXITCODE -eq 0) {
            $lines = $output -split "`n"
            return @{
                Version = $lines[0].Trim()
                Commit = if ($lines.Count -gt 1) { $lines[1].Trim() } else { $null }
                Architecture = if ($lines.Count -gt 2) { $lines[2].Trim() } else { $null }
            }
        }
    } catch {
        Write-Log -Level "DEBUG" -Message "VS Code not found: $_" -TraceLevel $TraceLevel
    }

    return $null
}

<#
.SYNOPSIS
    Gets installed VS Code extensions.
.OUTPUTS
    [array] List of installed extensions with id and version
#>
function Get-InstalledExtensions {
    param(
        [ValidateSet("ERROR", "WARN", "INFO", "DEBUG")]
        [string]$TraceLevel = "ERROR"
    )

    try {
        $output = & code --list-extensions --show-versions 2>&1
        if ($LASTEXITCODE -eq 0) {
            $extensions = @()
            foreach ($line in $output) {
                if ($line -match '^([^@]+)@(.+)$') {
                    $extensions += @{
                        Id = $matches[1].ToLower()
                        Version = $matches[2]
                    }
                }
            }
            return $extensions
        }
    } catch {
        Write-Log -Level "DEBUG" -Message "Failed to list extensions: $_" -TraceLevel $TraceLevel
    }

    return @()
}

<#
.SYNOPSIS
    Gets Claude Code credentials status.
.OUTPUTS
    [hashtable] Credentials info
#>
function Get-ClaudeCredentials {
    param(
        [ValidateSet("ERROR", "WARN", "INFO", "DEBUG")]
        [string]$TraceLevel = "ERROR"
    )

    $result = @{
        HasCredentials = $false
        SubscriptionType = $null
        RateLimitTier = $null
        ExpiresAt = $null
        IsExpired = $false
        Scopes = @()
    }

    $credentialsPath = Join-Path $env:USERPROFILE ".claude\.credentials.json"

    if (Test-Path $credentialsPath) {
        try {
            $creds = Get-Content $credentialsPath -Raw | ConvertFrom-Json

            if ($creds.claudeAiOauth) {
                $oauth = $creds.claudeAiOauth
                $result.HasCredentials = $true
                $result.SubscriptionType = $oauth.subscriptionType
                $result.RateLimitTier = $oauth.rateLimitTier
                $result.Scopes = $oauth.scopes

                if ($oauth.expiresAt) {
                    $expiresAt = [DateTimeOffset]::FromUnixTimeMilliseconds($oauth.expiresAt).LocalDateTime
                    $result.ExpiresAt = $expiresAt
                    $result.IsExpired = $expiresAt -lt (Get-Date)
                }
            }
        } catch {
            Write-Log -Level "DEBUG" -Message "Failed to parse credentials: $_" -TraceLevel $TraceLevel
        }
    }

    return $result
}

<#
.SYNOPSIS
    Checks for duplicate/conflicting Claude extensions.
.OUTPUTS
    [array] List of problematic extensions found
#>
function Get-DuplicateExtensions {
    param(
        [array]$InstalledExtensions,
        [ValidateSet("ERROR", "WARN", "INFO", "DEBUG")]
        [string]$TraceLevel = "ERROR"
    )

    $duplicates = @()

    foreach ($duplicate in $script:DuplicateClaudeExtensions) {
        $installed = $InstalledExtensions | Where-Object { $_.Id -eq $duplicate.Id.ToLower() }
        if ($installed) {
            $duplicates += @{
                Id = $duplicate.Id
                Name = $duplicate.Name
                Version = $installed.Version
                Reason = $duplicate.Reason
            }
        }
    }

    return $duplicates
}

<#
.SYNOPSIS
    Checks for missing required extensions.
.OUTPUTS
    [array] List of missing required extensions
#>
function Get-MissingExtensions {
    param(
        [array]$InstalledExtensions,
        [ValidateSet("ERROR", "WARN", "INFO", "DEBUG")]
        [string]$TraceLevel = "ERROR"
    )

    $missing = @()

    foreach ($ext in $script:OfficialExtensions) {
        if ($ext.Required) {
            $installed = $InstalledExtensions | Where-Object { $_.Id -eq $ext.Id.ToLower() }
            if (-not $installed) {
                $missing += @{
                    Id = $ext.Id
                    Name = $ext.Name
                }
            }
        }
    }

    return $missing
}

<#
.SYNOPSIS
    Uninstalls a VS Code extension.
.PARAMETER ExtensionId
    The extension ID to uninstall
.OUTPUTS
    [bool] True if successful
#>
function Uninstall-Extension {
    param(
        [string]$ExtensionId,
        [ValidateSet("ERROR", "WARN", "INFO", "DEBUG")]
        [string]$TraceLevel = "ERROR"
    )

    Write-Log -Level "INFO" -Message "Uninstalling extension: $ExtensionId" -TraceLevel $TraceLevel

    try {
        & code --uninstall-extension $ExtensionId 2>&1 | ForEach-Object {
            Write-Log -Level "DEBUG" -Message $_ -TraceLevel $TraceLevel
        }
        return ($LASTEXITCODE -eq 0)
    } catch {
        Write-Log -Level "ERROR" -Message "Failed to uninstall $ExtensionId : $_" -TraceLevel $TraceLevel
        return $false
    }
}

<#
.SYNOPSIS
    Installs a VS Code extension.
.PARAMETER ExtensionId
    The extension ID to install
.OUTPUTS
    [bool] True if successful
#>
function Install-Extension {
    param(
        [string]$ExtensionId,
        [ValidateSet("ERROR", "WARN", "INFO", "DEBUG")]
        [string]$TraceLevel = "ERROR"
    )

    Write-Log -Level "INFO" -Message "Installing extension: $ExtensionId" -TraceLevel $TraceLevel

    try {
        & code --install-extension $ExtensionId 2>&1 | ForEach-Object {
            Write-Log -Level "DEBUG" -Message $_ -TraceLevel $TraceLevel
        }
        return ($LASTEXITCODE -eq 0)
    } catch {
        Write-Log -Level "ERROR" -Message "Failed to install $ExtensionId : $_" -TraceLevel $TraceLevel
        return $false
    }
}

<#
.SYNOPSIS
    Runs all health checks and returns results.
.OUTPUTS
    [hashtable] Complete health check results
#>
function Invoke-HealthCheck {
    param(
        [switch]$Fix,
        [ValidateSet("ERROR", "WARN", "INFO", "DEBUG")]
        [string]$TraceLevel = "ERROR"
    )

    $results = @{
        Timestamp = Get-Date -Format "yyyy-MM-dd HH:mm:ss"
        VSCode = @{
            Status = "Unknown"
            Version = $null
            Message = ""
        }
        Extensions = @{
            Status = "Unknown"
            Installed = @()
            Duplicates = @()
            Missing = @()
            Message = ""
            FixesApplied = @()
        }
        Credentials = @{
            Status = "Unknown"
            SubscriptionType = $null
            RateLimitTier = $null
            ExpiresAt = $null
            Message = ""
        }
        Issues = @()
        OverallStatus = "Unknown"
    }

    # Check VS Code
    Write-Log -Level "DEBUG" -Message "Checking VS Code installation..." -TraceLevel $TraceLevel
    $vscodeInfo = Get-VSCodeVersion -TraceLevel $TraceLevel

    if ($vscodeInfo) {
        $results.VSCode.Status = "OK"
        $results.VSCode.Version = $vscodeInfo.Version
        $results.VSCode.Architecture = $vscodeInfo.Architecture
        $results.VSCode.Message = "VS Code $($vscodeInfo.Version) installed"
    } else {
        $results.VSCode.Status = "NotInstalled"
        $results.VSCode.Message = "VS Code is not installed or not in PATH"
        $results.Issues += @{
            Category = "VSCode"
            Severity = "Error"
            Message = "VS Code is not installed"
            Fix = "Install VS Code from https://code.visualstudio.com/"
            CanAutoFix = $false
        }
    }

    # Check extensions (only if VS Code is installed)
    if ($results.VSCode.Status -eq "OK") {
        Write-Log -Level "DEBUG" -Message "Checking extensions..." -TraceLevel $TraceLevel
        $extensions = Get-InstalledExtensions -TraceLevel $TraceLevel
        $results.Extensions.Installed = $extensions

        # Check for duplicates
        $duplicates = Get-DuplicateExtensions -InstalledExtensions $extensions -TraceLevel $TraceLevel
        $results.Extensions.Duplicates = $duplicates

        foreach ($dup in $duplicates) {
            $issue = @{
                Category = "Extensions"
                Severity = "Warning"
                Message = "Duplicate Claude extension found: $($dup.Name) ($($dup.Id))"
                Reason = $dup.Reason
                Fix = "code --uninstall-extension $($dup.Id)"
                CanAutoFix = $true
            }

            if ($Fix) {
                if (Uninstall-Extension -ExtensionId $dup.Id -TraceLevel $TraceLevel) {
                    $issue.Fixed = $true
                    $results.Extensions.FixesApplied += "Uninstalled $($dup.Id)"
                } else {
                    $issue.Fixed = $false
                }
            }

            $results.Issues += $issue
        }

        # Check for missing required extensions
        $missing = Get-MissingExtensions -InstalledExtensions $extensions -TraceLevel $TraceLevel
        $results.Extensions.Missing = $missing

        foreach ($ext in $missing) {
            $issue = @{
                Category = "Extensions"
                Severity = "Error"
                Message = "Required extension missing: $($ext.Name) ($($ext.Id))"
                Fix = "code --install-extension $($ext.Id)"
                CanAutoFix = $true
            }

            if ($Fix) {
                if (Install-Extension -ExtensionId $ext.Id -TraceLevel $TraceLevel) {
                    $issue.Fixed = $true
                    $results.Extensions.FixesApplied += "Installed $($ext.Id)"
                } else {
                    $issue.Fixed = $false
                }
            }

            $results.Issues += $issue
        }

        # Get Claude Code extension version
        $claudeExt = $extensions | Where-Object { $_.Id -eq "anthropic.claude-code" }
        if ($claudeExt) {
            $results.Extensions.ClaudeCodeVersion = $claudeExt.Version
        }

        # Set extension status
        if ($duplicates.Count -eq 0 -and $missing.Count -eq 0) {
            $results.Extensions.Status = "OK"
            $results.Extensions.Message = "$($extensions.Count) extensions installed, no issues"
        } elseif ($results.Extensions.FixesApplied.Count -gt 0) {
            $results.Extensions.Status = "Fixed"
            $results.Extensions.Message = "Issues fixed: $($results.Extensions.FixesApplied -join ', ')"
        } else {
            $results.Extensions.Status = "Issues"
            $results.Extensions.Message = "$($duplicates.Count) duplicates, $($missing.Count) missing"
        }
    }

    # Check credentials
    Write-Log -Level "DEBUG" -Message "Checking Claude credentials..." -TraceLevel $TraceLevel
    $creds = Get-ClaudeCredentials -TraceLevel $TraceLevel

    if ($creds.HasCredentials) {
        $results.Credentials.SubscriptionType = $creds.SubscriptionType
        $results.Credentials.RateLimitTier = $creds.RateLimitTier
        $results.Credentials.ExpiresAt = $creds.ExpiresAt
        $results.Credentials.Scopes = $creds.Scopes

        if ($creds.IsExpired) {
            $results.Credentials.Status = "Expired"
            $results.Credentials.Message = "Credentials expired at $($creds.ExpiresAt)"
            $results.Issues += @{
                Category = "Credentials"
                Severity = "Error"
                Message = "Claude credentials have expired"
                Fix = "Run 'claude auth login' to re-authenticate"
                CanAutoFix = $false
            }
        } else {
            $results.Credentials.Status = "OK"
            $subType = if ($creds.SubscriptionType) { $creds.SubscriptionType } else { "unknown" }
            $results.Credentials.Message = "Logged in ($subType subscription)"
        }
    } else {
        $results.Credentials.Status = "NotAuthenticated"
        $results.Credentials.Message = "Not logged in to Claude"
        $results.Issues += @{
            Category = "Credentials"
            Severity = "Warning"
            Message = "Not authenticated with Claude"
            Fix = "Run 'claude auth login' to authenticate"
            CanAutoFix = $false
        }
    }

    # Determine overall status
    $errorCount = ($results.Issues | Where-Object { $_.Severity -eq "Error" -and -not $_.Fixed }).Count
    $warningCount = ($results.Issues | Where-Object { $_.Severity -eq "Warning" -and -not $_.Fixed }).Count

    if ($errorCount -gt 0) {
        $results.OverallStatus = "Error"
    } elseif ($warningCount -gt 0) {
        $results.OverallStatus = "Warning"
    } else {
        $results.OverallStatus = "OK"
    }

    return $results
}

<#
.SYNOPSIS
    Displays formatted health check output.
.PARAMETER Results
    Health check results hashtable
#>
function Show-HealthCheckOutput {
    param(
        [hashtable]$Results,
        [switch]$Fix
    )

    Write-Host ""
    Write-Host "VS Code Health Check" -ForegroundColor Cyan
    Write-Host "====================" -ForegroundColor Cyan
    Write-Host "Timestamp: $($Results.Timestamp)"
    Write-Host ""

    # VS Code status
    $vscodeColor = if ($Results.VSCode.Status -eq "OK") { "Green" } else { "Red" }
    Write-Host "VS Code" -ForegroundColor White
    Write-Host "  Status:  " -NoNewline
    Write-Host $Results.VSCode.Status -ForegroundColor $vscodeColor
    if ($Results.VSCode.Version) {
        Write-Host "  Version: $($Results.VSCode.Version)"
    }
    Write-Host ""

    # Extensions status
    $extColor = switch ($Results.Extensions.Status) {
        "OK" { "Green" }
        "Fixed" { "Yellow" }
        default { "Red" }
    }
    Write-Host "Extensions" -ForegroundColor White
    Write-Host "  Status:  " -NoNewline
    Write-Host $Results.Extensions.Status -ForegroundColor $extColor
    Write-Host "  Total:   $($Results.Extensions.Installed.Count)"
    if ($Results.Extensions.ClaudeCodeVersion) {
        Write-Host "  Claude Code: v$($Results.Extensions.ClaudeCodeVersion)"
    }
    if ($Results.Extensions.Duplicates.Count -gt 0) {
        Write-Host "  Duplicates: $($Results.Extensions.Duplicates.Count)" -ForegroundColor Yellow
    }
    if ($Results.Extensions.Missing.Count -gt 0) {
        Write-Host "  Missing: $($Results.Extensions.Missing.Count)" -ForegroundColor Red
    }
    Write-Host ""

    # Credentials status
    $credColor = switch ($Results.Credentials.Status) {
        "OK" { "Green" }
        "Expired" { "Red" }
        default { "Yellow" }
    }
    Write-Host "Credentials" -ForegroundColor White
    Write-Host "  Status:  " -NoNewline
    Write-Host $Results.Credentials.Status -ForegroundColor $credColor
    if ($Results.Credentials.SubscriptionType) {
        Write-Host "  Subscription: $($Results.Credentials.SubscriptionType)"
    }
    if ($Results.Credentials.RateLimitTier) {
        Write-Host "  Rate Limit: $($Results.Credentials.RateLimitTier)"
    }
    if ($Results.Credentials.ExpiresAt) {
        Write-Host "  Expires: $($Results.Credentials.ExpiresAt)"
    }
    Write-Host ""

    # Issues
    $unfixedIssues = $Results.Issues | Where-Object { -not $_.Fixed }
    if ($unfixedIssues.Count -gt 0) {
        Write-Host "Issues Found" -ForegroundColor Yellow
        Write-Host "------------" -ForegroundColor Yellow

        foreach ($issue in $unfixedIssues) {
            $sevColor = if ($issue.Severity -eq "Error") { "Red" } else { "Yellow" }
            Write-Host "  [$($issue.Severity)] " -ForegroundColor $sevColor -NoNewline
            Write-Host $issue.Message

            if ($issue.Reason) {
                Write-Host "    Reason: $($issue.Reason)" -ForegroundColor Gray
            }

            if (-not $Fix -and $issue.Fix) {
                if ($issue.CanAutoFix) {
                    Write-Host "    Fix: " -NoNewline -ForegroundColor Gray
                    Write-Host $issue.Fix -ForegroundColor Cyan
                    Write-Host "    (or run with -Fix to auto-fix)" -ForegroundColor Gray
                } else {
                    Write-Host "    Fix: " -NoNewline -ForegroundColor Gray
                    Write-Host $issue.Fix -ForegroundColor Cyan
                }
            }
        }
        Write-Host ""
    }

    # Fixed issues
    $fixedIssues = $Results.Issues | Where-Object { $_.Fixed }
    if ($fixedIssues.Count -gt 0) {
        Write-Host "Issues Fixed" -ForegroundColor Green
        Write-Host "------------" -ForegroundColor Green
        foreach ($issue in $fixedIssues) {
            Write-Host "  [Fixed] $($issue.Message)" -ForegroundColor Green
        }
        Write-Host ""
    }

    # Overall status
    $overallColor = switch ($Results.OverallStatus) {
        "OK" { "Green" }
        "Warning" { "Yellow" }
        default { "Red" }
    }
    Write-Host "Overall: " -NoNewline
    Write-Host $Results.OverallStatus -ForegroundColor $overallColor
    Write-Host ""
}

<#
.SYNOPSIS
    Displays help information.
#>
function Show-Help {
    $help = @"
VS Code Health Check Script
============================

Diagnoses VS Code installation, extensions, Claude Code credentials, and common
configuration problems.

Usage:
    vs-code-health.ps1 -Doctor [-Fix] [-HashTable] [-Json] [-TraceLevel <level>]
    vs-code-health.ps1 -Help

Operations:
    -Doctor     Runs all health checks and displays results
    -Help       Shows this help message

Options:
    -Fix        Automatically fixes problems that can be resolved
    -HashTable  Returns results as PowerShell hashtable
    -Json       Returns results as JSON string
    -TraceLevel Sets output detail level (ERROR, WARN, INFO, DEBUG)

Checks Performed:
    VS Code
      - Installation status
      - Version information

    Extensions
      - Duplicate Claude extensions (e.g., Cline vs official Claude Code)
      - Missing required extensions
      - Extension versions

    Credentials
      - Claude authentication status
      - Subscription type (Free, Pro, Max)
      - Token expiration

Auto-Fixable Issues:
    - Duplicate extensions (uninstalls conflicting extensions)
    - Missing required extensions (installs them)

Manual Fixes Required:
    - VS Code not installed
    - Claude authentication (requires interactive login)

Examples:
    # Run health check
    vs-code-health.ps1 -Doctor

    # Run health check and auto-fix issues
    vs-code-health.ps1 -Doctor -Fix

    # Get results as JSON for scripting
    vs-code-health.ps1 -Doctor -Json

    # Verbose output
    vs-code-health.ps1 -Doctor -TraceLevel DEBUG
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
    if (-not $Doctor) {
        Show-Help
        exit 0
    }

    if ($Doctor) {
        $results = Invoke-HealthCheck -Fix:$Fix -TraceLevel $TraceLevel

        if ($HashTable) {
            return $results
        } elseif ($Json) {
            return ($results | ConvertTo-Json -Depth 10)
        } else {
            Show-HealthCheckOutput -Results $results -Fix:$Fix

            # Exit code based on status
            switch ($results.OverallStatus) {
                "OK" { exit 0 }
                "Warning" { exit 0 }
                default { exit 1 }
            }
        }
    }

    exit 0
} catch {
    Write-Log -Level "ERROR" -Message "An error occurred: $_" -TraceLevel $TraceLevel
    exit 1
}
