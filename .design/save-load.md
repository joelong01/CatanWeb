# Save / Load Pipeline

**Last verified:** January 30, 2026

## Overview

Games are persisted through a two-table database design that separates
lightweight queryable metadata from compressed game data blobs.

## Save Pipeline

```
GameStateMachine
  → SerializableLog (game state + undo/redo stacks)
    → JSON serialization
      → Compression (byte array)
        → GameSaveDataEntity (blob)
        → GameSaveMetadataEntity (queryable fields + FK)
```

### What Gets Saved

The `SerializableLog` captures the complete game state:

- Current `GameModel` (full board, players, buildings, roads, etc.)
- Undo stack (previous states for undo support)
- Redo stack (states undone, available for redo)
- Random seed and iteration count (for deterministic replay)

### Auto-Save

After every gameplay command processed by `AsyncCommandProcessor`, the
game is automatically saved to the database in parallel with the SignalR
broadcast. This happens on a background thread and does not block the
command response.

### Manual Save/Export

The `POST /api/game/persist` endpoint supports three actions:

| Action | Behavior |
|--------|----------|
| Save | Write to database (upsert by GameId) |
| Load | Read from database and restore to memory |
| Export | Generate `.catan` file for download |

## Load Pipeline

```
GameSaveMetadataEntity (query by GameId)
  → GameSaveDataEntity (follow FK, load blob)
    → Decompress
      → Deserialize JSON to SerializableLog
        → Reconstruct GameStateMachine
          → Register in GameRegistry
            → Broadcast GameModel via SignalR
```

### Load Sources

| Source | Endpoint | Description |
|--------|----------|-------------|
| Database | `POST /api/game/{gameId}/load` | Load saved game into memory |
| Compressed log | `POST /api/game/load` | Load from uploaded log data |
| GameModel JSON | `POST /api/game/loadmodel` | Load from raw GameModel |
| File import | `POST /api/game/import` | Import `.catan` file |

## Game Completion

When a winner is declared:

1. `DeclareWinnerMessage` processed by GameStateMachine
2. GameState transitions to `GameOver`
3. A `CompletedGameEntity` is created with winner info + full compressed
   game history
4. The active `GameSaveMetadataEntity` + `GameSaveDataEntity` are retained
   (can be cleaned up manually)
5. Lifetime player statistics are updated if `SaveLifetimeStats` is true

## File Format

The `.catan` file format is a compressed JSON blob containing the
`SerializableLog`. This format is used for:

- Game export/import
- Test data (`Tests/Data/*.catan_test`)
- Backup and transfer between instances

## React Client Integration

`GameServiceProxy.ts` methods for persistence:

| Method | REST Call | Purpose |
|--------|-----------|---------|
| `createGame(players, settings)` | `POST /api/game/new` | Create new game |
| `loadGame(gameId)` | `POST /api/game/{gameId}/load` | Load from database |
| `joinGame(gameId)` | SignalR `JoinGame` | Connect to game updates |

After loading, the proxy joins the SignalR game group and receives the
full `GameModel` via `GameStateUpdated`.
