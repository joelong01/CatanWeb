# Mobile UI Improvements

## Hamburger Menu Button Size on Mobile

**Issue**: The hamburger menu button (☰) is too small to tap easily on iPad and mobile devices.

**Desired behavior**: Make the hamburger button and menu items larger on touch devices without affecting desktop layout.

**Approach**: Use CSS media queries with `@media (pointer: coarse)` or `@media (hover: none)` to increase:
- `.hamburger-btn` font-size from 1.5rem to ~2.5rem
- `.menu-panel` width from 100px to ~140px
- `.nav-menu-item` min-height and padding
- `.nav-icon` and `.nav-label` font sizes

**Files to modify**:
- `WebUI/Layout/MainLayout.razor.css` - hamburger button and menu panel
- `WebUI/Layout/NavMenu.razor.css` - menu item sizes

**Notes**:
- Consider adding cache-busting query string to CSS link in `index.html` when deploying changes
- Test on actual iPad device, not just simulator
- The viewport meta tag in index.html sets width=1920 on mobile which may affect how media queries behave
