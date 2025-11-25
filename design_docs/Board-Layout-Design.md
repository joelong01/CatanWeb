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

### Compositional Design (Mirrors Desktop XAML)

The SVG rendering uses a **compositional approach** that mirrors the Desktop XAML control structure:

```
BoardSvgGenerator (like BoardLayoutCtrl.xaml)
├── TileSvgRenderer (like TileCtrl.xaml)
│   ├── Renders hex geometry (outer and inner polygons)
│   ├── Applies resource texture patterns
│   ├── Positions number tokens with probability pips
│   └── Handles tile-specific visuals
├── BuildingSvgRenderer (like BuildingCtrl.xaml)
│   ├── Renders settlements, cities, knights
│   ├── Uses SVG files (settlement.svg, city.svg, knight.svg)
│   ├── Applies player gradient colors
│   └── Handles visual states (highlighted, hidden, stars, normal)
├── RoadSvgRenderer (like RoadCtrl.xaml)
│   ├── Calculates 6-point road polygon using inner/outer hex geometry
│   ├── Applies player colors when placed
│   └── Handles road states (transparent, hover, placed)
└── HarborSvgRenderer (like HarborCtrl.xaml)
    ├── Positions harbors on hex edges
    ├── Renders water triangle background
    └── Applies harbor type patterns

```

**Benefits:**
- **Testable** - Each renderer can be unit tested independently
- **Reusable** - Components can be used outside board context
- **Maintainable** - Clear separation of concerns
- **Consistent** - Direct mapping to Desktop XAML controls
- **SVG-native** - Uses embedded SVG files instead of icon fonts (no CatanFont dependency)

**SVG File Strategy:**
- Use SVG files from `DesktopApp/Assets/SVG/` directory (settlement.svg, city.svg, knight.svg, road.svg, star.svg)
- Avoid CatanFont for game pieces - web has native SVG support
- Standard icon fonts (Segoe MDL2 Assets) still used for UI controls (shuffle, undo buttons)

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

### Temporary Gold Tiles Feature

**House Rule**: Some game variants allow random tiles to temporarily become gold mines during a turn.

**Visual Behavior:**
1. When `TileModel.TemporarilyGold` is true:
   - Tile's main texture changes to gold mine pattern
   - A small resource card (67×100) appears on the tile showing the **original** resource type
   - The card is positioned at tile center with 50px offset downward
   - Card flips over with 3D animation to reveal the resource

2. **Desktop Implementation** (`TileCtrl.xaml`):
   ```xml
   <Viewbox Height="50" Stretch="Uniform"
           HorizontalAlignment="Center"
           VerticalAlignment="Center" Margin="0, 50, 0, 0">
       <u:FlipperCtrl Orientation="{x:Bind TileViewModel.TempGoldOrientation(...)}">
           <u:FlipperCtrl.Front>
               <Border CornerRadius="5" Width="67" Height="100">
                   <Rectangle Fill="{x:Bind TileViewModel.GetResourceImage(...)}"/>
               </Border>
           </u:FlipperCtrl.Front>
           <u:FlipperCtrl.Back>
               <Grid Opacity="0" Background="Black" Width="67" Height="100"/>
           </u:FlipperCtrl.Back>
       </u:FlipperCtrl>
   </Viewbox>
   ```

3. **WebUI Implementation Strategy:**
   - TileSvgRenderer checks `tile.TemporarilyGold`
   - If true, render gold mine texture as base
   - Add embedded `<image>` element showing original resource card
   - Position at `(cx, cy + 50)` - 50px below tile center
   - Use CSS animation or SMIL for flip effect
   - Card size: 67×100 (matches Desktop resource card dimensions)

4. **Data Flow:**
   - GameService sets `TileModel.TemporarilyGold = true` based on house rules
   - Original resource type preserved in `TileModel.ResourceTileType`
   - Gold mine becomes the **displayed** resource (for roll resolution)
   - Small card shows **original** resource (for player reference)

