# Board Layout Design Document

## Overview

This document describes the visual layout and rendering architecture for the Catan game UI, covering both the Desktop app implementation (XAML) and the WebUI approach (SVG/HTML). The goal is to achieve visual parity while adapting to web technologies.

## Full Game UI Layout

The game interface is organized into distinct panels with specific responsibilities:

```
┌─────────────────────────────────────────────────────────────────────┐
│ ☰ │                                                                 │
├───┼─────────────────────┬───────────────────────┬───────────────────┤
│   │                     │                       │                   │
│ M │   LEFT PANEL        │    CENTER PANEL       │   RIGHT PANEL     │
│ E │                     │                       │                   │
│ N │ ┌─────────────────┐ │                       │ ┌───────────────┐ │
│ U │ │ Game Controls   │ │                       │ │ Tracked       │ │
│   │ │ (Undo/Next/Redo)│ │                       │ │ Resources     │ │
│ ( │ └─────────────────┘ │                       │ └───────────────┘ │
│ h │                     │                       │                   │
│ i │ ┌─────────────────┐ │    ┌───────────────┐  │ ┌───────────────┐ │
│ d │ │ Purchase        │ │    │               │  │ │               │ │
│ d │ │ Controls        │ │    │   GAME BOARD  │  │ │   Player      │ │
│ e │ │ (Road/Sett/City)│ │    │   (SVG Hex    │  │ │   List        │ │
│ n │ └─────────────────┘ │    │    Grid)      │  │ │   with        │ │
│ ) │                     │    │               │  │ │   Stats       │ │
│   │ ┌─────────────────┐ │    └───────────────┘  │ │               │ │
│   │ │ Roll Entry      │ │                       │ │               │ │
│   │ │ (2-12 buttons)  │ │                       │ │               │ │
│   │ └─────────────────┘ │                       │ └───────────────┘ │
│   │                     │                       │                   │
│   │ ┌─────────────────┐ │                       │                   │
│   │ │ Board           │ │                       │                   │
│   │ │ Measurements    │ │                       │                   │
│   │ │ (when picking)  │ │                       │                   │
│   │ └─────────────────┘ │                       │                   │
│   │                     │                       │                   │
└───┴─────────────────────┴───────────────────────┴───────────────────┘
```

### Panel Descriptions

#### Hamburger Menu (Hidden by Default)
- **Source**: `MainPage.xaml` SplitView.Pane
- **Purpose**: Navigation and game commands
- **Contents**: New Game, Open, Save, Save As, Edit Players, Balance, Settings, Quit
- **Behavior**: Slides open from left when ☰ button clicked

#### Left Panel - Game Controls & Input

**1. Game State Controls (`GameControls.xaml`)**
- **Undo Button** (E10E icon) - Revert last action
- **Next Button** (E101 icon) - Advance game state
- **Redo Button** (E10D icon) - Restore undone action
- **State Message** - Current game state description (e.g., "Place Settlement", "Roll Dice")
- **Styling**: Player color background, bmCherry border, white foreground
- **Enable States**: Buttons enable/disable based on `GameModel.ActionFlags`

**2. Purchase Controls (`PurchaseCtrl.xaml`)**
- **Purpose**: Buy roads, settlements, cities, development cards
- **Layout**: Grid of 100×100 flip cards
- **Front**: Glyph (CatanFont), count of unspent, description
- **Back**: Card back image (face-down when not purchasable)
- **Behavior**: Cards flip to reveal when player can afford the purchase
- **Animation**: Push/release scale animation on interaction

**3. Roll Entry**
- **Source**: Defined inline in `MainPage.xaml`
- **Purpose**: Select dice roll (2-12)
- **Layout**: GridView with WrapGrid (3 columns)
- **Components**: `SingleRoll` controls using CatanNumber style
- **Note**: Unlike tile numbers, the 7 IS visible (for robber)
- **Styling**: Current player background/foreground colors
- **Visibility**: Hidden during Supplemental phase

