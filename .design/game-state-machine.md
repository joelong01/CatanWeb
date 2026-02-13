# GameStateMachine Architecture

**Status:** Reference
**Date:** 2026-02-10

## Summary

Comprehensive reference for the `GameStateMachine` class (~2460 lines) in
`Catan3.Shared/GameLogic/GameStateMachine.cs`. Documents the current
architecture, state flow, rule logic, and extensibility boundaries to inform
the game engine decomposition design.

## Class Structure

### Dependencies

```csharp
public GameStateMachine(
    IGameLog gameLog,           // Undo/redo snapshot stack, serialization
    ICatanDebugTrace gameLogger, // Debug logging
    IPersistenceService persistenceService) // Save/load .catan files
```

Optional: `IGameRecorder? _recorder` for recording `.catan_test` replay files.

### Public Entry Point

`GameModel GetCurrentState()` — returns `_gameLog.CurrentState()`, the
canonical game state.

## IGameStateMachine Interface

File: `Catan3.Shared/Interfaces/IGameStateMachine.cs`

18 async handler methods:

| Method | Purpose |
|--------|---------|
| `HandleUndoAsync` | Undo last action |
| `HandleRedoAsync` | Redo undone action |
| `HandleNextAsync` | Advance state machine |
| `HandleRollAsync` | Roll dice |
| `HandlePurchaseAsync` | Buy entitlement (road/settlement/city/devcard/soldier) |
| `HandleShuffleAsync` | Shuffle board |
| `HandleBuildingUpgradeAsync` | Place/upgrade building |
| `HandleRoadPurchaseAsync` | Place road |
| `HandleMoveRobberAsync` | Move robber to new tile |
| `HandleSetPlayerOrderAsync` | Set turn order |
| `HandleParticipatingInSupplementalAsync` | Opt in/out of supplemental build |
| `HandleGoFirstAsync` | Choose who goes first |
| `HandleBalanceBoardAsync` | Balance board (no adjacent 6/8) |
| `HandleLoadCompressedLogAsync` | Load saved game |
| `HandleStartRecordingAsync` | Begin recording replay |
| `HandleStopRecordingAsync` | Stop recording replay |
| `HandlePersistGameAsync` | Save game to disk |
| `HandleEndGameAsync` | End game |

All handlers follow the same pattern:

1. Copy current state from `_gameLog`
2. Trace the action
3. Record the action (if recorder active)
4. Call private implementation method
5. Call `LogGameModel()` to commit state
6. Return updated `GameModel`

## GameState Enum

File: `Catan3.Shared/Models/GameEnums.cs` (line 34)

32 states total. States in **bold** are actively implemented; others are
defined but stub-only in `NextState()`.

### Setup Phase

- **Uninitialized** / **WaitingForNewGame**
- **WaitingForPlayers**
- **PickingBoard**
- **WaitingForRollForOrder** / **FinishedRollOrder**

### Resource Allocation Phase

- **BeginResourceAllocation**
- **AllocateResourceForward** / **AllocateResourceReverse**
- **DoneResourceAllocation**

### Main Game Loop

- **WaitingForRoll** — controlled by HandleRollAsync, not Next
- **WaitingForNext** — player's turn, can purchase/build
- **MustMoveRobber** — after rolling 7 or playing soldier
- **PickSupplementalPlayers** / **Supplemental**
- **GameOver**

### Unimplemented States (Stubs)

TooManyCards, MustDestroyCity, PickingRandomGoldTiles, HandlePirates,
DoneDestroyingCities, MustMoveMerchant, DestroyRoad, SwapNumbers,
PickDeserter, PlaceDeserterKnight, DoneWithDeserter, UpgradeToMetro,
TestCheckpoint, DisplaceVictimKnight, DisplaceKnightMoveVictim, ClickOnKnight

## State Flow

