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
    - Auto-crops viewBox to the actual artwork bounding box

  Files are overwritten in place (use git to revert if needed).

.PARAMETER Path
  One or more SVG file paths. Supports wildcards.
  Defaults to all *.svg files in the script's directory.

.PARAMETER ViewBox
  Target viewBox as "width height" (e.g. "236 208").
  Only applied if specified. When set, auto-crop is skipped.

.PARAMETER NoCrop
  Skip viewBox auto-cropping. By default the script crops the viewBox
  to tightly fit the actual artwork bounds (with 1-unit margin).

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

  [switch] $NoCrop,

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

# ── Bezier extrema helpers ──

function Get-CubicBezierExtrema {
  <#
    Return the extreme values (min/max) of a cubic Bezier on one axis.
    P0..P3 are the four control point coordinates on a single axis.
    Finds t values where B'(t)=0, evaluates B(t) there, plus endpoints.
  #>
  param([double]$P0, [double]$P1, [double]$P2, [double]$P3)
  $result = @($P0, $P3)
  # B'(t) = 3[(1-t)²(P1-P0) + 2(1-t)t(P2-P1) + t²(P3-P2)]
  # Quadratic At² + Bt + C = 0 where:
  $a = $P1 - $P0; $b = $P2 - $P1; $c = $P3 - $P2
  $A = $a - 2 * $b + $c
  $B = 2 * ($b - $a)
  $C = $a
  if ([Math]::Abs($A) -gt 1e-12) {
    $disc = $B * $B - 4 * $A * $C
    if ($disc -ge 0) {
      $sq = [Math]::Sqrt($disc)
      $denom2A = 2 * $A
      $t1 = (-$B + $sq) / $denom2A
      $t2 = (-$B - $sq) / $denom2A
      foreach ($t in @($t1, $t2)) {
        if ($t -gt 1e-8 -and $t -lt (1 - 1e-8)) {
          $u = 1 - $t
          $result += $u * $u * $u * $P0 + 3 * $u * $u * $t * $P1 + 3 * $u * $t * $t * $P2 + $t * $t * $t * $P3
        }
      }
    }
  } elseif ([Math]::Abs($B) -gt 1e-12) {
    $t = -$C / $B
    if ($t -gt 1e-8 -and $t -lt (1 - 1e-8)) {
      $u = 1 - $t
      $result += $u * $u * $u * $P0 + 3 * $u * $u * $t * $P1 + 3 * $u * $t * $t * $P2 + $t * $t * $t * $P3
    }
  }
  return $result
}

function Get-QuadBezierExtrema {
  <#
    Return the extreme values of a quadratic Bezier on one axis.
    P0, P1, P2 are the three control point coordinates on a single axis.
  #>
  param([double]$P0, [double]$P1, [double]$P2)
  $result = @($P0, $P2)
  $denom = $P0 - 2 * $P1 + $P2
  if ([Math]::Abs($denom) -gt 1e-12) {
    $t = ($P0 - $P1) / $denom
    if ($t -gt 1e-8 -and $t -lt (1 - 1e-8)) {
      $u = 1 - $t
      $result += $u * $u * $P0 + 2 * $u * $t * $P1 + $t * $t * $P2
    }
  }
  return $result
}

# ── Sub-path bounding box helper ──

