/**
 * Expansion game test data extracted from Catan3.Shared/TestData/Expansion.catan_test
 * Used for testing board rendering without a live SignalR connection.
 */

// Types matching GameModel structure
export interface HexCoordinates {
  q: number;
  r: number;
  s: number;
}

export type ResourceTileType =
  | 'Wheat'
  | 'Wood'
  | 'Sheep'
  | 'Brick'
  | 'Ore'
  | 'Desert'
  | 'GoldMine'
  | 'Water';

export type HarborType =
  | 'ThreeForOne'
  | 'Wheat'
  | 'Wood'
  | 'Sheep'
  | 'Brick'
  | 'Ore'
  | 'None';

export type HexSide =
  | 'Top'
  | 'TopRight'
  | 'BottomRight'
  | 'Bottom'
  | 'BottomLeft'
  | 'TopLeft';

/** Vertex position on a hex (where settlements/cities are placed) */
export type HexPosition =
  | 'Right'
  | 'BottomRight'
  | 'BottomLeft'
  | 'Left'
  | 'TopLeft'
  | 'TopRight';

/** Building state (matches C# BuildingState enum) */
export type BuildingState =
  | 'PossibleSettlement'
  | 'NotBuildable'
  | 'Settlement'
  | 'City'
  | 'Metropolis'
  | 'Knight';

/** Building key (location identifier) */
export interface BuildingKey {
  hexCoordinates: HexCoordinates;
  position: HexPosition;
}

/** Building model (matches C# BuildingModel) */
export interface BuildingModel {
  buildingKey: BuildingKey;
  buildingState: BuildingState;
  wall: boolean;
  metropolis: boolean;
  ownerId: string | null;
  hasRobber: boolean;
}

/** Road state (matches C# RoadState enum) */
export type RoadState = 'Unowned' | 'Road' | 'Ship' | 'Buildable';

/** Road key (location identifier) */
export interface RoadKey {
  tileKey: HexCoordinates;
  hexSide: HexSide;
}

/** Road model (matches C# RoadModel) */
export interface RoadModel {
  roadKey: RoadKey;
  roadState: RoadState;
  ownerId: string | null;
  buildIndex: number;
}

export interface TileModel {
  tileKey: HexCoordinates;
  number: number;
  resourceTileType: ResourceTileType;
  highlighted: boolean;
  temporarilyGold: boolean;
}

export interface HarborKey {
  hexCoordinates: HexCoordinates;
  harborType: HarborType;
  side: HexSide;
}

export interface HarborModel {
  harborKey: HarborKey;
  owner: string | null;
}

export interface TestGameData {
  gameType: string;
  gameName: string;
  tiles: TileModel[];
  harbors: HarborModel[];
  buildings?: BuildingModel[];
  roads?: RoadModel[];
}

/**
 * Expansion board test data - 30 tiles with harbors
 */
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
    ownerId,
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
    ownerId,
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
    ownerId,
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
  // Roads around tile (-1,0,1)
  if (playerIds[0]) {
    buildings.push(generateBuilding(-1, 0, 'TopRight', playerIds[0], 'Settlement'));
    buildings.push(generateBuilding(0, 0, 'Right', playerIds[0], 'City'));
    roads.push(generateRoad(-1, 0, 'Top', playerIds[0]));
    roads.push(generateRoad(-1, 0, 'TopRight', playerIds[0]));
  }

  // Player 2 (blue): Settlement at (1,-1,0) BottomLeft, City at (1,0,-1) TopLeft
  // Roads around tile (1,-1,0)
  if (playerIds[1]) {
    buildings.push(generateBuilding(1, -1, 'BottomLeft', playerIds[1], 'Settlement'));
    buildings.push(generateBuilding(1, 0, 'TopLeft', playerIds[1], 'City'));
    roads.push(generateRoad(1, -1, 'Bottom', playerIds[1]));
    roads.push(generateRoad(1, -1, 'BottomLeft', playerIds[1]));
  }

  // Player 3 (green): Settlement at (0,1,-1) Left, City at (-1,1,0) Right
  // Roads around tile (0,1,-1)
  if (playerIds[2]) {
    buildings.push(generateBuilding(0, 1, 'Left', playerIds[2], 'Settlement'));
    buildings.push(generateBuilding(-1, 1, 'Right', playerIds[2], 'City'));
    roads.push(generateRoad(0, 1, 'TopLeft', playerIds[2]));
    roads.push(generateRoad(0, 1, 'BottomLeft', playerIds[2]));
  }

  // Player 4 (orange): Settlement at (0,-1,1) BottomRight, City at (1,-1,0) TopRight
  // Roads around tile (0,-1,1)
  if (playerIds[3]) {
    buildings.push(generateBuilding(0, -1, 'BottomRight', playerIds[3], 'Settlement'));
    buildings.push(generateBuilding(1, -1, 'TopRight', playerIds[3], 'City'));
    roads.push(generateRoad(0, -1, 'BottomRight', playerIds[3]));
    roads.push(generateRoad(0, -1, 'Bottom', playerIds[3]));
  }

  return { buildings, roads };
}

