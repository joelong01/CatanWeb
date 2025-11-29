# WebUI Left Panel Refinement Plan

**Date:** 2025-11-27
**Status:** Ready for implementation
**Reference Images:** `.test_images/desktop first column.jpg`, `.test_images/web-firstcolumn.jpg`

## Overview

Refine the WebUI Game.razor left panel to match Desktop layout and styling, removing unnecessary headers and borders, improving control layouts,
and implementing proper roll display with CatanNumber rendering.

## Visual Comparison

### Current WebUI Issues

1. **Vertical separators** - Purple bordered boxes around each section create visual clutter
2. **All-caps headers** - "GAME CONTROLS", "PURCHASE CONTROLS", "ROLL ENTRY (2-12)", "BOARD MEASUREMENTS" not in Desktop
3. **Purchase buttons** - 2x2 grid, should be 1x4 row (card-shaped)
4. **Roll display** - Structure correct but missing CatanNumber rendering and percentage display
5. **Board measurements** - Layout differs from Desktop (resource cards, star circles arrangement)

### Desktop Reference

1. **No section borders** - Controls flow naturally without visual separators
2. **No headers** - Clean layout without section labels
3. **Purchase buttons** - Single row of 4 card-shaped buttons (Road, Settlement, City, Dev Card)
4. **Roll display** - 3 columns × 4 rows with:
   - Top: Roll count (starts at 0)
   - Middle: CatanNumber (identical to tile number tokens, includes 7)
   - Bottom: Percentage (0% initially)
5. **Board measurements** - Resource cards on top row, star circles (13,12,11,10) on second row

## Implementation Tasks

### 1. Remove Visual Clutter

**File:** `WebUI/Pages/Game.razor`

**Changes:**

- Remove `.panel-section` background and border styles
- Remove `.panel-label` headers from markup
- Set `.left-panel` and `.right-panel` background to transparent (controls blend with page background)

**CSS Updates:**

```css
.panel-section {
    /* Remove: border-radius, background, padding */
    /* Keep: display: flex, flex-direction: column */
}

.panel-label {
    /* Remove entire style or set display: none */
}
```

**Markup Updates:**

```razor
<!-- Before -->
<div class="panel-section game-controls">
    <div class="panel-label">GAME CONTROLS</div>
    <div class="panel-content">...</div>
</div>

<!-- After -->
<div class="game-controls">
    <!-- Controls directly, no wrapper -->
</div>
```

### 2. Purchase Buttons - Single Row Layout

**File:** `WebUI/Pages/Game.razor`

**Current:** 2x2 grid (Road, Settlement / City, Dev Card)
**Target:** 1x4 row (Road, Settlement, City, Dev Card)

**CSS Update:**

```css
.purchase-grid {
    display: grid;
    grid-template-columns: repeat(4, 1fr); /* Change from repeat(2, 1fr) */
    gap: 5px;
}
```

**Future Enhancement:** Render as card images (like Desktop) instead of text buttons

### 3. Roll Display - CatanNumber Rendering

**File:** `WebUI/Pages/Game.razor`

**Current Structure:**

```razor
<div class="roll-grid">
    @for (int roll = 2; roll <= 12; roll++)
    {
        <div class="roll-button @(IsHighProbRoll(roll) ? "high-prob" : "")">
            <span class="roll-number">@roll</span>
            <span class="roll-pips">@GetRollPips(roll)</span>
        </div>
    }
</div>
```

**Target Structure:**

```razor
<div class="roll-grid">
    @for (int roll = 2; roll <= 12; roll++)
    {
        <div class="roll-cell">
            <div class="roll-count">@GetRollCount(roll)</div>
            <div class="roll-catan-number">
                @((MarkupString)RenderCatanNumberSvg(roll))
            </div>
            <div class="roll-percentage">@GetRollPercentage(roll)</div>
        </div>
    }
</div>
```

**New C# Methods:**

