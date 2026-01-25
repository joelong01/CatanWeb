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
 * Cubic hex coordinates (Q, R, S) with constraint Q + R + S = 0.
 *
 * Consistent with C# HexCoordinates class.
 *
 * Q = column offset (increases to the right)
 * R = row offset (increases down-right)
 * S = computed (-q - r)
 */
export interface HexCoordinate {
  q: number;
  r: number;
  s: number;
}

/**
 * Create a cubic HexCoordinate from q and r (computes s automatically).
 *
 * @example
 * ```typescript
 * const center = cubicCoord(0, 0);  // { q: 0, r: 0, s: 0 }
 * const east = cubicCoord(1, 0);    // { q: 1, r: 0, s: -1 }
 * ```
 */
export function cubicCoord(q: number, r: number): HexCoordinate {
  // Use `|| 0` to normalize -0 to 0 (JavaScript quirk: -0 - 0 = -0)
  return { q, r, s: (-q - r) || 0 };
}

/**
 * Direction vectors for the 6 hex neighbors.
 * Matches C# HexCoordinates.cs direction definitions.
 */
export const DIRECTIONS = {
  North:     { q: 0, r: -1, s: 1 },
  NorthEast: { q: 1, r: -1, s: 0 },
  SouthEast: { q: 1, r: 0, s: -1 },
  South:     { q: 0, r: 1, s: -1 },
  SouthWest: { q: -1, r: 1, s: 0 },
  NorthWest: { q: -1, r: 0, s: 1 },
} as const;

export type Direction = keyof typeof DIRECTIONS;

/**
 * Manhattan distance between two hexes using cubic coordinates.
 */
export function distance(a: HexCoordinate, b: HexCoordinate): number {
  return (Math.abs(a.q - b.q) + Math.abs(a.r - b.r) + Math.abs(a.s - b.s)) / 2;
}

/**
 * Get the neighboring hex coordinate in a given direction.
 */
export function getNeighbor(coord: HexCoordinate, dir: Direction): HexCoordinate {
  const d = DIRECTIONS[dir];
  return { q: coord.q + d.q, r: coord.r + d.r, s: coord.s + d.s };
}

/**
 * Get all 6 neighboring hex coordinates.
 */
export function getAllNeighbors(coord: HexCoordinate): HexCoordinate[] {
  return (Object.keys(DIRECTIONS) as Direction[]).map(dir => getNeighbor(coord, dir));
}

/**
 * Check if two hexes are adjacent (distance === 1).
 */
