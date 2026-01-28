'use client';

/**
 * Controls Test Page - Styling test bed for game control components.
 *
 * Uses mock GameModel data to iterate on styling quickly without needing
 * a live SignalR connection. Similar approach to /hex-test for geometry.
 *
 * Implements hex-based UI per .design/ui/react/game-page.md:
 * - DiceCluster: Two 7-hex clusters for dice selection
 * - ActionCluster: 7-hex cluster for game controls
 * - PlayerTile: 13 stats with Catan font glyphs
 */

import { useState, useRef, useEffect, useMemo, ReactNode } from 'react';
import { MainLayout } from '@/components/layout';
import { HexGrid, HexGridItem } from '@/components/hex-grid';
import { HexCoordinate } from '@/components/hex-grid/hex-geometry';
import { FloatingPanel, MinimizedBar } from '@/components/game/panels';
import { GameResourcesHeader } from '@/components/game/panels/GameResourcesHeader';
import { GameBoard } from '@/components/game/board/GameBoard';
import type { GameModel } from '@/types/generated/models/game-model';
import type { ResourcesModel } from '@/types/generated/models/resources-model';
import { NumberToken } from '@/components/game/tiles/NumberToken';
import { EXPANSION_GAME_DATA, generateTestBuildingsAndRoads } from '@/lib/test-data/expansion-game';
import { gameActions } from '@/lib/stores/gameStoreHooks';
import type { PlayerProfile } from '@/types/player-profile';
import { FontAwesomeIcon } from '@fortawesome/react-fontawesome';
import { faRotateLeft, faRotateRight, faArrowsRotate, faCheck, faScaleBalanced, faReceipt } from '@fortawesome/free-solid-svg-icons';
import type { IconDefinition } from '@fortawesome/fontawesome-svg-core';

// ============================================================================
// Catan Font Glyphs (from PlayerTile.razor CatanGlyph class)
// ============================================================================

const CatanGlyph = {
  // Catan font glyphs (from PlayerTile.razor)
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
  DevCard: '?',
  Ship: '\uE90A',
};

// ============================================================================
// Player Colors (matches Catan3.Shared.Profiles.PlayerColors)
// ============================================================================

/**
 * Player color scheme used for rendering.
 * Matches the C# PlayerColors record from Catan3.Shared.Profiles.
 */
interface PlayerColors {
  /** Primary background color (hex: #RRGGBB) */
  primary: string;
  /** Secondary background/gradient color (hex: #RRGGBB) */
  secondary: string;
  /** Foreground/text color (hex: #RRGGBB) */
  foreground: string;
}

/**
 * Compute relative luminance of a hex color (0-1 scale).
 * Used to determine if a color is "light" or "dark".
 */
function getLuminance(hex: string): number {
  const rgb = hex.replace('#', '');
  const r = parseInt(rgb.substring(0, 2), 16) / 255;
  const g = parseInt(rgb.substring(2, 4), 16) / 255;
  const b = parseInt(rgb.substring(4, 6), 16) / 255;
  // Simplified luminance (weighted average)
  return 0.299 * r + 0.587 * g + 0.114 * b;
}

/**
 * Build CSS gradient from PlayerColors.
 * Heavy gradient: Primary → Secondary → (black for light colors, white for dark).
 * Matches the visual depth effect in Blazor.
 */
function buildCssGradient(colors: PlayerColors): string {
  // Determine if colors are light or dark based on average luminance
  const avgLuminance = (getLuminance(colors.primary) + getLuminance(colors.secondary)) / 2;
  const endColor = avgLuminance > 0.4 ? '#000000' : '#ffffff';
  return `linear-gradient(135deg, ${colors.primary}, ${colors.secondary}, ${endColor})`;
}

/**
 * Create PlayerColors with computed gradient.
 */
function createPlayerColors(primary: string, secondary: string, foreground: string): PlayerColors & { cssGradient: string } {
  const colors: PlayerColors = { primary, secondary, foreground };
  return {
    ...colors,
    cssGradient: buildCssGradient(colors),
  };
}

// ============================================================================
// Mock Data Types (simplified for testing)
// ============================================================================

/** Resources collected this turn (from PlayerModel.ResourcesThisTurn) */
interface ResourcesThisTurn {
  wheat: number;
  wood: number;
  sheep: number;
  brick: number;
  ore: number;
  goldMine: number;
  robber: number;
}

/** Resource types tracked for cards */
type TrackedResourceType = 'wheat' | 'wood' | 'sheep' | 'brick' | 'ore' | 'goldMine' | 'robber';

/** Harbors owned by player (for 2:1 trade indicator) */
type OwnedHarbor = 'wheat' | 'wood' | 'sheep' | 'brick' | 'ore' | 'generic';

interface MockPlayer {
  id: string;
  name: string;
  colors: PlayerColors & { cssGradient: string };
  imageFileName: string;
  // 13 stats from Blazor PlayerTile
  score: number;
  roads: number;
  cities: number;
  settlements: number;
  soldiers: number;
  devCards: number;
  robberLoss: number;
  timesTargeted: number;
  totalResources: number;
  longestRoad: number;
  goodRolls: number;
  badRolls: number;
  stars: number;
  // Highlight flags
  hasHighestScore: boolean;
  hasLongestRoad: boolean;
  hasLargestArmy: boolean;
  // Current turn indicator
  isCurrentPlayer: boolean;
  // Resources gained this turn (for flippable cards)
  resourcesThisTurn: ResourcesThisTurn;
  // Harbors owned (for 2:1 trade indicator)
  ownedHarbors: OwnedHarbor[];
}

// ============================================================================
// Mock Data
// ============================================================================

const MOCK_PLAYERS: MockPlayer[] = [
  {
    id: 'player-1',
    name: 'Joe',
    colors: createPlayerColors('#e53935', '#b71c1c', '#ffffff'),
    imageFileName: 'Joe-001',
    score: 7,
    roads: 8,
    cities: 1,
    settlements: 3,
    soldiers: 3,
    devCards: 2,
    robberLoss: 4,
    timesTargeted: 2,
    totalResources: 42,
    longestRoad: 5,
    goodRolls: 8,
    badRolls: 3,
    stars: 15,
    hasHighestScore: true,
    hasLongestRoad: true,
    hasLargestArmy: false,
    isCurrentPlayer: true,
    resourcesThisTurn: { wheat: 2, wood: 1, sheep: 0, brick: 0, ore: 3, goldMine: 0, robber: 0 },
    ownedHarbors: ['wheat', 'ore'],
  },
  {
    id: 'player-2',
    name: 'Doug',
    colors: createPlayerColors('#1e88e5', '#0d47a1', '#ffffff'),
    imageFileName: 'Doug-001',
    score: 5,
    roads: 5,
    cities: 0,
    settlements: 4,
    soldiers: 1,
    devCards: 1,
    robberLoss: 2,
    timesTargeted: 1,
    totalResources: 35,
    longestRoad: 3,
    goodRolls: 5,
    badRolls: 6,
    stars: 12,
    hasHighestScore: false,
    hasLongestRoad: false,
    hasLargestArmy: false,
    isCurrentPlayer: false,
    resourcesThisTurn: { wheat: 0, wood: 0, sheep: 1, brick: 2, ore: 0, goldMine: 0, robber: 1 },
    ownedHarbors: ['sheep'],
  },
  {
    id: 'player-3',
    name: 'Chris',
    colors: createPlayerColors('#43a047', '#1b5e20', '#ffffff'),
    imageFileName: 'Chris-001',
    score: 6,
    roads: 6,
    cities: 2,
    settlements: 2,
    soldiers: 4,
    devCards: 4,
    robberLoss: 6,
    timesTargeted: 3,
    totalResources: 48,
    longestRoad: 4,
    goodRolls: 6,
    badRolls: 4,
    stars: 14,
    hasHighestScore: false,
    hasLongestRoad: false,
    hasLargestArmy: true,
    isCurrentPlayer: false,
    resourcesThisTurn: { wheat: 1, wood: 2, sheep: 1, brick: 0, ore: 0, goldMine: 1, robber: 0 },
    ownedHarbors: ['wood', 'generic'],
  },
  {
    id: 'player-4',
    name: 'Adrian',
    colors: createPlayerColors('#fb8c00', '#e65100', '#000000'),
    imageFileName: 'Adrian-001',
    score: 4,
    roads: 4,
    cities: 0,
    settlements: 4,
    soldiers: 0,
    devCards: 0,
    robberLoss: 8,
    timesTargeted: 5,
    totalResources: 28,
    longestRoad: 2,
    goodRolls: 4,
    badRolls: 7,
    stars: 10,
    hasHighestScore: false,
    hasLongestRoad: false,
    hasLargestArmy: false,
    isCurrentPlayer: false,
    resourcesThisTurn: { wheat: 0, wood: 0, sheep: 0, brick: 0, ore: 0, goldMine: 0, robber: 2 },
    ownedHarbors: [],
  },
];