**4. Board Measurements (`BoardMeasurementCtrl.xaml`)**
- **Purpose**: Analyze board balance during setup
- **Visibility**: Only shown during PickingBoard state
- **Contents**:
  - Resource card grid with selection
  - Star count displays (10-13 pip tiles)
  - Previous Board / Shuffle buttons
  - Star slider (0-14)
- **Note**: Helps players create balanced boards

#### Center Panel - Game Board

**Board Control (`GameBoardCtrl.xaml`)**
- **Purpose**: Main game board visualization
- **Contents**: Hex tiles, roads, settlements, cities, harbors, robber
- **Interaction**: Drag-and-drop tiles (setup), click to place pieces
- **WebUI**: SVG-based rendering with hit testing

#### Right Panel - Game State Display

**1. Tracked Resources (`TrackedResourcesCtrl.xaml`)**
- **Purpose**: Show total resources in play
- **Layout**: GridView of ResourceCardCtrl items
- **Display**: Each resource type with flip animation and count
- **Selection**: Multi-select for trading analysis

**2. Player List**
- **Source**: ListView in `MainPage.xaml`
- **Templates**: Different templates per game state
  - `RollOrderTemplate` - During roll order determination
  - `PlayerStatsTemplate` - During gameplay (uses `PlayerCtrl`)
  - `PickSupplementalPlayersTemplate` - Supplemental phase selection
- **Contents per player** (`PlayerCtrl.xaml`):
  - Player photo (50×50 circle)
  - Stats grid with CatanFont glyphs and counts
  - Resources gained this turn (during Supplemental)
- **Behavior**: Drag-reorderable in some states

### UI State Variations

The layout adapts based on `GameModel.GameState`:

| State | Left Panel Shows | Right Panel Shows |
|-------|------------------|-------------------|
| WaitingForStart | Hidden | Roll order selection |
| PickingBoard | Board Measurements | Resource tracking |
| AllocateResourcesForward | Game Controls, Rolls | Player stats |
| AllocateResourcesReverse | Game Controls, Rolls | Player stats |
| WaitingForRoll | Game Controls, Rolls, Purchase | Player stats |
| Supplemental | Game Controls | Player stats + resources this turn |

### WebUI Layout Implementation

Create a responsive grid layout that reserves space for all panels:

```html
<div class="game-layout">
  <div class="hamburger-menu"><!-- Collapsible sidebar --></div>

  <div class="left-panel">
    <div class="game-controls">Game Controls</div>
    <div class="purchase-controls">Purchase Controls</div>
    <div class="roll-entry">Roll Entry (2-12)</div>
    <div class="board-measurements">Board Measurements</div>
  </div>

  <div class="center-panel">
    <div class="game-board"><!-- SVG Board --></div>
  </div>

  <div class="right-panel">
    <div class="tracked-resources">Tracked Resources</div>
    <div class="player-list">Player List & Stats</div>
  </div>
</div>
```

### Placeholder Colors for Development

Use distinct background colors during development to visualize layout:

| Panel | Background Color | Hex |
|-------|------------------|-----|
| Game Controls | Coral | #FF7F50 |
| Purchase Controls | Gold | #FFD700 |
| Roll Entry | Tomato | #FF6347 |
| Board Measurements | Orange | #FFA500 |
| Game Board | DodgerBlue | #1E90FF |
| Tracked Resources | MediumSeaGreen | #3CB371 |
| Player List | SlateBlue | #6A5ACD |

## Board Components

The game board consists of several layered visual elements:

1. **Tiles** - Hex-shaped resource tiles (land and sea)
2. **Number Tokens** - Roll numbers with probability pips
3. **Tile Borders** - Reddish-brown lines between tiles
4. **Roads** - Player-colored paths on hex edges
5. **Settlements/Cities** - Player buildings on hex vertices
6. **Harbors** - Trading ports on coastal edges
7. **Robber** - Piece that blocks resource production

## Current WebUI Implementation

### Completed Features

