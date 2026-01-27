# Game Page Implementation Plan

## Overview

Redesign the React Game page with an infinite hex grid, floating draggable/resizable panels, and hex-based UI controls instead of traditional rectangular buttons.

## Background & Inspiration

- **Blazor Reference**: 3-column layout (320px controls | flex board | 530px players)
- **Hex Test Page**: Working infinite pan/zoom viewport to reuse
- **Home Page**: Hex cluster menu pattern to extend

---

## Core Architecture

### Unified Hex Grid (NOT separate board + background)

The entire viewport is ONE infinite hex grid with pan/zoom:

```text
┌─────────────────────────────────────────────────────────┐
│  water  water  water  water  water  water  water  water │
│    water  water  water  water  water  water  water      │
│  water  water ┌─────────────────────┐ water  water      │
│    water      │  BOARD TILES        │   water  water    │
│  water  water │  (same grid, just   │ water  water      │
│    water      │   different content)│   water  water    │
│  water  water └─────────────────────┘ water  water      │
│    water  water  water  water  water  water  water      │
│  water  water  water  water  water  water  water  water │
└─────────────────────────────────────────────────────────┘
          ↑ Pan/zoom moves ALL hexes together
```

**Key insight**: The game board is NOT a separate floating element. Board tiles are simply hexes in the infinite grid at specific coordinates that render game content instead of water.

**Rendering logic per hex:**

```typescript
function renderHex(coord: HexCoordinate) {
  const tile = gameModel.tiles.find(t => hexEquals(t.coords, coord));

  if (tile) {
    // This hex is a board tile - render game content
    return <GameTile tile={tile} />;
  } else {
    // This hex is water - render water texture
    return <WaterHex />;
  }
}
```

**Flip Animation**: When game loads, board hexes animate from water → game tile (CSS 3D flip).

---

## Core Features

### 1. Infinite Hex Viewport

- Pan/zoom viewport filling the entire screen
- All hexes start as water.png texture
- Hexes at board coordinates render game tiles instead
- Border size: 1 pixel (gap between hexes)
- Reuse logic from `hex-test/page.tsx` infinite mode
- Board tiles flip-animate in when game loads

### 2. Floating Panel System

All control panels are:

- **Draggable** - move anywhere on screen
- **Resizable** - content scales proportionally inside
- **Minimizable** - collapse to title bar only (icon + name), expand on click
- **Persistent** - positions saved to localStorage
- **Reset button** - restore to default layout

**Minimize behavior:**

- Each panel has a minimize button (─) in title bar
- Minimized panels collapse to a small pill/chip showing icon + short name
- Click minimized panel to restore
- Minimized state persists in localStorage

**Panels:**

| Panel | Default Position | Content |
|-------|------------------|---------|
| Dice | Top-left | Two 7-hex clusters for dice selection |
| Actions | Left side | 7-hex cluster for game controls |
| Measurements | Top-right | Nested hex cluster (resources + stars) |
| Players | Right side | Player cards (separate panel) |
| Resources | Bottom-left | Total game resources |

### 3. Hex Size for Board Fitting

Since water and board are ONE unified grid, all hexes use the same size:

- **Hex size**: 100px (matches existing `boardConstants.ts`)
- **Regular board**: 19 land tiles (radius 2) centered at origin
- **Expansion board**: 30 land tiles (larger radius)
- **Water**: All other hexes in the infinite grid

The board "floats" visually because land tiles have colorful resources while surrounding water hexes have the subtle water.png texture.

---

## Implemented Components (as of 2025-01)

The following hex-based UI controls have been implemented in `react-ui/app/controls-test/page.tsx`:

| Component | Layout | Props | Data Source |
|-----------|--------|-------|-------------|
| **RollRing** | 11-hex (3-4-3 columns), 7 isolated at bottom-left | `rollStats`, `onRollClick`, `colors` | Roll history from `GameModel.players` |
| **DiceCluster** | Two 7-hex clusters side-by-side | `die1`, `die2`, `onSelect*`, `onSendRoll`, `colors` | Local state + `ActionFlags.rollsEnabled` |
| **ActionCluster** | 7-hex CLUSTER_7 with center Next | `actionFlags`, `gameState`, `entitlements`, `purchaseStats`, `commands`, `colors` | `GameModel.actionFlags`, `GameModel.gameState`, `PlayerModel.unspentEntitlements` |
| **MeasurementCluster** | 5 outer + 7 inner nested hexes | `resourceStars`, `buildingSpotCounts`, `variance`, `colors` | Pre-computed from `GameModel.tiles`, `GameModel.buildings` |

**Supporting Components:**

- **NumberToken** - SVG circle with number and probability stars (same as board tiles)
- **GameTile** - Board hex with resource texture, wood border, number token
- **Road** - Bowtie polygon for road/ship rendering
- **Building** - Settlement/city circles at hex vertices

---

## State Selection Strategy

### Principle: Props Are Pre-Computed Slices

Components receive only the data they need, pre-computed at the page level. This prevents re-renders when unrelated parts of GameModel change.

```typescript
// BAD: Passing entire GameModel forces re-render on any change
<MeasurementCluster gameModel={gameModel} />

// GOOD: Pre-compute and pass only what's needed
const resourceStars = useMemo(() => computeResourceStars(tiles), [tiles]);
<MeasurementCluster resourceStars={resourceStars} variance={0.5} colors={playerColors} />
```

### Selector Pattern (Zustand + subscribeWithSelector)

```typescript
// Define fine-grained selectors in gameStore.ts
const selectActionFlags = (state: GameStore) => state.gameModel?.actionFlags;
const selectTiles = (state: GameStore) => state.gameModel?.tiles ?? [];
const selectGameState = (state: GameStore) => state.gameModel?.gameState;

// Usage in page component - only re-renders when that specific slice changes
const actionFlags = useGameStore(selectActionFlags);
const tiles = useGameStore(selectTiles);
```

### Data Flow Diagram

```text
SignalR Event: GameStateUpdated
       │
       ▼
┌─────────────────────────────────────────────┐
│ reconcileGameModel(prevModel, newModel)     │
│   - Preserves unchanged array references    │
│   - Returns prev if nothing changed         │
└─────────────────────────────────────────────┘
       │
       ▼
┌─────────────────────────────────────────────┐
│ gameStore.setGameModel(reconciled)          │
│   - Zustand triggers selector subscriptions │
└─────────────────────────────────────────────┘
       │
       ├── selectActionFlags ─────► ActionCluster (only if flags changed)
       ├── selectTiles ───────────► MeasurementCluster (only if tiles changed)
       ├── selectRollsEnabled ────► DiceCluster (only if rollsEnabled changed)
       └── selectPlayers ─────────► PlayersPanel (only if players changed)
```

### Component-Specific Selectors

**ActionCluster:**

```typescript
const selectActionFlags = (state) => state.gameModel?.actionFlags;
const selectGameState = (state) => state.gameModel?.gameState;
const selectMyEntitlements = (state) => {
  const me = state.gameModel?.players.find(p => p.id === state.currentPlayerId);
  return me?.unspentEntitlements ?? [];
};
const selectPurchaseStats = (state) => state.gameModel?.entitlementPurchaseModel ?? [];
```

**DiceCluster:**

```typescript
const selectRollsEnabled = (state) => state.gameModel?.actionFlags?.rollsEnabled ?? false;
const selectIsMyTurn = (state) => state.gameModel?.currentPlayerId === state.currentPlayerId;
// Die values are LOCAL state, not from GameModel
```

**MeasurementCluster:**

```typescript
const selectTiles = (state) => state.gameModel?.tiles ?? [];
const selectBuildings = (state) => state.gameModel?.buildings ?? [];
// Star/resource filters are in layoutStore (UI state), not gameStore
```

---

## Memoization Requirements

### Component-Level (React.memo)

All cluster components MUST be wrapped with `memo()` to prevent re-renders when parent updates:

```typescript
// REQUIRED pattern for all hex controls
export const ActionCluster = memo(function ActionCluster(props: ActionClusterProps) {
  // ...
});

export const RollHexContent = memo(function RollHexContent(props: RollHexContentProps) {
  // ...
});
```

### Value-Level (useMemo)

Pre-compute derived values at the page level:

```typescript
// In game page component
const resourceStars = useMemo(() => {
  return RESOURCE_TYPES.reduce((acc, type) => {
    acc[type] = tiles
      .filter(t => t.resourceTileType === type)
      .reduce((sum, t) => sum + (t.stars || 0), 0);
    return acc;
  }, {} as Record<ResourceType, number>);
}, [tiles]);

const purchaseAvailability = useMemo(() => ({
  road: entitlements.includes(Entitlement.Road),
  settlement: entitlements.includes(Entitlement.Settlement),
  city: entitlements.includes(Entitlement.City),
  devCard: entitlements.includes(Entitlement.DevCard),
}), [entitlements]);
```

### Callback-Level (useCallback)

- Commands from `useGameCommands()` are already stable references - no wrapping needed
- Custom handlers that close over changing state need `useCallback`:

```typescript
// NOT needed - proxy methods are stable
const { next, undo, redo } = useGameCommands();

// NEEDED - closes over selectedResources state
const handleResourceClick = useCallback((resource: string) => {
  setSelectedResources(prev =>
    prev.includes(resource)
      ? prev.filter(r => r !== resource)
      : [...prev, resource].slice(-3)  // Max 3
  );
}, []);  // Empty deps - uses functional update
```

---

## Props Interface Reference

### Shared PlayerColors Interface

```typescript
/** Matches C# PlayerColors record - used across all controls */
interface PlayerColors {
  primary: string;      // #RRGGBB hex
  secondary: string;    // #RRGGBB hex
  foreground: string;   // #RRGGBB hex (text color)
  cssGradient: string;  // Computed: linear-gradient(135deg, primary, secondary, endColor)
}
```

### ActionCluster Props

```typescript
interface ActionClusterProps {
  actionFlags: ActionFlags | null;
  gameState: GameState | null;
  entitlements: Entitlement[];
  purchaseStats: EntitlementPurchaseModel[];
  commands: {
    onNext: () => void;
    onUndo: () => void;
    onRedo: () => void;
    onPurchaseRoad: () => void;
    onPurchaseSettlement: () => void;
    onPurchaseCity: () => void;
    onPurchaseDevCard: () => void;
  };
  playerColors: PlayerColors;
}
```

### MeasurementCluster Props

```typescript
interface MeasurementClusterProps {
  resourceStars: Record<ResourceType, number>;
  buildingSpotCounts: Record<number, number>;
  variance: number;
  playerColors: PlayerColors;
}
```

### RollRing Props

```typescript
interface RollRingProps {
  rollStats: Record<number, { count: number; percentage: number }>;
  onRollClick?: (roll: number) => void;
  colors: PlayerColors;
}
```

### DiceCluster Props

```typescript
interface DiceClusterProps {
  enabled: boolean;           // ActionFlags.rollsEnabled && isMyTurn
  isRolling: boolean;         // Local loading state
  onRoll: (die1: number, die2: number) => void;
  playerColors: PlayerColors;
}
```

---

## Hex-Based UI Controls

### Dice Panel - RollRing & DiceCluster

The Dice Panel contains two components: a **RollRing** showing roll statistics and a **DiceCluster** for dice selection.

#### RollRing - Roll Statistics (11-hex grid + isolated 7)

Layout: 3-4-3 column arrangement with 7 isolated at bottom-left.

```text
       [2]
   [5]    [10]
       [6]
   [3]    [11]
       [8]
   [4]    [12]
       [9]

[7] ← isolated bottom-left
```

**Column layout coordinates (from controls-test/page.tsx):**

```typescript
const rollCoords: { roll: number; coord: HexCoordinate }[] = [
  // Column 0 (q=0): 2, 3, 4 - shifted down 1 to align with middle column
  { roll: 2, coord: { q: 0, r: 1, s: -1 } },
  { roll: 3, coord: { q: 0, r: 2, s: -2 } },
  { roll: 4, coord: { q: 0, r: 3, s: -3 } },
  // Column 1 (q=1): 5, 6, 8, 9 (7 skipped - goes to isolated position)
  { roll: 5, coord: { q: 1, r: 0, s: -1 } },
  { roll: 6, coord: { q: 1, r: 1, s: -2 } },
  { roll: 8, coord: { q: 1, r: 2, s: -3 } },
  { roll: 9, coord: { q: 1, r: 3, s: -4 } },
  // Column 2 (q=2): 10, 11, 12
  { roll: 10, coord: { q: 2, r: 0, s: -2 } },
  { roll: 11, coord: { q: 2, r: 1, s: -3 } },
  { roll: 12, coord: { q: 2, r: 2, s: -4 } },
  // 7 isolated at bottom-left edge (robber roll)
  { roll: 7, coord: { q: -1, r: 4, s: -3 } },
];
```

**Visual rendering:**

- Each hex displays the **NumberToken** component (identical to board tiles)
- Blue background for normal rolls (2-5, 9-12)
- Black background with red text for high-probability (6, 8)
- Probability stars (★) below the number

**Button behavior (not selectable):**

- Hover: scale 0.96 → 0.94
- Press: scale 0.96 → 0.90
- Tiles are buttons that trigger `onRollClick(rollNumber)` for filtering

#### DiceCluster - Dice Selection (Two 7-hex clusters)

```text
      [1]              [1]
   [6]   [2]        [6]   [2]
     [D1]              [D2]
   [5]   [3]        [5]   [3]
      [4]              [4]
```

**Interaction:**

1. Click value (1-6) on Die 1 → highlights with player color
2. Click value (1-6) on Die 2 → highlights with player color
3. Click center hex (D1 or D2) to confirm roll
4. Both dice must be selected before confirming

**Visual States:**

- Idle: Gray background
- Selected: Player color gradient
- Rolling: Spinning/tumbling animation (during server request)
- Confirmed: Pulse animation showing result, then reset

---

### Action Controls - 7-Hex Cluster

```text
        [DevCard]
   [Undo]    [Road]
       [Next]
   [Redo]    [Settlement]
        [City]

   "Roll the Dice"  ← State message below cluster
```

**Layout (CLUSTER_7 positions):**

| Position | Content | Icon | Action |
|----------|---------|------|--------|
| Center | Next button | FontAwesome `faForward` | Advance game state |
| North | Dev Card | FontAwesome `faCreditCard` | Purchase development card |
| NE | Road | Catan font glyph `\uE92D` | Build road |
| SE | Settlement | Catan font glyph `\uE926` | Build settlement |
| South | City | Catan font glyph `\uE900` | Build city |
| SW | Redo | FontAwesome `faRotateRight` | Redo last action |
| NW | Undo | FontAwesome `faRotateLeft` | Undo last action |

**State Message:** Displayed as text below the hex cluster (not inside a hex). Shows current game state like "Roll the Dice", "Place Settlement", etc.

#### Purchase Button Features (Road, Settlement, City, DevCard)

**3D Flip Animation (disabled state):**

When a purchase action is disabled (no entitlement or insufficient resources), the hex tile flips to show the back face:

```css
/* CSS 3D flip implementation */
.action-hex {
  transform-style: preserve-3d;
  transition: transform 0.6s;
}
.action-hex.disabled {
  transform: rotateY(180deg);
}
.action-hex-front, .action-hex-back {
  backface-visibility: hidden;
}
.action-hex-back {
  transform: rotateY(180deg);
  /* Shows card artwork + resource cost */
}
```

**Purchase Count Badges:**

Purchase buttons display a badge showing how many of that item the player has built:

- Badge position: Upper-left vertex of hex (21% from left, 30% from top)
- Shows count from `purchaseStats` array (e.g., "3" for 3 roads built)
- Only shown when count > 0
- Styled with player color background

```typescript
// Badge positioning (from controls-test/page.tsx)
<div className="absolute" style={{ left: '21%', top: '30%' }}>
  <span className="bg-player-primary text-white text-xs font-bold rounded-full px-1.5">
    {purchaseCount}
  </span>
</div>
```

**Back Face Content:**

When flipped (disabled), the back shows:

- Card/item artwork image
- Resource cost icons
- Grayed out styling to indicate unavailable

**Keyboard Shortcut:**

- **Enter** key triggers "Next" action (when enabled)
- Global listener on the page, no focus required
- Only fires when Next button is in enabled state

---

### Board Measurements - Nested Hex Cluster

Two nested hex clusters: 5 outer resources + 1 variance (ring) with 7 inner star filters (cluster).

```text
          [Sheep]
             10

   [Ore]           [Wood]
    11    ┌─────┐    12
       [13]  [12]
    [8]  [Reset] [11]
       [9]   [10]
   [Brick]          [Wheat]
    11              4

        [Variance]
         ⚖️ 0.5
```

**Outer Ring (5 resources + 1 variance = 6 hexes):**

| Position | Content | Selection |
|----------|---------|-----------|
| North | Sheep icon + star count | Multi-select (max 3) |
| NE | Wood icon + star count | Multi-select (max 3) |
| SE | Wheat icon + star count | Multi-select (max 3) |
| South | Variance (scales icon + value) | Display only |
| SW | Brick icon + star count | Multi-select (max 3) |
| NW | Ore icon + star count | Multi-select (max 3) |

**Inner Cluster (7 hexes for star filtering):**

| Position | Content | Action |
|----------|---------|--------|
| Center | "Reset" | Clear star filter (single-select) |
| North | 13 | Filter ≥13 stars |
| NE | 12 | Filter ≥12 stars |
| SE | 11 | Filter ≥11 stars |
| South | 10 | Filter ≥10 stars |
| SW | 9 | Filter ≥9 stars |
| NW | 8 | Filter ≥8 stars |

#### Resource Hex Behavior (Multi-Select)

- Shows resource image + total star count for that resource on board
- **Multi-select**: Click to add to filter (max 3 resources)
- If 3 resources selected, clicking a 4th replaces the oldest (circular rotation)
- Selected resources show player color highlight
- Click selected resource again to deselect

```typescript
// Multi-select logic (from controls-test/page.tsx)
const handleResourceClick = (resource: string) => {
  setSelectedResources(prev => {
    if (prev.includes(resource)) {
      return prev.filter(r => r !== resource);  // Deselect
    }
    if (prev.length >= 3) {
      return [...prev.slice(1), resource];  // Circular rotation
    }
    return [...prev, resource];  // Add
  });
};
```

