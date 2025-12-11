# Grief Dodgy House Rule Design

## Overview

A special house rule that adds fun animations targeting the player named "Dodgy" (James) when the
baron/robber is being moved. This is a client-side visual effect that does not affect game logic.

## Feature Requirements

When `GriefDodgy` house rule is enabled:

1. **Tile Flip Animation**: When game enters `MustMoveRobber` state, flip tiles face-down using a
   CSS 3D flip animation. Tiles stay flipped for 5 seconds, then flip back. Tiles to flip:
   - Any tile that does NOT have a building owned by Dodgy adjacent to it
   - Tiles where Dodgy shares with other players (more than one owner has adjacent buildings)
   - Only tiles where Dodgy is the SOLE owner remain face-up

2. **Celebration When Dodgy Targeted**: When Dodgy is selected as the robber target, play a
   fireworks/celebration animation.

3. **Fake-Out Animation**: When Dodgy is NOT targeted, animate the robber to Dodgy's tile with the
   most stars first, pause for 1 second, then animate to the actual destination.

## Implementation Plan

### 1. Add House Rule Property

**File:** `Catan3.Shared/Models/HouseRules.cs`

```csharp
/// <summary>
/// Enables special animations targeting "Dodgy" player during baron/robber moves.
/// </summary>
public bool GriefDodgy { get; set; } = false;
```

### 2. Add Setting to Settings UI

**File:** `WebUI/wwwroot/settings.json`

Add new setting in "House Rules" category:

```json
{
  "settingName": "GriefDodgy",
  "description": "Grief Dodgy mode",
  "inputType": "checkbox",
  "value": false,
  "defaultValue": false,
  "tooltip": "When enabled, adds special animations targeting Dodgy during robber moves",
  "category": "House Rules"
}
```

### 3. Dodgy Player Detection

Match by player ID `"Dodgy-001"` exactly. This is the ID used in the database seeder.

### 4. Tile Flip Animation (Requirement 1)

**Files to modify:**

- `WebUI/Components/Board/TileLayer.razor` - Add flip animation support
- `WebUI/wwwroot/css/app.css` - Add CSS 3D flip transitions

**Approach:**

- Add parameter `FlippedTiles` (HashSet of HexCoordinates) to TileLayer
- When a tile is in `FlippedTiles`, apply CSS class `tile-flipped`
- CSS handles the 3D flip animation with perspective

**Logic for determining which tiles to flip:**

```csharp
private HashSet<HexCoordinates> GetTilesToFlip(GameModel game)
{
    const string DodgyId = "Dodgy-001";
    var tilesToFlip = new HashSet<HexCoordinates>();

    foreach (var tile in game.Tiles.Where(t => t.ResourceTileType != ResourceType.Sea))
    {
        // Get all owners of buildings adjacent to this tile
        var adjacentOwners = game.Buildings.OwnedBuildings(tile.TileKey)
            .Select(b => b.OwnerId)
            .Where(id => id != null)
            .Distinct()
            .ToList();

        // Flip if: no Dodgy buildings OR Dodgy shares with others
        bool hasDodgy = adjacentOwners.Contains(DodgyId);
        bool hasOthers = adjacentOwners.Any(id => id != DodgyId);

        if (!hasDodgy || (hasDodgy && hasOthers))
        {
            tilesToFlip.Add(tile.TileKey);
        }
    }
    return tilesToFlip;
}
```

**CSS Animation:**

```css
.tile-container {
    transform-style: preserve-3d;
    transition: transform 0.6s ease-in-out;
}

.tile-container.tile-flipped {
    transform: rotateY(180deg);
}

.tile-back {
    position: absolute;
    backface-visibility: hidden;
    transform: rotateY(180deg);
    /* Show a generic "back of tile" design */
}
```

### 5. Celebration Animation (Requirement 2)

CSS-only particle animation - lightweight, no dependencies. Can enhance later if desired.

**Files to modify:**

- `WebUI/Pages/Game.razor` - Detect Dodgy target selection, trigger celebration
- `WebUI/Pages/Game.razor.css` - Celebration animation styles
- `WebUI/wwwroot/css/app.css` - Global celebration keyframes

