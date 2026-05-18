# Design: Fix Replay Action (Issue #145)

## Problem

The "Replay" menu option on the Open Game page does not work. Replay is
supposed to clone a saved game so the same players can play again on the same
board, starting from `GameState.WaitingForRollForOrder`. Three defects break
this:

1. **GameId / GameName never get rewritten.** The backend rewrites the cloned
   snapshots with a string replace targeting PascalCase, space-free JSON
   (`"GameId":"..."`), but the serializer emits camelCase
   (`"gameId":"..."`). The replace never matches, so the replay's GameModel
   keeps the **original** GameId while it is registered and persisted under a
   **new** GUID. The registry key and the model's own `GameId` disagree, which
   breaks the SignalR join — the visible "doesn't work" symptom.

2. **Randomness is not reset.** The retained `WaitingForRollForOrder` snapshot
   still carries the original game's `ReplayableRandom` (seed + iterations at
   that moment). Replaying from it reproduces the original game's exact dice —
   a deterministic re-watch, not a fresh play.

3. **No navigation after replay.** The frontend creates the replay, refreshes
   the list, and drops the new game into inline rename mode. It never opens
   the game, so clicking Replay appears to do nothing.

## Relevant Code

| Concern | Location |
|---------|----------|
| Replay endpoint | `Catan3.GameService/Controllers/GameApiController.cs:764-865` |
| Snapshot string replace (bug 1) | `GameApiController.cs:811-815` |
| Serializer casing | `Catan3.Shared/Utility/JsonHelper.cs:20` |
| `ReplayableRandom` | `Catan3.Shared/Utility/ReplayableRandom.cs` |
| `DoneStack` ordering / round-trip | `Catan3.Shared/Utility/Log.cs:284-339` |
| Replay handler (frontend) | `react-ui/app/load-game/page.tsx:251-277` |
| `replayGame` API client | `react-ui/lib/api/gameApi.ts:326-334` |
| Original feature design | `.design/load-game-copy-replay.md` |

## Decisions (from design conversation)

- **Random behavior: brand-new random.** The replay gets a fresh, independent
  `ReplayableRandom` (new random seed, `Iterations = 0`). The board layout and
  player roster are preserved automatically because they are concrete data in
  the `WaitingForRollForOrder` snapshot — `Shuffle()` is not re-run on replay,
  so changing the seed cannot change the board.
  - **Deviation from issue #145:** the issue text asks for the *same* seed
    with `Iterations` advanced to one past the original game's last value.
    Brand-new random was chosen instead: simpler, no need to scan the full
    history for the max iteration count, and player-indistinguishable from the
    issue's approach (both yield fresh, reproducible-but-different dice; the
    board is identical either way). Recorded here for traceability.
- **Post-replay UX: open the new game immediately.** After a successful
  replay, navigate to `/game/{newGameId}` so the group lands in
  `WaitingForRollForOrder` ready to play, instead of returning to the list in
  rename mode.

## Approach

### Backend: reuse the loadmodel path, not string replace

Replace the fragile string replacement with: an eligibility guard, then
deserialize the single `WaitingForRollForOrder` snapshot, reset its identity
and randomness, and seed a fresh game from it via the existing
`InitializeLoggingState` path.

1. Load the source game (registry or database) — unchanged.
2. **Eligibility guard:** read the source game's *current* state
   (`GetCurrentState()`); if `RollModel.GameRollModel.TotalRolls <= 0`, return
   422 — a game with no rolls was never played, so there is nothing to replay.
   `TotalRolls` is judged from the current state, not the
   `WaitingForRollForOrder` snapshot (where it is always `0`).
3. Find the oldest `WaitingForRollForOrder` snapshot in the `DoneStack` —
   unchanged (this logic is correct). Its `replayIndex < 0` → 422 stays as
   defensive cover (unreachable once the rolls guard passes).
4. Deserialize that snapshot to `GameModel`, then set:
   - `GameId` = new GUID
   - `GameName` = provided name or `"{OriginalName} (Replay)"`
   - `Random` = a fresh `ReplayableRandom()` (new seed, `Iterations = 0`)
   - `GameState` confirmed to be `WaitingForRollForOrder`
5. Seed the new game via the same path `loadmodel` / `HandleNewGameAsync`
   use: empty `Log<string>` →
   `CreateGameStateMachineWithServiceDependencies` →
   `InitializeLoggingState(model)` → register under the new GameId. Then
   persist (compress `GetSerializableLog()` + `SaveAsync`).

This removes any dependency on JSON property casing, drops the manual
`SerializableLog` assembly in favour of a proven API, and guarantees the
registry key, the persisted key, and `GameModel.GameId` all agree.

### DoneStack shape (decided)

The replay's `DoneStack` contains exactly **one** entry — the mutated
`WaitingForRollForOrder` snapshot. `RedoStack` is empty.

Rationale: a single entry means no undo back into board-picking, and there is
no second snapshot carrying a stale `Random` that undo/redo could promote back
to "current." It matches the issue's intent ("starting with the
WaitingForRollForOrder state"). Confirmed by the developer.

Note: with one entry there is nothing to undo, so an `Undo` at the very first
replay state is rejected by the `GameStateMachine` (existing behavior — it
throws "nothing to undo"), not silently absorbed. The test therefore proves
the single-entry shape via the persisted `turnCount == 1`, not by invoking
`Undo`.

