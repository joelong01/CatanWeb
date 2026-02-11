/**
 * Constants for SVG board rendering - matches C# BoardSvgConstants.cs exactly.
 * SVG scales via viewBox, so we use the same sizes as the Blazor/Desktop apps.
 */

import type { ResourceType, HarborType, HexSide } from '@/types/generated/models';

// Base hex geometry - SAME AS DESKTOP APP
export const HEX_SIZE = 100; // OuterHexSize from BoardLayoutProps.cs
export const HEX_WIDTH = HEX_SIZE * 2;
export const HEX_HEIGHT = Math.sqrt(3) * HEX_SIZE;

// Board positioning
export const CENTER_X = 800;
export const CENTER_Y = 700;
export const PADDING = 5;

// Inner/Outer hex geometry - MATCHES DESKTOP APP
export const TILE_GAP = 2;
export const INNER_HEX_STROKE_THICKNESS = 14;
export const INNER_HEX_SIZE = HEX_SIZE - TILE_GAP - INNER_HEX_STROKE_THICKNESS * 0.5; // = 91

// Road rendering
export const ROAD_STROKE_THICKNESS = 2.0;

// Settlement/City
export const BUILDING_SIZE = 40;
export const SETTLEMENT_RADIUS = BUILDING_SIZE / 2; // = 20

// Number token
export const NUMBER_TOKEN_RADIUS = 30;
export const NUMBER_TOKEN_OFFSET_Y = 40;
export const NUMBER_TOKEN_OPACITY = 1.0;
export const NUMBER_FONT_SIZE = 24;
export const PIPS_FONT_SIZE = 12;
export const NUMBER_OFFSET_Y = -10;
export const PIPS_OFFSET_Y = 10;
export const NUMBER_TOKEN_STROKE_WIDTH = 0.5;

// Harbor rendering
export const HARBOR_CIRCLE_RADIUS = 35;
export const HARBOR_OFFSET = 70;
export const WATER_TRIANGLE_SIZE = 60;
export const WATER_COLOR = '#4169e1';

// Animation settings
export const TILE_DIM_DURATION_SECONDS = 5;

// Colors and patterns
export const HEX_BORDER_FILL_PATTERN = 'pattern-maple';
export const HEX_BORDER_STROKE_PATTERN = 'pattern-cherry';
export const HEX_HIGHLIGHT_COLOR = '#FFD700';
export const NUMBER_TOKEN_FILL = '#2F6999';
export const NUMBER_TOKEN_STROKE = 'white';
export const HIGH_PROB_COLOR = '#c00';
export const NORMAL_NUMBER_COLOR = '#fff';
export const BACKGROUND_COLOR = 'transparent';

/**
 * Edge rotation angles for each HexSide, used with SVG rotate transform.
 */
export const ROAD_EDGE_ANGLES: Record<HexSide, number> = {
  Top: 0,
  TopRight: 60,
  BottomRight: 120,
  Bottom: 180,
  BottomLeft: 240,
  TopLeft: 300,
  None: 0,
};

/**
 * Calculate edge midpoint offsets from tile center for each HexSide.
 * Distance from center to edge midpoint = apothem = HexSize * sqrt(3) / 2
 */
function calculateEdgeMidpointOffsets(): Record<HexSide, { dx: number; dy: number }> {
  const apothem = (HEX_SIZE * Math.sqrt(3)) / 2;

  // Midpoint direction angles (perpendicular to edge direction, pointing outward from center)
  const midpointAngles: Record<HexSide, number> = {
    Top: 270,
    TopRight: 330,
    BottomRight: 30,
    Bottom: 90,
    BottomLeft: 150,
    TopLeft: 210,
    None: 0,
  };

  const result: Record<HexSide, { dx: number; dy: number }> = {} as Record<
    HexSide,
    { dx: number; dy: number }
  >;

  for (const [side, angle] of Object.entries(midpointAngles) as [HexSide, number][]) {
    const angleRad = (angle * Math.PI) / 180;
    result[side] = {
      dx: apothem * Math.cos(angleRad),
      dy: apothem * Math.sin(angleRad),
    };
  }

  return result;
}

export const ROAD_EDGE_MIDPOINT_OFFSETS = calculateEdgeMidpointOffsets();

/**
 * Calculate the canonical road polygon (6-point bow-tie shape for horizontal edge).
 */
function calculateCanonicalRoadPolygon(): string {
  const R = HEX_SIZE; // 100 - outer hex circumradius
  const r = INNER_HEX_SIZE; // 91 - inner hex circumradius
  const apothemOuter = (R * Math.sqrt(3)) / 2;
  const apothemInner = (r * Math.sqrt(3)) / 2;

  const tipX = R / 2; // 50 - tip distance from midpoint along edge
  const innerX = r / 2; // 45.5 - inner vertex distance from midpoint along edge
  const perpDist = apothemOuter - apothemInner; // ~7.8 - perpendicular offset for inner vertices

  // 6 points forming the bow-tie shape
  const points: [number, number][] = [
    [-tipX, 0.0], // tip1: left vertex (outer hex)
    [-innerX, -perpDist], // innerA1: left inner vertex, tile A side
    [innerX, -perpDist], // innerA2: right inner vertex, tile A side
    [tipX, 0.0], // tip2: right vertex (outer hex)
    [innerX, perpDist], // innerB2: right inner vertex, tile B side
    [-innerX, perpDist], // innerB1: left inner vertex, tile B side
  ];

  return points.map(([x, y]) => `${x.toFixed(1)},${y.toFixed(1)}`).join(' ');
}

export const CANONICAL_ROAD_POLYGON = calculateCanonicalRoadPolygon();

/**
 * Gets the SVG pattern ID for a tile's resource type.
 */
export function getPatternId(resourceType: ResourceType): string {
  switch (resourceType) {
    case 'Brick':
      return 'tile-brick';
    case 'Wheat':
      return 'tile-wheat';
    case 'Wood':
      return 'tile-wood';
    case 'Ore':
      return 'tile-ore';
    case 'Sheep':
      return 'tile-sheep';
    case 'Desert':
      return 'tile-desert';
    case 'GoldMine':
      return 'tile-goldmine';
    case 'Sea':
      return 'tile-sea';
    default:
      return 'tile-default';
  }
}

/**
 * Gets the SVG pattern ID for a harbor's type.
 */
export function getHarborPatternId(harborType: HarborType): string {
  switch (harborType) {
    case 'Brick':
      return 'harbor-brick';
    case 'Ore':
      return 'harbor-ore';
    case 'Sheep':
      return 'harbor-sheep';
    case 'Wheat':
      return 'harbor-wheat';
    case 'Wood':
      return 'harbor-wood';
    case 'ThreeForOne':
      return 'harbor-3for1';
    default:
      return 'harbor-3for1';
  }
}