/** Mock game resources for testing */
const MOCK_RESOURCES: ResourcesModel = {
  wheat: 12,
  wood: 8,
  sheep: 15,
  brick: 6,
  ore: 9,
  goldMine: 2,
  paper: 0,
  cloth: 0,
  coin: 0,
  politics: 0,
  trade: 0,
  science: 0,
  victoryPoint: 0,
  anyDevCard: 0,
  robber: 3,
  count: 55,
};

// ============================================================================
// Mock Game Tiles (from Expansion.catan_test for resource counting)
// ============================================================================

/** Stars (probability dots) for each dice number */
function getStarsForNumber(num: number): number {
  // 7 = 0 (desert), 2/12 = 1, 3/11 = 2, 4/10 = 3, 5/9 = 4, 6/8 = 5
  if (num === 7) return 0;
  return 6 - Math.abs(7 - num);
}

/** Mock tile data extracted from Expansion.catan_test */
interface MockTile {
  q: number;
  r: number;
  s: number;
  number: number;
  resourceType: string;
}

const MOCK_TILES: MockTile[] = [
  // Expansion board tiles (30 land tiles)
  { q: -3, r: 1, s: 2, number: 4, resourceType: 'Ore' },
  { q: -3, r: 2, s: 1, number: 8, resourceType: 'Wheat' },
  { q: -3, r: 3, s: 0, number: 3, resourceType: 'Brick' },
  { q: -2, r: 0, s: 2, number: 6, resourceType: 'Sheep' },
  { q: -2, r: 1, s: 1, number: 2, resourceType: 'Wheat' },
  { q: -2, r: 2, s: 0, number: 12, resourceType: 'Brick' },
  { q: -2, r: 3, s: -1, number: 10, resourceType: 'Wood' },
  { q: -1, r: -1, s: 2, number: 11, resourceType: 'Sheep' },
  { q: -1, r: 0, s: 1, number: 11, resourceType: 'Ore' },
  { q: -1, r: 1, s: 0, number: 9, resourceType: 'Sheep' },
  { q: -1, r: 2, s: -1, number: 7, resourceType: 'Desert' },
  { q: -1, r: 3, s: -2, number: 8, resourceType: 'Wood' },
  { q: 0, r: -2, s: 2, number: 11, resourceType: 'Brick' },
  { q: 0, r: -1, s: 1, number: 7, resourceType: 'Desert' },
  { q: 0, r: 0, s: 0, number: 9, resourceType: 'Ore' },
  { q: 0, r: 1, s: -1, number: 2, resourceType: 'Wood' },
  { q: 0, r: 2, s: -2, number: 3, resourceType: 'Wheat' },
  { q: 0, r: 3, s: -3, number: 4, resourceType: 'Wood' },
  { q: 1, r: -2, s: 1, number: 10, resourceType: 'Brick' },
  { q: 1, r: -1, s: 0, number: 5, resourceType: 'Wood' },
  { q: 1, r: 0, s: -1, number: 9, resourceType: 'Brick' },
  { q: 1, r: 1, s: -2, number: 6, resourceType: 'Sheep' },
  { q: 1, r: 2, s: -3, number: 5, resourceType: 'Wood' },
  { q: 2, r: -2, s: 0, number: 10, resourceType: 'Sheep' },
  { q: 2, r: -1, s: -1, number: 8, resourceType: 'Wheat' },
  { q: 2, r: 0, s: -2, number: 5, resourceType: 'Ore' },
  { q: 2, r: 1, s: -3, number: 12, resourceType: 'Ore' },
  { q: 3, r: -2, s: -1, number: 4, resourceType: 'Sheep' },
  { q: 3, r: -1, s: -2, number: 3, resourceType: 'Wheat' },
  { q: 3, r: 0, s: -3, number: 6, resourceType: 'Brick' },
];

/** Calculate resource counts with stars from tiles */
function calculateResourceCounts(tiles: MockTile[]): Record<string, number> {
  const counts: Record<string, number> = {
    Sheep: 0,
    Wood: 0,
    Ore: 0,
    Wheat: 0,
    Brick: 0,
  };

  for (const tile of tiles) {
    if (tile.resourceType in counts && tile.resourceType !== 'Desert') {
      counts[tile.resourceType] += getStarsForNumber(tile.number);
    }
  }

  return counts;
}

/** Calculate variance (how unbalanced the resources are) */
function calculateVariance(counts: Record<string, number>): number {
  const values = Object.values(counts);
  const avg = values.reduce((a, b) => a + b, 0) / values.length;
  const variance = values.reduce((sum, v) => sum + Math.pow(v - avg, 2), 0) / values.length;
  return Math.sqrt(variance);
}

// ============================================================================
// 7-Hex Cluster Coordinates (CLUSTER_7 pattern)
// ============================================================================

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
// Roll Ring Component (2-12 roll buttons with count and percentage)
// ============================================================================

/** Roll stats for tracking */
interface RollStats {
  count: number;
  percentage: number;
}

/** Props for RollHexContent */
interface RollHexContentProps {
  rollNumber: number;
  count: number;
  percentage: number;
  colors?: PlayerColors & { cssGradient: string };
}

/** Get probability stars for a roll number (7 = no stars) */
function _getRollStars(number: number): string {
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
    case 7:
    default:
      return ''; // 7 has no stars
  }
}

/** Hex content for a roll button showing number token, count, and percentage */
function RollHexContent({
  rollNumber,
  count,
  percentage,
  colors,
}: RollHexContentProps) {
  const [isHovered, setIsHovered] = useState(false);
  const [isPressed, setIsPressed] = useState(false);

  const gradient = colors?.cssGradient || 'var(--hex-content-gradient)';
  const foreground = colors?.foreground || '#ffffff';
  const borderColor = isHovered
    ? 'var(--hex-border-hover)'
    : 'var(--hex-border-idle)';

  // Button scale: normal 0.96, hover 0.94, pressed 0.90
  const scale = isPressed ? 0.90 : isHovered ? 0.94 : 0.96;

  return (
    <div
      className="absolute inset-0 cursor-pointer"
      onMouseEnter={() => setIsHovered(true)}
      onMouseLeave={() => { setIsHovered(false); setIsPressed(false); }}
      onMouseDown={() => setIsPressed(true)}
      onMouseUp={() => setIsPressed(false)}
    >
      {/* Outer border */}
      <div
        className="absolute inset-0 hex-clip-flat transition-colors duration-150"
        style={{ background: borderColor }}
      />
      {/* Inner content */}
      <div
        className="absolute inset-0 hex-clip-flat flex flex-col items-center justify-center transition-all duration-150"
        style={{
          transform: `scale(${scale})`,
          background: gradient,
        }}
      >
        {/* Count at top */}
        <span
          className="text-[10px] font-bold"
          style={{ color: foreground }}
        >
          {count}
        </span>

        {/* Number token - using the same component as the board tiles */}
        <div className="w-10 h-10">
          <NumberToken number={rollNumber} className="w-full h-full" />
        </div>

        {/* Percentage at bottom */}
        <span
          className="text-[9px]"
          style={{ color: foreground, opacity: 0.8 }}
        >
          {percentage}%
        </span>
      </div>
    </div>
  );
}

