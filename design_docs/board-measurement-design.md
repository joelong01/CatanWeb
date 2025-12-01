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

- Background image from `/themes/base/tiles/` (matches tile rendering via IAssetService)
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

## Call Flow Analysis

**See comprehensive call flow documentation:** [`board-measurement-call-flow.md`](./board-measurement-call-flow.md)

This separate document contains:
- Complete step-by-step trace of slider movement (0 → 10)
- Mermaid sequence diagrams for both WebUI and Desktop
- Side-by-side comparison of architectural approaches
- Detailed analysis of building visual state logic
- Performance comparison and optimization notes

### Quick Summary

**WebUI Flow:**
```
User moves slider → @oninput event → HandleSliderInput()
→ ShownStarsChanged.InvokeAsync(10) → Game.HandleShownStarsChanged()
→ StateHasChanged() → GenerateBoardSvg()
→ gameModel.GenerateSvg(shownStars: 10)
→ GetBuildingVisualState() for each building
→ building.RenderSvg() → SVG markup → DOM update
```

**Desktop Flow:**
```
User moves slider → TwoWay binding → GameViewModel.ShownStars = 10
→ OnShownStarsChanged(10) → Loop through Buildings collection
→ Set building.VisualState (Stars or Hidden)
→ PropertyChanged events → XAML re-evaluates bindings
→ BIND_StateGlyph() returns glyph → WinUI3 renders
```

**Key Architectural Difference:**
- Desktop: Property-based reactivity with granular updates
- WebUI: Full SVG regeneration with DOM diffing

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
- Validate gradient-current-player definition exists

### E2E Tests

- Navigate through board picking workflow
- Adjust slider and verify visual changes
- Complete game start after board selection
- Test default slider value (should be 13, not 0)

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
3. ✅ Enhance building visual state logic to handle star display (completed)
4. ✅ Test building visibility filtering with different ShownStars values (completed)
5. ✅ Verify buildings show star counts in PickingBoard state (completed)

### Phase 4: Integration with Game.razor

1. Conditionally render Board Measurement in Game page left panel
2. Pass OnShuffle/OnUndo EventCallbacks to BoardMeasurement
3. Update Game.razor to pass `GameState.ShownStars` to `GenerateSvg()`
4. Connect shuffle command to GameHub SignalR call
5. Connect undo command to GameHub SignalR call
6. Test complete workflow: slider → building visibility → shuffle → undo

## Resource Filtering Feature

### Desktop Implementation Reference

**Location:** `DesktopApp/Game/GameView/GameViewModel.cs:634-665` (ExecuteQuery method)

Desktop allows multi-select on resource cards to filter buildings by adjacent tile resources. Buildings are shown if they have ALL selected resources (AND logic).

**Key Features:**

1. **Multi-select GridView** with `SelectionMode="Multiple"`
2. **Maximum 3 resources** can be selected at once (oldest selection auto-removed)
3. **AND logic**: Building must have ALL selected resources to be visible
4. **Empty selection**: Shows all buildings based on star threshold (normal behavior)
5. **Visual feedback**: Selected cards show checkmark overlay

### Blazor Implementation Approach

#### Component: ResourceCard.razor (Enhanced)

Add selection state and click handling to ResourceCard:

**New Parameters:**

```csharp
[Parameter]
public bool IsSelected { get; set; } = false;

[Parameter]
public EventCallback<ResourceType> OnToggleSelection { get; set; }
```

**Markup Changes:**

```html
<div class="resource-card @(IsSelected ? "resource-card-selected" : "")"
     @onclick="HandleClick"
     data-testid="resource-card-@Resource.ToString().ToLower()">
    <div class="resource-image" style="background-image: url('@GetResourceImageUrl()')"></div>
    <div class="resource-count-badge">@Count</div>
    @if (IsSelected)
    {
        <div class="selection-indicator">
            <span class="checkmark">&#xE10B;</span> <!-- Segoe Fluent Icons checkmark -->
        </div>
    }
</div>

@code {
    private async Task HandleClick()
    {
        await OnToggleSelection.InvokeAsync(Resource);
    }
}
```

**CSS for Selection State:**

