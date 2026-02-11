/**
 * Tests for layoutStore - computed layout functions, store actions, and migrations.
 */

import { describe, it, expect, beforeEach } from 'vitest';
import {
  computeLandscape,
  computePortrait,
  computePanelDefault,
  computeArrangedLayout,
  classifyGameState,
  useLayoutStore,
  PANEL_ORDER,
  type PanelId,
  type WindowPosition,
} from '../layoutStore';

// ============================================================================
// Pure Function Tests
// ============================================================================

describe('computeLandscape', () => {
  it('returns positions for all 9 panels at 1920x1080', () => {
    const result = computeLandscape(1920, 1080);
    for (const id of PANEL_ORDER) {
      expect(result[id]).toBeDefined();
      expect(result[id].width).toBeGreaterThan(0);
      expect(result[id].height).toBeGreaterThan(0);
    }
  });

  it('produces three-column layout with no overlaps between columns', () => {
    const result = computeLandscape(1920, 1080);

    // Left column panels should be in the left ~22%
    const leftEdge = result.dice.left + result.dice.width;
    expect(leftEdge).toBeLessThan(1920 * 0.3);

    // Right column panels should start past 70%
    expect(result.resources.left).toBeGreaterThan(1920 * 0.7);
    expect(result.players.left).toBeGreaterThan(1920 * 0.7);

    // Board should be between left and right columns
    expect(result.board.left).toBeGreaterThan(leftEdge);
    expect(result.board.left + result.board.width).toBeLessThan(result.resources.left);
  });

  it('produces no negative dimensions at 2560x1440', () => {
    const result = computeLandscape(2560, 1440);
    for (const id of PANEL_ORDER) {
      expect(result[id].width).toBeGreaterThan(0);
      expect(result[id].height).toBeGreaterThan(0);
      expect(result[id].left).toBeGreaterThanOrEqual(0);
      expect(result[id].top).toBeGreaterThanOrEqual(0);
    }
  });

  it('produces no negative dimensions at small 800x600', () => {
    const result = computeLandscape(800, 600);
    for (const id of PANEL_ORDER) {
      expect(result[id].width).toBeGreaterThan(0);
      expect(result[id].height).toBeGreaterThan(0);
    }
  });

  it('places modal overlays centered over board', () => {
    const result = computeLandscape(1920, 1080);
    const boardCX = result.board.left + result.board.width / 2;
    const boardCY = result.board.top + result.board.height / 2;

    for (const id of ['goFirst', 'supplemental', 'winner'] as PanelId[]) {
      const panelCX = result[id].left + result[id].width / 2;
      const panelCY = result[id].top + result[id].height / 2;
      // Within 2px of board center (rounding tolerance)
      expect(Math.abs(panelCX - boardCX)).toBeLessThanOrEqual(2);
      expect(Math.abs(panelCY - boardCY)).toBeLessThanOrEqual(2);
      expect(result[id].zIndex).toBe(1000);
    }
  });

  it('stacks left column panels vertically without overlap', () => {
    const result = computeLandscape(1920, 1080);
    expect(result.actions.top).toBeGreaterThanOrEqual(result.dice.top + result.dice.height);
    expect(result.measurements.top).toBeGreaterThanOrEqual(
      result.actions.top + result.actions.height
    );
  });

  it('all non-modal panels are not minimized', () => {
    const result = computeLandscape(1920, 1080);
    const nonModals: PanelId[] = [
      'dice',
      'actions',
      'measurements',
      'board',
      'resources',
      'players',
    ];
    for (const id of nonModals) {
      expect(result[id].minimized).toBe(false);
      expect(result[id].visible).toBe(true);
    }
  });
});

