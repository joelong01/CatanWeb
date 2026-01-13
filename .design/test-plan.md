# WebUI Test Plan

**Last Updated:** 2026-01-13
**Status:** Implemented (Parts A-I complete)
**Related:** [webui.md](projects/webui.md), [game-service-api.md](systems/game-service-api.md)

## Overview

This document describes a comprehensive test suite for the Catan WebUI using a **recording/replay approach**
that captures real gameplay and replays it to verify GameService consistency.

Key features:

- **Recording Infrastructure** - Capture gameplay at runtime from both GameHub (SignalR) and REST API
- **Database Storage** - Persist recordings immediately and after each action for crash recovery
- **Tests Page** - Manage recordings with Run, Rename, and Delete functionality
- **Headless CI Tests** - Automated replay tests for continuous integration

## Architecture

### Recording Flow

```text
User starts new game with "Record Game" checked
   OR clicks "Record" in NavMenu during gameplay
        ↓
RecordingService.StartRecordingAsync(gameId, gameName, initialGameModel)
        ↓
Recording saved to database IMMEDIATELY
        ↓
GameHub/REST API receives action
        ↓
RecordingService.RecordActionAsync(gameId, message)
        ↓
Recording updated in database AFTER EACH ACTION (crash recovery)
        ↓
User clicks "Stop Recording" in NavMenu
        ↓
Recording finalized (no prompt - name already set from game name)
```

### Replay Flow

```text
Test loads Recording from database
        ↓
Creates game with initialGameModel via LoadGameModelAsync()
        ↓
For each recorded action:
    - Execute action on GameStateMachine
    - Compare GameHash with expected
        ↓
All hashes match = PASS
```

## Database Schema

**RecordingEntity** (`Catan3.GameService/Data/RecordingEntity.cs`):

```csharp
public class RecordingEntity
{
    public string Id { get; set; }              // GUID
    public string Name { get; set; }            // Set at recording start (from game name)
    public DateTime CreatedAt { get; set; }
    public string GameType { get; set; }
    public int PlayerCount { get; set; }
    public string PlayerIds { get; set; }       // Comma-separated
    public int ActionCount { get; set; }
    public string Data { get; set; }            // JSON blob (RecordingData)
}
```

## Recording Data Format

Uses existing `IRecordedMessage` types from `RecordedMessage.cs`:

```json
{
  "initialGameModel": { ... full GameModel ... },
  "actions": [
    { "recordType": "Shuffle", "expectedGameHash": "abc123", ... },
    { "recordType": "Next", "expectedGameHash": "def456", ... },
    { "recordType": "Roll", "roll": { "redRoll": 3, "whiteRoll": 4 }, "expectedGameHash": "ghi789" }
  ]
}
```

## API Endpoints

| Endpoint | Method | Purpose |
|----------|--------|---------|
| `/api/recording/start/{gameId}` | POST | Start recording (body: `{ "name": "..." }` optional, defaults to game name) |
| `/api/recording/stop/{gameId}` | POST | Stop recording (no body needed - name already set) |
| `/api/recording/status/{gameId}` | GET | Get recording status (isRecording, actionCount, name) |
| `/api/recording/cancel/{gameId}` | POST | Cancel recording and delete from database |
| `/api/recordings` | GET | List all recordings |
| `/api/recording/{id}` | GET | Get recording data |
| `/api/recording/{id}` | DELETE | Delete recording |
| `/api/recording/{id}/rename` | PUT | Rename recording (body: `{ "name": "..." }`) |
| `/api/recording/{id}/replay` | POST | Replay recording server-side, returns pass/fail |
| `/api/recording/{id}/actions` | GET | Get action list for display (index, type, state, hash, details) |
| `/api/recording/{id}/replay/start` | POST | Start step-by-step replay session |
| `/api/replay/{sessionId}/step` | POST | Execute next action, returns hash comparison result |
| `/api/replay/{sessionId}` | DELETE | End replay session and cleanup |

## WebUI Features

### Recording from New Game Page

- **"Record Game" checkbox** next to "House Rules" on `/newgame`
- When checked, recording starts automatically with the game name
- Recording is saved immediately to database

