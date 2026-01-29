'use client';

/**
 * DiceCluster - Two 7-hex clusters for dice selection using HexGrid.
 *
 * Each cluster shows values 1-6 around the edge (as dice face glyphs) with
 * a confirm button in center. Players select a value on each die, then
 * click either center hex to confirm the roll.
 * 
 * When both dice are selected, the center hex displays the roll sum along
 * with the count and percentage from rollStats (matching Blazor behavior).
 */

import { useState, useCallback, memo } from 'react';
import { HexGrid, type HexGridItem, type HexCoordinate } from '@/components/hex-grid';
import type { PlayerColorsWithGradient } from '@/lib/utils/playerColors';
import type { RollStats } from '@/lib/extensions';

// ============================================================================
// Types
// ============================================================================

export interface DiceClusterProps {
  /** Whether dice rolling is enabled */
  enabled: boolean;
  /** Whether currently waiting for server response */
  isRolling: boolean;
  /** Callback when roll is confirmed */
  onRoll: (die1: number, die2: number) => void;
  /** Player colors for styling */
  colors?: PlayerColorsWithGradient;
  /** Roll statistics for each sum 2-12, used to display count/percentage for selected roll */
  rollStats?: Record<number, RollStats>;
}

// ============================================================================
// Constants
// ============================================================================

/** Unicode dice face glyphs */
const DICE_GLYPHS = ['', '⚀', '⚁', '⚂', '⚃', '⚄', '⚅'];

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
// DiceHexContent - Individual die value hex
// ============================================================================

interface DiceHexContentProps {
  value: number;
  isSelected: boolean;
  colors?: PlayerColorsWithGradient;
}

const DiceHexContent = memo(function DiceHexContent({
  value,
  isSelected,
  colors,
}: DiceHexContentProps) {
  const [isHovered, setIsHovered] = useState(false);
  const [isPressed, setIsPressed] = useState(false);

  const gradient = colors?.cssGradient || 'var(--hex-content-gradient)';
  const foreground = colors?.foreground || '#ffffff';
  const primary = colors?.primary || '#e53935';

  // Unselected: player gradient background, foreground text
  // Selected: foreground as background, primary as text (inverted)
  const bgStyle = isSelected
    ? { background: foreground }
    : { background: gradient };

  const textColor = isSelected ? primary : foreground;

  // Get dice glyph for value 1-6
  const glyph = DICE_GLYPHS[value] || '';

  // Scale based on interaction state
  const innerScale = isPressed ? 0.85 : isHovered ? 0.88 : 0.91;
  const borderColor = isSelected || isHovered ? 'var(--hex-border-hover)' : 'var(--hex-border-idle)';

  return (
    <div
      className="absolute inset-0"
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
        className="absolute inset-0 hex-clip-flat flex items-center justify-center transition-all duration-150"
        style={{
          transform: `scale(${innerScale})`,
          ...bgStyle,
        }}
      >
        <span
          style={{
            color: textColor,
            fontSize: '3em',
            textShadow: isSelected ? 'none' : '2px 2px 6px rgba(0,0,0,0.6)',
            fontWeight: 'normal',
            transition: 'transform 150ms',
            transform: isPressed ? 'scale(0.9)' : 'scale(1)',
          }}
        >
          {glyph}
        </span>
      </div>
    </div>
  );
});

// ============================================================================
// DiceCenterHex - "Send Roll" button in center with optional stats display
// ============================================================================

interface DiceCenterHexProps {
  isEnabled: boolean;
  colors?: PlayerColorsWithGradient;
  /** Roll sum to display stats for (when both dice selected) */
  rollSum?: number;
  /** Roll statistics */
  rollStats?: RollStats;
}

const DiceCenterHex = memo(function DiceCenterHex({ 
  isEnabled, 
  colors,
  rollSum,
  rollStats,
}: DiceCenterHexProps) {
  const [isHovered, setIsHovered] = useState(false);
  const [isPressed, setIsPressed] = useState(false);

  const gradient = colors?.cssGradient || 'var(--hex-content-gradient)';
  const foreground = colors?.foreground || '#ffffff';
  // Use primary color for checkmark (contrasts with foreground background)
  const checkColor = colors?.primary || '#1a1a1a';

  // Scale based on interaction state (only when enabled)
  const innerScale = isEnabled && isPressed ? 0.85 : isEnabled && isHovered ? 0.88 : 0.91;
  const borderColor = isEnabled && isHovered ? 'var(--hex-border-hover)' : 'var(--hex-border-idle)';

  // Show stats when both dice are selected (rollSum is provided)
  const showStats = rollSum !== undefined && rollStats !== undefined;

  return (
    <div
      className="absolute inset-0"
      onMouseEnter={() => isEnabled && setIsHovered(true)}
      onMouseLeave={() => { setIsHovered(false); setIsPressed(false); }}
      onMouseDown={() => isEnabled && setIsPressed(true)}
      onMouseUp={() => setIsPressed(false)}
      onTouchStart={() => isEnabled && setIsPressed(true)}
      onTouchEnd={() => setIsPressed(false)}
    >
      {/* Outer border */}
      <div
        className="absolute inset-0 hex-clip-flat transition-colors duration-150"
        style={{ background: borderColor }}
      />
      {/* Inner content */}
      <div
        className={`absolute inset-0 hex-clip-flat flex flex-col items-center justify-center transition-all duration-150 ${!isEnabled ? 'opacity-40' : ''}`}
        style={{
          transform: `scale(${innerScale})`,
          background: isEnabled ? foreground : gradient,
        }}
      >
        {showStats ? (
          // Show roll stats: count, sum, and percentage
          <>
            <span
              className="text-[10px] font-bold leading-tight"
              style={{ color: checkColor }}
            >
              {rollStats.count}×
            </span>
            <span
              className="text-xl font-bold leading-tight"
              style={{ color: checkColor }}
            >
              {rollSum}
            </span>
            <span
              className="text-[9px] leading-tight"
              style={{ color: checkColor, opacity: 0.8 }}
            >
              {rollStats.percentage}%
            </span>
          </>
        ) : isEnabled ? (
          <span
            style={{
              color: checkColor,
              fontSize: '2.5em',
              transition: 'transform 150ms',
              transform: isPressed ? 'scale(0.85)' : 'scale(1)',
            }}
          >
            ✓
          </span>
        ) : null}
      </div>
    </div>
  );
});

