# Game Service API Surface

Source: design_docs/GameServiceAPIs.md

## Base Configuration

- Base URL: `http://localhost:8080`
- SignalR hub: `/gameHub`
- REST root: `/api`
- JSON serialization uses `JsonHelper.ConfigureOptions` (camelCase, enum-as-strings, ignore cycles).

## Session Lifecycle

| Endpoint | Method | Description |
|----------|--------|-------------|
| `/api/game/new` | POST | Creates new game using `NewGameMessage` payload. Returns `{ success, gameId }`. |
| `/api/game/{gameId}/load` | POST | Loads saved game from SQLite into `GameStateMachineRegistry`. Returns `{ success, message }`. |
| `/api/game/end` | POST | Removes game from registry and disposes log. |
| `/api/game/load` | POST | Instantiates game from compressed `.catan` blob supplied by client. |
| `/api/game/loadmodel` | POST | Loads from raw `GameModel` JSON, initializing log for tests. |

## Gameplay Commands

| Path | Method | Handler | Notes |
|------|--------|---------|-------|
| `/api/game/action` | POST | `AsyncCommandProcessor` | Accepts IDs and message payload JSON; legacy desktop path that logs commandId. |
| `/api/game/{gameId}/shuffle` | POST | `HandleShuffleAsync` | Validates acting player matches `CurrentPlayerId` before executing. |
| `/api/game/persist` | POST | `HandlePersistGameAsync` | Accepts `{ gameId, action, location }`; delegates to shared persistence (desktop parity). |
| `/api/gamestate/{gameId}` | GET | `GetCurrentState` | Returns the latest `GameModel`. |

All command endpoints conclude with `ProcessGameActionResult` to persist and broadcast updates. Exceptions propagate as 400/500 with
`CommandResponse` fields.

## Catalog Endpoints

| Path | Method | Purpose |
|------|--------|---------|
| `/api/games?playerId=*` | GET | Lists saved games (`GameSaveMetadata`). |
| `/api/players` | GET | Returns seeded `PlayerProfile`s (deserialized from DB JSON). |
| `/api/images/{id}` | GET | Streams stored avatar image bytes. |
| `/api/companion/games` | GET | Lightweight list of active in-memory games (`GameStateMachineRegistry.GetAvailableGames`). |
| `/api/database/health` | GET | Reports `playerCount`, `gameCount`, `needsSeeding`, `needsGames`. |

## Import / Export

- `/api/game/import` (POST multipart/form-data) accepts `.catan` files, validates `SerializableLog`, and stores via `GamePersistenceService`.
- Export endpoint is not yet implemented (TODO).

## SignalR Contract (`/gameHub`)

### Client-to-Server Methods

`JoinGame`, `LeaveGame`, `Shuffle`, `Undo`, `Redo`, `Next`, `ExecutePurchase`, `BalanceBoard`, `ExecuteRoadPurchase`, `ExecuteBuildingUpgrade`,
`ExecuteMoveRobber`, `ExecuteRoll`, `ExecuteSetPlayerOrder`, `ExecuteParticipatingInSupplemental`, `ExecuteGoFirst`, `ExecuteSwapTileResources`.
Each method:

1. Fetches `GameStateMachine` from registry.
2. Validates caller with `ValidateCaller`.
3. Runs the shared async handler.
4. Broadcasts `GameStateUpdated` + issues `CommandCompleted` / `CommandFailed` callbacks.

### Server-to-Client Notifications

- `GameStateUpdated(GameModel model)` – full state sync.
- `PlayerPresenceChanged(playerId, isConnected)` – presence tracking.
- `CommandCompleted(commandId, success, message)` / `CommandFailed(commandId, errorInfo)` – command result metadata.

## Error Handling

- API controllers log using `ILogger.LogEvent` extension (see `GameServiceLogger`).
- SignalR errors include serialized metadata: message, operation, context, exception type, inner exception (preview), stack trace (debug only).
- Database failures during persistence are logged but do not interrupt gameplay (controller catches and swallows to avoid 500 unless critical).

## TODO / Open Work

- Authentication/authorization pipeline pending (`StartedBy` is placeholder string).
- `/api/game/action` still needed for parity but WebUI relies on SignalR direct methods; evaluate deprecation.
- Export API for `.catan` downloads remains unimplemented; CLI and Desktop rely on local file system today.
