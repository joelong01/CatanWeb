#!/usr/bin/env pwsh
<#
.SYNOPSIS
    Auto-format source files in the Catan project.

.DESCRIPTION
    Formats uncommitted changed files by default.
    Uses Prettier for TypeScript/JavaScript/CSS/JSON/Markdown in react-ui,
    and dotnet format for C# files.

.PARAMETER All
    Format all files, not just changed ones.

.PARAMETER Check
    Check formatting without writing changes (exit 1 if unformatted).

.PARAMETER Type
    Format only specific file types: cs, ts, or all.

.EXAMPLE
    ./.scripts/format.ps1              # Format changed files
    ./.scripts/format.ps1 -All         # Format entire codebase
    ./.scripts/format.ps1 -Check       # Check only (no writes)
    ./.scripts/format.ps1 -Type ts     # Format only TypeScript/web files
#>

[CmdletBinding()]
param(
    [switch]$All,
    [switch]$Check,
    [ValidateSet("all", "cs", "ts")]
    [string]$Type = "all"
)

$ErrorActionPreference = "Stop"
$projectRoot = Split-Path $PSScriptRoot -Parent
$reactUiPath = Join-Path $projectRoot "react-ui"

# Track results
$script:totalFormatted = 0
$script:totalFailed = 0
$script:failedFormatters = @()

function Write-Header {
    param([string]$Text)
    Write-Host ""
    Write-Host "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━" -ForegroundColor DarkGray
    Write-Host "  $Text" -ForegroundColor Cyan
    Write-Host "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━" -ForegroundColor DarkGray
}

function Write-SubHeader {
    param([string]$Text)
    Write-Host ""
    Write-Host "  $Text" -ForegroundColor Yellow
    Write-Host "  $("-" * $Text.Length)" -ForegroundColor DarkGray
}

function Write-Success {
    param([string]$Text)
    Write-Host "  ✓ $Text" -ForegroundColor Green
}

function Write-Warning {
    param([string]$Text)
    Write-Host "  ⚠ $Text" -ForegroundColor Yellow
}

function Write-Error {
    param([string]$Text)
    Write-Host "  ✗ $Text" -ForegroundColor Red
}

function Write-Info {
    param([string]$Text)
    Write-Host "  $Text" -ForegroundColor Gray
}

function Get-ChangedFiles {
    Push-Location $projectRoot
    try {
        $staged = git diff --cached --name-only 2>$null
        $unstaged = git diff --name-only 2>$null
        $untracked = git ls-files --others --exclude-standard 2>$null

        $allChanged = @()
        if ($staged) { $allChanged += $staged }
        if ($unstaged) { $allChanged += $unstaged }
        if ($untracked) { $allChanged += $untracked }

        $allChanged | Sort-Object -Unique | ForEach-Object {
            Join-Path $projectRoot $_
        } | Where-Object { Test-Path $_ }
    }
    finally {
        Pop-Location
    }
}

function Get-FilesByExtension {
    param(
        [string[]]$Files,
        [string[]]$Extensions
    )
    $Files | Where-Object {
        $ext = [System.IO.Path]::GetExtension($_).ToLower()
        $Extensions -contains $ext
    }
}

function Invoke-EnsureNodeModules {
    <#
    .SYNOPSIS
        Ensures react-ui node_modules are installed. Runs npm install if needed.
    .OUTPUTS
        [bool] True if node_modules are available.
    #>
    if (-not (Test-Path $reactUiPath)) { return $false }

    $nodeModulesPath = Join-Path $reactUiPath "node_modules"
    $packageJsonPath = Join-Path $reactUiPath "package.json"

    $needsInstall = $false
    if (-not (Test-Path $nodeModulesPath)) {
        Write-Info "node_modules not found. Running npm install..."
        $needsInstall = $true
    }
    elseif ((Get-Item $packageJsonPath).LastWriteTime -gt (Get-Item $nodeModulesPath).LastWriteTime) {
        Write-Info "package.json is newer than node_modules. Running npm install..."
        $needsInstall = $true
    }

    if ($needsInstall) {
        Push-Location $reactUiPath
        try {
            & npm install
            if ($LASTEXITCODE -ne 0) {
                Write-Error "npm install failed"
                return $false
            }
        }
        finally {
            Pop-Location
        }
    }

    return $true
}

