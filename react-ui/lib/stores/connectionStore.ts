/**
 * Connection state store - manages SignalR connection status.
 *
 * Tracks connection state, reconnection attempts, and error messages.
 * Used by UI to show connection status indicators.
 */

import { create } from 'zustand';
import { subscribeWithSelector } from 'zustand/middleware';

/** Connection status enum matching SignalR HubConnectionState */
export type ConnectionStatus =
  | 'disconnected'
  | 'connecting'
  | 'connected'
  | 'reconnecting';

interface ConnectionState {
  /** Current connection status */
  status: ConnectionStatus;

  /** Current game ID we're connected to (null if not in a game) */
  gameId: string | null;

  /** Number of reconnection attempts */
  reconnectAttempts: number;

  /** Last error message (null if no error) */
  lastError: string | null;

  /** Timestamp of last successful connection */
  lastConnectedAt: Date | null;

  /** Whether the page is currently visible (for mobile sleep detection) */
  isPageVisible: boolean;
}

interface ConnectionActions {
  /** Set connection status */
  setStatus: (status: ConnectionStatus) => void;

  /** Set connected to a game */
  setConnected: (gameId: string) => void;

  /** Set disconnected state */
  setDisconnected: (error?: string) => void;

  /** Set reconnecting state */
  setReconnecting: () => void;

  /** Increment reconnect attempts */
  incrementReconnectAttempts: () => void;

  /** Reset reconnect attempts (on successful connect) */
  resetReconnectAttempts: () => void;

  /** Set page visibility */
  setPageVisible: (visible: boolean) => void;

  /** Clear error */
  clearError: () => void;
}

type ConnectionStore = ConnectionState & ConnectionActions;

const initialState: ConnectionState = {
  status: 'disconnected',
  gameId: null,
  reconnectAttempts: 0,
  lastError: null,
  lastConnectedAt: null,
  isPageVisible: true,
};

/**
 * Zustand store for connection state.
 * Uses subscribeWithSelector for fine-grained status subscriptions.
 */
export const useConnectionStore = create<ConnectionStore>()(
  subscribeWithSelector((set) => ({
    ...initialState,

    setStatus: (status) => set({ status }),

    setConnected: (gameId) =>
      set({
        status: 'connected',
        gameId,
        lastError: null,
        reconnectAttempts: 0,
        lastConnectedAt: new Date(),
      }),

    setDisconnected: (error) =>
      set({
        status: 'disconnected',
        lastError: error ?? null,
      }),

    setReconnecting: () => set({ status: 'reconnecting' }),

    incrementReconnectAttempts: () =>
      set((state) => ({ reconnectAttempts: state.reconnectAttempts + 1 })),

    resetReconnectAttempts: () => set({ reconnectAttempts: 0 }),

    setPageVisible: (isPageVisible) => set({ isPageVisible }),

    clearError: () => set({ lastError: null }),
  }))
);

// ============================================================================
// Selectors
// ============================================================================

/** Select whether we're connected */
export const selectIsConnected = (state: ConnectionStore) =>
  state.status === 'connected';

/** Select whether we're attempting to connect */
export const selectIsConnecting = (state: ConnectionStore) =>
  state.status === 'connecting' || state.status === 'reconnecting';

/** Select connection status for display */
export const selectConnectionStatus = (state: ConnectionStore) => state.status;

/** Select current game ID */
export const selectConnectedGameId = (state: ConnectionStore) => state.gameId;

/** Select whether there's an error */
export const selectHasError = (state: ConnectionStore) =>
  state.lastError !== null;

/** Select the error message */
export const selectErrorMessage = (state: ConnectionStore) => state.lastError;