/** Props for RollRing */
interface RollRingProps {
  /** Roll statistics for each number 2-12 */
  rollStats: Record<number, RollStats>;
  /** Callback when a roll button is clicked */
  onRollClick?: (roll: number) => void;
  /** Player colors */
  colors?: PlayerColors & { cssGradient: string };
}

/**
 * RollRing - 11 hex buttons for roll numbers 2-12
 * Arranged in 3 rows: 4-4-3 pattern matching Blazor layout
 */
function RollRing({
  rollStats,
  onRollClick,
  colors,
}: RollRingProps) {
  // Hex coordinates for 3-4-3 COLUMN layout with 7 isolated
  // Reading top-to-bottom within each column, left-to-right across columns
  // 7 is special (robber roll) so it's isolated at bottom-left edge
  //
  // Col0  Col1  Col2
  //        5    10
  //  2     6    11
  //  3     8    12
  //  4     9
  //  7 (bottom-left edge)
  //
  const rollCoords: { roll: number; coord: HexCoordinate }[] = [
    // Column 0 (q=0): 2, 3, 4 - shifted down 1 to align with middle column
    { roll: 2, coord: { q: 0, r: 1, s: -1 } },
    { roll: 3, coord: { q: 0, r: 2, s: -2 } },
    { roll: 4, coord: { q: 0, r: 3, s: -3 } },
    // Column 1 (q=1): 5, 6, 8, 9 (7 skipped)
    { roll: 5, coord: { q: 1, r: 0, s: -1 } },
    { roll: 6, coord: { q: 1, r: 1, s: -2 } },
    { roll: 8, coord: { q: 1, r: 2, s: -3 } },
    { roll: 9, coord: { q: 1, r: 3, s: -4 } },
    // Column 2 (q=2): 10, 11, 12
    { roll: 10, coord: { q: 2, r: 0, s: -2 } },
    { roll: 11, coord: { q: 2, r: 1, s: -3 } },
    { roll: 12, coord: { q: 2, r: 2, s: -4 } },
    // 7 isolated at bottom-left edge
    { roll: 7, coord: { q: -1, r: 4, s: -3 } },
  ];

  const items: HexGridItem[] = rollCoords.map(({ roll, coord }) => {
    const stats = rollStats[roll] || { count: 0, percentage: 0 };
    return {
      id: `roll-${roll}`,
      coord,
      content: (
        <RollHexContent
          rollNumber={roll}
          count={stats.count}
          percentage={stats.percentage}
          colors={colors}
        />
      ),
      onClick: () => onRollClick?.(roll),
    };
  });

  return (
    <div className="w-full h-full">
      <HexGrid
        hexSize={38}
        items={items}
        gap={1}
        borderColor="transparent"
        fitToParent={true}
        fitPadding={8}
      />
    </div>
  );
}

/** Demo wrapper for RollRing with mock data */
interface RollRingDemoProps {
  colors?: PlayerColors & { cssGradient: string };
}

function RollRingDemo({ colors }: RollRingDemoProps) {
  // Mock roll stats (one roll of 7 for demo)
  const mockRollStats: Record<number, RollStats> = {
    2: { count: 0, percentage: 0 },
    3: { count: 0, percentage: 0 },
    4: { count: 0, percentage: 0 },
    5: { count: 0, percentage: 0 },
    6: { count: 0, percentage: 0 },
    7: { count: 1, percentage: 100 },
    8: { count: 0, percentage: 0 },
    9: { count: 0, percentage: 0 },
    10: { count: 0, percentage: 0 },
    11: { count: 0, percentage: 0 },
    12: { count: 0, percentage: 0 },
  };

  return (
    <RollRing
      rollStats={mockRollStats}
      onRollClick={(roll) => console.log(`Roll ${roll} clicked`)}
      colors={colors}
    />
  );
}

// ============================================================================
// Dice Cluster Component
// ============================================================================

/** Unicode dice face glyphs */
const DICE_GLYPHS = ['', '⚀', '⚁', '⚂', '⚃', '⚄', '⚅'];

interface DiceHexContentProps {
  value: number;
  isSelected: boolean;
  /** Player colors for styling */
  colors?: PlayerColors & { cssGradient: string };
}

function DiceHexContent({
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
}

/** Center hex - "Send Roll" button */
interface DiceCenterHexProps {
  isEnabled: boolean;
  colors?: PlayerColors & { cssGradient: string };
}

function DiceCenterHex({ isEnabled, colors }: DiceCenterHexProps) {
  const [isHovered, setIsHovered] = useState(false);
  const [isPressed, setIsPressed] = useState(false);

  const gradient = colors?.cssGradient || 'var(--hex-content-gradient)';
  const foreground = colors?.foreground || '#ffffff';
  // Use primary color for checkmark (contrasts with foreground background)
  const checkColor = colors?.primary || '#1a1a1a';

  // Scale based on interaction state (only when enabled)
  const innerScale = isEnabled && isPressed ? 0.85 : isEnabled && isHovered ? 0.88 : 0.91;
  const borderColor = isEnabled && isHovered ? 'var(--hex-border-hover)' : 'var(--hex-border-idle)';

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
        className={`absolute inset-0 hex-clip-flat flex items-center justify-center transition-all duration-150 ${!isEnabled ? 'opacity-40' : ''}`}
        style={{
          transform: `scale(${innerScale})`,
          background: isEnabled ? foreground : gradient,
        }}
      >
        {isEnabled && (
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
        )}
      </div>
    </div>
  );
}

interface DiceClusterProps {
  selectedValue: number | null;
  onSelect: (value: number | null) => void;
  /** Whether the center "send" button is enabled (both dice selected) */
  canSendRoll?: boolean;
  /** Callback when center button clicked */
  onSendRoll?: () => void;
  /** Player colors for styling */
  colors?: PlayerColors & { cssGradient: string };
}

