#!/usr/bin/env pwsh
<#
.SYNOPSIS
  Build the Catan TTF icon font from SVGs and install to all project locations.

.DESCRIPTION
  Builds Catan.ttf from SVG files using the TypeScript pipeline (svgicons2svgfont + svg2ttf).

  All paths are derived from the script location, so running from project root with no
  arguments is the default workflow:

    pwsh ./.assets/SVG\ For\ Font/create-catan-font.ps1

  After building, the font is copied to all project locations that need it:
    - Catan3.GameService/wwwroot/fonts/     (primary build output)
    - react-ui/public/fonts/                (Next.js)
    - react-ui/public/themes/base/fonts/    (theme)
    - WebUI/wwwroot/themes/base/fonts/      (Blazor)
    - DesktopApp/Assets/Fonts/              (WinUI3)

  The font is also installed to the OS per-user font directory so it appears in
  system tools (charmap, font viewers, etc.). Use -NoInstall to skip this.

  The .next cache is cleared automatically since Next.js hashes font files.

.PARAMETER Help
  Show this help message.

.PARAMETER SkipInstall
  Build the font but don't copy to other project locations.

.PARAMETER NoInstall
  Skip installing the font to the OS per-user font directory.

.PARAMETER SkipFix
  Skip running fix-svg-for-font.ps1 before building. By default, the fix
  script is called first to clean up SVGs (remove orphaned tags, fix fill-rules, etc.).

.PARAMETER SkipClearCache
  Don't clear the .next cache after installing.

.EXAMPLE
  pwsh ./.assets/SVG\ For\ Font/create-catan-font.ps1

.PARAMETER Command
  Optional verb. Use 'update' to sync glyph-map.json with SVG files in the directory
  (adds missing SVGs with the next available codepoints, preserving existing mappings).

.EXAMPLE
  pwsh ./.assets/SVG\ For\ Font/create-catan-font.ps1

.EXAMPLE
  pwsh ./.assets/SVG\ For\ Font/create-catan-font.ps1 update

.EXAMPLE
  pwsh ./.assets/SVG\ For\ Font/create-catan-font.ps1 -SkipInstall

.EXAMPLE
  pwsh ./.assets/SVG\ For\ Font/create-catan-font.ps1 -NoInstall
#>

