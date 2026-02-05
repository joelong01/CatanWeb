/**
 * Layout store - manages floating panel positions and viewport state.
 *
 * Uses persist middleware to save layout to localStorage.
 * Supports different layouts per board type (regular vs expansion).
 * Provides portrait and landscape default layouts matching Blazor.
 */

import { create } from 'zustand';
import { persist } from 'zustand/middleware';
import type { GameState } from '@/types/generated/models/game-state';

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
  | 'supplemental'
  | 'winner';

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
  winner: { title: 'Winner', icon: '🏆' },
};

/** Panel order for minimized bar (consistent ordering) */
export const PANEL_ORDER: PanelId[] = ['dice', 'actions', 'measurements', 'players', 'resources', 'board', 'goFirst', 'supplemental', 'winner'];

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
  winner: {
    left: 500,
    top: 200, // Centered over the board area
    width: 320,
    height: 380, // Taller to accommodate scoring controls
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
  winner: {
    left: 40,
    top: 200, // Centered over the board area
    width: 320,
    height: 380, // Taller to accommodate scoring controls
    minimized: false,
    visible: true,
    zIndex: 1000, // Very high z-index - modal overlay must be on top of everything
  },
};

// ============================================================================
// Computed Layout Functions (pure, no window access)
// ============================================================================

/** Modal overlay panel heights */
const MODAL_HEIGHTS: Partial<Record<PanelId, number>> = {
  goFirst: 300,
  supplemental: 340,
  winner: 380,
};

/** Modal overlay panel width */
const MODAL_WIDTH = 320;

/**
 * Compute landscape panel positions for the given viewport dimensions.
 * Three-column layout: left (controls) | center (board) | right (info).
 */
export function computeLandscape(vw: number, vh: number): Record<PanelId, WindowPosition> {
  const MARGIN = 16;
  const GAP = 8;
  const TOP = 80;
  const BAR_CLEARANCE = 40; // MinimizedBar at bottom

  const leftW = Math.floor(vw * 0.22);
  const rightW = Math.floor(vw * 0.22);
  const rightX = vw - rightW - MARGIN;
  const centerX = leftW + MARGIN + GAP;
  const centerW = rightX - centerX - GAP;
  const usableH = vh - TOP - MARGIN - BAR_CLEARANCE;

  // Left column: dice, actions, measurements stacked vertically
  const diceH = Math.floor(usableH * 0.33);
  const actionsH = Math.floor(usableH * 0.33);
  const actionsTop = TOP + diceH + GAP;
  const measTop = actionsTop + actionsH + GAP;
  const measH = Math.max(80, usableH - diceH - actionsH - GAP * 2);

  // Right column: resources (short), players (fills remaining)
  const resourcesH = Math.floor(usableH * 0.18);
  const playersTop = TOP + resourcesH + GAP;
  const playersH = Math.max(100, usableH - resourcesH - GAP);

  // Board fills center
  const boardH = usableH;

  // Modal overlays centered over board area
  const boardCenterX = centerX + centerW / 2;
  const boardCenterY = TOP + boardH / 2;

  function modal(panelId: PanelId): WindowPosition {
    const h = MODAL_HEIGHTS[panelId] ?? 300;
    return {
      left: Math.round(boardCenterX - MODAL_WIDTH / 2),
      top: Math.round(boardCenterY - h / 2),
      width: MODAL_WIDTH,
      height: h,
      minimized: false,
      visible: true,
      zIndex: 1000,
    };
  }

  return {
    dice: { left: MARGIN, top: TOP, width: leftW, height: diceH, minimized: false, visible: true, zIndex: 20 },
    actions: { left: MARGIN, top: actionsTop, width: leftW, height: actionsH, minimized: false, visible: true, zIndex: 21 },
    measurements: { left: MARGIN, top: measTop, width: leftW, height: measH, minimized: false, visible: true, zIndex: 22 },
    board: { left: centerX, top: TOP, width: centerW, height: boardH, minimized: false, visible: true, zIndex: 10 },
    resources: { left: rightX, top: TOP, width: rightW, height: resourcesH, minimized: false, visible: true, zIndex: 23 },
    players: { left: rightX, top: playersTop, width: rightW, height: playersH, minimized: false, visible: true, zIndex: 24 },
    goFirst: modal('goFirst'),
    supplemental: modal('supplemental'),
    winner: modal('winner'),
  };
}

/**
 * Compute portrait panel positions for the given viewport dimensions.
 * Vertical stack: board (top) | players | dice+actions | minimized stats/resources.
 */
