/**
 * Tests for gameStoreHooks
 *
 * Tests the custom Zustand hooks with their equality functions.
 */

import { describe, it, expect, beforeEach } from 'vitest';
import { renderHook, act } from '@testing-library/react';
import { useGameStore } from '../gameStore';
import {
  useGameState,
  useCurrentTurnPlayerId,
  useIsMyTurn,
  useGameId,
  useShownStars,
  useLastRoll,
  useMyPlayerId,
  useActionFlags,
  usePlayers,
  useTiles,
  useBuildings,
  useRoads,
  useHarbors,
  useCurrentPlayer,
  useMyPlayer,
  useIsAllocationPhase,
  useGamePhase,
  useBuildableBuildings,
  useBuildableRoads,
  useMyBuildings,
  useMyRoads,
  usePlayerProfile,
  useSetShownStars,
  gameActions,
} from '../gameStoreHooks';
import type { GameModel } from '@/types/generated/models/game-model';
import type { PlayerModel } from '@/types/generated/models/player-model';

// Helper to create a minimal player
function createMockPlayer(id: string, name: string): PlayerModel {
  return {
    id,
    name,
    goldRolls: 0,
    goodRolls: 0,
    badRolls: 0,
    hasLongestRoad: false,
    islandsPlayed: 0,
    largestArmy: false,
    longestRoad: 0,
    maxNoResourceRolls: 0,
    noResourceCount: 0,
    stars: 0,
    score: 0,
    highestScore: false,
    timesTargeted: 0,
    participatingInSupplemental: false,
    finishedSupplemental: false,
    resourcesThisTurn: {
      wheat: 0,
      wood: 0,
      ore: 0,
      sheep: 0,
      brick: 0,
      goldMine: 0,
      coin: 0,
      paper: 0,
      cloth: 0,
      politics: 0,
      trade: 0,
      science: 0,
      victoryPoint: 0,
      anyDevCard: 0,
      robber: 0,
    },
    resourcesThisGame: {
      wheat: 0,
      wood: 0,
      ore: 0,
      sheep: 0,
      brick: 0,
      goldMine: 0,
      coin: 0,
      paper: 0,
      cloth: 0,
      politics: 0,
      trade: 0,
      science: 0,
      victoryPoint: 0,
      anyDevCard: 0,
      robber: 0,
    },
    unspentEntitlements: [],
    spentEntitlementsThisGame: [],
    spentEntitlementsThisTurn: [],
    ownedHarbors: [],
    victoryPointCards: 0,
  };
}

// Helper to create a minimal game model
function createMockGameModel(overrides: Partial<GameModel> = {}): GameModel {
  return {
    gameId: 'test-game',
    gameName: 'Test Game',
    gameType: 'Regular',
    gameState: 'WaitingForRoll',
    currentPlayerId: 'player-1',
    players: [createMockPlayer('player-1', 'Alice'), createMockPlayer('player-2', 'Bob')],
    tiles: [],
    buildings: [],
    roads: [],
    harbors: [],
    actionFlags: {
      undoEnabled: false,
      redoEnabled: false,
      nextEnabled: true,
      rollsEnabled: true,
    },
    ...overrides,
  } as GameModel;
}

