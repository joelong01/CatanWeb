# React Game Page Design - Infinite Ocean Architecture

**Last Updated:** 2026-01-22
**Status:** Design Document
**Location:** `.design/ui/react/`

## Overview

This document describes the architecture for the main game page in the React UI. This is a significant evolution from the Blazor approach, introducing an infinite hex ocean as the base layer with floating control panels.

## Design Philosophy

The game board should feel like an island (or islands) in an endless ocean. Players can zoom and pan to explore, while game controls float above the water. This creates an immersive experience and enables future game modes with multiple islands or exploration mechanics.

## Architecture Layers

```text
┌─────────────────────────────────────────────────────────────────────┐
│                         Z-INDEX STACK                               │
├─────────────────────────────────────────────────────────────────────┤
│  z-50: Hamburger Menu + Navigation Overlay                          │
│  z-40: Modal Dialogs (winner celebration, robber target, etc.)      │
│  z-30: Left Panel (Controls) + Right Panel (Players)                │
│  z-20: Portrait Mode Tabs (when visible)                            │
│  z-10: Game Board Tiles (land hexes on the ocean)                   │
│  z-0:  Infinite Hex Ocean (water tiles, base layer)                 │
└─────────────────────────────────────────────────────────────────────┘
```

## Layer Details

### Layer 0: Infinite Hex Ocean (Base)

**Purpose:** Visual foundation that extends infinitely in all directions.

**Implementation:**
- SVG layer filling the entire viewport
- Dynamically renders water hexes based on current viewport + zoom level
- Only renders hexes visible on screen (%.%.%.%.%.%.%.%.%.%.%.%.%.%.%.%.%.%.%.%.%)+ buffer for smooth panning)
- Water hexes use "face-down" tile appearance
- Responds to pan (drag/swipe) and zoom (pinch/scroll wheel)

**Rendering Strategy:**
```typescript
interface ViewportState {
  centerX: number;      // Center of view in hex coordinates
  centerY: number;
  zoom: number;         // 1.0 = default, 0.5 = zoomed out, 2.0 = zoomed in
  viewportWidth: number;
  viewportHeight: number;
}

// Calculate which hexes to render
function getVisibleHexes(viewport: ViewportState): HexCoord[] {
  const buffer = 2; // Extra hexes beyond viewport edge
  // Calculate hex grid bounds based on viewport and zoom
  // Return array of hex coordinates to render
}
```

**Visual Design:**
- Water hexes: Blue gradient with subtle wave pattern
- Should tile seamlessly
- Slight transparency variation for depth effect

### Layer 10: Game Board Tiles

**Purpose:** The actual Catan board - land tiles sitting on the ocean.

**Initial State:**
- All tiles start face-down (matching water appearance)
- Board "emerges" from the ocean via flip animation

**Board Reveal Animation:**
```typescript
interface TileRevealConfig {
  pattern: 'ripple' | 'random' | 'spiral' | 'all-at-once';
  delayBetweenTiles: number;  // ms
  flipDuration: number;        // ms per tile
}

// Default: center-out ripple
const defaultReveal: TileRevealConfig = {
  pattern: 'ripple',
  delayBetweenTiles: 50,
  flipDuration: 400,
};
```

**Flip Animation:**
- CSS 3D transform: `rotateY(180deg)`
- Face-down side: water texture (matches ocean)
- Face-up side: resource type + number token
- Easing: `ease-out` for satisfying snap

**Tile States:**
- `face-down` - Shows water (pre-game, or hidden tiles)
- `face-up` - Shows resource and number
- `dimmed` - After roll, non-matching tiles dim
- `highlighted` - Gold tile selection, valid placement, etc.

### Layer 20: Portrait Mode Tabs

**Purpose:** Navigation between views on narrow screens.

**Tabs:**
1. **Board** - Shows ocean + board (default)
2. **Controls** - Game controls (roll, purchase, undo/redo)
3. **Players** - Player list with stats
4. **Me** - Personal player controller (NEW)

**"Me" Tab Features:**
```text
┌──────────────────────────┐
│  Who am I?               │
│  [Dropdown: Player List] │
├──────────────────────────┤
│                          │
│  (When it's your turn)   │
│  ┌────────────────────┐  │
│  │      NEXT          │  │
│  └────────────────────┘  │
│                          │
│  (During supplemental)   │
│  ┌─────────┐ ┌─────────┐ │
│  │ Yes Sup │ │ No Sup  │ │
│  └─────────┘ └─────────┘ │
│                          │
└──────────────────────────┘
```

**Use Case:** Players watching a shared TV/monitor. Each player has their phone as a personal controller. They select who they are, and get contextual buttons for their turn.

### Layer 30: Floating Panels (Left + Right)

**Purpose:** Game controls and player information, floating above the ocean.

