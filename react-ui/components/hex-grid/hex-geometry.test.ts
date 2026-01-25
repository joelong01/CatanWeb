/**
 * Unit tests for hex-geometry.ts
 *
 * Validates hex coordinate calculations against C# reference implementation:
 * - HexCoordinates.ToPixelCenter (Catan3.Shared/Utility/HexCoordinates.cs)
 * - RegularBoardInfo.TileKeys (Catan3.Shared/Models/RegularBoardInfo.cs)
 * - ExpansionBoardInfo.TileKeys (Catan3.Shared/Models/ExpansionBoardInfo.cs)
 */
import { describe, it, expect } from 'vitest';
import {
  calculateHexDimensions,
  hexToPixel,
  cubicCoord,
  HEX_LAYOUTS,
  HexCoordinate,
} from './hex-geometry';

describe('calculateHexDimensions', () => {
  it('should calculate correct width (2 × size)', () => {
    const dims = calculateHexDimensions(100);
    expect(dims.width).toBe(200);
  });

  it('should calculate correct height (sqrt(3) × size)', () => {
    const dims = calculateHexDimensions(100);
    expect(dims.height).toBeCloseTo(Math.sqrt(3) * 100, 10);
  });

  it('should calculate correct aspect ratio (~0.866)', () => {
    const dims = calculateHexDimensions(100);
    expect(dims.aspectRatio).toBeCloseTo(Math.sqrt(3) / 2, 10);
  });

  it('should preserve size parameter', () => {
    const dims = calculateHexDimensions(50);
    expect(dims.size).toBe(50);
  });

  it('should use default gap of 2', () => {
    const dims = calculateHexDimensions(100);
    expect(dims.gap).toBe(2);
  });

  it('should accept custom gap parameter', () => {
    const dims = calculateHexDimensions(100, 5);
    expect(dims.gap).toBe(5);
  });

  it('should handle size = 0', () => {
    const dims = calculateHexDimensions(0);
    expect(dims.width).toBe(0);
    expect(dims.height).toBe(0);
    expect(dims.size).toBe(0);
  });

  it('should scale linearly with size', () => {
    const dims50 = calculateHexDimensions(50);
    const dims100 = calculateHexDimensions(100);

    expect(dims100.width).toBe(dims50.width * 2);
    expect(dims100.height).toBeCloseTo(dims50.height * 2, 10);
  });
});

