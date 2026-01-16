# Board Measurement Implementation Plan

**Date:** 2025-11-28
**Related Docs:**

- Design: [`board-measurement-design.md`](./board-measurement-design.md)
- Call Flow: [`board-measurement-call-flow.md`](./board-measurement-call-flow.md)

---

## Issues Found

### Issue #1: Missing gradient-current-player Definition

**Priority:** CRITICAL
**Status:** 🔴 Blocking functionality

**Location:**

- `WebUI/Services/Rendering/BuildingSvgRenderer.cs:126`
- `WebUI/Services/Rendering/BoardSvgGenerator.cs:314-324`

**Problem:**

```csharp
// BuildingSvgRenderer.cs:126
var gradientId = "gradient-current-player";  // ← NOT DEFINED!

// Generated SVG references non-existent gradient
sb.AppendLine($@"<circle cx=""{x}"" cy=""{y}"" r=""{radius}""
                fill=""url(#gradient-current-player)""  // ← BROKEN!
                stroke=""{currentPlayerColors.Foreground}""
                stroke-width=""2""/>");
```

**Root Cause:**

- `GeneratePlayerGradients()` only creates gradients for players in the game
- Uses pattern: `gradient-{playerId}` (e.g., `gradient-joe-001`)
- Never creates the special `gradient-current-player` gradient
- Buildings in `Stars` visual state reference this missing gradient

**Impact:**

- Star buildings render with black/transparent fill instead of colored gradient
- Visual inconsistency with Desktop implementation
- Poor UX during board picking phase

**Desktop Behavior:**

- Buildings in Stars state use current player's gradient colors
- Gradient created from `currentPlayer.PlayerColors.BackgroundBrush`
- Provides visual feedback of "your potential settlement sites"

**Proposed Fix:**

**Option A: Add gradient-current-player to defs (RECOMMENDED)**

```csharp
// BoardSvgGenerator.cs - Update GeneratePlayerGradients()
private static void GeneratePlayerGradients(
    StringBuilder sb,
    IReadOnlyDictionary<string, PlayerViewModel> playerLookup,
    PlayerViewModel? currentPlayer)  // ← Add parameter
{
    // Generate current player gradient (for Stars state)
    if (currentPlayer != null)
    {
        sb.AppendLine($@"    <linearGradient id=""gradient-current-player"" x1=""0%"" y1=""0%"" x2=""100%"" y2=""100%"">");
        sb.AppendLine($@"      <stop offset=""0%"" style=""stop-color:{currentPlayer.Colors.Primary};stop-opacity:1"" />");
        sb.AppendLine($@"      <stop offset=""100%"" style=""stop-color:{currentPlayer.Colors.Secondary};stop-opacity:1"" />");
        sb.AppendLine("    </linearGradient>");
    }

    // Generate per-player gradients (for owned buildings)
    foreach (var (playerId, player) in playerLookup)
    {
        var gradientId = $"gradient-{playerId}";
        sb.AppendLine($@"    <linearGradient id=""{gradientId}"" x1=""0%"" y1=""0%"" x2=""100%"" y2=""100%"">");
        sb.AppendLine($@"      <stop offset=""0%"" style=""stop-color:{player.Colors.Primary};stop-opacity:1"" />");
        sb.AppendLine($@"      <stop offset=""100%"" style=""stop-color:{player.Colors.Secondary};stop-opacity:1"" />");
        sb.AppendLine("    </linearGradient>");
    }
}

// Update call site in GenerateSvg()
GeneratePlayerGradients(sb, playerLookup, currentPlayerViewModel);
```

**Option B: Use current player's gradient directly**

```csharp
// BuildingSvgRenderer.cs:126 - Change gradient reference
var gradientId = currentPlayerColors != null
    ? $"gradient-{/* need player ID - requires refactoring */}"
    : "gradient-current-player";
```

❌ Rejected - Requires passing player ID through method chain, more invasive.

**Implementation Steps:**