### Recording Toggle in NavMenu

- **"Record" button** in NavMenu when on Game page (below "New Game")
- Uses current game name for recording name
- Shows **"Stop Recording (N)"** with action count when active
- Pulsing red highlight indicates active recording

### Tests Page (`/tests`)

**Recordings Table (top):**
- Scrollable table of all recordings: Name, Date, Game Type, Players, Actions
- **Select button** - Load recording actions into second table
- **Run All button** - Replay entire recording and verify GameHash consistency
- **Rename button** - Change recording name via modal dialog
- **Delete button** - Remove recording with confirmation dialog
- **Run All (global)** - Execute all recordings sequentially
- **Results display** - Pass/fail for each full test run

**Actions Table (bottom, shown when recording selected):**
- Scrollable table showing all actions in selected recording
- Columns: #, Action, Game State, Details, Expected Hash, Actual Hash, Status
- **Start Replay button** - Initialize step-by-step replay session
- **Run Step button** - Execute current action and compare hashes
- **Reset button** - End session and clear results
- Current action highlighted in blue
- Executed actions colored green (pass) or red (fail)
- Hash comparison with ✓/✗ status indicators

### Home Page

- **Tests button** above Troubleshoot with visual separator
- Gray utility button style to distinguish from main gameplay buttons

## Implementation Status

### Part A: Database & Entity - COMPLETE

| Step | Status | Description |
|------|--------|-------------|
| A1 | Done | Created `RecordingEntity.cs`, added to `CatanDbContext` |
| A2 | Done | Database migration applied |

### Part B: Recording Service - COMPLETE

| Step | Status | Description |
|------|--------|-------------|
| B1 | Done | `RecordingService.cs` with async Start/Stop/Record methods |
| B2 | Done | `ActiveRecording` class for in-memory tracking |
| B3 | Done | Immediate save at start, save after each action for crash recovery |
| B4 | Done | `RenameRecordingAsync()` method added |

### Part C: Recording API - COMPLETE

| Step | Status | Description |
|------|--------|-------------|
| C1 | Done | `RecordingController.cs` with GET endpoints |
| C2 | Done | POST start/stop endpoints (name at start, not stop) |
| C3 | Done | DELETE endpoint |
| C4 | Done | PUT rename endpoint |
| C5 | Done | POST replay endpoint |

### Part D: Hook Recording - COMPLETE

| Step | Status | Description |
|------|--------|-------------|
| D1 | Done | `GameHub.cs` records all 15 action types via `TryRecordActionAsync()` |
| D2 | Done | `GameApiController.cs` records shuffle and winner actions |

### Part E: WebUI Recording - COMPLETE

| Step | Status | Description |
|------|--------|-------------|
| E1 | Done | Recording methods in `GameServiceProxy.cs` and `GameConnectionService.cs` |
| E2 | Done | Recording toggle in `NavMenu.razor` (Game page context) |
| E3 | Done | "Record Game" checkbox in `NewGame.razor` |

### Part F: Tests Page - COMPLETE

| Step | Status | Description |
|------|--------|-------------|
| F1 | Done | `Tests.razor` page with recording list table |
| F2 | Done | "Tests" button in `NavMenu.razor` and `Home.razor` |
| F3 | Done | Delete action with confirmation dialog |
| F4 | Done | Rename action with modal dialog |

### Part G: Non-Interactive Replay - COMPLETE

| Step | Status | Description |
|------|--------|-------------|
| G1 | Done | Replay API endpoint with GameHash verification |
| G2 | Done | "Run" button per recording |
| G3 | Done | "Run All" button |

### Part H: Interactive Test Runner - COMPLETE

| Step | Status | Description |
|------|--------|-------------|
| H1 | Done | Step-by-step replay API endpoints (start, step, end) |
| H2 | Done | Two-table layout on Tests page (recordings + actions) |
| H3 | Done | Run Step button with hash comparison and pass/fail indicators |

### Part I: Headless Tests - COMPLETE

| Step | Status | Description |
|------|--------|-------------|
| I1 | Done | `./catan.ps1 replay` command to run all recording tests |
| I2 | Done | Fetches recordings via API, runs replay, reports pass/fail |