```text
PickingBoard
  → WaitingForRollForOrder
  → FinishedRollOrder
  → BeginResourceAllocation
  → AllocateResourceForward ↔ AllocateResourceReverse
  → DoneResourceAllocation
  → WaitingForRoll          ← start of each turn
  → [HandleRollAsync]
    ├─ normal roll → WaitingForNext
    └─ rolled 7   → MustMoveRobber → WaitingForNext
  → [HandleNextAsync]
    ├─ supplemental? → PickSupplementalPlayers → Supplemental → ...
    └─ no           → advance player → WaitingForRoll
```

## NextState() Dispatcher

Line 1156. The core state transition logic. Giant switch on `GameState`:

### Key Transitions

| From State | To State | Condition |
|------------|----------|-----------|
| BeginResourceAllocation | AllocateResourceForward | Grant settlement + road |
| AllocateResourceForward | AllocateResourceForward | Next player (if not all placed) |
| AllocateResourceForward | AllocateResourceReverse | Last player placed (Score == 1) |
| AllocateResourceReverse | AllocateResourceReverse | Previous player |
| AllocateResourceReverse | DoneResourceAllocation | First player reached |
| DoneResourceAllocation | WaitingForRoll | Reset temp gold, start turns |
| WaitingForNext | PickSupplementalPlayers | If supplemental enabled + min players |
| WaitingForNext | WaitingForRoll | Advance player, start new turn |
| PickSupplementalPlayers | Supplemental | Found participating player |
| PickSupplementalPlayers | WaitingForRoll | No participants |
| Supplemental | Supplemental | Next participating player |
| Supplemental | WaitingForRoll | All participants done |

### Unimplemented Cases

Lines 1339-1372: All remaining states are `break` — no transition, no logic.
These are placeholders for future expansions (Cities & Knights states mostly).

## Handler Method Details

### OnRoll (line 989)

1. Store roll in `gameModel.RollModel.TurnRollModel`
2. Update global roll statistics (counts per number, total rolls)
3. Transition to `WaitingForNext`
4. Mark matching tiles as highlighted
5. Calculate resource distribution:
   - For each highlighted tile → find adjacent buildings
   - Determine resource type (handles temporarily-gold tiles, robber blocking)
   - Add resources to player inventories
6. If rolled 7: add `RolledSeven` entitlement, save `PreviousGameState`,
   transition to `MustMoveRobber`

### OnPurchase (line 764)

- **Soldier**: valid in WaitingForNext/WaitingForRoll only. Saves
  PreviousGameState, transitions to MustMoveRobber. Max one per turn.
- **DevCard**: goes directly to `SpentEntitlementsThisGame` (immediately spent).
- **Road/Settlement/City**: added to `UnspentEntitlements` (consumed on placement).

### MoveRobber (line 1811)

1. Update robber position and animation coordinates
2. Track who moved it and who was targeted
3. Consume entitlement (Soldier → return to PreviousGameState;
   RolledSeven → WaitingForNext)
4. GriefDodgy house rule: calculate fake-out coordinates

### BuildingUpgrade (line 1594)

State machine for building progression:

```text
PossibleSettlement → Settlement → City → Knight
```

- Settlement: consume entitlement, check for adjacent harbor
- City: consume City entitlement, net +1 city/-1 settlement
- Reverse allocation: grant resources from adjacent tiles

### RoadPurchase (line 1410)

Validate state, validate unspent Road entitlement, validate road is
Buildable + Unowned, set owner, consume entitlement.

## The LogGameModel Pipeline

Line 1490. **This is the critical integration point.** Every state change
flows through this method, which recalculates all derived state:

