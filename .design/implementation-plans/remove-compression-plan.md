# Implementation Plan — Bound Undo Depth + Synchronous Save

Plan for design [.design/remove-compression.md](../remove-compression.md), epic
[#197](https://github.com/joelong01/CatanWeb/issues/197).

## Scope (five coordinated changes)

A. Bound `DoneStack` to `MaxUndoDepth = 25`.
B. Synchronous save — remove the fire-and-forget background path.
C. Replay anchor — store the `WaitingForRollForOrder` GameModel snapshot.
D. `TurnCount` metadata sourced from `RollModel.GameRollModel.TotalRolls`.
E. ReplayableRandom verified; `[PERF-SAVE]` emits `TotalRolls`.

Keep compression; keep `CanUndo`/`CanRedo` handlers. No new interface methods, no
DB migration. Only format change: an additive `AnchorState` field on the
server-side `SerializableLog` (backward compatible).

## A — Bound the undo stack — `Catan3.Shared/Utility/Log.cs`

**1. Constant holder** (non-generic, so callers need no type arg):

```csharp
public static class LogConstants { public const int MaxUndoDepth = 25; }
```

**2. Private helper** on `Log<T>`:

```csharp
private void EnforceUndoLimit()
{
    while (DoneStack.Count > LogConstants.MaxUndoDepth + 1)
        DoneStack.RemoveAt(0);   // drop oldest; current (top) never removed
}
```

**3. Call in `Done()`** after `DoneStack.Push(val)` (before `RedoStack.Clear()`).

**4. Call on load** after the rebuild loop in `FromSerializableLog` and
`LoadFromSerializableLog` — but **after** the anchor salvage in Part C (ordering
matters: salvage scans the full stack, then trim).

No change to the `CollectionChanged` handlers or `CanUndo`/`CanRedo`.

## B — Synchronous save — `Log.cs` + `GameStateMachine.cs`

**Log.cs:** delete `RequestSave()`, `RunSaveLoopAsync()`, `_saveRequested`,
`_saveRunning`, and the coalescing/thread-safety comment. Keep `SaveAsync()`
(still `async Task`, still compresses) **including its existing try/catch that
logs and swallows** — that is the error contract (review N3): a transient Cosmos
failure does not throw into the action path; the next full-snapshot save
self-heals. Add a comment noting the brief in-memory-ahead-of-Cosmos window.

**GameStateMachine.cs:**

- Convert `private void LogGameModel(GameModel)` →
  `private async Task LogGameModelAsync(GameModel)`; replace
  `_gameLog.RequestSave();` with `await _gameLog.SaveAsync();`.
- Convert the 15 callers (lines 164, 187, 209, 227, 245, 263, 281, 392, 479, 497,
  527, 573, 617, 671, 761) from `LogGameModel(m); return Task.FromResult(m);` to
  `await LogGameModelAsync(m); return m;`, marking each enclosing method `async`.
  (Undo/Redo don't call it; the comment at line 422 is not a call.)
- Verify new-game seed (`HandleNewGameAsync` → `InitializeLoggingState`) and
  `ReplayGame` still persist (they save via the controller, not `LogGameModel`).

Latency note (review N1): inline Cosmos write per action; accepted budget ~10 ms
median / ≤~50 ms P99 (turn-based game). Documented, not blocking.

## C — Replay anchor — `IGameLog.cs` + `Log.cs` + `GameApiController.cs`

The anchor is just the serialized `GameModel` while in `WaitingForRollForOrder`.

**`IGameLog.cs`:** add `public string? AnchorState { get; set; }` to
`SerializableLog`.

**`Log.cs`:**

- Field `private string? _anchorState;`.
- In `Done(model)`: if `model.GameState == GameState.WaitingForRollForOrder` and
  `_anchorState is null`, set `_anchorState` to the serialized snapshot. (`Log`
  already deserializes `GameModel` via `ToGameModel`, so reading `GameState` is
  in-layer.)
- `GetSerializableLog()`: set `log.AnchorState = _anchorState`.
- `FromSerializableLog` / `LoadFromSerializableLog`: set `_anchorState =
  sLog.AnchorState`; **if null, scan the rebuilt DoneStack** (`ToGameModel` each)
  for the first `WaitingForRollForOrder` and set `_anchorState` — **then**
  `EnforceUndoLimit()`. This salvages the anchor for legacy long saves before
  trimming removes it.

**`GameApiController.cs` (`ReplayGame`, ~793–829):** replace the DoneStack scan
with `sourceLog.AnchorState` (fall back to the existing scan only if null).
Deserialize it; the downstream reset (new `GameId`/name,
`Random = new ReplayableRandom()`, `Validate`, `UpdateGameHash`,
`InitializeLoggingState`) is unchanged. Keep the `TotalRolls <= 0`
eligibility guard.

## D — TurnCount ← TotalRolls

Replace `TurnCount = …DoneCount` (and the lowercase `turnCount = …DoneCount`
response field) with `…RollModel.GameRollModel.TotalRolls` from the current
GameModel:

- `GameApiController.cs`: 683, 861, 1689, 1921, 2007, 2021.
- `DatabasePersistenceService.cs:153`:
  `gameStateMachine.GetCurrentState().RollModel.GameRollModel.TotalRolls`.

Leave the unrelated `DoneCount = sourceLog.DoneCount` (sets
`SerializableLog.DoneCount`) and `TurnCount = gameSaveData.TurnCount`
pass-throughs.

## E — ReplayableRandom + PERF-SAVE trace

- No code change beyond C's `new ReplayableRandom()` on the replay seed.
- `Log.SaveAsync` `[PERF-SAVE]` line: add
  `totalRolls={gameModel.RollModel.GameRollModel.TotalRolls}` (the method already
  calls `CurrentState()`), so the harness keeps a real game-length axis after the
  cap (review Finding 6).
- The existing `ReplayTests` message-replay determinism suite is the end-to-end
  RNG guard.

## Files-modified table

| File | Change | Risk |
|------|--------|------|
| `Catan3.Shared/Utility/Log.cs` | `LogConstants`; `EnforceUndoLimit`; anchor capture + salvage; remove async-save machinery; PERF-SAVE `totalRolls` | Med |
| `Catan3.Shared/Interfaces/IGameLog.cs` | `SerializableLog.AnchorState` (additive) | Low |
| `Catan3.Shared/GameLogic/GameStateMachine.cs` | `LogGameModel` async; await in 15 callers | Med (broad, mechanical) |
| `Catan3.GameService/Controllers/GameApiController.cs` | `ReplayGame` uses `AnchorState`; `TurnCount`←`TotalRolls` (6 sites) | Low–med |
| `Catan3.GameService/Services/DatabasePersistenceService.cs` | `TurnCount`←`TotalRolls` | Low |
| `Tests/Shared/LogUndoLimitTests.cs` (new) | Undo-cap / anchor / RNG tests | None |

## Tests — `Tests/Shared/LogUndoLimitTests.cs`

xunit, `new Log<string>(null, "<tmp>.catan")`, minimal `GameModel`s via `Done`:

- `Done_PastCapacity_EvictsOldest` → `DoneCount == 26`, current is last pushed.
- `EnforceUndoLimit_NeverEvictsCurrentState` → after 100 pushes, `CurrentState()`
  equals the 100th value.
- `Undo_AvailableUpToMaxUndoDepth` → exactly 25 undo steps, then `CanUndo == false`.
- `Done_StillClearsRedo` → undo×2, push, then `CanRedo == false`, `RedoCount == 0`.
- `Done_FullWindow_UndoThenNewAction_CorrectCounts` → at cap, undo 5, push 6;
  counts correct and `CanRedo == false` after each push.
- `CanUndo_FalseAtStart_TrueAfterSecondState`.
- `Load_OverCapLegacy_TrimsToCap`.
- `Load_WithRedoStack_TrimDoesNotCorruptRedoBranch` → legacy 100 done + 5 redo;
  after load `RedoCount == 5` with the correct (most-recent) redo entries.
- `Undo_RestoresReplayableRandomState` → states with differing
  `Random.Iterations`; undo restores the right value.
- `Anchor_CapturedAtWaitingForRollForOrder` → after logging that state,
  `GetSerializableLog().AnchorState` is non-null and equals that snapshot.
- `Anchor_SalvagedOnLoad_WhenNull` → legacy over-cap `SerializableLog` with
  `AnchorState == null` but a `WaitingForRollForOrder` entry; after load,
  `AnchorState` is set even though that entry was trimmed from `DoneStack`.

GameService-level: `ReplayGame_AfterCapExceeded_Succeeds` → game > 26 moves still
replays from `AnchorState`.

## Verification

1. `pwsh ./catan.ps1 build` clean.
2. `pwsh ./catan.ps1 test` green incl. full `ReplayTests`.
3. Re-run #197 perf harness (`recording play -Name 'Simulated Regular Game'`);
   confirm `jsonSize`/`serialize` flat across `totalRolls`, PERF-SAVE on the
   request thread.
4. Manual: play past 25 moves → undo to limit → "play again" → board-identical
   replay. Post after-fix perf to #197.

## Out of scope (deferred)

- Removing compression (Profile B).
- App Service SKU downgrade — now potentially **1-vCPU**; epic closing step after
  perf confirmed (stopping ≠ saving on dedicated tiers; SQL already auto-pauses).

---

**STOP — awaiting plan approval before implementing.**
