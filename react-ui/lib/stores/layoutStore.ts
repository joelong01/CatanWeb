/**
 * Layout store - manages floating panel positions and viewport state.
 *
 * Uses persist middleware to save layout to localStorage.
 * Supports different layouts per board type (regular vs expansion).
 * Provides portrait and landscape default layouts matching Blazor.
 */

import { create } from 'zustand';
import { persist } from 'zustand/middleware';

/** Board types with different default layouts */
export type BoardType = 'regular' | 'expansion';

/**
 * Window-like position structure for panels.
 * Simple absolute positioning - user controls placement via drag.
 */
export interface WindowPosition {
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
  /** Stacking order (higher = on top). Board should be lowest. */
  zIndex: number;
}

/** @deprecated Use WindowPosition instead */
export type PanelLayout = WindowPosition;

/** Viewport state for pan/zoom */
export interface ViewportState {
  pan: { x: number; y: number };
  zoom: number;
}

/** Panel IDs */
export type PanelId =
  | 'dice'
  | 'actions'
  | 'measurements'
  | 'players'
  | 'resources'
  | 'board'
  | 'goFirst'
  | 'supplemental';

/** Panel metadata for MinimizedBar display */
export interface PanelMetadata {
  title: string;
  icon?: string; // Emoji or icon identifier
}

/** Metadata for each panel type */
export const PANEL_METADATA: Record<PanelId, PanelMetadata> = {
  dice: { title: 'Dice', icon: '🎲' },
  actions: { title: 'Actions', icon: '⚡' },
  measurements: { title: 'Board Stats', icon: '📊' },
  players: { title: 'Players', icon: '👥' },
  resources: { title: 'Resources', icon: '📦' },
  board: { title: 'Board', icon: '🗺️' },
  goFirst: { title: 'Go First', icon: '🏁' },
  supplemental: { title: 'Supplemental', icon: '🔨' },
};

/** Panel order for minimized bar (consistent ordering) */
export const PANEL_ORDER: PanelId[] = ['dice', 'actions', 'measurements', 'players', 'resources', 'board', 'goFirst', 'supplemental'];

/**
 * Landscape default panel layouts (matching Blazor 3-column layout)
 *
 * Blazor layout: left (320px) | center (1fr) | right (530px) at 1920x1080
 * - Left column: Dice clusters, Actions, Measurements
 * - Center: Game board
 * - Right column: Resources, Players
 *
 * Absolute positions for a typical 1920x1080 viewport.
 * User can freely reposition panels; use Reset to restore defaults.
 */
const LANDSCAPE_PANELS: Record<PanelId, WindowPosition> = {
  dice: {
    left: 60,
    top: 80,
    width: 260,
    height: 200,
    minimized: false,
    visible: true,
    zIndex: 20,
  },
  actions: {
    left: 60,
    top: 300,
    width: 200,
    height: 200,
    minimized: false,
    visible: true,
    zIndex: 21,
  },
  measurements: {
    left: 60,
    top: 520,
    width: 280,
    height: 130,
    minimized: false,
    visible: true,
    zIndex: 22,
  },
  board: {
    left: 340,
    top: 80,
    width: 600,
    height: 550,
    minimized: false,
    visible: true,
    zIndex: 10, // Board is lowest - other panels float on top
  },
  resources: {
    left: 960,
    top: 80,
    width: 340,
    height: 130,
    minimized: false,
    visible: true,
    zIndex: 23,
  },
  players: {
    left: 960,
    top: 230,
    width: 340,
    height: 450,
    minimized: false,
    visible: true,
    zIndex: 24,
  },
  goFirst: {
    left: 500,
    top: 200, // Centered over the board area
    width: 320,
    height: 300,
    minimized: false,
    visible: true,
    zIndex: 1000, // Very high z-index - modal overlay must be on top of everything
  },
  supplemental: {
    left: 500,
    top: 200, // Centered over the board area
    width: 320,
    height: 340, // Slightly taller to accommodate Next button
    minimized: false,
    visible: true,
    zIndex: 1000, // Very high z-index - modal overlay must be on top of everything
  },
};

