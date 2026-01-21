/**
 * Model utility functions for game element lookup and navigation.
 * TypeScript port of C# extension methods from Catan3.Shared/Extensions/.
 *
 * These are pure functions (no side effects) for client-side rendering.
 * Game rules and validation remain server-side.
 */

import type {
  BuildingKey,
  BuildingModel,
  Direction,
  HexCoordinates,
  HexPosition,
  HexSide,
  RoadKey,
  RoadModel,
  TileModel,
  GameModel,
  PlayerModel,
} from '@/types/generated/models';
import {
  addHexCoords,
  hexEquals,
  DIRECTION_OFFSETS,
  type HexCoords,
} from '../geometry/boardGeometry';

// =============================================================================
// Key Comparison Utilities
// =============================================================================

/**
 * Compares two BuildingKeys for equality.
 */
export function buildingKeyEquals(a: BuildingKey, b: BuildingKey): boolean {
  return hexEquals(a.hexCoordinates, b.hexCoordinates) && a.position === b.position;
}

/**
 * Compares two RoadKeys for equality.
 */
export function roadKeyEquals(a: RoadKey, b: RoadKey): boolean {
  return hexEquals(a.tileKey, b.tileKey) && a.hexSide === b.hexSide;
}

// =============================================================================
// Hex Navigation (Direction-based)
// =============================================================================

/**
 * Gets the adjacent tile coordinates in the specified direction.
 * Matches C# HexCoordinates.GetAdjacentTile(Direction).
 */
export function getAdjacentTile(coords: HexCoords, direction: Direction): HexCoords {
  const offset = DIRECTION_OFFSETS[direction];
  if (!offset) {
    throw new Error(`Invalid direction: ${direction}`);
  }
  return addHexCoords(coords, offset);
}

// =============================================================================
// Building Aliases
// =============================================================================

/**
 * Building alias definition: alternative location for the same vertex.
 * A vertex is shared by 3 hexes, so each vertex has 2 aliases.
 */
export interface BuildingAlias {
  position: HexPosition;
  direction: Direction;
}

/**
 * Gets the aliases for a building key (the other 2 hexes that share this vertex).
 * Matches C# BuildingModelExtensions.Aliases().
 */
export function getBuildingAliases(key: BuildingKey): BuildingAlias[] {
  const aliases: BuildingAlias[] = [];

  switch (key.position) {
    case 'TopRight':
      aliases.push({ position: 'BottomRight', direction: 'North' });
      aliases.push({ position: 'Left', direction: 'NorthEast' });
      break;
    case 'Right':
      aliases.push({ position: 'BottomLeft', direction: 'NorthEast' });
      aliases.push({ position: 'TopLeft', direction: 'SouthEast' });
      break;
    case 'BottomRight':
      aliases.push({ position: 'TopRight', direction: 'South' });
      aliases.push({ position: 'Left', direction: 'SouthEast' });
      break;
    case 'BottomLeft':
      aliases.push({ position: 'Right', direction: 'SouthWest' });
      aliases.push({ position: 'TopLeft', direction: 'South' });
      break;
    case 'Left':
      aliases.push({ position: 'BottomRight', direction: 'NorthWest' });
      aliases.push({ position: 'TopRight', direction: 'SouthWest' });
      break;
    case 'TopLeft':
      aliases.push({ position: 'Right', direction: 'NorthWest' });
      aliases.push({ position: 'BottomLeft', direction: 'North' });
      break;
  }

  return aliases;
}

/**
 * Finds a building by key, checking aliases if not found at primary location.
 * Matches C# BuildingModelExtensions.FindBuildingModel().
 */
export function findBuilding(
  buildings: BuildingModel[],
  key: BuildingKey
): BuildingModel | undefined {
  if (!buildings || buildings.length === 0) return undefined;

  // Try primary key first
  let building = buildings.find((b) => buildingKeyEquals(b.buildingKey, key));
  if (building) return building;

  // Try aliases
  const aliases = getBuildingAliases(key);
  for (const alias of aliases) {
    const aliasCoords = getAdjacentTile(key.hexCoordinates, alias.direction);
    const aliasKey: BuildingKey = {
      hexCoordinates: aliasCoords,
      position: alias.position,
    } as BuildingKey;

    building = buildings.find((b) => buildingKeyEquals(b.buildingKey, aliasKey));
    if (building) return building;
  }

  return undefined;
}

/**
 * Gets adjacent buildings (within 1 edge) of the given building key.
 * Matches C# BuildingModelExtensions.AdjacentBuildings().
 */
