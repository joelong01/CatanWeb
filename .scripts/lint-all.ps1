#!/usr/bin/env pwsh
# Lint all PowerShell scripts in the project

$settingsPath = Join-Path $PSScriptRoot "PSScriptAnalyzerSettings.psd1"
$projectRoot = Split-Path $PSScriptRoot -Parent

$scripts = Get-ChildItem -Path $projectRoot -Filter "*.ps1" -Recurse |
    Where-Object { $_.FullName -notmatch "bin|obj|node_modules" }

$allIssues = @()

foreach ($script in $scripts) {
    $issues = Invoke-ScriptAnalyzer -Path $script.FullName -Settings $settingsPath
    if ($issues) {
        $allIssues += $issues
    }
}

if ($allIssues.Count -eq 0) {
    Write-Host "All $($scripts.Count) PowerShell scripts are clean!" -ForegroundColor Green
} else {
    Write-Host "Found $($allIssues.Count) issues:" -ForegroundColor Red
    $allIssues | Format-Table ScriptName, Line, RuleName, Message -AutoSize -Wrap
}

exit $allIssues.Count
