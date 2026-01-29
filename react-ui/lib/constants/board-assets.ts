/**
 * Board asset mappings - images for tiles, harbors, and game elements.
 *
 * These constants map game model enums to asset paths.
 */

import type { ResourceType } from '@/types/generated/models/resource-type';
import type { HarborType } from '@/types/generated/models/harbor-type';

/**
 * Resource type to tile image mapping
 */
export const RESOURCE_TILE_IMAGES: Partial<Record<ResourceType, string>> = {
  Wheat: '/themes/base/tiles/wheat.png',
  Wood: '/themes/base/tiles/wood.png',
  Sheep: '/themes/base/tiles/sheep.png',
  Brick: '/themes/base/tiles/brick.png',
  Ore: '/themes/base/tiles/ore.png',
  Desert: '/themes/base/tiles/desert.png',
  GoldMine: '/themes/base/tiles/goldMine.png',
  Back: '/themes/base/tiles/back.jpg',
  Sea: '/themes/base/tiles/back.jpg',
  None: '/themes/base/tiles/back.jpg',
};

/**
 * Harbor type to image mapping
 */
export const HARBOR_IMAGES: Partial<Record<HarborType, string>> = {
  ThreeForOne: '/themes/base/harbors/3 for 1.png',
  Wheat: '/themes/base/harbors/2 for 1 wheat.png',
  Wood: '/themes/base/harbors/2 for 1 wood.png',
  Sheep: '/themes/base/harbors/2 for 1 sheep.png',
  Brick: '/themes/base/harbors/2 for 1 brick.png',
  Ore: '/themes/base/harbors/2 for 1 ore.png',
  None: '',
};

/**
 * Pip count for each dice number (probability indicator)
 * 2 and 12: 1 pip (1/36 chance)
 * 3 and 11: 2 pips (2/36 chance)
 * 4 and 10: 3 pips (3/36 chance)
 * 5 and 9: 4 pips (4/36 chance)
 * 6 and 8: 5 pips (5/36 chance)
 * 7 (robber): 0 pips
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

/**
 * Get the image URL for a resource type
 */
export function getResourceTileImage(resourceType: ResourceType): string {
  return RESOURCE_TILE_IMAGES[resourceType] ?? '/themes/base/tiles/back.jpg';
}

/**
 * Resource type to card image mapping (for gold indicator)
 */
export const RESOURCE_CARD_IMAGES: Partial<Record<ResourceType, string>> = {
  Wheat: '/themes/base/resources/wheat.png',
  Wood: '/themes/base/resources/wood.png',
  Sheep: '/themes/base/resources/sheep.png',
  Brick: '/themes/base/resources/brick.png',
  Ore: '/themes/base/resources/ore.png',
  GoldMine: '/themes/base/resources/goldmine.png',
};

/**
 * Get the image URL for a resource card (used in gold indicator)
 */
export function getResourceCardImage(resourceType: ResourceType): string {
  return RESOURCE_CARD_IMAGES[resourceType] ?? '/themes/base/resources/back.png';
}

/**
 * Get the image URL for a harbor type
 */
export function getHarborImage(harborType: HarborType): string {
  return HARBOR_IMAGES[harborType] ?? '';
}
