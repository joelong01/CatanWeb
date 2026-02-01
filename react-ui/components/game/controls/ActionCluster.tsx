'use client';

/**
 * ActionCluster - Game action controls in a 3x3 square grid with 3D flip animation.
 *
 * Layout (3 columns × 3 rows using LAYOUTS.SQUARE_3x3):
 *
 *    DevCard   Settlement    Undo
 *    City       [State]      Next
 *    Road       Soldier      Redo
 *
 * Features:
 * - Center hex: Game state message display
 * - 8 action/purchase buttons around it
 * - 3D flip animation when disabled (shows card back with stats)
 * - Purchase count badges at upper-left vertex
 * - Player-colored gradients
 *
 * Button logic (matches Blazor):
 * - Face-up (enabled) when: gameModel.entitlementPurchaseModel[entitlement].enabled === true
 * - Soldier shows available count (unspent Soldier entitlements from dev cards in hand)
 */

import { memo, useState, useMemo, useEffect, type ReactNode } from 'react';
import { FontAwesomeIcon } from '@fortawesome/react-fontawesome';
import { faRotateLeft, faRotateRight, faReceipt } from '@fortawesome/free-solid-svg-icons';
import type { IconDefinition } from '@fortawesome/fontawesome-svg-core';
import { HexGrid, type HexGridItem, LAYOUTS } from '@/components/hex-grid';
import { CatanGlyph } from '@/lib/constants/catanGlyphs';
import type { PlayerColorsWithGradient } from '@/lib/utils/playerColors';

// ============================================================================
// Types
// ============================================================================

export interface EntityStats {
  unspent: number;
  spent: number;
  max: number;
}

export interface PurchaseStats {
  roads?: EntityStats;
  settlements?: EntityStats;
  cities?: EntityStats;
  devCards?: { spent: number };
  soldier?: { played: number; unspent: number };
}