export function computePortrait(vw: number, vh: number): Record<PanelId, WindowPosition> {
  const MARGIN = 12;
  const GAP = 6;
  const TOP = 60;
  const fullW = vw - 2 * MARGIN;

  const boardH = Math.floor(vh * 0.50);
  const playersTop = TOP + boardH + GAP;
  const playersH = Math.floor(vh * 0.18);
  const controlsTop = playersTop + playersH + GAP;
  const controlsH = Math.floor(vh * 0.15);
  const diceW = Math.floor(fullW * 0.48);
  const actionsW = fullW - diceW - GAP;

  // Modal overlays centered on viewport
  function modal(panelId: PanelId): WindowPosition {
    const h = MODAL_HEIGHTS[panelId] ?? 300;
    return {
      left: Math.round((vw - MODAL_WIDTH) / 2),
      top: Math.round((vh - h) / 2),
      width: MODAL_WIDTH,
      height: h,
      minimized: false,
      visible: true,
      zIndex: 1000,
    };
  }

  return {
    board: { left: MARGIN, top: TOP, width: fullW, height: boardH, minimized: false, visible: true, zIndex: 10 },
    players: { left: MARGIN, top: playersTop, width: fullW, height: playersH, minimized: false, visible: true, zIndex: 24 },
    dice: { left: MARGIN, top: controlsTop, width: diceW, height: controlsH, minimized: false, visible: true, zIndex: 20 },
    actions: { left: MARGIN + diceW + GAP, top: controlsTop, width: actionsW, height: controlsH, minimized: false, visible: true, zIndex: 21 },
    measurements: { left: MARGIN, top: 0, width: fullW, height: 100, minimized: true, visible: true, zIndex: 22 },
    resources: { left: MARGIN, top: 0, width: fullW, height: 40, minimized: true, visible: true, zIndex: 23 },
    goFirst: modal('goFirst'),
    supplemental: modal('supplemental'),
    winner: modal('winner'),
  };
}

/**
 * Compute a single panel's default position for the given viewport.
 * Dispatches to landscape or portrait based on aspect ratio.
 */
export function computePanelDefault(panelId: PanelId, vw: number, vh: number): WindowPosition {
  const all = (vh > vw) ? computePortrait(vw, vh) : computeLandscape(vw, vh);
  return all[panelId];
}

// ============================================================================
// Game-State-Aware Arrange
// ============================================================================

/** Gameplay phase for layout arrangement decisions */
export type ArrangePhase = 'default' | 'boardSetup' | 'allocation' | 'mainGame' | 'gameOver';

/** Panels to minimize in each phase */
const PHASE_MINIMIZED: Record<ArrangePhase, PanelId[]> = {
  default: [],
  boardSetup: ['dice', 'resources'],
  allocation: ['dice'],
  mainGame: ['measurements'],
  gameOver: ['dice', 'actions', 'measurements'],
};

/**
 * Classify a GameState into an ArrangePhase for layout decisions.
 * Pure function, no side effects.
 */
export function classifyGameState(gameState?: GameState | null): ArrangePhase {
  if (!gameState) return 'default';
  switch (gameState) {
    case 'Uninitialized':
    case 'WaitingForNewGame':
    case 'WaitingForPlayers':
      return 'default';
    case 'PickingBoard':
    case 'WaitingForRollForOrder':
    case 'FinishedRollOrder':
      return 'boardSetup';
    case 'BeginResourceAllocation':
    case 'AllocateResourceForward':
    case 'AllocateResourceReverse':
    case 'DoneResourceAllocation':
      return 'allocation';
    case 'GameOver':
      return 'gameOver';
    default:
      return 'mainGame';
  }
}

// ---------------------------------------------------------------------------
// Arrange layout helpers
// ---------------------------------------------------------------------------

/** Create a WindowPosition for a minimized panel */
function minPos(w: number, h: number, z: number): WindowPosition {
  return { left: 0, top: 0, width: w, height: h,
           minimized: true, visible: true, zIndex: z };
}

/** Create a visible WindowPosition */
function visPos(left: number, top: number, w: number, h: number, z: number): WindowPosition {
  return { left, top, width: w, height: h, minimized: false, visible: true, zIndex: z };
}

/**
 * Compute an arranged layout for the given viewport and game state.
 *
 * Design principle: the board fills the ENTIRE viewport as a background
 * canvas (z-index 10). All other panels float ON TOP of the board at
 * higher z-indexes. Panel sizes scale with the viewport so content
 * (hex clusters, player rows, resource cards) renders at a usable size.
 *
 * Portrait:  dice+actions top-left, players top-right, resources bottom
 * Landscape: dice+actions left side, players+resources right side
 *
 * Pure function -- no window access, no side effects, fully testable.
 */
