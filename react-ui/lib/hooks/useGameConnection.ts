/**
 * React hook for managing GameService connection.
 *
 * Integrates GameServiceProxy with Zustand stores and handles:
 * - Connection lifecycle
 * - State synchronization
 * - Page visibility (mobile sleep/wake recovery)
 */

import { useEffect, useCallback, useMemo, useRef } from 'react';
import { GameServiceProxy, getGameServiceProxy } from '../services/GameServiceProxy';
import { useGameStore } from '../stores/gameStore';
import { useConnectionStore } from '../stores/connectionStore';
import { reconcileGameModel } from '../utils/reconciliation';
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
export function useGameConnection(options: UseGameConnectionOptions): UseGameConnectionResult {
  const { playerId, gameId, autoConnect = true } = options;

  // Get game store actions (stable references from Zustand)
  const setGameModel = useGameStore((state) => state.setGameModel);
  const setCurrentPlayerId = useGameStore((state) => state.setCurrentPlayerId);

  // Get connection store actions (stable references from Zustand)
  // These don't change on state updates, so they're safe for dependencies
  const setConnected = useConnectionStore((state) => state.setConnected);
  const setStatus = useConnectionStore((state) => state.setStatus);
  const setReconnecting = useConnectionStore((state) => state.setReconnecting);
  const incrementReconnectAttempts = useConnectionStore(
    (state) => state.incrementReconnectAttempts
  );
  const setDisconnected = useConnectionStore((state) => state.setDisconnected);
  const setPageVisible = useConnectionStore((state) => state.setPageVisible);

  // Get connection store state (for return value)
  const connectionStatus = useConnectionStore((state) => state.status);

  // Track previous GameModel for reconciliation
  const prevGameModelRef = useRef<GameModel | null>(null);

  // Get or create proxy - useMemo ensures stable reference across renders
  // The proxy is a singleton per playerId, so this is safe
  const proxy = useMemo(() => getGameServiceProxy(playerId), [playerId]);

  // Set up event handlers - only depends on proxy and action functions
  useEffect(() => {
    console.log('[useGameConnection] Setting up event handlers');
    // Handle game state updates with reconciliation
    // Reconciliation preserves references to unchanged items, enabling
    // React.memo to skip re-renders for tiles/roads/buildings that didn't change
    const unsubGameState = proxy.onGameStateUpdated((gameModel: GameModel) => {
      console.log(
        '[useGameConnection] onGameStateUpdated handler called, tiles:',
        gameModel?.tiles?.length
      );
      console.log(
        '[useGameConnection] prevGameModelRef.current tiles:',
        prevGameModelRef.current?.tiles?.length ?? 'null'
      );
      const reconciled = reconcileGameModel(prevGameModelRef.current, gameModel);
      console.log(
        '[useGameConnection] Reconciled same as prev?',
        reconciled === prevGameModelRef.current
      );
      console.log('[useGameConnection] Reconciled same as incoming?', reconciled === gameModel);
      console.log('[useGameConnection] Calling setGameModel, tiles:', reconciled?.tiles?.length);
      prevGameModelRef.current = reconciled;
      setGameModel(reconciled);
      console.log('[useGameConnection] setGameModel called');
    });

    // Handle connection state changes
    const unsubConnection = proxy.onConnectionStateChanged((state) => {
      switch (state) {
        case 'connected':
          if (proxy.currentGameId) {
            setConnected(proxy.currentGameId);
          } else {
            setStatus('connected');
          }
          break;
        case 'connecting':
          setStatus('connecting');
          break;
        case 'reconnecting':
          setReconnecting();
          incrementReconnectAttempts();
          break;
        case 'disconnected':
          setDisconnected();
          break;
      }
    });

    // Set current player ID in store
    setCurrentPlayerId(playerId);

    return () => {
      unsubGameState();
      unsubConnection();
    };
  }, [
    proxy,
    playerId,
    setGameModel,
    setCurrentPlayerId,
    setConnected,
    setStatus,
    setReconnecting,
    incrementReconnectAttempts,
    setDisconnected,
  ]);

  // Page visibility handler for mobile sleep/wake recovery
  // When iOS/Android suspends the browser, the WebSocket is killed by the OS.
  // SignalR's withAutomaticReconnect doesn't help because the connection
  // goes to Disconnected state (not Reconnecting). We detect this on wake.
  useEffect(() => {
    const handleVisibilityChange = async () => {
      const isVisible = document.visibilityState === 'visible';
      setPageVisible(isVisible);

      if (isVisible && proxy.needsReconnection()) {
        // Page became visible and connection is dead - force reconnect
        console.log('[useGameConnection] Page visible, connection dead, reconnecting...');
        try {
          await proxy.forceReconnect();
          console.log('[useGameConnection] Reconnected successfully');
        } catch (error) {
          console.error('[useGameConnection] Reconnect failed:', error);
          setDisconnected(error instanceof Error ? error.message : 'Reconnection failed');
        }
      }
    };

    document.addEventListener('visibilitychange', handleVisibilityChange);
    return () => {
      document.removeEventListener('visibilitychange', handleVisibilityChange);
    };
  }, [proxy, setPageVisible, setDisconnected]);

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
      setConnected(gameIdToJoin);
    },
    [proxy, setConnected]
  );

  const leaveGame = useCallback(async () => {
    if (proxy.currentGameId) {
      await proxy.leaveGame(proxy.currentGameId);
    }
  }, [proxy]);

  return {
    proxy,
    isConnected: connectionStatus === 'connected',
    isConnecting: connectionStatus === 'connecting' || connectionStatus === 'reconnecting',
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
    throw new Error('useGameCommands must be used within a connected game context');
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
