# RoadSvgRenderer.cs Code Review

## Critical

- Roads default to `opacity="0"` because `RenderSvg`’s parameter defaults to `0.0` and `BoardSvgGenerator` never overrides it. Completed roads therefore disappear entirely. Desktop’s `RoadViewModel.Opacity` returns `1.0` for owned roads, `0.5` for buildable, and `0` otherwise. Please replicate that logic before rendering the SVG.

## Important

- `RoadState` is ignored when picking fill/stroke colours. The WinUI3 view model switches between owner colours, current-player highlights, and transparent builds depending on the state (`GetBackgroundBrush` / `GetForegroundBrush`). We only look at `OwnerId`, so buildable roads never highlight for the active player.
- The build index badge lacks the black rounded rectangle behind the number and uses raw text instead of Desktop’s Viewbox container. Visibility ends up poor on bright gradients; consider copying the backing rectangle to maintain legibility.

## Suggestion

- We should cache the polygon strings the same way the Desktop `RoadViewModel` caches point collections. Rendering every road every frame may be fine (<100 elements), but caching reduces GC pressure.
- The `.road:hover` CSS assumes the polygon is visible enough to hover. Once opacity is corrected, consider adding `pointer-events` or a cursor style to match Desktop’s interactivity cues.

## Question

- Do we plan to support ships (Expansion rules)? If so, we will need to adjust the polygon geometry/backgrounds similarly to Desktop.

## Praise

- The 6-point polygon mirroring the Desktop geometry is spot on; once opacity/colour state flows through, the roads should line up perfectly with WinUI3 screenshots.

## Desktop Comparison Notes

- WinUI3 uses `RoadViewModel.Opacity` plus owner/current-player brushes to communicate buildability. Without that state, the WebUI board loses all road guidance.
