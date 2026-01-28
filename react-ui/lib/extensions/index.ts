/**
 * Extensions Library
 *
 * TypeScript equivalents of C# extension methods from Catan3.Shared/Extensions/.
 * These provide consistent, reusable utility functions for working with game models.
 *
 * @module extensions
 */

// Player extensions
export { playerFromId } from './playerExtensions';

// Tile extensions
export {
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
} from './tileExtensions';

// Building extensions
export {
  type Direction,
  type BuildingAlias,
  DIRECTION_OFFSETS,
  HEX_POSITIONS,
  buildingKeyAliases,
  getAdjacentHex,
  buildingKeysEqual,
  findBuilding,
  adjacentBuildings,
  buildingsInTile,
  ownedBuildings,
  buildingResourceCount,
  buildingResources,
} from './buildingExtensions';

// Road extensions
export {
  type RoadAlias,
  HEX_SIDES,
  roadKeyAlias,
  roadKeysEqual,
  findRoad,
  adjacentRoadKeys,
  adjacentRoads,
  roadsInTile,
  ownedRoads,
  roadsOwnedByPlayer,
  buildableRoads,
} from './roadExtensions';

// GameModel extensions
export {
  type GamePhase,
  type AdjacentRoadsResult,
  currentPlayer,
  isAllocationPhase,
  gamePhase,
  nextPlayerId,
  adjacentRoadsForBuilding,
  adjacentBuildingsForRoad,
  tilesForBuilding,
  buildingBetweenRoads,
  playerIndex,
  currentPlayerIndex,
  isCurrentPlayer,
  buildableBuildings,
  buildableRoadsFromModel,
  buildingsOwnedByPlayer,
  roadsOwnedByPlayerFromModel,
  starsForBuilding,
  findAdjacentHarbor,
  ownedAdjacentRoadsNotCounted,
  purchaseModel,
  resourcesForBuilding,
} from './gameModelExtensions';

// Resources extensions
export {
  createEmptyResourcesModel,
  addResource,
} from './resourcesExtensions';
