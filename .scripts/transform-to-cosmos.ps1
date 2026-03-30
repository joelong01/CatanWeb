<#
.SYNOPSIS
    Transform raw SQL export JSON files into CosmosDB document format.
    Reads from Default Data/sql-export/, writes to Default Data/{Players,Recordings}/ etc.
#>
param([string]$TraceLevel = "INFO")

$ErrorActionPreference = "Stop"

$scriptPath = Split-Path -Parent $MyInvocation.MyCommand.Path
Import-Module "$scriptPath\utility-scripts.psm1" -Force

$PSDefaultParameterValues = @{ 'Write-Log:TraceLevel' = $TraceLevel }

$repoRoot   = Split-Path $PSScriptRoot
$exportDir  = Join-Path $repoRoot "Catan3.GameService/Default Data/sql-export"
$defaultDir = Join-Path $repoRoot "Catan3.GameService/Default Data"

if (-not (Test-Path $exportDir)) { throw "No sql-export directory. Run export-sql.ps1 first." }

function Read-Export { param([string]$Name) Get-Content (Join-Path $exportDir "$Name.json") -Raw | ConvertFrom-Json }
function Write-Doc {
    param([string]$Dir, [string]$FileName, [object]$Doc)
    $path = Join-Path $Dir $FileName
    $Doc | ConvertTo-Json -Depth 20 | Set-Content $path -Encoding UTF8
}

# ─── Players ────────────────────────────────────────────────────────────────
Write-Log -Level INFO -Message "Transforming Players..." -NoLabel -ForegroundColor Cyan
$players = Read-Export "players"
$images  = Read-Export "images"

# Index images by ID for fast lookup
$imageMap = @{}
foreach ($img in $images) { $imageMap[$img.id] = $img }

$playersDir = Join-Path $defaultDir "Players"
$playerCount = 0
foreach ($p in $players) {
    $profile = $p.data | ConvertFrom-Json
    $img = $imageMap[$p.id]

    $doc = [ordered]@{
        id               = $p.id
        name             = $profile.name
        colors           = [ordered]@{
            primary    = $profile.colors.primary
            secondary  = $profile.colors.secondary
            foreground = $profile.colors.foreground
        }
        imageUri         = $profile.imageUri
        imageData        = if ($img) { $img.dataBase64 } else { $null }
        imageContentType = if ($img) { $img.contentType } else { $null }
    }

    # Add lifetimeStats if present
    if ($profile.lifetimeStats) {
        $doc.lifetimeStats = $profile.lifetimeStats
    }

    Write-Doc -Dir $playersDir -FileName "$($p.id).json" -Doc $doc
    $playerCount++
    if ($TraceLevel -eq "DEBUG") { Write-Log -Level INFO -Message "  $($p.id): $($profile.name)" -NoLabel }
}
Write-Log -Level INFO -Message "  $playerCount players written to $playersDir" -NoLabel -ForegroundColor Green

# ─── Games ──────────────────────────────────────────────────────────────────
Write-Log -Level INFO -Message "`nTransforming Games..." -NoLabel -ForegroundColor Cyan
$games = Read-Export "games"

$gamesDir = Join-Path $defaultDir "Games"
if (-not (Test-Path $gamesDir)) { New-Item -ItemType Directory -Path $gamesDir -Force | Out-Null }

# Clear old .catan files (we're replacing with JSON)
Get-ChildItem $gamesDir -Filter "*.json" -ErrorAction SilentlyContinue | Remove-Item -Force

$gameCount = 0
foreach ($g in $games) {
    $doc = [ordered]@{
        id             = $g.gameId
        gameName       = $g.gameName
        gameState      = $g.gameState
        gameType       = $g.gameType
        startedBy      = $g.startedBy
        playerCount    = $g.playerCount
        playerNames    = $g.playerNames
        turnCount      = $g.turnCount
        savedAt        = $g.savedAt
        createdAt      = $g.createdAt
        size           = $g.size
        compressedData = $g.compressedDataBase64
    }

    $safeName = $g.gameId -replace '[^a-zA-Z0-9\-]', '_'
    Write-Doc -Dir $gamesDir -FileName "$safeName.json" -Doc $doc
    $gameCount++
    if ($TraceLevel -eq "DEBUG") { Write-Log -Level INFO -Message "  $($g.gameId): $($g.gameName)" -NoLabel }
}
Write-Log -Level INFO -Message "  $gameCount games written to $gamesDir" -NoLabel -ForegroundColor Green

# ─── Completed Games ────────────────────────────────────────────────────────
Write-Log -Level INFO -Message "`nTransforming Completed Games..." -NoLabel -ForegroundColor Cyan
$completed = Read-Export "completed-games"

$completedDir = Join-Path $defaultDir "CompletedGames"
if (-not (Test-Path $completedDir)) { New-Item -ItemType Directory -Path $completedDir -Force | Out-Null }

