/**
 * Expansion game test data extracted from Catan3.Shared/TestData/Expansion.catan_test
 * Used for testing board rendering without a live SignalR connection.
 *
 * ALL types are imported from generated models — no hand-written type definitions.
 * See .design/ts-test-strategy.md for the rationale.
 */

import type { ResourceType } from '@/types/generated/models/resource-type';
import type { HarborType } from '@/types/generated/models/harbor-type';
import type { HexSide } from '@/types/generated/models/hex-side';
import type { HexPosition } from '@/types/generated/models/hex-position';
import type { BuildingState } from '@/types/generated/models/building-state';
import type { BuildingModel } from '@/types/generated/models/building-model';
import type { RoadState } from '@/types/generated/models/road-state';
import type { RoadModel } from '@/types/generated/models/road-model';
import type { TileModel } from '@/types/generated/models/tile-model';
import type { HarborModel } from '@/types/generated/models/harbor-model';

// Re-export generated types so existing consumers don't break
export type { ResourceType, HarborType, HexSide, HexPosition };
export type { BuildingState, BuildingModel, RoadState, RoadModel, TileModel, HarborModel };

export interface TestGameData {
  gameType: string;
  gameName: string;
  tiles: TileModel[];
  harbors: HarborModel[];
  buildings?: BuildingModel[];
  roads?: RoadModel[];
}

/**
 * Generate roads for a tile with the given owner
 */
export function generateRoadsForTile(
  q: number,
  r: number,
  ownerId: string | null,
  roadState: RoadState = 'Road'
): RoadModel[] {
  const s = -q - r;
  const sides: HexSide[] = ['Top', 'TopRight', 'BottomRight', 'Bottom', 'BottomLeft', 'TopLeft'];
  return sides.map((side) => ({
    roadKey: { tileKey: { q, r, s }, hexSide: side },
    roadState,
    ownerId: ownerId ?? '',
    buildIndex: 0,
  }));
}

/**
 * Generate a single road on a specific tile side
 */
export function generateRoad(
  q: number,
  r: number,
  side: HexSide,
  ownerId: string | null,
  roadState: RoadState = 'Road'
): RoadModel {
  const s = -q - r;
  return {
    roadKey: { tileKey: { q, r, s }, hexSide: side },
    roadState,
    ownerId: ownerId ?? '',
    buildIndex: 0,
  };
}

/**
 * Generate a building at a specific position
 */
export function generateBuilding(
  q: number,
  r: number,
  position: HexPosition,
  ownerId: string | null,
  buildingState: BuildingState = 'Settlement'
): BuildingModel {
  const s = -q - r;
  return {
    buildingKey: { hexCoordinates: { q, r, s }, position },
    buildingState,
    wall: false,
    metropolis: buildingState === 'Metropolis',
    ownerId: ownerId ?? '',
    hasRobber: false,
  };
}

/**
 * Generate test data with buildings and roads for multiple players
 * Used to verify rendering with different player colors
 */
export function generateTestBuildingsAndRoads(playerIds: string[]): {
  buildings: BuildingModel[];
  roads: RoadModel[];
} {
  const buildings: BuildingModel[] = [];
  const roads: RoadModel[] = [];

  // Player 1 (red): Settlement at (-1,0,1) TopRight, City at (0,0,0) Right
  if (playerIds[0]) {
    buildings.push(generateBuilding(-1, 0, 'TopRight', playerIds[0], 'Settlement'));
    buildings.push(generateBuilding(0, 0, 'Right', playerIds[0], 'City'));
    roads.push(generateRoad(-1, 0, 'Top', playerIds[0]));
    roads.push(generateRoad(-1, 0, 'TopRight', playerIds[0]));
  }

  // Player 2 (blue): Settlement at (1,-1,0) BottomLeft, City at (1,0,-1) TopLeft
  if (playerIds[1]) {
    buildings.push(generateBuilding(1, -1, 'BottomLeft', playerIds[1], 'Settlement'));
    buildings.push(generateBuilding(1, 0, 'TopLeft', playerIds[1], 'City'));
    roads.push(generateRoad(1, -1, 'Bottom', playerIds[1]));
    roads.push(generateRoad(1, -1, 'BottomLeft', playerIds[1]));
  }

  // Player 3 (green): Settlement at (0,1,-1) Left, City at (-1,1,0) Right
  if (playerIds[2]) {
    buildings.push(generateBuilding(0, 1, 'Left', playerIds[2], 'Settlement'));
    buildings.push(generateBuilding(-1, 1, 'Right', playerIds[2], 'City'));
    roads.push(generateRoad(0, 1, 'TopLeft', playerIds[2]));
    roads.push(generateRoad(0, 1, 'BottomLeft', playerIds[2]));
  }

  // Player 4 (orange): Settlement at (0,-1,1) BottomRight, City at (1,-1,0) TopRight
  if (playerIds[3]) {
    buildings.push(generateBuilding(0, -1, 'BottomRight', playerIds[3], 'Settlement'));
    buildings.push(generateBuilding(1, -1, 'TopRight', playerIds[3], 'City'));
    roads.push(generateRoad(0, -1, 'BottomRight', playerIds[3]));
    roads.push(generateRoad(0, -1, 'Bottom', playerIds[3]));
  }

  return { buildings, roads };
}

/** Create a test tile (no phantom fields — matches wire format). */
function tile(q: number, r: number, num: number, res: ResourceType, gold = false): TileModel {
  return {
    tileKey: { q, r, s: -q - r || 0 },
    number: num,
    resourceTileType: res,
    highlighted: false,
    temporarilyGold: gold,
  };
}

