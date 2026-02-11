# App Shell Layout

**Last verified:** February 9, 2026

## Overview

The app shell is the outermost layout structure provided by
`MainLayout`. It has **two modes** depending on the page:

- **Standard mode** (non-game pages): three fixed zones — nav column,
  header bar, and content area. The content area is the only zone
  that scrolls.
- **Full-screen mode** (game page): content covers the entire
  viewport. The hamburger button floats over the content. No nav
  column or header bar reserves space.

The standard mode follows the "dashboard layout" pattern used by
Gmail, Slack, and Azure Portal. It maps to the WinUI 3 desktop app's
Grid layout with `ColumnDefinitions` and `RowDefinitions`.

## Standard Mode (Non-Game Pages)

```text
┌──────┬──────────────────────────────────────┐
│ Nav  │          Header Bar                  │
│ Col  │  [Back]  Page Title                  │
│      ├──────────────────────────────────────┤
│ ☰    │                                      │
│      │         Content Area                 │
│      │                                      │
│      │    (scrolls independently)           │
│      │                                      │
│      │                                      │
└──────┴──────────────────────────────────────┘
```

### Three Zones

| Zone | Purpose | Scroll/Zoom | CSS Position |
|------|---------|-------------|--------------|
| **Nav column** | Hamburger button; expands to show NavMenu | None — fixed | Grid column |
| **Header bar** | Page title, Back button | None — fixed | Grid row |
| **Content area** | Page-specific content (hex grids, forms, lists) | Independent scroll | `overflow: auto` |

### Zone Boundaries

The content area treats the nav column and header bar as hard
boundaries:

- **Pan left** stops at the nav column edge (content does not slide
  under the nav)
- **Pan up** stops at the header bar edge
- **Pinch-to-zoom** is scoped to the content area only — the nav
  column and header bar remain at 1x scale

This matches how the desktop app's `ScrollViewer` is contained within
its grid cell and cannot overlap the menu or title bar.

## Full-Screen Mode (Game Page)

```text
┌────────────────────────────────────────────┐
│ ☰                                          │
│                                            │
│         Content Area                       │
│         (full viewport)                    │
│                                            │
│    GameBoard + FloatingPanels              │
│    pans / zooms independently              │
│                                            │
│                                            │
└────────────────────────────────────────────┘
```

The game page needs every pixel for the board and floating panels.
There is no reserved nav column or header bar. The hamburger button
floats over the content at `position: fixed` with a high z-index,
exactly as it does today. Tapping it opens the nav menu as an
overlay.

**The hamburger button must not zoom when the game board zooms.**
Because it uses `position: fixed`, it lives outside the board's CSS
transform context and remains at 1x scale regardless of pinch-zoom
level. This is critical — the hamburger is the user's only way to
access the nav menu, so it must always be reachable at a predictable
size and position.

This mode is activated by passing `fullScreen={true}` (or similar
prop) to `MainLayout`.

## Current Implementation (Before)

```text
MainLayout (div.page — full viewport flex column)
├── button.hamburger-btn (position: fixed, z-index: 2000)
│   └── Overlaps content — no reserved space
├── div.menu-overlay (position: fixed, z-index: 1500)
│   └── div.menu-panel (100px wide, full height)
│       └── NavMenu
└── main
    └── article.content
        └── {children}
```

**Problems:**

1. Hamburger button floats over content (overlaps page titles, hex
   tiles on mobile)
2. No header bar — pages must add their own back buttons and titles
   with manual top padding (`pt-[60px]`)
3. All content shares the page scroll context — no independent zoom
4. On iPhone, the hamburger (90x90px at `font-size: 4.5rem`) covers
   a large area of the top-left content

## Proposed Implementation (After)

### HTML Structure

```tsx
<div className="app-shell">
  {/* Nav Column — collapses to icon strip, expands to sidebar */}
  <nav className="app-nav">
    <button className="nav-toggle" onClick={toggleMenu}>
      <FontAwesomeIcon icon={faBars} />
    </button>
    {isMenuOpen && <NavMenu onMenuAction={closeMenu} ... />}
  </nav>

  {/* Main area: header + content */}
  <div className="app-main">
    {/* Header Bar */}
    <header className="app-header">
      {onBack && (
        <button className="back-button" onClick={onBack}>
          <FontAwesomeIcon icon={faArrowLeft} /> Back
        </button>
      )}
      {title && <h1 className="page-title">{title}</h1>}
    </header>

    {/* Content Area — sole scrollable/zoomable zone */}
    <main className="app-content">
      {children}
    </main>
  </div>
</div>
```

