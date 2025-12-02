# Players Panel Design

## Overview

This document describes the redesign for the PlayersPanel and PlayerTile components in the WebUI.
The goal is to match the Desktop app layout (see `.test_images/desktop-player-tiles.jpg`) while adding
flipping resource cards that show face-down when count is 0.

## Reference: Desktop Screenshot Analysis

From `desktop-player-tiles.jpg`, the Desktop layout shows per player:

**Row 1 (Stats Row):**

- Player avatar with turn number badge
- 8 stat tiles in a row: Score, Roads, Cities, Settlements, Soldiers, Robber, Targeted, Total, LongestRoad, GoodRolls, BadRolls, Stars
- Final tile shows VP score

**Row 2 (Resources This Turn):**

- 6 resource cards: Wheat, Wood, Sheep, Brick, Ore, GoldMine
- A robber card at the end showing cards lost this turn (face-down when count = 0, face-up when > 0)

## Current Issues

1. **Score stat uses wrong icon**: Currently uses `star.svg` but should use `laurel.svg` with the
   score number centered inside the laurel wreath (like Desktop app)
2. **Resource tracking**: The game-level resource tracking should use `GameModel.GameResourcesModel`
   (total resources this game for everyone), not tile star counts
3. **Missing robber card**: Need to add robber/baron card showing cards lost this turn per player
4. **No flip animation**: Resource cards don't flip face-down when count is 0
5. **Alignment**: Resource cards should align with stat tiles width

## Data Sources

### Game-Level Resources (GameResourcesHeader)

- `GameModel.GameResourcesModel.Wheat` - Total wheat this game (all players)
- `GameModel.GameResourcesModel.Wood` - Total wood this game
- `GameModel.GameResourcesModel.Sheep` - Total sheep this game
- `GameModel.GameResourcesModel.Brick` - Total brick this game
- `GameModel.GameResourcesModel.Ore` - Total ore this game
- `GameModel.GameResourcesModel.GoldMine` - Total gold this game
- `GameModel.GameResourcesModel.Robber` - Total cards lost to robber (all players)

### Per-Player Resources (ResourcesThisTurn Row)

- `Player.ResourcesThisTurn.Wheat` - Wheat gained this turn
- `Player.ResourcesThisTurn.Wood` - Wood gained this turn
- `Player.ResourcesThisTurn.Sheep` - Sheep gained this turn
- `Player.ResourcesThisTurn.Brick` - Brick gained this turn
- `Player.ResourcesThisTurn.Ore` - Ore gained this turn
- `Player.ResourcesThisTurn.GoldMine` - Gold gained this turn
- `Player.ResourcesThisTurn.Robber` - Cards lost to robber this turn

## Component Structure

### 1. GameResourcesHeader (New Component)

Shows game-wide resource totals at the top of the right column.

```text
+------------------------------------------------------------------+
| [Wheat] [Wood] [Sheep] [Brick] [Ore] [Gold] [Robber]            |
|   12      8      15      10     7      3       4                 |
+------------------------------------------------------------------+
```

**Location**: `WebUI/Components/Players/GameResourcesHeader.razor`

**Parameters**:

| Parameter | Type | Description |
|-----------|------|-------------|
| `GameResources` | `ResourcesModel` | From `GameModel.GameResourcesModel` |

**Behavior**:

- Shows 7 resource cards (6 resources + robber)
- Cards flip face-up when count > 0, face-down when count = 0
- Uses same flip animation pattern as PurchaseButton

### 2. PlayersPanel (Modified)

Container that renders GameResourcesHeader followed by PlayerTiles.

```razor
<div class="players-panel">
    <GameResourcesHeader GameResources="@GameModel.GameResourcesModel" />
    @foreach (var player in GameModel.Players)
    {
        <PlayerTile ... />
    }
</div>
```

### 3. PlayerTile (Modified)

Each player tile shows stats and resources this turn.

```text
+------------------------------------------------------------------+
| [Avatar] [Score][Roads][Cities][Sett][Sold][Rob][Tgt][Tot]...[VP]|
|    2       4      2      1      3      1    0    2   18          |
+------------------------------------------------------------------+
| [Wheat] [Wood] [Sheep] [Brick] [Ore] [Gold] [Robber]            |
|   2       0      3       1      0      0       1                 |
+------------------------------------------------------------------+
```

**Row 1 (Stats)**: No changes - already correct

**Row 2 (Resources This Turn)**:

- Add 7th card: Robber card showing `Player.ResourcesThisTurn.Robber`
- Cards flip: face-down when count = 0, face-up when count > 0
- Cards must align with stat tiles above (width matching)