### Frontend: navigate on success

In `handleReplayGame` (`load-game/page.tsx`), after `gameApi.replayGame`
returns success, call `router.push(\`/game/${newGameId}\`)` instead of
refreshing the list and entering rename mode — mirroring the existing
`openGame` path. The backend already registers the new game in the
`GameStateMachineRegistry`, so it is immediately joinable via SignalR.

## Data Flow (after fix)

```text
User clicks Replay
  → POST /api/game/{gameId}/replay
      → load source game
      → eligibility: current state TotalRolls > 0  (else 422)
      → find oldest WaitingForRollForOrder snapshot
      → deserialize snapshot → set new GameId, GameName, fresh Random
      → InitializeLoggingState(model)  → single-entry log
      → register under newGameId + persist
      → 200 { success, newGameId, gameName }
  → frontend router.push(/game/{newGameId})
      → SignalR join → GameModel (WaitingForRollForOrder, same board,
        same players, fresh randomness)
```

## What Stays the Same

- The `DoneStack` search for the oldest `WaitingForRollForOrder` state.
- Registry registration and database persistence mechanics.
- The `replayGame` API client signature (`gameId`, optional `newName`).
- Copy, Rename, Delete, Load actions — untouched.
- The 422 response when no `WaitingForRollForOrder` state exists.

## Automated Testing

A new integration test follows the repo convention: a `WebApplicationFactory`
test driving the server through `GameServiceProxy`, the same infrastructure as
`ReplayTests/ReplayTest.cs`. Not a direct controller unit test — the repo has
no precedent for those.

**New file:** `Tests/GameService/ReplayTests/ReplayEndpointTests.cs`

### Fixture: the longest already-seeded game

The standard workflow always seeds the database after creating it
(`./catan.ps1 database install` → `Invoke-SeedDefaultData` loads
`Default Data/Games/`), and the Cosmos emulator persists that data across
runs. So the test **assumes the seed games are present** and picks one at
runtime rather than committing a new fixture:

1. `GET /api/games?playerId=*` → all saved games.
2. If the list is empty, `Assert.Fail` with a clear message:
   *"No seeded games found — run `./catan.ps1 database install`."*
3. Pick the game with the highest `TurnCount` (longest game → its `DoneStack`
   is guaranteed to contain a `WaitingForRollForOrder` snapshot).

No new fixture is committed and the seeding pipeline is unchanged.

### Positive test — `Replay_FromLongestSeedGame_ResetsToRollForOrder`

1. Select the highest-`TurnCount` game from `GET /api/games` (fail clearly if
   none — see fixture note above).
2. `POST /api/game/{originalId}/load` to register it from the DB; join it and
   capture original `GameModel`: tiles (resource per tile key), numbers,
   harbors, player Ids.
3. `POST /api/game/{originalId}/replay` → `newGameId`.
4. Join `newGameId` and assert the returned `GameModel`:

| Assertion | Proves |
|-----------|--------|
| Join succeeds; `GameId == newGameId` and `!= originalId` | Loads OK; Bug 1 (GameId rewrite) |
| `GameState == WaitingForRollForOrder` | Correct start state |
| Tiles, numbers, harbors identical to original | Same board |
| Same player Ids present | All players present |
| No player resources / score / dev cards; no owned buildings; no owned roads | Clean reset |
| `random.iterations == 0` and `random.seed != original` | Bug 2 (fresh random) |
| replay listed by `GET /api/games` with `turnCount == 1` | Single-entry history (nothing to undo into setup) |

### Negative test — `Replay_NeverRolledGame_Returns422`

Create a game via `POST /api/game/new`, leave it in `PickingBoard`
(`TotalRolls == 0`), call replay → assert HTTP 422 (eligibility guard).

**Run:** `dotnet test Tests\GameService\Tests.GameService.csproj --filter "ReplayEndpointTests"`
(and via `./catan.ps1 test`, which starts the Cosmos emulator the integration
tests require).

## Manual Verification

1. `pwsh ./catan.ps1 build` — solution builds.
2. Play a game past board setup into mid-game; save it.
3. From the Open Game page, click Replay on that game.
4. Confirm: the app navigates straight into the new game; state is
   `WaitingForRollForOrder`; board layout and player roster match the
   original; the URL/game uses the new GUID.
5. Roll for order and play a few turns; confirm dice differ from the original
   playthrough (fresh randomness).
6. Reload the replay from the Open Game page; confirm it loads correctly under
   its own GUID (proves GameId was rewritten).
7. Replay a game still in board setup (no `WaitingForRollForOrder` yet);
   confirm a clear error, not a crash.

## Scope

| File | Change |
|------|--------|
| `Catan3.GameService/Controllers/GameApiController.cs` | Add `TotalRolls > 0` eligibility guard; deserialize the snapshot, set new `GameId`/`GameName`/fresh `Random`, seed via `InitializeLoggingState`; delete the string-replace and manual `SerializableLog` assembly |
| `react-ui/app/load-game/page.tsx` | Navigate to `/game/{newGameId}` on replay success |
| `Tests/GameService/ReplayTests/ReplayEndpointTests.cs` | New — positive (longest seed game) + negative (never-rolled → 422) integration tests |
| `Catan3.GameService/Controllers/RecordingController.cs` | Drive-by: guard null id in `ReplayRecording` (fixes pre-existing CS8604) |
