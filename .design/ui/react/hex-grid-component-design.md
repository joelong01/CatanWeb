# Plan: Reusable Hex Grid Layout System for React

## Problem Statement

The New Game page needs hex grids for game type selection and player selection, but currently:

1. **Wrong aspect ratios** - Using w-52 (208px) × h-60 (240px) = 1.15 ratio, but flat-top hexagons should be sqrt(3)/2 ≈ 0.866
2. **Wrong spacing formulas** - Using 0.75 × dimension, causing overlaps instead of 1px gaps
3. **Duplicated code** - Hex layout logic copied between GameTypeSelector and PlayerSelector
4. **Not leveraging existing work** - BoardGeometry.cs has correct Red Blob Games formulas

## Existing Implementation Analysis

From `BoardGeometry.cs` and `HexCoordinates.cs`:

### Flat-Top Hex Math (Red Blob Games Standard)

```csharp
// Pixel position from hex coordinates (Q, R):
double x = size * 1.5 * Q + offsetX;
double y = size * Math.Sqrt(3) * (R + Q/2.0) + offsetY;

// Dimensions:
double width = 2 * size;                    // Circumradius to opposite vertices
double height = Math.Sqrt(3) * size;        // Height of flat-top hex
double aspectRatio = height / width;        // = sqrt(3)/2 ≈ 0.866
```

### Spacing for 1px Gaps

From `BoardSvgConstants.cs`:

```csharp
HexSize = 100;                              // Circumradius
TileGap = 2;                                // Stroke width = visible gap
InnerHexSize = HexSize - TileGap - 7;       // 91 (accounts for road space)
```

The gap comes from **stroke thickness**, not manual spacing adjustments.

## Proposed Solution: Reusable HexGrid Component

### Architecture

```text
react-ui/
├── components/
│   ├── hex-grid/
│   │   ├── HexGrid.tsx              # Layout engine component
│   │   ├── HexTile.tsx              # Individual hex tile wrapper
│   │   ├── hex-geometry.ts          # TypeScript port of HexCoordinates/BoardGeometry
│   │   └── index.ts                 # Exports
│   └── new-game/
│       ├── GameTypeSelector.tsx     # Uses <HexGrid>
│       └── PlayerSelector.tsx       # Uses <HexGrid>
```

### Phase 1: Create `hex-geometry.ts` (Pure Math)

Port the essential formulas from C# to TypeScript:

```typescript
// hex-geometry.ts

/**
 * Hex dimensions for flat-top hexagons.
 * Based on Red Blob Games hex grid formulas.
 */
export interface HexDimensions {
  /** Circumradius (center to vertex) */
  size: number;
  /** Full width (2 × size) */
  width: number;
  /** Full height (sqrt(3) × size) */
  height: number;
  /** Aspect ratio (height/width ≈ 0.866) */
  aspectRatio: number;
  /** Gap between hex edges (stroke thickness) */
  gap: number;
}

/**
 * Calculate hex dimensions from circumradius.
 *
 * @param size - Circumradius (distance from center to vertex)
 * @param gap - Gap between hex edges (default: 2px)
 */
export function calculateHexDimensions(size: number, gap: number = 2): HexDimensions {
  const width = 2 * size;
  const height = Math.sqrt(3) * size;

  return {
    size,
    width,
    height,
    aspectRatio: height / width,
    gap,
  };
}

/**
 * Axial hex coordinates (Q, R).
 * Q = column offset, R = row offset
 */
export interface HexCoordinate {
  q: number;
  r: number;
}

/**
 * Pixel position (x, y).
 */
export interface PixelPosition {
  x: number;
  y: number;
}

/**
 * Convert hex coordinates to pixel position (flat-top).
 * Formula from Red Blob Games.
 *
 * @param coord - Hex coordinate (q, r)
 * @param size - Hex circumradius
 * @param origin - Origin offset (default: 0, 0)
 */
export function hexToPixel(
  coord: HexCoordinate,
  size: number,
  origin: PixelPosition = { x: 0, y: 0 }
): PixelPosition {
  const x = size * 1.5 * coord.q + origin.x;
  const y = size * Math.sqrt(3) * (coord.r + coord.q / 2.0) + origin.y;

  return { x, y };
}

/**
 * Predefined hex grid layouts (center + surrounding hexes).
 */
export const HEX_LAYOUTS = {
  /** 7 hexes: center + 6 surrounding (classic Catan tile cluster) */
  CLUSTER_7: [
    { q: 0, r: 0 },   // Center
    { q: 0, r: -1 },  // North
    { q: 1, r: -1 },  // NorthEast
    { q: 1, r: 0 },   // SouthEast
    { q: 0, r: 1 },   // South
    { q: -1, r: 1 },  // SouthWest
    { q: -1, r: 0 },  // NorthWest
  ],

  /** 19 hexes: standard Catan board */
  CLUSTER_19: [
    // Row -2
    { q: 0, r: -2 }, { q: 1, r: -2 }, { q: 2, r: -2 },
    // Row -1
    { q: -1, r: -1 }, { q: 0, r: -1 }, { q: 1, r: -1 }, { q: 2, r: -1 },
    // Row 0 (center)
    { q: -2, r: 0 }, { q: -1, r: 0 }, { q: 0, r: 0 }, { q: 1, r: 0 }, { q: 2, r: 0 },
    // Row 1
    { q: -2, r: 1 }, { q: -1, r: 1 }, { q: 0, r: 1 }, { q: 1, r: 1 },
    // Row 2
    { q: -2, r: 2 }, { q: -1, r: 2 }, { q: 0, r: 2 },
  ],
} as const;
```

