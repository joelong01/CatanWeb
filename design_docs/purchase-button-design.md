# Purchase Button Design

## Overview

This document describes the design for the Purchase Button component in the WebUI, which allows players to
purchase Roads, Settlements, Cities, and play Soldiers (Knights). The design mirrors the Desktop app's
`PurchaseCtrl.xaml` with a flippable card metaphor.

## UI vs Behavior Contract

**IMPORTANT:** The WebUI visual design can differ from the Desktop app (but should be similar in spirit).
However, the **behavior** must be identical:

- **Visual flexibility**: Layout, animations, and styling may vary to suit web constraints
- **Behavioral contract**: The messages sent to GameService when buttons are clicked must be exactly the same
  as what the Desktop app sends. The game state changes triggered by purchases must be identical.

This ensures game logic remains consistent across platforms while allowing each UI to leverage its platform's
strengths.

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
| `Entitlement` | `Entitlement` enum | Road, Settlement, City, or Soldier (from Shared) |
| `IsFaceUp` | `bool` | Whether card shows front (true) or back (false) |
| `SpentCount` | `int` | Number of this item the player has built/played |
| `MaxCount` | `int` | Maximum allowed (from ResourceRules) |
| `BackgroundGradient` | `string` | CSS gradient from PlayerColors |
| `ForegroundColor` | `string` | Text/icon color from PlayerColors.Foreground |
| `OnRightClick` | `EventCallback` | Fired on right-click to toggle flip (testing) |

### Entitlement Enum (from Catan3.Shared)

Uses the existing `Entitlement` enum from `Catan3.Shared.Models.GameEnums` - no new enum needed.
The purchase button uses these values: `Road`, `Settlement`, `City`, `Soldier`.

### Theming Requirements

**IMPORTANT:** Both faces of the card must be fully themed to support light/dark/black-and-white modes.

All visual assets are resolved through `IAssetService` which returns theme-appropriate paths based on the
current `ThemeMode`. Text colors must also adapt to the current theme for proper contrast.

### Visual States

#### Back (Face Down) - Default State

- **Background**: Themed card back image via `AssetService.GetAssetPath(AssetName.CardBack)`
  - Returns theme-appropriate path (e.g., `assets/classic/back.png` or `assets/bw/back.png`)
- **Size**: 100x100 pixels
- **Corner radius**: 10px
- **Content**: "X of Y" text centered
  - X = SpentCount (items built)
  - Y = MaxCount (from ResourceRules)
  - For Soldier: Just show count of soldiers played (no "of Y")
  - **Text color**: Use themed text color via CSS variable `var(--text-primary)` for proper contrast

#### Front (Face Up) - After Right-Click

- **Background**: Player gradient (primary -> secondary color from PlayerColors)
- **Size**: 100x100 pixels
- **Corner radius**: 10px
- **Content**:
  - **Center**: Themed SVG icon for the purchase type (via AssetService)
    - Road: `AssetService.GetAssetPath(AssetName.BuildingRoad)`
    - Settlement: `AssetService.GetAssetPath(AssetName.BuildingSettlement)`
    - City: `AssetService.GetAssetPath(AssetName.BuildingCity)`
    - Soldier: `AssetService.GetAssetPath(AssetName.BuildingKnight)`
  - **SVG color**: Rendered with player's `ForegroundColor` from PlayerColors
  - **Bottom label**: Text showing type name ("Road", "Settlement", "City", "Soldier")
    - **Label color**: Use player's `ForegroundColor` for consistency with icon

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
    /* Themed card back image - path from AssetService */
    background-image: var(--card-back-url);
    background-size: cover;
    display: flex;
    align-items: center;
    justify-content: center;
}

.purchase-button-back .count-text {
    /* Themed text color for "X of Y" display */
    color: var(--text-primary);
    font-weight: bold;
    text-shadow: 0 1px 2px rgba(0, 0, 0, 0.5);
}

.purchase-button-front {
    transform: rotateY(180deg);
    /* Player gradient background - set via inline style */
    display: flex;
    flex-direction: column;
    align-items: center;
    justify-content: center;
}

.purchase-button-front .icon {
    /* SVG icon colored with player's foreground color */
    width: 44px;
    height: 44px;
}

.purchase-button-front .label {
    /* Label uses player's foreground color */
    font-size: 12px;
    margin-top: 4px;
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

### Face Up/Down State (Enabled)

The card's face up/down state is determined by `GameModel.PurchaseModel(entitlement).Enabled`:

- **Enabled = true** → Card is face up (player can purchase)
- **Enabled = false** → Card is face down (player cannot purchase)

This matches the Desktop implementation in `EntitlementPurchaseViewModel.Merge()`:

```csharp
Orientation = dataModel.Enabled ? CatanOrientation.FaceUp : CatanOrientation.FaceDown;
```

For testing purposes, right-clicking on a purchase button toggles the displayed state (overriding the game state).

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
    <PurchaseButton Entitlement="Entitlement.Road"
                    SpentCount="@GetSpentCount(Entitlement.Road)"
                    MaxCount="@(GameModel?.ResourceRules?.MaxRoads ?? 15)"
                    BackgroundGradient="@GetCurrentPlayerGradient()"
                    ForegroundColor="@GetCurrentPlayerForeground()"
                    IsFaceUp="@GetIsFaceUp(Entitlement.Road)"
                    OnRightClick="@(() => ToggleFaceUp(Entitlement.Road))" />
    <!-- Similar for Settlement, City, Soldier -->
</div>
```

## Files to Create/Modify

### New Files

1. `WebUI/Components/Shared/PurchaseButton.razor` - Component
2. `WebUI/Components/Shared/PurchaseButton.razor.css` - Scoped styles

### Modified Files

1. `WebUI/Pages/Game.razor` - Replace placeholder with PurchaseButton components

## Testing

### Basic Functionality

Right-click on any purchase button to flip it face-up, showing:

- The building SVG in the player's foreground color
- The player's gradient background
- The label at the bottom

Click again to flip back to face-down showing "X of Y" count.

### Theme Testing

Test all theme modes to verify proper theming on both faces:

1. **Classic Theme**: Verify card back shows classic texture, text is readable
2. **Black & White Theme**: Verify card back shows B&W version, text has proper contrast
3. **Dark Theme** (future): Verify assets and text adapt appropriately

For each theme, verify:

- **Back face**: Card back image matches theme, "X of Y" text is readable
- **Front face**: Building SVG loads correctly, player colors display properly

## Future Enhancements (Not in Initial Implementation)

- Left-click to actually purchase (requires game logic integration)
- Disable when player can't afford the item
- Highlight when item is purchasable
- Press animation (scale to 0.98)
