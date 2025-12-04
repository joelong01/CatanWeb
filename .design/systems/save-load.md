# Save & Load Pipeline

Source: design_docs/save-load-games.md

## Overview

The game saves on every state transition through the shared `Log<string>` undo stack. The Game Service persists compressed history to SQLite,
and the WebUI rehydrates games by loading logs back into memory. Desktop continues to write `.catan` files through its `FileService` while
sharing the same core serialization types.

## Data Flow

```text
WebUI / Desktop  ──▶  GameService (GameApiController)  ──▶  GameStateMachineRegistry  ──▶  SQLite (GameSaveMetadata + GameSaveData)
```

1. Client issues a command via SignalR or REST.
2. `GameStateMachine` mutates state and pushes the new `GameModel` into `_gameLog` (`LogGameModel`).
3. `ProcessGameActionResult` (GameApiController) retrieves `GameStateMachine.GetSerializableLog()`, compresses JSON via `JsonHelper.Compress`,
   and passes bytes plus `GameMetadata` to `IGamePersistence.SaveAsync`.
4. `GamePersistenceService` upserts `GameSaveData` (blob) and `GameSaveMetadata` (queryable fields, incl. `TurnCount`, `PlayerNames`, `GameState`).
5. Updated `GameModel` broadcasts to all hub clients to keep them synchronized.

## Database Schema (Catan3.GameService/Data)

- `GameSaveMetadataEntity`
  - Columns: `GameId` (unique), `StartedBy`, `SavedAt`, `CreatedAt`, `GameState`, `GameType`, `PlayerCount`, `PlayerNames`, `TurnCount`,
    `GameName`, FK `GameDataId`.
  - EF indexes configured for `GameId`, `StartedBy`, `SavedAt`.
- `GameSaveDataEntity`
  - Stores `CompressedData` (byte[]) and `Size` (int).
- `GameMetadata` DTO mirrors metadata fields and is constructed in `GameApiController.SaveGameToDatabase`.

## Loading Saved Games

1. WebUI fetches metadata using `GET /api/games?playerId=*` (controller filters out `GameOver`).
2. Selecting a game triggers `POST /api/game/{gameId}/load`.
3. Controller queries metadata + blob, decompresses JSON via `JsonHelper.Decompress`, deserializes to `SerializableLog`, and rebuilds a
   `Log<string>` with `Log<string>.FromSerializableLog`.
4. A `GameStateMachine` is created with the restored log and registered in `GameStateMachineRegistry` for in-memory access.
5. When a client joins via `GameHub.JoinGame`, the current `GameModel` is sent immediately over `GameStateUpdated`.

## Desktop Save Files

- Desktop uses `Trace<string>` with `FileService` to persist `.catan` (compressed `SerializableLog`) and `.catan_test` (action log) to the file
  system.
- `PersistGameMessage` handlers in `GameMessageService` map UI commands (`Save`, `SaveAs`, `Open`) to the shared `IPersistenceService`.
- `.catan` archives imported into service via `POST /api/game/import` are stored using the same `IGamePersistence.SaveAsync` pipeline.

## Scripts & Seeding

- `webui.ps1 run` starts the Game Service, calls `/api/database/health`, and batches `.catan` imports from `Default Data/Games/` when
  `needsGames == true`.
- On startup `DatabaseSeeder.SeedAsync` loads player avatars from `Default Data/Players/` if the tables are empty.

## TODO / Observations

- `StartedBy` currently hard-codes "WebUI" in `GameApiController`. Integrate authentication claims once available.
- Import endpoint does not deduplicate player profiles bundled in `.catan` archives; confirm cross-environment compatibility before enabling
  user uploads.
- Desktop save slots and service database coexist but are not synchronized. Document a migration plan for converging user IDs when accounts are
  introduced.
