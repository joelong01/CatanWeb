/**
 * Geometry helper functions for SVG board rendering.
 * Centralizes coordinate conversion and hex vertex calculations.
 * Matches C# WebUI/Services/Rendering/BoardGeometry.cs
 */

import type { HexCoordinates, HexSide, HexPosition } from '@/types/generated/models';
import { HEX_SIZE, CENTER_X, CENTER_Y } from './boardConstants';
import { DIRECTION_VECTORS, Direction, ALL_DIRECTIONS } from '@/components/hex-grid/hex-geometry';

/**
 * Type alias for HexCoordinates - used throughout the geometry module.
 * The generated HexCoordinates now only includes serialized properties (q, r, s).
 */
export type HexCoords = HexCoordinates;

/**
 * Converts axial coordinates to pixel position.
 * Uses flat-top hex formulas from Red Blob Games.
 */
export function axialToPixel(q: number, r: number): { x: number; y: number } {
  const x = HEX_SIZE * (3.0 / 2) * q;
  const y = HEX_SIZE * (Math.sqrt(3) / 2 * q + Math.sqrt(3) * r);
  return { x: x + CENTER_X, y: y + CENTER_Y };
}

/**
 * Converts pixel position to hex coordinates using cube rounding.
 * Returns the nearest hex tile coordinates.
 */
export function pixelToHex(px: number, py: number): HexCoords {
  // Remove center offset
  const x = px - CENTER_X;
  const y = py - CENTER_Y;

  // Convert to fractional axial coordinates
  const qf = (2.0 / 3.0) * x / HEX_SIZE;
  const rf = (-1.0 / 3.0 * x + Math.sqrt(3) / 3.0 * y) / HEX_SIZE;

  // Convert to cube coordinates for rounding
  const cubeX = qf;
  const cubeZ = rf;
  const cubeY = -cubeX - cubeZ;

  // Round cube coordinates
  let rx = Math.round(cubeX);
  let ry = Math.round(cubeY);
  let rz = Math.round(cubeZ);

  // Fix rounding errors by adjusting the coordinate with largest diff
  const xDiff = Math.abs(rx - cubeX);
  const yDiff = Math.abs(ry - cubeY);
  const zDiff = Math.abs(rz - cubeZ);

  if (xDiff > yDiff && xDiff > zDiff) {
    rx = -ry - rz;
  } else if (yDiff > zDiff) {
    ry = -rx - rz;
  } else {
    rz = -rx - ry;
  }

  // Return HexCoordinates (q=rx, r=rz, s=ry in cube coordinate convention)
  // Use (value || 0) to convert -0 to +0 for consistent comparisons
  return { q: rx || 0, r: rz || 0, s: ry || 0 };
}

/**
 * Gets hex vertices for a tile at the given center position and size.
 * Returns 6 vertices for a flat-top hexagon, starting at 0° (right vertex).
 */
export function getHexVertices(
  cx: number,
  cy: number,
  size: number = HEX_SIZE
): Array<{ x: number; y: number }> {
  const vertices: Array<{ x: number; y: number }> = [];
  for (let i = 0; i < 6; i++) {
    const angle = (Math.PI / 180) * (60 * i);
    const x = cx + size * Math.cos(angle);
    const y = cy + size * Math.sin(angle);
    vertices.push({ x, y });
  }
  return vertices;
}

/**
 * Generates SVG path for hexagon at given center position and size.
 */
export function generateHexPath(cx: number, cy: number, size: number): string {
  const vertices = getHexVertices(cx, cy, size);
  const points = vertices.map((v) => `${v.x.toFixed(1)},${v.y.toFixed(1)}`);
  return `M ${points[0]} L ${points.slice(1).join(' L ')} Z`;
}

/**
 * Maps HexSide to the two vertex indices for flat-top hex orientation.
 * Vertex indices: 0=Right, 1=BottomRight, 2=BottomLeft, 3=Left, 4=TopLeft, 5=TopRight
 */