function DiceCluster({
  selectedValue,
  onSelect,
  canSendRoll = false,
  onSendRoll,
  colors,
}: DiceClusterProps) {
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
    // Center hex - "send roll" button
    {
      id: 'center',
      coord: CLUSTER_7.center,
      content: (
        <DiceCenterHex
          isEnabled={canSendRoll}
          colors={colors}
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
}

/** Two dice clusters side by side */
interface DicePanelContentProps {
  die1: number | null;
  die2: number | null;
  onSelectDie1: (value: number | null) => void;
  onSelectDie2: (value: number | null) => void;
  onSendRoll?: () => void;
  colors?: PlayerColors & { cssGradient: string };
}

function _DicePanelContent({
  die1,
  die2,
  onSelectDie1,
  onSelectDie2,
  onSendRoll,
  colors,
}: DicePanelContentProps) {
  const canSendRoll = die1 !== null && die2 !== null;

  return (
    <div className="w-full h-full flex items-center justify-center overflow-hidden">
      <div className="flex-1 h-full min-w-0">
        <DiceCluster
          selectedValue={die1}
          onSelect={onSelectDie1}
          canSendRoll={canSendRoll}
          onSendRoll={onSendRoll}
          colors={colors}
        />
      </div>
      <div className="flex-1 h-full min-w-0">
        <DiceCluster
          selectedValue={die2}
          onSelect={onSelectDie2}
          canSendRoll={canSendRoll}
          onSendRoll={onSendRoll}
          colors={colors}
        />
      </div>
    </div>
  );
}

// ============================================================================
// Action Cluster Component
// ============================================================================

interface ActionHexContentProps {
  /** Text glyph to display (Catan font or regular text) */
  glyph?: string;
  /** FontAwesome icon to display (alternative to glyph) */
  faIcon?: IconDefinition;
  label?: string;
  isEnabled?: boolean;
  isPrimary?: boolean;
  /** Stats to show on back when flipped (e.g., "2/15") */
  backStats?: string;
  /** Count to show in top-left corner (e.g., purchased count) */
  count?: number;
  /** Player colors for styling */
  colors?: PlayerColors & { cssGradient: string };
  /** Use Catan font for glyph (default true for Catan glyphs) */
  useCatanFont?: boolean;
}

function ActionHexContent({
  glyph,
  faIcon,
  label,
  isEnabled = true,
  isPrimary = false,
  backStats,
  count,
  colors,
  useCatanFont = true,
}: ActionHexContentProps) {
  const [isHovered, setIsHovered] = useState(false);
  const [isPressed, setIsPressed] = useState(false);

  const gradient = colors?.cssGradient || 'var(--hex-content-gradient)';
  const foreground = colors?.foreground || '#ffffff';

  // All face-up buttons use player gradient
  const frontBg = gradient;

  // Scale based on interaction state (only when enabled)
  const innerScale = isEnabled && isPressed ? 0.90 : isEnabled && isHovered ? 0.93 : 0.96;
  const borderColor = isEnabled && isHovered ? 'var(--hex-border-hover)' : 'var(--hex-border-idle)';

  // Render the icon/glyph content
  const renderIcon = (size: 'large' | 'small', pressed = false): ReactNode => {
    const sizeClass = size === 'large'
      ? (isPrimary ? 'text-3xl' : 'text-2xl')
      : 'text-base';
    // All face-up buttons use player foreground color
    const color = foreground;

    if (faIcon) {
      return (
        <FontAwesomeIcon
          icon={faIcon}
          className={sizeClass}
          style={{
            color,
            filter: size === 'large' ? 'drop-shadow(1px 1px 2px rgba(0,0,0,0.5))' : undefined,
            transition: 'transform 150ms',
            transform: pressed ? 'scale(0.85)' : 'scale(1)',
          }}
        />
      );
    }

    return (
      <span
        className={`${useCatanFont ? 'font-catan' : ''} ${sizeClass}`}
        style={{
          color,
          textShadow: size === 'large' ? '1px 1px 2px rgba(0,0,0,0.5)' : undefined,
          transition: 'transform 150ms',
          transform: pressed ? 'scale(0.85)' : 'scale(1)',
          display: 'inline-block',
        }}
      >
        {glyph}
      </span>
    );
  };

  return (
    <div
      className="absolute inset-0"
      style={{ perspective: '500px' }}
      onMouseEnter={() => isEnabled && setIsHovered(true)}
      onMouseLeave={() => { setIsHovered(false); setIsPressed(false); }}
      onMouseDown={() => isEnabled && setIsPressed(true)}
      onMouseUp={() => setIsPressed(false)}
      onTouchStart={() => isEnabled && setIsPressed(true)}
      onTouchEnd={() => setIsPressed(false)}
    >
      {/* Flip container */}
      <div
        className="relative w-full h-full transition-transform duration-500"
        style={{
          transformStyle: 'preserve-3d',
          transform: isEnabled ? 'rotateY(0deg)' : 'rotateY(180deg)',
        }}
      >
        {/* Front face (enabled state) */}
        <div
          className="absolute inset-0"
          style={{ backfaceVisibility: 'hidden' }}
        >
          {/* Outer border */}
          <div
            className="absolute inset-0 hex-clip-flat transition-colors duration-150"
            style={{ background: borderColor }}
          />
          {/* Inner content */}
          <div
            className="absolute inset-0 hex-clip-flat flex flex-col items-center justify-center transition-all duration-150"
            style={{
              transform: `scale(${innerScale})`,
              background: frontBg,
            }}
          >
            {/* Count badge - positioned along line from upper-left vertex to center
                Flat-top hex: upper-left vertex at (25%, 13.4%), center at (50%, 50%)
                At t=0.2 along line: x = 25 + 0.2*25 = 30%, y = 13.4 + 0.2*36.6 = 20.7% */}
            {count !== undefined && (
              <span
                className="absolute text-sm font-bold"
                style={{
                  top: '21%',
                  left: '30%',
                  transform: 'translate(-50%, -50%)',
                  color: foreground,
                  textShadow: '1px 1px 2px rgba(0,0,0,0.5)',
                }}
              >
                {count}
              </span>
            )}
            {renderIcon('large', isPressed)}
            {label && (
              <span
                className="text-[8px] mt-0.5 uppercase tracking-wider transition-transform duration-150"
                style={{
                  color: foreground,
                  transform: isPressed ? 'scale(0.9)' : 'scale(1)',
                }}
              >
                {label}
              </span>
            )}
          </div>
        </div>

        {/* Back face (disabled/flipped state) - card back image */}
        <div
          className="absolute inset-0"
          style={{
            backfaceVisibility: 'hidden',
            transform: 'rotateY(180deg)',
          }}
        >
          {/* Card back with image */}
          <div
            className="absolute inset-0 hex-clip-flat flex flex-col items-center justify-center"
            style={{
              backgroundImage: 'url(/themes/base/resources/back.png)',
              backgroundSize: 'cover',
              backgroundPosition: 'center',
            }}
          >
            {/* Icon badge centered */}
            <div
              className="rounded-sm px-1.5 py-0.5"
              style={{ background: gradient }}
            >
              {renderIcon('small')}
            </div>
            {/* Stats below icon */}
            {backStats && (
              <span className="text-xs font-bold text-white mt-1 bg-black/70 px-1 rounded-sm">
                {backStats}
              </span>
            )}
          </div>
        </div>
      </div>
    </div>
  );
}

interface ActionClusterProps {
  colors?: PlayerColors & { cssGradient: string };
  gameState?: string;
  onAction?: (action: string) => void;
  /** Stats for disabled buttons: bought/available counts */
  purchaseStats?: {
    roads?: { bought: number; available: number };
    settlements?: { bought: number; available: number };
    cities?: { bought: number; available: number };
    devCards?: { bought: number; available: number };
  };
  /** Which buttons are enabled */
  enabledButtons?: {
    next?: boolean;
    undo?: boolean;
    redo?: boolean;
    road?: boolean;
    settlement?: boolean;
    city?: boolean;
    devCard?: boolean;
  };
}

function ActionCluster({
  colors,
  gameState = 'Roll the Dice',
  onAction,
  purchaseStats,
  enabledButtons = { next: true },
}: ActionClusterProps) {
  const handleAction = (action: string) => {
    onAction?.(action);
    console.log('Action:', action);
  };

  const formatStats = (stats?: { bought: number; available: number }) =>
    stats ? `${stats.bought}/${stats.available}` : undefined;

  const items: HexGridItem[] = [
    // Center: Next (primary action)
    {
      id: 'next',
      coord: CLUSTER_7.center,
      content: (
        <ActionHexContent
          glyph="➤"
          label="Next"
          isPrimary={true}
          isEnabled={enabledButtons.next !== false}
          colors={colors}
          useCatanFont={false}
        />
      ),
      onClick: () => handleAction('next'),
      disabled: enabledButtons.next === false,
    },
    // North: DevCard (receipt icon) - only shows count, not "bought/available"
    {
      id: 'devcard',
      coord: CLUSTER_7.north,
      content: (
        <ActionHexContent
          faIcon={faReceipt}
          label="Dev"
          isEnabled={enabledButtons.devCard !== false}
          backStats={purchaseStats?.devCards?.bought.toString()}
          count={purchaseStats?.devCards?.bought}
          colors={colors}
        />
      ),
      onClick: () => handleAction('devcard'),
      disabled: enabledButtons.devCard === false,
    },
    // NE: Road
    {
      id: 'road',
      coord: CLUSTER_7.northEast,
      content: (
        <ActionHexContent
          glyph={CatanGlyph.Road}
          label="Road"
          isEnabled={enabledButtons.road !== false}
          backStats={formatStats(purchaseStats?.roads)}
          count={purchaseStats?.roads?.bought}
          colors={colors}
        />
      ),
      onClick: () => handleAction('road'),
      disabled: enabledButtons.road === false,
    },
    // SE: Settlement
    {
      id: 'settlement',
      coord: CLUSTER_7.southEast,
      content: (
        <ActionHexContent
          glyph={CatanGlyph.Settlement}
          label="Settle"
          isEnabled={enabledButtons.settlement !== false}
          backStats={formatStats(purchaseStats?.settlements)}
          count={purchaseStats?.settlements?.bought}
          colors={colors}
        />
      ),
      onClick: () => handleAction('settlement'),
      disabled: enabledButtons.settlement === false,
    },
    // South: City
    {
      id: 'city',
      coord: CLUSTER_7.south,
      content: (
        <ActionHexContent
          glyph={CatanGlyph.City}
          label="City"
          isEnabled={enabledButtons.city !== false}
          backStats={formatStats(purchaseStats?.cities)}
          count={purchaseStats?.cities?.bought}
          colors={colors}
        />
      ),
      onClick: () => handleAction('city'),
      disabled: enabledButtons.city === false,
    },
    // SW: Redo (FontAwesome icon)
    {
      id: 'redo',
      coord: CLUSTER_7.southWest,
      content: (
        <ActionHexContent
          faIcon={faRotateRight}
          label="Redo"
          isEnabled={enabledButtons.redo === true}
          colors={colors}
          useCatanFont={false}
        />
      ),
      onClick: () => handleAction('redo'),
      disabled: enabledButtons.redo !== true,
    },
    // NW: Undo (FontAwesome icon)
    {
      id: 'undo',
      coord: CLUSTER_7.northWest,
      content: (
        <ActionHexContent
          faIcon={faRotateLeft}
          label="Undo"
          isEnabled={enabledButtons.undo === true}
          colors={colors}
          useCatanFont={false}
        />
      ),
      onClick: () => handleAction('undo'),
      disabled: enabledButtons.undo !== true,
    },
  ];

  return (
    <div className="w-full h-full flex flex-col">
      <div className="flex-1 min-h-0">
        <HexGrid
          hexSize={45}
          items={items}
          gap={2}
          borderColor="transparent"
          fitToParent={true}
          fitPadding={0}
        />
      </div>
      {/* State message below cluster */}
      <div
        className="text-sm font-semibold text-center py-1 flex-shrink-0"
        style={{ color: colors?.foreground || '#fbbf24' }}
      >
        {gameState}
      </div>
    </div>
  );
}

// ============================================================================
// Action Cluster Demo Wrapper (with toggle controls)
// ============================================================================

interface ActionClusterDemoProps {
  colors?: PlayerColors & { cssGradient: string };
}

function ActionClusterDemo({ colors }: ActionClusterDemoProps) {
  const [allFaceUp, setAllFaceUp] = useState(false);

  return (
    <div className="w-full h-full flex flex-col">
      <div className="flex-1 min-h-0">
        <ActionCluster
          colors={colors}
          gameState={allFaceUp ? 'Build Phase' : 'Roll the Dice'}
          purchaseStats={{
            roads: { bought: 3, available: 15 },
            settlements: { bought: 2, available: 5 },
            cities: { bought: 1, available: 4 },
            devCards: { bought: 2, available: 25 },
          }}
          enabledButtons={{
            next: true,
            road: allFaceUp,
            settlement: allFaceUp,
            city: allFaceUp,
            devCard: allFaceUp,
            undo: allFaceUp,
            redo: allFaceUp,
          }}
        />
      </div>
      {/* Toggle button */}
      <button
        onClick={() => setAllFaceUp(!allFaceUp)}
        className="text-xs px-2 py-1 rounded bg-gray-700 hover:bg-gray-600 text-gray-300 mt-1"
      >
        {allFaceUp ? 'Flip Face Down' : 'Flip Face Up'}
      </button>
    </div>
  );
}

// ============================================================================
// Measurement Cluster Component (Nested Hex Design)
// ============================================================================

/** Resource configuration for outer ring (matches mockup layout) */
const RESOURCE_CONFIG = [
  { key: 'Sheep', label: 'Sheep', position: 'north', image: '/themes/base/resources/sheep.png' },
  { key: 'Wood', label: 'Wood', position: 'northEast', image: '/themes/base/resources/wood.png' },
  { key: 'Wheat', label: 'Wheat', position: 'southEast', image: '/themes/base/resources/wheat.png' },
  { key: 'Brick', label: 'Brick', position: 'southWest', image: '/themes/base/resources/brick.png' },
  { key: 'Ore', label: 'Ore', position: 'northWest', image: '/themes/base/resources/ore.png' },
] as const;

/** Star filter values for inner cluster */
const STAR_VALUES_CONFIG = [
  { value: 13, position: 'north' },
  { value: 12, position: 'northEast' },
  { value: 11, position: 'southEast' },
  { value: 10, position: 'south' },
  { value: 9, position: 'southWest' },
  { value: 8, position: 'northWest' },
] as const;

/** Resource hex content for outer ring - shows resource image with count overlay */
interface ResourceHexContentProps {
  image: string;
  count: number;
  isSelected: boolean;
  colors?: PlayerColors & { cssGradient: string };
}

function ResourceHexContent({
  image,
  count,
  isSelected,
}: ResourceHexContentProps) {
  const [isHovered, setIsHovered] = useState(false);
  const [isPressed, setIsPressed] = useState(false);

  // Scale based on interaction state
  const innerScale = isPressed ? 0.86 : isHovered ? 0.89 : 0.92;
  const borderColor = isSelected ? '#3b82f6' : isHovered ? '#60a5fa' : 'rgba(255,255,255,0.3)';

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
      {/* Outer border - hover blue when selected */}
      <div
        className="absolute inset-0 hex-clip-flat transition-colors duration-150"
        style={{ background: borderColor }}
      />
      {/* Inner content - resource image background */}
      <div
        className="absolute inset-0 hex-clip-flat flex items-end justify-center transition-all duration-150"
        style={{
          transform: `scale(${innerScale})`,
          backgroundImage: `url(${image})`,
          backgroundSize: 'cover',
          backgroundPosition: 'center',
        }}
      >
        {/* Count overlay - white text on black background, flush to bottom */}
        <div
          className="px-2 py-0.5 rounded-t transition-transform duration-150"
          style={{
            background: 'rgba(0, 0, 0, 0.75)',
            transform: isPressed ? 'scale(0.9)' : 'scale(1)',
          }}
        >
          <span className="font-bold text-xl text-white leading-none">
            {count}
          </span>
        </div>
      </div>
      {/* Selection checkmark - upper-right area (matching PlayerSelector) */}
      {isSelected && (
        <div
          className="absolute z-10 w-6 h-6 rounded-full flex items-center justify-center bg-black/50 border border-white/50 -translate-x-1/2 -translate-y-1/2"
          style={{ top: '16%', left: '68%' }}
        >
          <FontAwesomeIcon
            icon={faCheck}
            className="text-xs text-white"
          />
        </div>
      )}
    </div>
  );
}

/** Variance/balance hex content (south position) - shows scale icon + variance value */
interface VarianceHexContentProps {
  variance: number;
  colors?: PlayerColors & { cssGradient: string };
}

function VarianceHexContent({ variance, colors }: VarianceHexContentProps) {
  const gradient = colors?.cssGradient || 'var(--hex-content-gradient)';
  const foreground = colors?.foreground || '#ffffff';

  return (
    <>
      {/* Outer border */}
      <div
        className="absolute inset-0 hex-clip-flat transition-colors duration-200"
        style={{ background: 'var(--hex-border-idle)' }}
      />
      {/* Inner content - player gradient background */}
      <div
        className="absolute inset-0 hex-clip-flat flex flex-col items-center justify-center"
        style={{
          transform: 'scale(0.92)',
          background: gradient,
        }}
      >
        {/* Scale icon */}
        <FontAwesomeIcon
          icon={faScaleBalanced}
          className="text-xl"
          style={{ color: foreground }}
        />
        {/* Variance value */}
        <span
          className="text-lg font-bold"
          style={{ color: foreground }}
        >
          {variance.toFixed(1)}
        </span>
      </div>
    </>
  );
}

/** Star filter hex content for inner cluster - purple/violet theme from mockup */
interface StarHexContentProps {
  value: number;
  isSelected: boolean;
  colors?: PlayerColors & { cssGradient: string };
}

function StarHexContent({ value, isSelected, colors }: StarHexContentProps) {
  const [isHovered, setIsHovered] = useState(false);
  const [isPressed, setIsPressed] = useState(false);

  // Mockup uses purple/lavender color scheme
  const purpleBg = '#a5b4fc'; // indigo-300
  const purpleBgSelected = '#6366f1'; // indigo-500

  // Scale based on interaction state
  const innerScale = isPressed ? 0.80 : isHovered ? 0.84 : 0.88;
  const borderColor = isSelected ? (colors?.primary || purpleBgSelected) : isHovered ? '#a5b4fc' : '#818cf8';

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
      {/* Inner content - purple/lavender */}
      <div
        className="absolute inset-0 hex-clip-flat flex items-center justify-center transition-all duration-150"
        style={{
          transform: `scale(${innerScale})`,
          background: isSelected ? (colors?.cssGradient || purpleBgSelected) : purpleBg,
        }}
      >
        <span
          className="font-bold text-[9px] leading-none transition-transform duration-150"
          style={{
            color: isSelected ? (colors?.foreground || '#ffffff') : '#1e1b4b',
            transform: isPressed ? 'scale(0.85)' : 'scale(1)',
          }}
        >
          {value}
        </span>
      </div>
    </div>
  );
}

