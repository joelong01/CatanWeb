'use client';

import React, { useMemo, useCallback } from 'react';
import { HexGrid, type HexGridItem, type HexGridLayoutInfo } from '@/components/hex-grid';
import {
  cubicCoord,
  getVertexPosition,
  type HexPosition as GeometryHexPosition,
} from '@/components/hex-grid/hex-geometry';
import { GameTile } from '@/components/game/tiles/GameTile';
import type { TileModel } from '@/types/generated/models/tile-model';
import type { PlayerColors } from '@/types/player-profile';

const DEFAULT_COLORS: PlayerColors = {
  primary: '#4A90A4',
  secondary: '#357A8C',
  foreground: '#FFFFFF',
};

/** Fallback colors when no player profiles are loaded, indexed by player order. */
const PLAYER_FALLBACKS: PlayerColors[] = [
  { primary: '#1565C0', secondary: '#0D47A1', foreground: '#FFFFFF' },
  { primary: '#C62828', secondary: '#B71C1C', foreground: '#FFFFFF' },
  { primary: '#2E7D32', secondary: '#1B5E20', foreground: '#FFFFFF' },
  { primary: '#6A1B9A', secondary: '#4A148C', foreground: '#FFFFFF' },
  { primary: '#E65100', secondary: '#BF360C', foreground: '#FFFFFF' },
  { primary: '#00838F', secondary: '#006064', foreground: '#FFFFFF' },
];

/** Map hex side to its two adjacent vertex positions for drawing road segments. */
const SIDE_TO_VERTEX_PAIR: Record<string, [GeometryHexPosition, GeometryHexPosition]> = {
  Top: ['TopLeft', 'TopRight'],
  TopRight: ['TopRight', 'Right'],
  BottomRight: ['Right', 'BottomRight'],
  Bottom: ['BottomRight', 'BottomLeft'],
  BottomLeft: ['BottomLeft', 'Left'],
  TopLeft: ['Left', 'TopLeft'],
};

export interface ReplayBoardPreviewProps {
  // eslint-disable-next-line @typescript-eslint/no-explicit-any
  gameModel: any;
  playerColorMap?: Map<string, PlayerColors>;
  onClick?: () => void;
}

/**
 * Standalone board preview that renders from a GameModel prop.
 *
 * Unlike GameBoard (which reads from the Zustand game store), this component
 * accepts game data directly, making it suitable for replay previews that
 * don't interfere with any active game.
 *
 * Renders tiles via GameTile (theme hooks are global), and draws buildings
 * and roads as simple SVG shapes in an overlay.
 */
