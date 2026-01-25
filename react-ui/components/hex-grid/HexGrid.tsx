'use client';

import { ReactNode, useMemo, useRef, useState, useEffect } from 'react';
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
  /** Exclude from bounding box calculation (renders but doesn't affect grid size) */
  excludeFromBounds?: boolean;
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
  /** Gap between hex edges in pixels (default: 4) */
  gap?: number;
  /** Optional border color for all hexes (CSS color string) */
  borderColor?: string;
  /** Scale to fit within parent container (default: false) */
  fitToParent?: boolean;
  /** Padding when using fitToParent (default: 8) */
  fitPadding?: number;
}

/**
 * Hex grid layout engine with two-pass rendering.
 *
 * Automatically positions hex tiles based on cubic coordinates using
 * Red Blob Games formulas. Supports zoom via scale parameter.
 *
 * The grid:
 * 1. Calculates pixel positions for each hex coordinate
 * 2. Determines bounding box to size container
 * 3. Centers the origin (0,0) hex in the container
 * 4. Applies scale transform for zoom
 *
 * Two-pass rendering:
 * - Pass 1: Optional border layer at full hex size (if borderColor provided)
 * - Pass 2: Content layer at reduced size (creates gap between hexes)
 *
 * Fit to parent:
 * - When `fitToParent` is true, the grid measures its parent container
 * - Automatically scales down (never up) to fit within the parent
 * - Centers the grid within the parent container
 * - Uses ResizeObserver for responsive behavior
 *
 * Usage:
 * ```tsx
 * // Fixed size grid
 * <HexGrid
 *   hexSize={100}
 *   gap={4}
 *   items={[...]}
 * />
 *
 * // Fit to parent container (scales down if needed)
 * <div className="w-full h-[400px]">
 *   <HexGrid
 *     hexSize={100}
 *     gap={4}
 *     fitToParent
 *     items={[...]}
 *   />
 * </div>
 * ```
 */
