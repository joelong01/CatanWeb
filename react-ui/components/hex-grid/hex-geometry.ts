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
  return { q, r, s: -q - r || 0 };
}

/**
 * Direction enum for the 6 hex neighbor directions.
 * Matches C# GameEnums.cs Direction enum.
 */
export enum Direction {
  North = 'North',
  NorthEast = 'NorthEast',
  SouthEast = 'SouthEast',
  South = 'South',
  SouthWest = 'SouthWest',
  NorthWest = 'NorthWest',
}

/**
 * Direction vectors mapping each direction to its coordinate offset.
 * Matches C# HexCoordinates.cs Directions dictionary.
 */
export const DIRECTION_VECTORS: Record<Direction, HexCoordinate> = {
  [Direction.North]: { q: 0, r: -1, s: 1 },
  [Direction.NorthEast]: { q: 1, r: -1, s: 0 },
  [Direction.SouthEast]: { q: 1, r: 0, s: -1 },
  [Direction.South]: { q: 0, r: 1, s: -1 },
  [Direction.SouthWest]: { q: -1, r: 1, s: 0 },
  [Direction.NorthWest]: { q: -1, r: 0, s: 1 },
};

/**
 * All directions in clockwise order starting from North.
 * Useful for iteration over all 6 directions.
 */
export const ALL_DIRECTIONS: readonly Direction[] = [
  Direction.North,
  Direction.NorthEast,
  Direction.SouthEast,
  Direction.South,
  Direction.SouthWest,
  Direction.NorthWest,
];

/**
 * Maps HexSide (screen-relative edge names) to Direction (compass neighbor directions).
 * Matches C# HexCoordinates.cs SideToDirection dictionary.
 */
export const SIDE_TO_DIRECTION: Record<HexSide, Direction> = {
  Top: Direction.North,
  TopRight: Direction.NorthEast,
  BottomRight: Direction.SouthEast,
  Bottom: Direction.South,
  BottomLeft: Direction.SouthWest,
  TopLeft: Direction.NorthWest,
};

/**
 * Maps Direction (compass neighbor directions) to HexSide (screen-relative edge names).
 * Matches C# HexCoordinates.cs DirectionToSide dictionary.
 */
export const DIRECTION_TO_SIDE: Record<Direction, HexSide> = {
  [Direction.North]: 'Top',
  [Direction.NorthEast]: 'TopRight',
  [Direction.SouthEast]: 'BottomRight',
  [Direction.South]: 'Bottom',
  [Direction.SouthWest]: 'BottomLeft',
  [Direction.NorthWest]: 'TopLeft',
};

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
  const d = DIRECTION_VECTORS[dir];
  return { q: coord.q + d.q, r: coord.r + d.r, s: coord.s + d.s };
}

/**
 * Get all 6 neighboring hex coordinates.
 */
