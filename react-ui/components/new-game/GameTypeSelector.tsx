'use client';

import React from 'react';
import { GameType } from '@/types/generated/models';
import { FontAwesomeIcon } from '@fortawesome/react-fontawesome';
import {
  faChessBoard,
  faCrown,
  faShip,
  faGlobe,
  faMapLocationDot,
  faDice,
} from '@fortawesome/free-solid-svg-icons';
import { HexGrid, HexGridItem, HEX_LAYOUTS, CenterHex, WaterHex } from '@/components/hex-grid';

/**
 * Props for the GameTypeSelector component.
 */
interface GameTypeSelectorProps {
  /** Currently selected game type */
  value: GameType;
  /** Callback when game type changes */
  onChange: (gameType: GameType) => void;
}

/**
 * Development card breakdown for a game type.
 */
interface DevCardBreakdown {
  knights: number;
  victoryPoints: number;
  monopoly: number;
  yearOfPlenty: number;
  roadBuilding: number;
}

/**
 * Game type configuration for display.
 */
interface GameTypeConfig {
  type: GameType;
  title: string;
  description: string;
  players: string;
  tiles: number;
  features: string[];
  devCards: DevCardBreakdown;
  icon: typeof faChessBoard;
  accentColor: string;
  enabled: boolean;
}

const GAME_TYPES: GameTypeConfig[] = [
  {
    type: 'Regular',
    title: 'Classic Catan',
    description: 'The original Settlers of Catan experience',
    players: '3-4 players',
    tiles: 19,
    features: ['Standard rules', 'Quick setup'],
    devCards: { knights: 14, victoryPoints: 5, monopoly: 2, yearOfPlenty: 2, roadBuilding: 2 },
    icon: faChessBoard,
    accentColor: '#E9C46A',
    enabled: true,
  },
  {
    type: 'Expansion',
    title: '5-6 Player Expansion',
    description: 'Play with more friends using the expansion board',
    players: '5-6 players',
    tiles: 30,
    features: ['Larger board', 'Special build phase'],
    devCards: { knights: 20, victoryPoints: 5, monopoly: 3, yearOfPlenty: 3, roadBuilding: 3 },
    icon: faGlobe,
    accentColor: '#06D6A0',
    enabled: true,
  },
  {
    type: 'Unset' as GameType, // Placeholder - Cities & Knights not yet implemented
    title: 'Cities & Knights',
    description: 'Enhanced gameplay with knights and city upgrades',
    players: '3-6 players',
    tiles: 30,
    features: ['Knights & barbarians', 'City improvements', 'Progress cards'],
    devCards: { knights: 0, victoryPoints: 0, monopoly: 0, yearOfPlenty: 0, roadBuilding: 0 },
    icon: faCrown,
    accentColor: '#9B5DE5',
    enabled: false,
  },
  {
    type: 'SavedGame' as GameType, // Placeholder - Seafarers not yet implemented
    title: 'Seafarers',
    description: 'Explore the seas with ships and islands',
    players: '3-4 players',
    tiles: 19,
    features: ['Ships & sea routes', 'Island discovery'],
    devCards: { knights: 14, victoryPoints: 5, monopoly: 2, yearOfPlenty: 2, roadBuilding: 2 },
    icon: faShip,
    accentColor: '#00B4D8',
    enabled: false,
  },
];

/**
 * Game type hex content component.
 */
interface GameTypeContentProps {
  config: GameTypeConfig;
  isSelected: boolean;
}

function GameTypeContent({ config, isSelected }: GameTypeContentProps): React.ReactElement {
  const isDisabled = !config.enabled;
  const [isHovered, setIsHovered] = React.useState(false);

  return (
    <div
      className="w-full h-full group"
      onMouseEnter={() => !isDisabled && setIsHovered(true)}
      onMouseLeave={() => setIsHovered(false)}
    >
      {/* Outer hex - border (full size) */}
      <div
        className={`absolute inset-0 hex-clip-flat transition-colors duration-200 ${
          isSelected ? 'bg-green-500' : isHovered && !isDisabled ? 'bg-blue-500' : 'bg-white/30'
        }`}
      />

      {/* Inner hex - content (91% scale to create border gap) */}
      <div
        className={`
          absolute inset-0 flex flex-col items-center justify-center
          hex-clip-flat
          transition-all duration-300 overflow-hidden
          ${isDisabled ? 'opacity-60 cursor-not-allowed' : 'cursor-pointer'}
        `}
        style={{
          background: 'linear-gradient(135deg, #2a2a2a, #1a1a1a)',
          transform: 'scale(0.91)',
        }}
      >
        {/* Construction banner for disabled types */}
        {isDisabled && (
          <div className="absolute inset-0 overflow-hidden pointer-events-none z-10">
            <span
              className="absolute top-1/2 left-1/2 -translate-x-1/2 -translate-y-1/2 -rotate-[35deg]
                font-bold text-xs uppercase tracking-widest py-2 px-12 whitespace-nowrap
                shadow-lg text-black bg-amber-500"
            >
              Coming Soon
            </span>
          </div>
        )}

        <div className="flex flex-col items-center justify-center gap-2.5 px-3">
          {/* Icon */}
          <div
            className="w-12 h-12 rounded-lg bg-white/10 flex items-center justify-center text-2xl"
            style={{ color: config.accentColor }}
          >
            <FontAwesomeIcon icon={config.icon} />
          </div>

          {/* Title */}
          <h3 className="text-base font-bold text-white text-center leading-tight">{config.title}</h3>

          {/* Players badge */}
          <span className="text-xs px-2.5 py-1 rounded-lg bg-white/10 text-gray-400">
            {config.players}
          </span>

          {/* Tiles count */}
          <div className="flex items-center gap-1.5 text-xs text-gray-400">
            <FontAwesomeIcon icon={faMapLocationDot} className="text-xs" />
            <span>{config.tiles} tiles</span>
          </div>
        </div>
      </div>
    </div>
  );
}

