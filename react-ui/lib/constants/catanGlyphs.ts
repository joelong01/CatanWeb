/**
 * Catan font glyphs for game icons.
 * These map to characters in the Catan.ttf font.
 * Codepoints match .assets/SVG For Font/glyph-map.json.
 */

export const CatanGlyph = {
  // Original glyphs (E900-E916)
  City: '\uE900',
  Deserter: '\uE901',
  Diplomat: '\uE902',
  Entry: '\uE903',
  Politics: '\uE904',
  Intrigue: '\uE905',
  Inventor: '\uE906',
  Laurel: '\uE907',
  Merchant: '\uE908',
  Road: '\uE909',
  Ship: '\uE90A',
  Science: '\uE90B',
  Robber: '\uE90C',
  PirateShip: '\uE90D',
  LargestArmy: '\uE90E',
  Metro: '\uE90F',
  Sum: '\uE910',
  Star: '\uE911',
  BadRoll: '\uE913',
  GoodRoll: '\uE914',
  LongestRoad: '\uE915',
  Target: '\uE916',

  // Extended glyphs (E925-E926)
  SolidShield: '\uE925',
  Settlement: '\uE926',

  // Knight variants and new glyphs (E930-E942)
  Knight: '\uE930',
  ThreeToOneHarbor: '\uE931',
  BrickHex: '\uE933',
  BrickHarbor: '\uE934',
  Check: '\uE935',
  DesertHex: '\uE936',
  KnightAttacking: '\uE937',
  KnightKneeling: '\uE938',
  KnightStanding: '\uE939',
  OreHex: '\uE93A',
  OreHarbor: '\uE93B',
  SheepHex: '\uE93C',
  SheepHarbor: '\uE93D',
  Wagon: '\uE93E',
  WheatHex: '\uE93F',
  WheatHarbor: '\uE940',
  WoodHex: '\uE941',
  WoodHarbor: '\uE942',
  GoldHex: '\uE943',
  TempGoldHex: '\uE944',

  // Aliases for backward compatibility
  Soldier: '\uE939',       // KnightStanding used for purchase UI
  Pirate: '\uE90C',        // Legacy alias for Robber
  DevCard: '?',             // Placeholder - uses FontAwesome icon instead
} as const;

export type CatanGlyphKey = keyof typeof CatanGlyph;
