/**
 * GameModel-related extension functions
 *
 * These are client-side utility functions for working with GameModel data.
 * Server-side operations (shuffle, hash, random, etc.) are NOT ported here.
 *
 * Ported from: Catan3.Shared/Extensions/GameModelExtensions.cs
 */

import type { GameModel } from '@/types/generated/models/game-model';
import type { PlayerModel } from '@/types/generated/models/player-model';
import type { TileModel } from '@/types/generated/models/tile-model';
import type { RoadModel } from '@/types/generated/models/road-model';
import type { BuildingModel } from '@/types/generated/models/building-model';
import type { BuildingKey } from '@/types/generated/models/building-key';
import type { RoadKey } from '@/types/generated/models/road-key';
import type { HexCoordinates } from '@/types/generated/models/hex-coordinates';
import type { HarborModel } from '@/types/generated/models/harbor-model';
import type { HexSide } from '@/types/generated/models/hex-side';
import type { HexPosition } from '@/types/generated/models/hex-position';
import type { EntitlementPurchaseModel } from '@/types/generated/models/entitlement-purchase-model';
import type { Entitlement } from '@/types/generated/models/entitlement';
import type { ResourcesModel } from '@/types/generated/models/resources-model';
import { playerFromId } from './playerExtensions';
import { tileFromCoords, totalStars, hexCoordsEqual } from './tileExtensions';
import { buildingKeyAliases, getAdjacentHex, findBuilding } from './buildingExtensions';
import { findRoad, adjacentRoads } from './roadExtensions';
import { createEmptyResourcesModel, addResource } from './resourcesExtensions';

/**
 * Game phases - higher-level categorization of game states
 */
export type GamePhase =
  | 'Starting'
  | 'PickingBoard'
  | 'PickingResources'
  | 'Rolling'
  | 'Purchase'
  | 'ActionRequired'
  | 'Unspecified';

/**
 * Returns the current player from the game model.
 * Returns undefined if currentPlayerId is not set or player not found.
 */
export function currentPlayer(gameModel: GameModel): PlayerModel | undefined {
  if (!gameModel.currentPlayerId || !gameModel.players) {
    return undefined;
  }
  return playerFromId(gameModel.players, gameModel.currentPlayerId);
}

/**
 * Checks if the game is in the allocation phase.
 */
export function isAllocationPhase(gameModel: GameModel): boolean {
  const state = gameModel.gameState;
  return (
    state === 'AllocateResourceForward' ||
    state === 'AllocateResourceReverse' ||
    state === 'WaitingForRollForOrder' ||
    state === 'FinishedRollOrder' ||
    state === 'BeginResourceAllocation' ||
    state === 'PickingBoard'
  );
}

/**
 * Returns the current game phase based on game state.
 */
export function gamePhase(gameModel: GameModel): GamePhase {
  switch (gameModel.gameState) {
    case 'Uninitialized':
    case 'WaitingForNewGame':
    case 'BeginResourceAllocation':
    case 'WaitingForPlayers':
      return 'Starting';

    case 'PickingBoard':
    case 'WaitingForRollForOrder':
    case 'FinishedRollOrder':
      return 'PickingBoard';

    case 'AllocateResourceForward':
    case 'AllocateResourceReverse':
    case 'DoneResourceAllocation':
      return 'PickingResources';

    case 'WaitingForRoll':
      return 'Rolling';

    case 'WaitingForNext':
    case 'Supplemental':
      return 'Purchase';

    case 'MustMoveRobber':
    case 'TooManyCards':
    case 'MustDestroyCity':
      return 'ActionRequired';

    default:
      return 'Unspecified';
  }
}

/**
 * Gets the player ID that is a specified number of positions away from a given start player.
 * Wraps around the player list.
 * Returns undefined if startPlayerId not found.
 */
export function nextPlayerId(
  gameModel: GameModel,
  startPlayerId: string,
  numberOfPositions: number
): string | undefined {
  if (!gameModel.players || gameModel.players.length === 0) {
    return undefined;
  }

  const startPlayer = playerFromId(gameModel.players, startPlayerId);
  if (!startPlayer) {
    return undefined;
  }

  const idx = gameModel.players.findIndex((p) => p.id === startPlayerId);
  if (idx === -1) {
    return undefined;
  }

  const count = gameModel.players.length;
  let newIdx = (idx + numberOfPositions) % count;
  if (newIdx < 0) {
    newIdx += count;
  }

  return gameModel.players[newIdx]?.id;
}

/**
 * Returns the roads adjacent to a building position.
 * Each building vertex has 3 adjacent roads.
 */
