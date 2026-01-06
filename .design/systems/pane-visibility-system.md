# Pane Visibility System Design

**Status:** Proposed
**Created:** 2025-01-05
**Last Updated:** 2025-01-05

## Overview

This document describes a centralized system for controlling UI pane visibility based on game state
and device orientation. The goal is to replace scattered `@if` conditions throughout `Game.razor`
with a declarative, maintainable configuration that maps `GameState` to pane visibility.

## Motivation

### Current Problems

1. **Scattered Logic**: Visibility conditions are spread across 1500+ lines of `Game.razor`
2. **Hard to Reason About**: No single place to see "what's visible in state X?"
3. **Inconsistent**: Some panes use `AllocationPhase()`, others check specific states
4. **Error-Prone**: Easy to forget a condition when adding new states or panes
5. **Duplication**: Portrait and landscape modes duplicate visibility logic

### Recent Example

The BoardMeasurement pane's Shuffle button was cut off because the RollEntry pane was also visible
during `PickingBoard` state. The fix required finding the right `@if` condition buried in the markup.
A centralized system would make such issues obvious at a glance.

## Proposed Architecture

### Core Components

```text
┌─────────────────────────────────────────────────────────────────┐
│                    PaneVisibilityService                        │
├─────────────────────────────────────────────────────────────────┤
│ - GetVisibility(GameState, Orientation) → PaneVisibilityConfig  │
│ - IsPaneVisible(UiPane, GameState, Orientation) → bool          │
└─────────────────────────────────────────────────────────────────┘
                              │
                              ▼
┌─────────────────────────────────────────────────────────────────┐
│                   PaneVisibilityConfig                          │
├─────────────────────────────────────────────────────────────────┤
│ Dictionary<UiPane, bool> - visibility for each pane             │
└─────────────────────────────────────────────────────────────────┘
                              │
                              ▼
┌─────────────────────────────────────────────────────────────────┐
│                        Game.razor                               │
├─────────────────────────────────────────────────────────────────┤
│ @if (IsPaneVisible(UiPane.RollEntry)) { ... }                   │
└─────────────────────────────────────────────────────────────────┘
```

### Pane Enum Definition

```csharp
/// <summary>
/// Identifies all UI panes that can be shown/hidden based on game state.
/// </summary>
public enum UiPane
{
    // === LEFT PANEL (Landscape) / CONTROLS TAB (Portrait) ===
    GameName,           // Editable game name
    GameControls,       // Undo/Next/Redo buttons + state message
    Purchase,           // Road/Settlement/City/Soldier purchase buttons
    RollEntry,          // Dice roll grid (2-12)
    BoardMeasurement,   // Resource tiles, stars slider, shuffle button

    // === CENTER PANEL ===
    GameBoard,          // Hex board with buildings/roads/robber
    ResourceTracking,   // Resource counts header bar

    // === RIGHT PANEL (Landscape) / PLAYERS TAB (Portrait) ===
    PlayersPanel,       // Player cards container

    // === PLAYER CARD SUB-PANES (modes within PlayerCard) ===
    GoFirst,            // "Go First" button overlay on player cards
    SupplementalSelect, // Checkbox overlay for supplemental selection
    PlayerStats,        // Normal resource/building display on cards

    // === PORTRAIT-SPECIFIC ===
    PortraitTabs,           // Board/Controls/Players tab bar
    PortraitOverlayNext,    // Floating Next button on board
    PortraitOverlayUndo,    // Floating Undo button on board
    PortraitStateMessage,   // Floating state message on board

    // === OVERLAY PANES ===
    RobberMenu,         // Robber target selection popup
    GriefCelebration,   // Dodgy celebration animation
}
```

## Pane Definitions

### Left Panel Panes (Landscape Mode)

| Pane | CSS Class | Purpose | Default |
|------|-----------|---------|---------|
| GameName | `game-name-container` | Display/edit game name | Visible |
| GameControls | `game-controls` | Undo/Next/Redo + state message | Visible |
| Purchase | `purchase-controls` | Buy roads/settlements/cities/soldiers | Visible |
| RollEntry | `roll-entry` | Click to record dice rolls (2-12) | Visible |
| BoardMeasurement | `board-measurements` | Resource counts, stars filter, shuffle | Hidden |

### Center Panel Panes

| Pane | CSS Class | Purpose | Default |
|------|-----------|---------|---------|
| GameBoard | `game-board` | The hex board SVG | Visible |
| ResourceTracking | `resource-tracking` | Total resources generated per type | Visible |

### Right Panel Panes (Landscape Mode)

| Pane | CSS Class | Purpose | Default |
|------|-----------|---------|---------|
| PlayersPanel | `players-panel` | Container for all player cards | Visible |

### Player Card Sub-Panes

