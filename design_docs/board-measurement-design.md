# Board Measurement Design

## Overview

The Board Measurement control appears during the `PickingBoard` game state, allowing players to evaluate board quality before starting the game. It displays resource distribution statistics, star counts for tile quality, and controls for shuffling or reverting the board layout.

**Updated: 2025-11-26** - Revised for client-side rendering architecture (thick client)

## Key Architecture Changes from Original Design

This design document has been updated to reflect the WebUI's **thick client architecture**:

### What Changed

- ❌ **Removed**: Server-side SVG generation endpoint (`GET /api/game/{gameId}/board.svg`)
- ❌ **Removed**: Server-side cache strategy for SVG variants
- ✅ **Added**: `GameStateService.ShownStars` property for UI state management
- ✅ **Added**: Direct binding to `GameStateService` instead of component parameters
- ✅ **Updated**: All SVG rendering happens client-side in browser via Blazor WASM

### Benefits of Client-Side Approach

- **Instant feedback**: Slider changes immediately update building visibility (no network latency)
- **Simpler architecture**: No server caching, no query parameters, no API versioning
- **Better UX**: Smooth animations and transitions using CSS
- **Reduced server load**: Server only handles game logic, not rendering

## Desktop Implementation Reference

**Location:** `DesktopApp\Resources\BoardMeasurementCtrl.xaml`

### Key Features

1. **Resource Cards Display** - Shows count of each resource type on the board
2. **Star Counters** - Four circular indicators showing counts for tiles with 13, 12, 11, and 10 stars
3. **Action Buttons** - "Previous Board" (Undo) and "Shuffle" controls
4. **Star Threshold Slider** - Range 0-14, filters buildings by minimum stars to display
5. **Building Visualization** - Buildings show star count when slider threshold is active

## WebUI Architecture (Thick Client)

### Client-Side Rendering Model

WebUI uses a thick client architecture where all SVG rendering happens in the browser:

- **GameStateService**: Singleton managing GameModel, PlayerData, and UI state (ShownStars)
- **Extension Method Renderers**: `gameModel.GenerateSvg()`, `tile.RenderSvg()`, `building.RenderSvg()`
- **Instant UI Updates**: Slider changes trigger re-render via `GameStateService.OnStateChanged` event
- **No Server Round-Trip**: Building visibility filtering happens entirely client-side

### Component Structure

```text
WebUI/
├── Services/
│   ├── GameStateService.cs                 # State manager with ShownStars property
│   └── Rendering/
│       ├── BoardSvgGenerator.cs            # gameModel.GenerateSvg() extension
│       └── BuildingSvgRenderer.cs          # building.RenderSvg() extension
├── Components/
│   ├── Board/
│   │   └── BoardMeasurement.razor          # Main measurement panel
│   ├── Resources/
│   │   ├── ResourceCard.razor              # Reusable resource card component
│   │   └── StarCounter.razor               # Circular star count indicator
│   └── Shared/
│       └── IconButton.razor                # Reusable icon button with label
```

### Blazor Component Model

Blazor supports reusable components similar to React components and XAML UserControls:

- **Component Definition**: `.razor` files with `@code` blocks
- **Parameters**: `[Parameter]` attributes for component props
- **Data Binding**: Two-way binding with `@bind` directive
- **Events**: `EventCallback` for parent-child communication
- **Styling**: Scoped CSS via `.razor.css` files
- **State Management**: Components subscribe to `GameStateService.OnStateChanged` event

## Component Specifications

### 1. ResourceCard.razor (Reusable)

**Purpose:** Display a single resource type with count

**Parameters:**

- `ResourceType Resource` - Type of resource (Wheat, Wood, etc.)
- `int Count` - Number of tiles with this resource

**Features:**

- Background image from `/images/tiles/` (bundled in wwwroot, matches tile rendering)
- Black background count badge at bottom
- CSS grid layout for card styling
- Hover effects for interactivity

**Usage Locations:**

- Board Measurement panel (current)
- Tracked Resources panel (right column) - future enhancement
- Trading interface - future

### 2. StarCounter.razor (Reusable)

**Purpose:** Circular indicator showing count of tiles with specific star value

**Parameters:**

