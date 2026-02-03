/**
 * Board asset mappings - images for tiles, harbors, and game elements.
 *
 * Asset paths are resolved through the theme store (current theme -> parent chain -> base).
 * No hardcoded paths — all resolution goes through themeStore.getAssetPath().
 */

import type { ResourceType } from '@/types/generated/models/resource-type';
import type { HarborType } from '@/types/generated/models/harbor-type';
import type { AssetName } from '@/lib/theme/types';
import { useThemeStore } from '@/lib/theme/themeStore';

// ============================================================================
// Mapping tables: game enum values -> AssetName keys
// ============================================================================

const RESOURCE_TO_TILE_ASSET: Partial<Record<ResourceType, AssetName>> = {
  Wheat: 'TileWheat',
  Wood: 'TileWood',
  Sheep: 'TileSheep',
  Brick: 'TileBrick',
  Ore: 'TileOre',
  Desert: 'TileDesert',
  GoldMine: 'TileGoldMine',
  Back: 'TileSea',
  Sea: 'TileSea',
  None: 'TileSea',
};

const HARBOR_TO_ASSET: Partial<Record<HarborType, AssetName>> = {
  ThreeForOne: 'HarborThreeForOne',
  Wheat: 'HarborWheat',
  Wood: 'HarborWood',
  Sheep: 'HarborSheep',
  Brick: 'HarborBrick',
  Ore: 'HarborOre',
};

const RESOURCE_TO_CARD_ASSET: Partial<Record<ResourceType, AssetName>> = {
  Wheat: 'CardWheat',
  Wood: 'CardWood',
  Sheep: 'CardSheep',
  Brick: 'CardBrick',
  Ore: 'CardOre',
  GoldMine: 'CardGoldMine',
};

// ============================================================================
// Asset resolution helper
// ============================================================================

function resolveAsset(asset: AssetName | undefined): string {
  if (!asset) return '';
  return useThemeStore.getState().getAssetPath(asset);
}

// ============================================================================
// Public API
// ============================================================================

/**
 * Pip count for each dice number (probability indicator).
 * Not theme-dependent.
 */
export const NUMBER_PIPS: Record<number, number> = {
  2: 1,
  3: 2,
  4: 3,
  5: 4,
  6: 5,
  7: 0,
  8: 5,
  9: 4,
  10: 3,
  11: 2,
  12: 1,
};

/** Get the image URL for a resource tile. */
export function getResourceTileImage(resourceType: ResourceType): string {
  return resolveAsset(RESOURCE_TO_TILE_ASSET[resourceType]);
}

/** Get the image URL for a harbor icon. */
export function getHarborImage(harborType: HarborType): string {
  return resolveAsset(HARBOR_TO_ASSET[harborType]);
}

/** Get the image URL for a resource card (used in gold indicator). */
export function getResourceCardImage(resourceType: ResourceType): string {
  return resolveAsset(RESOURCE_TO_CARD_ASSET[resourceType]);
}
