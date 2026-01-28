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
import type { GameState } from '@/types/generated/models/game-state';
import { playerFromId } from './playerExtensions';
import { tileFromCoords } from './tileExtensions';
import { buildingKeyAliases, getAdjacentHex, findBuilding } from './buildingExtensions';
import { findRoad } from './roadExtensions';

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