**Visual Design:**
- Semi-transparent backgrounds
- Glassmorphism effect (backdrop-filter: blur)
- Subtle border/glow to distinguish from ocean
- Should look "cool" - experiment with designs

**Left Panel (Controls):**
```text
┌─────────────────┐
│ Game Name       │
│ Turn: Player 1  │
├─────────────────┤
│ [Undo][Redo]    │
│ [Next]          │
├─────────────────┤
│   Roll Grid     │
│  [2][3][4]...   │
├─────────────────┤
│ Purchase Buttons│
│ [Road][Settl]   │
│ [City][DevCard] │
└─────────────────┘
```

**Right Panel (Players):**
```text
┌─────────────────┐
│ Resource Track  │
│ [W][B][O][S][G] │
├─────────────────┤
│ ┌─────────────┐ │
│ │ Player 1 ★  │ │
│ │ VP: 7       │ │
│ └─────────────┘ │
│ ┌─────────────┐ │
│ │ Player 2    │ │
│ │ VP: 5       │ │
│ └─────────────┘ │
│ ...             │
└─────────────────┘
```

**Event Handling:**
- Mouse/touch events on panels DO NOT bubble to ocean layer
- `pointer-events: auto` on panels
- Ocean underneath panels is not interactive

### Layer 40: Modal Dialogs

**Purpose:** Focused interactions that require attention.

**Examples:**
- Winner celebration (confetti, trophy)
- Grief/Dodgy celebration
- Robber target selection
- Trade offers
- Victory point entry

### Layer 50: Navigation

**Purpose:** Always-accessible menu.

**Components:**
- Hamburger button (fixed top-left)
- Slide-out navigation panel
- Outside scaled content (uses viewport pixels, not game coordinates)

## Interaction Model

### Pan (Scroll/Drag)

**Touch:**
- Single finger drag on ocean = pan
- Single finger drag on panel = scroll panel content

**Mouse:**
- Click and drag on ocean = pan
- Click and drag on panel = scroll panel content
- Middle mouse button drag = always pan (even over panels)

### Zoom

**Touch:**
- Pinch gesture on ocean = zoom
- Pinch on panels = no effect (or could zoom entire UI?)

**Mouse/Keyboard:**
- Scroll wheel on ocean = zoom
- Ctrl + scroll = zoom (anywhere)
- +/- buttons in UI (accessibility)

**Zoom Limits:**
```typescript
const ZOOM_CONFIG = {
  min: 0.25,    // Very zoomed out - see large area
  max: 3.0,     // Very zoomed in - detailed view
  default: 1.0, // Board + harbors fit nicely in center
  step: 0.1,    // Increment per scroll tick
};
```

### Default View

When game loads:
1. Calculate zoom level so board + harbors fit in center column
2. Center view on board center
3. User can then zoom/pan as desired

## Responsive Behavior

### Landscape (Desktop/TV)

```text
┌─────────────────────────────────────────────────────────────┐
│ [☰]                                                          │
│   ┌─────────┐                               ┌─────────────┐  │
│   │ Controls│     ~~~~ OCEAN ~~~~           │   Players   │  │
│   │         │        ┌─────┐                │             │  │
│   │ [Roll]  │       /       \               │ [Player 1]  │  │
│   │ [Buy]   │      │  BOARD  │              │ [Player 2]  │  │
│   │         │       \       /               │ [Player 3]  │  │
│   │         │        └─────┘                │             │  │
│   └─────────┘     ~~~~ OCEAN ~~~~           └─────────────┘  │
│                                                              │
└─────────────────────────────────────────────────────────────┘
```

- Three-column layout
- Panels float over ocean
- Full ocean visible, board centered

### Portrait (Phone)

```text
┌──────────────────────────┐
│ [☰]                      │
│                          │
│     ~~~~ OCEAN ~~~~      │
│        ┌─────┐           │
│       /       \          │
│      │  BOARD  │         │
│       \       /          │
│        └─────┘           │
│     ~~~~ OCEAN ~~~~      │
│                          │
├──────────────────────────┤
│ [Board][Ctrl][Players][Me]│
└──────────────────────────┘
```

- Tabs at bottom
- Board tab shows full ocean + board
- Other tabs show respective content
- Panels hidden when on Board tab

## Performance Considerations

### Dynamic Hex Rendering

Only render hexes in viewport + buffer:

```typescript
// Pseudo-code for render loop
function renderOcean(viewport: ViewportState) {
  const visibleHexes = getVisibleHexes(viewport);

  // Use React virtualization or direct SVG manipulation
  // Key insight: hex grid is regular, so we can calculate positions mathematically

  return visibleHexes.map(hex => (
    <WaterHex key={`${hex.q},${hex.r}`} q={hex.q} r={hex.r} />
  ));
}
```

### SVG vs Canvas

**Recommendation: SVG**
- Easier to style and animate individual hexes
- Better for the number of elements we have (hundreds, not thousands)
- CSS animations for flip effects
- Matches existing board rendering approach

