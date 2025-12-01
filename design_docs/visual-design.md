# Visual Design Convergence - WebUI to Desktop Parity

**Last Updated:** 2025-11-29
**Status:** Design Phase
**References:** `desktop-reference-view.jpg`, `web-full-view.jpg`

## Overview

This document analyzes the visual differences between the WebUI and Desktop implementations and provides a detailed plan to achieve visual parity.
The analysis uses Joe (blue gradient player) as the reference in both screenshots to enable direct comparison.

## Executive Summary

The WebUI implementation has achieved functional parity with Board Measurement and basic game controls, but significant visual layout differences remain:

1. **Board Layout**: Center panel board sizing is constrained to max-height on widescreen, creating wasted horizontal space
2. **Left Column**: Missing Player Tracking panel (ResourcesThisGame display)
3. **Right Column**: Missing Players panel with individual player tiles showing stats, score, and resources

The widescreen layout inefficiency is the most critical issue - the board scales to max viewport height, which limits width, leaving significant
unused space in columns 1 and 3 that should be allocated to richer UI elements.

## Current State Analysis

### Layout Architecture Comparison

#### Desktop Layout (3-Column Grid)

```text
┌─────────────┬──────────────────────┬─────────────┐
│             │                      │             │
│  Left Panel │    Center Panel      │ Right Panel │
│   (~250px)  │   (Board - Dynamic)  │  (~800px)   │
│             │                      │             │
│  • Controls │    SVG Board         │  • Players  │
│  • Purchase │    (max-height)      │    (Tiles)  │
│  • Roll     │                      │             │
│  • Board    │                      │  Each tile: │
│    Measure  │                      │  - Avatar   │
│             │                      │  - Score    │
│             │                      │  - Stats    │
│             │                      │  - Resources│
│             │                      │    ThisTurn │
│             │                      │             │
└─────────────┴──────────────────────┴─────────────┘
```

#### WebUI Current Layout (3-Column Grid)

```text
┌─────────────┬──────────────────────┬─────────────┐
│             │                      │             │
│  Left Panel │    Center Panel      │ Right Panel │
│   (~160px)  │   (Board - Dynamic)  │  (~300px)   │
│             │                      │             │
│  • Controls │    SVG Board         │  • Players  │
│  • Purchase │    (max-height)      │    List     │
│    (stub)   │                      │    (Basic)  │
│  • Roll     │                      │             │
│  • Board    │                      │  Just names │
│    Measure  │                      │  and scores │
│  ────────   │                      │             │
│  MISSING:   │                      │  MISSING:   │
│  • Player   │                      │  - Tiles    │
│    Tracking │                      │  - Stats    │
│             │                      │  - Resources│
└─────────────┴──────────────────────┴─────────────┘
```

### Widescreen Layout Issue

**Problem**: On widescreen displays (1920x1080+), the board is constrained by viewport height, not width. This creates:

- Board scales to max-height → determines max-width via aspect ratio
- Excess horizontal space distributed poorly to columns 1 and 3
- Left panel too narrow for optimal Player Tracking display
- Right panel too narrow for rich player tiles

**Solution**: Redistribute horizontal space allocation:

- Left panel: 250px (currently ~160px) - space for Player Tracking
- Right panel: 800px (currently ~300px) - space for player tiles with full stats
- Center panel: Remaining space (board will scale within this constraint)

## Section 1: Board Layout Optimization

### Current Issues

1. **CSS Grid Allocation**: Equal fractional units (1fr) for left/right panels inefficient
2. **No Minimum Width Constraints**: Panels collapse too narrow on smaller screens
3. **Board Scaling**: Currently uses `max-height: 100vh` which limits board size unnecessarily

### Proposed Solution

#### CSS Grid Template Update

**File**: `WebUI/Pages/Game.razor.css`

Current:

```css
.game-layout {
    display: grid;
    grid-template-columns: 1fr 2fr 1fr;
    height: 100vh;
}
```

Proposed:

```css
.game-layout {
    display: grid;
    grid-template-columns: minmax(250px, 300px) 1fr minmax(800px, 1000px);
    gap: 0;
    height: 100vh;
    width: 100vw;
    overflow: hidden;
}

.left-panel {
    min-width: 250px;
    max-width: 300px;
    overflow-y: auto;
    overflow-x: hidden;
}

.center-panel {
    min-width: 600px; /* Minimum board display width */
    display: flex;
    align-items: center;
    justify-content: center;
}

.right-panel {
    min-width: 800px;
    max-width: 1000px;
    overflow-y: auto;
    overflow-x: hidden;
}
```

#### Board Scaling Strategy

**File**: `WebUI/Pages/Game.razor.css` (`.board-container`)

```css
.board-container {
    width: 100%;
    height: 100%;
    display: flex;
    align-items: center;
    justify-content: center;
    position: relative;
}

.board-svg {
    max-width: 95%;
    max-height: 95vh;
    width: auto;
    height: auto;
}
```

**Rationale**: This allows the board to scale based on available space while maintaining aspect ratio. On widescreen, height will be the
limiting factor, but the fixed column widths ensure panels don't become unusably narrow.

