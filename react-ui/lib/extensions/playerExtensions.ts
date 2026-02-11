/**
 * Player extension functions - TypeScript equivalents of C# PlayerModelExtensions.
 *
 * These functions provide consistent player lookup utilities matching the
 * server-side API for use across the React application.
 *
 * @module playerExtensions
 */

import type { PlayerModel } from '@/types/generated/models/player-model';

/**
 * Finds a player by their ID in a collection of players.
 *
 * @param players - Array of player models to search
 * @param id - The player ID to find
 * @returns The matching PlayerModel, or undefined if not found
 *
 * @example
 * const player = playerFromId(gameModel.players, 'player-1');
 * if (player) {
 *   console.log(`Found player: ${player.name}`);
 * }
 */
export function playerFromId(
  players: PlayerModel[],
  id: string | null | undefined
): PlayerModel | undefined {
  if (!id || !players || players.length === 0) {
    return undefined;
  }
  return players.find((p) => p.id === id);
}