1. Update `GeneratePlayerGradients()` signature to accept `currentPlayer`
2. Generate `gradient-current-player` in defs section BEFORE player-specific gradients
3. Update call site in `GenerateSvg()` line 60
4. Test with slider in PickingBoard state
5. Verify star buildings show gradient fill

**Testing:**

```csharp
[Fact]
public void GenerateSvg_CreatesCurrentPlayerGradient()
{
    var svg = gameModel.GenerateSvg(players, shownStars: 10);
    Assert.Contains("gradient-current-player", svg);
    Assert.Contains($"stop-color:{currentPlayer.Colors.Primary}", svg);
    Assert.Contains($"stop-color:{currentPlayer.Colors.Secondary}", svg);
}
```

---

### Issue #2: Default Slider Value Mismatch

**Priority:** HIGH
**Status:** 🟡 UX inconsistency with Desktop

**Location:**

- `WebUI/Components/Board/BoardMeasurement.razor:74`
- `WebUI/Pages/Game.razor:458`

**Problem:**

```csharp
// BoardMeasurement.razor:74
[Parameter]
public int ShownStars { get; set; } = 0;  // ← WebUI defaults to 0

// Game.razor:458
private int ShownStars { get; set; } = 0;  // ← Game.razor defaults to 0
```

vs.

```csharp
// Desktop - GameViewProps.cs:119
[ObservableProperty]
public partial int ShownStars { get; set; } = 13;  // ← Desktop defaults to 13
```

**Root Cause:**

- WebUI components default to 0 (show all buildings)
- Desktop defaults to 13 (show only high-quality sites)
- No initialization logic to set value based on game state

**Impact:**

- Different UX between WebUI and Desktop
- Desktop encourages evaluating best sites first (13+ stars)
- WebUI shows cluttered board on initial load
- Users switching platforms may be confused

**Desktop Rationale:**

- Star count formula: 6 or 8 = 5 stars each
- Maximum 3 tiles × 5 stars = 15 total stars
- 13+ stars means:
  - Three tiles with 6 or 8 (5+5+3 or 5+4+4)
  - Or three tiles with 6, 8, and a 6/8 (5+5+5 or 5+5+3)
- Represents top 10-15% of building sites
- Helps players focus on premium locations first

**Proposed Fix:**

**Option A: Change default value (SIMPLE)**

```csharp
// BoardMeasurement.razor:74
[Parameter]
public int ShownStars { get; set; } = 13;  // ← Match Desktop

// Game.razor:458
private int ShownStars { get; set; } = 13;  // ← Match Desktop
```

**Option B: Initialize based on game state (BETTER)**

```csharp
// Game.razor - in OnGameStateUpdated()
private void OnGameStateUpdated(GameModel gameModel)
{
    var previousState = GameModel?.GameState;
    GameModel = gameModel;

    // Initialize slider when entering PickingBoard phase
    if (gameModel.GameState == GameState.PickingBoard
        && previousState != GameState.PickingBoard)
    {
        ShownStars = 13;  // Reset to default when entering phase
    }

    GameStateService.UpdateGameModel(gameModel);
    // ... rest of method
}
```

**Option C: Store in GameStateService (BEST - matches architecture)**

```csharp
// GameStateService.cs - Add initialization logic
public void UpdateGameModel(GameModel gameModel)
{
    var previousState = _gameModel?.GameState;
    _gameModel = gameModel;

    // Reset ShownStars when entering PickingBoard phase
    if (gameModel.GameState == GameState.PickingBoard
        && previousState != GameState.PickingBoard)
    {
        _shownStars = 13;  // Match Desktop default
    }

    NotifyStateChanged();
}
```

**Recommendation:** Use Option C (store in GameStateService)

- Centralizes state management
- Automatically resets when entering PickingBoard
- Persists across component re-renders
- Matches thick client architecture

**Implementation Steps:**