[CmdletBinding()]
param(
  [Parameter(Position = 0)]
  [string] $Command,
  [switch] $Help,
  [switch] $SkipInstall,
  [switch] $NoInstall,
  [switch] $SkipClearCache,
  [switch] $SkipFix
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

if ($Help) {
  Get-Help $PSCommandPath -Detailed
  return
}

# ── Derived paths ──
# Script lives in: <ProjectRoot>/.assets/SVG For Font/
$ScriptDir   = $PSScriptRoot
$ProjectRoot = (Resolve-Path (Join-Path $ScriptDir "../..")).Path

$InputDir    = $ScriptDir
$FontName    = "Catan"
$OutputDir   = Join-Path $ProjectRoot "Catan3.GameService/wwwroot/fonts"
$MapFile     = Join-Path $ScriptDir "glyph-map.json"
$TsSource    = Join-Path $ScriptDir "svg-font.ts"
$WorkDir     = Join-Path $ProjectRoot ".iconfont-build"
$StartHex    = "E000"

# ── Update command: sync glyph-map.json with SVG files ──
if ($Command -eq 'update') {
  Write-Host "Updating glyph-map.json..." -ForegroundColor Cyan

  # Read existing map
  $mapContent = Get-Content -LiteralPath $MapFile -Raw | ConvertFrom-Json
  $existingMap = [ordered]@{}
  foreach ($prop in $mapContent.PSObject.Properties) {
    if ($prop.Name -eq '_comment') { continue }
    $existingMap[$prop.Name] = $prop.Value
  }

  # Find highest existing codepoint
  $maxCodepoint = 0
  foreach ($hex in $existingMap.Values) {
    $val = [Convert]::ToInt32($hex, 16)
    if ($val -gt $maxCodepoint) { $maxCodepoint = $val }
  }

  # Scan directory for SVG files
  $svgFiles = Get-ChildItem -LiteralPath $InputDir -Filter "*.svg" | Sort-Object Name
  $newFiles = @()
  foreach ($svg in $svgFiles) {
    if (-not $existingMap.Contains($svg.Name)) {
      $newFiles += $svg.Name
    }
  }

  if ($newFiles.Count -eq 0) {
    Write-Host "  No new SVGs found. glyph-map.json is up to date." -ForegroundColor Green
    return
  }

  # Assign next sequential codepoints
  $nextCodepoint = $maxCodepoint + 1
  Write-Host "  Found $($newFiles.Count) new SVG(s):" -ForegroundColor Yellow
  foreach ($name in $newFiles) {
    $hex = $nextCodepoint.ToString("X4")
    $existingMap[$name] = $hex
    Write-Host "    $name -> $hex" -ForegroundColor Green
    $nextCodepoint++
  }

  # Write updated map preserving order: _comment first, then all entries
  $lines = @()
  $lines += '{'
  $lines += '  "_comment": "Maps every SVG filename to a fixed Unicode codepoint. All assignments are explicit for idempotent builds.",'
  $lines += ''
  $entries = @($existingMap.GetEnumerator())
  for ($i = 0; $i -lt $entries.Count; $i++) {
    $key = $entries[$i].Key
    $val = $entries[$i].Value
    $comma = if ($i -lt $entries.Count - 1) { ',' } else { '' }
    $lines += "  `"$key`": `"$val`"$comma"
  }
  $lines += '}'
  $lines += ''
  $newContent = $lines -join "`n"
  Set-Content -LiteralPath $MapFile -Value $newContent -NoNewline -Encoding UTF8

  Write-Host ""
  Write-Host "  Updated $MapFile" -ForegroundColor Green
  Write-Host "  Total glyphs: $($existingMap.Count) ($($newFiles.Count) added)" -ForegroundColor Cyan
  Write-Host ""
  Write-Host "  Next steps:" -ForegroundColor Yellow
  Write-Host "    1. Add entries to react-ui/lib/constants/catanGlyphs.ts" -ForegroundColor Yellow
  Write-Host "    2. Run: pwsh './.assets/SVG For Font/create-catan-font.ps1'" -ForegroundColor Yellow
  return
}

# Destinations to copy the built font (relative to project root)
$CopyTargets = @(
  "react-ui/public/fonts"
  "react-ui/public/themes/base/fonts"
  "WebUI/wwwroot/themes/base/fonts"
  "DesktopApp/Assets/Fonts"
)

$NextCacheDir = Join-Path $ProjectRoot "react-ui/.next"

# ── Helpers ──

function Assert-Command {
  param([Parameter(Mandatory)][string] $Name)
  if (-not (Get-Command $Name -ErrorAction SilentlyContinue)) {
    throw "Required command not found: $Name. Please install it and retry."
  }
}

function Ensure-Dir {
  param([Parameter(Mandatory)][string] $Path)
  if (-not (Test-Path -LiteralPath $Path)) {
    New-Item -ItemType Directory -Path $Path | Out-Null
  }
}

function Get-LockingProcesses {
  param([Parameter(Mandatory)][string] $FilePath)
  # Use the Windows Restart Manager API to find processes locking a file
  if (-not $IsWindows) { return @() }
  try {
    Add-Type -ErrorAction Stop -TypeDefinition @'
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;

public static class RestartManager {
    [DllImport("rstrtmgr.dll", CharSet = CharSet.Unicode)]
    static extern int RmStartSession(out uint pSessionHandle, int dwSessionFlags, string strSessionKey);

    [DllImport("rstrtmgr.dll")]
    static extern int RmEndSession(uint pSessionHandle);

    [DllImport("rstrtmgr.dll", CharSet = CharSet.Unicode)]
    static extern int RmRegisterResources(uint pSessionHandle,
        uint nFiles, string[] rgsFileNames,
        uint nApplications, [In] RM_UNIQUE_PROCESS[] rgApplications,
        uint nServices, string[] rgsServiceNames);

    [DllImport("rstrtmgr.dll")]
    static extern int RmGetList(uint dwSessionHandle,
        out uint pnProcInfoNeeded, ref uint pnProcInfo,
        [In, Out] RM_PROCESS_INFO[] rgAffectedApps, ref uint lpdwRebootReasons);

    [StructLayout(LayoutKind.Sequential)]
    struct RM_UNIQUE_PROCESS {
        public int dwProcessId;
        public System.Runtime.InteropServices.ComTypes.FILETIME ProcessStartTime;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    struct RM_PROCESS_INFO {
        public RM_UNIQUE_PROCESS Process;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 256)]
        public string strAppName;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 64)]
        public string strServiceShortName;
        public int ApplicationType;
        public uint AppStatus;
        public uint TSSessionId;
        [MarshalAs(UnmanagedType.Bool)]
        public bool bRestartable;
    }

    public static string[] FindLockingProcesses(string filePath) {
        uint sessionHandle;
        string sessionKey = Guid.NewGuid().ToString();
        int rv = RmStartSession(out sessionHandle, 0, sessionKey);
        if (rv != 0) return new string[0];
        try {
            rv = RmRegisterResources(sessionHandle, 1, new[] { filePath }, 0, null, 0, null);
            if (rv != 0) return new string[0];
            uint needed = 0, count = 0, reasons = 0;
            rv = RmGetList(sessionHandle, out needed, ref count, null, ref reasons);
            if (needed == 0) return new string[0];
            var infos = new RM_PROCESS_INFO[needed];
            count = needed;
            rv = RmGetList(sessionHandle, out needed, ref count, infos, ref reasons);
            if (rv != 0) return new string[0];
            var result = new List<string>();
            for (int i = 0; i < count; i++) {
                result.Add(infos[i].strAppName + " (PID " + infos[i].Process.dwProcessId + ")");
            }
            return result.ToArray();
        } finally {
            RmEndSession(sessionHandle);
        }
    }
}
'@
    $procs = [RestartManager]::FindLockingProcesses($FilePath)
    return $procs
  } catch {
    return @()
  }
}