**Trigger point:** In `SelectRobberTarget()` method, after successful move, check if target is Dodgy
and house rule enabled.

### 6. Fake-Out Animation (Requirement 3)

This is the most complex requirement - a two-phase robber animation.

**Current animation flow:**

1. Server sets `RobberModel.PreviousCoordinates` to old position
2. Client receives update, `RobberLayer` renders at previous position
3. `OnAfterRenderAsync` triggers re-render at new position
4. CSS transition animates the movement

**New flow for fake-out:**

1. Server sets `RobberModel.PreviousCoordinates` and `RobberModel.Coordinates` as normal
2. Client detects: GriefDodgy enabled + target is NOT Dodgy
3. Client calculates Dodgy's best tile (most stars with Dodgy's buildings)
4. Animation Phase 1: Animate to Dodgy's tile (via CSS)
5. Hold for 1 second
6. Animation Phase 2: Animate to actual destination

**Implementation approach:**

- Keep server logic unchanged (clean separation)
- Add client-side state in `RobberLayer.razor`:
  - `_fakeOutCoords` - Dodgy's best tile
  - `_fakeOutPhase` - 0=none, 1=animating to fake, 2=holding, 3=animating to real
- Override animation sequence when fake-out is active

**Finding Dodgy's best tile:**

```csharp
private HexCoordinates? FindDodgyBestTile(GameModel game, string dodgyId)
{
    // Find all tiles where Dodgy has buildings
    var dodgyTiles = game.Tiles
        .Where(t => game.Buildings.OwnedBuildings(t.TileKey)
            .Any(b => b.OwnerId == dodgyId))
        .OrderByDescending(t => t.Stars)
        .FirstOrDefault();

    return dodgyTiles?.TileKey;
}
```

### 7. Data Flow

```text
HouseRules.GriefDodgy
    ↓
GameModel.HouseRules (from server)
    ↓
Game.razor (detects MustMoveRobber state, calculates flipped tiles)
    ↓
├── TileLayer (tile flip animation)
├── RobberLayer (fake-out animation)
└── Game.razor (celebration on target select)
```

### 8. Files to Create/Modify Summary

| File | Change |
|------|--------|
| `Catan3.Shared/Models/HouseRules.cs` | Add `GriefDodgy` property |
| `WebUI/wwwroot/settings.json` | Add GriefDodgy setting |
| `WebUI/Pages/Game.razor` | Dodgy detection, tile flip logic, celebration trigger |
| `WebUI/Components/Board/TileLayer.razor` | Add tile flip support |
| `WebUI/Components/Board/RobberLayer.razor` | Fake-out animation logic |
| `WebUI/wwwroot/css/app.css` | Animation keyframes |

## Design Decisions

1. **Fireworks style**: CSS particles (simple, can enhance later)
2. **Sound effects**: None
3. **Dodgy detection**: Match player ID `"Dodgy-001"` exactly
4. **Robber vs Baron**: Both use same `MustMoveRobber` state - animations apply equally to both

## Implementation Status

**Completed 2025-12-10**

1. `HouseRules.GriefDodgy` property added
2. `RobberModel.FakeOutCoordinates` for animation state
3. `GameStateMachine.CalculateGriefDodgyFakeOut()` calculates Dodgy's best tile
4. Settings UI for GriefDodgy (default: true)
5. Tile flip animation in `TileSvgRenderer.RenderSvg()` with `isFlipped` parameter
6. `BaseLayer` passes `FlippedTiles` to tiles
7. `Game.razor` calculates `GriefDodgyFlippedTiles` based on building ownership
8. Celebration animation when targeting Dodgy
9. CSS animations in `app.css` for tile flip (scaleX-based for SVG compatibility)

## Testing Plan

1. Create new game with Dodgy as a player
2. Enable GriefDodgy in Settings
3. Progress to a 7 roll or soldier purchase
4. Verify:
   - Tiles without Dodgy-only buildings flip face-down for 5 seconds
   - Tiles where Dodgy is sole owner stay face-up
   - Selecting Dodgy triggers celebration
   - Selecting non-Dodgy shows fake-out animation
5. Disable GriefDodgy, verify normal behavior restored
