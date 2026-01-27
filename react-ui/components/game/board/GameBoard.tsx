'use client';

import React, { useMemo, useRef, useState, useEffect, useCallback } from 'react';
import { HexGrid, HexGridItem, type HexGridLayoutInfo } from '@/components/hex-grid';
import {
  cubicCoord,
  getNeighbor,
  getVertexPosition,
  getEdgeMidpoint,
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

// Import generated types
import type { TileModel } from '@/types/generated/models/tile-model';
import type { HarborModel } from '@/types/generated/models/harbor-model';
import type { BuildingModel } from '@/types/generated/models/building-model';
import type { RoadModel } from '@/types/generated/models/road-model';
import type { HexSide } from '@/types/generated/models/hex-side';
import type { HexPosition } from '@/types/generated/models/hex-position';

/**
 * Minimal game data required for board rendering.
 * Compatible with both TestGameData and full GameModel.
 */
export interface BoardGameData {
  tiles: TileModel[];
  harbors: HarborModel[];
  buildings?: BuildingModel[];
  roads?: RoadModel[];
}

/** Zoom configuration */
const ZOOM_CONFIG = {
  minHexSize: 20,
  maxHexSize: 150,
  zoomStep: 5,        // Pixels to change hexSize per wheel tick
  defaultSize: 50,
};

/** Player colors for buildings */
export interface PlayerColors {
  primary: string;
  secondary: string;
  foreground: string;
}

/** Player data for building rendering */
export interface BoardPlayer {
  id: string;
  name: string;
  colors: PlayerColors;
}

/** All 6 vertex positions on a hex (excluding 'None') */
const ALL_POSITIONS: GeometryHexPosition[] = ['Right', 'BottomRight', 'BottomLeft', 'Left', 'TopLeft', 'TopRight'];

/** All 6 edge sides on a hex (excluding 'None') */
const ALL_SIDES: GeometryHexSide[] = ['Top', 'TopRight', 'BottomRight', 'Bottom', 'BottomLeft', 'TopLeft'];

/**
 * Props for GameBoard component
 */
export interface GameBoardProps {
  /** Game data containing tiles, harbors, buildings, roads */
  gameModel: BoardGameData | null;
  /** Initial hex size (circumradius) - default 50. Controlled via mouse wheel after mount. */
  hexSize?: number;
  /** Gap between hexes - default 2 */
  gap?: number;
  /** Callback when a tile is clicked */
  onTileClick?: (tile: TileModel) => void;
  /** Set of highlighted tile keys (for dice roll highlighting) */
  highlightedTiles?: Set<string>;
  /** Players for building colors (click to assign) */
  players?: BoardPlayer[];
  /** Currently selected player ID (used when clicking buildings) */
  selectedPlayerId?: string;
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
 * Harbor hex content - displays harbor icon in a triangular dock connecting to tile
 * Background uses CSS gradient via hex-clip-flat (indicates current turn), SVG only for triangle and circle
 */
interface HarborHexContentProps {
  harbor: HarborModel;
  /** Player colors for background gradient (indicates whose turn it is) */
  playerColors?: PlayerColors;
}

function HarborHexContent({ harbor, playerColors }: HarborHexContentProps) {
  const { harborType, side } = harbor.harborKey;
  const imageUrl = getHarborImage(harborType);
  const vertices = SIDE_TO_VERTICES[side];

  // Circle parameters (in viewBox units)
  // Size matches NumberToken visually (radius 30 in 64x64 viewBox ≈ 26 in 100x86.6 viewBox)
  const cx = 50;
  const cy = 43.3;
  const circleRadius = 26;

  // CSS gradient from player colors
  const cssGradient = playerColors
    ? `linear-gradient(135deg, ${playerColors.primary}, ${playerColors.secondary})`
    : undefined;

  // For 'None' harbors, just show the gradient background (DOM-based)
  if (!imageUrl || harborType === 'None') {
    return cssGradient ? (
      <div
        className="absolute inset-0 hex-clip-flat"
        style={{ background: cssGradient, opacity: 0.7 }}
      />
    ) : null;
  }

  // Triangle points: center + two edge vertices
  const trianglePoints = `${cx},${cy} ${vertices[0][0]},${vertices[0][1]} ${vertices[1][0]},${vertices[1][1]}`;

  return (
    <div className="absolute inset-0">
      {/* Background hex fill with player gradient - DOM-based, indicates turn */}
      {cssGradient && (
        <div
          className="absolute inset-0 hex-clip-flat"
          style={{ background: cssGradient, opacity: 0.7 }}
        />
      )}

      {/* SVG for triangular dock and harbor circle (only these need SVG) */}
      <svg
        className="absolute inset-0 w-full h-full"
        viewBox="0 0 100 86.6"
        preserveAspectRatio="none"
      >
        <defs>
          <pattern
            id={`water-pattern-${side}`}
            patternUnits="userSpaceOnUse"
            width="100"
            height="86.6"
          >
            <image
              href="/themes/base/tiles/back.jpg"
              width="100"
              height="86.6"
              preserveAspectRatio="xMidYMid slice"
            />
          </pattern>
          <clipPath id={`harbor-clip-${side}`}>
            <circle cx={cx} cy={cy} r={circleRadius - 1} />
          </clipPath>
        </defs>

        {/* Triangle dock with water fill and border */}
        <polygon
          points={trianglePoints}
          fill={`url(#water-pattern-${side})`}
          stroke="#1e3a5f"
          strokeWidth="2"
          strokeLinejoin="round"
        />

        {/* Harbor circle background */}
        <circle
          cx={cx}
          cy={cy}
          r={circleRadius}
          fill="#f5f5dc"
          stroke="#1e3a5f"
          strokeWidth="2"
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
  gameModel,
  hexSize: initialHexSize = ZOOM_CONFIG.defaultSize,
  gap = 1,
  onTileClick,
  highlightedTiles,
  players = [],
  selectedPlayerId,
}: GameBoardProps): React.ReactElement {
  // Handle null gameModel
  const tiles = gameModel?.tiles ?? [];
  const harbors = gameModel?.harbors ?? [];
  const buildings = gameModel?.buildings ?? [];
  const roads = gameModel?.roads ?? [];

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

      return {
        id: `tile-${key}`,
        coord,
        content: (
          <GameTile
            tile={tile}
            hexSize={hexSize}
            isHighlighted={isHighlighted}
            onClick={onTileClick ? () => onTileClick(tile) : undefined}
          />
        ),
      };
    });
  }, [tiles, hexSize, highlightedTiles, onTileClick]);

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

      return {
        id: `harbor-${key}`,
        coord: waterCoord,
        content: <HarborHexContent harbor={harbor} playerColors={currentPlayerColors} />,
      };
    });
  }, [harbors, currentPlayerColors]);

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

        // Skip if this coord is a tile or harbor
        if (tileCoordSet.has(key) || harborCoordSet.has(key)) continue;

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
  const renderOverlay = useCallback((layoutInfo: HexGridLayoutInfo) => {
    const { origin, hexSize: hSize } = layoutInfo;

    // Building size: 40% of hexSize (matches Blazor: BuildingSize=40 at HexSize=100)
    const buildingSize = hSize * 0.4;

    // Road container size (SVG viewBox is hexSize * 1.2, centered at origin)
    const roadContainerSize = hSize * 1.2;

    // Get current player colors
    const currentPlayer = selectedPlayerId ? players.find(p => p.id === selectedPlayerId) : players[0];

    return (
      <>
        {/* Roads layer (render first, below buildings) */}
        {roadPositions.map(({ key, coord, side }) => {
          const pixelPos = getEdgeMidpoint(coord, side, hSize, origin);
          const roadModel = roadMap.get(key);

          // Get road state from model, default to Buildable for testing (shows on hover)
          const roadState: RoadState = (roadModel?.roadState as RoadState) ?? 'Buildable';
          const ownerId = roadModel?.ownerId;
          const owner = ownerId ? players.find((p) => p.id === ownerId) : null;

          return (
            <div
              key={`road-${key}`}
              className="absolute"
              style={{
                left: pixelPos.x - roadContainerSize / 2,
                top: pixelPos.y - roadContainerSize / 2,
              }}
            >
              <Road
                roadState={roadState}
                side={side}
                ownerColors={owner?.colors}
                currentPlayerColors={currentPlayer?.colors}
                hexSize={hSize}
              />
            </div>
          );
        })}

        {/* Buildings layer (render on top of roads) */}
        {buildingPositions.map(({ key, coord, position }) => {
          const pixelPos = getVertexPosition(coord, position, hSize, origin);
          const buildingModel = buildingMap.get(key);

          // Get building state from model, default to PossibleSettlement for testing
          const buildingState = buildingModel?.buildingState ?? 'PossibleSettlement';
          const ownerId = buildingModel?.ownerId;
          const owner = ownerId ? players.find((p) => p.id === ownerId) : null;

          // Calculate stars for this position
          const stars = calculateStars(coord, position);

          // Determine visual state based on building state, ownership, and star filter
          // Following Blazor precedent from BuildingOverlay.razor
          let visualState: BuildingVisualState = 'Hidden';

          if (owner) {
            // Owned buildings always show as Normal
            visualState = 'Normal';
          } else if (buildingState === 'NotBuildable') {
            // NotBuildable positions: only show during star evaluation (when filter active)
            if (starFilter === null) {
              return null; // Don't render NotBuildable when not filtering
            }
            // When filter is active, show NotBuildable spots for star evaluation if they pass threshold
            if (stars < starFilter) {
              return null; // Below threshold
            }
            visualState = 'Stars'; // Show star count for evaluation
          } else {
            // PossibleSettlement spots
            if (starFilter !== null) {
              // Star filter active: show spots that pass threshold with star counts
              if (stars < starFilter) {
                return null; // Below threshold - don't show
              }
              visualState = 'Stars'; // Above threshold - show star count
            } else {
              // No filter: Hidden (shows on hover)
              visualState = 'Hidden';
            }
          }

          return (
            <div
              key={`building-${key}`}
              className="absolute"
              style={{
                left: pixelPos.x - buildingSize / 2,
                top: pixelPos.y - buildingSize / 2,
              }}
            >
              <Building
                buildingState={buildingState}
                visualState={visualState}
                stars={stars}
                ownerColors={owner?.colors}
                currentPlayerColors={currentPlayer?.colors}
                size={buildingSize}
              />
            </div>
          );
        })}
      </>
    );
  }, [buildingPositions, roadPositions, buildingMap, roadMap, players, selectedPlayerId, calculateStars, starFilter]);

  // Debug logging
  console.log('[GameBoard] render, tiles:', tiles.length, 'containerSize:', containerSize);

  // Loading state - show message but keep container mounted for ref measurement
  const isLoading = !gameModel || tiles.length === 0;

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
          <span className="text-gray-400">
            {!gameModel ? 'Loading game board...' : 'Waiting for tiles...'}
          </span>
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

      {/* Controls indicator */}
      <div className="absolute bottom-2 right-2 bg-black/60 text-white text-xs px-2 py-1 rounded pointer-events-none">
        Hex: {hexSize}px | Scroll=zoom | CTRL+drag=pan
      </div>
    </div>
  );
}

export default GameBoard;