1. Update `GameStateService.UpdateGameModel()` to initialize ShownStars
2. Set default to 13 when entering PickingBoard state
3. Optional: Add property to control default (for testing)
4. Test phase transitions (Regular → PickingBoard → AllocateResource)
5. Verify slider resets to 13 each time board is shuffled

**Testing:**

```csharp
[Fact]
public void EnteringPickingBoard_ResetsShownStarsTo13()
{
    var service = new GameStateService();
    var gameModel = CreateGameModel(GameState.PickingBoard);

    service.UpdateGameModel(gameModel);

    Assert.Equal(13, service.ShownStars);
}
```

---

### Issue #3: Missing Initialization Trigger

**Priority:** MEDIUM
**Status:** 🟡 Affects phase transitions

**Location:**

- `WebUI/Pages/Game.razor` (no equivalent to Desktop logic)

**Desktop Implementation:**

```csharp
// GameViewModel.cs:247-250
if (gameModel.Phase() == GamePhase.PickingBoard || gameModel.Phase() == GamePhase.PickingResources)
{
    var currentStars = ShownStars;
    ShownStars = 14;           // Force to max
    ShownStars = currentStars; // Restore, triggers OnShownStarsChanged
}
```

**Purpose:**

- Forces re-evaluation of building visual states
- Triggered when entering PickingBoard or PickingResources phase
- Ensures buildings update correctly on phase transitions
- Workaround for XAML binding edge cases

**WebUI Equivalent:**

- Missing this re-initialization logic
- Phase transitions may not update building visibility correctly
- Buildings might retain old visual states

**Problem Scenarios:**

1. Enter PickingBoard with ShownStars = 5
2. Some buildings visible, some hidden
3. Shuffle board (tiles change, but ShownStars unchanged)
4. Buildings don't re-evaluate stars (stale visibility)

**Root Cause:**

- Desktop: ShownStars setter triggers `OnShownStarsChanged()` which loops through buildings
- WebUI: Slider value change triggers re-render, but phase transition doesn't
- No explicit "re-evaluate all buildings" mechanism

**Proposed Fix:**

**Option A: Force re-render on phase change**

```csharp
// Game.razor - in OnGameStateUpdated()
private void OnGameStateUpdated(GameModel gameModel)
{
    var previousPhase = GameModel?.Phase();
    GameModel = gameModel;

    // Force re-evaluation when entering PickingBoard/PickingResources
    if ((gameModel.Phase() == GamePhase.PickingBoard
         || gameModel.Phase() == GamePhase.PickingResources)
        && previousPhase != gameModel.Phase())
    {
        // Force re-render by toggling ShownStars
        var current = ShownStars;
        ShownStars = 14;
        StateHasChanged();  // Render with 14
        ShownStars = current;
        StateHasChanged();  // Render with original value
    }

    // ... rest of method
}
```

❌ Rejected - Hacky, causes double render, poor UX

**Option B: Explicit re-render call (SIMPLE)**

```csharp
private void OnGameStateUpdated(GameModel gameModel)
{
    var previousPhase = GameModel?.Phase();
    GameModel = gameModel;
    GameStateService.UpdateGameModel(gameModel);

    // Force re-render when phase changes
    if (previousPhase != gameModel.Phase())
    {
        StateHasChanged();  // Trigger SVG regeneration
    }

    // ... rest of method
}
```

✅ Simple, but may cause unnecessary renders

**Option C: Let Blazor handle it (CURRENT - TEST FIRST)**

```csharp
// No changes needed - GameModel update triggers re-render automatically
```

**Recommendation:** Option C - Test current behavior first

- Blazor automatically re-renders when `GameModel` property changes
- `GenerateBoardSvg()` is called on every render
- Building visual states re-evaluated each time
- Desktop's workaround may not be needed in WebUI

**Implementation Steps:**

1. Test phase transitions without changes
2. Verify buildings update correctly when:
   - Entering PickingBoard
   - Shuffling board
   - Accepting board (transition to AllocateResource)
3. If tests fail, implement Option B
4. Document findings

**Testing:**

