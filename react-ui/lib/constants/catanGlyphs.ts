/**
 * Catan font glyphs for game icons.
 * These map to characters in the Catan.ttf font.
 * See: WebUI/Layout/CatanFont.cs for Blazor equivalents.
 */

export const CatanGlyph = {
  City: '\uE900',
  Laurel: '\uE907',
  Road: '\uE909',
  Pirate: '\uE90C',
  Settlement: '\uE926',
  Soldier: '\uE90E',
  Sum: '\uE910',
  BadRoll: '\uE913',
  GoodRoll: '\uE914',
  LongestRoad: '\uE915',
  Target: '\uE916',
  Star: '\uE911',
  DevCard: '?', // Placeholder - uses FontAwesome icon instead
  Ship: '\uE90A',
} as const;

export type CatanGlyphKey = keyof typeof CatanGlyph;