export function ReplayBoardPreview({
  gameModel,
  playerColorMap,
  onClick,
}: ReplayBoardPreviewProps): React.ReactElement | null {
  // Extract data with stable references (avoids new [] on every render when gameModel is null)
  const _tiles = useMemo((): TileModel[] => gameModel?.tiles ?? [], [gameModel?.tiles]);
  const _buildings = useMemo(
    // eslint-disable-next-line @typescript-eslint/no-explicit-any
    (): any[] => gameModel?.buildings ?? [],
    [gameModel?.buildings]
  );
  const _roads = useMemo(
    // eslint-disable-next-line @typescript-eslint/no-explicit-any
    (): any[] => gameModel?.roads ?? [],
    [gameModel?.roads]
  );
  const _players = useMemo(
    // eslint-disable-next-line @typescript-eslint/no-explicit-any
    (): any[] => gameModel?.players ?? [],
    [gameModel?.players]
  );

  // Resolve player color by ID, falling back to positional colors
  const getColor = useCallback(
    (playerId: string | null): PlayerColors => {
      if (!playerId) return DEFAULT_COLORS;
      if (playerColorMap?.has(playerId)) return playerColorMap.get(playerId)!;
      // eslint-disable-next-line @typescript-eslint/no-explicit-any
      const players: any[] = gameModel?.players ?? [];
      // eslint-disable-next-line @typescript-eslint/no-explicit-any
      const idx = players.findIndex((p: any) => p.id === playerId);
      return PLAYER_FALLBACKS[idx >= 0 ? idx % PLAYER_FALLBACKS.length : 0];
    },
    [playerColorMap, gameModel?.players]
  );

  const tileItems: HexGridItem[] = useMemo(() => {
    const tiles: TileModel[] = gameModel?.tiles ?? [];
    return tiles.map((tile) => {
      const coord = cubicCoord(tile.tileKey.q, tile.tileKey.r);
      return {
        id: `tile-${coord.q},${coord.r},${coord.s}`,
        coord,
        content: <GameTile tile={tile} hexSize={50} />,
      };
    });
  }, [gameModel?.tiles]);

  // Only buildings that are placed on the board (owned settlements/cities)
  const ownedBuildings = useMemo(() => {
    // eslint-disable-next-line @typescript-eslint/no-explicit-any
    const buildings: any[] = gameModel?.buildings ?? [];
    return buildings.filter(
      // eslint-disable-next-line @typescript-eslint/no-explicit-any
      (b: any) => b.ownerId && (b.buildingState === 'Settlement' || b.buildingState === 'City')
    );
  }, [gameModel?.buildings]);

  // Only roads that belong to a player
  const ownedRoads = useMemo(() => {
    // eslint-disable-next-line @typescript-eslint/no-explicit-any
    const roads: any[] = gameModel?.roads ?? [];
    // eslint-disable-next-line @typescript-eslint/no-explicit-any
    return roads.filter((r: any) => r.ownerId);
  }, [gameModel?.roads]);

  const renderOverlay = useCallback(
    (layoutInfo: HexGridLayoutInfo) => {
      const { origin, hexSize } = layoutInfo;

      return (
        <svg
          className="absolute inset-0 pointer-events-none"
          style={{ width: '100%', height: '100%', overflow: 'visible' }}
        >
          {/* Roads: lines between edge vertices */}
          {ownedRoads.map((road, i) => {
            const tileKey = road.roadKey?.tileKey;
            const side: string = road.roadKey?.hexSide;
            if (!tileKey || !side || side === 'None') return null;

            const pair = SIDE_TO_VERTEX_PAIR[side];
            if (!pair) return null;

            const coord = cubicCoord(tileKey.q, tileKey.r);
            const v1 = getVertexPosition(coord, pair[0], hexSize, origin);
            const v2 = getVertexPosition(coord, pair[1], hexSize, origin);
            const colors = getColor(road.ownerId);

            return (
              <line
                key={`road-${i}`}
                x1={v1.x}
                y1={v1.y}
                x2={v2.x}
                y2={v2.y}
                stroke={colors.primary}
                strokeWidth={Math.max(2, hexSize * 0.08)}
                strokeLinecap="round"
              />
            );
          })}

          {/* Buildings: circles for settlements, rounded rects for cities */}
          {ownedBuildings.map((b, i) => {
            const hexCoords = b.buildingKey?.hexCoordinates;
            const position: string = b.buildingKey?.position;
            if (!hexCoords || !position || position === 'None') return null;

            const coord = cubicCoord(hexCoords.q, hexCoords.r);
            const pos = getVertexPosition(coord, position as GeometryHexPosition, hexSize, origin);
            const colors = getColor(b.ownerId);
            const r = b.buildingState === 'City' ? hexSize * 0.16 : hexSize * 0.12;

            return b.buildingState === 'City' ? (
              <rect
                key={`building-${i}`}
                x={pos.x - r}
                y={pos.y - r}
                width={r * 2}
                height={r * 2}
                fill={colors.primary}
                stroke={colors.foreground}
                strokeWidth={1.5}
                rx={2}
              />
            ) : (
              <circle
                key={`building-${i}`}
                cx={pos.x}
                cy={pos.y}
                r={r}
                fill={colors.primary}
                stroke={colors.foreground}
                strokeWidth={1.5}
              />
            );
          })}
        </svg>
      );
    },
    [ownedBuildings, ownedRoads, getColor]
  );

  if (!gameModel || tileItems.length === 0) return null;

  return (
    <div
      className="w-full h-full"
      onClick={onClick}
      style={{ cursor: onClick ? 'pointer' : 'default' }}
    >
      <HexGrid hexSize={50} items={tileItems} gap={2} fitToParent overlay={renderOverlay} />
    </div>
  );
}
