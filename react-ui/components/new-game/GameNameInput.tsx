'use client';

import { useRef, useEffect, useCallback } from 'react';
import { FontAwesomeIcon } from '@fortawesome/react-fontawesome';
import { faGamepad } from '@fortawesome/free-solid-svg-icons';
import type { GameType } from '@/types/generated/models';

/**
 * Props for the GameNameInput component.
 */
interface GameNameInputProps {
  /** Current game name value */
  value: string;
  /** Callback when name changes */
  onChange: (name: string) => void;
  /** Game type for auto-generating name */
  gameType: GameType;
  /** Placeholder text */
  placeholder?: string;
}

/**
 * Get day of week abbreviation.
 */
function getDayAbbrev(): string {
  const days = ['Sun', 'Mon', 'Tue', 'Wed', 'Thu', 'Fri', 'Sat'];
  return days[new Date().getDay()];
}

/**
 * Get formatted time (12-hour with AM/PM).
 */
function getTimeString(): string {
  const now = new Date();
  let hours = now.getHours();
  const ampm = hours >= 12 ? 'PM' : 'AM';
  hours = hours % 12 || 12;
  return `${hours}${ampm}`;
}

/**
 * Get game type display name.
 */
function getGameTypeName(gameType: GameType): string {
  switch (gameType) {
    case 'Regular':
      return 'Catan';
    case 'Expansion':
      return 'Catan 5-6';
    case 'Seafarers':
      return 'Seafarers';
    default:
      return 'Game';
  }
}

/**
 * Generate default game name from game type and current time.
 */
export function generateGameName(gameType: GameType): string {
  return `${getGameTypeName(gameType)} ${getDayAbbrev()} ${getTimeString()}`;
}

/**
 * Styled input for entering the game name.
 * Auto-generates a default name and selects text on focus.
 */
export function GameNameInput({
  value,
  onChange,
  gameType,
  placeholder = 'Enter game name...',
}: GameNameInputProps): React.ReactElement {
  const inputRef = useRef<HTMLInputElement>(null);
  const prevGameTypeRef = useRef<GameType>(gameType);

  // Update name when game type changes (if using default pattern)
  useEffect(() => {
    // Only run when gameType actually changed
    if (prevGameTypeRef.current === gameType) return;
    prevGameTypeRef.current = gameType;

    // Check if current value looks like a generated name
    const generatedPattern =
      /^(Catan|Catan 5-6|Game) (Sun|Mon|Tue|Wed|Thu|Fri|Sat) \d{1,2}(AM|PM)$/;
    if (generatedPattern.test(value)) {
      onChange(generateGameName(gameType));
    }
  }, [gameType, value, onChange]);

  const handleFocus = useCallback(() => {
    // Select all text when focused
    inputRef.current?.select();
  }, []);

  return (
    <div className="mb-6">
      <h2 className="text-xl font-semibold text-white mb-4">Game Name</h2>
      <div className="relative flex items-center">
        <FontAwesomeIcon icon={faGamepad} className="absolute left-4 text-gray-500" />
        <input
          ref={inputRef}
          type="text"
          value={value}
          onChange={(e) => onChange(e.target.value)}
          onFocus={handleFocus}
          placeholder={placeholder}
          maxLength={50}
          className="
            w-full py-4 px-12 pr-16
            bg-game-bg-panel border-2 border-white/10 rounded-xl
            text-white text-base
            transition-all duration-200
            placeholder:text-gray-500
            focus:outline-none focus:border-blue-500
          "
        />
        <span className="absolute right-4 text-xs text-gray-500">{value.length}/50</span>
      </div>
    </div>
  );
}