function Write-FileIfChanged {
  param(
    [Parameter(Mandatory)][string] $Path,
    [Parameter(Mandatory)][string] $Content
  )
  $dir = Split-Path -Parent $Path
  if ($dir) { Ensure-Dir -Path $dir }
  if (Test-Path -LiteralPath $Path) {
    $existing = Get-Content -LiteralPath $Path -Raw
    if ($existing -eq $Content) { return }
  }
  Set-Content -LiteralPath $Path -Value $Content -NoNewline
}

# ── Preflight ──

Assert-Command -Name "node"
Assert-Command -Name "npm"

if (-not (Test-Path -LiteralPath $TsSource)) {
  throw "Cannot find svg-font.ts at: $TsSource"
}

if (-not (Test-Path -LiteralPath $MapFile)) {
  Write-Warning "Glyph map not found at: $MapFile (auto-assignment will be used)"
  $MapFile = $null
}

Ensure-Dir -Path $OutputDir

# ── Verify hex SVG aspect ratios ──
# Flat-top hex: height/width = √3/2 ≈ 0.8660 (from hex-geometry.ts).
# The font normalizer (--normalize) scales each glyph independently, so
# absolute viewBox size doesn't matter — only the aspect ratio matters.
$hexRatio = [Math]::Sqrt(3) / 2  # ≈ 0.8660254
$hexSvgs = Get-ChildItem -LiteralPath $InputDir -Filter "*.svg" | Where-Object { $_.Name -match 'hex' }
$hexWarnings = 0
foreach ($hexSvg in $hexSvgs) {
  $svgContent = Get-Content -LiteralPath $hexSvg.FullName -Raw
  $vbMatch = [regex]::Match($svgContent, 'viewBox="(-?[\d.]+)\s+(-?[\d.]+)\s+([\d.]+)\s+([\d.]+)"')
  if ($vbMatch.Success) {
    $vbW = [double]$vbMatch.Groups[3].Value
    $vbH = [double]$vbMatch.Groups[4].Value
    $correctH = [Math]::Round($vbW * $hexRatio)
    if ($vbH -ne $correctH) {
      $actualRatio = $vbH / $vbW
      Write-Warning "  $($hexSvg.Name): aspect $('{0:F3}' -f $actualRatio), expected $('{0:F3}' -f $hexRatio) (viewBox ${vbW}x${vbH}, should be ${vbW}x${correctH})"
      $hexWarnings++
    }
  }
}
if ($hexWarnings -gt 0) {
  Write-Warning "$hexWarnings hex SVG(s) have wrong aspect ratio. Run fix-svg-for-font.ps1 to correct."
}

