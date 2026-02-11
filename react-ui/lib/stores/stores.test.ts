/**
 * Tests for Zustand stores.
 * Verifies store actions and selectors work correctly.
 */

import { describe, it, expect, beforeEach } from 'vitest';
import { useGameStore, selectGameState, selectIsMyTurn } from './gameStore';
import { useUIStore } from './uiStore';
import { useConnectionStore, selectIsConnected } from './connectionStore';
import { GameState } from '@/types/generated/models/game-state';
import { GameType } from '@/types/generated/models/game-type';
import type { GameModel } from '@/types/generated/models/game-model';

// Create a minimal mock GameModel for testing
function createMockGameModel(overrides: Partial<GameModel> = {}): GameModel {
  return {
    gameId: 'test-game-123',
    gameHash: 'ABC123',
    gameName: 'Test Game',
    gameType: GameType.Regular,
    gameState: GameState.WaitingForRoll,
    previousGameState: GameState.PickingBoard,
    currentPlayerId: 'player-1',
    hasSupplementalBuildPhase: false,
    saveLifetimeStats: false,
    gameStateMachineVersion: 1,
    createdTime: new Date(),
    nextPlayerToRollAfterSupplemental: '',
    players: [
      {
        id: 'player-1',
        name: 'Alice',
        score: 2,
        goldRolls: 0,
        hasLongestRoad: false,
        largestArmy: false,
        participatingInSupplemental: false,
        resourcesThisTurn: {
          brick: 0,
          ore: 0,
          sheep: 0,
          wheat: 0,
          wood: 0,
        },
        resourcesThisGame: {
          brick: 0,
          ore: 0,
          sheep: 0,
          wheat: 0,
          wood: 0,
        },
        unspentEntitlements: [],
        spentEntitlementsThisGame: [],
        spentEntitlementsThisTurn: [],
        ownedHarbors: [],
      },
      {
        id: 'player-2',
        name: 'Bob',
        score: 1,
        goldRolls: 0,
        hasLongestRoad: false,
        largestArmy: false,
        participatingInSupplemental: false,
        resourcesThisTurn: {
          brick: 0,
          ore: 0,
          sheep: 0,
          wheat: 0,
          wood: 0,
        },
        resourcesThisGame: {
          brick: 0,
          ore: 0,
          sheep: 0,
          wheat: 0,
          wood: 0,
        },
        unspentEntitlements: [],
        spentEntitlementsThisGame: [],
        spentEntitlementsThisTurn: [],
        ownedHarbors: [],
      },
    ],
    tiles: [],
    buildings: [],
    roads: [],
    harbors: [],
    robber: {
      coordinates: { q: 0, r: 0, s: 0 },
      movedBy: null,
      targeted: null,
    },
    actionFlags: {
      undoEnabled: false,
      redoEnabled: false,
      nextEnabled: true,
      rollsEnabled: true,
    },
    houseRules: {
      goldTiles: 1,
      wallsProtectCities: true,
    },
    resourceRules: {
      maxCities: 4,
      maxSettlements: 5,
      maxRoads: 15,
    },
    rollModel: {
      rolls: [],
    },
    gameResourcesModel: {
      brick: 0,
      ore: 0,
      sheep: 0,
      wheat: 0,
      wood: 0,
    },
    entitlementPurchaseModel: [],
    random: {
      seed: 12345,
      iterations: 0,
    },
    ...overrides,
  } as GameModel;
}

describe('gameStore', () => {
  beforeEach(() => {
    // Reset store state before each test
    useGameStore.setState({
      gameModel: null,
      playerProfiles: new Map(),
      currentPlayerId: null,
      shownStars: 0,
      lastRoll: null,
    });
  });

  it('should start with null gameModel', () => {
    const state = useGameStore.getState();
    expect(state.gameModel).toBeNull();
  });

  it('should update gameModel with setGameModel', () => {
    const mockGame = createMockGameModel();
    useGameStore.getState().setGameModel(mockGame);

    const state = useGameStore.getState();
    expect(state.gameModel).toBe(mockGame);
    expect(state.gameModel?.gameId).toBe('test-game-123');
  });

  it('should select game state correctly', () => {
    const mockGame = createMockGameModel({
      gameState: GameState.WaitingForNext,
    });
    useGameStore.getState().setGameModel(mockGame);

    const gameState = selectGameState(useGameStore.getState());
    expect(gameState).toBe(GameState.WaitingForNext);
  });

  it('should detect if it is my turn', () => {
    const mockGame = createMockGameModel({ currentPlayerId: 'player-1' });
    useGameStore.getState().setGameModel(mockGame);
    useGameStore.getState().setCurrentPlayerId('player-1');

    expect(selectIsMyTurn(useGameStore.getState())).toBe(true);

    useGameStore.getState().setCurrentPlayerId('player-2');
    expect(selectIsMyTurn(useGameStore.getState())).toBe(false);
  });

  it('should clear game state', () => {
    const mockGame = createMockGameModel();
    useGameStore.getState().setGameModel(mockGame);
    useGameStore.getState().setCurrentPlayerId('player-1');
    useGameStore.getState().setShownStars(5);

    useGameStore.getState().clearGameState();

    const state = useGameStore.getState();
    expect(state.gameModel).toBeNull();
    expect(state.currentPlayerId).toBeNull();
    expect(state.shownStars).toBe(0);
  });

  it('should set player profiles', () => {
    useGameStore.getState().setPlayerProfiles([
      {
        id: 'player-1',
        name: 'Alice',
        colors: { primary: '#ff0000', secondary: '#cc0000', foreground: '#ffffff' },
      },
      {
        id: 'player-2',
        name: 'Bob',
        colors: { primary: '#0000ff', secondary: '#0000cc', foreground: '#ffffff' },
      },
    ]);

    const state = useGameStore.getState();
    expect(state.playerProfiles.size).toBe(2);
    expect(state.playerProfiles.get('player-1')?.name).toBe('Alice');
  });
});

