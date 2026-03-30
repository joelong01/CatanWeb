# Long Game Performance Findings

**Date:** 2026-03-30
**Branch:** longgame-perf
**Test game:** 222 turns, 3 players, Regular

## Problem

Games with 150-200+ moves show noticeable UI slowdown.

## Root Cause

`AsyncCommandProcessor.ProcessAsync` was saving the full game log to CosmosDB
after **every action**, synchronously in the action path. The save requires:

1. `GetSerializableLog()` — iterate all DoneStack entries
2. `JsonHelper.Serialize()` — serialize the entire log to JSON
3. `JsonHelper.Compress()` — compress the JSON
4. `gamePersistence.SaveAsync()` — write to CosmosDB

At 200+ turns, the serializable log is ~10MB of JSON. Serializing + compressing
this **on every action** took 30-50ms, blocking the client response path via
`Task.WhenAll`.

## Fix: Background Save with Coalescing

Moved `SaveGameToDatabaseAsync` out of the `Task.WhenAll` await path into a
fire-and-forget background queue. Saves are coalesced — if multiple actions
arrive while a save is in progress, only the latest state is saved.

## Measurements at Turn 220-222

### Fast Path (user-facing)

| Action | Logic | Notify | Total |
|--------|-------|--------|-------|
| RollMessage | 5ms | 0ms | **5ms** |
| PurchaseMessage | 4ms | 0ms | **5ms** |
| MoveRobberMessage | 5ms | 0ms | **5ms** |

### Background Save (not blocking user)

| Turn | GetLog | Serialize | Compress | DB Write | Total | JSON Size | Compressed |
|------|--------|-----------|----------|----------|-------|-----------|------------|
| 220 | 0ms | 9ms | 10ms | 9ms | 29ms | 9,695 KB | 44 KB |
| 221 | 0ms | 14ms | 15ms | 11ms | 42ms | 9,746 KB | 42 KB |
| 222 | 0ms | 11ms | 16ms | 9ms | 36ms | 9,796 KB | 42 KB |

### Key Numbers

- **User-facing latency:** 5ms per action (constant, independent of game length)
- **Background save:** ~35ms at turn 222 (O(N) but not blocking user)
- **Log size growth:** ~44KB per turn (each turn adds a full GameModel snapshot as JSON string)
- **Compression ratio:** 99.6% (9.7MB → 42KB)
- **GetSerializableLog:** 0ms (DoneStack holds strings, no conversion needed)

### Replay Benchmark (105 actions)

| Metric | Before (save per action) | After (background save) |
|--------|--------------------------|------------------------|
| Total replay time | 562ms | 440ms |
| Average per action | ~5ms | ~4ms |
| Improvement | — | 22% |

## Architecture

```text
Action Path (fast):               Background (async):
  Client POST                       EnqueueSave()
    → ExecuteGameLogic (5ms)           → ConcurrentDictionary (coalesce)
    → SignalR Notify (0ms)             → ProcessPendingSavesAsync
    → Return to client                   → GetSerializableLog (0ms)
                                         → Serialize (11ms)
                                         → Compress (16ms)
                                         → DB Write (9ms)
```

## Final Architecture: Log Owns Persistence

The `AsyncCommandProcessor` no longer saves to the database. All persistence
goes through `Log.RequestSave()` which is called by `GameStateMachine.LogGameModel()`
after each action. This is the **only** persistence path for game state.

**Separation of concerns:**

- `GameStateMachine` — game logic, calls `Log.Done()` then `Log.RequestSave()`
- `Log<T>` — state management (undo/redo stacks) AND persistence (coalesced saves)
- `AsyncCommandProcessor` — command dispatch + SignalR notification only
- `IPersistenceService` — database abstraction (Log calls this)

**Coalescing:** `Log.RequestSave()` uses `Interlocked` flags. If a save is in progress
when a new request arrives, the request is noted and the save loop runs again with
the latest state. Rapid actions produce at most one save per save-cycle duration (~35ms).

## Save Path Audit

All persistence of game state was audited. Two categories:

**Gameplay saves (must use Log.RequestSave only):**

All gameplay actions (roll, purchase, build, shuffle, balance, declare winner, etc.)
go through `GameStateMachine.Handle*Async()` → `LogGameModel()` → `Log.RequestSave()`.
No other gameplay code path saves. The following redundant saves were removed:

- `GameApiController.SaveGameToDatabase` — deleted (third copy of serialize+compress)
- `GameApiController.ProcessGameActionResult` — deleted (called SaveGameToDatabase + broadcast)
- `PUT game/{gameId}/houserules` — removed redundant save, kept broadcast
- `POST game/{gameId}/shuffle` — removed redundant save, kept broadcast
- `POST game/{gameId}/winner` — removed redundant save

**Non-gameplay saves (legitimate, use _gamePersistence directly):**

These create or modify games outside the normal action flow:

| Endpoint | Purpose | Why direct save is correct |
|----------|---------|---------------------------|
| `POST game/{gameId}/replay` | Copy game for replay | New game, not in any Log yet |
| `PATCH game/{gameId}/rename` | Rename game | Modifies serialized log directly |
| Copy game path | Duplicate game | New game, separate log |
| Import game path | Restore from file | New game, separate log |

## Note: HandlePersistGameAsync

`GameStateMachine.HandlePersistGameAsync` provides an explicit save path for
user-initiated Save/SaveAs operations. This calls `Log.SaveAsync()` directly
(not `RequestSave`), which is correct — explicit saves should be synchronous
and guaranteed. This is the only other code that triggers persistence, and it's
user-initiated (not per-action).

## Remaining Items

- **Linear growth:** At turn 500, serialize+compress will be ~70ms per save. Still
  background but wasteful. Could debounce saves (save every N seconds).
- **Compression is worth it:** 9.7MB → 42KB (232x). At 400 RU/s provisioned CosmosDB,
  a 250-move game uses ~55 RU/min. Could support ~430 concurrent games.

## CosmosDB Cost Estimate

- Per save: ~10 RUs (42KB compressed document write)
- Per game (250 moves): ~2,500 RUs total over 45 minutes
- At 400 RU/s: ~430 concurrent games before hitting limit