export function adjacentRoadsForBuilding(
  gameModel: GameModel,
  buildingKey: BuildingKey
): RoadModel[] {
  if (!gameModel.roads || gameModel.roads.length === 0) {
    return [];
  }

  const roadKeys: RoadKey[] = [];
  const coords = buildingKey.hexCoordinates;

  switch (buildingKey.position) {
    case 'Right':
      roadKeys.push({ tileKey: coords, hexSide: 'TopRight' });
      roadKeys.push({ tileKey: coords, hexSide: 'BottomRight' });
      roadKeys.push({ tileKey: getAdjacentHex(coords, 'NorthEast'), hexSide: 'Bottom' });
      break;
    case 'BottomRight':
      roadKeys.push({ tileKey: coords, hexSide: 'Bottom' });
      roadKeys.push({ tileKey: coords, hexSide: 'BottomRight' });
      roadKeys.push({ tileKey: getAdjacentHex(coords, 'South'), hexSide: 'TopRight' });
      break;
    case 'BottomLeft':
      roadKeys.push({ tileKey: coords, hexSide: 'Bottom' });
      roadKeys.push({ tileKey: coords, hexSide: 'BottomLeft' });
      roadKeys.push({ tileKey: getAdjacentHex(coords, 'South'), hexSide: 'TopLeft' });
      break;
    case 'Left':
      roadKeys.push({ tileKey: coords, hexSide: 'TopLeft' });
      roadKeys.push({ tileKey: coords, hexSide: 'BottomLeft' });
      roadKeys.push({ tileKey: getAdjacentHex(coords, 'NorthWest'), hexSide: 'Bottom' });
      break;
    case 'TopLeft':
      roadKeys.push({ tileKey: coords, hexSide: 'TopLeft' });
      roadKeys.push({ tileKey: coords, hexSide: 'Top' });
      roadKeys.push({ tileKey: getAdjacentHex(coords, 'NorthWest'), hexSide: 'TopRight' });
      break;
    case 'TopRight':
      roadKeys.push({ tileKey: coords, hexSide: 'TopRight' });
      roadKeys.push({ tileKey: coords, hexSide: 'Top' });
      roadKeys.push({ tileKey: getAdjacentHex(coords, 'North'), hexSide: 'BottomRight' });
      break;
    case 'None':
    default:
      break;
  }

  const result: RoadModel[] = [];
  for (const key of roadKeys) {
    const road = findRoad(gameModel.roads, key);
    if (road) {
      result.push(road);
    }
  }

  return result;
}

/**
 * Returns the buildings adjacent to a road (the 2 buildings at each end).
 */
export function adjacentBuildingsForRoad(
  gameModel: GameModel,
  roadKey: RoadKey
): BuildingModel[] {
  if (!gameModel.buildings || gameModel.buildings.length === 0) {
    return [];
  }

  const buildingKeys: BuildingKey[] = [];
  const coords = roadKey.tileKey;

  switch (roadKey.hexSide) {
    case 'Top':
      buildingKeys.push({ hexCoordinates: coords, position: 'TopLeft', default: {} as BuildingKey });
      buildingKeys.push({ hexCoordinates: coords, position: 'TopRight', default: {} as BuildingKey });
      break;
    case 'TopRight':
      buildingKeys.push({ hexCoordinates: coords, position: 'TopRight', default: {} as BuildingKey });
      buildingKeys.push({ hexCoordinates: coords, position: 'Right', default: {} as BuildingKey });
      break;
    case 'BottomRight':
      buildingKeys.push({ hexCoordinates: coords, position: 'Right', default: {} as BuildingKey });
      buildingKeys.push({ hexCoordinates: coords, position: 'BottomRight', default: {} as BuildingKey });
      break;
    case 'Bottom':
      buildingKeys.push({ hexCoordinates: coords, position: 'BottomRight', default: {} as BuildingKey });
      buildingKeys.push({ hexCoordinates: coords, position: 'BottomLeft', default: {} as BuildingKey });
      break;
    case 'BottomLeft':
      buildingKeys.push({ hexCoordinates: coords, position: 'BottomLeft', default: {} as BuildingKey });
      buildingKeys.push({ hexCoordinates: coords, position: 'Left', default: {} as BuildingKey });
      break;
    case 'TopLeft':
      buildingKeys.push({ hexCoordinates: coords, position: 'Left', default: {} as BuildingKey });
      buildingKeys.push({ hexCoordinates: coords, position: 'TopLeft', default: {} as BuildingKey });
      break;
    case 'None':
    default:
      break;
  }

  const result: BuildingModel[] = [];
  for (const key of buildingKeys) {
    const building = findBuilding(gameModel.buildings, key);
    if (building) {
      result.push(building);
    }
  }

  return result;
}

