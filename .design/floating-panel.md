# Floating Panel System

**Last verified:** January 30, 2026

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
└── MinimizedBar (fixed, z-index 1000)
    └── Clickable buttons for each minimized panel
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
  | 'goFirst';
```

## Store (layoutStore)

**File:** `react-ui/lib/stores/layoutStore.ts`

### State

```typescript
interface LayoutState {
  panels: Record<PanelId, WindowPosition>;
}
```

### Actions

| Action | Purpose |
|--------|---------|
| `setPanelPosition(panelId, left, top)` | Move panel |
| `setPanelSize(panelId, width, height)` | Resize panel |
| `toggleMinimize(panelId)` | Toggle minimize state |
| `setMinimized(panelId, minimized)` | Set minimize explicitly |
| `setPanelVisible(panelId, visible)` | Show/hide panel |
| `bringToFront(panelId)` | Increase zIndex |
| `resetLayout()` | Reset all panels to defaults |

### Selectors

```typescript
// FloatingPanel subscribes to own state only
const panel = useLayoutStore(state => state.panels[panelId]);

// MinimizedBar subscribes to all minimized panels with shallow compare
const minimizedPanels = useLayoutStore(selectMinimizedPanels, shallow);
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
minimized panel with icon and title. Click expands the panel.

```
┌──────────────────────────────────────────────────────┐
│                    (game content)                      │
├──────────────────────────────────────────────────────┤
│ [Dice] [Actions] [Measurements]         ← 12px from bottom
└──────────────────────────────────────────────────────┘
```

## Persistence

Panel positions are persisted to `localStorage` via Zustand persist
middleware:

```typescript
persist(storeCreator, {
  name: 'catan-layout',
  version: 6,
  partialize: (state) => ({ panels: state.panels }),
})
```

Version is bumped when the WindowPosition schema changes to trigger
migration.

## Default Layouts

The store provides different default panel positions for `regular`
(3-4 player) and `expansion` (5-6 player) game types, adjusting
panel sizes and positions for the different board dimensions.

## Re-render Optimization

**Problem solved:** Previous design had FloatingPanel subscribe to all
minimized panels to calculate its position in the minimized bar. Every
panel re-rendered when ANY panel's minimized state changed.

**Solution:**

1. FloatingPanel only subscribes to own state (no array dependencies)
2. MinimizedBar handles array subscription once with shallow comparison
3. No cross-panel dependencies in FloatingPanel
