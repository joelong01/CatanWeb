# Design Review: game-state-machine

**Design:** `.design/game-state-machine.md`
**Reviewed:** 2026-02-13
**Reviewer:** GitHub Copilot
**Stage:** Reference / Architecture doc

## Summary

The doc is an accurate, high-fidelity map of the current `GameStateMachine` implementation. Most claims match the code, including handler flow, `NextState` transitions, `LogGameModel` pipeline, and longest-road logic. A few minor mismatches (state count, branch points) should be corrected to avoid confusion. The proposed `GameMode`/`IGameRules` hooks are forward-looking and consistent with the code’s current seams but need explicit integration steps.

## Critical Issues

_None._

## Important Issues

### 1) GameState enum count is 32, not 33

**Section:** GameState Enum
**Issue:** Doc claims 33 states. Scripted count shows 32 enumerants.
**Recommendation:** Update the doc’s table to reflect 32 states and include `MustMoveMerchant` explicitly.

### 2) “Single branching point” claim is incomplete

**Section:** Game Type Branching
**Issue:** Doc states a single branch at `GameStateMachine` line ~876. In practice, branching happens in `Catan3.GameService/Controllers/GameApiController.cs:280-292` and `DesktopApp/GameState/GameMessageService.cs:570` when selecting `RegularBoardInfo` vs `ExpansionBoardInfo` before calling the state machine.
**Recommendation:** Clarify that branching happens upstream of `HandleNewGameAsync`; keep the claim scoped to the state machine logic itself.

### 3) Out-of-date sync note

**Section:** File header comment references `Catan3.GameService/Controllers/GameStateMachine.cs`
**Issue:** That file does not exist; the shared class is the single source.
**Recommendation:** Update doc (and header comment) to state that the shared class is the authoritative implementation used by GameService and Desktop.

## Suggestions

- **Document recording message count**: Doc says 15 recorded message types—did not verify. Consider listing the concrete types from `Catan3.Shared/Models/MessageObjects.cs` so future additions can be tracked.
- **Call out soldier purchase guard**: The code guards against multiple soldiers per turn in `SetPlaySoldierAccess` and `OnPurchase`; doc already hints at this, but a short note referencing `gameModel.CurrentPlayer().SpentEntitlementsThisTurn` would help.
- **Spell out `LogGameModel` side effects**: Mention that `LogGameModel` also disables redo (`ActionFlags.RedoEnabled = false`) and triggers async `_gameLog.SaveAsync()`.
- **Clarify `AllowNext`**: Document the explicit non-next states list (`WaitingForRoll`, `MustMoveRobber`) to make future additions safer.
- **Tie-breaking in Longest Road**: Note the “first-to-reach” tie-break implemented via `playerWithLongestRoad` to guide Seafarers changes.

## Questions

1. Do we want a guardrail test for state count (so enum additions update the doc automatically)?
2. Should `GameMode` proposal replace `SaveLifetimeStats` or coexist? Where would TypeGen changes land?
3. For `IGameRules` extraction: do we plan to inject via DI in GameService and via constructor in Desktop/CLI? A short plan in the doc would prevent divergence.

## Verification

### 1. Handler pattern (copy, trace, record, invoke, LogGameModel)

**Design says:** All handlers follow the six-step pattern.
**Actual code:** `Catan3.Shared/GameLogic/GameStateMachine.cs:44-140 (HandleUndoAsync, HandleRedoAsync, HandleNextAsync, etc.)`
**Status:** Verified

### 2. `IGameStateMachine` has 18 methods

**Design says:** 18 async handlers listed.
**Actual code:** `Catan3.Shared/Interfaces/IGameStateMachine.cs:1-80`
**Status:** Verified

### 3. `NextState()` transitions

**Design says:** Switch covers setup → allocation → main loop; stubs break for unimplemented states.
**Actual code:** `Catan3.Shared/GameLogic/GameStateMachine.cs:1156-1328`
**Status:** Verified

### 4. `LogGameModel` pipeline

**Design says:** UpdateScore, UpdatePlayerStars, MarkBuildableRoads/Buildings, SetActionFlags, UpdatePurchaseUi, SetPlaySoldierAccess, SetDevCardAccess, UpdateGameHash, _gameLog.Done.
**Actual code:** `Catan3.Shared/GameLogic/GameStateMachine.cs:1459-1510`
**Status:** Verified (note: sets `ActionFlags.RedoEnabled = false` and kicks off async Save)

### 5. `OnRoll` handling of 7s and resource distribution

**Design says:** Stores roll, updates stats, highlights tiles, distributes resources, sets `PreviousGameState`, transitions to `MustMoveRobber` for 7s.
**Actual code:** `Catan3.Shared/GameLogic/GameStateMachine.cs:1008-1070`
**Status:** Verified

### 6. `OnPurchase` soldier/dev-card behavior

**Design says:** Soldier allowed in WaitingForNext/WaitingForRoll, sets `PreviousGameState`, moves to `MustMoveRobber`; DevCard goes to `SpentEntitlementsThisGame` immediately.
**Actual code:** `Catan3.Shared/GameLogic/GameStateMachine.cs:736-820`
**Status:** Verified

### 7. Move robber logic

**Design says:** Updates robber position, fake-out coords, consumes entitlement, returns to `PreviousGameState` for Soldier; to `WaitingForNext` for RolledSeven.
**Actual code:** `Catan3.Shared/GameLogic/GameStateMachine.cs:1811-1868`
**Status:** Verified

### 8. UpdateScore / Largest Army / Longest Road

**Design says:** Score = cities*2 + settlements + longest road (2) + largest army (2) + VP cards; longest road DFS handles forks; ships share RoadState.
**Actual code:** `Catan3.Shared/GameLogic/GameStateMachine.cs:1660-1745 (UpdateScore)`, `Catan3.Shared/GameLogic/GameStateMachine.cs:2200-2335 (CalculateLongestRoad)`, `Catan3.Shared/Extensions/RoadModelExtensions.cs:13-70`
**Status:** Verified

### 9. GameState enum count

**Design says:** 33 states.
**Actual code:** `Catan3.Shared/Models/GameEnums.cs`; scripted count shows 32 entries (Python script run Feb 13, 2026).
**Status:** Incorrect (doc needs update)

### 10. Branching point location

**Design says:** Single branch at `GameStateMachine` line ~876.
**Actual code:** Branching occurs in `Catan3.GameService/Controllers/GameApiController.cs:280-292` and Desktop `GameMessageService.cs:570`; `HandleNewGameAsync` receives `IGameMetadata` already selected.
**Status:** Needs clarification

## Praise

- Excellent mapping of `LogGameModel` pipeline—the doc correctly identifies this as the extensibility seam for Seafarers/CK.
- State flow diagram matches the code and is handy for CLI/replay tests.
- Calling out `RoadState.Ship` and existing Entitlement enum values positions the project well for future expansions.

## Follow-Up Actions

- [ ] Update doc to 32-state count and list `MustMoveMerchant` explicitly.
- [ ] Clarify branching location and remove outdated sync note.
- [ ] Decide on `GameMode` plan (and TypeGen impact); document integration steps.
- [ ] Optionally list recorded message types to keep doc in sync with code.