describe('uiStore', () => {
  beforeEach(() => {
    useUIStore.setState({
      isPortrait: false,
      isMobile: false,
      viewportScale: 1,
      activePortraitTab: 'board',
      isNavMenuOpen: false,
      isShowingCelebration: false,
      isRobberMenuOpen: false,
      menuPosition: null,
    });
  });

  it('should start with default values', () => {
    const state = useUIStore.getState();
    expect(state.isPortrait).toBe(false);
    expect(state.isMobile).toBe(false);
    expect(state.activePortraitTab).toBe('board');
  });

  it('should update orientation', () => {
    useUIStore.getState().setOrientation(true, true);

    const state = useUIStore.getState();
    expect(state.isPortrait).toBe(true);
    expect(state.isMobile).toBe(true);
  });

  it('should toggle nav menu', () => {
    expect(useUIStore.getState().isNavMenuOpen).toBe(false);

    useUIStore.getState().toggleNavMenu();
    expect(useUIStore.getState().isNavMenuOpen).toBe(true);

    useUIStore.getState().toggleNavMenu();
    expect(useUIStore.getState().isNavMenuOpen).toBe(false);
  });

  it('should set active portrait tab', () => {
    useUIStore.getState().setActivePortraitTab('controls');
    expect(useUIStore.getState().activePortraitTab).toBe('controls');

    useUIStore.getState().setActivePortraitTab('players');
    expect(useUIStore.getState().activePortraitTab).toBe('players');
  });

  it('should open and close robber menu', () => {
    useUIStore.getState().openRobberMenu(100, 200);

    let state = useUIStore.getState();
    expect(state.isRobberMenuOpen).toBe(true);
    expect(state.menuPosition).toEqual({ x: 100, y: 200 });

    useUIStore.getState().closeRobberMenu();

    state = useUIStore.getState();
    expect(state.isRobberMenuOpen).toBe(false);
    expect(state.menuPosition).toBeNull();
  });

  it('should close all overlays', () => {
    useUIStore.getState().setNavMenuOpen(true);
    useUIStore.getState().openRobberMenu(50, 50);

    useUIStore.getState().closeAllOverlays();

    const state = useUIStore.getState();
    expect(state.isNavMenuOpen).toBe(false);
    expect(state.isRobberMenuOpen).toBe(false);
  });
});

describe('connectionStore', () => {
  beforeEach(() => {
    useConnectionStore.setState({
      status: 'disconnected',
      gameId: null,
      reconnectAttempts: 0,
      lastError: null,
      lastConnectedAt: null,
      isPageVisible: true,
    });
  });

  it('should start disconnected', () => {
    const state = useConnectionStore.getState();
    expect(state.status).toBe('disconnected');
    expect(selectIsConnected(state)).toBe(false);
  });

  it('should set connected state', () => {
    useConnectionStore.getState().setConnected('game-123');

    const state = useConnectionStore.getState();
    expect(state.status).toBe('connected');
    expect(state.gameId).toBe('game-123');
    expect(state.lastConnectedAt).toBeInstanceOf(Date);
    expect(selectIsConnected(state)).toBe(true);
  });

  it('should set disconnected with error', () => {
    useConnectionStore.getState().setConnected('game-123');
    useConnectionStore.getState().setDisconnected('Connection lost');

    const state = useConnectionStore.getState();
    expect(state.status).toBe('disconnected');
    expect(state.lastError).toBe('Connection lost');
  });

  it('should track reconnect attempts', () => {
    useConnectionStore.getState().incrementReconnectAttempts();
    useConnectionStore.getState().incrementReconnectAttempts();

    expect(useConnectionStore.getState().reconnectAttempts).toBe(2);

    useConnectionStore.getState().resetReconnectAttempts();
    expect(useConnectionStore.getState().reconnectAttempts).toBe(0);
  });

  it('should track page visibility', () => {
    useConnectionStore.getState().setPageVisible(false);
    expect(useConnectionStore.getState().isPageVisible).toBe(false);

    useConnectionStore.getState().setPageVisible(true);
    expect(useConnectionStore.getState().isPageVisible).toBe(true);
  });
});
