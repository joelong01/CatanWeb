'use client';

import React, { memo } from 'react';
import type { TileModel } from '@/types/generated/models/tile-model';
import { getResourceTileImage } from '@/lib/constants/board-assets';
import { NumberToken } from './NumberToken';

/**
 * Props for GameTile component
 */
export interface GameTileProps {
  /** The tile model data */
  tile: TileModel;
  /** Hex size (circumradius) in pixels */
  hexSize: number;
  /** Whether this tile is highlighted (e.g., during roll) */
  isHighlighted?: boolean;
  /** Click handler for tile interactions */
  onClick?: () => void;
}

/**
 * GameTile - Renders a single board tile with resource background and number token.
 *
 * Layout (matches Blazor TileSvgRenderer):
 * - Outer hex: Maple wood texture border
 * - Inner hex: Resource image (91% scale to show border)
 * - Number token positioned ABOVE center (40px at HexSize=100 ≈ 27% from top)
 * - Highlight glow when tile is active
 * - Room at bottom for gold indicator
 *
 * Uses hex-clip-flat CSS class for hexagonal clipping.
 */
export const GameTile = memo(function GameTile({
  tile,
  hexSize: _hexSize, // Keep prop for API compatibility but use percentages internally
  isHighlighted = false,
  onClick,
}: GameTileProps) {
  const { number, resourceTileType } = tile;
  const imageUrl = getResourceTileImage(resourceTileType);
  const isDesert = resourceTileType === 'Desert';

  // Inner hex scale: InnerHexSize / HexSize = 91/100 = 0.91 (from Blazor)
  const innerHexScale = 0.91;

  // Number token offset: 40px up at HexSize=100, HexHeight≈173
  // As percentage: 40/173 ≈ 23% up from center
  // So token center is at approximately 27% from top
  const numberTokenTop = '27%';

  return (
    <div
      className="absolute inset-0 cursor-pointer transition-transform duration-150 hover:scale-[1.02]"
      onClick={onClick}
    >
      {/* Outer hex border - maple wood texture */}
      <div
        className="absolute inset-0 hex-clip-flat"
        style={{
          backgroundImage: 'url(/themes/base/backgrounds/maple.jpg)',
          backgroundSize: 'cover',
          backgroundPosition: 'center',
        }}
      />

      {/* Highlight border (yellow glow for robber placement, etc.) */}
      {isHighlighted && (
        <div
          className="absolute inset-0 hex-clip-flat pointer-events-none"
          style={{
            background: '#FFD700', // Gold/yellow highlight
          }}
        />
      )}

      {/* Inner hex - resource background (91% scale for border effect) */}
      <div
        className="absolute inset-0 hex-clip-flat"
        style={{
          transform: `scale(${isHighlighted ? innerHexScale - 0.02 : innerHexScale})`,
          backgroundImage: `url(${imageUrl})`,
          backgroundSize: 'cover',
          backgroundPosition: 'center',
        }}
      />

      {/* Number token (not shown for desert) - positioned above center */}
      {!isDesert && number > 0 && (
        <div
          className="absolute"
          style={{
            width: '45%',
            height: '45%',
            top: numberTokenTop,
            left: '50%',
            transform: 'translate(-50%, -50%)',
          }}
        >
          <NumberToken number={number} className="w-full h-full" />
        </div>
      )}
    </div>
  );
});

export default GameTile;