export function getEdgeVerticesForSide(side: HexSide): { v1Idx: number; v2Idx: number } {
  switch (side) {
    case 'Top':
      return { v1Idx: 4, v2Idx: 5 };
    case 'TopRight':
      return { v1Idx: 5, v2Idx: 0 };
    case 'BottomRight':
      return { v1Idx: 0, v2Idx: 1 };
    case 'Bottom':
      return { v1Idx: 1, v2Idx: 2 };
    case 'BottomLeft':
      return { v1Idx: 2, v2Idx: 3 };
    case 'TopLeft':
      return { v1Idx: 3, v2Idx: 4 };
    default:
      return { v1Idx: 0, v2Idx: 1 };
  }
}

/**
 * Maps HexPosition to vertex index (0-5) for flat-top hex orientation.
 */
export function getVertexIndexForPosition(position: HexPosition): number {
  switch (position) {
    case 'Right':
      return 0;
    case 'BottomRight':
      return 1;
    case 'BottomLeft':
      return 2;
    case 'Left':
      return 3;
    case 'TopLeft':
      return 4;
    case 'TopRight':
      return 5;
    default:
      return 0;
  }
}

/**
 * Gets pixel position for a building vertex in SVG coordinate space.
 * Used for placing settlements and cities.
 */
export function getVertexPosition(
  hexCoords: HexCoords,
  position: HexPosition
): { x: number; y: number } {
  // Convert tile axial coordinates to pixel position
  const { x: tileX, y: tileY } = axialToPixel(hexCoords.q, hexCoords.r);

  // Get hex vertices
  const vertices = getHexVertices(tileX, tileY);

  // Map HexPosition to vertex index
  const vertexIndex = getVertexIndexForPosition(position);

  return vertices[vertexIndex];
}


/**
 * Adds two hex coordinates.
 */
export function addHexCoords(a: HexCoords, b: HexCoords): HexCoords {
  return { q: a.q + b.q, r: a.r + b.r, s: a.s + b.s };
}

/**
 * Subtracts two hex coordinates.
 */
export function subtractHexCoords(a: HexCoords, b: HexCoords): HexCoords {
  return { q: a.q - b.q, r: a.r - b.r, s: a.s - b.s };
}

/**
 * Calculates the distance between two hex coordinates.
 * Returns the number of hex steps between them.
 */
export function hexDistance(a: HexCoords, b: HexCoords): number {
  const diff = subtractHexCoords(a, b);
  return (Math.abs(diff.q) + Math.abs(diff.r) + Math.abs(diff.s)) / 2;
}

/**
 * Gets all 6 neighboring hex coordinates.
 */
export function getHexNeighbors(coords: HexCoords): HexCoords[] {
  return ALL_DIRECTIONS.map((dir) => addHexCoords(coords, DIRECTION_VECTORS[dir]));
}

/**
 * Checks if two hex coordinates are adjacent (distance of 1).
 */
export function areHexesAdjacent(a: HexCoords, b: HexCoords): boolean {
  return hexDistance(a, b) === 1;
}

/**
 * Validates that cube coordinates satisfy Q + R + S = 0.
 */
export function isValidHexCoordinate(coords: HexCoords): boolean {
  return coords.q + coords.r + coords.s === 0;
}

/**
 * Creates a HexCoordinates from Q and R (axial), computing S.
 */
export function hexFromAxial(q: number, r: number): HexCoords {
  return { q, r, s: -q - r };
}

/**
 * Formats hex coordinates as a string "(Q,R,S)".
 */
export function hexToString(coords: HexCoords): string {
  return `(${coords.q},${coords.r},${coords.s})`;
}

/**
 * Parses hex coordinates from a string "(Q,R,S)" or "Q,R,S".
 */
export function hexFromString(str: string): HexCoords | null {
  const cleaned = str.replace(/[()]/g, '').trim();
  const parts = cleaned.split(',').map((s) => parseInt(s.trim(), 10));
  if (parts.length !== 3 || parts.some(isNaN)) {
    return null;
  }
  const [q, r, s] = parts;
  if (q + r + s !== 0) {
    return null;
  }
  return { q, r, s };
}

/**
 * Compares two hex coordinates for equality.
 */
export function hexEquals(a: HexCoords, b: HexCoords): boolean {
  return a.q === b.q && a.r === b.r && a.s === b.s;
}
