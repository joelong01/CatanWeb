/**
 * Extensions Library
 *
 * TypeScript equivalents of C# extension methods from Catan3.Shared/Extensions/.
 * These provide consistent, reusable utility functions for working with game models.
 *
 * @module extensions
 */

// Player extensions
export { playerFromId } from './playerExtensions';

// Tile extensions
export {
  NUMBER_PIPS,
  HEX_DIRECTIONS,
  hexCoordsEqual,
  hexCoordsAdd,
  tileFromCoords,
  adjacentTiles,
  pipsForNumber,
  totalStars,
  tilesWithNumber,
  tilesWithResource,
  tilesWithSixOrEight,
} from './tileExtensions';

// Future exports (uncomment as implemented):
// export * from './buildingExtensions';
// export * from './roadExtensions';
// export * from './gameModelExtensions';