$resolvedInput  = (Resolve-Path -LiteralPath $InputDir).Path
$resolvedOutput = (Resolve-Path -LiteralPath $OutputDir).Path
$resolvedMap    = if ($MapFile) { (Resolve-Path -LiteralPath $MapFile).Path } else { $null }

# ── Set up Node project ──

Ensure-Dir -Path $WorkDir
$resolvedWork = (Resolve-Path -LiteralPath $WorkDir).Path

Push-Location $resolvedWork
try {
  if (-not (Test-Path -LiteralPath (Join-Path $resolvedWork "package.json"))) {
    $initPkg = '{ "name": "iconfont-build", "version": "1.0.0", "private": true, "scripts": {} }'
    Set-Content -LiteralPath (Join-Path $resolvedWork "package.json") -Value $initPkg -NoNewline
  }

  npm install svgicons2svgfont@^15.0.1 svg2ttf@^6.0.3 fast-glob@^3.3.3 yargs@^17.7.2 svg-reorient@^1.0.3 | Out-Null
  npm install -D typescript@^5.7.3 tsx@^4.19.2 "@types/node@^22.10.7" | Out-Null

  $tsconfig = @'
{
  "compilerOptions": {
    "target": "ES2022",
    "lib": ["ES2022"],
    "module": "NodeNext",
    "moduleResolution": "NodeNext",
    "strict": true,
    "esModuleInterop": true,
    "skipLibCheck": true,
    "outDir": "dist"
  },
  "include": ["src/**/*.ts"]
}
'@
  Write-FileIfChanged -Path (Join-Path $resolvedWork "tsconfig.json") -Content $tsconfig

  $tsDest = Join-Path $resolvedWork "src/build-icon-font.ts"
  Ensure-Dir -Path (Join-Path $resolvedWork "src")
  Copy-Item -LiteralPath $TsSource -Destination $tsDest -Force

  # ── Run fix-svg-for-font.ps1 (unless -SkipFix) ──
  if (-not $SkipFix) {
    $fixScript = Join-Path $ScriptDir "fix-svg-for-font.ps1"
    if (Test-Path -LiteralPath $fixScript) {
      Write-Host ""
      Write-Host "Running fix-svg-for-font.ps1..." -ForegroundColor Cyan
      & $fixScript
      if ($LASTEXITCODE -and $LASTEXITCODE -ne 0) {
        throw "fix-svg-for-font.ps1 failed with exit code $LASTEXITCODE"
      }
    } else {
      Write-Warning "fix-svg-for-font.ps1 not found at: $fixScript (skipping)"
    }
  } else {
    Write-Host "  Skipping SVG fix step (-SkipFix)" -ForegroundColor DarkGray
  }

  # ── Convert evenodd → nonzero winding for hex glyphs ──
  # svgicons2svgfont ignores fill-rule:evenodd (known issue #62), so compound
  # paths render as solid blobs. reorient-evenodd.mjs uses svg-reorient to fix
  # subpath winding directions, producing the same visual under nonzero.
  # Only hex SVGs need this — they have 100-400 subpaths in compound paths.

  $reorientScript = Join-Path $ScriptDir "reorient-evenodd.mjs"
  $hexEvenoddSvgs = @(Get-ChildItem -LiteralPath $resolvedInput -Filter "*hex*.svg" | Where-Object {
    $c = Get-Content $_.FullName -Raw
    $c -match 'fill-rule[:\s]*evenodd'
  })

  if ($hexEvenoddSvgs.Count -gt 0) {
    Write-Host ""
    Write-Host "Converting $($hexEvenoddSvgs.Count) hex SVG(s) from evenodd to nonzero winding..." -ForegroundColor Cyan
    $reorientArgs = @($reorientScript) + ($hexEvenoddSvgs | ForEach-Object { $_.FullName })
    node @reorientArgs
    if ($LASTEXITCODE -ne 0) {
      throw "Evenodd-to-nonzero conversion failed"
    }
  }

  # ── Build ──

  Write-Host ""
  Write-Host "Building $FontName.ttf from SVGs..." -ForegroundColor Cyan
  Write-Host "  Input : $resolvedInput"
  Write-Host "  Output: $resolvedOutput"
  if ($resolvedMap) {
    Write-Host "  Map   : $resolvedMap"
  }

  # Log which SVGs will be included
  $svgFiles = Get-ChildItem -LiteralPath $resolvedInput -Filter "*.svg" | Sort-Object Name
  Write-Host "  SVGs  : $($svgFiles.Count) files" -ForegroundColor Cyan
  foreach ($svg in $svgFiles) {
    Write-Host "          $($svg.Name)" -ForegroundColor DarkGray
  }

  $buildArgs = @(
    "src/build-icon-font.ts",
    "--input", $resolvedInput,
    "--output", $resolvedOutput,
    "--name", $FontName,
    "--start", $StartHex,
    "--normalize",
    "--centerHorizontally"
  )

  if ($resolvedMap) {
    $buildArgs += @("--map", $resolvedMap)
  }

  npx tsx @buildArgs | Write-Host

  if ($LASTEXITCODE -ne 0) {
    throw "Font build failed with exit code $LASTEXITCODE"
  }

  Write-Host ""
  Write-Host "Build complete." -ForegroundColor Green
}
finally {
  Pop-Location
}