### CSS Layout (Tailwind + Custom Classes)

The shell uses CSS Grid for the two-column structure (nav + main),
and flexbox for the main area's vertical stack (header + content).

```css
/* Root grid: nav column + main area */
.app-shell {
  display: grid;
  grid-template-columns: var(--nav-width) 1fr;
  width: 100vw;
  height: 100dvh;
  overflow: hidden;
}

/* Nav column */
.app-nav {
  display: flex;
  flex-direction: column;
  align-items: center;
  background: var(--game-bg-primary);
  border-right: 1px solid rgba(255, 255, 255, 0.1);
  padding-top: 0.5rem;
  overflow: hidden;           /* Nav never scrolls */
  z-index: 100;               /* Above content, below modals */
}

/* Main area: header stacked above content */
.app-main {
  display: flex;
  flex-direction: column;
  min-height: 0;              /* Allow flex child to shrink */
  overflow: hidden;           /* Main wrapper doesn't scroll */
}

/* Header bar */
.app-header {
  display: flex;
  align-items: center;
  gap: 0.75rem;
  padding: 0.5rem 1rem;
  background: var(--game-bg-secondary);
  border-bottom: 1px solid rgba(255, 255, 255, 0.1);
  flex-shrink: 0;             /* Never collapses */
}

/* Content area — the ONLY zone that scrolls/zooms */
.app-content {
  flex: 1;
  overflow: auto;
  position: relative;
  min-height: 0;
  -webkit-overflow-scrolling: touch;
}
```

### CSS Custom Properties

```css
:root {
  /* Nav column width: collapsed (icon-only) vs expanded (with labels) */
  --nav-width-collapsed: 48px;
  --nav-width-expanded: 220px;
  --nav-width: var(--nav-width-collapsed);

  /* Header bar height */
  --header-height: 44px;
}

/* Mobile: wider collapsed nav for touch targets */
@media (pointer: coarse) {
  :root {
    --nav-width-collapsed: 56px;
    --nav-width-expanded: 260px;
    --header-height: 52px;
  }
}
```

### Nav Column Behavior

| State | Width | Contents |
|-------|-------|----------|
| **Collapsed** (default) | `--nav-width-collapsed` (48px) | Hamburger icon only |
| **Expanded** | `--nav-width-expanded` (220px) | Hamburger + NavMenu items with labels |

Expansion behavior options (to be decided during implementation):

- **Option A — Overlay:** Nav expands over content (current behavior
  with overlay backdrop). Simpler, no content reflow.
- **Option B — Push:** Nav expands and pushes content to the right.
  Content area shrinks. More "app-like" but causes reflow.
- **Option C — Hybrid:** Overlay on mobile, push on desktop.

The current implementation uses Option A (overlay). This is the
recommended default — it matches the Blazor WebUI and avoids layout
thrash on mobile.

### Header Bar

The header bar provides:

1. **Back button** — shown when the page has a parent (e.g., game
   page → home). Uses `router.back()` or explicit navigation.
2. **Page title** — the current page name (e.g., "Open Game",
   "New Game", "Settings").
3. **Status badges** — connection status, game state (game page only).

Pages that don't need a title or back button (e.g., home page) can
pass `hideHeader` to suppress the bar entirely, giving the content
area the full vertical space.

### Content Area

In standard mode, the content area is scoped for scrolling:

```css
.app-content {
  flex: 1;
  overflow: auto;
  position: relative;
  min-height: 0;
  -webkit-overflow-scrolling: touch;
}
```

For **non-game pages** (home, settings, load game), the content area
is a standard scrollable container. No zoom needed.

For the **game page** (full-screen mode), there is no `.app-content`
wrapper — the content IS the viewport. The GameBoard handles its own
pan/zoom via touch gestures, and floating panels sit at fixed
positions relative to the viewport.

## Page-Specific Behavior

### Home Page

- **Mode:** Standard
- Header: hidden (`hideHeader={true}`) — the hex grid clusters ARE
  the page content and centering looks better without a header bar
- Content: vertically centered hex grids with responsive scaling
- Nav column: collapsed (hamburger only)

### Game Page

- **Mode:** Full-screen (`fullScreen={true}`)
- No header bar, no nav column reservation
- Hamburger floats at top-left (`position: fixed`, z-index 2000)
- Content: GameBoard + FloatingPanels + MinimizedBar fill the
  entire viewport
- Content zooms independently via touch gestures
- Nav menu opens as overlay when hamburger is tapped, showing
  game-specific actions (Balance, Winner, Save Copy, Layout, Theme)

