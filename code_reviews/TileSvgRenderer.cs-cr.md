# TileSvgRenderer.cs Code Review

## Critical

- Tile highlighting is lost. Desktop renders two polygons—an outer maple frame that can switch to yellow when `TileModel.Highlighted` is true, and an inner hex for the resource art. The SVG pushes everything into a single `<path>` with a fixed stroke/color, ignoring `tile.Highlighted`. Players will never see robber/placement highlights. We need to split the geometry (outer frame + inner fill) and drive the stroke/fill from the highlight state.

## Important

- The resource artwork should sit inside the inner hex (Desktop uses `Layout.InnerHexPoints`). Because we render only the outer hex, the texture bleeds right up to the frame and differs noticeably from WinUI3.
- Temporary-gold tiles show the original resource card, but Desktop animates this with `FlipperCtrl` and hides the card when not in the temporary state. Right now the card just pops in without animation. If parity matters, consider mimicking the flip or at least hiding the element when not gold.

## Suggestion

- Probability “pips” use literal `★`. Desktop pulls `
E` from Segoe Fluent Icons so the dots match the board art. Swapping to Segoe MDL2/Fluent glyphs would tighten parity.
- The coordinates overlay pulls from `HexHeight / 2 - 10`. Desktop anchors it to `CooordinateTextMargin(tileGap, innerStroke)`, so if we realign the inner hex maths we should reuse that calculation to keep spacing consistent.

## Question

- Do we plan to expose tile dimming transitions (Desktop’s `DimAnimation`/`RevertAnimation`)? The CSS currently sets a static opacity; parity might require brief transitions or matching durations.

## Praise

- Nice reuse of shared geometry helpers—`AxialToPixel` and `GenerateHexPath` align well with our conversion formulas and should stay correct once the styling gaps are addressed.

## Desktop Comparison Notes

- WinUI3 tiles render a maple border (`TileGap`, highlight-dependent brush), an inner resource polygon, a 65px number token at 0.75 opacity, and Segoe Fluent pips. The single-path WebUI rendering misses those layered visuals, so tiles look and behave differently.
