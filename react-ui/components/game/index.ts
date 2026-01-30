/**
 * Game components index.
 */

// Viewport
export { BoardViewport } from './viewport/BoardViewport';

// Board
export { GameBoard } from './board/GameBoard';
export type { GameBoardProps } from './board/GameBoard';

// Tiles
export { GameTile } from './tiles/GameTile';
export type { GameTileProps } from './tiles/GameTile';
export { HarborHex } from './tiles/HarborHex';
export type { HarborHexProps } from './tiles/HarborHex';
export { NumberToken } from './tiles/NumberToken';
export type { NumberTokenProps } from './tiles/NumberToken';

// Panels
export { FloatingPanel } from './panels/FloatingPanel';
export { PlayersPanel } from './panels/PlayersPanel';
export { ResourcesPanel } from './panels/ResourcesPanel';

// Controls
export { DiceCluster } from './controls/DiceCluster';
export { ActionCluster } from './controls/ActionCluster';
export { MeasurementCluster } from './controls/MeasurementCluster';

// Overlays
export { GoFirstOverlay } from './overlays/GoFirstOverlay';
export type { GoFirstOverlayProps } from './overlays/GoFirstOverlay';
export { SupplementalOverlay } from './overlays/SupplementalOverlay';
export type { SupplementalOverlayProps } from './overlays/SupplementalOverlay';
export { WinnerOverlay } from './overlays/WinnerOverlay';
export type { WinnerOverlayProps, WinnerPlayer } from './overlays/WinnerOverlay';
