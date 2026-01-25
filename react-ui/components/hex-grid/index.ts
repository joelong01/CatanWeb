/**
 * Hex grid layout components and utilities.
 *
 * Reusable hex grid system using Red Blob Games formulas for flat-top hexagons.
 */

// Core layout components
export { HexGrid } from './HexGrid';
export type { HexGridProps, HexGridItem } from './HexGrid';

export { HexTile } from './HexTile';
export type { HexTileProps } from './HexTile';

// Geometry utilities
export {
  calculateHexDimensions,
  hexToPixel,
  HEX_LAYOUTS,
} from './hex-geometry';

export type {
  HexDimensions,
  HexCoordinate,
  PixelPosition,
} from './hex-geometry';