/** Reset/shuffle button hex content for inner cluster center */
interface ResetHexContentProps {
  colors?: PlayerColors & { cssGradient: string };
}

function ResetHexContent({ colors }: ResetHexContentProps) {
  const [isHovered, setIsHovered] = useState(false);
  const [isPressed, setIsPressed] = useState(false);

  // Same purple theme as star buttons
  const purpleBg = '#a5b4fc'; // indigo-300

  // Scale based on interaction state
  const innerScale = isPressed ? 0.80 : isHovered ? 0.84 : 0.88;
  const borderColor = isHovered ? '#a5b4fc' : '#818cf8';

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
      {/* Inner content - purple with shuffle/refresh icon */}
      <div
        className="absolute inset-0 hex-clip-flat flex items-center justify-center transition-all duration-150"
        style={{
          transform: `scale(${innerScale})`,
          background: colors?.cssGradient || purpleBg,
        }}
      >
        <FontAwesomeIcon
          icon={faArrowsRotate}
          className="text-[9px] transition-transform duration-150"
          style={{
            color: colors?.foreground || '#1e1b4b',
            transform: isPressed ? 'scale(0.85)' : 'scale(1)',
          }}
        />
      </div>
    </div>
  );
}

/** Measurement cluster with nested hexes */
interface MeasurementClusterProps {
  tiles: MockTile[];
  colors?: PlayerColors & { cssGradient: string };
}

