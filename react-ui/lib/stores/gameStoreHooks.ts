/**
 * Custom hooks for accessing game store state with optimized re-render behavior.
 *
 * These hooks use custom equality functions to prevent unnecessary re-renders
 * when complex objects are returned from selectors.
 *
 * @module gameStoreHooks
 */

import { useStoreWithEqualityFn } from 'zustand/traditional';
import { shallow } from 'zustand/shallow';
import { useGameStore } from './gameStore';
import type { GameModel } from '@/types/generated/models/game-model';
import type { PlayerModel } from '@/types/generated/models/player-model';
import type { TileModel } from '@/types/generated/models/tile-model';
import type { BuildingModel } from '@/types/generated/models/building-model';
import type { RoadModel } from '@/types/generated/models/road-model';
import type { HarborModel } from '@/types/generated/models/harbor-model';
import type { ActionFlags } from '@/types/generated/models/action-flags';
import type { PlayerProfile } from '@/types/player-profile';
import {
  currentPlayer,
  isAllocationPhase,
  gamePhase,
  isCurrentPlayer,
  buildableBuildings,
  buildableRoadsFromModel,
  buildingsOwnedByPlayer,
  roadsOwnedByPlayerFromModel,
  type GamePhase,
} from '../extensions';

// ============================================================================
// Stable Empty Array Constants
// (Prevents infinite re-render loops when selector returns empty array)
// ============================================================================

const EMPTY_PLAYERS: PlayerModel[] = [];
const EMPTY_TILES: TileModel[] = [];
const EMPTY_BUILDINGS: BuildingModel[] = [];
const EMPTY_ROADS: RoadModel[] = [];
const EMPTY_HARBORS: HarborModel[] = [];

// ============================================================================
// Custom Equality Functions
// ============================================================================

/**
 * Compare two arrays by length and reference equality of elements.
 * Used for arrays that change infrequently (players, tiles, etc.)
 */
function arraysEqual<T>(a: T[] | undefined, b: T[] | undefined): boolean {
  if (a === b) return true;
  if (!a || !b) return false;
  if (a.length !== b.length) return false;
  // Shallow comparison - assumes array elements are the same references if unchanged
  for (let i = 0; i < a.length; i++) {
    if (a[i] !== b[i]) return false;
  }
  return true;
}

/**
 * Compare ActionFlags objects for equality.
 */
function actionFlagsEqual(
  a: ActionFlags | undefined,
  b: ActionFlags | undefined
): boolean {
  if (a === b) return true;
  if (!a || !b) return false;
  return (
    a.undoEnabled === b.undoEnabled &&
    a.redoEnabled === b.redoEnabled &&
    a.nextEnabled === b.nextEnabled &&
    a.rollsEnabled === b.rollsEnabled
  );
}

/**
 * Compare player profiles map by checking each entry.
 */
function profilesEqual(
  a: Map<string, PlayerProfile>,
  b: Map<string, PlayerProfile>
): boolean {
  if (a === b) return true;
  if (a.size !== b.size) return false;
  for (const [key, val] of a) {
    const bVal = b.get(key);
    if (!bVal) return false;
    // Compare profile properties
    if (
      val.id !== bVal.id ||
      val.name !== bVal.name ||
      val.colors.primary !== bVal.colors.primary ||
      val.colors.secondary !== bVal.colors.secondary ||
      val.colors.foreground !== bVal.colors.foreground
    ) {
      return false;
    }
  }
  return true;
}

// ============================================================================
// Primitive Value Hooks (no equality function needed)
// ============================================================================

/**
 * Returns the current game state enum value.
 * Only re-renders when gameState actually changes.
 */
export function useGameState() {
  return useGameStore((state) => state.gameModel?.gameState);
}

/**
 * Returns the current turn player ID.
 */
export function useCurrentTurnPlayerId() {
  return useGameStore((state) => state.gameModel?.currentPlayerId);
}

/**
 * Returns whether it's the local player's turn.
 */
export function useIsMyTurn() {
  return useGameStore(
    (state) => state.gameModel?.currentPlayerId === state.currentPlayerId
  );
}

/**
 * Returns the game ID.
 */
export function useGameId() {
  return useGameStore((state) => state.gameModel?.gameId);
}

/**
 * Returns the game type.
 */
export function useGameType() {
  return useGameStore((state) => state.gameModel?.gameType);
}

