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

/** Panel position and state */
export interface PanelLayout {
  position: { x: number; y: number };
  size: { width: number; height: number };
  minimized: boolean;
  visible: boolean;
  /** Z-index for stacking order (higher = on top). Board should be lowest. */
  zIndex: number;
}

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
  | 'board';

/**
 * Landscape default panel layouts (matching Blazor 3-column layout)
 *
 * Blazor layout: left (320px) | center (1fr) | right (530px) at 1920x1080
 * - Left column: Dice clusters, Actions, Measurements
 * - Center: Game board
 * - Right column: Resources, Players
 *
 * Using percentages of typical viewport for floating panels:
 * - Left column starts at x=60 (clear of hamburger menu)
 * - Right column uses negative x (offset from right edge)
 * - Center board positioned after left column
 */
const LANDSCAPE_PANELS: Record<PanelId, PanelLayout> = {
  dice: {
    position: { x: 60, y: 80 },
    size: { width: 260, height: 200 },
    minimized: false,
    visible: true,
    zIndex: 20,
  },
  actions: {
    position: { x: 60, y: 300 },
    size: { width: 200, height: 200 },
    minimized: false,
    visible: true,
    zIndex: 21,
  },
  measurements: {
    position: { x: 60, y: 520 },
    size: { width: 280, height: 130 },
    minimized: false,
    visible: true,
    zIndex: 22,
  },
  board: {
    position: { x: 340, y: 80 },
    size: { width: 600, height: 550 },
    minimized: false,
    visible: true,
    zIndex: 10, // Board is lowest - other panels float on top
  },
  resources: {
    position: { x: -360, y: 80 },
    size: { width: 340, height: 130 },
    minimized: false,
    visible: true,
    zIndex: 23,
  },
  players: {
    position: { x: -360, y: 230 },
    size: { width: 340, height: 450 },
    minimized: false,
    visible: true,
    zIndex: 24,
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
const PORTRAIT_PANELS: Record<PanelId, PanelLayout> = {
  board: {
    position: { x: 20, y: 60 },
    size: { width: 400, height: 400 },
    minimized: false,
    visible: true,
    zIndex: 10, // Board is lowest - other panels float on top
  },
  players: {
    position: { x: 20, y: 480 },
    size: { width: 400, height: 300 },
    minimized: false,
    visible: true,
    zIndex: 24,
  },
  dice: {
    position: { x: 20, y: 800 },
    size: { width: 200, height: 150 },
    minimized: false,
    visible: true,
    zIndex: 20,
  },
  actions: {
    position: { x: 240, y: 800 },
    size: { width: 180, height: 150 },
    minimized: false,
    visible: true,
    zIndex: 21,
  },
  measurements: {
    position: { x: 20, y: 970 },
    size: { width: 400, height: 100 },
    minimized: true,
    visible: true,
    zIndex: 22,
  },
  resources: {
    position: { x: 20, y: 20 },
    size: { width: 300, height: 40 },
    minimized: true,
    visible: true,
    zIndex: 23,
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

  /** Panel layouts */
  panels: Record<PanelId, PanelLayout>;

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
  setPanelPosition: (panelId: PanelId, position: { x: number; y: number }) => void;

  /** Update panel size */
  setPanelSize: (panelId: PanelId, size: { width: number; height: number }) => void;

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

type LayoutStore = LayoutState & LayoutActions;

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
  version: 3, // Bumped version for z-index support
};

/**
 * Zustand store for layout state.
 * Uses persist middleware to save to localStorage per board type.
 */
export const useLayoutStore = create<LayoutStore>()(
  persist(
    (set, _get) => ({
      ...initialState,

      setBoardType: (boardType) => {
        set({ boardType });
      },

      setPanelPosition: (panelId, position) => {
        set((state) => ({
          panels: {
            ...state.panels,
            [panelId]: {
              ...state.panels[panelId],
              position,
            },
          },
        }));
      },

      setPanelSize: (panelId, size) => {
        set((state) => ({
          panels: {
            ...state.panels,
            [panelId]: {
              ...state.panels[panelId],
              size,
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
              .map(([_, p]) => p.zIndex)
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
      version: 3, // Increment when layout structure changes
      partialize: (state) => ({
        boardType: state.boardType,
        panels: state.panels,
        viewport: state.viewport,
        version: state.version,
      }),
      migrate: (persistedState, version) => {
        // If version < 3, reset to defaults (adds zIndex support)
        if (version < 3) {
          console.log('[layoutStore] Migrating from version', version, 'to 3 - resetting panels for zIndex support');
          return {
            ...initialState,
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

/** Select a specific panel's layout */
export const selectPanel = (panelId: PanelId) => (state: LayoutStore) =>
  state.panels[panelId];

/** Select viewport state */
export const selectViewport = (state: LayoutStore) => state.viewport;

/** Select star filter */
export const selectStarFilter = (state: LayoutStore) => state.starFilter;

/** Select resource filter */
export const selectResourceFilter = (state: LayoutStore) => state.resourceFilter;