#### Star Filter Behavior (Single-Select)

- Click star number to filter settlement spots with ≥ that many stars
- **Single-select**: Only one star filter active at a time
- Selected star shows player color highlight
- Click "Reset" center hex to clear filter

#### Variance Hex

The south position shows board variance/balance:

- **Icon**: FontAwesome `faScaleBalanced` (scales)
- **Value**: Variance number (e.g., "0.5")
- **Background**: Player color gradient (not white)
- Display only, not clickable

```typescript
// Variance hex content (from controls-test/page.tsx)
function VarianceHexContent({ variance, colors }: VarianceHexContentProps) {
  return (
    <div
      className="w-full h-full flex flex-col items-center justify-center"
      style={{ background: colors.cssGradient }}
    >
      <FontAwesomeIcon icon={faScaleBalanced} className="text-lg mb-1" />
      <span className="text-sm font-semibold">{variance.toFixed(1)}</span>
    </div>
  );
}
```

---

## Harbor Rendering

**Key change from Desktop/Blazor:** Harbors now render as full hexes in the unified grid (same space as tiles) so pan/zoom works correctly. Previously they were rendered as overlays.

### Existing Data Model (from GameModel)

Uses existing `GameModel.Harbors: HarborModel[]` - no model changes needed:

```typescript
// From types/generated/models (auto-generated from C#)
interface HarborModel {
  harborKey: HarborKey;
  owner: PlayerModel | null;
}

interface HarborKey {
  hexCoordinates: HexCoordinates;  // Position in the water ring
  harborType: HarborType;          // 'ThreeForOne' | 'Sheep' | 'Wood' | etc.
  side: HexSide;                   // Which edge connects to board
}

type HexSide = 'Top' | 'TopRight' | 'BottomRight' | 'Bottom' | 'BottomLeft' | 'TopLeft';
```

### Harbor Hex Structure

```text
     ┌─────────────┐
    /               \
   /    ┌───────┐    \
  │     │ 3:1   │     │
  │     │  ⚓   │     │◄── Side indicator (arrow/pointer toward board)
  │     └───────┘     │
   \                 /
    \               /
     └─────────────┘
```

**Components:**

- **Center circle**: Harbor type icon (3:1 generic or 2:1 resource image)
- **Side indicator**: Visual pointer showing which hex edge connects to land
- **Background**: Water texture with harbor overlay

### Rendering Logic

See **Efficient Board Rendering** section below for the optimized lookup approach using `HexGridCollection` for O(1) coordinate lookups instead of O(n) array searches.

### Flip Animation

Like board tiles, harbor hexes animate from water → harbor when the game loads:

1. Initial state: All hexes render as water
2. Board tiles flip first (staggered from center outward)
3. Harbor hexes flip after board tiles complete
4. Each harbor rotates during flip to orient the side indicator correctly

### Side Indicator Rotation

The `HexSide` value determines arrow rotation (pointing toward the board):

| HexSide | Rotation | Visual |
|---------|----------|--------|
| Top | 180° | Arrow pointing down (toward board below) |
| TopRight | 240° | Arrow pointing lower-left |
| BottomRight | 300° | Arrow pointing upper-left |
| Bottom | 0° | Arrow pointing up (toward board above) |
| BottomLeft | 60° | Arrow pointing upper-right |
| TopLeft | 120° | Arrow pointing lower-right |

**Note:** The arrow points TOWARD the board, so a harbor with `side: 'Top'` has its top edge facing the board, meaning the arrow points down.

---

## Efficient Board Rendering

**Key change from Blazor:** Moving from SVG polygons to pure DOM with CSS transforms and clip-paths. This section addresses performance concerns and the complexity of road rendering.

### Data Structures for O(1) Lookup

The naive `array.find()` approach is O(n) per hex - unacceptable when rendering 100+ hexes per frame during pan/zoom.

**Solution:** Build `HexGridCollection` maps on game load:

```typescript
// Built once when GameModel loads, updated on state changes
interface BoardLookups {
  tiles: HexGridCollection<TileModel>;      // 19 or 30 tiles
  harbors: HexGridCollection<HarborModel>;  // 9 harbors
  roads: HexGridCollection<RoadModel[]>;    // Roads indexed by tile coord
  buildings: HexGridCollection<BuildingModel[]>; // Buildings at vertices
}

// O(1) lookup during render
function getHexContent(coord: HexCoordinate, lookups: BoardLookups) {
  const tile = lookups.tiles.get(coord);
  if (tile) return { type: 'tile', data: tile };

  const harbor = lookups.harbors.get(coord);
  if (harbor) return { type: 'harbor', data: harbor };

  return { type: 'water' };
}
```

**Rebuild triggers:** Only rebuild lookups when `GameModel` changes (not on pan/zoom).

### Layer Organization (Z-Index)

Render order matches Blazor's proven layer stack:

```text
Layer 1 (bottom): Hex Grid
├─ Water hexes (infinite background)
├─ Board tiles (resource + number)
└─ Harbor hexes

Layer 2 (middle): Roads
└─ All roads rendered above tiles

Layer 3 (top): Buildings
├─ Settlements
└─ Cities

Layer 4 (overlay): Interactive
├─ Robber
├─ Build spots (during placement)
└─ Click targets
```

### Tile Rendering (Pure DOM)

Each tile is a positioned div with hex clip-path:

```typescript
interface TileProps {
  tile: TileModel;
  position: PixelPosition;  // From hexToPixel()
}

function GameTile({ tile, position }: TileProps) {
  return (
    <div
      className="absolute hex-clip-flat"
      style={{
        left: position.x,
        top: position.y,
        width: HEX_WIDTH,
        height: HEX_HEIGHT,
        transform: 'translate(-50%, -50%)',
      }}
    >
      {/* Outer border (wood texture) */}
      <div className="absolute inset-0 hex-clip-flat bg-wood-border" />

      {/* Inner content (resource) - scaled down for gap */}
      <div
        className="absolute inset-0 hex-clip-flat"
        style={{
          transform: 'scale(0.91)',  // Creates road gap
          backgroundImage: `url(/tiles/${tile.resource}.png)`,
        }}
      />

      {/* Number token */}
      {tile.number && <NumberToken number={tile.number} />}
    </div>
  );
}
```

**Key constants (from Blazor `BoardSvgConstants.cs`):**

| Constant | Value | Formula |
|----------|-------|---------|
| `HexSize` | 100 | Outer hex circumradius |
| `InnerHexSize` | 91 | `HexSize - TileGap - StrokeThickness/2` |
| `TileGap` | 2 | Border stroke width |
| `InnerHexStrokeThickness` | 14 | Road body width control |
| `Apothem` | 86.6 | `HexSize * sqrt(3)/2` (center to edge midpoint) |

### Road Rendering (Pure DOM) - Single Shape + Transform

**Key insight:** All roads are the **same shape** - a 6-point bow-tie polygon. We define the geometry ONCE, then use CSS `transform` (translate + rotate) to place each road.

#### Canonical Road Polygon (from Blazor)

The exact polygon used in `BoardSvgConstants.CanonicalRoadPolygon`:

```typescript
// Canonical road polygon (horizontal orientation, centered at origin)
// For HexSize=100, InnerHexSize=91:
//
//   tip = HexSize / 2 = 50.0
//   inner = InnerHexSize / 2 = 45.5
//   perpDist = (HexSize - InnerHexSize) * sqrt(3) / 2 ≈ 7.8
//
// Points (in SVG polygon order):
//   tip1 → innerA1 → innerA2 → tip2 → innerB2 → innerB1
//
const CANONICAL_ROAD_POINTS = '-50.0,0.0 -45.5,-7.8 45.5,-7.8 50.0,0.0 45.5,7.8 -45.5,7.8';

// Visual representation (horizontal, tips pointing left/right):
//
//       innerA1 ─────────────── innerA2
//        ╱                           ╲
//   tip1                               tip2
//        ╲                           ╱
//       innerB1 ─────────────── innerB2
```

**Scaling formula** for any hex size:

```typescript
function getRoadPolygonPoints(hexSize: number): string {
  const innerHexSize = hexSize * 0.91;  // 91% of outer
  const tip = hexSize / 2;
  const inner = innerHexSize / 2;
  const perpDist = (hexSize - innerHexSize) * Math.sqrt(3) / 2;

  return [
    `${-tip},0`,           // tip1 (left)
    `${-inner},${-perpDist}`, // innerA1
    `${inner},${-perpDist}`,  // innerA2
    `${tip},0`,            // tip2 (right)
    `${inner},${perpDist}`,   // innerB2
    `${-inner},${perpDist}`,  // innerB1
  ].join(' ');
}
```

#### Edge Midpoint Offsets (Exact from Blazor)

