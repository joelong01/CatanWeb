/**
 * Unit tests for board geometry functions.
 * Tests coordinate conversion, vertex calculations, and hex utilities.
 */

import { describe, it, expect } from 'vitest';
import {
  axialToPixel,
  pixelToHex,
  getHexVertices,
  generateHexPath,
  getEdgeVerticesForSide,
  getVertexIndexForPosition,
  addHexCoords,
  subtractHexCoords,
  hexDistance,
  getHexNeighbors,
  areHexesAdjacent,
  isValidHexCoordinate,
  hexFromAxial,
  hexToString,
  hexFromString,
  hexEquals,
} from './boardGeometry';
import { HEX_SIZE, CENTER_X, CENTER_Y } from './boardConstants';

describe('Coordinate Conversion', () => {
  describe('axialToPixel', () => {
    it('should convert origin (0,0) to center position', () => {
      const { x, y } = axialToPixel(0, 0);
      expect(x).toBe(CENTER_X);
      expect(y).toBe(CENTER_Y);
    });

    it('should convert positive Q to right of center', () => {
      const { x, y } = axialToPixel(1, 0);
      expect(x).toBeGreaterThan(CENTER_X);
      // Y should also change due to hex grid offset
      expect(y).toBeGreaterThan(CENTER_Y - 1);
    });

    it('should convert positive R to below center', () => {
      const { x } = axialToPixel(0, 1);
      expect(x).toBe(CENTER_X);
    });

    it('should match C# AxialToPixel formula', () => {
      // Test specific values from C# implementation
      const { x, y } = axialToPixel(2, -1);
      // x = HexSize * (3/2 * q) + CenterX = 100 * 3 + 800 = 1100
      // y = HexSize * (sqrt(3)/2 * q + sqrt(3) * r) + CenterY
      //   = 100 * (sqrt(3)/2 * 2 + sqrt(3) * -1) + 700
      //   = 100 * (sqrt(3) - sqrt(3)) + 700 = 700
      expect(x).toBeCloseTo(1100, 5);
      expect(y).toBeCloseTo(700, 5);
    });
  });

  describe('pixelToHex', () => {
    it('should convert center position to origin (0,0,0)', () => {
      const coords = pixelToHex(CENTER_X, CENTER_Y);
      expect(coords.q).toBe(0);
      expect(coords.r).toBe(0);
      expect(coords.s).toBe(0);
    });

    it('should round to nearest hex correctly', () => {
      // Test point slightly off center - should still be (0,0,0)
      const coords = pixelToHex(CENTER_X + 10, CENTER_Y + 10);
      expect(coords.q).toBe(0);
      expect(coords.r).toBe(0);
    });

    it('should satisfy cube coordinate constraint Q+R+S=0', () => {
      // Test various points
      const testPoints = [
        { px: CENTER_X, py: CENTER_Y },
        { px: CENTER_X + 150, py: CENTER_Y },
        { px: CENTER_X - 100, py: CENTER_Y + 87 },
        { px: CENTER_X + 200, py: CENTER_Y - 173 },
      ];

      for (const { px, py } of testPoints) {
        const coords = pixelToHex(px, py);
        expect(coords.q + coords.r + coords.s).toBe(0);
      }
    });

    it('should be inverse of axialToPixel (round trip)', () => {
      // Test round-trip conversion
      const testCoords = [
        { q: 0, r: 0 },
        { q: 1, r: 0 },
        { q: 0, r: 1 },
        { q: -1, r: 1 },
        { q: 2, r: -1 },
      ];

      for (const { q, r } of testCoords) {
        const pixel = axialToPixel(q, r);
        const result = pixelToHex(pixel.x, pixel.y);
        expect(result.q).toBe(q);
        expect(result.r).toBe(r);
        // Use || 0 to handle -0 == 0 comparison (JavaScript signed zero)
        expect(result.s).toBe((-q - r) || 0);
      }
    });
  });
});

