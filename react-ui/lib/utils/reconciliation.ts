/**
 * Reconciliation utilities for GameModel updates.
 *
 * When SignalR sends a new GameModel, we use structural sharing to preserve
 * references to unchanged items. This prevents unnecessary re-renders in
 * React components that use React.memo.
 *
 * Pattern:
 *   const reconciled = reconcileGameModel(prevModel, newModel);
 *   // reconciled.tiles[0] === prevModel.tiles[0] if that tile didn't change
 */

import type { GameModel } from '@/types/generated/models/game-model';
import type { TileModel } from '@/types/generated/models/tile-model';
import type { HexCoordinates } from '@/types/generated/models/hex-coordinates';
import type { RoadKey } from '@/types/generated/models/road-key';
import type { BuildingKey } from '@/types/generated/models/building-key';
import type { HarborKey } from '@/types/generated/models/harbor-key';

// ============================================================================
// Key String Functions
// ============================================================================

/** Create a string key from HexCoordinates */
export function hexCoordsToString(coords: HexCoordinates): string {
  return `${coords.q},${coords.r}`;
}

/** Create a string key from TileModel */
export function tileKeyString(tile: TileModel): string {
  return hexCoordsToString(tile.tileKey);
}

/** Create a string key from RoadKey */
export function roadKeyString(key: RoadKey): string {
  return `${hexCoordsToString(key.tileKey)}:${key.hexSide}`;
}

/** Create a string key from BuildingKey */
export function buildingKeyString(key: BuildingKey): string {
  return `${hexCoordsToString(key.hexCoordinates)}:${key.position}`;
}

/** Create a string key from HarborKey */
export function harborKeyString(key: HarborKey): string {
  return `${hexCoordsToString(key.hexCoordinates)}:${key.harborType}:${key.side}`;
}

// ============================================================================
// Deep Equality Check (for reconciliation)
// ============================================================================

/**
 * Shallow comparison of two objects.
 * Returns true if all own properties are strictly equal.
 */
function shallowEqual(a: unknown, b: unknown): boolean {
  if (a === b) return true;
  if (typeof a !== 'object' || typeof b !== 'object') return false;
  if (a === null || b === null) return false;

  const keysA = Object.keys(a as object);
  const keysB = Object.keys(b as object);

  if (keysA.length !== keysB.length) return false;

  for (const key of keysA) {
    if ((a as Record<string, unknown>)[key] !== (b as Record<string, unknown>)[key]) {
      return false;
    }
  }

  return true;
}

/**
 * Deep comparison of two values.
 * For objects, recursively compares all properties.
 * For arrays, recursively compares all elements.
 */
export function deepEqual(a: unknown, b: unknown): boolean {
  if (a === b) return true;

  if (typeof a !== typeof b) return false;
  if (typeof a !== 'object' || a === null || b === null) return false;

  if (Array.isArray(a) !== Array.isArray(b)) return false;

  if (Array.isArray(a) && Array.isArray(b)) {
    if (a.length !== b.length) return false;
    for (let i = 0; i < a.length; i++) {
      if (!deepEqual(a[i], b[i])) return false;
    }
    return true;
  }

  const keysA = Object.keys(a as object);
  const keysB = Object.keys(b as object);

  if (keysA.length !== keysB.length) return false;

  for (const key of keysA) {
    if (!keysB.includes(key)) return false;
    if (!deepEqual((a as Record<string, unknown>)[key], (b as Record<string, unknown>)[key])) {
      return false;
    }
  }

  return true;
}

// ============================================================================
// Array Reconciliation
// ============================================================================

/**
 * Reconcile an array by preserving references to unchanged items.
 *
 * For each item in the new array:
 * - If an item with the same key exists in the old array AND is deeply equal,
 *   reuse the old reference (prevents re-render)
 * - Otherwise, use the new item
 *
 * If the entire array is unchanged, return the old array reference.
 */
export function reconcileArray<T>(prev: T[], next: T[], getKey: (item: T) => string): T[] {
  // If same reference, return as-is
  if (prev === next) return prev;

  // Build lookup for previous items
  const prevMap = new Map<string, T>();
  for (const item of prev) {
    prevMap.set(getKey(item), item);
  }

  let hasChanges = false;
  const result: T[] = [];

  for (let i = 0; i < next.length; i++) {
    const nextItem = next[i];
    const key = getKey(nextItem);
    const prevItem = prevMap.get(key);

    if (prevItem && deepEqual(prevItem, nextItem)) {
      // Item unchanged - reuse old reference
      result.push(prevItem);
      // Check if order changed (item at different index)
      if (i < prev.length && getKey(prev[i]) !== key) {
        hasChanges = true;
      }
    } else {
      // Item is new or changed
      hasChanges = true;
      result.push(nextItem);
    }
  }

  // Check if any items were removed
  if (prev.length !== next.length) {
    hasChanges = true;
  }

  // If no changes (including order) and same length, return original array reference
  if (!hasChanges) {
    return prev;
  }

  return result;
}

// ============================================================================
// GameModel Reconciliation
// ============================================================================

/**
 * Reconcile a GameModel by preserving references to unchanged parts.
 *
 * This is the main entry point for SignalR updates:
 *
 * ```typescript
 * proxy.onGameStateUpdated((newModel) => {
 *   const reconciled = reconcileGameModel(prevModel, newModel);
 *   store.setGameModel(reconciled);
 * });
 * ```
 *
 * Benefits:
 * - Tiles array rarely changes → reuse reference → TilesLayer skips render
 * - Roads/buildings change on player actions → only affected components re-render
 * - With React.memo on GameTile, Road, Building, only changed items re-render
 *
 * Note: On F5/refresh, prev is null so we return next directly (no reconciliation).
 * The server always sends a new gameHash, so we don't use it for early exit.
 */
export function reconcileGameModel(prev: GameModel | null, next: GameModel): GameModel {
  // No previous model (F5 refresh, initial load) - just return next
  if (!prev) return next;

  // Different game - no reconciliation possible
  if (prev.gameId !== next.gameId) return next;

  // Reconcile each array
  const tiles = reconcileArray(prev.tiles, next.tiles, (t) => tileKeyString(t));
  const roads = reconcileArray(prev.roads, next.roads, (r) => roadKeyString(r.roadKey));
  const buildings = reconcileArray(prev.buildings, next.buildings, (b) =>
    buildingKeyString(b.buildingKey)
  );
  const harbors = reconcileArray(prev.harbors, next.harbors, (h) => harborKeyString(h.harborKey));
  const players = reconcileArray(prev.players, next.players, (p) => p.id);

  // Check if all arrays are unchanged (same references)
  const arraysUnchanged =
    tiles === prev.tiles &&
    roads === prev.roads &&
    buildings === prev.buildings &&
    harbors === prev.harbors &&
    players === prev.players;

  // Check if scalar fields are unchanged
  const scalarsUnchanged =
    prev.gameState === next.gameState &&
    prev.currentPlayerId === next.currentPlayerId &&
    prev.gameHash === next.gameHash &&
    shallowEqual(prev.actionFlags, next.actionFlags) &&
    shallowEqual(prev.robber, next.robber) &&
    shallowEqual(prev.rollModel, next.rollModel);

  // If everything is unchanged, return prev to avoid any re-renders
  if (arraysUnchanged && scalarsUnchanged) {
    return prev;
  }

  // Return new object with reconciled arrays
  return {
    ...next,
    tiles,
    roads,
    buildings,
    harbors,
    players,
  };
}
