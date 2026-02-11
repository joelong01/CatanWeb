/**
 * Tests for gameModelExtensions
 */

import { describe, it, expect } from 'vitest';
import {
  currentPlayer,
  isAllocationPhase,
  gamePhase,
  nextPlayerId,
  adjacentRoadsForBuilding,
  adjacentBuildingsForRoad,
  tilesForBuilding,
  playerIndex,
  currentPlayerIndex,
  isCurrentPlayer,
  buildableBuildings,
  buildableRoadsFromModel,
  buildingsOwnedByPlayer,
  roadsOwnedByPlayerFromModel,
  purchaseModel,
  resourcesForBuilding,
} from '../gameModelExtensions';
import type { EntitlementPurchaseModel } from '@/types/generated/models/entitlement-purchase-model';
import type { GameModel } from '@/types/generated/models/game-model';
import type { PlayerModel } from '@/types/generated/models/player-model';
import type { TileModel } from '@/types/generated/models/tile-model';
import type { BuildingModel } from '@/types/generated/models/building-model';
import type { RoadModel } from '@/types/generated/models/road-model';
import type { BuildingKey } from '@/types/generated/models/building-key';
import type { RoadKey } from '@/types/generated/models/road-key';
import type { GameState } from '@/types/generated/models/game-state';

// Helper to create a minimal player
function createMockPlayer(id: string, name: string): PlayerModel {
  return {
    id,
    name,
    goldRolls: 0,
    goodRolls: 0,
    badRolls: 0,
    hasLongestRoad: false,
    islandsPlayed: 0,
    largestArmy: false,
    longestRoad: 0,
    maxNoResourceRolls: 0,
    noResourceCount: 0,
    stars: 0,
    score: 0,
    highestScore: false,
    timesTargeted: 0,
    participatingInSupplemental: false,
    finishedSupplemental: false,
    resourcesThisTurn: {
      wheat: 0,
      wood: 0,
      ore: 0,
      sheep: 0,
      brick: 0,
      goldMine: 0,
      coin: 0,
      paper: 0,
      cloth: 0,
      politics: 0,
      trade: 0,
      science: 0,
      victoryPoint: 0,
      anyDevCard: 0,
      robber: 0,
    },
    resourcesThisGame: {
      wheat: 0,
      wood: 0,
      ore: 0,
      sheep: 0,
      brick: 0,
      goldMine: 0,
      coin: 0,
      paper: 0,
      cloth: 0,
      politics: 0,
      trade: 0,
      science: 0,
      victoryPoint: 0,
      anyDevCard: 0,
      robber: 0,
    },
    unspentEntitlements: [],
    spentEntitlementsThisGame: [],
    spentEntitlementsThisTurn: [],
    ownedHarbors: [],
    victoryPointCards: 0,
  };
}

// Helper to create a minimal tile
function createMockTile(
  q: number,
  r: number,
  resourceOrNumber: TileModel['resourceTileType'] | number = 6,
  tileNumber?: number
): TileModel {
  // Support both old signature (q, r, number) and new signature (q, r, resource, number)
  let resource: TileModel['resourceTileType'] = 'Ore';
  let number: number = 6;

  if (typeof resourceOrNumber === 'string') {
    resource = resourceOrNumber;
    number = tileNumber ?? 6;
  } else {
    number = resourceOrNumber;
  }

  return {
    tileKey: { q, r, s: -q - r },
    number,
    resourceTileType: resource,
    highlighted: false,
    temporarilyGold: false,
  };
}

// Helper to create a building
function createMockBuilding(
  q: number,
  r: number,
  position: BuildingKey['position'],
  ownerId: string | null = null,
  buildingState: BuildingModel['buildingState'] = 'NotBuildable'
): BuildingModel {
  return {
    buildingKey: {
      hexCoordinates: { q, r, s: -q - r },
      position,
    },
    buildingState,
    wall: false,
    metropolis: false,
    ownerId: ownerId ?? '',
    hasRobber: false,
  };
}

// Helper to create a road
function createMockRoad(
  q: number,
  r: number,
  hexSide: RoadKey['hexSide'],
  ownerId: string | null = null,
  roadState: RoadModel['roadState'] = 'Unowned'
): RoadModel {
  return {
    roadKey: {
      tileKey: { q, r, s: -q - r },
      hexSide,
    },
    roadState,
    ownerId: ownerId ?? '',
    buildIndex: 0,
  };
}