/**
 * Returns the star filter threshold.
 */
export function useShownStars() {
  return useGameStore((state) => state.shownStars);
}

/**
 * Returns the last roll value.
 */
export function useLastRoll() {
  return useGameStore((state) => state.lastRoll);
}

/**
 * Returns the local player's ID.
 */
export function useMyPlayerId() {
  return useGameStore((state) => state.currentPlayerId);
}

// ============================================================================
// Object Hooks (with custom equality)
// ============================================================================

/**
 * Returns the action flags for UI button states.
 * Uses custom equality to prevent re-renders when values haven't changed.
 */
export function useActionFlags(): ActionFlags | undefined {
  return useStoreWithEqualityFn(
    useGameStore,
    (state) => state.gameModel?.actionFlags,
    actionFlagsEqual
  );
}

/**
 * Returns all players in game order.
 * Uses shallow array comparison.
 */
export function usePlayers(): PlayerModel[] {
  return useStoreWithEqualityFn(
    useGameStore,
    (state) => state.gameModel?.players ?? EMPTY_PLAYERS,
    arraysEqual
  );
}

/**
 * Returns all tiles for board rendering.
 * Uses shallow array comparison.
 */
export function useTiles(): TileModel[] {
  return useStoreWithEqualityFn(
    useGameStore,
    (state) => state.gameModel?.tiles ?? EMPTY_TILES,
    arraysEqual
  );
}

/**
 * Returns all buildings for board rendering.
 * Uses shallow array comparison.
 */
export function useBuildings(): BuildingModel[] {
  return useStoreWithEqualityFn(
    useGameStore,
    (state) => state.gameModel?.buildings ?? EMPTY_BUILDINGS,
    arraysEqual
  );
}

/**
 * Returns all roads for board rendering.
 * Uses shallow array comparison.
 */
export function useRoads(): RoadModel[] {
  return useStoreWithEqualityFn(
    useGameStore,
    (state) => state.gameModel?.roads ?? EMPTY_ROADS,
    arraysEqual
  );
}

/**
 * Returns all harbors for board rendering.
 * Uses shallow array comparison.
 */
export function useHarbors(): HarborModel[] {
  return useStoreWithEqualityFn(
    useGameStore,
    (state) => state.gameModel?.harbors ?? EMPTY_HARBORS,
    arraysEqual
  );
}

/**
 * Returns the robber position.
 */
export function useRobber() {
  return useStoreWithEqualityFn(
    useGameStore,
    (state) => state.gameModel?.robber,
    shallow
  );
}

/**
 * Returns house rules.
 */
export function useHouseRules() {
  return useStoreWithEqualityFn(
    useGameStore,
    (state) => state.gameModel?.houseRules,
    shallow
  );
}

/**
 * Returns player profiles map.
 * Uses custom profiles equality check.
 */
export function usePlayerProfiles(): Map<string, PlayerProfile> {
  return useStoreWithEqualityFn(useGameStore, (state) => state.playerProfiles, profilesEqual);
}

// ============================================================================
// Derived Value Hooks (use extension functions)
// ============================================================================

/**
 * Returns the current player model (whose turn it is).
 * Derived from gameModel using currentPlayer extension.
 */
export function useCurrentPlayer(): PlayerModel | undefined {
  return useGameStore((state) => {
    if (!state.gameModel) return undefined;
    return currentPlayer(state.gameModel);
  });
}

/**
 * Returns the local player's model.
 */
export function useMyPlayer(): PlayerModel | undefined {
  return useGameStore((state) => {
    if (!state.gameModel || !state.currentPlayerId) return undefined;
    return state.gameModel.players?.find((p) => p.id === state.currentPlayerId);
  });
}

/**
 * Returns whether the game is in allocation phase.
 */
export function useIsAllocationPhase(): boolean {
  return useGameStore((state) => {
    if (!state.gameModel) return false;
    return isAllocationPhase(state.gameModel);
  });
}

/**
 * Returns the current game phase.
 */
export function useGamePhase(): GamePhase | undefined {
  return useGameStore((state) => {
    if (!state.gameModel) return undefined;
    return gamePhase(state.gameModel);
  });
}

/**
 * Returns whether a specific player is the current turn player.
 */
