/**
 * Player profile types for persistent player data.
 * These represent players stored in the database, not in-game player state.
 */

/**
 * Player color scheme for UI display.
 */
export interface PlayerColors {
  /** Primary background color (hex) */
  primary: string;
  /** Secondary background color for gradients (hex) */
  secondary: string;
  /** Foreground/text color (hex) */
  foreground: string;
}

/**
 * Default player colors when none specified.
 */
export const DEFAULT_PLAYER_COLORS: PlayerColors = {
  primary: '#4A90A4',
  secondary: '#357A8C',
  foreground: '#FFFFFF',
};

/**
 * Player profile data from the database.
 * Separate from PlayerModel (game state) - this is for identity, colors, statistics.
 */
export interface PlayerProfile {
  /** Unique identifier for the player */
  id: string;
  /** Display name of the player */
  name: string;
  /** Player color scheme */
  colors: PlayerColors;
  /** Relative URI to player image (e.g., "/images/players/joe.jpg") */
  imageUri?: string;
  /** Lifetime statistics (optional) */
  lifetimeStats?: LifetimeStats;
}

/**
 * Lifetime statistics for a player across all games.
 */
export interface LifetimeStats {
  /** Schema version for migrations */
  schemaVersion: number;
  /** Total aggregated game stats */
  totals: GameStats;
  /** Per-game type statistics */
  byGameType?: Record<string, GameStats>;
}

/**
 * Statistics for games played.
 */
export interface GameStats {
  /** Schema version for migrations */
  schemaVersion: number;
  /** Total games played */
  gamesPlayed: number;
  /** Total games won */
  gamesWon: number;
  /** Total victory points earned */
  totalVictoryPoints: number;
  /** Highest score in a single game */
  highestScore: number;
  /** Total longest road achievements */
  longestRoadCount: number;
  /** Total largest army achievements */
  largestArmyCount: number;
}

/**
 * Preset player colors for selection UI.
 * Each represents a distinct player color that works well visually.
 */
export const PRESET_PLAYER_COLORS: PlayerColors[] = [
  // Classic Catan player colors (from database seed)
  { primary: '#0000FF', secondary: '#000080', foreground: '#FFFFFF' }, // Blue
  { primary: '#FF0000', secondary: '#800000', foreground: '#FFFFFF' }, // Red
  { primary: '#008000', secondary: '#004000', foreground: '#FFFFFF' }, // Green
  { primary: '#800080', secondary: '#400040', foreground: '#FFFFFF' }, // Purple
  { primary: '#000000', secondary: '#333333', foreground: '#FFFFFF' }, // Black
  // Extended palette
  { primary: '#E63946', secondary: '#C1121F', foreground: '#FFFFFF' }, // Crimson
  { primary: '#2A9D8F', secondary: '#1D7A6D', foreground: '#FFFFFF' }, // Teal
  { primary: '#E9C46A', secondary: '#D4A843', foreground: '#1A1A1A' }, // Gold
  { primary: '#264653', secondary: '#1A3540', foreground: '#FFFFFF' }, // Navy
  { primary: '#F4A261', secondary: '#E07B39', foreground: '#1A1A1A' }, // Orange
  { primary: '#9B5DE5', secondary: '#7B2CBF', foreground: '#FFFFFF' }, // Violet
  { primary: '#2E7D32', secondary: '#1B5E20', foreground: '#FFFFFF' }, // Forest
  { primary: '#E91E63', secondary: '#C2185B', foreground: '#FFFFFF' }, // Pink
  { primary: '#42A5F5', secondary: '#1E88E5', foreground: '#FFFFFF' }, // Sky
  { primary: '#795548', secondary: '#5D4037', foreground: '#FFFFFF' }, // Brown
  { primary: '#8BC34A', secondary: '#689F38', foreground: '#1A1A1A' }, // Lime
  { primary: '#607D8B', secondary: '#455A64', foreground: '#FFFFFF' }, // Slate
];