// Helper to create a minimal game model
function createMockGameModel(overrides: Partial<GameModel> = {}): GameModel {
  return {
    gameId: 'test-game',
    gameName: 'Test Game',
    gameType: 'Regular',
    gameState: 'WaitingForRoll',
    currentPlayerId: 'player-1',
    players: [
      createMockPlayer('player-1', 'Alice'),
      createMockPlayer('player-2', 'Bob'),
      createMockPlayer('player-3', 'Charlie'),
    ],
    tiles: [
      createMockTile(0, 0),
      createMockTile(0, -1),
      createMockTile(1, -1),
      createMockTile(1, 0),
      createMockTile(0, 1),
      createMockTile(-1, 1),
      createMockTile(-1, 0),
    ],
    buildings: [],
    roads: [],
    harbors: [],
    ...overrides,
  } as GameModel;
}

describe('gameModelExtensions', () => {
  describe('currentPlayer', () => {
    it('returns the current player', () => {
      const game = createMockGameModel();
      const player = currentPlayer(game);
      expect(player).toBeDefined();
      expect(player?.id).toBe('player-1');
      expect(player?.name).toBe('Alice');
    });

    it('returns undefined if currentPlayerId is not set', () => {
      const game = createMockGameModel({ currentPlayerId: '' });
      expect(currentPlayer(game)).toBeUndefined();
    });

    it('returns undefined if player not found', () => {
      const game = createMockGameModel({ currentPlayerId: 'nonexistent' });
      expect(currentPlayer(game)).toBeUndefined();
    });
  });

  describe('isAllocationPhase', () => {
    const allocationStates: GameState[] = [
      'AllocateResourceForward',
      'AllocateResourceReverse',
      'WaitingForRollForOrder',
      'FinishedRollOrder',
      'BeginResourceAllocation',
      'PickingBoard',
    ];

    const nonAllocationStates: GameState[] = [
      'WaitingForRoll',
      'WaitingForNext',
      'MustMoveRobber',
      'TooManyCards',
      'Supplemental',
      'GameOver',
    ];

    it.each(allocationStates)('returns true for %s', (state) => {
      const game = createMockGameModel({ gameState: state });
      expect(isAllocationPhase(game)).toBe(true);
    });

    it.each(nonAllocationStates)('returns false for %s', (state) => {
      const game = createMockGameModel({ gameState: state });
      expect(isAllocationPhase(game)).toBe(false);
    });
  });

  describe('gamePhase', () => {
    it('returns Starting for WaitingForPlayers', () => {
      const game = createMockGameModel({ gameState: 'WaitingForPlayers' });
      expect(gamePhase(game)).toBe('Starting');
    });

    it('returns PickingBoard for PickingBoard', () => {
      const game = createMockGameModel({ gameState: 'PickingBoard' });
      expect(gamePhase(game)).toBe('PickingBoard');
    });

    it('returns PickingResources for AllocateResourceForward', () => {
      const game = createMockGameModel({ gameState: 'AllocateResourceForward' });
      expect(gamePhase(game)).toBe('PickingResources');
    });

    it('returns Rolling for WaitingForRoll', () => {
      const game = createMockGameModel({ gameState: 'WaitingForRoll' });
      expect(gamePhase(game)).toBe('Rolling');
    });

    it('returns Purchase for WaitingForNext', () => {
      const game = createMockGameModel({ gameState: 'WaitingForNext' });
      expect(gamePhase(game)).toBe('Purchase');
    });

    it('returns ActionRequired for MustMoveRobber', () => {
      const game = createMockGameModel({ gameState: 'MustMoveRobber' });
      expect(gamePhase(game)).toBe('ActionRequired');
    });
  });

  describe('nextPlayerId', () => {
    it('returns next player (position +1)', () => {
      const game = createMockGameModel();
      expect(nextPlayerId(game, 'player-1', 1)).toBe('player-2');
      expect(nextPlayerId(game, 'player-2', 1)).toBe('player-3');
    });

    it('wraps around to first player', () => {
      const game = createMockGameModel();
      expect(nextPlayerId(game, 'player-3', 1)).toBe('player-1');
    });

    it('handles negative positions', () => {
      const game = createMockGameModel();
      expect(nextPlayerId(game, 'player-1', -1)).toBe('player-3');
    });

    it('handles multiple positions', () => {
      const game = createMockGameModel();
      expect(nextPlayerId(game, 'player-1', 2)).toBe('player-3');
    });

    it('returns undefined for invalid startPlayerId', () => {
      const game = createMockGameModel();
      expect(nextPlayerId(game, 'invalid', 1)).toBeUndefined();
    });

    it('returns undefined for empty players', () => {
      const game = createMockGameModel({ players: [] });
      expect(nextPlayerId(game, 'player-1', 1)).toBeUndefined();
    });
  });

  describe('adjacentRoadsForBuilding', () => {
    it('returns empty array for empty roads', () => {
      const game = createMockGameModel({ roads: [] });
      const key: BuildingKey = {
        hexCoordinates: { q: 0, r: 0, s: 0 },
        position: 'TopRight',
      };
      expect(adjacentRoadsForBuilding(game, key)).toEqual([]);
    });

    it('finds 3 adjacent roads for TopRight', () => {
      const roads = [
        createMockRoad(0, 0, 'TopRight', 'player-1', 'Road'),
        createMockRoad(0, 0, 'Top', 'player-1', 'Road'),
        createMockRoad(0, -1, 'BottomRight', 'player-1', 'Road'), // North neighbor
      ];
      const game = createMockGameModel({ roads });
      const key: BuildingKey = {
        hexCoordinates: { q: 0, r: 0, s: 0 },
        position: 'TopRight',
      };
      const result = adjacentRoadsForBuilding(game, key);
      expect(result).toHaveLength(3);
    });

    it('returns only existing roads', () => {
      const roads = [createMockRoad(0, 0, 'TopRight', 'player-1', 'Road')];
      const game = createMockGameModel({ roads });
      const key: BuildingKey = {
        hexCoordinates: { q: 0, r: 0, s: 0 },
        position: 'TopRight',
      };
      const result = adjacentRoadsForBuilding(game, key);
      expect(result).toHaveLength(1);
    });
  });

  describe('adjacentBuildingsForRoad', () => {
    it('returns empty array for empty buildings', () => {
      const game = createMockGameModel({ buildings: [] });
      const key: RoadKey = { tileKey: { q: 0, r: 0, s: 0 }, hexSide: 'Top' };
      expect(adjacentBuildingsForRoad(game, key)).toEqual([]);
    });

    it('finds 2 adjacent buildings for Top road', () => {
      const buildings = [
        createMockBuilding(0, 0, 'TopLeft', 'player-1', 'Settlement'),
        createMockBuilding(0, 0, 'TopRight', 'player-2', 'Settlement'),
      ];
      const game = createMockGameModel({ buildings });
      const key: RoadKey = { tileKey: { q: 0, r: 0, s: 0 }, hexSide: 'Top' };
      const result = adjacentBuildingsForRoad(game, key);
      expect(result).toHaveLength(2);
    });
  });

  describe('tilesForBuilding', () => {
    it('returns empty array for empty tiles', () => {
      const game = createMockGameModel({ tiles: [] });
      const key: BuildingKey = {
        hexCoordinates: { q: 0, r: 0, s: 0 },
        position: 'TopRight',
      };
      expect(tilesForBuilding(game, key)).toEqual([]);
    });

    it('returns primary tile and alias tiles', () => {
      const game = createMockGameModel(); // Has tiles at center and neighbors
      const key: BuildingKey = {
        hexCoordinates: { q: 0, r: 0, s: 0 },
        position: 'TopRight',
      };
      const result = tilesForBuilding(game, key);
      // TopRight vertex touches current tile, North neighbor, and NorthEast neighbor
      expect(result.length).toBeGreaterThanOrEqual(1);
      expect(result.length).toBeLessThanOrEqual(3);
    });
  });

  describe('playerIndex', () => {
    it('returns correct index for player', () => {
      const game = createMockGameModel();
      expect(playerIndex(game, 'player-1')).toBe(0);
      expect(playerIndex(game, 'player-2')).toBe(1);
      expect(playerIndex(game, 'player-3')).toBe(2);
    });

    it('returns -1 for unknown player', () => {
      const game = createMockGameModel();
      expect(playerIndex(game, 'unknown')).toBe(-1);
    });

    it('returns -1 for empty playerId', () => {
      const game = createMockGameModel();
      expect(playerIndex(game, '')).toBe(-1);
    });
  });

  describe('currentPlayerIndex', () => {
    it('returns correct index for current player', () => {
      const game = createMockGameModel({ currentPlayerId: 'player-2' });
      expect(currentPlayerIndex(game)).toBe(1);
    });

    it('returns -1 if no current player', () => {
      const game = createMockGameModel({ currentPlayerId: '' });
      expect(currentPlayerIndex(game)).toBe(-1);
    });
  });

  describe('isCurrentPlayer', () => {
    it('returns true for current player', () => {
      const game = createMockGameModel();
      expect(isCurrentPlayer(game, 'player-1')).toBe(true);
    });

    it('returns false for other players', () => {
      const game = createMockGameModel();
      expect(isCurrentPlayer(game, 'player-2')).toBe(false);
    });
  });

  describe('buildableBuildings', () => {
    it('returns empty array for empty buildings', () => {
      const game = createMockGameModel({ buildings: [] });
      expect(buildableBuildings(game)).toEqual([]);
    });

    it('returns only PossibleSettlement buildings', () => {
      const buildings = [
        createMockBuilding(0, 0, 'TopRight', null, 'PossibleSettlement'),
        createMockBuilding(0, 0, 'TopLeft', 'player-1', 'Settlement'),
        createMockBuilding(0, 0, 'Right', null, 'NotBuildable'),
      ];
      const game = createMockGameModel({ buildings });
      const result = buildableBuildings(game);
      expect(result).toHaveLength(1);
      expect(result[0].buildingState).toBe('PossibleSettlement');
    });
  });

  describe('buildableRoadsFromModel', () => {
    it('returns empty array for empty roads', () => {
      const game = createMockGameModel({ roads: [] });
      expect(buildableRoadsFromModel(game)).toEqual([]);
    });

    it('returns only Buildable roads', () => {
      const roads = [
        createMockRoad(0, 0, 'Top', null, 'Buildable'),
        createMockRoad(0, 0, 'TopRight', 'player-1', 'Road'),
        createMockRoad(0, 0, 'Bottom', null, 'Unowned'),
      ];
      const game = createMockGameModel({ roads });
      const result = buildableRoadsFromModel(game);
      expect(result).toHaveLength(1);
      expect(result[0].roadState).toBe('Buildable');
    });
  });

  describe('buildingsOwnedByPlayer', () => {
    it('returns empty array for empty buildings', () => {
      const game = createMockGameModel({ buildings: [] });
      expect(buildingsOwnedByPlayer(game, 'player-1')).toEqual([]);
    });

    it('returns only buildings owned by specified player', () => {
      const buildings = [
        createMockBuilding(0, 0, 'TopRight', 'player-1', 'Settlement'),
        createMockBuilding(0, 0, 'TopLeft', 'player-1', 'City'),
        createMockBuilding(0, 0, 'Right', 'player-2', 'Settlement'),
        createMockBuilding(0, 0, 'Left', null, 'PossibleSettlement'),
      ];
      const game = createMockGameModel({ buildings });
      const result = buildingsOwnedByPlayer(game, 'player-1');
      expect(result).toHaveLength(2);
      expect(result.every((b) => b.ownerId === 'player-1')).toBe(true);
    });

    it('returns empty for empty playerId', () => {
      const buildings = [createMockBuilding(0, 0, 'TopRight', 'player-1', 'Settlement')];
      const game = createMockGameModel({ buildings });
      expect(buildingsOwnedByPlayer(game, '')).toEqual([]);
    });
  });

  describe('roadsOwnedByPlayerFromModel', () => {
    it('returns empty array for empty roads', () => {
      const game = createMockGameModel({ roads: [] });
      expect(roadsOwnedByPlayerFromModel(game, 'player-1')).toEqual([]);
    });

    it('returns only roads owned by specified player', () => {
      const roads = [
        createMockRoad(0, 0, 'Top', 'player-1', 'Road'),
        createMockRoad(0, 0, 'TopRight', 'player-1', 'Ship'),
        createMockRoad(0, 0, 'Bottom', 'player-2', 'Road'),
        createMockRoad(0, 0, 'BottomLeft', null, 'Buildable'),
      ];
      const game = createMockGameModel({ roads });
      const result = roadsOwnedByPlayerFromModel(game, 'player-1');
      expect(result).toHaveLength(2);
      expect(result.every((r) => r.ownerId === 'player-1')).toBe(true);
    });

    it('returns empty for empty playerId', () => {
      const roads = [createMockRoad(0, 0, 'Top', 'player-1', 'Road')];
      const game = createMockGameModel({ roads });
      expect(roadsOwnedByPlayerFromModel(game, '')).toEqual([]);
    });
  });

  describe('purchaseModel', () => {
    const createPurchaseModels = (): EntitlementPurchaseModel[] => [
      { entitlement: 'Settlement', enabled: true },
      { entitlement: 'City', enabled: false },
      { entitlement: 'Road', enabled: true },
      { entitlement: 'DevCard', enabled: false },
    ];

    it('returns undefined when entitlementPurchaseModel is undefined', () => {
      const game = createMockGameModel({});
      game.entitlementPurchaseModel = undefined as unknown as EntitlementPurchaseModel[];
      expect(purchaseModel(game, 'Settlement')).toBeUndefined();
    });

    it('returns undefined when entitlement not found', () => {
      const game = createMockGameModel({});
      game.entitlementPurchaseModel = createPurchaseModels();
      expect(purchaseModel(game, 'Soldier')).toBeUndefined();
    });

    it('finds Settlement purchase model', () => {
      const game = createMockGameModel({});
      game.entitlementPurchaseModel = createPurchaseModels();
      const result = purchaseModel(game, 'Settlement');
      expect(result).toBeDefined();
      expect(result?.entitlement).toBe('Settlement');
      expect(result?.enabled).toBe(true);
    });

    it('finds City purchase model', () => {
      const game = createMockGameModel({});
      game.entitlementPurchaseModel = createPurchaseModels();
      const result = purchaseModel(game, 'City');
      expect(result).toBeDefined();
      expect(result?.entitlement).toBe('City');
      expect(result?.enabled).toBe(false);
    });

    it('finds Road purchase model', () => {
      const game = createMockGameModel({});
      game.entitlementPurchaseModel = createPurchaseModels();
      const result = purchaseModel(game, 'Road');
      expect(result).toBeDefined();
      expect(result?.entitlement).toBe('Road');
      expect(result?.enabled).toBe(true);
    });
  });

  describe('resourcesForBuilding', () => {
    it('returns empty resources for building with no adjacent tiles', () => {
      const building = createMockBuilding(10, 10, 'TopRight', 'player-1', 'Settlement');
      const game = createMockGameModel({ tiles: [], buildings: [building] });
      const result = resourcesForBuilding(game, building);
      expect(result.wheat).toBe(0);
      expect(result.ore).toBe(0);
      expect(result.brick).toBe(0);
    });

    it('returns 1 resource per tile for Settlement', () => {
      const building = createMockBuilding(0, 0, 'TopRight', 'player-1', 'Settlement');
      const tiles = [createMockTile(0, 0, 'Wheat', 6)];
      const game = createMockGameModel({ tiles, buildings: [building] });
      const result = resourcesForBuilding(game, building);
      expect(result.wheat).toBe(1);
    });

    it('returns 2 resources per tile for City', () => {
      const building = createMockBuilding(0, 0, 'TopRight', 'player-1', 'City');
      const tiles = [createMockTile(0, 0, 'Ore', 8)];
      const game = createMockGameModel({ tiles, buildings: [building] });
      const result = resourcesForBuilding(game, building);
      expect(result.ore).toBe(2);
    });

    it('sums resources from multiple adjacent tiles', () => {
      const building = createMockBuilding(0, 0, 'TopRight', 'player-1', 'City');
      // TopRight vertex touches: (0,0), North (0,-1), NorthEast (1,-1)
      const tiles = [
        createMockTile(0, 0, 'Wheat', 6),
        createMockTile(0, -1, 'Ore', 8),
        createMockTile(1, -1, 'Wheat', 9),
      ];
      const game = createMockGameModel({ tiles, buildings: [building] });
      const result = resourcesForBuilding(game, building);
      // City gets 2 per tile: 2 wheat + 2 ore + 2 wheat = 4 wheat, 2 ore
      expect(result.wheat).toBe(4);
      expect(result.ore).toBe(2);
    });

    it('handles Desert tiles (no resources)', () => {
      const building = createMockBuilding(0, 0, 'TopRight', 'player-1', 'Settlement');
      const tiles = [createMockTile(0, 0, 'Desert', 7)];
      const game = createMockGameModel({ tiles, buildings: [building] });
      const result = resourcesForBuilding(game, building);
      // Desert shouldn't add any resources
      expect(result.wheat).toBe(0);
      expect(result.ore).toBe(0);
      expect(result.brick).toBe(0);
      expect(result.sheep).toBe(0);
      expect(result.wood).toBe(0);
    });
  });
});