```csharp
[Fact]
public async Task PhaseTransition_UpdatesBuildingVisibility()
{
    // Setup: Game in Regular state, ShownStars = 10
    var ctx = CreateTestContext();
    var cut = ctx.RenderComponent<Game>(p => p.Add(x => x.GameId, "test-game"));

    // Act: Transition to PickingBoard
    await SendGameStateUpdate(GameState.PickingBoard);

    // Assert: Buildings with >= 10 stars are visible
    var svg = cut.Find(".board-container").InnerHtml;
    Assert.Contains("BuildingVisualState.Stars", svg);
}
```

---

### Issue #4: StateHasChanged Redundancy

**Priority:** LOW
**Status:** 🟢 Not a bug, documentation needed

**Location:**

- `WebUI/Pages/Game.razor:531-536`

**Code:**

```csharp
private async Task HandleShownStarsChanged(int newValue)
{
    ShownStars = newValue;
    StateHasChanged();  // ← Potentially redundant?
    await Task.CompletedTask;
}
```

**Analysis:**

- Blazor automatically queues re-render after event handler completes
- `StateHasChanged()` forces immediate re-render
- May be redundant in this specific case

**Blazor Rendering Behavior:**

1. Event handler executes
2. State changes tracked
3. After handler completes, Blazor queues re-render
4. Render cycle executes asynchronously

**Explicit StateHasChanged Benefits:**

- Guarantees immediate re-render
- Makes intent explicit in code
- Ensures render happens before method returns
- Avoids edge cases with async timing

**Proposed Fix:** KEEP IT, ADD COMMENT

```csharp
private async Task HandleShownStarsChanged(int newValue)
{
    ShownStars = newValue;

    // Force immediate re-render to update SVG with new star threshold
    // (Blazor would render anyway, but explicit call ensures synchronous update)
    StateHasChanged();

    await Task.CompletedTask;
}
```

**Recommendation:** Document, don't remove

- Explicit is better than implicit
- Small performance cost (negligible)
- Improves code clarity
- Future-proofs against Blazor version changes

**Implementation Steps:**

1. Add inline comment explaining why `StateHasChanged()` is called
2. Document in design doc that this is intentional
3. No code changes needed

---

### Issue #5: Star Count Calculation Inconsistency

**Priority:** LOW
**Status:** 🟢 Already correct, but document

**Location:**

- `WebUI/Components/Board/BoardMeasurement.razor:105-114`
- Desktop: `DesktopApp/Game/GameView/GameViewBindings.cs:33-43`

**Desktop Implementation:**

```csharp
public string BIND_StarCount(int stars, List<TileModel> _tiles)
{
    Debug.Assert(GameModel is not null);
    int count = 0;
    foreach (var building in GameModel.Buildings)
    {
        var tiles = TilesForBuildings(building.BuildingKey);
        if (tiles.Stars() == stars) count++;
    }
    return count.ToString();
}
```

**WebUI Implementation:**

```csharp
private int GetStarCount(int threshold)
{
    return GameModel.Buildings
        .Count(building =>
        {
            var adjacentTiles = GameModel.TilesForBuildings(building.BuildingKey);
            var totalStars = adjacentTiles.Stars();
            return totalStars == threshold;
        });
}
```

**Analysis:**

- Both count buildings whose adjacent tiles sum to exact threshold
- WebUI uses LINQ for cleaner syntax
- Desktop uses explicit loop (same logic)
- Both call `tiles.Stars()` extension method
- ✅ Implementations are functionally equivalent

**Proposed Fix:** None needed, add documentation

**Documentation Update:**

```csharp
/// <summary>
/// Calculates the count of building sites with total adjacent tile stars matching the threshold.
/// Mirrors Desktop implementation: GameViewModel.cs:BIND_StarCount()
/// IMPORTANT: Counts buildings where stars EQUAL threshold (not >=)
/// </summary>
/// <param name="threshold">The star threshold to count (typically 10, 11, 12, or 13).</param>
/// <returns>Number of buildings whose adjacent tiles sum to exactly this star value.</returns>
private int GetStarCount(int threshold)
{
    // ... existing implementation
}
```

