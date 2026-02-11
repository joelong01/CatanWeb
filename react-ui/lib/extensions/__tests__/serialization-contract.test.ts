/**
 * Serialization contract tests — Layer 0 + Layer 1 of the test strategy.
 *
 * Layer 0: Can the generated TypeScript types correctly deserialize
 *          JSON produced by C#? If JSON.parse() + type assignment works
 *          and fields are accessible, the contract holds.
 *
 * Layer 1: Do extension functions compute correct values from real
 *          C#-serialized data? Compare against independently verified
 *          truth set.
 *
 * See .design/ts-test-strategy.md for full rationale.
 */

import { describe, it, expect, beforeAll } from 'vitest';
import { loadCatanTest, type CatanTestData } from '@/lib/test-data/load-catan-test';
import { EXPANSION_BOARD_TRUTH } from '@/lib/test-data/expansion-board-truth';
import type { GameModel } from '@/types/generated/models/game-model';
import {
  pipsForNumber,
  totalStars,
  tileFromCoords,
  tilesWithNumber,
  tilesWithResource,
  tilesWithSixOrEight,
} from '../tileExtensions';

describe('Serialization Contract — Expansion.catan_test', () => {
  let data: CatanTestData;
  let game: GameModel;

  beforeAll(() => {
    data = loadCatanTest('Expansion.catan_test');
    game = data.gameModel;
  });

  // ===========================================================================
  // Layer 0: Serialization contract — can we deserialize C#-serialized JSON?
  // ===========================================================================

  describe('Layer 0: Deserialization', () => {
    it('loads the file and parses as GameModel', () => {
      expect(data).toBeDefined();
      expect(game).toBeDefined();
    });

    it('game type matches', () => {
      expect(game.gameType).toBe(EXPANSION_BOARD_TRUTH.gameType);
    });

    it('has the expected number of tiles', () => {
      expect(game.tiles).toBeDefined();
      expect(Array.isArray(game.tiles)).toBe(true);
      expect(game.tiles.length).toBe(EXPANSION_BOARD_TRUTH.tileCount);
    });

    it('has the expected number of players', () => {
      expect(game.players).toBeDefined();
      expect(game.players.length).toBe(EXPANSION_BOARD_TRUTH.playerCount);
    });

    it('has the expected number of harbors', () => {
      expect(game.harbors).toBeDefined();
      expect(game.harbors.length).toBe(EXPANSION_BOARD_TRUTH.harborCount);
    });

    it('has the expected number of buildings', () => {
      expect(game.buildings).toBeDefined();
      expect(game.buildings.length).toBe(EXPANSION_BOARD_TRUTH.buildingCount);
    });

    it('has the expected number of roads', () => {
      expect(game.roads).toBeDefined();
      expect(game.roads.length).toBe(EXPANSION_BOARD_TRUTH.roadCount);
    });

    it('first tile has correct coordinates and data', () => {
      const tile = game.tiles[0];
      expect(tile.tileKey.q).toBe(EXPANSION_BOARD_TRUTH.firstTile.q);
      expect(tile.tileKey.r).toBe(EXPANSION_BOARD_TRUTH.firstTile.r);
      expect(tile.resourceTileType).toBe(EXPANSION_BOARD_TRUTH.firstTile.resource);
      expect(tile.number).toBe(EXPANSION_BOARD_TRUTH.firstTile.number);
    });

    it('player names match', () => {
      const names = game.players.map((p) => p.name);
      expect(names).toEqual(EXPANSION_BOARD_TRUTH.playerNames);
    });

    it('tiles do NOT have a stars property (JsonIgnore verification)', () => {
      // This is the critical phantom field check: C# has [JsonIgnore] on Stars,
      // so the JSON should not contain it. If someone adds stars back to the
      // generated type AND the test data provides it, this test would still
      // catch the wire format mismatch.
      const rawJson = JSON.stringify(game.tiles[0]);
      const rawObj = JSON.parse(rawJson) as Record<string, unknown>;
      expect('stars' in rawObj).toBe(false);
    });

    it('tiles do NOT have a default property (static field verification)', () => {
      const rawJson = JSON.stringify(game.tiles[0]);
      const rawObj = JSON.parse(rawJson) as Record<string, unknown>;
      expect('default' in rawObj).toBe(false);
    });
  });

  // ===========================================================================
  // Layer 1: Truth set verification — extension functions on real data
  // ===========================================================================

  describe('Layer 1: Truth set verification', () => {
    it('resource distribution matches truth set', () => {
      const counts: Record<string, number> = {};
      for (const tile of game.tiles) {
        const r = tile.resourceTileType;
        counts[r] = (counts[r] ?? 0) + 1;
      }
      expect(counts).toEqual(EXPANSION_BOARD_TRUTH.resourceCounts);
    });

    it('desert tile count matches', () => {
      const deserts = tilesWithResource(game.tiles, 'Desert');
      expect(deserts.length).toBe(EXPANSION_BOARD_TRUTH.desertCount);
    });

    it('tiles with 6 count matches', () => {
      const sixes = tilesWithNumber(game.tiles, 6);
      expect(sixes.length).toBe(EXPANSION_BOARD_TRUTH.tilesWithSix);
    });

    it('tiles with 8 count matches', () => {
      const eights = tilesWithNumber(game.tiles, 8);
      expect(eights.length).toBe(EXPANSION_BOARD_TRUTH.tilesWithEight);
    });

    it('tilesWithSixOrEight returns 6s and 8s combined', () => {
      const highProb = tilesWithSixOrEight(game.tiles);
      expect(highProb.length).toBe(
        EXPANSION_BOARD_TRUTH.tilesWithSix + EXPANSION_BOARD_TRUTH.tilesWithEight
      );
    });

    it('tile at origin matches truth set', () => {
      const center = tileFromCoords(game.tiles, { q: 0, r: 0, s: 0 });
      expect(center).toBeDefined();
      expect(center!.resourceTileType).toBe(EXPANSION_BOARD_TRUTH.tileAtOrigin.resource);
      expect(center!.number).toBe(EXPANSION_BOARD_TRUTH.tileAtOrigin.number);
      expect(pipsForNumber(center!.number)).toBe(EXPANSION_BOARD_TRUTH.tileAtOrigin.computedStars);
    });

    it('totalStars computed from tile numbers matches truth set', () => {
      const stars = totalStars(game.tiles);
      expect(stars).toBe(EXPANSION_BOARD_TRUTH.totalComputedStars);
    });
  });
});
