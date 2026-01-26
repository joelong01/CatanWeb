'use client';

/**
 * PlayersPanel - Shows player cards with stats and resources.
 *
 * Displays each player's score, resources this turn, and game statistics.
 * Current player is highlighted with their color gradient.
 */

import { memo } from 'react';
import type { PlayerModel } from '@/types/generated/models/player-model';
import type { GameModel } from '@/types/generated/models/game-model';

interface PlayersPanelProps {
  /** Game model containing players and current player */
  gameModel: GameModel | null;
}

/** Player color palette - maps player index to colors */
const PLAYER_COLORS = [
  { primary: '#ef4444', gradient: 'from-red-600 to-red-800' },      // Red
  { primary: '#3b82f6', gradient: 'from-blue-600 to-blue-800' },    // Blue
  { primary: '#f97316', gradient: 'from-orange-500 to-orange-700' }, // Orange
  { primary: '#22c55e', gradient: 'from-green-600 to-green-800' },   // Green
  { primary: '#a855f7', gradient: 'from-purple-600 to-purple-800' }, // Purple
  { primary: '#eab308', gradient: 'from-yellow-500 to-yellow-700' }, // Yellow
];

/** Get player color by index */
function getPlayerColor(index: number) {
  return PLAYER_COLORS[index % PLAYER_COLORS.length];
}

/** Resource icons using simple text/emoji */
const RESOURCE_ICONS: Record<string, string> = {
  wheat: '🌾',
  wood: '🪵',
  sheep: '🐑',
  brick: '🧱',
  ore: '�ite',
  goldMine: '✨',
  robber: '👤',
};

interface PlayerCardProps {
  player: PlayerModel;
  isCurrentPlayer: boolean;
  playerIndex: number;
  gameModel: GameModel;
}

/** Single player card */
const PlayerCard = memo(function PlayerCard({
  player,
  isCurrentPlayer,
  playerIndex,
  gameModel,
}: PlayerCardProps) {
  const colors = getPlayerColor(playerIndex);

  // Count player's roads, settlements, cities from game model
  const roadCount = gameModel.roads.filter(r => r.ownerId === player.id).length;
  const settlements = gameModel.buildings.filter(
    b => b.ownerId === player.id && b.buildingState === 'Settlement'
  ).length;
  const cities = gameModel.buildings.filter(
    b => b.ownerId === player.id && b.buildingState === 'City'
  ).length;

  // Count soldiers from spent entitlements
  const soldierCount = player.spentEntitlementsThisGame.filter(
    e => e === 'Soldier'
  ).length;

  return (
    <div
      className={`
        rounded-lg overflow-hidden border-2 transition-all
        ${isCurrentPlayer ? 'border-amber-400 shadow-lg' : 'border-gray-700'}
      `}
    >
      {/* Header with player name and score */}
      <div
        className={`
          px-3 py-2 flex items-center justify-between
          bg-gradient-to-r ${colors.gradient}
        `}
      >
        <div className="flex items-center gap-2">
          {/* Avatar placeholder */}
          <div
            className="w-8 h-8 rounded-full flex items-center justify-center text-white font-bold text-sm"
            style={{ backgroundColor: colors.primary }}
          >
            {player.name.charAt(0).toUpperCase()}
          </div>
          <span className="text-white font-medium">{player.name}</span>
        </div>
        <div className="flex items-center gap-1">
          <span className="text-yellow-300 text-lg font-bold">{player.score}</span>
          {player.highestScore && (
            <span className="text-yellow-300" title="Highest Score">👑</span>
          )}
        </div>
      </div>

      {/* Stats grid */}
      <div className="bg-gray-800/80 px-3 py-2">
        <div className="grid grid-cols-4 gap-2 text-xs">
          <StatItem label="Roads" value={roadCount} />
          <StatItem label="Settlements" value={settlements} />
          <StatItem label="Cities" value={cities} />
          <StatItem label="Soldiers" value={soldierCount} highlight={player.largestArmy} />
        </div>

        {/* Longest road indicator */}
        <div className="mt-2 grid grid-cols-2 gap-2 text-xs">
          <StatItem
            label="Longest Road"
            value={player.longestRoad}
            highlight={player.hasLongestRoad}
          />
          <StatItem label="Stars" value={player.stars} />
        </div>

        {/* Resources this turn */}
        <div className="mt-2 pt-2 border-t border-gray-700">
          <div className="text-xs text-gray-400 mb-1">Resources This Turn</div>
          <div className="flex flex-wrap gap-1">
            <ResourceBadge type="wheat" count={player.resourcesThisTurn.wheat} />
            <ResourceBadge type="wood" count={player.resourcesThisTurn.wood} />
            <ResourceBadge type="sheep" count={player.resourcesThisTurn.sheep} />
            <ResourceBadge type="brick" count={player.resourcesThisTurn.brick} />
            <ResourceBadge type="ore" count={player.resourcesThisTurn.ore} />
            {player.resourcesThisTurn.goldMine > 0 && (
              <ResourceBadge type="goldMine" count={player.resourcesThisTurn.goldMine} />
            )}
            {player.resourcesThisTurn.robber > 0 && (
              <ResourceBadge type="robber" count={player.resourcesThisTurn.robber} />
            )}
          </div>
        </div>
      </div>
    </div>
  );
});

/** Stat item component */
function StatItem({
  label,
  value,
  highlight = false,
}: {
  label: string;
  value: number;
  highlight?: boolean;
}) {
  return (
    <div className={`text-center ${highlight ? 'text-amber-400' : 'text-gray-300'}`}>
      <div className="font-bold">{value}</div>
      <div className="text-gray-500 text-[10px]">{label}</div>
    </div>
  );
}

/** Resource badge component */
function ResourceBadge({ type, count }: { type: string; count: number }) {
  if (count === 0) return null;

  return (
    <div className="flex items-center gap-1 bg-gray-700/50 rounded px-1.5 py-0.5">
      <span className="text-xs">{RESOURCE_ICONS[type] || '?'}</span>
      <span className="text-xs text-gray-300">{count}</span>
    </div>
  );
}

/** Main PlayersPanel component */
export function PlayersPanel({ gameModel }: PlayersPanelProps): React.ReactElement {
  if (!gameModel) {
    return (
      <div className="p-4 text-gray-400 text-center">
        Waiting for game data...
      </div>
    );
  }

  return (
    <div className="p-2 space-y-2">
      {gameModel.players.map((player, index) => (
        <PlayerCard
          key={player.id}
          player={player}
          isCurrentPlayer={player.id === gameModel.currentPlayerId}
          playerIndex={index}
          gameModel={gameModel}
        />
      ))}
    </div>
  );
}

export default PlayersPanel;