function Get-SubPathBBox {
  <#
    Parse an SVG path d="" string and return its bounding box.
    Properly handles:
    - All SVG path commands (M/L/H/V/C/S/Q/T/A/Z)
    - Relative commands (lowercase) by tracking current position
    - Tight Bezier bounds via derivative root-finding
  #>
  param([string] $SubPath)

  # Tokenize into command letters and numbers
  $tokens = [System.Collections.Generic.List[string]]::new()
  foreach ($m in [regex]::Matches($SubPath, '[MmLlHhVvCcSsQqTtAaZz]|-?\d*\.?\d+(?:[eE][+-]?\d+)?')) {
    $tokens.Add($m.Value)
  }
  if ($tokens.Count -lt 3) { return $null }

  [double]$minX = [double]::MaxValue; [double]$minY = [double]::MaxValue
  [double]$maxX = [double]::MinValue; [double]$maxY = [double]::MinValue
  [bool]$hasPoints = $false
  [double]$cx = 0; [double]$cy = 0  # current position
  [double]$mx = 0; [double]$my = 0  # sub-path start (for Z)
  # For smooth Bezier continuations
  [double]$lastCp2X = 0; [double]$lastCp2Y = 0  # last C/S 2nd control point
  [double]$lastQpX = 0; [double]$lastQpY = 0    # last Q/T control point
  [string]$lastCmd = ''

  $ti = 0

  while ($ti -lt $tokens.Count) {
    $tok = $tokens[$ti]
    # Skip stray numbers (shouldn't happen with well-formed paths)
    if ($tok -notmatch '^[A-Za-z]$') { $ti++; continue }
    $cmd = $tok
    $ti++
    $isRel = ($cmd -ceq $cmd.ToLower()) -and ($cmd -ne 'Z') -and ($cmd -ne 'z')
    $cmdUp = $cmd.ToUpper()

    # Helper: check if next token is a number (not a command)
    $isNum = { param($idx) $idx -lt $tokens.Count -and $tokens[$idx] -notmatch '^[A-Za-z]$' }

    switch ($cmdUp) {
      'M' {
        $first = $true
        while ((& $isNum $ti) -and ($ti + 1 -lt $tokens.Count) -and (& $isNum ($ti + 1))) {
          $x = [double]$tokens[$ti]; $ti++
          $y = [double]$tokens[$ti]; $ti++
          if ($isRel -and -not $first) { $x += $cx; $y += $cy }
          elseif ($isRel -and $first) { $x += $cx; $y += $cy }
          $cx = $x; $cy = $y
          if ($first) { $mx = $x; $my = $y; $first = $false }
          if ($x -lt $minX) { $minX = $x }; if ($x -gt $maxX) { $maxX = $x }
          if ($y -lt $minY) { $minY = $y }; if ($y -gt $maxY) { $maxY = $y }
          $hasPoints = $true
        }
        $lastCp2X = $cx; $lastCp2Y = $cy
        $lastQpX = $cx; $lastQpY = $cy
      }
      'L' {
        while ((& $isNum $ti) -and ($ti + 1 -lt $tokens.Count) -and (& $isNum ($ti + 1))) {
          $x = [double]$tokens[$ti]; $ti++
          $y = [double]$tokens[$ti]; $ti++
          if ($isRel) { $x += $cx; $y += $cy }
          $cx = $x; $cy = $y
          if ($x -lt $minX) { $minX = $x }; if ($x -gt $maxX) { $maxX = $x }
          if ($y -lt $minY) { $minY = $y }; if ($y -gt $maxY) { $maxY = $y }
          $hasPoints = $true
        }
        $lastCp2X = $cx; $lastCp2Y = $cy
        $lastQpX = $cx; $lastQpY = $cy
      }
      'H' {
        while (& $isNum $ti) {
          $x = [double]$tokens[$ti]; $ti++
          if ($isRel) { $x += $cx }
          $cx = $x
          if ($x -lt $minX) { $minX = $x }; if ($x -gt $maxX) { $maxX = $x }
          if ($cy -lt $minY) { $minY = $cy }; if ($cy -gt $maxY) { $maxY = $cy }
          $hasPoints = $true
        }
        $lastCp2X = $cx; $lastCp2Y = $cy
        $lastQpX = $cx; $lastQpY = $cy
      }
      'V' {
        while (& $isNum $ti) {
          $y = [double]$tokens[$ti]; $ti++
          if ($isRel) { $y += $cy }
          $cy = $y
          if ($cx -lt $minX) { $minX = $cx }; if ($cx -gt $maxX) { $maxX = $cx }
          if ($y -lt $minY) { $minY = $y }; if ($y -gt $maxY) { $maxY = $y }
          $hasPoints = $true
        }
        $lastCp2X = $cx; $lastCp2Y = $cy
        $lastQpX = $cx; $lastQpY = $cy
      }
      'C' {
        while ((& $isNum $ti) -and ($ti + 5 -lt $tokens.Count)) {
          $x1 = [double]$tokens[$ti]; $ti++
          $y1 = [double]$tokens[$ti]; $ti++
          $x2 = [double]$tokens[$ti]; $ti++
          $y2 = [double]$tokens[$ti]; $ti++
          $x  = [double]$tokens[$ti]; $ti++
          $y  = [double]$tokens[$ti]; $ti++
          if ($isRel) {
            $x1 += $cx; $y1 += $cy; $x2 += $cx; $y2 += $cy; $x += $cx; $y += $cy
          }
          foreach ($bx in (Get-CubicBezierExtrema $cx $x1 $x2 $x)) {
            if ($bx -lt $minX) { $minX = $bx }; if ($bx -gt $maxX) { $maxX = $bx }
          }
          foreach ($by in (Get-CubicBezierExtrema $cy $y1 $y2 $y)) {
            if ($by -lt $minY) { $minY = $by }; if ($by -gt $maxY) { $maxY = $by }
          }
          $lastCp2X = $x2; $lastCp2Y = $y2
          $cx = $x; $cy = $y
          $hasPoints = $true
        }
        $lastQpX = $cx; $lastQpY = $cy
      }
      'S' {
        while ((& $isNum $ti) -and ($ti + 3 -lt $tokens.Count)) {
          # Reflect previous C/S second control point
          if ($lastCmd -eq 'C' -or $lastCmd -eq 'S' -or $lastCmd -eq 'c' -or $lastCmd -eq 's') {
            $x1 = 2 * $cx - $lastCp2X; $y1 = 2 * $cy - $lastCp2Y
          } else {
            $x1 = $cx; $y1 = $cy
          }
          $x2 = [double]$tokens[$ti]; $ti++
          $y2 = [double]$tokens[$ti]; $ti++
          $x  = [double]$tokens[$ti]; $ti++
          $y  = [double]$tokens[$ti]; $ti++
          if ($isRel) { $x2 += $cx; $y2 += $cy; $x += $cx; $y += $cy }
          foreach ($bx in (Get-CubicBezierExtrema $cx $x1 $x2 $x)) {
            if ($bx -lt $minX) { $minX = $bx }; if ($bx -gt $maxX) { $maxX = $bx }
          }
          foreach ($by in (Get-CubicBezierExtrema $cy $y1 $y2 $y)) {
            if ($by -lt $minY) { $minY = $by }; if ($by -gt $maxY) { $maxY = $by }
          }
          $lastCp2X = $x2; $lastCp2Y = $y2
          $cx = $x; $cy = $y
          $hasPoints = $true
        }
        $lastQpX = $cx; $lastQpY = $cy
      }
      'Q' {
        while ((& $isNum $ti) -and ($ti + 3 -lt $tokens.Count)) {
          $qx = [double]$tokens[$ti]; $ti++
          $qy = [double]$tokens[$ti]; $ti++
          $x  = [double]$tokens[$ti]; $ti++
          $y  = [double]$tokens[$ti]; $ti++
          if ($isRel) { $qx += $cx; $qy += $cy; $x += $cx; $y += $cy }
          foreach ($bx in (Get-QuadBezierExtrema $cx $qx $x)) {
            if ($bx -lt $minX) { $minX = $bx }; if ($bx -gt $maxX) { $maxX = $bx }
          }
          foreach ($by in (Get-QuadBezierExtrema $cy $qy $y)) {
            if ($by -lt $minY) { $minY = $by }; if ($by -gt $maxY) { $maxY = $by }
          }
          $lastQpX = $qx; $lastQpY = $qy
          $cx = $x; $cy = $y
          $hasPoints = $true
        }
        $lastCp2X = $cx; $lastCp2Y = $cy
      }
      'T' {
        while ((& $isNum $ti) -and ($ti + 1 -lt $tokens.Count) -and (& $isNum ($ti + 1))) {
          # Reflect previous Q/T control point
          if ($lastCmd -eq 'Q' -or $lastCmd -eq 'T' -or $lastCmd -eq 'q' -or $lastCmd -eq 't') {
            $qx = 2 * $cx - $lastQpX; $qy = 2 * $cy - $lastQpY
          } else {
            $qx = $cx; $qy = $cy
          }
          $x = [double]$tokens[$ti]; $ti++
          $y = [double]$tokens[$ti]; $ti++
          if ($isRel) { $x += $cx; $y += $cy }
          foreach ($bx in (Get-QuadBezierExtrema $cx $qx $x)) {
            if ($bx -lt $minX) { $minX = $bx }; if ($bx -gt $maxX) { $maxX = $bx }
          }
          foreach ($by in (Get-QuadBezierExtrema $cy $qy $y)) {
            if ($by -lt $minY) { $minY = $by }; if ($by -gt $maxY) { $maxY = $by }
          }
          $lastQpX = $qx; $lastQpY = $qy
          $cx = $x; $cy = $y
          $hasPoints = $true
        }
        $lastCp2X = $cx; $lastCp2Y = $cy
      }
      'A' {
        while ((& $isNum $ti) -and ($ti + 6 -lt $tokens.Count)) {
          # Arc: rx ry x-rotation large-arc sweep x y
          # Skip rx, ry, rotation, flags; just use endpoint for bbox
          $ti += 5  # skip rx, ry, x-rotation, large-arc-flag, sweep-flag
          $x = [double]$tokens[$ti]; $ti++
          $y = [double]$tokens[$ti]; $ti++
          if ($isRel) { $x += $cx; $y += $cy }
          $cx = $x; $cy = $y
          if ($x -lt $minX) { $minX = $x }; if ($x -gt $maxX) { $maxX = $x }
          if ($y -lt $minY) { $minY = $y }; if ($y -gt $maxY) { $maxY = $y }
          $hasPoints = $true
        }
        $lastCp2X = $cx; $lastCp2Y = $cy
        $lastQpX = $cx; $lastQpY = $cy
      }
      'Z' {
        $cx = $mx; $cy = $my
        $lastCp2X = $cx; $lastCp2Y = $cy
        $lastQpX = $cx; $lastQpY = $cy
      }
    }
    $lastCmd = $cmd
  }

  if (-not $hasPoints) { return $null }
  return @{ MinX = $minX; MinY = $minY; MaxX = $maxX; MaxY = $maxY }
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

  # ── Auto-crop viewBox to artwork bounds ──
  # Only when -ViewBox is not specified and -NoCrop is not set.
  # Calculates the bounding box of all path data (accounting for
  # <g transform="matrix(...)"> translates from Designer exports)
  # and sets the viewBox to tightly wrap the artwork.
  if (-not $ViewBox -and -not $NoCrop) {
    $cropDAttrs = [regex]::Matches($content, ' d="([^"]+)"')
    if ($cropDAttrs.Count -gt 0) {
      # Detect <g transform="matrix(a,b,c,d,e,f)"> translate offset.
      # Affinity Designer exports with artboard-absolute coordinates and
      # a matrix(1,0,0,1,tx,ty) translate to map into the viewBox.
      $txOffset = 0.0
      $tyOffset = 0.0
      $hasNonTrivialTransform = $false
      $matrixMatch = [regex]::Match($content, '<g\s+transform="matrix\(([^)]+)\)"')
      if ($matrixMatch.Success) {
        $matrixParts = $matrixMatch.Groups[1].Value -split ','
        if ($matrixParts.Count -eq 6) {
          $a = [double]$matrixParts[0].Trim()
          $b = [double]$matrixParts[1].Trim()
          $c = [double]$matrixParts[2].Trim()
          $d = [double]$matrixParts[3].Trim()
          $e = [double]$matrixParts[4].Trim()
          $f = [double]$matrixParts[5].Trim()
          # Only handle simple translates (identity + offset)
          if ($a -eq 1 -and $b -eq 0 -and $c -eq 0 -and $d -eq 1) {
            $txOffset = $e
            $tyOffset = $f
          } else {
            $hasNonTrivialTransform = $true
          }
        }
      }

      if (-not $hasNonTrivialTransform) {
        $globalMinX = [double]::MaxValue
        $globalMinY = [double]::MaxValue
        $globalMaxX = [double]::MinValue
        $globalMaxY = [double]::MinValue
        $hasBounds = $false

        foreach ($dAttr in $cropDAttrs) {
          $dVal = $dAttr.Groups[1].Value
          $subPaths = [regex]::Split($dVal, '(?=M)') | Where-Object { $_.Trim().Length -gt 0 }
          foreach ($sp in $subPaths) {
            $bbox = Get-SubPathBBox -SubPath $sp
            if ($bbox) {
              $hasBounds = $true
              # Apply translate offset to map from artboard to viewBox coords
              $adjMinX = $bbox.MinX + $txOffset
              $adjMinY = $bbox.MinY + $tyOffset
              $adjMaxX = $bbox.MaxX + $txOffset
              $adjMaxY = $bbox.MaxY + $tyOffset
              if ($adjMinX -lt $globalMinX) { $globalMinX = $adjMinX }
              if ($adjMinY -lt $globalMinY) { $globalMinY = $adjMinY }
              if ($adjMaxX -gt $globalMaxX) { $globalMaxX = $adjMaxX }
              if ($adjMaxY -gt $globalMaxY) { $globalMaxY = $adjMaxY }
            }
          }
        }

        if ($hasBounds) {
          # Add 1-unit margin to avoid clipping anti-aliased edges
          $margin = 1
          $cropMinX = [Math]::Floor($globalMinX - $margin)
          $cropMinY = [Math]::Floor($globalMinY - $margin)
          $cropMaxX = [Math]::Ceiling($globalMaxX + $margin)
          $cropMaxY = [Math]::Ceiling($globalMaxY + $margin)
          $cropW = $cropMaxX - $cropMinX
          $cropH = $cropMaxY - $cropMinY

          # Parse current viewBox to compare dimensions
          $currentVBMatch = [regex]::Match($content, 'viewBox="([^"]+)"')
          if ($currentVBMatch.Success) {
            $vbParts = $currentVBMatch.Groups[1].Value -split '\s+'
            $oldVBW = [double]$vbParts[2]
            $oldVBH = [double]$vbParts[3]

            # Safety: only crop if result is SMALLER than current viewBox
            # but not absurdly small (< 10% means Bezier bbox is unreliable).
            $minW = $oldVBW * 0.1
            $minH = $oldVBH * 0.1
            if ($cropW -le $oldVBW -and $cropH -le $oldVBH -and
                $cropW -ge $minW -and $cropH -ge $minH) {
              $cropVB = "$cropMinX $cropMinY $cropW $cropH"
              if ($currentVBMatch.Groups[1].Value -ne $cropVB) {
                $oldVB = $currentVBMatch.Groups[1].Value
                $content = $content -replace 'viewBox="[^"]+"', "viewBox=`"$cropVB`""
                $changes += "viewBox cropped: $oldVB -> $cropVB"
              }
            }
          }
        }
      }
    }
  }

  # ── Enforce hex aspect ratio ──
  # Flat-top hex bounding box: height/width = √3/2 ≈ 0.8660.
  # SVGs with "hex" in the filename must match this ratio.
  # IMPORTANT: Always EXPAND the viewBox (never shrink) so artwork is
  # never clipped.  If the artwork is taller than the target ratio,
  # widen the viewBox; if wider, increase the height.
  $hexRatio = [Math]::Sqrt(3) / 2  # ≈ 0.8660254
  if ($file.Name -match 'hex') {
    $vbMatch = [regex]::Match($content, 'viewBox="(-?[\d.]+)\s+(-?[\d.]+)\s+([\d.]+)\s+([\d.]+)"')
    if ($vbMatch.Success) {
      $vbX = [double]$vbMatch.Groups[1].Value
      $vbY = [double]$vbMatch.Groups[2].Value
      $vbW = [double]$vbMatch.Groups[3].Value
      $vbH = [double]$vbMatch.Groups[4].Value
      $actualRatio = $vbH / $vbW
      $correctH = [Math]::Round($vbW * $hexRatio)

      if ($correctH -ge $vbH -and $correctH -ne $vbH) {
        # Height needs to grow (or stay same) — expand vertically, center
        $deltaH = $correctH - $vbH
        $newY = [Math]::Round($vbY - $deltaH / 2, 1)
        $newVB = "$vbX $newY $vbW $correctH"
        $content = $content -replace 'viewBox="[^"]+"', "viewBox=`"$newVB`""
        $changes += "hex aspect: ${vbW}x${vbH} ($('{0:F3}' -f $actualRatio)) -> ${vbW}x${correctH} ($('{0:F3}' -f $hexRatio))"
      } elseif ($correctH -lt $vbH) {
        # Height would shrink — expand width instead to preserve artwork
        $correctW = [Math]::Round($vbH / $hexRatio)
        if ($correctW -ne $vbW) {
          $deltaW = $correctW - $vbW
          $newX = [Math]::Round($vbX - $deltaW / 2, 1)
          $newRatio = $vbH / $correctW
          $newVB = "$newX $vbY $correctW $vbH"
          $content = $content -replace 'viewBox="[^"]+"', "viewBox=`"$newVB`""
          $changes += "hex aspect: ${vbW}x${vbH} ($('{0:F3}' -f $actualRatio)) -> ${correctW}x${vbH} ($('{0:F3}' -f $newRatio))"
        }
      }
    }
  }

  # ── Normalize viewBox origin to (0,0) ──
  # svgicons2svgfont ignores viewBox x,y offsets when mapping path
  # coordinates to the em-box.  If the auto-crop or aspect-ratio fix
  # produced a non-zero origin, wrap paths in a translate group so the
  # effective coordinates start at (0,0).
  $vbFinal = [regex]::Match($content, 'viewBox="(-?[\d.]+)\s+(-?[\d.]+)\s+([\d.]+)\s+([\d.]+)"')
  if ($vbFinal.Success) {
    $fX = [double]$vbFinal.Groups[1].Value
    $fY = [double]$vbFinal.Groups[2].Value
    $fW = [double]$vbFinal.Groups[3].Value
    $fH = [double]$vbFinal.Groups[4].Value
    if ([Math]::Abs($fX) -gt 0.01 -or [Math]::Abs($fY) -gt 0.01) {
      # Replace viewBox with zero origin
      $content = $content -replace 'viewBox="[^"]+"', "viewBox=`"0 0 $fW $fH`""
      # Wrap inner content in a translate group
      $content = $content -replace '(?s)(xmlns:serif="[^"]*"[^>]*>)', "`$1`n<g transform=`"translate($(-$fX),$(-$fY))`">"
      $content = $content -replace '</svg>', '</g></svg>'
      $changes += "viewBox origin: ($fX,$fY) -> (0,0) via translate"
    }
  }

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