export function getAllNeighbors(coord: HexCoordinate): HexCoordinate[] {
  return ALL_DIRECTIONS.map((dir) => getNeighbor(coord, dir));
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
 * Round fractional cube coordinates to the nearest hex.
 *
 * Uses the cube rounding algorithm from Red Blob Games:
 * Round each component, then fix the one with the largest rounding error
 * to maintain the q + r + s = 0 constraint.
 *
 * @param q - Fractional q coordinate
 * @param r - Fractional r coordinate
 * @param s - Fractional s coordinate
 * @returns Rounded HexCoordinate
 *
 * @see https://www.redblobgames.com/grids/hexagons/#rounding
 */
function cubeRound(q: number, r: number, s: number): HexCoordinate {
  let rq = Math.round(q);
  let rr = Math.round(r);
  let rs = Math.round(s);

  const qDiff = Math.abs(rq - q);
  const rDiff = Math.abs(rr - r);
  const sDiff = Math.abs(rs - s);

  // Reset the component with the largest rounding error
  if (qDiff > rDiff && qDiff > sDiff) {
    rq = -rr - rs;
  } else if (rDiff > sDiff) {
    rr = -rq - rs;
  } else {
    rs = -rq - rr;
  }

  // Normalize -0 to 0
  return { q: rq || 0, r: rr || 0, s: rs || 0 };
}

/**
 * Convert pixel position to hex coordinate (flat-top).
 *
 * Inverse of hexToPixel. Uses cube rounding to find the nearest hex.
 * Ported from C# HexCoordinates.FromPixel().
 *
 * @param pixel - Pixel position to convert
 * @param size - Hex circumradius
 * @param origin - Origin offset (default: 0, 0)
 *
 * @example
 * ```typescript
 * // Round-trip conversion
 * const coord = cubicCoord(2, -1);
 * const pixel = hexToPixel(coord, 100);
 * const back = pixelToHex(pixel, 100);
 * // back equals coord
 *
 * // Click handling
 * const clickedHex = pixelToHex({ x: mouseX, y: mouseY }, hexSize, gridOrigin);
 * ```
 */
export function pixelToHex(
  pixel: PixelPosition,
  size: number,
  origin: PixelPosition = { x: 0, y: 0 }
): HexCoordinate {
  // Translate to coordinates relative to origin
  const relX = pixel.x - origin.x;
  const relY = pixel.y - origin.y;

  // Convert to fractional axial coordinates (inverse of hexToPixel formulas)
  const qf = relX / (1.5 * size);
  const rf = relY / (size * Math.sqrt(3)) - qf / 2.0;

  // Convert axial to cube (sf = -qf - rf)
  const sf = -qf - rf;

  // Round to nearest hex
  return cubeRound(qf, rf, sf);
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
export function getSpiralCoordinates(count: number, clockwise = true): HexCoordinate[] {
  if (count <= 0) return [];

  const coords: HexCoordinate[] = [cubicCoord(0, 0)]; // Center
  if (count === 1) return coords;

  let ring = 1;
  while (coords.length < count) {
    // Collect all positions in this ring going clockwise from North
    const ringCoords: HexCoordinate[] = [];
    let current = cubicCoord(0, -ring);
    const walkDirections: Direction[] = [
      Direction.SouthEast,
      Direction.South,
      Direction.SouthWest,
      Direction.NorthWest,
      Direction.North,
      Direction.NorthEast,
    ];
    for (const dir of walkDirections) {
      for (let step = 0; step < ring; step++) {
        ringCoords.push(current);
        current = getNeighbor(current, dir);
      }
    }

    // CCW: keep the starting (north) position, reverse the rest
    const ordered = clockwise ? ringCoords : [ringCoords[0], ...ringCoords.slice(1).reverse()];

    for (const coord of ordered) {
      if (coords.length >= count) break;
      coords.push(coord);
    }
    ring++;
  }

  return coords;
}

/**
 * Generate hex coordinates in a compact "square" column layout.
 *
 * Adjacent columns differ by at most 1 in height. Tries hill patterns
 * (center tallest) first, then valley (edges tallest), picking the
 * arrangement with the best pixel aspect ratio.
 *
 * Must match C# HexCoordinates.GenerateSquareCoordinates() exactly.
 *
 * @param count - Number of coordinates to generate
 *
 * @example
 * ```typescript
 * getSquareCoordinates(19)  // columns [3,4,5,4,3] — regular board shape
 * getSquareCoordinates(30)  // columns [3,4,5,6,5,4,3] — expansion board shape
 * getSquareCoordinates(11)  // columns [4,3,4] — RollRing shape
 * ```
 */
export function getSquareCoordinates(count: number): HexCoordinate[] {
  if (count <= 0) return [];
  if (count === 1) return [cubicCoord(0, 0)];

  const heights = computeSquareColumnHeights(count);
  return generateFromColumnHeights(heights);
}

/**
 * Compute column heights for a square layout. Exported for testing.
 */
export function computeSquareColumnHeights(count: number): number[] {
  if (count <= 0) return [];
  if (count === 1) return [1];

  let bestHeights = [count];
  let bestScore = Infinity;

  for (let c = 1; c <= count; c += 2) {
    const k = (c - 1) / 2;

    // Hill pattern: sum = c*base + k², base = (count - k²) / c
    if (k * k < count) {
      const remainder = count - k * k;
      if (remainder > 0 && remainder % c === 0) {
        const b = remainder / c;
        if (b >= 1) {
          const heights = buildHillHeights(c, b, k);
          const score = squarenessScore(heights);
          if (score < bestScore) {
            bestScore = score;
            bestHeights = heights;
          }
        }
      }
    }

    // Valley pattern: sum = c*peak - k², peak = (count + k²) / c
    if (k > 0) {
      const numerator = count + k * k;
      if (numerator % c === 0) {
        const p = numerator / c;
        if (p >= 1 && p - k >= 1) {
          const heights = buildValleyHeights(c, p, k);
          const score = squarenessScore(heights) + 0.001;
          if (score < bestScore) {
            bestScore = score;
            bestHeights = heights;
          }
        }
      }
    }

    if (c > count) break;
  }

  return bestHeights;
}

function buildHillHeights(columns: number, baseHeight: number, halfWidth: number): number[] {
  const heights: number[] = [];
  for (let i = 0; i < columns; i++) {
    heights.push(baseHeight + halfWidth - Math.abs(i - halfWidth));
  }
  return heights;
}

function buildValleyHeights(columns: number, peak: number, halfWidth: number): number[] {
  const heights: number[] = [];
  for (let i = 0; i < columns; i++) {
    heights.push(peak - halfWidth + Math.abs(i - halfWidth));
  }
  return heights;
}

function squarenessScore(heights: number[]): number {
  const c = heights.length;
  const maxH = Math.max(...heights);
  const minH = Math.min(...heights);
  const ratio = (c * 1.5) / (maxH * 1.732);
  let score = Math.abs(Math.log(ratio));
  if (minH < 2) score += 10.0; // Heavily penalize single-hex columns
  return score;
}

function generateFromColumnHeights(heights: number[]): HexCoordinate[] {
  const c = heights.length;
  const centerOffset = (c - 1) / 2;
  const coords: HexCoordinate[] = [];

  const q0 = -centerOffset;
  let rMin = Math.round((-q0 - heights[0] + 1) / 2);

  for (let i = 0; i < c; i++) {
    if (i > 0) {
      if (heights[i] > heights[i - 1]) {
        rMin--;
      }
    }

    const q = i - centerOffset;
    for (let r = rMin; r < rMin + heights[i]; r++) {
      coords.push(cubicCoord(q, r));
    }
  }

  return coords;
}

/**
 * Generate coordinates for a ring of hexes at a specific radius from a center.
 *
 * A ring at radius N contains exactly 6*N hexes (or 1 for radius 0).
 * Hexes are returned in clockwise order starting from North.
 *
 * @param center - Center hex coordinate
 * @param radius - Distance from center (0 = just center, 1 = 6 adjacent, etc.)
 *
 * @example
 * ```typescript
 * // Get the 6 hexes adjacent to origin
 * const ring1 = getRingCoordinates(cubicCoord(0, 0), 1);
 * // ring1.length === 6
 *
 * // Get water ring for standard Catan board (radius 3)
 * const waterRing = getRingCoordinates(cubicCoord(0, 0), 3);
 * // waterRing.length === 18
 * ```
 */
export function getRingCoordinates(center: HexCoordinate, radius: number): HexCoordinate[] {
  if (radius < 0) return [];
  if (radius === 0) return [center];

  const coords: HexCoordinate[] = [];

  // Start at North of ring: center + radius * North direction
  const northVector = DIRECTION_VECTORS[Direction.North];
  let current: HexCoordinate = {
    q: center.q + northVector.q * radius,
    r: center.r + northVector.r * radius,
    s: center.s + northVector.s * radius,
  };

  // Walk around the ring clockwise
  const walkDirections: Direction[] = [
    Direction.SouthEast,
    Direction.South,
    Direction.SouthWest,
    Direction.NorthWest,
    Direction.North,
    Direction.NorthEast,
  ];

  for (const dir of walkDirections) {
    for (let step = 0; step < radius; step++) {
      coords.push(current);
      current = getNeighbor(current, dir);
    }
  }

  return coords;
}

/**
 * Generate coordinates for hexes along a line between two points.
 *
 * Uses linear interpolation with cube rounding. The line includes both
 * endpoints and returns distance(start, end) + 1 hexes.
 *
 * @param start - Starting hex coordinate
 * @param end - Ending hex coordinate
 *
 * @example
 * ```typescript
 * // Line between adjacent hexes
 * const short = getLineCoordinates(cubicCoord(0, 0), cubicCoord(1, 0));
 * // short.length === 2 (start and end)
 *
 * // Longer diagonal line
 * const line = getLineCoordinates(cubicCoord(0, 0), cubicCoord(3, -2));
 * // line includes all hexes crossed by the line
 * ```
 */
export function getLineCoordinates(start: HexCoordinate, end: HexCoordinate): HexCoordinate[] {
  const n = distance(start, end);
  if (n === 0) return [start];

  const coords: HexCoordinate[] = [];

  for (let i = 0; i <= n; i++) {
    const t = i / n;
    // Linear interpolation with small epsilon to handle edge cases
    const epsilon = 1e-6;
    const q = start.q + (end.q - start.q) * t + epsilon;
    const r = start.r + (end.r - start.r) * t + epsilon;
    const s = start.s + (end.s - start.s) * t - 2 * epsilon; // Adjust s to maintain sum constraint
    coords.push(cubeRound(q, r, s));
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
  const angleRad = (VERTEX_ANGLES[position] * Math.PI) / 180;
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
  const apothem = (size * Math.sqrt(3)) / 2;
  // Edge midpoint direction is perpendicular to edge (edge angle - 90°)
  const angleRad = ((EDGE_ANGLES[side] - 90) * Math.PI) / 180;
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
    { q: 0, r: 0, s: 0 }, // [0] Center
    { q: 0, r: -1, s: 1 }, // [1] North
    { q: 1, r: -1, s: 0 }, // [2] NorthEast
    { q: 1, r: 0, s: -1 }, // [3] SouthEast
    { q: 0, r: 1, s: -1 }, // [4] South
    { q: -1, r: 1, s: 0 }, // [5] SouthWest
    { q: -1, r: 0, s: 1 }, // [6] NorthWest
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
    { q: 0, r: -2, s: 2 },
    { q: 1, r: -2, s: 1 },
    { q: 2, r: -2, s: 0 },
    // Row -1
    { q: -1, r: -1, s: 2 },
    { q: 0, r: -1, s: 1 },
    { q: 1, r: -1, s: 0 },
    { q: 2, r: -1, s: -1 },
    // Row 0 (center)
    { q: -2, r: 0, s: 2 },
    { q: -1, r: 0, s: 1 },
    { q: 0, r: 0, s: 0 },
    { q: 1, r: 0, s: -1 },
    { q: 2, r: 0, s: -2 },
    // Row 1
    { q: -2, r: 1, s: 1 },
    { q: -1, r: 1, s: 0 },
    { q: 0, r: 1, s: -1 },
    { q: 1, r: 1, s: -2 },
    // Row 2
    { q: -2, r: 2, s: 0 },
    { q: -1, r: 2, s: -1 },
    { q: 0, r: 2, s: -2 },
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
    { q: 0, r: -2, s: 2 },
    { q: 1, r: -2, s: 1 },
    { q: 2, r: -2, s: 0 },
    { q: 3, r: -2, s: -1 },
    // Row -1 (5 hexes)
    { q: -1, r: -1, s: 2 },
    { q: 0, r: -1, s: 1 },
    { q: 1, r: -1, s: 0 },
    { q: 2, r: -1, s: -1 },
    { q: 3, r: -1, s: -2 },
    // Row 0 (6 hexes)
    { q: -2, r: 0, s: 2 },
    { q: -1, r: 0, s: 1 },
    { q: 0, r: 0, s: 0 },
    { q: 1, r: 0, s: -1 },
    { q: 2, r: 0, s: -2 },
    { q: 3, r: 0, s: -3 },
    // Row 1 (6 hexes)
    { q: -3, r: 1, s: 2 },
    { q: -2, r: 1, s: 1 },
    { q: -1, r: 1, s: 0 },
    { q: 0, r: 1, s: -1 },
    { q: 1, r: 1, s: -2 },
    { q: 2, r: 1, s: -3 },
    // Row 2 (5 hexes)
    { q: -3, r: 2, s: 1 },
    { q: -2, r: 2, s: 0 },
    { q: -1, r: 2, s: -1 },
    { q: 0, r: 2, s: -2 },
    { q: 1, r: 2, s: -3 },
    // Row 3 (4 hexes)
    { q: -3, r: 3, s: 0 },
    { q: -2, r: 3, s: -1 },
    { q: -1, r: 3, s: -2 },
    { q: 0, r: 3, s: -3 },
  ] as const,
} as const;

// =============================================================================
// HexGridCollection - O(1) Coordinate Lookup
// =============================================================================

/**
 * A Map-based collection that stores values by hex coordinate with O(1) lookup.
 *
 * Provides efficient coordinate-based access compared to array scanning.
 * Uses a string key derived from q,r coordinates (s is redundant since s = -q-r).
 *
 * @example
 * ```typescript
 * // Create and populate
 * const collection = new HexGridCollection<string>();
 * collection.set(cubicCoord(0, 0), 'center');
 * collection.set(cubicCoord(1, 0), 'east');
 *
 * // Lookup
 * const value = collection.get(cubicCoord(0, 0)); // 'center'
 * const exists = collection.has(cubicCoord(2, 0)); // false
 *
 * // Iterate
 * collection.forEach((value, coord) => {
 *   console.log(`${coord.q},${coord.r}: ${value}`);
 * });
 *
 * // Create from layout
 * const tiles = HexGridCollection.fromLayout(
 *   HEX_LAYOUTS.CLUSTER_7,
 *   (coord, i) => ({ id: i, type: i === 0 ? 'desert' : 'resource' })
 * );
 * ```
 */
export class HexGridCollection<T> {
  private map = new Map<string, { coord: HexCoordinate; value: T }>();

  /**
   * Convert a HexCoordinate to a string key.
   * Uses only q,r since s is always -q-r.
   */
  private static toKey(coord: HexCoordinate): string {
    // Normalize -0 to 0 for consistent keys
    const q = coord.q || 0;
    const r = coord.r || 0;
    return `${q},${r}`;
  }

  /**
   * Set a value at a coordinate.
   */
  set(coord: HexCoordinate, value: T): void {
    const key = HexGridCollection.toKey(coord);
    // Store the full coordinate for iteration
    this.map.set(key, { coord: { ...coord }, value });
  }

  /**
   * Get the value at a coordinate, or undefined if not present.
   */
  get(coord: HexCoordinate): T | undefined {
    const entry = this.map.get(HexGridCollection.toKey(coord));
    return entry?.value;
  }

  /**
   * Check if a coordinate exists in the collection.
   */
  has(coord: HexCoordinate): boolean {
    return this.map.has(HexGridCollection.toKey(coord));
  }

  /**
   * Remove a coordinate from the collection.
   * @returns true if the coordinate was present and removed
   */
  delete(coord: HexCoordinate): boolean {
    return this.map.delete(HexGridCollection.toKey(coord));
  }

  /**
   * Remove all entries from the collection.
   */
  clear(): void {
    this.map.clear();
  }

  /**
   * The number of entries in the collection.
   */
  get size(): number {
    return this.map.size;
  }

  /**
   * Iterate over all entries with a callback.
   */
  forEach(callback: (value: T, coord: HexCoordinate) => void): void {
    this.map.forEach(({ coord, value }) => callback(value, coord));
  }

  /**
   * Iterate over coordinate-value pairs.
   */
  *entries(): IterableIterator<[HexCoordinate, T]> {
    for (const { coord, value } of this.map.values()) {
      yield [coord, value];
    }
  }

  /**
   * Iterate over all values.
   */
  *values(): IterableIterator<T> {
    for (const { value } of this.map.values()) {
      yield value;
    }
  }

  /**
   * Iterate over all coordinates.
   */
  *keys(): IterableIterator<HexCoordinate> {
    for (const { coord } of this.map.values()) {
      yield coord;
    }
  }

  /**
   * Convert to an array of {coord, value} objects.
   */
  toArray(): Array<{ coord: HexCoordinate; value: T }> {
    return Array.from(this.map.values()).map(({ coord, value }) => ({
      coord: { ...coord },
      value,
    }));
  }

  /**
   * Create a collection from an array of {coord, value} objects.
   */
  static fromArray<T>(items: Array<{ coord: HexCoordinate; value: T }>): HexGridCollection<T> {
    const collection = new HexGridCollection<T>();
    for (const { coord, value } of items) {
      collection.set(coord, value);
    }
    return collection;
  }

  /**
   * Create a collection from a predefined layout using a mapper function.
   */
  static fromLayout<T>(
    layout: readonly HexCoordinate[],
    mapper: (coord: HexCoordinate, index: number) => T
  ): HexGridCollection<T> {
    const collection = new HexGridCollection<T>();
    layout.forEach((coord, index) => {
      collection.set(coord, mapper(coord, index));
    });
    return collection;
  }
}