export function computeArrangedLayout(
  vw: number,
  vh: number,
  gameState?: GameState | null,
): Record<PanelId, WindowPosition> {
  const phase = classifyGameState(gameState);
  const minimizedSet = new Set(PHASE_MINIMIZED[phase]);
  const isPortrait = vh > vw;

  const M = 8;     // edge margin
  const G = 6;     // inter-panel gap
  const TOP = 56;  // navbar clearance
  const fullW = vw - 2 * M;
  const usableH = vh - TOP - M;

  const result: Record<string, WindowPosition> = {};

  // Board ALWAYS fills the full viewport as a background canvas
  result.board = visPos(M, TOP, fullW, usableH, 10);

  // Modal overlays centered on board
  const bcx = M + fullW / 2;
  const bcy = TOP + usableH / 2;
  for (const id of ['goFirst', 'supplemental', 'winner'] as PanelId[]) {
    const mh = MODAL_HEIGHTS[id] ?? 300;
    result[id] = visPos(
      Math.round(bcx - MODAL_WIDTH / 2),
      Math.round(bcy - mh / 2),
      MODAL_WIDTH, mh, 1000);
  }

  /** Place a panel or mark it minimized. Visible panels float above board. */
  function place(id: PanelId, left: number, top: number, w: number, h: number, z: number): void {
    if (minimizedSet.has(id)) {
      result[id] = minPos(w, h, 20);
    } else {
      result[id] = visPos(Math.round(left), Math.round(top),
                          Math.round(w), Math.round(h), z);
    }
  }

  if (isPortrait) {
    // ---- PORTRAIT ----
    // Left column: dice (top), actions (mid), measurements (bottom) -- ~38% width
    // Right column: players -- ~58% width, ~75% of usable height
    // Bottom: resources spanning lower portion
    const colW = Math.round(fullW * 0.38);
    const diceH = Math.round(usableH * 0.38);
    const actionsH = Math.round(usableH * 0.38);
    const measH = Math.round(usableH * 0.12);
    const rightW = fullW - colW - G;
    const rightX = M + colW + G;

    place('dice',    M, TOP,                          colW, diceH, 1001);
    place('actions', M, TOP + diceH + G,              colW, actionsH, 1002);
    place('measurements', M, TOP + diceH + G + actionsH + G, colW, measH, 1003);

    place('players', rightX, TOP, rightW, Math.round(usableH * 0.75), 1004);

    const resH = Math.round(usableH * 0.15);
    const resW = Math.round(fullW * 0.65);
    const resX = M + (fullW - resW) / 2;
    place('resources', resX, TOP + usableH - resH, resW, resH, 1005);

  } else {
    // ---- LANDSCAPE ----
    // Left column: dice (top), actions (mid), measurements (bottom) -- ~22% width
    // Right column: players (tall), resources (short below) -- ~25% width
    const leftW = Math.round(fullW * 0.22);
    const rightW = Math.round(fullW * 0.25);
    const rightX = vw - M - rightW;
    const diceH = Math.round(usableH * 0.38);
    const actionsH = Math.round(usableH * 0.38);
    const measH = Math.round(usableH * 0.12);

    place('dice',    M, TOP,                          leftW, diceH, 1001);
    place('actions', M, TOP + diceH + G,              leftW, actionsH, 1002);
    place('measurements', M, TOP + diceH + G + actionsH + G, leftW, measH, 1003);

    const playersH = Math.round(usableH * 0.72);
    place('players', rightX, TOP, rightW, playersH, 1004);

    const resH = Math.round(usableH * 0.18);
    place('resources', rightX, TOP + playersH + G, rightW, resH, 1005);
  }

  // Ensure every panel ID has an entry
  for (const id of PANEL_ORDER) {
    if (!result[id]) {
      result[id] = minPos(200, 150, 20);
    }
  }

  return result as Record<PanelId, WindowPosition>;
}

// ============================================================================
// Saved Layout Type
// ============================================================================

/** A named layout preset saved by the user */
export interface SavedLayout {
  name: string;
  panels: Record<PanelId, WindowPosition>;
  viewport: ViewportState;
  createdAt: string;
  updatedAt: string;
}

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

  /** Resource filters — up to 3 selected resource types (AND logic) */
  resourceFilters: string[];

  /** Layout version for migrations */
  version: number;

  /** User-saved named layout presets */
  savedLayouts: SavedLayout[];

  /** Name of the currently active saved layout (null = unnamed) */
  currentLayoutName: string | null;
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
  setResourceFilters: (resources: string[]) => void;

  /** Reset layout based on current viewport (viewport-aware computed positions) */
  resetLayout: () => void;

  /** Reset a single panel to its computed default position */
  resetPanel: (panelId: PanelId) => void;

  /** Minimize all panels */
  minimizeAll: () => void;

  /** Save current panel positions under a name */
  saveLayout: (name: string) => void;

  /** Load a saved layout by name */
  loadLayout: (name: string) => void;

  /** Delete a saved layout by name */
  deleteLayout: (name: string) => void;

  /** Arrange layout based on game state -- game-aware optimal positioning */
  arrangeLayout: (gameState?: GameState | null) => void;
}

export type LayoutStore = LayoutState & LayoutActions;

