# Design: Declare-Winner Flow Analysis

## Overview

This document traces every step of the declare-winner path — from the React
button press to the final SignalR broadcast — and captures performance
bottlenecks and correctness issues found during the analysis.

---

## Step 1: React UI — Triggering the Request

**File:** `react-ui/components/game/panels/GameActionsPanel.tsx` (approximate)

The user presses "Winner!" which kicks off a 5-second celebration animation.
After the animation completes:

1. VP scoring runs synchronously to add any development-card victory points
   (`request.VictoryPoints` map).
2. `handleEndGame()` calls `proxy.declareWinner(gameId, winnerId, victoryPoints)`.
3. The proxy fires an HTTP `POST /api/game/{gameId}/winner` and awaits the
   response.
4. On success the proxy returns. The client does **not** update local state
   at this point — it waits for the SignalR `GameStateUpdated` broadcast.

**Performance note:** The 5-second animation is intentional UX; not a bug.

---

## Step 2: GameService Controller — `DeclareWinner`

**File:** `Catan3.GameService/Controllers/GameApiController.cs:477`

This is where the latency lives. The method executes these steps **in order**,
each blocking the next:

```text
1. Validate request / look up GameStateMachine
2. await UpdatePlayerLifetimeStats()   ← DB write, N players × FindAsync + SaveChangesAsync
3. await ArchiveCompletedGame()        ← DB write, compress log + SaveChangesAsync
4. HandleDeclareWinnerAsync()          ← synchronous (Task.FromResult), instant
5. TryRecordActionAsync()              ← lightweight, conditional
6. await ProcessGameActionResult()     ← LogGameModel (fire-and-forget) + SignalR broadcast
7. return Ok(...)
```

### Performance Bug — Blocking DB Writes Before Broadcast

**Root cause of the observed multi-second delay:**

`UpdatePlayerLifetimeStats` iterates every player, calling `FindAsync` for
each one (N individual DB round-trips), then calls `SaveChangesAsync()`:

```csharp
// GameApiController.cs:592
foreach (var player in gameModel.Players)
{
    var playerEntity = await _dbContext.Players.FindAsync(player.Id); // N awaits
    // ... compute stats ...
    playerEntity.Data = JsonHelper.Serialize(updatedProfile);
}
await _dbContext.SaveChangesAsync(); // blocks here
```

`ArchiveCompletedGame` then compresses the full game log (CPU work) and
calls `SaveChangesAsync()` again:

```csharp
// GameApiController.cs:737
_dbContext.CompletedGames.Add(completedGame);
await _dbContext.SaveChangesAsync(); // blocks here again
```

Both of these complete **before** `HandleDeclareWinnerAsync` is called and
**before** `ProcessGameActionResult` broadcasts to SignalR clients. In a
four-player game this is:

- 4 × `FindAsync` (SQLite round-trips)
- 1 × `SaveChangesAsync` (stats)
- 1 × JSON serialize + compress (CPU, potentially large)
- 1 × `SaveChangesAsync` (archive)

Total observed wall time: **2–5 seconds** depending on game log size and
SQLite lock contention.

### Bug — `UpdatePlayerLifetimeStats` Uses Pre-Winner State

`DeclareWinner` reads `currentState` (the live game model) **before** calling
`HandleDeclareWinnerAsync`. The stats snapshot is taken from this pre-winner
state. Any VP cards added via `request.VictoryPoints` are not yet reflected
in the player models when stats are saved. The final score in lifetime stats
may therefore be underreported by the number of secret VP cards.

Specifically:

- `CalculatePlayerScore` counts settlements and cities from the board — it
  does not include largest army / longest road bonuses or VP dev cards.
- The `score` passed to `AddGame` does not match the player's actual final
  score shown in the UI.

---

## Step 3: GameStateMachine — `HandleDeclareWinnerAsync`

**File:** `Catan3.Shared/GameLogic/GameStateMachine.cs`

```csharp
public Task<GameModel> HandleDeclareWinnerAsync(DeclareWinnerMessage msg)
{
    // ... applies VP card points to player scores
    // ... sets GameState = GameOver
    // ... updates HighestScore flag
    return Task.FromResult(currentModel);
}
```

This method is **synchronous** — it returns `Task.FromResult` with no I/O.
It contributes zero latency to the path. The `LogGameModel` call inside
`ProcessGameActionResult` uses `Task.Run` (fire-and-forget) for the actual
DB save, so that also does not block the broadcast.

