#!/usr/bin/env pwsh
<#
.SYNOPSIS
  Remove all installed instances of the Catan font from the system.

.DESCRIPTION
  Removes the Catan TrueType font from:
    1. System-wide fonts directory (C:\Windows\Fonts) — requires elevation
    2. Per-user fonts directory (%LOCALAPPDATA%\Microsoft\Windows\Fonts)
    3. System font registry (HKLM) — requires elevation
    4. Per-user font registry (HKCU)
    5. Restarts the Windows Font Cache service to flush cached glyph data

  Run from an elevated (Administrator) PowerShell prompt to remove system-wide
  fonts. Per-user fonts and registry entries can be removed without elevation.

  On macOS/Linux, removes from the per-user fonts directory.

.PARAMETER FontName
  Name of the font to uninstall. Defaults to "Catan".

.PARAMETER Quiet
  Suppress informational output (errors and warnings still shown).

.EXAMPLE
  # From elevated PowerShell:
  pwsh './.assets/SVG For Font/uninstall-catan-font.ps1'

.EXAMPLE
  # Non-elevated (per-user only):
  pwsh './.assets/SVG For Font/uninstall-catan-font.ps1'
#>

[CmdletBinding()]
param(
  [string] $FontName = "Catan",
  [switch] $Quiet
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

function Write-Status {
  param([string] $Message, [string] $Color = "White")
  if (-not $Quiet) { Write-Host $Message -ForegroundColor $Color }
}

$removedAny = $false

# ── Windows ──
if ($IsWindows) {
  # Check for elevation
  $isAdmin = ([Security.Principal.WindowsPrincipal] `
    [Security.Principal.WindowsIdentity]::GetCurrent()
  ).IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)

  # 1. Stop Font Cache service to release file locks
  if ($isAdmin) {
    try {
      $fontCache = Get-Service -Name "FontCache" -ErrorAction SilentlyContinue
      if ($fontCache -and $fontCache.Status -eq 'Running') {
        Write-Status "Stopping FontCache service..." "Cyan"
        Stop-Service -Name "FontCache" -Force -ErrorAction Stop
        Write-Status "  FontCache stopped" "Green"
      }
    } catch {
      Write-Warning "Could not stop FontCache service: $($_.Exception.Message)"
    }

    # Also stop FontCache3.0.0.0 if it exists (older Windows versions)
    try {
      $fontCache3 = Get-Service -Name "FontCache3.0.0.0" -ErrorAction SilentlyContinue
      if ($fontCache3 -and $fontCache3.Status -eq 'Running') {
        Stop-Service -Name "FontCache3.0.0.0" -Force -ErrorAction SilentlyContinue
      }
    } catch { }
  }

  # 2. Remove from system fonts directory (C:\Windows\Fonts)
  $systemFontsDir = Join-Path $env:WINDIR "Fonts"
  $systemFont = Join-Path $systemFontsDir "$FontName.ttf"
  if (Test-Path -LiteralPath $systemFont) {
    if ($isAdmin) {
      try {
        Remove-Item -LiteralPath $systemFont -Force -ErrorAction Stop
        Write-Status "  Removed system font: $systemFont" "Green"
        $removedAny = $true
      } catch {
        Write-Warning "  Failed to remove system font: $($_.Exception.Message)"
      }
    } else {
      Write-Warning "  System font found at $systemFont — run elevated to remove"
    }
  }

  # 3. Remove from system registry (HKLM)
  $hklmPath = "HKLM:\SOFTWARE\Microsoft\Windows NT\CurrentVersion\Fonts"
  $hklmName = "$FontName (TrueType)"
  try {
    $hklmValue = Get-ItemProperty -Path $hklmPath -Name $hklmName -ErrorAction SilentlyContinue
    if ($hklmValue) {
      if ($isAdmin) {
        Remove-ItemProperty -Path $hklmPath -Name $hklmName -Force -ErrorAction Stop
        Write-Status "  Removed HKLM registry entry" "Green"
        $removedAny = $true
      } else {
        Write-Warning "  HKLM registry entry found — run elevated to remove"
      }
    }
  } catch {
    Write-Warning "  Failed to remove HKLM registry entry: $($_.Exception.Message)"
  }

  # 4. Remove from per-user fonts directory
  $userFontsDir = Join-Path $env:LOCALAPPDATA "Microsoft\Windows\Fonts"
  $userFont = Join-Path $userFontsDir "$FontName.ttf"
  if (Test-Path -LiteralPath $userFont) {
    try {
      Remove-Item -LiteralPath $userFont -Force -ErrorAction Stop
      Write-Status "  Removed per-user font: $userFont" "Green"
      $removedAny = $true
    } catch {
      Write-Warning "  Failed to remove per-user font: $($_.Exception.Message)"
    }
  }

  # Also clean up any .old backup files
  $userFontOld = "$userFont.old"
  if (Test-Path -LiteralPath $userFontOld) {
    Remove-Item -LiteralPath $userFontOld -Force -ErrorAction SilentlyContinue
  }

  # 5. Remove from per-user registry (HKCU)
  $hkcuPath = "HKCU:\SOFTWARE\Microsoft\Windows NT\CurrentVersion\Fonts"
  $hkcuName = "$FontName (TrueType)"
  try {
    $hkcuValue = Get-ItemProperty -Path $hkcuPath -Name $hkcuName -ErrorAction SilentlyContinue
    if ($hkcuValue) {
      Remove-ItemProperty -Path $hkcuPath -Name $hkcuName -Force -ErrorAction Stop
      Write-Status "  Removed HKCU registry entry" "Green"
      $removedAny = $true
    }
  } catch {
    Write-Warning "  Failed to remove HKCU registry entry: $($_.Exception.Message)"
  }

  # 6. Restart Font Cache service
  if ($isAdmin) {
    try {
      Start-Service -Name "FontCache" -ErrorAction SilentlyContinue
      Write-Status "  FontCache service restarted" "Green"
    } catch {
      Write-Warning "  Could not restart FontCache service: $($_.Exception.Message)"
    }
  }

  # 7. Broadcast WM_FONTCHANGE to notify applications
  try {
    Add-Type -ErrorAction SilentlyContinue -TypeDefinition @'
using System;
using System.Runtime.InteropServices;

public static class FontNotify {
    [DllImport("user32.dll")]
    public static extern IntPtr SendMessage(IntPtr hWnd, uint Msg, IntPtr wParam, IntPtr lParam);

    public static void BroadcastFontChange() {
        IntPtr HWND_BROADCAST = (IntPtr)0xffff;
        uint WM_FONTCHANGE = 0x001D;
        SendMessage(HWND_BROADCAST, WM_FONTCHANGE, IntPtr.Zero, IntPtr.Zero);
    }
}
'@
    [FontNotify]::BroadcastFontChange()
    Write-Status "  Broadcast WM_FONTCHANGE to all windows" "Green"
  } catch {
    # Silently ignore — type may already be loaded from a previous run
    try { [FontNotify]::BroadcastFontChange() } catch { }
  }

} elseif ($IsMacOS) {
  $userFont = Join-Path $HOME "Library/Fonts/$FontName.ttf"
  if (Test-Path -LiteralPath $userFont) {
    Remove-Item -LiteralPath $userFont -Force
    Write-Status "  Removed: $userFont" "Green"
    $removedAny = $true
  }

} elseif ($IsLinux) {
  $userFont = Join-Path $HOME ".local/share/fonts/$FontName.ttf"
  if (Test-Path -LiteralPath $userFont) {
    Remove-Item -LiteralPath $userFont -Force
    Write-Status "  Removed: $userFont" "Green"
    $removedAny = $true
    # Rebuild font cache
    if (Get-Command "fc-cache" -ErrorAction SilentlyContinue) {
      fc-cache -f (Join-Path $HOME ".local/share/fonts") 2>&1 | Out-Null
    }
  }
}

# ── Summary ──
if ($removedAny) {
  Write-Status ""
  Write-Status "$FontName font uninstalled." "Green"
} else {
  Write-Status ""
  Write-Status "No $FontName font installations found." "Yellow"
}
