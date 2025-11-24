# Board Measurement Design

## Overview

The Board Measurement control appears during the `PickingBoard` game state, allowing players to evaluate board quality before starting the game. It displays resource distribution statistics, star counts for tile quality, and controls for shuffling or reverting the board layout.

## Desktop Implementation Reference

**Location:** `DesktopApp\Resources\BoardMeasurementCtrl.xaml`

### Key Features

1. **Resource Cards Display** - Shows count of each resource type on the board
2. **Star Counters** - Four circular indicators showing counts for tiles with 13, 12, 11, and 10 stars
3. **Action Buttons** - "Previous Board" (Undo) and "Shuffle" controls
4. **Star Threshold Slider** - Range 0-14, filters buildings by minimum stars to display
5. **Building Visualization** - Buildings show star count when slider threshold is active

## WebUI Architecture

### Component Structure

```
WebUI/
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

## Component Specifications

### 1. ResourceCard.razor (Reusable)

**Purpose:** Display a single resource type with count

**Parameters:**

- `ResourceType Resource` - Type of resource (Wheat, Wood, etc.)
- `int Count` - Number of tiles with this resource

**Features:**

- Background image from GameService (same as tile rendering)
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

**Parameters:**

- `GameModel GameModel` - Current game state
- `EventCallback OnShuffle` - Shuffle command
- `EventCallback OnUndo` - Previous board command
- `int ShownStars { get; set; }` - Slider value (0-14)

**Layout:**

```
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

- Only visible when `GameModel.GameState == GameState.PickingBoard`
- Replaces normal left panel content during board picking phase

## SVG Building Rendering Updates

### Current State

Buildings are rendered in SVG by `BoardSvgGenerator.RenderBuilding()` with basic circle shapes.

### Required Changes

**1. Add Building Visual State**

```csharp
public enum BuildingVisualState
{
    Hidden,        // Not shown
    Highlighted,   // Show building icon
    Stars          // Show star count
}
```

**2. Building Rendering Logic**

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

## Data Flow

### Star Count Calculation

Desktop uses: `GameViewModel.BIND_StarCount(int threshold, List<TileModel> tiles)`

WebUI equivalent:

```csharp
// In BoardMeasurement.razor @code block
private int GetStarCount(int threshold)
{
    return GameModel.Tiles
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
```

### Resource Count Calculation

```csharp
private Dictionary<ResourceType, int> GetResourceCounts()
{
    return GameModel.Tiles
        .Where(t => t.ResourceTileType != ResourceType.Desert 
                 && t.ResourceTileType != ResourceType.Sea)
        .GroupBy(t => t.ResourceTileType)
        .ToDictionary(g => g.Key, g => g.Count());
}
```

### Slider Integration

```csharp
<input type="range" 
       min="0" 
       max="14" 
       @bind="ShownStars" 
       @bind:event="oninput"
       class="star-slider" />

@code {
    [Parameter]
    public int ShownStars { get; set; } = 0;
    
    [Parameter]
    public EventCallback<int> ShownStarsChanged { get; set; }
}
```

## API Updates

### GameService Board SVG Endpoint

Current: `GET /api/game/{gameId}/board.svg`

Enhancement: Add query parameter for star threshold

```
GET /api/game/{gameId}/board.svg?shownStars={value}
```

**Benefits:**

- SVG regenerated with filtered buildings
- No client-side manipulation needed
- Consistent with existing caching strategy using `GameHash`

**Cache Strategy:**

- Include `shownStars` in cache key: `{gameId}_{gameHash}_{shownStars}`
- Only cache for `PickingBoard` state
- Normal games use `shownStars=0` (default, show all)

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

1. Create `WebUI/Components/Resources/ResourceCard.razor`
2. Create `WebUI/Components/Resources/StarCounter.razor`
3. Create `WebUI/Components/Shared/IconButton.razor`
4. Add component unit tests

### Phase 2: Board Measurement Panel

1. Create `WebUI/Components/Board/BoardMeasurement.razor`
2. Integrate reusable components
3. Implement star count calculations
4. Add slider with two-way binding
5. Wire up shuffle/undo commands

### Phase 3: SVG Building Updates

1. Add `shownStars` parameter to SVG generation
2. Implement building visual state logic
3. Add player gradient color extraction
4. Update building rendering for star display
5. Test building visibility filtering

### Phase 4: Integration

1. Conditionally show Board Measurement in Game page left panel
2. Connect to GameService commands (shuffle, undo)
3. Update SVG endpoint with query parameter
4. Implement proper cache invalidation
5. End-to-end testing

## Open Questions

1. **Resource Card Images**: Use same SVG patterns as tiles, or separate image assets?
   - **Recommendation**: Reuse tile patterns for consistency

2. **Star Calculation Location**: Client-side or server-side?
   - **Recommendation**: Both - client for display, server validates

3. **Slider Debouncing**: Should slider changes debounce before updating SVG?
   - **Recommendation**: Yes, 150ms debounce to prevent excessive regeneration

4. **Mobile Layout**: How should board measurement adapt for small screens?
   - **Recommendation**: Vertical stack instead of horizontal, smaller components

## References

- Desktop Implementation: `DesktopApp\Resources\BoardMeasurementCtrl.xaml`
- Building Control: `DesktopApp\Buildings\BuildingCtrl.xaml`
- Building View Model: `DesktopApp\Buildings\BuildingViewModel\BuildingViewModel.cs`
- SVG Generator: `Catan3.GameService\Services\BoardSvgGenerator.cs`
