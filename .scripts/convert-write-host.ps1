#!/usr/bin/env pwsh
<#
.SYNOPSIS
    Converts Write-Host calls to Write-Log calls in a PowerShell script.
.DESCRIPTION
    Processes each line individually, converting Write-Host to Write-Log.
    Handles color mapping, -NoNewline, bare variables, and various patterns.
    Use -WhatIf to preview changes without modifying the file.
.PARAMETER Path
    The file to convert.
.PARAMETER WhatIf
    Preview changes without writing to disk.
#>
param(
    [Parameter(Mandatory = $true, Position = 0)]
    [string]$Path,

    [switch]$WhatIf
)

if (-not (Test-Path $Path)) {
    Write-Host "File not found: $Path" -ForegroundColor Red
    exit 1
}

$lines = Get-Content $Path
$newLines = @()
$changeCount = 0
$skippedLines = @()

# Color to level mapping
$colorToLevel = @{
    'Red'      = 'ERROR'
    'Yellow'   = 'WARN'
    'Gray'     = 'DEBUG'
    'DarkGray' = 'DEBUG'
}

# Colors that map to a level and don't need explicit -ForegroundColor
$levelColors = @('Red', 'Yellow', 'Gray', 'DarkGray')

for ($i = 0; $i -lt $lines.Count; $i++) {
    $line = $lines[$i]
    $lineNum = $i + 1

    # Skip lines without Write-Host or commented-out lines
    if ($line -notmatch 'Write-Host' -or $line.TrimStart() -match '^\s*#') {
        $newLines += $line
        continue
    }

    # Preserve leading whitespace
    $indent = ''
    if ($line -match '^(\s+)') { $indent = $Matches[1] }

    # Extract the Write-Host portion (handle inline statements like "if (...) { Write-Host ... }")
    $prefix = ''
    $suffix = ''
    $whPart = $line.TrimStart()

    # Check if Write-Host is inside a statement (e.g., "if ($x) { Write-Host ... }")
    if ($whPart -match '^(.+?\{\s*)(Write-Host.+?)(\s*\}.*)?$') {
        $prefix = $Matches[1]
        $whPart = $Matches[2]
        $suffix = if ($Matches[3]) { $Matches[3] } else { '' }
    } elseif ($whPart -match '^(.+?\|.+?)(Write-Host.+)$') {
        # Pipeline: ... | ForEach-Object { Write-Host $_ }
        $skippedLines += "  L${lineNum}: $($line.Trim()) [pipeline - manual review]"
        $newLines += $line
        continue
    } elseif ($whPart -notmatch '^Write-Host') {
        # Write-Host not at start and not in a recognized pattern
        $skippedLines += "  L${lineNum}: $($line.Trim()) [complex statement - manual review]"
        $newLines += $line
        continue
    }

    # Parse the Write-Host call
    $hasNoNewline = $whPart -match '-NoNewline'
    $whPart = $whPart -replace '\s+-NoNewline', ''

    # Extract color
    $color = $null
    if ($whPart -match '-ForegroundColor\s+(\w+)') {
        $color = $Matches[1]
        $whPart = $whPart -replace '\s+-ForegroundColor\s+\w+', ''
    }
    # Also handle color before message: Write-Host -ForegroundColor Cyan "text"
    if ($whPart -match 'Write-Host\s+-ForegroundColor\s+(\w+)\s+') {
        $color = $Matches[1]
        $whPart = $whPart -replace '\s*-ForegroundColor\s+\w+', ''
    }

    # Extract message (everything after Write-Host, trimmed)
    $msg = ($whPart -replace '^Write-Host\s*', '').Trim()

    # Determine level
    $level = 'INFO'
    $noLabel = $true
    $needsColor = $false

    if ($color -and $levelColors -contains $color) {
        $level = $colorToLevel[$color]
        $noLabel = $true  # Write-Host never had labels, so converted calls shouldn't either
    } elseif ($color) {
        # Non-standard color: keep as INFO with explicit color
        $needsColor = $true
    }

    # Handle empty message
    if (-not $msg -or $msg -eq '""' -or $msg -eq "''") {
        $msg = '""'
    }

    # Build Write-Log call
    $logCall = "Write-Log -Level $level -Message $msg"
    if ($noLabel) { $logCall += " -NoLabel" }
    if ($hasNoNewline) { $logCall += " -NoNewline" }
    if ($needsColor) { $logCall += " -ForegroundColor $color" }

    $newLine = "$indent$prefix$logCall$suffix"
    $newLines += $newLine
    $changeCount++
}

Write-Host ""
Write-Host "Conversion results for: $Path" -ForegroundColor Cyan
Write-Host "  Converted: $changeCount lines"

$remaining = ($newLines | Where-Object { $_ -match 'Write-Host' -and $_.TrimStart() -notmatch '^\s*#' }).Count
if ($remaining -gt 0) {
    Write-Host "  Remaining Write-Host: $remaining (need manual review)" -ForegroundColor Yellow
}

if ($skippedLines.Count -gt 0) {
    Write-Host "  Skipped (complex patterns):" -ForegroundColor Yellow
    foreach ($s in $skippedLines) { Write-Host $s -ForegroundColor DarkGray }
}

if ($WhatIf) {
    Write-Host ""
    Write-Host "  [WhatIf] Changes preview:" -ForegroundColor Gray
    for ($i = 0; $i -lt $lines.Count; $i++) {
        if ($lines[$i] -ne $newLines[$i]) {
            Write-Host "  L$($i+1):" -ForegroundColor DarkGray
            Write-Host "    - $($lines[$i].TrimEnd())" -ForegroundColor Red
            Write-Host "    + $($newLines[$i].TrimEnd())" -ForegroundColor Green
        }
    }
} else {
    Set-Content -Path $Path -Value $newLines
    Write-Host "  File updated." -ForegroundColor Green
}
