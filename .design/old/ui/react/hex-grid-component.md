# HexGrid Component Architecture

**Created:** 2026-01-25
**Status:** Implementation pending

## Overview

HexGrid is a layout component that positions hex-shaped tiles using Red Blob Games coordinate math.
It handles positioning and optional gap rendering. Content components handle their own styling.

## Coordinate System

Uses **cubic coordinates** (q, r, s) for consistency with C# `HexCoordinates` class.

```typescript
interface HexCoordinate {
  q: number;
  r: number;
  s: number;  // Constraint: q + r + s === 0
}

// Helper to create coordinates (computes s automatically)
function hexCoord(q: number, r: number): HexCoordinate {
  return { q, r, s: -q - r };
}
```

### Direction Vectors

```typescript
const DIRECTIONS = {
  North:     { q: 0, r: -1, s: 1 },
  NorthEast: { q: 1, r: -1, s: 0 },
  SouthEast: { q: 1, r: 0, s: -1 },
  South:     { q: 0, r: 1, s: -1 },
  SouthWest: { q: -1, r: 1, s: 0 },
  NorthWest: { q: -1, r: 0, s: 1 },
} as const;
```

### Utility Functions

```typescript
// Manhattan distance between two hexes
function distance(a: HexCoordinate, b: HexCoordinate): number {
  return (Math.abs(a.q - b.q) + Math.abs(a.r - b.r) + Math.abs(a.s - b.s)) / 2;
}

// Get neighbor in a direction
function getNeighbor(coord: HexCoordinate, dir: keyof typeof DIRECTIONS): HexCoordinate {
  const d = DIRECTIONS[dir];
  return { q: coord.q + d.q, r: coord.r + d.r, s: coord.s + d.s };
}

// Check if two hexes are adjacent
function isAdjacent(a: HexCoordinate, b: HexCoordinate): boolean {
  return distance(a, b) === 1;
}

// Generate spiral coordinates for N items (center + surrounding rings)
function getSpiralCoordinates(count: number): HexCoordinate[] {
  const coords: HexCoordinate[] = [{ q: 0, r: 0, s: 0 }]; // Center
  if (count <= 1) return coords.slice(0, count);

  let ring = 1;
  while (coords.length < count) {
    // Start at "north" of ring, walk clockwise
    let coord = { q: 0, r: -ring, s: ring };
    const directions: (keyof typeof DIRECTIONS)[] = [
      'SouthEast', 'South', 'SouthWest', 'NorthWest', 'North', 'NorthEast'
    ];
    for (const dir of directions) {
      for (let i = 0; i < ring && coords.length < count; i++) {
        coords.push(coord);
        coord = getNeighbor(coord, dir);
      }
    }
    ring++;
  }
  return coords;
}
```

## Two Types of Borders

There are two distinct border concepts that must not be confused:

### 1. Container Gap (HexGrid responsibility)

The uniform spacing between adjacent hexes. Without a gap, hex edges touch perfectly (or overlap
due to rounding). The gap creates visual separation.

- Controlled by `gap` prop on HexGrid
- Same for all hexes in the grid
- Reveals container background through the gap

### 2. Tile Border (Content component responsibility)

Semantic per-hex border that indicates state. Examples:

- **Selected state**: Green border on selected game type
- **Hover state**: Blue border on hover
- **Player color**: Each player hex has their own color border
- **Disabled state**: Muted border

This is NOT handled by HexGrid. Each content component manages its own border via the
two-polygon pattern (outer hex for border, inner scaled hex for content).

## Two-Pass Rendering

To create uniform gaps without double-thickness borders:

```text
Pass 1 (z-0): Border hexes at full size with borderColor fill
Pass 2 (z-10): Content hexes at scale(1 - gap*2/hexSize)
```

### Render Logic

```tsx
// Pass 1: Border layer (if borderColor provided)
{borderColor && items.map(item => (
  <HexTile key={`border-${item.id}`} className="z-0" ...>
    <div className="w-full h-full hex-clip-flat" style={{ background: borderColor }} />
  </HexTile>
))}

// Pass 2: Content layer
{items.map(item => (
  <HexTile key={item.id} className="z-10" ...>
    <div style={{ transform: `scale(${contentScale})` }}>
      {item.content}
    </div>
  </HexTile>
))}
```