These are "modes" within the PlayerCard component, not separate DOM elements:

| Pane | Trigger | Purpose |
|------|---------|---------|
| GoFirst | `FinishedRollOrder` state | Shows "Go First" button on each card |
| SupplementalSelect | `PickSupplementalPlayers` state | Shows participation checkbox |
| PlayerStats | Default | Normal stats display (resources, buildings) |

### Portrait-Specific Panes

| Pane | CSS Class | Purpose | Default |
|------|-----------|---------|---------|
| PortraitTabs | `portrait-tabs` | Tab bar: Board/Controls/Players | Visible |
| PortraitOverlayNext | `portrait-next-btn` | Floating Next button on board | Visible |
| PortraitOverlayUndo | `portrait-undo-btn` | Floating Undo button on board | Visible |
| PortraitStateMessage | `portrait-state-message` | Floating state message | Visible |

### Overlay Panes

| Pane | CSS Class | Purpose | Trigger |
|------|-----------|---------|---------|
| RobberMenu | `robber-menu` | Target selection after robber move | User action in MustMoveRobber |
| GriefCelebration | `grief-celebration` | Animation when Dodgy is robbed | Targeting Dodgy with GriefDodgy rule |

## State-to-Visibility Mappings

### Landscape Mode Configuration

```csharp
private static readonly Dictionary<GameState, PaneVisibilityConfig> LandscapeVisibility = new()
{
    // === GAME SETUP PHASE ===
    [GameState.WaitingForRollForOrder] = new()
    {
        // Players roll to determine turn order
        GameControls = true,
        Purchase = false,       // Can't purchase yet
        RollEntry = true,       // Need to roll for order
        BoardMeasurement = false,
        PlayersPanel = true,
        GoFirst = false,
        SupplementalSelect = false,
    },

    [GameState.FinishedRollOrder] = new()
    {
        // Players select who goes first
        GameControls = true,
        Purchase = false,
        RollEntry = false,      // No more rolling
        BoardMeasurement = false,
        PlayersPanel = true,
        GoFirst = true,         // Show "Go First" buttons
        SupplementalSelect = false,
    },

    // === BOARD PICKING PHASE ===
    [GameState.PickingBoard] = new()
    {
        // Accept or shuffle the random board
        GameControls = true,
        Purchase = false,       // Can't purchase yet
        RollEntry = false,      // No rolling during board pick
        BoardMeasurement = true, // Show shuffle button!
        PlayersPanel = true,
        GoFirst = false,
        SupplementalSelect = false,
    },

    // === RESOURCE ALLOCATION PHASE ===
    [GameState.BeginResourceAllocation] = new()
    {
        GameControls = true,
        Purchase = true,        // Place initial settlements
        RollEntry = false,      // No rolling yet
        BoardMeasurement = true, // Stars filter useful here
        PlayersPanel = true,
        GoFirst = false,
        SupplementalSelect = false,
    },

    [GameState.AllocateResourceForward] = new()
    {
        GameControls = true,
        Purchase = true,
        RollEntry = false,
        BoardMeasurement = true,
        PlayersPanel = true,
        GoFirst = false,
        SupplementalSelect = false,
    },

    [GameState.AllocateResourceReverse] = new()
    {
        GameControls = true,
        Purchase = true,
        RollEntry = false,
        BoardMeasurement = true,
        PlayersPanel = true,
        GoFirst = false,
        SupplementalSelect = false,
    },

    [GameState.DoneResourceAllocation] = new()
    {
        GameControls = true,
        Purchase = true,
        RollEntry = false,
        BoardMeasurement = true, // Still useful to see board stats
        PlayersPanel = true,
        GoFirst = false,
        SupplementalSelect = false,
    },

    // === MAIN GAME PHASE ===
    [GameState.WaitingForRoll] = new()
    {
        GameControls = true,
        Purchase = false,       // Must roll first
        RollEntry = true,       // Click to roll
        BoardMeasurement = false,
        PlayersPanel = true,
        GoFirst = false,
        SupplementalSelect = false,
    },

    [GameState.WaitingForNext] = new()
    {
        // After rolling, before ending turn
        GameControls = true,
        Purchase = true,        // Can buy stuff
        RollEntry = true,       // Shows roll history
        BoardMeasurement = false,
        PlayersPanel = true,
        GoFirst = false,
        SupplementalSelect = false,
    },

    [GameState.MustMoveRobber] = new()
    {
        // Rolled 7 or played soldier
        GameControls = true,
        Purchase = false,       // Must move robber first
        RollEntry = true,
        BoardMeasurement = false,
        PlayersPanel = true,
        GoFirst = false,
        SupplementalSelect = false,
    },

    // === SUPPLEMENTAL PHASE ===
    [GameState.PickSupplementalPlayers] = new()
    {
        // Choose who participates in supplemental
        GameControls = true,
        Purchase = false,
        RollEntry = false,
        BoardMeasurement = false,
        PlayersPanel = true,
        GoFirst = false,
        SupplementalSelect = true, // Show checkboxes
    },

    [GameState.Supplemental] = new()
    {
        // Supplemental building round
        GameControls = true,
        Purchase = true,
        RollEntry = false,      // No rolling in supplemental
        BoardMeasurement = false,
        PlayersPanel = true,    // Only participating players shown
        GoFirst = false,
        SupplementalSelect = false,
    },

    // === GAME END ===
    [GameState.GameOver] = new()
    {
        GameControls = true,
        Purchase = false,
        RollEntry = true,       // Show final roll stats
        BoardMeasurement = false,
        PlayersPanel = true,
        GoFirst = false,
        SupplementalSelect = false,
    },
};
```