### Phase 2: Create `HexTile.tsx` (Positioned Hex Container)

```typescript
// HexTile.tsx

import { ReactNode, CSSProperties } from 'react';

export interface HexTileProps {
  /** Hex width in pixels */
  width: number;
  /** Hex height in pixels */
  height: number;
  /** Absolute position (x, y) */
  position: { x: number; y: number };
  /** Content to render inside hex */
  children: ReactNode;
  /** Optional className for styling */
  className?: string;
  /** Optional inline styles */
  style?: CSSProperties;
  /** Click handler */
  onClick?: () => void;
  /** Whether tile is disabled */
  disabled?: boolean;
}

/**
 * Individual hex tile with clip-path masking.
 * Positions absolutely within parent container.
 * Content manages its own borders, styling, animations.
 */
export function HexTile({
  width,
  height,
  position,
  children,
  className = '',
  style = {},
  onClick,
  disabled = false,
}: HexTileProps): React.ReactElement {
  return (
    <div
      className={`absolute hex-clip-flat ${className}`}
      style={{
        width: `${width}px`,
        height: `${height}px`,
        left: `${position.x}px`,
        top: `${position.y}px`,
        transform: 'translate(-50%, -50%)', // Center on position
        ...style,
      }}
      onClick={disabled ? undefined : onClick}
    >
      {children}
    </div>
  );
}
```

### Phase 3: Create `HexGrid.tsx` (Layout Engine)

```typescript
// HexGrid.tsx

import { ReactNode } from 'react';
import { calculateHexDimensions, hexToPixel, HexCoordinate, PixelPosition } from './hex-geometry';
import { HexTile } from './HexTile';

export interface HexGridItem {
  /** Unique key for React */
  id: string;
  /** Hex coordinate in the grid */
  coord: HexCoordinate;
  /** Content to render */
  content: ReactNode;
  /** Optional className for this specific tile */
  className?: string;
  /** Click handler */
  onClick?: () => void;
  /** Whether tile is disabled */
  disabled?: boolean;
}

export interface HexGridProps {
  /** Hex circumradius (distance from center to vertex) */
  hexSize: number;
  /** Array of hex items to render */
  items: HexGridItem[];
  /** Optional className for container */
  className?: string;
  /** Zoom/scale factor (default: 1.0) */
  scale?: number;
}

/**
 * Hex grid layout engine.
 * Automatically positions hex tiles based on axial coordinates.
 * Tiles manage their own borders/styling based on state.
 * Supports zoom/scale for responsive layouts.
 *
 * Note: Inter-hex spacing is provided by the two-hex polygon border
 * approach (inner hex at scale 0.91), not by a gap parameter.
 */
export function HexGrid({
  hexSize,
  items,
  className = '',
  scale = 1.0,
}: HexGridProps): React.ReactElement {
  const dims = calculateHexDimensions(hexSize);

  // Calculate bounding box
  const positions = items.map(item => hexToPixel(item.coord, hexSize));
  const minX = Math.min(...positions.map(p => p.x));
  const maxX = Math.max(...positions.map(p => p.x));
  const minY = Math.min(...positions.map(p => p.y));
  const maxY = Math.max(...positions.map(p => p.y));

  const containerWidth = maxX - minX + dims.width;
  const containerHeight = maxY - minY + dims.height;

  // Origin offset so leftmost/topmost tile edges align with container edges
  const origin: PixelPosition = {
    x: dims.width / 2 - minX,
    y: dims.height / 2 - minY,
  };

  return (
    <div
      className={`relative ${className}`}
      style={{
        width: `${containerWidth}px`,
        height: `${containerHeight}px`,
        transform: `scale(${scale})`,
        transformOrigin: 'center center',
        margin: '0 auto',
      }}
    >
      {items.map(item => {
        const pos = hexToPixel(item.coord, hexSize, origin);

        return (
          <HexTile
            key={item.id}
            width={dims.width}
            height={dims.height}
            position={pos}
            className={item.className}
            onClick={item.onClick}
            disabled={item.disabled}
          >
            {item.content}
          </HexTile>
        );
      })}
    </div>
  );
}
```