### Scale Calculation

```typescript
const contentScale = 1 - (gap * 2 / hexSize);
// gap=4, hexSize=100 → scale(0.92)
// gap=6, hexSize=100 → scale(0.88)
```

## Props

```typescript
interface HexGridProps {
  hexSize: number;           // Circumradius in pixels
  items: HexGridItem[];      // Items to render
  className?: string;        // Container className
  scale?: number;            // Overall grid scale (default: 1.0)
  gap?: number;              // Gap between hexes in pixels (default: 0)
  borderColor?: string;      // Tailwind class for gap/border fill
}
```

## Content Component Pattern

Content components use the two-polygon pattern for their semantic borders:

```tsx
function PlayerHex({ player, isSelected }: Props) {
  return (
    <div className="w-full h-full">
      {/* Outer hex - semantic border (player color, selection state) */}
      <div className={`absolute inset-0 hex-clip-flat ${
        isSelected ? 'bg-green-500' : `bg-${player.color}`
      }`} />

      {/* Inner hex - content */}
      <div className="absolute inset-0 scale-[0.91] hex-clip-flat bg-gray-900">
        <PlayerAvatar player={player} />
      </div>
    </div>
  );
}
```

The `scale-[0.91]` creates the visible border. This is independent of HexGrid's gap.

## Layout Ownership

HexGrid owns the spiral layout logic. Consumers provide data and render functions, not coordinates.

### Declarative API

```tsx
<HexGrid
  hexSize={100}
  center={<CenterHex icon={faUsers} title="Choose Players" />}
  data={players}
  renderItem={(player, index) => (
    <PlayerHex player={player} isSelected={selected.includes(player.id)} />
  )}
  onItemClick={(player) => onSelect(player.id)}
  fillEmpty="water"  // Fill remaining positions with WaterHex
  gap={4}
  borderColor="bg-gray-700"
/>
```

### Props (Declarative Mode)

```typescript
interface HexGridProps<T> {
  hexSize: number;
  center?: ReactNode;              // Center hex content (optional)
  data: T[];                       // Array of items to render
  renderItem: (item: T, index: number) => ReactNode;
  getItemId?: (item: T) => string; // Default: index
  onItemClick?: (item: T, coord: HexCoordinate) => void;  // Item + coordinate
  fillEmpty?: 'water' | false;     // Fill unused positions (default: false)
  gap?: number;
  borderColor?: string;
  scale?: number;
  gridRef?: React.Ref<HexGridRef<T>>;  // Imperative API
}
```

### Imperative API (gridRef) - DEFERRED

> **Note**: This API is documented for completeness but implementation is deferred until needed.
> For the initial implementation, we'll use the declarative API only.

Parent components can query the grid via ref:

```typescript
interface HexGridRef<T> {
  // Get item at coordinate (returns undefined for water/empty)
  getItemAt(coord: HexCoordinate): T | undefined;

  // Get coordinate for an item
  getCoordFor(item: T): HexCoordinate | undefined;

  // Convert pixel position to hex coordinate (for hit testing)
  pixelToHex(x: number, y: number): HexCoordinate;

  // Get all coordinates in the grid
  getAllCoords(): HexCoordinate[];

  // Check if coordinate has an item (vs water/empty)
  hasItemAt(coord: HexCoordinate): boolean;
}
```

### Usage with Ref

```tsx
function GamePage() {
  const gridRef = useRef<HexGridRef<Player>>(null);

  const handleBoardClick = (player: Player, coord: HexCoordinate) => {
    console.log(`Clicked ${player.name} at (${coord.q}, ${coord.r}, ${coord.s})`);

    // Query neighbor using direction helper
    const eastCoord = getNeighbor(coord, 'SouthEast');
    const neighbor = gridRef.current?.getItemAt(eastCoord);
    if (neighbor) {
      console.log(`Neighbor to the east: ${neighbor.name}`);
    }
  };

  return (
    <HexGrid
      gridRef={gridRef}
      data={players}
      renderItem={(p) => <PlayerHex player={p} />}
      onItemClick={handleBoardClick}
    />
  );
}
```

