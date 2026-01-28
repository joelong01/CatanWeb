/**
 * Tests for roadExtensions
 *
 * CRITICAL: These tests verify alias handling which is essential for
 * correct road lookups in the hex grid system. Roads are shared by 2 hexes.
 */

import { describe, it, expect } from 'vitest';
import {
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
} from '../roadExtensions';
import type { RoadModel } from '@/types/generated/models/road-model';
import type { RoadKey } from '@/types/generated/models/road-key';
import type { HexCoordinates } from '@/types/generated/models/hex-coordinates';
import type { HexSide } from '@/types/generated/models/hex-side';
import type { RoadState } from '@/types/generated/models/road-state';

// Helper to create a minimal road for testing
function createMockRoad(
  q: number,
  r: number,
  hexSide: HexSide,
  ownerId: string | null = null,
  roadState: RoadState = 'Unowned',
  buildIndex: number = 0
): RoadModel {
  return {
    roadKey: {
      tileKey: { q, r, s: -q - r },
      hexSide,
    },
    roadState,
    ownerId: ownerId ?? '',
    buildIndex,
  };
}

// Helper to create a road key
// Note: Using `|| 0` to normalize -0 to 0 for consistent equality checks
function createKey(q: number, r: number, hexSide: HexSide): RoadKey {
  return {
    tileKey: { q, r, s: (-q - r) || 0 },
    hexSide,
  };
}