```typescript
// Distance from tile center to edge midpoint = apothem = HexSize * sqrt(3)/2
// For HexSize=100: apothem ≈ 86.6

// Offsets for each HexSide (in pixels, for HexSize=100)
const ROAD_EDGE_OFFSETS: Record<HexSide, { x: number; y: number }> = {
  Top:         { x: 0,     y: -86.6 },   // Directly above
  TopRight:    { x: 75.0,  y: -43.3 },   // 30° from horizontal
  BottomRight: { x: 75.0,  y: 43.3 },    // 330° from horizontal
  Bottom:      { x: 0,     y: 86.6 },    // Directly below
  BottomLeft:  { x: -75.0, y: 43.3 },    // 210° from horizontal
  TopLeft:     { x: -75.0, y: -43.3 },   // 150° from horizontal
};

// Scale for any hex size
function getEdgeOffset(side: HexSide, hexSize: number): { x: number; y: number } {
  const scale = hexSize / 100;
  const base = ROAD_EDGE_OFFSETS[side];
  return { x: base.x * scale, y: base.y * scale };
}
```

**Formula derivation:**

```typescript
// Apothem = HexSize * sqrt(3)/2 = 86.6
// Midpoint angles (perpendicular outward from edge):
//   Top: 270° → (0, -apothem)
//   TopRight: 330° → (apothem*cos(30°), -apothem*sin(30°)) = (75, -43.3)
//   etc.
```

#### Edge Rotation Angles

```typescript
// Rotation to align canonical polygon with each edge
const ROAD_EDGE_ANGLES: Record<HexSide, number> = {
  Top: 0,           // Horizontal edge, no rotation
  TopRight: 60,     // Rotate 60° clockwise
  BottomRight: 120,
  Bottom: 180,      // Flip (same as Top visually)
  BottomLeft: 240,
  TopLeft: 300,
};
```

#### Road Component (SVG approach, matches Blazor)

```typescript
interface RoadProps {
  road: RoadModel;
  tileCenter: PixelPosition;  // Already in world coordinates (pan/zoom applied)
  hexSize: number;
}

function Road({ road, tileCenter, hexSize }: RoadProps) {
  const side = road.roadKey.hexSide;
  const offset = getEdgeOffset(side, hexSize);
  const angle = ROAD_EDGE_ANGLES[side];
  const points = getRoadPolygonPoints(hexSize);

  // Position = tile center + edge offset
  const midX = tileCenter.x + offset.x;
  const midY = tileCenter.y + offset.y;

  return (
    <polygon
      points={points}
      transform={`translate(${midX}, ${midY}) rotate(${angle})`}
      fill={road.owner?.color ?? 'rgba(255,255,255,0.3)'}
      stroke={road.owner?.secondaryColor ?? 'rgba(0,0,0,0.3)'}
      strokeWidth={2}
      className="road"
    />
  );
}
```

#### Alternative: CSS Clip-Path (Pure DOM)

If using divs instead of SVG:

```typescript
// Convert polygon points to CSS clip-path (percentage-based)
// Bounding box: width = HexSize (100), height = perpDist*2 (15.6)
const ROAD_CLIP_PATH = 'polygon(0% 50%, 4.5% 0%, 95.5% 0%, 100% 50%, 95.5% 100%, 4.5% 100%)';

function RoadDiv({ road, tileCenter, hexSize }: RoadProps) {
  const side = road.roadKey.hexSide;
  const offset = getEdgeOffset(side, hexSize);
  const angle = ROAD_EDGE_ANGLES[side];

  return (
    <div
      className="absolute"
      style={{
        left: tileCenter.x + offset.x,
        top: tileCenter.y + offset.y,
        width: hexSize,                    // Road length = hex side
        height: hexSize * 0.156,           // Road width = perpDist * 2
        transform: `translate(-50%, -50%) rotate(${angle}deg)`,
        backgroundColor: road.owner?.color ?? 'rgba(255,255,255,0.3)',
        clipPath: ROAD_CLIP_PATH,
      }}
    />
  );
}
```

**Recommendation (from Code Review):** Use the **SVG approach** (inline `<polygon>` inside positioned div) for maximum geometry fidelity. CSS `clip-path` may have precision issues at road/building overlap points. The SVG approach is "pure DOM in placement, SVG in internal shape."

**Why roads are indexed by tile:** Each road belongs to ONE tile (the one in its `RoadKey.tileKey`). The visual polygon spans two tiles, but data ownership is singular.

---

### Pan/Zoom Awareness for Board Elements

**Critical:** All board elements (tiles, roads, buildings, robber) must move together with pan/zoom. The solution: render everything inside a **single transformed container**.

#### World Coordinate System

```typescript
// All positions computed in "world coordinates" (hex grid space)
// Pan/zoom applied once at container level, not per-element

interface ViewportState {
  pan: { x: number; y: number };   // Offset in pixels
  zoom: number;                     // Scale factor (1 = 100%)
  hexSize: number;                  // Base hex size before zoom
}

// Effective hex size after zoom
const effectiveHexSize = viewport.hexSize * viewport.zoom;
```

#### Container Transform Architecture

```tsx
function BoardViewport({ children, viewport }: { children: ReactNode; viewport: ViewportState }) {
  return (
    <div className="viewport-container overflow-hidden w-full h-full">
      {/* Single transform applies to ALL board elements */}
      <div
        className="board-world"
        style={{
          transform: `translate(${viewport.pan.x}px, ${viewport.pan.y}px) scale(${viewport.zoom})`,
          transformOrigin: 'center center',
        }}
      >
        {children}
      </div>
    </div>
  );
}

// Usage:
<BoardViewport viewport={viewport}>
  <TilesLayer tiles={tiles} hexSize={hexSize} />
  <RoadsLayer roads={roads} hexSize={hexSize} />
  <BuildingsLayer buildings={buildings} hexSize={hexSize} />
  <RobberLayer position={robberPosition} hexSize={hexSize} />
</BoardViewport>
```

#### Position Calculation (World Coordinates)

```typescript
// Tile center in world coordinates (before pan/zoom)
function getTileCenter(coord: HexCoordinate, hexSize: number): PixelPosition {
  // Axial to pixel (flat-top hex)
  const x = hexSize * (3/2 * coord.q);
  const y = hexSize * (Math.sqrt(3)/2 * coord.q + Math.sqrt(3) * coord.r);
  return { x, y };
}

// Road position = tile center + edge offset (all in world coords)
// Building position = vertex position (all in world coords)
// Robber position = tile center (all in world coords)
```

#### Why This Works

```text
┌─────────────────────────────────────────────────────────┐
│  Viewport (screen space, fixed)                         │
│  ┌───────────────────────────────────────────────────┐  │
│  │  Board World (transformed by pan + zoom)          │  │
│  │  ┌─────────────────────────────────────────────┐  │  │
│  │  │  Tiles (world coordinates)                  │  │  │
│  │  │  Roads (world coordinates)                  │  │  │
│  │  │  Buildings (world coordinates)              │  │  │
│  │  │  Robber (world coordinates)                 │  │  │
│  │  └─────────────────────────────────────────────┘  │  │
│  │            ↑ All move together                    │  │
│  └───────────────────────────────────────────────────┘  │
│            ↑ Single transform: translate + scale        │
└─────────────────────────────────────────────────────────┘
```

**Benefits:**

1. **No per-element transform recalculation** on pan/zoom
2. **CSS handles interpolation** for smooth animations
3. **Consistent positioning** - roads always align with tile edges
4. **Simple click handling** - transform mouse coords once at viewport level

### Building Rendering (Pure DOM)

Buildings are circles positioned at hex vertices (where 3 hexes meet).

#### Vertex Geometry (from Blazor `BoardGeometry.cs`)

```typescript
// Flat-top hex vertices (from Blazor GetHexVertices):
// Starting at right (0°), going counter-clockwise
//
//         4 (TopLeft)     5 (TopRight)
//              ╲         ╱
//               ╲       ╱
//                ╲     ╱
//     3 (Left) ───────────── 0 (Right)
//                ╱     ╲
//               ╱       ╲
//              ╱         ╲
//     2 (BottomLeft)  1 (BottomRight)

// Vertex index to angle (degrees, counter-clockwise from right)
const VERTEX_ANGLES: number[] = [0, 60, 120, 180, 240, 300];

// HexPosition to vertex index mapping
const HEX_POSITION_TO_VERTEX: Record<HexPosition, number> = {
  Right: 0,
  BottomRight: 1,
  BottomLeft: 2,
  Left: 3,
  TopLeft: 4,
  TopRight: 5,
};

// Calculate vertex position in world coordinates
function getVertexPosition(
  tileCenter: PixelPosition,
  hexPosition: HexPosition,
  hexSize: number
): PixelPosition {
  const vertexIndex = HEX_POSITION_TO_VERTEX[hexPosition];
  const angleRad = VERTEX_ANGLES[vertexIndex] * (Math.PI / 180);
  return {
    x: tileCenter.x + hexSize * Math.cos(angleRad),
    y: tileCenter.y + hexSize * Math.sin(angleRad),
  };
}
```

#### HexSide to Adjacent Vertices

Roads connect two vertices. Each HexSide has two adjacent vertex indices:

```typescript
// HexSide to the two vertices that bound that edge
const HEXSIDE_TO_VERTICES: Record<HexSide, [number, number]> = {
  Top:         [4, 5],  // TopLeft ↔ TopRight
  TopRight:    [5, 0],  // TopRight ↔ Right
  BottomRight: [0, 1],  // Right ↔ BottomRight
  Bottom:      [1, 2],  // BottomRight ↔ BottomLeft
  BottomLeft:  [2, 3],  // BottomLeft ↔ Left
  TopLeft:     [3, 4],  // Left ↔ TopLeft
};
```

