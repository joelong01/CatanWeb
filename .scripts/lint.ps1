#!/usr/bin/env pwsh
<#
.SYNOPSIS
    Smart linting for changed files in the Catan project.

.DESCRIPTION
    Analyzes uncommitted changes and lints only the affected files.
    Supports C#, TypeScript/JavaScript, Markdown, JSON, and spell checking.
    Uses --fix where available to auto-correct issues.

.PARAMETER All
    Lint all files, not just changed ones.

.PARAMETER Fix
    Apply automatic fixes where possible (default: true).

.PARAMETER NoFix
    Disable automatic fixes (report only).

.PARAMETER Type
    Lint only specific file types: cs, ts, md, json, ps1, spell, or all.

.EXAMPLE
    ./.scripts/lint.ps1              # Lint changed files with auto-fix
    ./.scripts/lint.ps1 -NoFix       # Report issues without fixing
    ./.scripts/lint.ps1 -All         # Lint entire codebase
    ./.scripts/lint.ps1 -Type ts     # Lint only TypeScript files
#>

[CmdletBinding()]
param(
    [switch]$All,
    [switch]$NoFix,
    [ValidateSet("all", "cs", "ts", "md", "json", "ps1", "spell")]
    [string]$Type = "all"
)

$ErrorActionPreference = "Stop"
$projectRoot = Split-Path $PSScriptRoot -Parent
$reactUiPath = Join-Path $projectRoot "react-ui"

