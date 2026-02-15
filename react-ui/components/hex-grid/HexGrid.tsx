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
 * Layout info passed to overlay render prop
 */
export interface HexGridLayoutInfo {
  /** Origin offset for hexToPixel calculations */
  origin: PixelPosition;
  /** Hex circumradius */
  hexSize: number;
  /** Hex dimensions (width, height) */
  dims: { width: number; height: number };
}

/**
 * Props for the HexGrid component.
 *
 * Supports two rendering modes:
 *
 * 1. **Explicit items** (existing): Pass `items` array with coordinates and content per item.
 * 2. **Layout-driven** (ItemsControl pattern): Pass `coordinates` + `renderItem`.
 *    HexGrid generates positions from the coordinate array; caller provides a render
 *    function for each position. Use with `getSpiralCoordinates(n)`, `LAYOUTS`, etc.
 *
 * These modes are mutually exclusive -- provide one or the other, not both.
 */
export interface HexGridProps {
  /** Hex circumradius (distance from center to vertex) */
  hexSize: number;

  // --- Mode 1: Explicit items ---
  /** Array of hex items to render (mutually exclusive with coordinates + renderItem) */
  items?: HexGridItem[];

  // --- Mode 2: Layout-driven (ItemsControl) ---
  /** Hex coordinates for each item (from getSpiralCoordinates, LAYOUTS, etc.) */
  coordinates?: HexCoordinate[];
  /** Render function called for each coordinate position */
  renderItem?: (coord: HexCoordinate, index: number) => ReactNode;
  /** Optional explicit IDs per item (default: "item-{index}") */
  itemIds?: string[];
  /** Shared className applied to every item */
  itemClassName?: string;
  /** Per-item flag to exclude from bounding box calculation */
  excludeFromBounds?: boolean[];

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
  /** Maximum scale when using fitToParent (default: Infinity, no limit) */
  maxScale?: number;
  /** Render prop for overlay content (rendered in same coordinate space as hexes) */
  overlay?: (layoutInfo: HexGridLayoutInfo) => ReactNode;
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
  items: explicitItems,
  coordinates,
  renderItem,
  itemIds,
  itemClassName,
  excludeFromBounds: excludeFromBoundsArr,
  className = '',
  scale = 1.0,
  gap = 4,
  borderColor,
  fitToParent = false,
  fitPadding = 8,
  maxScale = Infinity,
  overlay,
}: HexGridProps): React.ReactElement {
  const wrapperRef = useRef<HTMLDivElement>(null);
  const [parentSize, setParentSize] = useState<{ width: number; height: number } | null>(null);

  // Derive items from coordinates + renderItem when in layout-driven mode
  const items = useMemo((): HexGridItem[] => {
    if (explicitItems && coordinates) {
      console.error('HexGrid: Provide either "items" or "coordinates + renderItem", not both.');
    }
    if (coordinates && renderItem) {
      return coordinates.map((coord, i) => ({
        id: itemIds?.[i] ?? `item-${i}`,
        coord,
        content: renderItem(coord, i),
        className: itemClassName,
        excludeFromBounds: excludeFromBoundsArr?.[i],
      }));
    }
    return explicitItems ?? [];
  }, [explicitItems, coordinates, renderItem, itemIds, itemClassName, excludeFromBoundsArr]);

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
    return 1 - gap / hexSize;
  }, [gap, hexSize]);

  // Memoize layout calculations (positions, bounding box, container size, origin)
  // Only recalculates when items coordinates or hexSize changes
  const layout = useMemo(() => {
    // Filter items for bounding box calculation (exclude items marked excludeFromBounds)
    const boundsItems = items.filter((item) => !item.excludeFromBounds);
    const boundsPositions = boundsItems.map((item) => hexToPixel(item.coord, hexSize));

    // Calculate bounding box from items that affect layout
    const minX = Math.min(...boundsPositions.map((p) => p.x));
    const maxX = Math.max(...boundsPositions.map((p) => p.x));
    const minY = Math.min(...boundsPositions.map((p) => p.y));
    const maxY = Math.max(...boundsPositions.map((p) => p.y));

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

    // Use the smaller scale to fit both dimensions, capped by maxScale
    return Math.min(scaleX, scaleY, maxScale);
  }, [fitToParent, parentSize, layout, fitPadding, maxScale]);

  // Final scale combines user scale with fit scale
  const finalScale = scale * fitScale;

  // Calculate scaled dimensions for proper centering
  const scaledWidth = layout.containerWidth * finalScale;
  const scaledHeight = layout.containerHeight * finalScale;

  // When fitToParent, use a wrapper that fills the parent
  // We need to wrap the scaled content in a container sized to the SCALED dimensions
  // so that flex centering works correctly (transform doesn't affect layout size)
  if (fitToParent) {
    return (
      <div
        ref={wrapperRef}
        className={`w-full h-full flex items-center justify-center ${className}`}
      >
        {/* This container is sized to the SCALED dimensions for proper flex centering */}
        <div
          style={{
            width: `${scaledWidth}px`,
            height: `${scaledHeight}px`,
          }}
        >
          {/* This container holds the actual content at original size, scaled down */}
          <div
            className="relative"
            style={{
              width: `${layout.containerWidth}px`,
              height: `${layout.containerHeight}px`,
              transform: `scale(${finalScale})`,
              transformOrigin: 'top left',
            }}
          >
            {/* Pass 1: Border layer (if borderColor provided) */}
            {borderColor &&
              items.map((item) => {
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
            {items.map((item) => {
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

            {/* Overlay layer (buildings, roads, etc.) */}
            {overlay?.({ origin: layout.origin, hexSize, dims })}
          </div>
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
          left: 0,
          top: 0,
        }}
      >
        {/* Pass 1: Border layer (if borderColor provided) */}
        {borderColor &&
          items.map((item) => {
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
        {items.map((item) => {
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

        {/* Overlay layer (buildings, roads, etc.) */}
        {overlay?.({ origin: layout.origin, hexSize, dims })}
      </div>
    </div>
  );
}