describe('computePortrait', () => {
  it('returns positions for all 9 panels at 430x932', () => {
    const result = computePortrait(430, 932);
    for (const id of PANEL_ORDER) {
      expect(result[id]).toBeDefined();
      expect(result[id].width).toBeGreaterThan(0);
      expect(result[id].height).toBeGreaterThan(0);
    }
  });

  it('minimizes measurements and resources by default', () => {
    const result = computePortrait(430, 932);
    expect(result.measurements.minimized).toBe(true);
    expect(result.resources.minimized).toBe(true);
  });

  it('board is at top, players below, controls below players', () => {
    const result = computePortrait(430, 932);
    expect(result.board.top).toBeLessThan(result.players.top);
    expect(result.players.top).toBeLessThan(result.dice.top);
    expect(result.dice.top).toEqual(result.actions.top);
  });

  it('dice and actions are side by side', () => {
    const result = computePortrait(430, 932);
    expect(result.dice.top).toEqual(result.actions.top);
    expect(result.actions.left).toBeGreaterThan(result.dice.left);
  });

  it('uses full width for board and players', () => {
    const result = computePortrait(430, 932);
    const MARGIN = 12;
    const fullW = 430 - 2 * MARGIN;
    expect(result.board.width).toBe(fullW);
    expect(result.players.width).toBe(fullW);
  });

  it('places modal overlays centered on viewport', () => {
    const vw = 430;
    const vh = 932;
    const result = computePortrait(vw, vh);

    for (const id of ['goFirst', 'supplemental', 'winner'] as PanelId[]) {
      const panelCX = result[id].left + result[id].width / 2;
      const panelCY = result[id].top + result[id].height / 2;
      expect(Math.abs(panelCX - vw / 2)).toBeLessThanOrEqual(2);
      expect(Math.abs(panelCY - vh / 2)).toBeLessThanOrEqual(2);
    }
  });
});

describe('computePanelDefault', () => {
  it('dispatches to landscape when width > height', () => {
    const landscape = computeLandscape(1920, 1080);
    const result = computePanelDefault('dice', 1920, 1080);
    expect(result).toEqual(landscape.dice);
  });

  it('dispatches to portrait when height > width', () => {
    const portrait = computePortrait(430, 932);
    const result = computePanelDefault('board', 430, 932);
    expect(result).toEqual(portrait.board);
  });

  it('returns correct panel for each panel ID', () => {
    const landscape = computeLandscape(1920, 1080);
    for (const id of PANEL_ORDER) {
      expect(computePanelDefault(id, 1920, 1080)).toEqual(landscape[id]);
    }
  });
});

// ============================================================================
// Store Action Tests
// ============================================================================

