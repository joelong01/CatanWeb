/**
 * Integration tests for GameServiceProxy.
 *
 * These tests require a running GameService instance at localhost:8080.
 * Run with: pwsh ./catan.ps1 run (in another terminal)
 * Then: npm run test:integration
 *
 * Test pattern follows Catan3.CLI approach:
 * 1. Verify service is running
 * 2. Get available players
 * 3. Create a game via REST
 * 4. Connect players via SignalR
 * 5. Execute game actions and verify state updates
 */

import { describe, it, expect, beforeAll, beforeEach, afterEach } from 'vitest';
import { GameServiceProxy } from './GameServiceProxy';
import { GameState } from '@/types/generated/models/game-state';
import { GameType } from '@/types/generated/models/game-type';
import type { GameModel } from '@/types/generated/models/game-model';

const SERVICE_URL = 'http://localhost:8080';
const TEST_TIMEOUT = 30000; // 30 seconds for integration tests

// Player IDs matching CLI pattern: {Name}-{Index:D3}
const PLAYER_IDS = ['Joe-001', 'Adrian-002', 'Dodgy-003'];

/**
 * Check if GameService is running.
 */
async function isServiceRunning(): Promise<boolean> {
  try {
    const response = await fetch(`${SERVICE_URL}/api/companion/games`, {
      signal: AbortSignal.timeout(5000),
    });
    return response.ok;
  } catch {
    return false;
  }
}

/**
 * Wait for a condition with timeout.
 */
async function waitFor(
  condition: () => boolean,
  timeoutMs: number = 10000,
  pollMs: number = 100
): Promise<void> {
  const start = Date.now();
  while (Date.now() - start < timeoutMs) {
    if (condition()) return;
    await new Promise((resolve) => setTimeout(resolve, pollMs));
  }
  throw new Error(`Timeout waiting for condition after ${timeoutMs}ms`);
}

