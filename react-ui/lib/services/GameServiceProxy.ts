/**
 * GameServiceProxy - Unified SignalR/REST client for the Catan GameService.
 *
 * Architecture:
 * - SignalR for real-time updates (GameStateUpdated broadcasts)
 * - REST for commands (avoids SignalR message ordering issues on mobile)
 *
 * This differs from the Blazor client which uses SignalR for both directions,
 * but both can play in the same game since updates broadcast to all clients.
 */

import {
  HubConnection,
  HubConnectionBuilder,
  HubConnectionState,
  LogLevel,
} from '@microsoft/signalr';
import type { GameModel } from '@/types/generated/models/game-model';
import type { GameType } from '@/types/generated/models/game-type';
import type { Entitlement } from '@/types/generated/models/entitlement';
import type { RoadKey } from '@/types/generated/models/road-key';
import type { BuildingKey } from '@/types/generated/models/building-key';
import type { HexCoordinates } from '@/types/generated/models/hex-coordinates';
import { serviceConfig } from './config';

// ============================================================================
// Types
// ============================================================================

/** Result of a command execution */
export interface CommandResult {
  success: boolean;
  message: string;
  gameId?: string;
}

/** Game info returned from REST API */
export interface GameInfo {
  gameId: string;
  gameName: string;
  gameType: GameType;
  playerCount: number;
  createdAt: string;
}

/** Player profile for display */
export interface PlayerProfile {
  id: string;
  name: string;
  color: string;
  imageUri?: string;
}

/** Connection state for external monitoring */
export type ConnectionState =
  | 'disconnected'
  | 'connecting'
  | 'connected'
  | 'reconnecting';

/** Event handler types */
export interface GameServiceProxyEvents {
  onGameStateUpdated?: (gameModel: GameModel) => void;
  onPlayersUpdated?: (players: PlayerProfile[]) => void;
  onConnectionStateChanged?: (state: ConnectionState) => void;
  onError?: (error: string) => void;
}

// ============================================================================
// GameServiceProxy Class
// ============================================================================

export class GameServiceProxy {
  private connection: HubConnection | null = null;
  private serviceUrl: string;
  private playerId: string;
  private gameId: string | null = null;
  private events: GameServiceProxyEvents = {};
  private isDisposed = false;

  constructor(playerId: string, serviceUrl?: string) {
    this.playerId = playerId;
    this.serviceUrl = serviceUrl ?? serviceConfig.serviceUrl;
  }

  // ==========================================================================
  // Connection Management
  // ==========================================================================

  /**
   * Connect to SignalR hub and optionally join a game.
   */
  async connect(gameId?: string): Promise<void> {
    if (this.isDisposed) {
      throw new Error('GameServiceProxy has been disposed');
    }

    if (this.connection?.state === HubConnectionState.Connected) {
      this.log('Already connected');
      return;
    }

    this.notifyConnectionState('connecting');

    try {
      this.connection = new HubConnectionBuilder()
        .withUrl(`${this.serviceUrl}/gameHub`)
        .withAutomaticReconnect({
          nextRetryDelayInMilliseconds: (retryContext) => {
            // Exponential backoff: 0, 2s, 4s, 8s, 16s, max 30s
            const delay = Math.min(
              1000 * Math.pow(2, retryContext.previousRetryCount),
              30000
            );
            return delay;
          },
        })
        .configureLogging(LogLevel.Information)
        .build();

      this.setupEventHandlers();

      await this.connection.start();
      this.log('Connected to SignalR hub');
      this.notifyConnectionState('connected');

      if (gameId) {
        await this.joinGame(gameId);
      }
    } catch (error) {
      this.log('Connection failed', error);
      this.notifyConnectionState('disconnected');
      throw error;
    }
  }

  /**
   * Disconnect from SignalR hub.
   */
  async disconnect(): Promise<void> {
    if (this.connection) {
      if (this.gameId) {
        try {
          await this.leaveGame(this.gameId);
        } catch {
          // Ignore errors when leaving during disconnect
        }
      }

      await this.connection.stop();
      this.connection = null;
      this.gameId = null;
      this.notifyConnectionState('disconnected');
    }
  }

  /**
   * Join a game's SignalR group to receive updates.
   */
  async joinGame(gameId: string): Promise<void> {
    this.ensureConnected();
    await this.connection!.invoke('JoinGame', gameId, this.playerId);
    this.gameId = gameId;
    this.log(`Joined game ${gameId}`);
  }

  /**
   * Leave a game's SignalR group.
   */
  async leaveGame(gameId: string): Promise<void> {
    this.ensureConnected();
    await this.connection!.invoke('LeaveGame', gameId, this.playerId);
    if (this.gameId === gameId) {
      this.gameId = null;
    }
    this.log(`Left game ${gameId}`);
  }