describe('layoutStore actions', () => {
  beforeEach(() => {
    // Reset store to initial state before each test
    const { resetLayout } = useLayoutStore.getState();
    resetLayout();
    // Clear saved layouts
    const state = useLayoutStore.getState();
    if (state.savedLayouts.length > 0) {
      for (const layout of state.savedLayouts) {
        useLayoutStore.getState().deleteLayout(layout.name);
      }
    }
    // Ensure currentLayoutName is null after cleanup
    useLayoutStore.setState({ currentLayoutName: null });
  });

  describe('saveLayout', () => {
    it('creates a new saved layout', () => {
      const { saveLayout } = useLayoutStore.getState();
      saveLayout('My Layout');

      const state = useLayoutStore.getState();
      expect(state.savedLayouts).toHaveLength(1);
      expect(state.savedLayouts[0].name).toBe('My Layout');
      expect(state.savedLayouts[0].createdAt).toBeTruthy();
      expect(state.savedLayouts[0].updatedAt).toBeTruthy();
      expect(state.currentLayoutName).toBe('My Layout');
    });

    it('overwrites existing layout with same name', () => {
      const { saveLayout, setPanelPosition } = useLayoutStore.getState();
      saveLayout('Test');

      const firstCreatedAt = useLayoutStore.getState().savedLayouts[0].createdAt;

      // Move a panel and save again
      setPanelPosition('dice', 500, 500);
      saveLayout('Test');

      const state = useLayoutStore.getState();
      expect(state.savedLayouts).toHaveLength(1);
      expect(state.savedLayouts[0].createdAt).toBe(firstCreatedAt);
      expect(state.savedLayouts[0].panels.dice.left).toBe(500);
    });

    it('saves multiple layouts', () => {
      const { saveLayout } = useLayoutStore.getState();
      saveLayout('Layout A');
      saveLayout('Layout B');

      expect(useLayoutStore.getState().savedLayouts).toHaveLength(2);
      expect(useLayoutStore.getState().currentLayoutName).toBe('Layout B');
    });
  });

  describe('loadLayout', () => {
    it('applies saved panel positions', () => {
      const { saveLayout, setPanelPosition, loadLayout } = useLayoutStore.getState();

      // Save current state
      saveLayout('Original');

      // Move panels
      setPanelPosition('dice', 999, 999);
      expect(useLayoutStore.getState().panels.dice.left).toBe(999);

      // Load saved layout
      loadLayout('Original');
      expect(useLayoutStore.getState().panels.dice.left).not.toBe(999);
      expect(useLayoutStore.getState().currentLayoutName).toBe('Original');
    });

    it('does nothing for non-existent layout name', () => {
      const before = useLayoutStore.getState().panels;
      useLayoutStore.getState().loadLayout('nonexistent');
      expect(useLayoutStore.getState().panels).toBe(before);
    });
  });

  describe('deleteLayout', () => {
    it('removes layout by name', () => {
      const { saveLayout, deleteLayout } = useLayoutStore.getState();
      saveLayout('ToDelete');
      expect(useLayoutStore.getState().savedLayouts).toHaveLength(1);

      deleteLayout('ToDelete');
      expect(useLayoutStore.getState().savedLayouts).toHaveLength(0);
    });

    it('clears currentLayoutName if it matches', () => {
      const { saveLayout, deleteLayout } = useLayoutStore.getState();
      saveLayout('Active');
      expect(useLayoutStore.getState().currentLayoutName).toBe('Active');

      deleteLayout('Active');
      expect(useLayoutStore.getState().currentLayoutName).toBeNull();
    });

    it('preserves currentLayoutName if it does not match', () => {
      const { saveLayout, deleteLayout } = useLayoutStore.getState();
      saveLayout('Keep');
      saveLayout('Remove');
      useLayoutStore.setState({ currentLayoutName: 'Keep' });

      deleteLayout('Remove');
      expect(useLayoutStore.getState().currentLayoutName).toBe('Keep');
    });
  });

  describe('minimizeAll', () => {
    it('minimizes all 9 panels', () => {
      useLayoutStore.getState().minimizeAll();
      const { panels } = useLayoutStore.getState();

      for (const id of PANEL_ORDER) {
        expect(panels[id].minimized).toBe(true);
      }
    });

    it('preserves other panel properties', () => {
      const beforeWidth = useLayoutStore.getState().panels.dice.width;
      useLayoutStore.getState().minimizeAll();
      expect(useLayoutStore.getState().panels.dice.width).toBe(beforeWidth);
    });
  });

  describe('resetPanel', () => {
    it('resets a single panel to computed position', () => {
      const { setPanelPosition, setPanelSize, resetPanel } = useLayoutStore.getState();

      // Move and resize dice panel to unusual values
      setPanelPosition('dice', 9999, 9999);
      setPanelSize('dice', 50, 50);

      // Reset just that panel
      resetPanel('dice');
      const { panels } = useLayoutStore.getState();

      // Should be back to a reasonable position
      expect(panels.dice.left).not.toBe(9999);
      expect(panels.dice.width).not.toBe(50);
      expect(panels.dice.minimized).toBe(false);
    });

    it('does not affect other panels', () => {
      const { setPanelPosition, resetPanel } = useLayoutStore.getState();

      // Move players panel
      setPanelPosition('players', 777, 777);

      // Reset only dice
      resetPanel('dice');

      expect(useLayoutStore.getState().panels.players.left).toBe(777);
    });

    it('un-minimizes the panel', () => {
      const { toggleMinimize, resetPanel } = useLayoutStore.getState();

      toggleMinimize('dice');
      expect(useLayoutStore.getState().panels.dice.minimized).toBe(true);

      resetPanel('dice');
      expect(useLayoutStore.getState().panels.dice.minimized).toBe(false);
    });
  });

  describe('resetLayout', () => {
    it('resets all panels and clears filters', () => {
      const { setStarFilter, setResourceFilters, saveLayout, resetLayout } =
        useLayoutStore.getState();

      setStarFilter(8);
      setResourceFilters(['brick']);
      saveLayout('SomeLayout');

      resetLayout();
      const state = useLayoutStore.getState();

      expect(state.starFilter).toBeNull();
      expect(state.resourceFilters).toEqual([]);
      expect(state.currentLayoutName).toBeNull();
      // Panels should exist for all IDs
      for (const id of PANEL_ORDER) {
        expect(state.panels[id]).toBeDefined();
      }
    });

    it('resets viewport to defaults', () => {
      useLayoutStore.getState().setViewport({ zoom: 2, pan: { x: 100, y: 200 } });
      useLayoutStore.getState().resetLayout();

      const { viewport } = useLayoutStore.getState();
      expect(viewport.zoom).toBe(1);
      expect(viewport.pan.x).toBe(0);
      expect(viewport.pan.y).toBe(0);
    });
  });
});

