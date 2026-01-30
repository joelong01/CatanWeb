# Board Rendering Pipeline

Source: design_docs/Board-Layout-Design.md, design_docs/render-perf-plan.md, design_docs/visual-design.md

## Goals

- Render the entire board client-side (Desktop WinUI XAML, WebUI SVG) from the shared `GameModel` without server-side rasterization.
- Maintain parity between Desktop and WebUI in layout, highlighting, and interaction affordances.

## WebUI Architecture

- `BoardSvgGenerator.GenerateSvg` orchestrates rendering:
  1. Computes viewBox bounds from tiles + harbors (`CalculateBounds`).
  2. Emits `<defs>` for textures (`GenerateTilePatterns`, `GenerateHarborPatterns`), gradients (`GeneratePlayerGradients`), and CSS rules.
  3. Renders background, tiles, harbors, roads, and buildings in z-order with transitions for animation.
- Rendering responsibilities delegated to composable helpers:
  - `TileSvgRenderer`: board hexes, number tokens, tile indices.
  - `RoadSvgRenderer`: road/ship paths, buildable outlines.
  - `BuildingSvgRenderer`: settlements/cities/metropolis, star overlays, build indices.
  - `HarborSvgRenderer`: harbor triangles and icons.
  - `BoardGeometry`: hex math (vertex positions, QR to pixel).
  - `BoardSvgConstants`: layout constants, fonts, colors, gradient identifiers.
- Interaction overlays (hover, highlights) controlled by CSS classes appended to SVG groups.

## Desktop Parity

- `DesktopApp/Layout/BoardCtrl` + `Resources/*Ctrl.xaml` use WinUI `Path`/`Polygon` elements.
  Bindings mirror WebUI logic (e.g., `BuildingVisualState`, road opacity).
- Shared helper methods (e.g., `GameModelExtensions.TilesForBuildings`) ensure star calculations and filtering behave identically.

## Performance Practices

- WebUI string-builds SVG once per state update; `<svg>` element bound via `MarkupString` to avoid DOM churn.
- Dimmed tiles and hidden buildings achieved with CSS opacity rather than removing elements.
- Patterns use `patternUnits="userSpaceOnUse"` to prevent zoom distortion.
- `BoardSvgGenerator` caches player lookup dictionary to avoid repeated LINQ lookups.

## Responsiveness

- `viewBox` computed from actual board extents with padding so the board scales to available container space.
- `preserveAspectRatio="xMidYMid meet"` keeps hex proportions intact on resize.
- Tooltip overlays and measurement controls adapt via CSS breakpoints defined in `wwwroot/css/app.css`.

## TODO / Future Work

- Shared geometry constants duplicated between WinUI and Blazor; consider moving into `Catan3.Shared.ViewData`.
- Evaluate shipping pre-computed SVG for static demo states to reduce initial render cost on low-end devices.
- Add server-driven diffing (SignalR) to surgically update changed elements instead of re-rendering entire SVG.
