/**
 * Game state store - manages the current GameModel received from the server.
 *
 * This is the single source of truth for game state in the React client.
 * State is updated when GameStateUpdated events are received via SignalR.
 */

import { create } from 'zustand';
import { subscribeWithSelector } from 'zustand/middleware';
import type { GameModel } from '@/types/generated/models/game-model';
import type { PlayerModel } from '@/types/generated/models/player-model';
import type { PlayerProfile } from '@/types/player-profile';

interface GameState {
  /** Current game model from server, null if no game loaded */
  gameModel: GameModel | null;

  /** Player profiles with display info (colors, images) */
  playerProfiles: Map<string, PlayerProfile>;

  /** Current player ID (the local user) */
  currentPlayerId: string | null;

  /** Star threshold for building visibility filter (0-14) */
  shownStars: number;

  /** Last roll value for tile highlighting */
  lastRoll: number | null;
}

interface GameActions {
  /** Update the game model with new data from server */
  setGameModel: (gameModel: GameModel) => void;

  /** Clear all game state (when leaving a game) */
  clearGameState: () => void;

  /** Set player profiles for display */
  setPlayerProfiles: (profiles: PlayerProfile[]) => void;

  /** Set the current player ID */
  setCurrentPlayerId: (playerId: string) => void;

  /** Set the star filter threshold */
  setShownStars: (stars: number) => void;

  /** Set the last roll value */
  setLastRoll: (roll: number | null) => void;
}

type GameStore = GameState & GameActions;

const initialState: GameState = {
  gameModel: null,
  playerProfiles: new Map(),
  currentPlayerId: null,
  shownStars: 0,
  lastRoll: null,
};

/**
 * Zustand store for game state.
 * Uses subscribeWithSelector middleware for fine-grained subscriptions.
 */
export const useGameStore = create<GameStore>()(
  subscribeWithSelector((set) => ({
    ...initialState,

    setGameModel: (gameModel) => set({ gameModel }),

    clearGameState: () => set(initialState),

    setPlayerProfiles: (profiles) =>
      set({
        playerProfiles: new Map(profiles.map((p) => [p.id, p])),
      }),

    setCurrentPlayerId: (playerId) => set({ currentPlayerId: playerId }),

    setShownStars: (stars) => set({ shownStars: stars }),

    setLastRoll: (roll) => set({ lastRoll: roll }),
  }))
);

// ============================================================================
// Selectors - use these for fine-grained subscriptions
// ============================================================================

/** Select the current game state enum */
export const selectGameState = (state: GameStore) => state.gameModel?.gameState;

/** Select the current player's turn */
export const selectCurrentTurnPlayerId = (state: GameStore) =>
  state.gameModel?.currentPlayerId;

/** Select whether it's the local player's turn */
export const selectIsMyTurn = (state: GameStore) =>
  state.gameModel?.currentPlayerId === state.currentPlayerId;

/** Select action flags for UI button states */
export const selectActionFlags = (state: GameStore) =>
  state.gameModel?.actionFlags;

/** Select all players in game order */
export const selectPlayers = (state: GameStore): PlayerModel[] =>
  state.gameModel?.players ?? [];

/** Select tiles for board rendering */
export const selectTiles = (state: GameStore) => state.gameModel?.tiles ?? [];

/** Select buildings for board rendering */
export const selectBuildings = (state: GameStore) =>
  state.gameModel?.buildings ?? [];

/** Select roads for board rendering */
export const selectRoads = (state: GameStore) => state.gameModel?.roads ?? [];

/** Select harbors for board rendering */
export const selectHarbors = (state: GameStore) =>
  state.gameModel?.harbors ?? [];

/** Select robber position */
export const selectRobber = (state: GameStore) => state.gameModel?.robber;

/** Select house rules */
export const selectHouseRules = (state: GameStore) =>
  state.gameModel?.houseRules;

/** Select the game ID */
export const selectGameId = (state: GameStore) => state.gameModel?.gameId;

/** Select the game type */
export const selectGameType = (state: GameStore) => state.gameModel?.gameType;

/** Get a player profile by ID */
export const selectPlayerProfile =
  (playerId: string) => (state: GameStore) =>
    state.playerProfiles.get(playerId);

/** Get the current player model (local user) */
export const selectMyPlayer = (state: GameStore): PlayerModel | undefined =>
  state.gameModel?.players.find((p) => p.id === state.currentPlayerId);
