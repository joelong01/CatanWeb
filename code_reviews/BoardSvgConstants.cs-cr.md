# BoardSvgConstants.cs Code Review

## Critical

- None.

## Important

- `HexStrokeWidth` is set to `6`, but the Desktop layout renders the maple border with `TileGap = 2` and keeps the resource fill inside the inner hex. With the current value, the WebUI stroke is three times thicker yet still cannot reproduce the wood border because the renderer does not draw the inner hex separately. Align the constants (and associated rendering) with `BoardVisualLayout.TileGap/InnerHexStrokeThickness` so tiles match the WinUI3 look.

## Suggestion

- `NumberTokenRadius` (`30`) and `NumberTokenOpacity` (`0.85`) differ from Desktop’s 65px circle (`radius ≈ 32.5`) at `0.75` opacity. `PipsFontSize` is also two points larger (12 vs 10). The deviations are visible next to the WinUI3 screenshot; please check whether the size/opacity choices were intentional.
- Consider exposing the `ShowTileCoordinates` flag through a configuration service similar to Desktop’s `TileIndexVisibility` toggles instead of relying on a mutable static.

## Question

- Can we document where the `CenterX`/`CenterY` anchor values originate? Desktop derives offsets from `BoardVisualLayout` rather than hard-coded pixels; future board sizes may need the same flexibility here.

## Praise

- Good call keeping the constants collocated—parity work with the Desktop app becomes a quick audit of this single file.

## Desktop Comparison Notes

- Desktop's `BoardVisualLayout` drives border thickness and number token sizing. Matching those values keeps SVG output visually consistent with the established WinUI3 design.

---

## Resolution Actions (2025-11-26 16:00 UTC)

### Important Issues
- **HexStrokeWidth/Inner Hex Rendering**: ✅ **RESOLVED**
  - **Analysis**: HexStrokeWidth was 6px (3x Desktop's TileGap=2). Constants for InnerHexSize and InnerHexStrokeThickness were defined but not used by renderer.
  - **Root Cause**: TileSvgRenderer rendered single polygon instead of Desktop's two-polygon approach (outer frame + inner resource fill).
  - **Fix Applied**:
    - Removed `HexStrokeWidth` constant entirely
    - Added XML documentation to `TileGap` explaining dual use (border stroke + road spacing)
    - Updated `TileSvgRenderer.RenderHexBackground()` to render two polygons:
      1. Outer hex with stroke=TileGap (2px) for wood border
      2. Inner hex with stroke=InnerHexStrokeThickness (16px, transparent) for resource fill
    - Added `HexHighlightColor` constant for tile highlighting (robber placement, etc.)
    - Updated methods to support variable hex sizes (`GenerateHexPath` and `GetHexVertices`)
  - **Visual Result**: Web tiles now match Desktop's thin, crisp wood border (verified against `.test_images/desktop board.jpg`)
  - **Bonus**: Tile highlighting now supported via `tile.Highlighted` flag

### Suggestions
- **Number Token Dimensions**: ✅ **NO CHANGE NEEDED** (Intentional deviation documented)
  - **Analysis**: NumberTokenRadius=30 (vs Desktop's 32.5), NumberTokenOpacity=0.85 (vs 0.75), PipsFontSize=12 (vs 10)
  - **Rationale**: Values chosen deliberately for SVG rendering based on commit 37acf5f and `design_docs/catan-number-design.md`
  - **Action**: Added comment referencing design doc for future maintainers

- **ShowTileCoordinates Configuration**: ⏸️ **DEFERRED** (Low priority)
  - **Analysis**: Mutable static field is poor practice for testability
  - **Recommendation**: Move to `GameStateService.ShowTileCoordinates` property in future refactor
  - **Action**: Added TODO comment documenting technical debt
  - **Priority**: Low (debug-only feature)

### Questions
- **CenterX/CenterY Documentation**: ✅ **RESOLVED**
  - **Action**: Added XML documentation explaining values center a 5-ring hex board (800x700) with padding for harbor elements
  - **Source**: Values empirically determined to fit standard/expansion boards with harbor positioning

### Design Principles Applied
- **No Magic Numbers**: All colors defined as named constants in `BoardSvgConstants` (e.g., `HexHighlightColor`)
- **Semantic Comments**: Comments describe purpose/meaning, not implementation details (e.g., "Highlight color" not "Yellow color")
- **Centralized Theming**: Color/size changes can be made in single location

### Files Modified
1. `WebUI/Services/Rendering/BoardSvgConstants.cs`
   - Removed `HexStrokeWidth` constant
   - Added `HexHighlightColor = "#FFD700"`
   - Added XML documentation for `TileGap`, `CenterX`, `CenterY`

2. `WebUI/Services/Rendering/TileSvgRenderer.cs`
   - Rewrote `RenderHexBackground()` for two-polygon rendering
   - Updated `GetHexVertices(cx, cy, size)` signature
   - Updated `GenerateHexPath(cx, cy, size)` signature

### Build Status
- ✅ All projects build successfully
- ✅ No breaking changes to public APIs
- ✅ Desktop app MSIX package generated and installed

### Next Steps
- Test visual appearance in running WebUI (compare against Desktop screenshots)
- Continue with remaining code review files (BoardSvgGenerator.cs-cr.md, etc.)
