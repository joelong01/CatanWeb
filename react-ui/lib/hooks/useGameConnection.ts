/**
 * React hook for managing GameService connection.
 *
 * Integrates GameServiceProxy with Zustand stores and handles:
 * - Connection lifecycle
 * - State synchronization
 * - Page visibility (mobile sleep/wake recovery)
 */

import { useEffect, useRef, useCallback } from 'react';
import { GameServiceProxy, getGameServiceProxy } from '../services/GameServiceProxy';
import { useGameStore } from '../stores/gameStore';
import { useConnectionStore } from '../stores/connectionStore';
import type { GameModel } from '@/types/generated/models/game-model';

interface UseGameConnectionOptions {
  /** Player ID for this connection */
  playerId: string;
  /** Game ID to join (optional - can join later) */
  gameId?: string;
  /** Auto-connect on mount (default: true) */
  autoConnect?: boolean;
}

interface UseGameConnectionResult {
  /** The GameServiceProxy instance */
  proxy: GameServiceProxy;
  /** Current connection state */
  isConnected: boolean;
  /** Whether we're attempting to connect */
  isConnecting: boolean;
  /** Current game ID if connected to a game */
  currentGameId: string | null;
  /** Connect to the service */
  connect: (gameId?: string) => Promise<void>;
  /** Disconnect from the service */
  disconnect: () => Promise<void>;
  /** Join a specific game */
  joinGame: (gameId: string) => Promise<void>;
  /** Leave the current game */
  leaveGame: () => Promise<void>;
}

/**
 * Hook for managing GameService connection with Zustand integration.
 */
export function useGameConnection(
  options: UseGameConnectionOptions
): UseGameConnectionResult {
  const { playerId, gameId, autoConnect = true } = options;

  // Get stores
  const setGameModel = useGameStore((state) => state.setGameModel);
  const setCurrentPlayerId = useGameStore((state) => state.setCurrentPlayerId);
  const connectionStore = useConnectionStore();

  // Proxy ref to maintain instance across renders
  const proxyRef = useRef<GameServiceProxy | null>(null);

  // Get or create proxy
  if (!proxyRef.current) {
    proxyRef.current = getGameServiceProxy(playerId);
  }
  const proxy = proxyRef.current;

  // Set up event handlers
  useEffect(() => {
    // Handle game state updates
    const unsubGameState = proxy.onGameStateUpdated((gameModel: GameModel) => {
      setGameModel(gameModel);
    });

    // Handle connection state changes
    const unsubConnection = proxy.onConnectionStateChanged((state) => {
      switch (state) {
        case 'connected':
          if (proxy.currentGameId) {
            connectionStore.setConnected(proxy.currentGameId);
          } else {
            connectionStore.setStatus('connected');
          }
          break;
        case 'connecting':
          connectionStore.setStatus('connecting');
          break;
        case 'reconnecting':
          connectionStore.setReconnecting();
          connectionStore.incrementReconnectAttempts();
          break;
        case 'disconnected':
          connectionStore.setDisconnected();
          break;
      }
    });

    // Set current player ID in store
    setCurrentPlayerId(playerId);

    return () => {
      unsubGameState();
      unsubConnection();
    };
  }, [proxy, playerId, setGameModel, setCurrentPlayerId, connectionStore]);

  // Page visibility handler for mobile sleep/wake recovery
  useEffect(() => {
    const handleVisibilityChange = async () => {
      const isVisible = document.visibilityState === 'visible';
      connectionStore.setPageVisible(isVisible);

      if (isVisible && proxy.connectionState === 'disconnected') {
        // Page became visible and we're disconnected - force reconnect
        console.log('[useGameConnection] Page visible, forcing reconnect');
        try {
          await proxy.forceReconnect();
        } catch (error) {
          console.error('[useGameConnection] Reconnect failed:', error);
        }
      }
    };

    document.addEventListener('visibilitychange', handleVisibilityChange);
    return () => {
      document.removeEventListener('visibilitychange', handleVisibilityChange);
    };
  }, [proxy, connectionStore]);

  // Auto-connect on mount
  useEffect(() => {
    if (autoConnect) {
      proxy.connect(gameId).catch((error) => {
        console.error('[useGameConnection] Auto-connect failed:', error);
      });
    }

    // Cleanup on unmount
    return () => {
      // Don't disconnect on unmount - the proxy is a singleton
      // and may be used by other components
    };
  }, [proxy, gameId, autoConnect]);

  // Callbacks
  const connect = useCallback(
    async (gameIdToJoin?: string) => {
      await proxy.connect(gameIdToJoin);
    },
    [proxy]
  );

  const disconnect = useCallback(async () => {
    await proxy.disconnect();
  }, [proxy]);

  const joinGame = useCallback(
    async (gameIdToJoin: string) => {
      await proxy.joinGame(gameIdToJoin);
      connectionStore.setConnected(gameIdToJoin);
    },
    [proxy, connectionStore]
  );

  const leaveGame = useCallback(async () => {
    if (proxy.currentGameId) {
      await proxy.leaveGame(proxy.currentGameId);
    }
  }, [proxy]);

  return {
    proxy,
    isConnected: connectionStore.status === 'connected',
    isConnecting:
      connectionStore.status === 'connecting' ||
      connectionStore.status === 'reconnecting',
    currentGameId: proxy.currentGameId,
    connect,
    disconnect,
    joinGame,
    leaveGame,
  };
}

/**
 * Hook for accessing game commands without connection management.
 * Use this in components that need to send commands but don't manage the connection.
 */
export function useGameCommands() {
  const playerId = useGameStore((state) => state.currentPlayerId);

  if (!playerId) {
    throw new Error(
      'useGameCommands must be used within a connected game context'
    );
  }

  const proxy = getGameServiceProxy(playerId);

  return {
    undo: () => proxy.undo(),
    redo: () => proxy.redo(),
    next: () => proxy.next(),
    shuffle: () => proxy.shuffle(),
    balanceBoard: () => proxy.balanceBoard(),
    roll: (die1: number, die2: number) => proxy.roll(die1, die2),
    purchase: proxy.purchase.bind(proxy),
    purchaseRoad: proxy.purchaseRoad.bind(proxy),
    upgradeBuilding: proxy.upgradeBuilding.bind(proxy),
    moveRobber: proxy.moveRobber.bind(proxy),
    goFirst: proxy.goFirst.bind(proxy),
  };
}