export function getAdjacentBuildings(
  buildings: BuildingModel[],
  key: BuildingKey
): BuildingModel[] {
  const adjacentKeys: BuildingKey[] = [];

  switch (key.position) {
    case 'Right':
      adjacentKeys.push({ hexCoordinates: key.hexCoordinates, position: 'TopRight' } as BuildingKey);
      adjacentKeys.push({
        hexCoordinates: key.hexCoordinates,
        position: 'BottomRight',
      } as BuildingKey);
      adjacentKeys.push({
        hexCoordinates: getAdjacentTile(key.hexCoordinates, 'NorthEast'),
        position: 'BottomRight',
      } as BuildingKey);
      break;
    case 'BottomRight':
      adjacentKeys.push({ hexCoordinates: key.hexCoordinates, position: 'Right' } as BuildingKey);
      adjacentKeys.push({
        hexCoordinates: key.hexCoordinates,
        position: 'BottomLeft',
      } as BuildingKey);
      adjacentKeys.push({
        hexCoordinates: getAdjacentTile(key.hexCoordinates, 'South'),
        position: 'Right',
      } as BuildingKey);
      break;
    case 'BottomLeft':
      adjacentKeys.push({ hexCoordinates: key.hexCoordinates, position: 'Left' } as BuildingKey);
      adjacentKeys.push({
        hexCoordinates: key.hexCoordinates,
        position: 'BottomRight',
      } as BuildingKey);
      adjacentKeys.push({
        hexCoordinates: getAdjacentTile(key.hexCoordinates, 'South'),
        position: 'Left',
      } as BuildingKey);
      break;
    case 'Left':
      adjacentKeys.push({ hexCoordinates: key.hexCoordinates, position: 'TopLeft' } as BuildingKey);
      adjacentKeys.push({
        hexCoordinates: key.hexCoordinates,
        position: 'BottomLeft',
      } as BuildingKey);
      adjacentKeys.push({
        hexCoordinates: getAdjacentTile(key.hexCoordinates, 'NorthWest'),
        position: 'BottomLeft',
      } as BuildingKey);
      break;
    case 'TopLeft':
      adjacentKeys.push({ hexCoordinates: key.hexCoordinates, position: 'TopRight' } as BuildingKey);
      adjacentKeys.push({ hexCoordinates: key.hexCoordinates, position: 'Left' } as BuildingKey);
      adjacentKeys.push({
        hexCoordinates: getAdjacentTile(key.hexCoordinates, 'NorthWest'),
        position: 'TopRight',
      } as BuildingKey);
      break;
    case 'TopRight':
      adjacentKeys.push({ hexCoordinates: key.hexCoordinates, position: 'TopLeft' } as BuildingKey);
      adjacentKeys.push({ hexCoordinates: key.hexCoordinates, position: 'Right' } as BuildingKey);
      adjacentKeys.push({
        hexCoordinates: getAdjacentTile(key.hexCoordinates, 'North'),
        position: 'Right',
      } as BuildingKey);
      break;
  }

  const result: BuildingModel[] = [];
  for (const adjacentKey of adjacentKeys) {
    const building = findBuilding(buildings, adjacentKey);
    if (building) {
      result.push(building);
    }
  }

  return result;
}

/**
 * Gets all buildings within a tile (6 vertices).
 * Matches C# BuildingModelExtensions.BuildingsInTile().
 */
export function getBuildingsInTile(
  buildings: BuildingModel[],
  coords: HexCoords
): BuildingModel[] {
  const positions: HexPosition[] = [
    'Right',
    'BottomRight',
    'BottomLeft',
    'Left',
    'TopLeft',
    'TopRight',
  ];
  const result: BuildingModel[] = [];

  for (const position of positions) {
    const key: BuildingKey = { hexCoordinates: coords, position } as BuildingKey;
    const building = findBuilding(buildings, key);
    if (building) {
      result.push(building);
    }
  }

  return result;
}

/**
 * Gets all owned buildings within a tile.
 * Matches C# BuildingModelExtensions.OwnedBuildings().
 */
export function getOwnedBuildingsInTile(
  buildings: BuildingModel[],
  coords: HexCoords
): BuildingModel[] {
  return getBuildingsInTile(buildings, coords).filter((b) => b.ownerId != null);
}

// =============================================================================
// Road Aliases
// =============================================================================

/**
 * Road alias definition: alternative location for the same edge.
 * An edge is shared by 2 hexes, so each road has 1 alias.
 */
export interface RoadAlias {
  hexSide: HexSide;
  direction: Direction;
}

