<#
.SYNOPSIS
    Performance benchmark for game log operations.
.DESCRIPTION
    Replays a recording through the GameService with server-side per-action
    timing, then reports cost by action type and game state.

    Uses the recording replay API with ?perf=true to get accurate
    server-side measurements (no network overhead in the timings).

    Prerequisites: GameService must be running (./catan.ps1 run).
.EXAMPLE
    ./.scripts/perf.ps1
.EXAMPLE
    ./.scripts/perf.ps1 -Name "Simulated Regular Game"
.EXAMPLE
    ./catan.ps1 test -Perf
#>

param(
    [ValidateSet("ERROR", "WARN", "INFO", "DEBUG")]
    [Alias("LogLevel")]
    [string]$TraceLevel = "INFO",

    [string]$Name,
    [switch]$Help
)

$ErrorActionPreference = "Stop"
$ScriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$ProjectRoot = Split-Path -Parent $ScriptDir
Import-Module "$ScriptDir\utility-scripts.psm1" -Force
Set-ModuleTraceLevel -TraceLevel $TraceLevel
$PSDefaultParameterValues = @{ 'Write-Log:TraceLevel' = $TraceLevel }

$GameServiceUrl = "http://localhost:8080"

if ($Help) {
    Write-Host @"

Performance Benchmark
=====================

Replays recordings with server-side per-action timing.
Reports cost by action type and game state.

Usage: perf.ps1 [-Name <pattern>] [-TraceLevel <level>]

Options:
  -Name <pattern>   Filter recordings by name (wildcard)
  -TraceLevel       Output: ERROR, WARN, INFO (default), DEBUG
  -Help             Show this message

Prerequisites:
  GameService must be running: ./catan.ps1 run

"@
    exit 0
}

# ─── Main ────────────────────────────────────────────────────────────────────

Write-Log -Level INFO -Message "" -NoLabel
Write-Log -Level INFO -Message "Performance Benchmark" -NoLabel -ForegroundColor Cyan
Write-Log -Level INFO -Message "=====================" -NoLabel -ForegroundColor Cyan
Write-Log -Level INFO -Message "" -NoLabel

# 1. Check GameService
try {
    $health = Invoke-RestMethod -Uri "$GameServiceUrl/health" -TimeoutSec 5
    Write-Log -Level INFO -Message "GameService: running" -NoLabel -ForegroundColor Green
}
catch {
    Write-Log -Level ERROR -Message "GameService not running on $GameServiceUrl. Start with: ./catan.ps1 run" -NoLabel
    exit 1
}

# 2. Get recordings
$recordings = Invoke-RestMethod -Uri "$GameServiceUrl/api/recordings" -TimeoutSec 10
if ($Name) { $recordings = $recordings | Where-Object { $_.name -like $Name } }
$recordings = $recordings | Sort-Object actionCount -Descending

if ($recordings.Count -eq 0) {
    Write-Log -Level WARN -Message "No recordings found." -NoLabel
    exit 0
}

# 3. Replay each with perf timing
foreach ($rec in $recordings) {
    Write-Log -Level INFO -Message "Replaying: $($rec.name) ($($rec.actionCount) actions)..." -NoLabel

    $result = Invoke-RestMethod -Uri "$GameServiceUrl/api/recording/$($rec.id)/replay?perf=true" `
        -Method Post -TimeoutSec 300

    if (-not $result.success) {
        Write-Log -Level ERROR -Message "  FAILED at action $($result.failedAtAction): $($result.errorMessage)" -NoLabel
        continue
    }

    Write-Log -Level INFO -Message "  Replayed $($result.actionsReplayed) actions in $($result.totalElapsedMs)ms" -NoLabel -ForegroundColor Green
    Write-Log -Level INFO -Message "" -NoLabel

    if (-not $result.timings) {
        Write-Log -Level WARN -Message "  No timing data returned (server may not support ?perf=true yet)" -NoLabel
        continue
    }

    # Per-action table
    Write-Log -Level INFO -Message ("  {0,5} {1,-22} {2,-22} {3,10}" -f "#", "ActionType", "GameState", "Time(ms)") -NoLabel -ForegroundColor Gray
    Write-Log -Level INFO -Message ("  {0,5} {1,-22} {2,-22} {3,10}" -f "-----", "----------------------", "----------------------", "----------") -NoLabel -ForegroundColor Gray

    foreach ($t in $result.timings) {
        $color = if ($t.elapsedMs -gt 50) { "Red" } elseif ($t.elapsedMs -gt 10) { "Yellow" } else { "White" }
        Write-Log -Level INFO -Message ("  {0,5} {1,-22} {2,-22} {3,10}" -f $t.index, $t.actionType, $t.gameState, $t.elapsedMs) -NoLabel -ForegroundColor $color
    }

    Write-Log -Level INFO -Message "" -NoLabel

    # Aggregate by action type
    Write-Log -Level INFO -Message "  Summary by Action Type:" -NoLabel -ForegroundColor Cyan
    $byType = $result.timings | Group-Object -Property actionType
    Write-Log -Level INFO -Message ("  {0,-22} {1,6} {2,10} {3,10} {4,10}" -f "ActionType", "Count", "Avg(ms)", "Max(ms)", "Total(ms)") -NoLabel -ForegroundColor Gray
    Write-Log -Level INFO -Message ("  {0,-22} {1,6} {2,10} {3,10} {4,10}" -f "----------------------", "------", "----------", "----------", "----------") -NoLabel -ForegroundColor Gray

    foreach ($group in $byType | Sort-Object { ($_.Group | Measure-Object -Property elapsedMs -Sum).Sum } -Descending) {
        $avg = [math]::Round(($group.Group | Measure-Object -Property elapsedMs -Average).Average, 2)
        $max = [math]::Round(($group.Group | Measure-Object -Property elapsedMs -Maximum).Maximum, 2)
        $total = [math]::Round(($group.Group | Measure-Object -Property elapsedMs -Sum).Sum, 2)
        Write-Log -Level INFO -Message ("  {0,-22} {1,6} {2,10} {3,10} {4,10}" -f $group.Name, $group.Count, $avg, $max, $total) -NoLabel
    }

    Write-Log -Level INFO -Message "" -NoLabel

    # Aggregate by game state
    Write-Log -Level INFO -Message "  Summary by Game State:" -NoLabel -ForegroundColor Cyan
    $byState = $result.timings | Group-Object -Property gameState
    Write-Log -Level INFO -Message ("  {0,-22} {1,6} {2,10} {3,10} {4,10}" -f "GameState", "Count", "Avg(ms)", "Max(ms)", "Total(ms)") -NoLabel -ForegroundColor Gray
    Write-Log -Level INFO -Message ("  {0,-22} {1,6} {2,10} {3,10} {4,10}" -f "----------------------", "------", "----------", "----------", "----------") -NoLabel -ForegroundColor Gray

    foreach ($group in $byState | Sort-Object { ($_.Group | Measure-Object -Property elapsedMs -Sum).Sum } -Descending) {
        $avg = [math]::Round(($group.Group | Measure-Object -Property elapsedMs -Average).Average, 2)
        $max = [math]::Round(($group.Group | Measure-Object -Property elapsedMs -Maximum).Maximum, 2)
        $total = [math]::Round(($group.Group | Measure-Object -Property elapsedMs -Sum).Sum, 2)
        Write-Log -Level INFO -Message ("  {0,-22} {1,6} {2,10} {3,10} {4,10}" -f $group.Name, $group.Count, $avg, $max, $total) -NoLabel
    }

    Write-Log -Level INFO -Message "" -NoLabel
}