### Automatic Spiral Layout

HexGrid automatically places items in spiral order from center outward:

```text
Ring 0: Center (1 hex)
Ring 1: Positions 1-6 (6 hexes) - clockwise from North
Ring 2: Positions 7-18 (12 hexes)
Ring 3: Positions 19-36 (18 hexes)
```

For 6 players + center: uses Ring 0-1 (CLUSTER_7)
For 6 players + Guest + center: uses Ring 0-1, fills 1 water hex

### Fill Empty Option

When `fillEmpty="water"`:

- Calculates minimum ring needed for all items
- Fills remaining positions in that ring with WaterHex
- Creates complete visual cluster

```tsx
// 4 game types + center = 5 items
// Ring 1 has 6 positions, so 2 filled with water
<HexGrid
  center={<CenterHex title="Choose Game" />}
  data={gameTypes}
  renderItem={(game) => <GameTypeHex game={game} />}
  fillEmpty="water"
/>
```

### Usage Example: Player Selector

```tsx
function PlayerSelector({ players, selected, onSelect }: Props) {
  return (
    <HexGrid
      hexSize={100}
      center={<CenterHex icon={faUsers} title="Choose Players" />}
      data={players}
      getItemId={(p) => p.id}
      renderItem={(player) => (
        <PlayerHex player={player} isSelected={selected.includes(player.id)} />
      )}
      onItemClick={(player) => onSelect(player.id)}
      fillEmpty={false}  // No water fill for players
      gap={4}
      borderColor="bg-gray-700"
    />
  );
}
```

### PlayerHex Component

Each content component manages its own semantic border:

```tsx
function PlayerHex({ player, isSelected }: { player: Player; isSelected: boolean }) {
  return (
    <div className="w-full h-full">
      {/* Outer hex - semantic border (selection state) */}
      <div className={`absolute inset-0 hex-clip-flat transition-colors ${
        isSelected ? 'bg-green-500' : 'bg-gray-500'
      }`} />

      {/* Inner hex - content with player color */}
      <div
        className="absolute inset-0 scale-[0.91] hex-clip-flat flex flex-col items-center justify-center"
        style={{ backgroundColor: player.color }}
      >
        {player.avatarUrl && (
          <img src={player.avatarUrl} className="w-16 h-16 rounded-full" />
        )}
        <span className="text-white font-bold mt-2">{player.name}</span>
      </div>
    </div>
  );
}
```

## Vertex and Edge Position APIs

For game board elements (buildings at vertices, roads at edges), hex-geometry.ts provides:

### Vertex Positions (Buildings)

```typescript
type HexPosition = 'Right' | 'BottomRight' | 'BottomLeft' | 'Left' | 'TopLeft' | 'TopRight';

const VERTEX_ANGLES: Record<HexPosition, number> = {
  Right: 0,
  BottomRight: 60,
  BottomLeft: 120,
  Left: 180,
  TopLeft: 240,
  TopRight: 300,
};

/**
 * Get pixel position for a hex vertex (where buildings are placed).
 * Vertices are at circumradius distance from hex center.
 */
function getVertexPosition(
  coord: HexCoordinate,
  position: HexPosition,
  size: number,
  origin?: PixelPosition
): PixelPosition {
  const center = hexToPixel(coord, size, origin);
  const angle = VERTEX_ANGLES[position] * Math.PI / 180;
  return {
    x: center.x + size * Math.cos(angle),
    y: center.y + size * Math.sin(angle),
  };
}
```

### Edge Positions (Roads)

