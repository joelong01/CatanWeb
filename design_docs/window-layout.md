# Window Layout Design

## Overview

The Catan WebUI uses a responsive layout system designed for 4K displays that scales
down to smaller screens while preserving aspect ratio and visual fidelity.

### Design Principles

- **Reference Resolution**: 4K (3840x2160)
- **Scaling Direction**: Scale DOWN from 4K to smaller screens (not up from 1080p)
- **Aspect Ratio**: 16:9 with black letterboxing on non-16:9 viewports
- **Layout Preservation**: Entire layout scales uniformly to prevent content reflow

## Desktop/TV Layout (Primary Experience)

The primary experience targets 4K TVs and monitors in landscape orientation.

### Grid Structure

```text
+------------------+------------------------+------------------+
|                  |                        |                  |
|   Left Panel     |     Center Panel       |   Right Panel    |
|   (Controls)     |     (Game Board)       |   (Players)      |
|   460px          |     2304px             |   1076px         |
|                  |                        |                  |
+------------------+------------------------+------------------+
                        3840px total
```

### Column Breakdown (at 4K)

| Column | Width | Content |
|--------|-------|---------|
| Left | 460px | Game controls, roll entry, board measurements, purchase buttons |
| Center | 2304px | Game board (hexagonal grid with buildings, roads, harbors) |
| Right | 1076px | Resource tracking, player tiles (up to 6 players) |

### Scaling Mechanism

The layout uses a fixed pixel reference size with JavaScript-calculated scaling:

```javascript
function updateScale() {
    const vw = window.innerWidth;
    const vh = window.innerHeight;
    const scale = Math.min(vw / 3840, vh / 2160);
    layout.style.setProperty('--viewport-scale', scale);
}

// Update on load and resize
updateScale();
window.addEventListener('resize', updateScale);
```

CSS applies the scale via transform:

```css
.game-layout {
    width: 3840px;
    height: 2160px;
    transform-origin: center center;
    transform: scale(var(--viewport-scale, 1));
}
```

### Viewport Wrapper

The viewport wrapper centers the scaled layout and provides letterboxing:

```css
.game-viewport {
    width: 100vw;
    height: 100vh;
    overflow: hidden;
    background: #000;  /* Black letterbox bars */
    display: flex;
    align-items: center;
    justify-content: center;
}
```

## Portrait/Tablet Layout (Developer Experience)

Portrait mode supports development and testing on vertical monitors. This is not a
primary user experience but ensures the game remains functional during development.

### Critical Constraint: Board Aspect Ratio

**The game board MUST preserve its aspect ratio.** The hexagonal grid and all
building/road SVG coordinates are calculated based on hex geometry. Stretching
the board non-uniformly will:

- Distort hex shapes (no longer regular hexagons)
- Misalign building placements (settlements, cities)
- Break road rendering (wrong angles)
- Corrupt harbor positioning

The board panel must always scale uniformly (same factor for width and height).

### Layout Reorganization

```text
+------------------------+
|                        |
|     Game Board         |
|  (fixed aspect ratio)  |
|                        |
+------------------------+
|      Controls          |
|  (Next, Undo, Roll)    |
+------------------------+
|    Player Tiles        |
|  (horizontal scroll)   |
+------------------------+
```

### Implementation Approach

Rather than stretching the layout, portrait mode uses the same 16:9 layout scaled
to fit within the portrait viewport. This results in significant letterboxing but
preserves all geometry:

```css
@media (orientation: portrait) {
    /* Same 16:9 layout, just scales smaller to fit portrait width */
    /* Letterboxing appears above and below the game */
}
```

The JavaScript scaling already handles this correctly:

```javascript
// This naturally handles portrait - the width becomes the limiting factor
const scale = Math.min(vw / 3840, vh / 2160);
// In portrait (e.g., 1080x1920): scale = min(1080/3840, 1920/2160)
//                                      = min(0.28, 0.89) = 0.28
```

### Stacked Layout Implementation

Portrait mode uses CSS grid areas to reorganize the layout without changing HTML:

```text
+------------------------+
|                        |
|     Game Board         |
|  (preserves aspect     |
|   ratio, centered)     |
|                        |
+------------------------+
|  Left Panel | Right    |
|  (controls) | (players)|
+------------------------+
```

CSS implementation using grid areas:

```css
@media (orientation: portrait) {
    .game-layout {
        grid-template-columns: 1fr 1fr;
        grid-template-rows: 60% 40%;
        grid-template-areas:
            "board board"
            "left right";
    }
    .center-panel { grid-area: board; }
    .left-panel { grid-area: left; }
    .right-panel { grid-area: right; }
}
```

The JavaScript detects orientation and applies different reference dimensions:

