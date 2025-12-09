# Portrait Mode Design

## Overview

When the viewport aspect ratio is less than 4:3 (portrait orientation), the game switches to a tabbed interface to maximize use of screen real estate.

## Scaling Architecture

Portrait mode uses the same `viewportScaler.js` as landscape mode. There is **no separate panel scaling** - the entire game container scales uniformly as a single unit.

**Key principle:** All components use fixed pixel dimensions designed for the reference resolution (1080x1920 for portrait). The viewport scaler handles fitting this to any screen size.

See `.design/ui/uiscale-design.md` for the complete scaling architecture.

## Layout Structure

```text
┌─────────────────────────────────────┐
│  Board  │  Controls  │   Players   │  ← Tab bar (60px, only visible in portrait)
├─────────────────────────────────────┤
│                                     │
│                                     │
│         Tab Content Area            │  ← 1080x1860 reference coordinates
│    (Board, Controls, or Players)    │
│                                     │
│                                     │
└─────────────────────────────────────┘
```

## Tab Descriptions

### Board Tab (Default)

- Shows the game board centered in available space
- Resource Tracking component displayed above the board
- Board scales to fit while maintaining aspect ratio

### Controls Tab

- Contains all game controls from the left panel:
  - Game name (editable)
  - Current player indicator
  - Undo / Next / Redo buttons
  - Purchase buttons (Road, Settlement, City, Soldier)
  - Roll entry grid (4 columns in portrait for better use of width)
  - Board measurements (during allocation phase)
- Content uses same fixed pixel sizes as landscape
- Centered horizontally in the 1080px width

### Players Tab

- Player tiles stacked vertically
- Each tile is 500px wide (same as landscape)
- Tiles centered horizontally (not right-aligned like landscape)
- No separate Resource Tracking here (it's on Board tab)

## CSS Architecture for Portrait Mode

### The IsPortrait Parameter Pattern

Components that need different styling in portrait mode receive an `IsPortrait` parameter:

```csharp
[Parameter] public bool IsPortrait { get; set; }
```

This parameter flows down from `Game.razor` through the component hierarchy:

```text
Game.razor (calculates _isPortrait from viewportScaler)
├── PlayersPanel (IsPortrait)
│   └── PlayerCard (IsPortrait)
│       └── PlayerTile (IsPortrait)
├── ResourceTracking
├── BoardMeasurement
└── PurchaseButton
```

### Portrait CSS Classes

Components apply a `.portrait` class when `IsPortrait` is true:

```razor
<div class="player-tile @(IsPortrait ? "portrait" : "")">
```

Then in scoped CSS:

```css
.player-tile {
    width: 500px;
    margin-left: auto;  /* Right-align in landscape */
}

.player-tile.portrait {
    margin-left: auto;
    margin-right: auto;  /* Center in portrait */
}
```

### What Changes in Portrait Mode

| Component | Landscape | Portrait |
|-----------|-----------|----------|
| PlayerTile | Right-aligned (`margin-left: auto`) | Centered (`margin: auto`) |
| PlayerCard | Right-aligned | Centered |
| PlayersPanel | `align-items: flex-end` | `align-items: center` |
| Roll Grid | 3 columns | 4 columns (uses width better) |
| Resource Tracking | In right panel | On Board tab |

### What Stays the Same

- All pixel dimensions (tile widths, stat sizes, fonts)
- Component internal layouts
- Colors, gradients, styling

## State Persistence

- Selected tab stored in `sessionStorage` as `portraitTab`
- Valid values: `"board"`, `"controls"`, `"players"`
- Persists across page refreshes within same session
- Blazor's `SetPortraitTab()` method handles tab switching

## Implementation Files

- **Scaling**: `wwwroot/js/viewportScaler.js` - Uniform container scaling
- **Layout CSS**: `Pages/Game.razor.css` - Panel visibility, tab bar
- **Tab State**: `Pages/Game.razor` - `_portraitTab` field, `SetPortraitTab()` method
- **Component CSS**: Each component's `.razor.css` file has `.portrait` rules

## Portrait Detection

Portrait mode activates when viewport aspect ratio < 4:3 (1.333):

- 16:9 (1.78) = landscape
- 4:3 (1.33) = landscape (boundary case)
- 9:16 (0.56) = portrait
- 3:4 (0.75) = portrait

The `viewportScaler.js` sets `data-layout-mode="portrait"` on the game container, which CSS uses for panel visibility.

### Dynamic Orientation Changes

When the user resizes the browser window and crosses the portrait/landscape threshold, the layout updates automatically:

1. `viewportScaler.updateScale()` runs on window resize
2. JavaScript detects orientation changed, calls Blazor via `DotNetObjectReference`
3. Blazor's `OnOrientationChanged(bool isPortrait)` method updates `_isPortrait`
4. `StateHasChanged()` triggers re-render with new `IsPortrait` values
5. Components apply/remove `.portrait` CSS classes accordingly

**Implementation:**

```javascript
// viewportScaler.js - notifies Blazor when orientation changes
if (this._lastIsPortrait !== isPortrait) {
    this._lastIsPortrait = isPortrait;
    if (this._dotNetRef) {
        this._dotNetRef.invokeMethodAsync('OnOrientationChanged', isPortrait);
    }
}
```

```csharp
// Game.razor - receives orientation change callback
[JSInvokable]
public void OnOrientationChanged(bool isPortrait)
{
    if (_isPortrait != isPortrait)
    {
        _isPortrait = isPortrait;
        InvokeAsync(StateHasChanged);
    }
}
```

## Landscape Mode (Default)

- Tab bar is hidden (`display: none`)
- Standard 3-column grid layout
- Left panel (controls), Center panel (board), Right panel (players)
- All panels visible simultaneously

## Future Considerations

- Swipe gestures between tabs on touch devices
- Indicator badges on tabs (e.g., "your turn" on Controls)
