# Coordinate System

**Last verified:** January 30, 2026

## Cube Coordinates

The board uses **cube coordinates** with the constraint `Q + R + S = 0`.
Each hex is identified by three integers `(Q, R, S)` where `S = -Q - R`.

### Direction Vectors

Six directions from any hex, using flat-top orientation:

| Direction | Vector (Q, R, S) |
|-----------|-----------------|
| North | (0, -1, +1) |
| NorthEast | (+1, -1, 0) |
| SouthEast | (+1, 0, -1) |
| South | (0, +1, -1) |
| SouthWest | (-1, +1, 0) |
| NorthWest | (-1, 0, +1) |

### Coordinate to Pixel (Flat-Top)

The conversion from cube coordinates to pixel position uses flat-top hex
formulas:

```
x = size * 1.5 * Q + originX
y = size * sqrt(3) * (R + Q/2) + originY
```

Where `size` is the distance from center to vertex (circumradius).

### Pixel to Coordinate

Inverse conversion uses fractional cube coordinates with rounding:

1. Convert pixel to fractional cube coords
2. Round each to nearest integer
3. Fix rounding errors by adjusting the component with largest rounding
   error to maintain `Q + R + S = 0`

## Server Implementation

**File:** `Catan3.Shared/Utility/HexCoordinates.cs`

Key methods:

| Method | Purpose |
|--------|---------|
| `ToPixelCenter(size, offsetX, offsetY)` | Cube to flat-top pixel |
| `FromPixel(pixelX, pixelY, size, offsetX, offsetY)` | Pixel to cube with rounding |
| `Distance(a, b)` | Manhattan distance in hex space |
| `IsAdjacent(other)` | True if distance == 1 |
| `GetAllNeighbors()` | Returns all 6 adjacent coordinates |
| `MidPoint(left, top, size, side)` | Edge midpoint for road/harbor placement |

Directional properties: `North`, `NorthEast`, `SouthEast`, `South`,
`SouthWest`, `NorthWest` -- each returns the neighbor in that direction.

## React Implementation

**File:** `react-ui/components/hex-grid/hex-geometry.ts`

The React client has its own hex math library with equivalent functions:

| Function | Purpose |
|----------|---------|
| `cubicCoord(q, r)` | Create coordinate (s computed as -q-r) |
| `hexToPixel(coord, size, origin)` | Cube to pixel (flat-top) |
| `pixelToHex(pixel, size, origin)` | Pixel to cube with rounding |
| `getNeighbor(coord, direction)` | Adjacent hex in given direction |
| `getVertexPosition(coord, position, size, origin)` | Building placement point |
| `getEdgeMidpoint(coord, side, size, origin)` | Road placement point |
| `distance(a, b)` | Hex distance |
| `getRingCoordinates(center, radius)` | All hexes at given radius |
| `getSpiralCoordinates(center, radius)` | Expanding spiral from center |

### HexGridCollection

`HexGridCollection<T>` provides O(1) coordinate-based lookup using string
keys in format `"${q},${r}"` (S is redundant given the constraint).

### Predefined Layouts

| Layout | Hex Count | Purpose |
|--------|-----------|---------|
| `CLUSTER_7` | 7 | Center + 6 surrounding |
| `CLUSTER_19` | 19 | Standard 3-4 player board |
| `CLUSTER_30` | 30 | Expansion 5-6 player board |

## Building and Road Positioning

### Building Positions (HexPosition)

Each hex has 6 vertices numbered 0-5 for building placement:

| Position | Index | Location |
|----------|-------|----------|
| Right | 0 | Right vertex |
| BottomRight | 1 | Bottom-right vertex |
| BottomLeft | 2 | Bottom-left vertex |
| Left | 3 | Left vertex |
| TopLeft | 4 | Top-left vertex |
| TopRight | 5 | Top-right vertex |

### Road Sides (HexSide)

Each hex has 6 edges for road placement:

| Side | Location |
|------|----------|
| Top | Top edge |
| TopRight | Top-right edge |
| BottomRight | Bottom-right edge |
| Bottom | Bottom edge |
| BottomLeft | Bottom-left edge |
| TopLeft | Top-left edge |
