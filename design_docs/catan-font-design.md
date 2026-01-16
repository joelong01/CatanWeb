# Catan Font Design Document

## Purpose

Define all glyphs needed in the Catan.ttf font for consistent, fast rendering across Desktop and WebUI.

## Current Glyphs (from DesktopApp/Layout/CatanFont.cs)

| Glyph Name | Unicode | Description |
|------------|---------|-------------|
| City | \uE900 | City building icon |
| Deserter | \uE901 | C&K expansion |
| Diplomat | \uE902 | C&K expansion |
| Gate | \uE903 | C&K expansion |
| Politics | \uE904 | C&K expansion |
| Spy | \uE905 | C&K expansion |
| Inventor | \uE906 | C&K expansion |
| Laurel | \uE907 | Score/victory wreath |
| Merchant | \uE908 | C&K expansion |
| Road | \uE909 | Road building icon |
| NoEntitlement | \uE90A | No build indicator |
| Science | \uE90B | C&K expansion |
| Pirate | \uE90C | Pirate/robber icon |
| Ship | \uE90D | Seafarers ship |
| Soldier | \uE90E | Knight/soldier card |
| Metro | \uE90F | Metropolis (C&K) |
| Sum | \uE910 | Total/sum indicator |
| Star | \uE911 | Star indicator |
| BadRoll | \uE913 | Bad roll indicator |
| GoodRoll | \uE914 | Good roll indicator |
| LongestRoad | \uE915 | Longest road icon |
| Target | \uE916 | Target/crosshair |
| SolidShield | \uE925 | Solid shield shape |
| Settlement | \uE926 | Settlement building |
| FancyShield | \uE927 | Decorative shield |
| Knight | \uE930 | Knight piece (C&K) |

## Proposed New Glyphs

**Note:** Unicode values are placeholders. Actual assignments will be determined when glyphs are added to the font. Update `DesktopApp/Layout/CatanFont.cs` and `WebUI/Components/Players/PlayerTile.razor` (CatanGlyph class) with final values.

### Resource Tiles

**Decision:** Use existing SVGs from svg-tiles theme. Convert to font glyphs.
Source: `WebUI/wwwroot/themes/svg-tiles/`

| Glyph Name | Source SVG |
|------------|------------|
| TileWheat | wheat.svg |
| TileWood | wood.svg |
| TileSheep | sheep.svg |
| TileBrick | brick.svg |
| TileOre | ore.svg |
| TileDesert | desert.svg |
| TileGold | gold.svg |
| TileSea | sea.svg |

### Catan Numbers (with pips)

Each number includes pip dots as a single glyph, matching physical tokens.

| Glyph Name | Description |
|------------|-------------|
| Number2 | "2" with 1 pip |
| Number3 | "3" with 2 pips |
| Number4 | "4" with 3 pips |
| Number5 | "5" with 4 pips |
| Number6 | "6" with 5 pips |
| Number8 | "8" with 5 pips |
| Number9 | "9" with 4 pips |
| Number10 | "10" with 3 pips |
| Number11 | "11" with 2 pips |
| Number12 | "12" with 1 pip |

### Harbors

**Decision:** Single glyph per harbor type, rotate via CSS `transform: rotate()` to face correct direction. Water rendered separately.

| Glyph Name | Description |
|------------|-------------|
| Harbor31 | Generic 3:1 harbor icon |
| Harbor21Wheat | 2:1 wheat harbor icon |
| Harbor21Wood | 2:1 wood harbor icon |
| Harbor21Sheep | 2:1 sheep harbor icon |
| Harbor21Brick | 2:1 brick harbor icon |
| Harbor21Ore | 2:1 ore harbor icon |

### Resource Cards

Convert existing card SVGs to font glyphs.

| Glyph Name | Description |
|------------|-------------|
| CardWheat | Wheat sheaf icon |
| CardWood | Wood/lumber icon |
| CardSheep | Sheep icon |
| CardBrick | Brick icon |
| CardOre | Ore/rock icon |

## Design Decisions

### Buildings: Keep as Circle + Glyph (No Change)

**Rationale:** The current approach of rendering a circle with the building glyph inside is correct because:

1. Circle color indicates player ownership
2. Circle can have visual states (highlighted, dimmed, hover)
3. Building transitions (settlement → city) only change inner content
4. Flexible for future building types

### Resource Tiles: Use Existing SVGs

**Decision:** Convert existing SVGs from `svg-tiles` theme to font glyphs.
**Note:** This approach means themes would need their own font file. Acceptable for now.

### Numbers: Include Pips

**Decision:** Number + pips as single glyph (matches physical Catan tokens).

### Harbors: Single Glyph + CSS Rotation

**Decision:** One glyph per harbor type (6 total). Rotate with CSS to face correct hex edge. Water background rendered separately - we'll evaluate if this causes issues.

## Critical Design Principle: Monochromatic Glyphs

**All font glyphs MUST be single-color (monochromatic).**

- Glyphs are shapes only - no color baked in
- Color applied via CSS `color` property at render time
- Player colors, highlights, states all controlled by CSS
- Multi-color effects achieved by:
  1. Layering multiple glyphs with different CSS colors
  2. CSS gradients on text
  3. Keeping truly multi-color assets as SVG (not in font)

This enables theming and dynamic coloring without regenerating the font.

## Implementation Notes

1. Font format: TTF (TrueType) for broad compatibility
2. Unicode range: Private Use Area (E900-E9FF)
3. Tools: FontForge, Glyphs, or IcoMoon for SVG-to-font conversion
4. Source SVGs should be stored in repo for regeneration
5. All source SVGs must be converted to single-color (black) before font import
