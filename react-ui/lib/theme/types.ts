/**
 * Theme system type definitions.
 *
 * Mirrors the Blazor ClientAssetService architecture:
 * - AssetName keys match base/theme.json asset keys
 * - RenderMode controls image vs font glyph rendering
 * - FontConfig/HarborFontConfig carry glyph + color data from theme.json
 */

/** All valid asset keys (matches base/theme.json). */
export type AssetName =
  // Tiles
  | 'TileBrick'
  | 'TileWheat'
  | 'TileWood'
  | 'TileOre'
  | 'TileSheep'
  | 'TileDesert'
  | 'TileGoldMine'
  | 'TileSea'
  | 'TileInvasion'
  // Harbors
  | 'HarborBrick'
  | 'HarborOre'
  | 'HarborSheep'
  | 'HarborWheat'
  | 'HarborWood'
  | 'HarborThreeForOne'
  // Cards
  | 'CardBrick'
  | 'CardWheat'
  | 'CardWood'
  | 'CardOre'
  | 'CardSheep'
  | 'CardGoldMine'
  | 'CardCloth'
  | 'CardPaper'
  | 'CardCoin'
  | 'CardTrade'
  | 'CardPolitics'
  | 'CardScience'
  | 'CardVictoryPoint'
  | 'CardBack'
  | 'CardRobber'
  | 'CardAnyDev'
  // Stats
  | 'StatScore'
  | 'StatLaurel'
  | 'StatRoads'
  | 'StatKnights'
  | 'StatCities'
  | 'StatSettlements'
  | 'StatShips'
  | 'StatDevCards'
  | 'StatResourceCards'
  | 'StatHarbors'
  | 'StatLongestRoad'
  | 'StatLargestArmy'
  | 'StatMetropolis'
  | 'StatGoodRoll'
  | 'StatBadRoll'
  | 'StatTargetted'
  | 'StatRobber'
  | 'StatSkulls'
  | 'StatCheck'
  | 'StatStar'
  | 'StatPirateShip'
  // Buildings
  | 'BuildingCity'
  | 'BuildingSettlement'
  | 'BuildingRoad'
  | 'BuildingShip'
  | 'BuildingKnight'
  // Backgrounds
  | 'BackgroundWater'
  | 'BackgroundBorderFill'
  | 'BackgroundBorderStroke'
  // Font
  | 'FontCatan';

/** How a theme renders game elements. */
export type RenderMode = 'image' | 'font';

/** Font config for a tile in theme.json colors.tiles section. */
export interface TileFontConfig {
  /** CatanGlyph key name (e.g., "WheatHex"), resolved at runtime */
  glyph: string;
  /** Glyph foreground color */
  color: string;
  /** Hex background fill color */
  bgColor: string;
  /** Outer hex border color */
  borderColor: string;
  /** Glyph scale relative to tile (0-1, default 1). Figurative glyphs use ~0.7 to fit within hex. */
  glyphScale?: number;
}

/** Font config for a harbor in theme.json colors.harbors section. */
export interface HarborFontConfig {
  /** CatanGlyph key for the harbor circle glyph */
  harborGlyph: string;
  /** CatanGlyph key for the hex background glyph (optional) */
  hexGlyph?: string;
  /** Foreground color */
  color: string;
  /** Background fill color (optional, for ThreeForOne) */
  bgColor?: string;
  /** Opacity for the hex background glyph (0-1, default 0.6) */
  hexOpacity?: number;
  /** FontAwesome icon name (e.g., "coins") */
  faIcon?: string;
}

/** Colors section of a font-mode theme.json. */
export interface ThemeColors {
  tiles?: Record<string, TileFontConfig>;
  harbors?: Record<string, HarborFontConfig>;
}

/** Full theme definition as loaded from theme.json. */
export interface ThemeDefinition {
  name: string;
  displayName: string;
  description?: string;
  preview: string;
  /** Parent theme name for fallback chain (e.g., modern → classic → base). */
  parent?: string;
  renderMode?: RenderMode;
  colors?: ThemeColors;
  assets: Partial<Record<AssetName, string>>;
}

/** Subset of ThemeDefinition exposed for theme selection UI. */
export interface ThemeMetadata {
  name: string;
  displayName: string;
  description?: string;
  preview: string;
  renderMode: RenderMode;
}