/** Max resources that can be selected at once */
const MAX_RESOURCE_SELECTION = 3;

function MeasurementCluster({ tiles, colors }: MeasurementClusterProps) {
  // Multi-select for resources (up to 3), single-select for stars
  const [selectedResources, setSelectedResources] = useState<string[]>([]);
  const [selectedStars, setSelectedStars] = useState<number | null>(null);

  const resourceCounts = calculateResourceCounts(tiles);
  const _variance = calculateVariance(resourceCounts);

  // Toggle resource selection (multi-select up to MAX_RESOURCE_SELECTION)
  const handleResourceClick = (key: string) => {
    if (selectedResources.includes(key)) {
      // Deselect if already selected
      setSelectedResources(selectedResources.filter(r => r !== key));
    } else if (selectedResources.length < MAX_RESOURCE_SELECTION) {
      // Add to selection if under limit
      setSelectedResources([...selectedResources, key]);
    } else {
      // At max: remove oldest, add new (circular selection)
      setSelectedResources([...selectedResources.slice(1), key]);
    }
  };

  // Single-select for star filter
  const handleStarClick = (value: number) => {
    setSelectedStars(selectedStars === value ? null : value);
  };

  const handleReset = () => {
    setSelectedResources([]);
    setSelectedStars(null);
  };

  // Build outer ring items (5 resources + variance)
  const outerItems: HexGridItem[] = [
    // Resources in outer ring positions
    ...RESOURCE_CONFIG.map(({ key, position, image }) => ({
      id: `resource-${key}`,
      coord: CLUSTER_7[position],
      content: (
        <ResourceHexContent
          image={image}
          count={resourceCounts[key] || 0}
          isSelected={selectedResources.includes(key)}
        />
      ),
      onClick: () => handleResourceClick(key),
    })),
    // Variance in south position - using dummy value 0.5
    {
      id: 'variance',
      coord: CLUSTER_7.south,
      content: <VarianceHexContent variance={0.5} colors={colors} />,
    },
  ];

  // Build inner cluster items (7 hexes for star filter)
  const innerItems: HexGridItem[] = [
    // Center: Reset button
    {
      id: 'reset',
      coord: CLUSTER_7.center,
      content: <ResetHexContent colors={colors} />,
      onClick: handleReset,
    },
    // Star values around the outside
    ...STAR_VALUES_CONFIG.map(({ value, position }) => ({
      id: `star-${value}`,
      coord: CLUSTER_7[position],
      content: (
        <StarHexContent
          value={value}
          isSelected={selectedStars === value}
          colors={colors}
        />
      ),
      onClick: () => handleStarClick(value),
    })),
  ];

  return (
    <div className="w-full h-full flex flex-col">
      {/* Nested hex clusters container */}
      <div className="flex-1 min-h-0 relative">
        {/* Outer ring - larger hexes */}
        <div className="absolute inset-0">
          <HexGrid
            items={outerItems}
            hexSize={50}
            borderColor="transparent"
            fitToParent={true}
            fitPadding={5}
          />
        </div>
        {/* Inner cluster - smaller hexes centered within the outer ring's empty center */}
        <div
          className="absolute"
          style={{
            left: '50%',
            top: '50%',
            transform: 'translate(-50%, -50%)',
            width: '32%',
            height: '32%',
          }}
        >
          <HexGrid
            items={innerItems}
            hexSize={12}
            gap={1}
            borderColor="transparent"
            fitToParent={true}
            fitPadding={0}
          />
        </div>
      </div>
    </div>
  );
}

// ============================================================================
// Resource Card Component (flippable cards for ResourcesThisTurn)
// ============================================================================

/** Resource card config with images */
const RESOURCE_CARD_CONFIG: { type: TrackedResourceType; image: string; label: string }[] = [
  { type: 'wheat', image: '/themes/base/resources/wheat.png', label: 'Wheat' },
  { type: 'wood', image: '/themes/base/resources/wood.png', label: 'Wood' },
  { type: 'sheep', image: '/themes/base/resources/sheep.png', label: 'Sheep' },
  { type: 'brick', image: '/themes/base/resources/brick.png', label: 'Brick' },
  { type: 'ore', image: '/themes/base/resources/ore.png', label: 'Ore' },
  { type: 'goldMine', image: '/themes/base/resources/goldmine.png', label: 'Gold' },
  { type: 'robber', image: '/themes/base/resources/robber.png', label: 'Robber' },
];

interface ResourceCardProps {
  resourceType: TrackedResourceType;
  count: number;
  hasHarbor?: boolean;
  /** Auto flip: show front when count > 0, back when count = 0 */
  autoFlip?: boolean;
}