# ── Install to all project locations ──

if (-not $SkipInstall) {
  $builtTtf = Join-Path $resolvedOutput "$FontName.ttf"
  if (-not (Test-Path -LiteralPath $builtTtf)) {
    throw "Built font not found: $builtTtf"
  }

  Write-Host ""
  Write-Host "Installing $FontName.ttf to project locations..." -ForegroundColor Cyan

  foreach ($rel in $CopyTargets) {
    $dest = Join-Path $ProjectRoot $rel
    if (Test-Path -LiteralPath $dest -PathType Container) {
      $destFile = Join-Path $dest "$FontName.ttf"
      # Handle EBUSY: rename-then-copy
      if (Test-Path -LiteralPath $destFile) {
        $backup = "$destFile.old"
        try {
          Move-Item -LiteralPath $destFile -Destination $backup -Force -ErrorAction Stop
          Copy-Item -LiteralPath $builtTtf -Destination $destFile -Force
          Remove-Item -LiteralPath $backup -Force -ErrorAction SilentlyContinue
        } catch {
          # Restore backup if copy failed
          if (Test-Path -LiteralPath $backup) {
            Move-Item -LiteralPath $backup -Destination $destFile -Force -ErrorAction SilentlyContinue
          }
          $lockers = @(Get-LockingProcesses -FilePath $destFile)
          if ($lockers.Count -gt 0) {
            Write-Warning "  SKIP $rel (file locked by: $($lockers -join ', '))"
          } else {
            Write-Warning "  SKIP $rel (file locked: $_)"
          }
          continue
        }
      } else {
        Copy-Item -LiteralPath $builtTtf -Destination $destFile -Force
      }
      Write-Host "  OK  $rel" -ForegroundColor Green
    } else {
      Write-Host "  SKIP $rel (directory not found)" -ForegroundColor DarkGray
    }
  }
}

# ── Install to OS ──

