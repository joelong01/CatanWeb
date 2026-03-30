<#
.SYNOPSIS
    Performance benchmark for long game log operations.
.DESCRIPTION
    Loads a long saved game and measures the time for undo/redo operations
    across the full game log. This exposes the compression/decompression
    cost that grows with game length.

    The test game: "Catan Thu 7PM" (81c12aa9) — a completed Regular game
    with 43,919 random iterations, 54 buildings, 72 roads.

    Prerequisites: GameService must be running (./catan.ps1 run or just the API).
.EXAMPLE
    ./.scripts/perf.ps1
.EXAMPLE
    ./catan.ps1 test -Perf
#>

param(
    [ValidateSet("ERROR", "WARN", "INFO", "DEBUG")]
    [Alias("LogLevel")]
    [string]$TraceLevel = "INFO",

    [int]$UndoRedoCycles = 1,
    [switch]$Help
)

$ErrorActionPreference = "Stop"
$ScriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$ProjectRoot = Split-Path -Parent $ScriptDir
Import-Module "$ScriptDir\utility-scripts.psm1" -Force
Set-ModuleTraceLevel -TraceLevel $TraceLevel
$PSDefaultParameterValues = @{ 'Write-Log:TraceLevel' = $TraceLevel }

$GameServiceUrl = "http://localhost:8080"
$TestGameId = "81c12aa9-6d0b-494a-bbeb-66d862918479"
$TestGameFile = Join-Path $ProjectRoot "Catan3.GameService/Default Data/Games/$TestGameId.json"

if ($Help) {
    Write-Host @"

Performance Benchmark
=====================

Usage: perf.ps1 [options]

Measures undo/redo performance on a long saved game to expose
compression/decompression bottlenecks in the game log.

Options:
  -UndoRedoCycles <n>   Number of full undo/redo cycles (default: 1)
  -TraceLevel           Output: ERROR, WARN, INFO (default), DEBUG
  -Help                 Show this message

Prerequisites:
  GameService must be running: ./catan.ps1 run

Test game: Catan Thu 7PM (81c12aa9)
  - Regular game, 3 players, GameOver
  - 43,919 random iterations
  - 54 buildings, 72 roads

"@
    exit 0
}

# ─── Helpers ─────────────────────────────────────────────────────────────────

function Test-GameService {
    try {
        $health = Invoke-RestMethod -Uri "$GameServiceUrl/health" -TimeoutSec 5
        return ($health.status -eq "healthy")
    }
    catch { return $false }
}

function Invoke-GameAction {
    param([string]$GameId, [string]$MessageType, [string]$PlayerId = "perf-test")

    $body = @{
        gameId = $GameId
        playerId = $PlayerId
        messageType = $MessageType
    } | ConvertTo-Json
    $response = Invoke-RestMethod -Uri "$GameServiceUrl/api/game/action" `
        -Method Post -Body $body -ContentType "application/json" -TimeoutSec 30
    return $response
}

function Get-GameState {
    param([string]$GameId)
    return Invoke-RestMethod -Uri "$GameServiceUrl/api/gamestate/$GameId" -TimeoutSec 30
}

# ─── Main ────────────────────────────────────────────────────────────────────

Write-Log -Level INFO -Message "" -NoLabel
Write-Log -Level INFO -Message "Performance Benchmark" -NoLabel -ForegroundColor Cyan
Write-Log -Level INFO -Message "=====================" -NoLabel -ForegroundColor Cyan
Write-Log -Level INFO -Message "" -NoLabel

# 1. Check GameService is running
if (-not (Test-GameService)) {
    Write-Log -Level ERROR -Message "GameService not running on $GameServiceUrl" -NoLabel
    Write-Log -Level INFO -Message "Start it with: ./catan.ps1 run" -NoLabel
    exit 1
}
Write-Log -Level INFO -Message "GameService: running" -NoLabel -ForegroundColor Green

# 2. Load the test game
if (-not (Test-Path $TestGameFile)) {
    Write-Log -Level ERROR -Message "Test game file not found: $TestGameFile" -NoLabel
    exit 1
}

Write-Log -Level INFO -Message "Loading test game: $TestGameId" -NoLabel
$saveData = Get-Content $TestGameFile -Raw | ConvertFrom-Json

$loadBody = @{ CompressedLog = $saveData.compressedData } | ConvertTo-Json
try {
    $loadResult = Invoke-RestMethod -Uri "$GameServiceUrl/api/game/load" `
        -Method Post -Body $loadBody -ContentType "application/json" -TimeoutSec 60
    Write-Log -Level INFO -Message "Game loaded: $($loadResult.gameId)" -NoLabel -ForegroundColor Green
}
catch {
    Write-Log -Level ERROR -Message "Failed to load game: $_" -NoLabel
    exit 1
}

