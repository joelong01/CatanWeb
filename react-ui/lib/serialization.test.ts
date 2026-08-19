/**
 * Tests that verify TypeGen-generated TypeScript models can deserialize
 * JSON from C# (System.Text.Json) and maintain type safety.
 *
 * Test data comes from actual .catan_test files used in C# integration tests.
 */

import { describe, it, expect } from 'vitest';
import { readFileSync } from 'fs';
import { join } from 'path';
import type { GameModel } from '@/types/generated/models/game-model';
import { GameState } from '@/types/generated/models/game-state';
import { GameType } from '@/types/generated/models/game-type';
import { ResourceType } from '@/types/generated/models/resource-type';
import { BuildingState } from '@/types/generated/models/building-state';
import { RoadState } from '@/types/generated/models/road-state';

/**
 * Load the .catan_test file that contains a real GameModel from C#.
 */
function loadTestGameModel(): GameModel {
  const testFilePath = join(
    __dirname,
    '..',
    '..',
    'Tests',
    'Desktop',
    'ScriptedTestData',
    'Regular.catan_test'
  );
  const fileContent = readFileSync(testFilePath, 'utf-8');
  const testData = JSON.parse(fileContent);
  return testData.gameModel as GameModel;
}

describe('TypeGen Generated Model Serialization', () => {
  describe('GameModel', () => {
    it('should deserialize a complete GameModel from C# JSON', () => {
      const gameModel = loadTestGameModel();

      // Verify the model loaded
      expect(gameModel).toBeDefined();
      expect(gameModel.gameId).toBeDefined();
      expect(typeof gameModel.gameId).toBe('string');
    });

    it('should have correct primitive properties', () => {
      const gameModel = loadTestGameModel();

      expect(gameModel.gameHash).toBe('669B687D');
      expect(gameModel.gameName).toBe('New Regular Game');
      expect(gameModel.saveLifetimeStats).toBeUndefined(); // not in test file
      expect(gameModel.hasSupplementalBuildPhase).toBe(false);
      expect(gameModel.gameStateMachineVersion).toBe(1);
    });

    it('should deserialize enum values as strings', () => {
      const gameModel = loadTestGameModel();

      // GameState enum
      expect(gameModel.gameState).toBe(GameState.PickingBoard);
      expect(gameModel.gameState).toBe('PickingBoard');

      // GameType enum
      expect(gameModel.gameType).toBe(GameType.Regular);
      expect(gameModel.gameType).toBe('Regular');

      // PreviousGameState
      expect(gameModel.previousGameState).toBe(GameState.Uninitialized);
    });

    it('should deserialize nested objects correctly', () => {
      const gameModel = loadTestGameModel();

      // ActionFlags
      expect(gameModel.actionFlags).toBeDefined();
      expect(gameModel.actionFlags.undoEnabled).toBe(false);
      expect(gameModel.actionFlags.redoEnabled).toBe(false);
      expect(gameModel.actionFlags.nextEnabled).toBe(true);
      expect(gameModel.actionFlags.rollsEnabled).toBe(false);

      // HouseRules
      expect(gameModel.houseRules).toBeDefined();
      expect(gameModel.houseRules.goldTiles).toBe(1);
      expect(gameModel.houseRules.wallsProtectCities).toBe(true);

      // ResourceRules
      expect(gameModel.resourceRules).toBeDefined();
      expect(gameModel.resourceRules.maxCities).toBe(4);
      expect(gameModel.resourceRules.maxSettlements).toBe(5);
      expect(gameModel.resourceRules.maxRoads).toBe(15);
    });

    it('should deserialize the robber correctly', () => {
      const gameModel = loadTestGameModel();

      expect(gameModel.robber).toBeDefined();
      expect(gameModel.robber.coordinates).toBeDefined();
      // Robber starts off-board at (-99, 99, 0)
      expect(gameModel.robber.coordinates.q).toBe(-99);
      expect(gameModel.robber.coordinates.r).toBe(99);
      expect(gameModel.robber.coordinates.s).toBe(0);
      expect(gameModel.robber.movedBy).toBeNull();
      expect(gameModel.robber.targeted).toBeNull();
    });
  });

  describe('PlayerModel array', () => {
    it('should deserialize all players', () => {
      const gameModel = loadTestGameModel();

      expect(gameModel.players).toBeDefined();
      expect(Array.isArray(gameModel.players)).toBe(true);
      expect(gameModel.players.length).toBe(3);
    });

    it('should have correct player properties', () => {
      const gameModel = loadTestGameModel();
      const player = gameModel.players[0];

      expect(player.id).toBe('Joe-001');
      expect(player.score).toBe(0);
      expect(player.goldRolls).toBe(0);
      expect(player.hasLongestRoad).toBe(false);
      expect(player.largestArmy).toBe(false);
      expect(player.participatingInSupplemental).toBe(false);
    });

    it('should deserialize player resources', () => {
      const gameModel = loadTestGameModel();
      const player = gameModel.players[0];

      expect(player.resourcesThisTurn).toBeDefined();
      expect(player.resourcesThisTurn.brick).toBe(0);
      expect(player.resourcesThisTurn.ore).toBe(0);
      expect(player.resourcesThisTurn.sheep).toBe(0);
      expect(player.resourcesThisTurn.wheat).toBe(0);
      expect(player.resourcesThisTurn.wood).toBe(0);

      expect(player.resourcesThisGame).toBeDefined();
    });

    it('should deserialize player arrays', () => {
      const gameModel = loadTestGameModel();
      const player = gameModel.players[0];

      expect(Array.isArray(player.unspentEntitlements)).toBe(true);
      expect(Array.isArray(player.spentEntitlementsThisGame)).toBe(true);
      expect(Array.isArray(player.spentEntitlementsThisTurn)).toBe(true);
      expect(Array.isArray(player.ownedHarbors)).toBe(true);
    });
  });

  describe('TileModel array', () => {
    it('should deserialize all tiles', () => {
      const gameModel = loadTestGameModel();

      expect(gameModel.tiles).toBeDefined();
      expect(Array.isArray(gameModel.tiles)).toBe(true);
      // Regular Catan has 19 tiles
      expect(gameModel.tiles.length).toBe(19);
    });

    it('should have correct tile properties', () => {
      const gameModel = loadTestGameModel();
      const firstTile = gameModel.tiles[0];

      expect(firstTile.tileKey).toBeDefined();
      expect(firstTile.tileKey.q).toBe(-2);
      expect(firstTile.tileKey.r).toBe(0);
      expect(firstTile.tileKey.s).toBe(2);
      expect(firstTile.number).toBe(12);
      expect(firstTile.resourceTileType).toBe(ResourceType.Sheep);
      expect(firstTile.highlighted).toBe(false);
      expect(firstTile.temporarilyGold).toBe(false);
    });

    it('should have desert tile with number 7', () => {
      const gameModel = loadTestGameModel();
      const desertTile = gameModel.tiles.find((t) => t.resourceTileType === ResourceType.Desert);

      expect(desertTile).toBeDefined();
      expect(desertTile!.number).toBe(7);
    });
  });

  describe('BuildingModel array', () => {
    it('should deserialize buildings', () => {
      const gameModel = loadTestGameModel();

      expect(gameModel.buildings).toBeDefined();
      expect(Array.isArray(gameModel.buildings)).toBe(true);
      // Regular Catan has 54 building positions (6 per tile * 19, minus overlaps)
      expect(gameModel.buildings.length).toBeGreaterThan(0);
    });

    it('should have correct building properties', () => {
      const gameModel = loadTestGameModel();
      const building = gameModel.buildings[0];

      expect(building.buildingKey).toBeDefined();
      expect(building.buildingKey.hexCoordinates).toBeDefined();
      expect(building.buildingKey.position).toBeDefined();
      expect(building.buildingState).toBe(BuildingState.PossibleSettlement);
      expect(building.wall).toBe(false);
      expect(building.metropolis).toBe(false);
      expect(building.ownerId).toBeNull();
    });
  });

  describe('RoadModel array', () => {
    it('should deserialize roads', () => {
      const gameModel = loadTestGameModel();

      expect(gameModel.roads).toBeDefined();
      expect(Array.isArray(gameModel.roads)).toBe(true);
      expect(gameModel.roads.length).toBeGreaterThan(0);
    });

    it('should have correct road properties', () => {
      const gameModel = loadTestGameModel();
      const road = gameModel.roads[0];

      expect(road.roadKey).toBeDefined();
      expect(road.roadKey.tileKey).toBeDefined();
      expect(road.roadKey.hexSide).toBeDefined();
      expect(road.roadState).toBe(RoadState.Unowned);
      expect(road.ownerId).toBeNull();
      expect(road.buildIndex).toBe(0);
    });
  });

  describe('HarborModel array', () => {
    it('should deserialize harbors', () => {
      const gameModel = loadTestGameModel();

      expect(gameModel.harbors).toBeDefined();
      expect(Array.isArray(gameModel.harbors)).toBe(true);
      // Regular Catan has 9 harbors
      expect(gameModel.harbors.length).toBe(9);
    });

    it('should have correct harbor properties', () => {
      const gameModel = loadTestGameModel();
      const harbor = gameModel.harbors[0];

      expect(harbor.harborKey).toBeDefined();
      expect(harbor.harborKey.hexCoordinates).toBeDefined();
      expect(harbor.harborKey.harborType).toBeDefined();
      expect(harbor.harborKey.side).toBeDefined();
      expect(harbor.owner).toBeNull();
    });
  });

  describe('Round-trip serialization', () => {
    it('should serialize back to JSON without losing data', () => {
      const gameModel = loadTestGameModel();

      // Serialize to JSON string
      const jsonString = JSON.stringify(gameModel);

      // Parse back
      const reparsed = JSON.parse(jsonString) as GameModel;

      // Verify key properties survived round-trip
      expect(reparsed.gameHash).toBe(gameModel.gameHash);
      expect(reparsed.gameState).toBe(gameModel.gameState);
      expect(reparsed.players.length).toBe(gameModel.players.length);
      expect(reparsed.tiles.length).toBe(gameModel.tiles.length);
      expect(reparsed.buildings.length).toBe(gameModel.buildings.length);
      expect(reparsed.roads.length).toBe(gameModel.roads.length);
    });
  });
});
