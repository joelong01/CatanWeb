'use client';

/**
 * ActionCluster - Game action controls in a 7-hex cluster with 3D flip animation.
 *
 * Features:
 * - Center: Next button (primary action)
 * - Around: Undo, Redo, Road, Settlement, City, DevCard
 * - 3D flip animation when disabled (shows card back with stats)
 * - Purchase count badges at upper-left vertex
 * - Player-colored gradients
 */

import { memo, useState, type ReactNode } from 'react';
import { FontAwesomeIcon } from '@fortawesome/react-fontawesome';
import { faRotateLeft, faRotateRight, faReceipt } from '@fortawesome/free-solid-svg-icons';
import type { IconDefinition } from '@fortawesome/fontawesome-svg-core';
import { HexGrid, type HexGridItem, type HexCoordinate } from '@/components/hex-grid';
import { CatanGlyph } from '@/lib/constants/catanGlyphs';
import type { PlayerColorsWithGradient } from '@/lib/utils/playerColors';

// ============================================================================
// Types
// ============================================================================

export interface PurchaseStats {
  roads?: { bought: number; available: number };
  settlements?: { bought: number; available: number };
  cities?: { bought: number; available: number };
  devCards?: { bought: number; available: number };
}

export interface EnabledButtons {
  next?: boolean;
  undo?: boolean;
  redo?: boolean;
  road?: boolean;
  settlement?: boolean;
  city?: boolean;
  devCard?: boolean;
}

export interface ActionClusterProps {
  /** Player colors for styling */
  colors?: PlayerColorsWithGradient;
  /** Current game state message */
  gameState?: string;
  /** Callback when an action button is clicked */
  onAction?: (action: string) => void;
  /** Stats for disabled buttons: bought/available counts */
  purchaseStats?: PurchaseStats;
  /** Which buttons are enabled */
  enabledButtons?: EnabledButtons;
}

// ============================================================================
// Constants
// ============================================================================

/** Standard 7-hex cluster: center + 6 neighbors */
const CLUSTER_7: Record<string, HexCoordinate> = {
  center: { q: 0, r: 0, s: 0 },
  north: { q: 0, r: -1, s: 1 },
  northEast: { q: 1, r: -1, s: 0 },
  southEast: { q: 1, r: 0, s: -1 },
  south: { q: 0, r: 1, s: -1 },
  southWest: { q: -1, r: 1, s: 0 },
  northWest: { q: -1, r: 0, s: 1 },
};

// ============================================================================
// ActionHexContent - Individual hex button with 3D flip
// ============================================================================

interface ActionHexContentProps {
  /** Text glyph to display (Catan font or regular text) */
  glyph?: string;
  /** FontAwesome icon to display (alternative to glyph) */
  faIcon?: IconDefinition;
  label?: string;
  isEnabled?: boolean;
  isPrimary?: boolean;
  /** Stats to show on back when flipped (e.g., "2/15") */
  backStats?: string;
  /** Count to show in top-left corner (e.g., purchased count) */
  count?: number;
  /** Player colors for styling */
  colors?: PlayerColorsWithGradient;
  /** Use Catan font for glyph (default true for Catan glyphs) */
  useCatanFont?: boolean;
}

