<#
.SYNOPSIS
    Systematic test suite for catan.ps1 and related scripts.
.DESCRIPTION
    Tests all catan.ps1 commands to ensure they work correctly.
    Azure tests run last (optionally async) since they take longer.
.PARAMETER SkipAzure
    Skip Azure tests entirely
.PARAMETER SkipBuild
    Skip build/test commands (faster iteration)
#>

param(
    [switch]$SkipAzure,
    [switch]$SkipBuild
)

$ErrorActionPreference = "Continue"
$scriptRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
$repoRoot = Split-Path $scriptRoot -Parent
$catanScript = Join-Path $repoRoot "catan.ps1"

$passed = 0
$failed = 0
$skipped = 0

function Test-Command {
    param(
        [string]$Name,
        [scriptblock]$Command,
        [switch]$ShouldFail,
        [switch]$Skip
    )

    if ($Skip) {
        Write-Host "  [SKIP] $Name" -ForegroundColor Yellow
        $script:skipped++
        return $true
    }

    Write-Host "  $Name... " -ForegroundColor Gray -NoNewline

    try {
        & $Command *> $null
        $exitCode = $LASTEXITCODE

        if ($ShouldFail) {
            if ($exitCode -ne 0) {
                Write-Host "PASS (expected failure)" -ForegroundColor Green
                $script:passed++
                return $true
            } else {
                Write-Host "FAIL (expected exit code != 0)" -ForegroundColor Red
                $script:failed++
                return $false
            }
        } else {
            if ($exitCode -eq 0 -or $null -eq $exitCode) {
                Write-Host "PASS" -ForegroundColor Green
                $script:passed++
                return $true
            } else {
                Write-Host "FAIL (exit code: $exitCode)" -ForegroundColor Red
                $script:failed++
                return $false
            }
        }
    }
    catch {
        Write-Host "ERROR: $_" -ForegroundColor Red
        $script:failed++
        return $false
    }
}

Write-Host ""
Write-Host "Catan.ps1 Test Suite" -ForegroundColor Cyan
Write-Host "====================" -ForegroundColor Cyan
Write-Host ""

# ============================================
# HELP & BASIC COMMANDS
# ============================================
Write-Host "1. Help & Basic Commands" -ForegroundColor Yellow

Test-Command "help verb" { & $catanScript help }
Test-Command "-Help switch" { & $catanScript -Help }
Test-Command "Unknown argument shows help" { & $catanScript run -Netork }

Write-Host ""

# ============================================
# DOCTOR COMMANDS
# ============================================
Write-Host "2. Doctor Commands" -ForegroundColor Yellow

Test-Command "doctor (top-level)" { & $catanScript doctor }
Test-Command "database doctor" { & $catanScript database doctor }
Test-Command "database (shows help)" { & $catanScript database }

Write-Host ""

# ============================================
# BUILD & TEST COMMANDS
# ============================================
Write-Host "3. Build & Test Commands" -ForegroundColor Yellow

Test-Command "build" -Skip:$SkipBuild { & $catanScript build }
Test-Command "test" -Skip:$SkipBuild { & $catanScript test }

Write-Host ""

# ============================================
# DATABASE COMMANDS
# ============================================
Write-Host "4. Database Commands" -ForegroundColor Yellow

Test-Command "database install (no force, already installed)" { & $catanScript database install }
Test-Command "database install -Force" { & $catanScript database install -Force }
Test-Command "database doctor (verify after install)" { & $catanScript database doctor }

Write-Host ""

# ============================================
# SERVICE COMMANDS
# ============================================
Write-Host "5. Service Commands" -ForegroundColor Yellow

Test-Command "stop" { & $catanScript stop }

Write-Host ""

# ============================================
# CLEAN COMMANDS
# ============================================
Write-Host "6. Clean Commands" -ForegroundColor Yellow

Test-Command "clean (preserves database)" -Skip:$SkipBuild { & $catanScript clean }

Write-Host ""

# ============================================
# INSTALL COMMANDS
# ============================================
Write-Host "7. Install Commands" -ForegroundColor Yellow

Test-Command "install (top-level)" { & $catanScript install }

Write-Host ""

# ============================================
# DEPENDENCY SCRIPTS
# ============================================
Write-Host "8. Dependency Scripts" -ForegroundColor Yellow

Test-Command "install-dependencies -Doctor" { & "$scriptRoot/install-dependencies.ps1" -Doctor }
Test-Command "dotnet.ps1 -Doctor" { & "$scriptRoot/dotnet.ps1" -Doctor }
Test-Command "node.ps1 -Doctor" { & "$scriptRoot/node.ps1" -Doctor }
Test-Command "claude-cli.ps1 -Doctor" { & "$scriptRoot/claude-cli.ps1" -Doctor }

Write-Host ""

# ============================================
# AZURE COMMANDS
# ============================================
Write-Host "9. Azure Commands" -ForegroundColor Yellow

Test-Command "azure (shows help)" -Skip:$SkipAzure { & $catanScript azure }
Test-Command "azure doctor" -Skip:$SkipAzure { & $catanScript azure doctor }

Write-Host ""

# ============================================
# FINAL VERIFICATION
# ============================================
Write-Host "10. Final Verification" -ForegroundColor Yellow

Test-Command "database doctor (final)" { & $catanScript database doctor }
Test-Command "doctor (final)" { & $catanScript doctor }

Write-Host ""

# ============================================
# SUMMARY
# ============================================
Write-Host "============================================" -ForegroundColor Cyan
Write-Host "Test Summary" -ForegroundColor Cyan
Write-Host "============================================" -ForegroundColor Cyan
Write-Host "  Passed:  $passed" -ForegroundColor Green
Write-Host "  Failed:  $failed" -ForegroundColor $(if ($failed -gt 0) { "Red" } else { "Green" })
Write-Host "  Skipped: $skipped" -ForegroundColor Yellow
Write-Host ""

if ($failed -gt 0) {
    Write-Host "Some tests failed!" -ForegroundColor Red
    exit 1
} else {
    Write-Host "All tests passed! System is ready to use." -ForegroundColor Green
    exit 0
}
