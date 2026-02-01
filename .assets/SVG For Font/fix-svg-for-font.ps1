#!/usr/bin/env pwsh
<#
.SYNOPSIS
  Fix SVG files for font conversion (svgicons2svgfont compatibility).

.DESCRIPTION
  Processes SVG files to make them compatible with the Catan font build pipeline.
  Fixes common issues from Affinity Designer exports:

    - Auto-detects compound paths and sets the correct fill-rule
      (evenodd for compound, nonzero for multi-path)
    - Removes hex outline sub-paths from compound paths (the outer hex
      frame that Designer includes but the font builder doesn't need)
    - Removes fill colors (font glyphs are monochrome black)
    - Removes stroke attributes (strokes don't convert to font outlines)
    - Removes clip-rule, stroke-linejoin, stroke-miterlimit from styles
    - Optionally sets the viewBox to a target size

  Files are overwritten in place (use git to revert if needed).

.PARAMETER Path
  One or more SVG file paths. Supports wildcards.
  Defaults to all *.svg files in the script's directory.

.PARAMETER ViewBox
  Target viewBox as "width height" (e.g. "236 208").
  Only applied if specified. Existing viewBox is preserved otherwise.

.PARAMETER WhatIf
  Show what would change without modifying files.

.PARAMETER Help
  Show this help message.

.EXAMPLE
  pwsh "./.assets/SVG For Font/fix-svg-for-font.ps1" wheat-bw-hex.svg

.EXAMPLE
  pwsh "./.assets/SVG For Font/fix-svg-for-font.ps1" *-bw-hex.svg -ViewBox "236 208"

.EXAMPLE
  pwsh "./.assets/SVG For Font/fix-svg-for-font.ps1" -WhatIf
#>

[CmdletBinding(SupportsShouldProcess)]
param(
  [Parameter(Position = 0)]
  [string[]] $Path,

  [string] $ViewBox,

  [switch] $Help
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

if ($Help) {
  Get-Help $PSCommandPath -Detailed
  return
}

$ScriptDir = $PSScriptRoot

# Default to all SVGs in the script directory
if (-not $Path -or $Path.Count -eq 0) {
  $Path = @(Join-Path $ScriptDir "*.svg")
}

# Resolve wildcards and filter to existing files
$files = @()
foreach ($p in $Path) {
  # If relative path, resolve against script directory
  if (-not [System.IO.Path]::IsPathRooted($p)) {
    $p = Join-Path $ScriptDir $p
  }
  $resolved = Get-Item -LiteralPath $p -ErrorAction SilentlyContinue
  if (-not $resolved) {
    $resolved = Get-Item -Path $p -ErrorAction SilentlyContinue
  }
  if ($resolved) {
    $files += $resolved
  } else {
    Write-Warning "No files matched: $p"
  }
}

if ($files.Count -eq 0) {
  Write-Warning "No SVG files found."
  return
}

# ── Sub-path bounding box helper ──

function Get-SubPathBBox {
  <#
    Parse a single SVG sub-path (M...Z or M...next M) and return its
    bounding box as @{ MinX; MinY; MaxX; MaxY }.  Only considers
    coordinate values from M, L, C, S, Q, T commands (absolute).
    Relative commands (lowercase) are ignored for simplicity -- Affinity
    Designer with "Flatten transforms" exports absolute coordinates.
  #>
  param([string] $SubPath)

  $nums = [regex]::Matches($SubPath, '(?<=[MLCSQTmlcsqt,\s])-?\d+\.?\d*')
  if ($nums.Count -lt 2) { return $null }

  $xs = @()
  $ys = @()
  for ($i = 0; $i -lt $nums.Count; $i += 2) {
    if ($i + 1 -ge $nums.Count) { break }
    $xs += [double]$nums[$i].Value
    $ys += [double]$nums[$i + 1].Value
  }

  if ($xs.Count -eq 0) { return $null }
  return @{
    MinX = ($xs | Measure-Object -Minimum).Minimum
    MinY = ($ys | Measure-Object -Minimum).Minimum
    MaxX = ($xs | Measure-Object -Maximum).Maximum
    MaxY = ($ys | Measure-Object -Maximum).Maximum
  }
}

# ── Style manipulation helpers ──

function Remove-StyleProperty {
  param(
    [string] $Style,
    [string] $Property
  )
  # Remove "property:value;" from a CSS style string
  $Style = $Style -replace "(?i)$Property\s*:\s*[^;]+;\s*", ""
  return $Style
}

function Set-StyleProperty {
  param(
    [string] $Style,
    [string] $Property,
    [string] $Value
  )
  # Remove existing property first
  $Style = Remove-StyleProperty -Style $Style -Property $Property
  # Append the new property
  $Style = $Style.TrimEnd("; ")
  if ($Style.Length -gt 0 -and -not $Style.EndsWith(";")) {
    $Style += ";"
  }
  $Style += "$Property`:$Value;"
  return $Style
}

# ── Process each file ──

foreach ($file in $files) {
  $content = Get-Content -LiteralPath $file.FullName -Raw -Encoding UTF8
  $original = $content
  $changes = @()

  # Count paths before changes
  $pathCount = ([regex]::Matches($content, '<path')).Count
  if ($pathCount -eq 0) {
    Write-Host "  SKIP $($file.Name) (no <path> elements)" -ForegroundColor DarkGray
    continue
  }

  # ── Fix viewBox if requested ──
  if ($ViewBox) {
    $parts = $ViewBox.Trim() -split '\s+'
    if ($parts.Count -eq 2) {
      $targetVB = "0 0 $($parts[0]) $($parts[1])"
      $currentVB = [regex]::Match($content, 'viewBox="([^"]+)"')
      if ($currentVB.Success -and $currentVB.Groups[1].Value -ne $targetVB) {
        $content = $content -replace 'viewBox="[^"]+"', "viewBox=`"$targetVB`""
        $changes += "viewBox: $($currentVB.Groups[1].Value) -> $targetVB"
      }
    } else {
      Write-Warning "Invalid ViewBox format '$ViewBox'. Use 'width height' (e.g. '236 208')."
    }
  }

  # ── Decide fill-rule strategy ──
  # Compound paths (single <path> with many M sub-paths) need evenodd because
  # their sub-path winding directions are arbitrary. Multi-path SVGs (separate
  # <path> elements per shape) work with nonzero.
  $allDAttrs = [regex]::Matches($content, ' d="([^"]+)"')
  $maxSubPaths = 0
  foreach ($d in $allDAttrs) {
    $mCount = ([regex]::Matches($d.Groups[1].Value, 'M')).Count
    if ($mCount -gt $maxSubPaths) { $maxSubPaths = $mCount }
  }
  # If any single path has many sub-paths, it's a compound path -- use evenodd
  $useEvenOdd = ($pathCount -le 2 -and $maxSubPaths -gt 10)
  $targetFillRule = if ($useEvenOdd) { "evenodd" } else { "nonzero" }
  if ($useEvenOdd) { $changes += "compound path detected, using evenodd" }

  # ── Strip hex outline sub-paths from compound paths ──
  # Compound paths from Designer often include the hex border as a sub-path.
  # The font builder doesn't need it (the hex shape comes from the app).
  # Detect sub-paths whose bounding box covers >80% of the viewBox.
  $script:hexOutlineCount = 0
  if ($useEvenOdd) {
    $vbMatch = [regex]::Match($content, 'viewBox="0 0 ([\d.]+) ([\d.]+)"')
    if ($vbMatch.Success) {
      $vbW = [double]$vbMatch.Groups[1].Value
      $vbH = [double]$vbMatch.Groups[2].Value

      $content = [regex]::Replace($content, ' d="([^"]+)"', {
        param($dAttrMatch)
        $dVal = $dAttrMatch.Groups[1].Value
        $subPaths = [regex]::Split($dVal, '(?=M)') | Where-Object { $_.Trim().Length -gt 0 }
        $kept = @()
        $removedCount = 0

        foreach ($sp in $subPaths) {
          $bbox = Get-SubPathBBox -SubPath $sp
          if ($bbox) {
            $spW = $bbox.MaxX - $bbox.MinX
            $spH = $bbox.MaxY - $bbox.MinY
            # Sub-path covers >80% of viewBox in both dimensions = hex outline
            if ($spW -gt ($vbW * 0.8) -and $spH -gt ($vbH * 0.8)) {
              $removedCount++
              continue
            }
          }
          $kept += $sp
        }

        if ($removedCount -gt 0) {
          $script:hexOutlineCount = $removedCount
          return " d=`"$($kept -join '')`""
        }
        return $dAttrMatch.Value
      })

      if ($script:hexOutlineCount -gt 0) {
        $changes += "removed $($script:hexOutlineCount) hex outline sub-path(s)"
        # Recount after removal
        $allDAttrs = [regex]::Matches($content, ' d="([^"]+)"')
        $maxSubPaths = 0
        foreach ($d in $allDAttrs) {
          $mCount = ([regex]::Matches($d.Groups[1].Value, 'M')).Count
          if ($mCount -gt $maxSubPaths) { $maxSubPaths = $mCount }
        }
      }
    }
  }

  # ── Clean SVG-level style attributes ──
  # Remove clip-rule, stroke-linejoin, stroke-miterlimit from <svg> style
  $content = [regex]::Replace($content, '(<svg[^>]*?)style="([^"]*)"', {
    param($m)
    $before = $m.Groups[1].Value
    $style = $m.Groups[2].Value
    $style = $style -replace '(?i)clip-rule\s*:\s*[^;]+;\s*', ''
    $style = $style -replace '(?i)stroke-linejoin\s*:\s*[^;]+;\s*', ''
    $style = $style -replace '(?i)stroke-miterlimit\s*:\s*[^;]+;\s*', ''
    $style = $style.Trim('; ')
    if ($style.Length -eq 0) {
      return $before
    }
    return "${before}style=`"$style`""
  })

  # ── Fix each <path> element ──
  $script:pathFixCount = 0
  $content = [regex]::Replace($content, '<path\s+([^>]*?)/?>', {
    param($match)
    $tag = $match.Value
    $tagChanges = @()

    # -- Handle style attribute --
    $styleMatch = [regex]::Match($tag, 'style="([^"]*)"')
    if ($styleMatch.Success) {
      $style = $styleMatch.Groups[1].Value
      $newStyle = $style

      # Remove fill color (let it default to black)
      if ($newStyle -match '(?i)fill\s*:\s*(?!rule)') {
        $newStyle = Remove-StyleProperty -Style $newStyle -Property "fill"
        $tagChanges += "removed fill color"
      }

      # Remove stroke properties
      if ($newStyle -match '(?i)stroke-width') {
        $newStyle = Remove-StyleProperty -Style $newStyle -Property "stroke-width"
        $tagChanges += "removed stroke-width"
      }
      if ($newStyle -match '(?i)(?<!-)stroke\s*:') {
        $newStyle = Remove-StyleProperty -Style $newStyle -Property "stroke"
        $tagChanges += "removed stroke"
      }

      # Remove other non-font style properties
      if ($newStyle -match '(?i)clip-rule') {
        $newStyle = Remove-StyleProperty -Style $newStyle -Property "clip-rule"
      }
      if ($newStyle -match '(?i)stroke-linejoin') {
        $newStyle = Remove-StyleProperty -Style $newStyle -Property "stroke-linejoin"
      }
      if ($newStyle -match '(?i)stroke-miterlimit') {
        $newStyle = Remove-StyleProperty -Style $newStyle -Property "stroke-miterlimit"
      }

      # Set fill-rule to the correct value for this SVG
      if ($newStyle -notmatch "fill-rule\s*:\s*$targetFillRule") {
        $newStyle = Set-StyleProperty -Style $newStyle -Property "fill-rule" -Value $targetFillRule
        $tagChanges += "set fill-rule:$targetFillRule"
      }

      # Apply the updated style
      if ($newStyle -ne $style) {
        $tag = $tag -replace 'style="[^"]*"', "style=`"$newStyle`""
      }
    } else {
      # No style attribute -- add one with the correct fill-rule
      $tag = $tag -replace '<path ', "<path style=`"fill-rule:$targetFillRule;`" "
      $tagChanges += "added fill-rule:$targetFillRule"
    }

    # -- Remove standalone fill= attribute (not in style) --
    if ($tag -match '\sfill="(?!none)[^"]*"' -and $tag -match 'style=') {
      $tag = [regex]::Replace($tag, '\s+fill="[^"]*"', '')
      $tagChanges += "removed fill attribute"
    }

    # -- Remove standalone stroke= and stroke-width= attributes --
    if ($tag -match '\sstroke="[^"]*"') {
      $tag = [regex]::Replace($tag, '\s+stroke="[^"]*"', '')
      $tagChanges += "removed stroke attribute"
    }
    if ($tag -match '\sstroke-width="[^"]*"') {
      $tag = [regex]::Replace($tag, '\s+stroke-width="[^"]*"', '')
      $tagChanges += "removed stroke-width attribute"
    }

    if ($tagChanges.Count -gt 0 -and $changes.Count -lt 5) {
      # Track first few path-level changes for reporting
      $script:pathFixCount++
    }

    return $tag
  })

  # ── Count what changed ──
  $script:pathFixCount = 0
  # Re-count by comparing
  $origPaths = [regex]::Matches($original, '<path\s+([^>]*?)/?>')
  $newPaths = [regex]::Matches($content, '<path\s+([^>]*?)/?>')
  $fixedCount = 0
  for ($i = 0; $i -lt [Math]::Min($origPaths.Count, $newPaths.Count); $i++) {
    if ($origPaths[$i].Value -ne $newPaths[$i].Value) { $fixedCount++ }
  }

  if ($fixedCount -gt 0) { $changes += "$fixedCount of $pathCount paths fixed" }

  # ── Report and save ──
  if ($content -eq $original) {
    Write-Host "  OK    $($file.Name) ($pathCount paths, no changes needed)" -ForegroundColor DarkGray
    continue
  }

  if ($WhatIfPreference) {
    Write-Host "  WOULD $($file.Name): $($changes -join ', ')" -ForegroundColor Yellow
  } else {
    Set-Content -LiteralPath $file.FullName -Value $content -NoNewline -Encoding UTF8
    Write-Host "  FIXED $($file.Name): $($changes -join ', ')" -ForegroundColor Green
  }
}

Write-Host ""
Write-Host "Done." -ForegroundColor Green
