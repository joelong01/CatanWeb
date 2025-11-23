# Assets Design Document

## Overview

This document describes the asset architecture for the Catan game, covering the existing Desktop app's resource system and the planned approach for WebUI asset delivery. The goal is to reuse the high-resolution tile images from the Desktop app in the WebUI with efficient caching and rendering.

## Desktop App Resource Architecture

### Resource Dictionary Structure

The Desktop app uses XAML ResourceDictionaries loaded in `App.xaml`:

```
DesktopApp/
├── Themes/
│   ├── ImageResources.xaml      # Primary image definitions (40+ ImageBrush)
│   ├── StyleResourceDictionary.xaml  # UI element styles
│   ├── Generic.xaml             # Control templates
│   └── ConverterDictionary.xaml # Value converters
├── Assets/
│   ├── Tiles/                   # Hex tile images
│   ├── Harbors/                 # Harbor images
│   ├── ResourceCards/           # Card images
│   ├── Fonts/                   # Custom Catan font
│   └── DefaultPlayers/          # Player profile images
```

**Load Order (App.xaml):**
1. XamlControlsResources (WinUI3 system styles)
2. ConverterDictionary.xaml (value converters - must load first)
3. Generic.xaml (control templates)
4. StyleResourceDictionary.xaml (UI element styles)
5. ImageResources.xaml (tile/card/harbor images)

### ImageResources.xaml Pattern

Each image is defined as a static `ImageBrush` resource with a key matching the enum value:

```xml
<ImageBrush x:Key="ResourceTileType.Brick"
            ImageSource="../Assets/Tiles/brick.png"
            Stretch="UniformToFill"/>
<ImageBrush x:Key="ResourceTileType.Wheat"
            ImageSource="../Assets/Tiles/wheat.png"
            Stretch="UniformToFill"/>
```

**Usage in XAML controls:**
```xml
<Polygon Fill="{StaticResource ResourceTileType.Brick}"/>
```

### Tile Images (Assets/Tiles/)

| Resource Type | File | Size | Notes |
|--------------|------|------|-------|
| Brick | brick.png | 487KB | Red/brown clay |
| Wheat | wheat.png | 633KB | Yellow grain field |
| Wood | wood.png | 531KB | Green forest |
| Ore | ore.png | 487KB | Gray mountains |
| Sheep | sheep.png | 439KB | Light green pasture |
| Desert | desert.png | 2.3MB | Tan desert |
| Gold Mine | goldMine.png | 5.6MB | Gold/orange mine |
| Sea | back.jpg | 18KB | Blue water |
| Back | back.jpg | 18KB | Tile back (same as sea) |

**Total tile images: ~10MB**

### Harbor Images (Assets/Harbors/)

| Harbor Type | File | Size |
|------------|------|------|
| 2:1 Brick | 2 for 1 brick.png | 72KB |
| 2:1 Ore | 2 for 1 ore.png | 72KB |
| 2:1 Sheep | 2 for 1 sheep.png | 57KB |
| 2:1 Wheat | 2 for 1 wheat.png | 56KB |
| 2:1 Wood | 2 for 1 wood.png | 58KB |
| 3:1 Generic | 3 for 1.png | 55KB |

**Total harbor images: ~370KB**

### Resource Card Images (Assets/ResourceCards/)

19 card types totaling ~97MB including:
- Base resources (brick, wheat, wood, ore, sheep)
- Expansion resources (cloth, paper, coin, gold mine)
- Development cards (trade, politics, science, victory point)
- Special cards (back, robber, invasion)

### Background Images

| Image | File | Size | Usage |
|-------|------|------|-------|
| Water | water.png | 32MB | Board background, water tiles |
| Cherry | cherry.jpg | 2.0MB | Tile border overlay |
| Maple | maple.jpg | 63KB | Alternative background |

### Custom Font (Assets/Fonts/)

| Font | File | Usage |
|------|------|-------|
| Catan | Catan.ttf | Symbol glyphs for game elements (icons, not numbers) |

**Font registration in App.xaml:**
```xml
<FontFamily x:Key="CatanFont">ms-appx:///../Assets/Fonts/Catan.ttf#Catan</FontFamily>
```

**Glyph Definitions (from `DesktopApp/Layout/CatanFont.cs`):**