const ActionHexContent = memo(function ActionHexContent({
  glyph,
  faIcon,
  label,
  isEnabled = true,
  isPrimary = false,
  backStats,
  count,
  colors,
  useCatanFont = true,
}: ActionHexContentProps) {
  const [isHovered, setIsHovered] = useState(false);
  const [isPressed, setIsPressed] = useState(false);

  const gradient = colors?.cssGradient || 'var(--hex-content-gradient)';
  const foreground = colors?.foreground || '#ffffff';

  // All face-up buttons use player gradient
  const frontBg = gradient;

  // Scale based on interaction state (only when enabled)
  const innerScale = isEnabled && isPressed ? 0.90 : isEnabled && isHovered ? 0.93 : 0.96;
  const borderColor = isEnabled && isHovered ? 'var(--hex-border-hover)' : 'var(--hex-border-idle)';

  // Render the icon/glyph content
  const renderIcon = (size: 'large' | 'small', pressed = false): ReactNode => {
    const sizeClass = size === 'large'
      ? (isPrimary ? 'text-3xl' : 'text-2xl')
      : 'text-base';
    const color = foreground;

    if (faIcon) {
      return (
        <FontAwesomeIcon
          icon={faIcon}
          className={sizeClass}
          style={{
            color,
            filter: size === 'large' ? 'drop-shadow(1px 1px 2px rgba(0,0,0,0.5))' : undefined,
            transition: 'transform 150ms',
            transform: pressed ? 'scale(0.85)' : 'scale(1)',
          }}
        />
      );
    }

    return (
      <span
        className={`${useCatanFont ? 'font-catan' : ''} ${sizeClass}`}
        style={{
          color,
          textShadow: size === 'large' ? '1px 1px 2px rgba(0,0,0,0.5)' : undefined,
          transition: 'transform 150ms',
          transform: pressed ? 'scale(0.85)' : 'scale(1)',
          display: 'inline-block',
        }}
      >
        {glyph}
      </span>
    );
  };

  return (
    <div
      className="absolute inset-0"
      style={{ perspective: '500px' }}
      onMouseEnter={() => isEnabled && setIsHovered(true)}
      onMouseLeave={() => { setIsHovered(false); setIsPressed(false); }}
      onMouseDown={() => isEnabled && setIsPressed(true)}
      onMouseUp={() => setIsPressed(false)}
      onTouchStart={() => isEnabled && setIsPressed(true)}
      onTouchEnd={() => setIsPressed(false)}
    >
      {/* Flip container */}
      <div
        className="relative w-full h-full transition-transform duration-500"
        style={{
          transformStyle: 'preserve-3d',
          transform: isEnabled ? 'rotateY(0deg)' : 'rotateY(180deg)',
        }}
      >
        {/* Front face (enabled state) */}
        <div
          className="absolute inset-0"
          style={{ backfaceVisibility: 'hidden' }}
        >
          {/* Outer border */}
          <div
            className="absolute inset-0 hex-clip-flat transition-colors duration-150"
            style={{ background: borderColor }}
          />
          {/* Inner content */}
          <div
            className="absolute inset-0 hex-clip-flat flex flex-col items-center justify-center transition-all duration-150"
            style={{
              transform: `scale(${innerScale})`,
              background: frontBg,
            }}
          >
            {/* Count badge - positioned along line from upper-left vertex to center
                Flat-top hex: upper-left vertex at (25%, 13.4%), center at (50%, 50%)
                At t=0.2 along line: x = 25 + 0.2*25 = 30%, y = 13.4 + 0.2*36.6 = 20.7% */}
            {count !== undefined && (
              <span
                className="absolute text-sm font-bold"
                style={{
                  top: '21%',
                  left: '30%',
                  transform: 'translate(-50%, -50%)',
                  color: foreground,
                  textShadow: '1px 1px 2px rgba(0,0,0,0.5)',
                }}
              >
                {count}
              </span>
            )}
            {renderIcon('large', isPressed)}
            {label && (
              <span
                className="text-[8px] mt-0.5 uppercase tracking-wider transition-transform duration-150"
                style={{
                  color: foreground,
                  transform: isPressed ? 'scale(0.9)' : 'scale(1)',
                }}
              >
                {label}
              </span>
            )}
          </div>
        </div>

        {/* Back face (disabled/flipped state) - card back image */}
        <div
          className="absolute inset-0"
          style={{
            backfaceVisibility: 'hidden',
            transform: 'rotateY(180deg)',
          }}
        >
          {/* Card back with image */}
          <div
            className="absolute inset-0 hex-clip-flat flex flex-col items-center justify-center"
            style={{
              backgroundImage: 'url(/themes/base/resources/back.png)',
              backgroundSize: 'cover',
              backgroundPosition: 'center',
            }}
          >
            {/* Icon badge centered */}
            <div
              className="rounded-sm px-1.5 py-0.5"
              style={{ background: gradient }}
            >
              {renderIcon('small')}
            </div>
            {/* Stats below icon */}
            {backStats && (
              <span className="text-xs font-bold text-white mt-1 bg-black/70 px-1 rounded-sm">
                {backStats}
              </span>
            )}
          </div>
        </div>
      </div>
    </div>
  );
});

// ============================================================================
// ActionCluster - Main component
// ============================================================================