1. **Flat-top hex rendering** - Tiles positioned using axial coordinates
2. **Dynamic viewBox** - SVG bounds calculated from actual tile positions
3. **Responsive sizing** - Board fills available container space
4. **Colored tile fills** - Resource types shown with solid colors (placeholder)
5. **Roll numbers** - Displayed with red highlighting for 6/8
6. **Road placeholders** - Transparent lines on hex edges with hover effect
7. **Settlement placeholders** - Transparent circles on vertices with hover effect
8. **Hamburger menu** - Collapsible sidebar navigation

### Pending Features

1. **Tile texture images** - Replace solid colors with hi-res images
2. **Number tokens with pips** - Add probability indicators
3. **Harbor rendering** - Position and rotate harbor images
4. **Road/Settlement coloring** - Show player colors when placed
5. **Roll highlighting** - Highlight tiles matching current roll
6. **Robber placement** - Show robber on desert/blocked tile

## Desktop App Visual Reference

### Tile Structure (TileCtrl.xaml)

Each tile is rendered as layered polygons:

```xml
<!-- Outer hex - border between tiles -->
<Polygon Stroke="{StaticResource bmCherry}"
         StrokeThickness="{x:Bind TileViewModel.Layout.TileGap}"
         Points="{x:Bind TileViewModel.Layout.OuterHexPoints}"
         Fill="{x:Bind TileViewModel.GetTileBorderBrush(...)}" />

<!-- Inner hex - resource texture -->
<Polygon Stroke="Transparent"
         Points="{x:Bind TileViewModel.Layout.InnerHexPoints}"
         Fill="{x:Bind TileViewModel.GetTileResourceType(...)}" />
```

**Key Visual Elements:**

| Element | Color/Style | Purpose |
|---------|-------------|---------|
| Outer hex stroke | `bmCherry` (reddish-brown) | Border between tiles |
| Outer hex fill | Border brush | Tile edge color |
| Inner hex fill | Resource image | Tile texture |
| Gap between tiles | `TileGap` property | Spacing for roads |

**Resource Image Styling:**
- All resource images have a common yellowish-brown edge color
- This creates visual cohesion when tiles are adjacent
- Images use `UniformToFill` stretch to maintain aspect ratio

### Number Token (CatanNumber)

Positioned at top-center of each tile:

```xml
<Grid Width="65" Height="65">
  <!-- Blue circle background -->
  <Ellipse Fill="#FF2F6999" Stroke="White" StrokeThickness=".5" Opacity=".75"/>

  <!-- Number (Segoe UI Bold) -->
  <TextBlock FontFamily="Segoe UI" FontWeight="Bold" FontSize="24"
             Text="{x:Bind Number}"
             Foreground="{x:Bind BIND_StarForeground(Number)}"/>

  <!-- Probability pips (Segoe Fluent Icons) -->
  <TextBlock FontFamily="Segoe Fluent Icons" FontSize="10"
             Text="{x:Bind Stars}"
             Foreground="{x:Bind BIND_StarForeground(Number)}"/>
</Grid>
```

**Probability Pip Mapping:**

| Number | Pips | Color |
|--------|------|-------|
| 2, 12 | • | White |
| 3, 11 | •• | White |
| 4, 10 | ••• | White |
| 5, 9 | •••• | White |
| 6, 8 | ••••• | Red |
| 7 | (none) | N/A |

### Harbor (HarborCtrl.xaml)

Harbors are ellipses filled with harbor type images:

```xml
<Ellipse Width="{x:Bind ViewModel.Layout.BuildingSize}"
         Height="{x:Bind ViewModel.Layout.BuildingSize}"
         Fill="{x:Bind Bind_HarborImage(ViewModel.Harbor.HarborKey.HarborType)}">
  <Ellipse.RenderTransform>
    <CompositeTransform ScaleX="1.5" ScaleY="1.5"
                        TranslateX="-10" TranslateY="-10"/>
  </Ellipse.RenderTransform>
</Ellipse>
```

**Harbor Positioning:**
- Placed at tile vertices (same positions as settlements)
- Scaled 1.5x for visibility
- Background polygon shows water extending into harbor area
- Rotation based on harbor direction (which edge faces the sea)

## WebUI SVG Rendering Architecture