function ResourceCard({
  resourceType,
  count,
  hasHarbor = false,
  autoFlip = true,
}: ResourceCardProps) {
  const [manualFlip, setManualFlip] = useState<boolean | null>(null);

  // Find the config for this resource
  const config = RESOURCE_CARD_CONFIG.find(c => c.type === resourceType);
  if (!config) return null;

  // Determine if showing front
  const isShowingFront = manualFlip !== null ? manualFlip : (autoFlip ? count > 0 : true);

  // Right-click to manually flip
  const handleContextMenu = (e: React.MouseEvent) => {
    e.preventDefault();
    setManualFlip(!isShowingFront);
  };

  // Blazor sizes: 71px × 100px (aspect ratio 5:7)
  return (
    <div
      className="relative cursor-pointer"
      style={{ width: '71px', height: '100px', perspective: '500px' }}
      onContextMenu={handleContextMenu}
      title={`${config.label}: ${count} (right-click to flip)`}
    >
      {/* Flip container */}
      <div
        className="relative w-full h-full transition-transform duration-500"
        style={{
          transformStyle: 'preserve-3d',
          transform: isShowingFront ? 'rotateY(0deg)' : 'rotateY(180deg)',
        }}
      >
        {/* Front face - resource image with count */}
        <div
          className="absolute inset-0 rounded overflow-hidden shadow-md"
          style={{ backfaceVisibility: 'hidden' }}
        >
          <div
            className="w-full h-full bg-cover bg-center"
            style={{ backgroundImage: `url(${config.image})` }}
          />
          {/* Count badge at bottom */}
          <div
            className="absolute bottom-1 left-0 right-0 flex justify-center"
          >
            <span
              className="px-2 py-1 rounded text-white font-bold text-lg leading-none"
              style={{ background: 'rgba(0, 0, 0, 0.85)' }}
            >
              {count}
            </span>
          </div>
          {/* Harbor indicator (ship glyph) */}
          {hasHarbor && (
            <div
              className="absolute top-1 right-1 w-6 h-6 rounded-full bg-black flex items-center justify-center"
            >
              <span className="font-catan text-white text-base">{'\uE90D'}</span>
            </div>
          )}
        </div>

        {/* Back face - card back */}
        <div
          className="absolute inset-0 rounded overflow-hidden shadow-md"
          style={{
            backfaceVisibility: 'hidden',
            transform: 'rotateY(180deg)',
          }}
        >
          <div
            className="w-full h-full bg-cover bg-center"
            style={{ backgroundImage: 'url(/themes/base/resources/back.png)' }}
          />
        </div>
      </div>
    </div>
  );
}

// ============================================================================
// Player Tile Component (13 stats with Catan font glyphs)
// ============================================================================

interface StatTileProps {
  glyph?: string;
  faIcon?: IconDefinition;
  count: number;
  isHighlighted?: boolean;
  isScore?: boolean;
  colors: PlayerColors & { cssGradient: string };
}

function StatTile({
  glyph,
  faIcon,
  count,
  isHighlighted = false,
  isScore = false,
  colors,
}: StatTileProps) {
  const bgStyle = isHighlighted
    ? { background: colors.cssGradient }
    : { background: colors.primary };

  // Blazor uses 35px stat tiles
  const tileSize = 'w-[35px] h-[35px]';

  // Render icon - either Catan font glyph or FontAwesome icon
  const renderIcon = (className: string) => {
    if (faIcon) {
      return (
        <FontAwesomeIcon
          icon={faIcon}
          className={className}
          style={{ color: colors.foreground }}
        />
      );
    }
    return (
      <span className={`font-catan ${className}`}>{glyph}</span>
    );
  };

  if (isScore) {
    // Score stat: laurel wreath with number centered inside
    return (
      <div
        className={`${tileSize} rounded-md flex items-center justify-center relative`}
        style={{ ...bgStyle, color: colors.foreground }}
      >
        <span className="font-catan text-[32px] absolute leading-none">{glyph}</span>
        <span className="font-bold text-lg z-10">{count}</span>
      </div>
    );
  }

  // Normal stat: icon above count
  return (
    <div
      className={`${tileSize} rounded-md flex flex-col items-center justify-center`}
      style={{ ...bgStyle, color: colors.foreground }}
    >
      {renderIcon('text-base leading-none')}
      <span className="text-xs font-bold leading-none mt-0.5">{count}</span>
    </div>
  );
}

interface PlayerTileProps {
  player: MockPlayer;
  isSelected?: boolean;
  onClick?: () => void;
}

function PlayerTile({ player, isSelected, onClick }: PlayerTileProps) {
  // 13 stats in display order (matching Blazor PlayerTile.razor)
  const stats: Omit<StatTileProps, 'colors'>[] = [
    { glyph: CatanGlyph.Laurel, count: player.score, isHighlighted: player.hasHighestScore, isScore: true },
    { glyph: CatanGlyph.Road, count: player.roads },
    { glyph: CatanGlyph.City, count: player.cities },
    { glyph: CatanGlyph.Settlement, count: player.settlements },
    { glyph: CatanGlyph.Soldier, count: player.soldiers, isHighlighted: player.hasLargestArmy },
    { faIcon: faReceipt, count: player.devCards },
    { glyph: CatanGlyph.Pirate, count: player.robberLoss },
    { glyph: CatanGlyph.Target, count: player.timesTargeted },
    { glyph: CatanGlyph.Sum, count: player.totalResources },
    { glyph: CatanGlyph.LongestRoad, count: player.longestRoad, isHighlighted: player.hasLongestRoad },
    { glyph: CatanGlyph.GoodRoll, count: player.goodRolls },
    { glyph: CatanGlyph.BadRoll, count: player.badRolls },
    { glyph: CatanGlyph.Star, count: player.stars },
  ];

  return (
    <div
      className={`p-1 rounded cursor-pointer transition-all ${isSelected ? 'ring-2 ring-amber-400' : 'hover:bg-white/5'}`}
      style={{
        backgroundColor: `${player.colors.primary}20`,
        borderLeft: `4px solid ${player.colors.primary}`,
      }}
      onClick={onClick}
    >
      {/* Row 1: Avatar + Stats Grid (all 13 stats in one row) */}
      <div className="flex gap-0.5 items-start mb-0.5">
        {/* Avatar */}
        <div
          className="w-10 h-10 rounded-full bg-cover bg-center flex-shrink-0 flex items-center justify-center"
          style={{
            backgroundImage: `url('http://localhost:8080/api/images/${player.imageFileName}')`,
            border: `1px solid ${player.colors.foreground}`,
            backgroundColor: '#333',
          }}
        >
          {/* Show first letter if no image */}
        </div>

        {/* Stats Grid (all 13 in one row) */}
        <div className="flex gap-px">
          {stats.map((stat, i) => (
            <StatTile
              key={i}
              glyph={stat.glyph}
              faIcon={stat.faIcon}
              count={stat.count}
              isHighlighted={stat.isHighlighted}
              isScore={stat.isScore}
              colors={player.colors}
            />
          ))}
        </div>
      </div>

      {/* Row 2: Resources This Turn (larger cards matching Blazor: 71px × 100px) */}
      <div className="flex gap-0.5 mt-1">
        {RESOURCE_CARD_CONFIG.map(({ type }) => {
          const count = player.resourcesThisTurn[type];
          // Check if player has harbor for this resource (excluding goldMine and robber)
          const hasHarbor = type !== 'goldMine' && type !== 'robber' &&
            player.ownedHarbors.includes(type as OwnedHarbor);
          return (
            <ResourceCard
              key={type}
              resourceType={type}
              count={count}
              hasHarbor={hasHarbor}
              autoFlip={true}
            />
          );
        })}
      </div>
    </div>
  );
}

// ============================================================================
// Scaled Players List (scales content to fit container while preserving aspect ratio)
// ============================================================================

interface ScaledPlayersListProps {
  players: MockPlayer[];
  selectedPlayerId: string;
  onSelectPlayer: (id: string) => void;
}