$completedCount = 0
foreach ($c in $completed) {
    $doc = [ordered]@{
        id             = $c.gameId
        gameId         = $c.gameId
        gameName       = $c.gameName
        winnerId       = $c.winnerId
        winnerName     = $c.winnerName
        playerCount    = $c.playerCount
        playerNames    = $c.playerNames
        turnCount      = $c.turnCount
        completedAt    = $c.completedAt
        startedAt      = $c.startedAt
        size           = $c.size
        compressedData = $c.compressedDataBase64
    }

    $safeName = $c.gameId -replace '[^a-zA-Z0-9\-]', '_'
    Write-Doc -Dir $completedDir -FileName "$safeName.json" -Doc $doc
    $completedCount++
    if ($TraceLevel -eq "DEBUG") { Write-Log -Level INFO -Message "  $($c.gameId): $($c.gameName)" -NoLabel }
}
Write-Log -Level INFO -Message "  $completedCount completed games written to $completedDir" -NoLabel -ForegroundColor Green

# ─── Recordings ─────────────────────────────────────────────────────────────
Write-Log -Level INFO -Message "`nTransforming Recordings..." -NoLabel -ForegroundColor Cyan
$recordings = Read-Export "recordings"

$recordingsDir = Join-Path $defaultDir "Recordings"
if (-not (Test-Path $recordingsDir)) { New-Item -ItemType Directory -Path $recordingsDir -Force | Out-Null }

$recordingCount = 0
foreach ($r in $recordings) {
    # Extract gameId from the Data JSON blob
    $gameId = ""
    try {
        $parsed = $r.data | ConvertFrom-Json
        $gameId = if ($parsed.gameId) { $parsed.gameId } elseif ($parsed.initialGameModel.gameId) { $parsed.initialGameModel.gameId } else { "" }
    } catch {}

    $doc = [ordered]@{
        id          = $r.id
        name        = $r.name
        gameType    = $r.gameType
        playerCount = $r.playerCount
        actionCount = $r.actionCount
        gameId      = $gameId
        createdAt   = $r.createdAt
        data        = $r.data
    }

    Write-Doc -Dir $recordingsDir -FileName "$($r.id).json" -Doc $doc
    $recordingCount++
    if ($TraceLevel -eq "DEBUG") { Write-Log -Level INFO -Message "  $($r.id): $($r.name)" -NoLabel }
}
Write-Log -Level INFO -Message "  $recordingCount recordings written to $recordingsDir" -NoLabel -ForegroundColor Green

# ─── Templates ──────────────────────────────────────────────────────────────
Write-Log -Level INFO -Message "`nTransforming Templates..." -NoLabel -ForegroundColor Cyan
$templates = Read-Export "templates"

$templatesDir = Join-Path $defaultDir "Templates"
if (-not (Test-Path $templatesDir)) { New-Item -ItemType Directory -Path $templatesDir -Force | Out-Null }

$templateCount = 0
foreach ($t in $templates) {
    # Parse the Data JSON to extract summary fields
    $minPlayers = 3
    $maxPlayers = 4
    $description = ""
    try {
        $parsed = $t.data | ConvertFrom-Json
        if ($parsed.minPlayers) { $minPlayers = $parsed.minPlayers }
        if ($parsed.maxPlayers) { $maxPlayers = $parsed.maxPlayers }
        if ($parsed.description) { $description = $parsed.description }
    } catch {}

    $doc = [ordered]@{
        id               = $t.id
        name             = $t.name
        category         = $t.category
        isSystemTemplate = $t.isSystemTemplate
        description      = $description
        minPlayers       = $minPlayers
        maxPlayers       = $maxPlayers
        createdAt        = $t.createdAt
        updatedAt        = $t.updatedAt
        dataJson         = $t.data
    }

    $safeName = $t.id -replace '[^a-zA-Z0-9\-]', '_'
    Write-Doc -Dir $templatesDir -FileName "$safeName.json" -Doc $doc
    $templateCount++
    if ($TraceLevel -eq "DEBUG") { Write-Log -Level INFO -Message "  $($t.id): $($t.name)" -NoLabel }
}
Write-Log -Level INFO -Message "  $templateCount templates written to $templatesDir" -NoLabel -ForegroundColor Green

# ─── Summary ────────────────────────────────────────────────────────────────
Write-Log -Level INFO -Message "`n=== Transform Complete ===" -NoLabel -ForegroundColor Cyan
Write-Log -Level INFO -Message "  Players:         $playerCount" -NoLabel
Write-Log -Level INFO -Message "  Games:           $gameCount" -NoLabel
Write-Log -Level INFO -Message "  Completed Games: $completedCount" -NoLabel
Write-Log -Level INFO -Message "  Recordings:      $recordingCount" -NoLabel
Write-Log -Level INFO -Message "  Templates:       $templateCount" -NoLabel
Write-Log -Level INFO -Message "" -NoLabel
Write-Log -Level WARN -Message "Inspect the JSON files, then run:"
Write-Log -Level WARN -Message "  pwsh .scripts/database.ps1 seed-data -Azure"