**Purpose**: Adds variability to game strategy by temporarily changing tile production.

### Tile Visual States and Animations

**Desktop Features Ported to WebUI** (`TileCtrl.xaml`):

1. **Dim/Revert Animations** (Gameplay Highlighting):
   - `DimAnimation`: Fade tile to 0.5 opacity (de-emphasize unrolled tiles)
   - `RevertAnimation`: Restore to 1.0 opacity (normal state)
   - **Usage**: After dice roll, all tiles NOT matching the roll dim so active tiles stand out
   - **WebUI**: CSS opacity transition on tile `<g>` element or class toggle

2. **Tile Coordinates Display** (Debug/Communication Tool):
   - Shows tile index (sequential number, e.g., "0", "1", "2")
   - Shows hex coordinates in q,r format (e.g., "0,0")
   - Positioned at bottom of tile with black background
   - **Purpose**: Useful for debugging layouts and communicating tile positions to AI
   - **WebUI**: SVG `<text>` elements at bottom of tile, visibility controlled by query param or setting

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

### Road Rendering Features

**All 4 Desktop Features Ported** (`RoadCtrl.xaml`):

#### 1. Player Colors (Fill and Stroke)
- Desktop:
  - `Fill="{x:Bind GetBackgroundBrush(RoadState, OwnerId, CurrentPlayer)}"`
  - `Stroke="{x:Bind GetForegroundBrush(RoadState, OwnerId, CurrentPlayer)}"`
- Uses player's colors when road is owned
- **WebUI**: Apply `PlayerData.PrimaryBackgroundColor` (or gradient) for fill, `ForegroundColor` for stroke

#### 2. Stroke Thickness
- Desktop: `StrokeThickness="{x:Bind Layout.RoadStrokeThickness}"`
- Consistent stroke width from layout constants
- **WebUI**: Apply to polygon `stroke-width` attribute

#### 3. Opacity Based on Road State (Optional)
- Desktop: `Opacity="{x:Bind Opacity(Road, RoadState)}"`
- Different opacity for different states (buildable, unowned, owned)
- **WebUI**: CSS class or inline `opacity` attribute
- **Status**: Optional - may not be needed initially

#### 4. Build Index Display
- Desktop: Viewbox at `RoadCenter` with black rounded background
  ```xml
  <Viewbox Canvas.Left="{RoadCenter.X}" Canvas.Top="{RoadCenter.Y}">
    <Grid Background="Black" CornerRadius="5">
      <TextBlock Text="{BuildIndex}" Foreground="White"/>
    </Grid>
  </Viewbox>
  ```
- Shows build order number at road midpoint
- **Purpose**: Player can say "Build road 7" instead of describing position
- **WebUI**: SVG `<text>` with black rounded `<rect>` background, positioned at road center

**Road Center Calculation:**
```csharp
// Midpoint of the 6-point polygon
var roadCenter = new Point(
    (v1.X + v2.X) / 2,  // Average of outer vertices
    (v1.Y + v2.Y) / 2
);
```

**WebUI SVG Implementation Example:**

```xml
<g class="road" data-road-id="0,0,0-1,0,-1">
  <!-- 6-point road polygon with player colors -->
  <polygon points="100,50 105,55 105,65 100,70 95,65 95,55"
           fill="#FF0000"
           stroke="#FFFFFF"
           stroke-width="2"
           opacity="1.0"/>

  <!-- Build index (optional, visibility controlled) -->
  <g transform="translate(100, 60)">
    <rect x="-10" y="-10" width="20" height="20" rx="5" fill="black"/>
    <text x="0" y="0" text-anchor="middle" dominant-baseline="central"
          font-size="12" fill="white">7</text>
  </g>
</g>
```

**Road States:**
- **Unowned/Buildable**: Transparent or semi-transparent
- **Owned**: Player colors with full opacity
- **Hover**: Highlighted for placement preview

### Building Rendering (Settlements/Cities/Knights)

**All 5 Desktop Features Ported** (`BuildingCtrl.xaml`):

