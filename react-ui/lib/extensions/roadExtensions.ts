/**
 * Road-related extension functions
 *
 * CRITICAL: Roads (edges) are shared by 2 hexes, so each road has 1 alias.
 * The findRoad function must check both the direct key and the alias.
 *
 * Ported from: Catan3.Shared/Extensions/RoadModelExtensions.cs
 */

import type { RoadModel } from '@/types/generated/models/road-model';
import type { RoadKey } from '@/types/generated/models/road-key';
import type { HexCoordinates } from '@/types/generated/models/hex-coordinates';
import type { HexSide } from '@/types/generated/models/hex-side';
import { getAdjacentHex, type Direction } from './buildingExtensions';

/**
 * Valid hex sides (excluding None)
 */
export const HEX_SIDES: HexSide[] = [
  'Top',
  'TopRight',
  'BottomRight',
  'Bottom',
  'BottomLeft',
  'TopLeft',
];

/**
 * Road alias definition - the other hex/side that refers to the same edge
 */
export interface RoadAlias {
  hexSide: HexSide;
  direction: Direction;
}

/**
 * Returns the alias for a road edge.
 * Each edge is shared by exactly 2 hexes, so there's 1 alias per road.
 *
 * Road aliases:
 * - Top → Bottom of North neighbor
 * - TopRight → BottomLeft of NorthEast neighbor
 * - BottomRight → TopLeft of SouthEast neighbor
 * - Bottom → Top of South neighbor
 * - BottomLeft → TopRight of SouthWest neighbor
 * - TopLeft → BottomRight of NorthWest neighbor
 */
export function roadKeyAlias(key: RoadKey): RoadAlias | undefined {
  switch (key.hexSide) {
    case 'Top':
      return { hexSide: 'Bottom', direction: 'North' };
    case 'TopRight':
      return { hexSide: 'BottomLeft', direction: 'NorthEast' };
    case 'BottomRight':
      return { hexSide: 'TopLeft', direction: 'SouthEast' };
    case 'Bottom':
      return { hexSide: 'Top', direction: 'South' };
    case 'BottomLeft':
      return { hexSide: 'TopRight', direction: 'SouthWest' };
    case 'TopLeft':
      return { hexSide: 'BottomRight', direction: 'NorthWest' };
    case 'None':
    default:
      return undefined;
  }
}

/**
 * Compares two RoadKey objects for equality
 */
export function roadKeysEqual(a: RoadKey, b: RoadKey): boolean {
  return a.tileKey.q === b.tileKey.q && a.tileKey.r === b.tileKey.r && a.hexSide === b.hexSide;
}

/**
 * Finds a road by its key, checking both direct match and alias.
 * Returns undefined if not found.
 */
export function findRoad(roads: RoadModel[], key: RoadKey): RoadModel | undefined {
  if (!roads || roads.length === 0) {
    return undefined;
  }

  // Try direct match first
  const direct = roads.find((r) => roadKeysEqual(r.roadKey, key));
  if (direct) {
    return direct;
  }

  // Check alias (same edge, different hex reference)
  const alias = roadKeyAlias(key);
  if (alias) {
    const aliasCoords = getAdjacentHex(key.tileKey, alias.direction);
    const aliasKey: RoadKey = {
      tileKey: aliasCoords,
      hexSide: alias.hexSide,
    };
    const aliasMatch = roads.find((r) => roadKeysEqual(r.roadKey, aliasKey));
    if (aliasMatch) {
      return aliasMatch;
    }
  }

  return undefined;
}

/**
 * Returns the keys of roads adjacent to a given road.
 * Each road edge connects 2 vertices, each vertex has 3 edges, minus the current edge = 4 adjacent roads.
 */
