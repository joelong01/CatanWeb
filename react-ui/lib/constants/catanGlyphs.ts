/**
 * Catan font glyphs for game icons.
 * These map to characters in the Catan.ttf font.
 * Codepoints match .assets/SVG For Font/glyph-map.json.
 *
 * Organized by logical category, not codepoint order.
 */

export const CatanGlyph = {
  // ── Buildings & Units ──
  City: '\uE900',
  Settlement: '\uE926',
  Road: '\uE909',
  Ship: '\uE90A',
  Metro: '\uE90F',
  Knight: '\uE930',
  KnightAttacking: '\uE937',
  KnightKneeling: '\uE938',
  KnightStanding: '\uE939',
  SolidShield: '\uE925',

  // ── Resource Tiles (detailed) ──
  BrickHex: '\uE933',
  DesertHex: '\uE936',
  OreHex: '\uE93A',
  SheepHex: '\uE93C',
  WheatHex: '\uE93F',
  WoodHex: '\uE941',
  GoldHex: '\uE943',
  TempGoldHex: '\uE944',

  // ── Resource Tiles (simple) ──
  SimpleBrickHex: '\uE951',
  SimpleDesertHex: '\uE952',
  SimpleOreHex: '\uE953',
  SimpleSheepHex: '\uE954',
  SimpleWheatHex: '\uE955',
  SimpleWoodHex: '\uE956',

  // ── Harbors ──
  ThreeToOneHarbor: '\uE931',
  BrickHarbor: '\uE934',
  OreHarbor: '\uE93B',
  SheepHarbor: '\uE93D',
  WheatHarbor: '\uE940',
  WoodHarbor: '\uE942',

  // ── Progress & Dev Cards ──
  Politics: '\uE904',
  Science: '\uE90B',
  Intrigue: '\uE905',
  Deserter: '\uE901',
  Diplomat: '\uE902',
  Inventor: '\uE906',
  Entry: '\uE903',
  Merchant: '\uE908',
  Wagon: '\uE93E',

  // ── Stats & Indicators ──
  Laurel: '\uE907',
  Star: '\uE911',
  LargestArmy: '\uE90E',
  LongestRoad: '\uE915',
  BadRoll: '\uE913',
  GoodRoll: '\uE914',
  Target: '\uE916',
  Sum: '\uE910',
  Robber: '\uE90C',
  PirateShip: '\uE90D',
  Skull: '\uE950',
  Check: '\uE935',

  // ── Numbers ──
  Number2: '\uE945',
  Number3: '\uE946',
  Number4: '\uE947',
  Number5: '\uE948',
  Number6: '\uE949',
  Number7: '\uE94A',
  Number8: '\uE94B',
  Number9: '\uE94C',
  Number10: '\uE94D',
  Number11: '\uE94E',
  Number12: '\uE94F',

  // ── Aliases ──
  Soldier: '\uE939', // KnightStanding used for purchase UI
  Pirate: '\uE90C', // Legacy alias for Robber
  DevCard: '?', // Placeholder - uses FontAwesome icon instead
} as const;

export type CatanGlyphKey = keyof typeof CatanGlyph;