// ============================================================================
// Arrange Tests
// ============================================================================

describe('classifyGameState', () => {
  it('returns default for undefined', () => {
    expect(classifyGameState(undefined)).toBe('default');
  });

  it('returns default for null', () => {
    expect(classifyGameState(null)).toBe('default');
  });

  it('returns default for Uninitialized', () => {
    expect(classifyGameState('Uninitialized')).toBe('default');
  });

  it('returns default for WaitingForNewGame', () => {
    expect(classifyGameState('WaitingForNewGame')).toBe('default');
  });

  it('returns default for WaitingForPlayers', () => {
    expect(classifyGameState('WaitingForPlayers')).toBe('default');
  });

  it('returns boardSetup for PickingBoard', () => {
    expect(classifyGameState('PickingBoard')).toBe('boardSetup');
  });

  it('returns boardSetup for WaitingForRollForOrder', () => {
    expect(classifyGameState('WaitingForRollForOrder')).toBe('boardSetup');
  });

  it('returns boardSetup for FinishedRollOrder', () => {
    expect(classifyGameState('FinishedRollOrder')).toBe('boardSetup');
  });

  it('returns allocation for AllocateResourceForward', () => {
    expect(classifyGameState('AllocateResourceForward')).toBe('allocation');
  });

  it('returns allocation for AllocateResourceReverse', () => {
    expect(classifyGameState('AllocateResourceReverse')).toBe('allocation');
  });

  it('returns allocation for DoneResourceAllocation', () => {
    expect(classifyGameState('DoneResourceAllocation')).toBe('allocation');
  });

  it('returns gameOver for GameOver', () => {
    expect(classifyGameState('GameOver')).toBe('gameOver');
  });

  it('returns mainGame for WaitingForRoll', () => {
    expect(classifyGameState('WaitingForRoll')).toBe('mainGame');
  });

  it('returns mainGame for WaitingForNext', () => {
    expect(classifyGameState('WaitingForNext')).toBe('mainGame');
  });

  it('returns mainGame for MustMoveRobber', () => {
    expect(classifyGameState('MustMoveRobber')).toBe('mainGame');
  });

  it('returns mainGame for TooManyCards', () => {
    expect(classifyGameState('TooManyCards')).toBe('mainGame');
  });

  it('returns mainGame for Supplemental', () => {
    expect(classifyGameState('Supplemental')).toBe('mainGame');
  });
});