/**
 * Returns the tiles that a building connects to (up to 3 tiles sharing that vertex).
 */
export function tilesForBuilding(
  gameModel: GameModel,
  buildingKey: BuildingKey
): TileModel[] {
  if (!gameModel.tiles || gameModel.tiles.length === 0) {
    return [];
  }

  const tiles: TileModel[] = [];

  // Get the primary tile
  const primaryTile = tileFromCoords(gameModel.tiles, buildingKey.hexCoordinates);
  if (primaryTile) {
    tiles.push(primaryTile);
  }

  // Get the alias tiles (other hexes sharing this vertex)
  const aliases = buildingKeyAliases(buildingKey);
  for (const alias of aliases) {
    const neighborCoords = getAdjacentHex(buildingKey.hexCoordinates, alias.direction);
    const neighborTile = tileFromCoords(gameModel.tiles, neighborCoords);
    if (neighborTile) {
      tiles.push(neighborTile);
    }
  }

  return tiles;
}

/**
 * Gets the building between two roads (the shared vertex).
 * Returns undefined if no shared building exists.
 */
export function buildingBetweenRoads(
  gameModel: GameModel,
  road1Key: RoadKey,
  road2Key: RoadKey
): BuildingModel | undefined {
  const buildings1 = adjacentBuildingsForRoad(gameModel, road1Key);
  const buildings2 = adjacentBuildingsForRoad(gameModel, road2Key);

  // Find the intersection
  for (const b1 of buildings1) {
    for (const b2 of buildings2) {
      if (
        b1.buildingKey.hexCoordinates.q === b2.buildingKey.hexCoordinates.q &&
        b1.buildingKey.hexCoordinates.r === b2.buildingKey.hexCoordinates.r &&
        b1.buildingKey.position === b2.buildingKey.position
      ) {
        return b1;
      }
    }
  }

  return undefined;
}

/**
 * Gets the player index (0-based) in the player list.
 * Returns -1 if not found.
 */
export function playerIndex(gameModel: GameModel, playerId: string): number {
  if (!gameModel.players || !playerId) {
    return -1;
  }
  return gameModel.players.findIndex((p) => p.id === playerId);
}

/**
 * Gets the current player index (0-based).
 * Returns -1 if no current player.
 */
export function currentPlayerIndex(gameModel: GameModel): number {
  if (!gameModel.currentPlayerId) {
    return -1;
  }
  return playerIndex(gameModel, gameModel.currentPlayerId);
}

/**
 * Checks if a specific player is the current player.
 */
export function isCurrentPlayer(gameModel: GameModel, playerId: string): boolean {
  return gameModel.currentPlayerId === playerId;
}

/**
 * Gets all buildable buildings (PossibleSettlement state).
 */
export function buildableBuildings(gameModel: GameModel): BuildingModel[] {
  if (!gameModel.buildings) {
    return [];
  }
  return gameModel.buildings.filter((b) => b.buildingState === 'PossibleSettlement');
}

/**
 * Gets all buildable roads (Buildable state).
 */
export function buildableRoadsFromModel(gameModel: GameModel): RoadModel[] {
  if (!gameModel.roads) {
    return [];
  }
  return gameModel.roads.filter((r) => r.roadState === 'Buildable');
}

/**
 * Gets all buildings owned by a specific player.
 */
export function buildingsOwnedByPlayer(
  gameModel: GameModel,
  playerId: string
): BuildingModel[] {
  if (!gameModel.buildings || !playerId) {
    return [];
  }
  return gameModel.buildings.filter(
    (b) =>
      b.ownerId === playerId &&
      (b.buildingState === 'Settlement' || b.buildingState === 'City')
  );
}

/**
 * Gets all roads owned by a specific player.
 */
export function roadsOwnedByPlayerFromModel(
  gameModel: GameModel,
  playerId: string
): RoadModel[] {
  if (!gameModel.roads || !playerId) {
    return [];
  }
  return gameModel.roads.filter(
    (r) =>
      r.ownerId === playerId &&
      (r.roadState === 'Road' || r.roadState === 'Ship')
  );
}

/**
 * Calculates the total stars for a building position.
 * Stars are the sum of pip values from adjacent tiles.
 * This is a UI convenience function (not ported from C#).
 *
 * @param gameModel - The game model containing tiles
 * @param buildingKey - The building position to calculate stars for
 * @returns Total star count for the position
 *
 * @example
 * const stars = starsForBuilding(gameModel, building.buildingKey);
 * // Returns 14 if building touches tiles with numbers 6, 8, 9 (5+5+4)
 */