## New Component: FlippableResourceCard

To implement the flip animation, create a new component that wraps resource display with flip logic.

**Location**: `WebUI/Components/Resources/FlippableResourceCard.razor`

**Parameters**:

| Parameter | Type | Description |
|-----------|------|-------------|
| `Resource` | `ResourceType` | The resource type to display |
| `Count` | `int` | The count to show |
| `IsFaceUp` | `bool` | Computed: `Count > 0` |

**Visual States**:

### Face Down (Count = 0)

- Shows card back image via `AssetService.GetAssetPath(AssetName.CardBack)`
- No count badge visible
- Uses CSS 3D flip transform

#### Face Up (Count > 0)

- Shows resource image via `AssetService.GetAssetPath(AssetName.CardXxx)`
- Count badge at bottom center
- Uses CSS 3D flip transform (rotateY 180deg)

**CSS Animation**: Same pattern as PurchaseButton flip:

```css
.flippable-card {
    perspective: 1000px;
}

.flippable-card-inner {
    transition: transform 0.6s;
    transform-style: preserve-3d;
}

.flippable-card.face-up .flippable-card-inner {
    transform: rotateY(180deg);
}

.flippable-card-front,
.flippable-card-back {
    position: absolute;
    backface-visibility: hidden;
}

.flippable-card-front {
    transform: rotateY(180deg);
}
```

## Layout Alignment

To align resource cards with stat tiles:

1. **Stat tile width**: Currently variable in `.player-stats-grid`
2. **Resource card wrapper**: Set to match stat tile width
3. **Total row width**: Stats row and resources row should have same total width

**Approach**: Use CSS Grid with fixed column widths for both rows:

```css
.player-stats-grid,
.resources-this-turn-row {
    display: grid;
    grid-template-columns: repeat(auto-fill, 40px);
    gap: 4px;
}
```

## Files to Create

1. `WebUI/Components/Players/GameResourcesHeader.razor` - Game-level resource totals
2. `WebUI/Components/Players/GameResourcesHeader.razor.css` - Styles
3. `WebUI/Components/Resources/FlippableResourceCard.razor` - Flipping card component
4. `WebUI/Components/Resources/FlippableResourceCard.razor.css` - Flip animation styles

## Files to Modify

1. `WebUI/Components/Players/PlayersPanel.razor` - Add GameResourcesHeader
2. `WebUI/Components/Players/PlayerTile.razor` - Add robber card, use FlippableResourceCard
3. `WebUI/Components/Players/PlayerTile.razor.css` - Alignment adjustments

## Implementation Order

1. Create FlippableResourceCard component with flip animation
2. Create GameResourcesHeader using FlippableResourceCard
3. Modify PlayerTile to use FlippableResourceCard and add robber card
4. Modify PlayersPanel to include GameResourcesHeader
5. Adjust CSS for alignment between stat tiles and resource cards

## Score Stat with Laurel Wreath

The Score stat tile requires special handling - it shows a laurel wreath SVG as background with
the score number centered inside.

### Asset Changes

1. Add `StatLaurel` to `AssetName.cs` enum
2. Update `themes/base/theme.json` to map `StatLaurel` to `/themes/base/stats/laurel.svg`
3. Change `StatScore` mapping from `star.svg` to `laurel.svg` (or use new `StatLaurel`)

### Rendering

The Score stat tile differs from other stats:

- **Other stats**: Icon above count number
- **Score stat**: Laurel wreath background, count number centered inside

```razor
@if (stat.Name == "Score")
{
    <div class="stat-tile score-tile" style="@GetStatStyle(stat.IsHighlighted)">
        <img src="@AssetService.GetAssetPath(AssetName.StatLaurel)" class="laurel-bg" />
        <div class="score-number">@stat.Count</div>
    </div>
}
else
{
    <!-- Normal stat tile rendering -->
}
```

CSS for score tile:

```css
.score-tile {
    position: relative;
}

.laurel-bg {
    position: absolute;
    inset: 0;
    width: 100%;
    height: 100%;
    object-fit: contain;
}

.score-number {
    position: relative;
    z-index: 1;
    font-size: 18px;
    font-weight: bold;
    text-align: center;
}
```

## Testing

1. Verify game-level resources show totals from `GameModel.GameResourcesModel`
2. Verify per-player resources show from `Player.ResourcesThisTurn`
3. Verify cards flip face-down when count = 0
4. Verify cards flip face-up when count > 0
5. Verify robber card appears in both header and per-player rows
6. Verify alignment of resource cards with stat tiles
7. Test all themes (classic, black-and-white) for proper card back images
8. Verify Score stat shows laurel wreath with number centered inside
