/**
 * Player color utilities for UI rendering.
 */

import type { PlayerColors } from '@/types/player-profile';

/**
 * Extended player colors with computed CSS gradient.
 */
export interface PlayerColorsWithGradient extends PlayerColors {
  cssGradient: string;
}

/**
 * Compute relative luminance of a hex color (0-1 scale).
 * Used to determine if a color is "light" or "dark".
 */
function getLuminance(hex: string): number {
  const rgb = hex.replace('#', '');
  const r = parseInt(rgb.substring(0, 2), 16) / 255;
  const g = parseInt(rgb.substring(2, 4), 16) / 255;
  const b = parseInt(rgb.substring(4, 6), 16) / 255;
  // Simplified luminance (weighted average)
  return 0.299 * r + 0.587 * g + 0.114 * b;
}

/**
 * Build CSS gradient from PlayerColors.
 * 3-stop gradient: Primary → Secondary → auto-contrast.
 * The third stop is black for light color schemes, white for dark ones,
 * creating visual depth.
 */
export function buildCssGradient(colors: PlayerColors): string {
  const avgLuminance = (getLuminance(colors.primary) + getLuminance(colors.secondary)) / 2;
  const endColor = avgLuminance > 0.4 ? '#000000' : '#ffffff';
  return `linear-gradient(135deg, ${colors.primary}, ${colors.secondary}, ${endColor})`;
}

/**
 * Create PlayerColors with computed gradient.
 */
export function createPlayerColorsWithGradient(colors: PlayerColors): PlayerColorsWithGradient {
  return {
    ...colors,
    cssGradient: buildCssGradient(colors),
  };
}

/**
 * Create PlayerColors from primary, secondary, foreground values.
 */
export function createPlayerColors(
  primary: string,
  secondary: string,
  foreground: string
): PlayerColorsWithGradient {
  const colors: PlayerColors = { primary, secondary, foreground };
  return createPlayerColorsWithGradient(colors);
}

/**
 * Convert hex color to rgba string.
 */
export function hexToRgba(hex: string, alpha: number): string {
  const rgb = hex.replace('#', '');
  // Handle shorthand hex #RGB
  if (rgb.length === 3) {
    const r = parseInt(rgb[0] + rgb[0], 16);
    const g = parseInt(rgb[1] + rgb[1], 16);
    const b = parseInt(rgb[2] + rgb[2], 16);
    return `rgba(${r}, ${g}, ${b}, ${alpha})`;
  }
  const r = parseInt(rgb.substring(0, 2), 16);
  const g = parseInt(rgb.substring(2, 4), 16);
  const b = parseInt(rgb.substring(4, 6), 16);
  return `rgba(${r}, ${g}, ${b}, ${alpha})`;
}