describe('hexToPixel', () => {
  const size = 100;

  it('should place center hex (0,0) at origin', () => {
    const pos = hexToPixel(cubicCoord(0, 0), size);
    expect(pos.x).toBe(0);
    expect(pos.y).toBe(0);
  });

  it('should match C# ToPixelCenter formula for x: size × 1.5 × q', () => {
    // C#: double x = size * 1.5 * Q + offsetX;
    const coord = cubicCoord(2, 0);
    const pos = hexToPixel(coord, size);
    expect(pos.x).toBe(size * 1.5 * coord.q);
  });

  it('should match C# ToPixelCenter formula for y: size × sqrt(3) × (r + q/2)', () => {
    // C#: double y = size * Math.Sqrt(3) * (R + Q / 2.0) + offsetY;
    const coord = cubicCoord(1, 2);
    const pos = hexToPixel(coord, size);
    const expectedY = size * Math.sqrt(3) * (coord.r + coord.q / 2);
    expect(pos.y).toBeCloseTo(expectedY, 10);
  });

  it('should apply origin offset correctly', () => {
    const coord = cubicCoord(1, 1);
    const origin = { x: 500, y: 400 };
    const pos = hexToPixel(coord, size, origin);

    const baseX = size * 1.5 * coord.q;
    const baseY = size * Math.sqrt(3) * (coord.r + coord.q / 2);

    expect(pos.x).toBe(baseX + origin.x);
    expect(pos.y).toBeCloseTo(baseY + origin.y, 10);
  });

  it('should handle negative coordinates', () => {
    const coord = cubicCoord(-2, -1);
    const pos = hexToPixel(coord, size);

    const expectedX = size * 1.5 * coord.q;
    const expectedY = size * Math.sqrt(3) * (coord.r + coord.q / 2);

    expect(pos.x).toBe(expectedX);
    expect(pos.y).toBeCloseTo(expectedY, 10);
  });

  describe('known coordinate positions', () => {
    // Test the 6 directions from center (CLUSTER_7 neighbors)
    it('should place North hex (0, -1) above center', () => {
      const center = hexToPixel(cubicCoord(0, 0), size);
      const north = hexToPixel(cubicCoord(0, -1), size);

      expect(north.x).toBe(center.x);
      expect(north.y).toBeLessThan(center.y);
    });

    it('should place NorthEast hex (1, -1) up-right from center', () => {
      const center = hexToPixel(cubicCoord(0, 0), size);
      const ne = hexToPixel(cubicCoord(1, -1), size);

      expect(ne.x).toBeGreaterThan(center.x);
      expect(ne.y).toBeLessThan(center.y);
    });

    it('should place SouthEast hex (1, 0) right of center', () => {
      const center = hexToPixel(cubicCoord(0, 0), size);
      const se = hexToPixel(cubicCoord(1, 0), size);

      expect(se.x).toBeGreaterThan(center.x);
      expect(se.y).toBeGreaterThan(center.y);
    });

    it('should place South hex (0, 1) below center', () => {
      const center = hexToPixel(cubicCoord(0, 0), size);
      const south = hexToPixel(cubicCoord(0, 1), size);

      expect(south.x).toBe(center.x);
      expect(south.y).toBeGreaterThan(center.y);
    });

    it('should place SouthWest hex (-1, 1) down-left from center', () => {
      const center = hexToPixel(cubicCoord(0, 0), size);
      const sw = hexToPixel(cubicCoord(-1, 1), size);

      expect(sw.x).toBeLessThan(center.x);
      expect(sw.y).toBeGreaterThan(center.y);
    });

    it('should place NorthWest hex (-1, 0) left of center', () => {
      const center = hexToPixel(cubicCoord(0, 0), size);
      const nw = hexToPixel(cubicCoord(-1, 0), size);

      expect(nw.x).toBeLessThan(center.x);
      expect(nw.y).toBeLessThan(center.y);
    });
  });

  describe('hex spacing', () => {
    it('should space adjacent hexes consistently', () => {
      // Distance from center to each neighbor should be equal
      const center = hexToPixel(cubicCoord(0, 0), size);
      const neighbors = [
        hexToPixel(cubicCoord(0, -1), size), // North
        hexToPixel(cubicCoord(1, -1), size), // NorthEast
        hexToPixel(cubicCoord(1, 0), size), // SouthEast
        hexToPixel(cubicCoord(0, 1), size), // South
        hexToPixel(cubicCoord(-1, 1), size), // SouthWest
        hexToPixel(cubicCoord(-1, 0), size), // NorthWest
      ];

      const distances = neighbors.map((n) =>
        Math.sqrt(Math.pow(n.x - center.x, 2) + Math.pow(n.y - center.y, 2))
      );

      // All distances should be approximately equal
      const firstDistance = distances[0];
      distances.forEach((d) => {
        expect(d).toBeCloseTo(firstDistance, 5);
      });

      // Distance should be size * sqrt(3) for adjacent hexes
      expect(firstDistance).toBeCloseTo(size * Math.sqrt(3), 5);
    });
  });
});

