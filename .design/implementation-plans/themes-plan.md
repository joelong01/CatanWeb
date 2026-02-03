# Theme System - Implementation Plan

**Design doc:** [`.design/themes.md`](../../.design/themes.md)

## Status Key

| Symbol | Meaning |
|--------|---------|
| `[ ]`  | Pending |
| `[~]`  | In progress |
| `[x]`  | Done |

## Phase 1: Foundation (types, store, hooks, theme.json files)

### Step 1.1 - `[x]` Create type definitions

**File:** `react-ui/lib/theme/types.ts` (NEW)

- Define `AssetName` union type from the 75 keys in `base/theme.json`
- Define `RenderMode = 'image' | 'font'`
- Define `ThemeDefinition`: `{ name, displayName, description?, preview, renderMode?,
  colors?, assets }`
- Define `ThemeMetadata`: `{ name, displayName, description?, preview, renderMode }`
- Define `FontConfig`: `{ glyph: string; color: string; bgColor: string;
  borderColor: string }`
- Define `HarborFontConfig`: extends FontConfig with `{ harborGlyph, hexGlyph?,
  faIcon? }`
- Define `TileColors` and `HarborColors` map types for the `colors` section

### Step 1.2 - `[x]` Create Zustand theme store

**File:** `react-ui/lib/theme/themeStore.ts` (NEW)

- Create Zustand store with `persist` middleware, localStorage key `"catan-theme"`
- State: `currentTheme: string`, `themes: Record<string, ThemeDefinition>`,
  `initialized: boolean`
- `initialize()`: fetch `base`, `classic`, `modern` theme.json in parallel via
  `Promise.all(fetch(...))`. Set `initialized = true` when done. Provide inline
  fallback data so the app works before fetch completes (no blank flash).
- `setTheme(name: string)`: validate name exists in `themes`, update
  `currentTheme`, persist
- `getAssetPath(asset: AssetName)`: look up in current theme's `assets` first,
  fall back to `base` theme's `assets`. Return the resolved path string.
- `getRenderMode()`: return current theme's `renderMode` (default `'image'`)
- `getFontConfig(resourceType: string)`: return tile font config from
  `themes[currentTheme].colors?.tiles?.[resourceType]`, resolved with glyph
  string from `CatanGlyph[glyphName]`
- `getHarborFontConfig(harborType: string)`: same pattern from
  `colors.harbors[harborType]`, resolve glyph names and faIcon
- `getAvailableThemes()`: return array of `ThemeMetadata` for all loaded themes

### Step 1.3 - `[x]` Create React hooks

**File:** `react-ui/lib/theme/hooks.ts` (NEW)

- `useThemeInitialized()`: subscribes to `initialized` flag
- `useRenderMode()`: subscribes to `getRenderMode()` via selector
- `useAssetPath(asset: AssetName)`: reactive wrapper around `getAssetPath()`
- `useTileFontConfig(resourceType: string)`: reactive wrapper around
  `getFontConfig()`
- `useHarborFontConfig(harborType: string)`: reactive wrapper around
  `getHarborFontConfig()`
- `useFontRendering()`: shorthand returning `useRenderMode() === 'font'`
- `useAvailableThemes()`: subscribes to theme list
- `useCurrentThemeName()`: subscribes to `currentTheme`

### Step 1.4 - `[x]` Create barrel export

**File:** `react-ui/lib/theme/index.ts` (NEW)

- Re-export all types, store, and hooks from their respective files

### Step 1.5 - `[x]` Create modern theme.json

**File:** `react-ui/public/themes/modern/theme.json` (NEW)

- Copy the JSON from design doc section 5: `renderMode: "font"`, `colors.tiles`
  with 6 resource entries, `colors.harbors` with 6 harbor entries, empty `assets`
- Glyph names are strings (e.g., `"WheatHex"`) resolved at runtime

### Step 1.6 - `[x]` Update classic theme.json