**Correctness note:** `HandleDeclareWinnerAsync` applies `VictoryPoints` from
the request, adding them to each player's `Score`. This is the **first** time
VP card counts are applied. However, `UpdatePlayerLifetimeStats` was already
called with the old scores (see Bug above).

---

## Step 4: React — Receiving the Broadcast

**Files:** `react-ui/lib/hooks/useGameConnection.ts`, `react-ui/lib/stores/gameStoreHooks.ts`

The SignalR hub broadcasts `GameStateUpdated` to all connected clients.
The React client handles it in `useGameConnection`:

1. `reconcileGameModel(prev, next)` runs — preserves existing object references
   where content is unchanged to prevent unnecessary re-renders.
2. `setGameModel(reconciled)` triggers a Zustand state update (plain setter in
   `gameStore.ts`; reconciliation is in the hook, not the store).
3. Selectors in `gameStoreHooks.ts` use `arraysEqual` and custom equality
   functions to prevent components from re-rendering unless their specific
   slice of data changed.
4. `PlayersPanel` re-renders because the player list changed (GameOver state,
   updated scores, `ResourcesThisGame` values).

After the recent fix (`isGameOver` prop passed from `ScaledPlayersList`),
the panel correctly switches the resource source to `resourcesThisGame` and
displays the "Game:" label.

**No performance issues in this step.** The Zustand reconciliation and
selective re-rendering are well-implemented.

---

## Issues Summary

| # | Severity | Description | Location |
|---|----------|-------------|----------|
| 1 | **Perf** | Two blocking `SaveChangesAsync` calls before SignalR broadcast add 2–5s delay | `GameApiController.cs:545,553` |
| 2 | **Perf** | `UpdatePlayerLifetimeStats` issues N individual `FindAsync` calls (one per player) instead of a single batch query | `GameApiController.cs:596` |
| 3 | **Bug** | Lifetime stats snapshot taken before `HandleDeclareWinnerAsync`; VP card points not included in saved score | `GameApiController.cs:545,557` |
| 4 | **Bug** | `CalculatePlayerScore` counts only settlements/cities — omits largest army, longest road, VP dev card bonuses | `GameApiController.cs:694` |

---

## Recommended Fix

**Decouple the post-game bookkeeping from the broadcast path.**

Move `UpdatePlayerLifetimeStats` and `ArchiveCompletedGame` to run **after**
`ProcessGameActionResult` (which broadcasts to clients), and run them in a
fire-and-forget `Task.Run` or a background service so they do not block the
HTTP response:

```csharp
// Proposed order:
1. HandleDeclareWinnerAsync()           // instant
2. ProcessGameActionResult()            // broadcast immediately
3. return Ok(...)                       // HTTP response to caller

// Fire-and-forget after response:
_ = Task.Run(async () =>
{
    await UpdatePlayerLifetimeStats(updatedGameModel, request.WinnerId);
    await ArchiveCompletedGame(...);
});
```

**Additional fixes to consider together:**

- Batch the `FindAsync` calls in `UpdatePlayerLifetimeStats` into a single
  `Where(p => playerIds.Contains(p.Id)).ToListAsync()`.
- Pass `updatedGameModel` (post-winner state) instead of `currentState` to
  `UpdatePlayerLifetimeStats` so VP card scores are included.
- Fix `CalculatePlayerScore` to use `player.Score` (already computed by the
  state machine) rather than re-deriving it from the board.

---

## What Does NOT Need to Change

- `HandleDeclareWinnerAsync` — correct and fast.
- `GameStateMachine.LogGameModel` — fire-and-forget `Task.Run` for `_gameLog.SaveAsync()`;
  does not add latency.
- React store reconciliation and selector equality — well-implemented.
- The `ResourcesThisGame` display in `PlayersPanel` — fixed and working.

## Note on `ProcessGameActionResult`

This shared helper does `await SaveGameToDatabase(...)` **then** broadcasts. That ordering
is correct for all normal game actions (save before clients see the new state). For
`DeclareWinner` specifically, the controller now bypasses `ProcessGameActionResult` and
calls the two operations in reverse order — broadcast first, then save — so clients
see the winner immediately without waiting for log compression and the DB write.