const initialState: LayoutState = {
  boardType: 'regular',
  panels: { ...LANDSCAPE_PANELS },
  viewport: { ...DEFAULT_VIEWPORT },
  starFilter: null,
  resourceFilters: [],
  version: 8,
  savedLayouts: [],
  currentLayoutName: null,
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

          // Only update if panel exists and isn't already on top
          if (!state.panels[panelId] || state.panels[panelId].zIndex >= maxZ) return state;

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

      setResourceFilters: (resourceFilters) => set({ resourceFilters }),

      resetLayout: () => {
        const vw = typeof window !== 'undefined' ? window.innerWidth : 1920;
        const vh = typeof window !== 'undefined' ? window.innerHeight : 1080;
        const panels = (vh > vw) ? computePortrait(vw, vh) : computeLandscape(vw, vh);
        set({
          panels,
          viewport: { ...DEFAULT_VIEWPORT },
          starFilter: null,
          resourceFilters: [],
          currentLayoutName: null,
        });
      },

      resetPanel: (panelId) => {
        const vw = typeof window !== 'undefined' ? window.innerWidth : 1920;
        const vh = typeof window !== 'undefined' ? window.innerHeight : 1080;
        const defaultPos = computePanelDefault(panelId, vw, vh);
        set((state) => ({
          panels: {
            ...state.panels,
            [panelId]: { ...defaultPos, minimized: false },
          },
        }));
      },

      minimizeAll: () => {
        set((state) => {
          const panels = { ...state.panels };
          for (const id of PANEL_ORDER) {
            panels[id] = { ...panels[id], minimized: true };
          }
          return { panels };
        });
      },

      saveLayout: (name) => {
        set((state) => {
          const now = new Date().toISOString();
          const existing = state.savedLayouts.findIndex((l) => l.name === name);
          const entry: SavedLayout = {
            name,
            panels: { ...state.panels },
            viewport: { ...state.viewport },
            createdAt: existing >= 0 ? state.savedLayouts[existing].createdAt : now,
            updatedAt: now,
          };
          const savedLayouts = [...state.savedLayouts];
          if (existing >= 0) {
            savedLayouts[existing] = entry;
          } else {
            savedLayouts.push(entry);
          }
          return { savedLayouts, currentLayoutName: name };
        });
      },

      loadLayout: (name) => {
        set((state) => {
          const layout = state.savedLayouts.find((l) => l.name === name);
          if (!layout) return state;
          return {
            panels: { ...layout.panels },
            viewport: layout.viewport ? { ...layout.viewport } : { ...DEFAULT_VIEWPORT },
            currentLayoutName: name,
          };
        });
      },

      deleteLayout: (name) => {
        set((state) => ({
          savedLayouts: state.savedLayouts.filter((l) => l.name !== name),
          currentLayoutName: state.currentLayoutName === name ? null : state.currentLayoutName,
        }));
      },

      arrangeLayout: (gameState) => {
        const vw = typeof window !== 'undefined' ? window.innerWidth : 1920;
        const vh = typeof window !== 'undefined' ? window.innerHeight : 1080;
        const panels = computeArrangedLayout(vw, vh, gameState);
        set({
          panels,
          // Preserve current viewport (pan/zoom) — don't reset the user's board view
          starFilter: null,
          resourceFilters: [],
          currentLayoutName: null,
        });
      },
    }),
    {
      name: 'catan-layout',
      version: 8, // Increment when layout structure changes
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
        savedLayouts: state.savedLayouts,
        currentLayoutName: state.currentLayoutName,
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
            savedLayouts: [],
            currentLayoutName: null,
            version: 8,
          };
        }

        // If version 6, migrate to version 7 format then fall through to 7->8
        if (version === 6) {
          console.log('[layoutStore] Migrating from version 6 to 7 - adding supplemental panel with high z-index');

          const panels = state.panels as Record<PanelId, WindowPosition>;

          const migratedPanels: Record<PanelId, WindowPosition> = {
            ...panels,
            goFirst: {
              ...(panels.goFirst ?? LANDSCAPE_PANELS.goFirst),
              zIndex: 1000,
            },
            supplemental: LANDSCAPE_PANELS.supplemental,
          };

          // Continue to v7->v8 migration below
          return {
            ...state,
            panels: migratedPanels,
            savedLayouts: [],
            currentLayoutName: null,
            version: 8,
          };
        }

        // Version 7 -> 8: add savedLayouts and currentLayoutName
        if (version === 7) {
          console.log('[layoutStore] Migrating from version 7 to 8 - adding savedLayouts');
          return {
            ...(persistedState as LayoutState),
            savedLayouts: [],
            currentLayoutName: null,
            version: 8,
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

/** Select resource filters */
export const selectResourceFilters = (state: LayoutStore) => state.resourceFilters;

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
