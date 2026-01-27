/**
 * Game API client for REST operations.
 * Handles game creation, player management, and game lifecycle operations.
 */

import { serviceConfig } from '../services/config';
import type { PlayerProfile } from '@/types/player-profile';
import type { NewGameMessage, GameType, HouseRules } from '@/types/generated/models';

/**
 * API response wrapper with success/error handling.
 */
export interface ApiResponse<T> {
  success: boolean;
  data?: T;
  error?: string;
}

/**
 * Response from creating a new game.
 */
export interface CreateGameResponse {
  success: boolean;
  gameId: string;
}

/**
 * Response from getting players.
 */
export interface PlayersResponse {
  success: boolean;
  players: PlayerProfile[];
  count: number;
  timestamp: string;
}

/**
 * Saved game summary for game list.
 */
export interface SavedGameSummary {
  gameId: string;
  gameName: string;
  gameType: GameType;
  players: string[];
  lastModified: string;
  isComplete: boolean;
  winnerName?: string;
}

/**
 * Response from getting saved games.
 */
export interface SavedGamesResponse {
  success: boolean;
  games: SavedGameSummary[];
  count: number;
}

/**
 * Response from copying a game.
 */
export interface CopyGameResponse {
  success: boolean;
  newGameId: string;
  gameName: string;
  message?: string;
}

/**
 * Makes a fetch request with standard error handling.
 */
async function apiFetch<T>(
  endpoint: string,
  options: RequestInit = {}
): Promise<ApiResponse<T>> {
  const url = `${serviceConfig.serviceUrl}${endpoint}`;

  try {
    const response = await fetch(url, {
      ...options,
      headers: {
        'Content-Type': 'application/json',
        ...options.headers,
      },
    });

    if (!response.ok) {
      const errorText = await response.text();
      return {
        success: false,
        error: errorText || `HTTP ${response.status}: ${response.statusText}`,
      };
    }

    const data = await response.json();
    return { success: true, data };
  } catch (error) {
    const message = error instanceof Error ? error.message : 'Unknown error';
    return { success: false, error: `Network error: ${message}` };
  }
}

/**
 * Game API client singleton.
 */
export const gameApi = {
  /**
   * Fetches all available players from the database.
   */
  async getPlayers(): Promise<ApiResponse<PlayerProfile[]>> {
    const result = await apiFetch<PlayersResponse>('/api/players');
    if (result.success && result.data) {
      return { success: true, data: result.data.players };
    }
    return { success: false, error: result.error };
  },

  /**
   * Creates a new game with the specified configuration.
   *
   * @param gameType - Type of game (Regular or Expansion)
   * @param playerIds - Array of player IDs (3-4 for Regular, 3-6 for Expansion)
   * @param gameName - Optional name for the game
   * @param houseRules - Optional house rules configuration
   * @param saveLifetimeStats - Whether to update lifetime stats (default: true)
   * @returns The created game's ID
   */
  async createGame(
    gameType: GameType,
    playerIds: string[],
    gameName?: string,
    houseRules?: Partial<HouseRules>,
    saveLifetimeStats: boolean = true
  ): Promise<ApiResponse<string>> {
    const message: NewGameMessage = {
      gameType,
      playerIds,
      gameName: gameName ?? 'Untitled Game',
      houseRules: houseRules as HouseRules,
      saveLifetimeStats,
    };

    const result = await apiFetch<CreateGameResponse>('/api/game/new', {
      method: 'POST',
      body: JSON.stringify(message),
    });

    if (result.success && result.data) {
      return { success: true, data: result.data.gameId };
    }
    return { success: false, error: result.error };
  },

  /**
   * Fetches saved games, optionally filtered by player.
   *
   * @param playerId - Optional player ID to filter games
   */
  async getSavedGames(playerId?: string): Promise<ApiResponse<SavedGameSummary[]>> {
    const endpoint = playerId ? `/api/games?playerId=${playerId}` : '/api/games';
    const result = await apiFetch<SavedGamesResponse>(endpoint);
    if (result.success && result.data) {
      return { success: true, data: result.data.games };
    }
    return { success: false, error: result.error };
  },

  /**
   * Creates a new player profile.
   *
   * @param player - The player profile to create
   */
  async createPlayer(player: PlayerProfile): Promise<ApiResponse<PlayerProfile>> {
    return apiFetch<PlayerProfile>('/api/players', {
      method: 'POST',
      body: JSON.stringify(player),
    });
  },

  /**
   * Updates an existing player profile.
   *
   * @param id - The player ID to update
   * @param player - The updated player data
   */
  async updatePlayer(
    id: string,
    player: Partial<PlayerProfile>
  ): Promise<ApiResponse<PlayerProfile>> {
    return apiFetch<PlayerProfile>(`/api/players/${id}`, {
      method: 'PUT',
      body: JSON.stringify(player),
    });
  },

  /**
   * Deletes a player profile.
   *
   * @param id - The player ID to delete
   */
  async deletePlayer(id: string): Promise<ApiResponse<void>> {
    return apiFetch<void>(`/api/players/${id}`, {
      method: 'DELETE',
    });
  },

  /**
   * Creates a copy of an existing game.
   *
   * @param gameId - The ID of the game to copy
   * @param newName - Optional name for the copy
   */
  async copyGame(gameId: string, newName?: string): Promise<ApiResponse<CopyGameResponse>> {
    const url = newName
      ? `/api/game/${gameId}/copy?newName=${encodeURIComponent(newName)}`
      : `/api/game/${gameId}/copy`;

    return apiFetch<CopyGameResponse>(url, {
      method: 'POST',
    });
  },
};
