# Game Service API Reference

**Last verified:** January 30, 2026

## Architecture

The game service uses two communication channels:

- **REST API** -- Commands from client to server (all gameplay actions)
- **SignalR Hub** -- Real-time broadcasts from server to all clients

The React client sends ALL gameplay commands via `POST /api/game/action` with a
typed message payload. The server processes asynchronously via
`AsyncCommandProcessor`, then broadcasts the updated `GameModel` to all
connected clients over SignalR.

```
Client                    Server                    All Clients
  |                         |                           |
  |-- POST /api/game/action -->                         |
  |                         |-- AsyncCommandProcessor   |
  |                         |   -> GameStateMachine     |
  |                         |                           |
  |                         |-- SignalR: GameStateUpdated -->
  |                         |        (GameModel)        |
```

The SignalR hub also exposes direct command methods (e.g., `Shuffle`,
`ExecutePurchase`) for the Blazor/Desktop clients, but the React client
does not use them for commands.

## REST API Endpoints

**Base route:** `/api`

### Primary Command Endpoint

| Method | Route | Handler | Purpose |
|--------|-------|---------|---------|
| POST | `/api/game/action` | `ExecuteGameAction` | Execute any gameplay command |

**Request body:**

```json
{
  "gameId": "string",
  "playerId": "string",
  "messageType": "string",
  "messageData": { ... }
}
```

Processing is fire-and-forget: the endpoint returns `202 Accepted` immediately.
`AsyncCommandProcessor` deserializes `messageType` and routes to the correct
`GameStateMachine.Handle*Async()` method. Results are broadcast via SignalR.

### Game Lifecycle

| Method | Route | Handler | Purpose |
|--------|-------|---------|---------|
| POST | `/api/game/new` | `NewGame` | Create a new game |
| GET | `/api/gamestate/{gameId}` | `GetGameState` | Get current GameModel |
| POST | `/api/game/load` | `LoadGame` | Load from compressed log |
| POST | `/api/game/loadmodel` | `LoadGameModel` | Load from GameModel JSON |
| POST | `/api/game/{gameId}/load` | `LoadGameFromDatabase` | Load saved game into memory |
| POST | `/api/game/end` | `EndGame` | End game, remove from registry |
| DELETE | `/api/game/{gameId}` | `DeleteGame` | Delete from memory and database |
| POST | `/api/game/{gameId}/copy` | `CopyGame` | Copy game (optional new name) |
| POST | `/api/game/import` | `ImportGame` | Import `.catan` file |
| PATCH | `/api/game/{gameId}/rename` | `RenameGame` | Rename game (query: `newName`) |

### Board Setup

| Method | Route | Handler | Purpose |
|--------|-------|---------|---------|
| POST | `/api/game/{gameId}/shuffle` | `Shuffle` | Shuffle board tiles |
| PUT | `/api/game/{gameId}/houserules` | `UpdateHouseRules` | Update house rules |

### Winner Declaration

| Method | Route | Handler | Purpose |
|--------|-------|---------|---------|
| POST | `/api/game/{gameId}/winner` | `DeclareWinner` | Declare winner with VP counts |

**Request body:**

```json
{
  "winnerId": "string",
  "victoryPoints": { "playerId": count, ... }
}
```

### Persistence

| Method | Route | Handler | Purpose |
|--------|-------|---------|---------|
| POST | `/api/game/persist` | `PersistGame` | Save, load, or export game |

### Game Discovery

| Method | Route | Handler | Purpose |
|--------|-------|---------|---------|
| GET | `/api/companion/games` | `GetAvailableGames` | Games currently in memory |
| GET | `/api/games` | `GetSavedGames` | Saved games from database |

Query parameter: `playerId` (default `"*"` for all)

### Player Management

| Method | Route | Handler | Purpose |
|--------|-------|---------|---------|
| GET | `/api/players` | `GetPlayers` | List all player profiles |
| POST | `/api/players` | `CreatePlayer` | Create player profile |
| PUT | `/api/players/{id}` | `UpdatePlayer` | Update player profile |
| DELETE | `/api/players/{id}` | `DeletePlayer` | Delete player profile |
| POST | `/api/players/{id}/image` | `UploadPlayerImage` | Upload avatar image |
| GET | `/api/images/{id}` | `GetImage` | Retrieve player avatar |