```csharp
private string GetRollCount(int roll)
{
    if (GameModel?.GameRollModel?.RollCounts == null) return "0";
    var index = roll - 2; // RollCounts array is 0-indexed for rolls 2-12
    return GameModel.GameRollModel.RollCounts[index].ToString();
}

private string GetRollPercentage(int roll)
{
    if (GameModel?.GameRollModel == null) return "0%";
    var rollCounts = GameModel.GameRollModel.RollCounts;
    var totalRolls = GameModel.GameRollModel.TotalRolls;

    if (totalRolls == 0) return "0%";

    var index = roll - 2;
    var count = rollCounts[index];
    var percent = (double)count / (double)totalRolls * 100;
    return $"{Math.Round(percent, 2)}%";
}

private string RenderCatanNumberSvg(int number)
{
    // Reuse TileSvgRenderer.RenderNumberToken logic
    // Create standalone SVG element showing number token
    var isHighProb = number == 6 || number == 8;
    var numberColor = isHighProb ? "#DC143C" : "white";
    var pips = GetPips(number);

    var radius = 30;
    var centerX = radius;
    var centerY = radius;
    var svgSize = radius * 2;

    var svg = new StringBuilder();
    svg.AppendLine($@"<svg width=""{svgSize}"" height=""{svgSize}"" viewBox=""0 0 {svgSize} {svgSize}"">");
    svg.AppendLine($@"  <circle cx=""{centerX}"" cy=""{centerY}"" r=""{radius}"" fill=""#2B4F81"" opacity=""0.9"" stroke=""black"" stroke-width=""2""/>");
    svg.AppendLine($@"  <text x=""{centerX}"" y=""{centerY - 10}"" text-anchor=""middle"" dominant-baseline=""middle"" font-family=""sans-serif"" font-size=""24"" font-weight=""bold"" fill=""{numberColor}"">{number}</text>");

    if (!string.IsNullOrEmpty(pips))
    {
        svg.AppendLine($@"  <text x=""{centerX}"" y=""{centerY + 10}"" text-anchor=""middle"" font-size=""12"" fill=""{numberColor}"">{pips}</text>");
    }

    svg.AppendLine("</svg>");
    return svg.ToString();
}

private string GetPips(int number)
{
    return number switch
    {
        2 or 12 => "★",
        3 or 11 => "★★",
        4 or 10 => "★★★",
        5 or 9 => "★★★★",
        6 or 8 => "★★★★★",
        _ => ""  // 7 has no pips
    };
}
```

**Reference:** `DesktopApp/Rolls/RollViewModel.cs` lines 141-165 for percentage calculation
**Reference:** `WebUI/Services/Rendering/TileSvgRenderer.cs` lines 94-113 for number token rendering

**CSS Updates:**

```css
.roll-grid {
    display: grid;
    grid-template-columns: repeat(3, 1fr);
    grid-template-rows: repeat(4, 1fr);
    gap: 5px;
}

.roll-cell {
    display: flex;
    flex-direction: column;
    align-items: center;
    gap: 2px;
    padding: 5px;
    background: var(--overlay-darker);
    border-radius: 4px;
}

.roll-count {
    font-size: 11px;
    color: var(--text-secondary);
}

.roll-catan-number {
    /* SVG will size itself */
}

.roll-percentage {
    font-size: 10px;
    color: var(--text-secondary);
}
```

### 4. Board Measurements - Resource Cards & Star Circles

**File:** `WebUI/Components/Board/BoardMeasurement.razor`

**Current Layout Issues:**

- Resource cards shown as small thumbnails with counts
- Count shown is tile count, not star count
- No checkbox filter support
- Star circles below but layout differs from Desktop

**Target Layout:**

```
[Resource Cards Row - Wheat Wood Sheep Brick Ore with star counts]
[Star Circles Row - 13, 12, 11, 10 with building counts]
[Previous Board] [Shuffle]
[Slider: Show Buildings (Stars) 0-14]
```

**Reference:** `DesktopApp/Resources/BoardMeasurementCtrl.xaml` lines 41-134

**Key Desktop Patterns:**

1. **Resource Cards** (lines 41-63):
   - GridView with SelectionMode="Multiple"
   - Checkbox filtering on upper right (SelectionChanged event)
   - Count shown is star count for that resource type (not tile count)
   - Items: GameViewModel.StarsResourceViewModel.ResourceCounters

2. **Star Circles** (lines 64-104):
   - 4 circles showing 13, 12, 11, 10
   - Each shows: star count threshold (13) and building count at that threshold
   - Binding: `BIND_StarCount(13, GameViewModel.GameModel.Tiles)`
   - Background: ResourceCard.GoldMine (gold color)
   - Size: 80x80 circles

