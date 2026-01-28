# Floating Panel Architecture

## Overview

The floating panel system provides draggable, resizable, minimizable windows for the game UI.
This design follows Windows-like window management patterns for familiar behavior and clean architecture.

## Core Data Structure

### WindowPosition

Each panel's state is captured in a `WindowPosition` structure:

```typescript
interface WindowPosition {
  /** Distance from left edge of viewport (pixels) */
  left: number;
  /** Distance from top edge of viewport (pixels) */
  top: number;
  /** Panel width (pixels) */
  width: number;
  /** Panel height (pixels) */
  height: number;
  /** Whether panel is minimized to the taskbar */
  minimized: boolean;
  /** Whether panel is visible at all */
  visible: boolean;
  /** Stacking order (higher = on top) */
  zIndex: number;
}
```

**Key Properties:**

- `left`, `top` - Absolute position from viewport edges
- Negative values supported for right/bottom anchoring (converted at render time)
- Position/size preserved when minimized (restored on expand)

### Panel Registry

```typescript
type PanelId = 'dice' | 'actions' | 'measurements' | 'players' | 'resources' | 'board' | 'goFirst';

interface LayoutState {
  panels: Record<PanelId, WindowPosition>;
  // ... other layout state
}
```

## Component Architecture

```
┌─────────────────────────────────────────────────────────────────┐
│  Page Container                                                  │
│                                                                  │
│  ┌───────────────────────────────────────────────────────────┐  │
│  │  Normal Panels Layer (z-index: 10-50)                     │  │
│  │                                                            │  │
│  │  <FloatingPanel panelId="dice">                           │  │
│  │    - Only renders when !minimized                          │  │
│  │    - Subscribes only to own WindowPosition                 │  │
│  │    - Handles drag, resize, minimize button                 │  │
│  │  </FloatingPanel>                                          │  │
│  │                                                            │  │
│  │  <FloatingPanel panelId="actions">...</FloatingPanel>     │  │
│  │  <FloatingPanel panelId="players">...</FloatingPanel>     │  │
│  │  ...                                                       │  │
│  └───────────────────────────────────────────────────────────┘  │
│                                                                  │
│  ┌───────────────────────────────────────────────────────────┐  │
│  │  MinimizedBar (fixed, z-index: 1000)                      │  │
│  │  ┌──────────┐ ┌──────────┐ ┌──────────┐                   │  │
│  │  │ 🎲 Dice  │ │ ⚡Actions│ │ 👥Players│  ← Click to expand │  │
│  │  └──────────┘ └──────────┘ └──────────┘                   │  │
│  │  - Single component subscribes to all minimized panels    │  │
│  │  - Calculates positions for minimized items               │  │
│  │  - Renders simple clickable buttons                       │  │
│  └───────────────────────────────────────────────────────────┘  │
└─────────────────────────────────────────────────────────────────┘
```

### Separation of Concerns

| Component | Responsibility | Subscribes To |
|-----------|---------------|---------------|
| `FloatingPanel` | Drag, resize, minimize button, content | Own `WindowPosition` only |
| `MinimizedBar` | Render all minimized panels in a row | All panels (for minimized state) |

**Why This Matters:**

- FloatingPanel doesn't need to know about other panels
- No cross-panel dependencies = no infinite render loops
- MinimizedBar is a single component that handles the array subscription once

## Store Design

### Actions

```typescript
interface LayoutActions {
  /** Update panel position (preserves other fields) */
  setPanelPosition: (panelId: PanelId, left: number, top: number) => void;

  /** Update panel size (preserves other fields) */
  setPanelSize: (panelId: PanelId, width: number, height: number) => void;

  /** Toggle minimized state */
  toggleMinimize: (panelId: PanelId) => void;

  /** Set minimized state explicitly */
  setMinimized: (panelId: PanelId, minimized: boolean) => void;

  /** Show/hide panel */
  setPanelVisible: (panelId: PanelId, visible: boolean) => void;

  /** Bring panel to front (increase zIndex) */
  bringToFront: (panelId: PanelId) => void;

  /** Reset all panels to defaults */
  resetLayout: () => void;
}
```

### Selectors

```typescript
/** Select a single panel's WindowPosition */
export const selectPanel = (panelId: PanelId) =>
  (state: LayoutStore): WindowPosition => state.panels[panelId];

/** Select all minimized panel IDs (for MinimizedBar) */
export const selectMinimizedPanelIds = (state: LayoutStore): PanelId[] =>
  PANEL_ORDER.filter(id => state.panels[id]?.minimized && state.panels[id]?.visible);

/** Select panel metadata for minimized bar (id, title, icon) */
export const selectMinimizedPanels = (state: LayoutStore): MinimizedPanelInfo[] =>
  PANEL_ORDER
    .filter(id => state.panels[id]?.minimized && state.panels[id]?.visible)
    .map(id => ({ id, ...PANEL_METADATA[id] }));
```