### Phase 4: Update `GameTypeSelector.tsx`

Uses two-hex polygon approach for borders (outer hex = border color, inner hex at 91% scale = content).
Hover/selection state changes the BORDER color, not the content background.

```typescript
// GameTypeSelector.tsx

import { HexGrid, HexGridItem, HEX_LAYOUTS } from '@/components/hex-grid';

// Two-hex border approach: outer hex is border, inner hex (scale 0.91) is content
function GameTypeContent({ config, isSelected }: { config: GameTypeConfig; isSelected: boolean }) {
  const isDisabled = !config.enabled;
  const [isHovered, setIsHovered] = React.useState(false);

  return (
    <div className="w-full h-full"
      onMouseEnter={() => !isDisabled && setIsHovered(true)}
      onMouseLeave={() => setIsHovered(false)}>
      {/* Outer hex - border (state-dependent color) */}
      <div className={`absolute inset-0 hex-clip-flat transition-colors duration-200 ${
        isSelected ? 'bg-green-500' : isHovered && !isDisabled ? 'bg-blue-500' : 'bg-white/30'
      }`} />
      {/* Inner hex - content (91% scale creates border gap) */}
      <div className="absolute inset-0 flex flex-col items-center justify-center hex-clip-flat"
        style={{ background: 'linear-gradient(135deg, #2a2a2a, #1a1a1a)', transform: 'scale(0.91)' }}>
        {/* Icon, title, players badge, tiles count */}
      </div>
    </div>
  );
}

export function GameTypeSelector({ value, onChange }: GameTypeSelectorProps) {
  const hexSize = 100;
  const items: HexGridItem[] = [
    // Center hex - "Choose Game" label (also uses two-hex approach)
    {
      id: 'center',
      coord: HEX_LAYOUTS.CLUSTER_7[0],
      content: (
        <div className="w-full h-full">
          <div className="absolute inset-0 hex-clip-flat bg-amber-500/50" />
          <div className="absolute inset-0 flex items-center justify-center hex-clip-flat
            bg-gradient-to-br from-amber-900/40 to-amber-950/40"
            style={{ transform: 'scale(0.91)' }}>
            <div className="text-center">
              <FontAwesomeIcon icon={faDice} className="text-amber-500 text-4xl mb-3" />
              <h3 className="text-base font-bold text-amber-400 uppercase">Choose Game</h3>
            </div>
          </div>
        </div>
      ),
      disabled: true,
    },
    // Game type hexes at positions 1-6, water placeholders for unused slots
    // ...
  ];

  return (
    <div className="flex flex-col h-full">
      <h2 className="text-lg font-semibold text-white mb-3">Choose Game Type</h2>
      <div className="flex-1 flex items-center justify-center">
        <HexGrid hexSize={hexSize} items={items} scale={1.0} />
      </div>
    </div>
  );
}
```

### Phase 5: Update `PlayerSelector.tsx`

Same two-hex border approach. Player gradient colors are ALWAYS shown (matching Blazor).
Selection is indicated by a **checkmark overlay** (not border color change).
Hover always shows blue border. Circular selection: when at max players, clicking a new
player removes the oldest selected and adds the new one.

