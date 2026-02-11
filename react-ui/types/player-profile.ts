/**
 * Player profile types - re-exported from auto-generated models.
 * Hand-written constants (DEFAULT_PLAYER_COLORS, PRESET_PLAYER_COLORS) are kept here
 * because they are React-only values not derived from C# models.
 *
 * Generated types come from: Catan3.Shared/TypeScript/TypeGenRunner
 * Regenerate with: pwsh ./catan.ps1 generate-types
 */

// Re-export generated types as the single source of truth
export type { PlayerProfile } from './generated/models/player-profile';
export type { PlayerColors } from './generated/models/player-colors';
export type { LifetimeStats } from './generated/models/lifetime-stats';
export type { GameStats } from './generated/models/game-stats';

import type { PlayerColors } from './generated/models/player-colors';

/**
 * Default player colors when none specified.
 * Only core color fields needed — svgGradientStops/cssGradient are server-computed.
 */
export const DEFAULT_PLAYER_COLORS = {
  primary: '#4A90A4',
  secondary: '#357A8C',
  foreground: '#FFFFFF',
} as PlayerColors;

/**
 * Preset player colors for selection UI.
 * Each represents a distinct player color that works well visually.
 */
export const PRESET_PLAYER_COLORS: PlayerColors[] = [
  // Classic Catan player colors (from database seed)
  { primary: '#0000FF', secondary: '#000080', foreground: '#FFFFFF' } as PlayerColors, // Blue
  { primary: '#FF0000', secondary: '#800000', foreground: '#FFFFFF' } as PlayerColors, // Red
  { primary: '#008000', secondary: '#004000', foreground: '#FFFFFF' } as PlayerColors, // Green
  { primary: '#800080', secondary: '#400040', foreground: '#FFFFFF' } as PlayerColors, // Purple
  { primary: '#000000', secondary: '#333333', foreground: '#FFFFFF' } as PlayerColors, // Black
  // Extended palette
  { primary: '#E63946', secondary: '#C1121F', foreground: '#FFFFFF' } as PlayerColors, // Crimson
  { primary: '#2A9D8F', secondary: '#1D7A6D', foreground: '#FFFFFF' } as PlayerColors, // Teal
  { primary: '#E9C46A', secondary: '#D4A843', foreground: '#1A1A1A' } as PlayerColors, // Gold
  { primary: '#264653', secondary: '#1A3540', foreground: '#FFFFFF' } as PlayerColors, // Navy
  { primary: '#F4A261', secondary: '#E07B39', foreground: '#1A1A1A' } as PlayerColors, // Orange
  { primary: '#9B5DE5', secondary: '#7B2CBF', foreground: '#FFFFFF' } as PlayerColors, // Violet
  { primary: '#2E7D32', secondary: '#1B5E20', foreground: '#FFFFFF' } as PlayerColors, // Forest
  { primary: '#E91E63', secondary: '#C2185B', foreground: '#FFFFFF' } as PlayerColors, // Pink
  { primary: '#42A5F5', secondary: '#1E88E5', foreground: '#FFFFFF' } as PlayerColors, // Sky
  { primary: '#795548', secondary: '#5D4037', foreground: '#FFFFFF' } as PlayerColors, // Brown
  { primary: '#8BC34A', secondary: '#689F38', foreground: '#1A1A1A' } as PlayerColors, // Lime
  { primary: '#607D8B', secondary: '#455A64', foreground: '#FFFFFF' } as PlayerColors, // Slate
];
