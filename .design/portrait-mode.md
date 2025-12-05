# Portrait Mode Design

## Overview

When the viewport aspect ratio is less than 4:3 (portrait orientation), the game switches to a tabbed interface to maximize use of screen real estate.

## Layout Structure

```
┌─────────────────────────────────────┐
│  Board  │  Controls  │   Players   │  ← Tab bar (only visible in portrait)
├─────────────────────────────────────┤
│                                     │
│                                     │
│         Tab Content Area            │  ← Full viewport minus tab bar
│    (Board, Controls, or Players)    │
│                                     │
│                                     │
└─────────────────────────────────────┘
```

## Tab Descriptions

### Board Tab (Default)
- Shows only the game board
- Board scales to fill viewport width
- Height determined by board's aspect ratio
- Centered vertically if space permits

### Controls Tab
- Contains all game controls from the left panel:
  - Game name (editable)
  - Current player indicator
  - Undo / Next / Redo buttons
  - Purchase buttons (Road, Settlement, City, Soldier)
  - Roll entry grid (2-12)
  - Board measurements (during allocation phase)
- **Scaling**: Content scales uniformly to fill viewport width
- Transform origin: top-left
- Preserves aspect ratio of control panel

### Players Tab
- Contains player information from the right panel:
  - Resource tracking (totals across all players)
  - Player cards showing:
    - Player avatar/name
    - Stats row (VP, settlements, cities, etc.)
    - Resource cards for each player
- **Scaling**: Content scales uniformly to fill viewport width
- Transform origin: top-left
- Preserves aspect ratio of players panel

## Scaling Behavior

### Key Principle
**The entire panel scales as a single unit.** All components inside (buttons, grids, cards, text) scale together uniformly. This is achieved by:

1. Wrapping all panel content in a single container
2. Measuring the container's natural width AND height
3. Calculating scale factors for both dimensions
4. Using the smaller scale to ensure content fits in both dimensions
5. Applying `transform: scale(factor)` to the entire container

### Controls Tab Scaling
- `.left-panel-content` is the scaling container
- Contains: game name, controls, purchase buttons, roll grid, measurements
- All child components scale together proportionally

### Players Tab Scaling
- `.right-panel-content` is the scaling container
- Contains: ResourceTracking component, PlayersPanel component
- All player cards and resource displays scale together
- Scales to fit both width AND height (important for 6-player games)

### Implementation
- `panelsScaler.js` handles the scaling logic
- Calculates available space:
  - `availableWidth = viewportWidth - padding`
  - `availableHeight = viewportHeight - tabBarHeight - padding`
- Measures natural dimensions of content wrapper (with transform reset)
- Calculates scale factors:
  - `scaleX = availableWidth / naturalWidth`
  - `scaleY = availableHeight / naturalHeight`
  - `scale = Math.min(scaleX, scaleY)` (use smaller to fit both)
- Applies `transform: scale(factor)` to wrapper with `transform-origin: top left`
- Triggered on:
  - Initial load (`panelsScaler.initialize()`)
  - Window resize (debounced)
  - Tab switch (`panelsScaler.updateScale()`)
- Only active when `aspect-ratio < 4/3`

## State Persistence
- Selected tab stored in `sessionStorage` as `portraitTab`
- Valid values: `"board"`, `"controls"`, `"players"`
- Persists across page refreshes within same session

## CSS Media Query
```css
@media (max-aspect-ratio: 4/3) {
    /* Portrait-specific styles */
}
```

## Landscape Mode (Default)
- Tab bar is hidden (`display: none`)
- Standard 3-column grid layout: `14fr 58fr 28fr`
- Left panel (controls), Center panel (board), Right panel (players)
- All visible simultaneously

## Future Considerations
- Swipe gestures between tabs on touch devices
- Indicator badges on tabs (e.g., "your turn" on Controls)
- Landscape tablet mode with 2-column layout option
