'use client';

import React, { useState } from 'react';
import type { PlayerColors } from '../board/GameBoard';

/**
 * Road state (matches C# RoadState enum)
 */
export type RoadState = 'Unowned' | 'Road' | 'Ship' | 'Buildable';

/**
 * Hex side (matches C# HexSide enum)
 */
export type HexSide = 'Top' | 'TopRight' | 'BottomRight' | 'Bottom' | 'BottomLeft' | 'TopLeft';

/**
 * Props for Road component
 */
export interface RoadProps {
  /** Road state (Road, Ship, Buildable, Unowned) */
  roadState: RoadState;
  /** Hex side this road is on (determines rotation) */
  side: HexSide;
  /** Player colors for owned roads */
  ownerColors?: PlayerColors | null;
  /** Current player colors (used for Buildable state) */
  currentPlayerColors?: PlayerColors | null;
  /** Hex size (circumradius) - road dimensions scale proportionally */
  hexSize: number;
  /** Click handler */
  onClick?: () => void;
  /** Additional className */
  className?: string;
}

/**
 * Edge rotation angles for each HexSide, matching Blazor BoardSvgConstants.RoadEdgeAngles.
 * Canonical polygon is horizontal (tips at left/right), so we rotate to match edge orientation.
 */
const EDGE_ANGLES: Record<HexSide, number> = {
  Top: 0,           // Edge runs horizontally
  TopRight: 60,     // Edge runs at 60°
  BottomRight: 120, // Edge runs at 120°
  Bottom: 180,      // Edge runs horizontally (same as 0°)
  BottomLeft: 240,  // Edge runs at 240°
  TopLeft: 300,     // Edge runs at 300°
};

/**
 * Neutral/default colors when no player colors are available
 */
const NEUTRAL_COLORS: PlayerColors = {
  primary: 'rgba(255, 255, 255, 0.3)',
  secondary: 'rgba(200, 200, 200, 0.3)',
  foreground: 'rgba(0, 0, 0, 0.5)',
};

/**
 * Calculate the canonical road polygon points for a given hex size.
 *
 * Geometry matches Blazor BoardSvgConstants.CalculateCanonicalRoadPolygon:
 * - Horizontal bowtie shape (tips at left and right)
 * - Tips at outer hex vertices (R/2 from edge midpoint)
 * - Inner points at inner hex vertices with perpendicular offset
 *
 * Inner hex ratio: 91/100 = 0.91 (from Blazor: InnerHexSize = HexSize - TileGap - InnerHexStrokeThickness * 0.5)
 */
function calculateRoadPolygon(hexSize: number): string {
  const R = hexSize;           // Outer hex circumradius
  const innerRatio = 0.91;     // InnerHexSize / HexSize (91/100 from Blazor)
  const r = R * innerRatio;    // Inner hex circumradius

  const apothemOuter = R * Math.sqrt(3) / 2;  // Distance from center to edge midpoint (outer)
  const apothemInner = r * Math.sqrt(3) / 2;  // Distance from center to edge midpoint (inner)

  const tipX = R / 2;          // Tip distance from midpoint along edge (50 at R=100)
  const innerX = r / 2;        // Inner vertex distance from midpoint along edge (45.5 at r=91)
  const perpDist = apothemOuter - apothemInner;  // Perpendicular offset for inner vertices (~7.8)

  // 6 points forming the bowtie shape (horizontal, centered at origin):
  // tip1 (left) -> innerA1 (left-top) -> innerA2 (right-top) -> tip2 (right) -> innerB2 (right-bottom) -> innerB1 (left-bottom)
  const points = [
    [-tipX, 0],           // tip1: left vertex
    [-innerX, -perpDist], // innerA1: left inner vertex, above edge
    [innerX, -perpDist],  // innerA2: right inner vertex, above edge
    [tipX, 0],            // tip2: right vertex
    [innerX, perpDist],   // innerB2: right inner vertex, below edge
    [-innerX, perpDist],  // innerB1: left inner vertex, below edge
  ];

  return points.map(([x, y]) => `${x.toFixed(1)},${y.toFixed(1)}`).join(' ');
}

/**
 * Road component - renders roads/ships as bowtie polygons matching Blazor implementation.
 *
 * Road states:
 * - Unowned: Invisible (no road built)
 * - Buildable: Shows at 50% opacity with current player colors
 * - Road: Shows fully opaque with owner colors
 * - Ship: Shows fully opaque with owner colors (similar to road)
 *
 * Hit testing: Only the polygon shape receives mouse events, not the container.
 */
export const Road = React.memo(function Road({
  roadState,
  side,
  ownerColors,
  currentPlayerColors,
  hexSize,
  onClick,
  className = '',
}: RoadProps) {
  const [isHovered, setIsHovered] = useState(false);

  // Unowned roads render nothing
  if (roadState === 'Unowned') {
    return null;
  }

  // Determine colors based on state
  const colors = roadState === 'Buildable'
    ? (currentPlayerColors ?? NEUTRAL_COLORS)
    : (ownerColors ?? NEUTRAL_COLORS);

  // Rotation based on edge
  const rotation = EDGE_ANGLES[side];

  // Calculate polygon points for this hex size
  const polygonPoints = calculateRoadPolygon(hexSize);

  // SVG viewBox needs to be large enough to contain the rotated polygon
  // The polygon extends tipX in each direction, so we need at least 2*tipX
  // Adding some margin for the stroke
  const viewBoxSize = hexSize * 1.2;
  const halfViewBox = viewBoxSize / 2;

  // Opacity: owned roads are fully visible, buildable only visible on hover
  const opacity = roadState === 'Buildable'
    ? (isHovered ? 0.7 : 0)  // Hidden until hovered
    : 1;

  // Gradient ID unique to this road (use stable ID based on side to avoid re-renders)
  const gradientId = `road-gradient-${side}`;

  return (
    <div
      className={className}
      style={{
        width: viewBoxSize,
        height: viewBoxSize,
        pointerEvents: 'none', // Container doesn't receive events
      }}
    >
      <svg
        width={viewBoxSize}
        height={viewBoxSize}
        viewBox={`${-halfViewBox} ${-halfViewBox} ${viewBoxSize} ${viewBoxSize}`}
        className="overflow-visible"
        style={{ pointerEvents: 'none' }} // SVG element doesn't receive events
      >
        <defs>
          <linearGradient id={gradientId} x1="0%" y1="0%" x2="100%" y2="100%">
            <stop offset="0%" stopColor={colors.primary} />
            <stop offset="50%" stopColor={colors.secondary} />
            <stop offset="100%" stopColor={colors.primary} />
          </linearGradient>
        </defs>
        <polygon
          points={polygonPoints}
          transform={`rotate(${rotation})`}
          fill={`url(#${gradientId})`}
          stroke={colors.foreground}
          strokeWidth={2}
          opacity={opacity}
          style={{
            transition: 'opacity 150ms',
            cursor: 'pointer',
            pointerEvents: 'auto', // Only polygon receives mouse events
          }}
          onClick={onClick}
          onMouseEnter={() => setIsHovered(true)}
          onMouseLeave={() => setIsHovered(false)}
        />
      </svg>
    </div>
  );
});

export default Road;
