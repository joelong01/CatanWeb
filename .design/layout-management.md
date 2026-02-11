# Layout Management System

**Status:** Design (not yet implemented)

## Overview

Adds named layout presets, viewport-aware reset, and right-click
context menus on minimized panels. Replaces the single "Reset Layout"
button in the NavMenu with a full Layout submenu.

## Current State

The layout system today:

- **layoutStore** persists 9 panel positions to `localStorage`
  under key `catan-layout` (version 7)
- **LANDSCAPE_PANELS / PORTRAIT_PANELS** provide hardcoded default
  positions designed for 1920x1080
- **NavMenu** has one "Reset Layout" button that calls `resetLayout()`
- **MinimizedBar** renders minimized panels as click-to-restore buttons
  with no context menu
- No concept of named layout presets
- No way to recover off-screen panels except full reset
- Reset snaps to hardcoded 1920x1080 positions regardless of actual
  screen size

## Data Model

### SavedLayout

```typescript
interface SavedLayout {
  name: string;
  panels: Record<PanelId, WindowPosition>;
  createdAt: string;  // ISO 8601
  updatedAt: string;  // ISO 8601
}
```

### layoutStore Additions

```typescript
// New state
savedLayouts: SavedLayout[];
currentLayoutName: string | null;  // null = unnamed/modified

// New actions
saveLayout: (name: string) => void;
loadLayout: (name: string) => void;
deleteLayout: (name: string) => void;
renameLayout: (oldName: string, newName: string) => void;
minimizeAll: () => void;
resetPanel: (panelId: PanelId) => void;
```