#### Building Component

```typescript
interface BuildingProps {
  building: BuildingModel;
  tileCenter: PixelPosition;  // World coordinates
  hexSize: number;
}

function Building({ building, tileCenter, hexSize }: BuildingProps) {
  const isCity = building.buildingType === 'City';
  const position = getVertexPosition(tileCenter, building.buildingKey.hexPosition, hexSize);

  // Size scales with hex size (default: 40px settlement, 48px city at hexSize=100)
  const size = (isCity ? 48 : 40) * (hexSize / 100);

  return (
    <div
      className="absolute rounded-full flex items-center justify-center"
      style={{
        left: position.x,
        top: position.y,
        width: size,
        height: size,
        transform: 'translate(-50%, -50%)',
        backgroundColor: building.owner?.color,
        border: '2px solid rgba(0,0,0,0.3)',
      }}
    >
      {/* Catan font glyph */}
      <span className="catan-font" style={{ fontSize: size * 0.5 }}>
        {isCity ? '\uE900' : '\uE926'}
      </span>
    </div>
  );
}
```

### Robber Rendering

The robber sits at a tile center, with support for "Fake Out" animation (from Blazor).

```typescript
interface RobberProps {
  position: PixelPosition;      // Current robber position (world coordinates)
  fakeOutTarget?: PixelPosition; // Optional: animate to this position and back
  hexSize: number;
}

function Robber({ position, fakeOutTarget, hexSize }: RobberProps) {
  const size = 60 * (hexSize / 100);  // Scale with hex size

  // Animate position when fakeOutTarget changes
  const animatedPosition = useFakeOutAnimation(position, fakeOutTarget);

  return (
    <motion.div
      className="absolute flex items-center justify-center pointer-events-none"
      animate={{
        left: animatedPosition.x,
        top: animatedPosition.y,
      }}
      transition={{ type: 'spring', stiffness: 300, damping: 30 }}
      style={{
        width: size,
        height: size,
        transform: 'translate(-50%, -50%)',
      }}
    >
      {/* Robber icon - semi-transparent dark figure */}
      <div
        className="rounded-full bg-gray-800/80"
        style={{ width: size, height: size }}
      />
    </motion.div>
  );
}

// Hook for fake-out animation (move to target, pause, return)
function useFakeOutAnimation(
  position: PixelPosition,
  fakeOutTarget?: PixelPosition
): PixelPosition {
  const [animatedPos, setAnimatedPos] = useState(position);

  useEffect(() => {
    if (!fakeOutTarget) {
      setAnimatedPos(position);
      return;
    }

    // Animate to fake target
    setAnimatedPos(fakeOutTarget);

    // After delay, return to real position
    const timer = setTimeout(() => {
      setAnimatedPos(position);
    }, 500);

    return () => clearTimeout(timer);
  }, [position, fakeOutTarget]);

  return animatedPos;
}
```

**Fake Out Use Case:** When a player considers moving the robber but changes their mind, the robber animates to the potential tile and back, providing visual feedback without committing the move.

### Performance Optimizations

**1. Memoization (useMemo for calculations):**

```typescript
// Memoize expensive calculations
const boardLookups = useMemo(() => buildLookups(gameModel), [gameModel]);
const visibleHexes = useMemo(() => getVisibleHexes(pan, zoom, viewport), [pan, zoom, viewport]);
```

**2. React.memo for Components (REQUIRED):**

Per code review, these components MUST be wrapped with `React.memo` to prevent re-renders when parent updates but props haven't changed:

```typescript
// GameTile - only re-renders when tile data changes
export const GameTile = React.memo(function GameTile({ tile, position }: TileProps) {
  // ...
});

// Road - only re-renders when road state/owner changes
export const Road = React.memo(function Road({ road, tileCenter, hexSize }: RoadProps) {
  // ...
});

// Building - only re-renders when building state/owner changes
export const Building = React.memo(function Building({ building, tileCenter, hexSize }: BuildingProps) {
  // ...
});
```

**Why required:** Without `React.memo`, every SignalR update causes ALL tiles/roads/buildings to re-render even if only one item changed. With `React.memo` + reconciliation, only changed items re-render.

**3. CSS-based hover (no re-render):**

```css
.road:hover { filter: brightness(1.2); }
.building:hover { transform: translate(-50%, -50%) scale(1.1); }
```

**3. Layer separation:**

```typescript
// Static layer (tiles/harbors) - rarely changes
<div className="board-static-layer">
  {visibleHexes.map(coord => <HexContent key={coordKey(coord)} ... />)}
</div>

// Interactive layer (roads/buildings) - changes on game actions
<div className="board-interactive-layer">
  <RoadsLayer roads={gameModel.roads} />
  <BuildingsLayer buildings={gameModel.buildings} />
</div>
```

**4. Virtual rendering for infinite grid:**

Only render hexes visible in viewport (already in hex-test implementation):

```typescript
const visibleHexes = useMemo(() => {
  // Calculate q/r ranges based on viewport bounds
  const minQ = Math.floor((worldLeft - margin) / (1.5 * hexSize));
  const maxQ = Math.ceil((worldRight + margin) / (1.5 * hexSize));
  // ... generate only visible coordinates
}, [pan, zoom, viewportSize]);
```

**5. Hash-based re-render prevention:**

```typescript
// Only re-render roads layer when road state changes
const roadsHash = useMemo(() =>
  gameModel.roads.map(r => `${r.roadKey}:${r.owner?.id}`).join('|'),
  [gameModel.roads]
);
```

### Store Architecture (from Code Review)

**Critical requirement from Phase 0.4:** The game page must work with a multi-game store, not a singleton.

#### Multi-Game Store Pattern

```typescript
// gameStore.ts - supports multiple games for spectating/previewing
interface GameStore {
  games: Record<string, GameModel>;  // Multiple games by ID
  activeGameId: string | null;       // Currently displayed game

  // Selectors
  getActiveGame: () => GameModel | null;
  getGame: (id: string) => GameModel | null;

  // Actions
  updateGame: (id: string, model: GameModel) => void;
  setActiveGame: (id: string) => void;
  removeGame: (id: string) => void;
}

// Game page usage
function GamePage({ gameId }: { gameId: string }) {
  const gameModel = useGameStore(state => state.games[gameId]);
  // ...
}
```

#### SignalR Reconciliation (Critical for Performance)

**Problem:** Every `GameStateUpdated` SignalR event sends a full `GameModel`. Without reconciliation, React sees a new object tree and re-renders everything.

**Solution:** Structural sharing via reconciliation utilities:

```typescript
// lib/utils/reconciliation.ts
export function reconcileGameModel(
  prev: GameModel | null,
  next: GameModel
): GameModel {
  if (!prev) return next;

  // Only create new references for changed parts
  return {
    ...next,
    tiles: reconcileArray(prev.tiles, next.tiles, t => t.tileKey),
    roads: reconcileArray(prev.roads, next.roads, r => roadKeyString(r.roadKey)),
    buildings: reconcileArray(prev.buildings, next.buildings, b => buildingKeyString(b.buildingKey)),
    harbors: reconcileArray(prev.harbors, next.harbors, h => harborKeyString(h.harborKey)),
    players: reconcileArray(prev.players, next.players, p => p.id),
  };
}

function reconcileArray<T>(
  prev: T[],
  next: T[],
  getKey: (item: T) => string
): T[] {
  // If arrays are identical by reference, return prev
  if (prev === next) return prev;

  // Build lookup for previous items
  const prevMap = new Map(prev.map(item => [getKey(item), item]));

  let hasChanges = false;
  const result = next.map(nextItem => {
    const key = getKey(nextItem);
    const prevItem = prevMap.get(key);

    // If item exists and is deeply equal, reuse reference
    if (prevItem && deepEqual(prevItem, nextItem)) {
      return prevItem;
    }

    hasChanges = true;
    return nextItem;
  });

  // If no changes and same length, return original array
  if (!hasChanges && prev.length === next.length) {
    return prev;
  }

  return result;
}
```

**Store integration:**

```typescript
// In gameStore.ts
updateGame: (id, newModel) => {
  set(state => {
    const prevModel = state.games[id];
    const reconciledModel = reconcileGameModel(prevModel, newModel);

    // If nothing changed, return same state (no re-render)
    if (reconciledModel === prevModel) {
      return state;
    }

    return {
      games: { ...state.games, [id]: reconciledModel },
    };
  });
},
```

**Why this matters:**

- Without reconciliation: 60 SignalR ticks/sec = 60 full re-renders
- With reconciliation: Only changed components re-render
- Tiles array rarely changes → reuse reference → TilesLayer skips render
- Roads/buildings change on player actions → only affected layers re-render

#### Player Profiles Store (Separate)

Player profiles should live in a separate store for cross-game persistence:

```typescript
// playerProfilesStore.ts - global cache, not per-game
interface PlayerProfilesStore {
  profiles: Map<string, PlayerProfile>;

  getProfile: (userId: string) => PlayerProfile | undefined;
  setProfile: (userId: string, profile: PlayerProfile) => void;
}

// PlayersPanel uses both stores
function PlayersPanel({ gameId }: { gameId: string }) {
  const players = useGameStore(state => state.games[gameId]?.players ?? []);
  const profiles = usePlayerProfilesStore(state => state.profiles);

  return (
    <div>
      {players.map(player => (
        <PlayerCard
          key={player.id}
          player={player}
          profile={profiles.get(player.userId)}
        />
      ))}
    </div>
  );
}
```