// ============================================================================
// SingleDieCluster - One 7-hex cluster for a single die
// ============================================================================

interface SingleDieClusterProps {
  selectedValue: number | null;
  onSelect: (value: number | null) => void;
  canSendRoll?: boolean;
  onSendRoll?: () => void;
  colors?: PlayerColorsWithGradient;
  /** Roll sum when both dice are selected */
  rollSum?: number;
  /** Roll statistics for the current sum */
  currentRollStats?: RollStats;
}

const SingleDieCluster = memo(function SingleDieCluster({
  selectedValue,
  onSelect,
  canSendRoll = false,
  onSendRoll,
  colors,
  rollSum,
  currentRollStats,
}: SingleDieClusterProps) {
  // Map die values to cluster positions
  const diePositions: { value: number; coord: HexCoordinate }[] = [
    { value: 1, coord: CLUSTER_7.north },
    { value: 2, coord: CLUSTER_7.northEast },
    { value: 3, coord: CLUSTER_7.southEast },
    { value: 4, coord: CLUSTER_7.south },
    { value: 5, coord: CLUSTER_7.southWest },
    { value: 6, coord: CLUSTER_7.northWest },
  ];

  // Toggle behavior: clicking selected value unselects it
  const handleSelect = (value: number) => {
    onSelect(selectedValue === value ? null : value);
  };

  const items: HexGridItem[] = [
    // Center hex - "send roll" button with stats display
    {
      id: 'center',
      coord: CLUSTER_7.center,
      content: (
        <DiceCenterHex
          isEnabled={canSendRoll}
          colors={colors}
          rollSum={rollSum}
          rollStats={currentRollStats}
        />
      ),
      onClick: canSendRoll ? onSendRoll : undefined,
      disabled: !canSendRoll,
    },
    // Value hexes around the outside
    ...diePositions.map(({ value, coord }) => ({
      id: `die-${value}`,
      coord,
      content: (
        <DiceHexContent
          value={value}
          isSelected={selectedValue === value}
          colors={colors}
        />
      ),
      onClick: () => handleSelect(value),
    })),
  ];

  return (
    <HexGrid
      hexSize={40}
      items={items}
      gap={2}
      borderColor="transparent"
      fitToParent={true}
      fitPadding={0}
    />
  );
});

// ============================================================================
// DiceCluster - Main component with two dice side by side
// ============================================================================

export const DiceCluster = memo(function DiceCluster({
  enabled,
  isRolling,
  onRoll,
  colors,
  rollStats,
}: DiceClusterProps): React.ReactElement {
  const [die1Value, setDie1Value] = useState<number | null>(null);
  const [die2Value, setDie2Value] = useState<number | null>(null);

  const handleConfirm = useCallback(() => {
    if (die1Value && die2Value && enabled && !isRolling) {
      onRoll(die1Value, die2Value);
      // Reset after roll
      setDie1Value(null);
      setDie2Value(null);
    }
  }, [die1Value, die2Value, enabled, isRolling, onRoll]);

  const canSendRoll = die1Value !== null && die2Value !== null && enabled && !isRolling;
  
  // Calculate roll sum and get stats for it when both dice are selected
  const rollSum = (die1Value !== null && die2Value !== null) ? die1Value + die2Value : undefined;
  const currentRollStats = rollSum !== undefined && rollStats ? rollStats[rollSum] : undefined;

  return (
    <div className="w-full h-full flex items-center justify-center overflow-hidden">
      <div className="flex-1 h-full min-w-0">
        <SingleDieCluster
          selectedValue={die1Value}
          onSelect={enabled && !isRolling ? setDie1Value : () => {}}
          canSendRoll={canSendRoll}
          onSendRoll={handleConfirm}
          colors={colors}
          rollSum={rollSum}
          currentRollStats={currentRollStats}
        />
      </div>
      <div className="flex-1 h-full min-w-0">
        <SingleDieCluster
          selectedValue={die2Value}
          onSelect={enabled && !isRolling ? setDie2Value : () => {}}
          canSendRoll={canSendRoll}
          onSendRoll={handleConfirm}
          colors={colors}
          rollSum={rollSum}
          currentRollStats={currentRollStats}
        />
      </div>
    </div>
  );
});

export default DiceCluster;
