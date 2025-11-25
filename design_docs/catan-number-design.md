# CatanNumber Design

## Overview
The CatanNumber control displays the roll number and probability indicators (stars/pips) on resource tiles. This document describes the exact positioning to match the Desktop app's `CatanNumber.xaml` layout.

## XAML Reference (CatanNumber.xaml)
```xaml
<Grid Width="65" Height="65">
    <Ellipse Fill="#FF2F6999" Stroke="White" StrokeThickness=".5" Opacity=".75" />
    <TextBlock FontFamily="Segoe UI" FontWeight="Bold"
            Text="{x:Bind Number, Mode=OneWay}"
            Foreground="{x:Bind BIND_StarForeground(Number), Mode=OneWay}"
            VerticalAlignment="Top" HorizontalAlignment="Center"
            FontSize="24" />
    <TextBlock FontFamily="Segoe Fluent Icons" FontSize="10" Margin="0,10,0,0"
            HorizontalAlignment="Center" VerticalAlignment="Center"
            Text="{x:Bind Stars,Mode=OneWay}"
            Foreground="{x:Bind BIND_StarForeground(Number), Mode=OneWay}" />
</Grid>
```

## Layout Specifications

### Container
- **Size**: 65×65 pixels
- **Circle radius**: 32.5 pixels (half of container)

### Background Circle
- **Fill**: `#2F6999` (blue)
- **Opacity**: 0.75
- **Stroke**: White
- **Stroke width**: 0.5 pixels

### Roll Number (Top TextBlock)
- **Font family**: Segoe UI (or sans-serif fallback in SVG)
- **Font size**: 24 pixels
- **Font weight**: Bold
- **Vertical alignment**: **Top** (key positioning detail)
- **Horizontal alignment**: Center
- **Color**:
  - High probability (6, 8): Red (`#c00`)
  - Normal: White (`#fff`)

#### Positioning in SVG
- The entire 65×65 grid is positioned above the tile center
- Circle center is at `tileY - 50` (50 pixels above tile center)
- Number text is at `circleY - 1` (1 pixel above circle center for visual balance)

### Probability Pips/Stars (Bottom TextBlock)
- **Font family**: Segoe Fluent Icons (rendered as star characters `★` in SVG)
- **Font size**: 14 pixels (specified as 10 in XAML but visually appears larger)
- **Vertical alignment**: Center
- **Top margin**: 10 pixels
- **Horizontal alignment**: Center
- **Color**: Same as roll number
- **Content**: Unicode stars (★) repeated based on probability:
  - 2 or 12: `•` (1 pip)
  - 3 or 11: `••` (2 pips)
  - 4 or 10: `•••` (3 pips)
  - 5 or 9: `••••` (4 pips)
  - 6 or 8: `•••••` (5 pips)
  - 7: (none)

#### Positioning in SVG
- Pips are positioned below the number within the circle
- Position is at `circleY + 20` (20 pixels below circle center)
- This matches XAML's `Margin="0,10,0,0"` + `VerticalAlignment="Center"`

## SVG Implementation

### Key Differences from XAML
1. **Coordinate system**: SVG uses absolute positioning, XAML uses alignment properties
2. **Text baseline**: SVG uses `dominant-baseline="middle"` to center text vertically at the y coordinate
3. **Font mapping**: Segoe UI → sans-serif, Segoe Fluent Icons → Unicode star character (★)

### Rendering Order
1. Background circle at tile center
2. Roll number text (offset from circle center)
3. Probability pips/stars (offset from circle center)

### Constants (BoardSvgConstants.cs)
```csharp
// Number token - matches original working server-side rendering
// Note: XAML uses 65x65 grid, but we render with radius 30 (not 32.5) for proper visual balance
public const double NumberTokenRadius = 30;  // From original working code
public const double NumberTokenOffsetY = 50;  // Circle center offset from tile center
public const double NumberTokenOpacity = 0.85;  // From original working code

// Font sizes - matches Desktop CatanNumber.xaml
public const double NumberFontSize = 24;  // Number text
public const double PipsFontSize = 12;    // Star/pip text (user-adjusted for visual match)

// Text positioning within the number token circle
// Number: VerticalAlignment="Top" in XAML, slightly offset from circle center for visual balance
public const double NumberOffsetY = -10;  // Small adjustment above circle center (user-adjusted)
// Pips: Below the number, matching XAML Margin="0,10,0,0" + VerticalAlignment="Center"
public const double PipsOffsetY = 10;  // Below circle center (user-adjusted)
```

**Note**: The radius (30 vs 32.5), pip font size (12 vs 24), and offsets have been manually adjusted from the XAML values to achieve the correct visual appearance in SVG. These values were verified against Desktop app screenshots.

## Visual Verification
The layout should match the Desktop app exactly:
- Roll number appears near the TOP of the circle (not centered)
- Probability pips appear centered below the number
- High probability numbers (6, 8) are red, others are white
- Circle is semi-transparent blue with white border