### Implementation Steps

1. Update `Game.razor.css` with new grid template columns
2. Add min/max width constraints to panel classes
3. Update board-container CSS for centered scaling
4. Test at multiple viewport sizes (1920x1080, 2560x1440, 1366x768)
5. Verify board remains playable and panels usable at all sizes

## Section 2: Left Column - Player Tracking Panel

### Desktop Reference

**File**: `DesktopApp/Resources/TrackedResourcesCtrl.xaml`
**Usage**: Displays cumulative resources granted throughout the game (ResourcesThisGame)

#### Visual Characteristics

- **Location**: Left panel, below Board Measurement panel
- **Display**: Horizontal row of resource cards matching Board Measurement style
- **Data Source**: `PlayerModel.ResourcesThisGame` (cumulative resource totals)
- **Card Style**: Same ResourceCard component used in Board Measurement
- **Harbor Indicator**: Small harbor glyph (&#xE128;) overlays card when player owns 2:1 harbor for that resource
- **Visibility**: Shown during all game states (not just PickingBoard)

#### XAML Implementation Details

From `TrackedResourcesCtrl.xaml:11-27`:

```xml
<GridView ItemsSource="{x:Bind ResourcesViewModel.ResourceCounters, Mode=OneWay}">
    <GridView.ItemContainerStyle>
        <Style TargetType="GridViewItem">
            <Setter Property="Padding" Value="3" />
            <Setter Property="Margin" Value="0" />
            <Setter Property="CornerRadius" Value="10" />
        </Style>
    </GridView.ItemContainerStyle>
    <GridView.ItemTemplate>
        <DataTemplate x:DataType="models:ResourceCounterViewModel">
            <Viewbox Stretch="Uniform" Height="120">
                <c:ResourceCardCtrl ViewModel="{x:Bind Mode=OneWay}" />
            </Viewbox>
        </DataTemplate>
    </GridView.ItemTemplate>
</GridView>
```

#### Data Binding Pattern

From `PlayerViewModel.cs:97-104`:

```csharp
[ObservableProperty]
[property: JsonIgnore]
public partial ResourcesViewModel ResourcesThisTurn { get; set; } = new(GameViewModelStatics.PlayerTrackResourceList);

[ObservableProperty]
[property: JsonIgnore]
public partial ResourcesViewModel ResourcesThisGame { get; set; } = new(GameViewModelStatics.PlayerTrackResourceList);
```

`ResourcesViewModel` wraps `ResourcesModel` (from GameModel) and exposes `ObservableCollection<ResourceCounterViewModel>` for UI binding.

### Proposed WebUI Implementation

#### Component Structure

**New Component**: `WebUI/Components/Resources/PlayerTracking.razor`

```razor
@using Catan3.Shared.Models
@using Catan3.WebUI.Components.Resources

<div class="player-tracking-panel" data-testid="player-tracking-panel">
    <div class="panel-header">Player Tracking</div>
    <div class="resource-cards-row">
        @foreach (var resourceType in GetTrackedResources())
        {
            var count = GetResourceCount(resourceType);
            var hasHarbor = HasHarbor(resourceType);
            <div class="tracked-resource-card">
                <ResourceCard
                    Resource="@resourceType"
                    Count="@count"
                    IsSelected="false"
                    OnToggleSelection="@(() => Task.CompletedTask)" />
                @if (hasHarbor)
                {
                    <div class="harbor-indicator" title="2:1 Harbor">&#xE128;</div>
                }
            </div>
        }
    </div>
</div>

@code {
    [Parameter, EditorRequired]
    public GameModel GameModel { get; set; } = null!;

    [Parameter, EditorRequired]
    public string CurrentPlayerId { get; set; } = string.Empty;

    private ResourceType[] GetTrackedResources()
    {
        // Match Desktop: Wheat, Wood, Sheep, Brick, Ore
        return new[]
        {
            ResourceType.Wheat,
            ResourceType.Wood,
            ResourceType.Sheep,
            ResourceType.Brick,
            ResourceType.Ore
        };
    }

    private int GetResourceCount(ResourceType resource)
    {
        var player = GameModel.Players.FirstOrDefault(p => p.Id == CurrentPlayerId);
        if (player == null) return 0;

        return resource switch
        {
            ResourceType.Wheat => player.ResourcesThisGame.Wheat,
            ResourceType.Wood => player.ResourcesThisGame.Wood,
            ResourceType.Sheep => player.ResourcesThisGame.Sheep,
            ResourceType.Brick => player.ResourcesThisGame.Brick,
            ResourceType.Ore => player.ResourcesThisGame.Ore,
            _ => 0
        };
    }

    private bool HasHarbor(ResourceType resource)
    {
        var player = GameModel.Players.FirstOrDefault(p => p.Id == CurrentPlayerId);
        if (player == null) return false;

        var harborKey = resource switch
        {
            ResourceType.Wheat => HarborKey.Wheat,
            ResourceType.Wood => HarborKey.Wood,
            ResourceType.Sheep => HarborKey.Sheep,
            ResourceType.Brick => HarborKey.Brick,
            ResourceType.Ore => HarborKey.Ore,
            _ => HarborKey.None
        };

        return player.OwnedHarbors.Contains(harborKey);
    }
}
```

#### CSS Styling

**New File**: `WebUI/Components/Resources/PlayerTracking.razor.css`

```css
.player-tracking-panel {
    background: var(--game-bg-panel);
    border-radius: 5px;
    padding: 10px;
    margin-bottom: 10px;
}

.panel-header {
    color: var(--text-primary);
    font-size: 14px;
    font-weight: 600;
    margin-bottom: 8px;
    text-align: center;
}

.resource-cards-row {
    display: flex;
    flex-direction: row;
    gap: 4px;
    justify-content: space-between;
}

.tracked-resource-card {
    position: relative;
    flex: 1;
}

.harbor-indicator {
    position: absolute;
    top: 4px;
    right: 4px;
    font-family: var(--icon-font-family);
    font-size: 16px;
    color: var(--accent-primary);
    background: var(--overlay-darker);
    border-radius: 50%;
    width: 24px;
    height: 24px;
    display: flex;
    align-items: center;
    justify-content: center;
    pointer-events: none;
}
```

#### Integration into Game.razor

**File**: `WebUI/Pages/Game.razor`

Add below Board Measurement panel in left-panel div:

```razor
<!-- Board Measurements (only during PickingBoard) -->
@if (GameModel.GameState == GameState.PickingBoard)
{
    <div class="board-measurements" style="@GetCurrentPlayerGradient()">
        <BoardMeasurement ... />
    </div>
}

<!-- Player Tracking (always visible during gameplay) -->
@if (GameModel.GameState != GameState.PickingBoard && CurrentPlayer != null)
{
    <div class="player-tracking" style="@GetCurrentPlayerGradient()">
        <PlayerTracking GameModel="@GameModel"
                        CurrentPlayerId="@CurrentPlayer.Id" />
    </div>
}
```

### Implementation Steps

1. Create `PlayerTracking.razor` component with parameter definitions
2. Create `PlayerTracking.razor.css` with styling
3. Add component to `Game.razor` left panel with visibility conditional
4. Test with various resource counts and harbor ownership states
5. Verify ResourcesThisGame updates correctly via SignalR
6. Add XML documentation comments

## Section 3: Right Column - Players Panel

### Desktop Reference

**File**: `DesktopApp/Player/PlayerCtrl.xaml`
**Usage**: Individual player tile showing comprehensive game statistics

#### Visual Characteristics

- **Layout**: Vertical stack of player tiles (one per player)
- **Background**: Maple wood texture (`bmMaple` brush)
- **Border Radius**: 5px rounded corners
- **Margin**: 2px between tiles
- **Height**: Auto-sized based on content

#### Player Tile Structure

Each tile consists of two rows:

##### Row 1: Player Info and Stats

```text
┌────────────────────────────────────────────┐
│ [Avatar]  [Score] [Stats Grid...]         │
│  50x50     50x50   43x53 each (12 stats)  │
│  Circle    Circle  Rounded rectangles     │
└────────────────────────────────────────────┘
```

**Avatar** (50x50px circle):

- Player profile image (cropped to circle)
- Border: 1px using player foreground color
- Background: ImageBrush with player image

**Score Tile** (50x50px circle):

- Background: Player gradient colors
- Icon: &#xE907; (Score/Trophy glyph) from Catan font
- Count: Overlaid number in center
- Foreground: Player foreground color

**Stats Grid** (12 tiles, 43x53px each):

From `PlayerCtrl.xaml:76-94` and `PlayerStatsViewModel.cs:154-168`:

1. Score (shown separately, see above)
2. Roads Played (&#xE901;) - CatanFont.Road
3. Cities Played (&#xE903;) - CatanFont.City
4. Settlements Played (&#xE902;) - CatanFont.Settlement
5. Soldiers Played (&#xE904;) - CatanFont.Soldier
6. Resources Lost to Robber (&#xE905;) - CatanFont.Pirate
7. Times Targeted (&#xE906;) - CatanFont.Target
8. Total Resources (&#xE00E;) - CatanFont.Sum
9. Longest Road (&#xE908;) - CatanFont.LongestRoad
10. Good Rolls (&#xE909;) - CatanFont.GoodRoll
11. Bad Rolls (&#xE90A;) - CatanFont.BadRoll
12. Stars (&#xE734;) - CatanFont.Star

Each stat tile:

- Background: Player gradient when highlighted, transparent otherwise
- Foreground: Player foreground color when highlighted, gradient when not
- Top: Icon glyph (28px CatanFont)
- Bottom: Count number (18px)
- Corner Radius: 10px
- Margins: 1px between tiles

##### Row 2: Resources This Turn

```text
┌────────────────────────────────────────────┐
│  [Wheat] [Wood] [Sheep] [Brick] [Ore]     │
│   Resource cards showing turn grants       │
└────────────────────────────────────────────┘
```

**From**: `PlayerCtrl.xaml:96-103` using `TrackedResourcesCtrl`

- Same ResourceCard style as Board Measurement and Player Tracking
- Data source: `PlayerViewModel.ResourcesThisTurn` (from `PlayerModel.ResourcesThisTurn`)
- Visibility: Collapsed during Supplemental game state
- Height: 120px per card (Viewbox stretched)

### Proposed WebUI Implementation

#### Component Structure

**New Component**: `WebUI/Components/Players/PlayerTile.razor`

```razor
@using Catan3.Shared.Models
@using Catan3.Shared.Profiles
@using Catan3.WebUI.Components.Resources

<div class="player-tile" data-testid="player-tile-@Player.Id">
    <!-- Row 1: Player Info and Stats -->
    <div class="player-info-row">
        <!-- Avatar -->
        <div class="player-avatar" style="@GetAvatarStyle()">
            <img src="@GetPlayerImageUrl()" alt="@Player.Name" />
        </div>

        <!-- Stats Grid -->
        <div class="player-stats-grid">
            @foreach (var stat in GetPlayerStats())
            {
                <div class="stat-tile @(stat.IsHighlighted ? "highlighted" : "")"
                     style="@GetStatStyle(stat.IsHighlighted)"
                     data-testid="stat-@stat.Name">
                    <div class="stat-icon">@((MarkupString)stat.Glyph)</div>
                    <div class="stat-count">@stat.Count</div>
                </div>
            }
        </div>
    </div>

    <!-- Row 2: Resources This Turn -->
    @if (GameModel.GameState != GameState.Supplemental)
    {
        <div class="resources-this-turn-row">
            @foreach (var resourceType in GetTrackedResources())
            {
                var count = GetResourceThisTurn(resourceType);
                <div class="resource-card-wrapper">
                    <ResourceCard
                        Resource="@resourceType"
                        Count="@count"
                        IsSelected="false"
                        OnToggleSelection="@(() => Task.CompletedTask)" />
                </div>
            }
        </div>
    }
</div>

@code {
    [Parameter, EditorRequired]
    public PlayerModel Player { get; set; } = null!;

    [Parameter, EditorRequired]
    public GameModel GameModel { get; set; } = null!;

    [Parameter]
    public PlayerColors? PlayerColors { get; set; }

    private class PlayerStat
    {
        public required string Name { get; init; }
        public required string Glyph { get; init; }
        public required int Count { get; init; }
        public required bool IsHighlighted { get; init; }
    }

    private List<PlayerStat> GetPlayerStats()
    {
        // Note: Score is displayed as first stat tile
        return new List<PlayerStat>
        {
            new() { Name = "Score", Glyph = "&#xE907;", Count = Player.Score, IsHighlighted = Player.HighestScore },
            new() { Name = "Roads", Glyph = "&#xE901;", Count = GetRoadsPlayed(), IsHighlighted = false },
            new() { Name = "Cities", Glyph = "&#xE903;", Count = GetCitiesPlayed(), IsHighlighted = false },
            new() { Name = "Settlements", Glyph = "&#xE902;", Count = GetSettlementsPlayed(), IsHighlighted = false },
            new() { Name = "Soldiers", Glyph = "&#xE904;", Count = GetSoldiersPlayed(), IsHighlighted = Player.LargestArmy },
            new() { Name = "Robber", Glyph = "&#xE905;", Count = GetResourcesLost(), IsHighlighted = false },
            new() { Name = "Targeted", Glyph = "&#xE906;", Count = Player.TimesTargeted, IsHighlighted = false },
            new() { Name = "Total", Glyph = "&#xE00E;", Count = GetTotalResources(), IsHighlighted = false },
            new() { Name = "LongestRoad", Glyph = "&#xE908;", Count = Player.LongestRoad, IsHighlighted = Player.HasLongestRoad },
            new() { Name = "GoodRolls", Glyph = "&#xE909;", Count = Player.GoodRolls, IsHighlighted = false },
            new() { Name = "BadRolls", Glyph = "&#xE90A;", Count = Player.BadRolls, IsHighlighted = false },
            new() { Name = "Stars", Glyph = "&#xE734;", Count = Player.Stars, IsHighlighted = false }
        };
    }

    private string GetAvatarStyle()
    {
        if (PlayerColors == null) return "";
        return $"border: 1px solid {PlayerColors.Foreground};";
    }

    private string GetStatStyle(bool isHighlighted)
    {
        if (PlayerColors == null) return "";

        if (isHighlighted)
        {
            return $"background: {PlayerColors.CssGradient}; color: {PlayerColors.Foreground};";
        }
        else
        {
            return $"background: transparent; color: {PlayerColors.CssGradient};";
        }
    }

    private string GetPlayerImageUrl()
    {
        // TODO: Implement player profile image loading
        // For now, return default guest image from shared assets
        return "/shared/players/guest.png";
    }

    private ResourceType[] GetTrackedResources()
    {
        return new[]
        {
            ResourceType.Wheat,
            ResourceType.Wood,
            ResourceType.Sheep,
            ResourceType.Brick,
            ResourceType.Ore
        };
    }

    private int GetResourceThisTurn(ResourceType resource)
    {
        return resource switch
        {
            ResourceType.Wheat => Player.ResourcesThisTurn.Wheat,
            ResourceType.Wood => Player.ResourcesThisTurn.Wood,
            ResourceType.Sheep => Player.ResourcesThisTurn.Sheep,
            ResourceType.Brick => Player.ResourcesThisTurn.Brick,
            ResourceType.Ore => Player.ResourcesThisTurn.Ore,
            _ => 0
        };
    }

    // Helper methods to calculate stats from GameModel
    private int GetRoadsPlayed() =>
        GameModel.Roads.Count(r => r.OwnerId == Player.Id);

    private int GetCitiesPlayed() =>
        GameModel.Buildings.Count(b => b.OwnerId == Player.Id && b.BuildingType == BuildingType.City);

    private int GetSettlementsPlayed() =>
        GameModel.Buildings.Count(b => b.OwnerId == Player.Id && b.BuildingType == BuildingType.Settlement);

    private int GetSoldiersPlayed()
    {
        // TODO: Implement soldier tracking from development cards
        return 0;
    }

    private int GetResourcesLost()
    {
        // TODO: Implement robber resource tracking
        return 0;
    }

    private int GetTotalResources()
    {
        var r = Player.ResourcesThisGame;
        return r.Wheat + r.Wood + r.Sheep + r.Brick + r.Ore;
    }
}
```

#### CSS Styling

**New File**: `WebUI/Components/Players/PlayerTile.razor.css`

```css
.player-tile {
    background: url('/themes/base/backgrounds/maple.jpg') center/cover;
    border-radius: 5px;
    margin: 2px;
    padding: 5px;
    visibility: visible;
}

.player-info-row {
    display: flex;
    flex-direction: row;
    gap: 4px;
    align-items: flex-start;
    margin-bottom: 5px;
}

.player-avatar {
    width: 50px;
    height: 50px;
    border-radius: 50%;
    overflow: hidden;
    flex-shrink: 0;
}

.player-avatar img {
    width: 100%;
    height: 100%;
    object-fit: cover;
}

.player-stats-grid {
    display: grid;
    grid-template-columns: repeat(12, 43px);
    grid-template-rows: 53px;
    gap: 1px;
    flex: 1;
}

.stat-tile {
    width: 43px;
    height: 53px;
    border-radius: 10px;
    display: flex;
    flex-direction: column;
    align-items: center;
    justify-content: space-between;
    padding: 4px 0 6px 0;
}

.stat-tile.highlighted {
    /* Background and color set via inline style for gradient */
}

.stat-icon {
    font-family: var(--icon-font-family);
    font-size: 28px;
    line-height: 28px;
    text-align: center;
}

.stat-count {
    font-size: 18px;
    line-height: 18px;
    text-align: center;
}

.resources-this-turn-row {
    display: flex;
    flex-direction: row;
    gap: 3px;
    padding: 0;
    margin: 0;
}

.resource-card-wrapper {
    flex: 1;
    max-height: 120px;
}
```

#### Parent Container Component

**New Component**: `WebUI/Components/Players/PlayersPanel.razor`

```razor
@using Catan3.Shared.Models
@using Catan3.Shared.Profiles

<div class="players-panel" data-testid="players-panel">
    @foreach (var player in GameModel.Players)
    {
        var playerColors = GetPlayerColors(player.Id);
        <PlayerTile Player="@player"
                    GameModel="@GameModel"
                    PlayerColors="@playerColors" />
    }
</div>

@code {
    [Parameter, EditorRequired]
    public GameModel GameModel { get; set; } = null!;

    [Parameter, EditorRequired]
    public Dictionary<string, PlayerColors> PlayerColorMap { get; set; } = new();

    private PlayerColors? GetPlayerColors(string playerId)
    {
        return PlayerColorMap.TryGetValue(playerId, out var colors) ? colors : null;
    }
}
```

**CSS File**: `WebUI/Components/Players/PlayersPanel.razor.css`

```css
.players-panel {
    display: flex;
    flex-direction: column;
    gap: 0;
    padding: 0;
    overflow-y: auto;
    height: 100%;
}
```

#### Integration into Game.razor

**File**: `WebUI/Pages/Game.razor`

Replace existing simple player list in right panel:

```razor
<!-- RIGHT PANEL -->
<div class="right-panel">
    <PlayersPanel GameModel="@GameModel"
                  PlayerColorMap="@PlayerColorMap" />
</div>
```

Add to `@code` block:

```csharp
/// <summary>
/// Dictionary mapping player IDs to their PlayerColors for gradient display.
/// Populated during OnInitializedAsync when loading player profiles.
/// </summary>
private Dictionary<string, PlayerColors> PlayerColorMap { get; set; } = new();
```

Update `LoadPlayerProfiles` method:

```csharp
private async Task LoadPlayerProfiles()
{
    if (GameModel == null) return;

    foreach (var player in GameModel.Players)
    {
        try
        {
            var response = await Http.GetFromJsonAsync<PlayerData>($"{Config.BaseUrl}/api/players/{player.Id}");
            if (response != null)
            {
                var colors = new PlayerColors(
                    response.Id,
                    response.PrimaryBackgroundColor,
                    response.SecondaryBackgroundColor,
                    response.ForegroundColor
                );
                PlayerColorMap[player.Id] = colors;
            }
        }
        catch (Exception ex)
        {
            Log.Error($"Failed to load profile for player {player.Id}: {ex.Message}");
        }
    }

    StateHasChanged();
}
```

### Implementation Steps

1. Create `PlayerTile.razor` component with full stat calculations
2. Create `PlayerTile.razor.css` with Desktop-matching styling
3. Create `PlayersPanel.razor` container component
4. Update `Game.razor` to use PlayersPanel in right column
5. Update `Game.razor` PlayerColorMap population logic
6. Add maple.jpg texture to `wwwroot/images/textures/`
7. Test with 3-4 player games
8. Verify stat highlighting (HighestScore, LongestRoad, LargestArmy)
9. Verify ResourcesThisTurn updates correctly
10. Add XML documentation comments

## Section 4: CSS Variables and Theming

### Required CSS Variables

**File**: `WebUI/wwwroot/css/app.css`

Add to `:root` section:

```css
:root {
    /* ... existing variables ... */

    /* Panel backgrounds */
    --game-bg-panel: rgba(20, 20, 20, 0.85);
    --game-bg-panel-hover: rgba(30, 30, 30, 0.9);

    /* Player tile backgrounds */
    --player-tile-bg: url('/themes/base/backgrounds/maple.jpg');

    /* Stat tile styling */
    --stat-tile-radius: 10px;
    --stat-tile-padding: 4px 0 6px 0;
    --stat-tile-highlighted-opacity: 1.0;
    --stat-tile-normal-opacity: 0.7;
}
```

### Icon Font Constants

**File**: `WebUI/wwwroot/css/app.css`

Document Segoe MDL2 Assets icon codes:

```css
/* Icon codes from Catan Desktop font mappings:
 * Score/Trophy: &#xE907;
 * Road: &#xE901;
 * City: &#xE903;
 * Settlement: &#xE902;
 * Soldier: &#xE904;
 * Pirate/Robber: &#xE905;
 * Target: &#xE906;
 * Sum: &#xE00E;
 * Longest Road: &#xE908;
 * Good Roll: &#xE909;
 * Bad Roll: &#xE90A;
 * Star: &#xE734;
 * Harbor: &#xE128;
 */
```

## Section 5: Data Flow Architecture

### SignalR Update Flow

```text
GameService
    │
    │ SignalR: GameStateUpdated
    ↓
Game.razor (HandleGameStateUpdated)
    │
    ├─→ Update GameModel
    ├─→ Trigger StateHasChanged()
    ↓
Child Components (Blazor cascading)
    │
    ├─→ BoardMeasurement (reads GameModel.Players[current].ResourcesThisGame)
    ├─→ PlayerTracking (reads GameModel.Players[current].ResourcesThisGame)
    └─→ PlayersPanel → PlayerTile[] (reads GameModel.Players[all])
            │
            ├─→ Stats calculated from GameModel.Buildings, Roads, etc.
            └─→ ResourcesThisTurn from PlayerModel.ResourcesThisTurn
```

### Component Parameter Dependencies

#### PlayerTracking Component

- **GameModel**: Full game state (passed from Game.razor)
- **CurrentPlayerId**: ID of current player (passed from Game.razor)
- **Reads**: `GameModel.Players[current].ResourcesThisGame`
- **Reads**: `GameModel.Players[current].OwnedHarbors`

#### PlayerTile Component

- **Player**: Individual PlayerModel (passed from PlayersPanel)
- **GameModel**: Full game state (for stat calculations)
- **PlayerColors**: Gradient colors (passed from PlayersPanel via PlayerColorMap)
- **Reads**: `Player.ResourcesThisTurn`, `Player.ResourcesThisGame`
- **Calculates**: Roads/Cities/Settlements from GameModel collections

#### PlayersPanel Component

- **GameModel**: Full game state (passed from Game.razor)
- **PlayerColorMap**: Dictionary<string, PlayerColors> (passed from Game.razor)
- **Iterates**: GameModel.Players to create PlayerTile for each

### State Management

All state is managed in `Game.razor`:

- `GameModel` - Updated via SignalR HandleGameStateUpdated
- `PlayerColorMap` - Loaded once during OnInitializedAsync via HTTP API
- `CurrentPlayer` - Computed from GameModel.CurrentPlayerIndex

Child components are **stateless presentational components** that receive data via `[Parameter]` and invoke `EventCallback` for user actions.

## Section 6: Missing Assets

### Required Images (COMPLETED)

Assets have been migrated to the theme system:

1. **Maple Wood Texture**: `wwwroot/themes/base/backgrounds/maple.jpg`
   - ✅ Copied and organized under theme system
   - Usage: Player tile background

2. **Default Player Images**: `wwwroot/shared/players/`
   - Note: Player avatars are NOT themed (user-specific, not themed content)
   - Files: guest.png, joe.png, ryan.png, adrian.png, dndav.png
   - Usage: Player avatar fallbacks

### Asset Locations

```text
# Theme-managed assets (use IAssetService)
WebUI/wwwroot/themes/base/backgrounds/maple.jpg
WebUI/wwwroot/themes/base/backgrounds/cherry.jpg

# Non-themed shared assets (direct paths)
WebUI/wwwroot/shared/players/*.png
```

## Section 7: Testing Strategy

### Visual Regression Testing

1. **Layout Testing** (multiple viewport sizes):
   - 1920x1080 (primary target)
   - 2560x1440 (widescreen)
   - 1366x768 (minimum supported)

2. **Component Visual Testing**:
   - Player Tracking panel displays all 5 resources
   - Harbor indicators appear correctly
   - Player tiles show all 12 stats
   - Score tile displays correctly (circle with icon + number)
   - Resources This Turn row displays correctly

3. **Gradient Testing**:
   - All 4 players display with correct gradient backgrounds
   - Stat highlighting uses correct foreground/background swap
   - Panel headers use current player gradient

### Functional Testing

1. **Data Binding**:
   - ResourcesThisGame updates when game progresses
   - ResourcesThisTurn clears between turns
   - Player stats update when buildings/roads placed
   - Harbor indicators appear when harbors acquired

2. **State Visibility**:
   - Player Tracking shows during gameplay (not PickingBoard)
   - Board Measurement shows during PickingBoard only
   - Resources This Turn row hidden during Supplemental state

3. **Performance**:
   - SignalR updates render smoothly
   - No layout thrashing during state changes
   - Scrolling in right panel (player tiles) is smooth

## Section 8: Implementation Phases

### Phase 1: Board Layout Optimization (2-3 hours)

**Goal**: Fix widescreen layout inefficiency

1. Update `Game.razor.css` grid-template-columns
2. Add min/max width constraints to panels
3. Update board scaling CSS
4. Test at multiple resolutions
5. Verify no regressions in existing features

**Deliverables**:

- Updated `Game.razor.css`
- Visual confirmation at 3 viewport sizes
- Build succeeds with no errors

### Phase 2: Player Tracking Component (3-4 hours)

**Goal**: Add ResourcesThisGame display to left panel

1. Create `PlayerTracking.razor` component
2. Create `PlayerTracking.razor.css` styling
3. Integrate into `Game.razor` left panel
4. Test with harbor ownership
5. Verify SignalR updates work correctly

**Deliverables**:

- `WebUI/Components/Resources/PlayerTracking.razor`
- `WebUI/Components/Resources/PlayerTracking.razor.css`
- Updated `Game.razor` with component integration
- XML documentation comments
- Build succeeds with no errors

### Phase 3: Player Tile Component (5-6 hours)

**Goal**: Create rich player stat display for right panel

1. Create `PlayerTile.razor` component
2. Implement all 12 stat calculations
3. Create `PlayerTile.razor.css` styling
4. Add Resources This Turn row
5. Test highlighting logic (score, longest road, largest army)

**Deliverables**:

- `WebUI/Components/Players/PlayerTile.razor`
- `WebUI/Components/Players/PlayerTile.razor.css`
- XML documentation comments
- Build succeeds with no errors

### Phase 4: Players Panel Integration (2-3 hours)

**Goal**: Replace simple player list with rich panel

1. Create `PlayersPanel.razor` container
2. Update `Game.razor` PlayerColorMap logic
3. Copy required texture/image assets
4. Test with 3-4 player games
5. Verify scrolling and layout

**Deliverables**:

- `WebUI/Components/Players/PlayersPanel.razor`
- `WebUI/Components/Players/PlayersPanel.razor.css`
- Updated `Game.razor` right panel
- Asset files copied to wwwroot
- Build succeeds with no errors

### Phase 5: Testing and Refinement (3-4 hours)

**Goal**: Visual parity verification and bug fixes

1. Side-by-side Desktop/WebUI comparison
2. Test all game states and transitions
3. Test with multiple players (different colors)
4. Performance testing (SignalR update latency)
5. Fix any visual discrepancies

**Deliverables**:

- Test results document
- Bug fixes committed
- Visual parity screenshots
- Performance metrics recorded

## Section 9: Open Questions

### Player Profile Images

**Question**: How should WebUI load player profile images?

**Options**:

1. **Option A**: Add GameService API endpoint `/api/players/{id}/image`
   - Pros: Centralized, follows REST pattern
   - Cons: Extra HTTP requests per player

2. **Option B**: Embed image URLs in PlayerData JSON
   - Pros: Single HTTP request includes image URL
   - Cons: Image data in JSON payload

3. **Option C**: Store images in WebUI wwwroot and reference by player ID
   - Pros: No HTTP requests, fast loading
   - Cons: Requires manual sync between Desktop and WebUI assets

**Recommendation**: Option B - include ImageUri in PlayerData API response

### Development Card Tracking

**Question**: How to track soldier cards played and other dev card stats?

**Current State**: GameModel doesn't expose development card history

**Recommendation**: Add to future iteration when development card system implemented

### Robber Resource Loss Tracking

**Question**: How to track resources lost to robber per player?

**Current State**: Not tracked in GameModel

**Recommendation**: Add to future iteration, requires GameStateMachine enhancement

## Section 10: Success Criteria

### Visual Parity Checklist

- [ ] Board scales correctly on widescreen (max-height constraint)
- [ ] Left panel width: 250-300px (adequate for Player Tracking)
- [ ] Right panel width: 800-1000px (adequate for player tiles)
- [ ] Player Tracking displays ResourcesThisGame correctly
- [ ] Harbor indicators appear on owned harbors
- [ ] Player tiles match Desktop layout (avatar + stats + resources)
- [ ] All 12 stats display with correct icons
- [ ] Score tile displays as circle (not rectangle)
- [ ] Stat highlighting works (HighestScore, LongestRoad, LargestArmy)
- [ ] Resources This Turn displays correctly
- [ ] Resources This Turn hidden during Supplemental state
- [ ] Player gradients apply correctly to all panels
- [ ] Maple texture background on player tiles
- [ ] All fonts/icons match Desktop (Segoe MDL2 Assets)

### Functional Parity Checklist

- [ ] SignalR updates trigger component re-renders
- [ ] ResourcesThisGame updates accumulate correctly
- [ ] ResourcesThisTurn clears between turns
- [ ] Player stats update when buildings/roads placed
- [ ] Harbor indicators update when harbors acquired
- [ ] Scrolling in Players panel works smoothly
- [ ] Layout responsive to viewport size changes
- [ ] No console errors or warnings
- [ ] Build succeeds with no errors
- [ ] All components have XML documentation

## Appendix A: Desktop XAML Reference Mappings

### PlayerCtrl.xaml Key Elements

| XAML Element | Line | WebUI Equivalent | Notes |
|--------------|------|------------------|-------|
| Grid.Background | 60 | .player-tile CSS | bmMaple brush → maple.jpg URL |
| Grid CornerRadius | 66-73 | .player-avatar CSS | 25px border-radius (50px / 2) |
| GridView (Stats) | 76-94 | .player-stats-grid CSS | 12-column grid, 43x53px cells |
| DataTemplate (Score) | 36-58 | .stat-tile.score | Circular background, 50x50px |
| DataTemplate (Normal) | 14-35 | .stat-tile | Rectangular, 43x53px |
| TrackedResourcesCtrl | 100-101 | Resources This Turn row | Height 120px, Viewbox stretch |

### PlayerStatsViewModel.cs Key Properties

| Property | Type | WebUI Equivalent | Usage |
|----------|------|------------------|-------|
| Count | int | stat.Count | Numeric value displayed |
| Glyph | string | stat.Glyph | Icon HTML entity code |
| Highlighted | bool | stat.IsHighlighted | CSS class toggle |
| PlayerColors | PlayerColorViewModel | PlayerColors parameter | Gradient and foreground |

### StatTemplate.cs Glyph Mappings

| Stat Name | Glyph Constant | HTML Entity | Icon |
|-----------|----------------|-------------|------|
| Score | CatanFont.Score | &#xE907; | Trophy |
| RoadsPlayed | CatanFont.Road | &#xE901; | Road segment |
| CitiesPlayed | CatanFont.City | &#xE903; | City building |
| SettlementsPlayed | CatanFont.Settlement | &#xE902; | House |
| SoldierPlayed | CatanFont.Soldier | &#xE904; | Knight helmet |
| ResourcesLostToRobber | CatanFont.Pirate | &#xE905; | Skull |
| TimesTargeted | CatanFont.Target | &#xE906; | Crosshair |
| TotalResources | CatanFont.Sum | &#xE00E; | Sigma (Σ) |
| LongestRoad | CatanFont.LongestRoad | &#xE908; | Road with star |
| GoodRolls | CatanFont.GoodRoll | &#xE909; | Thumbs up |
| BadRolls | CatanFont.BadRoll | &#xE90A; | Thumbs down |
| Stars | CatanFont.Star | &#xE734; | Star |

## Appendix B: File Checklist

### New Files to Create

```text
WebUI/Components/Resources/PlayerTracking.razor
WebUI/Components/Resources/PlayerTracking.razor.css
WebUI/Components/Players/PlayerTile.razor
WebUI/Components/Players/PlayerTile.razor.css
WebUI/Components/Players/PlayersPanel.razor
WebUI/Components/Players/PlayersPanel.razor.css
```

### Files to Modify

```text
WebUI/Pages/Game.razor
WebUI/Pages/Game.razor.css
WebUI/wwwroot/css/app.css
```

### Assets (MIGRATED)

Assets have been migrated to theme system. See `WebUI/wwwroot/themes/base/` for themed assets and `WebUI/wwwroot/shared/` for non-themed assets like player avatars.

### Documentation to Update

```text
design_docs/visual-design.md (this document)
.ai/sessions/SESSION_SUMMARY-{date}.md (after implementation)
```

## Revision History

| Date | Version | Author | Changes |
|------|---------|--------|---------|
| 2025-11-29 | 1.0 | AI Assistant | Initial comprehensive design document |
