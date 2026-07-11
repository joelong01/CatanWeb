'use client';

import React, { useMemo, useCallback, useRef, useState, useEffect } from 'react';
import { HexGrid, type HexGridItem } from '@/components/hex-grid';
import {
  cubicCoord,
  getNeighbor,
  ALL_DIRECTIONS,
  SIDE_TO_DIRECTION,
  DIRECTION_TO_SIDE,
  type HexCoordinate,
  type HexSide,
} from '@/components/hex-grid/hex-geometry';
import { WaterHex } from '@/components/hex-grid/content/WaterHex';
import { GameTile } from '@/components/game/tiles/GameTile';
import { useAssetPath, useFontRendering, useHarborFontConfig } from '@/lib/theme';
import { getHarborImage } from '@/lib/constants/board-assets';
import type { TileModel } from '@/types/generated/models/tile-model';
import type { ResourceType } from '@/types/generated/models/resource-type';
import type { HarborType } from '@/types/generated/models/harbor-type';
import type { TemplateTile, TemplateHarbor } from '@/types/generated/models';
import { faCoins } from '@fortawesome/free-solid-svg-icons';

/**
 * Map HexSide to the two vertex positions (in viewBox coordinates) that the harbor connects to.
 * ViewBox is 100x86.6 (flat-top hex proportions). Center is at (50, 43.3).
 */
const SIDE_TO_VERTICES: Record<string, [[number, number], [number, number]]> = {
  Top: [
    [25, 86.6],
    [75, 86.6],
  ],
  TopRight: [
    [0, 43.3],
    [25, 86.6],
  ],
  BottomRight: [
    [25, 0],
    [0, 43.3],
  ],
  Bottom: [
    [75, 0],
    [25, 0],
  ],
  BottomLeft: [
    [100, 43.3],
    [75, 0],
  ],
  TopLeft: [
    [75, 86.6],
    [100, 43.3],
  ],
};

/** FA icon SVG path data for harbors. */
const FA_ICON_PATHS: Record<string, string> = {
  coins: faCoins.icon[4] as string,
};

/** Zoom configuration */
const ZOOM_CONFIG = {
  minHexSize: 20,
  maxHexSize: 150,
  zoomStep: 5,
  defaultSize: 50,
};

/** Dock/pier colors for image-mode harbors. */
const DOCK_COLORS = {
  fill: '#8B7355',
  stroke: '#5D4E37',
  highlight: '#A08060',
};

interface EditorBoardProps {
  tiles: TemplateTile[];
  harbors: TemplateHarbor[];
  selectedTileIndex?: number | null;
  onTileClick?: (index: number) => void;
  onTileRightClick?: (index: number, event: React.MouseEvent) => void;
  /** Right-click an existing harbor to edit/remove it. */
  onHarborRightClick?: (index: number, event: React.MouseEvent) => void;
  /** Right-click an adjacent water hex. Includes the adjacent tile+side pairs for harbor placement. */
  onWaterRightClick?: (
    coord: HexCoordinate,
    adjacentTiles: { q: number; r: number; side: string }[],
    event: React.MouseEvent
  ) => void;
  flippingCoords?: Set<string>;
}

/** Convert a TemplateTile to TileModel for GameTile rendering. */
function toTileModel(t: TemplateTile): TileModel {
  return {
    tileKey: { q: t.q, r: t.r, s: -t.q - t.r },
    number: t.number,
    resourceTileType: t.resource as ResourceType,
    highlighted: false,
    temporarilyGold: false,
  };
}

export function coordKey(coord: HexCoordinate): string {
  return `${coord.q},${coord.r},${coord.s}`;
}

/**
 * Harbor hex content - renders harbor icon on a wooden dock.
 * Replicates HarborHexContent from GameBoard for template editing.
 */