Guest player is always filtered from the main grid. A separate "Include Guest" checkbox
controls whether a Guest hex appears at coordinate (-2, 1) to the left of the main cluster.

```typescript
// PlayerSelector.tsx

// Two-hex border: outer = hover color, inner = player gradient (always visible)
// Selection shown via checkmark overlay, NOT border color change
function PlayerCardContent({ player, isSelected }: { player: PlayerProfile; isSelected: boolean }) {
  const [isHovered, setIsHovered] = React.useState(false);

  return (
    <div className="w-full h-full"
      onMouseEnter={() => setIsHovered(true)}
      onMouseLeave={() => setIsHovered(false)}>
      {/* Outer hex - border (hover shows blue, default subtle white) */}
      <div className="absolute inset-0 hex-clip-flat transition-colors duration-200"
        style={{
          background: isHovered ? '#3b82f6' : 'rgba(255,255,255,0.3)',
        }} />
      {/* Inner hex - content (always shows player gradient with dark edge) */}
      <div className="absolute inset-0 flex flex-col items-center justify-center hex-clip-flat"
        style={{
          background: `linear-gradient(160deg, ${player.colors.primary} 0%, ${player.colors.secondary} 70%, rgba(0,0,0,0.3) 100%)`,
          transform: 'scale(0.91)',
        }}>
        {/* Avatar (circular, white border), name, trophy badge */}
      </div>
      {/* Selection checkmark - positioned at top:16%, left:68% (midpoint of center-to-upper-right-vertex) */}
      {isSelected && (
        <div className="absolute z-10 w-6 h-6 rounded-full bg-black/50 border border-white/50"
          style={{ top: '16%', left: '68%', transform: 'translate(-50%, -50%)' }}>
          <FontAwesomeIcon icon={faCheck} className="text-xs text-white" />
        </div>
      )}
    </div>
  );
}

export function PlayerSelector({ availablePlayers, selectedPlayerIds, onChange, gameType, includeGuest, ... }) {
  const hexSize = 100;

  // Guest always filtered from main grid; found separately for dedicated hex
  const sortedPlayers = availablePlayers.filter(p => p.name !== 'guest').sort(...);
  const guestPlayer = availablePlayers.find(p => p.name === 'guest');
  const visiblePlayers = sortedPlayers.slice(0, 6); // Max 6 surrounding hexes

  // Circular selection: when at max, remove oldest, add new
  const handleTogglePlayer = (playerId: string) => {
    if (selectedPlayerIds.includes(playerId)) {
      onChange(selectedPlayerIds.filter(id => id !== playerId));
    } else if (selectedPlayerIds.length < max) {
      onChange([...selectedPlayerIds, playerId]);
    } else {
      onChange([...selectedPlayerIds.slice(1), playerId]); // FIFO
    }
  };

  const items: HexGridItem[] = [
    // Center hex - "Choose Players" label (provides heading, no separate h2)
    { id: 'center', coord: HEX_LAYOUTS.CLUSTER_7[0], content: (...), disabled: true },
    // Surrounding player hexes (up to 6 positions, never disabled)
    ...visiblePlayers.map((player, idx) => ({
      id: player.id,
      coord: HEX_LAYOUTS.CLUSTER_7[idx + 1],
      content: <PlayerCardContent player={player} isSelected={...} />,
      onClick: () => handleTogglePlayer(player.id),
    })),
    // Guest hex - shown only when "Include Guest" checkbox is checked
    ...(includeGuest && guestPlayer ? [{
      id: guestPlayer.id,
      coord: { q: -2, r: 1 }, // Left column, creates 1-2-3-2 layout
      content: <PlayerCardContent player={guestPlayer} isSelected={...} />,
      onClick: () => handleTogglePlayer(guestPlayer.id),
    }] : []),
  ];

  return (
    <div className="flex flex-col">
      <HexGrid hexSize={hexSize} items={items} scale={1.0} />
      {/* Include Guest checkbox + validation message below grid */}
    </div>
  );
}
```

**Sitting Order (drag-drop)** is handled in `page.tsx`, not PlayerSelector.
PlayerSelector only handles selection; the parent page shows selected players
as draggable chips for reordering.

## Implementation Steps

