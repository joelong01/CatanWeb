/**
 * Truth set for Tests/Data/Expansion.catan_test
 *
 * These values were independently verified from the C#-serialized JSON.
 * They are used by the serialization contract tests to verify that the
 * generated TypeScript types correctly deserialize the wire format.
 *
 * If a test fails against this truth set, either:
 * 1. The generated types have drifted from the wire format, or
 * 2. An extension function has a computation bug.
 *
 * @module expansion-board-truth
 */

export const EXPANSION_BOARD_TRUTH = {
  /** Total number of tiles on the expansion board */
  tileCount: 30,

  /** Number of players in this game */
  playerCount: 5,

  /** Number of harbors */
  harborCount: 11,

  /** Number of building slots */
  buildingCount: 80,

  /** Number of road slots */
  roadCount: 109,

  /** Number of desert tiles */
  desertCount: 2,

  /** Resource distribution across all tiles */
  resourceCounts: {
    Ore: 5,
    Wheat: 5,
    Sheep: 6,
    Wood: 6,
    Brick: 6,
    Desert: 2,
  } as Record<string, number>,

  /** Number of tiles with dice number 6 */
  tilesWithSix: 3,

  /** Number of tiles with dice number 8 */
  tilesWithEight: 3,

  /** Known facts about the first tile (index 0) */
  firstTile: {
    q: -3,
    r: 1,
    resource: 'Ore' as const,
    number: 4,
  },

  /** Known facts about the tile at origin (0,0,0) */
  tileAtOrigin: {
    resource: 'Ore' as const,
    number: 9,
    computedStars: 4,
  },

  /** Total pip count (stars) computed from all tile numbers */
  totalComputedStars: 88,

  /** Player names in order */
  playerNames: ['Joe', 'Dodgy', 'Doug', 'Ryan', 'Adrian'],

  /** Game type */
  gameType: 'Expansion',

  /**
   * The tiles[0] object in the JSON must NOT have a 'stars' property.
   * Stars is [JsonIgnore] in C# — it's computed, never serialized.
   */
  tilesHaveNoStarsProperty: true,
} as const;