**Implementation Steps:**

1. Add XML doc comment to `GetStarCount()` method
2. Update design doc to reference Desktop implementation
3. Add unit test to verify calculation
4. No code changes needed

**Testing:**

```csharp
[Theory]
[InlineData(13, 2)]  // 2 buildings with exactly 13 stars
[InlineData(12, 4)]  // 4 buildings with exactly 12 stars
[InlineData(11, 6)]  // 6 buildings with exactly 11 stars
[InlineData(10, 8)]  // 8 buildings with exactly 10 stars
public void GetStarCount_MatchesDesktopCalculation(int threshold, int expectedCount)
{
    var gameModel = CreateStandardBoard();
    var component = CreateBoardMeasurement(gameModel);

    var count = component.Instance.GetStarCount(threshold);

    Assert.Equal(expectedCount, count);
}
```

---

### Issue #6: No Error Handling for Missing PlayerViewModel

**Priority:** LOW
**Status:** 🟡 Edge case, but should handle gracefully

**Location:**

- `WebUI/Services/Rendering/BuildingSvgRenderer.cs:93, 116, 139`

**Problem:**

```csharp
private static void RenderBuildingGlyph(
    StringBuilder sb, BuildingModel building,
    PlayerColors? ownerColors, double x, double y)
{
    ArgumentNullException.ThrowIfNull(ownerColors, nameof(ownerColors));  // ← Throws!
    // ...
}
```

**Scenario:**

- Building has `OwnerId` set
- Player not found in `playerLookup` dictionary
- `ownerColors` is null
- Method throws exception
- SVG generation fails
- Board doesn't render

**Root Cause:**

- Edge case: Player leaves game, but owns buildings
- Or: PlayerProfile not loaded from server yet
- `BuildingSvgRenderer` expects non-null colors for owned buildings
- No graceful degradation

**Proposed Fix:**

**Option A: Fallback to default colors**

```csharp
private static void RenderBuildingGlyph(
    StringBuilder sb, BuildingModel building,
    PlayerColors? ownerColors, double x, double y)
{
    // Fallback to default gray if owner colors not available
    var colors = ownerColors ?? PlayerColors.Default;

    var radius = BuildingSize / 2;
    var gradientId = $"gradient-{building.OwnerId ?? "default"}";

    sb.AppendLine($@"<circle cx=""{x}"" cy=""{y}"" r=""{radius}""
                    fill=""url(#{gradientId})""
                    stroke=""{colors.Foreground}""
                    stroke-width=""2""/>");
    // ...
}
```

**Option B: Skip rendering (current behavior)**

```csharp
private static void RenderBuildingGlyph(
    StringBuilder sb, BuildingModel building,
    PlayerColors? ownerColors, double x, double y)
{
    if (ownerColors == null)
    {
        // Log warning and skip rendering
        Console.WriteLine($"Warning: No colors for building {building.BuildingKey}, skipping render");
        return;
    }
    // ... rest of method
}
```

**Recommendation:** Option A (fallback to default)

- More robust
- Graceful degradation
- User sees gray building instead of nothing
- Better UX for edge cases

**Implementation Steps:**

1. Remove `ArgumentNullException.ThrowIfNull()` guards
2. Add null-coalescing to use `PlayerColors.Default` as fallback
3. Add warning log for troubleshooting
4. Test with missing player profile
5. Document expected behavior

---

## Implementation Priority

### Sprint 1: Critical Fixes (Must Have)

1. **Issue #1:** Fix gradient-current-player definition (CRITICAL)
   - Blocks star building rendering
   - Est: 2 hours

### Sprint 2: High Priority (Should Have)

2. **Issue #2:** Update default slider value to 13 (HIGH)
   - UX consistency with Desktop
   - Est: 1 hour

3. **Issue #3:** Test phase transition behavior (MEDIUM)
   - May not need changes
   - Est: 2 hours testing