| Symbol | Unicode | Constant | Usage |
|--------|---------|----------|-------|
| City | \uE900 | `CatanFont.City` | City building icon |
| Settlement | \uE926 | `CatanFont.Settlement` | Settlement building icon |
| Road | \uE909 | `CatanFont.Road` | Road icon |
| Soldier | \uE90E | `CatanFont.Soldier` | Soldier/knight icon |
| Score/Laurel | \uE907 | `CatanFont.Score` | Victory point display |
| Pirate | \uE90C | `CatanFont.Pirate` | Robber/resources lost |
| Target | \uE916 | `CatanFont.Target` | Times targeted |
| Sum | \uE910 | `CatanFont.Sum` | Total resources |
| Longest Road | \uE915 | `CatanFont.LongestRoad` | Achievement badge |
| Good Roll | \uE914 | `CatanFont.GoodRoll` | Favorable dice result |
| Bad Roll | \uE913 | `CatanFont.BadRoll` | Unfavorable dice result |
| Star | \uE911 | `CatanFont.Star` | Buildable indicator |
| Knight | \uE930 | `CatanFont.Knight` | Knight card |
| Ship | \uE90D | `CatanFont.Ship` | Naval unit |
| Metro | \uE90F | `CatanFont.Metro` | Metropolis |

**Primary Usage - PlayerCtrl.xaml:**

The main consumer is the player statistics panel which displays counts with icons:
```xml
<TextBlock FontFamily="{StaticResource CatanFont}" FontSize="28"
           Text="{x:Bind Glyph, Mode=OneWay}"/>
```

Each player stat (roads played, cities built, score, etc.) uses a glyph from CatanFont paired with a count number.

### Number Tokens (Roll Numbers on Tiles)

The number tokens displayed on resource tiles use a **different font system** than the Catan font glyphs.

**Implementation (from `DesktopApp/Tiles/CatanNumber.xaml`):**

```xml
<!-- Number display uses Segoe UI, NOT Catan font -->
<TextBlock FontFamily="Segoe UI" FontWeight="Bold" Text="{x:Bind Number}"/>

<!-- Probability pips use Segoe Fluent Icons -->
<TextBlock FontFamily="Segoe Fluent Icons" Text="{x:Bind Stars}"/>
```

**Probability Pips (Stars):**

The pips below each number indicate roll probability - more pips = more likely to be rolled:

| Number | Pips | Probability | Color |
|--------|------|-------------|-------|
| 2, 12 | 1 | Lowest | White |
| 3, 11 | 2 | Low | White |
| 4, 10 | 3 | Medium | White |
| 5, 9 | 4 | High | White |
| 6, 8 | 5 | Highest | **Red** |
| 7 | 0 | (Robber) | N/A |

**Visual Design:**
- Background: Blue ellipse (`#FF2F6999`, 75% opacity)
- Number: Bold, centered above pips
- Pips: Row of star characters (`\uE00A`) below number
- Red highlighting: Numbers 6 and 8 use red text/pips to indicate they're most likely

**WebUI SVG Rendering:**

For the SVG board, number tokens will be rendered as:
```xml
<g transform="translate(cx, cy)">
  <!-- Blue circle background -->
  <circle r="32" fill="#2F6999" opacity="0.75" stroke="white" stroke-width="0.5"/>

  <!-- Number -->
  <text y="-5" text-anchor="middle" font-family="sans-serif" font-weight="bold"
        font-size="24" fill="red">8</text>

  <!-- Probability pips -->
  <text y="12" text-anchor="middle" font-size="10" fill="red">•••••</text>
</g>
```

### How Images Are Used

1. **Data-Driven Rendering**: Controls use converters to map enum values to ImageBrush resources
2. **Converter Integration**:
   - `ResourceTypeToImageBrush` - Maps ResourceType enum to tile images
   - Similar converters for HarborType and ResourceCard types
3. **Tile Display Layers**:
   - Outer hex: Border brush (cherry.jpg pattern)
   - Inner hex: Resource tile image
   - Number display: Catan.ttf font
4. **Harbor Display**:
   - Positioned on tile vertices
   - Small resource card preview (67×100px)
   - Rotation based on harbor direction
5. **Resource Cards**:
   - Player hand display
   - Trade dialogs
   - Resource distribution
6. **Stretch Mode**: All tiles use `UniformToFill` to maintain aspect ratio

