/**
 * RecordingPlayer - Plays back recorded game sessions for testing.
 *
 * Loads recordings from the GameService API and executes each action
 * through GameServiceProxy, verifying game hashes at each step.
 *
 * This enables deterministic integration testing by replaying
 * previously recorded game sessions.
 */

import { GameServiceProxy } from './GameServiceProxy';
import type { GameModel } from '@/types/generated/models/game-model';
import type { Entitlement } from '@/types/generated/models/entitlement';
import type { RoadKey } from '@/types/generated/models/road-key';
import type { BuildingKey } from '@/types/generated/models/building-key';
import type { HexCoordinates } from '@/types/generated/models/hex-coordinates';
import { serviceConfig } from './config';

// ============================================================================
// Types
// ============================================================================

/** Recording entity as stored in the database */
export interface RecordingEntity {
  id: string;
  name: string;
  createdAt: string;
  gameType: string;
  playerCount: number;
  playerIds: string;
  actionCount: number;
  data: string; // JSON string of RecordingData
}

/** Parsed recording data */
export interface RecordingData {
  initialGameModel: GameModel;
  actions: RecordedAction[];
}

/** Base interface for all recorded actions */
export interface RecordedActionBase {
  type: string;
  expectedGameHash: string;
  expectedGameState: number;
}

/** Union of all recorded action types */
export type RecordedAction =
  | UndoRecord
  | RedoRecord
  | NextRecord
  | ShuffleRecord
  | BalanceBoardRecord
  | PurchaseRecord
  | BuildingUpgradeRecord
  | RoadPurchaseRecord
  | RollRecord
  | MoveRobberRecord
  | GoFirstRecord
  | SetPlayerOrderRecord
  | DeclareWinnerRecord
  | ParticipatingInSupplementalRecord
  | SwapTileResourcesRecord;

export interface UndoRecord extends RecordedActionBase {
  type: 'undoRecord';
}

export interface RedoRecord extends RecordedActionBase {
  type: 'redoRecord';
}

export interface NextRecord extends RecordedActionBase {
  type: 'nextRecord';
}

export interface ShuffleRecord extends RecordedActionBase {
  type: 'shuffleRecord';
}

export interface BalanceBoardRecord extends RecordedActionBase {
  type: 'balanceBoard';
}

export interface PurchaseRecord extends RecordedActionBase {
  type: 'purchase';
  entitlement: Entitlement;
}

export interface BuildingUpgradeRecord extends RecordedActionBase {
  type: 'buildingUpgrade';
  buildingKey: BuildingKey;
}

export interface RoadPurchaseRecord extends RecordedActionBase {
  type: 'roadPurchase';
  roadKey: RoadKey;
}

export interface RollRecord extends RecordedActionBase {
  type: 'roll';
  roll: TurnRollModel;
}

export interface TurnRollModel {
  redRoll: number;
  whiteRoll: number;
  specialDice: string;
  normalRoll: string;
}

export interface MoveRobberRecord extends RecordedActionBase {
  type: 'moveRobber';
  coordinates: HexCoordinates;
  targetPlayerId: string | null;
}

export interface GoFirstRecord extends RecordedActionBase {
  type: 'goFirst';
  playerId: string;
}

export interface SetPlayerOrderRecord extends RecordedActionBase {
  type: 'setPlayerOrder';
  playerIds: string[];
}

export interface DeclareWinnerRecord extends RecordedActionBase {
  type: 'declareWinner';
  winnerId: string;
  victoryPoints?: Record<string, number>;
}

export interface ParticipatingInSupplementalRecord extends RecordedActionBase {
  type: 'participatingInSupplemental';
  playerId: string;
  participating: boolean;
}

export interface SwapTileResourcesRecord extends RecordedActionBase {
  type: 'swapTileResources';
  sourceTileCoordinates: HexCoordinates;
  destinationTileCoordinates: HexCoordinates;
  sourceCurrentResource: string;
  destinationCurrentResource: string;
}

/** Result of playing back an entire recording */
export interface PlaybackResult {
  success: boolean;
  actionsExecuted: number;
  totalActions: number;
  failedAtIndex?: number;
  failedAction?: RecordedAction;
  error?: string;
  hashMismatch?: {
    expected: string;
    actual: string;
  };
}

/** Result of a single step execution */
export interface StepResult {
  success: boolean;
  actionIndex: number;
  action: RecordedAction;
  actualHash: string;
  expectedHash: string;
  hashMatch: boolean;
  error?: string;
}

/** Recording list item (summary without full data) */
export interface RecordingListItem {
  id: string;
  name: string;
  createdAt: string;
  gameType: string;
  playerCount: number;
  actionCount: number;
}