- `int StarValue` - The star threshold (10, 11, 12, or 13)
- `int Count` - Number of tiles meeting threshold
- `GameModel GameModel` - For calculating counts

**Layout:**

- 80x80 circular grid with gold gradient background
- 5 red stars at top (Segoe Fluent Icons &#xE00A;)
- Star value (bold, white, centered)
- Count value (bottom, smaller font)

**CSS:**

```css
.star-counter {
    width: 80px;
    height: 80px;
    border-radius: 50%;
    background: linear-gradient(135deg, #FFD700, #FFA500);
}
```

### 3. IconButton.razor (Reusable)

**Purpose:** Button with Segoe MDL2 Assets icon and label below

**Parameters:**

- `string Icon` - Unicode character code for icon
- `string Label` - Text label below icon
- `EventCallback OnClick` - Click handler
- `bool Disabled` - Enable/disable state
- `string AutomationId` - For testing

**Styling:**

- Transparent background
- White foreground
- Icon font-size: 48px
- Label font-size: 12px
- Vertical stack layout

**Usage Locations:**

- Board Measurement (Previous Board, Shuffle)
- Future: Game controls, toolbars

### 4. BoardMeasurement.razor (Main Component)

**Purpose:** Container for all board measurement UI

**Dependencies:**

- `@inject GameStateService GameState` - Accesses GameModel, PlayerData, ShownStars

**Parameters:**

- `EventCallback OnShuffle` - Shuffle command
- `EventCallback OnUndo` - Previous board command

**Note:** Component subscribes to `GameState.OnStateChanged` to re-render when GameModel or ShownStars changes

**Layout:**

```text
┌─────────────────────────────────────┐
│ [Resource Cards Row]                │
│  🌾  🪵  🪨  🧱  🐑                 │
├─────────────────────────────────────┤
│ [Star Counters Row]                 │
│  (13) (12) (11) (10)                │
├─────────────────────────────────────┤
│ [Action Buttons Row]                │
│  [Undo]     [Shuffle]               │
├─────────────────────────────────────┤
│ [Star Threshold Slider]             │
│  0 ━━━●━━━━━━━━━━━ 14              │
└─────────────────────────────────────┘
```

**Conditional Rendering:**

- Only visible when `GameState.GameModel?.GameState == GameState.PickingBoard`
- Replaces normal left panel content during board picking phase
- Auto-updates when GameModel changes via SignalR

## SVG Building Rendering Updates

### Current State

Buildings are rendered in SVG by `BoardSvgGenerator.RenderBuilding()` with basic circle shapes.

### Required Changes

1. Add Building Visual State**

```csharp
public enum BuildingVisualState
{
    Hidden,        // Not shown
    Highlighted,   // Show building icon
    Stars          // Show star count
}
```

2. Building Rendering Logic**

```csharp
private void RenderBuilding(BuildingModel building, GameModel gameModel, int shownStars)
{
    var visualState = DetermineVisualState(building, gameModel, shownStars);
    
    if (visualState == BuildingVisualState.Hidden)
        return;
        
    var (x, y) = building.Coords.ToPixelCenter(HexSize, offsetX, offsetY);
    var radius = BuildingSize / 2;
    
    // Background circle with player gradient color
    var playerColors = GetPlayerColors(building, gameModel);
    RenderCircle(x, y, radius, playerColors.Background);
    
    if (visualState == BuildingVisualState.Stars)
    {
        // Show star count
        RenderText(x, y, building.Stars.ToString(), playerColors.Foreground);
    }
    else if (visualState == BuildingVisualState.Highlighted)
    {
        // Show building icon (settlement/city glyph)
        var glyph = GetBuildingGlyph(building.BuildingState);
        RenderText(x, y, glyph, playerColors.Foreground);
    }
}

private BuildingVisualState DetermineVisualState(
    BuildingModel building, 
    GameModel gameModel, 
    int shownStars)
{
    if (gameModel.GameState != GameState.PickingBoard)
        return BuildingVisualState.Highlighted;
        
    if (shownStars == 0)
        return BuildingVisualState.Hidden;
        
    if (building.Stars >= shownStars)
        return BuildingVisualState.Stars;
        
    return BuildingVisualState.Hidden;
}
```

**3. Player Gradient Colors**

During `PickingBoard` state:

- Use current player's gradient colors for buildings
- Background: `PrimaryBackgroundColor` to `SecondaryBackgroundColor` gradient
- Foreground: `ForegroundColor` for text/icons

After placement:

- Use building owner's colors
- Standard rendering

### Color Extraction

```csharp
private (string Background, string Foreground) GetPlayerColors(
    BuildingModel building, 
    GameModel gameModel)
{
    PlayerData player;
    
    if (building.OwnerId != null)
    {
        player = GetPlayerById(building.OwnerId);
    }
    else if (gameModel.GameState == GameState.PickingBoard)
    {
        player = GetPlayerById(gameModel.CurrentPlayerId);
    }
    else
    {
        return ("transparent", "#fff");
    }
    
    var background = gameModel.GameState == GameState.PickingBoard
        ? $"linear-gradient(135deg, {player.PrimaryBackgroundColor}, {player.SecondaryBackgroundColor})"
        : player.PrimaryBackgroundColor;
        
    return (background, player.ForegroundColor);
}
```

## Data Flow (Client-Side)

### Architecture Overview

```text
User moves slider
    ↓
GameStateService.ShownStars = value
    ↓
OnStateChanged event fires
    ↓
Game.razor re-renders
    ↓
Calls gameModel.GenerateSvg(playerData, GameState.ShownStars)
    ↓
SVG updated with filtered buildings (instant, no server call)
```

### Star Count Calculation

Desktop uses: `GameViewModel.BIND_StarCount(int threshold, List<TileModel> tiles)`

WebUI equivalent in `BoardMeasurement.razor`:

```csharp
@inject GameStateService GameState

@code {
    private int GetStarCount(int threshold)
    {
        if (GameState.GameModel == null)
            return 0;

        return GameState.GameModel.Tiles
            .Count(t => t.ResourceTileType != ResourceType.Desert
                     && t.ResourceTileType != ResourceType.Sea
                     && GetTileStars(t) == threshold);
    }

    private int GetTileStars(TileModel tile)
    {
        // Calculate stars based on probability
        // 6 or 8 = 5 stars, 5 or 9 = 4 stars, etc.
        return tile.Number switch
        {
            6 or 8 => 5,
            5 or 9 => 4,
            4 or 10 => 3,
            3 or 11 => 2,
            2 or 12 => 1,
            _ => 0
        };
    }
}
```

### Resource Count Calculation

```csharp
private Dictionary<ResourceType, int> GetResourceCounts()
{
    if (GameState.GameModel == null)
        return new();

    return GameState.GameModel.Tiles
        .Where(t => t.ResourceTileType != ResourceType.Desert
                 && t.ResourceTileType != ResourceType.Sea)
        .GroupBy(t => t.ResourceTileType)
        .ToDictionary(g => g.Key, g => g.Count());
}
```

### Slider Integration with GameStateService

```csharp
@inject GameStateService GameState

<input type="range"
       min="0"
       max="14"
       value="@GameState.ShownStars"
       @oninput="@(e => GameState.ShownStars = int.Parse(e.Value?.ToString() ?? "0"))"
       class="star-slider" />

<span>@GameState.ShownStars</span>

@code {
    protected override void OnInitialized()
    {
        // Subscribe to state changes
        GameState.OnStateChanged += HandleStateChanged;
    }

    private void HandleStateChanged(object? sender, EventArgs e)
    {
        StateHasChanged(); // Trigger Blazor re-render
    }

    public void Dispose()
    {
        GameState.OnStateChanged -= HandleStateChanged;
    }
}
```

**Benefits of GameStateService Integration:**

- ✅ Single source of truth for ShownStars value
- ✅ Automatic propagation to Game.razor for SVG re-rendering
- ✅ No prop drilling through component hierarchy
- ✅ Instant UI feedback (no server round-trip)

## Styling Guidelines

### CSS Variables (add to app.css)

```css
:root {
    /* Resource Cards */
    --resource-card-width: 67px;
    --resource-card-height: 100px;
    --resource-card-radius: 5px;
    
    /* Star Counter */
    --star-counter-size: 80px;
    --star-counter-bg: linear-gradient(135deg, #FFD700, #FFA500);
    
    /* Board Measurement Panel */
    --board-measurement-bg: var(--game-bg-secondary);
    --board-measurement-padding: 20px;
}
```

### Component Scoping

Each component gets a `.razor.css` file:

- `ResourceCard.razor.css` - Card-specific styles
- `StarCounter.razor.css` - Counter-specific styles
- `BoardMeasurement.razor.css` - Panel layout styles

Blazor automatically scopes these styles to prevent conflicts.

## Testing Considerations

### Component Unit Tests

```csharp
// Tests/WebUI/Components/ResourceCardTests.cs
[Fact]
public void ResourceCard_DisplaysCorrectCount()
{
    var cut = RenderComponent<ResourceCard>(parameters => parameters
        .Add(p => p.Resource, ResourceType.Wheat)
        .Add(p => p.Count, 4));
        
    cut.Find(".resource-count").TextContent.Should().Be("4");
}
```

### Integration Tests

- Verify slider updates building visibility
- Confirm star counts match Desktop calculations
- Test shuffle/undo button commands

### E2E Tests

- Navigate through board picking workflow
- Adjust slider and verify visual changes
- Complete game start after board selection

## Implementation Phases

### Phase 1: Reusable Components (Foundation)

1. Create `WebUI/Components/Resources/ResourceCard.razor` and `.razor.css`
2. Create `WebUI/Components/Resources/StarCounter.razor` and `.razor.css`
3. Create `WebUI/Components/Shared/IconButton.razor` and `.razor.css`
4. Add CSS variables to `wwwroot/css/app.css`
5. Add component unit tests (optional, can defer)

### Phase 2: Board Measurement Panel

1. Create `WebUI/Components/Board/BoardMeasurement.razor` and `.razor.css`
2. Integrate reusable components (ResourceCard, StarCounter, IconButton)
3. Inject `GameStateService` dependency
4. Implement star count calculations using `GameState.GameModel`
5. Add slider bound to `GameState.ShownStars`
6. Wire up shuffle/undo EventCallbacks (to be handled by Game.razor)
7. Subscribe to `GameState.OnStateChanged` event

### Phase 3: SVG Building Updates (Already Partially Done)

1. ✅ `shownStars` parameter already exists in `BoardSvgGenerator.GenerateSvg()`
2. ✅ `GetBuildingVisualState()` already implemented
3. Enhance building visual state logic to handle star display (may need updates)
4. Test building visibility filtering with different ShownStars values
5. Verify buildings show star counts in PickingBoard state

### Phase 4: Integration with Game.razor

1. Conditionally render Board Measurement in Game page left panel
2. Pass OnShuffle/OnUndo EventCallbacks to BoardMeasurement
3. Update Game.razor to pass `GameState.ShownStars` to `GenerateSvg()`
4. Connect shuffle command to GameHub SignalR call
5. Connect undo command to GameHub SignalR call
6. Test complete workflow: slider → building visibility → shuffle → undo

## Open Questions

1. **Resource Card Images**: Use same image files as tiles from `/images/tiles/`?
   - **Answer**: Yes, reuse existing tile images for consistency

2. **Star Calculation**: Client-side only or validate server-side?
   - **Answer**: Client-side for display, server validates on shuffle/undo commands

3. **Slider Debouncing**: Should slider changes debounce before re-rendering SVG?
   - **Answer**: Not needed - Blazor WASM handles re-renders efficiently, instant feedback is better UX

4. **Mobile Layout**: How should board measurement adapt for small screens?
   - **Answer**: Defer to future, current focus is desktop/tablet layout

## References

- Desktop Implementation: `DesktopApp\Resources\BoardMeasurementCtrl.xaml`
- Building Control: `DesktopApp\Buildings\BuildingCtrl.xaml`
- Building View Model: `DesktopApp\Buildings\BuildingViewModel\BuildingViewModel.cs`
- Client-Side SVG Generator: `WebUI\Services\Rendering\BoardSvgGenerator.cs`
- Building Renderer: `WebUI\Services\Rendering\BuildingSvgRenderer.cs`
- Game State Service: `WebUI\Services\GameStateService.cs`