/**
 * Visual hex-grid selector for choosing game type.
 *
 * Uses HexGrid component with proper Red Blob Games formulas.
 * Center hex with "Choose Game" surrounded by 6 hexes:
 * - 4 game type hexes (Regular, Expansion, Cities & Knights, Seafarers)
 * - 2 water placeholder hexes (face down tiles)
 */
export function GameTypeSelector({
  value,
  onChange,
}: GameTypeSelectorProps): React.ReactElement {
  // Hex size: 100 to fit all content (icon, title, players, tiles) without clipping
  const hexSize = 100;

  // Build hex grid items
  const items: HexGridItem[] = [
    // Center hex - "Choose Game" label
    {
      id: 'center',
      coord: HEX_LAYOUTS.CLUSTER_7[0], // Center (0, 0)
      content: (
        <CenterHex
          icon={faDice}
          title="Choose"
          subtitle="Game"
          accentColor="text-amber-400"
          background="linear-gradient(160deg, rgba(120, 53, 15, 0.4) 0%, rgba(69, 26, 3, 0.4) 100%)"
        />
      ),
      disabled: true,
    },

    // North: Expansion
    {
      id: 'expansion',
      coord: HEX_LAYOUTS.CLUSTER_7[1], // North (0, -1)
      content: (
        <GameTypeContent
          config={GAME_TYPES[1]}
          isSelected={value === GAME_TYPES[1].type && GAME_TYPES[1].enabled}
        />
      ),
      onClick: () => GAME_TYPES[1].enabled && onChange(GAME_TYPES[1].type),
      disabled: !GAME_TYPES[1].enabled,
    },

    // NorthEast: Seafarers
    {
      id: 'seafarers',
      coord: HEX_LAYOUTS.CLUSTER_7[2], // NorthEast (1, -1)
      content: (
        <GameTypeContent
          config={GAME_TYPES[3]}
          isSelected={value === GAME_TYPES[3].type && GAME_TYPES[3].enabled}
        />
      ),
      onClick: () => GAME_TYPES[3].enabled && onChange(GAME_TYPES[3].type),
      disabled: !GAME_TYPES[3].enabled,
    },

    // SouthEast: Water placeholder
    {
      id: 'water-se',
      coord: HEX_LAYOUTS.CLUSTER_7[3], // SouthEast (1, 0)
      content: <WaterHex imageUrl="/water.png" showBorder opacity={0.6} />,
      disabled: true,
    },

    // South: Water placeholder
    {
      id: 'water-s',
      coord: HEX_LAYOUTS.CLUSTER_7[4], // South (0, 1)
      content: <WaterHex imageUrl="/water.png" showBorder opacity={0.6} />,
      disabled: true,
    },

    // SouthWest: Cities & Knights
    {
      id: 'cities-knights',
      coord: HEX_LAYOUTS.CLUSTER_7[5], // SouthWest (-1, 1)
      content: (
        <GameTypeContent
          config={GAME_TYPES[2]}
          isSelected={value === GAME_TYPES[2].type && GAME_TYPES[2].enabled}
        />
      ),
      onClick: () => GAME_TYPES[2].enabled && onChange(GAME_TYPES[2].type),
      disabled: !GAME_TYPES[2].enabled,
    },

    // NorthWest: Regular/Classic
    {
      id: 'regular',
      coord: HEX_LAYOUTS.CLUSTER_7[6], // NorthWest (-1, 0)
      content: (
        <GameTypeContent
          config={GAME_TYPES[0]}
          isSelected={value === GAME_TYPES[0].type && GAME_TYPES[0].enabled}
        />
      ),
      onClick: () => GAME_TYPES[0].enabled && onChange(GAME_TYPES[0].type),
      disabled: !GAME_TYPES[0].enabled,
    },
  ];

  return (
    <HexGrid hexSize={hexSize} items={items} scale={1.0} />
  );
}