export function adjacentRoadKeys(key: RoadKey): RoadKey[] {
  const keys: RoadKey[] = [];

  switch (key.hexSide) {
    case 'Top':
      // Top edge connects TopLeft and TopRight vertices
      // At TopRight vertex: TopRight (same hex), North.BottomRight (shares vertex)
      // At TopLeft vertex: TopLeft (same hex), NorthWest.Bottom (shares vertex)
      // NOTE: C# reference (RoadModelExtensions.cs L52) has NorthWest.BottomLeft which is wrong:
      // - BottomLeft edge doesn't touch TopLeft vertex at all
      // - BottomRight edge is an alias of TopLeft (same edge, not adjacent)
      // - Bottom edge correctly shares TopLeft vertex (= NorthWest's BottomRight vertex)
      // This bug may cause edge cases in longest road calculations. See: CATAN-BUG-001
      keys.push({ tileKey: key.tileKey, hexSide: 'TopRight' });
      keys.push({ tileKey: key.tileKey, hexSide: 'TopLeft' });
      keys.push({ tileKey: getAdjacentHex(key.tileKey, 'North'), hexSide: 'BottomRight' });
      keys.push({ tileKey: getAdjacentHex(key.tileKey, 'NorthWest'), hexSide: 'Bottom' });
      break;

    case 'TopRight':
      // TopRight edge connects TopRight and Right vertices
      keys.push({ tileKey: key.tileKey, hexSide: 'BottomRight' });
      keys.push({ tileKey: key.tileKey, hexSide: 'Top' });
      keys.push({ tileKey: getAdjacentHex(key.tileKey, 'NorthEast'), hexSide: 'TopLeft' });
      keys.push({ tileKey: getAdjacentHex(key.tileKey, 'NorthEast'), hexSide: 'Bottom' });
      break;

    case 'BottomRight':
      // BottomRight edge connects Right and BottomRight vertices
      keys.push({ tileKey: key.tileKey, hexSide: 'TopRight' });
      keys.push({ tileKey: key.tileKey, hexSide: 'Bottom' });
      keys.push({ tileKey: getAdjacentHex(key.tileKey, 'SouthEast'), hexSide: 'Top' });
      keys.push({ tileKey: getAdjacentHex(key.tileKey, 'SouthEast'), hexSide: 'BottomLeft' });
      break;

    case 'Bottom':
      // Bottom edge connects BottomRight and BottomLeft vertices
      keys.push({ tileKey: key.tileKey, hexSide: 'BottomLeft' });
      keys.push({ tileKey: key.tileKey, hexSide: 'BottomRight' });
      keys.push({ tileKey: getAdjacentHex(key.tileKey, 'South'), hexSide: 'TopLeft' });
      keys.push({ tileKey: getAdjacentHex(key.tileKey, 'South'), hexSide: 'TopRight' });
      break;

    case 'BottomLeft':
      // BottomLeft edge connects BottomLeft and Left vertices
      keys.push({ tileKey: key.tileKey, hexSide: 'Bottom' });
      keys.push({ tileKey: key.tileKey, hexSide: 'TopLeft' });
      keys.push({ tileKey: getAdjacentHex(key.tileKey, 'SouthWest'), hexSide: 'Top' });
      keys.push({ tileKey: getAdjacentHex(key.tileKey, 'SouthWest'), hexSide: 'BottomRight' });
      break;

    case 'TopLeft':
      // TopLeft edge connects Left and TopLeft vertices
      keys.push({ tileKey: key.tileKey, hexSide: 'Top' });
      keys.push({ tileKey: key.tileKey, hexSide: 'BottomLeft' });
      keys.push({ tileKey: getAdjacentHex(key.tileKey, 'NorthWest'), hexSide: 'TopRight' });
      keys.push({ tileKey: getAdjacentHex(key.tileKey, 'NorthWest'), hexSide: 'Bottom' });
      break;

    case 'None':
    default:
      // No adjacent roads for invalid side
      break;
  }

  return keys;
}

/**
 * Finds all roads adjacent to a given road key.
 * Returns only roads that exist in the provided array.
 */
export function adjacentRoads(roads: RoadModel[], key: RoadKey): RoadModel[] {
  if (!roads || roads.length === 0) {
    return [];
  }

  const adjKeys = adjacentRoadKeys(key);
  const result: RoadModel[] = [];

  for (const adjKey of adjKeys) {
    const road = findRoad(roads, adjKey);
    if (road) {
      result.push(road);
    }
  }

  return result;
}

/**
 * Finds all roads at a tile's edges (up to 6 roads).
 * Uses alias handling to find roads stored at neighbor coordinates.
 */
export function roadsInTile(roads: RoadModel[], coords: HexCoordinates): RoadModel[] {
  if (!roads || roads.length === 0) {
    return [];
  }

  const result: RoadModel[] = [];

  for (const side of HEX_SIDES) {
    const key: RoadKey = { tileKey: coords, hexSide: side };
    const road = findRoad(roads, key);
    if (road && !result.some((r) => roadKeysEqual(r.roadKey, road.roadKey))) {
      result.push(road);
    }
  }

  return result;
}

/**
 * Filters roads to only those that are owned (have an ownerId and state is Road or Ship)
 */
export function ownedRoads(roads: RoadModel[], coords: HexCoordinates): RoadModel[] {
  return roadsInTile(roads, coords).filter(
    (r) => r.ownerId && r.ownerId !== '' && (r.roadState === 'Road' || r.roadState === 'Ship')
  );
}

/**
 * Finds all roads owned by a specific player
 */
export function roadsOwnedByPlayer(roads: RoadModel[], playerId: string): RoadModel[] {
  if (!roads || roads.length === 0 || !playerId) {
    return [];
  }

  return roads.filter((r) => r.ownerId === playerId);
}

/**
 * Finds all buildable roads (roadState === 'Buildable')
 */
export function buildableRoads(roads: RoadModel[]): RoadModel[] {
  if (!roads || roads.length === 0) {
    return [];
  }

  return roads.filter((r) => r.roadState === 'Buildable');
}