/**
 * Gets the alias for a road key (the other hex that shares this edge).
 * Matches C# RoadModelExtensions.Aliases().
 */
export function getRoadAliases(key: RoadKey): RoadAlias[] {
  const aliases: RoadAlias[] = [];

  switch (key.hexSide) {
    case 'TopRight':
      aliases.push({ hexSide: 'BottomLeft', direction: 'NorthEast' });
      break;
    case 'BottomRight':
      aliases.push({ hexSide: 'TopLeft', direction: 'SouthEast' });
      break;
    case 'BottomLeft':
      aliases.push({ hexSide: 'TopRight', direction: 'SouthWest' });
      break;
    case 'Bottom':
      aliases.push({ hexSide: 'Top', direction: 'South' });
      break;
    case 'Top':
      aliases.push({ hexSide: 'Bottom', direction: 'North' });
      break;
    case 'TopLeft':
      aliases.push({ hexSide: 'BottomRight', direction: 'NorthWest' });
      break;
  }

  return aliases;
}

/**
 * Finds a road by key, checking alias if not found at primary location.
 * Matches C# RoadModelExtensions.FindRoad().
 */
export function findRoad(roads: RoadModel[], key: RoadKey): RoadModel | undefined {
  if (!roads || roads.length === 0) return undefined;

  // Try primary key first
  let road = roads.find((r) => roadKeyEquals(r.roadKey, key));
  if (road) return road;

  // Try alias
  const aliases = getRoadAliases(key);
  for (const alias of aliases) {
    const aliasCoords = getAdjacentTile(key.tileKey, alias.direction);
    const aliasKey: RoadKey = {
      tileKey: aliasCoords,
      hexSide: alias.hexSide,
    };

    road = roads.find((r) => roadKeyEquals(r.roadKey, aliasKey));
    if (road) return road;
  }

  return undefined;
}

/**
 * Gets adjacent roads (sharing a vertex) to the given road key.
 * Matches C# RoadModelExtensions.AdjacentRoads().
 */
export function getAdjacentRoads(roads: RoadModel[], key: RoadKey): RoadModel[] {
  const adjacentKeys: RoadKey[] = [];

  switch (key.hexSide) {
    case 'Top':
      adjacentKeys.push({ tileKey: key.tileKey, hexSide: 'TopRight' });
      adjacentKeys.push({ tileKey: key.tileKey, hexSide: 'TopLeft' });
      adjacentKeys.push({
        tileKey: getAdjacentTile(key.tileKey, 'North'),
        hexSide: 'BottomRight',
      });
      adjacentKeys.push({
        tileKey: getAdjacentTile(key.tileKey, 'NorthWest'),
        hexSide: 'BottomLeft',
      });
      break;
    case 'TopRight':
      adjacentKeys.push({ tileKey: key.tileKey, hexSide: 'BottomRight' });
      adjacentKeys.push({ tileKey: key.tileKey, hexSide: 'Top' });
      adjacentKeys.push({
        tileKey: getAdjacentTile(key.tileKey, 'NorthEast'),
        hexSide: 'TopLeft',
      });
      adjacentKeys.push({
        tileKey: getAdjacentTile(key.tileKey, 'NorthEast'),
        hexSide: 'Bottom',
      });
      break;
    case 'BottomRight':
      adjacentKeys.push({ tileKey: key.tileKey, hexSide: 'TopRight' });
      adjacentKeys.push({ tileKey: key.tileKey, hexSide: 'Bottom' });
      adjacentKeys.push({
        tileKey: getAdjacentTile(key.tileKey, 'SouthEast'),
        hexSide: 'Top',
      });
      adjacentKeys.push({
        tileKey: getAdjacentTile(key.tileKey, 'SouthEast'),
        hexSide: 'BottomLeft',
      });
      break;
    case 'Bottom':
      adjacentKeys.push({ tileKey: key.tileKey, hexSide: 'BottomLeft' });
      adjacentKeys.push({ tileKey: key.tileKey, hexSide: 'BottomRight' });
      adjacentKeys.push({
        tileKey: getAdjacentTile(key.tileKey, 'South'),
        hexSide: 'TopLeft',
      });
      adjacentKeys.push({
        tileKey: getAdjacentTile(key.tileKey, 'South'),
        hexSide: 'TopRight',
      });
      break;
    case 'BottomLeft':
      adjacentKeys.push({ tileKey: key.tileKey, hexSide: 'Bottom' });
      adjacentKeys.push({ tileKey: key.tileKey, hexSide: 'TopLeft' });
      adjacentKeys.push({
        tileKey: getAdjacentTile(key.tileKey, 'SouthWest'),
        hexSide: 'Top',
      });
      adjacentKeys.push({
        tileKey: getAdjacentTile(key.tileKey, 'SouthWest'),
        hexSide: 'BottomRight',
      });
      break;
    case 'TopLeft':
      adjacentKeys.push({ tileKey: key.tileKey, hexSide: 'Top' });
      adjacentKeys.push({ tileKey: key.tileKey, hexSide: 'BottomLeft' });
      adjacentKeys.push({
        tileKey: getAdjacentTile(key.tileKey, 'NorthWest'),
        hexSide: 'TopRight',
      });
      adjacentKeys.push({
        tileKey: getAdjacentTile(key.tileKey, 'NorthWest'),
        hexSide: 'Bottom',
      });
      break;
  }

  const result: RoadModel[] = [];
  for (const adjacentKey of adjacentKeys) {
    const road = findRoad(roads, adjacentKey);
    if (road) {
      result.push(road);
    }
  }

  return result;
}