### Settings & Database

| Method | Route | Handler | Purpose |
|--------|-------|---------|---------|
| POST | `/api/settings/update` | `UpdateSettings` | Update service settings |
| GET | `/api/database/health` | `GetDatabaseHealth` | Check database health |
| POST | `/api/database/migrate` | `MigrateDatabase` | Apply EF Core migrations |
| POST | `/api/troubleshoot` | `Troubleshoot` | Troubleshoot Azure SQL |

### Recording Management

See [recording-and-stats.md](recording-and-stats.md) for full details.

**Controller:** `RecordingController` (`[Route("api")]`)

| Method | Route | Purpose |
|--------|-------|---------|
| GET | `/api/recordings` | List all recordings |
| GET | `/api/recording/{id}` | Get full recording data |
| POST | `/api/recording/start/{gameId}` | Start recording a game |
| POST | `/api/recording/stop/{gameId}` | Stop recording |
| GET | `/api/recording/status/{gameId}` | Check recording status |
| DELETE | `/api/recording/{id}` | Delete recording |
| POST | `/api/recording/{id}/replay` | Full replay with verification |
| POST | `/api/recording/{id}/replay/start` | Start step-by-step session |
| POST | `/api/replay/{sessionId}/step` | Execute next replay action |
| DELETE | `/api/replay/{sessionId}` | End replay session |
| GET | `/api/recording/{id}/actions` | Get action summary |
| PUT | `/api/recording/{id}/rename` | Rename recording |
| POST | `/api/recording/import` | Import recording |
| POST | `/api/recording/cancel/{gameId}` | Cancel active recording |

### Statistics

**Controller:** `StatsController` (`[Route("api/stats")]`)

| Method | Route | Purpose |
|--------|-------|---------|
| GET | `/api/stats` | Get all player statistics |
| GET | `/api/stats/export` | Export stats to JSON |
| POST | `/api/stats/import` | Import stats from JSON |
| DELETE | `/api/stats` | Reset all lifetime statistics |

## AsyncCommandProcessor Message Routing

`POST /api/game/action` routes the `messageType` string to handler methods:

| messageType | Handler | messageData Properties |
|-------------|---------|----------------------|
| `UndoMessage` | `ProcessUndoMessage` | (none) |
| `RedoMessage` | `ProcessRedoMessage` | (none) |
| `NextMessage` | `ProcessNextMessage` | (none) |
| `ShuffleMessage` | `ProcessShuffleMessage` | (none) |
| `BalanceBoardMessage` | `ProcessBalanceBoard` | (none) |
| `PurchaseMessage` | `ProcessPurchaseMessage` | `entitlement` |
| `RoadPurchaseMessage` | `ProcessRoadPurchase` | `roadKey: { tileKey, hexSide }` |
| `BuildingUpgradeMessage` | `ProcessBuildingUpgrade` | `buildingKey: { hexCoordinates, position }` |
| `MoveRobberMessage` | `ProcessMoveRobber` | `coordinates`, `targetPlayerId?` |
| `RollMessage` | `ProcessRoll` | `roll: { normalRoll, specialDice? }` |
| `SetPlayerOrderMessage` | `ProcessSetPlayerOrder` | `playerIds[]` |
| `GoFirstMessage` | `ProcessGoFirst` | `playerId` |
| `ParticipatingInSupplementalMessage` | `ProcessParticipatingInSupplemental` | `playerId`, `participating` |
| `SwapTileResourcesMessage` | `ProcessSwapTileResources` | `sourceTileCoordinates`, `destinationTileCoordinates`, `sourceCurrentResource`, `destinationCurrentResource` |
| `DeclareWinnerMessage` | `ProcessDeclareWinner` | `winnerId`, `victoryPoints?` |

Each handler deserializes the `messageData` JSON, calls the corresponding
`GameStateMachine.Handle*Async()` method, then broadcasts the resulting
`GameModel` via SignalR `GameStateUpdated`.

After broadcasting, the processor also saves the game to the database in
parallel.

## SignalR Hub

**Hub path:** `/gamehub`