  /**
   * Force reconnection (useful for mobile wake recovery).
   */
  async forceReconnect(): Promise<void> {
    if (this.connection) {
      await this.disconnect();
    }

    const savedGameId = this.gameId;
    await this.connect(savedGameId ?? undefined);

    // Fetch full state after reconnect to catch any missed updates
    if (savedGameId) {
      await this.refreshGameState(savedGameId);
    }
  }

  // ==========================================================================
  // REST API - Game Lifecycle
  // ==========================================================================

  /**
   * Create a new game via REST API.
   */
  async createGame(
    gameType: GameType,
    playerNames: string[],
    gameName = 'New Game'
  ): Promise<CommandResult> {
    const response = await this.post('/api/game/new', {
      gameType,
      playerNames,
      gameName,
    });

    if (response.ok) {
      const data = await response.json();
      return { success: true, message: 'Game created', gameId: data.gameId };
    }

    return { success: false, message: await this.getErrorMessage(response) };
  }

  /**
   * Get current game state via REST API.
   */
  async getGameState(gameId: string): Promise<GameModel | null> {
    const response = await this.get(`/api/gamestate/${gameId}`);
    if (response.ok) {
      return (await response.json()) as GameModel;
    }
    return null;
  }

  /**
   * Refresh game state after reconnect.
   */
  async refreshGameState(gameId: string): Promise<void> {
    const gameModel = await this.getGameState(gameId);
    if (gameModel && this.events.onGameStateUpdated) {
      this.events.onGameStateUpdated(gameModel);
    }
  }

  /**
   * Get available games via REST API.
   */
  async getAvailableGames(): Promise<GameInfo[]> {
    const response = await this.get('/api/companion/games');
    if (response.ok) {
      const data = await response.json();
      return data.games ?? [];
    }
    return [];
  }

  /**
   * Get player profiles via REST API.
   */
  async getPlayers(): Promise<PlayerProfile[]> {
    const response = await this.get('/api/players');
    if (response.ok) {
      const data = await response.json();
      return data.players ?? [];
    }
    return [];
  }

  // ==========================================================================
  // REST API - Game Commands
  // ==========================================================================

  /**
   * Execute Undo command.
   */
  async undo(): Promise<CommandResult> {
    return this.executeCommand('undo');
  }

  /**
   * Execute Redo command.
   */
  async redo(): Promise<CommandResult> {
    return this.executeCommand('redo');
  }

  /**
   * Execute Next command.
   */
  async next(): Promise<CommandResult> {
    return this.executeCommand('next');
  }

  /**
   * Execute Shuffle command.
   */
  async shuffle(): Promise<CommandResult> {
    return this.executeCommand('shuffle');
  }

  /**
   * Execute Balance Board command.
   */
  async balanceBoard(): Promise<CommandResult> {
    return this.executeCommand('balance');
  }

  /**
   * Execute Roll command.
   */
  async roll(die1: number, die2: number): Promise<CommandResult> {
    return this.executeCommand('roll', {
      turnRollModel: { die1, die2, specialDice: 'None', rollIndex: 0 },
    });
  }

  /**
   * Execute Purchase command.
   */
  async purchase(entitlement: Entitlement): Promise<CommandResult> {
    return this.executeCommand('purchase', { entitlement });
  }

  /**
   * Execute Road Purchase command.
   */
  async purchaseRoad(roadKey: RoadKey): Promise<CommandResult> {
    return this.executeCommand('roadPurchase', { roadKey });
  }

  /**
   * Execute Building Upgrade command.
   */
  async upgradeBuilding(buildingKey: BuildingKey): Promise<CommandResult> {
    return this.executeCommand('buildingUpgrade', { buildingKey });
  }

  /**
   * Execute Move Robber command.
   */
  async moveRobber(
    coordinates: HexCoordinates,
    targetPlayerId?: string
  ): Promise<CommandResult> {
    return this.executeCommand('moveRobber', {
      coordinates,
      targetPlayerId: targetPlayerId ?? null,
    });
  }

  /**
   * Execute Go First command (during roll order phase).
   */
  async goFirst(firstPlayerId: string): Promise<CommandResult> {
    return this.executeCommand('goFirst', { firstPlayerId });
  }

  /**
   * Execute Participating In Supplemental command.
   */
  async setParticipatingInSupplemental(
    playerId: string,
    participating: boolean
  ): Promise<CommandResult> {
    return this.executeCommand('participatingInSupplemental', {
      playerId,
      participating,
    });
  }

  // ==========================================================================
  // Event Handlers
  // ==========================================================================

  /**
   * Set event handlers for game updates.
   */
  setEventHandlers(events: GameServiceProxyEvents): void {
    this.events = events;
  }

  /**
   * Subscribe to game state updates.
   */
  onGameStateUpdated(handler: (gameModel: GameModel) => void): () => void {
    this.events.onGameStateUpdated = handler;
    return () => {
      this.events.onGameStateUpdated = undefined;
    };
  }

