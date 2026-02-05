'use client';

import { useState, useMemo, useCallback, useRef, useEffect } from 'react';
import { MainLayout } from '@/components/layout';
import {
  HexGrid,
  HexGridItem,
  HexCoordinate,
  HEX_LAYOUTS,
  getSpiralCoordinates,
  getRingCoordinates,
  getLineCoordinates,
  pixelToHex,
  hexToPixel,
  cubicCoord,
  distance,
  Direction,
  DIRECTION_VECTORS,
  ALL_DIRECTIONS,
} from '@/components/hex-grid';

type LayoutMode = 'spiral' | 'ring' | 'cluster7' | 'cluster19' | 'cluster30' | 'line';
type ViewMode = 'normal' | 'scaleToFit' | 'infinite';

/**
 * Interactive hex grid test page.
 * Allows exploration of all hex-geometry features with live controls.
 */
export default function HexTestPage(): React.ReactElement {
  // Grid parameters
  const [hexSize, setHexSize] = useState(80);
  const [gap, setGap] = useState(4);
  const [layoutMode, setLayoutMode] = useState<LayoutMode>('spiral');
  const [count, setCount] = useState(7);
  const [ringRadius, setRingRadius] = useState(2);
  const [showCoords, setShowCoords] = useState(true);
  const [showBorder, setShowBorder] = useState(true);
  const [borderColor, setBorderColor] = useState('#f59e0b'); // amber-500
  const [showWaterTexture, setShowWaterTexture] = useState(false);
  const [viewMode, setViewMode] = useState<ViewMode>('normal');

  // Infinite viewport state
  const [pan, setPan] = useState({ x: 0, y: 0 });
  const [zoom, setZoom] = useState(1);
  const [isPanning, setIsPanning] = useState(false);
  const panStartRef = useRef({ x: 0, y: 0, panX: 0, panY: 0 });
  const viewportRef = useRef<HTMLDivElement>(null);
  const [viewportSize, setViewportSize] = useState({ width: 800, height: 600 });

  // Line drawing state
  const [lineStart, setLineStart] = useState<HexCoordinate | null>(null);
  const [lineEnd, setLineEnd] = useState<HexCoordinate | null>(null);

  // Click feedback
  const [lastClicked, setLastClicked] = useState<HexCoordinate | null>(null);

  // Resizable container state
  const [boxSize, setBoxSize] = useState({ width: 600, height: 500 });
  const [isResizing, setIsResizing] = useState(false);
  const resizeStartRef = useRef({ x: 0, y: 0, width: 0, height: 0 });

  // Handle resize drag
  const handleResizeStart = useCallback((e: React.MouseEvent) => {
    e.preventDefault();
    setIsResizing(true);
    resizeStartRef.current = {
      x: e.clientX,
      y: e.clientY,
      width: boxSize.width,
      height: boxSize.height,
    };
  }, [boxSize]);

  useEffect(() => {
    if (!isResizing) return;

    const handleMouseMove = (e: MouseEvent) => {
      const deltaX = e.clientX - resizeStartRef.current.x;
      const deltaY = e.clientY - resizeStartRef.current.y;
      setBoxSize({
        width: Math.max(200, resizeStartRef.current.width + deltaX),
        height: Math.max(200, resizeStartRef.current.height + deltaY),
      });
    };

    const handleMouseUp = () => {
      setIsResizing(false);
    };

    document.addEventListener('mousemove', handleMouseMove);
    document.addEventListener('mouseup', handleMouseUp);
    return () => {
      document.removeEventListener('mousemove', handleMouseMove);
      document.removeEventListener('mouseup', handleMouseUp);
    };
  }, [isResizing]);

  // Track viewport size for infinite mode
  useEffect(() => {
    const viewport = viewportRef.current;
    if (!viewport) return;

    const observer = new ResizeObserver((entries) => {
      const entry = entries[0];
      if (entry) {
        setViewportSize({
          width: entry.contentRect.width,
          height: entry.contentRect.height,
        });
      }
    });

    observer.observe(viewport);
    return () => observer.disconnect();
  }, []);

  // Pan handlers for infinite mode
  const handlePanStart = useCallback((e: React.MouseEvent) => {
    if (viewMode !== 'infinite') return;
    e.preventDefault();
    setIsPanning(true);
    panStartRef.current = {
      x: e.clientX,
      y: e.clientY,
      panX: pan.x,
      panY: pan.y,
    };
  }, [viewMode, pan]);

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
    };

    document.addEventListener('mousemove', handleMouseMove);
    document.addEventListener('mouseup', handleMouseUp);
    return () => {
      document.removeEventListener('mousemove', handleMouseMove);
      document.removeEventListener('mouseup', handleMouseUp);
    };
  }, [isPanning]);

  // Zoom handler for infinite mode - zooms to CENTER (origin stays fixed)
  const handleWheel = useCallback((e: React.WheelEvent) => {
    if (viewMode !== 'infinite') return;
    e.preventDefault();

    const zoomFactor = e.deltaY > 0 ? 0.9 : 1.1;
    const newZoom = Math.max(0.1, Math.min(3, zoom * zoomFactor));

    // Zoom to center - just change zoom, don't adjust pan
    // This keeps the origin (0,0,0) at its current screen position
    setZoom(newZoom);
  }, [viewMode, zoom]);

  // Reset pan/zoom
  const resetView = useCallback(() => {
    setPan({ x: 0, y: 0 });
    setZoom(1);
  }, []);

  // Recenter (pan to origin, keep zoom)
  const recenter = useCallback(() => {
    setPan({ x: 0, y: 0 });
  }, []);

  // Calculate visible hex coordinates for infinite viewport
  const visibleHexCoords = useMemo((): HexCoordinate[] => {
    if (viewMode !== 'infinite') return [];

    const effectiveSize = hexSize * zoom;
    const sqrt3 = Math.sqrt(3);

    // Hex dimensions
    const hexWidth = effectiveSize * 2;
    const hexHeight = effectiveSize * sqrt3;

    // Viewport bounds in world coordinates (grid origin is at center + pan)
    // When pan is (0,0), the grid origin is at viewport center
    // A hex at (0,0) in world coords appears at screen center + pan
    const halfWidth = viewportSize.width / 2;
    const halfHeight = viewportSize.height / 2;

    // World coordinates of viewport edges (what world position is at each screen edge)
    // Screen left edge (x=0) corresponds to world x = -halfWidth - pan.x
    const worldLeft = -halfWidth - pan.x;
    const worldRight = halfWidth - pan.x;
    const worldTop = -halfHeight - pan.y;
    const worldBottom = halfHeight - pan.y;

    // Margin must cover the full viewport to ensure complete filling at all zoom levels
    // We generate hexes covering 3x the viewport (1x on each side of center)
    const marginX = viewportSize.width;
    const marginY = viewportSize.height;

    // Calculate q range from x bounds: x = size * 1.5 * q, so q = x / (1.5 * size)
    const minQ = Math.floor((worldLeft - marginX) / (1.5 * effectiveSize));
    const maxQ = Math.ceil((worldRight + marginX) / (1.5 * effectiveSize));

    // Generate all hexes - calculate r range PER q value to fill rectangular viewport
    // y = size * sqrt(3) * (r + q/2), so r = y / (size * sqrt(3)) - q/2
    // Each q column has a different vertical offset, so we calculate r bounds per column
    const coords: HexCoordinate[] = [];

    for (let q = minQ; q <= maxQ; q++) {
      // Calculate r range for this specific q column
      // r = y / (size * sqrt3) - q/2
      const minR = Math.floor((worldTop - marginY) / (effectiveSize * sqrt3) - q / 2);
      const maxR = Math.ceil((worldBottom + marginY) / (effectiveSize * sqrt3) - q / 2);

      for (let r = minR; r <= maxR; r++) {
        coords.push(cubicCoord(q, r));
      }
    }

    return coords;
  }, [viewMode, hexSize, zoom, pan, viewportSize]);

  // Generate coordinates based on layout mode
  const coordinates = useMemo((): HexCoordinate[] => {
    // For infinite mode, use visible coords
    if (viewMode === 'infinite') {
      return visibleHexCoords;
    }

    switch (layoutMode) {
      case 'spiral':
        return getSpiralCoordinates(count);
      case 'ring':
        return getRingCoordinates(cubicCoord(0, 0), ringRadius);
      case 'cluster7':
        return [...HEX_LAYOUTS.CLUSTER_7];
      case 'cluster19':
        return [...HEX_LAYOUTS.CLUSTER_19];
      case 'cluster30':
        return [...HEX_LAYOUTS.CLUSTER_30];
      case 'line':
        if (lineStart && lineEnd) {
          return getLineCoordinates(lineStart, lineEnd);
        }
        // Default line for demo
        return getLineCoordinates(cubicCoord(-2, 0), cubicCoord(2, -1));
      default:
        return getSpiralCoordinates(7);
    }
  }, [viewMode, visibleHexCoords, layoutMode, count, ringRadius, lineStart, lineEnd]);

  // Handle hex click
  const handleHexClick = useCallback((coord: HexCoordinate) => {
    setLastClicked(coord);

    // In line mode, set start/end points
    if (layoutMode === 'line') {
      if (!lineStart) {
        setLineStart(coord);
        setLineEnd(null);
      } else if (!lineEnd) {
        setLineEnd(coord);
      } else {
        // Reset and start new line
        setLineStart(coord);
        setLineEnd(null);
      }
    }
  }, [layoutMode, lineStart, lineEnd]);

  // Build hex grid items
  const items: HexGridItem[] = useMemo(() => {
    return coordinates.map((coord, index) => {
      const isLineEndpoint = layoutMode === 'line' &&
        ((lineStart && coord.q === lineStart.q && coord.r === lineStart.r) ||
         (lineEnd && coord.q === lineEnd.q && coord.r === lineEnd.r));

      const isLastClicked = lastClicked &&
        coord.q === lastClicked.q && coord.r === lastClicked.r;

      return {
        id: `hex-${coord.q}-${coord.r}`,
        coord,
        content: (
          <div
            className={`
              w-full h-full flex flex-col items-center justify-center
              transition-all duration-200 cursor-pointer
              ${isLineEndpoint ? 'bg-green-600' : isLastClicked ? 'bg-blue-600' : 'bg-gray-800'}
              hover:bg-gray-700
            `}
            style={{
              clipPath: 'polygon(25% 0%, 75% 0%, 100% 50%, 75% 100%, 25% 100%, 0% 50%)',
            }}
          >
            {showCoords && (
              <>
                <span className="text-xs font-mono text-gray-300">
                  {coord.q},{coord.r},{coord.s}
                </span>
                <span className="text-[10px] text-gray-500">
                  #{index}
                </span>
              </>
            )}
          </div>
        ),
        onClick: () => handleHexClick(coord),
      };
    });
  }, [coordinates, showCoords, handleHexClick, layoutMode, lineStart, lineEnd, lastClicked]);

  // Calculate the hex size needed to fit the grid in the resizable box
  const fittedHexSize = useMemo(() => {
    if (coordinates.length === 0 || boxSize.width === 0) return hexSize;

    const sqrt3 = Math.sqrt(3);

    // Find q and r extents
    let minQ = Infinity, maxQ = -Infinity;
    let minYUnit = Infinity, maxYUnit = -Infinity;

    for (const coord of coordinates) {
      minQ = Math.min(minQ, coord.q);
      maxQ = Math.max(maxQ, coord.q);
      // y in unit coordinates (size=1): sqrt(3) * (r + q/2), so yUnit = r + q/2
      const yUnit = coord.r + coord.q / 2;
      minYUnit = Math.min(minYUnit, yUnit);
      maxYUnit = Math.max(maxYUnit, yUnit);
    }

    const qSpan = maxQ - minQ;
    const ySpan = maxYUnit - minYUnit;

    // Available space inside the box (subtract padding)
    const padding = 32;
    const availableWidth = boxSize.width - padding;
    const availableHeight = boxSize.height - padding;

    // For hex size S:
    // Total width = S * (1.5 * qSpan + 2) + gaps
    // Total height = S * sqrt(3) * (ySpan + 1) + gaps
    const numCols = qSpan + 1;
    const numRows = Math.ceil(ySpan) + 1;
    const gapWidthTotal = Math.max(0, numCols - 1) * gap;
    const gapHeightTotal = Math.max(0, numRows - 1) * gap;

    const widthFactor = 1.5 * qSpan + 2;
    const heightFactor = sqrt3 * (ySpan + 1);

    const maxSizeFromWidth = (availableWidth - gapWidthTotal) / widthFactor;
    const maxSizeFromHeight = (availableHeight - gapHeightTotal) / heightFactor;

    // Use the smaller to ensure fit
    const fitted = Math.min(maxSizeFromWidth, maxSizeFromHeight);

    // Clamp to reasonable bounds
    return Math.max(15, Math.min(150, fitted));
  }, [coordinates, boxSize, gap, hexSize]);

  // Effective hex size based on view mode
  const effectiveHexSize = useMemo(() => {
    if (viewMode === 'scaleToFit') return fittedHexSize;
    if (viewMode === 'infinite') return hexSize * zoom;
    return hexSize;
  }, [viewMode, fittedHexSize, hexSize, zoom]);

  // Calculate derived values
  const hexHeight = effectiveHexSize * Math.sqrt(3);
  const aspectRatio = Math.sqrt(3) / 2;

  return (
    <MainLayout className="overflow-y-auto">
      <div className="flex flex-col lg:flex-row gap-6 pt-[60px] px-6 pb-6 min-h-[calc(100vh-80px)]">
        {/* Control Panel */}
        <div className="lg:w-80 flex-shrink-0 bg-gray-800/50 rounded-xl p-6 space-y-6">
          <h2 className="text-xl font-bold text-white">Hex Grid Controls</h2>

          {/* View Mode */}
          <div className="space-y-4">
            <h3 className="text-sm font-semibold text-gray-400 uppercase tracking-wide">
              View Mode
            </h3>

            <div>
              <select
                value={viewMode}
                onChange={(e) => setViewMode(e.target.value as ViewMode)}
                className="w-full bg-gray-700 text-white rounded px-3 py-2 text-sm"
              >
                <option value="normal">Normal</option>
                <option value="scaleToFit">Scale to Fit (resizable)</option>
                <option value="infinite">Infinite (pan/zoom)</option>
              </select>
            </div>

            {viewMode === 'infinite' && (
              <div className="text-xs text-gray-400 bg-gray-900/50 rounded p-2 space-y-1">
                <div className="font-semibold mb-1">Controls</div>
                <div>Drag to pan</div>
                <div>Scroll to zoom</div>
                <div className="flex gap-2 mt-2">
                  <button
                    onClick={recenter}
                    className="flex-1 px-2 py-1 bg-gray-700 hover:bg-gray-600 rounded text-gray-300 transition-colors"
                  >
                    Recenter
                  </button>
                  <button
                    onClick={resetView}
                    className="flex-1 px-2 py-1 bg-gray-700 hover:bg-gray-600 rounded text-gray-300 transition-colors"
                  >
                    Reset All
                  </button>
                </div>
              </div>
            )}
          </div>

          {/* Size Controls */}
          <div className="space-y-4">
            <h3 className="text-sm font-semibold text-gray-400 uppercase tracking-wide">
              Size
            </h3>

            <div className={viewMode === 'scaleToFit' ? 'opacity-50' : ''}>
              <label className="flex justify-between text-sm text-gray-300 mb-1">
                <span>Hex Size (circumradius)</span>
                <span className="font-mono text-amber-400">{hexSize}px</span>
              </label>
              <input
                type="range"
                min="30"
                max="150"
                value={hexSize}
                onChange={(e) => setHexSize(Number(e.target.value))}
                className="w-full accent-amber-500"
                disabled={viewMode === 'scaleToFit'}
              />
              {viewMode === 'infinite' && (
                <div className="flex justify-between text-xs text-gray-500 mt-1">
                  <span>Zoom: {(zoom * 100).toFixed(0)}%</span>
                  <span>Effective: {effectiveHexSize.toFixed(0)}px</span>
                </div>
              )}
              {viewMode !== 'infinite' && (
                <div className="flex justify-between text-xs text-gray-500 mt-1">
                  <span>Width: {(effectiveHexSize * 2).toFixed(0)}px</span>
                  <span>Height: {hexHeight.toFixed(1)}px</span>
                </div>
              )}
            </div>

            <div>
              <label className="flex justify-between text-sm text-gray-300 mb-1">
                <span>Gap</span>
                <span className="font-mono text-amber-400">{gap}px</span>
              </label>
              <input
                type="range"
                min="0"
                max="20"
                value={gap}
                onChange={(e) => setGap(Number(e.target.value))}
                className="w-full accent-amber-500"
              />
            </div>

            <div className="text-xs text-gray-500 bg-gray-900/50 rounded p-2">
              <div>Aspect Ratio: {aspectRatio.toFixed(4)}</div>
              <div>≈ √3/2 ≈ 0.866</div>
            </div>
          </div>

          {/* Layout Controls - hidden in infinite mode */}
          {viewMode !== 'infinite' && (
            <div className="space-y-4">
              <h3 className="text-sm font-semibold text-gray-400 uppercase tracking-wide">
                Layout
              </h3>

              <div>
                <label className="text-sm text-gray-300 mb-1 block">Layout Mode</label>
                <select
                  value={layoutMode}
                  onChange={(e) => setLayoutMode(e.target.value as LayoutMode)}
                  className="w-full bg-gray-700 text-white rounded px-3 py-2 text-sm"
                >
                  <option value="spiral">Spiral (count)</option>
                  <option value="ring">Ring (radius)</option>
                  <option value="cluster7">CLUSTER_7 (7 hexes)</option>
                  <option value="cluster19">CLUSTER_19 (19 hexes)</option>
                  <option value="cluster30">CLUSTER_30 (30 hexes)</option>
                  <option value="line">Line (click 2 hexes)</option>
                </select>
              </div>

              {layoutMode === 'spiral' && (
                <div>
                  <label className="flex justify-between text-sm text-gray-300 mb-1">
                    <span>Count</span>
                    <span className="font-mono text-amber-400">{count}</span>
                  </label>
                  <input
                    type="range"
                    min="1"
                    max="61"
                    value={count}
                    onChange={(e) => setCount(Number(e.target.value))}
                    className="w-full accent-amber-500"
                  />
                  <div className="text-xs text-gray-500 mt-1">
                    1=center, 7=1 ring, 19=2 rings, 37=3 rings, 61=4 rings
                  </div>
                </div>
              )}

              {layoutMode === 'ring' && (
                <div>
                  <label className="flex justify-between text-sm text-gray-300 mb-1">
                    <span>Radius</span>
                    <span className="font-mono text-amber-400">{ringRadius}</span>
                  </label>
                  <input
                    type="range"
                    min="0"
                    max="5"
                    value={ringRadius}
                    onChange={(e) => setRingRadius(Number(e.target.value))}
                    className="w-full accent-amber-500"
                  />
                  <div className="text-xs text-gray-500 mt-1">
                    Hexes in ring: {ringRadius === 0 ? 1 : 6 * ringRadius}
                  </div>
                </div>
              )}

              {layoutMode === 'line' && (
                <div className="text-xs text-gray-400 bg-gray-900/50 rounded p-2">
                  <div className="font-semibold mb-1">Line Mode</div>
                  <div>Click hexes to set start/end points</div>
                  {lineStart && (
                    <div className="mt-1 text-green-400">
                      Start: ({lineStart.q}, {lineStart.r})
                    </div>
                  )}
                  {lineEnd && (
                    <div className="text-green-400">
                      End: ({lineEnd.q}, {lineEnd.r})
                    </div>
                  )}
                  {lineStart && lineEnd && (
                    <div className="text-amber-400">
                      Distance: {distance(lineStart, lineEnd)}
                    </div>
                  )}
                </div>
              )}
            </div>
          )}

          {/* Visual Options */}
          <div className="space-y-4">
            <h3 className="text-sm font-semibold text-gray-400 uppercase tracking-wide">
              Visual Options
            </h3>

            <label className="flex items-center gap-2 text-sm text-gray-300 cursor-pointer">
              <input
                type="checkbox"
                checked={showCoords}
                onChange={(e) => setShowCoords(e.target.checked)}
                className="accent-amber-500"
              />
              Show coordinates
            </label>

            <label className="flex items-center gap-2 text-sm text-gray-300 cursor-pointer">
              <input
                type="checkbox"
                checked={showBorder}
                onChange={(e) => setShowBorder(e.target.checked)}
                className="accent-amber-500"
              />
              Show border
            </label>

            {showBorder && (
              <div>
                <label className="text-sm text-gray-300 mb-1 block">Border Color</label>
                <div className="flex gap-2 items-center">
                  <input
                    type="color"
                    value={borderColor}
                    onChange={(e) => setBorderColor(e.target.value)}
                    className="w-10 h-10 rounded cursor-pointer"
                  />
                  <span className="font-mono text-xs text-gray-400">{borderColor}</span>
                </div>
              </div>
            )}

            {viewMode === 'infinite' && (
              <label className="flex items-center gap-2 text-sm text-gray-300 cursor-pointer">
                <input
                  type="checkbox"
                  checked={showWaterTexture}
                  onChange={(e) => setShowWaterTexture(e.target.checked)}
                  className="accent-amber-500"
                />
                Water texture (perf test)
              </label>
            )}
          </div>

          {/* Info Panel */}
          <div className="space-y-2">
            <h3 className="text-sm font-semibold text-gray-400 uppercase tracking-wide">
              Info
            </h3>
            <div className="text-xs text-gray-500 bg-gray-900/50 rounded p-2 space-y-1">
              <div>Total hexes: {coordinates.length}</div>
              {viewMode === 'scaleToFit' && (
                <div className="text-amber-400">
                  Box: {boxSize.width} x {boxSize.height}px
                </div>
              )}
              {viewMode === 'infinite' && (
                <>
                  <div className="text-purple-400">
                    Pan: ({pan.x.toFixed(0)}, {pan.y.toFixed(0)})
                  </div>
                  <div className="text-purple-400">
                    Zoom: {(zoom * 100).toFixed(0)}%
                  </div>
                </>
              )}
              {lastClicked && (
                <div className="text-blue-400">
                  Last clicked: ({lastClicked.q}, {lastClicked.r}, {lastClicked.s})
                </div>
              )}
            </div>
          </div>

          {/* Direction Reference */}
          <div className="space-y-2">
            <h3 className="text-sm font-semibold text-gray-400 uppercase tracking-wide">
              Direction Vectors
            </h3>
            <div className="text-xs font-mono bg-gray-900/50 rounded p-2 space-y-1">
              {ALL_DIRECTIONS.map((dir) => {
                const v = DIRECTION_VECTORS[dir];
                return (
                  <div key={dir} className="flex justify-between">
                    <span className="text-gray-400">{dir}:</span>
                    <span className="text-gray-300">
                      ({v.q}, {v.r}, {v.s})
                    </span>
                  </div>
                );
              })}
            </div>
          </div>
        </div>

        {/* Grid Display Area */}
        <div className="flex-1 flex items-center justify-center bg-gray-900/30 rounded-xl p-4 overflow-auto">
          {viewMode === 'scaleToFit' ? (
            // Resizable container mode
            <div
              className="relative bg-gray-800/50 rounded-xl border-2 border-gray-600 overflow-hidden"
              style={{ width: boxSize.width, height: boxSize.height }}
            >
              {/* Grid centered in box */}
              <div className="absolute inset-0 flex items-center justify-center">
                <HexGrid
                  hexSize={effectiveHexSize}
                  items={items}
                  gap={gap}
                  borderColor={showBorder ? borderColor : undefined}
                />
              </div>

              {/* Resize handle */}
              <div
                className="absolute bottom-0 right-0 w-6 h-6 cursor-se-resize"
                onMouseDown={handleResizeStart}
              >
                <svg
                  className="w-full h-full text-gray-400 hover:text-amber-400 transition-colors"
                  viewBox="0 0 24 24"
                  fill="currentColor"
                >
                  <path d="M22 22H20V20H22V22ZM22 18H20V16H22V18ZM18 22H16V20H18V22ZM22 14H20V12H22V14ZM18 18H16V16H18V18ZM14 22H12V20H14V22Z" />
                </svg>
              </div>

              {/* Size indicator */}
              <div className="absolute top-2 left-2 text-xs font-mono text-gray-500 bg-gray-900/70 px-2 py-1 rounded">
                {boxSize.width} x {boxSize.height}
              </div>
            </div>
          ) : viewMode === 'infinite' ? (
            // Infinite pan/zoom mode - renders hexes directly without HexGrid auto-layout
            <div
              ref={viewportRef}
              className="relative w-full h-full bg-gray-800/30 rounded-xl border-2 border-purple-600/50 overflow-hidden cursor-grab active:cursor-grabbing"
              onMouseDown={handlePanStart}
              onWheel={handleWheel}
              style={{ minHeight: 400 }}
            >
              {/* Transform container - origin at viewport center, shifted by pan */}
              <div
                className="absolute pointer-events-none"
                style={{
                  left: '50%',
                  top: '50%',
                  transform: `translate(${pan.x}px, ${pan.y}px)`,
                }}
              >
                {/* Render each hex directly at its world position */}
                {coordinates.map((coord) => {
                  const pixel = hexToPixel(coord, effectiveHexSize);
                  const hexWidth = effectiveHexSize * 2;
                  const hexHeight = effectiveHexSize * Math.sqrt(3);
                  const contentScale = 1 - (gap / effectiveHexSize);

                  const isLastClicked = lastClicked &&
                    coord.q === lastClicked.q && coord.r === lastClicked.r;

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
                      {/* Border layer */}
                      {showBorder && (
                        <div
                          className="absolute inset-0 hex-clip-flat"
                          style={{ background: borderColor }}
                        />
                      )}
                      {/* Content layer */}
                      <div
                        className={`
                          absolute hex-clip-flat cursor-pointer
                          transition-colors duration-200
                          ${!showWaterTexture && (isLastClicked ? 'bg-blue-600' : 'bg-gray-800')}
                          ${!showWaterTexture && 'hover:bg-gray-700'}
                        `}
                        style={{
                          width: `${hexWidth * contentScale}px`,
                          height: `${hexHeight * contentScale}px`,
                          left: '50%',
                          top: '50%',
                          transform: 'translate(-50%, -50%)',
                          ...(showWaterTexture && {
                            backgroundImage: 'url(/water.png)',
                            backgroundSize: 'cover',
                            backgroundPosition: 'center',
                          }),
                        }}
                        onClick={() => handleHexClick(coord)}
                      >
                        {showCoords && !showWaterTexture && (
                          <div className="w-full h-full flex flex-col items-center justify-center">
                            <span
                              className="font-mono text-gray-300"
                              style={{ fontSize: `${Math.max(8, effectiveHexSize * 0.15)}px` }}
                            >
                              {coord.q},{coord.r},{coord.s}
                            </span>
                          </div>
                        )}
                      </div>
                    </div>
                  );
                })}
              </div>

              {/* Origin marker (world 0,0) - moves with pan */}
              <div
                className="absolute pointer-events-none"
                style={{
                  left: `calc(50% + ${pan.x}px)`,
                  top: `calc(50% + ${pan.y}px)`,
                  transform: 'translate(-50%, -50%)',
                }}
              >
                <div className="w-4 h-[2px] bg-amber-500/60 absolute -left-2 top-0" />
                <div className="w-[2px] h-4 bg-amber-500/60 absolute left-0 -top-2" />
              </div>

              {/* Viewport info */}
              <div className="absolute top-2 left-2 text-xs font-mono text-gray-500 bg-gray-900/70 px-2 py-1 rounded">
                {viewportSize.width.toFixed(0)} x {viewportSize.height.toFixed(0)} | {coordinates.length} hexes
              </div>

              {/* Zoom indicator */}
              <div className="absolute bottom-2 right-2 text-xs font-mono text-gray-500 bg-gray-900/70 px-2 py-1 rounded">
                {(zoom * 100).toFixed(0)}%
              </div>
            </div>
          ) : (
            // Normal mode - grid fills available space
            <HexGrid
              hexSize={effectiveHexSize}
              items={items}
              gap={gap}
              borderColor={showBorder ? borderColor : undefined}
            />
          )}
        </div>
      </div>
    </MainLayout>
  );
}
