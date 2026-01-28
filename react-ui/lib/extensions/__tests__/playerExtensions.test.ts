/**
 * Tests for playerExtensions
 */

import { describe, it, expect } from 'vitest';
import { playerFromId } from '../playerExtensions';
import type { PlayerModel } from '@/types/generated/models/player-model';

// Create a minimal mock PlayerModel for testing
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
      count: 0
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
      count: 0
    },
    unspentEntitlements: [],
    spentEntitlementsThisGame: [],
    spentEntitlementsThisTurn: [],
    ownedHarbors: [],
    victoryPointCards: 0,
  };
}

describe('playerExtensions', () => {
  describe('playerFromId', () => {
    const player1 = createMockPlayer('player-1', 'Alice');
    const player2 = createMockPlayer('player-2', 'Bob');
    const player3 = createMockPlayer('player-3', 'Charlie');
    const players = [player1, player2, player3];

    it('returns undefined for empty players array', () => {
      const result = playerFromId([], 'player-1');
      expect(result).toBeUndefined();
    });

    it('returns undefined for null id', () => {
      const result = playerFromId(players, null);
      expect(result).toBeUndefined();
    });

    it('returns undefined for undefined id', () => {
      const result = playerFromId(players, undefined);
      expect(result).toBeUndefined();
    });

    it('returns undefined when player not found', () => {
      const result = playerFromId(players, 'nonexistent-player');
      expect(result).toBeUndefined();
    });

    it('returns player when found by id', () => {
      const result = playerFromId(players, 'player-2');
      expect(result).toBe(player2);
      expect(result?.name).toBe('Bob');
    });

    it('returns first player when searching for first id', () => {
      const result = playerFromId(players, 'player-1');
      expect(result).toBe(player1);
      expect(result?.name).toBe('Alice');
    });

    it('returns last player when searching for last id', () => {
      const result = playerFromId(players, 'player-3');
      expect(result).toBe(player3);
      expect(result?.name).toBe('Charlie');
    });
  });
});
