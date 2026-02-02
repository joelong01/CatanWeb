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
| renderMode       |     | themes{}          |     | useAssetPath() |
| colors{}         |     | getAssetPath()    |     | useFontConfig()|
| assets{}         |     | getRenderMode()   |     +----------------+
+------------------+     +-------------------+
                               |
                          localStorage
                         "catan-theme"
```

**Key concept**: `renderMode` in theme.json controls how components render:

- `"image"` (Classic) -- PNG/JPG backgrounds via asset path resolution
- `"font"` (Modern) -- CatanFont glyphs with colors from theme.json `colors` section

## Changes

### 1. NEW: `react-ui/lib/theme/types.ts`

Type definitions mirroring Blazor's `IAssetService`:

- `AssetName` union type (75 string literals matching base `theme.json` keys)
- `RenderMode = 'image' | 'font'`
- `ThemeDefinition { name, displayName, description?, preview, renderMode?, colors?, assets }`
- `ThemeMetadata { name, displayName, description?, preview, renderMode }`
- `FontConfig { glyph: string; color: string; bgColor: string; borderColor: string }`
- `HarborFontConfig extends FontConfig { harborGlyph, hexGlyph?, faIcon? }`

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
  "renderMode": "font",
  "colors": {
    "tiles": {
      "Wheat":    { "glyph": "WheatHex",   "color": "#DAA520", "bgColor": "white", "borderColor": "#B8860B" },
      "Wood":     { "glyph": "WoodHex",    "color": "#2E7D32", "bgColor": "white", "borderColor": "#1B5E20" },
      "Sheep":    { "glyph": "SheepHex",   "color": "#A68B6B", "bgColor": "white", "borderColor": "#8B7355" },
      "Brick":    { "glyph": "BrickHex",   "color": "#BF360C", "bgColor": "white", "borderColor": "#8C2700" },
      "Ore":      { "glyph": "OreHex",     "color": "#546E7A", "bgColor": "white", "borderColor": "#37474F" },
      "Desert":   { "glyph": "DesertHex",  "color": "#8D6E63", "bgColor": "white", "borderColor": "#5D4037" }
    },
    "harbors": {
      "Wheat":       { "hexGlyph": "WheatHex",  "harborGlyph": "WheatHarbor",       "color": "#DAA520" },
      "Wood":        { "hexGlyph": "WoodHex",   "harborGlyph": "WoodHarbor",        "color": "#2E7D32" },
      "Sheep":       { "hexGlyph": "SheepHex",  "harborGlyph": "SheepHarbor",       "color": "#A68B6B" },
      "Brick":       { "hexGlyph": "BrickHex",  "harborGlyph": "BrickHarbor",       "color": "#BF360C" },
      "Ore":         { "hexGlyph": "OreHex",    "harborGlyph": "OreHarbor",         "color": "#546E7A" },
      "ThreeForOne": { "harborGlyph": "ThreeToOneHarbor", "color": "#D4AF37", "bgColor": "white", "faIcon": "coins" }
    }
  },
  "assets": {}
}
```

Glyph names (e.g., `"WheatHex"`) are resolved to `CatanGlyph[name]` at runtime by the store.
Empty `assets` means all image paths fall back to base theme.

### 6. MODIFY: `react-ui/public/themes/classic/theme.json`

Add `"renderMode": "image"` field (explicit default).

### 7. MODIFY: `react-ui/lib/constants/board-assets.ts`

Replace hardcoded `/themes/base/` paths with theme-resolved lookups:

- `getResourceTileImage(type)` calls `themeStore.getAssetPath(RESOURCE_TO_ASSET[type])`
- `getHarborImage(type)` calls `themeStore.getAssetPath(HARBOR_TO_ASSET[type])`
- Keep `NUMBER_PIPS` and `getResourceCardImage()` similarly redirected
- Map tables (`RESOURCE_TO_ASSET`, `HARBOR_TO_ASSET`) replace old path dictionaries

### 8. MODIFY: `react-ui/components/game/tiles/GameTile.tsx`

Add font rendering branch:

- Import `useFontRendering`, `useTileFontConfig` from theme hooks
- When `renderMode === 'font'` and font config exists for the resource:
  - **Outer hex border**: `<div>` with solid `borderColor` from theme (replaces maple.jpg)
  - **Inner hex**: `<div>` with `bgColor` background + SVG `<text>` with CatanFont glyph
    clipped to hex, matching the proven harbor glyph pattern
  - **Number token**: unchanged (renders on top as before)
  - **Gold flip**: image-based fallback until GoldHex glyph exists
- When `renderMode === 'image'`: existing code unchanged

### 9. MODIFY: `react-ui/components/game/board/GameBoard.tsx`

- Remove `HARBOR_FONT_CONFIG` constant (now in theme.json, resolved by store)
- Remove `fontRendering` prop from `GameBoardProps` and `HarborHexContentProps`
- `HarborHexContent` calls `useFontRendering()` and `useHarborFontConfig(harborType)` internally
- Harbor rendering logic stays the same, just reads config from theme instead of local constant
- Update `harborItems` useMemo to remove `fontRendering` dependency (no longer a prop)

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

## Future Work (not in this design)

- Gold tile: user creates gold.svg, adds GoldHex glyph to font, adds to modern theme.json
- Baron/Robber theming: add colors section for robber rendering
- Number token theming: add token colors/styles to theme config
- Additional themes (e.g., black-and-white font variant)
