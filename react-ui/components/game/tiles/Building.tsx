'use client';

import React, { useState } from 'react';
import type { PlayerColors } from '../board/GameBoard';

/**
 * Building state (matches C# BuildingState enum)
 */
export type BuildingState =
  | 'PossibleSettlement'
  | 'NotBuildable'
  | 'Settlement'
  | 'City'
  | 'Metropolis'
  | 'Knight';

/**
 * Building visual state (matches C# BuildingVisualState enum)
 */
export type BuildingVisualState = 'Highlighted' | 'Hidden' | 'Stars' | 'Normal';

/**
 * Catan font glyph codes (matches Blazor CatanGlyphs)
 */
const CATAN_GLYPHS = {
  Settlement: '\uE926',
  City: '\uE900',
  Metropolis: '\uE900', // Uses city glyph with different styling
  Knight: '\uE912', // Knight glyph
} as const;

/**
 * Props for Building component
 */
export interface BuildingProps {
  /** Building state (Settlement, City, etc.) */
  buildingState: BuildingState;
  /** Visual state controlling how the building renders */
  visualState: BuildingVisualState;
  /** Number of stars for this building position (used when visualState is 'Stars' or 'Hidden' on hover) */
  stars?: number;
  /** Player colors for owned buildings */
  ownerColors?: PlayerColors | null;
  /** Current player colors (used for highlighting/placement/stars) */
  currentPlayerColors?: PlayerColors | null;
  /** Size of the building in pixels (diameter) */
  size: number;
  /** Click handler */
  onClick?: () => void;
  /** Additional className */
  className?: string;
}

/**
 * Neutral/default colors when no player colors are available
 * HotPink makes bugs obvious - if you see this color, something is wrong
 */
const NEUTRAL_COLORS: PlayerColors = {
  primary: 'rgba(255, 255, 255, 0.3)',
  secondary: 'rgba(200, 200, 200, 0.3)',
  foreground: 'rgba(0, 0, 0, 0.5)',
};

/**
 * Building component - renders settlements, cities, or star indicators
 *
 * Visual states:
 * - Normal: Shows settlement/city glyph with owner colors
 * - Highlighted: Shows settlement/city glyph with current player colors (for placement)
 * - Stars: Shows numeric star count (for board evaluation)
 * - Hidden: Invisible by default, shows Stars on hover (for allocation phase)
 */
export const Building = React.memo(function Building({
  buildingState,
  visualState,
  stars = 0,
  ownerColors,
  currentPlayerColors,
  size,
  onClick,
  className = '',
}: BuildingProps) {
  // Track hover state for Hidden -> Stars transition
  const [isHovered, setIsHovered] = useState(false);

  // Determine effective visual state (Hidden becomes Stars on hover)
  const effectiveState = visualState === 'Hidden' && isHovered ? 'Stars' : visualState;

  // Determine which colors to use
  const colors = effectiveState === 'Highlighted' || effectiveState === 'Stars'
    ? (currentPlayerColors ?? NEUTRAL_COLORS)
    : (ownerColors ?? NEUTRAL_COLORS);

  // Determine what to render inside the circle
  let content: React.ReactNode = null;
  const fontSize = size * 0.5;

  if (effectiveState === 'Stars') {
    // Stars mode: show numeric star count
    content = (
      <span
        className="font-bold select-none"
        style={{
          fontSize: fontSize * 0.9,
          color: colors.foreground,
          textShadow: '0 0 2px black, 0 0 2px black',
        }}
      >
        {stars}
      </span>
    );
  } else if (effectiveState === 'Hidden') {
    // Hidden: render invisible hit target (no content)
    content = null;
  } else if (buildingState === 'Settlement' || buildingState === 'PossibleSettlement') {
    // Settlement: show settlement glyph
    content = (
      <span
        className="catan-font select-none"
        style={{
          fontSize,
          color: colors.foreground,
        }}
      >
        {CATAN_GLYPHS.Settlement}
      </span>
    );
  } else if (buildingState === 'City') {
    // City: show city glyph
    content = (
      <span
        className="catan-font select-none"
        style={{
          fontSize,
          color: colors.foreground,
        }}
      >
        {CATAN_GLYPHS.City}
      </span>
    );
  } else if (buildingState === 'Metropolis') {
    // Metropolis: show city glyph with different size
    content = (
      <span
        className="catan-font select-none"
        style={{
          fontSize: fontSize * 1.2,
          color: colors.foreground,
        }}
      >
        {CATAN_GLYPHS.Metropolis}
      </span>
    );
  } else if (buildingState === 'Knight') {
    // Knight: show knight glyph
    content = (
      <span
        className="catan-font select-none"
        style={{
          fontSize,
          color: colors.foreground,
        }}
      >
        {CATAN_GLYPHS.Knight}
      </span>
    );
  }

  // For Hidden state (not hovered), render invisible hit target
  if (effectiveState === 'Hidden') {
    return (
      <div
        className={`rounded-full cursor-pointer ${className}`}
        style={{
          width: size,
          height: size,
          // Invisible but still interactive
          background: 'transparent',
        }}
        onClick={onClick}
        onMouseEnter={() => setIsHovered(true)}
        onMouseLeave={() => setIsHovered(false)}
      />
    );
  }

  // Gradient background using player colors
  const gradient = `linear-gradient(135deg, ${colors.primary} 0%, ${colors.secondary} 100%)`;

  return (
    <div
      className={`rounded-full flex items-center justify-center cursor-pointer transition-all duration-150 ${className}`}
      style={{
        width: size,
        height: size,
        background: gradient,
        border: `2px solid ${colors.foreground}`,
        boxShadow: effectiveState === 'Highlighted' ? '0 0 8px rgba(255, 255, 255, 0.5)' : undefined,
        transform: isHovered ? 'scale(1.15)' : 'scale(1)',
      }}
      onClick={onClick}
      onMouseEnter={() => setIsHovered(true)}
      onMouseLeave={() => setIsHovered(false)}
    >
      {content}
    </div>
  );
});

export default Building;
