# Save/Load Games Architecture

## Overview

This document describes how games are saved and loaded in the Catan WebUI and GameService. The design
supports:

- Automatic saving on every state transition
- Querying active games by user
- Loading saved games into memory for continued play
- Future user authentication without schema changes

## Architecture

```text
┌─────────────┐     ┌─────────────────┐     ┌──────────────────────────────────┐
│   WebUI     │────▶│   GameService   │────▶│           Database               │
│  (Blazor)   │     │   (ASP.NET)     │     │                                  │
└─────────────┘     └─────────────────┘     │  ┌────────────────────────────┐  │
                            │               │  │    GameSaveMetadata        │  │
                            │               │  │  (queryable, lightweight)  │  │
                    ┌───────┴───────┐       │  └─────────────┬──────────────┘  │
                    │               │       │                │ FK              │
              SignalR Hub    REST API       │  ┌─────────────▼──────────────┐  │
                    │               │       │  │      GameSaveData          │  │
                    └───────┬───────┘       │  │   (compressed blob)        │  │
                            │               │  └────────────────────────────┘  │
                    GameStateMachine        └──────────────────────────────────┘
                            │
                    In-Memory Registry
```

## Database Schema

### GameSaveMetadata Table

Lightweight table for querying and displaying saved games without loading the full game data.

| Column | Type | Description |
|--------|------|-------------|
| Id | INTEGER | Primary key (auto-increment) |
| GameId | TEXT | Unique game identifier (indexed) |
| StartedBy | TEXT | User who created the game (indexed) |
| SavedAt | DATETIME | Last save timestamp (indexed) |
| CreatedAt | DATETIME | Game creation timestamp |
| GameState | TEXT | Current game state for filtering |
| GameType | TEXT | "Regular" or "Expansion" |
| PlayerCount | INTEGER | Number of players |
| PlayerNames | TEXT | Comma-separated player names |
| TurnCount | INTEGER | Number of state transitions |
| GameName | TEXT | Display name |
| GameDataId | INTEGER | Foreign key to GameSaveData |

**Indexes:**

- `IX_GameSaveMetadata_StartedBy` - Filter by user
- `IX_GameSaveMetadata_GameState` - Filter active games
- `IX_GameSaveMetadata_SavedAt` - Sort by recent

### GameSaveData Table

Heavy table storing the actual compressed game log.

| Column | Type | Description |
|--------|------|-------------|
| Id | INTEGER | Primary key (auto-increment) |
| CompressedData | BLOB | Compressed SerializableLog JSON |
| Size | INTEGER | Size in bytes (for display) |

### Relationship

```text
GameSaveMetadata.GameDataId ──FK──▶ GameSaveData.Id
```

One metadata record per game. The compressed data is stored separately to keep metadata queries fast.

## Save Flow

Saves occur automatically on every state transition (game action).

```text
1. Client sends action via REST API
   POST /api/game/action { gameId, playerId, messageType, ... }

2. GameApiController receives request
   └─▶ GameStateMachine.Handle*Async()
       └─▶ Updates game state
       └─▶ LogGameModel(gameModel)

3. ProcessGameActionResult() called
   └─▶ SaveGameToDatabase()
       │
       ├─▶ Get SerializableLog from GameStateMachine
       │   (contains DoneStack + RedoStack as JSON strings)
       │
       ├─▶ Compress JSON to bytes
       │
       ├─▶ Build GameMetadata:
       │   - PlayerNames = gameModel.Players.Select(p => p.Name).Join(", ")
       │   - TurnCount = serializableLog.DoneCount
       │   - GameState = gameModel.GameState.ToString()
       │   - Size = compressedData.Length
       │
       └─▶ IGamePersistence.SaveAsync(gameId, compressedData, metadata)
           │
           ├─▶ Upsert GameSaveData (insert or update CompressedData)
           └─▶ Upsert GameSaveMetadata (update all fields, set FK)

4. Broadcast GameModel to clients via SignalR
```

### What Gets Saved

The `SerializableLog` contains:

- `DoneStack`: List of GameModel JSON strings (full history)
- `RedoStack`: List of undone GameModel JSON strings
- `DoneCount`: Number of items in DoneStack
- `RedoCount`: Number of items in RedoStack
- `GameType`: Regular or Expansion

This preserves full undo/redo capability when the game is loaded.

## Load Flow

Loading happens when a user selects a saved game from the list.

```text
1. WebUI displays saved games table
   GET /api/games?playerId=WebUI
   └─▶ Returns metadata only (no CompressedData)

2. User clicks a game row
   └─▶ WebUI calls POST /api/game/{gameId}/load

3. GameService loads the game:
   a. Query GameSaveMetadata by GameId
   b. Follow FK to get GameSaveData.CompressedData
   c. Decompress bytes to JSON string
   d. Deserialize to SerializableLog
   e. Create Log<string> from SerializableLog
   f. Create GameStateMachine with the Log
   g. Add to GameStateMachineRegistry[gameId]

4. Return { success: true, gameId }

5. WebUI navigates to /game/{gameId}

6. Game.razor joins SignalR group for gameId
   └─▶ Calls GameHub.JoinGame(gameId)

7. GameService broadcasts current GameModel via SignalR
   └─▶ GameHub sends "GameStateUpdated" to ALL clients in the game group
   └─▶ This includes the WebUI that just joined and any other connected clients
   └─▶ Clients receive the most recent GameModel from GameStateMachine.GetCurrentState()
```

### SignalR Notification on Load

When a game is loaded and a client joins the SignalR group, the GameService **immediately sends the
current GameModel** to all clients in that game's group:

```csharp
// In GameHub.JoinGame()
await Groups.AddToGroupAsync(Context.ConnectionId, gameId);
var gameStateMachine = GameStateMachineRegistry.GetGameStateMachine(gameId);
var currentGameModel = gameStateMachine.GetCurrentState();
await Clients.Group(gameId).SendAsync("GameStateUpdated", currentGameModel);
```

This ensures:

- The joining client receives the game state immediately
- Any other clients already in the group get a refresh
- All clients are synchronized to the same state

## API Endpoints

### GET /api/games

List saved games for a user.

**Query Parameters:**

| Parameter | Required | Description |
|-----------|----------|-------------|
| playerId | No | Filter by StartedBy (default: "*" for all) |

**Response:**

```json
{
  "success": true,
  "games": [
    {
      "gameId": "abc-123",
      "gameName": "Game 14:30",
      "gameState": "WaitingForRoll",
      "gameType": "Regular",
      "playerCount": 4,
      "playerNames": "Joe, Alice, Bob, Charlie",
      "turnCount": 47,
      "size": 12345,
      "savedAt": "2025-12-03T10:00:00Z",
      "createdAt": "2025-12-03T09:00:00Z"
    }
  ],
  "count": 1
}
```

**Notes:**

- `startedBy` and `gameDataId` are NOT returned (internal use only)
- Filter `GameState != 'GameOver'` applied server-side for active games

### POST /api/game/{gameId}/load

Load a saved game into memory.

**Response:**

```json
{
  "success": true,
  "gameId": "abc-123",
  "message": "Game loaded successfully"
}
```

If already loaded, returns success with "Game already loaded" message.

## StartedBy Convention

| Context | StartedBy Value |
|---------|-----------------|
| WebUI (current) | `"WebUI"` |
| Future: Authenticated user | User ID from auth token |
| Desktop app | `"Desktop"` or user ID |

The schema supports future user authentication without changes.

## WebUI Integration

### NewGame Page Table

Below the "Start Game" button, display a table of active saved games:

| Date | Size | Type | Players | Turns |
|------|------|------|---------|-------|
| 12/3 10:00 | 12.3 KB | Regular | Joe, Alice, Bob | 47 |
| 12/2 15:30 | 8.7 KB | Expansion | Joe, Charlie | 23 |

Clicking a row:

1. Calls `POST /api/game/{gameId}/load`
2. Navigates to `/game/{gameId}`

### Query Pattern

```csharp
// Get active games for WebUI
var response = await Http.GetFromJsonAsync<SavedGamesResponse>(
    $"{Config.BaseUrl}/api/games?playerId=WebUI");

// Filter to non-GameOver (done server-side, but can double-check client-side)
var activeGames = response.Games
    .Where(g => g.GameState != "GameOver")
    .OrderByDescending(g => g.SavedAt);
```

## Migration Notes

### Development

Delete and recreate the database:

```powershell
pwsh ./webui.ps1 clean
pwsh ./webui.ps1 run
```

### Production (Future)

EF Core migrations would handle schema evolution:

```bash
dotnet ef migrations add AddNormalizedGameSaves
dotnet ef database update
```

## Game Import/Export API

### POST /api/game/import

Imports a `.catan` file into the database. Used for:
- Seeding default games on startup (via `webui.ps1`)
- Future: User uploads from WebUI
- Bidirectional Desktop ↔ WebUI game transfer

**Request:** `multipart/form-data` with a `.catan` file

**Response:**

```json
{
  "success": true,
  "gameId": "abc-123",
  "gameName": "Game 14:30",
  "playerNames": "Joe, Alice, Bob",
  "turnCount": 47,
  "message": "Game imported successfully"
}
```

**Notes:**
- Uses the `GameId` from the file (preserves Desktop/WebUI compatibility)
- Returns "Game already exists" if game is already in database
- Parses `.catan` file: Brotli-compressed `SerializableLog` JSON

### GET /api/database/health

Returns database status for script decisions.

**Response:**

```json
{
  "healthy": true,
  "playerCount": 7,
  "gameCount": 3,
  "needsSeeding": false,
  "needsGames": false,
  "timestamp": "2025-12-03T10:00:00Z"
}
```

Used by `webui.ps1`:
1. Start GameService
2. Call `/api/database/health`
3. If `needsGames == true`, POST each `.catan` file from `Default Data/Games/`

### GET /api/game/{gameId}/export (Future)

Downloads a game as a `.catan` file for Desktop App import.

## Default Data Seeding

### Player ID Format

Player IDs use PascalCase to match the Desktop App:
- `Joe-001`, `Dodgy-001`, `Doug-001`, `Ryan-001`, `Adrian-001`, `Chris-001`, `Guest-001`

### Default Data Location

```
Catan3.GameService/Default Data/
├── Players/           # Player images (seeded on startup)
│   ├── joe.jpg
│   ├── Dodgy.png
│   └── ...
└── Games/             # .catan files (imported via API)
    └── Game1.catan
```

### Seeding Flow

1. **Startup**: `DatabaseSeeder.SeedAsync()` seeds players/images if empty
2. **After service starts**: `webui.ps1` calls health endpoint
3. **If games needed**: Script POSTs each `.catan` file to import endpoint

## File Locations

| File | Purpose |
|------|---------|
| `Catan3.GameService/Data/CatanDbContext.cs` | Entity definitions, DbContext |
| `Catan3.GameService/Data/DatabaseSeeder.cs` | Player/image seeding |
| `Catan3.GameService/Services/IGamePersistence.cs` | Interface + GameMetadata class |
| `Catan3.GameService/Services/DatabasePersistenceService.cs` | Save/Load implementation |
| `Catan3.GameService/Controllers/GameApiController.cs` | API endpoints (including import) |
| `WebUI/Pages/NewGame.razor` | Saved games table UI |
| `webui.ps1` | Script with database doctor and game import |