The existing `resetLayout()` action is replaced with a
viewport-aware version (see [Reset Algorithm](#reset-algorithm)).

`savedLayouts` is persisted alongside `panels` via the existing
Zustand `partialize` config. The `currentLayoutName` tracks whether
the active layout has a name (for Save vs Save As behavior).

When the user modifies any panel position after loading a named
layout, `currentLayoutName` remains set (Save overwrites that slot).
The name only clears on Reset All or loading a different layout.

### Persistence

Saved layouts share the existing `catan-layout` localStorage key.
The persist `partialize` function adds:

```typescript
partialize: (state) => ({
  boardType: state.boardType,
  panels: state.panels,
  viewport: state.viewport,
  version: state.version,
  savedLayouts: state.savedLayouts,
  currentLayoutName: state.currentLayoutName,
})
```

Bump `version` to 8 with a migration that adds `savedLayouts: []`
and `currentLayoutName: null`.

## Menu Structure

The NavMenu "Layout" button replaces "Reset Layout". Clicking it
toggles an expandable section in the sidebar with these sub-items:

```text
[Layout]                     <- top-level button (toggles section)
  |-- Minimize All           <- minimizes all 9 panels
  |-- Reset >                <- expands sub-list
  |   |-- All
  |   |-- Dice
  |   |-- Actions
  |   |-- Board Stats
  |   |-- Players
  |   |-- Resources
  |   |-- Board
  |   |-- Go First
  |   |-- Supplemental
  |   +-- Winner
  |-- Save                   <- saves to current name, or acts as Save As
  |-- Save As...             <- opens name prompt
  +-- Open >                 <- expands saved layout list
      |-- Landscape 4K
      |-- Compact
      +-- (etc.)
```

### Why No Per-Panel Minimize in Menu

Every panel already has a minimize button (`-` in its top-right
corner). Adding per-panel minimize to the menu would duplicate that.
The menu provides "Minimize All" as the only minimize action since
that is the one operation the per-panel buttons cannot do.

### Menu Behavior

| Action | Effect |
|--------|--------|
| **Minimize All** | Minimizes all 9 panels to the MinimizedBar. |
| **Reset > All** | Computes orientation-aware positions for the current viewport and applies them. Un-minimizes all panels. Clears `currentLayoutName`. |
| **Reset > (panel)** | Computes the default position for that single panel based on current viewport and applies it. Un-minimizes it. |
| **Save** | If `currentLayoutName` is set, overwrites that saved layout. Otherwise acts as Save As. |
| **Save As...** | Opens a text input prompt. User enters a name. Saves all panel positions under that name. Sets `currentLayoutName`. |
| **Open > (name)** | Loads the saved layout. Sets `currentLayoutName`. |

Each menu action closes the NavMenu after executing (existing
`onMenuAction()` pattern).

## Reset Algorithm

Reset computes panel positions from the actual viewport dimensions
rather than using hardcoded constants. This means Reset always
produces a good layout regardless of screen size -- 1080p laptop,
4K desktop, portrait tablet, etc.

### Inputs

```typescript
const vw = window.innerWidth;
const vh = window.innerHeight;
const isPortrait = vh > vw;
```

### Landscape Layout

Three-column layout matching the Blazor reference:

```text
+----------+------------------+-----------+
| Left col |     Center       | Right col |
| (25%)    |     (50%)        | (25%)     |
|          |                  |           |
| Dice     |     Board        | Resources |
| Actions  |                  | Players   |
| Stats    |                  |           |
+----------+------------------+-----------+
```

- **Left column** (x: 0..25% of vw): Dice, Actions, Measurements
  stacked vertically with gap
- **Center** (x: 25%..75% of vw): Board fills available space
- **Right column** (x: 75%..100% of vw): Resources, Players
  stacked vertically
- **Modal overlays** (goFirst, supplemental, winner): Centered
  over the board area
- All panels un-minimized, visible
- Margin/padding: 16px from edges, 8px between panels

### Portrait Layout

Vertical stack with board prominent:

```text
+-------------------------+
|         Board           |
|         (60% height)    |
+-------------------------+
|   Players               |
+-------------------------+
| Dice    | Actions       |
+-------------------------+
| Stats (minimized)       |
| Resources (minimized)   |
+-------------------------+
```

- Board takes top 60% of viewport height
- Players below board
- Dice and Actions side-by-side below players
- Measurements and Resources minimized to reduce clutter
- Modal overlays centered

### Position Calculation

Pure functions that compute positions from viewport dimensions:

```typescript
function computeLandscape(vw: number, vh: number): Record<PanelId, WindowPosition>;
function computePortrait(vw: number, vh: number): Record<PanelId, WindowPosition>;
function computePanelDefault(panelId: PanelId, vw: number, vh: number): WindowPosition;
```

These are exported for testing. No side effects, no window access
inside -- the caller passes dimensions in.

### Hardcoded Constants

The existing `LANDSCAPE_PANELS` and `PORTRAIT_PANELS` constants
remain as the `initialState` for SSR / first hydration (before
`window` is available in Next.js). After hydration, localStorage
takes over. Reset always uses the computed functions, never the
hardcoded constants.

## MinimizedBar Context Menu

Right-click (desktop) or long-press (mobile) on a minimized panel
button opens a small context menu:

```text
+-----------+
| Restore   |
| Save      |
| Open >    |
| Reset     |
+-----------+
```

| Action | Effect |
|--------|--------|
| **Restore** | Same as clicking the button (toggleMinimize). |
| **Save** | Same as Layout > Save (saves current layout). |
| **Open** | Shows saved layout names as sub-items. Loads on click. |
| **Reset** | Computes this panel's default position for current viewport and restores it (un-minimizes). |

### Implementation

Add a `ContextMenu` component rendered as a positioned `<div>`
with `position: fixed` at the cursor/touch point. Dismiss on click
outside or Escape key.

MinimizedBar adds `onContextMenu` (desktop) and long-press detection
(mobile, reuse the 400ms timer pattern from FloatingPanel) to each
panel button.

The context menu state lives in MinimizedBar's local state:

```typescript
const [contextMenu, setContextMenu] = useState<{
  panelId: PanelId;
  x: number;
  y: number;
} | null>(null);
```

## Save As Dialog

A modal overlay for entering a layout name. Appears centered
over the game area.

```text
+---------------------------+
|  Save Layout As           |
|                           |
|  Name: [______________]   |
|                           |
|  [Cancel]  [Save]         |
+---------------------------+
```

- Pre-fills with `currentLayoutName` if set
- Validates: non-empty, no duplicate names (or confirm overwrite)
- On save: calls `saveLayout(name)`, closes dialog
- Dismisses on Escape or Cancel

This is a simple controlled dialog -- no new FloatingPanel needed.
Render as a fixed overlay with backdrop.

## NavMenuItem Changes

The existing `NavMenuItem` renders `icon + label` as a button.
The Layout section needs two additions:

1. **Expandable parent item** -- click toggles a nested list.
   Use local state in NavMenu (`expandedSection: 'reset' | 'open'
   | null`).

2. **Indented child items** -- same `NavMenuItem` component but with
   an `indent` prop or a wrapper `<div>` with left padding.

No new component files needed for the menu itself -- the expansion
logic lives in NavMenu.

## New Store Actions Detail

### `resetLayout()` (replaces existing)

```text
1. Read window.innerWidth, window.innerHeight
2. Determine orientation
3. Compute positions via computeLandscape() or computePortrait()
4. Set all panels to computed positions (un-minimized, visible)
5. Clear currentLayoutName
```

### `resetPanel(panelId: PanelId)`

```text
1. Read window.innerWidth, window.innerHeight
2. Determine orientation
3. Compute this panel's position via computePanelDefault()
4. Replace this panel's WindowPosition, set minimized = false
```

### `saveLayout(name: string)`

```text
1. Snapshot current panels as SavedLayout
2. If name already exists in savedLayouts, overwrite it (update updatedAt)
3. If new name, append to savedLayouts
4. Set currentLayoutName = name
```

### `loadLayout(name: string)`

```text
1. Find layout by name in savedLayouts
2. Apply panels to state
3. Set currentLayoutName = name
```

### `deleteLayout(name: string)`

```text
1. Remove from savedLayouts array
2. If currentLayoutName === name, set to null
```

### `minimizeAll()`

```text
1. For every panel (all 9):
   set minimized = true
```

## Files Modified

| File | Action |
|------|--------|
| `react-ui/lib/stores/layoutStore.ts` | Add SavedLayout type, new state fields, new actions, computed layout functions, version 8 migration. Replace hardcoded resetLayout with viewport-aware version. |
| `react-ui/components/layout/NavMenu.tsx` | Replace "Reset Layout" with expandable Layout section |
| `react-ui/components/game/panels/MinimizedBar.tsx` | Add right-click context menu |
| `react-ui/components/game/panels/ContextMenu.tsx` | NEW -- small positioned menu component |
| `react-ui/components/game/panels/SaveLayoutDialog.tsx` | NEW -- name prompt modal |
| `react-ui/lib/stores/stores.test.ts` | Add tests for new layoutStore actions and computed layout functions |
| `.design/floating-panel.md` | Update panel registry and version number |

## Verification

1. `npx next build` -- no type errors
2. `npx vitest run react-ui/lib/stores/stores.test.ts` -- tests pass
3. NavMenu: Layout section expands/collapses correctly
4. Minimize All minimizes all 9 panels
5. Reset > All computes positions for current window, all panels visible
6. Reset on 1920x1080 produces a reasonable 3-column layout
7. Reset on 2560x1440 uses the extra space (wider columns, taller panels)
8. Reset on narrow portrait window stacks vertically
9. Reset > (panel) restores single panel to computed default
10. Save As prompts for name, appears in Open list
11. Open loads saved layout correctly
12. Save overwrites current named layout
13. Right-click minimized panel shows context menu
14. Context menu Restore/Save/Reset work correctly
15. Saved layouts persist across page reload
