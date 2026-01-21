/**
 * Tests for model utility functions.
 * Validates TypeScript implementations match C# behavior.
 */

import { describe, it, expect } from 'vitest';
import type {
  BuildingKey,
  BuildingModel,
  RoadKey,
  RoadModel,
  TileModel,
} from '@/types/generated/models';
import {
  getBuildingAliases,
  getRoadAliases,
  findBuilding,
  findRoad,
  findTile,
  getAdjacentBuildings,
  getAdjacentRoads,
  getAdjacentTiles,
  getAdjacentTile,
  getBuildingsInTile,
  getNumberStars,
  getTotalStars,
  getTilesWithNumber,
  getHighProbabilityTiles,
  buildingKeyEquals,
  roadKeyEquals,
  getBuildingAutomationId,
  getRoadAutomationId,
  getTileAutomationId,
} from './modelUtils';

// =============================================================================
// Test Fixtures
// =============================================================================

const CENTER_HEX = { q: 0, r: 0, s: 0 };

function makeBuildingKey(
  q: number,
  r: number,
  s: number,
  position: string
): BuildingKey {
  return {
    hexCoordinates: { q, r, s },
    position,
  } as BuildingKey;
}

function makeBuildingModel(
  q: number,
  r: number,
  s: number,
  position: string,
  ownerId: string | null = null
): BuildingModel {
  return {
    buildingKey: makeBuildingKey(q, r, s, position),
    buildingState: 'Settlement',
    wall: false,
    metropolis: false,
    ownerId: ownerId ?? '',
    hasRobber: false,
  } as BuildingModel;
}

function makeRoadKey(q: number, r: number, s: number, hexSide: string): RoadKey {
  return {
    tileKey: { q, r, s },
    hexSide,
  } as RoadKey;
}

function makeRoadModel(
  q: number,
  r: number,
  s: number,
  hexSide: string,
  ownerId: string | null = null
): RoadModel {
  return {
    roadKey: makeRoadKey(q, r, s, hexSide),
    roadState: 'Road',
    ownerId: ownerId ?? '',
    buildIndex: 0,
  } as RoadModel;
}

function makeTileModel(q: number, r: number, s: number, number: number): TileModel {
  return {
    tileKey: { q, r, s },
    number,
    resourceTileType: 'Wheat',
    highlighted: false,
    temporarilyGold: false,
    stars: getNumberStars(number),
  } as TileModel;
}

// =============================================================================
// Building Alias Tests
// =============================================================================

describe('getBuildingAliases', () => {
  it('returns 2 aliases for TopRight position', () => {
    const key = makeBuildingKey(0, 0, 0, 'TopRight');
    const aliases = getBuildingAliases(key);

    expect(aliases).toHaveLength(2);
    expect(aliases).toContainEqual({ position: 'BottomRight', direction: 'North' });
    expect(aliases).toContainEqual({ position: 'Left', direction: 'NorthEast' });
  });

  it('returns 2 aliases for Right position', () => {
    const key = makeBuildingKey(0, 0, 0, 'Right');
    const aliases = getBuildingAliases(key);

    expect(aliases).toHaveLength(2);
    expect(aliases).toContainEqual({ position: 'BottomLeft', direction: 'NorthEast' });
    expect(aliases).toContainEqual({ position: 'TopLeft', direction: 'SouthEast' });
  });

  it('returns 2 aliases for BottomRight position', () => {
    const key = makeBuildingKey(0, 0, 0, 'BottomRight');
    const aliases = getBuildingAliases(key);

    expect(aliases).toHaveLength(2);
    expect(aliases).toContainEqual({ position: 'TopRight', direction: 'South' });
    expect(aliases).toContainEqual({ position: 'Left', direction: 'SouthEast' });
  });

  it('returns empty array for None position', () => {
    const key = makeBuildingKey(0, 0, 0, 'None');
    const aliases = getBuildingAliases(key);
    expect(aliases).toHaveLength(0);
  });
});

// =============================================================================
// Road Alias Tests
// =============================================================================

describe('getRoadAliases', () => {
  it('returns 1 alias for TopRight side', () => {
    const key = makeRoadKey(0, 0, 0, 'TopRight');
    const aliases = getRoadAliases(key);

    expect(aliases).toHaveLength(1);
    expect(aliases[0]).toEqual({ hexSide: 'BottomLeft', direction: 'NorthEast' });
  });

  it('returns 1 alias for Top side', () => {
    const key = makeRoadKey(0, 0, 0, 'Top');
    const aliases = getRoadAliases(key);

    expect(aliases).toHaveLength(1);
    expect(aliases[0]).toEqual({ hexSide: 'Bottom', direction: 'North' });
  });

  it('returns empty array for None side', () => {
    const key = makeRoadKey(0, 0, 0, 'None');
    const aliases = getRoadAliases(key);
    expect(aliases).toHaveLength(0);
  });
});

