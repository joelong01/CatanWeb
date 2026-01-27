'use client';

/**
 * RollRing - 11 hex buttons for roll statistics (2-12).
 *
 * Features:
 * - 3-4-3 column layout with 7 isolated at bottom-left
 * - Shows count and percentage for each roll
 * - Uses NumberToken component for consistent styling with board tiles
 * - Player-colored gradients
 */

import { memo, useState } from 'react';
import { HexGrid, type HexGridItem, type HexCoordinate } from '@/components/hex-grid';
import { NumberToken } from '@/components/game/tiles/NumberToken';
import type { PlayerColorsWithGradient } from '@/lib/utils/playerColors';

// ============================================================================
// Types
// ============================================================================

/** Roll stats for tracking */
export interface RollStats {
  count: number;
  percentage: number;
}

export interface RollRingProps {
  /** Roll statistics for each number 2-12 */
  rollStats: Record<number, RollStats>;
  /** Callback when a roll button is clicked */
  onRollClick?: (roll: number) => void;
  /** Player colors for styling */
  colors?: PlayerColorsWithGradient;
}

// ============================================================================
// RollHexContent - Individual roll button
// ============================================================================

interface RollHexContentProps {
  rollNumber: number;
  count: number;
  percentage: number;
  colors?: PlayerColorsWithGradient;
}

const RollHexContent = memo(function RollHexContent({
  rollNumber,
  count,
  percentage,
  colors,
}: RollHexContentProps) {
  const [isHovered, setIsHovered] = useState(false);
  const [isPressed, setIsPressed] = useState(false);

  const gradient = colors?.cssGradient || 'var(--hex-content-gradient)';
  const foreground = colors?.foreground || '#ffffff';
  const borderColor = isHovered
    ? 'var(--hex-border-hover)'
    : 'var(--hex-border-idle)';

  // Button scale: normal 0.96, hover 0.94, pressed 0.90
  const scale = isPressed ? 0.90 : isHovered ? 0.94 : 0.96;

  return (
    <div
      className="absolute inset-0 cursor-pointer"
      onMouseEnter={() => setIsHovered(true)}
      onMouseLeave={() => { setIsHovered(false); setIsPressed(false); }}
      onMouseDown={() => setIsPressed(true)}
      onMouseUp={() => setIsPressed(false)}
      onTouchStart={() => setIsPressed(true)}
      onTouchEnd={() => setIsPressed(false)}
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
          transform: `scale(${scale})`,
          background: gradient,
        }}
      >
        {/* Count at top */}
        <span
          className="text-[10px] font-bold"
          style={{ color: foreground }}
        >
          {count}
        </span>

        {/* Number token - using the same component as the board tiles */}
        <div className="w-10 h-10">
          <NumberToken number={rollNumber} className="w-full h-full" />
        </div>

        {/* Percentage at bottom */}
        <span
          className="text-[9px]"
          style={{ color: foreground, opacity: 0.8 }}
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
 * Arranged in 3 columns: 3-4-3 pattern with 7 isolated at bottom-left
 */
export const RollRing = memo(function RollRing({
  rollStats,
  onRollClick,
  colors,
}: RollRingProps): React.ReactElement {
  // Hex coordinates for 3-4-3 COLUMN layout with 7 isolated
  // Reading top-to-bottom within each column, left-to-right across columns
  // 7 is special (robber roll) so it's isolated at bottom-left edge
  //
  // Col0  Col1  Col2
  //        5    10
  //  2     6    11
  //  3     8    12
  //  4     9
  //  7 (bottom-left edge)
  //
  const rollCoords: { roll: number; coord: HexCoordinate }[] = [
    // Column 0 (q=0): 2, 3, 4 - shifted down 1 to align with middle column
    { roll: 2, coord: { q: 0, r: 1, s: -1 } },
    { roll: 3, coord: { q: 0, r: 2, s: -2 } },
    { roll: 4, coord: { q: 0, r: 3, s: -3 } },
    // Column 1 (q=1): 5, 6, 8, 9 (7 skipped)
    { roll: 5, coord: { q: 1, r: 0, s: -1 } },
    { roll: 6, coord: { q: 1, r: 1, s: -2 } },
    { roll: 8, coord: { q: 1, r: 2, s: -3 } },
    { roll: 9, coord: { q: 1, r: 3, s: -4 } },
    // Column 2 (q=2): 10, 11, 12
    { roll: 10, coord: { q: 2, r: 0, s: -2 } },
    { roll: 11, coord: { q: 2, r: 1, s: -3 } },
    { roll: 12, coord: { q: 2, r: 2, s: -4 } },
    // 7 isolated at bottom-left edge
    { roll: 7, coord: { q: -1, r: 4, s: -3 } },
  ];

  const items: HexGridItem[] = rollCoords.map(({ roll, coord }) => {
    const stats = rollStats[roll] || { count: 0, percentage: 0 };
    return {
      id: `roll-${roll}`,
      coord,
      content: (
        <RollHexContent
          rollNumber={roll}
          count={stats.count}
          percentage={stats.percentage}
          colors={colors}
        />
      ),
      onClick: () => onRollClick?.(roll),
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
