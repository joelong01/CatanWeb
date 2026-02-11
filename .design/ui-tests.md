# UI Tests: Test Recording & Replay Management

## Overview

The Test Recordings page is a developer/QA tool for validating game state determinism.
It manages recorded gameplay sessions and replays them to verify that the game engine
produces consistent results (hash-verified at each action step).

## User Workflows

### 1. View Recordings

The page loads a table of all saved recordings from the backend. Each row shows:

- **Name** -- recording identifier
- **Date** -- when it was created
- **Game Type** -- Regular or Expansion
- **Players** -- player count
- **Actions** -- total recorded actions
- **Buttons** -- Select, Run, Rename, Delete

### 2. Full Replay (Single Recording)

Click "Run" on a recording row. The backend replays all actions from the initial
game state and validates that each action produces the expected game hash.

Result appears in a test results section below the table:

- Green row with checkmark for pass
- Red row with X for fail (includes action index and hash mismatch details)

### 3. Full Replay (All Recordings)

Click "Run All" in the section header. All recordings are replayed sequentially.
Results accumulate in the test results section.

### 4. Step-by-Step Replay

1. Click "Select" on a recording to load its action list
2. Actions table appears with columns: #, Action, Game State, Details, Expected
   Hash, Actual Hash, Status
3. Click "Start Replay" to create a server-side replay session
4. Click "Run Step" to execute one action at a time
5. After each step, the Actual Hash and Status columns update
6. The current action row is highlighted and auto-scrolled into view
7. Click "Reset" to end the session and start over

### 5. Board Preview (Optional)

When "Show Board" is enabled during step-by-step replay:

- The layout splits into two columns: actions table (left) and board preview (right)
- After each step, the board renders the current game state
- Click the preview to open a full-screen zoomed view
- The board shows tiles, buildings, roads, and player colors

### 6. Rename Recording

Click "Rename" to open a modal dialog with the current name pre-filled and
auto-selected. Press Enter to confirm or Escape to cancel.

### 7. Delete Recording

Click "Delete" to open a confirmation modal. Confirming removes the recording
from the database. If the deleted recording was selected, the actions view clears.

## API Endpoints

All endpoints are served by `RecordingController.cs`. No backend changes are needed
for the base feature. The board preview requires one small addition.

| Endpoint                               | Method | Purpose                          |
|----------------------------------------|--------|----------------------------------|
| `/api/recordings`                      | GET    | List all recordings              |
| `/api/recording/{id}/actions`          | GET    | Get action list for a recording  |
| `/api/recording/{id}/replay`           | POST   | Full replay with hash validation |
| `/api/recording/{id}/replay/start`     | POST   | Start step-by-step session       |
| `/api/replay/{sessionId}/step`         | POST   | Execute next action in session   |
| `/api/replay/{sessionId}`              | DELETE | End replay session               |
| `/api/recording/{id}/rename`           | PUT    | Rename (body: `{ name }`)        |
| `/api/recording/{id}`                  | DELETE | Delete recording                 |

### Step API Extension (for Board Preview)

The step endpoint accepts an optional query parameter `?includeGameModel=true`.
When set, the response includes the full `GameModel` after the action executes,
enabling the frontend to render the board state.

## Data Types

### RecordingSummary

```typescript
interface RecordingSummary {
  id: string;
  name: string;
  createdAt: string;
  gameType: string;
  playerCount: number;
  actionCount: number;
  gameId: string;
}
```

### ActionSummary

```typescript
interface ActionSummary {
  index: number;
  actionType: string;
  gameState: string;
  expectedHash: string;
  details: string;
}
```

### StepResult

```typescript
interface StepResult {
  success: boolean;
  actionIndex: number;
  expectedHash: string;
  actualHash: string;
  hashMatch: boolean;
  errorMessage?: string;
  gameModel?: GameModel;  // Only when includeGameModel=true
}
```

### ReplayResult

```typescript
interface ReplayResult {
  success: boolean;
  recordingName: string;
  actionsReplayed: number;
  totalActions: number;
  failedAtAction?: number;
  expectedHash?: string;
  actualHash?: string;
  errorMessage?: string;
}
```

## UI Layout

```text
+------------------------------------------------------------------+
| <- Back   Test Recordings                                        |
|           Recorded gameplay for testing GameService consistency   |
|                                                                  |
| Recordings                              [Run All] [Refresh]     |
| +--------------------------------------------------------------+|
| | Name     | Date       | Type    | Players | Actions | Actions ||
| |----------|------------|---------|---------|---------|---------|+|
| | Game 1   | 2026-01-15 | Regular | 4       | 120     | [S][R] ||
| | Game 2   | 2026-01-20 | Expand  | 6       | 200     | [S][R] ||
| +--------------------------------------------------------------+|
|                                                                  |
| Test Results                                                     |
| +--------------------------------------------------------------+|
| | [check] Game 1 -- Replay successful (120 actions)            ||
| | [x]     Game 2 -- Failed at action 45/200: Hash mismatch     ||
| +--------------------------------------------------------------+|
|                                                                  |
| Actions: Game 1          [Start Replay] [Show Board] [Close]    |
| +------------------------------+  +----------------------------+|
| | #  | Action | State | Hash   |  |                            ||
| |----|--------|-------|--------|  |   [Board Preview]           ||
| | 0  | Roll   | Setup | a1b2.. |  |                            ||
| | 1  | Build  | Setup | c3d4.. |  |   Click to zoom            ||
| | 2> | Road   | Setup | e5f6.. |  |                            ||
| +------------------------------+  +----------------------------+|
+------------------------------------------------------------------+
```

When "Show Board" is disabled, the actions table takes full width.

## Board Preview Architecture

The existing `GameBoard` component reads from the Zustand game store, which
would conflict with any active game. The `ReplayBoardPreview` component is a
standalone renderer that takes `GameModel` data as props.

It uses `HexGrid` with `fitToParent={true}` to auto-scale the board into a
fixed-size container. Clicking opens a full-screen modal with the board at
a larger size for detailed inspection.

## Navigation

- Home page dev ring: "Tests" tile at SW position with `faVial` icon
- Route: `/tests`
- Back link returns to home page
