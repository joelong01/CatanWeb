'use client';

/**
 * DiceCluster - Two 7-hex clusters for dice selection.
 *
 * Each cluster shows values 1-6 around the edge with a confirm button in center.
 * Players select a value on each die, then click center to confirm roll.
 * Scales responsively to fit container.
 */

import { useState, useCallback, memo } from 'react';

interface DiceClusterProps {
  /** Whether dice rolling is enabled */
  enabled: boolean;
  /** Whether currently waiting for server response */
  isRolling: boolean;
  /** Callback when roll is confirmed */
  onRoll: (die1: number, die2: number) => void;
  /** Current player's color for highlighting */
  playerColor?: string;
}

/** Die value positions around the hex cluster (clockwise from top) */
const DIE_POSITIONS: { value: number; angle: number }[] = [
  { value: 1, angle: -90 },   // Top
  { value: 2, angle: -30 },   // Top-right
  { value: 3, angle: 30 },    // Bottom-right
  { value: 4, angle: 90 },    // Bottom
  { value: 5, angle: 150 },   // Bottom-left
  { value: 6, angle: 210 },   // Top-left (or -150)
];

interface SingleDieProps {
  label: string;
  selectedValue: number | null;
  onSelect: (value: number) => void;
  playerColor: string;
  disabled: boolean;
}

/** Single die cluster using CSS positioning */
const SingleDie = memo(function SingleDie({
  label,
  selectedValue,
  onSelect,
  playerColor,
  disabled,
}: SingleDieProps) {
  // Hex cluster dimensions - center hex + 6 surrounding
  const centerSize = 36; // Center hex size
  const outerSize = 28;  // Outer hex size
  const radius = 38;     // Distance from center to outer hexes

  return (
    <div
      className="relative"
      style={{ width: 110, height: 100 }}
    >
      {/* Center hex (label) */}
      <div
        className="absolute left-1/2 top-1/2 -translate-x-1/2 -translate-y-1/2 flex items-center justify-center text-xs font-bold text-gray-400"
        style={{
          width: centerSize,
          height: centerSize * 0.866,
          clipPath: 'polygon(25% 0%, 75% 0%, 100% 50%, 75% 100%, 25% 100%, 0% 50%)',
          backgroundColor: '#374151',
        }}
      >
        {label}
      </div>

      {/* Outer hexes (values 1-6) */}
      {DIE_POSITIONS.map(({ value, angle }) => {
        const isSelected = value === selectedValue;
        const radians = (angle * Math.PI) / 180;
        const x = Math.cos(radians) * radius;
        const y = Math.sin(radians) * radius;

        return (
          <button
            key={value}
            onClick={() => !disabled && onSelect(value)}
            disabled={disabled}
            className={`
              absolute flex items-center justify-center
              transition-all duration-150
              ${disabled ? 'opacity-50 cursor-not-allowed' : 'cursor-pointer hover:brightness-125'}
            `}
            style={{
              width: outerSize,
              height: outerSize * 0.866,
              left: `calc(50% + ${x}px)`,
              top: `calc(50% + ${y}px)`,
              transform: 'translate(-50%, -50%)',
              clipPath: 'polygon(25% 0%, 75% 0%, 100% 50%, 75% 100%, 25% 100%, 0% 50%)',
              backgroundColor: isSelected ? playerColor : '#4b5563',
            }}
          >
            <span className={`text-sm font-bold ${isSelected ? 'text-white' : 'text-gray-200'}`}>
              {value}
            </span>
          </button>
        );
      })}
    </div>
  );
});

/** Main DiceCluster component with two dice */
export function DiceCluster({
  enabled,
  isRolling,
  onRoll,
  playerColor = '#f59e0b',
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

  const canConfirm = die1Value !== null && die2Value !== null && enabled && !isRolling;
  const total = die1Value && die2Value ? die1Value + die2Value : null;

  return (
    <div className="p-2 flex flex-col items-center">
      {/* Dice row */}
      <div className="flex items-center justify-center gap-2">
        <SingleDie
          label="D1"
          selectedValue={die1Value}
          onSelect={setDie1Value}
          playerColor={playerColor}
          disabled={!enabled || isRolling}
        />
        <SingleDie
          label="D2"
          selectedValue={die2Value}
          onSelect={setDie2Value}
          playerColor={playerColor}
          disabled={!enabled || isRolling}
        />
      </div>

      {/* Roll button and status */}
      <div className="mt-2 flex flex-col items-center gap-1">
        <button
          onClick={handleConfirm}
          disabled={!canConfirm}
          className={`
            px-4 py-1.5 rounded-lg font-bold text-white text-sm
            transition-all duration-200
            ${canConfirm
              ? 'bg-amber-600 hover:bg-amber-500 shadow-lg'
              : 'bg-gray-600 cursor-not-allowed opacity-50'
            }
            ${isRolling ? 'animate-pulse' : ''}
          `}
        >
          {isRolling ? 'Rolling...' : total ? `Roll ${total}` : 'Select Dice'}
        </button>

        <div className="text-[10px] text-gray-500 text-center">
          {!enabled && 'Waiting for turn'}
          {enabled && !die1Value && !die2Value && 'Click numbers'}
          {enabled && die1Value && !die2Value && 'Select D2'}
          {enabled && !die1Value && die2Value && 'Select D1'}
          {enabled && die1Value && die2Value && 'Click to roll'}
        </div>
      </div>
    </div>
  );
}

export default DiceCluster;
