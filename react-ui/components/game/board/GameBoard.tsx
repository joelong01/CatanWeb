'use client';

import React, { useMemo, useRef, useState, useEffect, useCallback } from 'react';
import { HexGrid, HexGridItem, type HexGridLayoutInfo } from '@/components/hex-grid';
import {
  cubicCoord,
  getNeighbor,
  getVertexPosition,
  getEdgeMidpoint,
  hexToPixel,
  pixelToHex,
  Direction,
  SIDE_TO_DIRECTION,
  type HexCoordinate,
  type PixelPosition,
  type HexPosition as GeometryHexPosition,
  type HexSide as GeometryHexSide,
} from '@/components/hex-grid/hex-geometry';
import { NUMBER_PIPS, getHarborImage } from '@/lib/constants/board-assets';
import { useAssetPath, useFontRendering, useHarborFontConfig } from '@/lib/theme';
import { WaterHex } from '@/components/hex-grid/content/WaterHex';
import { GameTile } from '@/components/game/tiles/GameTile';
import { Building, Road, type BuildingVisualState, type RoadState } from '@/components/game/tiles';
import { useLayoutStore } from '@/lib/stores/layoutStore';
import { hexToRgba } from '@/lib/utils/playerColors';
import { faCoins } from '@fortawesome/free-solid-svg-icons';
import { useBoardData, useBoardPlayers, useRolledNumber } from '@/lib/hooks';
import { useIsAllocationPhase, useGameState } from '@/lib/stores/gameStoreHooks';

// Import generated types
import type { TileModel } from '@/types/generated/models/tile-model';
import type { HarborModel } from '@/types/generated/models/harbor-model';
import type { BuildingModel } from '@/types/generated/models/building-model';
import type { RoadModel } from '@/types/generated/models/road-model';
import type { HexSide } from '@/types/generated/models/hex-side';
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
  zoomStep: 5, // Pixels to change hexSize per wheel tick
  defaultSize: 50,
};

// PlayerColors and BoardPlayer are now imported from lib/hooks/useBoardData.ts
import type { PlayerColors } from '@/types/player-profile';

/** All 6 vertex positions on a hex (excluding 'None') */
const ALL_POSITIONS: GeometryHexPosition[] = [
  'Right',
  'BottomRight',
  'BottomLeft',
  'Left',
  'TopLeft',
  'TopRight',
];

/** All 6 edge sides on a hex (excluding 'None') */
const ALL_SIDES: GeometryHexSide[] = [
  'Top',
  'TopRight',
  'BottomRight',
  'Bottom',
  'BottomLeft',
  'TopLeft',
];

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
  /**
   * Callback when a tile is clicked.
   *
   * `clientX`/`clientY` are client (viewport) coordinates for the click position,
   * suitable for positioning click-based UI such as context menus or popovers.
   */
  onTileClick?: (tile: TileModel, clientX: number, clientY: number) => void;
  /** Callback when a tile is right-clicked (e.g., robber placement) */
  onTileRightClick?: (tile: TileModel, event: React.MouseEvent) => void;
  /** Set of highlighted tile keys (for dice roll highlighting) */
  highlightedTiles?: Set<string>;
  /** Callback when a buildable building spot is clicked */
  onBuildingClick?: (buildingKey: BuildingKey) => void;
  /** Callback when a buildable road is clicked */
  onRoadClick?: (roadKey: RoadKey) => void;
  /** Ref updated with the screen position of hex (0,0,0) whenever layout changes */
  hexCenterRef?: React.RefObject<{ x: number; y: number } | null>;
}

/**
 * Create a unique key string from hex coordinates
 */
function coordKeyString(coord: HexCoordinate): string {
  return `${coord.q},${coord.r},${coord.s}`;
}

/**
 * Map HexSide to the two vertex positions (in viewBox coordinates) that the harbor connects to.
 * These are the vertices of the harbor hex's edge that faces the connected tile.
 * ViewBox is 100x86.6 (flat-top hex proportions).
 * Center is at (50, 43.3).
 */
const SIDE_TO_VERTICES: Record<HexSide, [[number, number], [number, number]]> = {
  Top: [
    [25, 86.6],
    [75, 86.6],
  ], // Harbor's bottom edge (faces south toward tile)
  TopRight: [
    [0, 43.3],
    [25, 86.6],
  ], // Harbor's bottom-left edge
  BottomRight: [
    [25, 0],
    [0, 43.3],
  ], // Harbor's top-left edge
  Bottom: [
    [75, 0],
    [25, 0],
  ], // Harbor's top edge (faces north toward tile)
  BottomLeft: [
    [100, 43.3],
    [75, 0],
  ], // Harbor's top-right edge
  TopLeft: [
    [75, 86.6],
    [100, 43.3],
  ], // Harbor's bottom-right edge
  None: [
    [50, 50],
    [50, 50],
  ], // Fallback
};

/**
 * Map vertex position to which 2 neighbor directions also touch this vertex.
 * Each vertex is shared by 3 hexes: the tile itself + 2 neighbors.
 * Used by calculateStars and getAdjacentResources.
 */
const VERTEX_NEIGHBOR_DIRECTIONS: Record<GeometryHexPosition, Direction[]> = {
  Right: [Direction.NorthEast, Direction.SouthEast],
  BottomRight: [Direction.SouthEast, Direction.South],
  BottomLeft: [Direction.South, Direction.SouthWest],
  Left: [Direction.SouthWest, Direction.NorthWest],
  TopLeft: [Direction.NorthWest, Direction.North],
  TopRight: [Direction.North, Direction.NorthEast],
};