export const EXPANSION_GAME_DATA: TestGameData = {
  gameType: 'Expansion',
  gameName: 'New Expansion Game',
  // Roads for center tile (0,0,0) - owned by player-1 to test rendering
  roads: generateRoadsForTile(0, 0, 'player-1'),
  tiles: [
    { tileKey: { q: -3, r: 1, s: 2 }, number: 4, resourceTileType: 'Ore', highlighted: false, temporarilyGold: false },
    { tileKey: { q: -3, r: 2, s: 1 }, number: 8, resourceTileType: 'Wheat', highlighted: false, temporarilyGold: false },
    { tileKey: { q: -3, r: 3, s: 0 }, number: 3, resourceTileType: 'Brick', highlighted: false, temporarilyGold: false },
    { tileKey: { q: -2, r: 0, s: 2 }, number: 6, resourceTileType: 'Sheep', highlighted: false, temporarilyGold: false },
    { tileKey: { q: -2, r: 1, s: 1 }, number: 2, resourceTileType: 'Wheat', highlighted: false, temporarilyGold: false },
    { tileKey: { q: -2, r: 2, s: 0 }, number: 12, resourceTileType: 'Brick', highlighted: false, temporarilyGold: false },
    { tileKey: { q: -2, r: 3, s: -1 }, number: 10, resourceTileType: 'Wood', highlighted: false, temporarilyGold: false },
    { tileKey: { q: -1, r: -1, s: 2 }, number: 11, resourceTileType: 'Sheep', highlighted: false, temporarilyGold: false },
    { tileKey: { q: -1, r: 0, s: 1 }, number: 11, resourceTileType: 'Ore', highlighted: false, temporarilyGold: false },
    { tileKey: { q: -1, r: 1, s: 0 }, number: 9, resourceTileType: 'Sheep', highlighted: false, temporarilyGold: false },
    { tileKey: { q: -1, r: 2, s: -1 }, number: 7, resourceTileType: 'Desert', highlighted: false, temporarilyGold: false },
    { tileKey: { q: -1, r: 3, s: -2 }, number: 8, resourceTileType: 'Wood', highlighted: false, temporarilyGold: false },
    { tileKey: { q: 0, r: -2, s: 2 }, number: 11, resourceTileType: 'Brick', highlighted: false, temporarilyGold: false },
    { tileKey: { q: 0, r: -1, s: 1 }, number: 7, resourceTileType: 'Desert', highlighted: false, temporarilyGold: false },
    { tileKey: { q: 0, r: 0, s: 0 }, number: 9, resourceTileType: 'Ore', highlighted: false, temporarilyGold: false },
    { tileKey: { q: 0, r: 1, s: -1 }, number: 2, resourceTileType: 'Wood', highlighted: false, temporarilyGold: false },
    { tileKey: { q: 0, r: 2, s: -2 }, number: 3, resourceTileType: 'Wheat', highlighted: false, temporarilyGold: false },
    { tileKey: { q: 0, r: 3, s: -3 }, number: 4, resourceTileType: 'Wood', highlighted: false, temporarilyGold: false },
    { tileKey: { q: 1, r: -2, s: 1 }, number: 10, resourceTileType: 'Brick', highlighted: false, temporarilyGold: false },
    { tileKey: { q: 1, r: -1, s: 0 }, number: 5, resourceTileType: 'Wood', highlighted: false, temporarilyGold: false },
    { tileKey: { q: 1, r: 0, s: -1 }, number: 9, resourceTileType: 'Brick', highlighted: false, temporarilyGold: false },
    { tileKey: { q: 1, r: 1, s: -2 }, number: 6, resourceTileType: 'Sheep', highlighted: false, temporarilyGold: false },
    { tileKey: { q: 1, r: 2, s: -3 }, number: 5, resourceTileType: 'Wood', highlighted: false, temporarilyGold: false },
    { tileKey: { q: 2, r: -2, s: 0 }, number: 10, resourceTileType: 'Sheep', highlighted: false, temporarilyGold: false },
    { tileKey: { q: 2, r: -1, s: -1 }, number: 8, resourceTileType: 'Wheat', highlighted: false, temporarilyGold: false },
    { tileKey: { q: 2, r: 0, s: -2 }, number: 5, resourceTileType: 'Ore', highlighted: false, temporarilyGold: false },
    { tileKey: { q: 2, r: 1, s: -3 }, number: 12, resourceTileType: 'Ore', highlighted: false, temporarilyGold: false },
    { tileKey: { q: 3, r: -2, s: -1 }, number: 4, resourceTileType: 'Sheep', highlighted: false, temporarilyGold: false },
    { tileKey: { q: 3, r: -1, s: -2 }, number: 3, resourceTileType: 'Wheat', highlighted: false, temporarilyGold: false },
    { tileKey: { q: 3, r: 0, s: -3 }, number: 6, resourceTileType: 'Brick', highlighted: false, temporarilyGold: false },
  ],
  harbors: [
    { harborKey: { hexCoordinates: { q: -2, r: 0, s: 2 }, harborType: 'Brick', side: 'TopLeft' }, owner: null },
    { harborKey: { hexCoordinates: { q: 0, r: -2, s: 2 }, harborType: 'Wheat', side: 'Top' }, owner: null },
    { harborKey: { hexCoordinates: { q: 1, r: -2, s: 1 }, harborType: 'ThreeForOne', side: 'TopRight' }, owner: null },
    { harborKey: { hexCoordinates: { q: 2, r: -2, s: 0 }, harborType: 'Sheep', side: 'TopRight' }, owner: null },
    { harborKey: { hexCoordinates: { q: 3, r: 0, s: -3 }, harborType: 'ThreeForOne', side: 'BottomRight' }, owner: null },
    { harborKey: { hexCoordinates: { q: 2, r: 1, s: -3 }, harborType: 'ThreeForOne', side: 'Bottom' }, owner: null },
    { harborKey: { hexCoordinates: { q: 1, r: 2, s: -3 }, harborType: 'Wood', side: 'Bottom' }, owner: null },
    { harborKey: { hexCoordinates: { q: -1, r: 3, s: -2 }, harborType: 'ThreeForOne', side: 'Bottom' }, owner: null },
    { harborKey: { hexCoordinates: { q: -3, r: 3, s: 0 }, harborType: 'ThreeForOne', side: 'BottomLeft' }, owner: null },
    { harborKey: { hexCoordinates: { q: -3, r: 2, s: 1 }, harborType: 'Ore', side: 'TopLeft' }, owner: null },
    { harborKey: { hexCoordinates: { q: 3, r: -1, s: -2 }, harborType: 'ThreeForOne', side: 'TopRight' }, owner: null },
  ],
};

/**
 * Resource type to tile image mapping
 */
export const RESOURCE_TILE_IMAGES: Record<ResourceTileType, string> = {
  Wheat: '/themes/base/tiles/wheat.png',
  Wood: '/themes/base/tiles/wood.png',
  Sheep: '/themes/base/tiles/sheep.png',
  Brick: '/themes/base/tiles/brick.png',
  Ore: '/themes/base/tiles/ore.png',
  Desert: '/themes/base/tiles/desert.png',
  GoldMine: '/themes/base/tiles/goldMine.png',
  Water: '/themes/base/tiles/back.jpg',
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
