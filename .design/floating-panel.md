# Floating Panel System

**Last verified:** January 31, 2026

## Overview

The floating panel system provides draggable, resizable, minimizable
windows for game UI elements. It follows Windows-like window management
patterns with position persistence via localStorage.

## Architecture

```
Page Container
├── FloatingPanel (per panel, z-index 10-50)
│   ├── Title bar (drag handle, minimize button)
│   ├── Content area (children)
│   └── Resize handle (corner)
├── MinimizedBar (fixed, z-index 1000)
│   ├── Clickable buttons for each minimized panel
│   └── Right-click / long-press context menu
├── ContextMenu (generic positioned menu)
└── SaveLayoutDialog (name prompt modal)
```

**Key separation:** FloatingPanel only manages its own state.
MinimizedBar manages the minimized panel array. This prevents
cross-panel dependencies and infinite render loops.

## WindowPosition

Each panel's state is a flat structure:

```typescript
interface WindowPosition {
  left: number;       // Pixels from left edge
  top: number;        // Pixels from top edge
  width: number;      // Panel width
  height: number;     // Panel height
  minimized: boolean; // Collapsed to MinimizedBar
  visible: boolean;   // Whether panel renders at all
  zIndex: number;     // Stacking order
}
```

Negative `left`/`top` values supported for right/bottom anchoring
(converted at render time).

## Panel Registry

```typescript
type PanelId =
  | 'dice'
  | 'actions'
  | 'measurements'
  | 'players'
  | 'resources'
  | 'board'
  | 'goFirst'
  | 'supplemental'
  | 'winner';
```

All panels are registered in `PANEL_ORDER` and `PANEL_METADATA`
(title + emoji) for automatic inclusion in MinimizedBar, Reset
submenu, and layout management.

## Store (layoutStore)

**File:** `react-ui/lib/stores/layoutStore.ts`

### State

```typescript
interface LayoutState {
  panels: Record<PanelId, WindowPosition>;
  viewport: ViewportState;
  starFilter: number | null;
  resourceFilter: string | null;
  savedLayouts: SavedLayout[];
  currentLayoutName: string | null;
  version: number; // Currently 8
}
```

### Actions

| Action | Purpose |
|--------|---------|
| `setPanelPosition(panelId, left, top)` | Move panel |
| `setPanelSize(panelId, width, height)` | Resize panel |
| `toggleMinimize(panelId)` | Toggle minimize state |
| `setPanelVisible(panelId, visible)` | Show/hide panel |
| `bringToFront(panelId)` | Increase zIndex |
| `resetLayout()` | Viewport-aware reset of all panels |
| `resetPanel(panelId)` | Reset single panel to computed position |
| `minimizeAll()` | Minimize all 9 panels |
| `saveLayout(name)` | Save current positions as named layout |
| `loadLayout(name)` | Load a saved layout by name |
| `deleteLayout(name)` | Delete a saved layout |

### Computed Layout Functions

Three pure exported functions compute positions from viewport
dimensions (no `window` access, fully testable):

- **`computeLandscape(vw, vh)`** - Three-column layout (controls |
  board | info) with modal overlays centered over board
- **`computePortrait(vw, vh)`** - Vertical stack (board | players |
  dice+actions) with stats/resources minimized
- **`computePanelDefault(panelId, vw, vh)`** - Dispatches to
  landscape or portrait based on aspect ratio

`resetLayout()` and `resetPanel()` call these functions with
`window.innerWidth`/`innerHeight` at runtime (SSR fallback:
1920x1080).

### Selectors

```typescript
// FloatingPanel subscribes to own state only
const panel = useLayoutStore(state => state.panels[panelId]);

// MinimizedBar derives array from panels object
const minimizedPanels = useMemo(() =>
  PANEL_ORDER
    .filter(id => panels[id]?.minimized && panels[id]?.visible)
    .map(id => ({ id, title, icon })),
  [panels]
);
```

## FloatingPanel Component

**File:** `react-ui/components/game/panels/FloatingPanel.tsx`

| Props | Purpose |
|-------|---------|
| `panelId` | Panel identifier |
| `title` | Title bar text |
| `icon` | Optional title bar icon |
| `children` | Panel content |
| `minWidth` / `minHeight` | Minimum dimensions |
| `enableBackgroundDrag` | Allow drag from content area |

**Behavior:**

1. Subscribes to `selectPanel(panelId)` -- only its own state
2. Returns `null` if minimized or not visible
3. Handles CTRL+click drag (desktop), long press drag (mobile)
4. Corner resize handle
5. Minimize button calls `toggleMinimize(panelId)`

## MinimizedBar Component

**File:** `react-ui/components/game/panels/MinimizedBar.tsx`

Fixed bar at bottom of viewport. Renders clickable buttons for each
minimized panel with icon and title.

- **Click** expands the panel
- **Right-click** (desktop) or **long-press** (mobile) opens a
  context menu with Restore, Save Layout, and Reset Panel options

```
┌──────────────────────────────────────────────────────┐
│                    (game content)                      │
├──────────────────────────────────────────────────────┤
│ [Dice] [Actions] [Measurements]         <- 12px from bottom
└──────────────────────────────────────────────────────┘
```

## Layout Management (NavMenu)

The NavMenu provides a Layout section (Game page and Tests page)
with expandable sub-items:

- **Minimize All** - Minimizes all 9 panels
- **Reset** (expandable) - All + per-panel reset to computed
  viewport-aware positions
- **Save** - Overwrites current named layout (or opens Save As)
- **Save As...** - Opens SaveLayoutDialog to name the layout
- **Open** (expandable) - Lists saved layouts to load

## Persistence

Panel positions are persisted to `localStorage` via Zustand persist
middleware:

```typescript
persist(storeCreator, {
  name: 'catan-layout',
  version: 8,
  partialize: (state) => ({
    panels, viewport, boardType, version,
    savedLayouts, currentLayoutName,
  }),
})
```

Version is bumped when the schema changes to trigger migration.
Current migration chain: v<6 -> v8, v6 -> v8, v7 -> v8.

## Default Layouts

Two sets of static defaults exist (`LANDSCAPE_PANELS` and
`PORTRAIT_PANELS`) for SSR initial state only. At runtime,
`resetLayout()` computes positions from the actual viewport using
`computeLandscape()` or `computePortrait()`.

## Re-render Optimization

**Problem solved:** Previous design had FloatingPanel subscribe to all
minimized panels to calculate its position in the minimized bar. Every
panel re-rendered when ANY panel's minimized state changed.

**Solution:**

1. FloatingPanel only subscribes to own state (no array dependencies)
2. MinimizedBar derives array with `useMemo` from the panels object
3. No cross-panel dependencies in FloatingPanel

## Adding a New Panel

Minimal changes to make a new panel fully layout-aware:

1. **`layoutStore.ts`** - Add ID to `PanelId` union, `PANEL_METADATA`,
   and `PANEL_ORDER`
2. **`layoutStore.ts`** - Add default positions in `LANDSCAPE_PANELS`
   and `PORTRAIT_PANELS`
3. **`layoutStore.ts`** - Add positions in `computeLandscape()` and
   `computePortrait()`. For modals, add entry to `MODAL_HEIGHTS`
4. **`layoutStore.ts`** - Bump `version` and add migration case
5. **Page/component** - Wrap content in `<FloatingPanel panelId="..." title="...">`

No changes needed in NavMenu, MinimizedBar, ContextMenu, or
SaveLayoutDialog -- they derive panel lists from `PANEL_ORDER`
and `PANEL_METADATA` dynamically.