export function starsForBuilding(
  gameModel: GameModel,
  buildingKey: BuildingKey
): number {
  const tiles = tilesForBuilding(gameModel, buildingKey);
  return totalStars(tiles);
}

/**
 * Maps each vertex position to the two adjacent hex sides.
 * Used to find harbor locations for a building position.
 */
const VERTEX_TO_SIDES: Record<HexPosition, HexSide[]> = {
  TopLeft: ['Top', 'TopLeft'],
  TopRight: ['Top', 'TopRight'],
  Right: ['TopRight', 'BottomRight'],
  BottomRight: ['BottomRight', 'Bottom'],
  BottomLeft: ['Bottom', 'BottomLeft'],
  Left: ['BottomLeft', 'TopLeft'],
  None: [],
};

/**
 * Gets all possible harbor locations adjacent to a building position.
 * Includes the original position and all alias positions.
 *
 * @param buildingKey - The building position to check
 * @returns Array of (hex, side) pairs where harbors could be adjacent
 */
function getAdjacentHarborLocations(
  buildingKey: BuildingKey
): Array<{ hex: HexCoordinates; side: HexSide }> {
  const locations: Array<{ hex: HexCoordinates; side: HexSide }> = [];

  // Helper to add all harbor locations for a vertex
  function addLocationsForVertex(hex: HexCoordinates, position: HexPosition): void {
    const sides = VERTEX_TO_SIDES[position];
    if (sides) {
      for (const side of sides) {
        locations.push({ hex, side });
      }
    }
  }

  // Include the original position
  addLocationsForVertex(buildingKey.hexCoordinates, buildingKey.position);

  // Include all alias positions
  const aliases = buildingKeyAliases(buildingKey);
  for (const alias of aliases) {
    const aliasHex = getAdjacentHex(buildingKey.hexCoordinates, alias.direction);
    addLocationsForVertex(aliasHex, alias.position);
  }

  return locations;
}

/**
 * Finds a harbor adjacent to a building position.
 * Checks all possible harbor locations including alias positions.
 *
 * @param gameModel - The game model containing harbors
 * @param buildingKey - The building position to check
 * @returns The adjacent harbor, or undefined if none exists
 *
 * @example
 * const harbor = findAdjacentHarbor(gameModel, building.buildingKey);
 * if (harbor) {
 *   // Building has harbor access - check harbor.harborKey.harborType for trade ratio
 * }
 */
export function findAdjacentHarbor(
  gameModel: GameModel,
  buildingKey: BuildingKey
): HarborModel | undefined {
  if (!gameModel.harbors || gameModel.harbors.length === 0) {
    return undefined;
  }

  const locations = getAdjacentHarborLocations(buildingKey);

  for (const { hex, side } of locations) {
    const harbor = gameModel.harbors.find(
      (h) =>
        hexCoordsEqual(h.harborKey.hexCoordinates, hex) && h.harborKey.side === side
    );
    if (harbor) {
      return harbor;
    }
  }

  return undefined;
}

/**
 * Result type for ownedAdjacentRoadsNotCounted.
 * Replaces C# `out bool adjacentFork` parameter.
 */
export interface AdjacentRoadsResult {
  /** Adjacent roads owned by the same player that aren't in the owned list */
  roads: RoadModel[];
  /** Whether the blockedFork road was found adjacent */
  adjacentFork: boolean;
}

/**
 * Finds adjacent roads owned by the same player that haven't been counted yet.
 * Used for longest road calculation.
 *
 * - Filters to roads owned by the same player as the input road
 * - Excludes roads already in the `owned` list
 * - Excludes roads where an opponent's building blocks the connection
 * - Returns whether the `blockedFork` was found adjacent
 *
 * @param gameModel - The game model
 * @param road - The road to find adjacent roads for
 * @param owned - Roads already counted (to avoid duplicates)
 * @param blockedFork - Optional road to check for fork detection
 * @returns Object with roads array and adjacentFork flag
 */
