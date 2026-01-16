# Allocation Phase Rendering

## Overview

This document describes how buildings and roads are rendered during the allocation phase
(AllocateResourceForward, AllocateResourceReverse) in the WebUI.

## Key Files

| File | Purpose |
|------|---------|
| `WebUI/Components/Board/BuildingOverlay.razor` | Renders building spots (settlements, cities, buildable) |
| `WebUI/Components/Board/BuildingOverlay.razor.css` | Styles for building spots including hidden state |
| `WebUI/Components/Board/RoadOverlay.razor` | Renders roads (owned, buildable) |
| `WebUI/Components/Board/RoadOverlay.razor.css` | Styles for roads |
| `WebUI/Components/Board/BoardMeasurement.razor` | Contains the star slider control |
| `Catan3.Shared/GameLogic/GameStateMachine.cs` | `MarkBuildableRoads()` and `MarkBuildableBuildings()` |

## Game States

```
PickingBoard          - Board evaluation, stars shown, nothing buildable
AllocateResourceForward - First allocation pass (players place settlement + road)
AllocateResourceReverse - Second allocation pass (reverse order)
WaitingForRoll        - Normal gameplay begins
```

## Stars vs Build Index

| Item | Stars | Build Index |
|------|-------|-------------|
| Building spot | Sum of adjacent tile probabilities (0-15) | Sequential number (1, 2, 3...) during gameplay |
| Road | N/A | Sequential number during gameplay |

**Star Calculation** (`TileModel.Stars`):

- Roll 6 or 8 → 5 stars (highest probability)
- Roll 5 or 9 → 4 stars
- Roll 4 or 10 → 3 stars
- Roll 3 or 11 → 2 stars
- Roll 2 or 12 → 1 star
- Roll 7 (robber) → 0 stars

## Slider Control

The star slider (`BoardMeasurement.razor`) controls `ShownStars` (0-14).

**Behavior by Game State:**

| State | Spots >= ShownStars | Spots < ShownStars |
|-------|---------------------|---------------------|
| PickingBoard | Visible with stars | Not rendered |
| Allocation | Visible with stars | Hidden (opacity: 0), revealed on hover, clickable |
| Normal Gameplay | Visible with build index | Visible with build index (no hiding) |

**Key Point:** During allocation, ALL buildable spots are in the DOM and clickable.
The slider just controls default visibility - hover reveals hidden spots.

## Building Rendering Logic

`BuildingOverlay.GetBuildableSpots()` determines what to show:

```csharp
// During PickingBoard
if (isPickingBoard)
{
    shouldShow = spotStars >= ShownStars;  // Only render if meets threshold
    isBuildable = false;  // Can't click during board picking
}

// During allocation (PickingResources phase)
else if (hasSettlementEntitlement && building.BuildingState == BuildingState.PossibleSettlement)
{
    shouldShow = true;   // Always in DOM
    isBuildable = true;  // Always clickable

    if (isPickingResources)
    {
        // Slider controls default visibility, NOT clickability
        // Spots >= ShownStars: visible (opacity: 1)
        // Spots < ShownStars: hidden (opacity: 0), but hover reveals
        isHidden = spotStars < ShownStars;
    }
    else
    {
        // Normal gameplay: show build index, no hiding
        buildIndex = settlementIndex.ToString();
        isHidden = false;
    }
}
```

## Road Rendering Logic

`RoadOverlay.GetVisibleRoads()`:

```csharp
return Roads.Where(r => r.OwnerId != null || r.RoadState == RoadState.Buildable);
```

Roads are visible if:

- Owned by any player, OR
- Marked as `RoadState.Buildable` by game logic

**No slider filtering on roads** - they show if buildable.

## CSS Classes

### Building Spots

| Class | Opacity | Pointer Events | Use Case |
|-------|---------|----------------|----------|
| `.building-spot-stars` | 0.8 | auto | PickingBoard evaluation |
| `.building-spot-buildable` | 1.0 | auto | Clickable buildable spot |
| `.building-spot-hidden` | 0 (1 on hover) | auto | Was used for slider hiding |
| `.building-spot-indexed` | 1.0 | auto | Shows build index number |

### Roads

| Class | Opacity | Use Case |
|-------|---------|----------|
| `.road-owned` | 1.0 | Player's built road |
| `.road-buildable` | 0.5 | Clickable buildable position |

## MarkBuildableRoads Logic

In `GameStateMachine.cs`:

```csharp
// During allocation (PickingResources phase):
// - Only roads adjacent to player's settlements WITH NO existing roads are buildable
// - This enforces the Catan rule: during setup, place 1 road per settlement

if (gameModel.Phase() == GamePhase.PickingResources)
{
    var ownedRoads = gameModel.AdjacentRoads(building.BuildingKey)
        .Where(r => r.OwnerId == gameModel.CurrentPlayerId).ToList();
    if (ownedRoads.Count == 0)  // Settlement has no road yet
    {
        buildableRoads.AddRange(gameModel.AdjacentRoads(building.BuildingKey));
    }
}
```

## Click Handlers

### Building Click

```csharp
// BuildingOverlay.razor
private async Task OnSpotClick(BuildingSpot spot)
{
    if (spot.IsBuildable && OnBuildingClick.HasDelegate)
    {
        await OnBuildingClick.InvokeAsync(spot.Building.BuildingKey);
    }
}
```

### Road Click

```csharp
// RoadOverlay.razor
private async Task HandleRoadClick(RoadModel road)
{
    if (road.RoadState == RoadState.Buildable && OnRoadClick.HasDelegate)
    {
        await OnRoadClick.InvokeAsync(road.RoadKey);
    }
}
```

## Common Issues

1. **Buildings not clickable during allocation**
   - Verify `isBuildable = true` for buildable spots
   - Verify CSS `.building-spot-hidden` has `pointer-events: auto`
   - Hidden spots should still receive mouse events and reveal on hover

2. **Roads not showing as buildable**
   - Check `MarkBuildableRoads()` logic in GameStateMachine
   - During allocation: only roads next to settlements with NO existing roads

3. **Hidden spots not revealing on hover**
   - Check CSS: `.building-spot-hidden:hover { opacity: 1 }`
   - Ensure `pointer-events: auto` is set (not `none`)

4. **Slider behavior during allocation**
   - Slider controls DEFAULT visibility, not clickability
   - Spots >= threshold: always visible
   - Spots < threshold: hidden but hoverable/clickable
   - This lets players quickly see high-value spots while still accessing all options

5. **0-star spots (desert edge, harbor access)**
   - During PickingBoard: not rendered (no production value to evaluate)
   - During allocation/gameplay: rendered if buildable (player may want harbor access)
   - Fixed by: `if (spotStars <= 0 && isPickingBoard) continue;`

6. **Tile indexes during allocation**
   - Show when player has Settlement entitlement (placing building)
     - Helps players communicate: "build on tile 3" or "upper right of tile 5"
   - Hide when player has Road entitlement (placing road)
     - Road indexes on the roads themselves are sufficient
   - Controlled by `ShouldShowTileIndexes` in `BoardContainer.razor`
   - CSS class `show-tile-indexes` toggles visibility