# 3. Get initial state
$state = Get-GameState -GameId $TestGameId
$gameName = $state.gameName
$turnCount = $state.random.iterations
Write-Log -Level INFO -Message "Game: $gameName" -NoLabel
Write-Log -Level INFO -Message "State: $($state.gameState)" -NoLabel
Write-Log -Level INFO -Message "Random iterations: $turnCount" -NoLabel
Write-Log -Level INFO -Message "" -NoLabel

# 4. Undo all the way back, measuring each action
Write-Log -Level INFO -Message "Undoing all actions..." -NoLabel
$undoCount = 0
$undoTimes = @()

$countSw = [System.Diagnostics.Stopwatch]::StartNew()
while ($true) {
    try {
        $sw = [System.Diagnostics.Stopwatch]::StartNew()
        Invoke-GameAction -GameId $TestGameId -MessageType "UndoMessage" | Out-Null
        $sw.Stop()
        $undoCount++
        $undoTimes += $sw.ElapsedMilliseconds
        if ($undoCount % 50 -eq 0) {
            Write-Log -Level INFO -Message "  $undoCount undos... (last: $($sw.ElapsedMilliseconds)ms)" -NoLabel
        }
    }
    catch {
        # Undo failed = we've reached the beginning
        break
    }
}
$countSw.Stop()

Write-Log -Level INFO -Message "Undo depth: $undoCount actions" -NoLabel
Write-Log -Level INFO -Message "Total undo time: $([math]::Round($countSw.Elapsed.TotalSeconds, 1))s" -NoLabel
Write-Log -Level INFO -Message "" -NoLabel

# 5. Redo all back to the end and measure
Write-Log -Level INFO -Message "Redoing all $undoCount actions..." -NoLabel
$redoTimes = @()
$redoSw = [System.Diagnostics.Stopwatch]::StartNew()
for ($i = 0; $i -lt $undoCount; $i++) {
    $sw = [System.Diagnostics.Stopwatch]::StartNew()
    Invoke-GameAction -GameId $TestGameId -MessageType "RedoMessage" | Out-Null
    $sw.Stop()
    $redoTimes += $sw.ElapsedMilliseconds
    if (($i + 1) % 50 -eq 0) {
        Write-Log -Level DEBUG -Message "  Redone $($i + 1)/$undoCount..." -NoLabel
    }
}
$redoSw.Stop()

Write-Log -Level INFO -Message "Total redo time: $([math]::Round($redoSw.Elapsed.TotalSeconds, 1))s" -NoLabel
Write-Log -Level INFO -Message "" -NoLabel

# 6. Report results
Write-Log -Level INFO -Message "Results" -NoLabel -ForegroundColor Cyan
Write-Log -Level INFO -Message "=======" -NoLabel -ForegroundColor Cyan
Write-Log -Level INFO -Message "" -NoLabel

