# BoardSvgGenerator.cs Code Review

## Critical

- `GetBuildingVisualState` always collapses the Desktop logic to `Normal` for both `PossibleSettlement` and `City` states. On the Desktop side (`GameViewModel.MergeBuildings` and `BuildingViewModel.BIND_StateGlyph`) the visual state depends on entitlements, `ShownStars`, and ownership. Treating everything as `Normal` causes empty build sites to render as built structures, hides placement highlights, and breaks the star/threshold workflow entirely.
- Buildings never render their highlight/star metadata. `RenderSvg` accepts `stars` and `buildIndex`, but `BoardSvgGenerator` never supplies either value. As a result, even if we corrected the visual state logic, highlight builds would still miss their badge/index and star-only views would remain blank—behaviour that Desktop users rely on when planning placements.

## Important

- The `shownStars` argument passed into `GetBuildingVisualState` is unused. This dead parameter blocks the star-threshold slider from ever doing anything and signals that the Desktop parity work was left unfinished. Please either wire it up (matching `GameViewModel.MergeBuildings`) or remove it.
- Roads are rendered without considering `RoadState` or owner opacity. Desktop `RoadViewModel.Opacity` uses 0.0/0.5/1.0 based on ownership/buildable status. Here we always pass the default `opacity` (=0) into `RoadSvgRenderer`, so finished roads disappear unless some other layer overrides it.
- Harbor/tile pattern `<image>` elements are emitted inside `objectBoundingBox` patterns without setting `patternContentUnits="userSpaceOnUse"`. Browsers treat this inconsistently; the Desktop assets rely on deterministic scaling. Please double-check the SVG to ensure textures are not distorted.

## Suggestion

- Consider centralising shared geometry helpers (`AxialToPixel`, `GetHexVertices`, etc.) to avoid the four duplicate implementations in the renderer files.

## Question

- Do we plan to support debug overlays similar to Desktop’s `TileIndexVisibility`/`DimAnimation`? If so, where will the triggers live in the WebUI pipeline?

## Praise\

- Nice job mirroring the compositional structure (tiles ➝ harbors ➝ roads ➝ buildings). It keeps parity with the Desktop rendering order and will make diffing much easier.

## Desktop Comparison Notes

- Desktop uses `GameViewModel.MergeBuildings` to decide `BuildingVisualState`, including entitlement-based highlights and star-only visibility. The current WebUI logic omits all of this, so placement guidance diverges sharply from the WinUI3 behaviour.
- Road opacity/backgrounds in WinUI3 are derived from `RoadViewModel` brushes and opacity helpers; none of that state flows into the SVG yet.
