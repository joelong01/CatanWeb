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
  cubicCoord,
  HEX_LAYOUTS,
  DIRECTIONS,
  distance,
  getNeighbor,
  getAllNeighbors,
  isAdjacent,
  getSpiralCoordinates,
  getVertexPosition,
  getEdgeMidpoint,
  VERTEX_ANGLES,
  EDGE_ANGLES,
} from './hex-geometry';

export type {
  HexDimensions,
  HexCoordinate,
  PixelPosition,
  Direction,
  HexPosition,
  HexSide,
} from './hex-geometry';

// Visual constants
export {
  HEX_BORDER_FRACTION,
  HEX_CONTENT_SCALE,
  HEX_HOVER_SHRINK,
  HEX_HOVER_SCALE,
  HEX_ICON_HOVER_SCALE,
} from './constants';

// Content components
export { WaterHex, CenterHex, MenuHex } from './content';
export type { WaterHexProps, CenterHexProps, MenuHexProps } from './content';