**File:** `react-ui/public/themes/classic/theme.json` (MODIFY)

- Add `"renderMode": "image"` to existing JSON

### Step 1.7 - `[x]` Initialize store in app layout

**File:** `react-ui/app/layout.tsx` (MODIFY)

- Import `useThemeStore` from `@/lib/theme`
- Add a `ThemeInitializer` client component (or `useEffect` in existing client
  wrapper) that calls `themeStore.getState().initialize()` on mount
- No visual change; store loads theme.json files in background

### Step 1.8 - `[x]` Build and verify

- Run `pwsh ./catan.ps1 build` -- must pass with no errors
- Verify app still renders identically (Classic is default, all PNG paths resolve
  through base fallback)

## Phase 2: Asset resolution (board-assets.ts migration)

### Step 2.1 - `[x]` Migrate board-assets.ts to use themeStore

**File:** `react-ui/lib/constants/board-assets.ts` (MODIFY)

- Create mapping tables:
  - `RESOURCE_TO_ASSET: Record<string, AssetName>` mapping resource type strings
    to AssetName keys (e.g., `'Brick' -> 'TileBrick'`)
  - `HARBOR_TO_ASSET: Record<string, AssetName>` mapping harbor type strings to
    AssetName keys (e.g., `'Brick' -> 'HarborBrick'`)
  - `CARD_TO_ASSET: Record<string, AssetName>` for resource card images
- Rewrite `getResourceTileImage(type)`: look up `RESOURCE_TO_ASSET[type]`, call
  `themeStore.getState().getAssetPath(assetName)`. Fall back to current hardcoded
  path if store not initialized.
- Rewrite `getHarborImage(type)`: same pattern with `HARBOR_TO_ASSET`
- Rewrite `getResourceCardImage(type)`: same pattern with `CARD_TO_ASSET`
- Keep `NUMBER_PIPS` unchanged (not theme-dependent)
- Keep `RESOURCE_CARD_IMAGES` as a computed getter or lazy object that calls
  through `getAssetPath()`

### Step 2.2 - `[x]` Build and verify Classic is identical

- Run `pwsh ./catan.ps1 build`
- Verify controls-test page renders tiles and harbors identically to current
  (all paths still resolve to `/themes/base/...` because Classic has empty assets
  and base is the fallback)

## Phase 3: Theme UI (NavMenu theme switcher)

### Step 3.1 - `[x]` Add ThemeSection to NavMenu

**File:** `react-ui/components/layout/NavMenu.tsx` (MODIFY)

- Import `useAvailableThemes`, `useCurrentThemeName`, `useThemeStore` from
  `@/lib/theme`
- Import `faPalette` from FontAwesome
- Create `ThemeSection` component (same pattern as existing `LayoutSection`):
  - Header: palette icon + "Theme"
  - List items: one per available theme, showing `displayName`
  - Active theme gets a visual indicator (checkmark or highlight)
  - Click calls `themeStore.getState().setTheme(name)`
- Insert `ThemeSection` after the Layout section in the Game page menu
  (approximately after line 270 in current file)

### Step 3.2 - `[x]` Build and verify theme switching

- Run `pwsh ./catan.ps1 build`
- Verify NavMenu shows Theme section with Classic and Modern options
- Verify clicking switches `currentTheme` in store (inspect localStorage
  `catan-theme`)
- Verify page reload preserves selected theme
- At this point Modern looks identical to Classic (no font rendering yet wired up)

## Phase 4: Tile font rendering (GameTile Modern branch)

### Step 4.1 - `[x]` Add font rendering branch to GameTile

**File:** `react-ui/components/game/tiles/GameTile.tsx` (MODIFY)

- Import `useFontRendering`, `useTileFontConfig` from `@/lib/theme`
- At top of component, call `const fontRendering = useFontRendering()` and
  `const fontConfig = useTileFontConfig(tile.resourceTileType)`