**Consider Canvas if:**
- Performance issues with many hexes
- Need pixel-level effects (particles, etc.)

### Memoization

- Memoize hex position calculations
- Memoize panel content when game state hasn't changed
- Use React.memo for hex components
- Zustand selectors to prevent unnecessary re-renders

## Animation Specifications

### Board Reveal (Game Start)

```css
.tile-flip {
  transform-style: preserve-3d;
  transition: transform 400ms ease-out;
}

.tile-flip.face-down {
  transform: rotateY(180deg);
}

.tile-flip.face-up {
  transform: rotateY(0deg);
}

.tile-face {
  backface-visibility: hidden;
  position: absolute;
  width: 100%;
  height: 100%;
}

.tile-back {
  transform: rotateY(180deg);
}
```

**Reveal Sequence (Ripple):**
1. Calculate distance of each tile from board center
2. Sort tiles by distance
3. Stagger flip start time: `delay = distance * 50ms`
4. All tiles flipping creates wave effect

### Pan Animation

```css
.ocean-container {
  transition: transform 50ms linear; /* Smooth but responsive */
}
```

Or use `requestAnimationFrame` for smoother 60fps panning.

### Zoom Animation

```css
.ocean-container {
  transition: transform 200ms ease-out;
  transform-origin: center center; /* Zoom toward center */
}
```

For pinch zoom, zoom toward pinch center point.

## State Management

### Viewport Store (new)

```typescript
interface ViewportStore {
  // View state
  centerQ: number;
  centerR: number;
  zoom: number;

  // Actions
  pan: (deltaQ: number, deltaR: number) => void;
  zoomTo: (level: number, focalPoint?: {x: number, y: number}) => void;
  resetView: () => void;
  fitBoardToView: () => void;
}
```

### Integration with Game Store

```typescript
// Game store additions
interface GameStore {
  // ... existing state ...

  // Board reveal
  revealedTiles: Set<string>; // Tile keys that have been flipped
  isRevealing: boolean;

  // Actions
  startBoardReveal: () => void;
  revealTile: (tileKey: string) => void;
}
```

## File Structure

```text
react-ui/
├── app/
│   └── game/
│       └── [gameId]/
│           └── page.tsx          # Game page shell
├── components/
│   ├── game/
│   │   ├── GameContainer.tsx     # Manages layers and layout
│   │   ├── OceanLayer.tsx        # Infinite hex ocean
│   │   ├── BoardLayer.tsx        # Game tiles on ocean
│   │   ├── ControlsPanel.tsx     # Left floating panel
│   │   ├── PlayersPanel.tsx      # Right floating panel
│   │   ├── MeTab.tsx             # Personal controller tab
│   │   └── PortraitTabs.tsx      # Tab bar for portrait
│   ├── board/
│   │   ├── WaterHex.tsx          # Single water hex
│   │   ├── LandHex.tsx           # Land tile with flip animation
│   │   ├── HexTile.tsx           # Base hex rendering
│   │   └── ... (existing)
│   └── ...
├── hooks/
│   ├── useViewport.ts            # Pan/zoom state and handlers
│   ├── usePanGesture.ts          # Touch/mouse pan handling
│   ├── useZoomGesture.ts         # Pinch/scroll zoom handling
│   └── ...
└── lib/
    └── stores/
        └── viewportStore.ts      # Viewport state
```

## Implementation Phases

### Phase 1: Ocean Foundation
- Create OceanLayer component
- Implement dynamic hex rendering based on viewport
- Basic pan with mouse drag
- Basic zoom with scroll wheel

### Phase 2: Board Integration
- Position game board tiles on ocean
- Implement face-down initial state
- Add board reveal animation

### Phase 3: Floating Panels
- Create semi-transparent panel containers
- Port controls to ControlsPanel
- Port players to PlayersPanel
- Implement event isolation (no bubble to ocean)

### Phase 4: Touch Support
- Implement pinch-to-zoom
- Implement touch pan
- Test on actual devices

### Phase 5: Portrait Mode
- Implement tab navigation
- Create "Me" tab with player selector
- Add contextual buttons (Next, Yes/No Sup)

### Phase 6: Polish
- Glassmorphism effects on panels
- Smooth animations
- Performance optimization
- Edge case handling

## Future Possibilities

This architecture enables:

1. **Multi-Island Games**: Boards with ocean between land masses
2. **Exploration Mode**: Fog of war, discovering new tiles
3. **Scenario Editor**: Pan around large custom maps
4. **Spectator Mode**: Watch game with free camera control
5. **Replay Mode**: Scrub through game history with zoom to action

## Related Documents

- `responsive-design.md` - Form page responsive patterns
- `uiscale-design.md` - Original scaling approach (superseded for game page)
- `typescript-porting-design.md` - Overall migration design
- `ts-port-impl-plan.md` - Implementation phases