### Layer Order (Z-Index)

SVG elements are rendered in document order (later = on top):

1. **Background** - Ocean blue fill
2. **Sea tiles** - Water hexes around the board
3. **Land tiles** - Resource hexes with texture patterns
4. **Tile borders** - Reddish-brown strokes between tiles
5. **Number tokens** - Roll numbers with probability pips
6. **Harbors** - Trading port images
7. **Roads** - Player-colored edge paths
8. **Settlements/Cities** - Player-colored vertex circles

### Tile Rendering

```xml
<defs>
  <!-- Define patterns for each resource type -->
  <pattern id="tile-brick" patternUnits="objectBoundingBox" width="1" height="1">
    <image href="/images/tiles/brick.png" preserveAspectRatio="xMidYMid slice"
           width="100" height="87"/>
  </pattern>
  <!-- ... other patterns -->
</defs>

<!-- Each tile hex -->
<g class="tile" data-q="0" data-r="0">
  <!-- Border (outer hex) -->
  <path d="M..." stroke="#8B4513" stroke-width="4" fill="url(#tile-brick)"/>

  <!-- Number token -->
  <g transform="translate(cx, cy-20)">
    <circle r="20" fill="#2F6999" opacity="0.75" stroke="white" stroke-width="0.5"/>
    <text y="-3" text-anchor="middle" font-weight="bold" font-size="18" fill="white">5</text>
    <text y="8" text-anchor="middle" font-size="8" fill="white">••••</text>
  </g>
</g>
```

### Road Rendering

Roads are rendered as 6-point polygons using an **inner/outer hex geometry** approach that guarantees perfect alignment at vertices.

#### Inner/Outer Hex Architecture

The Desktop app uses two concentric hexagons per tile:
- **Outer Hex**: Defines the tile boundaries and vertex positions (size = `OuterHexSize`)
- **Inner Hex**: Not visible, used for layout calculations (size = `InnerHexSize`)

Both hexes share the same center. The gap between them creates the space for roads.

```
     Outer Hex (visible tile boundary)
    /‾‾‾‾‾‾‾‾‾‾‾‾‾‾\
   /  Inner Hex     \
  /  /‾‾‾‾‾‾‾‾‾‾\    \
 |  |   Tile    |    |
 |  |  Content  |    |
  \  \__________/    /
   \   Road Gap    /
    \______________/
```

#### Size Calculations

From `BoardVisualLayout.cs`:

```csharp
InnerHexSize = OuterHexSize - TileGap - InnerHexStrokeThickness * 0.5
```

**Desktop Default Values:**
- `OuterHexSize = 100` (distance from center to vertex)
- `TileGap = 2`
- `InnerHexStrokeThickness = 16`
- Therefore: `InnerHexSize = 100 - 2 - 8 = 90`

**WebUI Equivalent (HexSize = 50):**
- `OuterHexSize = 50`
- Using ratio: `InnerHexSize = OuterHexSize * 0.9 = 45`

#### Road Polygon Construction

Each road is a 6-point polygon connecting two adjacent tiles:

```
     O1 (outer vertex 1)
    /  \
   I1   I1_adj
   |     |
   I2   I2_adj
    \  /
     O2 (outer vertex 2)
```

**The 6 points:**
1. **O1** - Outer vertex 1 (tip at vertex)
2. **I1** - This tile's inner vertex 1
3. **I2** - This tile's inner vertex 2
4. **O2** - Outer vertex 2 (tip at vertex)
5. **I2_adj** - Adjacent tile's inner vertex 2
6. **I1_adj** - Adjacent tile's inner vertex 1

#### How Inner Points Are Calculated

For a flat-top hex, vertices are calculated from the center at angles 0°, 60°, 120°, 180°, 240°, 300°.

**This tile's inner points:** Scale outer vertex toward this tile's center
```
I = TileCenter + (O - TileCenter) * (InnerHexSize / OuterHexSize)
```

**Adjacent tile's inner points:** Scale outer vertex toward adjacent tile's center
```
I_adj = AdjTileCenter + (O - AdjTileCenter) * (InnerHexSize / OuterHexSize)
```

