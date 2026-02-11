# Visual Assets and Fonts

**Last verified:** January 30, 2026

## Overview

The application uses two font families for icons and a theme system
for resolving asset paths. A strict approval policy governs which
icon sources are permitted.

## Approved Font Sources

### Catan.ttf (~52 KB)

Game-specific icon font with glyphs for board elements:

| Glyph | Unicode | Usage |
|-------|---------|-------|
| City | `\uE900` | City markers, stats |
| Laurel | `\uE907` | Score display |
| Road | `\uE909` | Road markers |
| Ship | `\uE90A` | Ship/harbor icon |
| Pirate | `\uE90C` | Robber glyph |
| Soldier | `\uE90E` | Knight card |
| Star | `\uE911` | Probability stars |
| Sum | `\uE910` | Total resources |
| BadRoll | `\uE913` | Below-average roll |
| GoodRoll | `\uE914` | Above-average roll |
| LongestRoad | `\uE915` | Longest road badge |
| Target | `\uE916` | Times targeted |
| SolidShield | `\uE925` | Robber shield |
| Settlement | `\uE926` | Settlement markers |

**React loading:** Next.js `localFont()` API in `layout.tsx`:
```typescript
const catanFont = localFont({
    src: '../public/fonts/Catan.ttf',
    variable: '--font-catan',
    display: 'swap',
});
```

Preloaded via `<link rel="preload">` in the document head.

**Blazor loading:** Standard `@font-face` in `app.css`:
```css
@font-face {
    font-family: 'Catan';
    src: url('/themes/base/fonts/Catan.ttf') format('truetype');
}
```

### Font Awesome 6 Free (~200 KB)

General UI icons for navigation and controls:

| Icon | Usage |
|------|-------|
| `faHouse` | Home navigation |
| `faPlus` | New game |
| `faFolderOpen` | Load game |
| `faUsers` | Edit players |
| `faChartBar` | Statistics |
| `faGear` | Settings |
| `faPlay` | Start/resume |
| `faRotate` | Refresh/shuffle |
| `faScaleBalanced` | Balance board |
| `faTrophy` | Winner/stats |
| `faDownload` | Export/download |
| `faXmark` | Close/dismiss |
| `faExpand` | Expand/fullscreen |

**React:** `@fortawesome/react-fontawesome` package (v3.1.1) with
tree-shaking via individual icon imports.

**Blazor:** Static CSS file at
`wwwroot/lib/fontawesome/fontawesome-solid.css`.

**Total font payload:** ~252 KB (0.6% of typical app bundle).

## Approval Policy

**Mandatory:** Any glyph or icon not in Catan.ttf or Font Awesome 6
Free requires explicit human approval before use.

**Prohibited without approval:**
- Unicode symbols and emoji
- New font families
- Browser-specific fonts
- Inline SVGs for icons
- Icon libraries beyond Font Awesome

## Theme System

### React

Theme configuration in `public/themes/base/theme.json`:
- Maps asset names to file paths
- Includes tile images, harbor images, resource cards, building
  icons, stat icons, backgrounds, and fonts
- Font entry: `"FontCatan": "/themes/base/fonts/Catan.ttf"`

### Blazor

`ClientAssetService` loads theme JSON via HTTP:
- Supports theme hierarchy: `base` (fallback), `classic`,
  `black-and-white`
- Resolves `AssetName` enum to URL paths
- Theme preference persisted to localStorage (`"catan-theme"`)
- Maintains backward-compatible fallback paths

### Desktop

Uses Segoe MDL2 Assets (Windows-only) for general UI icons.
Game-specific icons use the same Catan.ttf.

## React Glyph Constants

**File:** `react-ui/lib/constants/catanGlyphs.ts`

```typescript
export const CatanGlyph = {
    City: '\uE900',
    Laurel: '\uE907',
    Road: '\uE909',
    Pirate: '\uE90C',
    Settlement: '\uE926',
    Soldier: '\uE90E',
    // ... (14 total)
} as const;
```

**Known issue:** `Building.tsx` duplicates some glyph constants
locally instead of importing from the shared constants file.

## Font File Locations

| File | Location |
|------|----------|
| Catan.ttf (React) | `react-ui/public/fonts/Catan.ttf` |
| Catan.ttf (theme) | `react-ui/public/themes/base/fonts/Catan.ttf` |
| Catan.ttf (Blazor) | `WebUI/wwwroot/themes/base/fonts/Catan.ttf` |

**Note:** The React app has two copies of Catan.ttf -- one in
`public/fonts/` (loaded by Next.js) and one in
`public/themes/base/fonts/` (referenced by theme.json). Only the
`public/fonts/` copy is actively loaded.