function Invoke-PrettierFormat {
    param([string[]]$Files)

    if (-not (Test-Path $reactUiPath)) {
        Write-Info "react-ui directory not found, skipping Prettier"
        return
    }

    # Ensure node_modules are installed
    if (-not (Invoke-EnsureNodeModules)) {
        $script:failedFormatters += "Prettier"
        Write-Error "Cannot run Prettier — npm install failed"
        return
    }

    # Filter to Prettier-eligible files inside react-ui
    $webFiles = $Files | Where-Object {
        $_ -like "*react-ui*" -and
        ($_ -match "\.(ts|tsx|js|jsx|mjs|css|scss|json|md)$") -and
        $_ -notmatch "node_modules|\.next|\\out\\|\\build\\|types[\\/]generated|package-lock\.json"
    }

    if ($webFiles.Count -eq 0) {
        Write-Info "No web files to format"
        return
    }

    Write-SubHeader "Prettier ($($webFiles.Count) files)"

    Push-Location $reactUiPath
    try {
        # Use "." for large file counts to avoid Windows command line length limits
        if ($webFiles.Count -gt 50) {
            $prettierTargets = @(".")
        }
        else {
            $prettierTargets = @($webFiles | ForEach-Object {
                $_.Replace("$reactUiPath\", "").Replace("$reactUiPath/", "")
            })
        }

        $prettierAction = if ($Check) { "--check" } else { "--write" }
        Write-Info "Running prettier $prettierAction..."
        $output = & npx prettier $prettierAction @prettierTargets 2>&1

        $prettierExit = $LASTEXITCODE

        if ($prettierExit -eq 0) {
            if ($Check) {
                Write-Success "All files are formatted correctly"
            }
            else {
                Write-Success "Prettier formatting complete"
            }
        }
        else {
            if ($Check) {
                # --check exits 1 when files need formatting
                # Strip ANSI escape codes before matching (Prettier v3 outputs colored [warn])
                $unformatted = @($output | Where-Object {
                    $stripped = $_ -replace '\x1b\[[0-9;]*m', ''
                    $stripped -match "\[warn\]" -and $stripped -notmatch "Code style"
                })
                $count = $unformatted.Count
                $script:totalFailed += $count
                $script:failedFormatters += "Prettier"
                Write-Error "$count file(s) need formatting"
                $unformatted | Select-Object -First 10 | ForEach-Object {
                    $clean = $_ -replace '\x1b\[[0-9;]*m', ''
                    Write-Host "    $clean" -ForegroundColor Gray
                }
                if ($count -gt 10) {
                    Write-Info "    ... and $($count - 10) more"
                }
            }
            else {
                # --write shouldn't normally fail, but handle it
                $script:failedFormatters += "Prettier"
                Write-Error "Prettier encountered errors"
                $output | Select-Object -First 5 | ForEach-Object {
                    Write-Host "    $_" -ForegroundColor Gray
                }
            }
        }
    }
    finally {
        Pop-Location
    }
}

function Invoke-DotnetFormat {
    param([string[]]$Files)

    $csFiles = $Files | Where-Object {
        $_ -match "\.cs$" -and
        $_ -notmatch "\\bin\\|\\obj\\|\\\.git\\"
    }

    if ($csFiles.Count -eq 0) {
        Write-Info "No C# files to format"
        return
    }

    Write-SubHeader "dotnet format ($($csFiles.Count) files)"

    Push-Location $projectRoot
    try {
        $formatArgs = @("format", "Catan.sln", "--verbosity", "quiet")

        if ($Check) {
            $formatArgs += "--verify-no-changes"
        }

        if (-not $All) {
            # dotnet format supports --include for specific files
            $relativePaths = $csFiles | ForEach-Object {
                $_.Replace("$projectRoot\", "").Replace("$projectRoot/", "")
            }
            foreach ($path in $relativePaths) {
                $formatArgs += "--include"
                $formatArgs += $path
            }
        }

        Write-Info "Running dotnet format$(if ($Check) { ' --verify-no-changes' } else { '' })..."
        $output = & dotnet @formatArgs 2>&1
        $formatExit = $LASTEXITCODE

        if ($formatExit -eq 0) {
            if ($Check) {
                Write-Success "All C# files are formatted correctly"
            }
            else {
                Write-Success "C# formatting complete"
            }
        }
        else {
            $script:totalFailed++
            $script:failedFormatters += "dotnet format"
            if ($Check) {
                Write-Error "C# files need formatting"
            }
            else {
                Write-Error "dotnet format encountered errors"
            }
            $output | Select-Object -First 5 | ForEach-Object {
                Write-Host "    $_" -ForegroundColor Gray
            }
        }
    }
    finally {
        Pop-Location
    }
}

# =============================================================================
# Main Execution
# =============================================================================

Write-Header "Catan Project Formatter"

# Determine which files to format
if ($All) {
    Write-Info "Mode: Formatting ALL files"
    $targetFiles = Get-ChildItem -Path $projectRoot -Recurse -File |
        Where-Object {
            $_.FullName -notmatch "node_modules|\\bin\\|\\obj\\|\\.git\\|\\.next\\|\\out\\|\\build\\" -and
            $_.Extension -match "\.(cs|ts|tsx|js|jsx|mjs|css|scss|json|md)$"
        } |
        Select-Object -ExpandProperty FullName
}
else {
    Write-Info "Mode: Formatting CHANGED files only"
    $targetFiles = Get-ChangedFiles

    if ($targetFiles.Count -eq 0) {
        Write-Success "No uncommitted changes to format"
        Write-Host ""
        exit 0
    }
}

Write-Info "Found $($targetFiles.Count) file(s)"
if ($Check) {
    Write-Info "Mode: CHECK only (no writes)"
}

# Run formatters based on Type parameter
switch ($Type) {
    "cs" {
        Invoke-DotnetFormat -Files $targetFiles
    }
    "ts" {
        Invoke-PrettierFormat -Files $targetFiles
    }
    "all" {
        Invoke-PrettierFormat -Files $targetFiles
        Invoke-DotnetFormat -Files $targetFiles
    }
}

# Summary
Write-Header "Summary"

if ($script:totalFailed -eq 0 -and $script:failedFormatters.Count -eq 0) {
    if ($Check) {
        Write-Success "All files are correctly formatted!"
    }
    else {
        Write-Success "Formatting complete!"
    }
    Write-Host ""
    exit 0
}
else {
    Write-Error "Formatting issues in: $($script:failedFormatters -join ', ')"
    Write-Host ""
    if ($Check) {
        Write-Host "  To fix: ./catan.ps1 format$(if ($Type -ne 'all') { " $Type" })" -ForegroundColor Yellow
    }
    Write-Host ""
    exit 1
}
