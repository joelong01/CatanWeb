/**
 * Hex grid geometry utilities.
 *
 * Based on Red Blob Games hex grid formulas (flat-top hexagons).
 * Ported from C# BoardGeometry.cs and HexCoordinates.cs.
 *
 * @see https://www.redblobgames.com/grids/hexagons/
 */

/**
 * Hex dimensions for flat-top hexagons.
 */
export interface HexDimensions {
  /** Circumradius (center to vertex) */
  size: number;
  /** Full width (2 × size) */
  width: number;
  /** Full height (sqrt(3) × size) */
  height: number;
  /** Aspect ratio (height/width ≈ 0.866) */
  aspectRatio: number;
  /** Gap between hex edges (stroke thickness) */
  gap: number;
}

/**
 * Calculate hex dimensions from circumradius.
 *
 * For flat-top hexagons:
 * - width = 2 × size
 * - height = sqrt(3) × size
 * - aspectRatio = height/width = sqrt(3)/2 ≈ 0.866
 *
 * @param size - Circumradius (distance from center to vertex)
 * @param gap - Gap between hex edges (default: 2px)
 *
 * @example
 * ```typescript
 * const dims = calculateHexDimensions(100);
 * // dims.width = 200
 * // dims.height ≈ 173.2 (100 × sqrt(3))
 * // dims.aspectRatio ≈ 0.866
 * ```
 */
export function calculateHexDimensions(size: number, gap: number = 2): HexDimensions {
  const width = 2 * size;
  const height = Math.sqrt(3) * size;

  return {
    size,
    width,
    height,
    aspectRatio: height / width,
    gap,
  };
}

/**
 * Axial hex coordinates (Q, R).
 *
 * Q = column offset (increases to the right)
 * R = row offset (increases down-right)
 *
 * Cube coordinates: Q + R + S = 0
 */
export interface HexCoordinate {
  q: number;
  r: number;
}

/**
 * Pixel position (x, y).
 */
export interface PixelPosition {
  x: number;
  y: number;
}

/**
 * Convert hex coordinates to pixel position (flat-top).
 *
 * Formula from Red Blob Games:
 * - x = size × 1.5 × Q + origin.x
 * - y = size × sqrt(3) × (R + Q/2) + origin.y
 *
 * @param coord - Hex coordinate (q, r)
 * @param size - Hex circumradius
 * @param origin - Origin offset (default: 0, 0)
 *
 * @example
 * ```typescript
 * // Center hex at origin
 * const center = hexToPixel({ q: 0, r: 0 }, 100);
 * // center = { x: 0, y: 0 }
 *
 * // Hex to the east (q=1, r=0)
 * const east = hexToPixel({ q: 1, r: 0 }, 100);
 * // east = { x: 150, y: 86.6 }
 *
 * // With origin offset
 * const offset = hexToPixel({ q: 0, r: 0 }, 100, { x: 500, y: 400 });
 * // offset = { x: 500, y: 400 }
 * ```
 */
export function hexToPixel(
  coord: HexCoordinate,
  size: number,
  origin: PixelPosition = { x: 0, y: 0 }
): PixelPosition {
  const x = size * 1.5 * coord.q + origin.x;
  const y = size * Math.sqrt(3) * (coord.r + coord.q / 2.0) + origin.y;

  return { x, y };
}

/**
 * Predefined hex grid layouts (center + surrounding hexes).
 *
 * @example
 * ```typescript
 * // Iterate over a 7-hex cluster
 * HEX_LAYOUTS.CLUSTER_7.forEach((coord, index) => {
 *   const pos = hexToPixel(coord, 100);
 *   console.log(`Hex ${index}: (${coord.q}, ${coord.r}) -> (${pos.x}, ${pos.y})`);
 * });
 *
 * // Create items for HexGrid from a layout
 * const items = HEX_LAYOUTS.CLUSTER_7.map((coord, i) => ({
 *   id: `hex-${i}`,
 *   coord,
 *   content: <div>Hex {i}</div>,
 * }));
 * ```
 */
export const HEX_LAYOUTS = {
  /**
   * 7 hexes: center + 6 surrounding (classic Catan tile cluster).
   *
   * Layout:
   *       [1]
   *    [6] [0] [2]
   *       [5] [3]
   *          [4]
   */
  CLUSTER_7: [
    { q: 0, r: 0 },   // [0] Center
    { q: 0, r: -1 },  // [1] North
    { q: 1, r: -1 },  // [2] NorthEast
    { q: 1, r: 0 },   // [3] SouthEast
    { q: 0, r: 1 },   // [4] South
    { q: -1, r: 1 },  // [5] SouthWest
    { q: -1, r: 0 },  // [6] NorthWest
  ] as const,

  /**
   * 19 hexes: standard Catan board (3-4 players).
   *
   * Layout:
   *         [0] [1] [2]
   *      [3] [4] [5] [6]
   *   [7] [8] [9] [10] [11]
   *      [12] [13] [14] [15]
   *         [16] [17] [18]
   */
  CLUSTER_19: [
    // Row -2
    { q: 0, r: -2 }, { q: 1, r: -2 }, { q: 2, r: -2 },
    // Row -1
    { q: -1, r: -1 }, { q: 0, r: -1 }, { q: 1, r: -1 }, { q: 2, r: -1 },
    // Row 0 (center)
    { q: -2, r: 0 }, { q: -1, r: 0 }, { q: 0, r: 0 }, { q: 1, r: 0 }, { q: 2, r: 0 },
    // Row 1
    { q: -2, r: 1 }, { q: -1, r: 1 }, { q: 0, r: 1 }, { q: 1, r: 1 },
    // Row 2
    { q: -2, r: 2 }, { q: -1, r: 2 }, { q: 0, r: 2 },
  ] as const,

  /**
   * 30 hexes: expansion board (5-6 players).
   * Matches ExpansionBoardInfo.cs TileKeys layout.
   *
   * Layout (4-5-6-6-5-4 rows):
   *         [0]  [1]  [2]  [3]
   *      [4]  [5]  [6]  [7]  [8]
   *   [9] [10] [11] [12] [13] [14]
   *   [15] [16] [17] [18] [19] [20]
   *      [21] [22] [23] [24] [25]
   *         [26] [27] [28] [29]
   */
  CLUSTER_30: [
    // Row -2 (4 hexes)
    { q: 0, r: -2 }, { q: 1, r: -2 }, { q: 2, r: -2 }, { q: 3, r: -2 },
    // Row -1 (5 hexes)
    { q: -1, r: -1 }, { q: 0, r: -1 }, { q: 1, r: -1 }, { q: 2, r: -1 }, { q: 3, r: -1 },
    // Row 0 (6 hexes)
    { q: -2, r: 0 }, { q: -1, r: 0 }, { q: 0, r: 0 }, { q: 1, r: 0 }, { q: 2, r: 0 }, { q: 3, r: 0 },
    // Row 1 (6 hexes)
    { q: -3, r: 1 }, { q: -2, r: 1 }, { q: -1, r: 1 }, { q: 0, r: 1 }, { q: 1, r: 1 }, { q: 2, r: 1 },
    // Row 2 (5 hexes)
    { q: -3, r: 2 }, { q: -2, r: 2 }, { q: -1, r: 2 }, { q: 0, r: 2 }, { q: 1, r: 2 },
    // Row 3 (4 hexes)
    { q: -3, r: 3 }, { q: -2, r: 3 }, { q: -1, r: 3 }, { q: 0, r: 3 },
  ] as const,
} as const;