### Client-to-Server Methods

These methods exist for Blazor/Desktop clients. The React client uses REST
instead.

| Method | Parameters | Purpose |
|--------|-----------|---------|
| `JoinGame` | `gameId`, `playerId` | Join game group, receive GameModel |
| `LeaveGame` | `gameId`, `playerId` | Leave game group |
| `Shuffle` | `gameId`, `playerId` | Shuffle board |
| `Undo` | `gameId`, `playerId` | Undo last action |
| `Redo` | `gameId`, `playerId` | Redo last action |
| `Next` | `gameId`, `playerId` | Advance turn |
| `BalanceBoard` | `gameId`, `playerId` | Balance board |
| `ExecutePurchase` | `gameId`, `playerId`, `PurchaseMessage` | Purchase entitlement |
| `ExecuteRoadPurchase` | `gameId`, `playerId`, `RoadPurchaseMessage` | Place road |
| `ExecuteBuildingUpgrade` | `gameId`, `playerId`, `BuildingUpgradeMessage` | Place/upgrade building |
| `ExecuteMoveRobber` | `gameId`, `playerId`, `MoveRobberMessage` | Move robber |
| `ExecuteRoll` | `gameId`, `playerId`, `RollMessage` | Submit dice roll |
| `ExecuteSetPlayerOrder` | `gameId`, `playerId`, `SetPlayerOrderMessage` | Set turn order |
| `ExecuteGoFirst` | `gameId`, `playerId`, `GoFirstMessage` | Choose first player |
| `ExecuteBalanceBoard` | `gameId`, `playerId`, `BalanceBoardMessage` | Balance board |
| `ExecuteParticipatingInSupplemental` | `gameId`, `playerId`, `bool` | Set supplemental participation |
| `ExecuteSwapTileResources` | `gameId`, `playerId`, `SwapTileResources` | Swap tile resources |
| `BroadcastToGame` | `gameId`, `messageType`, `data` | Generic broadcast |

### Server-to-Client Events

| Event | Payload | Purpose |
|-------|---------|---------|
| `GameStateUpdated` | `GameModel` | Full game state after every action |
| `PlayerPresenceChanged` | `playerId`, `bool` | Player connected/disconnected |
| `PlayersUpdated` | `List<PlayerProfile>` | Player profile changes |
| `CommandCompleted` | `commandId`, `success`, `message` | Async command result |
| `CommandFailed` | `commandId`, `errorInfo` | Async command error |

### React Client Connection

`GameServiceProxy.ts` manages the SignalR connection:

- Automatic reconnection with exponential backoff (0, 2s, 4s, 8s, 16s,
  max 30s)
- `forceReconnect()` for mobile wake recovery
- `needsReconnection()` check for stale connections
- Deferred game re-join after reconnect with state refresh
- Singleton pattern: one proxy per `playerId`

## Server Startup Pipeline

**File:** `Catan3.GameService/Program.cs`

1. Configure logging, JSON serializer via `JsonHelper`, CORS
   (`AllowLocalhost`), and SignalR (`GameHub`)
2. Kestrel listens on `0.0.0.0:8080` (HTTP only in development)
3. Database seeding (`DatabaseSeeder.SeedAsync`) runs once per
   startup; `--seed-database` flag exits after seeding
4. Static files served from `wwwroot` with caching for images/fonts

### Dependency Injection

| Registration | Type | Service |
|-------------|------|---------|
| Scoped | `IGamePersistence` | `GamePersistenceService` |
| Singleton | `IPersistenceService` | `NullPersistenceService` |
| Singleton | `SignalRNotificationService` | `IClientNotification` |
| Singleton | `AsyncCommandProcessor` | Command routing |
| Singleton | `GameStateMachineRegistry` | In-memory game instances |
| Singleton | `RecordingService` | Recording capture |

### In-Memory Game Registry

`GameStateMachineRegistry` holds all active game instances in
memory. Games are loaded from the database on first access and
remain until explicitly ended or the service restarts.

### Security Gap

**No authentication or authorization is implemented.** All endpoints
trust caller-supplied `playerId` values. Any client can impersonate
any player. This is acceptable for the current local/trusted-network
deployment model but must be addressed before public hosting.
