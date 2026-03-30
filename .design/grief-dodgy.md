# Grief Dodgy Feature

**Last verified:** January 30, 2026

## Overview

Grief Dodgy is a house rule that adds humorous animations when the
robber moves, specifically targeting the player "Dodgy" (player ID
`Dodgy-001`). When enabled, robber movements include tile flipping,
fake-out animations, and celebrations.

## Configuration

**Property:** `HouseRules.GriefDodgy` (boolean)

**File:** `Catan3.Shared/Models/HouseRules.cs`

**Known issue:** Defaults to `true` in the C# model. The design doc
specifies it should default to `false` to avoid unwanted effects
when creating new games. This bug is documented but not yet fixed.

## Feature Components

### 1. Tile Flip Animation

When the game enters `MustMoveRobber` state and GriefDodgy is
enabled:

- All tiles flip face-down (showing water pattern back) except
  tiles where Dodgy is the **sole** building owner
- Tiles stay flipped for ~5 seconds, then flip back
- Animation: SVG `scaleX()` transform (6 seconds total: 0.25s flip
  in, 5s hold, 0.25s flip out)

**Logic:** A tile flips if Dodgy has no buildings on it OR if other
players also have buildings on it. Only tiles exclusively owned by
Dodgy stay face-up.

### 2. Robber Fake-Out Animation

When the robber target is NOT Dodgy:

1. Robber first animates to Dodgy's "best tile" (highest stars with
   Dodgy's buildings) -- 1.25 seconds
2. Holds at fake position -- 1 second pause
3. Animates to actual destination -- 1.25 seconds

**Server-side:** `GameStateMachine.CalculateGriefDodgyFakeOut()`
calculates the fake-out coordinates and stores them in
`RobberModel.FakeOutCoordinates`.

**Client-side state machine:**

```
enum FakeOutPhase { None, AnimatingToFake, HoldingAtFake, AnimatingToFinal }
```

### 3. Celebration Animation

When Dodgy IS selected as the robber target:

- Particle/burst animation plays for 4.5 seconds
- CSS animation class: `.grief-celebration`

## Server Implementation

**File:** `GameStateMachine.cs`

| Method | Lines | Purpose |
|--------|-------|---------|
| `CalculateGriefDodgyFakeOut()` | 1862-1887 | Find Dodgy's best tile for fake-out |
| `MoveRobber()` | 1851 | Sets `FakeOutCoordinates` on `RobberModel` |
| Clearing logic | 995-996, 1216-1217 | Clears coordinates on roll/next |

**RobberModel properties:**

```typescript
interface RobberModel {
    coordinates: HexCoordinates;
    previousCoordinates: HexCoordinates;
    fakeOutCoordinates: HexCoordinates;  // Dodgy's best tile
    movedBy: string;
    targeted: string;
    resourcesStolen: number;
}
```

## Implementation Status

| Component | Blazor | React |
|-----------|--------|-------|
| HouseRules property | Implemented | TypeGen generated |
| Tile flip animation | Implemented | **Not implemented** |
| Robber fake-out | Implemented | **Not implemented** |
| Celebration animation | Implemented | **Not implemented** |
| Dodgy detection | Implemented | **Not implemented** |
| Animation guards | Implemented | **Not implemented** |
| Settings UI | Implemented | **Not implemented** |

### Blazor Files

| File | Component |
|------|-----------|
| `WebUI/Components/Board/RobberLayer.razor` | Fake-out state machine (120+ lines) |
| `WebUI/Components/Board/BaseLayer.razor` | Tile flip logic |
| `WebUI/Pages/Game.razor` | Celebration trigger, flip calculation |
| `WebUI/Services/Rendering/TileSvgRenderer.cs` | `isFlipped` rendering |
| `WebUI/wwwroot/css/app.css` | All animation CSS |

### React Status

React has the `RobberModel` TypeScript types (including
`fakeOutCoordinates`) but no components consume the GriefDodgy
data. The `RobberTargetMenu` handles basic targeting without
Dodgy-specific logic. `GameTile` has gold tile flip animation
but not GriefDodgy tile flipping.

## Bug Fixes (Blazor)

Three bugs documented in `grief-dodgy-design.md`:

### 1. Robber Animation Re-triggers

**Problem:** Animation replayed on every `GameModel` update, even
when robber didn't move.

**Fix:** Track `_lastAnimatedFromCoords` and `_lastAnimatedToCoords`
in `RobberLayer.razor`. Skip animation if coordinates match.

**Status:** Fixed in Blazor.

### 2. Double Tile Flip

**Problem:** Overlapping timers caused tiles to flip twice.

**Fix:** `_flipAnimationCompleted` flag prevents re-triggering while
animation is in progress.

**Status:** Fixed in Blazor.

### 3. HouseRules Default

**Problem:** `GriefDodgy` defaults to `true`, causing unwanted
effects when creating new games with "Use House Rules" checked.

**Fix:** Design says to explicitly set `GriefDodgy = false` in
`NewGame.razor` fallback.

**Status:** **Not fixed.** `HouseRules.cs` still defaults to `true`.

## Dodgy Player ID

The constant `"Dodgy-001"` is hardcoded in:

- `GameStateMachine.cs` (line 1864)
- `Game.razor` (line 755)
- `RobberLayer.razor` (line 96)
- `DatabaseSeeder.cs` (line 107) -- seeded as default player
- Test data files (`Expansion.catan_test`, `Regular.catan_test`)
