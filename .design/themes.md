# Theme System Implementation

## Goal

Add a theme system to the React UI with "Classic" (current PNGs) and "Modern" (CatanFont glyphs)
themes, mirroring the Blazor `ClientAssetService` architecture: `theme.json` per theme, sparse
override model, `base` fallback, NavMenu selection, localStorage persistence.

## Architecture

```text
theme.json (per theme)      Zustand themeStore        Components
+------------------+     +-------------------+     +----------------+
| name, displayName|---->| currentTheme      |---->| useRenderMode()|
| parent           |     | themes{}          |     | useAssetPath() |
| renderMode       |     | getAssetPath()    |     | useFontConfig()|
| colors{}         |     | getRenderMode()   |     +----------------+
| assets{}         |     | themeChain()      |
+------------------+     +-------------------+
                               |
                          localStorage
                         "catan-theme"
```

**Key concept**: `renderMode` in theme.json controls how components render:

- `"image"` (Classic) -- PNG/JPG backgrounds via asset path resolution
- `"font"` (Modern) -- CatanFont glyphs with colors from theme.json `colors` section

### Parent chain (sparse override model)

Each theme has an optional `parent` field defining the fallback chain:

```text
modern (font overrides) → classic (empty pass-through) → base (all assets)
```

Resolution walks the chain via a `themeChain()` generator with cycle protection.
`getAssetPath()`, `getTileFontConfig()`, and `getHarborFontConfig()` all walk the chain.

## Design Decisions

### Glyph-as-complete-tile

Font glyphs contain the **full tile design**: outer hex border, inner pattern fill, and center
circle cutout. The rendering code does not add its own border or inner hex scaling -- the glyph
defines the visual completely.

- No `hex-clip-flat` CSS clip-path on font-mode content (the glyph's transparent areas define
  the hex boundary)
- No `scale(0.91)` inner hex (the glyph includes its own border proportions)
- No `bgColor` fill behind the glyph (transparent areas show through naturally)

### Font rendering: square viewBox with stretch

Font glyphs are normalized into a square em-square. To fill the non-square hex rectangle
(2:sqrt(3) aspect ratio), we use:

```tsx
<svg viewBox="0 0 100 100" preserveAspectRatio="none">
  <text x="50" y="50" fontSize="100" fill={color}>{glyph}</text>
</svg>
```

This stretches the glyph from the square em-square into the hex's rectangle. Applied to both
tile hexes and harbor hex backgrounds.

### Harbor rendering (font mode)

Harbors look like **faded versions of the resource tile** with a harbor ship in center:

1. **Hex glyph background**: same glyph as the board tile, rendered at `hexOpacity` (default
   0.6). Makes the harbor visually match its resource type while being distinguishable from
   regular tiles.
2. **Connection line**: thick colored line on the dock-side edge (facing the owning tile).
   Uses the harbor's foreground `color`.