export const ActionCluster = memo(function ActionCluster({
  colors,
  gameState = 'Roll the Dice',
  onAction,
  purchaseStats,
  enabledButtons = { next: true },
}: ActionClusterProps): React.ReactElement {
  const handleAction = (action: string) => {
    onAction?.(action);
  };

  const formatStats = (stats?: { bought: number; available: number }) =>
    stats ? `${stats.bought}/${stats.available}` : undefined;

  const items: HexGridItem[] = [
    // Center: Next (primary action)
    {
      id: 'next',
      coord: CLUSTER_7.center,
      content: (
        <ActionHexContent
          glyph="➤"
          label="Next"
          isPrimary={true}
          isEnabled={enabledButtons.next !== false}
          colors={colors}
          useCatanFont={false}
        />
      ),
      onClick: () => handleAction('next'),
      disabled: enabledButtons.next === false,
    },
    // North: DevCard (receipt icon)
    {
      id: 'devcard',
      coord: CLUSTER_7.north,
      content: (
        <ActionHexContent
          faIcon={faReceipt}
          label="Dev"
          isEnabled={enabledButtons.devCard !== false}
          backStats={purchaseStats?.devCards?.bought.toString()}
          count={purchaseStats?.devCards?.bought}
          colors={colors}
        />
      ),
      onClick: () => handleAction('devcard'),
      disabled: enabledButtons.devCard === false,
    },
    // NE: Road
    {
      id: 'road',
      coord: CLUSTER_7.northEast,
      content: (
        <ActionHexContent
          glyph={CatanGlyph.Road}
          label="Road"
          isEnabled={enabledButtons.road !== false}
          backStats={formatStats(purchaseStats?.roads)}
          count={purchaseStats?.roads?.bought}
          colors={colors}
        />
      ),
      onClick: () => handleAction('road'),
      disabled: enabledButtons.road === false,
    },
    // SE: Settlement
    {
      id: 'settlement',
      coord: CLUSTER_7.southEast,
      content: (
        <ActionHexContent
          glyph={CatanGlyph.Settlement}
          label="Settle"
          isEnabled={enabledButtons.settlement !== false}
          backStats={formatStats(purchaseStats?.settlements)}
          count={purchaseStats?.settlements?.bought}
          colors={colors}
        />
      ),
      onClick: () => handleAction('settlement'),
      disabled: enabledButtons.settlement === false,
    },
    // South: City
    {
      id: 'city',
      coord: CLUSTER_7.south,
      content: (
        <ActionHexContent
          glyph={CatanGlyph.City}
          label="City"
          isEnabled={enabledButtons.city !== false}
          backStats={formatStats(purchaseStats?.cities)}
          count={purchaseStats?.cities?.bought}
          colors={colors}
        />
      ),
      onClick: () => handleAction('city'),
      disabled: enabledButtons.city === false,
    },
    // SW: Redo (FontAwesome icon)
    {
      id: 'redo',
      coord: CLUSTER_7.southWest,
      content: (
        <ActionHexContent
          faIcon={faRotateRight}
          label="Redo"
          isEnabled={enabledButtons.redo === true}
          colors={colors}
          useCatanFont={false}
        />
      ),
      onClick: () => handleAction('redo'),
      disabled: enabledButtons.redo !== true,
    },
    // NW: Undo (FontAwesome icon)
    {
      id: 'undo',
      coord: CLUSTER_7.northWest,
      content: (
        <ActionHexContent
          faIcon={faRotateLeft}
          label="Undo"
          isEnabled={enabledButtons.undo === true}
          colors={colors}
          useCatanFont={false}
        />
      ),
      onClick: () => handleAction('undo'),
      disabled: enabledButtons.undo !== true,
    },
  ];

  return (
    <div className="w-full h-full flex flex-col">
      <div className="flex-1 min-h-0">
        <HexGrid
          hexSize={45}
          items={items}
          gap={2}
          borderColor="transparent"
          fitToParent={true}
          fitPadding={0}
        />
      </div>
      {/* State message below cluster */}
      <div
        className="text-sm font-semibold text-center py-1 flex-shrink-0"
        style={{ color: colors?.foreground || '#fbbf24' }}
      >
        {gameState}
      </div>
    </div>
  );
});

export default ActionCluster;