// =============================================================================
// Tile Utilities
// =============================================================================

/**
 * Finds a tile by coordinates.
 * Matches C# TileModelExtensions.TileFromCoords().
 */
export function findTile(tiles: TileModel[], coords: HexCoords): TileModel | undefined {
  return tiles.find((t) => hexEquals(t.tileKey, coords));
}

/**
 * Gets adjacent tiles (6 neighbors).
 * Matches C# TileModelExtensions.AdjacentTiles().
 */
export function getAdjacentTiles(tiles: TileModel[], tile: TileModel): TileModel[] {
  const result: TileModel[] = [];

  for (const direction of Object.keys(DIRECTION_OFFSETS) as Direction[]) {
    const neighborCoords = getAdjacentTile(tile.tileKey, direction);
    const neighbor = findTile(tiles, neighborCoords);
    if (neighbor) {
      result.push(neighbor);
    }
  }

  return result;
}

/**
 * Gets the star count (probability weight) for a dice number.
 * 6 and 8 are most common (5 stars), 2 and 12 are rarest (1 star).
 */
export function getNumberStars(diceNumber: number): number {
  switch (diceNumber) {
    case 2:
    case 12:
      return 1;
    case 3:
    case 11:
      return 2;
    case 4:
    case 10:
      return 3;
    case 5:
    case 9:
      return 4;
    case 6:
    case 8:
      return 5;
    case 7:
      return 0; // Robber number
    default:
      return 0;
  }
}

/**
 * Sums the star count for all tiles in the collection.
 * Matches C# TileModelExtensions.Stars().
 */
export function getTotalStars(tiles: TileModel[]): number {
  return tiles.reduce((sum, tile) => sum + getNumberStars(tile.number), 0);
}

/**
 * Gets all tiles with the specified dice number.
 * Matches C# TileModelExtensions.TilesWithNumber().
 */
export function getTilesWithNumber(tiles: TileModel[], number: number): TileModel[] {
  return tiles.filter((t) => t.number === number);
}

/**
 * Gets all tiles with the specified resource type.
 * Matches C# TileModelExtensions.TilesWithResource().
 */
export function getTilesWithResource(
  tiles: TileModel[],
  resource: string
): TileModel[] {
  return tiles.filter((t) => t.resourceTileType === resource);
}

/**
 * Gets all tiles with number 6 or 8 (high probability).
 * Matches C# TileModelExtensions.TilesWithSixOrEight().
 */
export function getHighProbabilityTiles(tiles: TileModel[]): TileModel[] {
  return tiles.filter((t) => t.number === 6 || t.number === 8);
}

// =============================================================================
// Player Utilities
// =============================================================================

/**
 * Finds a player by ID.
 * Matches C# PlayerModelExtensions.PlayerFromId().
 */
export function findPlayer(players: PlayerModel[], id: string): PlayerModel | undefined {
  return players.find((p) => p.playerId === id);
}

// =============================================================================
// GameModel Utilities
// =============================================================================

/**
 * Gets the current player from the game model.
 */
export function getCurrentPlayer(game: GameModel): PlayerModel | undefined {
  return findPlayer(game.players, game.currentPlayerId);
}

/**
 * Gets roads adjacent to a building (for road building validation visuals).
 * Uses the game's road collection.
 */