export const EXPANSION_GAME_DATA: TestGameData = {
  gameType: 'Expansion',
  gameName: 'New Expansion Game',
  roads: generateRoadsForTile(0, 0, 'player-1'),
  tiles: [
    tile(-3, 1, 4, 'Ore'),
    tile(-3, 2, 8, 'Wheat'),
    tile(-3, 3, 3, 'Brick'),
    tile(-2, 0, 6, 'Sheep'),
    tile(-2, 1, 2, 'Wheat'),
    tile(-2, 2, 12, 'Brick'),
    tile(-2, 3, 10, 'Wood'),
    tile(-1, -1, 11, 'Sheep'),
    tile(-1, 0, 11, 'Ore'),
    tile(-1, 1, 9, 'Sheep'),
    tile(-1, 2, 7, 'Desert'),
    tile(-1, 3, 8, 'Wood'),
    tile(0, -2, 11, 'Brick'),
    tile(0, -1, 7, 'Desert'),
    tile(0, 0, 9, 'Ore'),
    tile(0, 1, 2, 'Wood'),
    tile(0, 2, 3, 'Wheat'),
    tile(0, 3, 4, 'Wood'),
    tile(1, -2, 10, 'Brick'),
    tile(1, -1, 5, 'Wood'),
    tile(1, 0, 9, 'Brick'),
    tile(1, 1, 6, 'Sheep'),
    tile(1, 2, 5, 'Wood'),
    tile(2, -2, 10, 'Sheep'),
    tile(2, -1, 8, 'Wheat'),
    tile(2, 0, 5, 'Ore'),
    tile(2, 1, 12, 'Ore'),
    tile(3, -2, 4, 'Sheep'),
    tile(3, -1, 3, 'Wheat'),
    tile(3, 0, 6, 'Brick'),
  ],
  harbors: [
    {
      harborKey: { hexCoordinates: { q: -2, r: 0, s: 2 }, harborType: 'Brick', side: 'TopLeft' },
      owner: null as unknown as HarborModel['owner'],
    },
    {
      harborKey: { hexCoordinates: { q: 0, r: -2, s: 2 }, harborType: 'Wheat', side: 'Top' },
      owner: null as unknown as HarborModel['owner'],
    },
    {
      harborKey: {
        hexCoordinates: { q: 1, r: -2, s: 1 },
        harborType: 'ThreeForOne',
        side: 'TopRight',
      },
      owner: null as unknown as HarborModel['owner'],
    },
    {
      harborKey: { hexCoordinates: { q: 2, r: -2, s: 0 }, harborType: 'Sheep', side: 'TopRight' },
      owner: null as unknown as HarborModel['owner'],
    },
    {
      harborKey: {
        hexCoordinates: { q: 3, r: 0, s: -3 },
        harborType: 'ThreeForOne',
        side: 'BottomRight',
      },
      owner: null as unknown as HarborModel['owner'],
    },
    {
      harborKey: {
        hexCoordinates: { q: 2, r: 1, s: -3 },
        harborType: 'ThreeForOne',
        side: 'Bottom',
      },
      owner: null as unknown as HarborModel['owner'],
    },
    {
      harborKey: { hexCoordinates: { q: 1, r: 2, s: -3 }, harborType: 'Wood', side: 'Bottom' },
      owner: null as unknown as HarborModel['owner'],
    },
    {
      harborKey: {
        hexCoordinates: { q: -1, r: 3, s: -2 },
        harborType: 'ThreeForOne',
        side: 'Bottom',
      },
      owner: null as unknown as HarborModel['owner'],
    },
    {
      harborKey: {
        hexCoordinates: { q: -3, r: 3, s: 0 },
        harborType: 'ThreeForOne',
        side: 'BottomLeft',
      },
      owner: null as unknown as HarborModel['owner'],
    },
    {
      harborKey: { hexCoordinates: { q: -3, r: 2, s: 1 }, harborType: 'Ore', side: 'TopLeft' },
      owner: null as unknown as HarborModel['owner'],
    },
    {
      harborKey: {
        hexCoordinates: { q: 3, r: -1, s: -2 },
        harborType: 'ThreeForOne',
        side: 'TopRight',
      },
      owner: null as unknown as HarborModel['owner'],
    },
  ],
};

/**
 * Resource type to tile image mapping
 */
export const RESOURCE_TILE_IMAGES: Partial<Record<ResourceType, string>> = {
  Wheat: '/themes/base/tiles/wheat.png',
  Wood: '/themes/base/tiles/wood.png',
  Sheep: '/themes/base/tiles/sheep.png',
  Brick: '/themes/base/tiles/brick.png',
  Ore: '/themes/base/tiles/ore.png',
  Desert: '/themes/base/tiles/desert.png',
  GoldMine: '/themes/base/tiles/goldMine.png',
  Sea: '/themes/base/tiles/back.jpg',
};

/**
 * Harbor type to image mapping
 */
export const HARBOR_IMAGES: Record<HarborType, string> = {
  ThreeForOne: '/themes/base/harbors/3 for 1.png',
  Wheat: '/themes/base/harbors/2 for 1 wheat.png',
  Wood: '/themes/base/harbors/2 for 1 wood.png',
  Sheep: '/themes/base/harbors/2 for 1 sheep.png',
  Brick: '/themes/base/harbors/2 for 1 brick.png',
  Ore: '/themes/base/harbors/2 for 1 ore.png',
  None: '',
};

/**
 * Pip count for each dice number (matches Blazor implementation)
 * Number of pips indicates probability (2-12 have different probabilities)
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
