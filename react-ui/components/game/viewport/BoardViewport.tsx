'use client';

/**
 * BoardViewport - Infinite pan/zoom viewport for the game board.
 *
 * Renders an infinite grid of water hexes as the background, with the game
 * board tiles rendered at their specific coordinates. All hexes use the
 * same grid - board tiles are simply hexes that render game content instead
 * of water.
 */

import { useState, useMemo, useCallback, useRef, useEffect, ReactNode } from 'react';
import {
  HexCoordinate,
  cubicCoord,
  hexToPixel,
} from '@/components/hex-grid';
import { useLayoutStore } from '@/lib/stores/layoutStore';
import { WaterHex } from '@/components/hex-grid/content';

interface BoardViewportProps {
  /** Hex size in pixels (circumradius) */
  hexSize?: number;
  /** Gap between hexes in pixels */
  gap?: number;
  /** Children to render inside the viewport (floating panels, etc.) */
  children?: ReactNode;
  /** Render function for board content at a coordinate */
  renderHex?: (coord: HexCoordinate) => ReactNode;
}

/** Default hex size matching Blazor BoardSvgConstants */
const DEFAULT_HEX_SIZE = 100;
const DEFAULT_GAP = 2;

export function BoardViewport({
  hexSize = DEFAULT_HEX_SIZE,
  gap = DEFAULT_GAP,
  children,
  renderHex,
}: BoardViewportProps): React.ReactElement {
  // Get viewport state from store
  const viewport = useLayoutStore((state) => state.viewport);
  const setViewport = useLayoutStore((state) => state.setViewport);

  // Local pan state for smooth dragging
  const [pan, setPan] = useState(viewport.pan);
  const [zoom, setZoom] = useState(viewport.zoom);
  const [isPanning, setIsPanning] = useState(false);
  const panStartRef = useRef({ x: 0, y: 0, panX: 0, panY: 0 });
  const viewportRef = useRef<HTMLDivElement>(null);
  const [viewportSize, setViewportSize] = useState({ width: 800, height: 600 });

  // Sync with store on mount
  useEffect(() => {
    setPan(viewport.pan);
    setZoom(viewport.zoom);
  }, [viewport.pan, viewport.zoom]);

  // Track viewport size
  useEffect(() => {
    const viewportEl = viewportRef.current;
    if (!viewportEl) return;

    const observer = new ResizeObserver((entries) => {
      const entry = entries[0];
      if (entry) {
        setViewportSize({
          width: entry.contentRect.width,
          height: entry.contentRect.height,
        });
      }
    });

    observer.observe(viewportEl);
    return () => observer.disconnect();
  }, []);

  // Pan handlers
  const handlePanStart = useCallback((e: React.MouseEvent) => {
    // Only pan on left mouse button
    if (e.button !== 0) return;
    e.preventDefault();
    setIsPanning(true);
    panStartRef.current = {
      x: e.clientX,
      y: e.clientY,
      panX: pan.x,
      panY: pan.y,
    };
  }, [pan]);

  useEffect(() => {
    if (!isPanning) return;

    const handleMouseMove = (e: MouseEvent) => {
      const deltaX = e.clientX - panStartRef.current.x;
      const deltaY = e.clientY - panStartRef.current.y;
      setPan({
        x: panStartRef.current.panX + deltaX,
        y: panStartRef.current.panY + deltaY,
      });
    };

    const handleMouseUp = () => {
      setIsPanning(false);
      // Save to store on pan end
      setViewport({ pan });
    };

    document.addEventListener('mousemove', handleMouseMove);
    document.addEventListener('mouseup', handleMouseUp);
    return () => {
      document.removeEventListener('mousemove', handleMouseMove);
      document.removeEventListener('mouseup', handleMouseUp);
    };
  }, [isPanning, pan, setViewport]);

  // Zoom handler - zooms to center
  const handleWheel = useCallback((e: React.WheelEvent) => {
    e.preventDefault();

    const zoomFactor = e.deltaY > 0 ? 0.9 : 1.1;
    const newZoom = Math.max(0.3, Math.min(3, zoom * zoomFactor));

    setZoom(newZoom);
    setViewport({ zoom: newZoom });
  }, [zoom, setViewport]);

  // Calculate effective hex size after zoom
  const effectiveHexSize = hexSize * zoom;

  // Calculate visible hex coordinates
  const visibleHexCoords = useMemo((): HexCoordinate[] => {
    const sqrt3 = Math.sqrt(3);

    // Viewport bounds in world coordinates
    const halfWidth = viewportSize.width / 2;
    const halfHeight = viewportSize.height / 2;

    const worldLeft = -halfWidth - pan.x;
    const worldRight = halfWidth - pan.x;
    const worldTop = -halfHeight - pan.y;
    const worldBottom = halfHeight - pan.y;

    // Margin to ensure complete filling
    const marginX = viewportSize.width;
    const marginY = viewportSize.height;

    // Calculate q range
    const minQ = Math.floor((worldLeft - marginX) / (1.5 * effectiveHexSize));
    const maxQ = Math.ceil((worldRight + marginX) / (1.5 * effectiveHexSize));

    // Generate hexes
    const coords: HexCoordinate[] = [];

    for (let q = minQ; q <= maxQ; q++) {
      const minR = Math.floor((worldTop - marginY) / (effectiveHexSize * sqrt3) - q / 2);
      const maxR = Math.ceil((worldBottom + marginY) / (effectiveHexSize * sqrt3) - q / 2);

      for (let r = minR; r <= maxR; r++) {
        coords.push(cubicCoord(q, r));
      }
    }

    return coords;
  }, [effectiveHexSize, pan, viewportSize]);

  // Render a single hex
  const renderHexContent = useCallback((coord: HexCoordinate) => {
    // If custom render function provided, use it
    if (renderHex) {
      const content = renderHex(coord);
      if (content) return content;
    }

    // Default: render water hex
    return <WaterHex />;
  }, [renderHex]);

  return (
    <div
      ref={viewportRef}
      className="relative w-full h-full overflow-hidden bg-blue-950 cursor-grab active:cursor-grabbing"
      onMouseDown={handlePanStart}
      onWheel={handleWheel}
    >
      {/* Transform container - origin at viewport center */}
      <div
        className="absolute pointer-events-none"
        style={{
          left: '50%',
          top: '50%',
          transform: `translate(${pan.x}px, ${pan.y}px)`,
        }}
      >
        {/* Render each hex */}
        {visibleHexCoords.map((coord) => {
          const pixel = hexToPixel(coord, effectiveHexSize);
          const hexWidth = effectiveHexSize * 2;
          const hexHeight = effectiveHexSize * Math.sqrt(3);
          const contentScale = 1 - (gap / effectiveHexSize);

          return (
            <div
              key={`hex-${coord.q}-${coord.r}`}
              className="absolute pointer-events-auto"
              style={{
                left: `${pixel.x}px`,
                top: `${pixel.y}px`,
                width: `${hexWidth}px`,
                height: `${hexHeight}px`,
                transform: 'translate(-50%, -50%)',
              }}
            >
              {/* Content layer with gap */}
              <div
                className="absolute hex-clip-flat"
                style={{
                  width: `${hexWidth * contentScale}px`,
                  height: `${hexHeight * contentScale}px`,
                  left: '50%',
                  top: '50%',
                  transform: 'translate(-50%, -50%)',
                }}
              >
                {renderHexContent(coord)}
              </div>
            </div>
          );
        })}
      </div>

      {/* Floating panels and other children */}
      <div className="absolute inset-0 pointer-events-none">
        <div className="pointer-events-auto">
          {children}
        </div>
      </div>

      {/* Zoom indicator */}
      <div className="absolute bottom-4 right-4 text-xs font-mono text-white/50 bg-black/30 px-2 py-1 rounded">
        {(zoom * 100).toFixed(0)}%
      </div>
    </div>
  );
}

export default BoardViewport;