/**
 * Dock/pier colors - neutral wood tones that don't imply ownership
 */
const DOCK_COLORS = {
  fill: '#8B7355', // Weathered wood brown
  stroke: '#5D4E37', // Darker wood border
  highlight: '#A08060', // Lighter wood accent
};

/** Map faIcon string names from theme.json to FA icon SVG path data. */
const FA_ICON_PATHS: Record<string, string> = {
  coins: faCoins.icon[4] as string,
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
  const isFontMode = useFontRendering();
  const themeFontConfig = useHarborFontConfig(harborType);
  const fontConfig = isFontMode ? themeFontConfig : undefined;
  const dockVertices = SIDE_TO_VERTICES[side];

  // Circle parameters (in viewBox units)
  const cx = 50;
  const cy = 43.3;
  const circleRadius = 26;

  // For 'None' harbors, render nothing (water shows through)
  if (!imageUrl || harborType === 'None') {
    return null;
  }

  // Full hex polygon points (flat-top hex, viewBox 100x86.6)
  const hexPoints = '25,0 75,0 100,43.3 75,86.6 25,86.6 0,43.3';

  // Font mode: tile-like rendering with harbor ship in center
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
            <clipPath id={`harbor-clip-${side}`}>
              <circle cx={cx} cy={cy} r={circleRadius - 1} />
            </clipPath>
          </defs>

          {/* Solid base behind the hex glyph (theme bgColor or black fallback) */}
          <polygon points={hexPoints} fill={fontConfig.bgColor || 'black'} />

          {/* Hex glyph background at reduced opacity (looks like a faded tile) */}
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

          {/* Fallback solid background (ThreeForOne: no hex glyph) */}
          {!fontConfig.hexGlyph && fontConfig.bgColor && (
            <polygon points={hexPoints} fill={fontConfig.bgColor} opacity={hexOpacity} />
          )}

          {/* FontAwesome icon fill for ThreeForOne */}
          {fontConfig.faIcon && FA_ICON_PATHS[fontConfig.faIcon] && (
            <path
              d={FA_ICON_PATHS[fontConfig.faIcon]}
              fill={fontConfig.color}
              opacity={0.3}
              transform="translate(10, 3.3) scale(0.156)"
            />
          )}

          {/* Connection rectangle on dock-side edge (faces the owning tile) */}
          <line
            x1={dockVertices[0][0]}
            y1={dockVertices[0][1]}
            x2={dockVertices[1][0]}
            y2={dockVertices[1][1]}
            stroke={fontConfig.color}
            strokeWidth="10"
            strokeLinecap="butt"
          />

          {/* Center circle: owner's primary color border when owned, theme color when unowned */}
          <circle
            cx={cx}
            cy={cy}
            r={circleRadius}
            fill="#f5f0e1"
            stroke={ownerColors ? ownerColors.primary : fontConfig.color}
            strokeWidth={ownerColors ? 4 : 2}
          />

          {/* Harbor ship/resource glyph inside circle */}
          <text
            x={cx}
            y={cy}
            textAnchor="middle"
            dominantBaseline="central"
            style={{ fontFamily: 'var(--font-catan)' }}
            fontSize={42}
            fill={fontConfig.color}
            clipPath={`url(#harbor-clip-${side})`}
          >
            {fontConfig.harborGlyph}
          </text>
        </svg>
      </div>
    );
  }

  // Image mode: frosted backdrop with dock/circle design
  const backgroundStyle: React.CSSProperties = {
    clipPath: 'polygon(25% 0%, 75% 0%, 100% 50%, 75% 100%, 25% 100%, 0% 50%)',
    backdropFilter: 'blur(4px)',
    WebkitBackdropFilter: 'blur(4px)',
  };

  if (ownerColors) {
    const start = hexToRgba(ownerColors.primary, 1.0);
    const end = hexToRgba(ownerColors.secondary, 1.0);
    backgroundStyle.background = `linear-gradient(135deg, ${start}, ${end})`;
  } else {
    backgroundStyle.backgroundColor = 'rgba(30, 41, 59, 0.3)';
  }

  return (
    <div className="absolute inset-0" style={backgroundStyle} data-drag-through>
      <svg
        className="absolute inset-0 w-full h-full"
        viewBox="0 0 100 86.6"
        preserveAspectRatio="none"
      >
        <defs>
          <linearGradient id={`dock-wood-${side}`} x1="0%" y1="0%" x2="100%" y2="100%">
            <stop offset="0%" stopColor={DOCK_COLORS.highlight} />
            <stop offset="50%" stopColor={DOCK_COLORS.fill} />
            <stop offset="100%" stopColor={DOCK_COLORS.stroke} />
          </linearGradient>
          <clipPath id={`harbor-clip-${side}`}>
            <circle cx={cx} cy={cy} r={circleRadius - 1} />
          </clipPath>
        </defs>

        {/* Dock-side edge highlight */}
        <line
          x1={dockVertices[0][0]}
          y1={dockVertices[0][1]}
          x2={dockVertices[1][0]}
          y2={dockVertices[1][1]}
          stroke={DOCK_COLORS.stroke}
          strokeWidth="14"
          strokeLinecap="round"
        />

        {/* Harbor circle */}
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
  hexCenterRef,
}: GameBoardProps): React.ReactElement {
  // Use internal hooks for game data (server-driven UI pattern)
  const boardData = useBoardData();
  const players = useBoardPlayers();
  const rolledNumber = useRolledNumber();
  const isAllocationPhase = useIsAllocationPhase();
  const gameState = useGameState();
  const isPickingBoard = gameState === 'PickingBoard';

  // Destructure board data from hook
  const { tiles, harbors, buildings, roads, currentPlayerId, currentPlayerEntitlements, robber } =
    boardData;

  // Derive showSettlementIndexes from server state (not from props)
  // Show indexes when NOT in allocation phase and player has Settlement entitlement
  const showSettlementIndexes = useMemo(() => {
    if (isAllocationPhase) return false;
    return currentPlayerEntitlements.includes('Settlement' as Entitlement);
  }, [isAllocationPhase, currentPlayerEntitlements]);

  // Robber animation state - tracks the position to render (for CSS transition)
  const [animatedRobberCoords, setAnimatedRobberCoords] = useState<{ q: number; r: number } | null>(
    null
  );
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
    // Note: (0,0,0) is the center tile and is a valid position
    const hasValidCurrent = currCoords !== null && currCoords !== undefined;
    const hasValidPrevious = prevCoords !== null && prevCoords !== undefined;
    const coordsDiffer =
      hasValidCurrent &&
      hasValidPrevious &&
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
  const layoutInfoRef = useRef<HexGridLayoutInfo | null>(null);
  const hexGridContentRef = useRef<HTMLDivElement>(null);
  const [containerSize, setContainerSize] = useState<{ width: number; height: number } | null>(
    null
  );

  // Viewport state from layoutStore (persisted, reset via resetLayout)
  const viewport = useLayoutStore((state) => state.viewport);
  const setViewport = useLayoutStore((state) => state.setViewport);

  // Star filter from layoutStore (filters building spots by minimum star value)
  const starFilter = useLayoutStore((state) => state.starFilter);

  // Resource filters from layoutStore (up to 3 selected resources, AND logic)
  const resourceFilters = useLayoutStore((state) => state.resourceFilters);

  // Use viewport zoom or initial prop (viewport.zoom is a multiplier, convert to hexSize)
  const hexSize = viewport.zoom > 0 ? Math.round(initialHexSize * viewport.zoom) : initialHexSize;
  const panOffset = viewport.pan;

  // --- Unified pointer interaction state ---
  const PAN_DRAG_THRESHOLD = 5;
  const LONG_PRESS_MS = 500;

  const dragStateRef = useRef<{
    panning: boolean;
    startClient: PixelPosition;
    startPan: PixelPosition;
    hitTarget: HitTarget;
    isModifier: boolean;
    isTouch: boolean;
    longPressFired: boolean;
  } | null>(null);

  const recentPanRef = useRef(false);
  const longPressTimerRef = useRef<ReturnType<typeof setTimeout> | null>(null);
  const [isPanningForCursor, setIsPanningForCursor] = useState(false);

  // Pinch-to-zoom state
  const activePointersRef = useRef<Map<number, { x: number; y: number }>>(new Map());
  const pinchStateRef = useRef<{
    initialDist: number;
    initialZoom: number;
  } | null>(null);

  const canPan = useCallback((target: HitTarget, isModifier: boolean, isTouch: boolean) => {
    if (isModifier) return true;
    if (target.type === 'none') return true;
    if (isTouch) return true; // Touch always pans (drag threshold disambiguates)
    if (target.type === 'tile' && target.tile.resourceTileType === 'Sea') return true;
    return false;
  }, []);

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

  // Update hex (0,0,0) screen position for overlay positioning
  useEffect(() => {
    if (!hexCenterRef) return;
    const contentEl = hexGridContentRef.current;
    const layout = layoutInfoRef.current;
    if (!contentEl || !layout) {
      hexCenterRef.current = null;
      return;
    }
    const rect = contentEl.getBoundingClientRect();
    hexCenterRef.current = {
      x: rect.left + layout.origin.x,
      y: rect.top + layout.origin.y,
    };
  }, [
    hexCenterRef,
    panOffset.x,
    panOffset.y,
    hexSize,
    containerSize?.width,
    containerSize?.height,
  ]);

  // Handle mouse wheel zoom
  const handleWheel = useCallback(
    (e: React.WheelEvent) => {
      e.preventDefault();
      const delta = e.deltaY > 0 ? -ZOOM_CONFIG.zoomStep : ZOOM_CONFIG.zoomStep;
      const newHexSize = Math.max(
        ZOOM_CONFIG.minHexSize,
        Math.min(ZOOM_CONFIG.maxHexSize, hexSize + delta)
      );
      // Convert hexSize to zoom multiplier
      const newZoom = newHexSize / initialHexSize;
      setViewport({ zoom: newZoom });
    },
    [hexSize, initialHexSize, setViewport]
  );

  // Build set of tile coordinates for quick lookup
  const tileCoordSet = useMemo(() => {
    const set = new Set<string>();
    tiles.forEach((tile) => {
      const coord = cubicCoord(tile.tileKey.q, tile.tileKey.r);
      set.add(coordKeyString(coord));
    });
    return set;
  }, [tiles]);

  // Build tile lookup map for O(1) coord → TileModel access
  const tileMap = useMemo(() => {
    const map = new Map<string, TileModel>();
    tiles.forEach((tile) => {
      const coord = cubicCoord(tile.tileKey.q, tile.tileKey.r);
      map.set(coordKeyString(coord), tile);
    });
    return map;
  }, [tiles]);

  // Build HexGrid items from tiles
  const tileItems: HexGridItem[] = useMemo(() => {
    // Show tile indexes when in MustMoveRobber state or allocation phases
    const showTileIndexes =
      gameState === 'MustMoveRobber' ||
      gameState === 'AllocateResourceForward' ||
      gameState === 'AllocateResourceReverse';

    return tiles.map((tile, index) => {
      const coord = cubicCoord(tile.tileKey.q, tile.tileKey.r);
      const key = coordKeyString(coord);
      const isHighlighted = highlightedTiles?.has(key) || tile.highlighted;
      // Tile is dimmed when a roll is active AND this tile's number doesn't match
      // Sea/Desert tiles (number 0) are also dimmed when a number is rolled
      const isDimmed =
        rolledNumber !== null && rolledNumber !== undefined && tile.number !== rolledNumber;

      return {
        id: `tile-${key}`,
        coord,
        content: (
          <GameTile
            tile={tile}
            hexSize={hexSize}
            isHighlighted={isHighlighted}
            isDimmed={isDimmed}
            tileIndex={showTileIndexes ? index + 1 : undefined}
          />
        ),
      };
    });
  }, [tiles, hexSize, highlightedTiles, rolledNumber, gameState]);

  // Build HexGrid items from harbors (at water hex positions)
  const harborItems: HexGridItem[] = useMemo(() => {
    return harbors
      .filter((h) => h.harborKey.side !== 'None')
      .map((harbor) => {
        const { hexCoordinates, side } = harbor.harborKey;
        const tileCoord = cubicCoord(hexCoordinates.q, hexCoordinates.r);

        // Find the water hex adjacent to the tile in the harbor's direction
        const direction = SIDE_TO_DIRECTION[side as GeometryHexSide];
        const waterCoord = getNeighbor(tileCoord, direction);
        const key = coordKeyString(waterCoord);

        // Get owner colors if harbor is owned
        const ownerColors = harbor.owner?.id
          ? players.find((p) => p.id === harbor.owner.id)?.colors
          : null;

        return {
          id: `harbor-${key}`,
          coord: waterCoord,
          content: <HarborHexContent harbor={harbor} ownerColors={ownerColors} />,
        };
      });
  }, [harbors, players]);

  // Build set of harbor coordinates for quick lookup
  const _harborCoordSet = useMemo(() => {
    const set = new Set<string>();
    harborItems.forEach((item) => {
      set.add(coordKeyString(item.coord));
    });
    return set;
  }, [harborItems]);


  // Calculate board bounds (for water generation) - NO pan offset here
  const boardBounds = useMemo(() => {
    let minQ = Infinity,
      maxQ = -Infinity,
      minR = Infinity,
      maxR = -Infinity;
    tiles.forEach((tile) => {
      minQ = Math.min(minQ, tile.tileKey.q);
      maxQ = Math.max(maxQ, tile.tileKey.q);
      minR = Math.min(minR, tile.tileKey.r);
      maxR = Math.max(maxR, tile.tileKey.r);
    });
    return { minQ, maxQ, minR, maxR };
  }, [tiles]);

  // Theme-resolved sea tile image for water hexes
  const seaTilePath = useAssetPath('TileSea');

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
          content: <WaterHex imageUrl={seaTilePath || undefined} opacity={1} />,
        });
      }
    }

    return items;
  }, [boardBounds, tileCoordSet, seaTilePath]);

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

  // --- Unified interaction: hit-test and dispatch ---

  type HitTarget =
    | { type: 'tile'; tile: TileModel }
    | { type: 'road'; key: string; model: RoadModel; tile: TileModel }
    | { type: 'building'; key: string; model: BuildingModel; tile: TileModel }
    | { type: 'none' };

  const hitTest = useCallback(
    (clientX: number, clientY: number): HitTarget => {
      const layout = layoutInfoRef.current;
      const contentEl = hexGridContentRef.current;
      if (!layout || !contentEl) return { type: 'none' };

      // Convert client coords → hex-grid-local coords
      const rect = contentEl.getBoundingClientRect();
      const localX = clientX - rect.left;
      const localY = clientY - rect.top;

      const { origin, hexSize: hSize } = layout;

      // 1. Resolve underlying tile
      const hexCoord = pixelToHex({ x: localX, y: localY }, hSize, origin);
      const tileKey = coordKeyString(hexCoord);
      const tile = tileMap.get(tileKey);
      if (!tile) return { type: 'none' };

      // 2. Building check — search clicked hex + 6 neighbors
      const buildingHitRadius = hSize * 0.3;
      let closestBuilding: { key: string; model: BuildingModel; dist: number } | null = null;

      const allDirections = [
        Direction.North,
        Direction.NorthEast,
        Direction.SouthEast,
        Direction.South,
        Direction.SouthWest,
        Direction.NorthWest,
      ];
      const hexesToCheck = [hexCoord, ...allDirections.map((dir) => getNeighbor(hexCoord, dir))];
      const hexKeySet = new Set(hexesToCheck.map(coordKeyString));

      for (const bp of buildingPositions) {
        if (!hexKeySet.has(coordKeyString(bp.coord))) continue;
        const model = buildingMap.get(bp.key);
        if (!model) continue;
        const pos = getVertexPosition(bp.coord, bp.position, hSize, origin);
        const dx = localX - pos.x;
        const dy = localY - pos.y;
        const dist = Math.sqrt(dx * dx + dy * dy);
        if (dist < buildingHitRadius && (!closestBuilding || dist < closestBuilding.dist)) {
          closestBuilding = { key: bp.key, model, dist };
        }
      }

      if (closestBuilding) {
        return { type: 'building', key: closestBuilding.key, model: closestBuilding.model, tile };
      }

      // 3. Road check — same neighbor set
      const roadHitRadius = hSize * 0.25;
      let closestRoad: { key: string; model: RoadModel; dist: number } | null = null;

      for (const rp of roadPositions) {
        if (!hexKeySet.has(coordKeyString(rp.coord))) continue;
        const model = roadMap.get(rp.key);
        if (!model) continue;
        const pos = getEdgeMidpoint(rp.coord, rp.side, hSize, origin);
        const dx = localX - pos.x;
        const dy = localY - pos.y;
        const dist = Math.sqrt(dx * dx + dy * dy);
        if (dist < roadHitRadius && (!closestRoad || dist < closestRoad.dist)) {
          closestRoad = { key: rp.key, model, dist };
        }
      }

      if (closestRoad) {
        return { type: 'road', key: closestRoad.key, model: closestRoad.model, tile };
      }

      // 4. Tile
      return { type: 'tile', tile };
    },
    [tileMap, buildingPositions, roadPositions, buildingMap, roadMap]
  );

  const dispatchInteraction = useCallback(
    (target: HitTarget, button: 'left' | 'right', clientX?: number, clientY?: number) => {
      if (target.type === 'none') return;

      const tile = target.tile;

      // Sea tiles are pan-only — no game actions
      if (tile.resourceTileType === 'Sea') return;

      // Right-click always goes to the tile (robber placement, etc.)
      if (button === 'right') {
        if (clientX == null || clientY == null) {
          if (process.env.NODE_ENV !== 'production') {
            // Missing coordinates would misplace context menus; treat as a programming error.
            // All current call sites are expected to pass real coordinates.
            // eslint-disable-next-line no-console
            console.error('dispatchInteraction: right-click called without clientX/clientY', {
              target,
            });
          }
          return;
        }

        // Synthesize a minimal MouseEvent with real coordinates for menu positioning
        const syntheticEvent = { clientX, clientY } as React.MouseEvent;
        onTileRightClick?.(tile, syntheticEvent);
        return;
      }

      // Left-click: dispatch based on target type with fallthrough
      switch (target.type) {
        case 'building': {
          if (onBuildingClick) {
            const bk = target.model.buildingKey;
            onBuildingClick(bk);
            return;
          }
          break;
        }
        case 'road': {
          if (target.model.roadState === 'Buildable' && onRoadClick) {
            onRoadClick(target.model.roadKey);
            return;
          }
          break; // Non-buildable road → fall through to tile
        }
      }

      // Tile-level left-click (or fallthrough)
      if (clientX == null || clientY == null) {
        if (process.env.NODE_ENV !== 'production') {
          // Missing coordinates would misplace click-positioned UI; treat as a programming error.
          // All current call sites are expected to pass real coordinates.
          // eslint-disable-next-line no-console
          console.error('dispatchInteraction: left-click called without clientX/clientY', {
            target,
          });
        }
        return;
      }

      onTileClick?.(tile, clientX, clientY);
    },
    [onTileClick, onTileRightClick, onBuildingClick, onRoadClick]
  );

  // --- Pointer event handlers (must be after hitTest/dispatchInteraction) ---

  const handlePointerDown = useCallback(
    (e: React.PointerEvent) => {
      if (e.button !== 0) return; // Left button only for pan/click

      // Track pointer for pinch-to-zoom
      activePointersRef.current.set(e.pointerId, { x: e.clientX, y: e.clientY });

      // Second finger → start pinch-zoom, cancel any single-finger interaction
      if (activePointersRef.current.size === 2) {
        if (longPressTimerRef.current) {
          clearTimeout(longPressTimerRef.current);
          longPressTimerRef.current = null;
        }
        dragStateRef.current = null;
        setIsPanningForCursor(false);

        const pointers = Array.from(activePointersRef.current.values());
        const dx = pointers[0].x - pointers[1].x;
        const dy = pointers[0].y - pointers[1].y;
        pinchStateRef.current = {
          initialDist: Math.sqrt(dx * dx + dy * dy),
          initialZoom: viewport.zoom > 0 ? viewport.zoom : 1,
        };
        e.preventDefault();
        return;
      }

      // Third+ finger → ignore
      if (activePointersRef.current.size > 2) return;

      const target = hitTest(e.clientX, e.clientY);
      const isModifier = e.ctrlKey || e.metaKey;
      const isTouch = e.pointerType === 'touch';

      // For mouse on pan-only surfaces (water/void), skip drag threshold — start panning
      // immediately. These surfaces have no click action, so there's nothing to disambiguate.
      const isPanOnlySurface =
        !isTouch &&
        !isModifier &&
        (target.type === 'none' ||
          (target.type === 'tile' && target.tile.resourceTileType === 'Sea'));

      dragStateRef.current = {
        panning: isPanOnlySurface,
        startClient: { x: e.clientX, y: e.clientY },
        startPan: { ...panOffset },
        hitTarget: target,
        isModifier,
        isTouch,
        longPressFired: false,
      };

      if (isPanOnlySurface) {
        setIsPanningForCursor(true);
      }

      // Pointer capture so we get move/up even if cursor leaves
      (e.target as HTMLElement).setPointerCapture(e.pointerId);

      // Start long-press timer for touch
      if (isTouch) {
        longPressTimerRef.current = setTimeout(() => {
          const state = dragStateRef.current;
          if (state && !state.panning) {
            state.longPressFired = true;
            navigator.vibrate?.(50);
            dispatchInteraction(state.hitTarget, 'right', state.startClient.x, state.startClient.y);
          }
        }, LONG_PRESS_MS);
      }

      // Prevent text selection if we might pan
      if (canPan(target, isModifier, isTouch)) {
        e.preventDefault();
      }
    },
    [hitTest, panOffset, dispatchInteraction, viewport.zoom, canPan]
  );

  const handlePointerMove = useCallback(
    (e: React.PointerEvent) => {
      // Update pointer position for pinch tracking
      if (activePointersRef.current.has(e.pointerId)) {
        activePointersRef.current.set(e.pointerId, { x: e.clientX, y: e.clientY });
      }

      // Handle pinch-to-zoom when two pointers are active
      if (pinchStateRef.current && activePointersRef.current.size >= 2) {
        const pointers = Array.from(activePointersRef.current.values());
        const dx = pointers[0].x - pointers[1].x;
        const dy = pointers[0].y - pointers[1].y;
        const currentDist = Math.sqrt(dx * dx + dy * dy);
        const scale = currentDist / pinchStateRef.current.initialDist;
        const newZoom = pinchStateRef.current.initialZoom * scale;

        const minZoom = ZOOM_CONFIG.minHexSize / initialHexSize;
        const maxZoom = ZOOM_CONFIG.maxHexSize / initialHexSize;
        setViewport({ zoom: Math.max(minZoom, Math.min(maxZoom, newZoom)) });
        return;
      }

      // Single-pointer pan handling
      const state = dragStateRef.current;
      if (!state) return;

      const dx = e.clientX - state.startClient.x;
      const dy = e.clientY - state.startClient.y;
      const dist = Math.sqrt(dx * dx + dy * dy);

      if (!state.panning) {
        if (dist >= PAN_DRAG_THRESHOLD) {
          // Cancel long-press
          if (longPressTimerRef.current) {
            clearTimeout(longPressTimerRef.current);
            longPressTimerRef.current = null;
          }

          if (canPan(state.hitTarget, state.isModifier, state.isTouch)) {
            state.panning = true;
            setIsPanningForCursor(true);
          }
        }
      }

      if (state.panning) {
        setViewport({
          pan: {
            x: state.startPan.x + dx,
            y: state.startPan.y + dy,
          },
        });
      }
    },
    [setViewport, initialHexSize, canPan]
  );

  const handlePointerUp = useCallback(
    (e: React.PointerEvent) => {
      // Remove pointer from tracking
      activePointersRef.current.delete(e.pointerId);

      // End pinch if was pinching
      if (pinchStateRef.current) {
        if (activePointersRef.current.size < 2) {
          pinchStateRef.current = null;
        }
        dragStateRef.current = null;
        return; // Don't dispatch click after pinch
      }

      const state = dragStateRef.current;
      if (!state) return;

      // Cancel long-press timer
      if (longPressTimerRef.current) {
        clearTimeout(longPressTimerRef.current);
        longPressTimerRef.current = null;
      }

      if (state.panning) {
        // Suppress click after pan
        recentPanRef.current = true;
        setTimeout(() => {
          recentPanRef.current = false;
        }, 0);
        setIsPanningForCursor(false);
      } else if (!state.longPressFired) {
        // Not panning, not long-press → dispatch as click
        dispatchInteraction(state.hitTarget, 'left', e.clientX, e.clientY);
      }

      dragStateRef.current = null;
    },
    [dispatchInteraction]
  );

  const handleContextMenu = useCallback(
    (e: React.MouseEvent) => {
      e.preventDefault();
      if (recentPanRef.current) return;
      const target = hitTest(e.clientX, e.clientY);
      dispatchInteraction(target, 'right', e.clientX, e.clientY);
    },
    [hitTest, dispatchInteraction]
  );

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

  // Build a map of tile coordinates to their resource type for resource filtering
  const tileResourceMap = useMemo(() => {
    const map = new Map<string, string>();
    tiles.forEach((tile) => {
      const coord = cubicCoord(tile.tileKey.q, tile.tileKey.r);
      map.set(coordKeyString(coord), tile.resourceTileType);
    });
    return map;
  }, [tiles]);

  // Calculate star value for a building position based on adjacent tiles' pips
  // Stars = sum of pips from all tiles touching this vertex (up to 3 tiles)
  const calculateStars = useCallback(
    (coord: HexCoordinate, position: GeometryHexPosition): number => {
      const adjacentCoords: HexCoordinate[] = [coord];
      const directions = VERTEX_NEIGHBOR_DIRECTIONS[position];
      directions.forEach((dir) => {
        adjacentCoords.push(getNeighbor(coord, dir));
      });

      let totalPips = 0;
      adjacentCoords.forEach((c) => {
        totalPips += tilePipsMap.get(coordKeyString(c)) ?? 0;
      });

      return totalPips;
    },
    [tilePipsMap]
  );

  // Get resource types of tiles adjacent to a building vertex (up to 3, excluding Desert/Sea/None)
  const getAdjacentResources = useCallback(
    (coord: HexCoordinate, position: GeometryHexPosition): string[] => {
      const adjacentCoords: HexCoordinate[] = [coord];
      const directions = VERTEX_NEIGHBOR_DIRECTIONS[position];
      directions.forEach((dir) => {
        adjacentCoords.push(getNeighbor(coord, dir));
      });

      const resources: string[] = [];
      adjacentCoords.forEach((c) => {
        const r = tileResourceMap.get(coordKeyString(c));
        if (r && r !== 'Desert' && r !== 'Sea' && r !== 'None') {
          resources.push(r);
        }
      });
      return resources;
    },
    [tileResourceMap]
  );

  // Render buildings and roads overlay (DOM divs)
  // Implements Blazor BuildingOverlay.razor and RoadOverlay.razor logic
  const renderOverlay = useCallback(
    (layoutInfo: HexGridLayoutInfo) => {
      const { origin, hexSize: hSize } = layoutInfo;

      // Building sizes: owned=larger, buildable=smaller
      // Base ratios from Blazor (24 vs 18 SVG units at HexSize=100), scaled up ~25% per user feedback
      const ownedBuildingSize = hSize * 0.6; // 24/50 * 1.25 = 0.60
      const buildableBuildingSize = hSize * 0.45; // 18/50 * 1.25 = 0.45

      // Road container size (SVG viewBox is hexSize * 1.2, centered at origin)
      const roadContainerSize = hSize * 1.2;

      // Get current player colors
      const currentPlayer = currentPlayerId
        ? players.find((p) => p.id === currentPlayerId)
        : players[0];

      // Check entitlements for current player
      const hasSettlementEntitlement = currentPlayerEntitlements.includes(
        'Settlement' as Entitlement
      );
      const hasCityEntitlement = currentPlayerEntitlements.includes('City' as Entitlement);
      const _hasRoadEntitlement = currentPlayerEntitlements.includes('Road' as Entitlement);

      // Build city upgrade index map (Z, Y, X...) for current player's settlements
      // Uses reverse alphabet to avoid overlap with road labels (1-9, A-Z)
      const cityUpgradeIndexMap = new Map<string, string>();
      if (hasCityEntitlement && currentPlayer) {
        let cityIndex = 0; // 0 = 'Z', 1 = 'Y', etc. (reverse alphabet)
        buildingPositions.forEach(({ key }) => {
          const buildingModel = buildingMap.get(key);
          if (
            buildingModel &&
            buildingModel.buildingState === 'Settlement' &&
            buildingModel.ownerId === currentPlayer.id
          ) {
            cityUpgradeIndexMap.set(key, String.fromCharCode(90 - cityIndex)); // 90 = 'Z', descending
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
          if (
            buildingModel?.buildingState === 'PossibleSettlement' &&
            buildingModel?.ownerId === null
          ) {
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

            // Include player ID in key to force re-render on ownership/turn changes
            // Buildable roads: use current player ID (changes on turn change)
            // Owned roads: use owner ID (ensures correct colors after purchase)
            const roadKeyString =
              roadState === 'Buildable'
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
                  currentPlayerId={currentPlayerId}
                  hexSize={hSize}
                  buildIndex={roadModel.buildIndex}
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
            const _owner = players.find((p) => p.id === ownerId);

            // City upgrade: clickable if current player owns this settlement AND has city entitlement
            const isCityUpgradeable =
              buildingState === 'Settlement' && ownerId === currentPlayer?.id && hasCityEntitlement;

            // Get city upgrade letter (Z, Y, X...) if this is an upgradeable settlement
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
                  currentPlayerId={currentPlayerId}
                  size={ownedBuildingSize}
                  buildIndex={cityUpgradeIndex}
                />
              </div>
            );
          })}

          {/* Loop 2: Building spots — three-state logic matching Blazor BuildingOverlay.razor
            PickingBoard: show all spots (incl. NotBuildable) for star evaluation, not buildable
            Allocation:   show PossibleSettlement with star/resource filters, buildable
            Gameplay:     show PossibleSettlement with numbered build indexes, buildable */}
          {buildingPositions.map(({ key, coord, position }) => {
            const buildingModel = buildingMap.get(key);
            const buildingState = buildingModel?.buildingState ?? 'PossibleSettlement';
            const ownerId = buildingModel?.ownerId;

            // Skip owned buildings (handled in Loop 1)
            if (ownerId !== null) return null;

            // During PickingBoard: show both PossibleSettlement and NotBuildable for evaluation
            // Otherwise: only show PossibleSettlement
            if (buildingState === 'NotBuildable') {
              if (!isPickingBoard) return null;
            } else if (buildingState !== 'PossibleSettlement') {
              return null;
            }

            // Calculate stars for this position
            const stars = calculateStars(coord, position);

            // During PickingBoard: skip 0-star spots (no production value to evaluate)
            if (stars <= 0 && isPickingBoard) return null;

            // Resource filter: apply during PickingBoard and allocation (not regular gameplay)
            // Matches Blazor BuildingOverlay.razor line 193
            if (resourceFilters.length > 0 && (isPickingBoard || isAllocationPhase)) {
              const adjResources = getAdjacentResources(coord, position);
              if (!resourceFilters.every((r) => adjResources.includes(r))) return null;
            }

            // Determine visibility based on game state (Blazor lines 214-248)
            let isHidden: boolean;
            let settlementBuildIndex: string | undefined;

            if (isPickingBoard) {
              // PickingBoard: evaluation mode — show stars, not buildable
              // No star filter selected → hide all evaluation spots (user must choose a filter)
              if (starFilter === null) return null;
              // starFilter=0 ("All") → stars < 0 always false → show everything
              // starFilter>0 → hide spots below threshold
              if (stars < starFilter) return null;
              isHidden = false;
            } else if (hasSettlementEntitlement && buildingState === 'PossibleSettlement') {
              // Buildable settlement spot
              settlementBuildIndex = settlementIndexMap.get(key);

              if (settlementBuildIndex !== undefined) {
                // Regular gameplay: indexed spots always visible (per Blazor line 232)
                isHidden = false;
              } else {
                // Allocation phase: star threshold for visibility (hover reveals hidden spots)
                isHidden = starFilter === null || stars < starFilter;
              }
            } else {
              // Not buildable, not picking board — don't show
              return null;
            }

            const pixelPos = getVertexPosition(coord, position, hSize, origin);

            // Visual state: Highlighted when showing build indexes, Stars when visible, Hidden otherwise
            const visualState: BuildingVisualState =
              settlementBuildIndex !== undefined ? 'Highlighted' : isHidden ? 'Hidden' : 'Stars';

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
                  currentPlayerId={currentPlayerId}
                  size={buildableBuildingSize}
                  buildIndex={settlementBuildIndex}
                />
              </div>
            );
          })}

          {/* Robber layer - renders on top of everything using CatanFont glyphs with player colors */}
          {/* Uses inline SVG to properly render gradient fills like Blazor does */}
          {/* Animation: uses animatedRobberCoords with CSS transition (matches Blazor RobberLayer.razor) */}
          {robber &&
            animatedRobberCoords &&
            (() => {
              const robberCoord = cubicCoord(animatedRobberCoords.q, animatedRobberCoords.r);
              const robberPos = hexToPixel(robberCoord, hSize, origin);
              // Hex height = sqrt(3) * hexSize for flat-top hexes
              const hHeight = Math.sqrt(3) * hSize;
              const robberFontSize = hHeight * 0.5; // 50% of hex height (matches Blazor)

              // Get colors for player who moved the robber
              const movedByPlayer = players.find((p) => p.id === robber.movedBy);
              const movedByColors = movedByPlayer?.colors;
              const primaryColor = movedByColors?.primary || '#666';
              const secondaryColor = movedByColors?.secondary || '#888';
              const foregroundColor = movedByColors?.foreground || '#fff';

              // Shield is larger than the robber icon so the count fits inside
              const shieldFontSize = robberFontSize * 1.2;
              // SVG viewBox size - large enough for the glyphs
              const svgSize = shieldFontSize * 1.5;

              return (
                <div
                  key="robber"
                  className="absolute pointer-events-none"
                  style={{
                    left: robberPos.x,
                    top: robberPos.y,
                    transform: 'translate(-50%, -50%)',
                    filter: 'drop-shadow(2px 2px 4px rgba(0,0,0,0.5))',
                    // CSS transition for smooth animation - uses --animation-slow (500ms at Normal)
                    // Scales with AnimationSpeed setting (Slow=1s, Normal=500ms, Fast=250ms, None=0ms)
                    transition:
                      'left var(--animation-slow) ease-in-out, top var(--animation-slow) ease-in-out',
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
                      style={{ fontFamily: 'var(--font-catan)' }}
                      fontSize={shieldFontSize}
                      fill="url(#robber-gradient)"
                    >
                      {'\uE925'}
                    </text>
                    {/* Foreground: Pirate glyph (E90C) with player foreground color, shifted up */}
                    <text
                      x={svgSize / 2}
                      y={svgSize / 2 - shieldFontSize * 0.1}
                      textAnchor="middle"
                      dominantBaseline="central"
                      style={{ fontFamily: 'var(--font-catan)' }}
                      fontSize={robberFontSize * 0.8}
                      fill={foregroundColor}
                    >
                      {'\uE90C'}
                    </text>
                    {/* Resources stolen count (matches Blazor RobberLayer.razor RenderResourcesStolen) */}
                    {robber.resourcesStolen > 0 && (
                      <text
                        x={svgSize / 2}
                        y={svgSize / 2 + shieldFontSize * 0.28}
                        textAnchor="middle"
                        dominantBaseline="central"
                        style={{ fontFamily: 'Segoe UI, sans-serif' }}
                        fontSize={shieldFontSize * 0.4}
                        fontWeight="bold"
                        fill={foregroundColor}
                      >
                        {robber.resourcesStolen}
                      </text>
                    )}
                  </svg>
                </div>
              );
            })()}
        </>
      );
    },
    [
      buildingPositions,
      roadPositions,
      buildingMap,
      roadMap,
      players,
      currentPlayerId,
      calculateStars,
      starFilter,
      resourceFilters,
      getAdjacentResources,
      currentPlayerEntitlements,
      robber,
      animatedRobberCoords,
      showSettlementIndexes,
      isPickingBoard,
      isAllocationPhase,
    ]
  );

  // Loading state - show message but keep container mounted for ref measurement
  // With hooks, data comes from store - check if tiles are loaded
  const isLoading = tiles.length === 0;

  return (
    <div
      ref={containerRef}
      className="relative w-full h-full overflow-hidden select-none"
      onWheel={handleWheel}
      onPointerDown={handlePointerDown}
      onPointerMove={handlePointerMove}
      onPointerUp={handlePointerUp}
      onContextMenu={handleContextMenu}
      style={{
        cursor: isPanningForCursor ? 'grabbing' : 'grab',
        touchAction: 'none',
      }}
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
            overlay={
              players.length > 0
                ? (layoutInfo) => {
                    layoutInfoRef.current = layoutInfo;
                    return (
                      <>
                        <div
                          ref={(el) => {
                            if (el?.parentElement) {
                              hexGridContentRef.current = el.parentElement as HTMLDivElement;
                            }
                          }}
                          style={{ display: 'none' }}
                        />
                        {renderOverlay(layoutInfo)}
                      </>
                    );
                  }
                : undefined
            }
          />
        </div>
      ) : null}
    </div>
  );
}

export default GameBoard;
