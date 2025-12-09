# Session Summary: SVG Coordinate System Refactor - 2025-12-09

## Primary Goal
Fix board scaling issues on widescreen displays and unify the coordinate system for roads, buildings, and tiles.

## Problem Solved
The board had fixed `width: 1050px; height: 950px` which overrode the dynamic `aspect-ratio` CSS property, causing overflow on widescreen displays. Additionally, HTML overlay positioning for roads/buildings was misaligned because percentage-based CSS positioning didn't match the SVG viewBox coordinate system.

## Key Changes Made

### 1. Unified SVG Coordinate System
**Before:** Roads and buildings were HTML elements positioned with CSS percentages, separate from SVG tiles.
**After:** Roads and buildings are now SVG elements inside the same SVG as tiles, using `transform="translate(x, y)"` for positioning.

**Files Changed:**
- `WebUI/Components/Board/RoadOverlay.razor` - Converted from HTML div with CSS to SVG `<g>` with `<rect>` elements
- `WebUI/Components/Board/BuildingOverlay.razor` - Converted from HTML div to SVG `<g>` with `<circle>` and `<text>` elements
- `WebUI/Components/Board/BoardContainer.razor` - Moved overlays inside the interactive SVG layer
- `WebUI/Components/Board/BoardContainer.razor.css` - Removed HTML overlay positioning styles

### 2. Constraint-Based Board Sizing
**Principle:** Set the constraining dimension (height in landscape, width in portrait), let `aspect-ratio` calculate the other.

```css
/* Landscape: fill height, width follows aspect-ratio */
:global(.game-container[data-layout-mode="landscape"]) .board-svg-container {
    height: 100%;
    width: auto;
    aspect-ratio: var(--board-aspect-ratio);
    max-width: 100%;
}

/* Portrait: fill width, height follows aspect-ratio */
:global(.game-container[data-layout-mode="portrait"]) .board-svg-container {
    width: 100%;
    height: auto;
    aspect-ratio: var(--board-aspect-ratio);
    max-height: 100%;
}
```

### 3. Building Circle Sizing
Buildings now have a circle background with the player gradient, and the Catan font glyph is sized to fit with a margin:

```csharp
private const double BuildingRadius = 24;
private const double GlyphMargin = 5;
private const double GlyphFontSize = BuildingRadius * 1.4;  // Fits inside circle with border
```

### 4. Theme Cleanup
- Removed `svg-theme` (Vector Tiles) - too slow in current implementation
- Removed `Font-Theme` folder
- Removed non-existent `web` theme reference
- Themes now: `classic`, `black-and-white`

**File:** `WebUI/Services/ClientAssetService.cs`

### 5. Debug Border Toggle
Added global CSS variable to toggle debug borders:

```css
:root {
    --debug-borders: 0;  /* Set to 1 to show debug borders */
}

/* Usage in CSS */
border: calc(var(--debug-borders) * 4px) dashed red;
```

**Files with debug borders:**
- `WebUI/wwwroot/css/app.css` - Variable definition
- `WebUI/Pages/Game.razor.css` - game-container (red), center-panel (white), game-board (cyan), board-container (magenta)
- `WebUI/Components/Board/BoardContainer.razor.css` - board-svg-container (yellow)

### 6. Launch Settings Fix
Changed WebUI binding from `0.0.0.0:5296` to `localhost:5296` for Windows compatibility.

**File:** `WebUI/Properties/launchSettings.json`

## Architecture Notes

### SVG Layer Order (bottom to top)
1. **Static Layer** - Tiles, harbors (board-static-layer SVG)
2. **Interactive Layer** - Roads, robber, buildings (board-interactive-layer SVG)
   - Roads rendered first (bottom)
   - Robber in middle
   - Buildings rendered last (top)

### Coordinate System
All game elements now use `BoardGeometry` methods:
- `BoardGeometry.AxialToPixel(q, r)` - Tile centers
- `BoardGeometry.GetHexVertices(x, y)` - Hex corner positions
- `BoardGeometry.GetVertexPosition(buildingKey)` - Building positions
- Roads use edge midpoints calculated from vertex positions

### CSS Variable Flow
```
BoardContainer.razor (C#)
  → calculates viewBox bounds from tiles/harbors
  → sets inline style: --board-aspect-ratio, --board-width, --board-height
BoardContainer.razor.css
  → uses --board-aspect-ratio for sizing
```

## Files Modified (Summary)
1. `WebUI/Components/Board/RoadOverlay.razor` - Complete rewrite to SVG
2. `WebUI/Components/Board/RoadOverlay.razor.css` - Now unused (can be deleted)
3. `WebUI/Components/Board/BuildingOverlay.razor` - Complete rewrite to SVG
4. `WebUI/Components/Board/BuildingOverlay.razor.css` - Now unused (can be deleted)
5. `WebUI/Components/Board/BoardContainer.razor` - Moved overlays inside SVG
6. `WebUI/Components/Board/BoardContainer.razor.css` - Simplified, removed HTML overlay styles
7. `WebUI/Pages/Game.razor.css` - Added grid templates, debug borders
8. `WebUI/wwwroot/css/app.css` - Added --debug-borders, --portrait-tab-height
9. `WebUI/Services/ClientAssetService.cs` - Removed svg-theme, web theme
10. `WebUI/Properties/launchSettings.json` - Changed to localhost
11. `WebUI/Layout/MainLayout.razor.css` - Added 100dvh

## Testing Notes
- Board scales correctly on widescreen
- Roads and buildings align perfectly with tiles (same coordinate system)
- Theme switching works (Classic, Black & White)
- Debug borders can be enabled by setting `--debug-borders: 1` in app.css

## Known Issues / Future Work
1. `RoadOverlay.razor.css` and `BuildingOverlay.razor.css` are now unused - can be deleted
2. Portrait mode needs testing
3. iOS Safari 50% width issue - may need further investigation
4. Consider removing `updated-svg` theme folder if unused
5. **BUG: Azure deploy script doesn't detect uncommitted changes** - `catan-azure.ps1` compares git commit hashes but should compare build timestamps or file modification times to detect uncommitted changes. Currently requires `-Force` or committing first.