function EditorHarborHex({
  side,
  harborType,
  uniqueId,
}: {
  side: string;
  harborType: string;
  uniqueId: string;
}) {
  const imageUrl = getHarborImage(harborType as HarborType);
  const isFontMode = useFontRendering();
  const themeFontConfig = useHarborFontConfig(harborType);
  const fontConfig = isFontMode ? themeFontConfig : undefined;
  const dockVertices = SIDE_TO_VERTICES[side];

  const cx = 50;
  const cy = 43.3;
  const circleRadius = 26;

  if (!imageUrl || harborType === 'None') return null;

  const hexPoints = '25,0 75,0 100,43.3 75,86.6 25,86.6 0,43.3';

  if (fontConfig) {
    const hexOpacity = fontConfig.hexOpacity ?? 0.6;
    return (
      <div className="absolute inset-0" data-drag-through>
        <svg
          className="absolute inset-0 w-full h-full"
          viewBox="0 0 100 86.6"
          preserveAspectRatio="none"
        >
          <defs>
            <clipPath id={`editor-harbor-clip-${uniqueId}`}>
              <circle cx={cx} cy={cy} r={circleRadius - 1} />
            </clipPath>
          </defs>
          <polygon points={hexPoints} fill={fontConfig.bgColor || 'black'} />
          {fontConfig.hexGlyph && (
            <g opacity={hexOpacity}>
              <svg
                x="0"
                y="0"
                width="100"
                height="86.6"
                viewBox="0 0 100 100"
                preserveAspectRatio="none"
              >
                <text
                  x="50"
                  y="50"
                  textAnchor="middle"
                  dominantBaseline="central"
                  style={{ fontFamily: 'var(--font-catan)' }}
                  fontSize="100"
                  fill={fontConfig.color}
                >
                  {fontConfig.hexGlyph}
                </text>
              </svg>
            </g>
          )}
          {!fontConfig.hexGlyph && fontConfig.bgColor && (
            <polygon points={hexPoints} fill={fontConfig.bgColor} opacity={hexOpacity} />
          )}
          {fontConfig.faIcon && FA_ICON_PATHS[fontConfig.faIcon] && (
            <path
              d={FA_ICON_PATHS[fontConfig.faIcon]}
              fill={fontConfig.color}
              opacity={0.3}
              transform="translate(10, 3.3) scale(0.156)"
            />
          )}
          <line
            x1={dockVertices[0][0]}
            y1={dockVertices[0][1]}
            x2={dockVertices[1][0]}
            y2={dockVertices[1][1]}
            stroke={fontConfig.color}
            strokeWidth="10"
            strokeLinecap="butt"
          />
          <circle
            cx={cx}
            cy={cy}
            r={circleRadius}
            fill="#f5f0e1"
            stroke={fontConfig.color}
            strokeWidth={2}
          />
          <text
            x={cx}
            y={cy}
            textAnchor="middle"
            dominantBaseline="central"
            style={{ fontFamily: 'var(--font-catan)' }}
            fontSize={42}
            fill={fontConfig.color}
            clipPath={`url(#editor-harbor-clip-${uniqueId})`}
          >
            {fontConfig.harborGlyph}
          </text>
        </svg>
      </div>
    );
  }

  // Image mode
  const backgroundStyle: React.CSSProperties = {
    clipPath: 'polygon(25% 0%, 75% 0%, 100% 50%, 75% 100%, 25% 100%, 0% 50%)',
    backdropFilter: 'blur(4px)',
    WebkitBackdropFilter: 'blur(4px)',
    backgroundColor: 'rgba(30, 41, 59, 0.3)',
  };

  return (
    <div className="absolute inset-0" style={backgroundStyle} data-drag-through>
      <svg
        className="absolute inset-0 w-full h-full"
        viewBox="0 0 100 86.6"
        preserveAspectRatio="none"
      >
        <defs>
          <linearGradient id={`editor-dock-wood-${uniqueId}`} x1="0%" y1="0%" x2="100%" y2="100%">
            <stop offset="0%" stopColor={DOCK_COLORS.highlight} />
            <stop offset="50%" stopColor={DOCK_COLORS.fill} />
            <stop offset="100%" stopColor={DOCK_COLORS.stroke} />
          </linearGradient>
          <clipPath id={`editor-harbor-clip-${uniqueId}`}>
            <circle cx={cx} cy={cy} r={circleRadius - 1} />
          </clipPath>
        </defs>
        <line
          x1={dockVertices[0][0]}
          y1={dockVertices[0][1]}
          x2={dockVertices[1][0]}
          y2={dockVertices[1][1]}
          stroke={DOCK_COLORS.stroke}
          strokeWidth="14"
          strokeLinecap="round"
        />
        <circle
          cx={cx}
          cy={cy}
          r={circleRadius}
          fill="#f5f0e1"
          stroke={DOCK_COLORS.stroke}
          strokeWidth="2.5"
        />
        <image
          href={imageUrl}
          x={cx - circleRadius + 1}
          y={cy - circleRadius + 1}
          width={(circleRadius - 1) * 2}
          height={(circleRadius - 1) * 2}
          clipPath={`url(#editor-harbor-clip-${uniqueId})`}
          preserveAspectRatio="xMidYMid slice"
        />
      </svg>
    </div>
  );
}