// =============================================================================
// Find Building Tests
// =============================================================================

describe('findBuilding', () => {
  it('finds building by primary key', () => {
    const buildings = [
      makeBuildingModel(0, 0, 0, 'TopRight', 'player1'),
      makeBuildingModel(0, 0, 0, 'Right', 'player2'),
    ];

    const found = findBuilding(buildings, makeBuildingKey(0, 0, 0, 'TopRight'));
    expect(found).toBeDefined();
    expect(found?.ownerId).toBe('player1');
  });

  it('finds building by alias key', () => {
    // Building at (0,0,0) TopRight is the same as (-1,1,0) Left
    const buildings = [makeBuildingModel(0, 0, 0, 'TopRight', 'player1')];

    // Search using alias coordinates (NorthEast neighbor's Left position)
    const found = findBuilding(buildings, makeBuildingKey(1, -1, 0, 'Left'));
    expect(found).toBeDefined();
    expect(found?.ownerId).toBe('player1');
  });

  it('returns undefined when not found', () => {
    const buildings = [makeBuildingModel(0, 0, 0, 'TopRight', 'player1')];

    const found = findBuilding(buildings, makeBuildingKey(5, 5, -10, 'TopRight'));
    expect(found).toBeUndefined();
  });

  it('returns undefined for empty array', () => {
    const found = findBuilding([], makeBuildingKey(0, 0, 0, 'TopRight'));
    expect(found).toBeUndefined();
  });
});

// =============================================================================
// Find Road Tests
// =============================================================================

describe('findRoad', () => {
  it('finds road by primary key', () => {
    const roads = [
      makeRoadModel(0, 0, 0, 'TopRight', 'player1'),
      makeRoadModel(0, 0, 0, 'Top', 'player2'),
    ];

    const found = findRoad(roads, makeRoadKey(0, 0, 0, 'TopRight'));
    expect(found).toBeDefined();
    expect(found?.ownerId).toBe('player1');
  });

  it('finds road by alias key', () => {
    // Road at (0,0,0) TopRight is the same as (1,-1,0) BottomLeft
    const roads = [makeRoadModel(0, 0, 0, 'TopRight', 'player1')];

    // Search using alias coordinates
    const found = findRoad(roads, makeRoadKey(1, -1, 0, 'BottomLeft'));
    expect(found).toBeDefined();
    expect(found?.ownerId).toBe('player1');
  });

  it('returns undefined when not found', () => {
    const roads = [makeRoadModel(0, 0, 0, 'TopRight', 'player1')];

    const found = findRoad(roads, makeRoadKey(5, 5, -10, 'TopRight'));
    expect(found).toBeUndefined();
  });
});

// =============================================================================
// Adjacent Building Tests
// =============================================================================

describe('getAdjacentBuildings', () => {
  it('finds adjacent buildings for Right position', () => {
    const buildings = [
      makeBuildingModel(0, 0, 0, 'Right', 'center'),
      makeBuildingModel(0, 0, 0, 'TopRight', 'adjacent1'),
      makeBuildingModel(0, 0, 0, 'BottomRight', 'adjacent2'),
      makeBuildingModel(1, -1, 0, 'BottomRight', 'adjacent3'), // NorthEast neighbor
      makeBuildingModel(0, 0, 0, 'Left', 'not-adjacent'),
    ];

    const adjacent = getAdjacentBuildings(
      buildings,
      makeBuildingKey(0, 0, 0, 'Right')
    );

    expect(adjacent).toHaveLength(3);
    expect(adjacent.map((b) => b.ownerId).sort()).toEqual([
      'adjacent1',
      'adjacent2',
      'adjacent3',
    ]);
  });
});

// =============================================================================
// Adjacent Road Tests
// =============================================================================

describe('getAdjacentRoads', () => {
  it('finds adjacent roads for Top side', () => {
    const roads = [
      makeRoadModel(0, 0, 0, 'Top', 'center'),
      makeRoadModel(0, 0, 0, 'TopRight', 'adjacent1'),
      makeRoadModel(0, 0, 0, 'TopLeft', 'adjacent2'),
      makeRoadModel(0, -1, 1, 'BottomRight', 'adjacent3'), // North neighbor
      makeRoadModel(-1, 0, 1, 'BottomLeft', 'adjacent4'), // NorthWest neighbor
      makeRoadModel(0, 0, 0, 'Bottom', 'not-adjacent'),
    ];

    const adjacent = getAdjacentRoads(roads, makeRoadKey(0, 0, 0, 'Top'));

    expect(adjacent).toHaveLength(4);
    expect(adjacent.map((r) => r.ownerId).sort()).toEqual([
      'adjacent1',
      'adjacent2',
      'adjacent3',
      'adjacent4',
    ]);
  });
});