1. **Create `hex-geometry.ts`** - Port Red Blob Games formulas
2. **Create `HexTile.tsx`** - Positioned hex container with clip-path
3. **Create `HexGrid.tsx`** - Layout engine with auto-centering
4. **Update `GameTypeSelector`** - Use HexGrid with two-hex border approach
5. **Update `PlayerSelector`** - Use HexGrid with two-hex border approach
6. **Test & verify** - Check aspect ratios, spacing, centering

## Design Principles

**Separation of Concerns:**

- **HexGrid** = Pure layout engine (positioning + spacing)
  - Positions hexes in correct grid layout
  - Manages gap/spacing between tiles (creates empty space)
  - Does NOT render borders (tiles do that)
- **Content components** = Handle everything visual (injected via `content`)
  - Content rendering, styling, animations
  - Border rendering (can change based on state!)

**Two-hex polygon approach for borders (matching Blazor SVG rendering):**

CSS `border` doesn't work on clipped elements (clip-path clips the border too).
Instead, each tile renders two nested hex-clipped divs:

- **Outer hex** (full size, `inset-0`): Background color = border color (state-dependent)
- **Inner hex** (`scale(0.91)`): Background = content (gradient, image, etc.)

The 9% gap between outer and inner creates a visible "border" that follows the hex shape.
State changes (hover, selection) only affect the outer hex's background color.

**Why this works:**

- Selection changes outer hex color (e.g., green for game types, player primary for players)
- Hover changes outer hex color (blue)
- Default state shows subtle outer hex color (white/30%)
- Content (inner hex) stays consistent regardless of state

This means:

- **Layout manages**: Positioning, container sizing, centering (`margin: 0 auto`)
- **Tile content manages**: Two-hex rendering, state-dependent border colors, animations
- **Clean separation**: HexGrid is pure math; content components own all visual behavior

## Benefits

1. **Correct math** - Uses proven Red Blob Games formulas from BoardGeometry.cs
2. **Correct aspect ratios** - 0.866 (sqrt(3)/2) for flat-top hexagons
3. **Correct spacing** - Proper hex positioning (gap is just visual spacing)
4. **Reusable** - Use for any hex grid layout (game tiles, player cards, thumbnails)
5. **Type-safe** - TypeScript interfaces for coordinates and dimensions
6. **Maintainable** - Change hex size in one place, applies everywhere
7. **Future-proof** - Easy to add 19-hex boards, 30-hex expansion boards, etc.
8. **Scalable** - Built-in zoom/scale support for responsive layouts and thumbnails
9. **Flexible** - Grid doesn't care about content, you control everything inside

## Hex Size & Scale Recommendations

Based on viewport space and content:

- **GameTypeSelector**: `hexSize = 100, scale = 1.0` → hex width 200px, height 173px
- **PlayerSelector**: `hexSize = 100, scale = 1.0` → hex width 200px, height 173px
- **Board thumbnails**: `hexSize = 40, scale = 1.0` → hex width 80px, height 69px (7-tile preview)

Both selectors use the same hex size for visual consistency on the New Game page.
The `scale` prop can be used for responsive sizing on smaller viewports.

## Player Colors - Database is CORRECT

**Player colors ARE correct in the database** (verified from Blazor screenshot):

- Doug = Green gradient ✓
- Joe = Blue gradient ✓
- Ryan = Yellow/gold gradient ✓
- Adrian = Purple gradient ✓

The React app is pulling colors from the API correctly via `player.colors.primary` and `player.colors.secondary`.

**Issue was in React rendering:** The old PlayerCard code showed gray when unselected, instead of always showing the player's gradient like Blazor does.

**Fix:** Always render `linear-gradient(135deg, ${player.colors.primary}, ${player.colors.secondary})` on the inner hex regardless of selection state. Selection is shown by changing the outer hex (border) color to the player's primary color. Hover shows blue border.

## Files to Create/Modify

### Create

- `react-ui/components/hex-grid/hex-geometry.ts`
- `react-ui/components/hex-grid/HexTile.tsx`
- `react-ui/components/hex-grid/HexGrid.tsx`
- `react-ui/components/hex-grid/index.ts`

### Modify

- `react-ui/components/new-game/GameTypeSelector.tsx` - Replace manual layout with HexGrid
- `react-ui/components/new-game/PlayerSelector.tsx` - Replace manual layout with HexGrid
- `react-ui/app/globals.css` - Keep hex-clip-flat utility

### Remove

- Manual hex positioning logic from both selectors
- Incorrect spacing calculations (0.75 × dimension)