describe('computeArrangedLayout', () => {
  it('returns all panels visible when gameState is undefined', () => {
    const result = computeArrangedLayout(1920, 1080, undefined);
    const nonModals: PanelId[] = [
      'dice',
      'actions',
      'measurements',
      'players',
      'resources',
      'board',
    ];
    for (const id of nonModals) {
      expect(result[id].minimized).toBe(false);
    }
  });

  it('board fills entire viewport as background in landscape', () => {
    const arranged = computeArrangedLayout(1920, 1080, undefined);
    // Board should span the full usable area (background canvas)
    expect(arranged.board.width).toBeGreaterThan(arranged.dice.width);
    expect(arranged.board.width).toBeGreaterThan(arranged.players.width);
    // Board left should be at the margin (fills viewport)
    expect(arranged.board.left).toBe(8); // M = 8
    expect(arranged.board.top).toBe(56); // TOP = 56
    // Board z-index should be below floating panels
    expect(arranged.board.zIndex).toBeLessThan(arranged.dice.zIndex);
    expect(arranged.board.zIndex).toBeLessThan(arranged.players.zIndex);
  });

  it('board fills entire viewport as background in portrait', () => {
    const arranged = computeArrangedLayout(430, 932, 'WaitingForRoll');
    // Board should span the full usable area
    expect(arranged.board.left).toBe(8); // M = 8
    expect(arranged.board.top).toBe(56); // TOP = 56
    expect(arranged.board.width).toBeGreaterThan(300);
    // Board z-index should be below floating panels
    expect(arranged.board.zIndex).toBeLessThan(arranged.players.zIndex);
  });

  it('minimizes dice and resources during PickingBoard (boardSetup)', () => {
    const result = computeArrangedLayout(1920, 1080, 'PickingBoard');
    expect(result.dice.minimized).toBe(true);
    expect(result.resources.minimized).toBe(true);
    expect(result.measurements.minimized).toBe(false);
    expect(result.board.minimized).toBe(false);
    expect(result.players.minimized).toBe(false);
    expect(result.actions.minimized).toBe(false);
  });

  it('minimizes dice during allocation', () => {
    const result = computeArrangedLayout(1920, 1080, 'AllocateResourceForward');
    expect(result.dice.minimized).toBe(true);
    expect(result.resources.minimized).toBe(false);
    expect(result.measurements.minimized).toBe(false);
    expect(result.actions.minimized).toBe(false);
  });

  it('minimizes measurements during mainGame (WaitingForRoll)', () => {
    const result = computeArrangedLayout(1920, 1080, 'WaitingForRoll');
    expect(result.measurements.minimized).toBe(true);
    expect(result.dice.minimized).toBe(false);
    expect(result.actions.minimized).toBe(false);
  });

  it('minimizes measurements during mainGame (MustMoveRobber)', () => {
    const result = computeArrangedLayout(1920, 1080, 'MustMoveRobber');
    expect(result.measurements.minimized).toBe(true);
  });

  it('minimizes dice, actions, measurements during GameOver', () => {
    const result = computeArrangedLayout(1920, 1080, 'GameOver');
    expect(result.dice.minimized).toBe(true);
    expect(result.actions.minimized).toBe(true);
    expect(result.measurements.minimized).toBe(true);
    expect(result.players.minimized).toBe(false);
    expect(result.resources.minimized).toBe(false);
  });

  it('never minimizes board or players regardless of state', () => {
    const states = [
      'PickingBoard',
      'WaitingForRoll',
      'WaitingForNext',
      'GameOver',
      'AllocateResourceForward',
      'MustMoveRobber',
    ] as const;
    for (const gs of states) {
      const result = computeArrangedLayout(1920, 1080, gs);
      expect(result.board.minimized).toBe(false);
      expect(result.players.minimized).toBe(false);
    }
  });

  it('handles null gameState same as undefined', () => {
    const withNull = computeArrangedLayout(1920, 1080, null);
    const withUndefined = computeArrangedLayout(1920, 1080, undefined);
    for (const id of PANEL_ORDER) {
      expect(withNull[id].minimized).toBe(withUndefined[id].minimized);
    }
  });
});

describe('arrangeLayout action', () => {
  beforeEach(() => {
    useLayoutStore.getState().resetLayout();
    useLayoutStore.setState({ currentLayoutName: null });
  });

  it('applies game-state-aware layout', () => {
    useLayoutStore.getState().arrangeLayout('WaitingForRoll');
    const { panels, viewport, starFilter, resourceFilters, currentLayoutName } =
      useLayoutStore.getState();

    expect(panels.measurements.minimized).toBe(true);
    expect(panels.dice.minimized).toBe(false);
    expect(viewport.zoom).toBe(1);
    expect(viewport.pan.x).toBe(0);
    expect(starFilter).toBeNull();
    expect(resourceFilters).toEqual([]);
    expect(currentLayoutName).toBeNull();
  });

  it('clears filters and layout name', () => {
    const store = useLayoutStore.getState();
    store.setStarFilter(10);
    store.setResourceFilters(['wheat']);
    store.saveLayout('Test');

    useLayoutStore.getState().arrangeLayout('WaitingForNext');
    const state = useLayoutStore.getState();
    expect(state.starFilter).toBeNull();
    expect(state.resourceFilters).toEqual([]);
    expect(state.currentLayoutName).toBeNull();
  });

  it('preserves current viewport (pan/zoom) when arranging', () => {
    useLayoutStore.getState().setViewport({ zoom: 3, pan: { x: 200, y: -100 } });
    useLayoutStore.getState().arrangeLayout('GameOver');

    const { viewport } = useLayoutStore.getState();
    expect(viewport.zoom).toBe(3);
    expect(viewport.pan.x).toBe(200);
    expect(viewport.pan.y).toBe(-100);
  });

  it('works with undefined gameState', () => {
    useLayoutStore.getState().arrangeLayout(undefined);
    const { panels } = useLayoutStore.getState();

    // Default phase: no panels minimized (in landscape)
    const nonModals: PanelId[] = [
      'dice',
      'actions',
      'measurements',
      'players',
      'resources',
      'board',
    ];
    for (const id of nonModals) {
      expect(panels[id].minimized).toBe(false);
    }
  });
});