function ScaledPlayersList({ players, selectedPlayerId, onSelectPlayer }: ScaledPlayersListProps) {
  const containerRef = useRef<HTMLDivElement>(null);
  const contentRef = useRef<HTMLDivElement>(null);
  const [scale, setScale] = useState(1);
  const [naturalSize, setNaturalSize] = useState({ width: 0, height: 0 });

  // Measure natural content size once on mount (at scale=1)
  useEffect(() => {
    if (contentRef.current) {
      // Temporarily set scale to 1 to measure natural size
      contentRef.current.style.transform = 'scale(1)';
      const width = contentRef.current.scrollWidth;
      const height = contentRef.current.scrollHeight;
      setNaturalSize({ width, height });
    }
  }, [players]);

  // Update scale when container resizes or natural size changes
  useEffect(() => {
    if (!naturalSize.width || !naturalSize.height) return;

    const updateScale = () => {
      if (containerRef.current) {
        const containerWidth = containerRef.current.clientWidth;
        const containerHeight = containerRef.current.clientHeight;

        // Calculate scale to fit content in container (preserving aspect ratio)
        const widthScale = containerWidth / naturalSize.width;
        const heightScale = containerHeight / naturalSize.height;

        // Use smaller scale so content fits in BOTH dimensions
        const newScale = Math.min(widthScale, heightScale);
        setScale(newScale > 0 ? newScale : 1);
      }
    };

    updateScale();

    const resizeObserver = new ResizeObserver(updateScale);
    if (containerRef.current) {
      resizeObserver.observe(containerRef.current);
    }

    return () => resizeObserver.disconnect();
  }, [naturalSize]);

  return (
    <div
      ref={containerRef}
      className="w-full h-full overflow-hidden"
    >
      <div
        ref={contentRef}
        className="space-y-2 p-2 inline-block"
        style={{
          transform: `scale(${scale})`,
          transformOrigin: 'top left',
        }}
      >
        {players.map(player => (
          <PlayerTile
            key={player.id}
            player={player}
            isSelected={player.id === selectedPlayerId}
            onClick={() => onSelectPlayer(player.id)}
          />
        ))}
      </div>
    </div>
  );
}

// ============================================================================
// Main Page Component
// ============================================================================

export default function ControlsTestPage(): React.ReactElement {
  const [die1, setDie1] = useState<number | null>(null);
  const [die2, setDie2] = useState<number | null>(null);
  const [selectedPlayerId, setSelectedPlayerId] = useState<string>(MOCK_PLAYERS[0].id);

  const selectedPlayer = MOCK_PLAYERS.find(p => p.id === selectedPlayerId) || MOCK_PLAYERS[0];

  // Generate buildings and roads for all players to test multi-player rendering
  const playerIds = MOCK_PLAYERS.map(p => p.id);
  const testData = useMemo(() => generateTestBuildingsAndRoads(playerIds), []);

  // Robber on desert tile with first player's colors
  const testRobber = useMemo(() => ({
    coordinates: { q: -1, r: 2, s: -1 }, // Desert tile
    previousCoordinates: { q: -1, r: 2, s: -1 },
    fakeOutCoordinates: { q: -1, r: 2, s: -1 },
    movedBy: MOCK_PLAYERS[0].id, // First player's colors
    targeted: '',
    resourcesStolen: 0,
  }), []);

  // Populate game store with player profiles and game model
  // GameBoard now uses internal hooks, so we must populate the store
  useEffect(() => {
    // Set player profiles
    const profiles: PlayerProfile[] = MOCK_PLAYERS.map(p => ({
      id: p.id,
      name: p.name,
      colors: p.colors,
      imageUri: `http://localhost:8080/api/images/${p.imageFileName}`,
    }));
    gameActions.setPlayerProfiles(profiles);

    // Set current player ID (for building/road interactions)
    gameActions.setCurrentPlayerId(selectedPlayerId);

    // Create mock GameModel with test data
    // Use 'unknown' casts for test data types that don't perfectly match generated types
    const mockGameModel: Partial<GameModel> = {
      tiles: EXPANSION_GAME_DATA.tiles as unknown as GameModel['tiles'],
      harbors: EXPANSION_GAME_DATA.harbors as unknown as GameModel['harbors'],
      buildings: testData.buildings as unknown as GameModel['buildings'],
      roads: testData.roads as unknown as GameModel['roads'],
      robber: testRobber as unknown as GameModel['robber'],
      players: MOCK_PLAYERS.map(p => ({
        id: p.id,
        name: p.name,
        score: 0,
        unspentEntitlements: [],
        spentThisTurn: [],
        spentThisGame: [],
      })) as unknown as GameModel['players'],
      currentPlayerId: selectedPlayerId,
      gameState: 'WaitingForNext' as GameModel['gameState'],
    };
    gameActions.setGameModel(mockGameModel as GameModel);
  }, [testData, testRobber, selectedPlayerId]);

  const _handleSendRoll = () => {
    if (die1 !== null && die2 !== null) {
      console.log(`Sending roll: ${die1} + ${die2} = ${die1 + die2}`);
      // Reset dice after sending
      setDie1(null);
      setDie2(null);
    }
  };

  return (
    <MainLayout>
      {/* Full viewport for floating panels */}
      <div className="relative w-full h-full min-h-screen">
        {/* Rolls Panel - Roll statistics with hex buttons */}
        <FloatingPanel
          panelId="dice"
          title="Rolls"
          className="bg-white/5 border-white/10"
          minWidth={280}
          minHeight={200}
        >
          <div className="w-full h-full p-2">
            <RollRingDemo colors={selectedPlayer.colors} />
          </div>
        </FloatingPanel>

        {/* Actions Panel */}
        <FloatingPanel
          panelId="actions"
          title="Actions"
          className="bg-white/5 border-white/10"
          minWidth={180}
          minHeight={200}
        >
          <div className="w-full h-full p-3">
            <ActionClusterDemo colors={selectedPlayer.colors} />
          </div>
        </FloatingPanel>

        {/* Measurement Panel - Board resource measurements */}
        <FloatingPanel
          panelId="measurements"
          title="Board Measurement"
          className="bg-white/5 border-white/10"
          minWidth={280}
          minHeight={280}
        >
          <div className="w-full h-full p-2">
            <MeasurementCluster tiles={MOCK_TILES} colors={selectedPlayer.colors} />
          </div>
        </FloatingPanel>

        {/* Players Panel - click to select current player, scales with panel size */}
        <FloatingPanel
          panelId="players"
          title="Players (click to select)"
          className="bg-white/5 border-white/10"
          minWidth={280}
          minHeight={200}
        >
          <ScaledPlayersList
            players={MOCK_PLAYERS}
            selectedPlayerId={selectedPlayerId}
            onSelectPlayer={setSelectedPlayerId}
          />
        </FloatingPanel>

        {/* Resources Panel - Total game resources */}
        <FloatingPanel
          panelId="resources"
          title="Resources"
          className="bg-white/5 border-white/10"
        >
          <GameResourcesHeader resources={MOCK_RESOURCES} />
        </FloatingPanel>

        {/* Game Board Panel - renders expansion board from test data */}
        <FloatingPanel
          panelId="board"
          title="Game Board (click buildings to assign)"
          className="border-white/10"
          style={{ background: selectedPlayer.colors.cssGradient }}
          minWidth={400}
          minHeight={350}
        >
          {/* GameBoard uses internal hooks for data - store is populated in useEffect */}
          <GameBoard
            hexSize={50}
            gap={1}
          />
        </FloatingPanel>

        {/* Minimized panels bar - fixed at bottom */}
        <MinimizedBar />

        {/* Instructions overlay */}
        <div className="absolute bottom-14 left-1/2 -translate-x-1/2 bg-gray-800/90 px-4 py-2 rounded-lg text-sm text-gray-400">
          <span className="text-amber-400">CTRL+click</span> to drag panels • Click player to change colors
        </div>
      </div>
    </MainLayout>
  );
}
