# Recording & Statistics

**Last verified:** January 30, 2026

## Recording Infrastructure

The system includes a recording infrastructure that captures gameplay for
regression testing. Recordings store every action and the resulting game
hash, enabling deterministic replay verification.

### Recording Flow

1. **Start:** `POST /api/recording/start/{gameId}` begins capturing
2. **Streaming:** `RecordingService` captures every action message after
   each `GameStateMachine` handler call
3. **Persistence:** Recordings are saved to the `RecordingEntity` database
   table immediately after each action (crash-safe)
4. **Stop:** `POST /api/recording/stop/{gameId}` finalizes the recording

### RecordingService

**File:** `Catan3.GameService/Services/RecordingService.cs`

Tracks active recordings in memory during gameplay. Key methods:

| Method | Purpose |
|--------|---------|
| `StartRecordingAsync(gameId, name, initialGameModel)` | Begin recording |
| `RecordActionAsync(gameId, message)` | Capture action + save |
| `StopRecordingAsync(gameId)` | Finalize recording |
| `CancelRecordingAsync(gameId)` | Remove from memory + database |
| `GetRecordingsAsync()` | List all recordings |
| `ImportRecordingAsync(...)` | Sync recordings between databases |
| `RenameRecordingAsync(recordingId, newName)` | Update name |

### Recording Data Format

```json
{
  "initialGameModel": { ... },
  "actions": [
    {
      "messageType": "RollMessage",
      "messageData": { ... },
      "expectedGameHash": "abc123",
      "gameState": "WaitingForRoll"
    }
  ]
}
```

Each action includes the expected `GameHash` for verification during
replay.

### Replay System

Replay re-runs recorded actions against the current `GameStateMachine`
and verifies the resulting `GameHash` matches the recorded hash at each
step.

**Full replay:** `POST /api/recording/{id}/replay` -- runs all actions,
returns pass/fail with first mismatch details.

**Step-by-step debugging:**

1. `POST /api/recording/{id}/replay/start` -- create replay session
2. `POST /api/replay/{sessionId}/step` -- execute next action
3. `DELETE /api/replay/{sessionId}` -- end session

Step-by-step mode allows inspecting the `GameModel` after each action to
diagnose where a hash mismatch occurs.

### Recording REST Endpoints

| Method | Route | Purpose |
|--------|-------|---------|
| GET | `/api/recordings` | List all recordings with metadata |
| GET | `/api/recording/{id}` | Get full recording data |
| POST | `/api/recording/start/{gameId}` | Start recording a game |
| POST | `/api/recording/stop/{gameId}` | Stop recording |
| GET | `/api/recording/status/{gameId}` | Check if game is being recorded |
| DELETE | `/api/recording/{id}` | Delete a recording |
| POST | `/api/recording/{id}/replay` | Full replay with hash verification |
| POST | `/api/recording/{id}/replay/start` | Start step-by-step session |
| POST | `/api/replay/{sessionId}/step` | Execute next action in session |
| DELETE | `/api/replay/{sessionId}` | End replay session |
| GET | `/api/recording/{id}/actions` | Get action summary list |
| PUT | `/api/recording/{id}/rename` | Rename recording |
| POST | `/api/recording/import` | Import from external source |
| POST | `/api/recording/cancel/{gameId}` | Cancel active recording |

### Supported Message Types

Recordings capture all message types: `ShuffleMessage`, `NextMessage`,
`GoFirstMessage`, `BuildingUpgradeMessage`, `RoadPurchaseMessage`,
`MoveRobberMessage`, `RollMessage`, `SetPlayerOrderMessage`,
`ParticipatingInSupplementalMessage`, `BalanceBoardMessage`,
`PurchaseMessage`, `UndoMessage`, `RedoMessage`,
`SwapTileResourcesMessage`, `DeclareWinnerMessage`.

## Lifetime Player Statistics

Statistics are tracked to settle debates about luck and strategy across
games.

### Metrics Tracked

| Category | Metric | Description |
|----------|--------|-------------|
| General | Games Played | Total games participated in |
| General | Wins | Total games won |
| Special | Longest Road Wins | Games ended with Longest Road |
| Special | Largest Army Wins | Games ended with Largest Army |
| Aggregates | Soldiers | Total, Max, Min, Average knights played |
| Aggregates | Stars | Total, Max, Min, Average victory points |
| Misery | Times Targeted | Robber targeting (Total, Max, Min, Avg) |
| Misery | Robber Losses | Resources lost to 7-rule (Total, Max, Min, Avg) |

### Implementation

- **Storage:** Stats are stored within the `PlayerEntity` JSON document
  (part of the `PlayerProfile`)
- **Update trigger:** `GameStateMachine.UpdateScore()` calculates local
  scores, but lifetime stats are only persisted when a winner is declared
  via `DeclareWinnerMessage`
- **Test isolation:** `SaveLifetimeStats` flag in `GameModel` prevents
  test games from polluting the leaderboard

### Stats REST Endpoints

| Method | Route | Purpose |
|--------|-------|---------|
| GET | `/api/stats` | Get all player statistics |
| GET | `/api/stats/export` | Export stats to JSON backup |
| POST | `/api/stats/import` | Import stats from JSON backup |
| DELETE | `/api/stats` | Reset all lifetime statistics |

### CLI Operations

```powershell
./catan.ps1 recording list       # View saved recordings
./catan.ps1 recording replay     # Re-run recorded game
./catan.ps1 recording save       # Export recording
./catan.ps1 recording load       # Import recording
```