if (-not $NoInstall) {
  $builtTtf = Join-Path $resolvedOutput "$FontName.ttf"
  if (-not (Test-Path -LiteralPath $builtTtf)) {
    throw "Built font not found: $builtTtf"
  }

  Write-Host ""
  Write-Host "Installing $FontName.ttf to OS fonts..." -ForegroundColor Cyan

  if ($IsWindows) {
    # Per-user font directory (no admin required)
    $userFontsDir = Join-Path $env:LOCALAPPDATA "Microsoft\Windows\Fonts"
    Ensure-Dir -Path $userFontsDir
    $osDest = Join-Path $userFontsDir "$FontName.ttf"

    # Handle EBUSY: rename-then-copy (font may be loaded by a process)
    $osInstalled = $false
    if (Test-Path -LiteralPath $osDest) {
      $backup = "$osDest.old"
      try {
        Move-Item -LiteralPath $osDest -Destination $backup -Force -ErrorAction Stop
        Copy-Item -LiteralPath $builtTtf -Destination $osDest -Force
        Remove-Item -LiteralPath $backup -Force -ErrorAction SilentlyContinue
        $osInstalled = $true
      } catch {
        # Restore backup if copy failed
        if (Test-Path -LiteralPath $backup) {
          Move-Item -LiteralPath $backup -Destination $osDest -Force -ErrorAction SilentlyContinue
        }
        # Report which processes are locking the file
        $lockers = @(Get-LockingProcesses -FilePath $osDest)
        if ($lockers.Count -gt 0) {
          Write-Warning "  SKIP OS font install (file locked by: $($lockers -join ', '))"
        } else {
          Write-Warning "  SKIP OS font install (file locked: $_)"
        }
      }
    } else {
      try {
        Copy-Item -LiteralPath $builtTtf -Destination $osDest -Force
        $osInstalled = $true
      } catch {
        $lockers = @(Get-LockingProcesses -FilePath $osDest)
        if ($lockers.Count -gt 0) {
          Write-Warning "  SKIP OS font install (file locked by: $($lockers -join ', '))"
        } else {
          Write-Warning "  SKIP OS font install (failed: $_)"
        }
      }
    }

    if ($osInstalled) {
      # Register in per-user font registry so Windows recognizes it
      try {
        $regPath = "HKCU:\SOFTWARE\Microsoft\Windows NT\CurrentVersion\Fonts"
        $regName = "$FontName (TrueType)"
        Set-ItemProperty -Path $regPath -Name $regName -Value $osDest -Type String
        Write-Host "  Registered in HKCU font registry" -ForegroundColor Green
      } catch {
        Write-Warning "  Failed to register font in registry: $($_.Exception.Message)"
      }

      Write-Host "  OK  $userFontsDir" -ForegroundColor Green
    }
  } elseif ($IsMacOS) {
    $userFontsDir = Join-Path $HOME "Library/Fonts"
    Ensure-Dir -Path $userFontsDir
    Copy-Item -LiteralPath $builtTtf -Destination (Join-Path $userFontsDir "$FontName.ttf") -Force
    Write-Host "  OK  ~/Library/Fonts/" -ForegroundColor Green
  } elseif ($IsLinux) {
    $userFontsDir = Join-Path $HOME ".local/share/fonts"
    Ensure-Dir -Path $userFontsDir
    Copy-Item -LiteralPath $builtTtf -Destination (Join-Path $userFontsDir "$FontName.ttf") -Force

    # Rebuild font cache
    if (Get-Command "fc-cache" -ErrorAction SilentlyContinue) {
      fc-cache -f $userFontsDir 2>&1 | Out-Null
      Write-Host "  OK  ~/.local/share/fonts/ (fc-cache updated)" -ForegroundColor Green
    } else {
      Write-Host "  OK  ~/.local/share/fonts/ (fc-cache not found, may need manual refresh)" -ForegroundColor Yellow
    }
  } else {
    Write-Host "  SKIP (unsupported OS)" -ForegroundColor Yellow
  }
}

# ── Clear .next cache ──
# In dev mode, layout.tsx loads the font via an API route with no-cache headers,
# so clearing .next is unnecessary (and destructive — it kills the running dev
# server's compilation cache). Only clear when doing a clean production build.

if (-not $SkipClearCache -and -not $SkipInstall) {
  # Detect if Next.js dev server is running (port 3000)
  $devRunning = $false
  try {
    $listener = Get-NetTCPConnection -LocalPort 3000 -State Listen -ErrorAction SilentlyContinue
    if ($listener) { $devRunning = $true }
  } catch { }

  if ($devRunning) {
    Write-Host ""
    Write-Host "Next.js dev server detected — skipping .next cache clear." -ForegroundColor Yellow
    Write-Host "  Hard-refresh (Ctrl+Shift+R) or press Ctrl+Shift+F to reload the font." -ForegroundColor Yellow
  } elseif (Test-Path -LiteralPath $NextCacheDir) {
    Write-Host ""
    Write-Host "Clearing .next cache..." -ForegroundColor Cyan
    Remove-Item -LiteralPath $NextCacheDir -Recurse -Force
    Write-Host "  Cleared react-ui/.next" -ForegroundColor Green
  }
}

# ── Summary ──

Write-Host ""
Write-Host "Done." -ForegroundColor Green
$ttfSize = (Get-Item -LiteralPath (Join-Path $resolvedOutput "$FontName.ttf")).Length
Write-Host "  $FontName.ttf: $([math]::Round($ttfSize / 1024))KB"
$mapPath = Join-Path $resolvedOutput "$FontName.map.json"
if (Test-Path -LiteralPath $mapPath) {
  $map = Get-Content -LiteralPath $mapPath -Raw | ConvertFrom-Json
  Write-Host "  Glyphs: $($map.glyphCount) ($($map.mappedCount) mapped, $($map.autoAssignedCount) auto-assigned)"
}
