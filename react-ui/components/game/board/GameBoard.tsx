'use client';

import React, { useMemo, useRef, useState, useEffect, useCallback } from 'react';
import { HexGrid, HexGridItem, type HexGridLayoutInfo } from '@/components/hex-grid';
import {
  cubicCoord,
  getNeighbor,
  getVertexPosition,
  getEdgeMidpoint,
  hexToPixel,
  Direction,
  type HexCoordinate,
  type PixelPosition,
  type HexPosition as GeometryHexPosition,
  type HexSide as GeometryHexSide,
} from '@/components/hex-grid/hex-geometry';
import { NUMBER_PIPS, getHarborImage } from '@/lib/constants/board-assets';
import { WaterHex } from '@/components/hex-grid/content/WaterHex';
import { GameTile } from '@/components/game/tiles/GameTile';
import { Building, Road, type BuildingVisualState, type RoadState } from '@/components/game/tiles';
import { useLayoutStore } from '@/lib/stores/layoutStore';
import { hexToRgba } from '@/lib/utils/playerColors';
import { useBoardData, useBoardPlayers, useSelectedPlayerId, useRolledNumber } from '@/lib/hooks';
import { useIsAllocationPhase } from '@/lib/stores/gameStoreHooks';

// Import generated types
import type { TileModel } from '@/types/generated/models/tile-model';
import type { HarborModel } from '@/types/generated/models/harbor-model';
import type { BuildingModel } from '@/types/generated/models/building-model';
import type { RoadModel } from '@/types/generated/models/road-model';
import type { HexSide } from '@/types/generated/models/hex-side';
import type { HexPosition } from '@/types/generated/models/hex-position';
import type { BuildingKey } from '@/types/generated/models/building-key';
import type { RoadKey } from '@/types/generated/models/road-key';
import type { Entitlement } from '@/types/generated/models/entitlement';

// BoardGameData and BoardPlayer types are now in lib/hooks/useBoardData.ts
// Re-export for backwards compatibility with any external consumers
export type { BoardGameData, BoardPlayer } from '@/lib/hooks';

/** Zoom configuration */
const ZOOM_CONFIG = {
  minHexSize: 20,
  maxHexSize: 150,
  zoomStep: 5,        // Pixels to change hexSize per wheel tick
  defaultSize: 50,
};

// PlayerColors and BoardPlayer are now imported from lib/hooks/useBoardData.ts
import type { PlayerColors } from '@/types/player-profile';

/** All 6 vertex positions on a hex (excluding 'None') */
const ALL_POSITIONS: GeometryHexPosition[] = ['Right', 'BottomRight', 'BottomLeft', 'Left', 'TopLeft', 'TopRight'];

/** All 6 edge sides on a hex (excluding 'None') */
const ALL_SIDES: GeometryHexSide[] = ['Top', 'TopRight', 'BottomRight', 'Bottom', 'BottomLeft', 'TopLeft'];

/**
 * Props for GameBoard component.
 *
 * GameBoard uses internal Zustand hooks for game data (tiles, buildings, roads, etc.)
 * and only accepts callbacks and configuration as props.
 */
export interface GameBoardProps {
  /** Initial hex size (circumradius) - default 50. Controlled via mouse wheel after mount. */
  hexSize?: number;
  /** Gap between hexes - default 2 */
  gap?: number;
  /** Callback when a tile is clicked */
  onTileClick?: (tile: TileModel) => void;
  /** Callback when a tile is right-clicked (e.g., robber placement) */
  onTileRightClick?: (tile: TileModel, event: React.MouseEvent) => void;
  /** Set of highlighted tile keys (for dice roll highlighting) */
  highlightedTiles?: Set<string>;
  /** Callback when a buildable building spot is clicked */
  onBuildingClick?: (buildingKey: BuildingKey) => void;
  /** Callback when a buildable road is clicked */
  onRoadClick?: (roadKey: RoadKey) => void;
}

/**
 * Create a unique key string from hex coordinates
 */
function coordKeyString(coord: HexCoordinate): string {
  return `${coord.q},${coord.r},${coord.s}`;
}

/**
 * Map HexSide (from game data) to Direction (for neighbor calculation)
 */
const SIDE_TO_DIRECTION: Record<HexSide, Direction> = {
  Top: Direction.North,
  TopRight: Direction.NorthEast,
  BottomRight: Direction.SouthEast,
  Bottom: Direction.South,
  BottomLeft: Direction.SouthWest,
  TopLeft: Direction.NorthWest,
  None: Direction.North, // Fallback
};

/**
 * Map HexSide to the two vertex positions (in viewBox coordinates) that the harbor connects to.
 * These are the vertices of the harbor hex's edge that faces the connected tile.
 * ViewBox is 100x86.6 (flat-top hex proportions).
 * Center is at (50, 43.3).
 */
const SIDE_TO_VERTICES: Record<HexSide, [[number, number], [number, number]]> = {
  Top: [[25, 86.6], [75, 86.6]],       // Harbor's bottom edge (faces south toward tile)
  TopRight: [[0, 43.3], [25, 86.6]],   // Harbor's bottom-left edge
  BottomRight: [[25, 0], [0, 43.3]],   // Harbor's top-left edge
  Bottom: [[75, 0], [25, 0]],          // Harbor's top edge (faces north toward tile)
  BottomLeft: [[100, 43.3], [75, 0]],  // Harbor's top-right edge
  TopLeft: [[75, 86.6], [100, 43.3]],  // Harbor's bottom-right edge
  None: [[50, 50], [50, 50]],          // Fallback
};