- Add conditional rendering branch when `fontRendering && fontConfig`:
  - **Outer hex**: replace maple.jpg `background-image` with solid
    `fontConfig.borderColor` background
  - **Inner hex**: replace PNG resource image with SVG containing:
    - `<polygon>` fill with `fontConfig.bgColor`
    - `<text>` with CatanFont glyph (`fontConfig.glyph`) in `fontConfig.color`,
      `fontSize=150`, centered, clipped to hex shape (same pattern as harbor hex
      glyph rendering in GameBoard.tsx)
  - **Number token**: unchanged, renders on top
  - **Gold flip**: keep image-based rendering (no GoldHex glyph yet)
- When `!fontRendering` or `!fontConfig`: existing image rendering unchanged

### Step 4.2 - `[x]` Build and verify tiles

- Run `pwsh ./catan.ps1 build`
- Switch to Modern theme in NavMenu
- Verify: 6 resource tiles show CatanFont hex glyphs with colored borders
  (Wheat=gold, Wood=green, Sheep=tan, Brick=red, Ore=grey, Desert=brown)
- Verify: number tokens still show correctly on top of glyphs
- Verify: gold flip animation still works (falls back to image)
- Switch to Classic, verify tiles show PNGs with maple borders (unchanged)

## Phase 5: Harbor cleanup (move config to theme, remove fontRendering prop)

### Step 5.1 - `[x]` Remove HARBOR_FONT_CONFIG from GameBoard

**File:** `react-ui/components/game/board/GameBoard.tsx` (MODIFY)

- Remove `HARBOR_FONT_CONFIG` constant (lines ~156-163)
- Remove `fontRendering` from `GameBoardProps` interface and `HarborHexContentProps`
- In `HarborHexContent`:
  - Import and call `useFontRendering()` and `useHarborFontConfig(harborType)`
    from `@/lib/theme`
  - Replace references to `fontConfig` (from old local constant lookup) with the
    hook return value
  - The rendering logic stays the same -- hex glyph background, harbor glyph in
    circle, dock edge, FA coins for ThreeForOne
- In `harborItems` useMemo:
  - Remove `fontRendering` from the dependency array
  - Remove `fontRendering` prop from `<HarborHexContent>` JSX

### Step 5.2 - `[x]` Remove fontRendering prop from controls-test

**File:** `react-ui/app/controls-test/page.tsx` (MODIFY)

- Remove `fontRendering={true}` from `<GameBoard>` (line ~2159)
- Harbor rendering is now fully driven by the active theme's `renderMode`

### Step 5.3 - `[x]` Build and final verification

- Run `pwsh ./catan.ps1 build` -- must pass
- **Classic theme**:
  - Tiles: PNG images with maple borders (identical to pre-theme rendering)
  - Harbors: PNG harbor icons (identical to pre-theme rendering)
- **Modern theme**:
  - Tiles: CatanFont hex glyphs with colored borders
  - Harbors: CatanFont glyphs with colored hex backgrounds, dock edge highlights,
    ThreeForOne with gold coins on white
- Theme switching via NavMenu is instant (no page reload)
- localStorage `catan-theme` persists across page reloads
- Controls-test page works with both themes

## Phase 6: Diagnostic script

### Step 6.1 - `[x]` Create themes.ps1 script

**File:** `.scripts/themes.ps1` (NEW)

- Implement `doctor` command with `-Table`, `-Json`, `-HashTable` output formats
- Per the design doc, checks: theme.json validity, required fields, asset file
  resolution, base completeness (75 keys), sparse override validity, glyph name
  resolution against catanGlyphs.ts, renderMode presence
- Output format: hashtable per theme with Theme, DisplayName, RenderMode,
  AssetCount, BaseAssets, MissingFiles, InvalidGlyphs, Status fields

### Step 6.2 - `[x]` Verify script

- Run `pwsh .scripts/themes.ps1 doctor -Table` and confirm all themes show
  `Status = 'OK'`