export function HexGrid({
  hexSize,
  items,
  className = '',
  scale = 1.0,
  gap = 4,
  borderColor,
  fitToParent = false,
  fitPadding = 8,
}: HexGridProps): React.ReactElement {
  const wrapperRef = useRef<HTMLDivElement>(null);
  const [parentSize, setParentSize] = useState<{ width: number; height: number } | null>(null);

  // Measure parent container when fitToParent is enabled
  useEffect(() => {
    if (!fitToParent) return;

    const measureParent = (): void => {
      const parent = wrapperRef.current?.parentElement;
      if (parent) {
        setParentSize({
          width: parent.clientWidth,
          height: parent.clientHeight,
        });
      }
    };

    measureParent();

    // Use ResizeObserver for responsive behavior
    const parent = wrapperRef.current?.parentElement;
    if (!parent) return;

    const observer = new ResizeObserver(measureParent);
    observer.observe(parent);

    return () => observer.disconnect();
  }, [fitToParent]);

  // Memoize hex dimensions to avoid recalculating on every render
  const dims = useMemo(() => calculateHexDimensions(hexSize, gap), [hexSize, gap]);

  // Calculate inner content scale based on gap
  // Content renders smaller to reveal gap between hexes
  const contentScale = useMemo(() => {
    return 1 - (gap / hexSize);
  }, [gap, hexSize]);

  // Memoize layout calculations (positions, bounding box, container size, origin)
  // Only recalculates when items coordinates or hexSize changes
  const layout = useMemo(() => {
    // Filter items for bounding box calculation (exclude items marked excludeFromBounds)
    const boundsItems = items.filter(item => !item.excludeFromBounds);
    const boundsPositions = boundsItems.map(item => hexToPixel(item.coord, hexSize));

    // Calculate bounding box from items that affect layout
    const minX = Math.min(...boundsPositions.map(p => p.x));
    const maxX = Math.max(...boundsPositions.map(p => p.x));
    const minY = Math.min(...boundsPositions.map(p => p.y));
    const maxY = Math.max(...boundsPositions.map(p => p.y));

    // Container dimensions (add hex width/height to account for tile size)
    const containerWidth = maxX - minX + dims.width;
    const containerHeight = maxY - minY + dims.height;

    // Origin offset so leftmost/topmost tile edges align with container edges
    const origin: PixelPosition = {
      x: dims.width / 2 - minX,
      y: dims.height / 2 - minY,
    };

    return { containerWidth, containerHeight, origin };
  }, [items, hexSize, dims]);

  // Calculate fit scale based on parent size
  const fitScale = useMemo(() => {
    if (!fitToParent || !parentSize) return 1.0;

    const availableWidth = parentSize.width - fitPadding * 2;
    const availableHeight = parentSize.height - fitPadding * 2;

    const scaleX = availableWidth / layout.containerWidth;
    const scaleY = availableHeight / layout.containerHeight;

    // Use the smaller scale to fit both dimensions, but don't scale up beyond 1.0
    return Math.min(scaleX, scaleY, 1.0);
  }, [fitToParent, parentSize, layout, fitPadding]);

  // Final scale combines user scale with fit scale
  const finalScale = scale * fitScale;

  // Calculate scaled dimensions for proper centering
  const scaledWidth = layout.containerWidth * finalScale;
  const scaledHeight = layout.containerHeight * finalScale;

  // When fitToParent, use a wrapper that fills the parent
  if (fitToParent) {
    return (
      <div
        ref={wrapperRef}
        className={`w-full h-full flex items-center justify-center ${className}`}
      >
        <div
          className="relative"
          style={{
            width: `${layout.containerWidth}px`,
            height: `${layout.containerHeight}px`,
            transform: `scale(${finalScale})`,
            transformOrigin: 'center center',
          }}
        >
          {/* Pass 1: Border layer (if borderColor provided) */}
          {borderColor && items.map(item => {
            const pos = hexToPixel(item.coord, hexSize, layout.origin);

            return (
              <div
                key={`border-${item.id}`}
                className="absolute hex-clip-flat"
                style={{
                  width: `${dims.width}px`,
                  height: `${dims.height}px`,
                  left: `${pos.x}px`,
                  top: `${pos.y}px`,
                  transform: 'translate(-50%, -50%)',
                  background: borderColor,
                }}
              />
            );
          })}

          {/* Pass 2: Content layer */}
          {items.map(item => {
            const pos = hexToPixel(item.coord, hexSize, layout.origin);

            return (
              <HexTile
                key={item.id}
                width={dims.width * contentScale}
                height={dims.height * contentScale}
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
      </div>
    );
  }

  // Standard rendering (no fitToParent)
  return (
    <div
      className={`relative ${className}`}
      style={{
        width: `${scaledWidth}px`,
        height: `${scaledHeight}px`,
        margin: '0 auto',
      }}
    >
      <div
        className="absolute"
        style={{
          width: `${layout.containerWidth}px`,
          height: `${layout.containerHeight}px`,
          transform: `scale(${finalScale})`,
          transformOrigin: 'top left',
          left: '50%',
          top: '50%',
          marginLeft: `-${layout.containerWidth / 2}px`,
          marginTop: `-${layout.containerHeight / 2}px`,
        }}
      >
        {/* Pass 1: Border layer (if borderColor provided) */}
        {borderColor && items.map(item => {
          const pos = hexToPixel(item.coord, hexSize, layout.origin);

          return (
            <div
              key={`border-${item.id}`}
              className="absolute hex-clip-flat"
              style={{
                width: `${dims.width}px`,
                height: `${dims.height}px`,
                left: `${pos.x}px`,
                top: `${pos.y}px`,
                transform: 'translate(-50%, -50%)',
                background: borderColor,
              }}
            />
          );
        })}

        {/* Pass 2: Content layer */}
        {items.map(item => {
          const pos = hexToPixel(item.coord, hexSize, layout.origin);

          return (
            <HexTile
              key={item.id}
              width={dims.width * contentScale}
              height={dims.height * contentScale}
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
    </div>
  );
}