/**
 * Map HexSide to the OPPOSITE edge vertices (for water triangle on far side).
 * These face away from the connected tile, toward open water.
 */
const SIDE_TO_OPPOSITE_VERTICES: Record<HexSide, [[number, number], [number, number]]> = {
  Top: [[75, 0], [25, 0]],             // Top edge (opposite of bottom)
  TopRight: [[100, 43.3], [75, 0]],    // Top-right edge (opposite of bottom-left)
  BottomRight: [[75, 86.6], [100, 43.3]], // Bottom-right edge (opposite of top-left)
  Bottom: [[25, 86.6], [75, 86.6]],    // Bottom edge (opposite of top)
  BottomLeft: [[0, 43.3], [25, 86.6]], // Bottom-left edge (opposite of top-right)
  TopLeft: [[25, 0], [0, 43.3]],       // Top-left edge (opposite of bottom-right)
  None: [[50, 50], [50, 50]],          // Fallback
};

/**
 * Dock/pier colors - neutral wood tones that don't imply ownership
 */
const DOCK_COLORS = {
  fill: '#8B7355',      // Weathered wood brown
  stroke: '#5D4E37',    // Darker wood border
  highlight: '#A08060', // Lighter wood accent
};

/**
 * Water colors for harbor water triangle (opposite side of dock)
 */
const WATER_COLORS = {
  fill: '#1e4078',      // Deep ocean blue
  stroke: '#162e5a',    // Darker water border
  highlight: '#2a5090', // Lighter water accent
};

/**
 * Harbor hex content - displays harbor icon on a triangular wooden dock.
 *
 * When a player owns the harbor (building at adjacent vertex), the hex
 * background is filled with the player's gradient color. Otherwise,
 * transparent background lets water show through.
 */
interface HarborHexContentProps {
  harbor: HarborModel;
  /** Owner colors when harbor is owned by a player */
  ownerColors?: PlayerColors | null;
}

function HarborHexContent({ harbor, ownerColors }: HarborHexContentProps) {
  const { harborType, side } = harbor.harborKey;
  const imageUrl = getHarborImage(harborType);
  const dockVertices = SIDE_TO_VERTICES[side];
  const waterVertices = SIDE_TO_OPPOSITE_VERTICES[side];

  // Circle parameters (in viewBox units)
  const cx = 50;
  const cy = 43.3;
  const circleRadius = 26;

  // For 'None' harbors, render nothing (water shows through)
  if (!imageUrl || harborType === 'None') {
    return null;
  }

  // Full hex polygon points (flat-top hex, viewBox 100x86.6)
  // Vertices clockwise from top-left: top-left, top-right, right, bottom-right, bottom-left, left
  const hexPoints = '25,0 75,0 100,43.3 75,86.6 25,86.6 0,43.3';

  // Dock triangle points: center + two edge vertices facing the tile
  const dockTrianglePoints = `${cx},${cy} ${dockVertices[0][0]},${dockVertices[0][1]} ${dockVertices[1][0]},${dockVertices[1][1]}`;

  // Water triangle points: center + two edge vertices facing open water
  const waterTrianglePoints = `${cx},${cy} ${waterVertices[0][0]},${waterVertices[0][1]} ${waterVertices[1][0]},${waterVertices[1][1]}`;

  // Generate unique gradient ID for this harbor's owner
  const ownerGradientId = ownerColors ? `harbor-owner-${side}` : null;

  // Determine background style
  const backgroundStyle: React.CSSProperties = {
    clipPath: 'polygon(25% 0%, 75% 0%, 100% 50%, 75% 100%, 25% 100%, 0% 50%)',
    backdropFilter: 'blur(4px)',
    WebkitBackdropFilter: 'blur(4px)', // Safari support
  };

  if (ownerColors) {
    // Owned: Fully opaque player gradient
    const start = hexToRgba(ownerColors.primary, 1.0);
    const end = hexToRgba(ownerColors.secondary, 1.0);
    backgroundStyle.background = `linear-gradient(135deg, ${start}, ${end})`;
  } else {
    // Unowned: Frosted dark background (similar to panels)
    // Decreased opacity to 0.3 just to be sure blur is visible if any
    backgroundStyle.backgroundColor = 'rgba(30, 41, 59, 0.3)'; // slate-800 at 0.3
  }

  return (
    <div className="absolute inset-0" style={backgroundStyle} data-drag-through>
      {/* SVG for triangular dock, water triangle, and harbor circle */}
      <svg
        className="absolute inset-0 w-full h-full"
        viewBox="0 0 100 86.6"
        preserveAspectRatio="none"
      >
        <defs>
          {/* Wood grain gradient for dock */}
          <linearGradient id={`dock-wood-${side}`} x1="0%" y1="0%" x2="100%" y2="100%">
            <stop offset="0%" stopColor={DOCK_COLORS.highlight} />
            <stop offset="50%" stopColor={DOCK_COLORS.fill} />
            <stop offset="100%" stopColor={DOCK_COLORS.stroke} />
          </linearGradient>
          {/* Water gradient for opposite side */}
          <linearGradient id={`dock-water-${side}`} x1="0%" y1="0%" x2="100%" y2="100%">
            <stop offset="0%" stopColor={WATER_COLORS.highlight} />
            <stop offset="50%" stopColor={WATER_COLORS.fill} />
            <stop offset="100%" stopColor={WATER_COLORS.stroke} />
          </linearGradient>
          {/* Owner gradient when harbor is owned */}
          {ownerColors && ownerGradientId && (
            <linearGradient id={ownerGradientId} x1="0%" y1="0%" x2="100%" y2="100%">
              <stop offset="0%" stopColor={ownerColors.primary} />
              <stop offset="100%" stopColor={ownerColors.secondary} />
            </linearGradient>
          )}
          <clipPath id={`harbor-clip-${side}`}>
            <circle cx={cx} cy={cy} r={circleRadius - 1} />
          </clipPath>
        </defs>

        {/* Connection lines to tile vertices (subtle 2px lines) */}
        <line
          x1={cx} y1={cy}
          x2={dockVertices[0][0]} y2={dockVertices[0][1]}
          stroke="rgba(255, 255, 255, 0.4)"
          strokeWidth="2"
        />
        <line
          x1={cx} y1={cy}
          x2={dockVertices[1][0]} y2={dockVertices[1][1]}
          stroke="rgba(255, 255, 255, 0.4)"
          strokeWidth="2"
        />

        {/* Dock circle with wood fill (toward tile connection) */}
        <circle
          cx={cx}
          cy={cy}
          r={circleRadius}
          fill="#f5f0e1"
          stroke={DOCK_COLORS.stroke}
          strokeWidth="2.5"
        />

        {/* Harbor image inside circle */}
        <image
          href={imageUrl}
          x={cx - circleRadius + 1}
          y={cy - circleRadius + 1}
          width={(circleRadius - 1) * 2}
          height={(circleRadius - 1) * 2}
          clipPath={`url(#harbor-clip-${side})`}
          preserveAspectRatio="xMidYMid slice"
        />
      </svg>
    </div>
  );
}