### Enum to Image Mappings

**ResourceType → Tile Image:**
```csharp
ResourceType.Brick    → "../Assets/Tiles/brick.png"
ResourceType.Wheat    → "../Assets/Tiles/wheat.png"
ResourceType.Wood     → "../Assets/Tiles/wood.png"
ResourceType.Ore      → "../Assets/Tiles/ore.png"
ResourceType.Sheep    → "../Assets/Tiles/sheep.png"
ResourceType.Desert   → "../Assets/Tiles/desert.png"
ResourceType.GoldMine → "../Assets/Tiles/goldMine.png"
ResourceType.Sea      → "../Assets/Tiles/back.jpg"
```

**HarborType → Harbor Image:**
```csharp
HarborType.Brick       → "../Assets/Harbors/2 for 1 brick.png"
HarborType.Ore         → "../Assets/Harbors/2 for 1 ore.png"
HarborType.Sheep       → "../Assets/Harbors/2 for 1 sheep.png"
HarborType.Wheat       → "../Assets/Harbors/2 for 1 wheat.png"
HarborType.Wood        → "../Assets/Harbors/2 for 1 wood.png"
HarborType.ThreeForOne → "../Assets/Harbors/3 for 1.png"
```

**ResourceCard → Card Image:**
```csharp
// Base resources
ResourceCard.Brick    → "../Assets/ResourceCards/brick.png"
ResourceCard.Wheat    → "../Assets/ResourceCards/wheat.png"
ResourceCard.Wood     → "../Assets/ResourceCards/wood.png"
ResourceCard.Ore      → "../Assets/ResourceCards/ore.png"
ResourceCard.Sheep    → "../Assets/ResourceCards/sheep.png"
ResourceCard.GoldMine → "../Assets/ResourceCards/goldMine.png"

// Expansion commodities
ResourceCard.Cloth    → "../Assets/ResourceCards/cloth.png"
ResourceCard.Paper    → "../Assets/ResourceCards/paper.png"
ResourceCard.Coin     → "../Assets/ResourceCards/coin.png"

// Development cards
ResourceCard.Trade        → "../Assets/ResourceCards/trade.png"
ResourceCard.Politics     → "../Assets/ResourceCards/politics.png"
ResourceCard.Science      → "../Assets/ResourceCards/science.png"
ResourceCard.VictoryPoint → "../Assets/ResourceCards/victorypoint.png"

// Special
ResourceCard.Back    → "../Assets/ResourceCards/back.png"
ResourceCard.Robber  → "../Assets/ResourceCards/robber.png"
```

## WebUI Asset Strategy

### Design Goals

1. **Reuse hi-res images** - Same visual quality as Desktop app
2. **Efficient caching** - Download once, reuse everywhere
3. **Fast initial load** - Progressive loading for large assets
4. **SVG pattern rendering** - Define patterns once, reference multiple times

### Architecture

```
WebUI/
├── wwwroot/
│   ├── images/
│   │   ├── tiles/           # Copied from DesktopApp/Assets/Tiles/
│   │   │   ├── brick.png
│   │   │   ├── wheat.png
│   │   │   ├── wood.png
│   │   │   ├── ore.png
│   │   │   ├── sheep.png
│   │   │   ├── desert.png
│   │   │   ├── goldMine.png
│   │   │   └── back.jpg
│   │   ├── harbors/         # Copied from DesktopApp/Assets/Harbors/
│   │   │   ├── 2-for-1-brick.png
│   │   │   ├── 2-for-1-ore.png
│   │   │   ├── 2-for-1-sheep.png
│   │   │   ├── 2-for-1-wheat.png
│   │   │   ├── 2-for-1-wood.png
│   │   │   └── 3-for-1.png
│   │   └── cards/           # Copied from DesktopApp/Assets/ResourceCards/
│   ├── fonts/
│   │   └── Catan.ttf        # Copied from DesktopApp/Assets/Fonts/
│   └── css/
│       └── app.css          # @font-face for Catan font
```

### Custom Font in WebUI

Register the Catan font in CSS:

```css
@font-face {
    font-family: 'Catan';
    src: url('/fonts/Catan.ttf') format('truetype');
    font-weight: normal;
    font-style: normal;
}
```

