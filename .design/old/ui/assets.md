# Asset & Theme Pipeline

**Last Updated:** January 15, 2026
Source: design_docs/assets-design.md, design_docs/catan-font-design.md

## Overview

Assets are delivered from the Game Service static file host (`wwwroot/themes`). The WebUI resolves them through `ClientAssetService`, which
implements `IAssetService` shared with Desktop.

## Theme Resolution (WebUI)

1. `ClientAssetService.InitializeAsync` loads `theme.json` for `base`, `classic`, `svg-theme`, and `black-and-white` over HTTP.
2. Each theme defines sparse overrides; `base` contains the complete asset map used as fallback.
3. After Blazor JS interop becomes available, `SetLocalStorage` is called so the chosen theme persists under key `catan-theme`.
4. `SetTheme` updates `_currentTheme`, saves to localStorage, and raises `ThemeChanged` (components subscribe to update bindings).
5. `GetAssetPath(AssetName)` first checks theme override dictionary, then base, then returns a legacy fallback path.

## AssetName Enum

Defined in `Catan3.Shared` to ensure parity across Desktop and WebUI. Entries cover tiles, harbors, board backgrounds, robbers, fonts, and
iconography. Adding a new asset requires:

- Updating `AssetName` enum.
- Adding default path in `/themes/base/theme.json`.
- Optionally adding overrides for other themes.
- Desktop binds via `AssetService.GetAsset` (WinUI resources).

## Typography & Icons

### Approved Font Sources

All UI icons and glyphs MUST come from one of two approved font sources to ensure cross-browser compatibility (including WebOS, older Safari,
and embedded browsers).

#### 1. Catan Font (`Catan.ttf`)

**Location:** `WebUI/wwwroot/themes/base/fonts/Catan.ttf`
**Size:** ~52 KB
**Purpose:** Game-specific iconography (resources, buildings, game pieces)

| Glyph Code | Description | Usage |
|------------|-------------|-------|
| `\uE90C` | Robber | Robber overlay |
| `\uE90D` | Harbor | Harbor markers |
| `\uE925` | Pirate | Pirate ship |
| Various | Resource icons | Player tiles, stats |

```css
.catan-icon {
    font-family: 'Catan', sans-serif;
}
```

#### 2. Font Awesome 6 Free (`fa-solid-900.woff2`)

**Location:** `WebUI/wwwroot/lib/fontawesome/`
**Size:** ~200 KB
**Purpose:** General UI icons (navigation, actions, status indicators)

| Icon | FA Class | Usage |
|------|----------|-------|
| Hamburger menu | `fa-bars` | Navigation toggle |
| Play | `fa-play` | Start/continue actions |
| Undo | `fa-rotate-left` | Undo action |
| Redo | `fa-rotate-right` | Redo action |
| Refresh/Shuffle | `fa-arrows-rotate` | Board shuffle |
| Balance | `fa-scale-balanced` | Balance indicator |
| Close | `fa-xmark` | Close/dismiss |
| Home | `fa-house` | Home navigation |
| Plus | `fa-plus` | Add/create actions |
| Folder | `fa-folder-open` | Load/browse |
| Save | `fa-floppy-disk` | Save action |
| Stop | `fa-stop` | Stop recording |
| Record | `fa-circle` | Recording indicator |
| Users | `fa-users` | Player management |
| Trophy | `fa-trophy` | Winner/stats |
| Settings | `fa-gear` | Settings menu |
| Trash | `fa-trash` | Delete action |

```html
<i class="fa-solid fa-bars"></i>
```

### MANDATORY: Human Approval for New Icons

**Any glyph or icon not available in Catan.ttf or Font Awesome 6 Free REQUIRES explicit human approval before implementation.**

This rule exists because:

1. **Compatibility:** Unicode symbols render inconsistently across browsers (WebOS, older Safari, embedded browsers)
2. **Consistency:** Using unapproved fonts creates visual inconsistency
3. **Maintenance:** Random Unicode characters are hard to track and maintain
4. **Size:** Adding new font files impacts load time

### Prohibited Practices

The following are **NOT ALLOWED** without explicit human approval:

- Using Unicode symbols directly (e.g., `☰`, `▶`, `⟳`, `⚖`)
- Using emoji characters (e.g., `🏆`, `🏠`, `📂`)
- Adding new font files
- Using browser-specific fonts (e.g., Segoe MDL2 Assets)
- Using inline SVGs as icon replacements

### When You Need a New Icon

1. **Search Font Awesome first** - It has 2000+ icons; the icon likely exists
2. **Check if Catan font has it** - For game-specific glyphs
3. **If neither works, STOP and ask for human approval**

### Unicode Migration Reference

Existing Unicode symbols should be migrated to Font Awesome:

| Old (Unicode) | New (Font Awesome) |
|---------------|-------------------|
| `☰` | `<i class="fa-solid fa-bars"></i>` |
| `▶` / `&#x25B6;` | `<i class="fa-solid fa-play"></i>` |
| `&#x21A9;` | `<i class="fa-solid fa-rotate-left"></i>` |
| `&#x21AA;` | `<i class="fa-solid fa-rotate-right"></i>` |
| `&#x27F3;` | `<i class="fa-solid fa-arrows-rotate"></i>` |
| `&#x2696;` | `<i class="fa-solid fa-scale-balanced"></i>` |
| `&#x1F3C6;` | `<i class="fa-solid fa-trophy"></i>` |
| `&#x1F3E0;` | `<i class="fa-solid fa-house"></i>` |
| `&#x2795;` | `<i class="fa-solid fa-plus"></i>` |
| `&#x1F4C2;` | `<i class="fa-solid fa-folder-open"></i>` |
| `&#x1F4BE;` | `<i class="fa-solid fa-floppy-disk"></i>` |
| `&#x1F465;` | `<i class="fa-solid fa-users"></i>` |
| `&#x2699;` | `<i class="fa-solid fa-gear"></i>` |
| `&#x1F5D1;` | `<i class="fa-solid fa-trash"></i>` |
| `&#x2715;` | `<i class="fa-solid fa-xmark"></i>` |

### Font Size Impact

| Asset | Size | Notes |
|-------|------|-------|
| Catan.ttf | 52 KB | Game-specific icons |
| Font Awesome (solid) | ~200 KB | General UI icons |
| **Total font payload** | ~252 KB | 0.6% of 40 MB app |

## Desktop Asset Service

- `Catan.Services.AssetService` mirrors theme lookup with file-system paths (not shown here). Uses `SettingsService` to store selection.
- Both Desktop and WebUI share theme identifiers (`classic`, `black-and-white`). Asset packs kept in sync across projects.
- Desktop uses Segoe MDL2 Assets for icons (Windows-only); WebUI uses Font Awesome for cross-platform support.

## TODO / Follow-Up

- Theme metadata currently hard-coded in `InitializeAsync`; introduce discovery API to avoid editing code when adding themes.
- LocalStorage persistence occurs only after `LoadThemeFromStorage` is called manually; automate during host startup once WASM JS interop race is
  resolved.
- Document contribution pipeline for artists (expected image sizes, naming conventions) in `Docs/` repository.
- Complete migration from Unicode symbols to Font Awesome icons.