describe('HEX_LAYOUTS', () => {
  describe('CLUSTER_7', () => {
    it('should have exactly 7 hexes', () => {
      expect(HEX_LAYOUTS.CLUSTER_7).toHaveLength(7);
    });

    it('should have center hex at (0, 0, 0)', () => {
      expect(HEX_LAYOUTS.CLUSTER_7[0]).toEqual(cubicCoord(0, 0));
    });

    it('should include all 6 neighbor directions', () => {
      const coords = HEX_LAYOUTS.CLUSTER_7;
      const coordSet = new Set(coords.map((c) => `${c.q},${c.r}`));

      // Center + 6 directions from C# HexCoordinates.Directions
      expect(coordSet.has('0,0')).toBe(true); // Center
      expect(coordSet.has('0,-1')).toBe(true); // North
      expect(coordSet.has('1,-1')).toBe(true); // NorthEast
      expect(coordSet.has('1,0')).toBe(true); // SouthEast
      expect(coordSet.has('0,1')).toBe(true); // South
      expect(coordSet.has('-1,1')).toBe(true); // SouthWest
      expect(coordSet.has('-1,0')).toBe(true); // NorthWest
    });

    it('should have unique coordinates', () => {
      const coords = HEX_LAYOUTS.CLUSTER_7;
      const coordSet = new Set(coords.map((c) => `${c.q},${c.r}`));
      expect(coordSet.size).toBe(coords.length);
    });

    it('should satisfy q + r + s === 0 constraint for all coordinates', () => {
      HEX_LAYOUTS.CLUSTER_7.forEach((c) => {
        expect(c.q + c.r + c.s).toBe(0);
      });
    });
  });

  describe('CLUSTER_19', () => {
    it('should have exactly 19 hexes', () => {
      expect(HEX_LAYOUTS.CLUSTER_19).toHaveLength(19);
    });

    it('should match C# RegularBoardInfo.TileKeys coordinates', () => {
      // From Catan3.Shared/Models/RegularBoardInfo.cs TileKeys
      const csharpTileKeys: HexCoordinate[] = [
        cubicCoord(-2, 0), cubicCoord(-2, 1), cubicCoord(-2, 2),
        cubicCoord(-1, -1), cubicCoord(-1, 0), cubicCoord(-1, 1), cubicCoord(-1, 2),
        cubicCoord(0, -2), cubicCoord(0, -1), cubicCoord(0, 0), cubicCoord(0, 1), cubicCoord(0, 2),
        cubicCoord(1, -2), cubicCoord(1, -1), cubicCoord(1, 0), cubicCoord(1, 1),
        cubicCoord(2, -2), cubicCoord(2, -1), cubicCoord(2, 0),
      ];

      const tsCoordSet = new Set(HEX_LAYOUTS.CLUSTER_19.map((c) => `${c.q},${c.r}`));
      const csCoordSet = new Set(csharpTileKeys.map((c) => `${c.q},${c.r}`));

      // Both sets should contain same coordinates
      expect(tsCoordSet.size).toBe(csCoordSet.size);
      csCoordSet.forEach((coord) => {
        expect(tsCoordSet.has(coord)).toBe(true);
      });
    });

    it('should have 3-4-5-4-3 row pattern', () => {
      const coords = HEX_LAYOUTS.CLUSTER_19;
      const rowCounts = new Map<number, number>();

      coords.forEach((c) => {
        rowCounts.set(c.r, (rowCounts.get(c.r) || 0) + 1);
      });

      expect(rowCounts.get(-2)).toBe(3); // Top row
      expect(rowCounts.get(-1)).toBe(4);
      expect(rowCounts.get(0)).toBe(5); // Middle row
      expect(rowCounts.get(1)).toBe(4);
      expect(rowCounts.get(2)).toBe(3); // Bottom row
    });

    it('should have unique coordinates', () => {
      const coords = HEX_LAYOUTS.CLUSTER_19;
      const coordSet = new Set(coords.map((c) => `${c.q},${c.r}`));
      expect(coordSet.size).toBe(coords.length);
    });

    it('should satisfy q + r + s === 0 constraint for all coordinates', () => {
      HEX_LAYOUTS.CLUSTER_19.forEach((c) => {
        expect(c.q + c.r + c.s).toBe(0);
      });
    });
  });

  describe('CLUSTER_30', () => {
    it('should have exactly 30 hexes', () => {
      expect(HEX_LAYOUTS.CLUSTER_30).toHaveLength(30);
    });

    it('should match C# ExpansionBoardInfo.TileKeys coordinates', () => {
      // From Catan3.Shared/Models/ExpansionBoardInfo.cs TileKeys
      const csharpTileKeys: HexCoordinate[] = [
        // Column 1 (q=-3)
        cubicCoord(-3, 1), cubicCoord(-3, 2), cubicCoord(-3, 3),
        // Column 2 (q=-2)
        cubicCoord(-2, 0), cubicCoord(-2, 1), cubicCoord(-2, 2), cubicCoord(-2, 3),
        // Column 3 (q=-1)
        cubicCoord(-1, -1), cubicCoord(-1, 0), cubicCoord(-1, 1), cubicCoord(-1, 2), cubicCoord(-1, 3),
        // Column 4 (q=0)
        cubicCoord(0, -2), cubicCoord(0, -1), cubicCoord(0, 0), cubicCoord(0, 1), cubicCoord(0, 2), cubicCoord(0, 3),
        // Column 5 (q=1)
        cubicCoord(1, -2), cubicCoord(1, -1), cubicCoord(1, 0), cubicCoord(1, 1), cubicCoord(1, 2),
        // Column 6 (q=2)
        cubicCoord(2, -2), cubicCoord(2, -1), cubicCoord(2, 0), cubicCoord(2, 1),
        // Column 7 (q=3)
        cubicCoord(3, -2), cubicCoord(3, -1), cubicCoord(3, 0),
      ];

      const tsCoordSet = new Set(HEX_LAYOUTS.CLUSTER_30.map((c) => `${c.q},${c.r}`));
      const csCoordSet = new Set(csharpTileKeys.map((c) => `${c.q},${c.r}`));

      // Both sets should contain same coordinates
      expect(tsCoordSet.size).toBe(csCoordSet.size);
      csCoordSet.forEach((coord) => {
        expect(tsCoordSet.has(coord)).toBe(true);
      });
    });

    it('should have 4-5-6-6-5-4 row pattern', () => {
      const coords = HEX_LAYOUTS.CLUSTER_30;
      const rowCounts = new Map<number, number>();

      coords.forEach((c) => {
        rowCounts.set(c.r, (rowCounts.get(c.r) || 0) + 1);
      });

      expect(rowCounts.get(-2)).toBe(4); // Top row
      expect(rowCounts.get(-1)).toBe(5);
      expect(rowCounts.get(0)).toBe(6);
      expect(rowCounts.get(1)).toBe(6);
      expect(rowCounts.get(2)).toBe(5);
      expect(rowCounts.get(3)).toBe(4); // Bottom row
    });

    it('should have unique coordinates', () => {
      const coords = HEX_LAYOUTS.CLUSTER_30;
      const coordSet = new Set(coords.map((c) => `${c.q},${c.r}`));
      expect(coordSet.size).toBe(coords.length);
    });

    it('should satisfy q + r + s === 0 constraint for all coordinates', () => {
      HEX_LAYOUTS.CLUSTER_30.forEach((c) => {
        expect(c.q + c.r + c.s).toBe(0);
      });
    });
  });
});

describe('pixel position snapshot tests', () => {
  // Snapshot tests at specific hex sizes for regression detection
  const testCases: Array<{ name: string; coord: HexCoordinate; size: number }> = [
    { name: 'center at size 100', coord: cubicCoord(0, 0), size: 100 },
    { name: 'north at size 100', coord: cubicCoord(0, -1), size: 100 },
    { name: 'southeast at size 100', coord: cubicCoord(1, 0), size: 100 },
    { name: 'center at size 50', coord: cubicCoord(0, 0), size: 50 },
    { name: 'expansion corner at size 100', coord: cubicCoord(3, -2), size: 100 },
  ];

  testCases.forEach(({ name, coord, size }) => {
    it(`should produce consistent position for ${name}`, () => {
      const pos = hexToPixel(coord, size);

      // Calculate expected values using the formula directly
      const expectedX = size * 1.5 * coord.q;
      const expectedY = size * Math.sqrt(3) * (coord.r + coord.q / 2);

      expect(pos.x).toBeCloseTo(expectedX, 10);
      expect(pos.y).toBeCloseTo(expectedY, 10);
    });
  });
});