## Files Created

| File | Purpose |
|------|---------|
| `Catan3.GameService/Data/RecordingEntity.cs` | Recording entity for EF Core |
| `Catan3.GameService/Services/RecordingService.cs` | Recording business logic |
| `Catan3.GameService/Controllers/RecordingController.cs` | Recording REST API |
| `WebUI/Pages/Tests.razor` | Tests management page |
| `WebUI/Pages/Tests.razor.css` | Tests page styles |

## Files Modified

| File | Changes |
|------|---------|
| `Catan3.GameService/Data/CatanDbContext.cs` | Added `DbSet<RecordingEntity> Recordings` |
| `Catan3.GameService/Hubs/GameHub.cs` | Added `TryRecordActionAsync()` for all 15 action handlers |
| `Catan3.GameService/Controllers/GameApiController.cs` | Added recording hooks for shuffle/winner |
| `Catan3.Shared/Services/GameServiceProxy.cs` | Added recording API methods |
| `WebUI/Services/GameConnectionService.cs` | Added recording wrapper methods |
| `WebUI/Layout/NavMenu.razor` | Added Record/Stop Recording toggle in Game context |
| `WebUI/Layout/NavMenu.razor.css` | Added recording-active animation styles |
| `WebUI/Pages/NewGame.razor` | Added "Record Game" checkbox |
| `WebUI/Pages/Home.razor` | Added Tests button with separator |

## Running Tests

```bash
# Run ALL recording replay tests (requires server running)
pwsh ./catan.ps1 replay

# Run existing xUnit replay tests
dotnet test Tests/GameService --filter "ReplayRegularTest"

# List recordings via API
curl http://localhost:8080/api/recordings

# Run a specific recording replay (full)
curl -X POST http://localhost:8080/api/recording/{id}/replay

# Get actions from a recording
curl http://localhost:8080/api/recording/{id}/actions

# Start step-by-step replay session
curl -X POST http://localhost:8080/api/recording/{id}/replay/start

# Execute one step
curl -X POST http://localhost:8080/api/replay/{sessionId}/step

# End replay session
curl -X DELETE http://localhost:8080/api/replay/{sessionId}

# Rename a recording
curl -X PUT http://localhost:8080/api/recording/{id}/rename \
  -H "Content-Type: application/json" \
  -d '{"name": "new-name"}'
```

## Key Design Decisions

1. **Name at start, not stop** - Recording name is set when recording starts (from game name),
   eliminating the need for a prompt when stopping

2. **Immediate database save** - Recording is saved to database immediately when started,
   and updated after each action, providing crash recovery

3. **NavMenu location** - Recording toggle is in NavMenu (below "New Game") rather than
   the command bar, keeping the command bar focused on game actions

4. **Auto-start from New Game** - "Record Game" checkbox on New Game page allows
   recording to start with the game, capturing all actions from the beginning

## Benefits

1. **Real test data** - Captures actual user gameplay
2. **Easy test creation** - Just play the game and record
3. **Crash recovery** - Recordings saved after each action
4. **Migration validation** - Same recordings work for SignalR AND REST API
5. **Regression testing** - Record bug scenarios, fix, keep recording forever
6. **Reuses existing infrastructure** - IRecordedMessage types, ReplayTest pattern

## Acceptance Criteria

- [x] Recording service captures actions from both GameHub and REST API
- [x] Recording name set at start (from game name), not at stop
- [x] Recordings save immediately and after each action (crash recovery)
- [x] Recordings persist in database
- [x] "Record Game" checkbox on New Game page
- [x] Recording toggle in NavMenu for Game page
- [x] Tests page lists all recordings
- [x] Run button replays and verifies GameHash
- [x] Rename button changes recording name
- [x] Delete button with confirmation
- [x] Run All button executes all tests
- [x] Tests button on Home page (above Troubleshoot)
- [x] Step-by-step replay with hash comparison and pass/fail indicators
- [x] Two-table layout with recordings and actions
- [x] `./catan.ps1 replay` command for automated testing
- [x] Replay tests integrated into pre-checkin workflow
- [ ] At least 3 recordings created covering different game scenarios