#### SVG Implementation

For SVG rendering without per-tile context, calculate inner points using the perpendicular offset:

```csharp
private string GenerateRoadPolygon((double x, double y) v1, (double x, double y) v2)
{
    // Edge direction and perpendicular
    var dx = v2.x - v1.x;
    var dy = v2.y - v1.y;
    var length = Math.Sqrt(dx * dx + dy * dy);
    var dirX = dx / length;
    var dirY = dy / length;
    var perpX = -dirY;  // Perpendicular (90° CCW)
    var perpY = dirX;

    // Inner hex offset from outer vertex
    // This is the "inset" distance at the vertex
    double ratio = InnerHexSize / OuterHexSize;  // e.g., 0.9
    double inset = OuterHexSize * (1 - ratio);   // e.g., 5

    // The perpendicular offset at the vertex (not at edge midpoint)
    // For proper alignment, use the hex geometry relationship
    double vertexOffset = inset * Math.Sqrt(3) / 2;

    // 6 points: outer tips and inner body edges
    var points = new List<string>
    {
        // Point 1: O1 (outer vertex 1 - tip)
        $"{v1.x:F1},{v1.y:F1}",
        // Point 2: I1 (this tile's inner, offset toward tile A)
        $"{(v1.x + perpX * vertexOffset):F1},{(v1.y + perpY * vertexOffset):F1}",
        // Point 3: I2 (this tile's inner, offset toward tile A)
        $"{(v2.x + perpX * vertexOffset):F1},{(v2.y + perpY * vertexOffset):F1}",
        // Point 4: O2 (outer vertex 2 - tip)
        $"{v2.x:F1},{v2.y:F1}",
        // Point 5: I2_adj (adjacent tile's inner, offset toward tile B)
        $"{(v2.x - perpX * vertexOffset):F1},{(v2.y - perpY * vertexOffset):F1}",
        // Point 6: I1_adj (adjacent tile's inner, offset toward tile B)
        $"{(v1.x - perpX * vertexOffset):F1},{(v1.y - perpY * vertexOffset):F1}"
    };

    return string.Join(" ", points);
}
```

#### Why This Works

1. **Tips at outer vertices**: The road extends to the exact vertex where tiles meet
2. **Body at inner vertices**: Each tile's inner boundary defines its side of the road
3. **Automatic 120° angle**: The ratio between inner and outer hex sizes naturally creates the correct tip angle
4. **Perfect snapping**: Roads from adjacent tiles share the same outer vertices and their inner edges align

#### Visual Result

When 3 roads meet at a vertex:

```
       Road A
         /\
        /  \      <-- Tips meet at outer vertex
       /    \
      I1    I1_adj
     /        \
    /          \
   I2          I2_adj
    \          /
     \        /
      \______/
     Road B   Road C
```

**Road States:**
- **Empty**: Transparent (hidden)
- **Hover**: White semi-transparent (shows placement option)
- **Placed**: Player color with stroke

**Z-Order:**
Roads are rendered BEFORE settlements/cities, ensuring buildings appear on top of road intersections.

#### Desktop Implementation Reference

See `DesktopApp/Roads/RoadViewModel.cs` `PointsForSide()` method for the authoritative implementation that builds road polygons using `outerHexPoints`, `innerHexPoints`, and adjacent tile deltas.

### Settlement/City Rendering

Settlements are circles at hex vertices:

```xml
<!-- Settlement at vertex -->
<g class="settlement" transform="translate(vx, vy)">
  <circle r="12" fill="transparent" stroke="transparent"/>

  <!-- When placed, show player color and icon -->
  <circle r="12" fill="#FF0000" stroke="#333" stroke-width="1"/>
  <text text-anchor="middle" dominant-baseline="central"
        font-family="Catan" font-size="16" fill="white">&#xE926;</text>
</g>
```

**Building Icons (Catan Font):**
- Settlement: `\uE926`
- City: `\uE900`

