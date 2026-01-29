/**
 * Composite hooks for GameBoard data.
 *
 * These hooks aggregate multiple fine-grained Zustand hooks into single
 * return values, avoiding "hook explosion" in components while maintaining
 * efficient re-render behavior.
 *
 * @module useBoardData
 */

import { useMemo } from 'react';
import {
  useTiles,
  useBuildings,
  useRoads,
  useHarbors,
  useRobber,
  usePlayers,
  usePlayerProfiles,
  useCurrentPlayer,
  useLastRoll,
} from '@/lib/stores/gameStoreHooks';
import { DEFAULT_PLAYER_COLORS, type PlayerColors } from '@/types/player-profile';
import type { TileModel } from '@/types/generated/models/tile-model';
import type { BuildingModel } from '@/types/generated/models/building-model';
import type { RoadModel } from '@/types/generated/models/road-model';
import type { HarborModel } from '@/types/generated/models/harbor-model';
import type { RobberModel } from '@/types/generated/models/robber-model';
import type { Entitlement } from '@/types/generated/models/entitlement';

// ============================================================================
// Types
// ============================================================================

/**
 * Player data needed for board rendering.
 * Combines PlayerModel identity with PlayerProfile colors.
 */
export interface BoardPlayer {
  id: string;
  name: string;
  colors: PlayerColors;
}

/**
 * Minimal game data required for board rendering.
 * Used by GameBoard component internally via hooks.
 */
export interface BoardGameData {
  tiles: TileModel[];
  harbors: HarborModel[];
  buildings: BuildingModel[];
  roads: RoadModel[];
  /** The turn player's ID (from gameModel.currentPlayerId) */
  currentPlayerId: string | undefined;
  currentPlayerEntitlements: Entitlement[];
  robber: RobberModel | undefined;
}

// ============================================================================
// Hooks
// ============================================================================

/**
 * Returns players mapped to BoardPlayer format with colors from profiles.
 *
 * Combines usePlayers() and usePlayerProfiles() into a single array
 * where each player has their associated colors from their profile.
 *
 * @returns Array of BoardPlayer with id, name, and colors
 */
export function useBoardPlayers(): BoardPlayer[] {
  const players = usePlayers();
  const profiles = usePlayerProfiles();

  return useMemo(
    () =>
      players.map((p) => ({
        id: p.id,
        name: p.name,
        colors: profiles.get(p.id)?.colors ?? DEFAULT_PLAYER_COLORS,
      })),
    [players, profiles]
  );
}

/**
 * Returns all data needed for GameBoard rendering.
 *
 * Aggregates multiple fine-grained hooks into a single BoardGameData object.
 * This prevents "hook explosion" (7+ hooks in GameBoard) while still
 * benefiting from fine-grained Zustand subscriptions.
 *
 * @returns BoardGameData with tiles, buildings, roads, harbors, robber, and entitlements
 */
export function useBoardData(): BoardGameData {
  const tiles = useTiles();
  const buildings = useBuildings();
  const roads = useRoads();
  const harbors = useHarbors();
  const robber = useRobber();
  const currentPlayer = useCurrentPlayer();

  return useMemo(
    () => ({
      tiles: tiles ?? [],
      buildings: buildings ?? [],
      roads: roads ?? [],
      harbors: harbors ?? [],
      robber,
      currentPlayerId: currentPlayer?.id,
      currentPlayerEntitlements: currentPlayer?.unspentEntitlements ?? [],
    }),
    [tiles, buildings, roads, harbors, robber, currentPlayer?.id, currentPlayer?.unspentEntitlements]
  );
}

/**
 * Returns the last rolled dice number.
 *
 * Re-exported for convenience alongside other board hooks.
 */
export { useLastRoll as useRolledNumber };
