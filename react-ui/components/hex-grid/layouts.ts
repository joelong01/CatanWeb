/**
 * HexGrid Layout Utilities
 *
 * Provides named layout patterns for flat-top hex grids.
 * Each layout returns an array of HexCoordinates that can be mapped to content.
 *
 * Layout types:
 * - **Square**: Rectangular grid (columns × rows). E.g., "3x4" = 3 columns, 4 rows
 * - **Radial**: Concentric rings from center. E.g., rings=2 = center + 6 + 12 = 19 hexes
 * - **Diamond**: Diamond shape with specified radius
 *
 * For flat-top hexes:
 * - Columns stack vertically (same q, increasing r)
 * - Adjacent columns offset by half hex height
 * - To align columns vertically, even columns (q=0,2,4...) align, odd columns offset
 */

import type { HexCoordinate } from './hex-geometry';
import { cubicCoord } from './hex-geometry';

// ============================================================================
// Square Layout
// ============================================================================

export interface SquareLayoutOptions {
  /** Number of columns (horizontal) */
  columns: number;
  /** Number of rows (vertical) */
  rows: number;
  /**
   * Column height pattern. If provided, overrides uniform rows.
   * E.g., [4, 3, 4] for a 4-3-4 pattern (3 columns with heights 4, 3, 4)
   */
  columnHeights?: number[];
}

/**
 * Generate coordinates for a square/rectangular hex grid layout.
 *
 * For flat-top hexes, columns stack vertically. Even columns (0, 2, 4...)
 * align at the top, odd columns (1, 3, 5...) are offset down by half a hex.
 *
 * @example
 * // 3x4 uniform grid (3 columns, 4 rows each)
 * const coords = squareLayout({ columns: 3, rows: 4 });
 *
 * @example
 * // 4-3-4 pattern (3 columns with heights 4, 3, 4)
 * const coords = squareLayout({ columns: 3, rows: 4, columnHeights: [4, 3, 4] });
 */
export function squareLayout(options: SquareLayoutOptions): HexCoordinate[] {
  const { columns, rows, columnHeights } = options;
  const coords: HexCoordinate[] = [];

  for (let col = 0; col < columns; col++) {
    const colHeight = columnHeights?.[col] ?? rows;

    // For flat-top hexes: y = sqrt(3) * size * (r + q/2)
    // To align even columns (q=0,2,4) at same y, we need r offset based on q
    // Column 0 (q=0): r starts at 0
    // Column 1 (q=1): naturally offset by 0.5 hex height (no adjustment needed)
    // Column 2 (q=2): r starts at -1 to align with column 0
    // Column 3 (q=3): r starts at -1, naturally offset by 0.5
    // Pattern: r_start = -floor(col/2)
    const rStart = -Math.floor(col / 2);

    for (let row = 0; row < colHeight; row++) {
      coords.push(cubicCoord(col, rStart + row));
    }
  }

  return coords;
}

/**
 * Shorthand for square layout with column heights pattern.
 *
 * @example
 * // 4-3-4 column pattern
 * const coords = columnPattern([4, 3, 4]);
 */
export function columnPattern(heights: number[]): HexCoordinate[] {
  return squareLayout({
    columns: heights.length,
    rows: Math.max(...heights),
    columnHeights: heights,
  });
}

// ============================================================================
// Radial Layout
// ============================================================================

export interface RadialLayoutOptions {
  /** Number of rings around the center (0 = just center, 1 = center + 6, 2 = center + 6 + 12) */
  rings: number;
  /** Whether to include the center hex (default: true) */
  includeCenter?: boolean;
}

/**
 * Direction vectors for the 6 neighbors of a hex (flat-top orientation).
 */
const DIRECTIONS: HexCoordinate[] = [
  { q: 1, r: 0, s: -1 },   // East
  { q: 1, r: -1, s: 0 },   // Northeast
  { q: 0, r: -1, s: 1 },   // Northwest
  { q: -1, r: 0, s: 1 },   // West
  { q: -1, r: 1, s: 0 },   // Southwest
  { q: 0, r: 1, s: -1 },   // Southeast
];

/**
 * Generate coordinates for a radial hex grid layout (concentric rings).
 *
 * Ring 0: Center only (1 hex)
 * Ring 1: Center + 6 neighbors (7 hexes)
 * Ring 2: Center + 6 + 12 (19 hexes)
 * Ring n: Total = 1 + 3*n*(n+1) hexes
 *
 * @example
 * // Center + 1 ring (7 hexes)
 * const coords = radialLayout({ rings: 1 });
 *
 * @example
 * // 2 rings without center (18 hexes)
 * const coords = radialLayout({ rings: 2, includeCenter: false });
 */
