'use client';

import React, { memo } from 'react';
import { motion, AnimatePresence } from 'framer-motion';
import type { TileModel } from '@/types/generated/models/tile-model';
import { getResourceTileImage, getResourceCardImage } from '@/lib/constants/board-assets';
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
  /** Whether this tile is dimmed (non-matching roll number) */
  isDimmed?: boolean;
  /** Click handler for tile interactions */
  onClick?: () => void;
  /** Right-click handler for tile interactions (e.g., robber placement) */
  onRightClick?: (e: React.MouseEvent) => void;
  /** Tile index to display (1-based, for MustMoveRobber state) */
  tileIndex?: number;
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
  isDimmed = false,
  onClick,
  onRightClick,
  tileIndex,
}: GameTileProps) {
  const { number, resourceTileType, temporarilyGold } = tile;
  // When temporarily gold, show gold mine background instead of original resource
  const displayResource = temporarilyGold ? 'GoldMine' : resourceTileType;
  const imageUrl = getResourceTileImage(displayResource);
  const originalImageUrl = getResourceCardImage(resourceTileType);
  const isDesert = resourceTileType === 'Desert';

  // Inner hex scale: InnerHexSize / HexSize = 91/100 = 0.91 (from Blazor)
  const innerHexScale = 0.91;

  // Number token offset: 40px up at HexSize=100, HexHeight≈173
  // As percentage: 40/173 ≈ 23% up from center
  // So token center is at approximately 27% from top
  const numberTokenTop = '27%';

  // Handle right-click with context menu prevention
  const handleContextMenu = onRightClick
    ? (e: React.MouseEvent) => {
        e.preventDefault();
        onRightClick(e);
      }
    : undefined;

  return (
    <div
      className="absolute inset-0 cursor-pointer hover:scale-[1.02]"
      style={{
        opacity: isDimmed ? 0.5 : 1,
        transition: 'opacity 0.3s ease, transform 0.15s ease',
      }}
      onClick={onClick}
      onContextMenu={handleContextMenu}
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
      {/* Flip animation container */}
      <motion.div
        className="absolute inset-0"
        style={{
          transformStyle: 'preserve-3d',
          perspective: '1000px',
        }}
        animate={{ rotateY: temporarilyGold ? 180 : 0 }}
        transition={{ duration: 0.5, ease: 'easeInOut' }}
      >
        {/* Front face - original resource */}
        <div
          className="absolute inset-0 hex-clip-flat"
          style={{
            transform: `scale(${isHighlighted ? innerHexScale - 0.02 : innerHexScale})`,
            backgroundImage: `url(${getResourceTileImage(resourceTileType)})`,
            backgroundSize: 'cover',
            backgroundPosition: 'center',
            backfaceVisibility: 'hidden',
          }}
        />
        {/* Back face - gold mine (pre-rotated) */}
        <div
          className="absolute inset-0 hex-clip-flat"
          style={{
            transform: `scale(${isHighlighted ? innerHexScale - 0.02 : innerHexScale}) rotateY(180deg)`,
            backgroundImage: `url(${getResourceTileImage('GoldMine')})`,
            backgroundSize: 'cover',
            backgroundPosition: 'center',
            backfaceVisibility: 'hidden',
          }}
        />
      </motion.div>

      {/* Number token (not shown for desert) - positioned above center */}
      {!isDesert && number > 0 && (
        <div
          className="absolute pointer-events-none"
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

      {/* Gold indicator - shows original resource when temporarily gold */}
      {/* Blazor uses 40x60px card (2:3 aspect ratio), positioned below number token */}
      <AnimatePresence>
        {temporarilyGold && !isDesert && (
          <motion.div
            className="absolute rounded overflow-hidden shadow-lg pointer-events-none"
            style={{
              width: '23%', // 40px at HexSize=100, width ~174px = 23%
              aspectRatio: '2 / 3', // Matches Blazor 40x60 card
              top: '55%',
              left: '50%',
              x: '-50%',
            }}
            initial={{ opacity: 0, scale: 0.6, y: 10 }}
            animate={{ opacity: 1, scale: 1, y: 0 }}
            exit={{ opacity: 0, scale: 0.6, y: 10 }}
            transition={{ delay: 0.3, duration: 0.3, ease: 'easeOut' }}
          >
            <img
              src={originalImageUrl}
              alt={`Original: ${resourceTileType}`}
              className="w-full h-full object-cover"
            />
          </motion.div>
        )}
      </AnimatePresence>

      {/* Tile index overlay (for MustMoveRobber state) */}
      {tileIndex !== undefined && (
        <div
          className="absolute pointer-events-none"
          style={{
            bottom: '15%',
            left: '50%',
            transform: 'translateX(-50%)',
          }}
        >
          <div
            className="flex items-center justify-center font-bold rounded-md px-2 py-1"
            style={{
              backgroundColor: 'rgba(0, 0, 0, 0.7)',
              color: 'white',
              fontSize: '1.2em',
              minWidth: '1.8em',
              textAlign: 'center',
            }}
          >
            {tileIndex}
          </div>
        </div>
      )}
    </div>
  );
});

export default GameTile;