  /**
   * Subscribe to player profile updates.
   */
  onPlayersUpdated(handler: (players: PlayerProfile[]) => void): () => void {
    this.events.onPlayersUpdated = handler;
    return () => {
      this.events.onPlayersUpdated = undefined;
    };
  }

  /**
   * Subscribe to connection state changes.
   */
  onConnectionStateChanged(
    handler: (state: ConnectionState) => void
  ): () => void {
    this.events.onConnectionStateChanged = handler;
    return () => {
      this.events.onConnectionStateChanged = undefined;
    };
  }

  // ==========================================================================
  // Getters
  // ==========================================================================

  get currentGameId(): string | null {
    return this.gameId;
  }

  get currentPlayerId(): string {
    return this.playerId;
  }

  get connectionState(): ConnectionState {
    if (!this.connection) return 'disconnected';
    switch (this.connection.state) {
      case HubConnectionState.Connected:
        return 'connected';
      case HubConnectionState.Connecting:
        return 'connecting';
      case HubConnectionState.Reconnecting:
        return 'reconnecting';
      default:
        return 'disconnected';
    }
  }

  // ==========================================================================
  // Disposal
  // ==========================================================================

  /**
   * Dispose the proxy and release resources.
   */
  async dispose(): Promise<void> {
    this.isDisposed = true;
    await this.disconnect();
    this.events = {};
  }

  // ==========================================================================
  // Private Methods
  // ==========================================================================

  private setupEventHandlers(): void {
    if (!this.connection) return;

    // Game state updates
    this.connection.on('GameStateUpdated', (gameModel: GameModel) => {
      this.log('Received GameStateUpdated');
      this.events.onGameStateUpdated?.(gameModel);
    });

    // Player profile updates
    this.connection.on('PlayersUpdated', (players: PlayerProfile[]) => {
      this.log(`Received PlayersUpdated with ${players.length} players`);
      this.events.onPlayersUpdated?.(players);
    });

    // Connection events
    this.connection.onreconnecting((error) => {
      this.log('Reconnecting...', error);
      this.notifyConnectionState('reconnecting');
    });

    this.connection.onreconnected(async (connectionId) => {
      this.log(`Reconnected with connectionId: ${connectionId}`);
      this.notifyConnectionState('connected');

      // Re-join game after reconnect
      if (this.gameId) {
        try {
          await this.joinGame(this.gameId);
          await this.refreshGameState(this.gameId);
        } catch (error) {
          this.log('Failed to re-join game after reconnect', error);
        }
      }
    });

    this.connection.onclose((error) => {
      this.log('Connection closed', error);
      this.notifyConnectionState('disconnected');
    });
  }

  private async executeCommand(
    command: string,
    payload?: Record<string, unknown>
  ): Promise<CommandResult> {
    if (!this.gameId) {
      return { success: false, message: 'Not connected to a game' };
    }

    const body = {
      gameId: this.gameId,
      playerId: this.playerId,
      command,
      ...payload,
    };

    const response = await this.post('/api/game/action', body);

    if (response.ok) {
      return { success: true, message: `${command} executed` };
    }

    return { success: false, message: await this.getErrorMessage(response) };
  }

  private async get(path: string): Promise<Response> {
    return fetch(`${this.serviceUrl}${path}`, {
      method: 'GET',
      headers: { 'Content-Type': 'application/json' },
    });
  }

  private async post(
    path: string,
    body: Record<string, unknown>
  ): Promise<Response> {
    return fetch(`${this.serviceUrl}${path}`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify(body),
    });
  }

  private async getErrorMessage(response: Response): Promise<string> {
    try {
      const text = await response.text();
      return text || `HTTP ${response.status}`;
    } catch {
      return `HTTP ${response.status}`;
    }
  }

  private ensureConnected(): void {
    if (!this.connection || this.connection.state !== HubConnectionState.Connected) {
      throw new Error('Not connected to SignalR hub');
    }
  }

  private notifyConnectionState(state: ConnectionState): void {
    this.events.onConnectionStateChanged?.(state);
  }

  private log(message: string, error?: unknown): void {
    const prefix = `[GameServiceProxy] [${this.playerId}]`;
    if (error) {
      console.error(`${prefix} ${message}`, error);
      this.events.onError?.(
        error instanceof Error ? error.message : String(error)
      );
    } else {
      console.log(`${prefix} ${message}`);
    }
  }
}

// ============================================================================
// Singleton Instance
// ============================================================================

let proxyInstance: GameServiceProxy | null = null;

/**
 * Get or create the singleton GameServiceProxy instance.
 */
export function getGameServiceProxy(playerId: string): GameServiceProxy {
  if (!proxyInstance || proxyInstance.currentPlayerId !== playerId) {
    proxyInstance?.dispose();
    proxyInstance = new GameServiceProxy(playerId);
  }
  return proxyInstance;
}