## Phase 7: Eliminate all hardcoded asset paths

**Goal:** Zero literal `/themes/base/` strings in any source file (except test
fixtures). Every asset resolves through the theme store so theme overrides work
globally.

### Audit results

49 hardcoded `/themes/base/` references across 9 files:

| File | Hardcoded refs | What's hardcoded |
|------|---------------|-----------------|
| `board-assets.ts` | 18 | `TILE_FALLBACKS`, `SEA_FALLBACK`, `CARD_FALLBACK`, `RESOURCE_CARD_IMAGES` |
| `GameResourcesHeader.tsx` | 8 | Resource card images, robber, card back |
| `PlayersPanel.tsx` | 8 | Resource card images, robber, card back |
| `MeasurementCluster.tsx` | 5 | Resource card images |
| `GameTile.tsx` | 1 | `maple.jpg` outer hex border |
| `NumberToken.tsx` | 1 | `maple.jpg` default border |
| `GameBoard.tsx` | 1 | `back.jpg` sea tile |
| `ActionCluster.tsx` | 1 | `back.png` card back |
| `expansion-game.ts` | 14 | Test fixture data (acceptable) |

### Step 7.1 - `[ ]` Remove fallback constants from board-assets.ts

**File:** `react-ui/lib/constants/board-assets.ts` (MODIFY)

- Delete `TILE_FALLBACKS`, `SEA_FALLBACK`, `CARD_FALLBACK` constants
- Delete deprecated `RESOURCE_CARD_IMAGES` export
- `resolveAsset()` should use the store's `getAssetPath()` which already walks the
  parent chain. For pre-initialization, the store's inline defaults provide base
  paths. Return `''` (not a hardcoded path) when the store has no match.
- Verify existing callers: `getResourceTileImage()`, `getHarborImage()`,
  `getResourceCardImage()` all work without fallback constants

### Step 7.2 - `[ ]` Migrate GameTile.tsx and NumberToken.tsx

**File:** `react-ui/components/game/tiles/GameTile.tsx` (MODIFY)

- Replace `'url(/themes/base/backgrounds/maple.jpg)'` with theme-resolved path
- Call `useAssetPath('BackgroundBorderFill')` at top of component
- Use the resolved path in the outer hex border `backgroundImage` style

**File:** `react-ui/components/game/tiles/NumberToken.tsx` (MODIFY)

- Replace hardcoded `'url(/themes/base/backgrounds/maple.jpg) center/cover'` default
  in the `borderBackground` prop fallback
- Option A: Make `borderBackground` required (callers always pass it)
- Option B: Import `useAssetPath` and resolve internally
- Option A is cleaner since NumberToken is used in both board (maple) and RollRing
  (player gradient) contexts -- the caller decides the border, not the component

### Step 7.3 - `[ ]` Migrate GameBoard.tsx sea tile

**File:** `react-ui/components/game/board/GameBoard.tsx` (MODIFY)

- Replace literal `"/themes/base/tiles/back.jpg"` (water hex imageUrl) with
  `useAssetPath('TileSea')` or `useAssetPath('BackgroundWater')`
- Resolve at component level, pass into the water hex rendering

### Step 7.4 - `[ ]` Migrate GameResourcesHeader.tsx and PlayersPanel.tsx

**File:** `react-ui/components/game/panels/GameResourcesHeader.tsx` (MODIFY)
**File:** `react-ui/components/game/panels/PlayersPanel.tsx` (MODIFY)

Both files have identical `RESOURCE_CARD_CONFIG` arrays with hardcoded image paths.

- Replace the static config with a hook or helper that resolves asset paths:
  - `useAssetPath('CardWheat')`, `useAssetPath('CardRobber')`, etc.
- The card back (`/themes/base/resources/back.png`) should use
  `useAssetPath('CardBack')`
- Extract shared config to avoid duplication between the two files

### Step 7.5 - `[ ]` Migrate MeasurementCluster.tsx

