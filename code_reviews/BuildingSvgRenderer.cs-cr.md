# BuildingSvgRenderer.cs Code Review

## Critical

- `RenderBuildingGlyph` always draws the full settlement/city glyph even when the visual state is `Stars` or `Highlighted` for empty sites. Desktop’s `BuildingViewModel.BIND_StateGlyph` shows nothing (Hidden), numeric stars, or a hollow highlight depending on entitlements and `ShownStars`. Rendering a solid building misleads the player about what is already built.

## Important

- Highlight styling diverges from WinUI3. Desktop swaps foreground/background brushes based on owner/current player and only shows the build index badge when `VisualState == Highlighted`. Here we hardcode a filter (`brightness(1.5)`) and omit the badge background/visibility check, so highlighted placements are both visually and functionally different.
- Star-only mode (`BuildingVisualState.Stars`) tries to draw a string of `★`, but Desktop displays the numeric star count (via `glyph = stars.ToString()`). We also never receive the star count because `BoardSvgGenerator` passes the default `-1`. Fixing both sides is necessary for the board-measurement slider to work.

## Suggestion

- Fallback colours for unowned buildings default to gray. Desktop uses the current player’s colours when no owner is set, which helps players see whose entitlement is active. Consider mirroring that behaviour.
- The build index text lacks accessibility affordances (no `aria-label`/`title`). Adding a tooltip or data attribute would ease testing.

## Question

- Do we plan to include robber indicators or metropolis walls like the Desktop control does? These aren’t represented here yet.

## Praise

- Leveraging gradients from `PlayerData` keeps parity with the shared colour palette—good foundation for matching the WinUI3 theme once the visual states are fixed.

## Desktop Comparison Notes

- WinUI3’s `BuildingCtrl` swaps glyphs based on `BuildingVisualState` and entitlements. The SVG must follow suit to avoid showing phantom settlements or hiding build windows.