```css
.resource-card-selected {
    outline: 3px solid var(--accent-primary);
    outline-offset: -3px;
}

.selection-indicator {
    position: absolute;
    top: 4px;
    right: 4px;
    width: 24px;
    height: 24px;
    background: var(--accent-primary);
    border-radius: 50%;
    display: flex;
    align-items: center;
    justify-content: center;
}

.checkmark {
    font-family: 'Segoe Fluent Icons', var(--icon-font-family);
    color: white;
    font-size: 16px;
}
```

#### Component: BoardMeasurement.razor (Enhanced)

Track selected resources and expose filter event:

**New State:**

```csharp
private HashSet<ResourceType> SelectedResources { get; set; } = new();
```

**New Parameter:**

```csharp
[Parameter]
public EventCallback<HashSet<ResourceType>> SelectedResourcesChanged { get; set; }
```

**Selection Handler:**

```csharp
private async Task HandleResourceToggle(ResourceType resource)
{
    if (SelectedResources.Contains(resource))
    {
        SelectedResources.Remove(resource);
    }
    else
    {
        SelectedResources.Add(resource);

        // Enforce max 3 selections (match Desktop behavior)
        if (SelectedResources.Count > 3)
        {
            // Remove oldest selection (first item in HashSet)
            var oldest = SelectedResources.First();
            SelectedResources.Remove(oldest);
        }
    }

    await SelectedResourcesChanged.InvokeAsync(SelectedResources);
}
```

**Updated Markup:**

```html
<div class="resource-cards-row">
    @foreach (var resourceType in GetDisplayedResources())
    {
        <ResourceCard
            Resource="@resourceType"
            Count="@GameModel.StarCount(resourceType)"
            IsSelected="@SelectedResources.Contains(resourceType)"
            OnToggleSelection="@HandleResourceToggle" />
    }
</div>
```

#### Game.razor Integration

Track filter state and pass to SVG generator:

**New State:**

```csharp
private HashSet<ResourceType> FilteredResources { get; set; } = new();
```

**Handler:**

```csharp
private void HandleSelectedResourcesChanged(HashSet<ResourceType> selectedResources)
{
    FilteredResources = selectedResources;
    StateHasChanged(); // Trigger SVG re-render
}
```

**Pass to SVG Generator:**

```csharp
@gameModel.GenerateSvg(
    players: playersInGameOrder,
    shownStars: ShownStars,
    filteredResources: FilteredResources
)
```

#### BoardSvgGenerator.cs Updates

Add filtering logic to building visibility:

**Method Signature:**

```csharp
public static string GenerateSvg(
    this GameModel gameModel,
    IReadOnlyList<PlayerViewModel> players,
    int shownStars = 0,
    HashSet<HexCoordinates>? dimmedTiles = null,
    HashSet<ResourceType>? filteredResources = null)  // NEW
```

**Updated GetBuildingVisualState:**

```csharp
private static BuildingVisualState GetBuildingVisualState(
    BuildingModel building,
    GameModel gameModel,
    int stars,
    int shownStars,
    HashSet<ResourceType>? filteredResources)  // NEW
{
    var currentPlayer = gameModel.CurrentPlayer();
    var hasCityEntitlement = currentPlayer.UnspentEntitlements.Contains(Entitlement.City);
    var hasSettlementEntitlement = currentPlayer.UnspentEntitlements.Contains(Entitlement.Settlement);
    var isPickingBoard = gameModel.GameState == GameState.PickingBoard;

    // NEW: Check resource filter (AND logic)
    if (filteredResources != null && filteredResources.Count > 0 && building.OwnerId == null)
    {
        var adjacentTiles = gameModel.TilesForBuildings(building.BuildingKey);
        var tileResources = adjacentTiles
            .Select(t => t.ResourceTileType)
            .Where(rt => rt != ResourceType.Desert && rt != ResourceType.Sea)
            .ToHashSet();

        // Building must have ALL filtered resources (AND logic)
        bool hasAllResources = filteredResources.All(resource => tileResources.Contains(resource));

        if (!hasAllResources)
        {
            return BuildingVisualState.Hidden;  // Filter out buildings without all resources
        }
    }

    // Existing logic continues...
    return building.BuildingState switch
    {
        BuildingState.PossibleSettlement => hasSettlementEntitlement && gameModel.Phase() != GamePhase.PickingResources
            ? BuildingVisualState.Highlighted
            : stars >= shownStars && (hasSettlementEntitlement || isPickingBoard)
                ? BuildingVisualState.Stars
                : BuildingVisualState.Hidden,
        // ... rest of switch statement
    };
}
```