# Track results
$script:totalIssues = 0
$script:totalFixed = 0
$script:failedLinters = @()

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
    <#
    .SYNOPSIS
        Gets list of uncommitted changed files from git.
    #>
    Push-Location $projectRoot
    try {
        # Get staged and unstaged changes
        $staged = git diff --cached --name-only 2>$null
        $unstaged = git diff --name-only 2>$null
        $untracked = git ls-files --others --exclude-standard 2>$null

        $allChanged = @()
        if ($staged) { $allChanged += $staged }
        if ($unstaged) { $allChanged += $unstaged }
        if ($untracked) { $allChanged += $untracked }

        # Remove duplicates and return full paths
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

function Invoke-CSharpLint {
    param([string[]]$Files)

    if ($Files.Count -eq 0) {
        Write-Info "No C# files to lint"
        return
    }

    Write-SubHeader "C# Linting ($($Files.Count) files)"

    # For C#, we use dotnet build with warnings as errors
    # Filter to only files in solution directories
    $solutionFiles = $Files | Where-Object {
        $_ -match "Catan3\.|Tests\.|WebUI|DesktopApp" -and
        $_ -notmatch "\\bin\\" -and
        $_ -notmatch "\\obj\\"
    }

    if ($solutionFiles.Count -eq 0) {
        Write-Info "No solution C# files changed"
        return
    }

    Write-Info "Checking via dotnet build..."

    Push-Location $projectRoot
    try {
        $buildOutput = & dotnet build Catan.sln --verbosity quiet --no-restore 2>&1
        if ($LASTEXITCODE -eq 0) {
            Write-Success "C# compilation clean (no warnings/errors)"
        }
        else {
            $script:totalIssues++
            $script:failedLinters += "C#"
            Write-Error "C# build has issues:"
            $buildOutput | Where-Object { $_ -match "error|warning" } | ForEach-Object {
                Write-Host "    $_" -ForegroundColor Gray
            }
        }
    }
    finally {
        Pop-Location
    }
}

function Invoke-TypeScriptLint {
    param([string[]]$Files)

    if (-not (Test-Path $reactUiPath)) {
        Write-Info "react-ui directory not found, skipping TypeScript lint"
        return
    }

    # Filter to react-ui files
    $tsFiles = $Files | Where-Object {
        $_ -like "*react-ui*" -and
        ($_ -match "\.(ts|tsx|js|jsx|mjs)$")
    }

    if ($tsFiles.Count -eq 0) {
        Write-Info "No TypeScript/JavaScript files to lint"
        return
    }

    Write-SubHeader "TypeScript/ESLint ($($tsFiles.Count) files)"

    Push-Location $reactUiPath
    try {
        # Get relative paths for eslint
        $relativePaths = $tsFiles | ForEach-Object {
            $_.Replace("$reactUiPath\", "").Replace("$reactUiPath/", "")
        }

        $fixArg = if (-not $NoFix) { "--fix" } else { "" }

        Write-Info "Running ESLint$(if ($fixArg) { ' with --fix' } else { '' })..."

        # Run eslint on specific files
        # --no-warn-ignored suppresses "File ignored because of a matching ignore pattern" warnings
        $eslintArgs = @($relativePaths) + @("--format", "stylish", "--no-warn-ignored")
        if ($fixArg) { $eslintArgs += "--fix" }

        $eslintOutput = & npx eslint @eslintArgs 2>&1
        $eslintExit = $LASTEXITCODE

        if ($eslintExit -eq 0) {
            Write-Success "ESLint clean"
            if (-not $NoFix) {
                Write-Info "(auto-fixes applied if any)"
            }
        }
        else {
            # Count errors vs warnings
            $errorCount = ($eslintOutput | Select-String -Pattern "\d+ error" -AllMatches).Matches.Count
            $warningCount = ($eslintOutput | Select-String -Pattern "\d+ warning" -AllMatches).Matches.Count

            if ($errorCount -gt 0) {
                $script:totalIssues += $errorCount
                $script:failedLinters += "ESLint"
                Write-Error "ESLint found $errorCount error(s), $warningCount warning(s)"
            }
            else {
                Write-Warning "ESLint found $warningCount warning(s) (no errors)"
            }

            # Show summary of issues
            $eslintOutput | Select-String -Pattern "^\s+\d+:\d+" | Select-Object -First 10 | ForEach-Object {
                Write-Host "    $_" -ForegroundColor Gray
            }
            if (($eslintOutput | Select-String -Pattern "^\s+\d+:\d+").Count -gt 10) {
                Write-Info "    ... and more (run manually for full output)"
            }
        }
    }
    finally {
        Pop-Location
    }
}

function Invoke-MarkdownLint {
    param([string[]]$Files)

    $mdFiles = $Files | Where-Object { $_ -match "\.md$" }

    if ($mdFiles.Count -eq 0) {
        Write-Info "No Markdown files to lint"
        return
    }

    Write-SubHeader "Markdown Linting ($($mdFiles.Count) files)"

    $fixArg = if (-not $NoFix) { "--fix" } else { "" }

    Write-Info "Running markdownlint$(if ($fixArg) { ' with --fix' } else { '' })..."

    # Filter out node_modules, bin, obj directories
    $mdFiles = $mdFiles | Where-Object {
        $_ -notmatch "node_modules|\\bin\\|\\obj\\"
    }

    if ($mdFiles.Count -eq 0) {
        Write-Info "No Markdown files to lint (after filtering)"
        return
    }

    Push-Location $projectRoot
    try {
        # Run markdownlint on all files at once using glob pattern or file list
        # This is more efficient and avoids per-file issues
        $relativePaths = $mdFiles | ForEach-Object {
            $_.Replace("$projectRoot\", "").Replace("$projectRoot/", "").Replace("\", "/")
        }

        if ($fixArg) {
            # Run with fix first - use Start-Process to avoid encoding issues
            $null = & npx markdownlint-cli @relativePaths --fix --dot 2>&1
        }

        # Then check for remaining issues
        $mdOutput = & npx markdownlint-cli @relativePaths --dot 2>&1
        $mdExit = $LASTEXITCODE

        if ($mdExit -eq 0) {
            Write-Success "Markdown files are clean"
        }
        else {
            # Parse output to count issues and display them
            $issueLines = $mdOutput | Where-Object { $_ -match "^[^:]+:\d+" }
            $issueCount = $issueLines.Count

            if ($issueCount -gt 0) {
                $script:totalIssues += $issueCount
                $script:failedLinters += "Markdown"

                # Group by file for cleaner output
                $byFile = $issueLines | Group-Object { ($_ -split ":")[0] }
                foreach ($group in $byFile | Select-Object -First 10) {
                    $relativePath = $group.Name
                    Write-Warning "$relativePath has $($group.Count) issue(s)"
                    $group.Group | Select-Object -First 3 | ForEach-Object {
                        Write-Host "      $_" -ForegroundColor Gray
                    }
                }
                if ($byFile.Count -gt 10) {
                    Write-Info "    ... and $($byFile.Count - 10) more files with issues"
                }
                Write-Error "$issueCount markdown issue(s) remain after$(if ($fixArg) { ' auto-fix' } else { '' })"
            }
            else {
                # Non-zero exit but no parse-able issues - might be usage error
                Write-Warning "markdownlint returned non-zero but no issues found in output"
                $mdOutput | Select-Object -First 5 | ForEach-Object {
                    Write-Host "      $_" -ForegroundColor Gray
                }
            }
        }
    }
    finally {
        Pop-Location
    }
}

function Invoke-SpellCheck {
    param([string[]]$Files)

    # Spell check always runs on changed files only, even with -All
    # This is because the historical codebase has many technical terms not in dictionary
    # We only want to catch spelling issues in new/modified content
    $changedFiles = Get-ChangedFiles

    # Filter to text-based changed files
    $textFiles = $changedFiles | Where-Object {
        $_ -match "\.(ts|tsx|js|jsx|md|cs|json|ps1)$" -and
        $_ -notmatch "node_modules|\\bin\\|\\obj\\|package-lock\.json"
    }

    if ($textFiles.Count -eq 0) {
        Write-Info "No changed files to spell check"
        return
    }

    Write-SubHeader "Spell Checking ($($textFiles.Count) changed files)"

    Write-Info "Running cspell on changed files only..."

    Push-Location $projectRoot
    try {
        # Convert to relative paths for cspell
        $relativePaths = $textFiles | ForEach-Object {
            $_.Replace("$projectRoot\", "").Replace("$projectRoot/", "").Replace("\", "/")
        }

        # For large file counts, batch to avoid command line limits
        if ($relativePaths.Count -gt 50) {
            Write-Warning "Many changed files ($($relativePaths.Count)), batching spell check"
            $cspellOutput = @()
            $batches = [Math]::Ceiling($relativePaths.Count / 50)
            for ($i = 0; $i -lt $batches; $i++) {
                $batch = $relativePaths | Select-Object -Skip ($i * 50) -First 50
                $batchOutput = & npx cspell @batch --no-progress --no-summary 2>&1
                $cspellOutput += $batchOutput
            }
            $cspellExit = if ($cspellOutput | Where-Object { $_ -match "Unknown word" }) { 1 } else { 0 }
        }
        else {
            $cspellOutput = & npx cspell @relativePaths --no-progress --no-summary 2>&1
            $cspellExit = $LASTEXITCODE
        }

        if ($cspellExit -eq 0) {
            Write-Success "No spelling issues found in changed files"
        }
        else {
            $issueLines = $cspellOutput | Where-Object { $_ -match "Unknown word" }
            $issueCount = $issueLines.Count

            if ($issueCount -gt 0) {
                $script:totalIssues += $issueCount
                $script:failedLinters += "Spelling"
                Write-Error "$issueCount spelling issue(s) found"
                $issueLines | Select-Object -First 5 | ForEach-Object {
                    Write-Host "    $_" -ForegroundColor Gray
                }
                if ($issueCount -gt 5) {
                    Write-Info "    ... and $($issueCount - 5) more"
                }
                Write-Info "Fix: correct spelling or add to cspell.json dictionary"
            }
        }
    }
    finally {
        Pop-Location
    }
}

function Invoke-PowerShellLint {
    param([string[]]$Files)

    $ps1Files = $Files | Where-Object {
        $_ -match "\.ps1$" -and
        $_ -notmatch "node_modules|\\bin\\|\\obj\\"
    }

    if ($ps1Files.Count -eq 0) {
        Write-Info "No PowerShell files to lint"
        return
    }

    Write-SubHeader "PowerShell Linting ($($ps1Files.Count) files)"

    # Check if PSScriptAnalyzer is available
    if (-not (Get-Module -ListAvailable -Name PSScriptAnalyzer)) {
        Write-Warning "PSScriptAnalyzer not installed, skipping"
        Write-Info "Install with: Install-Module PSScriptAnalyzer -Scope CurrentUser"
        return
    }

    $settingsPath = Join-Path $PSScriptRoot "PSScriptAnalyzerSettings.psd1"
    $settingsArg = if (Test-Path $settingsPath) { @{ Settings = $settingsPath } } else { @{} }

    $allIssues = @()
    foreach ($file in $ps1Files) {
        $issues = Invoke-ScriptAnalyzer -Path $file @settingsArg
        if ($issues) {
            $allIssues += $issues
        }
    }

    if ($allIssues.Count -eq 0) {
        Write-Success "PowerShell scripts are clean"
    }
    else {
        $script:totalIssues += $allIssues.Count
        $script:failedLinters += "PowerShell"
        Write-Error "$($allIssues.Count) PowerShell issue(s) found"
        $allIssues | Select-Object -First 5 | ForEach-Object {
            Write-Host "    $($_.ScriptName):$($_.Line) - $($_.Message)" -ForegroundColor Gray
        }
    }
}

function Invoke-JsonLint {
    param([string[]]$Files)

    $jsonFiles = $Files | Where-Object {
        $_ -match "\.json$" -and
        $_ -notmatch "node_modules|\\bin\\|\\obj\\|package-lock\.json"
    }

    if ($jsonFiles.Count -eq 0) {
        Write-Info "No JSON files to lint"
        return
    }

    Write-SubHeader "JSON Validation ($($jsonFiles.Count) files)"

    $invalidCount = 0
    foreach ($file in $jsonFiles) {
        try {
            $null = Get-Content $file -Raw | ConvertFrom-Json
        }
        catch {
            $invalidCount++
            $relativePath = $file.Replace("$projectRoot\", "").Replace("$projectRoot/", "")
            Write-Error "$relativePath is invalid JSON"
            Write-Host "    $($_.Exception.Message)" -ForegroundColor Gray
        }
    }

    if ($invalidCount -eq 0) {
        Write-Success "All JSON files are valid"
    }
    else {
        $script:totalIssues += $invalidCount
        $script:failedLinters += "JSON"
    }
}

# =============================================================================
# Main Execution
# =============================================================================

Write-Header "Catan Project Linter"

# Determine which files to lint
if ($All) {
    Write-Info "Mode: Linting ALL files"
    # Get all relevant files
    $changedFiles = Get-ChildItem -Path $projectRoot -Recurse -File |
        Where-Object {
            $_.FullName -notmatch "node_modules|\\bin\\|\\obj\\|\\.git\\" -and
            $_.Extension -match "\.(cs|ts|tsx|js|jsx|mjs|md|json|ps1)$"
        } |
        Select-Object -ExpandProperty FullName
}
else {
    Write-Info "Mode: Linting CHANGED files only"
    $changedFiles = Get-ChangedFiles

    if ($changedFiles.Count -eq 0) {
        Write-Success "No uncommitted changes to lint"
        Write-Host ""
        exit 0
    }
}

Write-Info "Found $($changedFiles.Count) file(s) to check"
if (-not $NoFix) {
    Write-Info "Auto-fix: ENABLED (use -NoFix to disable)"
}
else {
    Write-Info "Auto-fix: DISABLED (report only)"
}

# Run linters based on Type parameter
switch ($Type) {
    "cs" {
        Invoke-CSharpLint -Files $changedFiles
    }
    "ts" {
        Invoke-TypeScriptLint -Files $changedFiles
    }
    "md" {
        Invoke-MarkdownLint -Files $changedFiles
    }
    "json" {
        Invoke-JsonLint -Files $changedFiles
    }
    "ps1" {
        Invoke-PowerShellLint -Files $changedFiles
    }
    "spell" {
        Invoke-SpellCheck -Files $changedFiles
    }
    "all" {
        Invoke-CSharpLint -Files (Get-FilesByExtension -Files $changedFiles -Extensions @(".cs"))
        Invoke-TypeScriptLint -Files $changedFiles
        Invoke-MarkdownLint -Files (Get-FilesByExtension -Files $changedFiles -Extensions @(".md"))
        Invoke-JsonLint -Files (Get-FilesByExtension -Files $changedFiles -Extensions @(".json"))
        Invoke-PowerShellLint -Files (Get-FilesByExtension -Files $changedFiles -Extensions @(".ps1"))
        Invoke-SpellCheck -Files $changedFiles
    }
}

# Summary
Write-Header "Summary"

if ($script:totalIssues -eq 0) {
    Write-Success "All checks passed!"
    Write-Host ""
    exit 0
}
else {
    Write-Error "$($script:totalIssues) issue(s) found in: $($script:failedLinters -join ', ')"
    Write-Host ""
    Write-Host "  To fix:" -ForegroundColor Yellow
    if ($script:failedLinters -contains "ESLint") {
        Write-Host "    • ESLint: cd react-ui && npm run lint -- --fix" -ForegroundColor Gray
    }
    if ($script:failedLinters -contains "Markdown") {
        Write-Host "    • Markdown: npx markdownlint-cli 'file.md' --fix" -ForegroundColor Gray
    }
    if ($script:failedLinters -contains "Spelling") {
        Write-Host "    • Spelling: Add words to cspell.json or fix typos" -ForegroundColor Gray
    }
    if ($script:failedLinters -contains "C#") {
        Write-Host "    • C#: Fix compiler warnings/errors" -ForegroundColor Gray
    }
    Write-Host ""
    exit 1
}
