'use client';

import { GameType } from '@/types/generated/models';
import { FontAwesomeIcon } from '@fortawesome/react-fontawesome';
import {
  faChessBoard,
  faCrown,
  faShip,
  faGlobe,
  faMapLocationDot,
  faIdCard,
} from '@fortawesome/free-solid-svg-icons';

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
 * Get total dev card count.
 */
function getTotalDevCards(devCards: DevCardBreakdown): number {
  return devCards.knights + devCards.victoryPoints + devCards.monopoly + devCards.yearOfPlenty + devCards.roadBuilding;
}

/**
 * Visual card-based selector for choosing game type.
 * Displays game type options as large, interactive cards with descriptions.
 */
export function GameTypeSelector({
  value,
  onChange,
}: GameTypeSelectorProps): React.ReactElement {
  return (
    <div className="mb-6">
      <h2 className="text-xl font-semibold text-white mb-4">Choose Game Type</h2>
      <div className="grid grid-cols-1 md:grid-cols-2 gap-5">
        {GAME_TYPES.map((config) => {
          const isSelected = value === config.type && config.enabled;
          const isDisabled = !config.enabled;

          return (
            <button
              key={config.title}
              type="button"
              className={`
                relative bg-game-bg-panel rounded-2xl p-6 text-left
                transition-all duration-300 overflow-hidden
                border-2
                ${isDisabled
                  ? 'opacity-60 cursor-not-allowed'
                  : 'cursor-pointer hover:-translate-y-0.5 hover:shadow-lg hover:shadow-black/30'
                }
                ${isSelected
                  ? 'shadow-lg shadow-black/30'
                  : 'border-white/10 hover:border-white/20'
                }
              `}
              style={{
                borderColor: isSelected ? config.accentColor : undefined,
                boxShadow: isSelected ? `0 0 0 1px ${config.accentColor}` : undefined,
              }}
              onClick={() => config.enabled && onChange(config.type)}
              disabled={isDisabled}
            >
              {/* Accent bar at top */}
              <div
                className={`
                  absolute top-0 left-0 right-0 h-1
                  transition-opacity duration-300
                  ${isSelected ? 'opacity-100' : isDisabled ? 'opacity-0' : 'opacity-0 group-hover:opacity-50'}
                `}
                style={{ backgroundColor: config.accentColor }}
              />

              {/* Construction banner for disabled types */}
              {isDisabled && (
                <div className="absolute inset-0 overflow-hidden pointer-events-none z-10">
                  <span
                    className="absolute top-1/2 left-1/2 -translate-x-1/2 -translate-y-1/2 -rotate-[35deg]
                      font-bold text-sm uppercase tracking-widest py-2 px-20 whitespace-nowrap
                      shadow-lg text-black bg-amber-500"
                  >
                    Under Construction
                  </span>
                </div>
              )}

              {/* Card header with icon and player badge */}
              <div className="flex justify-between items-start mb-4">
                <div
                  className="w-12 h-12 rounded-xl bg-white/10 flex items-center justify-center text-2xl"
                  style={{ color: config.accentColor }}
                >
                  <FontAwesomeIcon icon={config.icon} />
                </div>
                <span className="text-xs px-2.5 py-1 rounded-xl bg-white/10 text-gray-400">
                  {config.players}
                </span>
              </div>

              {/* Title and description */}
              <h3 className="text-xl font-semibold text-white mb-2">{config.title}</h3>
              <p className="text-sm text-gray-500 mb-4 leading-relaxed">{config.description}</p>

              {/* Stats row */}
              <div className="flex gap-4 mb-4">
                <div className="flex items-center gap-1.5 text-sm text-gray-400">
                  <FontAwesomeIcon icon={faMapLocationDot} className="text-gray-500 text-xs" />
                  <span>{config.tiles} hex tiles</span>
                </div>
              </div>

              {/* Features list */}
              <ul className="flex flex-col gap-2 mb-4">
                {config.features.map((feature, i) => (
                  <li key={i} className="flex items-center gap-2.5 text-sm text-gray-400">
                    <span style={{ color: config.accentColor }}>•</span>
                    {feature}
                  </li>
                ))}
              </ul>

              {/* Dev cards breakdown */}
              {config.devCards.knights > 0 && (
                <div className="mt-3 pt-3 border-t border-white/10">
                  <div className="flex items-center gap-2 text-xs text-gray-500 mb-2.5">
                    <FontAwesomeIcon icon={faIdCard} className="text-sm" />
                    <span>Development Cards ({getTotalDevCards(config.devCards)})</span>
                  </div>
                  <div className="grid grid-cols-5 gap-1.5">
                    <span
                      className="flex items-center justify-center gap-1 py-1.5 px-1
                        bg-white/5 rounded-md text-sm text-gray-400"
                      title="Knight cards"
                    >
                      ⚔️ {config.devCards.knights}
                    </span>
                    <span
                      className="flex items-center justify-center gap-1 py-1.5 px-1
                        bg-white/5 rounded-md text-sm text-gray-400"
                      title="Victory Point cards"
                    >
                      🏆 {config.devCards.victoryPoints}
                    </span>
                    <span
                      className="flex items-center justify-center gap-1 py-1.5 px-1
                        bg-white/5 rounded-md text-sm text-gray-400"
                      title="Monopoly cards"
                    >
                      💰 {config.devCards.monopoly}
                    </span>
                    <span
                      className="flex items-center justify-center gap-1 py-1.5 px-1
                        bg-white/5 rounded-md text-sm text-gray-400"
                      title="Year of Plenty cards"
                    >
                      🌾 {config.devCards.yearOfPlenty}
                    </span>
                    <span
                      className="flex items-center justify-center gap-1 py-1.5 px-1
                        bg-white/5 rounded-md text-sm text-gray-400"
                      title="Road Building cards"
                    >
                      🛤️ {config.devCards.roadBuilding}
                    </span>
                  </div>
                </div>
              )}

            </button>
          );
        })}
      </div>
    </div>
  );
}