### Filter Logic Explanation

**Desktop Behavior (GameViewModel.cs:634-665):**

```csharp
// Get resources from adjacent tiles
var tiles = TilesForBuildings(building.BuildingKey);
List<ResourceType> tileResources = tiles.Select(tile => tile.ResourceTileType).ToList();

// Check if building has ALL selected resources
bool containsAllResources = resources.All(resource => tileResources.Contains(resource));

if (containsAllResources)
{
    building.VisualState = BuildingVisualState.Stars;
}
else
{
    building.VisualState = BuildingVisualState.Hidden;
}
```

**WebUI Equivalent:**

Same logic, applied in `GetBuildingVisualState()` before normal star threshold checks. If filter is active and building doesn't have all resources, return Hidden immediately.

### User Experience Flow

1. **User clicks Wheat card** → Wheat selected → Buildings with Wheat (and stars >= threshold) shown
2. **User clicks Wood card** → Wheat + Wood selected → Only buildings with BOTH Wheat AND Wood shown
3. **User clicks Brick card** → Wheat + Wood + Brick selected → Only buildings with all 3 resources shown
4. **User clicks Ore card (4th)** → Wheat removed (oldest), now Wood + Brick + Ore selected
5. **User clicks Wheat again (deselect)** → Wheat removed → Filter cleared, all buildings by star threshold shown
6. **Slider still active** → Buildings must meet BOTH resource filter AND star threshold

### Testing Strategy

**Unit Tests:**

```csharp
[Fact]
public void ResourceCard_ShowsCheckmarkWhenSelected()
{
    var cut = RenderComponent<ResourceCard>(parameters => parameters
        .Add(p => p.Resource, ResourceType.Wheat)
        .Add(p => p.IsSelected, true));

    cut.Find(".selection-indicator").Should().NotBeNull();
}

[Fact]
public void BoardMeasurement_EnforcesMaxThreeSelections()
{
    // Select 4 resources, verify oldest is removed
}
```

**Integration Tests:**

- Select resource → verify filtered buildings shown
- Select multiple resources → verify AND logic
- Deselect resource → verify filter updated
- Select 4 resources → verify oldest removed

### Implementation Phases

1. **Enhance ResourceCard component** with selection state and click handler
2. **Update BoardMeasurement** to track selections and enforce max-3 rule
3. **Add CSS** for selected state visual feedback
4. **Update Game.razor** to handle SelectedResourcesChanged event
5. **Extend BoardSvgGenerator** to accept filteredResources parameter
6. **Update GetBuildingVisualState** to apply resource filter with AND logic
7. **Test** all scenarios (single, multiple, deselect, max-3 enforcement)

### CSS Visual Design

Selected resource cards should have:
- **Outline**: 3px solid accent color (blue/purple)
- **Checkmark indicator**: Top-right corner, circular badge
- **Hover state**: Slightly brighter outline
- **Transition**: Smooth 150ms animation

## Open Questions

1. **Resource Card Images**: Use same image files as tiles from `/images/tiles/`?
   - **Answer**: Yes, reuse existing tile images for consistency

2. **Star Calculation**: Client-side only or validate server-side?
   - **Answer**: Client-side for display, server validates on shuffle/undo commands

3. **Slider Debouncing**: Should slider changes debounce before re-rendering SVG?
   - **Answer**: Not needed - Blazor WASM handles re-renders efficiently, instant feedback is better UX

4. **Mobile Layout**: How should board measurement adapt for small screens?
   - **Answer**: Defer to future, current focus is desktop/tablet layout

5. **Resource Filter Interaction**: Should filter persist after board shuffle/undo?
   - **Answer**: Match Desktop - filter persists until user manually deselects resources

## References

- Desktop Implementation: `DesktopApp\Resources\BoardMeasurementCtrl.xaml`
- Building Control: `DesktopApp\Buildings\BuildingCtrl.xaml`
- Building View Model: `DesktopApp\Buildings\BuildingViewModel\BuildingViewModel.cs`
- Client-Side SVG Generator: `WebUI\Services\Rendering\BoardSvgGenerator.cs`
- Building Renderer: `WebUI\Services\Rendering\BuildingSvgRenderer.cs`
- Game State Service: `WebUI\Services\GameStateService.cs`
