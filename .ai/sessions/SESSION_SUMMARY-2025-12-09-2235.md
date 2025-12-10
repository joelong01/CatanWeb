# Session Summary - 2025-12-09 2235

**Session Duration:** ~4 hours
**Build Status:** Not verified (skipped per user request)
**Test Status:** Not verified (skipped per user request)
**Branch:** WebUI

## Work Completed

### Major Features

#### Mobile Support for WebUI (iPhone/iPad)

Implemented comprehensive mobile support enabling the Catan game to work on iOS devices with proper scaling, scrolling, and pinch-to-zoom functionality.

**Key files:**

- `WebUI/wwwroot/index.html` - Dynamic viewport meta for mobile
- `WebUI/wwwroot/css/app.css` - Mobile overflow and touch-action
- `WebUI/Pages/Game.razor.css` - Extensive mobile CSS (v35)
- `WebUI/Components/Board/BoardContainer.razor.css` - Board sizing by orientation
- `WebUI/wwwroot/js/viewportScaler.js` - Mobile device detection

**Implementation Details:**

1. **Network Access**: Added `-Network` flag to `webui.ps1` to bind services to `0.0.0.0` for iPhone simulator/device access
2. **Viewport Meta**: Dynamic viewport that uses `width=1920` for mobile (enabling pinch-zoom) vs `width=device-width` for desktop
3. **Pinch-to-Zoom**: Configured `initial-scale=0.39, minimum-scale=0.1, maximum-scale=2.0` for mobile
4. **Scrolling**: Enabled touch scrolling with `-webkit-overflow-scrolling: touch` on mobile only
5. **Board Sizing**:
   - Desktop landscape: Board fills height, width from aspect ratio
   - Desktop portrait: Board fills width, height from aspect ratio
   - Mobile landscape: Fixed 1920x1080 dimensions with scroll/zoom

#### Portrait Mode Layout Fixes

Fixed board sizing in portrait mode on desktop browsers by:

- Using `::deep` selector to pierce Blazor component CSS isolation
- Setting `display: block` on board hierarchy to break grid sizing
- Changing center-panel to use `align-items: stretch` for full-width children

#### BoardMeasurements Component Relocation

Moved BoardMeasurements from Controls tab to Board tab in portrait mode:

- Added `portrait-board-measurements` div below the board in center-panel
- Added CSS to hide/show based on orientation (similar to ResourceTracking pattern)

### Infrastructure/Tooling

#### webui.ps1 Network Flag

- Added `-Network` switch parameter
- When enabled, services bind to `0.0.0.0:8080` and `0.0.0.0:5296`
- Fixed AppleScript escaping for macOS by using single quotes
- Enables testing on physical iPhone or iOS Simulator

### Bug Fixes

- Fixed `0.0.0.0` hostname detection for LOCAL vs WEB environment indicator
- Removed `max-width: 1024px` from media queries to prevent narrow desktop windows from triggering mobile CSS

## Decisions Made

### Architecture Decisions

1. **Mobile Viewport Strategy**
   - **Context:** iOS Safari requires special handling for pinch-zoom and scrolling
   - **Options Considered:**
     - CSS transform scaling (used for desktop) - Rejected: doesn't allow zoom-out
     - Fixed viewport width with zoom - **CHOSEN**: allows full zoom control
   - **Implementation:** `width=1920, initial-scale=0.39` for mobile devices
   - **Implications:** Mobile users see full 1920px layout, can zoom in/out freely

2. **Touch Device Detection**
   - **Context:** Need to apply mobile CSS only to actual touch devices
   - **Chosen:** `@media (pointer: coarse)` instead of `max-width: 1024px`
   - **Rationale:** Width-based detection incorrectly triggered on narrow desktop windows

3. **Board Sizing by Orientation**
   - **Desktop Landscape:** `height: 100%; width: auto; aspect-ratio` - fills height
   - **Desktop Portrait:** `width: 100%; height: auto; display: block` - fills width
   - **Mobile:** Fixed 1920x1080 with scrolling
   - **Key insight:** Required `display: block` to break grid/flex height constraints

### Design Patterns

- Used `::deep` selector for Blazor scoped CSS to pierce into child components
- Followed existing `portrait-resource-tracking` pattern for conditional component visibility

## Important Context

### Critical Information

- **CSS Version Indicator:** Shows `CSS 2025-12-09 v35 {ENV} {orientation}` in bottom-right
- **Debug Borders:** Controlled by `--debug-borders: 0|1` in app.css (currently OFF)
- **Environment Detection:** `0.0.0.0` now detected as LOCAL (in addition to localhost/127.0.0.1)

### Gotchas and Non-Obvious Aspects

1. **Blazor Scoped CSS Limitation**
   - Scoped CSS can't target elements in child components
   - Solution: Use `::deep` selector (e.g., `.parent ::deep .child-component-element`)
   - Surprisingly, `::deep` worked even though Blazor docs say `:deep()`

2. **iOS Safari Zoom Behavior**
   - Can't zoom out smaller than content width
   - Must set viewport `width` larger than device width to enable zoom-out
   - `initial-scale` determines starting zoom level

3. **Grid/Flex Height Issues**
   - Elements with `height: 100%` in flex containers may collapse
   - Setting `display: block` breaks the flex/grid sizing and allows natural flow
   - Critical for portrait mode where board should size from width

4. **Media Query for Mobile**
   - `(pointer: coarse)` detects touch devices
   - `(max-width: 1024px)` incorrectly triggers on narrow desktop windows
   - Use only `pointer: coarse` for true mobile-only styles

### Key Files and Patterns

- **Mobile CSS:** `WebUI/Pages/Game.razor.css` lines 727-830 (all mobile overrides)
- **Viewport Meta:** `WebUI/wwwroot/index.html` lines 6-17 (dynamic viewport script)
- **Board Sizing:** `WebUI/Components/Board/BoardContainer.razor.css` (orientation-specific rules)

## Next Session Priority

1. **Test All Platforms**
   - Verify desktop landscape works correctly
   - Verify desktop portrait works correctly
   - Test iPhone landscape with zoom
   - Test iPhone portrait (if implemented)

2. **Performance Testing**
   - Check if fixed 1920px mobile layout causes performance issues
   - Consider if scroll performance is acceptable

3. **Consider Portrait Mode for Mobile**
   - Current implementation focused on landscape
   - May need mobile-specific portrait layout

### Follow-Up Tasks

- [ ] Run full build and test suite
- [ ] Test on actual iPhone device (not just simulator)
- [ ] Verify Azure deployment still works
- [ ] Consider adding user preference for zoom level

## Quick Start for Next Session

### Immediate Actions

1. **Kill any running services:**

   ```bash
   lsof -ti:8080 | xargs kill -9 2>/dev/null
   lsof -ti:5296 | xargs kill -9 2>/dev/null
   ```

2. **Start services:**

   ```bash
   ./webui.ps1 run          # Normal
   ./webui.ps1 run -Network # For iPhone testing
   ```

3. **Test URLs:**
   - Desktop: `http://localhost:5296`
   - iPhone: `http://{mac-ip}:5296`

### Debug Mode

To enable debug borders:

```css
/* In app.css */
--debug-borders: 1;
```

### CSS Version Check

Look for version indicator in bottom-right corner of game screen:

- `CSS 2025-12-09 v35 LOCAL landscape` (or portrait/WEB variants)