describe('GameServiceProxy Integration Tests', () => {
  let serviceAvailable = false;

  beforeAll(async () => {
    serviceAvailable = await isServiceRunning();
    if (!serviceAvailable) {
      console.warn(
        '\n⚠️  GameService not running at localhost:8080\n' +
          '   Run: pwsh ./catan.ps1 run\n' +
          '   Skipping integration tests.\n'
      );
    }
  }, TEST_TIMEOUT);

  describe('Service Health', () => {
    it('should connect to GameService', async () => {
      if (!serviceAvailable) return;

      const response = await fetch(`${SERVICE_URL}/api/companion/games`);
      expect(response.ok).toBe(true);
    });
  });

  describe('REST API - Players', () => {
    it('should get available players', async () => {
      if (!serviceAvailable) return;

      const proxy = new GameServiceProxy('test-player', SERVICE_URL);
      const players = await proxy.getPlayers();

      expect(Array.isArray(players)).toBe(true);
      // Service should have some default players
      console.log(`Found ${players.length} players`);
    });
  });

  describe('REST API - Game Lifecycle', () => {
    it('should create a new game', async () => {
      if (!serviceAvailable) return;

      const proxy = new GameServiceProxy(PLAYER_IDS[0], SERVICE_URL);

      const result = await proxy.createGame(
        GameType.Regular,
        PLAYER_IDS,
        'Integration Test Game'
      );

      expect(result.success).toBe(true);
      expect(result.gameId).toBeDefined();
      console.log(`Created game: ${result.gameId}`);

      await proxy.dispose();
    }, TEST_TIMEOUT);

    it('should get available games', async () => {
      if (!serviceAvailable) return;

      const proxy = new GameServiceProxy('test-player', SERVICE_URL);
      const games = await proxy.getAvailableGames();

      expect(Array.isArray(games)).toBe(true);
      console.log(`Found ${games.length} available games`);

      await proxy.dispose();
    });
  });

  describe('SignalR - Connection', () => {
    let proxy: GameServiceProxy;
    let gameId: string;

    beforeEach(async () => {
      if (!serviceAvailable) return;

      proxy = new GameServiceProxy(PLAYER_IDS[0], SERVICE_URL);

      // Create a game first
      const result = await proxy.createGame(
        GameType.Regular,
        PLAYER_IDS,
        'SignalR Test Game'
      );
      expect(result.success).toBe(true);
      gameId = result.gameId!;
    });

    afterEach(async () => {
      if (proxy) {
        await proxy.dispose();
      }
    });

    it('should connect to SignalR and join game', async () => {
      if (!serviceAvailable) return;

      let receivedGameModel: GameModel | null = null;

      proxy.onGameStateUpdated((gameModel) => {
        receivedGameModel = gameModel;
      });

      await proxy.connect(gameId);

      // Wait for initial GameStateUpdated
      await waitFor(() => receivedGameModel !== null, 10000);

      expect(receivedGameModel).not.toBeNull();
      expect(receivedGameModel!.gameId).toBe(gameId);
      // Game starts in PickingBoard state (players auto-join on creation)
      expect(receivedGameModel!.gameState).toBe(GameState.PickingBoard);

      console.log(`Connected to game ${gameId}, state: ${receivedGameModel!.gameState}`);
    }, TEST_TIMEOUT);
  });

  describe('Game Actions', () => {
    let proxies: GameServiceProxy[] = [];
    let gameId: string;
    let latestGameModel: GameModel | null = null;

    beforeEach(async () => {
      if (!serviceAvailable) return;

      // Create proxies for all players
      proxies = PLAYER_IDS.map((id) => new GameServiceProxy(id, SERVICE_URL));

      // Create game with first player
      const result = await proxies[0].createGame(
        GameType.Regular,
        PLAYER_IDS,
        'Action Test Game'
      );
      expect(result.success).toBe(true);
      gameId = result.gameId!;

      // Set up game state listener on first proxy
      proxies[0].onGameStateUpdated((gameModel) => {
        latestGameModel = gameModel;
      });

      // Connect all players
      for (const proxy of proxies) {
        await proxy.connect(gameId);
      }

      // Wait for initial state
      await waitFor(() => latestGameModel !== null, 10000);
    });

    afterEach(async () => {
      for (const proxy of proxies) {
        await proxy.dispose();
      }
      proxies = [];
      latestGameModel = null;
    });

    it('should execute shuffle command during board picking', async () => {
      if (!serviceAvailable) return;

      expect(latestGameModel).not.toBeNull();
      // Game starts directly in PickingBoard state
      console.log(`Initial state: ${latestGameModel!.gameState}`);
      console.log(`Proxy gameId: ${proxies[0].currentGameId}`);
      console.log(`Proxy playerId: ${proxies[0].currentPlayerId}`);
      expect(latestGameModel!.gameState).toBe(GameState.PickingBoard);

      const hashBefore = latestGameModel!.gameHash;

      const result = await proxies[0].shuffle();
      console.log(`Shuffle result: ${result.success}, message: ${result.message}`);
      expect(result.success).toBe(true);

      // Wait for state update (board should change)
      await waitFor(() => latestGameModel!.gameHash !== hashBefore, 5000);
      console.log(`Shuffled board, new hash: ${latestGameModel!.gameHash}`);
    }, TEST_TIMEOUT);

    it('should execute undo/redo commands', async () => {
      if (!serviceAvailable) return;

      expect(latestGameModel).not.toBeNull();
      // Game starts directly in PickingBoard
      expect(latestGameModel!.gameState).toBe(GameState.PickingBoard);

      // Shuffle to create something to undo
      await proxies[0].shuffle();
      await new Promise((r) => setTimeout(r, 500)); // Brief wait for state update

      const hashBeforeUndo = latestGameModel!.gameHash;

      // Undo should be enabled after shuffle
      if (latestGameModel!.actionFlags.undoEnabled) {
        const undoResult = await proxies[0].undo();
        expect(undoResult.success).toBe(true);

        await waitFor(() => latestGameModel!.gameHash !== hashBeforeUndo, 5000);
        console.log(`Undo successful, hash changed`);

        // Redo should now be enabled
        const hashBeforeRedo = latestGameModel!.gameHash;
        if (latestGameModel!.actionFlags.redoEnabled) {
          const redoResult = await proxies[0].redo();
          expect(redoResult.success).toBe(true);

          await waitFor(() => latestGameModel!.gameHash !== hashBeforeRedo, 5000);
          console.log(`Redo successful, hash changed`);
        }
      }
    }, TEST_TIMEOUT);

    it('should receive updates on all connected proxies', async () => {
      if (!serviceAvailable) return;

      // Set up listeners on all proxies
      const receivedUpdates: Map<string, GameModel> = new Map();

      proxies.forEach((proxy, i) => {
        proxy.onGameStateUpdated((gameModel) => {
          receivedUpdates.set(PLAYER_IDS[i], gameModel);
        });
      });

      // Game starts in PickingBoard - execute shuffle action
      expect(latestGameModel!.gameState).toBe(GameState.PickingBoard);

      // Clear any previous updates
      receivedUpdates.clear();

      await proxies[0].shuffle();

      // Wait for all proxies to receive update
      await waitFor(() => receivedUpdates.size === proxies.length, 10000);

      // Verify all proxies have the same game state
      const hashes = Array.from(receivedUpdates.values()).map((gm) => gm.gameHash);
      const allSameHash = hashes.every((h) => h === hashes[0]);

      expect(allSameHash).toBe(true);
      console.log(`All ${proxies.length} proxies received consistent update`);
    }, TEST_TIMEOUT);
  });
});
