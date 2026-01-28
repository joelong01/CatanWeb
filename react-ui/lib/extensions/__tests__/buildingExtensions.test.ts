/**
 * Tests for buildingExtensions
 *
 * CRITICAL: These tests verify alias handling which is essential for
 * correct building lookups in the hex grid system.
 */

import { describe, it, expect } from 'vitest';
import {
  DIRECTION_OFFSETS,
  HEX_POSITIONS,
  buildingKeyAliases,
  getAdjacentHex,
  buildingKeysEqual,
  findBuilding,
  adjacentBuildings,
  buildingsInTile,
  ownedBuildings,
} from '../buildingExtensions';
import type { BuildingModel } from '@/types/generated/models/building-model';
import type { BuildingKey } from '@/types/generated/models/building-key';
import type { HexCoordinates } from '@/types/generated/models/hex-coordinates';

// Helper to create a minimal building for testing
function createMockBuilding(
  q: number,
  r: number,
  position: BuildingKey['position'],
  ownerId: string | null = null,
  buildingState: BuildingModel['buildingState'] = 'PossibleSettlement'
): BuildingModel {
  return {
    buildingKey: {
      hexCoordinates: { q, r, s: -q - r },
      position,
      default: {} as BuildingKey,
    },
    buildingState,
    wall: false,
    metropolis: false,
    ownerId: ownerId ?? '',
    hasRobber: false,
    default: {} as BuildingModel,
  };
}

// Helper to create a building key
function createKey(q: number, r: number, position: BuildingKey['position']): BuildingKey {
  return {
    hexCoordinates: { q, r, s: -q - r },
    position,
    default: {} as BuildingKey,
  };
}