describe('saveLayout/loadLayout viewport', () => {
  beforeEach(() => {
    useLayoutStore.getState().resetLayout();
    for (const layout of useLayoutStore.getState().savedLayouts) {
      useLayoutStore.getState().deleteLayout(layout.name);
    }
    useLayoutStore.setState({ currentLayoutName: null });
  });

  it('saveLayout captures viewport state', () => {
    useLayoutStore.getState().setViewport({ zoom: 2.5, pan: { x: 100, y: -50 } });
    useLayoutStore.getState().saveLayout('Zoomed');

    const saved = useLayoutStore.getState().savedLayouts[0];
    expect(saved.viewport).toBeDefined();
    expect(saved.viewport.zoom).toBe(2.5);
    expect(saved.viewport.pan.x).toBe(100);
    expect(saved.viewport.pan.y).toBe(-50);
  });

  it('loadLayout restores viewport state', () => {
    // Save with custom viewport
    useLayoutStore.getState().setViewport({ zoom: 3, pan: { x: 50, y: 75 } });
    useLayoutStore.getState().saveLayout('CustomView');

    // Change viewport
    useLayoutStore.getState().setViewport({ zoom: 1, pan: { x: 0, y: 0 } });
    expect(useLayoutStore.getState().viewport.zoom).toBe(1);

    // Load saved layout
    useLayoutStore.getState().loadLayout('CustomView');
    const { viewport } = useLayoutStore.getState();
    expect(viewport.zoom).toBe(3);
    expect(viewport.pan.x).toBe(50);
    expect(viewport.pan.y).toBe(75);
  });

  it('loadLayout falls back to default viewport for legacy layouts without viewport', () => {
    // Simulate a legacy saved layout without viewport field
    useLayoutStore.setState((state) => ({
      savedLayouts: [
        {
          name: 'Legacy',
          panels: { ...state.panels },
          createdAt: new Date().toISOString(),
          updatedAt: new Date().toISOString(),
          // eslint-disable-next-line @typescript-eslint/no-explicit-any
        } as any,
      ],
    }));

    // Change viewport to non-default
    useLayoutStore.getState().setViewport({ zoom: 5, pan: { x: 999, y: 999 } });

    // Load legacy layout
    useLayoutStore.getState().loadLayout('Legacy');
    const { viewport } = useLayoutStore.getState();
    expect(viewport.zoom).toBe(1);
    expect(viewport.pan.x).toBe(0);
    expect(viewport.pan.y).toBe(0);
  });
});

// ============================================================================
// Migration Tests
// ============================================================================

describe('layoutStore migrations', () => {
  it('version 7 state migrates to version 8 with savedLayouts and currentLayoutName', () => {
    // Access the persist config to test migration directly
    const persistConfig = (
      useLayoutStore as unknown as {
        persist: { getOptions: () => { migrate: (state: unknown, version: number) => unknown } };
      }
    ).persist.getOptions();

    const v7State = {
      boardType: 'regular',
      panels: {
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
          zIndex: 10,
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
          top: 200,
          width: 320,
          height: 300,
          minimized: false,
          visible: true,
          zIndex: 1000,
        },
        supplemental: {
          left: 500,
          top: 200,
          width: 320,
          height: 340,
          minimized: false,
          visible: true,
          zIndex: 1000,
        },
        winner: {
          left: 500,
          top: 200,
          width: 320,
          height: 380,
          minimized: false,
          visible: true,
          zIndex: 1000,
        },
      },
      viewport: { pan: { x: 0, y: 0 }, zoom: 1 },
      starFilter: null,
      resourceFilters: [],
      version: 7,
    };

    const migrated = persistConfig.migrate(v7State, 7) as Record<string, unknown>;

    expect(migrated.savedLayouts).toEqual([]);
    expect(migrated.currentLayoutName).toBeNull();
    expect(migrated.version).toBe(8);
    // Panels should be preserved
    expect((migrated.panels as Record<string, WindowPosition>).dice.left).toBe(60);
  });
});