describe('Hex Vertex Calculations', () => {
  describe('getHexVertices', () => {
    it('should return 6 vertices', () => {
      const vertices = getHexVertices(0, 0);
      expect(vertices).toHaveLength(6);
    });

    it('should have first vertex at 0 degrees (right)', () => {
      const vertices = getHexVertices(0, 0, 100);
      // At 0 degrees: x = size, y = 0
      expect(vertices[0].x).toBeCloseTo(100, 5);
      expect(vertices[0].y).toBeCloseTo(0, 5);
    });

    it('should have vertices at 60 degree intervals', () => {
      const vertices = getHexVertices(0, 0, 100);

      // Vertex 0: 0° - right
      expect(vertices[0].x).toBeCloseTo(100, 5);
      expect(vertices[0].y).toBeCloseTo(0, 5);

      // Vertex 1: 60° - bottom-right
      expect(vertices[1].x).toBeCloseTo(50, 5);
      expect(vertices[1].y).toBeCloseTo(86.6, 0);

      // Vertex 2: 120° - bottom-left
      expect(vertices[2].x).toBeCloseTo(-50, 5);
      expect(vertices[2].y).toBeCloseTo(86.6, 0);

      // Vertex 3: 180° - left
      expect(vertices[3].x).toBeCloseTo(-100, 5);
      expect(vertices[3].y).toBeCloseTo(0, 4);
    });

    it('should respect center position', () => {
      const cx = 500;
      const cy = 300;
      const vertices = getHexVertices(cx, cy, 100);

      // First vertex should be at center + (100, 0)
      expect(vertices[0].x).toBeCloseTo(600, 5);
      expect(vertices[0].y).toBeCloseTo(300, 5);
    });

    it('should respect custom size', () => {
      const vertices = getHexVertices(0, 0, 50);

      // First vertex at half the normal distance
      expect(vertices[0].x).toBeCloseTo(50, 5);
      expect(vertices[0].y).toBeCloseTo(0, 5);
    });
  });

  describe('generateHexPath', () => {
    it('should generate valid SVG path', () => {
      const path = generateHexPath(0, 0, 100);

      // Should start with M (moveto)
      expect(path.startsWith('M ')).toBe(true);

      // Should contain L (lineto) commands
      expect(path.includes(' L ')).toBe(true);

      // Should end with Z (close path)
      expect(path.endsWith(' Z')).toBe(true);
    });

    it('should have 6 points in path', () => {
      const path = generateHexPath(0, 0, 100);

      // Count points: 1 after M, 5 after L commands
      const lCount = (path.match(/ L /g) || []).length;
      expect(lCount).toBe(5);
    });
  });
});

describe('Edge and Vertex Mapping', () => {
  describe('getEdgeVerticesForSide', () => {
    it('should map Top edge to vertices 4 and 5', () => {
      const { v1Idx, v2Idx } = getEdgeVerticesForSide('Top');
      expect(v1Idx).toBe(4);
      expect(v2Idx).toBe(5);
    });

    it('should map TopRight edge to vertices 5 and 0', () => {
      const { v1Idx, v2Idx } = getEdgeVerticesForSide('TopRight');
      expect(v1Idx).toBe(5);
      expect(v2Idx).toBe(0);
    });

    it('should map BottomRight edge to vertices 0 and 1', () => {
      const { v1Idx, v2Idx } = getEdgeVerticesForSide('BottomRight');
      expect(v1Idx).toBe(0);
      expect(v2Idx).toBe(1);
    });

    it('should map Bottom edge to vertices 1 and 2', () => {
      const { v1Idx, v2Idx } = getEdgeVerticesForSide('Bottom');
      expect(v1Idx).toBe(1);
      expect(v2Idx).toBe(2);
    });
  });

  describe('getVertexIndexForPosition', () => {
    it('should map Right to vertex 0', () => {
      expect(getVertexIndexForPosition('Right')).toBe(0);
    });

    it('should map BottomRight to vertex 1', () => {
      expect(getVertexIndexForPosition('BottomRight')).toBe(1);
    });

    it('should map Left to vertex 3', () => {
      expect(getVertexIndexForPosition('Left')).toBe(3);
    });

    it('should map TopRight to vertex 5', () => {
      expect(getVertexIndexForPosition('TopRight')).toBe(5);
    });
  });
});