```csharp
private void LogGameModel(GameModel gameModel)
{
    UpdateScore(gameModel);                     // Scores, longest road, largest army
    UpdatePlayerStars(gameModel);               // Probability-weighted holdings
    MarkBuildableRoads(gameModel);              // Which roads can be built
    MarkBuildableBuildings(gameModel);          // Which buildings can be placed/upgraded
    SetActionFlags(gameModel);                  // Undo/Next/Roll enabled
    gameModel.ActionFlags.RedoEnabled = false;  // New state invalidates redo chain
    UpdatePurchaseUi(gameModel);                // Purchase button states
    SetPlaySoldierAccess(gameModel);            // Soldier availability
    SetDevCardAccess(gameModel);                // DevCard availability
    gameModel.UpdateGameHash();                 // State hash for validation
    _gameLog.Done(gameModel);                   // Commit to undo/redo stack
    Task.Run(() => _gameLog.SaveAsync());       // Async save to disk (fire-and-forget)
}
```

### Why This Matters for Extensibility

The `LogGameModel` pipeline is the **primary extensibility boundary**. This is
where game types actually differ:

- Seafarers needs `CalculateLongestRoute` (roads + ships) instead of
  `CalculateLongestRoad` (roads only)
- Seafarers needs `MarkBuildableShips` in addition to `MarkBuildableRoads`
- Island discovery scoring modifies `UpdateScore`
- Gold field resource choice modifies the distribution step

The state transitions (`NextState`) are mostly shared across game types. The
derived-state calculations are where the divergence happens.

## Score Calculation

Line 1687. `UpdateScore()`:

1. Call `CalculateLongestRoad(gameModel)` — sets `HasLongestRoad` per player
2. Count max soldiers across all players
3. For each player:
   - Count cities (2 VP each), settlements (1 VP each)
   - Determine Largest Army (3+ soldiers, max count wins, first-to-reach tiebreak)
   - Score = `cities*2 + settlements + longestRoad(2) + largestArmy(2) + vpCards`
4. Mark highest-scoring player(s)

## Longest Road Calculation

Line 2228. `CalculateLongestRoad()`:

### Main Entry

For each player: try starting from each owned road, DFS to find max length.
Award `HasLongestRoad` to player with 5+ roads and longest count (first-to-reach
tiebreak).

### Recursive DFS (line 2286)

```text
CalculateLongestRoad(gameModel, start, counted, blockedFork)
```

- **No adjacent roads**: return current count
- **Single adjacent**: continue path, recurse
- **Fork (>1 adjacent)**: try each branch with others blocked, return max

