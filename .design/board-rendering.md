# Board Rendering (React)

**Last verified:** January 30, 2026

## Overview

The React UI renders the game board using DOM-based hex components, not SVG.
The `HexGrid` component handles layout and positioning, while `GameBoard`
orchestrates tiles, buildings, roads, harbors, and the robber.

This is a complete departure from the Blazor implementation, which used
server-side SVG string generation.

## Component Hierarchy

```
GameBoard
├── HexGrid (layout engine)
│   ├── HexTile (per hex)
│   │   ├── TileContent (resource type, number token, pip dots)
│   │   ├── HarborHexContent (dock/water triangles, owner colors)
│   │   └── WaterHex / MenuHex / CenterHex (specialized content)
│   └── Overlay (buildings + roads layer)
│       ├── Building markers (settlements, cities, buildable spots)
│       └── Road segments (owned roads, buildable spots)
├── Robber layer (CatanFont glyphs with player gradients)
└── Zoom/pan controls
```

## HexGrid Layout Engine

**File:** `react-ui/components/hex-grid/HexGrid.tsx`

The HexGrid component converts cube coordinates to pixel positions and
renders hex tiles in a two-pass system:

### Two-Pass Rendering

1. **Border layer** -- Renders hex outlines with gap scaling
2. **Content layer** -- Renders hex interior content

The gap between hexes is proportional to hex size, creating visual
separation without coordinate math changes.

### Sizing

- Calculates bounding box from all hex positions
- Supports `fitToParent` mode using `ResizeObserver` to auto-scale
- Manual zoom via mouse wheel (20px to 150px hex size range)

### Overlay System

HexGrid accepts an `overlay` render prop that receives the same coordinate
space as tiles. This is how buildings and roads are positioned relative to
hex vertices and edges.

## GameBoard Component

**File:** `react-ui/components/game/board/GameBoard.tsx`

### Data Flow

GameBoard reads all data from Zustand store hooks (no props):

- `useGameTiles()` -- Tile data (resource, number, coordinates)
- `useGameBuildings()` -- Building positions and states
- `useGameRoads()` -- Road positions and states
- `useGameHarbors()` -- Harbor types and positions
- `useRobber()` -- Robber position and target player
- `useCurrentPlayerId()` -- Active player for highlighting

### Zoom and Pan

- **Zoom:** Mouse wheel adjusts hex size (20-150px range)
- **Pan:** Ctrl+drag moves the viewport
- Both managed as local component state

### Building Rendering

Two-loop system for correct z-ordering:

1. **Owned buildings first** -- Settlements and cities with player colors
2. **Buildable spots second** -- Semi-transparent markers at valid positions

Building placement uses `HexPosition` indices (0-5) mapping to hex
vertices. Cities show as larger markers than settlements.

### Road Rendering

Roads are positioned at hex edge midpoints using `HexSide` values.
Visual states:

- **Owned** -- Solid color matching player
- **Buildable** -- Semi-transparent hover target

### Robber

- Uses CatanFont glyphs (`SolidShield` + `Pirate`)
- Animated CSS transitions (1.2s) when moving between hexes
- Player color gradient when targeting a specific player

### Harbor Rendering

`HarborHexContent` splits the hex into dock and water triangles:

- **Dock side** -- Colored by owning player (if settlement adjacent)
- **Water side** -- Default water styling
- Harbor type icon (resource or 3:1) centered

## Hex Geometry

**File:** `react-ui/components/hex-grid/hex-geometry.ts`

Core math functions for flat-top hex positioning:

| Function | Purpose |
|----------|---------|
| `hexToPixel` | Cube coords to pixel center |
| `pixelToHex` | Pixel to cube coords (with rounding) |
| `getVertexPosition` | Building placement at hex vertex |
| `getEdgeMidpoint` | Road placement at hex edge |
| `getNeighbor` | Adjacent hex in any direction |
| `distance` | Hex distance between two coords |

See [coordinates.md](coordinates.md) for the full coordinate system
reference.

## Tile Content

Each tile renders:

- **Resource type** -- Background color/texture via CSS class
- **Number token** -- Circular overlay showing the dice number
- **Pip dots** -- Probability indicators (more dots = more likely)
- **Dimming** -- Non-matching tiles dim for 5 seconds after a roll

## CSS Architecture

Board styling uses Tailwind v4 with custom `@utility` directives:

- `hex-clip-flat` -- CSS clip-path for flat-top hexagon shape
- 3D transform utilities for tile flip animations
- Animation utilities for robber movement and highlighting

All custom utilities MUST use `@utility`, not `@layer utilities`
(Tailwind v4 breaking change).
