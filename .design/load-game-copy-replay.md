# Design: Copy and Replay Actions on Load Game Page

## Problem

The Load Game page has two per-row actions: Edit (rename) and Load (play).
Players need two more:

- **Copy** — clone a saved game as-is so they can branch from a mid-game
  state without losing the original.
- **Replay** — clone a saved game but reset it to the "Roll For Order" state
  (same board, same players, fresh game) so the same group can play again
  without setting up from scratch.

## UI Design

Replace the two inline icon buttons (✏️ ▶️) with a small action-menu
button (⋯ or ▾) that opens a popover/dropdown. This avoids crowding the row
with four icons and gives room for clear labels.

**Menu items (in order):**

| Item | Icon | Description |
|------|------|-------------|
| Load | ▶ | Open game — same as current Load button |
| Copy | ⧉ | Clone game as-is; new name "{Name} (Copy)", auto-enter rename mode |
| Replay | ↺ | Clone game reset to Roll For Order; new name "{Name} (Replay)" |
| Rename | ✏ | Inline rename — same as current Edit button |
| Delete | 🗑 | Delete game — same as current Delete icon |

Mobile card layout gets the same ⋯ menu replacing the current Play button.

## Backend Changes

### Fix: `POST /api/game/{gameId}/copy`

Currently requires the game to be in `GameStateMachineRegistry` (i.e., already
loaded into memory). Games sitting only in the database return 404.

Fix: if `GetGameStateMachine` throws, load the game from the database first
(same logic as the existing `LoadGame` endpoint), then proceed with the copy.

### New: `POST /api/game/{gameId}/replay`

Creates a copy of the game truncated to the first `WaitingForRollForOrder`
state in the DoneStack, so players reuse the same board and player order but
start from the beginning.

Steps:

1. Load game from registry or database (same fix as above).
2. Get the `SerializableLog`.
3. Find the index in `DoneStack` where the serialized GameModel has
   `GameState == WaitingForRollForOrder`.
4. Keep only `DoneStack[0..index]` (index inclusive) — discard all later
   states. Clear RedoStack.
5. Assign a new `GameId` and name `"{OriginalName} (Replay)"`.
6. Persist and register the new game.
7. Return `{ success, newGameId, gameName }`.

If no `WaitingForRollForOrder` state is found (e.g., game never got past
setup), return 422 with a clear error message.

### New: `GET /api/games` filter update

No change needed — the game list already excludes `GameOver` games and sorts
by `SavedAt DESC`, so copies/replays appear at the top automatically.

## Frontend Changes

**File:** `react-ui/app/load-game/page.tsx`

1. Replace the two icon buttons per row with an `ActionMenu` component.
2. `ActionMenu` renders a ⋯ button; click opens a small positioned dropdown
   with the five menu items above.
3. Copy handler:
   - Call `gameApi.loadGame(gameId)` to ensure game is in registry (can be
     removed once the backend fix lands, but harmless to keep).
   - Call `gameApi.copyGame(gameId)`.
   - Refresh game list.
   - Auto-focus the new row and enter rename mode.
4. Replay handler:
   - Call new `gameApi.replayGame(gameId)`.
   - Refresh game list.
   - Auto-focus the new row and enter rename mode.
5. Add `replayGame(gameId: string)` to `gameApi.ts`:
   `POST /api/game/{gameId}/replay`

## In-Game Copy Button (ActionCluster)

A second entry point for copying: a **Copy** button on the in-game command
bar (`ActionCluster.tsx`) so you can snapshot a game mid-play without leaving
the game screen or answering a prompt.

**Behavior:**

- Single click — no dialog, no prompt.
- Backend auto-generates the name: `"{GameName} - Copy N"` where N is the
  lowest integer not already taken by an existing game with that prefix.
- A brief toast/flash confirms: *"Saved as '{name}'"*.
- You stay in the current game — the copy is created in the background.

**Auto-naming on the backend:**
When `newName` is omitted (or empty) on `POST /api/game/{gameId}/copy`,
the controller queries `GameSaveMetadata` for games whose name matches
`"{OriginalName} - Copy %"` and picks the next available integer:

```text
"My Game - Copy 1"   → already exists
"My Game - Copy 2"   → already exists
"My Game - Copy 3"   → used as new name  ✓
```

This replaces the current default of appending `" (Copy)"` (no number),
which breaks on the second copy.

**Relationship to NavMenu "Save Copy":**
There is an existing "Save Copy" item in the NavMenu that opens
`window.prompt` for a name. That prompt will be removed — the NavMenu
item will call the same silent handler. Explicit renaming belongs in the
Load Game page.

**Files touched (in addition to the Load Game page changes above):**

| File | Change |
|------|--------|
| `GameApiController.cs` | Fix auto-name to use `"{Name} - Copy N"` pattern |
| `react-ui/components/game/controls/ActionCluster.tsx` | Add Copy button |
| `react-ui/app/game/[id]/page.tsx` | Add `handleCopyGame` (silent, shows toast); wire into `handleAction`; remove `window.prompt` from `handleSaveCopy` |

## What Stays the Same

- Load, Rename (inline F2), Delete — no behavior changes.
- `SavedGameSummary` type — no new fields needed.
- Keyboard shortcuts — F2, Enter, Delete still work on the selected row.
- Bulk-delete — unchanged.
- Mobile layout gets the same menu, just smaller.

## Scope

| File | Change |
|------|--------|
| `GameApiController.cs` | Fix copy to auto-load; add replay endpoint |
| `react-ui/app/load-game/page.tsx` | Replace icon buttons with action menu |
| `react-ui/lib/api/gameApi.ts` | Add `replayGame()` method |
