# Plan: Layered SVG Rendering Architecture

## Problem

The current `BoardSvgGenerator.GenerateSvg()` regenerates the entire board SVG (~150 elements) on every
state change. This causes lag in two critical scenarios:

1. **Slider drag** - Star threshold changes trigger full re-render
2. **Building hover during allocation** - Mouse enter/exit on buildings triggers re-render to show/hide stars

Both need to feel instant for good UX.

## Game Phases and Rendering Behavior

### Allocation Phase

- Buildings show star counts on hover (CSS opacity trick currently works)
- Need instant hover response - no re-render on mouse enter/exit

### Post-Allocation (Normal Play)

- **Gold tiles**: When player starts turn, some tiles become "gold" temporarily
  - Background changes to GoldMine asset
  - Small ResourceCard overlay shows original resource type
- **Buildable locations**: When player buys road/building:
  - Buildable positions are calculated
  - Numbered indicators are ADDED to the board (not full re-render)
  - After player picks one, unbuilt indicators are REMOVED
  - Only the placed building/road remains

### Key Insight

The rendering model should support:

1. **Layer updates** - Replace entire layer content (e.g., all buildings change)
2. **Additive rendering** - Add buildable indicators without touching other layers
3. **Subtractive rendering** - Remove buildable indicators, keep placed item

## Solution

Replace the monolithic SVG generation with a layered component architecture where each layer only
re-renders when its specific data changes.

## Architecture

Single `<svg>` element with multiple `<g>` group layers as child Blazor components:

```text
BoardContainer.razor (owns <svg>)
├── SharedDefinitions.razor  → <defs> (patterns, gradients)
├── BaseLayer.razor          → <g> tiles, numbers, harbors
├── GoldTilesLayer.razor     → <g> temporary gold overlays
├── RoadsLayer.razor         → <g> all roads
├── BuildingsLayer.razor     → <g> all buildings (slider-sensitive)
└── RobberLayer.razor        → <g> robber piece
```

**Why single SVG with `<g>` groups (not multiple stacked SVGs):**

- Shared viewBox automatically aligns coordinates
- Single `<defs>` section - patterns defined once, referenced everywhere
- Natural SVG painting order handles z-ordering
- CSS hover works normally across all elements

## Re-render Matrix

| Action               | Defs | Base | Gold | Roads | Buildings | Robber |
|----------------------|------|------|------|-------|-----------|--------|
| Slider drag          | -    | -    | -    | -     | YES       | -      |
| Resource filter      | -    | -    | -    | -     | YES       | -      |
| Board shuffle        | -    | YES  | -    | -     | YES       | -      |
| Building placed      | -    | -    | -    | -     | YES       | -      |
| Road placed          | -    | -    | -    | YES   | -         | -      |
| Robber moved         | -    | -    | -    | -     | -         | YES    |
| Turn starts          | YES* | -    | YES  | -     | YES*      | -      |
| Theme change         | YES  | YES  | YES  | YES   | YES       | YES    |

**Key win:** Slider drag only re-renders BuildingsLayer (~50 elements), not all 6 layers.

## Implementation Steps

### Step 1: Create Layer Components

Create 7 new files in `WebUI/Components/Board/`:

1. **BoardContainer.razor** - Parent component owning the `<svg>` element
   - Parameters: GameModel, Players, AssetService, ShownStars, FilteredResources
   - Calculates viewBox bounds (port from BoardSvgGenerator)
   - Renders child layer components

2. **SharedDefinitions.razor** - The `<defs>` section
   - Port pattern generation from BoardSvgGenerator lines 268-389
   - ShouldRender: only when Players, CurrentPlayer, or Theme changes

3. **BaseLayer.razor** - Tiles, numbers, harbors
   - Uses existing TileSvgRenderer.RenderSvg() and HarborSvgRenderer.RenderSvg()
   - ShouldRender: only when tile hash changes (board shuffle/resize)

4. **GoldTilesLayer.razor** - Temporary gold tile overlays
   - Renders only tiles where TemporarilyGold is true
   - ShouldRender: only when gold tile set changes

5. **RoadsLayer.razor** - All roads
   - Uses existing RoadSvgRenderer.RenderSvg()
   - ShouldRender: only when road states/ownership changes

6. **BuildingsLayer.razor** - All buildings (primary optimization target)
   - Port visual state logic from BoardSvgGenerator lines 119-139, 152-212
   - Uses existing BuildingSvgRenderer.RenderSvg()
   - ShouldRender: when ShownStars, FilteredResources, buildings, or game state changes

7. **RobberLayer.razor** - Robber piece
   - ShouldRender: only when robber position changes

### Step 2: ShouldRender Pattern

Each layer implements change detection via hash comparison:

```csharp
@code {
    private int _previousHash;

    protected override bool ShouldRender()
    {
        var currentHash = ComputeHash();
        if (currentHash != _previousHash)
        {
            _previousHash = currentHash;
            return true;
        }
        return false;
    }
}
```

### Step 3: Integrate in Game.razor

Replace line 118:

```razor
// Before:
@((MarkupString)GenerateBoardSvg())

// After:
<BoardContainer GameModel="@GameModel"
                Players="@GameStateService.Players"
                AssetService="@AssetService"
                ShownStars="@ShownStars"
                FilteredResources="@FilteredResources" />
```

Remove `GenerateBoardSvg()` method (lines 639-658).

### Step 4: Cleanup

- Delete or mark `BoardSvgGenerator.GenerateSvg()` as obsolete
- Keep individual renderers (TileSvgRenderer, etc.) - they're still used by layers
- Update design documentation

## Files to Create

| File | Purpose |
|------|---------|
| `WebUI/Components/Board/BoardContainer.razor` | SVG parent, viewBox, layer orchestration |
| `WebUI/Components/Board/SharedDefinitions.razor` | Patterns and gradients in `<defs>` |
| `WebUI/Components/Board/BaseLayer.razor` | Tiles, numbers, harbors |
| `WebUI/Components/Board/GoldTilesLayer.razor` | Temporary gold tile overlays |
| `WebUI/Components/Board/RoadsLayer.razor` | All roads |
| `WebUI/Components/Board/BuildingsLayer.razor` | All buildings with star/filter logic |
| `WebUI/Components/Board/RobberLayer.razor` | Robber piece |

## Files to Modify

| File | Changes |
|------|---------|
| `WebUI/Pages/Game.razor` | Replace MarkupString with BoardContainer component |
| `WebUI/Services/Rendering/BoardSvgGenerator.cs` | Mark GenerateSvg() obsolete or delete |

## Files to Reference (port logic from)

- `BoardSvgGenerator.cs` - viewBox calculation, pattern generation, building visual state
- `BoardSvgConstants.cs` - shared constants
- `BoardGeometry.cs` - coordinate calculations

## Scope Decisions

1. **Layering first, caching later** - Implement the 7-layer architecture, measure performance, add
   SvgCacheService only if still needed
2. **Buildable indicators in existing layers** - BuildingsLayer renders building placement indicators,
   RoadsLayer renders road placement indicators (no separate layer)

## Future Optimization (if needed)

If slider performance is still insufficient after this refactor:

1. Add fine-grained events to GameStateService (OnBuildingsChanged, etc.)
2. Use `@key` directive on individual building `<g>` elements for better Blazor diffing
3. Consider mouse tracking at board level with manual building hit detection
4. Add SvgCacheService for static element caching (CatanNumbers, patterns)