/**
 * Portrait default panel layouts (matching Blazor tabbed layout concept)
 *
 * In portrait mode, Blazor uses tabs to switch between views.
 * For floating panels, we stack them vertically with board prominent.
 *
 * Layout concept:
 * - Board takes center/top area (main focus)
 * - Players panel below board
 * - Controls (dice, actions) at bottom
 * - Some panels minimized by default to reduce clutter
 */
const PORTRAIT_PANELS: Record<PanelId, WindowPosition> = {
  board: {
    left: 20,
    top: 60,
    width: 400,
    height: 400,
    minimized: false,
    visible: true,
    zIndex: 10, // Board is lowest - other panels float on top
  },
  players: {
    left: 20,
    top: 480,
    width: 400,
    height: 300,
    minimized: false,
    visible: true,
    zIndex: 24,
  },
  dice: {
    left: 20,
    top: 800,
    width: 200,
    height: 150,
    minimized: false,
    visible: true,
    zIndex: 20,
  },
  actions: {
    left: 240,
    top: 800,
    width: 180,
    height: 150,
    minimized: false,
    visible: true,
    zIndex: 21,
  },
  measurements: {
    left: 20,
    top: 970,
    width: 400,
    height: 100,
    minimized: true,
    visible: true,
    zIndex: 22,
  },
  resources: {
    left: 20,
    top: 20,
    width: 300,
    height: 40,
    minimized: true,
    visible: true,
    zIndex: 23,
  },
  goFirst: {
    left: 40,
    top: 200, // Centered on typical portrait screen
    width: 320,
    height: 300,
    minimized: false,
    visible: true,
    zIndex: 1000, // Very high z-index - modal overlay must be on top of everything
  },
  supplemental: {
    left: 40,
    top: 200, // Centered over the board area
    width: 320,
    height: 340, // Slightly taller to accommodate Next button
    minimized: false,
    visible: true,
    zIndex: 1000, // Very high z-index - modal overlay must be on top of everything
  },
};

/** Default viewport state */
const DEFAULT_VIEWPORT: ViewportState = {
  pan: { x: 0, y: 0 },
  zoom: 1,
};

interface LayoutState {
  /** Current board type */
  boardType: BoardType;

  /** Panel layouts (WindowPosition for each panel) */
  panels: Record<PanelId, WindowPosition>;

  /** Viewport state */
  viewport: ViewportState;

  /** Star filter threshold (8-13 or null) */
  starFilter: number | null;

  /** Resource filter (resource type or null) */
  resourceFilter: string | null;

  /** Layout version for migrations */
  version: number;
}

interface LayoutActions {
  /** Set board type and load appropriate layout */
  setBoardType: (boardType: BoardType) => void;

  /** Update panel position */
  setPanelPosition: (panelId: PanelId, left: number, top: number) => void;

  /** Update panel size */
  setPanelSize: (panelId: PanelId, width: number, height: number) => void;

  /** Toggle panel minimized state */
  toggleMinimize: (panelId: PanelId) => void;

  /** Set panel visibility */
  setPanelVisible: (panelId: PanelId, visible: boolean) => void;

  /** Bring panel to front (update z-index tracking) */
  bringToFront: (panelId: PanelId) => void;

  /** Update viewport state */
  setViewport: (viewport: Partial<ViewportState>) => void;

  /** Set star filter */
  setStarFilter: (stars: number | null) => void;

  /** Set resource filter */
  setResourceFilter: (resource: string | null) => void;

  /** Reset layout to landscape defaults */
  resetToLandscape: () => void;

  /** Reset layout to portrait defaults */
  resetToPortrait: () => void;

  /** Reset layout based on current viewport orientation */
  resetLayout: () => void;
}

export type LayoutStore = LayoutState & LayoutActions;

/**
 * Detects if viewport is portrait orientation
 */
function isPortraitViewport(): boolean {
  if (typeof window === 'undefined') return false;
  return window.innerHeight > window.innerWidth;
}

const initialState: LayoutState = {
  boardType: 'regular',
  panels: { ...LANDSCAPE_PANELS },
  viewport: { ...DEFAULT_VIEWPORT },
  starFilter: null,
  resourceFilter: null,
  version: 7, // Bumped version for WindowPosition migration
};