**Settlement States:**
- **Empty**: Transparent (hidden)
- **Hover**: White semi-transparent (shows placement option)
- **Placed Settlement**: Player color with settlement icon
- **Placed City**: Player color with city icon

### Harbor Rendering

Harbors are positioned on hex edges, showing trading ports with image patterns.

#### Harbor Data Model

From `HarborModel.cs`:
- **HarborKey**: Contains HexCoordinates, HarborType, and Side (HexSide)
- **HarborType enum**: `Sheep, Wood, Ore, Wheat, Brick, ThreeForOne, None`

#### Harbor Positioning

Harbors are positioned at the midpoint of hex edges, offset outward to sit "on" the edge:

```csharp
// Get vertex position for the side using PointyHexPoints
WinPoint vertexPoint = pointDictionary[side];

// Center harbor on vertex, then offset to edge
top += vertexPoint.Y - size / 2.0;
left += vertexPoint.X - size / 2.0;

// Edge offset calculations
double edgeOffset = size / 2.0;  // Harbor's radius
double horizontalOffset = edgeOffset * Math.Sqrt(3) / 2;  // sqrt(3)/2
double verticalOffset = edgeOffset * 0.5;  // 1/2

// Adjust based on side
switch (side) {
    case HexSide.Top:        top -= edgeOffset; break;
    case HexSide.TopRight:   left += horizontalOffset; top -= verticalOffset; break;
    case HexSide.BottomRight: left += horizontalOffset; top += verticalOffset; break;
    case HexSide.Bottom:     top += edgeOffset; break;
    case HexSide.BottomLeft: left -= horizontalOffset; top += verticalOffset; break;
    case HexSide.TopLeft:    left -= horizontalOffset; top -= verticalOffset; break;
}
```

#### Visual Elements

Each harbor consists of:

1. **Water background triangle (HarborPoints)**: Connects harbor center to the two adjacent hex vertices
2. **Harbor circle**: Ellipse with harbor type image
   - Size = BuildingSize (40 in Desktop app)
   - Scaled 1.5x for visibility
   - Translated to center properly

#### Harbor Images

| HarborType | Image File | Trade Rate |
|------------|------------|------------|
| Brick | `2 for 1 brick.png` | 2:1 Brick |
| Ore | `2 for 1 ore.png` | 2:1 Ore |
| Sheep | `2 for 1 sheep.png` | 2:1 Sheep |
| Wheat | `2 for 1 wheat.png` | 2:1 Wheat |
| Wood | `2 for 1 wood.png` | 2:1 Wood |
| ThreeForOne | `3 for 1.png` | 3:1 Any |
| None | `water.png` | (No harbor) |

#### SVG Implementation

```xml
<defs>
  <!-- Harbor image patterns -->
  <pattern id="harbor-brick" patternUnits="objectBoundingBox" width="1" height="1">
    <image href="/images/harbors/2-for-1-brick.png" width="60" height="60"
           preserveAspectRatio="xMidYMid slice"/>
  </pattern>
  <!-- ... other harbor types ... -->
</defs>

<!-- For each harbor -->
<g class="harbor">
  <!-- Water background triangle -->
  <polygon points="cx,cy v1x,v1y v2x,v2y" fill="#3498db" opacity="0.7"/>

  <!-- Harbor circle with image -->
  <circle cx="hx" cy="hy" r="30" fill="url(#harbor-brick)"
          stroke="#333" stroke-width="1"/>
</g>
```

#### Harbor Position by Side

For a flat-top hex, harbors on each side face outward:

```
         TopLeft    Top    TopRight
              \      |      /
               \     |     /
                +----+----+
               /           \
      Left    |    Hex     |   Right (not a side)
               \           /
                +----+----+
               /     |     \
              /      |      \
      BottomLeft  Bottom  BottomRight
```

**Edge-to-vertex mapping:**
- Top: TopLeft ↔ TopRight vertices
- TopRight: TopRight ↔ Right vertices
- BottomRight: Right ↔ BottomRight vertices
- Bottom: BottomRight ↔ BottomLeft vertices
- BottomLeft: BottomLeft ↔ Left vertices
- TopLeft: Left ↔ TopLeft vertices