// =============================================================================
// Tile Utilities Tests
// =============================================================================

describe('findTile', () => {
  it('finds tile by coordinates', () => {
    const tiles = [
      makeTileModel(0, 0, 0, 6),
      makeTileModel(1, -1, 0, 8),
      makeTileModel(0, 1, -1, 5),
    ];

    const found = findTile(tiles, { q: 1, r: -1, s: 0 });
    expect(found).toBeDefined();
    expect(found?.number).toBe(8);
  });

  it('returns undefined when not found', () => {
    const tiles = [makeTileModel(0, 0, 0, 6)];
    const found = findTile(tiles, { q: 5, r: 5, s: -10 });
    expect(found).toBeUndefined();
  });
});

describe('getAdjacentTiles', () => {
  it('finds all 6 neighbors when present', () => {
    // Create center tile and all 6 neighbors
    const tiles = [
      makeTileModel(0, 0, 0, 7), // Center
      makeTileModel(0, -1, 1, 6), // North
      makeTileModel(1, -1, 0, 8), // NorthEast
      makeTileModel(1, 0, -1, 5), // SouthEast
      makeTileModel(0, 1, -1, 9), // South
      makeTileModel(-1, 1, 0, 4), // SouthWest
      makeTileModel(-1, 0, 1, 10), // NorthWest
    ];

    const center = tiles[0];
    const adjacent = getAdjacentTiles(tiles, center);

    expect(adjacent).toHaveLength(6);
  });

  it('returns only existing neighbors', () => {
    const tiles = [
      makeTileModel(0, 0, 0, 7), // Center
      makeTileModel(0, -1, 1, 6), // North only
    ];

    const center = tiles[0];
    const adjacent = getAdjacentTiles(tiles, center);

    expect(adjacent).toHaveLength(1);
    expect(adjacent[0].number).toBe(6);
  });
});

// =============================================================================
// Star Calculation Tests
// =============================================================================

describe('getNumberStars', () => {
  it('returns correct stars for each dice number', () => {
    expect(getNumberStars(2)).toBe(1);
    expect(getNumberStars(3)).toBe(2);
    expect(getNumberStars(4)).toBe(3);
    expect(getNumberStars(5)).toBe(4);
    expect(getNumberStars(6)).toBe(5);
    expect(getNumberStars(7)).toBe(0); // Robber
    expect(getNumberStars(8)).toBe(5);
    expect(getNumberStars(9)).toBe(4);
    expect(getNumberStars(10)).toBe(3);
    expect(getNumberStars(11)).toBe(2);
    expect(getNumberStars(12)).toBe(1);
  });
});

describe('getTotalStars', () => {
  it('sums stars correctly', () => {
    const tiles = [
      makeTileModel(0, 0, 0, 6), // 5 stars
      makeTileModel(1, -1, 0, 8), // 5 stars
      makeTileModel(0, 1, -1, 2), // 1 star
    ];

    expect(getTotalStars(tiles)).toBe(11);
  });
});

describe('getTilesWithNumber', () => {
  it('filters tiles by number', () => {
    const tiles = [
      makeTileModel(0, 0, 0, 6),
      makeTileModel(1, -1, 0, 6),
      makeTileModel(0, 1, -1, 8),
    ];

    const sixes = getTilesWithNumber(tiles, 6);
    expect(sixes).toHaveLength(2);
  });
});

describe('getHighProbabilityTiles', () => {
  it('returns only 6 and 8 tiles', () => {
    const tiles = [
      makeTileModel(0, 0, 0, 6),
      makeTileModel(1, -1, 0, 8),
      makeTileModel(0, 1, -1, 5),
      makeTileModel(-1, 1, 0, 9),
    ];

    const highProb = getHighProbabilityTiles(tiles);
    expect(highProb).toHaveLength(2);
    expect(highProb.every((t) => t.number === 6 || t.number === 8)).toBe(true);
  });
});

// =============================================================================
// Key Equality Tests
// =============================================================================

describe('buildingKeyEquals', () => {
  it('returns true for equal keys', () => {
    const a = makeBuildingKey(1, -1, 0, 'TopRight');
    const b = makeBuildingKey(1, -1, 0, 'TopRight');
    expect(buildingKeyEquals(a, b)).toBe(true);
  });

  it('returns false for different coordinates', () => {
    const a = makeBuildingKey(1, -1, 0, 'TopRight');
    const b = makeBuildingKey(0, 0, 0, 'TopRight');
    expect(buildingKeyEquals(a, b)).toBe(false);
  });

  it('returns false for different positions', () => {
    const a = makeBuildingKey(1, -1, 0, 'TopRight');
    const b = makeBuildingKey(1, -1, 0, 'Left');
    expect(buildingKeyEquals(a, b)).toBe(false);
  });
});