export function useIsPlayerTurn(playerId: string): boolean {
  return useGameStore((state) => {
    if (!state.gameModel) return false;
    return isCurrentPlayer(state.gameModel, playerId);
  });
}

/**
 * Returns all buildable buildings (PossibleSettlement state).
 * Uses shallow array comparison.
 */
export function useBuildableBuildings(): BuildingModel[] {
  return useStoreWithEqualityFn(
    useGameStore,
    (state) => {
      if (!state.gameModel) return EMPTY_BUILDINGS;
      return buildableBuildings(state.gameModel);
    },
    arraysEqual
  );
}

/**
 * Returns all buildable roads (Buildable state).
 * Uses shallow array comparison.
 */
export function useBuildableRoads(): RoadModel[] {
  return useStoreWithEqualityFn(
    useGameStore,
    (state) => {
      if (!state.gameModel) return EMPTY_ROADS;
      return buildableRoadsFromModel(state.gameModel);
    },
    arraysEqual
  );
}

/**
 * Returns buildings owned by the local player.
 * Uses shallow array comparison.
 */
export function useMyBuildings(): BuildingModel[] {
  return useStoreWithEqualityFn(
    useGameStore,
    (state) => {
      if (!state.gameModel || !state.currentPlayerId) return EMPTY_BUILDINGS;
      return buildingsOwnedByPlayer(state.gameModel, state.currentPlayerId);
    },
    arraysEqual
  );
}

/**
 * Returns roads owned by the local player.
 * Uses shallow array comparison.
 */
export function useMyRoads(): RoadModel[] {
  return useStoreWithEqualityFn(
    useGameStore,
    (state) => {
      if (!state.gameModel || !state.currentPlayerId) return EMPTY_ROADS;
      return roadsOwnedByPlayerFromModel(state.gameModel, state.currentPlayerId);
    },
    arraysEqual
  );
}

// ============================================================================
// Player Profile Hooks
// ============================================================================

/**
 * Returns a specific player's profile.
 * @param playerId The player ID to get profile for
 */
export function usePlayerProfile(playerId: string): PlayerProfile | undefined {
  return useGameStore((state) => state.playerProfiles.get(playerId));
}

/**
 * Returns the current turn player's profile.
 */
export function useCurrentPlayerProfile(): PlayerProfile | undefined {
  return useGameStore((state) => {
    const turnPlayerId = state.gameModel?.currentPlayerId;
    if (!turnPlayerId) return undefined;
    return state.playerProfiles.get(turnPlayerId);
  });
}

/**
 * Returns the local player's profile.
 */
export function useMyProfile(): PlayerProfile | undefined {
  return useGameStore((state) => {
    if (!state.currentPlayerId) return undefined;
    return state.playerProfiles.get(state.currentPlayerId);
  });
}

// ============================================================================
// Action Hooks
// ============================================================================

/**
 * Returns the setGameModel action.
 */
export function useSetGameModel() {
  return useGameStore((state) => state.setGameModel);
}

/**
 * Returns the clearGameState action.
 */
export function useClearGameState() {
  return useGameStore((state) => state.clearGameState);
}

/**
 * Returns the setPlayerProfiles action.
 */
export function useSetPlayerProfiles() {
  return useGameStore((state) => state.setPlayerProfiles);
}

/**
 * Returns the setCurrentPlayerId action.
 */
export function useSetCurrentPlayerId() {
  return useGameStore((state) => state.setCurrentPlayerId);
}

/**
 * Returns the setShownStars action.
 */
export function useSetShownStars() {
  return useGameStore((state) => state.setShownStars);
}

/**
 * Returns the setLastRoll action.
 */
export function useSetLastRoll() {
  return useGameStore((state) => state.setLastRoll);
}

/**
 * Returns all store actions.
 * Note: For performance-critical code, prefer individual action hooks above.
 */
export const gameActions = {
  get setGameModel() {
    return useGameStore.getState().setGameModel;
  },
  get clearGameState() {
    return useGameStore.getState().clearGameState;
  },
  get setPlayerProfiles() {
    return useGameStore.getState().setPlayerProfiles;
  },
  get setCurrentPlayerId() {
    return useGameStore.getState().setCurrentPlayerId;
  },
  get setShownStars() {
    return useGameStore.getState().setShownStars;
  },
  get setLastRoll() {
    return useGameStore.getState().setLastRoll;
  },
};