**Usage in SVG:**
```xml
<text font-family="Catan" font-size="24">8</text>
```

**Usage for player stats, cities, settlements:**
- SVG text elements with `font-family="Catan"`
- CSS classes for HTML elements: `.catan-font { font-family: 'Catan', sans-serif; }`

### Static File Caching

Configure aggressive caching for immutable assets in `Program.cs`:

```csharp
app.UseStaticFiles(new StaticFileOptions
{
    OnPrepareResponse = ctx =>
    {
        // Cache images for 1 year (immutable content)
        if (ctx.File.Name.EndsWith(".png") || ctx.File.Name.EndsWith(".jpg"))
        {
            ctx.Context.Response.Headers.Append(
                "Cache-Control", "public, max-age=31536000, immutable");
        }
    }
});
```

**Cache behavior:**
- First visit: Browser downloads all tile images (~10MB)
- Subsequent visits: Browser uses cached versions (0 network requests)
- Cache invalidation: Change filename or add version query string

### SVG Pattern Rendering

The BoardSvgGenerator creates SVG with `<defs>` containing pattern definitions for each tile type. Each pattern is defined once and referenced by all matching hexes.

**Pattern Definition:**
```xml
<svg>
  <defs>
    <pattern id="tile-brick" patternUnits="objectBoundingBox" width="1" height="1">
      <image href="/images/tiles/brick.png"
             preserveAspectRatio="xMidYMid slice"
             width="100" height="87"/>
    </pattern>
    <pattern id="tile-wheat" ...>
      <image href="/images/tiles/wheat.png" .../>
    </pattern>
    <!-- ... other tile patterns -->
  </defs>

  <!-- Each hex references a pattern by ID -->
  <path d="M..." fill="url(#tile-brick)"/>
  <path d="M..." fill="url(#tile-wheat)"/>
</svg>
```

**Benefits:**
- Pattern defined once in `<defs>`, used N times
- Browser caches image files independently
- Same image data not duplicated in SVG
- Clean separation of tile texture from hex geometry

### Image Sizing for SVG Patterns

For flat-top hexes with size 50px (radius):
- Hex width: 100px (2 × size)
- Hex height: ~87px (√3 × size)

Pattern image dimensions should match or exceed hex dimensions for crisp rendering:
- Minimum: 100×87px
- Recommended: 200×174px (2x for retina displays)
- Current hi-res: Much larger (will be downscaled by browser)

### Resource Type to Pattern ID Mapping

**Tile Patterns:**
```csharp
private string GetTilePatternId(ResourceType resourceType)
{
    return resourceType switch
    {
        ResourceType.Brick => "tile-brick",
        ResourceType.Wheat => "tile-wheat",
        ResourceType.Wood => "tile-wood",
        ResourceType.Ore => "tile-ore",
        ResourceType.Sheep => "tile-sheep",
        ResourceType.Desert => "tile-desert",
        ResourceType.GoldMine => "tile-goldmine",
        ResourceType.Sea => "tile-sea",
        _ => "tile-default"
    };
}
```

**Harbor Patterns:**
```csharp
private string GetHarborPatternId(HarborType harborType)
{
    return harborType switch
    {
        HarborType.Brick => "harbor-brick",
        HarborType.Ore => "harbor-ore",
        HarborType.Sheep => "harbor-sheep",
        HarborType.Wheat => "harbor-wheat",
        HarborType.Wood => "harbor-wood",
        HarborType.ThreeForOne => "harbor-3for1",
        _ => "harbor-default"
    };
}
```

### Harbor Rendering

Harbors are positioned at tile vertices with rotation based on direction:

```xml
<defs>
  <pattern id="harbor-brick" patternUnits="objectBoundingBox" width="1" height="1">
    <image href="/images/harbors/2-for-1-brick.png" width="40" height="60"/>
  </pattern>
</defs>

<!-- Harbor positioned at vertex with rotation -->
<g transform="translate(x, y) rotate(angle)">
  <rect width="40" height="60" fill="url(#harbor-brick)"/>
</g>
```

