/**
 * Hex grid visual constants.
 *
 * All scale values are computed from the base BORDER_FRACTION to ensure
 * visual consistency across all hex content components.
 *
 * The two-polygon border pattern:
 * - Outer hex: full size (100%)
 * - Inner hex: scaled down to reveal outer hex as border
 */

/**
 * Border thickness as a fraction of hex size.
 * 0.09 = 9% border visible around content.
 */
export const HEX_BORDER_FRACTION = 0.09;

/**
 * Content scale factor (inner hex size relative to outer hex).
 * Computed as 1 - BORDER_FRACTION = 0.91
 */
export const HEX_CONTENT_SCALE = 1 - HEX_BORDER_FRACTION;

/**
 * Additional shrink on hover to show more border.
 * Creates visual feedback by revealing more of the outer border hex.
 */
export const HEX_HOVER_SHRINK = 0.03;

/**
 * Content scale when hovered.
 * Computed as CONTENT_SCALE - HOVER_SHRINK = 0.88
 */
export const HEX_HOVER_SCALE = HEX_CONTENT_SCALE - HEX_HOVER_SHRINK;

/**
 * Icon scale multiplier on hover.
 * 1.1 = 10% larger icon when hovered.
 */
export const HEX_ICON_HOVER_SCALE = 1.1;