Ships are modeled as `RoadState.Ship` on the same `RoadModel` objects (the
`RoadState` enum already has `Ship` as a value). This means the existing
`CalculateLongestRoad` algorithm will automatically include ships in route
calculation without code changes. The only Seafarers modification needed is
pirate-blocking (skipping roads/ships on the pirate's hex).

## Player Stars Calculation

Line 1773. Each tile has a probability weight (6/8=5, 5/9=4, etc.). Sum across
all buildings: settlements 1x, cities 2x. Used for AI evaluation and
GriefDodgy targeting.

## Buildable Location Marking

### MarkBuildableRoads (line 2059)

- During purchase: adjacent to owned roads OR adjacent to buildings without roads
- During allocation: only adjacent to building with no adjacent owned roads
- Assigns BuildIndex for display ordering

### MarkBuildableBuildings (line 2129)

- Settlements: no adjacent owned buildings, must be adjacent to owned road (in
  purchase phase)
- Cities: upgrade owned settlements
- Updates purchase button enabled state

## Action Flags

`SetActionFlags()` (line 1091):

- `UndoEnabled` — based on game log's undo stack
- `NextEnabled` — `AllowNext()`: disabled in WaitingForRoll/MustMoveRobber, or
  if player has unspent entitlements
- `RollsEnabled` — only in WaitingForRoll state

## Undo/Redo System

Snapshot-based: each `LogGameModel()` stores a complete `GameModel` copy.
Undo pops from done stack to redo stack. Redo reverses.

**Undo/Redo do NOT call LogGameModel** — they restore directly from snapshots
to preserve the redo chain.

## Game Type Branching

**No game-type branching inside the state machine.** `HandleNewGameAsync`
receives an `IGameMetadata` parameter already selected by the caller. The
branching happens upstream:

- **GameApiController** (`GameApiController.cs:280-292`): maps `GameType` →
  `RegularBoardInfo.Default` or `ExpansionBoardInfo.Default`
- **Desktop** (`GameMessageService.cs`): same selection logic

Inside `GameStateMachine`, all logic is identical for Regular and Expansion.
The only differences come from metadata: board size, tile count, player
limits, harbor positions, supplemental phase configuration.

## House Rules Integration

Checked in specific places:

- `SupplementalMinPlayers` — whether supplemental phase activates
- `GoldTiles` — count of temporarily-gold tiles per turn
- `GriefDodgy` — fake-out robber animation for "Dodgy" player

## Recording/Replay System

`IGameRecorder` captures `IRecordedMessage` instances with:

- `ExpectedGameHash` — for state verification
- `ExpectedGameState` — for state verification
- Message-specific data (roll values, purchase type, coordinates, etc.)

15 recorded message types with polymorphic JSON serialization. The recording
system is orthogonal to game rules — it captures the public handler inputs and
expected state, not internal logic.

## GameMode on GameModel

`GameModel` already carries `SaveLifetimeStats` (a boolean controlling whether
stats are persisted) and `LoadGameModelMessage.IsTest` exists in the message
layer. Rather than scattered booleans, add a single `GameMode` enum to
`GameModel`:

```csharp
public enum GameMode
{
    Production,  // Normal gameplay — full stats, normal logging
    Staging,     // Staging deployment — stats to test partition, verbose logging
    Test         // Replay/unit tests — skip stats, extra validation logging
}
```

**Why on GameModel:**

- Already passed everywhere (state machine, database layer, client, recording)
- Serializes naturally — `.catan_test` files carry the mode, so loading one
  automatically puts you in test mode
- No new plumbing — check `gameModel.GameMode` wherever needed
- Replaces `SaveLifetimeStats` (Production saves stats, Test/Staging don't)

**Usage examples:**

- `LogGameModel`: verbose logging when `GameMode != Production`
- Database layer: skip lifetime stats when `GameMode == Test`
- Recording system: extra hash validation logging when `GameMode == Test`
- Client: show environment indicator when `GameMode == Staging`

**Not for game rules.** `GameMode` controls infrastructure side-effects (logging,
stats, DB access). Game rules are identical across modes — that's the whole
point of replay tests. `IGameRules` is for game-type variation; `GameMode` is
for environment variation.

## Key Design Observations

1. **No game-type branching in rules** — Regular and Expansion are identical
   except for board metadata.
2. **Mutable GameModel** — state machine modifies in place, snapshots for undo.
3. **LogGameModel is the integration point** — all derived state recalculated
   here.
4. **Many states pre-defined but unimplemented** — designed for future expansions.
5. **RoadState already has `Ship`** — enum value exists, logic doesn't.
6. **Entitlement enum has expansion values** — BuyKnight, UpgradeKnight, Wall,
   Bishop, Deserter, Inventor, Intrigue, Diplomat, Merchant, etc.
7. **Handler pattern is stable** — public async → private logic → LogGameModel.

## Extensibility Boundaries

Based on analysis, the natural extensibility boundaries are:

| Boundary | What Changes | Why |
|----------|-------------|-----|
| `LogGameModel` pipeline | Score calc, route calc, buildable marking | Game types differ in derived-state calculation |
| `NextState()` return value | State transition targets | New game types may add states |
| `IGameStateMachine` interface | New handler methods | New actions (ship movement, pirate, gold choice) |
| `IGameMetadata` | Board metadata | New tile types, islands, sea configuration |

What does NOT change per game type:

- The handler → LogGameModel pattern
- The undo/redo snapshot mechanism
- The recording/replay system
- The public handler calling convention