/**
 * GameBoard - Renders the full game board with tiles and harbors.
 *
 * Uses HexGrid for consistent positioning of both tiles and harbors.
 * Harbors are placed at water hex coordinates adjacent to their connected tile.
 *
 * Features:
 * - Mouse wheel zoom: Scroll to change hex size (tiles get bigger/smaller)
 * - Water plane: Water tiles fill the entire visible viewport, not just around the board
 * - Scrollable viewport: Board can be larger than container, use scroll to pan
 */
export function GameBoard({
  hexSize: initialHexSize = ZOOM_CONFIG.defaultSize,
  gap = 1,
  onTileClick,
  onTileRightClick,
  highlightedTiles,
  onBuildingClick,
  onRoadClick,
}: GameBoardProps): React.ReactElement {
  // Use internal hooks for game data (server-driven UI pattern)
  const boardData = useBoardData();
  const players = useBoardPlayers();
  const selectedPlayerId = useSelectedPlayerId();
  const rolledNumber = useRolledNumber();
  const isAllocationPhase = useIsAllocationPhase();

  // Destructure board data from hook
  const { tiles, harbors, buildings, roads, currentPlayerEntitlements, robber } = boardData;

  // Derive showSettlementIndexes from server state (not from props)
  // Show indexes when NOT in allocation phase and player has Settlement entitlement
  const showSettlementIndexes = useMemo(() => {
    if (isAllocationPhase) return false;
    return currentPlayerEntitlements.includes('Settlement' as Entitlement);
  }, [isAllocationPhase, currentPlayerEntitlements]);

  // Robber animation state - tracks the position to render (for CSS transition)
  const [animatedRobberCoords, setAnimatedRobberCoords] = useState<{ q: number; r: number } | null>(null);
  const [isRobberAnimating, setIsRobberAnimating] = useState(false);
  // Track last animated "from" and "to" to prevent re-animating same movement on re-render
  const lastAnimatedFromRef = useRef<{ q: number; r: number } | null>(null);
  const lastAnimatedToRef = useRef<{ q: number; r: number } | null>(null);

  // Robber animation effect - when previousCoordinates differs from coordinates, animate
  useEffect(() => {
    if (!robber) return;

    const currCoords = robber.coordinates;
    const prevCoords = robber.previousCoordinates;

    // Check if we have a valid movement to animate
    const hasValidCurrent = currCoords && (currCoords.q !== 0 || currCoords.r !== 0);
    const hasValidPrevious = prevCoords && (prevCoords.q !== 0 || prevCoords.r !== 0);
    const coordsDiffer = hasValidCurrent && hasValidPrevious &&
      (currCoords.q !== prevCoords.q || currCoords.r !== prevCoords.r);

    if (coordsDiffer) {
      // Guard: Skip if we already animated this exact movement
      const alreadyAnimated =
        lastAnimatedFromRef.current?.q === prevCoords.q &&
        lastAnimatedFromRef.current?.r === prevCoords.r &&
        lastAnimatedToRef.current?.q === currCoords.q &&
        lastAnimatedToRef.current?.r === currCoords.r;

      if (!alreadyAnimated) {
        // Record this animation
        lastAnimatedFromRef.current = { q: prevCoords.q, r: prevCoords.r };
        lastAnimatedToRef.current = { q: currCoords.q, r: currCoords.r };

        // Start animation: render at previous position first
        setAnimatedRobberCoords({ q: prevCoords.q, r: prevCoords.r });
        setIsRobberAnimating(true);

        // After a frame, move to current position (CSS transition will animate)
        requestAnimationFrame(() => {
          setTimeout(() => {
            setAnimatedRobberCoords({ q: currCoords.q, r: currCoords.r });
            // Mark animation as complete after transition duration (1.2s like Blazor)
            setTimeout(() => setIsRobberAnimating(false), 1200);
          }, 20);
        });
      }
    } else if (hasValidCurrent && !isRobberAnimating) {
      // No animation needed - just set current position
      setAnimatedRobberCoords({ q: currCoords.q, r: currCoords.r });
      // Clear animation tracking when robber is placed without movement
      if (!hasValidPrevious) {
        lastAnimatedFromRef.current = null;
        lastAnimatedToRef.current = null;
      }
    }
  }, [robber, isRobberAnimating]);

  const containerRef = useRef<HTMLDivElement>(null);
  const [containerSize, setContainerSize] = useState<{ width: number; height: number } | null>(null);

  // Viewport state from layoutStore (persisted, reset via resetLayout)
  const viewport = useLayoutStore((state) => state.viewport);
  const setViewport = useLayoutStore((state) => state.setViewport);

  // Star filter from layoutStore (filters building spots by minimum star value)
  const starFilter = useLayoutStore((state) => state.starFilter);

  // Use viewport zoom or initial prop (viewport.zoom is a multiplier, convert to hexSize)
  const hexSize = viewport.zoom > 0 ? Math.round(initialHexSize * viewport.zoom) : initialHexSize;
  const panOffset = viewport.pan;

  // Local pan interaction state
  const [isPanning, setIsPanning] = useState(false);
  const [panStart, setPanStart] = useState<PixelPosition>({ x: 0, y: 0 });

  // Measure container size
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
    if (containerRef.current) {
      observer.observe(containerRef.current);
    }

    return () => observer.disconnect();
  }, []);

  // Handle mouse wheel zoom
  const handleWheel = useCallback((e: React.WheelEvent) => {
    e.preventDefault();
    const delta = e.deltaY > 0 ? -ZOOM_CONFIG.zoomStep : ZOOM_CONFIG.zoomStep;
    const newHexSize = Math.max(ZOOM_CONFIG.minHexSize, Math.min(ZOOM_CONFIG.maxHexSize, hexSize + delta));
    // Convert hexSize to zoom multiplier
    const newZoom = newHexSize / initialHexSize;
    setViewport({ zoom: newZoom });
  }, [hexSize, initialHexSize, setViewport]);

  // Handle CTRL+drag panning (matches FloatingPanel behavior)
  const handleMouseDown = useCallback((e: React.MouseEvent) => {
    if (e.ctrlKey || e.metaKey) {
      e.preventDefault();
      setIsPanning(true);
      setPanStart({ x: e.clientX - panOffset.x, y: e.clientY - panOffset.y });
    }
  }, [panOffset]);

  const handleMouseMove = useCallback((e: React.MouseEvent) => {
    if (isPanning) {
      setViewport({
        pan: {
          x: e.clientX - panStart.x,
          y: e.clientY - panStart.y,
        },
      });
    }
  }, [isPanning, panStart, setViewport]);

  const handleMouseUp = useCallback(() => {
    setIsPanning(false);
  }, []);

  const handleMouseLeave = useCallback(() => {
    setIsPanning(false);
  }, []);

  // Build set of tile coordinates for quick lookup
  const tileCoordSet = useMemo(() => {
    const set = new Set<string>();
    tiles.forEach((tile) => {
      const coord = cubicCoord(tile.tileKey.q, tile.tileKey.r);
      set.add(coordKeyString(coord));
    });
    return set;
  }, [tiles]);

  // Build HexGrid items from tiles
  const tileItems: HexGridItem[] = useMemo(() => {
    return tiles.map((tile) => {
      const coord = cubicCoord(tile.tileKey.q, tile.tileKey.r);
      const key = coordKeyString(coord);
      const isHighlighted = highlightedTiles?.has(key) || tile.highlighted;
      // Tile is dimmed when a roll is active AND this tile's number doesn't match
      // Sea/Desert tiles (number 0) are also dimmed when a number is rolled
      const isDimmed = rolledNumber !== null && rolledNumber !== undefined && tile.number !== rolledNumber;

      return {
        id: `tile-${key}`,
        coord,
        content: (
          <GameTile
            tile={tile}
            hexSize={hexSize}
            isHighlighted={isHighlighted}
            isDimmed={isDimmed}
            onClick={onTileClick ? () => onTileClick(tile) : undefined}
            onRightClick={onTileRightClick ? (e) => onTileRightClick(tile, e) : undefined}
          />
        ),
      };
    });
  }, [tiles, hexSize, highlightedTiles, rolledNumber, onTileClick, onTileRightClick]);

  // Get the current player's colors for harbor backgrounds
  const currentPlayerColors = useMemo((): PlayerColors | undefined => {
    if (!selectedPlayerId || !players || players.length === 0) return undefined;
    const player = players.find(p => p.id === selectedPlayerId);
    return player?.colors;
  }, [players, selectedPlayerId]);

  // Build HexGrid items from harbors (at water hex positions)
  const harborItems: HexGridItem[] = useMemo(() => {
    return harbors.map((harbor) => {
      const { hexCoordinates, side } = harbor.harborKey;
      const tileCoord = cubicCoord(hexCoordinates.q, hexCoordinates.r);

      // Find the water hex adjacent to the tile in the harbor's direction
      const direction = SIDE_TO_DIRECTION[side];
      const waterCoord = getNeighbor(tileCoord, direction);
      const key = coordKeyString(waterCoord);

      // Get owner colors if harbor is owned
      const ownerColors = harbor.owner?.id
        ? players.find(p => p.id === harbor.owner.id)?.colors
        : null;

      return {
        id: `harbor-${key}`,
        coord: waterCoord,
        content: <HarborHexContent harbor={harbor} ownerColors={ownerColors} />,
      };
    });
  }, [harbors, players]);

  // Build set of harbor coordinates for quick lookup
  const harborCoordSet = useMemo(() => {
    const set = new Set<string>();
    harborItems.forEach((item) => {
      set.add(coordKeyString(item.coord));
    });
    return set;
  }, [harborItems]);

  // Calculate board bounds (for water generation) - NO pan offset here
  const boardBounds = useMemo(() => {
    let minQ = Infinity, maxQ = -Infinity, minR = Infinity, maxR = -Infinity;
    tiles.forEach((tile) => {
      minQ = Math.min(minQ, tile.tileKey.q);
      maxQ = Math.max(maxQ, tile.tileKey.q);
      minR = Math.min(minR, tile.tileKey.r);
      maxR = Math.max(maxR, tile.tileKey.r);
    });
    return { minQ, maxQ, minR, maxR };
  }, [tiles]);

  // Generate water hexes as a rectangular grid CENTERED around the board
  // For flat-top hexes: x = 1.5*q (columns), y = sqrt(3)*(r + q/2)
  // To get rectangular grid centered on board: adjust r based on distance from center q
  const waterItems: HexGridItem[] = useMemo(() => {
    const items: HexGridItem[] = [];
    const { minQ, maxQ, minR, maxR } = boardBounds;

    const padding = 8; // Extra rings of water around the board (acts as viewport)
    const qStart = minQ - padding;
    const qEnd = maxQ + padding;
    // Use board center as reference for symmetric r adjustment
    const centerQ = (minQ + maxQ) / 2;

    for (let q = qStart; q <= qEnd; q++) {
      // Adjust r range based on distance from CENTER to maintain visual symmetry
      // As q moves away from center, shift r to keep visual rectangle centered
      const qOffsetFromCenter = q - centerQ;
      const rAdjust = Math.round(qOffsetFromCenter / 2);

      const rStart = minR - padding - rAdjust;
      const rEnd = maxR + padding - rAdjust;

      for (let r = rStart; r <= rEnd; r++) {
        const coord = cubicCoord(q, r);
        const key = coordKeyString(coord);

        // Skip if this coord is a tile (harbors sit on top of water)
        if (tileCoordSet.has(key)) continue;

        items.push({
          id: `water-${key}`,
          coord,
          excludeFromBounds: true, // Don't affect HexGrid's bounding box
          content: (
            <WaterHex
              imageUrl="/themes/base/tiles/back.jpg"
              opacity={1}
            />
          ),
        });
      }
    }

    return items;
  }, [boardBounds, tileCoordSet, harborCoordSet]);

  // Generate all unique building positions (vertices shared between tiles)
  // Each vertex is identified by the tile coord + position
  const buildingPositions = useMemo(() => {
    const positions: { key: string; coord: HexCoordinate; position: GeometryHexPosition }[] = [];
    const seen = new Set<string>();

    tiles.forEach((tile) => {
      const coord = cubicCoord(tile.tileKey.q, tile.tileKey.r);

      ALL_POSITIONS.forEach((position) => {
        // Create a unique key for this vertex
        const key = `${coord.q},${coord.r},${coord.s}-${position}`;

        // Avoid duplicates (vertices are shared between adjacent hexes)
        if (!seen.has(key)) {
          seen.add(key);
          positions.push({ key, coord, position });
        }
      });
    });

    return positions;
  }, [tiles]);

  // Generate all unique road positions (edges shared between tiles)
  // Each edge is identified by the tile coord + side
  const roadPositions = useMemo(() => {
    const positions: { key: string; coord: HexCoordinate; side: GeometryHexSide }[] = [];
    const seen = new Set<string>();

    tiles.forEach((tile) => {
      const coord = cubicCoord(tile.tileKey.q, tile.tileKey.r);

      ALL_SIDES.forEach((side) => {
        // Create a unique key for this edge
        const key = `${coord.q},${coord.r},${coord.s}-${side}`;

        // Avoid duplicates (edges are shared between adjacent hexes)
        if (!seen.has(key)) {
          seen.add(key);
          positions.push({ key, coord, side });
        }
      });
    });

    return positions;
  }, [tiles]);

  // Build lookup maps for buildings and roads from model data
  const buildingMap = useMemo(() => {
    const map = new Map<string, BuildingModel>();
    buildings.forEach((b) => {
      const coord = b.buildingKey.hexCoordinates;
      const key = `${coord.q},${coord.r},${-coord.q - coord.r}-${b.buildingKey.position}`;
      map.set(key, b);
    });
    return map;
  }, [buildings]);

  const roadMap = useMemo(() => {
    const map = new Map<string, RoadModel>();
    roads.forEach((r) => {
      const coord = r.roadKey.tileKey;
      const key = `${coord.q},${coord.r},${-coord.q - coord.r}-${r.roadKey.hexSide}`;
      map.set(key, r);
    });
    return map;
  }, [roads]);

  // Combine all items: water first (background), then tiles, then harbors
  const allItems = useMemo(() => {
    // Deduplicate harbors: if a harbor is at the same coord as a tile, tile wins
    const harborItemsFiltered = harborItems.filter(
      (item) => !tileCoordSet.has(coordKeyString(item.coord))
    );
    // Water renders first (z-order), then tiles, then harbors on top
    return [...waterItems, ...tileItems, ...harborItemsFiltered];
  }, [waterItems, tileItems, harborItems, tileCoordSet]);

  // Build a map of tile coordinates to their pip values for fast lookup
  const tilePipsMap = useMemo(() => {
    const map = new Map<string, number>();
    tiles.forEach((tile) => {
      const coord = cubicCoord(tile.tileKey.q, tile.tileKey.r);
      const key = coordKeyString(coord);
      // Pips represent probability (2-12 dice numbers, 7=desert has 0 pips)
      const pips = NUMBER_PIPS[tile.number] ?? 0;
      map.set(key, pips);
    });
    return map;
  }, [tiles]);

  // Calculate star value for a building position based on adjacent tiles' pips
  // Stars = sum of pips from all tiles touching this vertex (up to 3 tiles)
  const calculateStars = useCallback((coord: HexCoordinate, position: GeometryHexPosition): number => {
    // A vertex touches up to 3 tiles depending on its position
    // For each HexPosition, we need to check the current tile and its neighbors
    const adjacentCoords: HexCoordinate[] = [coord];

    // Map vertex position to which neighbors also touch this vertex
    // Based on hex geometry: each vertex is shared by 3 hexes
    const neighborDirections: Record<GeometryHexPosition, Direction[]> = {
      Right: [Direction.NorthEast, Direction.SouthEast],
      BottomRight: [Direction.SouthEast, Direction.South],
      BottomLeft: [Direction.South, Direction.SouthWest],
      Left: [Direction.SouthWest, Direction.NorthWest],
      TopLeft: [Direction.NorthWest, Direction.North],
      TopRight: [Direction.North, Direction.NorthEast],
    };

    const directions = neighborDirections[position];
    directions.forEach((dir) => {
      adjacentCoords.push(getNeighbor(coord, dir));
    });

    // Sum pips from all adjacent tiles
    let totalPips = 0;
    adjacentCoords.forEach((c) => {
      const key = coordKeyString(c);
      totalPips += tilePipsMap.get(key) ?? 0;
    });

    return totalPips;
  }, [tilePipsMap]);

  // Render buildings and roads overlay (DOM divs)
  // Implements Blazor BuildingOverlay.razor and RoadOverlay.razor logic
  const renderOverlay = useCallback((layoutInfo: HexGridLayoutInfo) => {
    const { origin, hexSize: hSize } = layoutInfo;

    // Building sizes: owned=larger, buildable=smaller
    // Base ratios from Blazor (24 vs 18 SVG units at HexSize=100), scaled up ~25% per user feedback
    const ownedBuildingSize = hSize * 0.60;     // 24/50 * 1.25 = 0.60
    const buildableBuildingSize = hSize * 0.45; // 18/50 * 1.25 = 0.45

    // Road container size (SVG viewBox is hexSize * 1.2, centered at origin)
    const roadContainerSize = hSize * 1.2;

    // Get current player colors
    const currentPlayer = selectedPlayerId ? players.find(p => p.id === selectedPlayerId) : players[0];

    // Check entitlements for current player
    const hasSettlementEntitlement = currentPlayerEntitlements.includes('Settlement' as Entitlement);
    const hasCityEntitlement = currentPlayerEntitlements.includes('City' as Entitlement);
    const hasRoadEntitlement = currentPlayerEntitlements.includes('Road' as Entitlement);

    // Build city upgrade index map (A, B, C...) for current player's settlements
    // Matches Blazor BuildingOverlay.razor: city upgrades get letters, settlements get numbers
    const cityUpgradeIndexMap = new Map<string, string>();
    if (hasCityEntitlement && currentPlayer) {
      let cityIndex = 0; // 0 = 'A', 1 = 'B', etc.
      buildingPositions.forEach(({ key }) => {
        const buildingModel = buildingMap.get(key);
        if (buildingModel &&
            buildingModel.buildingState === 'Settlement' &&
            buildingModel.ownerId === currentPlayer.id) {
          cityUpgradeIndexMap.set(key, String.fromCharCode(65 + cityIndex)); // 65 = 'A'
          cityIndex++;
        }
      });
    }

    // Build settlement placement index map (1, 2, 3...) for buildable spots
    // Only shown during regular gameplay (NOT during allocation phase)
    // Matches Blazor BuildingOverlay.razor line 230: buildIndex = settlementIndex.ToString()
    const settlementIndexMap = new Map<string, string>();
    if (showSettlementIndexes && hasSettlementEntitlement) {
      let settlementIndex = 1; // 1-based numbering
      buildingPositions.forEach(({ key }) => {
        const buildingModel = buildingMap.get(key);
        if (buildingModel?.buildingState === 'PossibleSettlement' && buildingModel?.ownerId === null) {
          settlementIndexMap.set(key, settlementIndex.toString());
          settlementIndex++;
        }
      });
    }

    return (
      <>
        {/* Roads layer (render first, below buildings) */}
        {/* Per RoadOverlay.razor: Only render if ownerId != null OR roadState === 'Buildable' */}
        {roadPositions.map(({ key, coord, side }) => {
          const roadModel = roadMap.get(key);

          // CRITICAL: Only render roads that are owned OR server-marked as Buildable
          // Do NOT default to Buildable - the server controls buildability
          if (!roadModel) {
            return null; // No model = no road to render
          }

          const roadState = roadModel.roadState as RoadState;
          const ownerId = roadModel.ownerId;

          // Visibility filter: owned OR buildable (matches Blazor GetVisibleRoads)
          if (ownerId === null && roadState !== 'Buildable') {
            return null; // Unowned and not buildable = don't render
          }

          const pixelPos = getEdgeMidpoint(coord, side, hSize, origin);

          // Build the road key for click handler
          const roadKey: RoadKey = {
            tileKey: { q: coord.q, r: coord.r, s: coord.s },
            hexSide: side as HexSide,
          };

          // Include player ID in key to force re-render on ownership/turn changes
          // Buildable roads: use current player ID (changes on turn change)
          // Owned roads: use owner ID (ensures correct colors after purchase)
          const roadKeyString = roadState === 'Buildable'
            ? `road-${key}-buildable-${currentPlayer?.id ?? 'none'}`
            : `road-${key}-owned-${ownerId ?? 'none'}`;

          return (
            <div
              key={roadKeyString}
              className="absolute"
              style={{
                left: pixelPos.x - roadContainerSize / 2,
                top: pixelPos.y - roadContainerSize / 2,
              }}
            >
              <Road
                roadState={roadState}
                side={side}
                ownerId={ownerId}
                currentPlayerId={selectedPlayerId}
                hexSize={hSize}
                buildIndex={roadModel.buildIndex}
                onClick={roadState === 'Buildable' && onRoadClick ? () => onRoadClick(roadKey) : undefined}
              />
            </div>
          );
        })}

        {/* Buildings layer - TWO loops per Blazor BuildingOverlay.razor */}

        {/* Loop 1: Owned buildings (Settlement or City with ownerId) */}
        {buildingPositions.map(({ key, coord, position }) => {
          const buildingModel = buildingMap.get(key);
          if (!buildingModel) return null;

          const buildingState = buildingModel.buildingState;
          const ownerId = buildingModel.ownerId;

          // Only render owned Settlement or City in this loop
          if (ownerId === null) return null;
          if (buildingState !== 'Settlement' && buildingState !== 'City') return null;

          const pixelPos = getVertexPosition(coord, position, hSize, origin);
          const owner = players.find((p) => p.id === ownerId);

          // Build the building key for click handler (city upgrades)
          // Cast needed because generated BuildingKey has spurious 'default' property
          const buildingKey = {
            hexCoordinates: { q: coord.q, r: coord.r, s: coord.s },
            position: position as HexPosition,
          } as BuildingKey;

          // City upgrade: clickable if current player owns this settlement AND has city entitlement
          const isCityUpgradeable = buildingState === 'Settlement' &&
            ownerId === currentPlayer?.id &&
            hasCityEntitlement;

          // Get city upgrade letter (A, B, C...) if this is an upgradeable settlement
          const cityUpgradeIndex = cityUpgradeIndexMap.get(key);

          return (
            <div
              key={`owned-building-${key}`}
              className="absolute"
              style={{
                left: pixelPos.x - ownedBuildingSize / 2,
                top: pixelPos.y - ownedBuildingSize / 2,
              }}
            >
              <Building
                buildingState={buildingState}
                visualState={isCityUpgradeable ? 'Highlighted' : 'Normal'}
                ownerId={ownerId}
                currentPlayerId={selectedPlayerId}
                size={ownedBuildingSize}
                buildIndex={cityUpgradeIndex}
                onClick={isCityUpgradeable && onBuildingClick ? () => onBuildingClick(buildingKey) : undefined}
              />
            </div>
          );
        })}

        {/* Loop 2: Buildable spots - only show when player has Settlement entitlement */}
        {buildingPositions.map(({ key, coord, position }) => {
          const buildingModel = buildingMap.get(key);
          const buildingState = buildingModel?.buildingState ?? 'PossibleSettlement';
          const ownerId = buildingModel?.ownerId;

          // Skip owned buildings (handled in Loop 1)
          if (ownerId !== null) return null;

          // Only show buildable spots when player has Settlement entitlement
          if (!hasSettlementEntitlement) return null;

          // Only render PossibleSettlement spots (not NotBuildable)
          if (buildingState !== 'PossibleSettlement') return null;

          // Calculate stars for this position
          const stars = calculateStars(coord, position);

          // Get settlement build index (1, 2, 3...) if showing indexes
          const settlementBuildIndex = settlementIndexMap.get(key);

          // Determine if spot should be hidden (invisible but hoverable)
          // When build indexes are shown, all spots visible (no hiding per Blazor line 232)
          // Otherwise: No filter = all hidden (hover to reveal), filter active = hide spots below threshold
          const isHidden = settlementBuildIndex ? false : (starFilter === null || stars < starFilter);

          const pixelPos = getVertexPosition(coord, position, hSize, origin);

          // Build the building key for click handler
          // Cast needed because generated BuildingKey has spurious 'default' property
          const buildingKey = {
            hexCoordinates: { q: coord.q, r: coord.r, s: coord.s },
            position: position as HexPosition,
          } as BuildingKey;

          // Visual state: Highlighted when showing build indexes, Stars when visible, Hidden otherwise
          const visualState: BuildingVisualState = settlementBuildIndex
            ? 'Highlighted'
            : isHidden ? 'Hidden' : 'Stars';

          return (
            <div
              key={`buildable-${key}`}
              className="absolute"
              style={{
                left: pixelPos.x - buildableBuildingSize / 2,
                top: pixelPos.y - buildableBuildingSize / 2,
              }}
            >
              <Building
                buildingState="PossibleSettlement"
                visualState={visualState}
                stars={stars}
                currentPlayerId={selectedPlayerId}
                size={buildableBuildingSize}
                buildIndex={settlementBuildIndex}
                onClick={onBuildingClick ? () => onBuildingClick(buildingKey) : undefined}
              />
            </div>
          );
        })}

        {/* Robber layer - renders on top of everything using CatanFont glyphs with player colors */}
        {/* Uses inline SVG to properly render gradient fills like Blazor does */}
        {/* Animation: uses animatedRobberCoords with CSS transition (matches Blazor RobberLayer.razor) */}
        {robber && animatedRobberCoords && (
          (() => {
            const robberCoord = cubicCoord(animatedRobberCoords.q, animatedRobberCoords.r);
            const robberPos = hexToPixel(robberCoord, hSize, origin);
            // Hex height = sqrt(3) * hexSize for flat-top hexes
            const hHeight = Math.sqrt(3) * hSize;
            const robberFontSize = hHeight * 0.5; // 50% of hex height (matches Blazor)

            // Get colors for player who moved the robber
            const movedByPlayer = players.find(p => p.id === robber.movedBy);
            const movedByColors = movedByPlayer?.colors;
            const primaryColor = movedByColors?.primary || '#666';
            const secondaryColor = movedByColors?.secondary || '#888';
            const foregroundColor = movedByColors?.foreground || '#fff';

            // SVG viewBox size - large enough for the glyphs
            const svgSize = robberFontSize * 1.5;

            return (
              <div
                key="robber"
                className="absolute pointer-events-none"
                style={{
                  left: robberPos.x,
                  top: robberPos.y,
                  transform: 'translate(-50%, -50%)',
                  filter: 'drop-shadow(2px 2px 4px rgba(0,0,0,0.5))',
                  // CSS transition for smooth animation (1.2s matches Blazor)
                  transition: 'left 1.2s ease-in-out, top 1.2s ease-in-out',
                }}
              >
                {/* Inline SVG for proper gradient rendering (matches Blazor RobberLayer.razor) */}
                <svg
                  width={svgSize}
                  height={svgSize}
                  viewBox={`0 0 ${svgSize} ${svgSize}`}
                  style={{ opacity: 0.75 }}
                >
                  {/* Gradient definition for shield */}
                  <defs>
                    <linearGradient id="robber-gradient" x1="0%" y1="0%" x2="100%" y2="100%">
                      <stop offset="0%" stopColor={primaryColor} />
                      <stop offset="100%" stopColor={secondaryColor} />
                    </linearGradient>
                  </defs>
                  {/* Background: SolidShield glyph (E925) with player gradient */}
                  <text
                    x={svgSize / 2}
                    y={svgSize / 2}
                    textAnchor="middle"
                    dominantBaseline="central"
                    fontFamily="Catan"
                    fontSize={robberFontSize}
                    fill="url(#robber-gradient)"
                  >
                    {'\uE925'}
                  </text>
                  {/* Foreground: Pirate glyph (E90C) with player foreground color */}
                  <text
                    x={svgSize / 2}
                    y={svgSize / 2}
                    textAnchor="middle"
                    dominantBaseline="central"
                    fontFamily="Catan"
                    fontSize={robberFontSize * 0.8}
                    fill={foregroundColor}
                  >
                    {'\uE90C'}
                  </text>
                </svg>
              </div>
            );
          })()
        )}
      </>
    );
  }, [buildingPositions, roadPositions, buildingMap, roadMap, players, selectedPlayerId, calculateStars, starFilter, currentPlayerEntitlements, onBuildingClick, onRoadClick, robber, animatedRobberCoords, showSettlementIndexes]);

  // Debug logging
  console.log('[GameBoard] render, tiles:', tiles.length, 'containerSize:', containerSize);

  // Loading state - show message but keep container mounted for ref measurement
  // With hooks, data comes from store - check if tiles are loaded
  const isLoading = tiles.length === 0;

  return (
    <div
      ref={containerRef}
      className="relative w-full h-full overflow-hidden select-none"
      onWheel={handleWheel}
      onMouseDown={handleMouseDown}
      onMouseMove={handleMouseMove}
      onMouseUp={handleMouseUp}
      onMouseLeave={handleMouseLeave}
      style={{ cursor: isPanning ? 'grabbing' : 'default' }}
    >
      {isLoading ? (
        <div className="absolute inset-0 flex items-center justify-center bg-gray-900">
          <span className="text-gray-400">Loading game board...</span>
        </div>
      ) : containerSize ? (
        <div
          className="absolute inset-0 flex items-center justify-center"
          style={{
            transform: `translate(${panOffset.x}px, ${panOffset.y}px)`,
          }}
        >
          <HexGrid
            hexSize={hexSize}
            items={allItems}
            gap={gap}
            borderColor="transparent"
            fitToParent={false}
            overlay={players.length > 0 ? (layoutInfo) => renderOverlay(layoutInfo) : undefined}
          />
        </div>
      ) : null}

    </div>
  );
}

export default GameBoard;
