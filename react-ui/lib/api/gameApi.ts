/**
 * Game API client for REST operations.
 * Handles game creation, player management, and game lifecycle operations.
 */

import { serviceConfig } from '../services/config';
import type { PlayerProfile } from '@/types/player-profile';
import type {
  NewGameMessage,
  GameType,
  HouseRules,
  GameTemplateData,
  GameTemplateSummary,
} from '@/types/generated/models';

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
 * Matches the GameSaveInfo returned by the /api/games endpoint.
 */
export interface SavedGameSummary {
  gameId: string;
  gameName: string;
  gameState: string;
  playerCount: number;
  playerNames: string;
  turnCount: number;
  gameType: string;
  savedAt: string;
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

// ── Recording types ──

/**
 * Summary of a saved recording for list display.
 */
export interface RecordingSummary {
  id: string;
  name: string;
  createdAt: string;
  gameType: string;
  playerCount: number;
  actionCount: number;
  gameId: string;
}

/**
 * Summary of a single action in a recording.
 */
export interface ActionSummary {
  index: number;
  actionType: string;
  gameState: string;
  expectedHash: string;
  details: string;
}

/**
 * Result of executing a single replay step.
 */
export interface StepResult {
  success: boolean;
  actionIndex: number;
  expectedHash: string;
  actualHash: string;
  hashMatch: boolean;
  errorMessage?: string;
  // eslint-disable-next-line @typescript-eslint/no-explicit-any
  gameModel?: any;
}

/**
 * Result of a full recording replay.
 */
export interface ReplayResult {
  success: boolean;
  recordingName: string;
  actionsReplayed: number;
  totalActions: number;
  failedAtAction?: number;
  expectedHash?: string;
  actualHash?: string;
  errorMessage?: string;
}

/**
 * Response from starting a step-by-step replay session.
 */
export interface StartSessionResult {
  sessionId: string;
  recordingName: string;
  totalActions: number;
  currentIndex: number;
}

/**
 * Makes a fetch request with standard error handling.
 */
async function apiFetch<T>(endpoint: string, options: RequestInit = {}): Promise<ApiResponse<T>> {
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

    if (response.status === 204 || response.headers.get('content-length') === '0') {
      return { success: true, data: undefined as T };
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
    // Map gameType to templateId for the template engine
    const templateId = gameType === 'Regular' ? 'regular' : 'expansion';

    const message: NewGameMessage = {
      gameType,
      playerIds,
      gameName: gameName ?? 'Untitled Game',
      houseRules: houseRules as HouseRules,
      saveLifetimeStats,
      templateId,
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

  /**
   * Creates a replay of an existing game, reset to the WaitingForRollForOrder state.
   *
   * @param gameId - The ID of the game to replay
   * @param newName - Optional name for the replay
   */
  async replayGame(gameId: string, newName?: string): Promise<ApiResponse<CopyGameResponse>> {
    const url = newName
      ? `/api/game/${gameId}/replay?newName=${encodeURIComponent(newName)}`
      : `/api/game/${gameId}/replay`;

    return apiFetch<CopyGameResponse>(url, {
      method: 'POST',
    });
  },

  /**
   * Removes a game from the server's in-memory registry without deleting it from the database.
   * Call this when the player explicitly closes a game session.
   */
  async closeGame(gameId: string): Promise<ApiResponse<void>> {
    return apiFetch<void>(`/api/game/${gameId}/close`, { method: 'POST' });
  },

  /**
   * Deletes a game from the database.
   *
   * @param gameId - The ID of the game to delete
   */
  async deleteGame(gameId: string): Promise<ApiResponse<void>> {
    return apiFetch<void>(`/api/game/${gameId}`, {
      method: 'DELETE',
    });
  },

  /**
   * Renames a game.
   *
   * @param gameId - The ID of the game to rename
   * @param newName - The new name for the game
   */
  async renameGame(gameId: string, newName: string): Promise<ApiResponse<void>> {
    return apiFetch<void>(`/api/game/${gameId}/rename?newName=${encodeURIComponent(newName)}`, {
      method: 'PATCH',
    });
  },

  /**
   * Loads a game from the database.
   * This makes the game the active game on the server.
   *
   * @param gameId - The ID of the game to load
   */
  async loadGame(gameId: string): Promise<ApiResponse<void>> {
    return apiFetch<void>(`/api/game/${gameId}/load`, {
      method: 'POST',
    });
  },

  /**
   * Uploads a player image (avatar).
   * Uses multipart/form-data (no Content-Type header -- browser sets boundary).
   *
   * @param playerId - The player ID to upload the image for
   * @param file - The image file (JPEG, PNG, GIF; max 5MB)
   * @returns The image URI on success
   */
  async uploadPlayerImage(playerId: string, file: File): Promise<ApiResponse<string>> {
    const url = `${serviceConfig.serviceUrl}/api/players/${playerId}/image`;
    const formData = new FormData();
    formData.append('file', file);
    try {
      const response = await fetch(url, { method: 'POST', body: formData });
      if (!response.ok) {
        const errorText = await response.text();
        return { success: false, error: errorText || `HTTP ${response.status}` };
      }
      const data = await response.json();
      return { success: true, data: data.imageUri };
    } catch (error) {
      const message = error instanceof Error ? error.message : 'Upload failed';
      return { success: false, error: `Network error: ${message}` };
    }
  },

  // ── Recording API ──

  /**
   * Fetches all saved recordings.
   */
  async getRecordings(): Promise<ApiResponse<RecordingSummary[]>> {
    return apiFetch<RecordingSummary[]>('/api/recordings');
  },

  /**
   * Gets action details for a recording.
   */
  async getRecordingActions(id: string): Promise<ApiResponse<ActionSummary[]>> {
    return apiFetch<ActionSummary[]>(`/api/recording/${id}/actions`);
  },

  /**
   * Runs full replay with hash validation.
   */
  async replayRecording(id: string): Promise<ApiResponse<ReplayResult>> {
    return apiFetch<ReplayResult>(`/api/recording/${id}/replay`, {
      method: 'POST',
    });
  },

  /**
   * Starts a step-by-step replay session.
   */
  async startReplaySession(id: string): Promise<ApiResponse<StartSessionResult>> {
    return apiFetch<StartSessionResult>(`/api/recording/${id}/replay/start`, {
      method: 'POST',
    });
  },

  /**
   * Executes next step in a replay session.
   *
   * @param sessionId - The replay session ID
   * @param includeGameModel - When true, response includes full GameModel for board rendering
   */
  async stepReplay(sessionId: string, includeGameModel = false): Promise<ApiResponse<StepResult>> {
    const query = includeGameModel ? '?includeGameModel=true' : '';
    return apiFetch<StepResult>(`/api/replay/${sessionId}/step${query}`, {
      method: 'POST',
    });
  },

  /**
   * Ends a replay session.
   */
  async endReplaySession(sessionId: string): Promise<ApiResponse<void>> {
    return apiFetch<void>(`/api/replay/${sessionId}`, {
      method: 'DELETE',
    });
  },

  /**
   * Renames a recording.
   */
  async renameRecording(id: string, name: string): Promise<ApiResponse<void>> {
    return apiFetch<void>(`/api/recording/${id}/rename`, {
      method: 'PUT',
      body: JSON.stringify({ name }),
    });
  },

  /**
   * Deletes a recording.
   */
  async deleteRecording(id: string): Promise<ApiResponse<void>> {
    return apiFetch<void>(`/api/recording/${id}`, {
      method: 'DELETE',
    });
  },

  /**
   * Updates house rules on a running game.
   * Routes through GameStateMachine for undo/redo support.
   *
   * @param gameId - The active game ID
   * @param houseRules - Complete house rules object
   */
  async updateHouseRules(gameId: string, houseRules: HouseRules): Promise<ApiResponse<void>> {
    return apiFetch<void>(`/api/game/${gameId}/houserules`, {
      method: 'PUT',
      body: JSON.stringify(houseRules),
    });
  },

  // ── Template API ──

  async getTemplates(category?: string): Promise<ApiResponse<GameTemplateSummary[]>> {
    const query = category ? `?category=${encodeURIComponent(category)}` : '';
    return apiFetch<GameTemplateSummary[]>(`/api/game/templates${query}`);
  },

  async getTemplate(id: string): Promise<ApiResponse<GameTemplateData>> {
    return apiFetch<GameTemplateData>(`/api/game/templates/${encodeURIComponent(id)}`);
  },

  async updateTemplate(id: string, data: GameTemplateData): Promise<ApiResponse<GameTemplateData>> {
    return apiFetch<GameTemplateData>(`/api/game/templates/${encodeURIComponent(id)}`, {
      method: 'PUT',
      body: JSON.stringify(data),
    });
  },

  async createTemplate(data: GameTemplateData): Promise<ApiResponse<GameTemplateData>> {
    return apiFetch<GameTemplateData>('/api/game/templates', {
      method: 'POST',
      body: JSON.stringify(data),
    });
  },

  async deleteTemplate(id: string): Promise<ApiResponse<void>> {
    return apiFetch<void>(`/api/game/templates/${encodeURIComponent(id)}`, {
      method: 'DELETE',
    });
  },
};