## Component Specifications

### FloatingPanel

```typescript
interface FloatingPanelProps {
  panelId: PanelId;
  title: string;
  icon?: ReactNode;
  children: ReactNode;
  className?: string;
  style?: React.CSSProperties;
  minWidth?: number;
  minHeight?: number;
  enableBackgroundDrag?: boolean;
}
```

**Behavior:**

1. Subscribes to `selectPanel(panelId)` - only its own state
2. Returns `null` if `minimized` or `!visible`
3. Handles:
   - CTRL+click drag (desktop)
   - Long press drag (mobile)
   - Background drag (optional)
   - Corner resize handle
   - Minimize button (top-right)
4. On minimize: calls `toggleMinimize(panelId)`, does NOT render minimized state

### MinimizedBar

```typescript
interface MinimizedBarProps {
  /** Optional className for the container */
  className?: string;
}
```

**Behavior:**

1. Subscribes to `selectMinimizedPanels` with shallow comparison
2. Renders fixed bar at bottom of viewport
3. Each minimized panel shown as clickable button with icon + title
4. Click expands panel: calls `toggleMinimize(panelId)`
5. Supports CTRL+drag to reposition (optional, future)

**Layout:**

```
┌─────────────────────────────────────────────────────────────┐
│                                                              │
│                      (game content)                          │
│                                                              │
├──────────────────────────────────────────────────────────────┤
│ [🎲 Dice] [⚡ Actions] [📊 Measurements]        ← 12px from bottom
│ └─ 12px gap ─┘                                   └─ 12px from left
└──────────────────────────────────────────────────────────────┘
```

## Re-render Optimization

### Problem Solved

Previous design had FloatingPanel subscribe to all minimized panels to calculate its
position in the minimized bar. This caused:

1. Every panel re-rendered when ANY panel's minimized state changed
2. Array selector returned new array each time → infinite loop

### Solution

1. **FloatingPanel only subscribes to own state** - no array dependencies
2. **MinimizedBar handles array subscription once** - single point of array comparison
3. **Shallow comparison in MinimizedBar** - prevents unnecessary re-renders

```typescript
// MinimizedBar - single subscription with shallow comparison
const minimizedPanels = useLayoutStore(selectMinimizedPanels, shallow);

// FloatingPanel - only own state, primitive comparison
const panel = useLayoutStore(state => state.panels[panelId]);
```

## Persistence

WindowPosition is persisted to localStorage via Zustand persist middleware:

```typescript
persist(
  (set, get) => ({ ... }),
  {
    name: 'catan-layout',
    version: 6, // Bump for WindowPosition migration
    partialize: (state) => ({
      panels: state.panels,
      // ... other persisted state
    }),
  }
)
```

## Migration from PanelLayout

| Old (PanelLayout) | New (WindowPosition) |
|-------------------|---------------------|
| `position: { x, y }` | `left`, `top` |
| `size: { width, height }` | `width`, `height` |
| `minimized` | `minimized` |
| `visible` | `visible` |
| `zIndex` | `zIndex` |

Migration is straightforward - flatten nested objects to direct properties.

## File Structure

```
react-ui/
├── components/game/panels/
│   ├── FloatingPanel.tsx      # Individual panel (drag, resize, content)
│   ├── MinimizedBar.tsx       # Bottom bar for minimized panels (NEW)
│   └── index.ts               # Exports
├── lib/stores/
│   └── layoutStore.ts         # WindowPosition type, store, selectors
```

## Usage Example

```tsx
// In game page
export default function GamePage() {
  return (
    <div className="relative w-full h-screen">
      {/* Normal panels - each manages itself */}
      <FloatingPanel panelId="dice" title="Dice" icon={<DiceIcon />}>
        <DiceCluster />
      </FloatingPanel>

      <FloatingPanel panelId="actions" title="Actions" icon={<BoltIcon />}>
        <ActionCluster />
      </FloatingPanel>

      <FloatingPanel panelId="players" title="Players" icon={<UsersIcon />}>
        <PlayersPanel />
      </FloatingPanel>

      {/* Minimized bar - renders all minimized panels */}
      <MinimizedBar />
    </div>
  );
}
```

## Benefits

1. **No infinite loops** - No cross-panel dependencies in FloatingPanel
2. **Predictable re-renders** - Each component subscribes to exactly what it needs
3. **Windows-like UX** - Familiar minimize/restore behavior
4. **Clean separation** - FloatingPanel handles normal state, MinimizedBar handles minimized
5. **Easy persistence** - WindowPosition is a flat, serializable structure
6. **Testable** - Components can be tested in isolation