### Sprint 3: Polish (Nice to Have)

4. **Issue #4:** Document StateHasChanged usage (LOW)
   - Est: 30 minutes

5. **Issue #5:** Document star count calculation (LOW)
   - Est: 30 minutes

6. **Issue #6:** Add error handling for missing players (LOW)
   - Edge case, low priority
   - Est: 1 hour

---

## Testing Strategy

### Unit Tests

```csharp
// WebUI.Tests/Rendering/BoardSvgGeneratorTests.cs
[Fact]
public void GenerateSvg_WithShownStars_FiltersBuildings()
{
    var gameModel = CreateGameModel(GameState.PickingBoard);
    var players = CreatePlayers();

    var svg = gameModel.GenerateSvg(players, shownStars: 10);

    // Buildings with >= 10 stars should have SVG markup
    Assert.Contains("BuildingVisualState.Stars", svg);

    // Buildings with < 10 stars should be absent
    // (test by checking absence of specific building coordinates)
}

[Fact]
public void GenerateSvg_CreatesCurrentPlayerGradient()
{
    var gameModel = CreateGameModel(GameState.PickingBoard);
    var players = CreatePlayers();

    var svg = gameModel.GenerateSvg(players, shownStars: 0);

    Assert.Contains(@"<linearGradient id=""gradient-current-player""", svg);
}
```

### Integration Tests

```csharp
// WebUI.Tests/Components/BoardMeasurementTests.cs
[Fact]
public void Slider_UpdatesBuildingVisibility()
{
    var ctx = CreateTestContext();
    var gameModel = CreateGameModel(GameState.PickingBoard);

    var cut = ctx.RenderComponent<BoardMeasurement>(p => p
        .Add(x => x.GameModel, gameModel)
        .Add(x => x.ShownStars, 13));

    // Move slider to 10
    var slider = cut.Find("input[type=range]");
    slider.Change(10);

    // Verify callback invoked with correct value
    // (requires EventCallback test harness)
}
```

### E2E Tests

```
Scenario: User adjusts building visibility slider

Given: Player is in PickingBoard state
When: Slider moves from 13 to 10
Then: Buildings with 10-12 stars become visible
And: Buildings with < 10 stars remain hidden
And: Star buildings show gradient background
And: Star count displayed in building circle
```

---

## Success Criteria

### Functional Requirements

- ✅ Slider defaults to 13 when entering PickingBoard
- ✅ Moving slider updates building visibility immediately
- ✅ Star buildings show colored gradient (not black)
- ✅ Star count calculations match Desktop exactly
- ✅ Phase transitions update building states correctly

### Visual Requirements

- ✅ Buildings with stars >= threshold are visible
- ✅ Buildings with stars < threshold are hidden
- ✅ Star buildings show current player's gradient colors
- ✅ Star count rendered in white text on gradient circle
- ✅ Smooth transitions (< 16ms render time)

### Performance Requirements

- ✅ SVG generation < 50ms for standard board
- ✅ Slider response time < 100ms (perceived as instant)
- ✅ No visual flicker or double rendering
- ✅ Memory usage stable (no leaks)

---

## Rollback Plan

If issues arise:

1. Revert `BoardSvgGenerator.cs` changes
2. Set default ShownStars to 0 (current behavior)
3. Hide slider until fixes validated
4. Deploy hotfix reverting commits
5. File bug report with reproduction steps

---

## Related Work

### Follow-up Tasks (Post-Implementation)

1. Add slider keyboard accessibility (arrow keys)
2. Add tooltip showing "X buildings visible"
3. Animate building fade in/out on slider change
4. Add "Show All" / "Show Best" preset buttons
5. Persist ShownStars preference in local storage

### Future Enhancements

1. Resource filtering (show buildings with specific resources)
2. Port number filtering (show buildings near harbors)
3. Heat map overlay (color buildings by star count)
4. Accessibility improvements (ARIA labels, screen reader support)

---

**End of Implementation Plan**