export function isAdjacent(a: HexCoordinate, b: HexCoordinate): boolean {
  return distance(a, b) === 1;
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
 * Generate spiral coordinates for N items (center + surrounding rings).
 *
 * Creates coordinates in spiral order from center outward:
 * - Ring 0: Center (1 hex)
 * - Ring 1: 6 hexes clockwise from North
 * - Ring 2: 12 hexes
 * - Ring 3: 18 hexes
 * etc.
 *
 * @param count - Number of coordinates to generate
 *
 * @example
 * ```typescript
 * const coords = getSpiralCoordinates(7);  // Center + 6 surrounding
 * const coords19 = getSpiralCoordinates(19);  // Standard Catan board
 * ```
 */
export function getSpiralCoordinates(count: number): HexCoordinate[] {
  if (count <= 0) return [];

  const coords: HexCoordinate[] = [cubicCoord(0, 0)]; // Center
  if (count === 1) return coords;

  let ring = 1;
  while (coords.length < count) {
    // Start at "north" of ring (q=0, r=-ring)
    let current = cubicCoord(0, -ring);

    // Walk around the ring clockwise
    const walkDirections: Direction[] = [
      'SouthEast', 'South', 'SouthWest', 'NorthWest', 'North', 'NorthEast'
    ];

    for (const dir of walkDirections) {
      for (let step = 0; step < ring && coords.length < count; step++) {
        coords.push(current);
        current = getNeighbor(current, dir);
      }
    }
    ring++;
  }

  return coords;
}

// =============================================================================
// Vertex and Edge Positions (for game board elements)
// =============================================================================

/**
 * Hex vertex positions (corners where buildings are placed).
 * For flat-top hexagons, vertices are at 0°, 60°, 120°, 180°, 240°, 300°.
 */
export type HexPosition = 'Right' | 'BottomRight' | 'BottomLeft' | 'Left' | 'TopLeft' | 'TopRight';

/**
 * Angles for each vertex position (degrees, 0° = right/east).
 */
export const VERTEX_ANGLES: Record<HexPosition, number> = {
  Right: 0,
  BottomRight: 60,
  BottomLeft: 120,
  Left: 180,
  TopLeft: 240,
  TopRight: 300,
};

/**
 * Get pixel position for a hex vertex (where buildings are placed).
 * Vertices are at circumradius distance from hex center.
 *
 * @param coord - Hex coordinate
 * @param position - Which vertex
 * @param size - Hex circumradius
 * @param origin - Origin offset (default: 0, 0)
 */
export function getVertexPosition(
  coord: HexCoordinate,
  position: HexPosition,
  size: number,
  origin: PixelPosition = { x: 0, y: 0 }
): PixelPosition {
  const center = hexToPixel(coord, size, origin);
  const angleRad = VERTEX_ANGLES[position] * Math.PI / 180;
  return {
    x: center.x + size * Math.cos(angleRad),
    y: center.y + size * Math.sin(angleRad),
  };
}

/**
 * Hex edge/side identifiers (where roads are placed).
 * For flat-top hexagons.
 */
export type HexSide = 'Top' | 'TopRight' | 'BottomRight' | 'Bottom' | 'BottomLeft' | 'TopLeft';

/**
 * Angles for each edge (degrees, direction of edge line).
 */
export const EDGE_ANGLES: Record<HexSide, number> = {
  Top: 0,
  TopRight: 60,
  BottomRight: 120,
  Bottom: 180,
  BottomLeft: 240,
  TopLeft: 300,
};

/**
 * Get pixel position for a hex edge midpoint (where roads are placed).
 * Edge midpoints are at apothem distance from hex center.
 *
 * @param coord - Hex coordinate
 * @param side - Which edge
 * @param size - Hex circumradius
 * @param origin - Origin offset (default: 0, 0)
 */
export function getEdgeMidpoint(
  coord: HexCoordinate,
  side: HexSide,
  size: number,
  origin: PixelPosition = { x: 0, y: 0 }
): PixelPosition {
  const center = hexToPixel(coord, size, origin);
  const apothem = size * Math.sqrt(3) / 2;
  // Edge midpoint direction is perpendicular to edge (edge angle - 90°)
  const angleRad = (EDGE_ANGLES[side] - 90) * Math.PI / 180;
  return {
    x: center.x + apothem * Math.cos(angleRad),
    y: center.y + apothem * Math.sin(angleRad),
  };
}

// =============================================================================
// Predefined Layouts
// =============================================================================

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
    { q: 0, r: 0, s: 0 },    // [0] Center
    { q: 0, r: -1, s: 1 },   // [1] North
    { q: 1, r: -1, s: 0 },   // [2] NorthEast
    { q: 1, r: 0, s: -1 },   // [3] SouthEast
    { q: 0, r: 1, s: -1 },   // [4] South
    { q: -1, r: 1, s: 0 },   // [5] SouthWest
    { q: -1, r: 0, s: 1 },   // [6] NorthWest
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
    { q: 0, r: -2, s: 2 }, { q: 1, r: -2, s: 1 }, { q: 2, r: -2, s: 0 },
    // Row -1
    { q: -1, r: -1, s: 2 }, { q: 0, r: -1, s: 1 }, { q: 1, r: -1, s: 0 }, { q: 2, r: -1, s: -1 },
    // Row 0 (center)
    { q: -2, r: 0, s: 2 }, { q: -1, r: 0, s: 1 }, { q: 0, r: 0, s: 0 }, { q: 1, r: 0, s: -1 }, { q: 2, r: 0, s: -2 },
    // Row 1
    { q: -2, r: 1, s: 1 }, { q: -1, r: 1, s: 0 }, { q: 0, r: 1, s: -1 }, { q: 1, r: 1, s: -2 },
    // Row 2
    { q: -2, r: 2, s: 0 }, { q: -1, r: 2, s: -1 }, { q: 0, r: 2, s: -2 },
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
    { q: 0, r: -2, s: 2 }, { q: 1, r: -2, s: 1 }, { q: 2, r: -2, s: 0 }, { q: 3, r: -2, s: -1 },
    // Row -1 (5 hexes)
    { q: -1, r: -1, s: 2 }, { q: 0, r: -1, s: 1 }, { q: 1, r: -1, s: 0 }, { q: 2, r: -1, s: -1 }, { q: 3, r: -1, s: -2 },
    // Row 0 (6 hexes)
    { q: -2, r: 0, s: 2 }, { q: -1, r: 0, s: 1 }, { q: 0, r: 0, s: 0 }, { q: 1, r: 0, s: -1 }, { q: 2, r: 0, s: -2 }, { q: 3, r: 0, s: -3 },
    // Row 1 (6 hexes)
    { q: -3, r: 1, s: 2 }, { q: -2, r: 1, s: 1 }, { q: -1, r: 1, s: 0 }, { q: 0, r: 1, s: -1 }, { q: 1, r: 1, s: -2 }, { q: 2, r: 1, s: -3 },
    // Row 2 (5 hexes)
    { q: -3, r: 2, s: 1 }, { q: -2, r: 2, s: 0 }, { q: -1, r: 2, s: -1 }, { q: 0, r: 2, s: -2 }, { q: 1, r: 2, s: -3 },
    // Row 3 (4 hexes)
    { q: -3, r: 3, s: 0 }, { q: -2, r: 3, s: -1 }, { q: -1, r: 3, s: -2 }, { q: 0, r: 3, s: -3 },
  ] as const,
} as const;
