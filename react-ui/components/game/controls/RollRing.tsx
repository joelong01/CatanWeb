'use client';

/**
 * RollRing - 11 hex buttons for roll statistics (2-12).
 *
 * Features:
 * - 4-3-4 column layout (square-ish shape) using LAYOUTS.COLUMNS_4_3_4
 * - Shows count and percentage for each roll
 * - Uses NumberToken component for consistent styling with board tiles
 * - Player-colored gradients
 */

import { memo, useState, useMemo } from 'react';
import { HexGrid, type HexGridItem, LAYOUTS } from '@/components/hex-grid';
import { NumberToken } from '@/components/game/tiles/NumberToken';
import type { PlayerColorsWithGradient } from '@/lib/utils/playerColors';
import type { RollStats } from '@/lib/extensions';

// Re-export for convenience
export type { RollStats };

// ============================================================================
// Types
// ============================================================================

export interface RollRingProps {
  /** Roll statistics for each number 2-12 */
  rollStats: Record<number, RollStats>;
  /** Callback when a roll button is clicked */
  onRollClick?: (roll: number) => void;
  /** Player colors for styling */
  colors?: PlayerColorsWithGradient;
  /**
   * Whether rolling is currently enabled. Defaults to true.
   * When false, tiles show a muted background and a not-allowed cursor, and
   * clicks are ignored. Stats (count/percentage/number) stay fully legible.
   */
  rollsEnabled?: boolean;
}

// ============================================================================
// RollHexContent - Individual roll button
// ============================================================================

interface RollHexContentProps {
  rollNumber: number;
  count: number;
  percentage: number;
  colors?: PlayerColorsWithGradient;
  /** When true, the tile is muted, non-interactive, and shows not-allowed. */
  disabled?: boolean;
}

const RollHexContent = memo(function RollHexContent({
  rollNumber,
  count,
  percentage,
  colors,
  disabled = false,
}: RollHexContentProps) {
  const [isHovered, setIsHovered] = useState(false);
  const [isPressed, setIsPressed] = useState(false);

  const foreground = colors?.foreground || '#ffffff';

  // Disabled: swap to the muted resting fill, drop the hover border, and hold
  // the resting scale so the tile no longer reacts to the mouse. Only the
  // background/border/cursor change — the stat layers below are untouched.
  const gradient = disabled
    ? 'var(--hex-content-gradient-disabled)'
    : colors?.cssGradient || 'var(--hex-content-gradient)';
  const borderColor = disabled
    ? 'var(--hex-disabled-border)'
    : isHovered
      ? 'var(--hex-border-hover)'
      : 'var(--hex-border-idle)';

  // Button scale: normal 0.96, hover 0.94, pressed 0.90 (frozen when disabled)
  const scale = disabled ? 0.96 : isPressed ? 0.9 : isHovered ? 0.94 : 0.96;

  return (
    <div
      className={`absolute inset-0 ${disabled ? 'cursor-not-allowed' : 'cursor-pointer'}`}
      onMouseEnter={() => !disabled && setIsHovered(true)}
      onMouseLeave={() => {
        setIsHovered(false);
        setIsPressed(false);
      }}
      onMouseDown={() => !disabled && setIsPressed(true)}
      onMouseUp={() => setIsPressed(false)}
      onTouchStart={() => !disabled && setIsPressed(true)}
      onTouchEnd={() => setIsPressed(false)}
    >
      {/* Outer border */}
      <div
        className="absolute inset-0 hex-clip-flat transition-colors duration-150"
        style={{ background: borderColor }}
      />
      {/* Inner content */}
      <div
        className="absolute inset-0 hex-clip-flat transition-all duration-150"
        style={{
          transform: `scale(${scale})`,
          background: gradient,
        }}
      >
        {/* Count - centered in top third */}
        <span
          className="absolute top-[3%] left-1/2 -translate-x-1/2 text-sm font-bold leading-none"
          style={{ color: foreground }}
        >
          {count}
        </span>

        {/* Number token - centered */}
        <div className="absolute top-1/2 left-1/2 -translate-x-1/2 -translate-y-1/2 w-9 aspect-square">
          <NumberToken number={rollNumber} className="w-full h-full" borderColor="transparent" />
        </div>

        {/* Percentage - centered in bottom third */}
        <span
          className="absolute bottom-[3%] left-1/2 -translate-x-1/2 text-xs leading-none opacity-80"
          style={{ color: foreground }}
        >
          {percentage}%
        </span>
      </div>
    </div>
  );
});

// ============================================================================
// RollRing - Main component
// ============================================================================

/**
 * RollRing - 11 hex buttons for roll numbers 2-12
 * Arranged in 4-3-4 column pattern (compact square shape)
 */
export const RollRing = memo(function RollRing({
  rollStats,
  onRollClick,
  colors,
  rollsEnabled = true,
}: RollRingProps): React.ReactElement {
  // Roll numbers 2-12 mapped to 4-3-4 column layout
  // Visual layout (probabilities match across rows):
  //  2 (1pip)    6 (5pips)   12 (1pip)
  //  3 (2pips)               11 (2pips)
  //  4 (3pips)   7 (6pips)   10 (3pips)
  //  5 (4pips)   8 (5pips)    9 (4pips)
  const rollNumbers = [2, 3, 4, 5, 6, 7, 8, 12, 11, 10, 9];

  // Get coordinates from the layout utility (memoized since it's static)
  const coords = useMemo(() => LAYOUTS.COLUMNS_4_3_4(), []);

  const items: HexGridItem[] = rollNumbers.map((roll, index) => {
    const coord = coords[index];
    const stats = rollStats[roll] || { count: 0, percentage: 0 };
    return {
      id: `roll-${roll}`,
      coord,
      // disabled removes the HexTile's onClick and applies cursor-not-allowed on
      // the tile wrapper; the closure below is a redundant safety gate.
      disabled: !rollsEnabled,
      content: (
        <RollHexContent
          rollNumber={roll}
          count={stats.count}
          percentage={stats.percentage}
          colors={colors}
          disabled={!rollsEnabled}
        />
      ),
      onClick: () => {
        if (rollsEnabled) onRollClick?.(roll);
      },
    };
  });

  return (
    <div className="w-full h-full">
      <HexGrid
        hexSize={38}
        items={items}
        gap={1}
        borderColor="transparent"
        fitToParent={true}
        fitPadding={8}
      />
    </div>
  );
});

export default RollRing;
