# Asset & Theme Pipeline

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

- Fonts live under `wwwroot/themes/base/fonts/`; CSS variables (`--font-display`, `--font-numbers`) applied in `wwwroot/css/app.css`.
- Icon buttons use Unicode glyphs (no Segoe MDL2) to remain cross-platform. Glyph mapping defined in CSS using `::before` pseudo-elements.

## Desktop Asset Service

- `Catan.Services.AssetService` mirrors theme lookup with file-system paths (not shown here). Uses `SettingsService` to store selection.
- Both Desktop and WebUI share theme identifiers (`classic`, `black-and-white`). Asset packs kept in sync across projects.

## TODO / Follow-Up

- Theme metadata currently hard-coded in `InitializeAsync`; introduce discovery API to avoid editing code when adding themes.
- LocalStorage persistence occurs only after `LoadThemeFromStorage` is called manually; automate during host startup once WASM JS interop race is
  resolved.
- Document contribution pipeline for artists (expected image sizes, naming conventions) in `Docs/` repository.