// ============================================================================
// RecordingPlayer Class
// ============================================================================

export class RecordingPlayer {
  private serviceUrl: string;
  private proxy: GameServiceProxy | null = null;
  private currentRecording: RecordingData | null = null;
  private currentGameId: string | null = null;
  private latestGameModel: GameModel | null = null;
  private actionIndex: number = 0;

  constructor(serviceUrl?: string) {
    this.serviceUrl = serviceUrl ?? serviceConfig.serviceUrl;
  }

  // ==========================================================================
  // Recording API
  // ==========================================================================

  /**
   * Get list of all available recordings.
   */
  async listRecordings(): Promise<RecordingListItem[]> {
    const response = await fetch(`${this.serviceUrl}/api/recordings`);
    if (!response.ok) {
      throw new Error(`Failed to list recordings: ${response.status}`);
    }
    const data = await response.json();
    return data.recordings ?? data ?? [];
  }

  /**
   * Load a recording by ID.
   */
  async loadRecording(recordingId: string): Promise<RecordingData> {
    const response = await fetch(`${this.serviceUrl}/api/recording/${recordingId}`);
    if (!response.ok) {
      throw new Error(`Failed to load recording: ${response.status}`);
    }
    const entity: RecordingEntity = await response.json();

    // Parse the nested JSON data
    const data: RecordingData = JSON.parse(entity.data);
    this.currentRecording = data;
    this.actionIndex = 0;

    return data;
  }

  // ==========================================================================
  // Playback Control
  // ==========================================================================

  /**
   * Initialize playback by loading the recording's exact initial game state.
   * This ensures the board layout and all state matches the recording exactly.
   * Returns the game ID.
   */
  async initializePlayback(recording?: RecordingData): Promise<string> {
    const rec = recording ?? this.currentRecording;
    if (!rec) {
      throw new Error('No recording loaded');
    }

    // Use the first player from the recording
    const firstPlayerId = rec.initialGameModel.players[0].id;

    // Create proxy for first player
    this.proxy = new GameServiceProxy(firstPlayerId, this.serviceUrl);

    // Set up game state listener
    this.proxy.onGameStateUpdated((gameModel) => {
      this.latestGameModel = gameModel;
    });

    // Load the exact game state from the recording (preserves board layout, random seed, etc.)
    const result = await this.proxy.loadGameModel(rec.initialGameModel, true);

    if (!result.success || !result.gameId) {
      throw new Error(`Failed to load game model: ${result.message}`);
    }

    this.currentGameId = result.gameId;

    // Connect to SignalR and join the game
    await this.proxy.connect(this.currentGameId);

    // Wait for initial game state
    await this.waitForGameState();

    // Verify the initial hash matches the recording
    if (this.latestGameModel?.gameHash !== rec.initialGameModel.gameHash) {
      console.warn(
        `Initial hash mismatch: expected ${rec.initialGameModel.gameHash}, got ${this.latestGameModel?.gameHash}`
      );
    }

    this.actionIndex = 0;
    return this.currentGameId;
  }

  /**
   * Execute a single action and verify the result.
   */
  async executeStep(): Promise<StepResult> {
    if (!this.currentRecording) {
      throw new Error('No recording loaded');
    }
    if (!this.proxy || !this.currentGameId) {
      throw new Error('Playback not initialized');
    }
    if (this.actionIndex >= this.currentRecording.actions.length) {
      throw new Error('No more actions to execute');
    }

    const action = this.currentRecording.actions[this.actionIndex];
    const hashBefore = this.latestGameModel?.gameHash;

    try {
      // Execute the action
      const cmdResult = await this.executeAction(action);

      if (!cmdResult.success) {
        return {
          success: false,
          actionIndex: this.actionIndex,
          action,
          actualHash: this.latestGameModel?.gameHash ?? '',
          expectedHash: action.expectedGameHash,
          hashMatch: false,
          error: cmdResult.message,
        };
      }

      // Wait for game state update
      await this.waitForHashChange(hashBefore);

      const actualHash = this.latestGameModel?.gameHash ?? '';
      const hashMatch = actualHash === action.expectedGameHash;

      this.actionIndex++;

      return {
        success: hashMatch,
        actionIndex: this.actionIndex - 1,
        action,
        actualHash,
        expectedHash: action.expectedGameHash,
        hashMatch,
        error: hashMatch ? undefined : 'Hash mismatch',
      };
    } catch (error) {
      return {
        success: false,
        actionIndex: this.actionIndex,
        action,
        actualHash: this.latestGameModel?.gameHash ?? '',
        expectedHash: action.expectedGameHash,
        hashMatch: false,
        error: error instanceof Error ? error.message : String(error),
      };
    }
  }

