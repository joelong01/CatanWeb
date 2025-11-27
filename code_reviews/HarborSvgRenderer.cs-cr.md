# HarborSvgRenderer.cs Code Review

## Critical

- None.

## Important

- The harbor circle radius (`35`) and lack of translation do not match Desktop’s ellipse, which scales the 40px control by 1.5 and offsets it (`TranslateX/Y = -10`). The SVG circle ends up larger and centred differently than the WinUI3 asset, so the token floats farther from the board edge.
- Desktop wraps the ellipse in a `FlipperCtrl` to handle face-up/face-down transitions (e.g., during shuffles). The SVG renders a static circle, so we lose that animation cue.

## Suggestion

- Consider reusing the same water colour (`bmWater`) as the Desktop palette. `#4169e1` (Royal Blue) is noticeably brighter.
- As with the tile textures, verify that the `<pattern>` coordinate system preserves the harbour art aspect ratio.

## Question

- Do we need to support harbour orientation flips (e.g., backside art) similar to Desktop? If not, documenting the difference will help future parity work.

## Praise

- The outward normal computation for harbour placement matches the Desktop layout logic, keeping harbour positioning accurate across all sides.

## Desktop Comparison Notes

- WinUI3 harbours render a 60px (scaled) ellipse with bmWater background and flipper animations. Aligning the SVG size/offsets will make the WebUI board feel more familiar to desktop users.