#### 1. Circular Shape (CornerRadius="20")
- Desktop: `Grid` with `CornerRadius="20"`, `Width="40"`, `Height="40"`
- WebUI: SVG `<circle>` with `r="20"` (radius = BuildingSize/2)

#### 2. Player Gradient Backgrounds
- Desktop: `Background="{x:Bind BIND_Background(VisualState, OwnerId, CurrentPlayer)}"`
- Gradient from `PlayerData.PrimaryBackgroundColor` to `PlayerData.SecondaryBackgroundColor`
- WebUI: SVG `<linearGradient>` defined per player, referenced in circle fill

#### 3. Player Foreground Colors
- Desktop: `Foreground="{x:Bind BIND_Foreground(VisualState, OwnerId, CurrentPlayer)}"`
- Uses `PlayerData.ForegroundColor` for text, icons, and border
- WebUI: Apply to SVG `stroke`, `<text>` fill, or `<image>` filter

#### 4. State Glyph Rendering (Icon or Star Number)
- Desktop: `Text="{x:Bind BIND_StateGlyph(BuildingState, VisualState, Stars)}"`
- Logic:
  - If `VisualState == Stars`: Display star number (e.g., "13")
  - If `VisualState == Highlighted/Normal`: Display building icon from CatanFont
  - If `VisualState == Hidden`: Display nothing
- **WebUI Replacement**: Use SVG files instead of CatanFont
  - `settlement.svg` for Settlement
  - `city.svg` for City
  - `knight.svg` for Knight (Cities & Knights expansion - activated knights placed on vertices)
  - Or plain `<text>` for star numbers
- **Note**: `robber.svg` is separate - it's the piece on tiles that blocks production, not a building

#### 5. Build Index Display (Optional)
- Desktop: `Text="{x:Bind BuildIndex}"` with visibility binding
- Small number on right side showing build order
- Useful for debugging/game analysis
- WebUI: Optional `<text>` element positioned at right edge

**Visual State Logic** (`BuildingVisualState` enum):

| State | When Used | Background | Content |
|-------|-----------|------------|---------|
| **Highlighted** | Normal gameplay | Player gradient | Building icon |
| **Hidden** | Building not visible | Transparent | None |
| **Stars** | During PickingBoard | Player gradient | Star number (10-13) |
| **Normal** | Default state | Player gradient | Building icon |

**WebUI SVG Implementation Example:**

```xml
<defs>
  <!-- Player gradient (define once per player) -->
  <linearGradient id="player-alice-gradient" x1="0%" y1="0%" x2="100%" y2="100%">
    <stop offset="0%" stop-color="#0000FF"/> <!-- PrimaryBackgroundColor -->
    <stop offset="100%" stop-color="#000080"/> <!-- SecondaryBackgroundColor -->
  </linearGradient>
</defs>

<g class="building" data-building-id="0,0,0:TopRight">
  <!-- 1. Circle with player gradient background -->
  <circle cx="100" cy="100" r="20"
          fill="url(#player-alice-gradient)"
          stroke="#FFFFFF" stroke-width="1"/> <!-- 3. Player foreground color -->

  <!-- 4. State glyph: Either icon or star number -->
  <!-- If VisualState = Stars: -->
  <text x="100" y="100" text-anchor="middle" dominant-baseline="central"
        font-size="16" font-weight="bold" fill="#FFFFFF">13</text>

  <!-- OR if VisualState = Highlighted: -->
  <!-- settlement.svg, city.svg, or knight.svg depending on BuildingState -->
  <image href="/images/svg/settlement.svg" x="90" y="90" width="20" height="20"/>

  <!-- 5. Build index (optional) -->
  <text x="115" y="100" font-size="10" fill="#FFFFFF">1</text>
</g>
```

**Building States:**
- **Empty/Not Built**: No circle rendered (or transparent circle for hover preview)
- **Possible Settlement**: Semi-transparent white circle (placement preview)
- **Placed**: Player gradient + icon or star count based on visual state

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
