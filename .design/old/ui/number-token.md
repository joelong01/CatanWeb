# Number Token Rendering

Source: design_docs/catan-number-design.md

## Shared Constants

- `BoardSvgConstants` controls token radius (`NumberTokenRadius`=30), font sizes, offsets, fill colors, and highlight colors.
- High-probability numbers (6, 8) use `HighProbColor`; others use `NormalNumberColor`.

## WebUI Implementation

- `CatanNumberSvg.Render(number, radius)` builds an SVG group:
  1. Background circle with configurable opacity/stroke.
  2. Number text centered at `BoardSvgConstants.NumberOffsetY`.
  3. Probability stars (★) from `GetPips` positioned via `PipsOffsetY`.
- `TileSvgRenderer.RenderNumberToken` positions token using `<g transform="translate(x,y)">` so tokens scale with board geometry.
- `RenderStandalone` helper generates standalone SVG (used in UI previews/tooltips).

## Desktop Implementation

- `DesktopApp/Tiles/TileCtrl.xaml` draws an ellipse + text blocks with the same probability star mapping.
- Colors and font weights pulled from `NumberTokenTheme` resource dictionary to ensure parity with WebUI constants.

## Probability Mapping

| Roll | Stars |
|------|-------|
| 2, 12 | ★ |
| 3, 11 | ★★ |
| 4, 10 | ★★★ |
| 5, 9 | ★★★★ |
| 6, 8 | ★★★★★ |

## TODO

- Unify constant definitions by moving shared values into `Catan3.Shared.ViewData` to avoid manual sync between `BoardSvgConstants` and WinUI
  resource dictionaries.
- Support alternate glyph sets for accessibility (e.g., numeric odds) via theme metadata.