export function getRoadsAdjacentToBuilding(
  game: GameModel,
  buildingKey: BuildingKey
): RoadModel[] {
  // A building vertex touches 3 road edges
  const roadKeys: RoadKey[] = [];

  switch (buildingKey.position) {
    case 'Right':
      roadKeys.push({ tileKey: buildingKey.hexCoordinates, hexSide: 'TopRight' });
      roadKeys.push({ tileKey: buildingKey.hexCoordinates, hexSide: 'BottomRight' });
      roadKeys.push({
        tileKey: getAdjacentTile(buildingKey.hexCoordinates, 'NorthEast'),
        hexSide: 'Bottom',
      });
      break;
    case 'BottomRight':
      roadKeys.push({ tileKey: buildingKey.hexCoordinates, hexSide: 'BottomRight' });
      roadKeys.push({ tileKey: buildingKey.hexCoordinates, hexSide: 'Bottom' });
      roadKeys.push({
        tileKey: getAdjacentTile(buildingKey.hexCoordinates, 'SouthEast'),
        hexSide: 'TopLeft',
      });
      break;
    case 'BottomLeft':
      roadKeys.push({ tileKey: buildingKey.hexCoordinates, hexSide: 'Bottom' });
      roadKeys.push({ tileKey: buildingKey.hexCoordinates, hexSide: 'BottomLeft' });
      roadKeys.push({
        tileKey: getAdjacentTile(buildingKey.hexCoordinates, 'SouthWest'),
        hexSide: 'TopRight',
      });
      break;
    case 'Left':
      roadKeys.push({ tileKey: buildingKey.hexCoordinates, hexSide: 'BottomLeft' });
      roadKeys.push({ tileKey: buildingKey.hexCoordinates, hexSide: 'TopLeft' });
      roadKeys.push({
        tileKey: getAdjacentTile(buildingKey.hexCoordinates, 'NorthWest'),
        hexSide: 'Bottom',
      });
      break;
    case 'TopLeft':
      roadKeys.push({ tileKey: buildingKey.hexCoordinates, hexSide: 'TopLeft' });
      roadKeys.push({ tileKey: buildingKey.hexCoordinates, hexSide: 'Top' });
      roadKeys.push({
        tileKey: getAdjacentTile(buildingKey.hexCoordinates, 'NorthWest'),
        hexSide: 'BottomRight',
      });
      break;
    case 'TopRight':
      roadKeys.push({ tileKey: buildingKey.hexCoordinates, hexSide: 'Top' });
      roadKeys.push({ tileKey: buildingKey.hexCoordinates, hexSide: 'TopRight' });
      roadKeys.push({
        tileKey: getAdjacentTile(buildingKey.hexCoordinates, 'North'),
        hexSide: 'BottomLeft',
      });
      break;
  }

  const result: RoadModel[] = [];
  for (const roadKey of roadKeys) {
    const road = findRoad(game.roads, roadKey);
    if (road) {
      result.push(road);
    }
  }

  return result;
}

/**
 * Gets tiles connected to a building (for resource calculation display).
 * A building vertex touches up to 3 tiles.
 */
export function getTilesForBuilding(game: GameModel, buildingKey: BuildingKey): TileModel[] {
  const result: TileModel[] = [];

  // Primary tile
  const primaryTile = findTile(game.tiles, buildingKey.hexCoordinates);
  if (primaryTile) {
    result.push(primaryTile);
  }

  // Alias tiles (the other 2 hexes sharing this vertex)
  const aliases = getBuildingAliases(buildingKey);
  for (const alias of aliases) {
    const tileCoords = getAdjacentTile(buildingKey.hexCoordinates, alias.direction);
    const tile = findTile(game.tiles, tileCoords);
    if (tile) {
      result.push(tile);
    }
  }

  return result;
}

// =============================================================================
// Automation ID Utilities (for testing)
// =============================================================================

/**
 * Gets the automation ID for a building.
 * Matches C# BuildingModelExtensions.GetAutomationId().
 */
export function getBuildingAutomationId(key: BuildingKey): string {
  const coords = key.hexCoordinates;
  return `Building-(${coords.q},${coords.r},${coords.s})-${key.position}`;
}

/**
 * Gets the automation ID for a road.
 * Matches C# RoadModelExtensions.GetAutomationId().
 */
export function getRoadAutomationId(key: RoadKey): string {
  const coords = key.tileKey;
  return `Road-(${coords.q},${coords.r},${coords.s})-${key.hexSide}`;
}

/**
 * Gets the automation ID for a tile.
 * Matches C# TileModelExtensions.GetTileAutomationId().
 */
export function getTileAutomationId(tile: TileModel): string {
  const coords = tile.tileKey;
  return `Tile-(${coords.q},${coords.r},${coords.s})`;
}
