# Purchase Button Design

## Overview

This document describes the design for the Purchase Button component in the WebUI, which allows players to
purchase Roads, Settlements, Cities, and play Soldiers (Knights). The design mirrors the Desktop app's
`PurchaseCtrl.xaml` with a flippable card metaphor.

## Reference: Desktop Implementation

From `DesktopApp/Controls/PurchaseCtrl.xaml`:

- **Size**: 100x100 pixels
- **Corner Radius**: 10px
- **Uses FlipperCtrl**: Card can flip between front and back
- **Back**: Shows `ResourceCard.Back` image
- **Front**: Shows player gradient background with:
  - Top-left: Unspent count
  - Center: Glyph (icon) in CatanFont at 44px
  - Bottom: Description text at 12px
- **Animations**: Scale to 0.98 on press, back to 1.0 on release

## WebUI Component Design

### Component: `PurchaseButton.razor`

Location: `WebUI/Components/Shared/PurchaseButton.razor`

### Injected Services

```razor
@inject IAssetService AssetService
```

The component uses `IAssetService` to resolve themed asset paths for:

- Card back image: `AssetService.GetAssetPath(AssetName.CardBack)`
- Building SVGs: `AssetService.GetAssetPath(AssetName.BuildingRoad)`, etc.

### Properties

| Parameter | Type | Description |
|-----------|------|-------------|
| `PurchaseType` | `PurchaseType` enum | Road, Settlement, City, or Soldier |
| `IsFaceUp` | `bool` | Whether card shows front (true) or back (false) |
| `SpentCount` | `int` | Number of this item the player has built/played |
| `MaxCount` | `int` | Maximum allowed (from ResourceRules) |
| `BackgroundGradient` | `string` | CSS gradient from PlayerColors |
| `ForegroundColor` | `string` | Text/icon color from PlayerColors.Foreground |
| `OnRightClick` | `EventCallback` | Fired on right-click to toggle flip (testing) |

### PurchaseType Enum

```csharp
public enum PurchaseType
{
    Road,
    Settlement,
    City,
    Soldier  // Display as Knight
}
```

### Visual States

#### Back (Face Down) - Default State

- Background: `AssetService.GetAssetPath(AssetName.CardBack)` - themed card back image
- Size: 100x100 pixels
- Corner radius: 10px
- Content: "X of Y" text centered
  - X = SpentCount (items built)
  - Y = MaxCount (from ResourceRules)
  - For Soldier: Just show count of soldiers played (no "of Y")

#### Front (Face Up) - After Right-Click

- Background: Player gradient (primary -> secondary color)
- Size: 100x100 pixels
- Corner radius: 10px
- Content:
  - Center: SVG icon for the purchase type (via AssetService)
    - Road: `AssetService.GetAssetPath(AssetName.BuildingRoad)`
    - Settlement: `AssetService.GetAssetPath(AssetName.BuildingSettlement)`
    - City: `AssetService.GetAssetPath(AssetName.BuildingCity)`
    - Soldier: `AssetService.GetAssetPath(AssetName.BuildingKnight)`
  - SVG rendered with player's foreground color
  - Bottom: Label text ("Road", "Settlement", "City", "Soldier")

### CSS Flip Animation

Use CSS 3D transforms for the card flip effect:

```css
.purchase-button {
    width: 100px;
    height: 100px;
    perspective: 1000px;
}

.purchase-button-inner {
    position: relative;
    width: 100%;
    height: 100%;
    transition: transform 0.6s;
    transform-style: preserve-3d;
}

.purchase-button.face-up .purchase-button-inner {
    transform: rotateY(180deg);
}

.purchase-button-front,
.purchase-button-back {
    position: absolute;
    width: 100%;
    height: 100%;
    backface-visibility: hidden;
    border-radius: 10px;
}

.purchase-button-back {
    /* back.png background */
}

.purchase-button-front {
    transform: rotateY(180deg);
    /* player gradient background */
}
```

## Data Sources

### Spent Counts

From `PlayerModel`:

- Roads: `player.SpentEntitlementsThisGame.Count(e => e == Entitlement.Road)`
- Settlements: `player.SpentEntitlementsThisGame.Count(e => e == Entitlement.Settlement)`
- Cities: `player.SpentEntitlementsThisGame.Count(e => e == Entitlement.City)`
- Soldiers: `player.SpentEntitlementsThisGame.Count(e => e == Entitlement.Soldier)`

### Max Counts

From `GameModel.ResourceRules`:

- Roads: `gameModel.ResourceRules.MaxRoads` (typically 15)
- Settlements: `gameModel.ResourceRules.MaxSettlements` (typically 5)
- Cities: `gameModel.ResourceRules.MaxCities` (typically 4)
- Soldiers: No max (just show count)

### Player Colors

From `PlayerProfile.Colors` (PlayerColors record):

- `PrimaryBackgroundColor` - Gradient start
- `SecondaryBackgroundColor` - Gradient end
- `ForegroundColor` - Icon/text color

## Integration with Game.razor

Replace the current placeholder purchase-grid:

```razor
<!-- Current -->
<div class="purchase-grid">
    <div class="purchase-card">Road</div>
    <div class="purchase-card">Settlement</div>
    <div class="purchase-card">City</div>
    <div class="purchase-card">Dev Card</div>
</div>

<!-- New -->
<div class="purchase-grid">
    <PurchaseButton PurchaseType="PurchaseType.Road"
                    SpentCount="@GetSpentCount(Entitlement.Road)"
                    MaxCount="@GameModel.ResourceRules.MaxRoads"
                    BackgroundGradient="@GetCurrentPlayerGradient()"
                    ForegroundColor="@GetCurrentPlayerForeground()"
                    IsFaceUp="@_roadFaceUp"
                    OnRightClick="@(() => _roadFaceUp = !_roadFaceUp)" />
    <!-- Similar for Settlement, City, Soldier -->
</div>
```

## Files to Create/Modify

### New Files

1. `WebUI/Components/Shared/PurchaseButton.razor` - Component
2. `WebUI/Components/Shared/PurchaseButton.razor.css` - Scoped styles
3. `WebUI/Models/PurchaseType.cs` - Enum definition

### Modified Files

1. `WebUI/Pages/Game.razor` - Replace placeholder with PurchaseButton components

## Testing

Right-click on any purchase button to flip it face-up, showing:

- The building SVG in the player's foreground color
- The player's gradient background
- The label at the bottom

Click again to flip back to face-down showing "X of Y" count.

## Future Enhancements (Not in Initial Implementation)

- Left-click to actually purchase (requires game logic integration)
- Disable when player can't afford the item
- Highlight when item is purchasable
- Press animation (scale to 0.98)
