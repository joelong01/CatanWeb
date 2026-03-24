<#
.SYNOPSIS
    Export all data from Azure SQL into raw JSON files for migration to CosmosDB.
    Outputs to Catan3.GameService/Default Data/sql-export/
#>
param([string]$TraceLevel = "INFO")

$ErrorActionPreference = "Stop"
$repoRoot = Split-Path $PSScriptRoot
$outDir = Join-Path $repoRoot "Catan3.GameService/Default Data/sql-export"

if (Test-Path $outDir) { Remove-Item $outDir -Recurse -Force }
New-Item -ItemType Directory -Path $outDir -Force | Out-Null

# Get AAD token for Azure SQL
Write-Host "Getting AAD token for Azure SQL..." -ForegroundColor Cyan
$token = az account get-access-token --resource "https://database.windows.net/" --query accessToken --output tsv
if ($LASTEXITCODE -ne 0) { throw "Failed to get AAD token" }

$connStr = "Server=tcp:sql-catan.database.windows.net,1433;Initial Catalog=catan;Encrypt=true;TrustServerCertificate=False;Connection Timeout=30;"

Import-Module SqlServer -ErrorAction Stop

function Run-Query {
    param([string]$Query, [string]$Label)
    Write-Host "  Exporting $Label..." -ForegroundColor Gray
    $result = Invoke-Sqlcmd -ConnectionString $connStr -AccessToken $token -Query $Query -OutputAs DataRows -MaxBinaryLength 10485760 -MaxCharLength 10485760
    Write-Host "    $($result.Count) rows" -ForegroundColor Green
    return $result
}

# --- Players ---
Write-Host "`nPlayers:" -ForegroundColor Cyan
$players = Run-Query -Label "players" -Query "SELECT Id, Data FROM Players"
$playersOut = @()
foreach ($row in $players) {
    $playersOut += @{ id = $row.Id; data = $row.Data }
}
$playersOut | ConvertTo-Json -Depth 10 | Set-Content (Join-Path $outDir "players.json") -Encoding UTF8

# --- Images ---
Write-Host "`nImages:" -ForegroundColor Cyan
$images = Run-Query -Label "images" -Query "SELECT Id, ContentType, Data FROM Images"
$imagesOut = @()
foreach ($row in $images) {
    $base64 = if ($row.Data -is [byte[]]) { [Convert]::ToBase64String($row.Data) } else { $null }
    $imagesOut += @{ id = $row.Id; contentType = $row.ContentType; dataBase64 = $base64 }
}
$imagesOut | ConvertTo-Json -Depth 10 | Set-Content (Join-Path $outDir "images.json") -Encoding UTF8

# --- Game Saves (metadata + data joined) ---
Write-Host "`nGame Saves:" -ForegroundColor Cyan
$games = Run-Query -Label "game saves" -Query @"
SELECT m.Id AS MetaId, m.GameId, m.GameName, m.GameState, m.GameType,
       m.StartedBy, m.PlayerCount, m.PlayerNames, m.TurnCount,
       m.SavedAt, m.CreatedAt, m.GameDataId,
       d.CompressedData, d.Size
FROM GameSaveMetadata m
LEFT JOIN GameSaveData d ON m.GameDataId = d.Id
"@
$gamesOut = @()
foreach ($row in $games) {
    $blob = if ($row.CompressedData -is [byte[]]) { [Convert]::ToBase64String($row.CompressedData) } else { $null }
    $gamesOut += @{
        metaId      = $row.MetaId
        gameId      = $row.GameId
        gameName    = $row.GameName
        gameState   = $row.GameState
        gameType    = $row.GameType
        startedBy   = $row.StartedBy
        playerCount = $row.PlayerCount
        playerNames = $row.PlayerNames
        turnCount   = $row.TurnCount
        savedAt     = $row.SavedAt.ToString("o")
        createdAt   = $row.CreatedAt.ToString("o")
        size        = $row.Size
        compressedDataBase64 = $blob
    }
}
$gamesOut | ConvertTo-Json -Depth 10 | Set-Content (Join-Path $outDir "games.json") -Encoding UTF8

# --- Completed Games ---
Write-Host "`nCompleted Games:" -ForegroundColor Cyan
$completed = Run-Query -Label "completed games" -Query @"
SELECT Id, GameId, GameName, WinnerId, WinnerName, PlayerCount, PlayerNames,
       TurnCount, CompletedAt, StartedAt, CompressedData, Size
FROM CompletedGames
"@
$completedOut = @()
foreach ($row in $completed) {
    $blob = if ($row.CompressedData -is [byte[]]) { [Convert]::ToBase64String($row.CompressedData) } else { $null }
    $completedOut += @{
        sqlId          = $row.Id
        gameId         = $row.GameId
        gameName       = $row.GameName
        winnerId       = $row.WinnerId
        winnerName     = $row.WinnerName
        playerCount    = $row.PlayerCount
        playerNames    = $row.PlayerNames
        turnCount      = $row.TurnCount
        completedAt    = $row.CompletedAt.ToString("o")
        startedAt      = $row.StartedAt.ToString("o")
        size           = $row.Size
        compressedDataBase64 = $blob
    }
}
$completedOut | ConvertTo-Json -Depth 10 | Set-Content (Join-Path $outDir "completed-games.json") -Encoding UTF8

# --- Recordings ---
Write-Host "`nRecordings:" -ForegroundColor Cyan
$recordings = Run-Query -Label "recordings" -Query @"
SELECT Id, Name, GameType, PlayerCount, PlayerIds, ActionCount, CreatedAt, Data
FROM Recordings
"@
$recordingsOut = @()
foreach ($row in $recordings) {
    $recordingsOut += @{
        id          = $row.Id
        name        = $row.Name
        gameType    = $row.GameType
        playerCount = $row.PlayerCount
        playerIds   = $row.PlayerIds
        actionCount = $row.ActionCount
        createdAt   = $row.CreatedAt.ToString("o")
        data        = $row.Data
    }
}
$recordingsOut | ConvertTo-Json -Depth 10 | Set-Content (Join-Path $outDir "recordings.json") -Encoding UTF8

# --- Templates ---
Write-Host "`nTemplates:" -ForegroundColor Cyan
$templates = Run-Query -Label "templates" -Query @"
SELECT Id, Name, Category, IsSystemTemplate, Version, Data, CreatedAt, UpdatedAt
FROM GameTemplates
"@
$templatesOut = @()
foreach ($row in $templates) {
    $templatesOut += @{
        id               = $row.Id
        name             = $row.Name
        category         = $row.Category
        isSystemTemplate = [bool]$row.IsSystemTemplate
        version          = $row.Version
        data             = $row.Data
        createdAt        = $row.CreatedAt.ToString("o")
        updatedAt        = $row.UpdatedAt.ToString("o")
    }
}
$templatesOut | ConvertTo-Json -Depth 10 | Set-Content (Join-Path $outDir "templates.json") -Encoding UTF8

# --- Summary ---
Write-Host "`n=== Export Complete ===" -ForegroundColor Cyan
Write-Host "  Players:         $($playersOut.Count)"
Write-Host "  Images:          $($imagesOut.Count)"
Write-Host "  Game Saves:      $($gamesOut.Count)"
Write-Host "  Completed Games: $($completedOut.Count)"
Write-Host "  Recordings:      $($recordingsOut.Count)"
Write-Host "  Templates:       $($templatesOut.Count)"
Write-Host "  Output:          $outDir"
