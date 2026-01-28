/**
 * Building extension functions - TypeScript equivalents of C# BuildingModelExtensions.
 *
 * These functions provide consistent building lookup and adjacency utilities
 * matching the server-side API. CRITICAL: Alias handling ensures buildings
 * are found regardless of which hex's vertex is used to reference them.
 *
 * @module buildingExtensions
 */

import type { BuildingModel } from '@/types/generated/models/building-model';
import type { BuildingKey } from '@/types/generated/models/building-key';
import type { HexCoordinates } from '@/types/generated/models/hex-coordinates';
import type { HexPosition } from '@/types/generated/models/hex-position';
import { hexCoordsEqual, hexCoordsAdd } from './tileExtensions';

/**
 * Direction type matching C# Direction enum.
 * Used for calculating adjacent hex positions.
 */
export type Direction =
  | 'North'
  | 'NorthEast'
  | 'SouthEast'
  | 'South'
  | 'SouthWest'
  | 'NorthWest';

/**
 * Direction offsets for hex coordinate system.
 * Maps direction to coordinate offset for finding neighbor hexes.
 */
export const DIRECTION_OFFSETS: Record<Direction, HexCoordinates> = {
  North: { q: 0, r: -1, s: 1 },
  NorthEast: { q: 1, r: -1, s: 0 },
  SouthEast: { q: 1, r: 0, s: -1 },
  South: { q: 0, r: 1, s: -1 },
  SouthWest: { q: -1, r: 1, s: 0 },
  NorthWest: { q: -1, r: 0, s: 1 },
};

/**
 * All valid hex positions for building vertices (excluding None).
 */
export const HEX_POSITIONS: HexPosition[] = [
  'Right',
  'BottomRight',
  'BottomLeft',
  'Left',
  'TopLeft',
  'TopRight',
];

/**
 * Represents an alias position - same vertex referenced from a different hex.
 */
export interface BuildingAlias {
  position: HexPosition;
  direction: Direction;
}

/**
 * Returns the 2 alias positions for a building key.
 * Each vertex on a hex grid is shared by 3 hexes. This function returns
 * the other 2 hexes that share this vertex.
 *
 * @param key - The building key to find aliases for
 * @returns Array of 2 alias definitions (position + direction to neighbor hex)
 *
 * @example
 * // TopRight vertex of hex (0,0,0) is also:
 * // - BottomRight of the North neighbor
 * // - Left of the NorthEast neighbor
 * const aliases = buildingKeyAliases({
 *   hexCoordinates: { q: 0, r: 0, s: 0 },
 *   position: 'TopRight'
 * });
 */
export function buildingKeyAliases(key: BuildingKey): BuildingAlias[] {
  switch (key.position) {
    case 'TopRight':
      return [
        { position: 'BottomRight', direction: 'North' },
        { position: 'Left', direction: 'NorthEast' },
      ];
    case 'Right':
      return [
        { position: 'BottomLeft', direction: 'NorthEast' },
        { position: 'TopLeft', direction: 'SouthEast' },
      ];
    case 'BottomRight':
      return [
        { position: 'TopRight', direction: 'South' },
        { position: 'Left', direction: 'SouthEast' },
      ];
    case 'BottomLeft':
      return [
        { position: 'Right', direction: 'SouthWest' },
        { position: 'TopLeft', direction: 'South' },
      ];
    case 'Left':
      return [
        { position: 'BottomRight', direction: 'NorthWest' },
        { position: 'TopRight', direction: 'SouthWest' },
      ];
    case 'TopLeft':
      return [
        { position: 'Right', direction: 'NorthWest' },
        { position: 'BottomLeft', direction: 'North' },
      ];
    default:
      return [];
  }
}

/**
 * Gets the adjacent hex coordinates in a given direction.
 *
 * @param coords - Starting hex coordinates
 * @param direction - Direction to move
 * @returns Coordinates of the adjacent hex
 */
export function getAdjacentHex(
  coords: HexCoordinates,
  direction: Direction
): HexCoordinates {
  return hexCoordsAdd(coords, DIRECTION_OFFSETS[direction]);
}

/**
 * Compares two building keys for equality (without alias checking).
 * For alias-aware comparison, use findBuilding instead.
 *
 * @param a - First building key
 * @param b - Second building key
 * @returns True if keys are exactly equal
 */
export function buildingKeysEqual(a: BuildingKey, b: BuildingKey): boolean {
  return (
    a.position === b.position && hexCoordsEqual(a.hexCoordinates, b.hexCoordinates)
  );
}

/**
 * Finds a building by its key, handling alias positions.
 *
 * Buildings can exist at multiple equivalent positions (aliases) due to
 * hex geometry - each vertex is shared by 3 hexes. This function checks
 * both the primary key and all aliases.
 *
 * @param buildings - Array of building models to search
 * @param key - The building key to find
 * @returns The matching BuildingModel, or undefined if not found
 *
 * @example
 * // This will find the building whether it's stored at:
 * // - TopRight of (0,0,0)
 * // - BottomRight of (0,-1,1) (North neighbor)
 * // - Left of (1,-1,0) (NorthEast neighbor)
 * const building = findBuilding(gameModel.buildings, {
 *   hexCoordinates: { q: 0, r: 0, s: 0 },
 *   position: 'TopRight'
 * });
 */