/**
 * Migrate old PanelLayout format to new WindowPosition format
 */
function migratePanel(old: unknown): WindowPosition {
  // Handle old format with nested position/size objects
  const oldPanel = old as {
    position?: { x: number; y: number };
    size?: { width: number; height: number };
    left?: number;
    top?: number;
    width?: number;
    height?: number;
    minimized?: boolean;
    visible?: boolean;
    zIndex?: number;
  };

  // Check if it's already in new format
  if (typeof oldPanel.left === 'number' && typeof oldPanel.width === 'number') {
    return oldPanel as WindowPosition;
  }

  // Convert from old format
  return {
    left: oldPanel.position?.x ?? 100,
    top: oldPanel.position?.y ?? 100,
    width: oldPanel.size?.width ?? 300,
    height: oldPanel.size?.height ?? 200,
    minimized: oldPanel.minimized ?? false,
    visible: oldPanel.visible ?? true,
    zIndex: oldPanel.zIndex ?? 20,
  };
}

/**
 * Zustand store for layout state.
 * Uses persist middleware to save to localStorage per board type.
 */
export const useLayoutStore = create<LayoutStore>()(
  persist(
    (set) => ({
      ...initialState,

      setBoardType: (boardType) => {
        set({ boardType });
      },

      setPanelPosition: (panelId, left, top) => {
        console.log(`[layoutStore] setPanelPosition called: panelId=${panelId}, left=${left}, top=${top}`);
        set((state) => ({
          panels: {
            ...state.panels,
            [panelId]: {
              ...state.panels[panelId],
              left,
              top,
            },
          },
        }));
      },

      setPanelSize: (panelId, width, height) => {
        set((state) => ({
          panels: {
            ...state.panels,
            [panelId]: {
              ...state.panels[panelId],
              width,
              height,
            },
          },
        }));
      },

      toggleMinimize: (panelId) => {
        set((state) => ({
          panels: {
            ...state.panels,
            [panelId]: {
              ...state.panels[panelId],
              minimized: !state.panels[panelId].minimized,
            },
          },
        }));
      },

      setPanelVisible: (panelId, visible) => {
        set((state) => ({
          panels: {
            ...state.panels,
            [panelId]: {
              ...state.panels[panelId],
              visible,
            },
          },
        }));
      },

      bringToFront: (panelId) => {
        set((state) => {
          // Don't change board z-index - it always stays at the bottom
          if (panelId === 'board') return state;

          // Find max z-index among non-board panels
          const maxZ = Math.max(
            ...Object.entries(state.panels)
              .filter(([id]) => id !== 'board')
              .map(([, p]) => p.zIndex)
          );

          // Only update if this panel isn't already on top
          if (state.panels[panelId].zIndex >= maxZ) return state;

          return {
            panels: {
              ...state.panels,
              [panelId]: {
                ...state.panels[panelId],
                zIndex: maxZ + 1,
              },
            },
          };
        });
      },

      setViewport: (viewport) => {
        set((state) => ({
          viewport: {
            ...state.viewport,
            ...viewport,
          },
        }));
      },

      setStarFilter: (starFilter) => set({ starFilter }),

      setResourceFilter: (resourceFilter) => set({ resourceFilter }),

      resetToLandscape: () => {
        set({
          panels: { ...LANDSCAPE_PANELS },
          viewport: { ...DEFAULT_VIEWPORT },
          starFilter: null,
          resourceFilter: null,
        });
      },

      resetToPortrait: () => {
        set({
          panels: { ...PORTRAIT_PANELS },
          viewport: { ...DEFAULT_VIEWPORT },
          starFilter: null,
          resourceFilter: null,
        });
      },

      resetLayout: () => {
        const panels = isPortraitViewport() ? PORTRAIT_PANELS : LANDSCAPE_PANELS;
        set({
          panels: { ...panels },
          viewport: { ...DEFAULT_VIEWPORT },
          starFilter: null,
          resourceFilter: null,
        });
      },
    }),
    {
      name: 'catan-layout',
      version: 7, // Increment when layout structure changes
      onRehydrateStorage: () => {
        console.log('[layoutStore] Starting hydration from localStorage...');
        return (state, error) => {
          if (error) {
            console.error('[layoutStore] Hydration error:', error);
          } else {
            console.log('[layoutStore] Hydration complete. State:', state);
            if (state?.panels?.supplemental) {
              console.log('[layoutStore] Supplemental panel position:', state.panels.supplemental);
            }
          }
        };
      },
      partialize: (state) => ({
        boardType: state.boardType,
        panels: state.panels,
        viewport: state.viewport,
        version: state.version,
      }),
      migrate: (persistedState, version) => {
        console.log(`[layoutStore] migrate called - version=${version}, persistedState:`, persistedState);
        const state = persistedState as LayoutState & {
          panels?: Record<string, unknown>;
        };

        // If version < 6, migrate to new WindowPosition format
        if (version < 6) {
          console.log('[layoutStore] Migrating from version', version, 'to 6 - WindowPosition format');

          // Migrate each panel
          const migratedPanels: Record<PanelId, WindowPosition> = {} as Record<PanelId, WindowPosition>;
          for (const panelId of PANEL_ORDER) {
            if (state.panels?.[panelId]) {
              migratedPanels[panelId] = migratePanel(state.panels[panelId]);
            } else {
              // Use default if panel doesn't exist
              migratedPanels[panelId] = LANDSCAPE_PANELS[panelId];
            }
          }

          return {
            ...initialState,
            ...state,
            panels: migratedPanels,
            version: 7,
          };
        }

        // If version 6, migrate to version 7 - add supplemental panel and fix z-indexes for modal overlays
        if (version === 6) {
          console.log('[layoutStore] Migrating from version 6 to 7 - adding supplemental panel with high z-index');

          const panels = state.panels as Record<PanelId, WindowPosition>;

          // Add supplemental panel if missing, and fix z-indexes for modal overlays
          const migratedPanels: Record<PanelId, WindowPosition> = {
            ...panels,
            // Ensure goFirst has high z-index
            goFirst: {
              ...(panels.goFirst ?? LANDSCAPE_PANELS.goFirst),
              zIndex: 1000,
            },
            // Add supplemental panel with high z-index
            supplemental: LANDSCAPE_PANELS.supplemental,
          };

          return {
            ...state,
            panels: migratedPanels,
            version: 7,
          };
        }

        // Always ensure all panels exist (fills in any missing panels with defaults)
        // This handles cases where storage was saved without all panels
        const currentPanels = (persistedState as LayoutState).panels ?? {};
        const completePanels: Record<PanelId, WindowPosition> = { ...currentPanels };
        let hasMissingPanels = false;

        for (const panelId of PANEL_ORDER) {
          if (!completePanels[panelId]) {
            console.log(`[layoutStore] Adding missing panel: ${panelId}`);
            completePanels[panelId] = LANDSCAPE_PANELS[panelId];
            hasMissingPanels = true;
          }
        }

        if (hasMissingPanels) {
          return {
            ...(persistedState as LayoutState),
            panels: completePanels,
          };
        }

        return persistedState as LayoutState;
      },
    }
  )
);

// ============================================================================
// Selectors
// ============================================================================

/** Select a specific panel's WindowPosition */
export const selectPanel = (panelId: PanelId) => (state: LayoutStore) =>
  state.panels[panelId];

/** Select viewport state */
export const selectViewport = (state: LayoutStore) => state.viewport;

/** Select star filter */
export const selectStarFilter = (state: LayoutStore) => state.starFilter;

/** Select resource filter */
export const selectResourceFilter = (state: LayoutStore) => state.resourceFilter;

/** Info about a minimized panel for the MinimizedBar */
export interface MinimizedPanelInfo {
  id: PanelId;
  title: string;
  icon?: string;
}

/**
 * Select minimized panel info in consistent order.
 * Used by MinimizedBar to render minimized panels.
 * Returns array - use with shallow comparison!
 */
export const selectMinimizedPanels = (state: LayoutStore): MinimizedPanelInfo[] =>
  PANEL_ORDER
    .filter((id) => state.panels[id]?.minimized && state.panels[id]?.visible)
    .map((id) => ({
      id,
      title: PANEL_METADATA[id].title,
      icon: PANEL_METADATA[id].icon,
    }));
