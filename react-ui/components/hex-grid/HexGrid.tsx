'use client';

import { ReactNode } from 'react';
import { calculateHexDimensions, hexToPixel, HexCoordinate, PixelPosition } from './hex-geometry';
import { HexTile } from './HexTile';

/**
 * Hex grid item definition.
 */
export interface HexGridItem {
  /** Unique key for React */
  id: string;
  /** Hex coordinate in the grid */
  coord: HexCoordinate;
  /** Content to render */
  content: ReactNode;
  /** Optional className for this specific tile */
  className?: string;
  /** Click handler */
  onClick?: () => void;
  /** Whether tile is disabled */
  disabled?: boolean;
}

/**
 * Props for the HexGrid component.
 */
export interface HexGridProps {
  /** Hex circumradius (distance from center to vertex) */
  hexSize: number;
  /** Array of hex items to render */
  items: HexGridItem[];
  /** Optional className for container */
  className?: string;
  /** Optional scale factor for zoom (default: 1.0) */
  scale?: number;
}

/**
 * Hex grid layout engine.
 *
 * Automatically positions hex tiles based on axial coordinates using
 * Red Blob Games formulas. Supports zoom via scale parameter.
 *
 * The grid:
 * 1. Calculates pixel positions for each hex coordinate
 * 2. Determines bounding box to size container
 * 3. Centers the origin (0,0) hex in the container
 * 4. Applies scale transform for zoom
 *
 * Usage:
 * ```tsx
 * <HexGrid
 *   hexSize={100}
 *   scale={0.8}
 *   items={[
 *     {
 *       id: 'center',
 *       coord: { q: 0, r: 0 },
 *       content: <div>Center</div>,
 *     },
 *     {
 *       id: 'north',
 *       coord: { q: 0, r: -1 },
 *       content: <div>North</div>,
 *     },
 *   ]}
 * />
 * ```
 */
export function HexGrid({
  hexSize,
  items,
  className = '',
  scale = 1.0,
}: HexGridProps): React.ReactElement {
  const dims = calculateHexDimensions(hexSize);

  // Calculate pixel positions for all items
  const positions = items.map(item => hexToPixel(item.coord, hexSize));

  // Calculate bounding box
  const minX = Math.min(...positions.map(p => p.x));
  const maxX = Math.max(...positions.map(p => p.x));
  const minY = Math.min(...positions.map(p => p.y));
  const maxY = Math.max(...positions.map(p => p.y));

  // Container dimensions (add hex width/height to account for tile size)
  const containerWidth = maxX - minX + dims.width;
  const containerHeight = maxY - minY + dims.height;

  // Origin offset so leftmost/topmost tile edges align with container edges
  const origin: PixelPosition = {
    x: dims.width / 2 - minX,
    y: dims.height / 2 - minY,
  };

  return (
    <div
      className={`relative ${className}`}
      style={{
        width: `${containerWidth}px`,
        height: `${containerHeight}px`,
        transform: `scale(${scale})`,
        transformOrigin: 'center center',
        margin: '0 auto',
      }}
    >
      {items.map(item => {
        const pos = hexToPixel(item.coord, hexSize, origin);

        return (
          <HexTile
            key={item.id}
            width={dims.width}
            height={dims.height}
            position={pos}
            className={item.className}
            onClick={item.onClick}
            disabled={item.disabled}
          >
            {item.content}
          </HexTile>
        );
      })}
    </div>
  );
}