export function findBuilding(
  buildings: BuildingModel[],
  key: BuildingKey
): BuildingModel | undefined {
  if (!buildings || buildings.length === 0 || !key) {
    return undefined;
  }

  // First try direct match
  const direct = buildings.find((b) => buildingKeysEqual(b.buildingKey, key));
  if (direct) {
    return direct;
  }

  // Check alias positions (same vertex, different hex reference)
  const aliases = buildingKeyAliases(key);
  for (const alias of aliases) {
    const aliasCoords = getAdjacentHex(key.hexCoordinates, alias.direction);
    const aliasKey: BuildingKey = {
      hexCoordinates: aliasCoords,
      position: alias.position,
      default: {} as BuildingKey,
    };
    const aliasMatch = buildings.find((b) =>
      buildingKeysEqual(b.buildingKey, aliasKey)
    );
    if (aliasMatch) {
      return aliasMatch;
    }
  }

  return undefined;
}

/**
 * Returns the 3 adjacent building positions for a given building key.
 * Each vertex on a hex grid has exactly 3 adjacent vertices.
 *
 * @param buildings - Array of all buildings to search
 * @param key - The building key to find neighbors for
 * @returns Array of adjacent buildings (up to 3)
 *
 * @example
 * const neighbors = adjacentBuildings(gameModel.buildings, buildingKey);
 * // Used to check settlement spacing rule (no adjacent settlements)
 */
export function adjacentBuildings(
  buildings: BuildingModel[],
  key: BuildingKey
): BuildingModel[] {
  if (!buildings || !key) {
    return [];
  }

  const result: BuildingModel[] = [];
  const adjacentKeys: BuildingKey[] = [];

  // Define adjacent positions based on current position
  switch (key.position) {
    case 'Right':
      adjacentKeys.push(
        { hexCoordinates: key.hexCoordinates, position: 'TopRight', default: {} as BuildingKey },
        { hexCoordinates: key.hexCoordinates, position: 'BottomRight', default: {} as BuildingKey },
        { hexCoordinates: getAdjacentHex(key.hexCoordinates, 'NorthEast'), position: 'BottomRight', default: {} as BuildingKey }
      );
      break;
    case 'BottomRight':
      adjacentKeys.push(
        { hexCoordinates: key.hexCoordinates, position: 'Right', default: {} as BuildingKey },
        { hexCoordinates: key.hexCoordinates, position: 'BottomLeft', default: {} as BuildingKey },
        { hexCoordinates: getAdjacentHex(key.hexCoordinates, 'South'), position: 'Right', default: {} as BuildingKey }
      );
      break;
    case 'BottomLeft':
      adjacentKeys.push(
        { hexCoordinates: key.hexCoordinates, position: 'Left', default: {} as BuildingKey },
        { hexCoordinates: key.hexCoordinates, position: 'BottomRight', default: {} as BuildingKey },
        { hexCoordinates: getAdjacentHex(key.hexCoordinates, 'South'), position: 'Left', default: {} as BuildingKey }
      );
      break;
    case 'Left':
      adjacentKeys.push(
        { hexCoordinates: key.hexCoordinates, position: 'TopLeft', default: {} as BuildingKey },
        { hexCoordinates: key.hexCoordinates, position: 'BottomLeft', default: {} as BuildingKey },
        { hexCoordinates: getAdjacentHex(key.hexCoordinates, 'NorthWest'), position: 'BottomLeft', default: {} as BuildingKey }
      );
      break;
    case 'TopLeft':
      adjacentKeys.push(
        { hexCoordinates: key.hexCoordinates, position: 'TopRight', default: {} as BuildingKey },
        { hexCoordinates: key.hexCoordinates, position: 'Left', default: {} as BuildingKey },
        { hexCoordinates: getAdjacentHex(key.hexCoordinates, 'NorthWest'), position: 'TopRight', default: {} as BuildingKey }
      );
      break;
    case 'TopRight':
      adjacentKeys.push(
        { hexCoordinates: key.hexCoordinates, position: 'TopLeft', default: {} as BuildingKey },
        { hexCoordinates: key.hexCoordinates, position: 'Right', default: {} as BuildingKey },
        { hexCoordinates: getAdjacentHex(key.hexCoordinates, 'North'), position: 'Right', default: {} as BuildingKey }
      );
      break;
  }

  // Find each adjacent building (uses alias-aware lookup)
  for (const adjacentKey of adjacentKeys) {
    const building = findBuilding(buildings, adjacentKey);
    if (building) {
      result.push(building);
    }
  }

  return result;
}

/**
 * Returns all buildings in the tile at the given coordinates.
 * A hex tile has 6 vertices where buildings can be placed.
 *
 * @param buildings - Array of all buildings
 * @param coords - The hex coordinates
 * @returns Array of buildings at this tile's vertices (up to 6)
 */
export function buildingsInTile(
  buildings: BuildingModel[],
  coords: HexCoordinates
): BuildingModel[] {
  if (!buildings || !coords) {
    return [];
  }

  const result: BuildingModel[] = [];
  for (const position of HEX_POSITIONS) {
    const key: BuildingKey = {
      hexCoordinates: coords,
      position,
      default: {} as BuildingKey,
    };
    const building = findBuilding(buildings, key);
    if (building) {
      result.push(building);
    }
  }
  return result;
}

/**
 * Returns all owned buildings in the tile at the given coordinates.
 *
 * @param buildings - Array of all buildings
 * @param coords - The hex coordinates
 * @returns Array of owned buildings at this tile's vertices
 */
export function ownedBuildings(
  buildings: BuildingModel[],
  coords: HexCoordinates
): BuildingModel[] {
  if (!buildings || !coords) {
    return [];
  }

  const result: BuildingModel[] = [];
  for (const position of HEX_POSITIONS) {
    const key: BuildingKey = {
      hexCoordinates: coords,
      position,
      default: {} as BuildingKey,
    };
    const building = findBuilding(buildings, key);
    if (building && building.ownerId) {
      result.push(building);
    }
  }
  return result;
}
