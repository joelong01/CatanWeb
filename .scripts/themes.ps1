#!/usr/bin/env pwsh
<#
.SYNOPSIS
    Theme system diagnostic tool.

.DESCRIPTION
    Validates theme configuration files and reports status.
    Checks theme.json validity, asset file resolution, glyph name resolution,
    and render mode presence for all themes under react-ui/public/themes/.

.PARAMETER Command
    The command to run. Currently only 'doctor' is supported.

.PARAMETER Table
    Output as a pretty-printed table.

.PARAMETER Json
    Output as JSON.

.PARAMETER HashTable
    Return raw PowerShell hashtable array (for pipeline use).

.EXAMPLE
    pwsh .scripts/themes.ps1 doctor -Table
    pwsh .scripts/themes.ps1 doctor -Json
    pwsh .scripts/themes.ps1 doctor -HashTable
#>

param(
    [Parameter(Position = 0)]
    [ValidateSet('doctor')]
    [string]$Command = 'doctor',

    [switch]$Table,
    [switch]$Json,
    [switch]$HashTable
)

$ErrorActionPreference = 'Stop'

# Resolve paths relative to repo root
$RepoRoot = Split-Path -Parent $PSScriptRoot
$ThemesDir = Join-Path $RepoRoot 'react-ui' 'public' 'themes'
$GlyphsFile = Join-Path $RepoRoot 'react-ui' 'lib' 'constants' 'catanGlyphs.ts'

# Default to -Table if no output format specified
if (-not $Table -and -not $Json -and -not $HashTable) {
    $Table = $true
}

function Get-ValidGlyphNames {
    <#
    .SYNOPSIS
        Parse catanGlyphs.ts and return all valid glyph key names.
    #>
    $content = Get-Content -Path $GlyphsFile -Raw
    $matches = [regex]::Matches($content, "^\s+(\w+):\s*'\\u", [System.Text.RegularExpressions.RegexOptions]::Multiline)
    $names = @()
    foreach ($m in $matches) {
        $names += $m.Groups[1].Value
    }
    return $names
}

function Get-ExpectedAssetKeys {
    <#
    .SYNOPSIS
        Return the 75 expected asset keys from base theme.json.
    #>
    $baseThemePath = Join-Path $ThemesDir 'base' 'theme.json'
    if (-not (Test-Path $baseThemePath)) {
        return @()
    }
    $baseTheme = Get-Content -Path $baseThemePath -Raw | ConvertFrom-Json
    return @($baseTheme.assets.PSObject.Properties.Name)
}