describe('roadExtensions', () => {
  describe('HEX_SIDES', () => {
    it('has 6 sides', () => {
      expect(HEX_SIDES).toHaveLength(6);
    });

    it('excludes None', () => {
      expect(HEX_SIDES).not.toContain('None');
    });

    it('contains all valid sides', () => {
      expect(HEX_SIDES).toContain('Top');
      expect(HEX_SIDES).toContain('TopRight');
      expect(HEX_SIDES).toContain('BottomRight');
      expect(HEX_SIDES).toContain('Bottom');
      expect(HEX_SIDES).toContain('BottomLeft');
      expect(HEX_SIDES).toContain('TopLeft');
    });
  });

  describe('roadKeyAlias', () => {
    it('returns correct alias for Top', () => {
      const alias = roadKeyAlias(createKey(0, 0, 'Top'));
      expect(alias).toEqual({ hexSide: 'Bottom', direction: 'North' });
    });

    it('returns correct alias for TopRight', () => {
      const alias = roadKeyAlias(createKey(0, 0, 'TopRight'));
      expect(alias).toEqual({ hexSide: 'BottomLeft', direction: 'NorthEast' });
    });

    it('returns correct alias for BottomRight', () => {
      const alias = roadKeyAlias(createKey(0, 0, 'BottomRight'));
      expect(alias).toEqual({ hexSide: 'TopLeft', direction: 'SouthEast' });
    });

    it('returns correct alias for Bottom', () => {
      const alias = roadKeyAlias(createKey(0, 0, 'Bottom'));
      expect(alias).toEqual({ hexSide: 'Top', direction: 'South' });
    });

    it('returns correct alias for BottomLeft', () => {
      const alias = roadKeyAlias(createKey(0, 0, 'BottomLeft'));
      expect(alias).toEqual({ hexSide: 'TopRight', direction: 'SouthWest' });
    });

    it('returns correct alias for TopLeft', () => {
      const alias = roadKeyAlias(createKey(0, 0, 'TopLeft'));
      expect(alias).toEqual({ hexSide: 'BottomRight', direction: 'NorthWest' });
    });

    it('returns undefined for None', () => {
      const alias = roadKeyAlias(createKey(0, 0, 'None'));
      expect(alias).toBeUndefined();
    });

    it('each valid side has exactly 1 alias', () => {
      for (const side of HEX_SIDES) {
        const alias = roadKeyAlias(createKey(0, 0, side));
        expect(alias).toBeDefined();
      }
    });
  });

  describe('roadKeysEqual', () => {
    it('returns true for equal keys', () => {
      const a = createKey(0, 0, 'Top');
      const b = createKey(0, 0, 'Top');
      expect(roadKeysEqual(a, b)).toBe(true);
    });

    it('returns false for different sides', () => {
      const a = createKey(0, 0, 'Top');
      const b = createKey(0, 0, 'Bottom');
      expect(roadKeysEqual(a, b)).toBe(false);
    });

    it('returns false for different coordinates', () => {
      const a = createKey(0, 0, 'Top');
      const b = createKey(1, 0, 'Top');
      expect(roadKeysEqual(a, b)).toBe(false);
    });
  });

  describe('findRoad', () => {
    it('returns undefined for empty array', () => {
      const result = findRoad([], createKey(0, 0, 'Top'));
      expect(result).toBeUndefined();
    });

    it('finds road by direct key match', () => {
      const road = createMockRoad(0, 0, 'Top', 'player-1', 'Road');
      const result = findRoad([road], createKey(0, 0, 'Top'));
      expect(result).toBe(road);
    });

    it('returns undefined when not found', () => {
      const road = createMockRoad(0, 0, 'Top');
      const result = findRoad([road], createKey(0, 0, 'Bottom'));
      expect(result).toBeUndefined();
    });

    // CRITICAL ALIAS TESTS
    describe('alias handling', () => {
      it('finds Top road via North neighbor Bottom alias', () => {
        // Road stored at Top of (0,0,0)
        // Should be found via Bottom of (0,-1,1) (North neighbor)
        const road = createMockRoad(0, 0, 'Top', 'player-1', 'Road');

        const aliasKey = createKey(0, -1, 'Bottom'); // North neighbor's Bottom
        const result = findRoad([road], aliasKey);
        expect(result).toBe(road);
      });

      it('finds TopRight road via NorthEast neighbor BottomLeft alias', () => {
        const road = createMockRoad(0, 0, 'TopRight', 'player-1', 'Road');

        const aliasKey = createKey(1, -1, 'BottomLeft'); // NorthEast neighbor's BottomLeft
        const result = findRoad([road], aliasKey);
        expect(result).toBe(road);
      });

      it('finds BottomRight road via SouthEast neighbor TopLeft alias', () => {
        const road = createMockRoad(0, 0, 'BottomRight', 'player-1', 'Road');

        const aliasKey = createKey(1, 0, 'TopLeft'); // SouthEast neighbor's TopLeft
        const result = findRoad([road], aliasKey);
        expect(result).toBe(road);
      });

      it('finds Bottom road via South neighbor Top alias', () => {
        const road = createMockRoad(0, 0, 'Bottom', 'player-1', 'Road');

        const aliasKey = createKey(0, 1, 'Top'); // South neighbor's Top
        const result = findRoad([road], aliasKey);
        expect(result).toBe(road);
      });

      it('finds BottomLeft road via SouthWest neighbor TopRight alias', () => {
        const road = createMockRoad(0, 0, 'BottomLeft', 'player-1', 'Road');

        const aliasKey = createKey(-1, 1, 'TopRight'); // SouthWest neighbor's TopRight
        const result = findRoad([road], aliasKey);
        expect(result).toBe(road);
      });

      it('finds TopLeft road via NorthWest neighbor BottomRight alias', () => {
        const road = createMockRoad(0, 0, 'TopLeft', 'player-1', 'Road');

        const aliasKey = createKey(-1, 0, 'BottomRight'); // NorthWest neighbor's BottomRight
        const result = findRoad([road], aliasKey);
        expect(result).toBe(road);
      });

      it('finds road stored at alias position via primary key', () => {
        // Road stored at Bottom of (0,-1,1) (the North neighbor)
        // Should be found via Top of (0,0,0)
        const road = createMockRoad(0, -1, 'Bottom', 'player-1', 'Road');

        const primaryKey = createKey(0, 0, 'Top');
        const result = findRoad([road], primaryKey);
        expect(result).toBe(road);
      });

      it('handles multiple roads with distinct keys', () => {
        const road1 = createMockRoad(0, 0, 'Top', 'player-1', 'Road');
        const road2 = createMockRoad(0, 0, 'Bottom', 'player-2', 'Road');
        const roads = [road1, road2];

        expect(findRoad(roads, createKey(0, 0, 'Top'))).toBe(road1);
        expect(findRoad(roads, createKey(0, 0, 'Bottom'))).toBe(road2);

        // Via aliases
        expect(findRoad(roads, createKey(0, -1, 'Bottom'))).toBe(road1);
        expect(findRoad(roads, createKey(0, 1, 'Top'))).toBe(road2);
      });
    });
  });

  describe('adjacentRoadKeys', () => {
    it('returns 4 adjacent keys for Top', () => {
      const keys = adjacentRoadKeys(createKey(0, 0, 'Top'));
      expect(keys).toHaveLength(4);
    });

    it('returns 4 adjacent keys for each valid side', () => {
      for (const side of HEX_SIDES) {
        const keys = adjacentRoadKeys(createKey(0, 0, side));
        expect(keys).toHaveLength(4);
      }
    });

    it('returns empty array for None', () => {
      const keys = adjacentRoadKeys(createKey(0, 0, 'None'));
      expect(keys).toHaveLength(0);
    });

    it('Top adjacent roads include TopRight and TopLeft on same hex', () => {
      const keys = adjacentRoadKeys(createKey(0, 0, 'Top'));
      // Check for adjacent roads on same hex (note: use functional equality, not deep equality due to -0/0)
      const hasTopRight = keys.some(
        (k) => k.tileKey.q === 0 && k.tileKey.r === 0 && k.hexSide === 'TopRight'
      );
      const hasTopLeft = keys.some(
        (k) => k.tileKey.q === 0 && k.tileKey.r === 0 && k.hexSide === 'TopLeft'
      );
      expect(hasTopRight).toBe(true);
      expect(hasTopLeft).toBe(true);
    });
  });

  describe('adjacentRoads', () => {
    it('returns empty array for empty roads', () => {
      const result = adjacentRoads([], createKey(0, 0, 'Top'));
      expect(result).toEqual([]);
    });

    it('finds adjacent roads for Top position', () => {
      const topRight = createMockRoad(0, 0, 'TopRight', 'player-1', 'Road');
      const topLeft = createMockRoad(0, 0, 'TopLeft', 'player-2', 'Road');
      const roads = [topRight, topLeft];

      const result = adjacentRoads(roads, createKey(0, 0, 'Top'));
      expect(result).toHaveLength(2);
      expect(result).toContain(topRight);
      expect(result).toContain(topLeft);
    });

    it('returns only existing adjacent roads', () => {
      const topRight = createMockRoad(0, 0, 'TopRight', 'player-1', 'Road');
      const roads = [topRight];

      const result = adjacentRoads(roads, createKey(0, 0, 'Top'));
      expect(result).toHaveLength(1);
      expect(result[0]).toBe(topRight);
    });

    it('finds adjacent roads via aliases', () => {
      // Road at BottomRight of North neighbor is adjacent to Top
      const neighborRoad = createMockRoad(0, -1, 'BottomRight', 'player-1', 'Road');
      const roads = [neighborRoad];

      const result = adjacentRoads(roads, createKey(0, 0, 'Top'));
      expect(result).toHaveLength(1);
      expect(result[0]).toBe(neighborRoad);
    });
  });

  describe('roadsInTile', () => {
    it('returns empty array for empty roads', () => {
      const result = roadsInTile([], { q: 0, r: 0, s: 0 });
      expect(result).toEqual([]);
    });

    it('returns all roads at tile edges', () => {
      const top = createMockRoad(0, 0, 'Top', 'player-1', 'Road');
      const bottom = createMockRoad(0, 0, 'Bottom', 'player-2', 'Road');
      const roads = [top, bottom];

      const result = roadsInTile(roads, { q: 0, r: 0, s: 0 });
      expect(result).toHaveLength(2);
      expect(result).toContain(top);
      expect(result).toContain(bottom);
    });

    it('finds roads stored at alias positions', () => {
      // Road stored at North neighbor's Bottom (alias for our Top)
      const aliasRoad = createMockRoad(0, -1, 'Bottom', 'player-1', 'Road');
      const roads = [aliasRoad];

      const result = roadsInTile(roads, { q: 0, r: 0, s: 0 });
      expect(result).toHaveLength(1);
      expect(result[0]).toBe(aliasRoad);
    });

    it('returns up to 6 roads', () => {
      const roads = HEX_SIDES.map((side, i) =>
        createMockRoad(0, 0, side, `player-${i}`, 'Road')
      );

      const result = roadsInTile(roads, { q: 0, r: 0, s: 0 });
      expect(result).toHaveLength(6);
    });

    it('does not duplicate roads', () => {
      // Create road and its alias - should only appear once
      const road = createMockRoad(0, 0, 'Top', 'player-1', 'Road');
      const roads = [road];

      const result = roadsInTile(roads, { q: 0, r: 0, s: 0 });
      expect(result).toHaveLength(1);
    });
  });

  describe('ownedRoads', () => {
    it('returns empty array for empty roads', () => {
      const result = ownedRoads([], { q: 0, r: 0, s: 0 });
      expect(result).toEqual([]);
    });

    it('returns only owned roads', () => {
      const owned = createMockRoad(0, 0, 'Top', 'player-1', 'Road');
      const unowned = createMockRoad(0, 0, 'Bottom', null, 'Unowned');
      const roads = [owned, unowned];

      const result = ownedRoads(roads, { q: 0, r: 0, s: 0 });
      expect(result).toHaveLength(1);
      expect(result[0]).toBe(owned);
    });

    it('excludes roads with empty string ownerId', () => {
      const road = createMockRoad(0, 0, 'Top', '', 'Buildable');
      const result = ownedRoads([road], { q: 0, r: 0, s: 0 });
      expect(result).toHaveLength(0);
    });

    it('includes Ship state', () => {
      const ship = createMockRoad(0, 0, 'Top', 'player-1', 'Ship');
      const result = ownedRoads([ship], { q: 0, r: 0, s: 0 });
      expect(result).toHaveLength(1);
      expect(result[0]).toBe(ship);
    });

    it('excludes Buildable state even with ownerId', () => {
      const buildable = createMockRoad(0, 0, 'Top', 'player-1', 'Buildable');
      const result = ownedRoads([buildable], { q: 0, r: 0, s: 0 });
      expect(result).toHaveLength(0);
    });
  });

  describe('roadsOwnedByPlayer', () => {
    it('returns empty array for empty roads', () => {
      const result = roadsOwnedByPlayer([], 'player-1');
      expect(result).toEqual([]);
    });

    it('returns empty array for null playerId', () => {
      const road = createMockRoad(0, 0, 'Top', 'player-1', 'Road');
      const result = roadsOwnedByPlayer([road], '');
      expect(result).toEqual([]);
    });

    it('returns only roads owned by the specified player', () => {
      const p1Road1 = createMockRoad(0, 0, 'Top', 'player-1', 'Road');
      const p1Road2 = createMockRoad(0, 0, 'Bottom', 'player-1', 'Road');
      const p2Road = createMockRoad(1, 0, 'Top', 'player-2', 'Road');
      const roads = [p1Road1, p1Road2, p2Road];

      const result = roadsOwnedByPlayer(roads, 'player-1');
      expect(result).toHaveLength(2);
      expect(result).toContain(p1Road1);
      expect(result).toContain(p1Road2);
      expect(result).not.toContain(p2Road);
    });
  });

  describe('buildableRoads', () => {
    it('returns empty array for empty roads', () => {
      const result = buildableRoads([]);
      expect(result).toEqual([]);
    });

    it('returns only buildable roads', () => {
      const buildable = createMockRoad(0, 0, 'Top', '', 'Buildable', 1);
      const owned = createMockRoad(0, 0, 'Bottom', 'player-1', 'Road');
      const unowned = createMockRoad(1, 0, 'Top', '', 'Unowned');
      const roads = [buildable, owned, unowned];

      const result = buildableRoads(roads);
      expect(result).toHaveLength(1);
      expect(result[0]).toBe(buildable);
    });

    it('returns buildable roads with buildIndex', () => {
      const buildable1 = createMockRoad(0, 0, 'Top', '', 'Buildable', 1);
      const buildable2 = createMockRoad(0, 0, 'Bottom', '', 'Buildable', 2);
      const roads = [buildable1, buildable2];

      const result = buildableRoads(roads);
      expect(result).toHaveLength(2);
      expect(result.map((r) => r.buildIndex)).toContain(1);
      expect(result.map((r) => r.buildIndex)).toContain(2);
    });
  });
});