export function radialLayout(options: RadialLayoutOptions): HexCoordinate[] {
  const { rings, includeCenter = true } = options;
  const coords: HexCoordinate[] = [];

  if (includeCenter) {
    coords.push(cubicCoord(0, 0));
  }

  // Generate each ring
  for (let ring = 1; ring <= rings; ring++) {
    // Start at the "east" position of this ring
    let q = ring;
    let r = 0;

    // Walk around the ring in 6 directions
    for (let dir = 0; dir < 6; dir++) {
      // Each side of the ring has 'ring' hexes
      for (let step = 0; step < ring; step++) {
        coords.push(cubicCoord(q, r));
        // Move in the current direction
        q += DIRECTIONS[(dir + 2) % 6].q;
        r += DIRECTIONS[(dir + 2) % 6].r;
      }
    }
  }

  return coords;
}

/**
 * Get just a single ring (not including inner rings).
 *
 * @example
 * // Just the outer ring at distance 2 (12 hexes)
 * const coords = singleRing(2);
 */
export function singleRing(radius: number): HexCoordinate[] {
  if (radius === 0) {
    return [cubicCoord(0, 0)];
  }

  const coords: HexCoordinate[] = [];
  let q = radius;
  let r = 0;

  for (let dir = 0; dir < 6; dir++) {
    for (let step = 0; step < radius; step++) {
      coords.push(cubicCoord(q, r));
      q += DIRECTIONS[(dir + 2) % 6].q;
      r += DIRECTIONS[(dir + 2) % 6].r;
    }
  }

  return coords;
}

// ============================================================================
// Diamond Layout
// ============================================================================

export interface DiamondLayoutOptions {
  /** Radius of the diamond (0 = 1 hex, 1 = 7 hexes, 2 = 19 hexes) */
  radius: number;
}

/**
 * Generate coordinates for a diamond-shaped hex grid.
 *
 * This is essentially the same as radialLayout but named for clarity
 * when used for game board shapes.
 *
 * @example
 * // Standard Catan board size (radius 2 = 19 land tiles)
 * const coords = diamondLayout({ radius: 2 });
 */
export function diamondLayout(options: DiamondLayoutOptions): HexCoordinate[] {
  return radialLayout({ rings: options.radius, includeCenter: true });
}

// ============================================================================
// Preset Layouts
// ============================================================================

/**
 * Common preset layouts for quick access.
 */
export const LAYOUTS = {
  /** Single hex */
  SINGLE: (): HexCoordinate[] => [cubicCoord(0, 0)],

  /** 7-hex cluster (center + 6 neighbors) */
  CLUSTER_7: (): HexCoordinate[] => radialLayout({ rings: 1 }),

  /** 19-hex cluster (center + 6 + 12) */
  CLUSTER_19: (): HexCoordinate[] => radialLayout({ rings: 2 }),

  /** Just the outer ring of 6 */
  RING_6: (): HexCoordinate[] => singleRing(1),

  /** Just the outer ring of 12 */
  RING_12: (): HexCoordinate[] => singleRing(2),

  /** 3x3 square grid (9 hexes) */
  SQUARE_3x3: (): HexCoordinate[] => squareLayout({ columns: 3, rows: 3 }),

  /** 3x4 square grid (12 hexes) */
  SQUARE_3x4: (): HexCoordinate[] => squareLayout({ columns: 3, rows: 4 }),

  /** 4-3-4 column pattern (11 hexes) - used for RollRing */
  COLUMNS_4_3_4: (): HexCoordinate[] => columnPattern([4, 3, 4]),

  /** 3-4-3 column pattern (10 hexes) */
  COLUMNS_3_4_3: (): HexCoordinate[] => columnPattern([3, 4, 3]),
} as const;

// ============================================================================
// Utility Functions
// ============================================================================

/**
 * Parse a layout string like "3x4" into columns and rows.
 *
 * @example
 * parseSquareLayout("3x4") // { columns: 3, rows: 4 }
 */
export function parseSquareLayout(spec: string): { columns: number; rows: number } | null {
  const match = spec.match(/^(\d+)x(\d+)$/);
  if (!match) return null;
  return {
    columns: parseInt(match[1], 10),
    rows: parseInt(match[2], 10),
  };
}

/**
 * Get bounding box of a set of coordinates.
 */
export function getBounds(coords: HexCoordinate[]): {
  minQ: number;
  maxQ: number;
  minR: number;
  maxR: number;
} {
  if (coords.length === 0) {
    return { minQ: 0, maxQ: 0, minR: 0, maxR: 0 };
  }

  let minQ = coords[0].q;
  let maxQ = coords[0].q;
  let minR = coords[0].r;
  let maxR = coords[0].r;

  for (const coord of coords) {
    minQ = Math.min(minQ, coord.q);
    maxQ = Math.max(maxQ, coord.q);
    minR = Math.min(minR, coord.r);
    maxR = Math.max(maxR, coord.r);
  }

  return { minQ, maxQ, minR, maxR };
}
