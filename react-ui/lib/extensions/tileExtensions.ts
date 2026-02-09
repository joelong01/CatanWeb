/**
 * Tile extension functions - TypeScript equivalents of C# TileModelExtensions.
 *
 * These functions provide consistent tile lookup and calculation utilities
 * matching the server-side API for use across the React application.
 *
 * @module tileExtensions
 */

import type { TileModel } from '@/types/generated/models/tile-model';
import type { HexCoordinates } from '@/types/generated/models/hex-coordinates';
import type { ResourceType } from '@/types/generated/models/resource-type';

/**
 * Pip count for each dice number (probability indicator).
 * Higher pip count = more likely to be rolled.
 */
export const NUMBER_PIPS: Record<number, number> = {
  2: 1,
  3: 2,
  4: 3,
  5: 4,
  6: 5,
  7: 0, // Desert/robber
  8: 5,
  9: 4,
  10: 3,
  11: 2,
  12: 1,
};

/**
 * Direction offsets for adjacent hex coordinates in axial coordinate system.
 * Each direction maps to a q,r,s offset that when added to a coordinate
 * gives the neighbor in that direction.
 */
export const HEX_DIRECTIONS: HexCoordinates[] = [
  { q: 1, r: 0, s: -1 }, // East
  { q: 1, r: -1, s: 0 }, // NorthEast
  { q: 0, r: -1, s: 1 }, // NorthWest
  { q: -1, r: 0, s: 1 }, // West
  { q: -1, r: 1, s: 0 }, // SouthWest
  { q: 0, r: 1, s: -1 }, // SouthEast
];

/**
 * Compares two hex coordinates for equality.
 *
 * @param a - First coordinate
 * @param b - Second coordinate
 * @returns True if coordinates are equal
 */
export function hexCoordsEqual(a: HexCoordinates, b: HexCoordinates): boolean {
  return a.q === b.q && a.r === b.r && a.s === b.s;
}

/**
 * Adds two hex coordinates together.
 *
 * @param a - First coordinate
 * @param b - Second coordinate (or direction offset)
 * @returns Sum of the two coordinates
 */
export function hexCoordsAdd(a: HexCoordinates, b: HexCoordinates): HexCoordinates {
  return {
    q: a.q + b.q,
    r: a.r + b.r,
    s: a.s + b.s,
  };
}

/**
 * Finds a tile by its coordinates in a collection of tiles.
 *
 * @param tiles - Array of tile models to search
 * @param coords - The hex coordinates to find
 * @returns The matching TileModel, or undefined if not found
 *
 * @example
 * const tile = tileFromCoords(gameModel.tiles, { q: 0, r: 0, s: 0 });
 */
export function tileFromCoords(tiles: TileModel[], coords: HexCoordinates): TileModel | undefined {
  if (!tiles || tiles.length === 0 || !coords) {
    return undefined;
  }
  return tiles.find((t) => hexCoordsEqual(t.tileKey, coords));
}

/**
 * Gets all tiles adjacent to a given tile.
 *
 * @param tiles - Array of all tiles to search
 * @param tile - The tile to find neighbors for
 * @returns Array of adjacent tiles (up to 6)
 *
 * @example
 * const neighbors = adjacentTiles(gameModel.tiles, centerTile);
 */
export function adjacentTiles(tiles: TileModel[], tile: TileModel): TileModel[] {
  if (!tiles || !tile) {
    return [];
  }
  const result: TileModel[] = [];
  for (const direction of HEX_DIRECTIONS) {
    const neighborCoords = hexCoordsAdd(tile.tileKey, direction);
    const neighbor = tileFromCoords(tiles, neighborCoords);
    if (neighbor) {
      result.push(neighbor);
    }
  }
  return result;
}

/**
 * Calculates the pip value (probability indicator) for a tile number.
 *
 * @param number - The tile's dice number (2-12)
 * @returns Pip count (1-5, or 0 for 7/desert)
 */
export function pipsForNumber(number: number): number {
  return NUMBER_PIPS[number] ?? 0;
}

/**
 * Calculates total stars (pip sum) for a collection of tiles.
 * Stars indicate the combined probability of rolling any of the tiles.
 * Computed client-side from tile numbers (stars/pips are not in the wire format).
 *
 * @param tiles - Array of tiles to sum
 * @returns Total star count
 *
 * @example
 * const stars = totalStars(tilesAroundBuilding);
 * // A building touching 6, 8, 9 would have 5 + 5 + 4 = 14 stars
 */
export function totalStars(tiles: TileModel[]): number {
  if (!tiles || tiles.length === 0) {
    return 0;
  }
  return tiles.reduce((sum, tile) => sum + pipsForNumber(tile.number), 0);
}

/**
 * Filters tiles to only those with a specific dice number.
 *
 * @param tiles - Array of tiles to filter
 * @param number - The dice number to match
 * @returns Array of tiles with the specified number
 *
 * @example
 * const sixes = tilesWithNumber(gameModel.tiles, 6);
 */
export function tilesWithNumber(tiles: TileModel[], number: number): TileModel[] {
  if (!tiles) {
    return [];
  }
  return tiles.filter((t) => t.number === number);
}

/**
 * Filters tiles to only those with a specific resource type.
 *
 * @param tiles - Array of tiles to filter
 * @param resource - The resource type to match
 * @returns Array of tiles with the specified resource
 *
 * @example
 * const oreTiles = tilesWithResource(gameModel.tiles, 'Ore');
 */
export function tilesWithResource(tiles: TileModel[], resource: ResourceType): TileModel[] {
  if (!tiles) {
    return [];
  }
  return tiles.filter((t) => t.resourceTileType === resource);
}

/**
 * Filters tiles to only those with high probability numbers (6 or 8).
 * These are the most likely to be rolled (5 pips each).
 *
 * @param tiles - Array of tiles to filter
 * @returns Array of tiles with number 6 or 8
 *
 * @example
 * const highProbTiles = tilesWithSixOrEight(gameModel.tiles);
 */
export function tilesWithSixOrEight(tiles: TileModel[]): TileModel[] {
  if (!tiles) {
    return [];
  }
  return tiles.filter((t) => t.number === 6 || t.number === 8);
}