describe('buildingExtensions', () => {
  describe('DIRECTION_OFFSETS', () => {
    it('has 6 directions', () => {
      expect(Object.keys(DIRECTION_OFFSETS)).toHaveLength(6);
    });

    it('all offsets sum to zero (valid hex coordinates)', () => {
      for (const [, offset] of Object.entries(DIRECTION_OFFSETS)) {
        expect(offset.q + offset.r + offset.s).toBe(0);
      }
    });

    it('North offset is correct', () => {
      expect(DIRECTION_OFFSETS.North).toEqual({ q: 0, r: -1, s: 1 });
    });
  });

  describe('HEX_POSITIONS', () => {
    it('has 6 positions', () => {
      expect(HEX_POSITIONS).toHaveLength(6);
    });

    it('excludes None', () => {
      expect(HEX_POSITIONS).not.toContain('None');
    });
  });

  describe('buildingKeyAliases', () => {
    it('returns 2 aliases for TopRight', () => {
      const aliases = buildingKeyAliases(createKey(0, 0, 'TopRight'));
      expect(aliases).toHaveLength(2);
      // TopRight at (0,0,0) = BottomRight at North neighbor, Left at NorthEast neighbor
      expect(aliases[0]).toEqual({ position: 'BottomRight', direction: 'North' });
      expect(aliases[1]).toEqual({ position: 'Left', direction: 'NorthEast' });
    });

    it('returns 2 aliases for Right', () => {
      const aliases = buildingKeyAliases(createKey(0, 0, 'Right'));
      expect(aliases).toHaveLength(2);
      expect(aliases[0]).toEqual({ position: 'BottomLeft', direction: 'NorthEast' });
      expect(aliases[1]).toEqual({ position: 'TopLeft', direction: 'SouthEast' });
    });

    it('returns 2 aliases for each position', () => {
      for (const position of HEX_POSITIONS) {
        const aliases = buildingKeyAliases(createKey(0, 0, position));
        expect(aliases).toHaveLength(2);
      }
    });

    it('returns empty array for None', () => {
      const aliases = buildingKeyAliases(createKey(0, 0, 'None'));
      expect(aliases).toHaveLength(0);
    });
  });

  describe('getAdjacentHex', () => {
    it('returns correct North neighbor', () => {
      const coords: HexCoordinates = { q: 0, r: 0, s: 0 };
      const north = getAdjacentHex(coords, 'North');
      expect(north).toEqual({ q: 0, r: -1, s: 1 });
    });

    it('returns correct SouthEast neighbor', () => {
      const coords: HexCoordinates = { q: 0, r: 0, s: 0 };
      const southeast = getAdjacentHex(coords, 'SouthEast');
      expect(southeast).toEqual({ q: 1, r: 0, s: -1 });
    });

    it('works from non-origin coordinates', () => {
      const coords: HexCoordinates = { q: 2, r: -1, s: -1 };
      const north = getAdjacentHex(coords, 'North');
      expect(north).toEqual({ q: 2, r: -2, s: 0 });
    });
  });

  describe('buildingKeysEqual', () => {
    it('returns true for equal keys', () => {
      const a = createKey(0, 0, 'TopRight');
      const b = createKey(0, 0, 'TopRight');
      expect(buildingKeysEqual(a, b)).toBe(true);
    });

    it('returns false for different positions', () => {
      const a = createKey(0, 0, 'TopRight');
      const b = createKey(0, 0, 'TopLeft');
      expect(buildingKeysEqual(a, b)).toBe(false);
    });

    it('returns false for different coordinates', () => {
      const a = createKey(0, 0, 'TopRight');
      const b = createKey(1, 0, 'TopRight');
      expect(buildingKeysEqual(a, b)).toBe(false);
    });
  });

  describe('findBuilding', () => {
    it('returns undefined for empty array', () => {
      const result = findBuilding([], createKey(0, 0, 'TopRight'));
      expect(result).toBeUndefined();
    });

    it('finds building by direct key match', () => {
      const building = createMockBuilding(0, 0, 'TopRight', 'player-1');
      const result = findBuilding([building], createKey(0, 0, 'TopRight'));
      expect(result).toBe(building);
    });

    it('returns undefined when not found', () => {
      const building = createMockBuilding(0, 0, 'TopRight');
      const result = findBuilding([building], createKey(0, 0, 'BottomRight'));
      expect(result).toBeUndefined();
    });

    // CRITICAL ALIAS TESTS
    describe('alias handling', () => {
      it('finds TopRight building via North neighbor BottomRight alias', () => {
        // Building stored at TopRight of (0,0,0)
        // Should also be found via BottomRight of (0,-1,1) (North neighbor)
        const building = createMockBuilding(0, 0, 'TopRight', 'player-1');

        // Search via the alias position
        const aliasKey = createKey(0, -1, 'BottomRight'); // North neighbor's BottomRight
        const result = findBuilding([building], aliasKey);
        expect(result).toBe(building);
      });

      it('finds TopRight building via NorthEast neighbor Left alias', () => {
        // Building stored at TopRight of (0,0,0)
        // Should also be found via Left of (1,-1,0) (NorthEast neighbor)
        const building = createMockBuilding(0, 0, 'TopRight', 'player-1');

        const aliasKey = createKey(1, -1, 'Left'); // NorthEast neighbor's Left
        const result = findBuilding([building], aliasKey);
        expect(result).toBe(building);
      });

      it('finds building stored at alias position via primary key', () => {
        // Building stored at BottomRight of (0,-1,1) (the North neighbor)
        // Should be found via TopRight of (0,0,0)
        const building = createMockBuilding(0, -1, 'BottomRight', 'player-1');

        const primaryKey = createKey(0, 0, 'TopRight');
        const result = findBuilding([building], primaryKey);
        expect(result).toBe(building);
      });

      it('finds Right building via aliases', () => {
        const building = createMockBuilding(0, 0, 'Right', 'player-1');

        // Should find via BottomLeft of NorthEast neighbor
        const alias1Key = createKey(1, -1, 'BottomLeft');
        expect(findBuilding([building], alias1Key)).toBe(building);

        // Should find via TopLeft of SouthEast neighbor
        const alias2Key = createKey(1, 0, 'TopLeft');
        expect(findBuilding([building], alias2Key)).toBe(building);
      });

      it('finds BottomLeft building via aliases', () => {
        const building = createMockBuilding(0, 0, 'BottomLeft', 'player-1');

        // Should find via Right of SouthWest neighbor
        const alias1Key = createKey(-1, 1, 'Right');
        expect(findBuilding([building], alias1Key)).toBe(building);

        // Should find via TopLeft of South neighbor
        const alias2Key = createKey(0, 1, 'TopLeft');
        expect(findBuilding([building], alias2Key)).toBe(building);
      });

      it('handles multiple buildings with overlapping alias spaces', () => {
        const building1 = createMockBuilding(0, 0, 'TopRight', 'player-1');
        const building2 = createMockBuilding(0, 0, 'TopLeft', 'player-2');
        const buildings = [building1, building2];

        // Each should be found independently
        expect(findBuilding(buildings, createKey(0, 0, 'TopRight'))).toBe(building1);
        expect(findBuilding(buildings, createKey(0, 0, 'TopLeft'))).toBe(building2);

        // And via aliases
        expect(findBuilding(buildings, createKey(0, -1, 'BottomRight'))).toBe(building1);
        expect(findBuilding(buildings, createKey(-1, 0, 'Right'))).toBe(building2);
      });
    });
  });

  describe('adjacentBuildings', () => {
    it('returns empty array for empty buildings', () => {
      const result = adjacentBuildings([], createKey(0, 0, 'TopRight'));
      expect(result).toEqual([]);
    });

    it('finds adjacent buildings for TopRight position', () => {
      // TopRight is adjacent to: TopLeft, Right, and Right of North neighbor
      const topLeft = createMockBuilding(0, 0, 'TopLeft', 'player-1');
      const right = createMockBuilding(0, 0, 'Right', 'player-2');
      const northRight = createMockBuilding(0, -1, 'Right', 'player-3');
      const buildings = [topLeft, right, northRight];

      const result = adjacentBuildings(buildings, createKey(0, 0, 'TopRight'));
      expect(result).toHaveLength(3);
      expect(result).toContain(topLeft);
      expect(result).toContain(right);
      expect(result).toContain(northRight);
    });

    it('returns only existing adjacent buildings', () => {
      // Only one adjacent building exists
      const topLeft = createMockBuilding(0, 0, 'TopLeft', 'player-1');
      const buildings = [topLeft];

      const result = adjacentBuildings(buildings, createKey(0, 0, 'TopRight'));
      expect(result).toHaveLength(1);
      expect(result[0]).toBe(topLeft);
    });

    it('does not include the building itself', () => {
      const self = createMockBuilding(0, 0, 'TopRight', 'player-1');
      const topLeft = createMockBuilding(0, 0, 'TopLeft', 'player-2');
      const buildings = [self, topLeft];

      const result = adjacentBuildings(buildings, createKey(0, 0, 'TopRight'));
      expect(result).not.toContain(self);
      expect(result).toContain(topLeft);
    });
  });

  describe('buildingsInTile', () => {
    it('returns empty array for empty buildings', () => {
      const result = buildingsInTile([], { q: 0, r: 0, s: 0 });
      expect(result).toEqual([]);
    });

    it('returns all buildings at tile vertices', () => {
      const topRight = createMockBuilding(0, 0, 'TopRight', 'player-1');
      const bottomLeft = createMockBuilding(0, 0, 'BottomLeft', 'player-2');
      const buildings = [topRight, bottomLeft];

      const result = buildingsInTile(buildings, { q: 0, r: 0, s: 0 });
      expect(result).toHaveLength(2);
      expect(result).toContain(topRight);
      expect(result).toContain(bottomLeft);
    });

    it('finds buildings stored at alias positions', () => {
      // Building stored at North neighbor's BottomRight (alias for our TopRight)
      const aliasBuilding = createMockBuilding(0, -1, 'BottomRight', 'player-1');
      const buildings = [aliasBuilding];

      const result = buildingsInTile(buildings, { q: 0, r: 0, s: 0 });
      expect(result).toHaveLength(1);
      expect(result[0]).toBe(aliasBuilding);
    });

    it('returns up to 6 buildings', () => {
      const buildings = HEX_POSITIONS.map((pos, i) =>
        createMockBuilding(0, 0, pos, `player-${i}`)
      );

      const result = buildingsInTile(buildings, { q: 0, r: 0, s: 0 });
      expect(result).toHaveLength(6);
    });
  });

  describe('ownedBuildings', () => {
    it('returns empty array for empty buildings', () => {
      const result = ownedBuildings([], { q: 0, r: 0, s: 0 });
      expect(result).toEqual([]);
    });

    it('returns only owned buildings', () => {
      const owned = createMockBuilding(0, 0, 'TopRight', 'player-1', 'Settlement');
      const unowned = createMockBuilding(0, 0, 'BottomLeft', null, 'PossibleSettlement');
      const buildings = [owned, unowned];

      const result = ownedBuildings(buildings, { q: 0, r: 0, s: 0 });
      expect(result).toHaveLength(1);
      expect(result[0]).toBe(owned);
    });

    it('excludes buildings with empty string ownerId', () => {
      const building = createMockBuilding(0, 0, 'TopRight', '', 'PossibleSettlement');
      const result = ownedBuildings([building], { q: 0, r: 0, s: 0 });
      expect(result).toHaveLength(0);
    });
  });
});