#### Desktop Implementation Reference

See `DesktopApp/Harbors/HarborViewModel.cs` for positioning logic and `HarborCtrl.xaml` for visual structure.

## Game State Visualization

### Roll Highlighting

When dice are rolled, tiles with matching numbers are highlighted:

```xml
<!-- Highlighted tile -->
<path d="M..." class="tile highlighted"
      stroke="#FFD700" stroke-width="6"
      filter="url(#glow)"/>
```

**Implementation:**
- Add CSS class or stroke color change
- Optional glow filter for emphasis
- Animate highlight appearing/fading

### Robber Placement

The robber blocks a tile's production:

```xml
<!-- Robber on tile -->
<g class="robber" transform="translate(cx, cy+20)">
  <text font-family="Catan" font-size="32" fill="#333">&#xE90C;</text>
</g>
```

## Other Game Controls

### Resource Tracking Panel

Displays each player's current resources:

```
┌─────────────────────────────┐
│ Player 1 (Red)              │
│ ┌───┐ ┌───┐ ┌───┐ ┌───┐ ┌───┐│
│ │🌾 │ │🪵 │ │🪨 │ │🧱 │ │🐑 ││
│ │ 3 │ │ 2 │ │ 0 │ │ 1 │ │ 4 ││
│ └───┘ └───┘ └───┘ └───┘ └───┘│
└─────────────────────────────┘
```

### Roll Entry / Dice Display

Shows current dice roll and allows input:

- Two dice images or numbers
- Total displayed prominently
- History of recent rolls

### Action Buttons

```
┌──────┐ ┌──────┐ ┌──────┐
│ Next │ │ Undo │ │ Redo │
└──────┘ └──────┘ └──────┘
```

### Player Statistics Panel

Uses Catan font glyphs with counts (from PlayerCtrl.xaml):

```
┌─────────────────────────────────────────┐
│ [Photo] │ 🏆 │ 🛤️ │ 🏠 │ 🏰 │ ⚔️ │ 💰 │
│         │ 7  │ 8  │ 3  │ 1  │ 2  │ 12 │
├─────────────────────────────────────────┤
│ Resources this turn: +2🪨 +1🌾         │
└─────────────────────────────────────────┘
```

**Stats tracked:**
- Score (victory points)
- Roads played
- Settlements played
- Cities played
- Soldiers played
- Resources lost to robber
- Times targeted
- Total resources
- Longest road
- Good/bad rolls
- Stars (buildable indicators)

### Resource Card Flip Animation

When a roll produces resources, cards flip to show what each player received:

```
Before roll:          After roll (6 rolled):
┌─────┐               ┌─────┐
│ ??? │    →          │ 🪨  │
│     │               │  2  │
└─────┘               └─────┘
```

**Animation sequence:**
1. Card starts face-down
2. Card flips over (3D rotation)
3. Shows resource image and count
4. Remains visible until acknowledged or next roll

## Hit Testing

### SVG Element Selection

Use data attributes for identifying clicked elements:

```xml
<path class="tile" data-q="0" data-r="0" data-resource="brick"/>
<line class="road" data-edge="0,0,0-1,-1,0"/>
<circle class="settlement" data-vertex="0,0,0-1,-1,0-0,1,-1"/>
```

### Coordinate Conversion

Convert mouse position to game coordinates:

```javascript
// Get SVG point from mouse event
const svgPoint = svg.createSVGPoint();
svgPoint.x = event.clientX;
svgPoint.y = event.clientY;
const transformed = svgPoint.matrixTransform(svg.getScreenCTM().inverse());

// Convert to hex coordinates (use shared HexCoordinates logic)
const hexCoords = pixelToAxial(transformed.x, transformed.y, hexSize, offsetX, offsetY);
```

## Color Scheme

### Resource Type Colors (Fallback)

When images aren't loaded, use these solid colors:

| Resource | Color | Hex |
|----------|-------|-----|
| Wheat | Yellow | #f4d03f |
| Wood | Green | #27ae60 |
| Ore | Gray | #7f8c8d |
| Brick | Red/Brown | #c0392b |
| Sheep | Light Green | #a8e6cf |
| Desert | Tan | #f5deb3 |
| Sea | Blue | #3498db |
| Gold Mine | Gold | #f39c12 |

### UI Colors

| Element | Color | Usage |
|---------|-------|-------|
| Ocean background | #1e90ff | Board background |
| Tile border | bmCherry | Between tiles |
| Number token bg | #2F6999 | Roll number circle |
| Highlight | #FFD700 | Active tile |
| Road hover | rgba(255,255,255,0.8) | Placement preview |

## Responsive Design

### Board Scaling

- SVG uses `viewBox` for coordinate space
- CSS `width: 100%; height: 100%` fills container
- `preserveAspectRatio="xMidYMid meet"` centers and scales

### Touch Support

- Larger hit targets for mobile
- Touch events mapped to mouse events
- Pinch-to-zoom consideration

## Animation Considerations

### CSS Transitions

```css
.tile {
  transition: stroke 0.3s ease, filter 0.3s ease;
}

.road:hover {
  stroke: rgba(255, 255, 255, 0.8);
  transition: stroke 0.2s ease;
}

.settlement:hover {
  fill: rgba(255, 255, 255, 0.9);
  transition: fill 0.2s ease;
}
```

### SVG Animations

For more complex animations (card flips, dice rolls):
- CSS transforms for 3D rotations
- JavaScript-driven animations for sequencing
- Consider using a library like GSAP for smooth animations

## File References

### Desktop App (Visual Reference)

- `DesktopApp/Tiles/TileCtrl.xaml` - Tile rendering structure
- `DesktopApp/Tiles/CatanNumber.xaml` - Number token with pips
- `DesktopApp/Harbors/HarborCtrl.xaml` - Harbor rendering
- `DesktopApp/Player/PlayerCtrl.xaml` - Player stats panel
- `DesktopApp/Layout/CatanFont.cs` - Glyph definitions
- `DesktopApp/Themes/ImageResources.xaml` - Image brush definitions

### WebUI Implementation

- `Catan3.GameService/Services/BoardSvgGenerator.cs` - SVG generation
- `WebUI/Pages/Game.razor` - Game page with SVG display
- `WebUI/wwwroot/images/` - Static image assets
- `WebUI/wwwroot/fonts/Catan.ttf` - Catan font

### Shared Logic

- `Catan3.Shared/Utility/HexCoordinates.cs` - Coordinate math
- `Catan3.Shared/Models/GameModel.cs` - Game state
- `Catan3.Shared/Models/TileModel.cs` - Tile data

## Implementation Phases

### Phase 1: Basic Board (Current)
- [x] Hex grid with correct orientation
- [x] Solid color fills for resources
- [x] Basic number display
- [x] Road/settlement hover effects
- [x] Responsive sizing

### Phase 2: Visual Polish
- [ ] Tile texture patterns from images
- [ ] Number tokens with probability pips
- [ ] Tile border styling (bmCherry)
- [ ] Catan font for building icons

### Phase 3: Harbors & Buildings
- [ ] Harbor image patterns
- [ ] Harbor positioning and rotation
- [ ] Settlement/city rendering with player colors
- [ ] Road rendering with player colors

### Phase 4: Interactivity
- [ ] Click handlers for placement
- [ ] Roll highlighting
- [ ] Robber movement
- [ ] Building upgrade (settlement → city)

### Phase 5: Game Controls
- [ ] Player stats panel
- [ ] Resource tracking
- [ ] Dice/roll display
- [ ] Action buttons (Next/Undo/Redo)
- [ ] Resource card flip animation

## References

- [assets-design.md](./assets-design.md) - Image assets and patterns
- [WebUI-Design.md](./WebUI-Design.md) - Overall WebUI architecture
- [Coordinate-Design.md](./Coordinate-Design.md) - Hex coordinate system
- [Red Blob Games Hexagons](https://www.redblobgames.com/grids/hexagons/) - Hex math reference