**File:** `react-ui/components/game/controls/MeasurementCluster.tsx` (MODIFY)

- Replace hardcoded resource image paths with `useAssetPath('CardSheep')`, etc.
- 5 resource images need migration

### Step 7.6 - `[ ]` Migrate ActionCluster.tsx

**File:** `react-ui/components/game/controls/ActionCluster.tsx` (MODIFY)

- Replace hardcoded `/themes/base/resources/back.png` with
  `useAssetPath('CardBack')`

### Step 7.7 - `[ ]` Fix useAssetPath hook to walk parent chain

**File:** `react-ui/lib/theme/hooks.ts` (MODIFY)

- Current implementation: `themes[currentTheme]?.assets[asset] ?? themes.base?.assets[asset]`
- This skips intermediate parents (e.g., `modern → classic → base` would skip
  classic if it had an override)
- Fix: call `store.getAssetPath(asset)` which already uses `themeChain()` with
  proper chain walking, or replicate the chain logic in the hook

### Step 7.8 - `[ ]` Build and verify

- Run `pwsh ./catan.ps1 build` -- must pass
- Grep for remaining `/themes/base/` in non-test source files -- should be zero
- Verify Classic theme renders identically (all paths still resolve to base)
- Verify Modern theme: all UI elements render (robber, card backs, resource icons,
  maple borders, sea tiles)
- Verify theme switching works for all panels (not just board tiles)

## Files Modified Summary

| File | Action | Phase |
|------|--------|-------|
| `react-ui/lib/theme/types.ts` | NEW | 1 |
| `react-ui/lib/theme/themeStore.ts` | NEW | 1 |
| `react-ui/lib/theme/hooks.ts` | NEW | 1 |
| `react-ui/lib/theme/index.ts` | NEW | 1 |
| `react-ui/public/themes/modern/theme.json` | NEW | 1 |
| `react-ui/public/themes/classic/theme.json` | MODIFY | 1 |
| `react-ui/components/providers/ThemeInitializer.tsx` | NEW | 1 |
| `react-ui/app/layout.tsx` | MODIFY | 1 |
| `react-ui/lib/constants/board-assets.ts` | MODIFY | 2, 7 |
| `react-ui/components/layout/NavMenu.tsx` | MODIFY | 3 |
| `react-ui/components/game/tiles/GameTile.tsx` | MODIFY | 4, 7 |
| `react-ui/components/game/board/GameBoard.tsx` | MODIFY | 5, 7 |
| `react-ui/app/controls-test/page.tsx` | MODIFY | 5 |
| `.scripts/themes.ps1` | NEW | 6 |
| `react-ui/components/game/tiles/NumberToken.tsx` | MODIFY | 7 |
| `react-ui/components/game/panels/GameResourcesHeader.tsx` | MODIFY | 7 |
| `react-ui/components/game/panels/PlayersPanel.tsx` | MODIFY | 7 |
| `react-ui/components/game/controls/MeasurementCluster.tsx` | MODIFY | 7 |
| `react-ui/components/game/controls/ActionCluster.tsx` | MODIFY | 7 |
| `react-ui/lib/theme/hooks.ts` | MODIFY | 7 |

## Dependencies Between Phases

```text
Phase 1 (Foundation) --> Phase 2 (Asset resolution) --> Phase 3 (Theme UI)
                                                              |
                                                              v
                         Phase 5 (Harbor cleanup) <-- Phase 4 (Tile rendering)
                                                              |
                                                              v
                                                     Phase 6 (Script)
                                                              |
                                                              v
                                                Phase 7 (Eliminate hardcoded paths)
```

Phases 1-3 must be sequential. Phase 4 requires Phase 3 (need NavMenu to test
switching). Phase 5 requires Phase 4 (harbor + tile rendering both need to work
before removing the old prop). Phase 6 can run any time after Phase 1. Phase 7
requires Phase 2 (asset resolution infrastructure must exist) and can run after
Phase 6.
