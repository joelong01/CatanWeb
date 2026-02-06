'use client';

import React, { memo } from 'react';
import { CatanGlyph } from '@/lib/constants/catanGlyphs';

/**
 * Map roll number (2-12) to the corresponding Catan font glyph.
 * Each glyph includes the number and probability pips.
 */
const NUMBER_GLYPHS: Record<number, string> = {
  2: CatanGlyph.Number2,
  3: CatanGlyph.Number3,
  4: CatanGlyph.Number4,
  5: CatanGlyph.Number5,
  6: CatanGlyph.Number6,
  7: CatanGlyph.Number7,
  8: CatanGlyph.Number8,
  9: CatanGlyph.Number9,
  10: CatanGlyph.Number10,
  11: CatanGlyph.Number11,
  12: CatanGlyph.Number12,
};

/**
 * Color scheme matching BoardSvgConstants.cs for visual parity with Blazor app.
 */
const NUMBER_TOKEN = {
  normalNumberColor: '#fff',
  highProbColor: '#c00',
  normalBackground: '#2F6999',
  highProbBackground: 'black',
} as const;

export interface NumberTokenProps {
  /** Roll number (2-12) */
  number: number;
  /** Optional className for positioning */
  className?: string;
  /** Border ring color. Defaults to 'white'. */
  borderColor?: string;
}

/**
 * NumberToken - Renders a Catan number token using the Catan font.
 *
 * Uses Catan font glyphs (E945-E94F) which include the number and
 * probability pips in a single glyph. Preserves the color scheme:
 * - Blue circle background for normal numbers
 * - Black background with red text for high probability (6, 8)
 */
export const NumberToken = memo(function NumberToken({
  number,
  className = '',
  borderColor = 'white',
}: NumberTokenProps) {
  const isHighProb = number === 6 || number === 8;
  const textColor = isHighProb ? NUMBER_TOKEN.highProbColor : NUMBER_TOKEN.normalNumberColor;
  const bgColor = isHighProb ? NUMBER_TOKEN.highProbBackground : NUMBER_TOKEN.normalBackground;
  const glyph = NUMBER_GLYPHS[number];

  if (!glyph) return null;

  return (
    <div
      className={`rounded-full aspect-square ${className}`}
      style={{ backgroundColor: borderColor }}
    >
      {/* Inner circle scaled down to reveal border ring */}
      <div
        className="rounded-full w-full h-full"
        style={{ backgroundColor: bgColor, transform: 'scale(0.85)' }}
      >
        <svg className="w-full h-full" viewBox="0 0 64 64" preserveAspectRatio="xMidYMid meet">
          <text
            x="32"
            y="32"
            textAnchor="middle"
            dominantBaseline="central"
            className="font-catan"
            fontSize={46}
            fill={textColor}
          >
            {glyph}
          </text>
        </svg>
      </div>
    </div>
  );
});

export default NumberToken;