export interface EnabledButtons {
  next?: boolean;
  undo?: boolean;
  redo?: boolean;
  soldier?: boolean;
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

/**
 * Button order for 3x3 square grid layout (maps to LAYOUTS.SQUARE_3x3 indices)
 *
 * Visual layout:
 *    Col 0      Col 1      Col 2
 *   DevCard   Settlement   Undo      (row 0)
 *   City       [State]     Next      (row 1)
 *   Road       Soldier     Redo      (row 2)
 *
 * SQUARE_3x3 produces coords column-by-column:
 * [0]=(0,0) [1]=(0,1) [2]=(0,2) [3]=(1,0) [4]=(1,1) [5]=(1,2) [6]=(2,-1) [7]=(2,0) [8]=(2,1)
 */
const BUTTON_ORDER = ['devcard', 'city', 'road', 'settlement', 'state', 'soldier', 'undo', 'next', 'redo'] as const;

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
  /** Tooltip text shown on hover */
  tooltip?: string;
  /** Make the glyph bold */
  bold?: boolean;
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
  tooltip,
  bold = false,
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
          className={`${sizeClass} transition-transform duration-150`}
          style={{
            color,
            filter: size === 'large' ? 'drop-shadow(1px 1px 2px rgba(0,0,0,0.5))' : undefined,
            transform: pressed ? 'scale(0.85)' : 'scale(1)',
          }}
        />
      );
    }

    return (
      <span
        className={`${useCatanFont ? 'font-catan' : ''} ${sizeClass} ${bold ? 'font-bold' : ''} inline-block transition-transform duration-150`}
        style={{
          color,
          textShadow: size === 'large' ? '1px 1px 2px rgba(0,0,0,0.5)' : undefined,
          transform: pressed ? 'scale(0.85)' : 'scale(1)',
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
      title={tooltip}
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
            className="absolute inset-0 hex-clip-flat flex flex-col items-center justify-center bg-[url('/themes/base/resources/back.png')] bg-cover bg-center"
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

  // Add keyboard handler for Enter key to trigger Next button
  useEffect(() => {
    const handleKeyDown = (e: KeyboardEvent) => {
      // Only handle Enter key
      if (e.key !== 'Enter') return;

      // Don't trigger if user is typing in an input field
      const target = e.target as HTMLElement;
      if (target.tagName === 'INPUT' || target.tagName === 'TEXTAREA' || target.isContentEditable) {
        return;
      }

      // Trigger Next action if enabled
      if (enabledButtons.next !== false) {
        e.preventDefault();
        handleAction('next');
      }
    };

    window.addEventListener('keydown', handleKeyDown);
    return () => window.removeEventListener('keydown', handleKeyDown);
  }, [enabledButtons.next, onAction]);

  const formatStats = (stats?: EntityStats) =>
    stats ? `${stats.spent} of ${stats.max}` : undefined;

  // Get 3x3 square layout coordinates
  const coords = useMemo(() => LAYOUTS.SQUARE_3x3(), []);

  // Button configurations mapped to BUTTON_ORDER
  const buttonConfigs: Record<string, Omit<HexGridItem, 'coord'>> = {
    devcard: {
      id: 'devcard',
      content: (
        <ActionHexContent
          faIcon={faReceipt}
          label="Dev"
          tooltip="Buy Development Card"
          isEnabled={enabledButtons.devCard !== false}
          backStats={purchaseStats?.devCards ? `${purchaseStats.devCards.spent}` : undefined}
          count={purchaseStats?.devCards?.spent}
          colors={colors}
        />
      ),
      onClick: () => handleAction('devcard'),
      disabled: enabledButtons.devCard === false,
    },
    city: {
      id: 'city',
      content: (
        <ActionHexContent
          glyph={CatanGlyph.City}
          label="City"
          tooltip="Buy City"
          isEnabled={enabledButtons.city !== false}
          backStats={formatStats(purchaseStats?.cities)}
          count={purchaseStats?.cities?.unspent}
          colors={colors}
        />
      ),
      onClick: () => handleAction('city'),
      disabled: enabledButtons.city === false,
    },
    road: {
      id: 'road',
      content: (
        <ActionHexContent
          glyph={CatanGlyph.Road}
          label="Road"
          tooltip="Buy Road"
          isEnabled={enabledButtons.road !== false}
          backStats={formatStats(purchaseStats?.roads)}
          count={purchaseStats?.roads?.unspent}
          colors={colors}
          bold={true}
        />
      ),
      onClick: () => handleAction('road'),
      disabled: enabledButtons.road === false,
    },
    settlement: {
      id: 'settlement',
      content: (
        <ActionHexContent
          glyph={CatanGlyph.Settlement}
          label="Settle"
          tooltip="Buy Settlement"
          isEnabled={enabledButtons.settlement !== false}
          backStats={formatStats(purchaseStats?.settlements)}
          count={purchaseStats?.settlements?.unspent}
          colors={colors}
        />
      ),
      onClick: () => handleAction('settlement'),
      disabled: enabledButtons.settlement === false,
    },
    state: {
      id: 'state',
      content: (
        <div
          className="absolute inset-0 hex-clip-flat flex items-center justify-center"
          style={{
            background: colors?.cssGradient || 'var(--hex-content-gradient)',
            transform: 'scale(0.96)',
          }}
        >
          <span
            className="text-[9px] font-semibold text-center px-1 leading-tight"
            style={{ color: colors?.foreground || '#ffffff' }}
          >
            {gameState}
          </span>
        </div>
      ),
    },
    soldier: {
      id: 'soldier',
      content: (
        <ActionHexContent
          glyph={CatanGlyph.Soldier}
          label="Soldier"
          tooltip="Play Soldier Card"
          isPrimary
          isEnabled={enabledButtons.soldier === true}
          backStats={purchaseStats?.soldier?.played.toString()}
          count={purchaseStats?.soldier?.unspent}
          colors={colors}
        />
      ),
      onClick: () => handleAction('soldier'),
      disabled: enabledButtons.soldier !== true,
    },
    undo: {
      id: 'undo',
      content: (
        <ActionHexContent
          faIcon={faRotateLeft}
          label="Undo"
          tooltip="Undo Last Action"
          isEnabled={enabledButtons.undo === true}
          colors={colors}
          useCatanFont={false}
        />
      ),
      onClick: () => handleAction('undo'),
      disabled: enabledButtons.undo !== true,
    },
    next: {
      id: 'next',
      content: (
        <ActionHexContent
          glyph="➤"
          label="Next"
          tooltip="End Turn"
          isPrimary={true}
          isEnabled={enabledButtons.next !== false}
          colors={colors}
          useCatanFont={false}
        />
      ),
      onClick: () => handleAction('next'),
      disabled: enabledButtons.next === false,
    },
    redo: {
      id: 'redo',
      content: (
        <ActionHexContent
          faIcon={faRotateRight}
          label="Redo"
          tooltip="Redo Action"
          isEnabled={enabledButtons.redo === true}
          colors={colors}
          useCatanFont={false}
        />
      ),
      onClick: () => handleAction('redo'),
      disabled: enabledButtons.redo !== true,
    },
  };

  // Map button order to coordinates
  const items: HexGridItem[] = BUTTON_ORDER.map((buttonId, index) => ({
    ...buttonConfigs[buttonId],
    coord: coords[index],
  }));

  return (
    <div className="w-full h-full">
      <HexGrid
        hexSize={45}
        items={items}
        gap={2}
        borderColor="transparent"
        fitToParent={true}
        fitPadding={0}
      />
    </div>
  );
});

export default ActionCluster;