describe('Hex Coordinate Utilities', () => {
  describe('addHexCoords', () => {
    it('should add coordinates correctly', () => {
      const a = { q: 1, r: 2, s: -3 };
      const b = { q: -1, r: 0, s: 1 };
      const result = addHexCoords(a, b);
      expect(result).toEqual({ q: 0, r: 2, s: -2 });
    });
  });

  describe('subtractHexCoords', () => {
    it('should subtract coordinates correctly', () => {
      const a = { q: 2, r: 1, s: -3 };
      const b = { q: 1, r: 0, s: -1 };
      const result = subtractHexCoords(a, b);
      expect(result).toEqual({ q: 1, r: 1, s: -2 });
    });
  });

  describe('hexDistance', () => {
    it('should return 0 for same coordinate', () => {
      const a = { q: 1, r: 2, s: -3 };
      expect(hexDistance(a, a)).toBe(0);
    });

    it('should return 1 for adjacent hexes', () => {
      const a = { q: 0, r: 0, s: 0 };
      const b = { q: 1, r: 0, s: -1 };
      expect(hexDistance(a, b)).toBe(1);
    });

    it('should return correct distance for non-adjacent hexes', () => {
      const a = { q: 0, r: 0, s: 0 };
      const b = { q: 2, r: -1, s: -1 };
      expect(hexDistance(a, b)).toBe(2);
    });
  });

  describe('getHexNeighbors', () => {
    it('should return 6 neighbors', () => {
      const coords = { q: 0, r: 0, s: 0 };
      const neighbors = getHexNeighbors(coords);
      expect(neighbors).toHaveLength(6);
    });

    it('should return all adjacent coordinates', () => {
      const coords = { q: 0, r: 0, s: 0 };
      const neighbors = getHexNeighbors(coords);

      // All neighbors should be distance 1 away
      for (const neighbor of neighbors) {
        expect(hexDistance(coords, neighbor)).toBe(1);
      }
    });

    it('should return valid cube coordinates', () => {
      const coords = { q: 1, r: 2, s: -3 };
      const neighbors = getHexNeighbors(coords);

      for (const neighbor of neighbors) {
        expect(isValidHexCoordinate(neighbor)).toBe(true);
      }
    });
  });

  describe('areHexesAdjacent', () => {
    it('should return true for adjacent hexes', () => {
      const a = { q: 0, r: 0, s: 0 };
      const b = { q: 1, r: -1, s: 0 };
      expect(areHexesAdjacent(a, b)).toBe(true);
    });

    it('should return false for non-adjacent hexes', () => {
      const a = { q: 0, r: 0, s: 0 };
      const b = { q: 2, r: 0, s: -2 };
      expect(areHexesAdjacent(a, b)).toBe(false);
    });

    it('should return false for same hex', () => {
      const a = { q: 1, r: 1, s: -2 };
      expect(areHexesAdjacent(a, a)).toBe(false);
    });
  });

  describe('isValidHexCoordinate', () => {
    it('should return true for valid coordinates (Q+R+S=0)', () => {
      expect(isValidHexCoordinate({ q: 1, r: 2, s: -3 })).toBe(true);
      expect(isValidHexCoordinate({ q: 0, r: 0, s: 0 })).toBe(true);
      expect(isValidHexCoordinate({ q: -5, r: 3, s: 2 })).toBe(true);
    });

    it('should return false for invalid coordinates', () => {
      expect(isValidHexCoordinate({ q: 1, r: 1, s: 1 })).toBe(false);
      expect(isValidHexCoordinate({ q: 0, r: 0, s: 1 })).toBe(false);
    });
  });

  describe('hexFromAxial', () => {
    it('should compute S from Q and R', () => {
      const coords = hexFromAxial(2, 3);
      expect(coords.q).toBe(2);
      expect(coords.r).toBe(3);
      expect(coords.s).toBe(-5);
      expect(isValidHexCoordinate(coords)).toBe(true);
    });
  });

  describe('hexToString / hexFromString', () => {
    it('should format coordinates as string', () => {
      const str = hexToString({ q: 1, r: 2, s: -3 });
      expect(str).toBe('(1,2,-3)');
    });

    it('should parse string to coordinates', () => {
      const coords = hexFromString('(1,2,-3)');
      expect(coords).toEqual({ q: 1, r: 2, s: -3 });
    });

    it('should parse string without parentheses', () => {
      const coords = hexFromString('1,2,-3');
      expect(coords).toEqual({ q: 1, r: 2, s: -3 });
    });

    it('should return null for invalid string', () => {
      expect(hexFromString('invalid')).toBeNull();
      expect(hexFromString('1,2')).toBeNull();
      expect(hexFromString('1,2,3')).toBeNull(); // Q+R+S != 0
    });

    it('should round-trip correctly', () => {
      const original = { q: -2, r: 5, s: -3 };
      const str = hexToString(original);
      const parsed = hexFromString(str);
      expect(parsed).toEqual(original);
    });
  });

  describe('hexEquals', () => {
    it('should return true for equal coordinates', () => {
      const a = { q: 1, r: 2, s: -3 };
      const b = { q: 1, r: 2, s: -3 };
      expect(hexEquals(a, b)).toBe(true);
    });

    it('should return false for different coordinates', () => {
      const a = { q: 1, r: 2, s: -3 };
      const b = { q: 1, r: 3, s: -4 };
      expect(hexEquals(a, b)).toBe(false);
    });
  });
});

describe('Constants', () => {
  it('should have expected HEX_SIZE', () => {
    expect(HEX_SIZE).toBe(100);
  });

  it('should have expected center coordinates', () => {
    expect(CENTER_X).toBe(800);
    expect(CENTER_Y).toBe(700);
  });
});