**Harbor positioning:**
- Calculate vertex position from adjacent tile coordinates
- Rotation angle based on harbor direction (0°, 60°, 120°, etc.)
- Size: 40×60px (scaled from Desktop's 67×100px)

## Implementation Plan

### Phase 1: Copy Assets to WebUI

1. Create directory structure:
   - `WebUI/wwwroot/images/tiles/`
   - `WebUI/wwwroot/images/harbors/`
   - `WebUI/wwwroot/images/cards/`
   - `WebUI/wwwroot/fonts/`
2. Copy tile images from `DesktopApp/Assets/Tiles/`
3. Copy harbor images from `DesktopApp/Assets/Harbors/`
4. Copy Catan font from `DesktopApp/Assets/Fonts/Catan.ttf`
5. Register font in `app.css` with `@font-face`
6. Copy resource card images (for player hand display)

### Phase 2: Update BoardSvgGenerator

1. Add tile pattern definitions to `<defs>` section
2. Map ResourceType enum to pattern IDs
3. Use `fill="url(#pattern-id)"` instead of solid colors
4. Calculate proper pattern dimensions for hex size

### Phase 2b: Add Harbor Rendering

1. Add harbor pattern definitions to `<defs>`
2. Map HarborType enum to harbor pattern IDs
3. Calculate harbor positions at tile vertices
4. Apply rotation based on harbor direction
5. Render harbors as positioned/rotated rectangles with pattern fill

### Phase 3: Configure Caching

1. Add static file middleware with cache headers
2. Set 1-year cache for image files
3. Add `immutable` directive for browser optimization

### Phase 4: Progressive Enhancement

1. Start with colored fills (current implementation)
2. Add pattern definitions for textured tiles
3. Add harbor rendering with images
4. Add resource card display

## Performance Considerations

### Initial Load

| Asset Category | Size | Strategy |
|----------------|------|----------|
| Tile images | ~10MB | Lazy load after critical path |
| Harbor images | ~370KB | Load with tiles |
| Card images | ~97MB | Load on demand when needed |

### Network Optimization

1. **HTTP/2 multiplexing**: Multiple images download in parallel
2. **Browser caching**: Images cached after first download
3. **SVG streaming**: SVG renders progressively as images load

### Memory Usage

- SVG patterns reference external images (not embedded)
- Browser manages image memory independently
- Patterns share same image data across all instances

## Comparison: Desktop vs WebUI

| Aspect | Desktop (XAML) | WebUI (SVG) |
|--------|----------------|-------------|
| Image storage | Embedded resources | Static files in wwwroot |
| Image reference | `{StaticResource key}` | `url(#pattern-id)` |
| Caching | In-process memory | Browser HTTP cache |
| Reuse mechanism | ResourceDictionary | SVG `<defs>` patterns |
| Stretch mode | `UniformToFill` | `preserveAspectRatio="xMidYMid slice"` |

## Future Enhancements

### Image Optimization

Consider creating optimized versions for web:
- WebP format (30% smaller than PNG)
- Multiple resolutions for different device densities
- Compressed versions for slower connections

### CDN Deployment

For production, consider serving images from CDN:
- Global edge caching
- Automatic format negotiation (WebP, AVIF)
- Bandwidth cost reduction

### Sprite Sheet Alternative

For maximum efficiency, could combine all tiles into single sprite sheet:
- One HTTP request for all tiles
- Use SVG `viewBox` to select tile region
- Trade-off: More complex pattern definitions

## File Reference Summary

### Desktop App Files

- `DesktopApp/Themes/ImageResources.xaml` - Image brush definitions
- `DesktopApp/Assets/Tiles/*.png` - Tile texture images
- `DesktopApp/Assets/Harbors/*.png` - Harbor images
- `DesktopApp/Assets/ResourceCards/*.png` - Card images

### WebUI Files (to be created/modified)

- `WebUI/wwwroot/images/tiles/` - Copied tile images
- `WebUI/wwwroot/images/harbors/` - Copied harbor images
- `WebUI/Program.cs` - Static file caching configuration
- `Catan3.GameService/Services/BoardSvgGenerator.cs` - SVG pattern generation

## References

- [MDN: SVG Pattern Element](https://developer.mozilla.org/en-US/docs/Web/SVG/Element/pattern)
- [Static Files in ASP.NET Core](https://docs.microsoft.com/aspnet/core/fundamentals/static-files)
- [HTTP Caching](https://developer.mozilla.org/en-US/docs/Web/HTTP/Caching)
- [WebUI-Design.md](./WebUI-Design.md) - Overall WebUI architecture