describe('gameStoreHooks', () => {
  // Reset store before each test
  beforeEach(() => {
    const { clearGameState } = useGameStore.getState();
    clearGameState();
  });

  describe('primitive value hooks', () => {
    describe('useGameState', () => {
      it('returns undefined when no game model', () => {
        const { result } = renderHook(() => useGameState());
        expect(result.current).toBeUndefined();
      });

      it('returns game state when game model exists', () => {
        const gameModel = createMockGameModel({ gameState: 'WaitingForRoll' });
        act(() => {
          useGameStore.getState().setGameModel(gameModel);
        });

        const { result } = renderHook(() => useGameState());
        expect(result.current).toBe('WaitingForRoll');
      });
    });

    describe('useCurrentTurnPlayerId', () => {
      it('returns the current turn player ID', () => {
        const gameModel = createMockGameModel({ currentPlayerId: 'player-2' });
        act(() => {
          useGameStore.getState().setGameModel(gameModel);
        });

        const { result } = renderHook(() => useCurrentTurnPlayerId());
        expect(result.current).toBe('player-2');
      });
    });

    describe('useIsMyTurn', () => {
      it('returns true when it is my turn', () => {
        const gameModel = createMockGameModel({ currentPlayerId: 'player-1' });
        act(() => {
          useGameStore.getState().setGameModel(gameModel);
          useGameStore.getState().setCurrentPlayerId('player-1');
        });

        const { result } = renderHook(() => useIsMyTurn());
        expect(result.current).toBe(true);
      });

      it('returns false when it is not my turn', () => {
        const gameModel = createMockGameModel({ currentPlayerId: 'player-2' });
        act(() => {
          useGameStore.getState().setGameModel(gameModel);
          useGameStore.getState().setCurrentPlayerId('player-1');
        });

        const { result } = renderHook(() => useIsMyTurn());
        expect(result.current).toBe(false);
      });
    });

    describe('useGameId', () => {
      it('returns the game ID', () => {
        const gameModel = createMockGameModel({ gameId: 'my-game-123' });
        act(() => {
          useGameStore.getState().setGameModel(gameModel);
        });

        const { result } = renderHook(() => useGameId());
        expect(result.current).toBe('my-game-123');
      });
    });

    describe('useShownStars', () => {
      it('returns the shown stars filter', () => {
        act(() => {
          useGameStore.getState().setShownStars(8);
        });

        const { result } = renderHook(() => useShownStars());
        expect(result.current).toBe(8);
      });
    });

    describe('useLastRoll', () => {
      it('returns the last roll value', () => {
        act(() => {
          useGameStore.getState().setLastRoll(7);
        });

        const { result } = renderHook(() => useLastRoll());
        expect(result.current).toBe(7);
      });
    });

    describe('useMyPlayerId', () => {
      it('returns the local player ID', () => {
        act(() => {
          useGameStore.getState().setCurrentPlayerId('local-player');
        });

        const { result } = renderHook(() => useMyPlayerId());
        expect(result.current).toBe('local-player');
      });
    });
  });

  describe('object hooks with equality', () => {
    describe('useActionFlags', () => {
      it('returns action flags', () => {
        const gameModel = createMockGameModel({
          actionFlags: {
            undoEnabled: true,
            redoEnabled: false,
            nextEnabled: true,
            rollsEnabled: false,
          },
        });
        act(() => {
          useGameStore.getState().setGameModel(gameModel);
        });

        const { result } = renderHook(() => useActionFlags());
        expect(result.current).toEqual({
          undoEnabled: true,
          redoEnabled: false,
          nextEnabled: true,
          rollsEnabled: false,
        });
      });
    });

    describe('usePlayers', () => {
      it('returns empty array when no game model', () => {
        const { result } = renderHook(() => usePlayers());
        expect(result.current).toEqual([]);
      });

      it('returns players array', () => {
        const gameModel = createMockGameModel();
        act(() => {
          useGameStore.getState().setGameModel(gameModel);
        });

        const { result } = renderHook(() => usePlayers());
        expect(result.current).toHaveLength(2);
        expect(result.current[0].id).toBe('player-1');
      });
    });

    describe('useTiles', () => {
      it('returns empty array when no tiles', () => {
        const { result } = renderHook(() => useTiles());
        expect(result.current).toEqual([]);
      });
    });

    describe('useBuildings', () => {
      it('returns empty array when no buildings', () => {
        const { result } = renderHook(() => useBuildings());
        expect(result.current).toEqual([]);
      });
    });

    describe('useRoads', () => {
      it('returns empty array when no roads', () => {
        const { result } = renderHook(() => useRoads());
        expect(result.current).toEqual([]);
      });
    });

    describe('useHarbors', () => {
      it('returns empty array when no harbors', () => {
        const { result } = renderHook(() => useHarbors());
        expect(result.current).toEqual([]);
      });
    });
  });

  describe('derived value hooks', () => {
    describe('useCurrentPlayer', () => {
      it('returns undefined when no game model', () => {
        const { result } = renderHook(() => useCurrentPlayer());
        expect(result.current).toBeUndefined();
      });

      it('returns the current turn player', () => {
        const gameModel = createMockGameModel({ currentPlayerId: 'player-2' });
        act(() => {
          useGameStore.getState().setGameModel(gameModel);
        });

        const { result } = renderHook(() => useCurrentPlayer());
        expect(result.current?.id).toBe('player-2');
        expect(result.current?.name).toBe('Bob');
      });
    });

    describe('useMyPlayer', () => {
      it('returns undefined when no current player ID set', () => {
        const gameModel = createMockGameModel();
        act(() => {
          useGameStore.getState().setGameModel(gameModel);
        });

        const { result } = renderHook(() => useMyPlayer());
        expect(result.current).toBeUndefined();
      });

      it('returns the local player model', () => {
        const gameModel = createMockGameModel();
        act(() => {
          useGameStore.getState().setGameModel(gameModel);
          useGameStore.getState().setCurrentPlayerId('player-1');
        });

        const { result } = renderHook(() => useMyPlayer());
        expect(result.current?.id).toBe('player-1');
        expect(result.current?.name).toBe('Alice');
      });
    });

    describe('useIsAllocationPhase', () => {
      it('returns false when no game model', () => {
        const { result } = renderHook(() => useIsAllocationPhase());
        expect(result.current).toBe(false);
      });

      it('returns true for allocation states', () => {
        const gameModel = createMockGameModel({ gameState: 'AllocateResourceForward' });
        act(() => {
          useGameStore.getState().setGameModel(gameModel);
        });

        const { result } = renderHook(() => useIsAllocationPhase());
        expect(result.current).toBe(true);
      });

      it('returns false for non-allocation states', () => {
        const gameModel = createMockGameModel({ gameState: 'WaitingForRoll' });
        act(() => {
          useGameStore.getState().setGameModel(gameModel);
        });

        const { result } = renderHook(() => useIsAllocationPhase());
        expect(result.current).toBe(false);
      });
    });

    describe('useGamePhase', () => {
      it('returns undefined when no game model', () => {
        const { result } = renderHook(() => useGamePhase());
        expect(result.current).toBeUndefined();
      });

      it('returns Rolling phase for WaitingForRoll', () => {
        const gameModel = createMockGameModel({ gameState: 'WaitingForRoll' });
        act(() => {
          useGameStore.getState().setGameModel(gameModel);
        });

        const { result } = renderHook(() => useGamePhase());
        expect(result.current).toBe('Rolling');
      });

      it('returns Purchase phase for WaitingForNext', () => {
        const gameModel = createMockGameModel({ gameState: 'WaitingForNext' });
        act(() => {
          useGameStore.getState().setGameModel(gameModel);
        });

        const { result } = renderHook(() => useGamePhase());
        expect(result.current).toBe('Purchase');
      });
    });

    describe('useBuildableBuildings', () => {
      it('returns empty array when no game model', () => {
        const { result } = renderHook(() => useBuildableBuildings());
        expect(result.current).toEqual([]);
      });
    });

    describe('useBuildableRoads', () => {
      it('returns empty array when no game model', () => {
        const { result } = renderHook(() => useBuildableRoads());
        expect(result.current).toEqual([]);
      });
    });

    describe('useMyBuildings', () => {
      it('returns empty array when no game model', () => {
        const { result } = renderHook(() => useMyBuildings());
        expect(result.current).toEqual([]);
      });
    });

    describe('useMyRoads', () => {
      it('returns empty array when no game model', () => {
        const { result } = renderHook(() => useMyRoads());
        expect(result.current).toEqual([]);
      });
    });
  });

  describe('player profile hooks', () => {
    describe('usePlayerProfile', () => {
      it('returns undefined for unknown player', () => {
        const { result } = renderHook(() => usePlayerProfile('unknown'));
        expect(result.current).toBeUndefined();
      });

      it('returns profile for known player', () => {
        act(() => {
          useGameStore.getState().setPlayerProfiles([
            {
              id: 'player-1',
              name: 'Alice',
              colors: { primary: '#ff0000', secondary: '#00ff00', foreground: '#ffffff' },
            },
          ]);
        });

        const { result } = renderHook(() => usePlayerProfile('player-1'));
        expect(result.current).toEqual({
          id: 'player-1',
          name: 'Alice',
          colors: { primary: '#ff0000', secondary: '#00ff00', foreground: '#ffffff' },
        });
      });
    });
  });

  describe('action hooks', () => {
    describe('useSetShownStars', () => {
      it('returns the setShownStars action', () => {
        const { result } = renderHook(() => useSetShownStars());
        expect(typeof result.current).toBe('function');
      });

      it('action works correctly', () => {
        const { result } = renderHook(() => useSetShownStars());

        act(() => {
          result.current(10);
        });

        expect(useGameStore.getState().shownStars).toBe(10);
      });
    });

    describe('gameActions', () => {
      it('provides all action functions', () => {
        expect(typeof gameActions.setGameModel).toBe('function');
        expect(typeof gameActions.clearGameState).toBe('function');
        expect(typeof gameActions.setPlayerProfiles).toBe('function');
        expect(typeof gameActions.setCurrentPlayerId).toBe('function');
        expect(typeof gameActions.setShownStars).toBe('function');
        expect(typeof gameActions.setLastRoll).toBe('function');
      });

      it('actions work correctly', () => {
        act(() => {
          gameActions.setShownStars(15);
        });

        expect(useGameStore.getState().shownStars).toBe(15);
      });
    });
  });
});