### New Files for Rendering

```text
react-ui/
├── components/game/
│   ├── board/
│   │   ├── BoardLookups.ts          # HexGridCollection builders
│   │   ├── BoardConstants.ts        # ROAD_EDGE_ANGLES, VERTEX_ANGLES, etc.
│   │   ├── RoadsLayer.tsx           # All roads as one layer
│   │   ├── BuildingsLayer.tsx       # All buildings as one layer
│   │   ├── RobberLayer.tsx          # Robber piece
│   │   ├── TileOverlay.tsx          # Clickable hex overlays (robber)
│   │   ├── RobberTargetMenu.tsx     # Context menu for robber targeting
│   │   └── useRollDimming.ts        # Hook for 5-second roll dimming
│   │
│   └── tiles/
│       ├── GameTile.tsx             # Single tile with resource
│       ├── Road.tsx                 # Single road polygon
│       ├── Building.tsx             # Single building circle
│       └── NumberToken.tsx          # Tile number with pips
```

---

## Board Interactions

This section covers how users interact with the board: clicking tiles, roads, and buildings based on game state.

### Interaction Architecture

The board uses **layered click targets** (bottom to top):

```text
Layer 1: TileOverlay     ─ Clickable hexes for robber placement
Layer 2: RoadsLayer      ─ Clickable roads for building
Layer 3: BuildingsLayer  ─ Clickable vertices for settlements/cities
Layer 4: RobberLayer     ─ Robber piece (not clickable, just visual)
```

Each layer only captures clicks when appropriate for the current game state.

### Click Targets by Game State

| GameState | Tiles | Roads | Buildings |
|-----------|-------|-------|-----------|
| PickingBoard | - | - | Show only (evaluation) |
| AllocateResourceForward | - | Buildable clickable | Buildable clickable |
| AllocateResourceReverse | - | Buildable clickable | Buildable clickable |
| WaitingForRoll | - | Buildable clickable | Buildable clickable |
| WaitingForNext | - | Buildable clickable | Buildable clickable |
| MustMoveRobber | **All land tiles clickable** | - | - |
| GameOver | - | - | - |

### Tile Clicks (Robber Placement)

**When:** `GameState.MustMoveRobber`

```typescript
interface TileOverlayProps {
  tiles: TileModel[];
  robberLocation: HexCoordinate;
  onTileClick: (tileKey: HexCoordinate) => void;
}

function TileOverlay({ tiles, robberLocation, onTileClick }: TileOverlayProps) {
  const isClickable = (tile: TileModel) =>
    tile.resourceType !== 'Sea' &&
    !hexEquals(tile.tileKey.hexCoordinates, robberLocation);

  return (
    <div className="tile-overlay pointer-events-auto">
      {tiles.filter(isClickable).map(tile => (
        <div
          key={coordKey(tile.tileKey.hexCoordinates)}
          className="absolute hex-clip-flat cursor-pointer hover:bg-white/20"
          style={getHexStyle(tile.tileKey.hexCoordinates)}
          onClick={() => onTileClick(tile.tileKey.hexCoordinates)}
        />
      ))}
    </div>
  );
}
```

**Click-to-coordinate conversion:** Uses `pixelToHex()` from hex-geometry.ts.

### Road Clicks (Road Building)

**When:** Player has `Entitlement.Road` AND road is `RoadState.Buildable`

```typescript
interface RoadsLayerProps {
  roads: RoadModel[];
  onRoadClick: (roadKey: RoadKey) => void;
}

function RoadsLayer({ roads, onRoadClick }: RoadsLayerProps) {
  return (
    <div className="roads-layer pointer-events-auto">
      {roads.map(road => {
        const isBuildable = road.roadState === 'Buildable';
        const isOwned = road.ownerId !== null;

        // Only render if owned or buildable
        if (!isOwned && !isBuildable) return null;

        return (
          <Road
            key={roadKey(road.roadKey)}
            road={road}
            onClick={isBuildable ? () => onRoadClick(road.roadKey) : undefined}
          />
        );
      })}
    </div>
  );
}
```

### Building Clicks (Settlement/City)

**When:** Player has `Entitlement.Settlement` or `Entitlement.City`

```typescript
interface BuildingsLayerProps {
  buildings: BuildingModel[];
  starThreshold: number;  // From BoardMeasurement slider
  onBuildingClick: (buildingKey: BuildingKey) => void;
}

function BuildingsLayer({ buildings, starThreshold, onBuildingClick }: BuildingsLayerProps) {
  return (
    <div className="buildings-layer pointer-events-auto">
      {buildings.map(building => {
        const isBuildable = building.buildingState === 'PossibleSettlement';
        const isOwned = building.ownerId !== null;
        const isHidden = building.stars < starThreshold && !isOwned;

        return (
          <Building
            key={buildingKey(building.buildingKey)}
            building={building}
            isHidden={isHidden}
            onClick={isBuildable ? () => onBuildingClick(building.buildingKey) : undefined}
          />
        );
      })}
    </div>
  );
}
```

### Hover/Highlight States

All hover states are **CSS-only** (no React re-renders):

```css
/* Roads: brighten on hover */
.road-buildable {
  opacity: 0.5;
  cursor: pointer;
}
.road-buildable:hover {
  opacity: 0.75;
  filter: brightness(1.2);
}

/* Buildings: reveal hidden spots on hover */
.building-hidden {
  opacity: 0.1;
}
.building-hidden:hover {
  opacity: 0.5;
}

/* Buildings: scale on hover */
.building-buildable:hover {
  transform: translate(-50%, -50%) scale(1.1);
}

/* Tiles: highlight on hover during robber placement */
.tile-overlay-hex:hover {
  background-color: rgba(255, 255, 255, 0.2);
}
```

### Roll Dimming (Client-Side Only)

After a dice roll, non-matching tiles dim for 5 seconds to highlight which tiles produce resources.

**From Blazor `BaseLayer.razor`:**

```typescript
// Local state - NOT part of GameModel
const [rollDimming, setRollDimming] = useState<number | null>(null);

const handleRoll = async (die1: number, die2: number) => {
  await gameService.roll(die1, die2);

  // Start local dimming timer (client-side only)
  const rollTotal = die1 + die2;
  setRollDimming(rollTotal);

  // Clear after 5 seconds (client-only feature)
  setTimeout(() => setRollDimming(null), 5000);
};

// In tile rendering (matches Blazor logic)
// Note: tile.number > 0 check skips the desert tile (which has no number)
const isDimmed = rollDimming !== null &&
                 tile.number !== rollDimming &&
                 tile.number > 0;
```

```css
/* Match Blazor: .tile-dimmed { opacity: 0.5; } */
.tile-dimmed {
  opacity: 0.5;
  transition: opacity 0.3s ease;
}

/* Optional: More dramatic dimming for better contrast */
.tile-dimmed-dramatic {
  opacity: 0.3;
  filter: grayscale(100%);
  transition: opacity 0.3s, filter 0.3s;
}
```

**useRollDimming hook:**

```typescript
// lib/hooks/useRollDimming.ts
export function useRollDimming(durationMs: number = 5000) {
  const [rolledNumber, setRolledNumber] = useState<number | null>(null);

  const triggerDimming = useCallback((total: number) => {
    setRolledNumber(total);
  }, []);

  // Auto-clear after duration
  useEffect(() => {
    if (rolledNumber === null) return;

    const timer = setTimeout(() => {
      setRolledNumber(null);
    }, durationMs);

    return () => clearTimeout(timer);
  }, [rolledNumber, durationMs]);

  // Helper to check if a tile should be dimmed
  const isTileDimmed = useCallback((tileNumber: number) => {
    return rolledNumber !== null &&
           tileNumber !== rolledNumber &&
           tileNumber > 0;  // Skip desert
  }, [rolledNumber]);

  return { rolledNumber, triggerDimming, isTileDimmed };
}
```

### Pointer Events Strategy

```css
/* Static layer: no pointer events */
.board-static-layer {
  pointer-events: none;
}

/* Interactive layer: enable only on specific elements */
.board-interactive-layer {
  pointer-events: none;  /* Default off */
}

.board-interactive-layer .tile-overlay,
.board-interactive-layer .roads-layer,
.board-interactive-layer .buildings-layer {
  pointer-events: auto;  /* Enable on interactive elements */
}
```

### Game Page Event Handlers

