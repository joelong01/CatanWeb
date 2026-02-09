/**
 * Tests for tileExtensions
 */

import { describe, it, expect } from 'vitest';
import {
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
} from '../tileExtensions';
import { EXPANSION_GAME_DATA } from '@/lib/test-data/expansion-game';
import type { TileModel } from '@/types/generated/models/tile-model';
import type { HexCoordinates } from '@/types/generated/models/hex-coordinates';

// Helper to create a minimal tile for testing (matches wire format — no phantom fields)
function createMockTile(
  q: number,
  r: number,
  number: number,
  resourceTileType: string = 'Ore'
): TileModel {
  return {
    tileKey: { q, r, s: -q - r },
    number,
    resourceTileType: resourceTileType as TileModel['resourceTileType'],
    highlighted: false,
    temporarilyGold: false,
  };
}

describe('tileExtensions', () => {
  describe('NUMBER_PIPS', () => {
    it('returns correct pips for all dice numbers', () => {
      expect(NUMBER_PIPS[2]).toBe(1);
      expect(NUMBER_PIPS[3]).toBe(2);
      expect(NUMBER_PIPS[4]).toBe(3);
      expect(NUMBER_PIPS[5]).toBe(4);
      expect(NUMBER_PIPS[6]).toBe(5);
      expect(NUMBER_PIPS[7]).toBe(0);
      expect(NUMBER_PIPS[8]).toBe(5);
      expect(NUMBER_PIPS[9]).toBe(4);
      expect(NUMBER_PIPS[10]).toBe(3);
      expect(NUMBER_PIPS[11]).toBe(2);
      expect(NUMBER_PIPS[12]).toBe(1);
    });
  });

  describe('HEX_DIRECTIONS', () => {
    it('has 6 directions', () => {
      expect(HEX_DIRECTIONS).toHaveLength(6);
    });

    it('all directions sum to zero (valid hex offsets)', () => {
      for (const dir of HEX_DIRECTIONS) {
        expect(dir.q + dir.r + dir.s).toBe(0);
      }
    });
  });

  describe('hexCoordsEqual', () => {
    it('returns true for equal coordinates', () => {
      const a: HexCoordinates = { q: 1, r: 2, s: -3 };
      const b: HexCoordinates = { q: 1, r: 2, s: -3 };
      expect(hexCoordsEqual(a, b)).toBe(true);
    });

    it('returns false for different coordinates', () => {
      const a: HexCoordinates = { q: 1, r: 2, s: -3 };
      const b: HexCoordinates = { q: 1, r: 3, s: -4 };
      expect(hexCoordsEqual(a, b)).toBe(false);
    });
  });

  describe('hexCoordsAdd', () => {
    it('adds coordinates correctly', () => {
      const a: HexCoordinates = { q: 1, r: 2, s: -3 };
      const b: HexCoordinates = { q: 2, r: -1, s: -1 };
      const result = hexCoordsAdd(a, b);
      expect(result).toEqual({ q: 3, r: 1, s: -4 });
    });

    it('adding zero offset returns same coordinates', () => {
      const a: HexCoordinates = { q: 1, r: 2, s: -3 };
      const zero: HexCoordinates = { q: 0, r: 0, s: 0 };
      const result = hexCoordsAdd(a, zero);
      expect(result).toEqual(a);
    });
  });

  describe('tileFromCoords', () => {
    const tiles = EXPANSION_GAME_DATA.tiles;

    it('returns undefined for empty tiles array', () => {
      const result = tileFromCoords([], { q: 0, r: 0, s: 0 });
      expect(result).toBeUndefined();
    });

    it('returns undefined when tile not found', () => {
      const result = tileFromCoords(tiles, { q: 100, r: 100, s: -200 });
      expect(result).toBeUndefined();
    });

    it('finds tile at origin', () => {
      const result = tileFromCoords(tiles, { q: 0, r: 0, s: 0 });
      expect(result).toBeDefined();
      expect(result?.tileKey).toEqual({ q: 0, r: 0, s: 0 });
    });

    it('finds tile at non-origin coordinates', () => {
      const result = tileFromCoords(tiles, { q: -3, r: 1, s: 2 });
      expect(result).toBeDefined();
      expect(result?.number).toBe(4);
      expect(result?.resourceTileType).toBe('Ore');
    });
  });

  describe('adjacentTiles', () => {
    const tiles = EXPANSION_GAME_DATA.tiles;

    it('returns empty array for empty tiles', () => {
      const tile = createMockTile(0, 0, 9);
      const result = adjacentTiles([], tile);
      expect(result).toEqual([]);
    });

    it('returns neighbors for center tile', () => {
      const centerTile = tileFromCoords(tiles, { q: 0, r: 0, s: 0 });
      expect(centerTile).toBeDefined();
      const neighbors = adjacentTiles(tiles, centerTile!);
      // Center tile should have multiple neighbors
      expect(neighbors.length).toBeGreaterThan(0);
      expect(neighbors.length).toBeLessThanOrEqual(6);
    });

    it('each neighbor is at distance 1 from original tile', () => {
      const centerTile = tileFromCoords(tiles, { q: 0, r: 0, s: 0 });
      expect(centerTile).toBeDefined();
      const neighbors = adjacentTiles(tiles, centerTile!);
      for (const neighbor of neighbors) {
        const dq = Math.abs(neighbor.tileKey.q - centerTile!.tileKey.q);
        const dr = Math.abs(neighbor.tileKey.r - centerTile!.tileKey.r);
        const ds = Math.abs(neighbor.tileKey.s - centerTile!.tileKey.s);
        // Adjacent hex has exactly one coordinate differ by 1
        expect(dq + dr + ds).toBe(2); // Each move changes 2 coordinates by 1
      }
    });
  });

  describe('pipsForNumber', () => {
    it('returns correct pips for valid numbers', () => {
      expect(pipsForNumber(2)).toBe(1);
      expect(pipsForNumber(6)).toBe(5);
      expect(pipsForNumber(8)).toBe(5);
      expect(pipsForNumber(12)).toBe(1);
    });

    it('returns 0 for 7 (desert)', () => {
      expect(pipsForNumber(7)).toBe(0);
    });

    it('returns 0 for invalid numbers', () => {
      expect(pipsForNumber(0)).toBe(0);
      expect(pipsForNumber(13)).toBe(0);
      expect(pipsForNumber(-1)).toBe(0);
    });
  });

  describe('totalStars', () => {
    it('returns 0 for empty array', () => {
      expect(totalStars([])).toBe(0);
    });

    it('calculates correct stars for single tile', () => {
      const tiles = [createMockTile(0, 0, 6)]; // 6 has 5 pips
      expect(totalStars(tiles)).toBe(5);
    });

    it('calculates correct stars for multiple tiles', () => {
      const tiles = [
        createMockTile(0, 0, 6), // 5 pips
        createMockTile(1, 0, 8), // 5 pips
        createMockTile(0, 1, 9), // 4 pips
      ];
      expect(totalStars(tiles)).toBe(14);
    });

    it('includes 0 pips for desert (7)', () => {
      const tiles = [
        createMockTile(0, 0, 7), // 0 pips
        createMockTile(1, 0, 6), // 5 pips
      ];
      expect(totalStars(tiles)).toBe(5);
    });
  });

  describe('tilesWithNumber', () => {
    const tiles = EXPANSION_GAME_DATA.tiles;

    it('returns empty array for empty input', () => {
      expect(tilesWithNumber([], 6)).toEqual([]);
    });

    it('returns tiles matching the number', () => {
      const sixes = tilesWithNumber(tiles, 6);
      expect(sixes.length).toBeGreaterThan(0);
      for (const tile of sixes) {
        expect(tile.number).toBe(6);
      }
    });

    it('returns empty array when no tiles match', () => {
      const ones = tilesWithNumber(tiles, 1);
      expect(ones).toEqual([]);
    });
  });

  describe('tilesWithResource', () => {
    const tiles = EXPANSION_GAME_DATA.tiles;

    it('returns empty array for empty input', () => {
      expect(tilesWithResource([], 'Ore')).toEqual([]);
    });

    it('returns tiles matching the resource', () => {
      const oreTiles = tilesWithResource(tiles, 'Ore');
      expect(oreTiles.length).toBeGreaterThan(0);
      for (const tile of oreTiles) {
        expect(tile.resourceTileType).toBe('Ore');
      }
    });

    it('returns wheat tiles', () => {
      const wheatTiles = tilesWithResource(tiles, 'Wheat');
      expect(wheatTiles.length).toBeGreaterThan(0);
      for (const tile of wheatTiles) {
        expect(tile.resourceTileType).toBe('Wheat');
      }
    });
  });

  describe('tilesWithSixOrEight', () => {
    const tiles = EXPANSION_GAME_DATA.tiles;

    it('returns empty array for empty input', () => {
      expect(tilesWithSixOrEight([])).toEqual([]);
    });

    it('returns only tiles with number 6 or 8', () => {
      const highProb = tilesWithSixOrEight(tiles);
      expect(highProb.length).toBeGreaterThan(0);
      for (const tile of highProb) {
        expect([6, 8]).toContain(tile.number);
      }
    });

    it('finds both 6s and 8s', () => {
      const highProb = tilesWithSixOrEight(tiles);
      const hasSix = highProb.some((t) => t.number === 6);
      const hasEight = highProb.some((t) => t.number === 8);
      expect(hasSix || hasEight).toBe(true);
    });
  });
});
