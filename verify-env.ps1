<#
.SYNOPSIS
  Verifies a Windows + VS Code + .NET environment for a multi-project solution,
  with special handling for WinUI 3 (Windows App SDK) projects.

.DESCRIPTION
  - Detects Visual Studio (including Preview) and MSBuild via vswhere.
  - Reads the .sln, enumerates all .csproj files.
  - Uses "dotnet msbuild -getProperty" to fetch evaluated TargetFramework/TargetFrameworks.
  - Detects WinUI 3 via WindowsAppSDK/UseWinUI or a -windows TFM suffix.
  - Checks Windows SDK reference assemblies match any -windows10.x.y.z requirement.
  - Checks VS Code extensions (C# + C# Dev Kit) and .NET SDK presence.
#>

[CmdletBinding()]
param(
  [string] $SolutionPath = (Get-Location).Path
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

function Write-Header($text) { Write-Host "`n=== $text ===" -ForegroundColor Cyan }
function Ok($text)          { Write-Host "  ✔ $text" -ForegroundColor Green }
function Warn($text)        { Write-Host "  ✖ $text" -ForegroundColor Yellow }
function Info($text)        { Write-Host "    $text" -ForegroundColor Gray }

function Get-SolutionFile([string]$path) {
  $resolved = Resolve-Path -LiteralPath $path
  if ((Test-Path -LiteralPath $resolved -PathType Leaf) -and $resolved.Path.ToLower().EndsWith('.sln')) {
    return Get-Item -LiteralPath $resolved.Path
  }
  return Get-ChildItem -LiteralPath $resolved.Path -Filter *.sln -Recurse -ErrorAction SilentlyContinue | Select-Object -First 1
}

function Get-ProjectsFromSolution([System.IO.FileInfo]$sln) {
  $root = $sln.Directory.FullName
  $lines = Get-Content -LiteralPath $sln.FullName
  $projPaths = foreach ($line in $lines) {
    if ($line -match 'Project\(".*"\)\s*=\s*".*?",\s*"(.*?)",\s*".*?"') {
      $rel = $Matches[1]
      if ($rel -like '*.csproj') {
        $abs = Resolve-Path -LiteralPath (Join-Path $root $rel) -ErrorAction SilentlyContinue
        if ($abs) { $abs.Path }
      }
    }
  }
  $projPaths | Sort-Object -Unique
}

# Evaluated MSBuild property (handles Directory.Build.props, conditions, etc.)
function Get-MSBuildProperty([string]$projectPath, [string]$propertyName) {
  $dotnet = Get-Command dotnet -ErrorAction SilentlyContinue
  if (-not $dotnet) { return $null }
  $out = & $dotnet.Path msbuild $projectPath /nologo -v:quiet -property:DesignTimeBuild=true -getProperty:$propertyName 2>$null
  if ($LASTEXITCODE -ne 0) { return $null }
  # dotnet msbuild prints "Project \"...csproj\"" header sometimes—strip it
  $lines = $out | Where-Object { $_ -and ($_ -notmatch '^\s*Project\s+"') }
  $value = ($lines -join "`n").Trim()
  if ([string]::IsNullOrWhiteSpace($value)) { return $null }
  return $value
}

function Get-ProjectInfo([string]$csprojPath) {
  $tfm  = Get-MSBuildProperty -projectPath $csprojPath -propertyName 'TargetFramework'
  $tfms = Get-MSBuildProperty -projectPath $csprojPath -propertyName 'TargetFrameworks'
  $useWinUI = (Get-MSBuildProperty -projectPath $csprojPath -propertyName 'UseWinUI')

  $allTfms = @()
  if ($tfms) { $allTfms = $tfms.Split(';') }
  elseif ($tfm) { $allTfms = @($tfm) }

  $winSuffix = $null
  foreach ($t in $allTfms) {
    if ($t -match 'windows(?<sdk>\d{2}\.\d{1,2}\.\d{4,5}\.\d+)') { $winSuffix = $Matches['sdk']; break }
  }

  # Fallback check for WindowsAppSDK by reading XML/ text if MSBuild property checks aren’t enough
  $hasAppSdk = $false
  try {
    $text = Get-Content -LiteralPath $csprojPath -Raw
    if ($text -match 'Microsoft\.WindowsAppSDK') { $hasAppSdk = $true }
  } catch { }

  [pscustomobject]@{
    Path             = $csprojPath
    Name             = [IO.Path]::GetFileNameWithoutExtension($csprojPath)
    TargetFrameworks = if ($allTfms) { $allTfms -join ';' } else { '(none)' }
    WindowsSdkSuffix = $winSuffix
    IsWinUI3         = ($hasAppSdk -or ($useWinUI -and $useWinUI.ToString().Trim().ToLower() -eq 'true') -or $winSuffix)
  }
}

# 0) OS
Write-Header "Host OS"
if (-not $IsWindows) { Warn "Not running on Windows."; return }
Ok "Windows host detected."

# 1) Solution & projects
Write-Header "Solution & project discovery"
$sln = Get-SolutionFile -path $SolutionPath
if (-not $sln) { Warn "No .sln found under '$SolutionPath'."; return }
Ok "Solution: $($sln.FullName)"

$projects = Get-ProjectsFromSolution -sln $sln
if (-not $projects) { Warn "No C# projects (.csproj) found in solution."; return }
Ok "Found $($projects.Count) projects:"
$projects | ForEach-Object { Info " - $_" }

# 2) VS Code
Write-Header "VS Code & extensions"
$codeCmd = (Get-Command code -ErrorAction SilentlyContinue) ?? (Get-Command 'code.cmd' -ErrorAction SilentlyContinue)
if ($codeCmd) {
  Ok "VS Code CLI: $($codeCmd.Path)"
  try {
    $exts = & $codeCmd.Path --list-extensions 2>$null
    if ($exts -match '^ms-dotnettools\.csharp$')   { Ok "C# extension installed (ms-dotnettools.csharp)." } else { Warn "C# extension not installed."; Info "Fix: code --install-extension ms-dotnettools.csharp" }
    if ($exts -match '^ms-dotnettools\.csdevkit$') { Ok "C# Dev Kit installed (ms-dotnettools.csdevkit)." } else { Warn "C# Dev Kit not installed.";   Info "Fix: code --install-extension ms-dotnettools.csdevkit" }
    if ($exts -match '^ms-vscode\.csharp$')        { Warn "Legacy OmniSharp extension detected (ms-vscode.csharp)."; Info "Fix: code --uninstall-extension ms-vscode.csharp" }
  } catch {
    Warn "Could not query VS Code extensions."; Info "Ensure VS Code is installed and 'code' is on PATH."
  }
} else {
  Warn "VS Code CLI not found."; Info "Install VS Code and ensure 'code' is on PATH."
}

# 3) .NET SDK
Write-Header ".NET SDK"
$dotnet = Get-Command dotnet -ErrorAction SilentlyContinue
if (-not $dotnet) { Warn "dotnet CLI not found."; Info "Fix: Install .NET SDK."; }
else {
  Ok "dotnet: $($dotnet.Path)"
  $sdks = & $dotnet.Path --list-sdks
  if ($sdks) { Ok "Installed SDKs:`n$($sdks -join "`n")" } else { Warn "No .NET SDKs found."; Info "Fix: Install a .NET SDK (e.g., 8/9)." }
}

# 4) VS / MSBuild / Windows SDK (Preview included)
Write-Header "Visual Studio / MSBuild / Windows SDK"
$vswhere = Join-Path ${env:ProgramFiles(x86)} 'Microsoft Visual Studio\Installer\vswhere.exe'
if (-not (Test-Path $vswhere)) {
  Warn "vswhere.exe not found."; Info "Fix: Install Visual Studio Build Tools 2022+."
} else {
  $msbuildExe = & $vswhere -latest -products * -prerelease -requires Microsoft.Component.MSBuild -find '**\MSBuild.exe' 2>$null
  if ([string]::IsNullOrWhiteSpace($msbuildExe)) { Warn "MSBuild not found via vswhere (including prerelease)."; Info "Fix: In VS Installer, enable '.NET desktop development' (MSBuild)." }
  else { Ok "MSBuild: $msbuildExe" }
  $kitsRoot = "C:\Program Files (x86)\Windows Kits\10"
  if (Test-Path $kitsRoot) { Ok "Windows Kits folder present." } else { Warn "Windows 10/11 SDK not detected at '$kitsRoot'."; Info "Fix: Install the Windows SDK via VS Installer." }
}

# 5) Per-project validation (evaluated)
Write-Header "Per-project validation"
$projInfos = foreach ($p in $projects) {
  try { Get-ProjectInfo -csprojPath $p } catch {
    [pscustomobject]@{ Path=$p; Name=[IO.Path]::GetFileNameWithoutExtension($p); TargetFrameworks='(error)'; WindowsSdkSuffix=$null; IsWinUI3=$false }
  }
}

foreach ($pi in $projInfos) {
  Write-Host ""
  Write-Host "Project: $($pi.Name)" -ForegroundColor White
  Info "Path              : $($pi.Path)"
  Info "TargetFramework(s): $($pi.TargetFrameworks)"
  if ($pi.WindowsSdkSuffix) { Ok "Windows TFM suffix : $($pi.WindowsSdkSuffix)" } else { Info "Windows TFM suffix : (none detected)" }
  if ($pi.IsWinUI3) { Ok "WinUI 3 / WindowsAppSDK: detected" } else { Info "WinUI 3 / WindowsAppSDK: not detected" }

  if ($pi.WindowsSdkSuffix) {
    $refs = Join-Path "C:\Program Files (x86)\Windows Kits\10\References" $pi.WindowsSdkSuffix
    if (Test-Path $refs) { Ok "Windows SDK refs   : found ($refs)" }
    else {
      Warn "Windows SDK refs   : NOT found for $($pi.WindowsSdkSuffix)"
      $installed = Get-ChildItem "C:\Program Files (x86)\Windows Kits\10\References" -Directory -ErrorAction SilentlyContinue | Select-Object -ExpandProperty Name
      if ($installed) { Info ("Installed SDK refs: " + ($installed -join ', ')) }
      Info "Fix: Install Windows SDK $($pi.WindowsSdkSuffix) OR retarget the TFM to an installed SDK."
    }
  }
}

# 6) Summary / next steps
Write-Header "Summary"
$winui = $projInfos | Where-Object { $_.IsWinUI3 }
if ($winui) { Ok "Detected potential WinUI 3 projects:"; $winui | ForEach-Object { Info " - $($_.Name)" } }
else { Info "No WinUI 3 projects detected (no WindowsAppSDK/UseWinUI and no -windows TFM suffix)." }

Write-Header "Next actions"
Info "If IntelliSense is broken for WinUI projects:"
Info "  • Install/repair Windows SDK matching the TFM suffix (or retarget)."
Info "  • VS Code: Developer: Restart C# Language Server"
Info "  • VS Code: Developer: Reload Window"
Info "  • Ensure C# Dev Kit is using Visual Studio's MSBuild (auto-detected when VS is installed)."
Info "  • Clean build: remove bin/ & obj/, then: dotnet restore; dotnet build"