3. **Center circle**: parchment-colored (#f5f0e1) circle with the harbor ship/resource glyph,
   similar to how tiles have a NumberToken in their center.
4. **Owner indication**: when a player owns the harbor, the center circle's border switches to
   the owner's `primary` color with increased stroke width (4px vs 2px). The glyph color stays
   the theme color for readability.

For `ThreeForOne` harbors (no resource hex glyph): solid `bgColor` polygon at `hexOpacity` with
FontAwesome coins icon.

### Image-mode harbor rendering

Unchanged from original: frosted backdrop with blur, dock-side line, parchment circle with
harbor image clipped inside. Owner gradient fills the hex background.

### No hardcoded asset paths

**Every** image/font/background URL must resolve through the theme store. No component may
contain a literal `/themes/base/...` string. This ensures:

- Theme overrides work for all assets (not just tiles and harbors)
- The parent chain (`modern → classic → base`) is respected everywhere
- A future theme can replace maple borders, card backs, resource icons, etc.

**Architecture:**

```text
Component                  board-assets.ts / hooks.ts        themeStore
+-----------------------+  +----------------------------+  +-----------+
| useAssetPath('X')     |->| resolveAsset(AssetName)    |->| themeChain|
| getResourceCardImage()|  | (store.getState() for non- |  | walk      |
| getResourceTileImage()|  |  React contexts)           |  +-----------+
+-----------------------+  +----------------------------+
```

**Two resolution APIs:**

1. **`useAssetPath(asset)`** hook -- for React components (reactive, re-renders on theme
   change). Used in render functions.
2. **`resolveAsset(asset, fallback)`** in board-assets.ts -- for non-hook contexts (e.g.,
   `getResourceTileImage()` called from `useMemo`). Uses `useThemeStore.getState()`.

**Fallback strategy:** The store is initialized with hardcoded inline defaults for base theme
assets so `resolveAsset()` returns valid paths even before `fetch()` completes. This
eliminates the need for separate fallback constants.

**Files that must NOT contain hardcoded `/themes/` paths:**

| File | What to use instead |
|------|-------------------|
| `GameTile.tsx` | `useAssetPath('BackgroundBorderFill')` for maple border |
| `NumberToken.tsx` | Accept `borderBackground` prop; board passes maple via `useAssetPath` |
| `GameBoard.tsx` | `useAssetPath('TileSea')` for water tiles |
| `ActionCluster.tsx` | `useAssetPath('CardBack')` for card back background |
| `MeasurementCluster.tsx` | `useAssetPath('CardSheep')`, etc. for resource images |
| `GameResourcesHeader.tsx` | `useAssetPath('CardWheat')`, `useAssetPath('CardRobber')`, etc. |
| `PlayersPanel.tsx` | Same as GameResourcesHeader |
| `board-assets.ts` | Remove `TILE_FALLBACKS`, `RESOURCE_CARD_IMAGES`, inline fallback constants |

## Changes

### 1. NEW: `react-ui/lib/theme/types.ts`

Type definitions mirroring Blazor's `IAssetService`:

- `AssetName` union type (75 string literals matching base `theme.json` keys)
- `RenderMode = 'image' | 'font'`
- `ThemeDefinition { name, displayName, description?, preview, parent?, renderMode?, colors?, assets }`
- `ThemeMetadata { name, displayName, description?, preview, renderMode }`
- `TileFontConfig { glyph, color, bgColor, borderColor }`
- `HarborFontConfig { harborGlyph, hexGlyph?, color, bgColor?, hexOpacity?, faIcon? }`

### 2. NEW: `react-ui/lib/theme/themeStore.ts`

Zustand store with `persist` middleware (localStorage key: `catan-theme`):

- `initialize()` -- fetches `base`, `classic`, `modern` theme.json files in parallel
- `setTheme(name)` -- switches theme, fires re-renders
- `getAssetPath(AssetName)` -- theme assets -> base fallback (same logic as Blazor)
- `getRenderMode()` -- returns current theme's renderMode
- `getFontConfig(resourceType)` -- returns tile font config from theme.json `colors.tiles`
- `getHarborFontConfig(harborType)` -- returns harbor font config from `colors.harbors`
- Default theme: `'classic'`

### 3. NEW: `react-ui/lib/theme/hooks.ts`

Convenience hooks for components:

- `useRenderMode()` -- subscribes to render mode changes
- `useAssetPath(AssetName)` -- reactive asset path resolution
- `useTileFontConfig(resourceType)` -- tile glyph + colors from theme
- `useHarborFontConfig(harborType)` -- harbor glyph + colors from theme
- `useFontRendering()` -- shorthand for `useRenderMode() === 'font'`

### 4. NEW: `react-ui/lib/theme/index.ts`

Barrel exports for all theme types, store, and hooks.

### 5. NEW: `react-ui/public/themes/modern/theme.json`

```json
{
  "name": "modern",
  "displayName": "Modern",
  "description": "Clean CatanFont glyph rendering with themed colors",
  "preview": "/themes/base/tiles/wheat.png",
  "parent": "classic",
  "renderMode": "font",
  "colors": {
    "tiles": {
      "Wheat":    { "glyph": "WheatHex",   "color": "#DAA520", "bgColor": "white", "borderColor": "#B8860B" },
      "Wood":     { "glyph": "WoodHex",    "color": "#2E7D32", "bgColor": "white", "borderColor": "#1B5E20" },
      "Sheep":    { "glyph": "SheepHex",   "color": "#A68B6B", "bgColor": "white", "borderColor": "#8B7355" },
      "Brick":    { "glyph": "BrickHex",   "color": "#BF360C", "bgColor": "white", "borderColor": "#8C2700" },
      "Ore":      { "glyph": "OreHex",     "color": "#4682B4", "bgColor": "white", "borderColor": "#31628A" },
      "Desert":   { "glyph": "DesertHex",  "color": "#8D6E63", "bgColor": "white", "borderColor": "#5D4037" },
      "GoldMine": { "glyph": "GoldHex",    "color": "#D4AF37", "bgColor": "white", "borderColor": "#B8860B" }
    },
    "harbors": {
      "Wheat":       { "hexGlyph": "WheatHex",  "harborGlyph": "WheatHarbor",       "color": "#DAA520", "hexOpacity": 0.6 },
      "Wood":        { "hexGlyph": "WoodHex",   "harborGlyph": "WoodHarbor",        "color": "#2E7D32", "hexOpacity": 0.6 },
      "Sheep":       { "hexGlyph": "SheepHex",  "harborGlyph": "SheepHarbor",       "color": "#A68B6B", "hexOpacity": 0.6 },
      "Brick":       { "hexGlyph": "BrickHex",  "harborGlyph": "BrickHarbor",       "color": "#BF360C", "hexOpacity": 0.6 },
      "Ore":         { "hexGlyph": "OreHex",    "harborGlyph": "OreHarbor",         "color": "#4682B4", "hexOpacity": 0.6 },
      "ThreeForOne": { "harborGlyph": "ThreeToOneHarbor", "color": "#D4AF37", "bgColor": "white", "hexOpacity": 0.6, "faIcon": "coins" }
    }
  },
  "assets": {}
}
```

Glyph names (e.g., `"WheatHex"`) are resolved to `CatanGlyph[name]` at runtime by the store.
Empty `assets` means all image paths fall back to base theme via the parent chain.

### 6. MODIFY: `react-ui/public/themes/classic/theme.json`

Add `"renderMode": "image"` field (explicit default).

### 7. MODIFY: `react-ui/lib/constants/board-assets.ts`

Replace hardcoded `/themes/base/` paths with theme-resolved lookups:

- `getResourceTileImage(type)` calls `themeStore.getAssetPath(RESOURCE_TO_ASSET[type])`
- `getHarborImage(type)` calls `themeStore.getAssetPath(HARBOR_TO_ASSET[type])`
- Keep `NUMBER_PIPS` and `getResourceCardImage()` similarly redirected
- Map tables (`RESOURCE_TO_ASSET`, `HARBOR_TO_ASSET`) replace old path dictionaries

### 8. MODIFY: `react-ui/components/game/tiles/GameTile.tsx`

Add font rendering branch using glyph-as-complete-tile approach:

- Import `useFontRendering`, `useTileFontConfig` from theme hooks
- Resolve both base resource config and GoldMine config (for temporarily-gold tiles)
- When `renderMode === 'font'` and font config exists:
  - **No outer border div** (glyph includes its own hex border)
  - **No inner hex scaling** (glyph fills the full hex naturally)
  - **SVG text** with `viewBox="0 0 100 100"`, `preserveAspectRatio="none"`, `fontSize="100"`
  - **Number token**: unchanged (renders on top as before)
  - **Gold tiles**: uses GoldHex glyph (no image fallback needed)
- When `renderMode === 'image'`: existing flip animation code unchanged

### 9. MODIFY: `react-ui/components/game/board/GameBoard.tsx`

Harbor hex rendering redesigned for font mode (see Design Decisions above):

- `HarborHexContent` calls `useFontRendering()` and `useHarborFontConfig(harborType)` internally
- Font mode: hex glyph at `hexOpacity`, connection line on dock edge, center circle with ship
  glyph, owner's primary color as circle border
- Image mode: frosted backdrop with dock line and harbor image circle (unchanged)
- Removed: `HARBOR_FONT_CONFIG` constant, `fontRendering` prop, `SIDE_TO_OPPOSITE_VERTICES`,
  `WATER_COLORS`, dock/water triangle geometry (all superseded by glyph-as-complete-tile)

### 10. MODIFY: `react-ui/components/layout/NavMenu.tsx`

Add `ThemeSection` component following existing `LayoutSection` pattern:

- Lists available themes from `themeStore.getAvailableThemes()`
- Shows active theme indicator
- `setTheme()` on click, persists automatically via Zustand persist middleware
- Uses `faPalette` icon
- Inserted after Layout section in Game page menu

### 11. MODIFY: `react-ui/app/controls-test/page.tsx`

- Remove `fontRendering` prop from `<GameBoard>` (now theme-driven)

### 12. MODIFY: `react-ui/app/layout.tsx` (or providers)

- Call `themeStore.initialize()` on app mount to fetch theme.json files
- Provide hardcoded base fallbacks until fetch completes (no blank flash)

## Files Modified

| File | Action | Purpose |
|------|--------|---------|
| `react-ui/lib/theme/types.ts` | NEW | AssetName, RenderMode, ThemeDefinition, FontConfig types |
| `react-ui/lib/theme/themeStore.ts` | NEW | Zustand store: theme state, asset resolution, font configs |
| `react-ui/lib/theme/hooks.ts` | NEW | useRenderMode, useAssetPath, useTileFontConfig, etc. |
| `react-ui/lib/theme/index.ts` | NEW | Barrel exports |
| `react-ui/public/themes/modern/theme.json` | NEW | Modern theme: renderMode=font, colors, empty assets |
| `react-ui/public/themes/classic/theme.json` | MODIFY | Add renderMode=image |
| `react-ui/lib/constants/board-assets.ts` | MODIFY | Route through themeStore.getAssetPath() |
| `react-ui/components/game/tiles/GameTile.tsx` | MODIFY | Add font glyph rendering, themed border |
| `react-ui/components/game/board/GameBoard.tsx` | MODIFY | Remove embedded config, use theme hooks |
| `react-ui/components/layout/NavMenu.tsx` | MODIFY | Add ThemeSection |
| `react-ui/app/controls-test/page.tsx` | MODIFY | Remove fontRendering prop |
| `react-ui/app/layout.tsx` | MODIFY | Initialize theme store |

## Implementation Phases

1. **Foundation**: types + store + hooks + theme.json files + initialization
2. **Asset resolution**: Migrate board-assets.ts to use themeStore; verify Classic is identical
3. **Theme UI**: NavMenu ThemeSection; verify switching + persistence
4. **Tile font rendering**: GameTile font branch with themed border colors
5. **Harbor cleanup**: Move harbor config to theme.json, remove fontRendering prop

## Verification

1. `pwsh ./catan.ps1 build` passes
2. Classic theme: pixel-identical to current rendering (all PNGs, maple borders)
3. Modern theme: tiles show CatanFont glyphs with colored borders, harbors show font glyphs
4. Theme switching via NavMenu works instantly (no page reload)
5. localStorage persistence: reload preserves selected theme
6. Controls-test page works with both themes
7. ThreeForOne harbor shows gold coins icon on white background in Modern

## Diagnostic Script: `.scripts/themes.ps1`

A PowerShell 7 script that validates theme configuration and reports status.

### Usage

```powershell
pwsh .scripts/themes.ps1 doctor -Table       # Pretty-printed table to stdout
pwsh .scripts/themes.ps1 doctor -Json        # JSON to stdout
pwsh .scripts/themes.ps1 doctor -HashTable   # Returns PS HashTable object
```

### What `doctor` checks

For each theme directory under `react-ui/public/themes/`:

1. **theme.json exists** and is valid JSON
2. **Required fields** present: `name`, `displayName`, `assets`
3. **Asset file resolution**: for every asset in `assets{}`, verify the file exists on disk
4. **Base completeness**: base theme has all 75 expected asset keys
5. **Sparse override validity**: non-base theme asset paths point to files that exist
6. **Glyph name resolution** (font themes): every glyph name in `colors.tiles` and
   `colors.harbors` maps to a valid key in `catanGlyphs.ts`
7. **Render mode** is present and is `"image"` or `"font"`

### Output format

Internally builds a PowerShell hashtable per theme:

```powershell
@{
    Theme       = 'modern'
    DisplayName = 'Modern'
    RenderMode  = 'font'
    AssetCount  = 0          # overrides (sparse)
    BaseAssets  = 75         # inherited from base
    MissingFiles = @()       # asset paths that don't resolve to files
    InvalidGlyphs = @()      # glyph names not found in catanGlyphs.ts
    Status      = 'OK'       # or 'ERROR' with details
}
```

- `-Table`: formats the hashtable array with `Format-Table` for readable stdout
- `-Json`: converts with `ConvertTo-Json -Depth 4` and writes to stdout
- `-HashTable`: returns the raw hashtable array (for pipeline use)

## Modern Theme Color Palette

Each resource needs a single emblematic color that is instantly recognizable and clearly
distinguishable from every other resource on the board.

### Design constraints

- Glyphs are monochrome (single `color` fill on white `bgColor`), so hue alone must carry
  the identity — no textures or gradients to help.
- Wheat and Gold are the hardest pair: both traditionally "golden." They must occupy
  different parts of the yellow spectrum.
- Sheep and Wood are both "green" in Catan imagery (pastures vs forests). They must differ
  in lightness and warmth.
- Colors must have enough saturation to render glyph detail clearly against white.

### Chosen palette

| Resource   | Color     | Hue         | Rationale |
|------------|-----------|-------------|-----------|
| Brick      | `#BF360C` | Red         | Terracotta/fired clay — unmistakable |
| Wheat      | `#CC7A00` | Warm amber  | Harvest wheat fields — orange-shifted to separate from gold |
| Gold       | `#FFD700` | Bright gold | Precious metal — brightest yellow, max separation from wheat |
| TempGold   | `#FFD700` | Bright gold | Same as GoldMine for visual consistency |
| Sheep      | `#A68B6B` | Warm taupe  | Natural wool/fleece — earthy warmth, distinct from wood's green |
| Wood       | `#228B22` | Forest green| Dense forest — darker/cooler than sheep's pastoral green |
| Ore        | `#4682B4` | Steel blue  | Metal/rock — vivid steel blue, pops against white |
| Desert     | `#D97706` | Sun orange  | Blazing desert sun — hot and barren |
| 3:1 Harbor | `#D4AF37` | Old gold    | Trade/commerce — metallic but muted vs tile gold |

### Border colors

Each tile's `borderColor` is a darker shade of the primary `color`, used for the glyph's
built-in hex border. Typically 20-30% darker:

| Resource | color     | borderColor |
|----------|-----------|-------------|
| Brick    | `#BF360C` | `#8C2700`   |
| Wheat    | `#CC7A00` | `#995C00`   |
| Gold     | `#FFD700` | `#B8960B`   |
| TempGold | `#FFD700` | `#B8960B`   |
| Sheep    | `#A68B6B` | `#7D6850`   |
| Wood     | `#228B22` | `#1B5E20`   |
| Ore      | `#4682B4` | `#31628A`   |
| Desert   | `#D97706` | `#9A5504`   |

### Hue wheel spacing

```text
        Red (#BF360C) Brick
       /
  Amber (#CC7A00) Wheat
     |
  Yellow (#FFD700) Gold
     |
  Taupe (#A68B6B) Sheep
     |
  Dk Green (#228B22) Wood
       \
  Steel Blue (#4682B4) Ore
       |
  Sun Orange (#D97706) Desert
```

Adjacent pairs have sufficient hue and/or lightness separation to be distinguishable
at a glance, even at small tile sizes.

## Future Work (not in this design)

- Baron/Robber theming: add colors section for robber rendering
- Number token theming: add token colors/styles to theme config
- Additional themes (e.g., black-and-white font variant)