export function ownedAdjacentRoadsNotCounted(
  gameModel: GameModel,
  road: RoadModel,
  owned: RoadModel[],
  blockedFork: RoadModel | null
): AdjacentRoadsResult {
  const list: RoadModel[] = [];

  if (!gameModel.roads || !road.ownerId) {
    return { roads: list, adjacentFork: false };
  }

  // Get adjacent roads owned by the same player
  const allAdjacentRoads = adjacentRoads(gameModel.roads, road.roadKey);
  const ownedAdjacentRoads = allAdjacentRoads.filter(
    (r) => r.ownerId === road.ownerId
  );

  for (const r of ownedAdjacentRoads) {
    // Check if there's a building between the roads
    const buildingBetween = buildingBetweenRoads(gameModel, road.roadKey, r.roadKey);

    // If there's a building owned by someone else, skip this road (road is blocked)
    if (
      buildingBetween &&
      buildingBetween.ownerId !== null &&
      buildingBetween.ownerId !== r.ownerId
    ) {
      continue;
    }

    // Don't add roads already in the owned list
    if (!owned.includes(r)) {
      list.push(r);
    }
  }

  // Check if blockedFork is adjacent
  let adjacentFork = false;
  if (blockedFork !== null) {
    const forkIndex = list.indexOf(blockedFork);
    if (forkIndex !== -1) {
      list.splice(forkIndex, 1);
      adjacentFork = true;
    }
  }

  return { roads: list, adjacentFork };
}

/**
 * Finds the purchase model for a specific entitlement.
 * Returns the EntitlementPurchaseModel from the game's entitlement list.
 *
 * @param gameModel - The game model containing entitlement purchase models
 * @param entitlement - The entitlement type to look up
 * @returns The EntitlementPurchaseModel, or undefined if not found
 *
 * @example
 * const roadPurchase = purchaseModel(gameModel, 'Road');
 * if (roadPurchase?.enabled) {
 *   // Player can purchase a road
 * }
 */
export function purchaseModel(
  gameModel: GameModel,
  entitlement: Entitlement
): EntitlementPurchaseModel | undefined {
  if (!gameModel.entitlementPurchaseModel) {
    return undefined;
  }
  return gameModel.entitlementPurchaseModel.find(
    (m) => m.entitlement === entitlement
  );
}

/**
 * Calculates the resources a building would produce for a given roll.
 * For each adjacent tile, adds resources based on building state:
 * - City: 2 resources
 * - Settlement: 1 resource
 *
 * @param gameModel - The game model
 * @param building - The building to calculate resources for
 * @returns ResourcesModel with the resources the building produces
 *
 * @example
 * const resources = resourcesForBuilding(gameModel, building);
 * // resources.wheat will be 2 if building is a city touching wheat
 */
export function resourcesForBuilding(
  gameModel: GameModel,
  building: BuildingModel
): ResourcesModel {
  const resources = createEmptyResourcesModel();

  // Determine count based on building state (City = 2, Settlement = 1)
  const count = building.buildingState === 'City' ? 2 : 1;

  // Get tiles adjacent to this building
  const tiles = tilesForBuilding(gameModel, building.buildingKey);

  // Add resources from each tile
  for (const tile of tiles) {
    addResource(resources, tile.resourceTileType, count);
  }

  return resources;
}

// ============================================================================
// Roll Statistics
// ============================================================================

/**
 * Roll statistics for a single dice sum (2-12).
 */
export interface RollStats {
  /** Number of times this sum was rolled */
  count: number;
  /** Percentage of total rolls (0-100) */
  percentage: number;
}

/**
 * Calculates roll statistics from the game's roll model.
 * Returns a record mapping each dice sum (2-12) to its count and percentage.
 *
 * This is a UI convenience function for displaying roll history in the RollRing component.
 *
 * @param gameModel - The game model containing rollModel
 * @returns Record mapping dice sums (2-12) to RollStats
 *
 * @example
 * const stats = calculateRollStats(gameModel);
 * // stats[7] = { count: 5, percentage: 25 } // 7 was rolled 5 times (25% of rolls)
 */
export function calculateRollStats(gameModel: GameModel): Record<number, RollStats> {
  const stats: Record<number, RollStats> = {};
  const rollCounts = gameModel?.rollModel?.gameRollModel?.rollCounts ?? [];
  const totalRolls = gameModel?.rollModel?.gameRollModel?.totalRolls ?? 0;

  for (let roll = 2; roll <= 12; roll++) {
    // RollCounts array is 0-indexed for rolls 2-12 (index 0 = roll 2, index 1 = roll 3, etc.)
    // Matches C# GameRollModel logic: Indexes 0 to 10 correspond to rolls 2 to 12.
    const index = roll - 2;
    const count = rollCounts[index] ?? 0;
    const percentage = totalRolls > 0 ? Math.round((count / totalRolls) * 100) : 0;
    stats[roll] = { count, percentage };
  }

  return stats;
}