```typescript
// Game page wires up all click handlers
function GamePage({ gameId }: { gameId: string }) {
  const { gameModel, gameState } = useGameStore();

  // Tile click → Robber placement
  const handleTileClick = async (tileKey: HexCoordinate) => {
    if (gameState !== 'MustMoveRobber') return;
    // Show robber target menu, then call:
    await gameService.moveRobber(tileKey, targetPlayerId);
  };

  // Road click → Road building
  const handleRoadClick = async (roadKey: RoadKey) => {
    if (!canPurchase('Road')) return;
    await gameService.buildRoad(roadKey);
  };

  // Building click → Settlement/City
  const handleBuildingClick = async (buildingKey: BuildingKey) => {
    if (!canPurchase('Settlement') && !canPurchase('City')) return;
    await gameService.buildBuilding(buildingKey);
  };

  return (
    <div className="game-page">
      <BoardViewport>
        {/* Static layer */}
        <div className="board-static-layer">
          <HexGrid ... />
        </div>

        {/* Interactive layer */}
        <div className="board-interactive-layer">
          {gameState === 'MustMoveRobber' && (
            <TileOverlay tiles={gameModel.tiles} onTileClick={handleTileClick} />
          )}
          <RoadsLayer roads={gameModel.roads} onRoadClick={handleRoadClick} />
          <BuildingsLayer buildings={gameModel.buildings} onBuildingClick={handleBuildingClick} />
          <RobberLayer position={gameModel.robber.hexCoordinates} />
        </div>
      </BoardViewport>

      {/* Floating panels */}
      <FloatingPanelContainer ... />
    </div>
  );
}
```

### Robber Target Context Menu

When a tile is clicked during MustMoveRobber, show a context menu:

```typescript
const [robberMenu, setRobberMenu] = useState<{
  visible: boolean;
  position: { x: number; y: number };
  tile: TileModel | null;
  targets: PlayerModel[];
} | null>(null);

const handleTileClick = (tileKey: HexCoordinate, event: React.MouseEvent) => {
  const tile = getTile(tileKey);
  const targets = getPlayersOnTile(tile);

  setRobberMenu({
    visible: true,
    position: { x: event.clientX, y: event.clientY },
    tile,
    targets,
  });
};

// Render context menu
{robberMenu?.visible && (
  <div
    className="fixed bg-gray-800 rounded shadow-lg p-2 z-50"
    style={{ left: robberMenu.position.x, top: robberMenu.position.y }}
  >
    <div className="text-sm font-bold mb-2">Move Robber</div>
    {robberMenu.targets.map(player => (
      <button
        key={player.id}
        className="block w-full text-left px-2 py-1 hover:bg-gray-700"
        onClick={() => selectRobberTarget(player.id)}
      >
        Target {player.name}
      </button>
    ))}
    <button onClick={() => selectRobberTarget(null)}>
      Nobody
    </button>
  </div>
)}
```

### New Files for Interactions

```text
react-ui/components/game/
├── board/
│   ├── TileOverlay.tsx        # Clickable hex overlays for robber
│   ├── RobberTargetMenu.tsx   # Context menu for robber targeting
│   └── useRollDimming.ts      # Hook for 5-second roll dimming
```

---

## Component Architecture

### New Files

```text
react-ui/
├── app/game/[id]/
│   └── page.tsx                    # Main game page
│
├── components/game/
│   ├── index.ts                    # Re-exports
│   │
│   ├── viewport/
│   │   └── InfiniteWaterViewport.tsx   # Pan/zoom water background
│   │
│   ├── panels/
│   │   ├── FloatingPanel.tsx           # Base draggable/resizable
│   │   ├── FloatingPanelContainer.tsx  # Manages all panels
│   │   ├── PlayersPanel.tsx            # Player cards
│   │   └── ResourcesPanel.tsx          # Total resources
│   │
│   ├── controls/
│   │   ├── HexControlCluster.tsx       # Base hex cluster component
│   │   ├── DiceCluster.tsx             # Two 7-hex dice clusters
│   │   ├── ActionCluster.tsx           # Game action controls
│   │   └── MeasurementCluster.tsx      # Nested hex for measurements
│   │
│   ├── board/
│   │   ├── BoardLookups.ts             # HexGridCollection builders for O(1) lookup
│   │   ├── BoardConstants.ts           # ROAD_EDGE_ANGLES, VERTEX_ANGLES, etc.
│   │   ├── RoadsLayer.tsx              # All roads as one layer
│   │   ├── BuildingsLayer.tsx          # All buildings as one layer
│   │   ├── RobberLayer.tsx             # Robber piece
│   │   ├── TileOverlay.tsx             # Clickable hex overlays (robber)
│   │   ├── RobberTargetMenu.tsx        # Context menu for robber targeting
│   │   └── useRollDimming.ts           # Hook for 5-second roll dimming
│   │
│   └── tiles/
│       ├── GameTile.tsx                # Board tile (resource + number)
│       ├── HarborHex.tsx               # Harbor hex with side indicator
│       ├── HarborIcon.tsx              # Center circle with trade ratio
│       ├── Road.tsx                    # Single road (CSS clip-path polygon)
│       ├── Building.tsx                # Single building (circle + glyph)
│       └── NumberToken.tsx             # Tile number with pip dots
│
└── lib/stores/
    └── layoutStore.ts              # Panel positions with persistence
```

### Store: `layoutStore.ts`

```typescript
interface PanelLayout {
  id: string;
  position: { x: number; y: number };
  size: { width: number; height: number };
  visible: boolean;
  minimized: boolean;  // Collapsed to title bar
  zIndex: number;      // Click brings to front
}

interface LayoutState {
  panels: Record<string, PanelLayout>;
  viewport: { pan: { x: number; y: number }; zoom: number };
  starFilter: number | null;  // 8-13 or null
  resourceFilter: string | null;  // 'ore' | 'sheep' | etc. or null

  // Actions
  setPanelPosition: (id: string, pos: Position) => void;
  setPanelSize: (id: string, size: Size) => void;
  toggleMinimize: (id: string) => void;  // Toggle minimized state
  bringToFront: (id: string) => void;    // Increment zIndex
  resetLayout: () => void;
  setStarFilter: (stars: number | null) => void;
  setResourceFilter: (resource: string | null) => void;
}

// Use Zustand with persist middleware (pattern from uiStore.ts)
```

---

## Implementation Phases

### Phase 1: Foundation

**Files:** `page.tsx`, `InfiniteWaterViewport.tsx`, `FloatingPanel.tsx`, `layoutStore.ts`

1. Create empty `game/[id]/page.tsx` with layout structure
2. Extract infinite viewport logic from hex-test into `InfiniteWaterViewport.tsx`
3. Create `FloatingPanel.tsx` with framer-motion drag/resize
4. Create `layoutStore.ts` with Zustand + localStorage persistence
5. Wire up one test panel to verify drag/resize/persist works

**Verification:**

- Water hexes fill viewport at all zoom levels
- Test panel drags, resizes, persists across refresh
- Reset button restores default position

### Phase 2: Hex Control Clusters

**Files:** `HexControlCluster.tsx`, `DiceCluster.tsx`, `ActionCluster.tsx`

1. Create `HexControlCluster.tsx` - generic hex cluster with click handlers
2. Create `DiceCluster.tsx` - two side-by-side 7-hex clusters
3. Create `ActionCluster.tsx` - game controls cluster
4. Wire up to game actions (Next, Undo, Redo via gameStore)

**Verification:**

- Dice selection flow works (select both → confirm)
- Action buttons trigger correct game commands
- State message displays in center hex

### Phase 3: Measurement Cluster

**Files:** `MeasurementCluster.tsx`

1. Create nested hex cluster (outer ring + inner cluster)
2. Render resources with images and counts from game state
3. Implement star filter (click number → update layoutStore)
4. Implement resource filter
5. Show variance/balance in south hex

**Verification:**

- Resource counts match game state
- Star filter updates board highlighting
- Reset clears filters

### Phase 4: Board Rendering (Tiles, Roads, Buildings)

**Files:** See "Efficient Board Rendering" section and "New Files for Rendering"

**4a. Data Structures:**

1. Create `BoardLookups.ts` - builds `HexGridCollection` maps for O(1) lookup
2. Create `BoardConstants.ts` - `ROAD_EDGE_ANGLES`, `ROAD_EDGE_OFFSETS`, `VERTEX_ANGLES`

**4b. Tiles & Harbors:**

1. Create `GameTile.tsx` - hex with resource background, number token, wood border
2. Create `HarborHex.tsx` - water background with center icon and side indicator
3. Create `NumberToken.tsx` - circular token with number and pip dots
4. Use `HexGridCollection` for O(1) coordinate lookups (not `array.find()`)
5. Add flip animation (water → tile/harbor) on game load

**4c. Roads (the complex part):**

1. Create `Road.tsx` - single road using CSS clip-path polygon
2. Create `RoadsLayer.tsx` - renders all roads as one layer
3. Implement `ROAD_EDGE_OFFSETS` for positioning at edge midpoints
4. Implement `ROAD_EDGE_ANGLES` for rotation (0°, 60°, 120°, 180°, 240°, 300°)
5. CSS-based hover effects (no re-render)

**4d. Buildings:**

1. Create `Building.tsx` - circle with Catan font glyph
2. Create `BuildingsLayer.tsx` - renders all buildings as one layer
3. Implement `getVertexPosition()` for hex vertex positioning

**4e. Interactions (see "Board Interactions" section):**

1. Create `TileOverlay.tsx` - clickable hex overlays for robber placement
2. Create `RobberTargetMenu.tsx` - context menu for selecting steal target
3. Create `useRollDimming.ts` - hook for 5-second roll dimming effect
4. Wire up click handlers: tiles → robber, roads → build, buildings → build/upgrade
5. Implement CSS hover states (no re-render)
6. Implement pointer-events strategy (static layer off, interactive layer on)