  /**
   * Play back all remaining actions in the recording.
   */
  async playAll(): Promise<PlaybackResult> {
    if (!this.currentRecording) {
      throw new Error('No recording loaded');
    }

    const totalActions = this.currentRecording.actions.length;
    let actionsExecuted = 0;

    while (this.actionIndex < totalActions) {
      const stepResult = await this.executeStep();

      if (!stepResult.success) {
        return {
          success: false,
          actionsExecuted,
          totalActions,
          failedAtIndex: stepResult.actionIndex,
          failedAction: stepResult.action,
          error: stepResult.error,
          hashMismatch: stepResult.hashMatch
            ? undefined
            : {
                expected: stepResult.expectedHash,
                actual: stepResult.actualHash,
              },
        };
      }

      actionsExecuted++;
    }

    return {
      success: true,
      actionsExecuted,
      totalActions,
    };
  }

  /**
   * Play back a recording from start to finish.
   * Convenience method that loads, initializes, and plays all actions.
   */
  async playRecording(recordingId: string): Promise<PlaybackResult> {
    await this.loadRecording(recordingId);
    await this.initializePlayback();
    return this.playAll();
  }

  // ==========================================================================
  // Getters
  // ==========================================================================

  get currentActionIndex(): number {
    return this.actionIndex;
  }

  get totalActions(): number {
    return this.currentRecording?.actions.length ?? 0;
  }

  get gameModel(): GameModel | null {
    return this.latestGameModel;
  }

  get gameId(): string | null {
    return this.currentGameId;
  }

  get recording(): RecordingData | null {
    return this.currentRecording;
  }

  // ==========================================================================
  // Cleanup
  // ==========================================================================

  async dispose(): Promise<void> {
    if (this.proxy) {
      await this.proxy.dispose();
      this.proxy = null;
    }
    this.currentRecording = null;
    this.currentGameId = null;
    this.latestGameModel = null;
    this.actionIndex = 0;
  }

  // ==========================================================================
  // Private Methods
  // ==========================================================================

  private async executeAction(
    action: RecordedAction
  ): Promise<{ success: boolean; message: string }> {
    if (!this.proxy) {
      return { success: false, message: 'Proxy not initialized' };
    }

    switch (action.type) {
      case 'undoRecord':
        return this.proxy.undo();

      case 'redoRecord':
        return this.proxy.redo();

      case 'nextRecord':
        return this.proxy.next();

      case 'shuffleRecord':
        return this.proxy.shuffle();

      case 'balanceBoard':
        return this.proxy.balanceBoard();

      case 'purchase':
        return this.proxy.purchase(action.entitlement);

      case 'buildingUpgrade':
        return this.proxy.upgradeBuilding(action.buildingKey);

      case 'roadPurchase':
        return this.proxy.purchaseRoad(action.roadKey);

      case 'roll':
        return this.proxy.roll(action.roll.redRoll, action.roll.whiteRoll);

      case 'moveRobber':
        return this.proxy.moveRobber(action.coordinates, action.targetPlayerId ?? undefined);

      case 'goFirst':
        return this.proxy.goFirst(action.playerId);

      case 'setPlayerOrder':
        return this.proxy.setPlayerOrder(action.playerIds);

      case 'declareWinner':
        return this.proxy.declareWinner(action.winnerId, action.victoryPoints);

      case 'participatingInSupplemental':
        return this.proxy.setParticipatingInSupplemental(action.playerId, action.participating);

      case 'swapTileResources':
        return this.proxy.swapTileResources(
          action.sourceTileCoordinates,
          action.destinationTileCoordinates,
          action.sourceCurrentResource,
          action.destinationCurrentResource
        );

      default:
        return {
          success: false,
          message: `Unknown action type: ${(action as RecordedActionBase).type}`,
        };
    }
  }

  private async waitForGameState(timeoutMs: number = 5000): Promise<void> {
    const start = Date.now();
    while (!this.latestGameModel && Date.now() - start < timeoutMs) {
      await new Promise((r) => setTimeout(r, 50));
    }
    if (!this.latestGameModel) {
      throw new Error('Timeout waiting for game state');
    }
  }

  private async waitForHashChange(
    previousHash: string | undefined,
    timeoutMs: number = 5000
  ): Promise<void> {
    const start = Date.now();
    while (Date.now() - start < timeoutMs) {
      if (this.latestGameModel && this.latestGameModel.gameHash !== previousHash) {
        return;
      }
      await new Promise((r) => setTimeout(r, 50));
    }
    // Don't throw - some actions may not change the hash
  }
}