```typescript
type HexSide = 'Top' | 'TopRight' | 'BottomRight' | 'Bottom' | 'BottomLeft' | 'TopLeft';

const EDGE_ANGLES: Record<HexSide, number> = {
  Top: 0,
  TopRight: 60,
  BottomRight: 120,
  Bottom: 180,
  BottomLeft: 240,
  TopLeft: 300,
};

/**
 * Get pixel position for a hex edge midpoint (where roads are placed).
 * Edge midpoints are at apothem distance from hex center.
 */
function getEdgeMidpoint(
  coord: HexCoordinate,
  side: HexSide,
  size: number,
  origin?: PixelPosition
): PixelPosition {
  const center = hexToPixel(coord, size, origin);
  const apothem = size * Math.sqrt(3) / 2;
  const angle = (EDGE_ANGLES[side] - 90) * Math.PI / 180; // -90 to point outward
  return {
    x: center.x + apothem * Math.cos(angle),
    y: center.y + apothem * Math.sin(angle),
  };
}
```

## CatanBoard Architecture (All-DOM)

The game board uses **all DOM/CSS rendering** for consistency with the HexGrid pattern.
No SVG overlays - buildings and roads are positioned DOM elements.

```text
┌─────────────────────────────────────────┐
│  Buildings (positioned DOM circles)     │  ← Vertex positions (54)
├─────────────────────────────────────────┤
│  Roads (CSS clip-path polygons)         │  ← Edge positions (72)
├─────────────────────────────────────────┤
│  Tiles (HexGrid component)              │  ← Hex centers (19-30)
└─────────────────────────────────────────┘
```

### Benefits of All-DOM

- Single rendering approach (no DOM/SVG mixing)
- Consistent Tailwind styling everywhere
- Natural React component model with memoization
- Standard CSS hover/transitions
- Easier debugging (DOM inspector)

### Road Rendering

Roads use CSS clip-path for the bow-tie polygon shape:

```css
.road-clip {
  clip-path: polygon(0% 50%, 15% 0%, 85% 0%, 100% 50%, 85% 100%, 15% 100%);
}
```

```tsx
function Road({ coord, side, player, hexSize }: Props) {
  const { x, y } = getEdgeMidpoint(coord, side, hexSize);
  return (
    <div
      className="absolute road-clip w-[50px] h-[16px] -translate-x-1/2 -translate-y-1/2"
      style={{
        left: x,
        top: y,
        rotate: `${EDGE_ANGLES[side]}deg`,
        background: player.gradient
      }}
    />
  );
}
```

### Building Rendering

Buildings are positioned circles at hex vertices:

```tsx
function Building({ coord, position, player, hexSize }: Props) {
  const { x, y } = getVertexPosition(coord, position, hexSize);
  return (
    <div
      className="absolute w-10 h-10 rounded-full -translate-x-1/2 -translate-y-1/2"
      style={{ left: x, top: y, background: player.gradient }}
    >
      <SettlementIcon />
    </div>
  );
}
```

### CatanBoard Component

```tsx
function CatanBoard({ gameModel, hexSize = 100 }) {
  return (
    <div className="relative">
      {/* Tiles layer - uses HexGrid */}
      <HexGrid
        hexSize={hexSize}
        data={gameModel.tiles}
        renderItem={(tile) => <GameTile tile={tile} />}
        gap={2}
      />

      {/* Roads layer - positioned DOM elements */}
      {gameModel.roads.map(road => (
        <Road key={road.id} {...road} hexSize={hexSize} />
      ))}

      {/* Buildings layer - positioned DOM circles */}
      {gameModel.buildings.map(building => (
        <Building key={building.id} {...building} hexSize={hexSize} />
      ))}
    </div>
  );
}
```

### Performance Strategy

CSS transforms (`translate`) are GPU accelerated - standard for high-performance DOM rendering.

- HexGrid uses `position: relative`, children use `position: absolute`
- React 18 automatic batching handles state updates efficiently
- `React.memo` on Tile, Road, Building components
- Key by coordinate for minimal re-renders
- State normalization for O(1) lookups
- Virtualization if needed for large boards

## File Location

`react-ui/components/hex-grid/HexGrid.tsx`

## Related Components

- `HexTile` - Individual positioned hex with clip-path
- `hex-geometry.ts` - Coordinate math, layouts, vertex/edge positions
- Content components in `content/` subdirectory