/**
 * Interactive board for the template editor.
 *
 * Reuses the same rendering approach as GameBoard:
 * - Infinite water hex background
 * - Pan (pointer drag) and zoom (mouse wheel)
 * - Harbors rendered as hex items at water positions
 * - Tiles rendered via GameTile
 */
export function EditorBoard({
  tiles,
  harbors,
  selectedTileIndex,
  onTileClick,
  onTileRightClick,
  onHarborRightClick,
  onWaterRightClick,
  flippingCoords,
}: EditorBoardProps): React.ReactElement | null {
  const initialHexSize = ZOOM_CONFIG.defaultSize;
  const [hexSize, setHexSize] = useState(initialHexSize);
  const [panOffset, setPanOffset] = useState({ x: 0, y: 0 });
  const [isPanning, setIsPanning] = useState(false);
  const containerRef = useRef<HTMLDivElement>(null);
  const [containerSize, setContainerSize] = useState<{ width: number; height: number } | null>(
    null
  );

  const dragStateRef = useRef<{
    panning: boolean;
    startClient: { x: number; y: number };
    startPan: { x: number; y: number };
  } | null>(null);

  const seaTilePath = useAssetPath('TileSea');

  // Measure container
  useEffect(() => {
    const measure = (): void => {
      if (containerRef.current) {
        setContainerSize({
          width: containerRef.current.clientWidth,
          height: containerRef.current.clientHeight,
        });
      }
    };
    measure();
    const observer = new ResizeObserver(measure);
    if (containerRef.current) observer.observe(containerRef.current);
    return () => observer.disconnect();
  }, []);

  // Mouse wheel zoom
  const handleWheel = useCallback((e: React.WheelEvent) => {
    e.preventDefault();
    const delta = e.deltaY > 0 ? -ZOOM_CONFIG.zoomStep : ZOOM_CONFIG.zoomStep;
    setHexSize((prev) =>
      Math.max(ZOOM_CONFIG.minHexSize, Math.min(ZOOM_CONFIG.maxHexSize, prev + delta))
    );
  }, []);

  // Pan via pointer events
  const handlePointerDown = useCallback(
    (e: React.PointerEvent) => {
      if (e.button !== 0) return;
      dragStateRef.current = {
        panning: false,
        startClient: { x: e.clientX, y: e.clientY },
        startPan: { ...panOffset },
      };
      (e.target as HTMLElement).setPointerCapture(e.pointerId);
      e.preventDefault();
    },
    [panOffset]
  );

  const handlePointerMove = useCallback((e: React.PointerEvent) => {
    const state = dragStateRef.current;
    if (!state) return;
    const dx = e.clientX - state.startClient.x;
    const dy = e.clientY - state.startClient.y;
    const dist = Math.sqrt(dx * dx + dy * dy);
    if (!state.panning && dist >= 5) {
      state.panning = true;
      setIsPanning(true);
    }
    if (state.panning) {
      setPanOffset({ x: state.startPan.x + dx, y: state.startPan.y + dy });
    }
  }, []);

  const handlePointerUp = useCallback((_e: React.PointerEvent) => {
    const state = dragStateRef.current;
    if (!state) return;
    setIsPanning(false);
    dragStateRef.current = null;
  }, []);

  const handleContextMenu = useCallback((e: React.MouseEvent) => {
    e.preventDefault();
  }, []);

  // Build tile coordinate set for water generation
  const tileCoordSet = useMemo(() => {
    const set = new Set<string>();
    tiles.forEach((t) => {
      const coord = cubicCoord(t.q, t.r);
      set.add(coordKey(coord));
    });
    return set;
  }, [tiles]);

  // Land tiles only. A Seafarers harbor sits ON a coastal Sea hex, so its water
  // coord is a real (Sea) tile; we must only drop a harbor when its coord is a
  // LAND tile (a genuine overlap), not when it lands on a Sea tile.
  const landTileCoordSet = useMemo(() => {
    const set = new Set<string>();
    tiles.forEach((t) => {
      if (t.resource === 'Sea') return;
      set.add(coordKey(cubicCoord(t.q, t.r)));
    });
    return set;
  }, [tiles]);

  // Build set of water coords adjacent to at least one land tile
  const adjacentWaterSet = useMemo(() => {
    const set = new Set<string>();
    tiles.forEach((t) => {
      const coord = cubicCoord(t.q, t.r);
      for (const dir of ALL_DIRECTIONS) {
        const neighbor = getNeighbor(coord, dir);
        const key = coordKey(neighbor);
        if (!tileCoordSet.has(key)) {
          set.add(key);
        }
      }
    });
    return set;
  }, [tiles, tileCoordSet]);

  // Build tile items
  const tileItems: HexGridItem[] = useMemo(
    () =>
      tiles.map((tile, index) => {
        const coord = cubicCoord(tile.q, tile.r);
        const tileModel = toTileModel(tile);
        const isSelected = selectedTileIndex === index;

        const isFlipping = flippingCoords?.has(coordKey(coord)) ?? false;

        return {
          id: `tile-${coordKey(coord)}`,
          coord,
          content: (
            <div
              className="w-full h-full relative"
              style={{
                outline: isSelected ? '3px solid #3B82F6' : 'none',
                outlineOffset: '-3px',
                borderRadius: '2px',
                animation: isFlipping ? 'hexFlip 0.3s ease-in-out' : undefined,
              }}
            >
              <GameTile
                tile={tileModel}
                hexSize={hexSize}
                onClick={() => onTileClick?.(index)}
                onRightClick={(e) => {
                  e.preventDefault();
                  onTileRightClick?.(index, e);
                }}
              />
              {/* Coordinate label */}
              <div
                className="absolute left-1/2 -translate-x-1/2 pointer-events-none"
                style={{ bottom: '8%' }}
              >
                <span
                  className="text-white font-mono leading-none px-1 rounded"
                  style={{
                    fontSize: Math.max(8, hexSize * 0.18),
                    backgroundColor: 'rgba(0,0,0,0.6)',
                  }}
                >
                  ({tile.q},{tile.r})
                </span>
              </div>
            </div>
          ),
        };
      }),
    [tiles, hexSize, selectedTileIndex, onTileClick, onTileRightClick, flippingCoords]
  );

  // Build harbor items at water positions adjacent to tiles (same as GameBoard)
  const harborItems: HexGridItem[] = useMemo(() => {
    return harbors.map((harbor, index) => {
      const tileCoord = cubicCoord(harbor.q, harbor.r);
      const direction = SIDE_TO_DIRECTION[harbor.side as HexSide];
      const waterCoord = direction ? getNeighbor(tileCoord, direction) : tileCoord;

      const harborId = `${harbor.q}-${harbor.r}-${harbor.side}`;
      return {
        id: `harbor-${harborId}`,
        coord: waterCoord,
        content: (
          <div
            className="cursor-pointer"
            onContextMenu={(e) => {
              e.preventDefault();
              e.stopPropagation();
              onHarborRightClick?.(index, e);
            }}
          >
            <EditorHarborHex side={harbor.side} harborType={harbor.type} uniqueId={harborId} />
          </div>
        ),
      };
    });
  }, [harbors, onHarborRightClick]);

  // Generate water hexes (same algorithm as GameBoard)
  const boardBounds = useMemo(() => {
    let minQ = Infinity,
      maxQ = -Infinity,
      minR = Infinity,
      maxR = -Infinity;
    tiles.forEach((t) => {
      minQ = Math.min(minQ, t.q);
      maxQ = Math.max(maxQ, t.q);
      minR = Math.min(minR, t.r);
      maxR = Math.max(maxR, t.r);
    });
    return { minQ, maxQ, minR, maxR };
  }, [tiles]);

  const waterItems: HexGridItem[] = useMemo(() => {
    const items: HexGridItem[] = [];
    const { minQ, maxQ, minR, maxR } = boardBounds;
    if (!isFinite(minQ)) return items;

    const padding = 8;
    const qStart = minQ - padding;
    const qEnd = maxQ + padding;
    const centerQ = (minQ + maxQ) / 2;

    for (let q = qStart; q <= qEnd; q++) {
      const qOffsetFromCenter = q - centerQ;
      const rAdjust = Math.round(qOffsetFromCenter / 2);
      const rStart = minR - padding - rAdjust;
      const rEnd = maxR + padding - rAdjust;

      for (let r = rStart; r <= rEnd; r++) {
        const coord = cubicCoord(q, r);
        const key = coordKey(coord);
        if (tileCoordSet.has(key)) continue;

        const isAdjacent = adjacentWaterSet.has(key);
        const isFlipping = flippingCoords?.has(key) ?? false;
        const clickable = isAdjacent && onWaterRightClick;

        // Compute which land tiles are adjacent and what side faces this water hex
        let adjacentTiles: { q: number; r: number; side: string }[] | undefined;
        if (clickable) {
          adjacentTiles = [];
          for (const dir of ALL_DIRECTIONS) {
            const neighbor = getNeighbor(coord, dir);
            if (tileCoordSet.has(coordKey(neighbor))) {
              // Opposite direction = side of the tile facing this water hex
              const oppositeIdx = (ALL_DIRECTIONS.indexOf(dir) + 3) % 6;
              const side = DIRECTION_TO_SIDE[ALL_DIRECTIONS[oppositeIdx]];
              adjacentTiles.push({ q: neighbor.q, r: neighbor.r, side });
            }
          }
        }

        items.push({
          id: `water-${key}`,
          coord,
          excludeFromBounds: true,
          content: (
            <div
              className={clickable ? 'cursor-pointer group' : ''}
              style={isFlipping ? { animation: 'hexFlip 0.3s ease-in-out' } : undefined}
              onContextMenu={
                clickable
                  ? (e) => {
                      e.preventDefault();
                      e.stopPropagation();
                      onWaterRightClick(coord, adjacentTiles!, e);
                    }
                  : undefined
              }
            >
              <WaterHex imageUrl={seaTilePath || undefined} opacity={1} />
              {clickable && (
                <div
                  className="absolute inset-0 opacity-0 group-hover:opacity-100 transition-opacity pointer-events-none"
                  style={{
                    clipPath: 'polygon(25% 0%, 75% 0%, 100% 50%, 75% 100%, 25% 100%, 0% 50%)',
                    backgroundColor: 'rgba(59, 130, 246, 0.2)',
                    border: '2px solid rgba(59, 130, 246, 0.4)',
                  }}
                />
              )}
            </div>
          ),
        });
      }
    }
    return items;
  }, [boardBounds, tileCoordSet, adjacentWaterSet, seaTilePath, onWaterRightClick, flippingCoords]);

  // Combine: water first, then tiles, then harbors
  const allItems = useMemo(() => {
    // Drop a harbor only when it overlaps a LAND tile; harbors on coastal Sea
    // tiles (Seafarers) render on top of the sea hex.
    const harborFiltered = harborItems.filter(
      (item) => !landTileCoordSet.has(coordKey(item.coord))
    );
    return [...waterItems, ...tileItems, ...harborFiltered];
  }, [waterItems, tileItems, harborItems, landTileCoordSet]);

  if (tiles.length === 0) {
    return (
      <div className="flex items-center justify-center h-full text-gray-500">
        No tiles defined. Add tiles in the form panel.
      </div>
    );
  }

  return (
    <>
      <style>{`
      @keyframes hexFlip {
        0%   { transform: rotateY(0deg); opacity: 1; }
        50%  { transform: rotateY(90deg); opacity: 0; }
        51%  { transform: rotateY(90deg); opacity: 0; }
        100% { transform: rotateY(0deg); opacity: 1; }
      }
    `}</style>
      <div
        ref={containerRef}
        className="relative w-full h-full overflow-hidden select-none"
        onWheel={handleWheel}
        onPointerDown={handlePointerDown}
        onPointerMove={handlePointerMove}
        onPointerUp={handlePointerUp}
        onContextMenu={handleContextMenu}
        style={{
          cursor: isPanning ? 'grabbing' : 'grab',
          touchAction: 'none',
        }}
      >
        {containerSize ? (
          <div
            className="absolute inset-0 flex items-center justify-center"
            style={{ transform: `translate(${panOffset.x}px, ${panOffset.y}px)` }}
          >
            <HexGrid
              hexSize={hexSize}
              items={allItems}
              gap={1}
              borderColor="transparent"
              fitToParent={false}
            />
          </div>
        ) : null}
      </div>
    </>
  );
}