$undoAvg = if ($undoTimes.Count -gt 0) { [math]::Round(($undoTimes | Measure-Object -Average).Average, 1) } else { 0 }
$undoMax = if ($undoTimes.Count -gt 0) { ($undoTimes | Measure-Object -Maximum).Maximum } else { 0 }
$undoMin = if ($undoTimes.Count -gt 0) { ($undoTimes | Measure-Object -Minimum).Minimum } else { 0 }
$undoP90 = if ($undoTimes.Count -gt 0) { $sorted = $undoTimes | Sort-Object; $sorted[[math]::Floor($sorted.Count * 0.9)] } else { 0 }

$redoAvg = if ($redoTimes.Count -gt 0) { [math]::Round(($redoTimes | Measure-Object -Average).Average, 1) } else { 0 }
$redoMax = if ($redoTimes.Count -gt 0) { ($redoTimes | Measure-Object -Maximum).Maximum } else { 0 }
$redoMin = if ($redoTimes.Count -gt 0) { ($redoTimes | Measure-Object -Minimum).Minimum } else { 0 }
$redoP90 = if ($redoTimes.Count -gt 0) { $sorted = $redoTimes | Sort-Object; $sorted[[math]::Floor($sorted.Count * 0.9)] } else { 0 }

Write-Log -Level INFO -Message ("  {0,-12} {1,8} {2,8} {3,8} {4,8} {5,8}" -f "Operation", "Count", "Avg(ms)", "P90(ms)", "Max(ms)", "Min(ms)") -NoLabel -ForegroundColor Gray
Write-Log -Level INFO -Message ("  {0,-12} {1,8} {2,8} {3,8} {4,8} {5,8}" -f "--------", "-----", "-------", "-------", "-------", "-------") -NoLabel -ForegroundColor Gray
Write-Log -Level INFO -Message ("  {0,-12} {1,8} {2,8} {3,8} {4,8} {5,8}" -f "Undo", $undoCount, $undoAvg, $undoP90, $undoMax, $undoMin) -NoLabel
Write-Log -Level INFO -Message ("  {0,-12} {1,8} {2,8} {3,8} {4,8} {5,8}" -f "Redo", $undoCount, $redoAvg, $redoP90, $redoMax, $redoMin) -NoLabel
Write-Log -Level INFO -Message "" -NoLabel

# Show timing curve (first 10, last 10)
if ($undoTimes.Count -ge 20) {
    Write-Log -Level INFO -Message "Undo timing curve (ms):" -NoLabel -ForegroundColor Cyan
    $first10 = ($undoTimes[0..9] | ForEach-Object { [math]::Round($_) }) -join ", "
    $last10 = ($undoTimes[($undoTimes.Count - 10)..($undoTimes.Count - 1)] | ForEach-Object { [math]::Round($_) }) -join ", "
    Write-Log -Level INFO -Message "  First 10: $first10" -NoLabel
    Write-Log -Level INFO -Message "  Last 10:  $last10" -NoLabel
    Write-Log -Level INFO -Message "" -NoLabel
    Write-Log -Level INFO -Message "Redo timing curve (ms):" -NoLabel -ForegroundColor Cyan
    $first10 = ($redoTimes[0..9] | ForEach-Object { [math]::Round($_) }) -join ", "
    $last10 = ($redoTimes[($redoTimes.Count - 10)..($redoTimes.Count - 1)] | ForEach-Object { [math]::Round($_) }) -join ", "
    Write-Log -Level INFO -Message "  First 10: $first10" -NoLabel
    Write-Log -Level INFO -Message "  Last 10:  $last10" -NoLabel
}

Write-Log -Level INFO -Message "" -NoLabel

# Assessment
if ($undoAvg -gt 100) {
    Write-Log -Level INFO -Message "Assessment: SLOW — average undo > 100ms" -NoLabel -ForegroundColor Red
} elseif ($undoAvg -gt 50) {
    Write-Log -Level INFO -Message "Assessment: MODERATE — average undo 50-100ms" -NoLabel -ForegroundColor Yellow
} else {
    Write-Log -Level INFO -Message "Assessment: GOOD — average undo < 50ms" -NoLabel -ForegroundColor Green
}
