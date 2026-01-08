# GriefDodgy Feature Bug Fixes

## Overview

The GriefDodgy feature has three issues that need to be addressed:

1. Robber animation runs when GameModel updates even if robber didn't move
2. "Flip and highlight Dodgy tiles" animation runs twice when enabled
3. Client acts like GriefDodgy is enabled when HouseRules selected for new game, even if not set

## Issue Analysis

### Issue 1: Robber Animation Re-triggers

**Root Cause**: The client-side check in `RobberLayer.razor` compares `PreviousCoordinates` vs `RobberCoordinates`, but animations may be re-triggered on subsequent GameModel updates if `_animationPending` state isn't properly guarded.

**Solution**: Add fields to track the last animated from/to coordinates and skip animation if it's a re-render of the same movement.

### Issue 2: Double Flip Animation

**Root Cause**: Multiple triggers:

1. `StartFlipAnimationTimer()` can create overlapping timers
2. `GriefDodgyFlippedTiles` is recalculated on every render
3. `OnGameStateUpdated` fires multiple times during state transitions

**Solution**: Add a `_flipAnimationRunning` flag to prevent re-starting animation while one is already in progress.

### Issue 3: HouseRules Initialization

**Root Cause**: `GriefDodgy` defaults to `true` in `HouseRules.cs`. When creating a new game with the "Use House Rules" checkbox checked but no saved settings, the fallback creates a HouseRules object with only `GoldTiles` set, leaving `GriefDodgy` at its default value of `true`.

**Location**: `WebUI/Pages/NewGame.razor` (lines 230-244)

```csharp
// Current problematic code:
houseRules ??= new HouseRules
{
    GoldTiles = SelectedGameType == GameType.Expansion ? 2 : 1
};
// GriefDodgy defaults to true!
```

**Solution**: Explicitly set `GriefDodgy = false` in the fallback HouseRules.

## Implementation Plan

### Step 1: Fix Robber Animation Guard

**File**: `WebUI/Components/Board/RobberLayer.razor`

```csharp
// Add fields
private HexCoordinates? _lastAnimatedFromCoords;
private HexCoordinates? _lastAnimatedToCoords;

// In OnParametersSet(), before setting _animationPending:
if (_lastAnimatedFromCoords?.Equals(prevCoords) == true &&
    _lastAnimatedToCoords?.Equals(RobberCoordinates) == true)
{
    return; // Already animated this movement
}

// When animation starts:
_lastAnimatedFromCoords = prevCoords;
_lastAnimatedToCoords = RobberCoordinates;
```

### Step 2: Fix Double Flip Animation

**File**: `WebUI/Pages/Game.razor`

```csharp
// Add field
private bool _flipAnimationRunning = false;

// In GriefDodgyFlippedTiles property:
if (_flipAnimationRunning)
{
    return _cachedFlippedTiles ?? new HashSet<HexCoordinates>();
}

// In StartFlipAnimationTimer():
if (_flipAnimationRunning) return;
_flipAnimationRunning = true;
// ... existing timer logic ...
// In timer callback:
_flipAnimationRunning = false;
```

### Step 3: Fix HouseRules Initialization

**File**: `WebUI/Pages/NewGame.razor`

```csharp
houseRules ??= new HouseRules
{
    GoldTiles = SelectedGameType == GameType.Expansion ? 2 : 1,
    GriefDodgy = false,  // Default to OFF unless explicitly saved
    WallsProtectCities = true,
    KnightMovesBaronBeforeRoll = true,
    HideBaronBeforeInvasion = false,
    KnightMovesRobberBeforeRoll = false,
    HideRobberBeforeInvasion = false,
    SupplementalMinPlayers = 5
};
```

### Step 4: Verify PreviousCoordinates Clearing

**File**: `Catan3.Shared/GameLogic/GameStateMachine.cs`

Ensure `PreviousCoordinates` is cleared in all appropriate places:

- Line 902: In `OnRoll()` - already done
- Line 1123: In `NextState()` - already done
- When loading saved games
- When resetting games

## Critical Files

| File | Changes |
|------|---------|
| `WebUI/Components/Board/RobberLayer.razor` | Add animation guard |
| `WebUI/Pages/Game.razor` | Add flip animation running flag |
| `WebUI/Pages/NewGame.razor` | Fix fallback HouseRules |
| `Catan3.Shared/GameLogic/GameStateMachine.cs` | Verify clearing |

## Testing Plan

1. **Animation test**: Move robber, verify animation plays once. Wait for other GameModel updates, verify animation doesn't replay.
2. **Double flip test**: Enter MustMoveRobber state, verify tiles flip only once.
3. **HouseRules test**:
   - Clear localStorage HouseRules
   - Create new game with "Use House Rules" checked
   - Enter MustMoveRobber state
   - Verify tiles do NOT flip (GriefDodgy should be false)