3. **Buttons** (lines 106-128):
   - Previous Board (Undo) and Shuffle
   - No "Accept Board" button (that's in Game Controls section)

4. **Slider** (lines 130-133):
   - Range: 0-14
   - Binding: `GameViewModel.ShownStars` (TwoWay)
   - Controls building visibility on board

**Markup Changes:**

```razor
<!-- Resource Cards Row -->
<div class="resource-cards-row">
    @foreach (var resource in new[] { ResourceType.Wheat, ResourceType.Wood, ResourceType.Sheep, ResourceType.Brick, ResourceType.Ore })
    {
        <ResourceCard
            Resource="@resource"
            Count="@GetResourceStarCount(resource)"
            IsSelected="@IsResourceSelected(resource)"
            OnClick="@(() => ToggleResourceSelection(resource))" />
    }
</div>

<!-- Star Circles Row -->
<div class="star-circles-row">
    @foreach (var threshold in new[] { 13, 12, 11, 10 })
    {
        <div class="star-circle">
            <div class="star-display">★★★★★</div>
            <div class="threshold-number">@threshold</div>
            <div class="building-count">@GetStarCount(threshold)</div>
        </div>
    }
</div>

<!-- Control Buttons -->
<div class="board-controls">
    <button @onclick="OnUndo" class="board-btn">
        <span class="btn-icon">&#xE10E;</span>
        <span class="btn-label">Previous Board</span>
    </button>
    <button @onclick="OnShuffle" class="board-btn">
        <span class="btn-icon">&#xE10D;</span>
        <span class="btn-label">Shuffle</span>
    </button>
</div>

<!-- Slider -->
<div class="star-slider">
    <input type="range" min="0" max="14" step="1"
           value="@ShownStars"
           @oninput="@((e) => HandleShownStarsChanged(int.Parse(e.Value?.ToString() ?? "0")))" />
    <div class="slider-label">Show Buildings (Stars): @ShownStars</div>
</div>
```

**New C# Methods:**

```csharp
private int GetResourceStarCount(ResourceType resource)
{
    // Count buildings adjacent to tiles with this resource type
    // Sum the stars of adjacent tiles for each building
    if (GameModel?.Buildings == null) return 0;

    int count = 0;
    foreach (var building in GameModel.Buildings)
    {
        var adjacentTiles = GameModel.TilesForBuildings(building.BuildingKey);
        var hasResource = adjacentTiles.Any(t => t.ResourceTileType == resource);
        if (hasResource)
        {
            count += adjacentTiles.Stars();
        }
    }
    return count;
}

private bool IsResourceSelected(ResourceType resource)
{
    // Track selected resources for filtering
    return _selectedResources.Contains(resource);
}

private void ToggleResourceSelection(ResourceType resource)
{
    if (_selectedResources.Contains(resource))
        _selectedResources.Remove(resource);
    else
        _selectedResources.Add(resource);
    StateHasChanged();
}
```

**CSS Updates:**

```css
.resource-cards-row {
    display: flex;
    gap: 5px;
    justify-content: center;
    margin-bottom: 10px;
}

.star-circles-row {
    display: flex;
    gap: 6px;
    justify-content: center;
    margin-bottom: 20px;
}

.star-circle {
    width: 80px;
    height: 80px;
    border-radius: 50%;
    background: linear-gradient(135deg, #DAA520 0%, #FFD700 100%); /* Gold gradient */
    display: flex;
    flex-direction: column;
    align-items: center;
    justify-content: center;
    position: relative;
}

.star-display {
    position: absolute;
    top: 10px;
    font-size: 14px;
    color: red;
}

.threshold-number {
    font-size: 20px;
    font-weight: bold;
    color: white;
}

.building-count {
    position: absolute;
    bottom: 10px;
    font-size: 20px;
    font-weight: bold;
    color: white;
}

.board-controls {
    display: flex;
    gap: 20px;
    justify-content: center;
    margin-bottom: 20px;
}

.board-btn {
    display: flex;
    flex-direction: column;
    align-items: center;
    gap: 5px;
    background: transparent;
    border: none;
    color: white;
    cursor: pointer;
}

.star-slider {
    display: flex;
    flex-direction: column;
    align-items: center;
    gap: 5px;
}

.slider-label {
    font-size: 11px;
    color: var(--text-secondary);
}
```

## Implementation Order

1. **Remove visual clutter** - Quick wins, immediate visual improvement
2. **Purchase buttons layout** - Simple CSS change
3. **Roll display with CatanNumber** - Moderate complexity, reuse existing rendering code
4. **Board measurements refinement** - Most complex, affects multiple files

## Testing Checklist

- [ ] Left panel has no section borders or headers
- [ ] Purchase buttons in single row (4 buttons)
- [ ] Roll display shows: count, CatanNumber, percentage
- [ ] Roll percentages calculate correctly (count/total × 100)
- [ ] CatanNumber SVG matches tile number tokens (including red 6/8)
- [ ] Roll 7 shows with no pips
- [ ] Board measurements has resource cards in top row
- [ ] Resource card counts show star totals (not tile counts)
- [ ] Star circles show 13, 12, 11, 10 with correct building counts
- [ ] Slider updates ShownStars and filters board rendering
- [ ] Previous Board and Shuffle buttons work

## Files to Modify

1. `WebUI/Pages/Game.razor` - Main layout, remove headers/borders, purchase grid, roll display
2. `WebUI/Components/Board/BoardMeasurement.razor` - Resource cards, star circles, slider
3. `WebUI/Components/Resources/ResourceCard.razor` - Support click events, selection state
4. `Catan3.Shared/Models/GameRollModel.cs` - Verify RollCounts and TotalRolls properties exist

## References

- Desktop XAML: `DesktopApp/Resources/BoardMeasurementCtrl.xaml`
- Roll ViewModel: `DesktopApp/Rolls/RollViewModel.cs`
- Tile Renderer: `WebUI/Services/Rendering/TileSvgRenderer.cs`
- Test Images: `.test_images/desktop first column.jpg`, `.test_images/rolls example.jpg`
