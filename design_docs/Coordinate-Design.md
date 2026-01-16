# Coordinate System Design

## Overview

The Catan game uses a **cube coordinate system** for hexagonal tiles, following the patterns described in the [Red Blob Games Hexagon Guide](https://www.redblobgames.com/grids/hexagons/). This document explains how coordinates work, how to convert between pixels and hex coordinates, and where to find implementations in the codebase.

## Cube Coordinate System

### Basic Concepts

Each hexagon is identified by three coordinates: **Q, R, S** (also known as x, y, z in some literature).

**Constraint**: `Q + R + S = 0` (always)

This constraint means you only need two coordinates to identify a hex, but using all three makes calculations elegant and symmetric.

### Coordinate Example

```text
Center hex: (0, 0, 0)
North:      (0, -1, 1)
South:      (0, 1, -1)
NorthEast:  (1, -1, 0)
SouthWest:  (-1, 1, 0)
NorthWest:  (-1, 0, 1)
SouthEast:  (1, 0, -1)
```

### Visual Layout (Flat-Top Hexagons)

```
        (-1,-1,2)  (0,-1,1)  (1,-1,0)
              \      |      /
       (-1,0,1) -- (0,0,0) -- (1,0,-1)
              /      |      \
        (-1,1,0)  (0,1,-1)  (1,1,-2)
```

## Hex Orientation

The game uses **flat-top hexagons**:

- Flat edges at top and bottom
- Pointed vertices on left and right
- Height = √3 × size
- Width = 2 × size

## Key Formulas

### Hex to Pixel (Center Point)

For flat-top hexagons:

```csharp
x = size * 1.5 * Q + offsetX
y = size * √3 * (R + Q/2) + offsetY
```

### Pixel to Hex

```csharp
// Translate relative to hex (0,0,0) center
relX = pixelX - offsetX
relY = pixelY - offsetY

// Convert to fractional axial coordinates
qf = relX / (1.5 * size)
rf = relY / (size * √3) - qf / 2

// Convert to cube coordinates
x = qf
z = rf
y = -x - z

// Apply cube rounding algorithm (see HexCoordinates.FromPixel)
```

### Distance Between Hexes

```csharp
distance = (|Q1-Q2| + |R1-R2| + |S1-S2|) / 2
```

Or equivalently:

```csharp
distance = max(|Q1-Q2|, |R1-R2|, |S1-S2|)
```

### Adjacent Hexes

Two hexes are adjacent if their distance is exactly 1.

The 6 direction vectors are:

```csharp
North:     (0, -1, +1)
NorthEast: (+1, -1, 0)
SouthEast: (+1, 0, -1)
South:     (0, +1, -1)
SouthWest: (-1, +1, 0)
NorthWest: (-1, 0, +1)
```

## Data Model Relationships

### HexCoordinates → TileModel

```
HexCoordinates (Q, R, S)
       ↓
   TileModel.TileKey
       ↓
   TileModel (ResourceTileType, Number, etc.)
```

### Finding a Tile by Coordinates

```csharp
// From a collection of TileModels
var tile = tiles.TileFromCoords(hexCoordinates);

// From GameModel
var tile = gameModel.Tiles.FirstOrDefault(t => t.TileKey == hexCoordinates);
```

### Finding Adjacent Tiles

```csharp
// Get all adjacent TileModels (only those that exist)
var neighbors = gameModel.Tiles.AdjacentTiles(tile);

// Get all 6 neighbor coordinates (may not all exist on board)
var neighborCoords = hexCoordinates.GetAllNeighbors();
```

## Board Layout

### Layout Parameters

The board layout is defined by:

- **OuterHexSize**: Distance from hex center to vertex
- **TileXOffset**: X offset for the board origin (top-left of bounding box)
- **TileYOffset**: Y offset for the board origin (top-left of bounding box)

### Converting Offsets

The TileXOffset/TileYOffset represent the **top-left corner** of the hex bounding box, not the center. To get the center of hex (0,0,0):

```csharp
centerX = TileXOffset + size
centerY = TileYOffset + size * √3 / 2
```

## Common Operations

### Hit Testing (Pixel → Tile)

```csharp
// Get hex coordinates from mouse position
var coords = HexCoordinates.FromPixel(
    mouseX, mouseY,
    layout.OuterHexSize,
    centerX, centerY);

// Find the tile at those coordinates
var tile = tiles.TileFromCoords(coords);
```

### Check if Two Tiles are Adjacent

```csharp
// Using HexCoordinates
bool adjacent = hex1.IsAdjacent(hex2);

// Using distance
bool adjacent = HexCoordinates.Distance(hex1, hex2) == 1;
```

### Get Tile Position for Rendering

```csharp
// Get pixel center of a hex
var center = hexCoords.ToPixelCenter(size, offsetX, offsetY);
```

### Validate Board (No Adjacent 6s and 8s)

```csharp
// Check all tiles with 6 for adjacent 6s or 8s
var tiles6 = game.Tiles.TilesWithNumber(6);
foreach (var tile in tiles6)
{
    var adjacent = game.Tiles.AdjacentTiles(tile);
    var bad = adjacent.TilesWithSixOrEight();
    if (bad.Count > 0) return false;
}
// Same for tiles with 8
```

## Implementation Map

### Core Coordinate Logic

| File | Purpose |
|------|---------|
| `Catan3.Shared/Utility/HexCoordinates.cs` | Core coordinate class with Q, R, S properties, distance, directions, pixel conversion |
| `Catan3.Shared/Models/Point.cs` | Simple Point struct for pixel coordinates |

### HexCoordinates Key Methods

| Method | Purpose |
|--------|---------|
| `HexCoordinates(q, r, s)` | Constructor with Q+R+S=0 validation |
| `Distance(a, b)` | Calculate distance between two hexes |
| `IsAdjacent(other)` | Check if two hexes are neighbors |
| `GetAllNeighbors()` | Get all 6 adjacent hex coordinates |
| `ToPixelCenter(size, offsetX, offsetY)` | Convert hex to pixel coordinates |
| `FromPixel(x, y, size, offsetX, offsetY)` | Convert pixel to hex coordinates |
| `Directions` | Static dictionary of the 6 direction vectors |
| `North`, `South`, etc. | Properties to get adjacent hex in specific direction |

### Tile Extensions

| File | Purpose |
|------|---------|
| `Catan3.Shared/Extensions/TileModelExtensions.cs` | Extensions for finding and filtering tiles |

| Method | Purpose |
|--------|---------|
| `TileFromCoords(coords)` | Find tile by HexCoordinates |
| `AdjacentTiles(tile)` | Get all adjacent tiles that exist |
| `TilesWithNumber(n)` | Filter tiles by number |
| `TilesWithSixOrEight()` | Filter tiles with 6 or 8 |

### Board Layout

| File | Purpose |
|------|---------|
| `DesktopApp/Layout/BoardVisualLayout.cs` | Layout properties (offsets, sizes) |
| `DesktopApp/Layout/HexGeometry.cs` | Hex vertex calculations for drawing |
| `DesktopApp/Game/GameFactory/RegularBoardInfo.cs` | Regular game board definition |
| `DesktopApp/Game/GameFactory/ExpansionBoardInfo.cs` | Expansion game board definition |

### Hit Testing / User Interaction

| File | Purpose |
|------|---------|
| `DesktopApp/Game/GameFactory/GameBoardCtrl.xaml.cs` | Mouse interaction, drag-drop, `HitTestBoard()` |

### Validation

| File | Purpose |
|------|---------|
| `Catan3.Shared/Extensions/GameModelExtensions.cs` | `ValidateGame()` - checks for invalid adjacent tiles |

### Tile Rendering

| File | Purpose |
|------|---------|
| `DesktopApp/Tiles/TileCtrl.xaml` | Tile visual template |
| `DesktopApp/Tiles/TileViewModel.cs` | Tile view model with coordinates |

## Validation Rules

### Adjacent 6s and 8s

In Catan, the following adjacencies are **invalid**:

- 6 adjacent to 6
- 6 adjacent to 8
- 8 adjacent to 8

The `ValidateGame()` method in `GameModelExtensions.cs` enforces these rules.

## Coordinate String Format

HexCoordinates can be serialized to/from strings:

```csharp
// ToString() format
"(0,-1,1)"

// FromString() accepts both
"(0,-1,1)"  // with parentheses
"0,-1,1"   // without parentheses
```

## Performance Considerations

- All coordinate operations are O(1)
- Finding a tile by coordinates is O(n) where n = number of tiles (<50)
- Finding adjacent tiles is O(6n) but n is small
- Board validation is O(n) for n tiles with 6 or 8

For the typical Catan board with 19-37 tiles, performance is not a concern.

## Example: Complete Hit Test Flow

```csharp
// 1. User clicks on board
var mousePoint = e.GetCurrentPoint(canvas).Position;

// 2. Calculate hex (0,0,0) center position
double size = layout.OuterHexSize;
double centerX = layout.TileXOffset + size;
double centerY = layout.TileYOffset + size * Math.Sqrt(3) / 2;

// 3. Convert pixel to hex coordinates
var coords = HexCoordinates.FromPixel(
    mousePoint.X, mousePoint.Y,
    size, centerX, centerY);

// 4. Find the tile at those coordinates
var tileVM = gameViewModel.Tiles.TileFromCoords(coords);

// 5. Use the tile
if (tileVM != null)
{
    var resource = tileVM.Tile.ResourceTileType;
    var number = tileVM.Tile.Number;
    // ...
}
```

## References

- [Red Blob Games: Hexagonal Grids](https://www.redblobgames.com/grids/hexagons/) - The authoritative guide to hex grid implementations
- Cube coordinates section explains the Q+R+S=0 constraint
- Pixel conversion section has the flat-top formulas