function Test-ThemeDoctor {
    $validGlyphs = Get-ValidGlyphNames
    $expectedKeys = Get-ExpectedAssetKeys
    $results = @()

    $themeDirs = Get-ChildItem -Path $ThemesDir -Directory
    foreach ($dir in $themeDirs) {
        $themeName = $dir.Name
        $themeJsonPath = Join-Path $dir.FullName 'theme.json'

        $result = @{
            Theme         = $themeName
            DisplayName   = ''
            RenderMode    = ''
            AssetCount    = 0
            BaseAssets     = $expectedKeys.Count
            MissingFiles  = @()
            InvalidGlyphs = @()
            Status        = 'OK'
        }

        # Check 1: theme.json exists and is valid JSON
        if (-not (Test-Path $themeJsonPath)) {
            $result.Status = 'ERROR: theme.json not found'
            $results += $result
            continue
        }

        try {
            $theme = Get-Content -Path $themeJsonPath -Raw | ConvertFrom-Json
        }
        catch {
            $result.Status = "ERROR: Invalid JSON - $($_.Exception.Message)"
            $results += $result
            continue
        }

        # Check 2: Required fields
        $missingFields = @()
        if (-not $theme.name) { $missingFields += 'name' }
        if (-not $theme.displayName) { $missingFields += 'displayName' }
        if ($null -eq $theme.assets) { $missingFields += 'assets' }

        if ($missingFields.Count -gt 0) {
            $result.Status = "ERROR: Missing fields: $($missingFields -join ', ')"
            $results += $result
            continue
        }

        $result.DisplayName = $theme.displayName

        # Check 7: Render mode
        if ($theme.renderMode) {
            $result.RenderMode = $theme.renderMode
            if ($theme.renderMode -notin @('image', 'font')) {
                $result.Status = "ERROR: Invalid renderMode '$($theme.renderMode)'"
                $results += $result
                continue
            }
        }
        else {
            $result.RenderMode = '(none)'
        }

        # Check 3: Asset file resolution
        $assetProps = @()
        if ($theme.assets -and $theme.assets.PSObject.Properties) {
            $assetProps = @($theme.assets.PSObject.Properties)
        }
        $result.AssetCount = $assetProps.Count

        $missingFiles = @()
        foreach ($prop in $assetProps) {
            $assetPath = $prop.Value
            # Asset paths are relative to react-ui/public
            $fullPath = Join-Path $RepoRoot 'react-ui' 'public' $assetPath.TrimStart('/')
            if (-not (Test-Path $fullPath)) {
                $missingFiles += $assetPath
            }
        }
        $result.MissingFiles = $missingFiles

        # Check 4: Base completeness
        if ($themeName -eq 'base') {
            $currentKeys = @($assetProps.Name)
            $missing = $expectedKeys | Where-Object { $_ -notin $currentKeys }
            if ($missing.Count -gt 0) {
                # Not an error, just informational
            }
        }

        # Check 6: Glyph name resolution (font themes)
        $invalidGlyphs = @()
        if ($theme.colors) {
            if ($theme.colors.tiles -and $theme.colors.tiles.PSObject.Properties) {
                foreach ($prop in $theme.colors.tiles.PSObject.Properties) {
                    $tile = $prop.Value
                    if ($tile.glyph -and $tile.glyph -notin $validGlyphs) {
                        $invalidGlyphs += "tiles.$($prop.Name).glyph=$($tile.glyph)"
                    }
                }
            }
            if ($theme.colors.harbors -and $theme.colors.harbors.PSObject.Properties) {
                foreach ($prop in $theme.colors.harbors.PSObject.Properties) {
                    $harbor = $prop.Value
                    if ($harbor.harborGlyph -and $harbor.harborGlyph -notin $validGlyphs) {
                        $invalidGlyphs += "harbors.$($prop.Name).harborGlyph=$($harbor.harborGlyph)"
                    }
                    if ($harbor.hexGlyph -and $harbor.hexGlyph -notin $validGlyphs) {
                        $invalidGlyphs += "harbors.$($prop.Name).hexGlyph=$($harbor.hexGlyph)"
                    }
                }
            }
        }
        $result.InvalidGlyphs = $invalidGlyphs

        # Set final status
        $errors = @()
        if ($missingFiles.Count -gt 0) {
            $errors += "$($missingFiles.Count) missing asset file(s)"
        }
        if ($invalidGlyphs.Count -gt 0) {
            $errors += "$($invalidGlyphs.Count) invalid glyph name(s)"
        }
        if ($errors.Count -gt 0) {
            $result.Status = "ERROR: $($errors -join '; ')"
        }

        $results += $result
    }

    return $results
}

# Run the doctor command
$results = Test-ThemeDoctor

if ($HashTable) {
    return $results
}
elseif ($Json) {
    $results | ConvertTo-Json -Depth 4 | Write-Output
}
elseif ($Table) {
    $results | ForEach-Object {
        [PSCustomObject]@{
            Theme         = $_.Theme
            DisplayName   = $_.DisplayName
            RenderMode    = $_.RenderMode
            Assets        = $_.AssetCount
            BaseAssets    = $_.BaseAssets
            MissingFiles  = ($_.MissingFiles -join ', ')
            InvalidGlyphs = ($_.InvalidGlyphs -join ', ')
            Status        = $_.Status
        }
    } | Format-Table -AutoSize -Wrap
}
