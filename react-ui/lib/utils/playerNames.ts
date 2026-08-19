/**
 * Player display-name resolution.
 *
 * `PlayerProfile` is the only source of truth for a display name. `PlayerModel` carries no
 * name at all — a name must never be derived by parsing a player ID, which is what produced
 * GUID fragments like `1ffb33af` for players whose ID was not `Name-NNN` (issue #208).
 *
 * These are plain functions rather than hooks so they can be used inside `useMemo` and
 * `useCallback`, where hooks are illegal. Components with only an ID should prefer
 * `usePlayerName` from `@/lib/stores/gameStoreHooks`.
 *
 * @module playerNames
 */

import type { PlayerProfile } from '@/types/player-profile';

/** Shown while player profiles are still being fetched. */
export const PLAYER_NAME_LOADING = 'Loading...';

/** Shown when a player ID has no corresponding profile. */
export const PLAYER_NAME_MISSING = 'Profile Error';

/**
 * Resolves a player's current display name from the profile map.
 *
 * @param profiles - Profile map keyed by player ID; empty means "not yet loaded"
 * @param playerId - The player ID to resolve
 * @returns The profile name, or a visibly non-name placeholder
 */
export function resolvePlayerName(
  profiles: ReadonlyMap<string, PlayerProfile>,
  playerId: string
): string {
  const name = profiles.get(playerId)?.name;
  if (name) return name;
  return profiles.size === 0 ? PLAYER_NAME_LOADING : PLAYER_NAME_MISSING;
}

/**
 * True when `id` is a bare GUID — no name segment ahead of the hex.
 *
 * This is the population that could ever have had a corrupt stored name, because it is the
 * only ID shape with no name to parse. `Joe-001` and `Ty-<uuid>` both begin with the real
 * name, so a stored name matching their prefix is genuine and must not be second-guessed.
 */
function isBareGuid(id: string): boolean {
  return /^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i.test(id);
}

/**
 * Resolves the display name for a **historical** record (a completed game).
 *
 * Completed games are point-in-time documents: the stored name is what the player was called
 * when the game ended, and a later rename must not rewrite it. So the stored name wins —
 * except when it is provably a bug artifact rather than a name anyone ever had.
 *
 * A stored name is treated as corrupt only when the ID is a **bare GUID** and the stored
 * name equals its first segment (e.g. id `1ffb33af-9316-…` stored as `"1ffb33af"`). Scoping
 * by ID shape matters: an earlier version of this rule compared the stored name against the
 * current profile name, which fired on every rename of a `Ty-<uuid>` player and destroyed
 * exactly the history it was meant to protect.
 *
 * @param profiles - Profile map keyed by player ID
 * @param playerId - The player ID recorded on the historical entry
 * @param storedName - The name recorded on the historical entry, if any
 * @returns The name to display for this historical record
 */
export function resolveHistoricalName(
  profiles: ReadonlyMap<string, PlayerProfile>,
  playerId: string,
  storedName: string | undefined
): string {
  if (!storedName) return resolvePlayerName(profiles, playerId);

  const isCorruptArtifact = isBareGuid(playerId) && storedName === playerId.split('-')[0];
  if (isCorruptArtifact) return resolvePlayerName(profiles, playerId);

  return storedName;
}
