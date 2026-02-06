'use client';

import React, { useState } from 'react';
import type { PlayerColors } from '@/types/player-profile';
import { usePlayerColors } from '@/lib/stores/gameStoreHooks';

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
  /** Owner player ID - component looks up colors via usePlayerColors */
  ownerId?: string | null;
  /** Current turn player ID - used for Buildable state colors */
  currentPlayerId?: string | null;
  /** Hex size (circumradius) - road dimensions scale proportionally */
  hexSize: number;
  /** Build index for numbered display (0 = no number, >0 = show number) */
  buildIndex?: number;
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
 * Convert numeric index to alphanumeric label (0-9, then A-Z)
 * @param index - Zero-based index
 * @returns Alphanumeric label (0, 1, ..., 9, A, B, ..., Z)
 */
function indexToAlphanumeric(index: number): string {
  if (index < 0) return '0';
  if (index <= 9) return index.toString();
  // After 9, use A-Z (index 10 = A, 11 = B, etc.)
  const letterIndex = index - 10;
  if (letterIndex < 26) {
    return String.fromCharCode(65 + letterIndex); // 65 is 'A'
  }
  // If we somehow have more than 36 items, just show the number
  return index.toString();
}

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
  ownerId,
  currentPlayerId,
  hexSize,
  buildIndex = 0,
  onClick,
  className = '',
}: RoadProps) {
  const [_isHovered, setIsHovered] = useState(false);

  // Look up player colors from store
  const ownerColors = usePlayerColors(ownerId);
  const currentPlayerColors = usePlayerColors(currentPlayerId);

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
  // Adding some margin for the stroke and build index label
  const viewBoxSize = hexSize * 1.2;
  const halfViewBox = viewBoxSize / 2;

  // Opacity: owned roads are fully visible, buildable at 50% (matches Blazor)
  const opacity = roadState === 'Buildable' ? 0.5 : 1;

  // Gradient ID must include colors to ensure correct rendering when player changes
  // Using color values in ID prevents gradient caching issues across different players
  const colorKey = `${colors.primary}-${colors.secondary}`.replace(/[^a-zA-Z0-9]/g, '');
  const gradientId = `road-gradient-${side}-${colorKey}`;

  // Scale factor for build index label (relative to Blazor's 36x28 at hexSize=100)
  const labelScale = hexSize / 100;

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
          {/* 2-stop gradient matching Blazor SvgGradientStops */}
          <linearGradient id={gradientId} x1="0%" y1="0%" x2="100%" y2="100%">
            <stop offset="0%" stopColor={colors.primary} />
            <stop offset="100%" stopColor={colors.secondary} />
          </linearGradient>
        </defs>
        <polygon
          points={polygonPoints}
          transform={`rotate(${rotation})`}
          fill={`url(#${gradientId})`}
          stroke={colors.foreground}
          strokeWidth={Math.max(2, hexSize * 0.05)}
          opacity={opacity}
          style={{
            transition: 'opacity 150ms',
            cursor: roadState === 'Buildable' ? 'pointer' : 'default',
            pointerEvents: 'auto', // Only polygon receives mouse events
          }}
          onClick={onClick}
          onMouseEnter={() => setIsHovered(true)}
          onMouseLeave={() => setIsHovered(false)}
        />
        {/* Build index label - black rounded rect with white alphanumeric (0-9, A-Z) */}
        {buildIndex > 0 && (
          <g style={{ pointerEvents: 'auto', cursor: 'pointer' }} onClick={onClick}>
            <rect
              x={-18 * labelScale}
              y={-14 * labelScale}
              width={36 * labelScale}
              height={28 * labelScale}
              rx={6 * labelScale}
              fill="black"
            />
            <text
              x={0}
              y={0}
              textAnchor="middle"
              dominantBaseline="central"
              fontFamily="Segoe UI, sans-serif"
              fontSize={24 * labelScale}
              fontWeight="bold"
              fill="white"
            >
              {indexToAlphanumeric(buildIndex)}
            </text>
          </g>
        )}
      </svg>
    </div>
  );
});

export default Road;
