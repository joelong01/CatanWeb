/**
 * Zustand stores barrel export.
 * Import stores and selectors from this file for convenience.
 */

export {
  useGameStore,
  selectGameState,
  selectCurrentTurnPlayerId,
  selectIsMyTurn,
  selectActionFlags,
  selectPlayers,
  selectTiles,
  selectBuildings,
  selectRoads,
  selectHarbors,
  selectRobber,
  selectHouseRules,
  selectGameId,
  selectGameType,
  selectPlayerProfile,
  selectMyPlayer,
  type PlayerProfile,
} from './gameStore';

export {
  useUIStore,
  selectIsPortrait,
  selectIsMobile,
  selectActivePortraitTab,
  selectHasOpenOverlay,
  type PortraitTab,
} from './uiStore';

export {
  useConnectionStore,
  selectIsConnected,
  selectIsConnecting,
  selectConnectionStatus,
  selectConnectedGameId,
  selectHasError,
  selectErrorMessage,
  type ConnectionStatus,
} from './connectionStore';