describe('roadKeyEquals', () => {
  it('returns true for equal keys', () => {
    const a = makeRoadKey(1, -1, 0, 'Top');
    const b = makeRoadKey(1, -1, 0, 'Top');
    expect(roadKeyEquals(a, b)).toBe(true);
  });

  it('returns false for different coordinates', () => {
    const a = makeRoadKey(1, -1, 0, 'Top');
    const b = makeRoadKey(0, 0, 0, 'Top');
    expect(roadKeyEquals(a, b)).toBe(false);
  });
});

// =============================================================================
// Automation ID Tests
// =============================================================================

describe('Automation IDs', () => {
  it('generates building automation ID', () => {
    const key = makeBuildingKey(1, -2, 1, 'TopRight');
    expect(getBuildingAutomationId(key)).toBe('Building-(1,-2,1)-TopRight');
  });

  it('generates road automation ID', () => {
    const key = makeRoadKey(0, 1, -1, 'BottomLeft');
    expect(getRoadAutomationId(key)).toBe('Road-(0,1,-1)-BottomLeft');
  });

  it('generates tile automation ID', () => {
    const tile = makeTileModel(-1, 0, 1, 6);
    expect(getTileAutomationId(tile)).toBe('Tile-(-1,0,1)');
  });
});

// =============================================================================
// Direction Navigation Tests
// =============================================================================

describe('getAdjacentTile', () => {
  it('calculates North correctly', () => {
    const result = getAdjacentTile(CENTER_HEX, 'North');
    expect(result).toEqual({ q: 0, r: -1, s: 1 });
  });

  it('calculates NorthEast correctly', () => {
    const result = getAdjacentTile(CENTER_HEX, 'NorthEast');
    expect(result).toEqual({ q: 1, r: -1, s: 0 });
  });

  it('calculates SouthEast correctly', () => {
    const result = getAdjacentTile(CENTER_HEX, 'SouthEast');
    expect(result).toEqual({ q: 1, r: 0, s: -1 });
  });

  it('calculates South correctly', () => {
    const result = getAdjacentTile(CENTER_HEX, 'South');
    expect(result).toEqual({ q: 0, r: 1, s: -1 });
  });

  it('calculates SouthWest correctly', () => {
    const result = getAdjacentTile(CENTER_HEX, 'SouthWest');
    expect(result).toEqual({ q: -1, r: 1, s: 0 });
  });

  it('calculates NorthWest correctly', () => {
    const result = getAdjacentTile(CENTER_HEX, 'NorthWest');
    expect(result).toEqual({ q: -1, r: 0, s: 1 });
  });

  it('maintains cube coordinate constraint', () => {
    const directions = [
      'North',
      'NorthEast',
      'SouthEast',
      'South',
      'SouthWest',
      'NorthWest',
    ] as const;

    for (const dir of directions) {
      const result = getAdjacentTile(CENTER_HEX, dir);
      expect(result.q + result.r + result.s).toBe(0);
    }
  });
});

// =============================================================================
// Buildings In Tile Tests
// =============================================================================

describe('getBuildingsInTile', () => {
  it('finds all 6 buildings in a tile', () => {
    const buildings = [
      makeBuildingModel(0, 0, 0, 'Right', 'p1'),
      makeBuildingModel(0, 0, 0, 'BottomRight', 'p2'),
      makeBuildingModel(0, 0, 0, 'BottomLeft', 'p3'),
      makeBuildingModel(0, 0, 0, 'Left', 'p4'),
      makeBuildingModel(0, 0, 0, 'TopLeft', 'p5'),
      makeBuildingModel(0, 0, 0, 'TopRight', 'p6'),
    ];

    const found = getBuildingsInTile(buildings, CENTER_HEX);
    expect(found).toHaveLength(6);
  });

  it('returns empty array when no buildings exist', () => {
    const found = getBuildingsInTile([], CENTER_HEX);
    expect(found).toHaveLength(0);
  });

  it('includes buildings found via aliases', () => {
    // Building stored at neighbor's position but should be found via alias
    const buildings = [
      makeBuildingModel(0, -1, 1, 'BottomRight', 'alias-building'), // North neighbor's BottomRight = center's TopRight
    ];

    const found = getBuildingsInTile(buildings, CENTER_HEX);
    expect(found).toHaveLength(1);
    expect(found[0].ownerId).toBe('alias-building');
  });
});
