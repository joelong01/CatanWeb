'use client';

import React, { memo, useMemo } from 'react';
import type { HarborModel, HexSide } from '@/lib/test-data/expansion-game';
import { HARBOR_IMAGES } from '@/lib/test-data/expansion-game';
import { hexToPixel, cubicCoord, getEdgeMidpoint } from '@/components/hex-grid/hex-geometry';

/**
 * Props for HarborHex component
 */
export interface HarborHexProps {
  /** The harbor model data */
  harbor: HarborModel;
  /** Hex size (circumradius) in pixels */
  hexSize: number;
  /** Origin offset for coordinate system */
  origin?: { x: number; y: number };
}

/**
 * Edge vertex indices for each hex side (for creating the water triangle)
 * Each side connects two adjacent vertices.
 */
const EDGE_VERTICES: Record<HexSide, [number, number]> = {
  Top: [5, 0],      // TopLeft vertex (5) to Right vertex (0)
  TopRight: [0, 1], // Right (0) to BottomRight (1)
  BottomRight: [1, 2], // BottomRight (1) to BottomLeft (2)
  Bottom: [2, 3],   // BottomLeft (2) to Left (3)
  BottomLeft: [3, 4], // Left (3) to TopLeft (4)
  TopLeft: [4, 5],  // TopLeft (4) to Top (5)
};

/**
 * Gets the two vertex positions for a hex edge
 */
function getEdgeVertices(
  centerX: number,
  centerY: number,
  hexSize: number,
  side: HexSide
): { v1: { x: number; y: number }; v2: { x: number; y: number } } {
  const [idx1, idx2] = EDGE_VERTICES[side];

  // Flat-topped hex vertices: 0° = right, going counterclockwise
  // Vertex angles: 0°, 60°, 120°, 180°, 240°, 300°
  const angles = [0, 60, 120, 180, 240, 300];

  const getVertex = (idx: number) => {
    const angleRad = (angles[idx] * Math.PI) / 180;
    return {
      x: centerX + hexSize * Math.cos(angleRad),
      y: centerY + hexSize * Math.sin(angleRad),
    };
  };

  return {
    v1: getVertex(idx1),
    v2: getVertex(idx2),
  };
}

/**
 * HarborHex - Renders a harbor with water triangle and circular icon.
 *
 * Harbor rendering (from HarborSvgRenderer.cs):
 * 1. Calculate edge midpoint for the connected side
 * 2. Calculate outward normal direction
 * 3. Position harbor circle offset outward from edge
 * 4. Draw water triangle from edge vertices to harbor center
 * 5. Draw harbor circle with icon
 */
export const HarborHex = memo(function HarborHex({
  harbor,
  hexSize,
  origin = { x: 0, y: 0 },
}: HarborHexProps) {
  const { harborKey } = harbor;
  const { hexCoordinates, harborType, side } = harborKey;

  // Calculate positions
  const positions = useMemo(() => {
    // Get tile center
    const coord = cubicCoord(hexCoordinates.q, hexCoordinates.r);
    const center = hexToPixel(coord, hexSize, origin);

    // Get edge vertices
    const { v1, v2 } = getEdgeVertices(center.x, center.y, hexSize, side);

    // Calculate edge midpoint
    const midX = (v1.x + v2.x) / 2;
    const midY = (v1.y + v2.y) / 2;

    // Calculate outward normal direction
    const dx = midX - center.x;
    const dy = midY - center.y;
    const length = Math.sqrt(dx * dx + dy * dy);
    const normX = dx / length;
    const normY = dy / length;

    // Harbor position offset outward from edge (similar to HarborOffset in C#)
    const harborOffset = hexSize * 0.65;
    const harborX = midX + normX * harborOffset;
    const harborY = midY + normY * harborOffset;

    // Harbor circle radius
    const circleRadius = hexSize * 0.35;

    return {
      v1,
      v2,
      harborX,
      harborY,
      circleRadius,
    };
  }, [hexCoordinates.q, hexCoordinates.r, side, hexSize, origin]);

  const imageUrl = HARBOR_IMAGES[harborType];
  if (!imageUrl || harborType === 'None') return null;

  const { v1, v2, harborX, harborY, circleRadius } = positions;

  // SVG polygon points for water triangle
  const trianglePoints = `${v1.x},${v1.y} ${v2.x},${v2.y} ${harborX},${harborY}`;

  return (
    <g className="harbor" data-type={harborType}>
      {/* Water triangle background */}
      <polygon
        points={trianglePoints}
        fill="url(#pattern-water)"
        className="harbor-triangle"
      />

      {/* Harbor circle with icon */}
      <circle
        cx={harborX}
        cy={harborY}
        r={circleRadius}
        fill="#f5f5dc" /* beige background */
        stroke="#2a5d8f"
        strokeWidth={3}
        className="harbor-circle"
      />

      {/* Harbor icon (using clipPath to circular crop) */}
      <clipPath id={`harbor-clip-${hexCoordinates.q}-${hexCoordinates.r}`}>
        <circle cx={harborX} cy={harborY} r={circleRadius - 2} />
      </clipPath>
      <image
        href={imageUrl}
        x={harborX - circleRadius + 2}
        y={harborY - circleRadius + 2}
        width={(circleRadius - 2) * 2}
        height={(circleRadius - 2) * 2}
        clipPath={`url(#harbor-clip-${hexCoordinates.q}-${hexCoordinates.r})`}
        preserveAspectRatio="xMidYMid slice"
      />
    </g>
  );
});

export default HarborHex;