### Portrait Mode Configuration

Portrait mode has the same logical visibility but different physical layout. The key differences:

1. **Tab-based navigation**: Only one "area" visible at a time (Board, Controls, Players)
2. **Overlay controls**: Next/Undo buttons float over the board
3. **Bottom controls**: Purchase and rolls appear below the board

```csharp
private static readonly Dictionary<GameState, PortraitPaneConfig> PortraitVisibility = new()
{
    [GameState.PickingBoard] = new()
    {
        // Board tab
        BoardOverlayNext = true,
        BoardOverlayUndo = true,
        BoardOverlayState = true,
        BottomBoardMeasurement = true,  // Shuffle button accessible
        BottomRolls = false,
        BottomPurchase = false,

        // Controls tab (if user switches)
        ControlsGameControls = true,
        ControlsPurchase = false,
        ControlsRollEntry = false,
        ControlsBoardMeasurement = true,

        // Players tab
        PlayersGoFirst = false,
        PlayersSupplemental = false,
    },

    [GameState.FinishedRollOrder] = new()
    {
        // Auto-switch to Players tab for GoFirst selection
        AutoSwitchToTab = "players",

        BoardOverlayNext = true,
        BoardOverlayUndo = true,
        BoardOverlayState = true,
        BottomBoardMeasurement = false,
        BottomRolls = false,
        BottomPurchase = false,

        ControlsGameControls = true,
        ControlsPurchase = false,
        ControlsRollEntry = false,
        ControlsBoardMeasurement = false,

        PlayersGoFirst = true,  // Show "Go First" buttons!
        PlayersSupplemental = false,
    },

    [GameState.WaitingForNext] = new()
    {
        BoardOverlayNext = true,
        BoardOverlayUndo = true,
        BoardOverlayState = true,
        BottomBoardMeasurement = false,
        BottomRolls = true,     // Show roll history
        BottomPurchase = true,  // Can purchase

        ControlsGameControls = true,
        ControlsPurchase = true,
        ControlsRollEntry = true,
        ControlsBoardMeasurement = false,

        PlayersGoFirst = false,
        PlayersSupplemental = false,
    },

    // ... additional states follow same pattern
};
```

## Implementation Approach

### Phase 1: Create Core Infrastructure

1. **Create `UiPane` enum** in `Catan3.WebUI/Models/UiPane.cs`
2. **Create `PaneVisibilityConfig` record** with boolean for each pane
3. **Create `PaneVisibilityService`** with static mappings
4. **Add helper method** `IsPaneVisible(UiPane pane)` to `Game.razor`

### Phase 2: Migrate Existing Conditions

Replace scattered `@if` conditions with centralized checks:

**Before:**

```razor
@if (GameModel?.AllocationPhase() != true)
{
    <div class="roll-entry">...</div>
}

@if (GameModel?.AllocationPhase() == true)
{
    <div class="board-measurements">...</div>
}
```

**After:**

```razor
@if (IsPaneVisible(UiPane.RollEntry))
{
    <div class="roll-entry">...</div>
}

@if (IsPaneVisible(UiPane.BoardMeasurement))
{
    <div class="board-measurements">...</div>
}
```

### Phase 3: Handle Special Cases

Some visibility depends on more than just `GameState`:

1. **HouseRules**: GriefCelebration only visible with `GriefDodgy` rule enabled
2. **User Actions**: RobberMenu triggered by right-click/tap, not state
3. **Tab Selection**: Portrait mode visibility depends on selected tab
4. **Player Context**: Some panes might vary by current player (future)

These can be handled by:

- Additional parameters to `IsPaneVisible()`
- Separate overlay visibility flags
- Combining state visibility with local conditions

### Phase 4: Add Portrait Mode Support

Extend the system to handle orientation:

```csharp
public bool IsPaneVisible(UiPane pane)
{
    if (GameModel == null) return false;

    var config = _isPortrait
        ? GetPortraitVisibility(GameModel.GameState, _portraitTab)
        : GetLandscapeVisibility(GameModel.GameState);

    return config.IsVisible(pane);
}
```

## File Structure

```text
WebUI/
├── Models/
│   ├── UiPane.cs                    # Enum definition
│   └── PaneVisibilityConfig.cs      # Configuration record
├── Services/
│   └── PaneVisibilityService.cs     # Static mappings and lookup
└── Pages/
    └── Game.razor                   # Uses IsPaneVisible()
```

## State Groups

To reduce duplication, define state groups that share visibility:

```csharp
/// <summary>
/// Groups of GameStates that share the same pane visibility configuration.
/// </summary>
public static class StateGroups
{
    /// <summary>
    /// States during initial board setup and resource allocation.
    /// BoardMeasurement visible, RollEntry hidden.
    /// </summary>
    public static readonly GameState[] AllocationPhase =
    [
        GameState.PickingBoard,
        GameState.BeginResourceAllocation,
        GameState.AllocateResourceForward,
        GameState.AllocateResourceReverse,
        GameState.DoneResourceAllocation,
    ];

    /// <summary>
    /// States during normal gameplay with dice rolling.
    /// RollEntry visible, BoardMeasurement hidden.
    /// </summary>
    public static readonly GameState[] MainGamePhase =
    [
        GameState.WaitingForRoll,
        GameState.WaitingForNext,
        GameState.MustMoveRobber,
    ];

    /// <summary>
    /// States where player selection UI is needed.
    /// </summary>
    public static readonly GameState[] PlayerSelectionPhase =
    [
        GameState.FinishedRollOrder,      // GoFirst selection
        GameState.PickSupplementalPlayers, // Supplemental selection
    ];
}
```

## Visibility Matrix (Quick Reference)

### Landscape Mode

| State | Purchase | RollEntry | BoardMeasure | GoFirst | Supplemental |
|-------|----------|-----------|--------------|---------|--------------|
| WaitingForRollForOrder | - | YES | - | - | - |
| FinishedRollOrder | - | - | - | YES | - |
| PickingBoard | - | - | YES | - | - |
| BeginResourceAllocation | YES | - | YES | - | - |
| AllocateResourceForward | YES | - | YES | - | - |
| AllocateResourceReverse | YES | - | YES | - | - |
| DoneResourceAllocation | YES | - | YES | - | - |
| WaitingForRoll | - | YES | - | - | - |
| WaitingForNext | YES | YES | - | - | - |
| MustMoveRobber | - | YES | - | - | - |
| PickSupplementalPlayers | - | - | - | - | YES |
| Supplemental | YES | - | - | - | - |
| GameOver | - | YES | - | - | - |

### Portrait Mode (Board Tab - Bottom Controls)

| State | BottomRolls | BottomPurchase | BottomBoardMeasure |
|-------|-------------|----------------|---------------------|
| PickingBoard | - | - | YES |
| Allocation* | - | - | YES |
| WaitingForRoll | YES | - | - |
| WaitingForNext | YES | YES | - |
| MustMoveRobber | YES | - | - |
| Supplemental | - | YES | - |

## Testing Strategy

1. **Unit Tests**: Test `PaneVisibilityService` returns correct config for each state
2. **Integration Tests**: Verify `IsPaneVisible()` in `Game.razor` matches expected behavior
3. **Visual Tests**: Screenshot comparison for each state (using Playwright when stable)

## Migration Checklist

- [ ] Create `UiPane.cs` enum
- [ ] Create `PaneVisibilityConfig.cs` record
- [ ] Create `PaneVisibilityService.cs` with landscape mappings
- [ ] Add `IsPaneVisible()` helper to `Game.razor`
- [ ] Migrate RollEntry visibility (already partially done)
- [ ] Migrate BoardMeasurement visibility
- [ ] Migrate Purchase visibility
- [ ] Migrate GoFirst/SupplementalSelect to PlayerCard
- [ ] Add portrait mode support
- [ ] Add portrait auto-tab-switching logic
- [ ] Update PlayersPanel to use visibility service
- [ ] Remove old `@if` conditions
- [ ] Add unit tests
- [ ] Update session summary

## Future Enhancements

1. **Per-Player Visibility**: Some panes could vary based on whose turn it is
2. **Animation States**: Track panes that are animating in/out
3. **Debug Mode**: Overlay showing current visibility config
4. **Configuration UI**: Admin panel to adjust visibility for testing

## References

- Desktop app: `MainPageViewModel.BIND_ShowBoardMeasurements()` - similar pattern
- Desktop app: `GameStateToVisibilityConverter` - XAML value converter approach
- Current WebUI: `Game.razor` lines 137-165 - scattered visibility conditions