```javascript
function updateScale() {
    const vw = window.innerWidth;
    const vh = window.innerHeight;
    const isPortrait = vh > vw;

    if (isPortrait) {
        // Portrait: 9:16 reference
        const scale = Math.min(vw / 2160, vh / 3840);
        layout.style.setProperty('--viewport-scale', scale);
        layout.classList.add('portrait');
        layout.classList.remove('landscape');
    } else {
        // Landscape: 16:9 reference
        const scale = Math.min(vw / 3840, vh / 2160);
        layout.style.setProperty('--viewport-scale', scale);
        layout.classList.add('landscape');
        layout.classList.remove('portrait');
    }
}
```

## Phone Companion Mode

Phone companion mode provides a secondary control interface for players whose main
game view is on a shared TV or monitor.

### Design Philosophy

- **NOT a full game** - Players watch the 4K TV for the board
- **Command entry only** - Phone is for entering moves and viewing state
- **Simultaneous connections** - All players can connect their phones
- **Current player enforcement** - Only the current player can execute actions
- **Override capability** - Admin can override for "potty break" scenarios

### Routes

| Route | Purpose |
|-------|---------|
| `/game/{GameId}/commands` | Primary phone UI with game controls |
| `/game/{GameId}/board` | Optional view-only board for reference |

### Commands Page UI

Touch-optimized layout with large tap targets:

```text
+---------------------------+
|  [Player Name] - [State]  |
+---------------------------+
|                           |
|  [Undo]  [Next]  [Redo]   |
|                           |
+---------------------------+
|      Roll Entry Grid      |
|   [2] [3] [4] [5] [6]     |
|   [7] [8] [9] [10][11][12]|
+---------------------------+
|    Purchase Buttons       |
| [Road][Settlement][City]  |
+---------------------------+
|       [Shuffle]           |
+---------------------------+
```

### Command Page Features

- **Large touch targets** - Minimum 48px tap areas per accessibility guidelines
- **Current player indicator** - Shows whose turn it is with player color
- **State message** - "Roll Dice", "Place Settlement", etc.
- **Disabled states** - Grayed out buttons when not your turn or action unavailable
- **Error feedback** - Toast notifications for failed actions

### Board View Page

Simplified read-only board for reference:

- Full board rendering (scaled to phone screen)
- No interactive elements
- Shows current buildings, roads, harbors
- Updates in real-time via SignalR

### Phone Detection

```css
@media (max-width: 768px) {
    /* Redirect to companion mode or show mode selector */
}
```

```javascript
// On mobile, offer choice or auto-redirect
if (window.innerWidth <= 768) {
    // Show companion mode selector or redirect to /commands
}
```

## Implementation Details

### CSS Custom Properties

```css
:root {
    --ref-width: 3840;
    --ref-height: 2160;
    --viewport-scale: 1;
}
```

### Breakpoints Summary

| Breakpoint | Condition | Layout |
|------------|-----------|--------|
| Desktop | `min-width: 1024px` AND `orientation: landscape` | Full 3-column |
| Portrait | `orientation: portrait` | Vertical stack |
| Phone | `max-width: 768px` | Companion mode |

### SignalR Connection

All layout modes share the same SignalR connection:

```csharp
_hubConnection.On<GameModel>("GameStateUpdated", OnGameStateUpdated);
```

The `GameModel` includes:

- Current player ID (for turn enforcement)
- Action flags (what actions are enabled)
- Game state (for state message display)
- Player list (for multi-phone coordination)

### Turn Enforcement

Phone companion enforces current-player-only actions:

```csharp
private bool CanExecuteAction()
{
    // Normal case: only current player can act
    if (GameModel.CurrentPlayerId == _myPlayerId)
        return true;

    // Override case: admin bypass
    if (_adminOverride)
        return true;

    return false;
}
```

## File Changes Required

### Phase 1: Desktop Layout (Current Work)

1. Update `Game.razor` to use 4K reference (3840x2160)
2. Update JavaScript scaling calculation
3. Verify all child components render correctly at scale

### Phase 2: Portrait Layout

1. Add CSS media queries for portrait orientation
2. Reorganize grid to vertical stack
3. Test on portrait monitor

### Phase 3: Phone Companion

1. Create `Pages/Commands.razor` for phone command entry
2. Create `Pages/BoardView.razor` for optional board view
3. Add phone detection and routing logic
4. Implement turn enforcement UI

## Testing Checklist

- [ ] Desktop: 4K monitor fullscreen
- [ ] Desktop: 1080p monitor fullscreen
- [ ] Desktop: Browser window resize (various sizes)
- [ ] Desktop: Ultra-wide monitor (21:9)
- [ ] Portrait: Vertical monitor
- [ ] Phone: iOS Safari
- [ ] Phone: Android Chrome
- [ ] Multi-phone: Multiple players connected simultaneously