### Form Pages (New Game, Edit Players, Settings, Load Game)

- **Mode:** Standard
- Header: page title + back button
- Content: scrollable form/list content
- Nav column: collapsed

## Responsive Behavior

### Desktop (pointer: fine)

- Standard mode: nav collapsed to 48px icon strip, header bar 44px,
  content fills remaining space
- Full-screen mode: no nav/header, hamburger floats over content
- No touch-action styling needed (mouse wheel zoom handled by
  BoardViewport if present)

### Mobile / Touch (pointer: coarse)

- Standard mode: nav collapsed to 56px (larger touch target), header
  bar 52px, content scrolls
- Full-screen mode: hamburger icon at touch-friendly size (current
  90x90px), floats over full-viewport content
- Content area: `touch-action: pan-x pan-y pinch-zoom`
- Nav expansion uses overlay (Option A) to preserve content area size

### Portrait Mode

The app shell structure remains the same in portrait mode. The
difference is in what the **content area** renders:

- **Landscape:** Three-column floating panels over game board
- **Portrait:** Tabbed interface (Board / Controls / Players) — see
  [portrait-mode.md](portrait-mode.md)

The nav column and header bar are identical in both orientations.

## Relationship to Existing Systems

### FloatingPanel System

On the game page (full-screen mode), floating panels render inside
the full-viewport content area. Their `position: absolute` is
relative to the viewport — same as today. No changes to panel
positioning or `resetLayout()` computations.

On standard-mode pages, floating panels (if used) would be
contained within `app-content`, but currently only the game page
uses floating panels.

### Layout Persistence

No changes to `layoutStore` persistence. Panel positions are still
relative coordinates within the content area.

### NavMenu

NavMenu renders inside the expanded nav column instead of a separate
overlay panel. The menu overlay backdrop still appears when the nav
is expanded (to dismiss on outside click), but the menu content is
anchored in the nav column.

## Migration from Current Layout

`MainLayout` gains a `fullScreen` prop that selects the mode.

### Standard Mode (non-game pages)

The `.page` class and its children are replaced by the new grid
layout:

| Before | After |
|--------|-------|
| `div.page` | `div.app-shell` |
| `button.hamburger-btn` (fixed) | `button.nav-toggle` (inside `.app-nav`) |
| `.menu-overlay` + `.menu-panel` | NavMenu inside `.app-nav` (expanded state) |
| `main` | `main.app-content` |
| `article.content` | removed (unnecessary wrapper) |
| Manual `pt-[60px]` on pages | `app-header` provides the space naturally |

### Full-Screen Mode (game page)

Structurally similar to today:

| Before | After |
|--------|-------|
| `div.page` | `div.app-shell-fullscreen` (full viewport) |
| `button.hamburger-btn` (fixed) | Same — `position: fixed`, z-index 2000 |
| `.menu-overlay` + `.menu-panel` | Same overlay behavior |
| `main > article.content` | `main` only (remove unnecessary wrapper) |

The game page layout is largely unchanged. The hamburger continues
to float over the full-screen content.

### Backward Compatibility

During migration, both `.page` and `.app-shell` classes can coexist.
Pages are migrated one at a time. The `MainLayout` component
switches to the new structure and all pages inherit it automatically.

## Files Affected

| File | Change |
|------|--------|
| `react-ui/components/layout/MainLayout.tsx` | Add `fullScreen` prop; standard mode uses grid layout, full-screen mode keeps current structure |
| `react-ui/components/layout/NavMenu.tsx` | Render inside nav column (standard mode) or overlay (full-screen mode) |
| `react-ui/app/globals.css` | Add `.app-shell`, `.app-nav`, `.app-main`, `.app-header`, `.app-content` classes; keep `.page` for full-screen mode |
| `react-ui/app/page.tsx` | Remove `pt-[60px]`, pass `hideHeader` |
| `react-ui/app/game/[id]/page.tsx` | Pass `fullScreen={true}` to MainLayout (minimal change) |
| `react-ui/app/load-game/page.tsx` | Remove manual back button, pass title/onBack props |
| `react-ui/app/new-game/page.tsx` | Remove manual back button, pass title/onBack props |
| `react-ui/app/edit-players/page.tsx` | Remove manual back button, pass title/onBack props |
| `react-ui/app/settings/page.tsx` | Remove manual back button, pass title/onBack props |
| `.design/portrait-mode.md` | Update to reference app shell |
| `.design/floating-panel.md` | Note full-screen mode unchanged |
| `.design/css-theming.md` | Add new layout tokens |
| `.design/react-architecture.md` | Update component hierarchy |
