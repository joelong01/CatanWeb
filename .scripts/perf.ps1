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

# Reusable HttpClient to avoid TCP port exhaustion on rapid requests
$script:httpClient = [System.Net.Http.HttpClient]::new()
$script:httpClient.Timeout = [TimeSpan]::FromSeconds(30)

function Invoke-GameAction {
    param([string]$GameId, [string]$MessageType, [string]$PlayerId = "perf-test")

    $body = @{
        gameId = $GameId
        playerId = $PlayerId
        messageType = $MessageType
    } | ConvertTo-Json
    $content = [System.Net.Http.StringContent]::new($body, [System.Text.Encoding]::UTF8, "application/json")
    $response = $script:httpClient.PostAsync("$GameServiceUrl/api/game/action", $content).GetAwaiter().GetResult()
    if (-not $response.IsSuccessStatusCode) {
        throw "HTTP $($response.StatusCode): $($response.Content.ReadAsStringAsync().GetAwaiter().GetResult())"
    }
    return $response.Content.ReadAsStringAsync().GetAwaiter().GetResult() | ConvertFrom-Json
}

function Get-GameState {
    param([string]$GameId)
    $response = $script:httpClient.GetStringAsync("$GameServiceUrl/api/gamestate/$GameId").GetAwaiter().GetResult()
    return $response | ConvertFrom-Json
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

# 4. Find a WaitingForNext state by undoing until we hit one
Write-Log -Level INFO -Message "Searching for WaitingForNext state (undoing from end)..." -NoLabel
$undoCount = 0
$foundState = $null

while ($true) {
    $state = Get-GameState -GameId $TestGameId
    if ($state.gameState -eq "WaitingForNext") {
        $foundState = $state
        Write-Log -Level INFO -Message "Found WaitingForNext after $undoCount undos" -NoLabel -ForegroundColor Green
        break
    }
    try {
        Invoke-GameAction -GameId $TestGameId -MessageType "UndoMessage" | Out-Null
        $undoCount++
        if ($undoCount % 100 -eq 0) {
            Write-Log -Level INFO -Message "  $undoCount undos... (state: $($state.gameState))" -NoLabel
        }
    }
    catch {
        Write-Log -Level ERROR -Message "Reached beginning without finding WaitingForNext" -NoLabel
        exit 1
    }
}

# 5. Now undo one more to get to a state BEFORE the build, so we can redo it
Write-Log -Level INFO -Message "" -NoLabel
Write-Log -Level INFO -Message "Game state: $($foundState.gameState)" -NoLabel
Write-Log -Level INFO -Message "Current player: $($foundState.currentPlayerId)" -NoLabel
Write-Log -Level INFO -Message "Log depth at this point: ~$(15028 - $undoCount) entries" -NoLabel
Write-Log -Level INFO -Message "" -NoLabel

# 6. Measure the cost of a REDO (which triggers FromGameModel → serialize → compress)
#    This is equivalent to making a forward action at this log depth
Write-Log -Level INFO -Message "Benchmarking forward action cost..." -NoLabel -ForegroundColor Cyan
Write-Log -Level INFO -Message "" -NoLabel

# Undo once more, then redo to measure the forward action
Invoke-GameAction -GameId $TestGameId -MessageType "UndoMessage" | Out-Null

$iterations = 10
$forwardTimes = @()
for ($i = 0; $i -lt $iterations; $i++) {
    # Redo = forward action (triggers Done() → FromGameModel → serialize → compress)
    $sw = [System.Diagnostics.Stopwatch]::StartNew()
    Invoke-GameAction -GameId $TestGameId -MessageType "RedoMessage" | Out-Null
    $sw.Stop()
    $forwardTimes += $sw.ElapsedMilliseconds

    # Undo to reset for next measurement
    Invoke-GameAction -GameId $TestGameId -MessageType "UndoMessage" | Out-Null

    Write-Log -Level DEBUG -Message "  Iteration $($i + 1): $($sw.ElapsedMilliseconds)ms" -NoLabel
}

# 7. Report
Write-Log -Level INFO -Message "Results — Forward Action at Log Depth ~$(15028 - $undoCount)" -NoLabel -ForegroundColor Cyan
Write-Log -Level INFO -Message ("=" * 55) -NoLabel -ForegroundColor Cyan
Write-Log -Level INFO -Message "" -NoLabel

$avg = [math]::Round(($forwardTimes | Measure-Object -Average).Average, 1)
$max = ($forwardTimes | Measure-Object -Maximum).Maximum
$min = ($forwardTimes | Measure-Object -Minimum).Minimum
$p90 = ($forwardTimes | Sort-Object)[[math]::Floor($forwardTimes.Count * 0.9)]

Write-Log -Level INFO -Message ("  {0,-12} {1,8} {2,8} {3,8} {4,8}" -f "Metric", "Avg(ms)", "P90(ms)", "Max(ms)", "Min(ms)") -NoLabel -ForegroundColor Gray
Write-Log -Level INFO -Message ("  {0,-12} {1,8} {2,8} {3,8} {4,8}" -f "--------", "-------", "-------", "-------", "-------") -NoLabel -ForegroundColor Gray
Write-Log -Level INFO -Message ("  {0,-12} {1,8} {2,8} {3,8} {4,8}" -f "Forward", $avg, $p90, $max, $min) -NoLabel
Write-Log -Level INFO -Message "" -NoLabel
Write-Log -Level INFO -Message "  All timings (ms): $($forwardTimes -join ', ')" -NoLabel
Write-Log -Level INFO -Message "" -NoLabel

# Compare: measure same action at a SHALLOW log depth (undo to near beginning)
Write-Log -Level INFO -Message "Measuring same action type at shallow log depth..." -NoLabel
# Undo most of the way back
$targetDepth = 10
$remaining = 15028 - $undoCount - $targetDepth
for ($i = 0; $i -lt $remaining; $i++) {
    try { Invoke-GameAction -GameId $TestGameId -MessageType "UndoMessage" | Out-Null }
    catch { break }
    if ($i % 1000 -eq 0 -and $i -gt 0) {
        Write-Log -Level INFO -Message "  Undone $i more..." -NoLabel
    }
}

# Find WaitingForNext at shallow depth
$shallowFound = $false
for ($i = 0; $i -lt 50; $i++) {
    try { Invoke-GameAction -GameId $TestGameId -MessageType "RedoMessage" | Out-Null }
    catch { break }
    $s = Get-GameState -GameId $TestGameId
    if ($s.gameState -eq "WaitingForNext") { $shallowFound = $true; break }
}

if ($shallowFound) {
    Invoke-GameAction -GameId $TestGameId -MessageType "UndoMessage" | Out-Null
    $shallowTimes = @()
    for ($i = 0; $i -lt $iterations; $i++) {
        $sw = [System.Diagnostics.Stopwatch]::StartNew()
        Invoke-GameAction -GameId $TestGameId -MessageType "RedoMessage" | Out-Null
        $sw.Stop()
        $shallowTimes += $sw.ElapsedMilliseconds
        Invoke-GameAction -GameId $TestGameId -MessageType "UndoMessage" | Out-Null
    }

    $shallowAvg = [math]::Round(($shallowTimes | Measure-Object -Average).Average, 1)
    Write-Log -Level INFO -Message "" -NoLabel
    Write-Log -Level INFO -Message "Results — Forward Action at Shallow Log (~$targetDepth entries)" -NoLabel -ForegroundColor Cyan
    Write-Log -Level INFO -Message ("  {0,-12} {1,8}" -f "Avg(ms)", $shallowAvg) -NoLabel
    Write-Log -Level INFO -Message "  All timings (ms): $($shallowTimes -join ', ')" -NoLabel
    Write-Log -Level INFO -Message "" -NoLabel
    Write-Log -Level INFO -Message "Slowdown factor: $([math]::Round($avg / [math]::Max($shallowAvg, 0.1), 1))x" -NoLabel -ForegroundColor Yellow
} else {
    Write-Log -Level WARN -Message "Could not find WaitingForNext at shallow depth" -NoLabel
}

Write-Log -Level INFO -Message "" -NoLabel
