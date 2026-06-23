# Bound Undo Depth + Synchronous Save (epic #197)

Design for epic [#197](https://github.com/joelong01/CatanWeb/issues/197).
Supersedes the earlier "remove compression" framing — compression stays; the
real win is a bounded stack that lets saving run synchronously on one thread.

## Problem

Every game action serializes the entire undo history and Brotli-compresses it on
every save — O(N) in move count. Measured baseline (#197): ~38–40 KB/turn,
~4 MB at 102 turns, ~10 MB at 200+. To keep the action path responsive, saving
was moved to a background fire-and-forget thread (#155). That background thread
is why the service needs ≥2 cores and still stalls past ~150 moves.

## The fix, in one idea

**Bound the undo history to a small constant.** Then per-save serialize+compress
is cheap and constant, which lets us **delete the background-save path and save
synchronously on the action thread**. That removes all stack-access concurrency
and lets the service run on a **1-vCPU SKU**.

## Locked decisions

- **D1 — Bound `DoneStack` to `MaxUndoDepth = 25`** (capacity 26: current + 25).
  Cap the existing stacks; no ring-buffer rewrite.
- **D2 — Synchronous save.** Remove `RequestSave`/`RunSaveLoopAsync`/
  `_saveRequested`/`_saveRunning`; `LogGameModel` awaits `SaveAsync()` inline.
- **D3 — Keep compression** (O(1) on bounded input).
- **D4 — Keep `CanUndo`/`CanRedo` change-notification handlers** (proven; now
  trivially race-free since saves are synchronous).
- **D5 — Replay: store the setup snapshot** as `AnchorState` on `SerializableLog`
  (server-only persistence; never sent to the client). Replaces the earlier
  "reconstruct from current GameModel" plan — exact and far lower risk than
  re-deriving every per-game field for Regular + Expansion (review N2).
- **D6 — `TurnCount` metadata: source from `RollModel.TotalRolls`** (in GameModel).
- **D7 — ReplayableRandom must remain correct** across undo/redo and replay.

## Guiding constraints

- **Client rendering is driven by the GameModel.** Nothing the client renders may
  depend on the undo log or server-only structures. `AnchorState` lives in
  `SerializableLog` (server persistence) and is never sent to the client, so it
  honors this rule.
- **Reuse existing APIs.** No new persistence/interface methods and no DB
  migration. The only format change is an **additive** `AnchorState` field on the
  server-side `SerializableLog` (backward compatible — old saves deserialize with
  it null and are salvaged on load).
- Observable game changes are limited to: undo depth (D1) and the saved-games
  list "turns" number (D6). Replay (D5) is behavior-preserving.

## Current architecture (as-is)

- `Catan3.Shared/Utility/Log<T>`: `DoneStack`/`RedoStack`
  (`ObservableCollection<T>`); `Done()` pushes + clears redo; `Undo`/`Redo` move
  between stacks; `CanUndo`/`CanRedo` maintained by `CollectionChanged` handlers.
  `Log` already deserializes GameModels (`ToGameModel`), so it may inspect
  `GameState`.
- `GameStateMachine.LogGameModel()`: recompute → flags → hash → `_gameLog.Done()`
  → **`_gameLog.RequestSave()` (fire-and-forget background save)**.
- `Log.SaveAsync()` serializes the whole `SerializableLog`, Brotli-compresses,
  writes bytes to Cosmos. It already wraps its body in try/catch that **logs and
  swallows** ([Log.cs:663](../Catan3.Shared/Utility/Log.cs)).
- `ReplayGame` ([GameApiController.cs:793](../Catan3.GameService/Controllers/GameApiController.cs)):
  scans `DoneStack` for the first `WaitingForRollForOrder` snapshot and seeds a
  new game from it. The oldest snapshot — first thing eviction would drop.
- `TurnCount` metadata = `SerializableLog.DoneCount` at ~6 sites — would cap at 26.
- `GameModel.Random` is serialized `ReplayableRandom` (`Seed` + `Iterations`).

## Semantics audit

| Behavior | Today | After |
| --- | --- | --- |
| Act-after-undo discards redo branch | `Done()` → `RedoStack.Clear()` | **unchanged** |
| Undo / Redo mechanics + flags | move between stacks, set flags | **unchanged** |
| `CanUndo`/`CanRedo` values | `Count>1` / `Count>0` (event-maintained) | **unchanged** |
| `SetActionFlags` runs before `Done()` | reads pre-push stack, so each saved snapshot's `UndoEnabled` lags one action | **unchanged** (pre-existing, intentional) |
| Save coalescing / threading | background fire-and-forget | **removed — synchronous (D2)** |
| **Undo depth** | unbounded | **capped at 25 (D1)** |
| Replay source | scan DoneStack | **`AnchorState` field (D5)** — same result |
| `TurnCount` source | `DoneCount` | **`TotalRolls` (D6)** |
| Compression | Brotli | **unchanged** |

## Part A — Bound the undo stack (`Log.cs`)

In `Done()`, after the push, evict oldest until within capacity:

```csharp
DoneStack.Push(val);
EnforceUndoLimit();   // while (DoneStack.Count > LogConstants.MaxUndoDepth + 1) DoneStack.RemoveAt(0);
RedoStack.Clear();    // unchanged: act-after-undo
```

- Current state (top) is never evicted; `RemoveAt(0)` is O(n≤26).
- Also enforce after the DoneStack rebuild in `FromSerializableLog` /
  `LoadFromSerializableLog` (see Part C for ordering vs. anchor salvage).
- `CanUndo`/`CanRedo` handlers fire on every mutation (incl. `RemoveAt`), so flags
  stay correct.
- Constant: `public static class LogConstants { public const int MaxUndoDepth = 25; }`.

## Part B — Synchronous save (`Log.cs` + `GameStateMachine.cs`)

- Delete `RequestSave`, `RunSaveLoopAsync`, `_saveRequested`, `_saveRunning`, and
  the coalescing/thread-safety comment.
- `LogGameModel` → `async Task`; `await _gameLog.SaveAsync()` at the end. The ~15
  `Handle*Async` callers already return `Task`; await it. (Undo/Redo don't call
  it.)
- One thread mutates and reads the stacks → the `RemoveAt(0)` /
  `GetSerializableLog()` race cannot occur. No lock.

**Error contract (review N3):** keep `SaveAsync`'s existing swallow-and-log. A
transient Cosmos failure does not throw into the action path (behavior matches
today). Because each save writes the **full** bounded snapshot (not a delta), a
missed save self-heals — the next successful action save includes the earlier
state. Document the brief in-memory-ahead-of-Cosmos window in the comment.

**Latency budget (review N1):** each action now awaits the Cosmos write inline.
This is a turn-based game; the accepted budget is ~10 ms median / ≤~50 ms P99 per
action, well below the perceptible threshold. The original problem (50–900 ms
long-game stalls) is gone because compression is now O(1). If P99 ever matters,
the fallback is notify-then-save ordering (still single-threaded / 1-proc).

## Part C — Replay anchor (`SerializableLog` + `Log.cs` + `ReplayGame`)

Store the setup snapshot once, server-side, instead of scanning a now-bounded
stack.

- **Format:** add `public string? AnchorState { get; set; }` to `SerializableLog`
  (additive, backward compatible).
- **Capture** (`Log.Done`): when `model.GameState == WaitingForRollForOrder` and
  the anchor is unset, store the serialized snapshot. (`Log` already works with
  `GameModel`, so reading `GameState` is in-layer.)
- **Persist/restore:** `GetSerializableLog` emits `AnchorState`;
  `FromSerializableLog`/`LoadFromSerializableLog` read it back.
- **Legacy salvage (ordering matters):** in the load paths, after rebuilding the
  DoneStack, if `AnchorState` is null, scan the rebuilt stack for the first
  `WaitingForRollForOrder` snapshot and set it — **before** `EnforceUndoLimit()`
  trims. This keeps replay working for pre-existing long games whose anchor only
  lives in the (about-to-be-trimmed) stack.
- **`ReplayGame`:** read `sourceLog.AnchorState` instead of scanning; if null
  (shouldn't happen after salvage), fall back to the existing scan. Everything
  downstream (reset GameId/name/`Random`, `Validate`, `UpdateGameHash`,
  `InitializeLoggingState`) is unchanged.

Cost: ~1 extra snapshot in the persisted blob (compressed, negligible). No reset
enumeration, no Regular/Expansion divergence — the anchor is an exact snapshot.

## Part D — TurnCount ← TotalRolls

Replace `TurnCount = …DoneCount` (and the one lowercase `turnCount`) with the
current GameModel's `RollModel.GameRollModel.TotalRolls` at the 6 sites
(GameApiController 683, 861, 1689, 1921, 2007, 2021) and
`DatabasePersistenceService.cs:153`. Already in the GameModel, never capped;
nothing reads `TurnCount` back. (Label may read "rolls"; cosmetic.)

## Part E — ReplayableRandom

- Each snapshot carries `Random` (`Seed` + `Iterations`); deserialize rebuilds
  `_rng`. Undo/redo within the window restore RNG deterministically; bounding only
  drops old snapshots.
- The anchor snapshot's `Random` is reset to fresh in `ReplayGame` (unchanged).
- **Trace (review Finding 6):** emit `TotalRolls` in the `[PERF-SAVE]` line
  (alongside `turns`) so the perf harness keeps a real game-length axis once the
  stack caps at 26.
- Guard: the existing `ReplayTests` message-replay determinism suite must pass.

## Compression — kept, and measured to be the better choice

Compression stays (`LogConstants.CompressSaves = true`). On a bounded ≤26-snapshot
log it is O(1), and a no-compression experiment (`CompressSaves = false`, with a
format-tolerant `JsonHelper.Decompress`) proved compression is **faster and
smaller**, not just smaller — see Results. Reads are tolerant of both compressed
and plain-JSON payloads, so the flag can be flipped safely and legacy saves load
unchanged.

## Implementation notes (as built)

- **Synchronous save:** `LogGameModel` stays synchronous and blocks on the (now
  cheap, bounded) `SaveAsync` via `GetAwaiter().GetResult()`. ASP.NET Core has no
  `SynchronizationContext`, so this does not deadlock. Chosen over an async ripple
  through ~15 handlers — identical observable behavior, zero caller churn. The
  background coalescing path (`RequestSave`/`RunSaveLoopAsync`) was removed.
- **Anchor propagation bug found & fixed:** `GameStateMachine.GetSerializableLog()`
  rebuilt the `SerializableLog` field-by-field and dropped `AnchorState`; now
  copied. (This was the root cause of replay failing for long games.)

## Affected code (as built)

| File | Change |
|------|--------|
| `Catan3.Shared/Utility/Log.cs` | Eviction (`EnforceUndoLimit`); anchor capture + load-salvage; removed async-save machinery; `LogConstants` (`MaxUndoDepth`, `CompressSaves`); sync save |
| `Catan3.Shared/Utility/JsonHelper.cs` | `Decompress` tolerant of plain-JSON payloads |
| `Catan3.Shared/Interfaces/IGameLog.cs` | `SerializableLog.AnchorState` (additive); removed `RequestSave` |
| `Catan3.Shared/GameLogic/GameStateMachine.cs` | sync save in `LogGameModel`; **`GetSerializableLog` now copies `AnchorState`** |
| `Catan3.GameService/Controllers/GameApiController.cs` | `ReplayGame` uses `AnchorState`; `TurnCount` ← `TotalRolls` (6 sites) |
| `Catan3.GameService/Services/DatabasePersistenceService.cs` | `TurnCount` ← `TotalRolls` |
| `Tests/Shared/LogUndoLimitTests.cs` (new) | 11 tests: eviction, undo window, act-after-undo, never-evict-current, full-window, load trim + redo preserved, RNG restore, anchor capture + legacy salvage |
| `Tests/GameService/ReplayTests/ReplayEndpointTests.cs` | assertion updated for `TurnCount`←`TotalRolls` |

## Results (measured 2026-06-23, `recording play 'Simulated Regular Game'`)

**Before — unbounded + compressed (the bug):** save cost grows with game length.

| turns | serialize | jsonSize | stored |
|------:|----------:|---------:|-------:|
| 2     | 1ms  | 77 KB    | 2 KB  |
| 28    | 2ms  | 1,084 KB | 8 KB  |
| 52    | 6ms  | 2,021 KB | 12 KB |
| 76    | 10ms | 2,981 KB | 16 KB |
| 102   | 10ms | 4,090 KB | 20 KB |

→ ~38 KB/turn; ~10 MB at 200+ turns; per-action save latency climbs (50–900ms on
Azure shared CPU per #155).

**After — bounded (cap 26) + compressed (shipped):** flat regardless of length.

| metric | value @ cap |
|---|---|
| turns | 26 (constant) |
| serialize | 2ms |
| compress | 2ms |
| db write | 15ms |
| total | 19ms |
| jsonSize | ~1.1 MB |
| stored | **7 KB** |

**Measured alternative — bounded + uncompressed (rejected):**

| metric | value @ cap |
|---|---|
| serialize/compress | 0ms |
| db write | **60ms** |
| total | **60ms** |
| stored | **1,136 KB** |

**Conclusion:** on a bounded stack, compression is **~3× faster** (19ms vs 60ms —
the 7 KB blob writes far faster than 1.1 MB) **and 160× smaller**, for ~2ms of
compress CPU. Keep compression.

## Verification (all passing)

- `pwsh ./catan.ps1 test` green — Shared 95/95 (incl. 11 new), GameService passing,
  TypeScript passing.
- Perf harness confirms flat `serialize`/`jsonSize` across game length.
- Replay-after-cap works (long legacy seed game replays via salvaged anchor).

## Status: implemented on `epic/197-bound-undo-depth`
