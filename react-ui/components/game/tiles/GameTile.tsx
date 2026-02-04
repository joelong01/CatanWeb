'use client';

import React, { memo } from 'react';
import { motion, AnimatePresence } from 'framer-motion';
import type { TileModel } from '@/types/generated/models/tile-model';
import { getResourceTileImage, getResourceCardImage } from '@/lib/constants/board-assets';
import { useAssetPath, useFontRendering, useTileFontConfig } from '@/lib/theme';
import { NumberToken } from './NumberToken';

/** Primary color per resource type for number token border ring. */
const RESOURCE_COLORS: Record<string, string> = {
  Wheat: '#CC7A00',
  Wood: '#228B22',
  Sheep: '#A68B6B',
  Brick: '#BF360C',
  Ore: '#4682B4',
  Desert: '#F59E0B',
  GoldMine: '#FFD700',
  TempGold: '#FFD700',
};

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
  const tokenBorderColor = RESOURCE_COLORS[resourceTileType] ?? 'white';

  // Theme-driven font rendering
  const isFontMode = useFontRendering();
  const baseFontConfig = useTileFontConfig(resourceTileType);
  const goldFontConfig = useTileFontConfig('TempGold');
  // Theme-resolved border fill (maple wood texture in base/classic)
  const borderFillPath = useAssetPath('BackgroundBorderFill');
  // Use font rendering when theme says "font" AND we have config for the base resource
  const useFont = isFontMode && !!baseFontConfig;
  // Whether the gold flip animation is possible (need both base and gold configs)
  const canFlipGold = useFont && !!goldFontConfig;

  // Inner hex scale: InnerHexSize / HexSize = 91/100 = 0.91 (from Blazor)
  const innerHexScale = 0.91;

  // Handle right-click with context menu prevention
  const handleContextMenu = onRightClick
    ? (e: React.MouseEvent) => {
        e.preventDefault();
        onRightClick(e);
      }
    : undefined;

  return (
    <div
      className={`absolute inset-0 cursor-pointer hover:scale-[1.02] ${isHighlighted ? 'tile-highlight-glow' : ''}`}
      style={{
        opacity: isDimmed ? 0.5 : 1,
        transition: 'opacity 0.3s ease, transform 0.15s ease',
      }}
      onClick={onClick}
      onContextMenu={handleContextMenu}
    >
      {/* Outer hex border */}
      {useFont ? (
        /* Font mode: solid border color from theme */
        <div
          className="absolute inset-0 hex-clip-flat"
          style={{ background: baseFontConfig!.borderColor }}
        />
      ) : (
        /* Image mode: maple texture border */
        <div
          className="absolute inset-0 hex-clip-flat"
          style={{
            backgroundImage: borderFillPath ? `url(${borderFillPath})` : undefined,
            backgroundSize: 'cover',
            backgroundPosition: 'center',
          }}
        />
      )}

      {/* Highlight wash — subtle white overlay to brighten the tile when active */}
      {isHighlighted && (
        <div
          className="absolute inset-0 hex-clip-flat pointer-events-none"
          style={{
            background: 'rgba(255, 255, 255, 0.2)',
          }}
        />
      )}

      {/* Tile content */}
      {useFont ? (
        /* Font mode: flip animation between base resource and gold glyphs.
           Glyph contains full tile design (border, pattern, center circle).
           viewBox is square (matches font em-square); container stretches to hex. */
        <motion.div
          className="absolute inset-0"
          style={{
            transformStyle: 'preserve-3d',
            perspective: '1000px',
          }}
          animate={{ rotateY: canFlipGold && temporarilyGold ? 180 : 0 }}
          transition={{ duration: 1.0, ease: 'easeInOut' }}
        >
          {/* Front face - base resource glyph on bgColor background */}
          <div className="absolute inset-0" style={{ backfaceVisibility: 'hidden' }}>
            <svg
              className="absolute inset-0 w-full h-full"
              viewBox="0 0 100 100"
              preserveAspectRatio="none"
            >
              <rect width="100" height="100" fill={baseFontConfig!.bgColor} />
              <text
                x="50"
                y="50"
                textAnchor="middle"
                dominantBaseline="central"
                style={{ fontFamily: 'var(--font-catan)' }}
                fontSize="100"
                fill={baseFontConfig!.color}
              >
                {baseFontConfig!.glyph}
              </text>
            </svg>
          </div>
          {/* Back face - gold glyph (pre-rotated 180°) */}
          {goldFontConfig && (
            <div
              className="absolute inset-0"
              style={{ backfaceVisibility: 'hidden', transform: 'rotateY(180deg)' }}
            >
              <svg
                className="absolute inset-0 w-full h-full"
                viewBox="0 0 100 100"
                preserveAspectRatio="none"
              >
                <rect width="100" height="100" fill={goldFontConfig.bgColor} />
                <text
                  x="50"
                  y="50"
                  textAnchor="middle"
                  dominantBaseline="central"
                  style={{ fontFamily: 'var(--font-catan)' }}
                  fontSize="100"
                  fill={goldFontConfig.color}
                >
                  {goldFontConfig.glyph}
                </text>
              </svg>
            </div>
          )}
        </motion.div>
      ) : (
        /* Image mode: flip animation with PNG backgrounds */
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
      )}

      {/* Gold indicator - shows original resource when temporarily gold */}
      {/* Rendered BEFORE number token so number stays on top in z-order */}
      <AnimatePresence>
        {temporarilyGold && !isDesert && (
          useFont && baseFontConfig ? (
            /* Font mode: hex-clipped glyph of original resource in bottom half.
               Same hex-clip-flat shape as NumberToken so the two stack cleanly. */
            <motion.div
              key="gold-indicator-font"
              className="absolute pointer-events-none"
              style={{
                width: '38%',
                height: '38%',
                left: '50%',
              }}
              initial={{ rotateX: -90, opacity: 0, top: '70%', x: '-50%', y: '-50%' }}
              animate={{ rotateX: 0, opacity: 1, top: '70%', x: '-50%', y: '-50%' }}
              exit={{ rotateX: 90, opacity: 0, transition: { duration: 0.2, delay: 0 } }}
              transition={{ delay: 2.0, duration: 1.0, ease: 'easeOut' }}
            >
              <div className="hex-clip-flat w-full h-full" style={{ backgroundColor: 'white' }}>
                <svg
                  className="w-full h-full"
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
                    fill={baseFontConfig.color}
                  >
                    {baseFontConfig.glyph}
                  </text>
                </svg>
              </div>
            </motion.div>
          ) : (
            /* Image mode: card rectangle with original resource image */
            <motion.div
              key="gold-indicator-image"
              className="absolute rounded overflow-hidden shadow-lg pointer-events-none"
              style={{
                width: '23%',
                aspectRatio: '2 / 3',
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
          )
        )}
      </AnimatePresence>

      {/* Number token (not shown for desert) - on top of gold indicator */}
      {!isDesert && number > 0 && (
        useFont ? (
          /* Font mode: animated position — centered normally, slides up when gold.
             Delay 0.5s on gold entry (after tile flip), instant on exit. */
          <motion.div
            className="absolute pointer-events-none"
            initial={false}
            animate={{
              top: canFlipGold && temporarilyGold ? '30%' : '50%',
              x: '-50%',
              y: '-50%',
            }}
            transition={
              canFlipGold && temporarilyGold
                ? { top: { duration: 1.0, delay: 1.0, ease: 'easeInOut' } }
                : { top: { duration: 0.3, ease: 'easeInOut' } }
            }
            style={{
              width: '38%',
              aspectRatio: '1',
              left: '50%',
            }}
          >
            <NumberToken number={number} className="w-full h-full" borderColor={tokenBorderColor} />
          </motion.div>
        ) : (
          /* Image mode: static position above center */
          <div
            className="absolute pointer-events-none"
            style={{
              width: '38%',
              aspectRatio: '1',
              top: '27%',
              left: '50%',
              transform: 'translate(-50%, -50%)',
            }}
          >
            <NumberToken number={number} className="w-full h-full" borderColor={tokenBorderColor} />
          </div>
        )
      )}

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
