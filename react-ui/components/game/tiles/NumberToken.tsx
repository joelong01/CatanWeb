'use client';

import React, { memo } from 'react';

/**
 * Constants matching BoardSvgConstants.cs for visual parity with Blazor app.
 */
const NUMBER_TOKEN = {
  // Colors
  normalNumberColor: '#fff',
  highProbColor: '#c00',
  normalBackground: '#2F6999',
  highProbBackground: 'black',
  strokeColor: 'white',
  strokeWidth: 0.5,

  // Text positioning (relative to center, scaled to viewBox)
  numberOffsetY: -8,  // Number slightly above center
  pipsOffsetY: 12,    // Pips below center
} as const;

/**
 * Get probability stars for a roll number.
 * Matches CatanNumberSvg.GetPips() in Blazor.
 */
function getStars(number: number): string {
  switch (number) {
    case 2:
    case 12:
      return '★';
    case 3:
    case 11:
      return '★★';
    case 4:
    case 10:
      return '★★★';
    case 5:
    case 9:
      return '★★★★';
    case 6:
    case 8:
      return '★★★★★';
    default:
      return '';
  }
}

export interface NumberTokenProps {
  /** Roll number (2-12) */
  number: number;
  /** Optional className for positioning */
  className?: string;
  /** Border ring color. Defaults to 'white'. */
  borderColor?: string;
}

/**
 * NumberToken - Renders a Catan number token with proper styling.
 *
 * Matches the Blazor CatanNumberSvg.cs rendering:
 * - Blue circle background for normal numbers
 * - Black background with red text for high probability (6, 8)
 * - White star characters (★) for probability pips
 * - Number positioned slightly above center
 * - Pips positioned below the number
 *
 * Uses SVG for pixel-perfect circular rendering.
 */
export const NumberToken = memo(function NumberToken({
  number,
  className = '',
  borderColor = 'white',
}: NumberTokenProps) {
  const isHighProb = number === 6 || number === 8;
  const textColor = isHighProb ? NUMBER_TOKEN.highProbColor : NUMBER_TOKEN.normalNumberColor;
  const bgColor = isHighProb ? NUMBER_TOKEN.highProbBackground : NUMBER_TOKEN.normalBackground;
  const stars = getStars(number);

  // High prob numbers have 5 stars, need smaller font
  const pipsFontSize = isHighProb ? 8 : 10;

  return (
    <div
      className={`rounded-full aspect-square ${className}`}
      style={{
        backgroundColor: borderColor,
      }}
    >
      {/* Inner circle scaled down to reveal border ring */}
      <div
        className="rounded-full w-full h-full"
        style={{ backgroundColor: bgColor, transform: 'scale(0.85)' }}
      >
        <svg
          className="w-full h-full"
          viewBox="0 0 64 64"
          preserveAspectRatio="xMidYMid meet"
        >
          {/* Roll number - centered horizontally, offset above center vertically */}
          <text
            x="32"
            y={32 + NUMBER_TOKEN.numberOffsetY}
            textAnchor="middle"
            dominantBaseline="middle"
            fontFamily="sans-serif"
            fontSize="24"
            fontWeight="bold"
            fill={textColor}
          >
            {number}
          </text>

          {/* Probability stars - centered horizontally, below the number */}
          {stars && (
            <text
              x="32"
              y={32 + NUMBER_TOKEN.pipsOffsetY}
              textAnchor="middle"
              dominantBaseline="middle"
              fontFamily="sans-serif"
              fontSize={pipsFontSize}
              fill={textColor}
            >
              {stars}
            </text>
          )}
        </svg>
      </div>
    </div>
  );
});

export default NumberToken;
