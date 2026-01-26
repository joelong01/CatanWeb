/**
 * Layout store - manages floating panel positions and viewport state.
 *
 * Uses persist middleware to save layout to localStorage.
 * Supports different layouts per board type (regular vs expansion).
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
  | 'resources';

/** Default panel layouts */
const DEFAULT_PANELS: Record<PanelId, PanelLayout> = {
  dice: {
    position: { x: 20, y: 80 },
    size: { width: 260, height: 200 },
    minimized: false,
    visible: true,
  },
  actions: {
    position: { x: 20, y: 300 },
    size: { width: 200, height: 200 },
    minimized: false,
    visible: true,
  },
  measurements: {
    position: { x: 20, y: 520 },
    size: { width: 280, height: 130 },
    minimized: false,
    visible: true,
  },
  players: {
    position: { x: -340, y: 80 },
    size: { width: 320, height: 400 },
    minimized: false,
    visible: true,
  },
  resources: {
    position: { x: -340, y: 500 },
    size: { width: 320, height: 130 },
    minimized: false,
    visible: true,
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

  /** Reset layout to defaults */
  resetLayout: () => void;
}

type LayoutStore = LayoutState & LayoutActions;

const initialState: LayoutState = {
  boardType: 'regular',
  panels: { ...DEFAULT_PANELS },
  viewport: { ...DEFAULT_VIEWPORT },
  starFilter: null,
  resourceFilter: null,
  version: 1,
};

/**
 * Zustand store for layout state.
 * Uses persist middleware to save to localStorage per board type.
 */
export const useLayoutStore = create<LayoutStore>()(
  persist(
    (set, get) => ({
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

      bringToFront: (_panelId) => {
        // Z-index managed via CSS order; this is a hook for future use
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

      resetLayout: () => {
        set({
          panels: { ...DEFAULT_PANELS },
          viewport: { ...DEFAULT_VIEWPORT },
          starFilter: null,
          resourceFilter: null,
        });
      },
    }),
    {
      name: 'catan-layout',
      partialize: (state) => ({
        boardType: state.boardType,
        panels: state.panels,
        viewport: state.viewport,
        version: state.version,
      }),
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