**Verification:**

- Board tiles render at correct coordinates from GameModel
- Harbors render in water ring with correct side indicators
- Roads align perfectly with hex gaps (no visual artifacts)
- Clicking road during gameplay triggers build command
- Clicking building spot triggers settlement/city placement
- Clicking tile during MustMoveRobber shows target menu
- Hover states work without causing re-renders
- Buildings render at hex vertices
- Layer z-order: tiles → roads → buildings
- Performance: 60fps during pan/zoom with full board

### Phase 5: Player Panels & Polish

**Files:** `PlayersPanel.tsx`, `ResourcesPanel.tsx`

1. Create floating players panel (port from Blazor design)
2. Create floating resources panel
3. Add panel collapse/expand toggle
4. Performance optimization (memoization)
5. Accessibility (keyboard navigation)

**Verification:**

- All panels functional and draggable
- Layout persists correctly
- Performance smooth at 60fps

---

## Technical Notes

### Framer Motion for Drag/Resize

```typescript
// FloatingPanel.tsx uses framer-motion (already installed)
<motion.div
  drag
  dragMomentum={false}
  dragConstraints={parentRef}
  onDragEnd={(_, info) => setPanelPosition(id, info.point)}
>
  {/* Panel content with transform scale for resize */}
  <div style={{ transform: `scale(${scale})`, transformOrigin: 'top left' }}>
    {children}
  </div>
</motion.div>
```

### Content Scaling on Resize

Panels have a "base size" for content layout. When resized, content scales uniformly:

```typescript
const scaleX = actualWidth / baseWidth;
const scaleY = actualHeight / baseHeight;
const scale = Math.min(scaleX, scaleY); // Uniform scale
```

### Nested Hex Geometry

For the measurement cluster, use two HexGrid instances:

1. Outer: `HEX_LAYOUTS.CLUSTER_7` at larger size (e.g., 100px)
2. Inner: `HEX_LAYOUTS.CLUSTER_7` at smaller size (e.g., 40px) centered in outer

```typescript
// Outer hex cluster
<HexGrid hexSize={100} items={outerItems} />

// Inner hex cluster (positioned at outer center)
<div style={{ position: 'absolute', top: '50%', left: '50%', transform: 'translate(-50%, -50%)' }}>
  <HexGrid hexSize={40} items={innerItems} />
</div>
```

---

## Key Files Reference

| File | Purpose |
|------|---------|
| `react-ui/app/hex-test/page.tsx` | Infinite viewport pan/zoom (lines 117-228) |
| `react-ui/components/hex-grid/hex-geometry.ts` | HEX_LAYOUTS.CLUSTER_7, coordinate math |
| `react-ui/components/hex-grid/HexGrid.tsx` | Core grid component to reuse |
| `react-ui/lib/stores/uiStore.ts` | Zustand persist pattern |
| `react-ui/lib/geometry/boardConstants.ts` | HEX_SIZE=100, CENTER coordinates |
| `Catan3.WebUI/Pages/Game.razor` | Blazor reference for game controls |

---

## Design Decisions

1. **Mobile/Tablet**: Floating panels work on all devices (same UX everywhere). Touch drag for positioning.

2. **Panel Z-Order**: Click brings panel to front. Track z-index in layoutStore, increment on click.

3. **Keyboard Shortcuts**: Enter key triggers Next action. Future enhancement - arrow keys for hex cluster navigation.

---

## Configuration Persistence (localStorage)

Layout settings are **per board type** and stored in **localStorage** (not in the database). This keeps the settings local to the machine rather than global across all devices.

### Why localStorage (Not Database)

- **Local to machine**: Different computers/monitors may need different layouts
- **No server roundtrip**: Instant load/save
- **Privacy**: Panel positions are personal preference, not game data
- **Offline capable**: Works without network

### Storage Keys

```typescript
// Key pattern: catan:layout:{boardType}
const LAYOUT_STORAGE_KEYS = {
  regular: 'catan:layout:regular',    // Standard 19-tile board
  expansion: 'catan:layout:expansion', // 30-tile expansion board
} as const;

type BoardType = keyof typeof LAYOUT_STORAGE_KEYS;
```

### What Gets Persisted

```typescript
interface PersistedLayout {
  // Per-panel settings
  panels: Record<string, {
    position: { x: number; y: number };
    size: { width: number; height: number };
    minimized: boolean;
    visible: boolean;
  }>;

  // Viewport settings
  viewport: {
    hexSize: number;       // Default: 100, adjustable via zoom
    pan: { x: number; y: number };
    zoom: number;
  };

  // Version for migration
  version: number;
}
```

### Default Values

```typescript
const DEFAULT_LAYOUT: PersistedLayout = {
  panels: {
    dice: { position: { x: 20, y: 20 }, size: { width: 280, height: 140 }, minimized: false, visible: true },
    actions: { position: { x: 20, y: 180 }, size: { width: 200, height: 240 }, minimized: false, visible: true },
    measurements: { position: { x: -20, y: 20 }, size: { width: 300, height: 320 }, minimized: false, visible: true },
    players: { position: { x: -20, y: 360 }, size: { width: 300, height: 400 }, minimized: false, visible: true },
    resources: { position: { x: 20, y: 440 }, size: { width: 200, height: 100 }, minimized: false, visible: true },
  },
  viewport: {
    hexSize: 100,  // Reasonable default, user can adjust
    pan: { x: 0, y: 0 },
    zoom: 1,
  },
  version: 1,
};
```

**Note:** Negative position values (e.g., `x: -20`) mean "from right edge" - handled by the panel container.

### Load/Save Behavior

```typescript
// In layoutStore.ts
import { create } from 'zustand';
import { persist, createJSONStorage } from 'zustand/middleware';

interface LayoutStore extends PersistedLayout {
  boardType: BoardType;

  // Actions
  loadLayout: (boardType: BoardType) => void;
  saveLayout: () => void;
  resetLayout: () => void;
  setPanelPosition: (panelId: string, position: { x: number; y: number }) => void;
  setPanelSize: (panelId: string, size: { width: number; height: number }) => void;
  setHexSize: (size: number) => void;
  setViewport: (pan: { x: number; y: number }, zoom: number) => void;
}

export const useLayoutStore = create<LayoutStore>()(
  persist(
    (set, get) => ({
      ...DEFAULT_LAYOUT,
      boardType: 'regular',

      loadLayout: (boardType: BoardType) => {
        const key = LAYOUT_STORAGE_KEYS[boardType];
        const stored = localStorage.getItem(key);

        if (stored) {
          const layout = JSON.parse(stored) as PersistedLayout;
          set({ ...layout, boardType });
        } else {
          set({ ...DEFAULT_LAYOUT, boardType });
        }
      },

      saveLayout: () => {
        const state = get();
        const key = LAYOUT_STORAGE_KEYS[state.boardType];
        const toSave: PersistedLayout = {
          panels: state.panels,
          viewport: state.viewport,
          version: state.version,
        };
        localStorage.setItem(key, JSON.stringify(toSave));
      },

      resetLayout: () => {
        const { boardType } = get();
        const key = LAYOUT_STORAGE_KEYS[boardType];
        localStorage.removeItem(key);
        set({ ...DEFAULT_LAYOUT, boardType });
      },

      // ... other actions auto-save on change
    }),
    {
      name: 'catan-layout-temp', // Zustand's internal key (we manage per-board manually)
      storage: createJSONStorage(() => localStorage),
    }
  )
);
```

### When to Load/Save

| Event | Action |
|-------|--------|
| Game page mount | `loadLayout(boardType)` based on GameModel.boardType |
| Panel drag end | Auto-save position |
| Panel resize end | Auto-save size |
| Zoom/pan change | Debounced save (500ms) |
| Minimize toggle | Auto-save state |
| Reset button click | `resetLayout()` |

### HexSize Adjustment

HexSize starts at 100 (default) but adjusts based on zoom level:

```typescript
// When user zooms, we can optionally recalculate hexSize
// to fit the board nicely at different zoom levels
const effectiveHexSize = baseHexSize * zoom;

// Or, allow direct hexSize adjustment via UI control
// (future enhancement: slider in measurements panel)
```

### Migration Strategy

```typescript
function migrateLayout(stored: unknown): PersistedLayout {
  if (!stored || typeof stored !== 'object') {
    return DEFAULT_LAYOUT;
  }

  const layout = stored as Partial<PersistedLayout>;

  // Version 0 → 1: Add hexSize to viewport
  if (!layout.version || layout.version < 1) {
    return {
      ...DEFAULT_LAYOUT,
      panels: layout.panels ?? DEFAULT_LAYOUT.panels,
      viewport: {
        ...DEFAULT_LAYOUT.viewport,
        ...(layout.viewport ?? {}),
      },
      version: 1,
    };
  }

  return layout as PersistedLayout;
}
```

### UI for Reset

Add a reset button to each panel's title bar (or a global "Reset All" in settings):

```typescript
// In FloatingPanel.tsx header
<button
  onClick={() => useLayoutStore.getState().resetLayout()}
  title="Reset all panels to default positions"
>
  ↺
</button>
```
