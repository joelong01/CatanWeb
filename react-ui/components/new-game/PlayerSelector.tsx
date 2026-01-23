'use client';

import { useState, useCallback, useMemo } from 'react';
import Image from 'next/image';
import { FontAwesomeIcon } from '@fortawesome/react-fontawesome';
import {
  faGripVertical,
  faUser,
  faUserPlus,
  faTrophy,
  faCheck,
} from '@fortawesome/free-solid-svg-icons';
import { serviceConfig } from '@/lib/services/config';
import type { PlayerProfile } from '@/types/player-profile';
import type { GameType } from '@/types/generated/models';

/**
 * Props for the PlayerSelector component.
 */
interface PlayerSelectorProps {
  /** Available players to choose from */
  availablePlayers: PlayerProfile[];
  /** Currently selected player IDs in order */
  selectedPlayerIds: string[];
  /** Callback when selection changes */
  onChange: (playerIds: string[]) => void;
  /** Current game type (affects min/max players) */
  gameType: GameType;
  /** Whether players are loading */
  loading?: boolean;
  /** Error message if player fetch failed */
  error?: string;
  /** Whether to include guest player option */
  includeGuest?: boolean;
  /** Callback when includeGuest changes */
  onIncludeGuestChange?: (include: boolean) => void;
}

/**
 * Get min/max players for a game type.
 */
function getPlayerLimits(gameType: GameType): { min: number; max: number } {
  return gameType === 'Expansion' ? { min: 3, max: 6 } : { min: 3, max: 4 };
}

/**
 * Get the full image URL for a player.
 */
function getPlayerImageUrl(imageUri: string | undefined): string | null {
  if (!imageUri) return null;
  if (imageUri.startsWith('http')) return imageUri;
  return `${serviceConfig.serviceUrl}${imageUri}`;
}

/**
 * Get win count from player's lifetime stats.
 */
function getWinCount(player: PlayerProfile): number {
  return player.lifetimeStats?.totals?.gamesWon ?? 0;
}

/**
 * Selected player chip - compact draggable representation in turn order.
 */
interface SelectedPlayerChipProps {
  index: number;
  player: PlayerProfile;
  onDragStart: (index: number) => void;
  onDragOver: (index: number) => void;
  onDragEnd: () => void;
  isDragging: boolean;
  isDragOver: boolean;
}

function SelectedPlayerChip({
  index,
  player,
  onDragStart,
  onDragOver,
  onDragEnd,
  isDragging,
  isDragOver,
}: SelectedPlayerChipProps): React.ReactElement {
  const imageUrl = getPlayerImageUrl(player.imageUri);

  return (
    <div
      className={`
        flex items-center gap-2 px-3 py-2 rounded-lg cursor-grab active:cursor-grabbing
        transition-all duration-200 select-none
        ${isDragging ? 'opacity-50 scale-95' : ''}
        ${isDragOver ? 'ring-2 ring-blue-500 ring-offset-2 ring-offset-gray-900' : ''}
      `}
      draggable
      onDragStart={() => onDragStart(index)}
      onDragOver={(e) => {
        e.preventDefault();
        onDragOver(index);
      }}
      onDragEnd={onDragEnd}
      style={{
        background: `linear-gradient(135deg, ${player.colors.primary}, ${player.colors.secondary})`,
        color: player.colors.foreground,
      }}
    >
      <FontAwesomeIcon icon={faGripVertical} className="opacity-60 text-xs" />
      <span className="font-bold text-sm">P{index + 1}</span>
      <div className="w-6 h-6 rounded-full overflow-hidden bg-black/20 flex items-center justify-center">
        {imageUrl ? (
          <Image src={imageUrl} alt={player.name} width={24} height={24} unoptimized />
        ) : (
          <FontAwesomeIcon icon={faUser} className="text-xs" />
        )}
      </div>
      <span className="font-medium text-sm">{player.name}</span>
    </div>
  );
}

/**
 * Available player card - clickable card to add/remove from game.
 */
interface PlayerCardProps {
  player: PlayerProfile;
  isSelected: boolean;
  onToggle: () => void;
  disabled: boolean;
}

function PlayerCard({
  player,
  isSelected,
  onToggle,
  disabled,
}: PlayerCardProps): React.ReactElement {
  const imageUrl = getPlayerImageUrl(player.imageUri);
  const wins = getWinCount(player);

  return (
    <button
      type="button"
      onClick={onToggle}
      disabled={disabled && !isSelected}
      className={`
        relative flex flex-col items-center gap-2 p-3 rounded-xl
        transition-all duration-200 border-2
        ${isSelected
          ? 'border-green-500 bg-green-500/10'
          : 'border-white/10 bg-white/5 hover:bg-white/10 hover:border-white/20'
        }
        ${disabled && !isSelected ? 'opacity-40 cursor-not-allowed' : 'cursor-pointer'}
      `}
    >
      {/* Avatar with selection badge */}
      <div className="relative">
        <div
          className="w-12 h-12 rounded-full overflow-hidden flex items-center justify-center"
          style={{
            background: `linear-gradient(135deg, ${player.colors.primary}, ${player.colors.secondary})`,
            color: player.colors.foreground,
          }}
        >
          {imageUrl ? (
            <Image src={imageUrl} alt={player.name} width={48} height={48} unoptimized />
          ) : (
            <FontAwesomeIcon icon={faUser} className="text-xl" />
          )}
        </div>
        {isSelected && (
          <div className="absolute -bottom-1 -right-1 w-5 h-5 bg-green-500 rounded-full flex items-center justify-center">
            <FontAwesomeIcon icon={faCheck} className="text-white text-xs" />
          </div>
        )}
      </div>

      {/* Name */}
      <span className="text-sm font-medium text-white truncate max-w-full">
        {player.name}
      </span>

      {/* Wins badge - positioned in lower right corner */}
      {wins > 0 && (
        <span className="absolute bottom-1.5 right-1.5 flex items-center gap-1 text-xs text-yellow-400 bg-black/50 px-1.5 py-0.5 rounded">
          <FontAwesomeIcon icon={faTrophy} className="text-[10px]" />
          {wins}
        </span>
      )}
    </button>
  );
}

/**
 * Modern player selection interface with drag-and-drop reordering.
 * Shows all players in a grid, selected players shown as draggable chips.
 */
export function PlayerSelector({
  availablePlayers,
  selectedPlayerIds,
  onChange,
  gameType,
  loading,
  error,
  includeGuest = false,
  onIncludeGuestChange,
}: PlayerSelectorProps): React.ReactElement {
  const [dragIndex, setDragIndex] = useState<number | null>(null);
  const [dragOverIndex, setDragOverIndex] = useState<number | null>(null);

  const { min, max } = getPlayerLimits(gameType);

  // Sort players: Guest last, others alphabetically by name
  const sortedPlayers = useMemo(() => {
    const sorted = [...availablePlayers].sort((a, b) => {
      if (a.name.toLowerCase() === 'guest') return 1;
      if (b.name.toLowerCase() === 'guest') return -1;
      return a.name.localeCompare(b.name);
    });

    if (!includeGuest) {
      return sorted.filter((p) => p.name.toLowerCase() !== 'guest');
    }
    return sorted;
  }, [availablePlayers, includeGuest]);

  // Check if Guest exists in the available players
  const hasGuest = useMemo(
    () => availablePlayers.some((p) => p.name.toLowerCase() === 'guest'),
    [availablePlayers]
  );

  // Get selected players in order
  const selectedPlayers = useMemo(() => {
    return selectedPlayerIds
      .map((id) => sortedPlayers.find((p) => p.id === id))
      .filter((p): p is PlayerProfile => p !== undefined);
  }, [selectedPlayerIds, sortedPlayers]);

  const handleTogglePlayer = useCallback(
    (playerId: string) => {
      if (selectedPlayerIds.includes(playerId)) {
        // Remove player (only if above minimum)
        if (selectedPlayerIds.length > min) {
          onChange(selectedPlayerIds.filter((id) => id !== playerId));
        }
      } else {
        // Add player (only if below maximum)
        if (selectedPlayerIds.length < max) {
          onChange([...selectedPlayerIds, playerId]);
        }
      }
    },
    [selectedPlayerIds, onChange, min, max]
  );

  const handleDragStart = useCallback((index: number) => {
    setDragIndex(index);
  }, []);

  const handleDragOver = useCallback((index: number) => {
    setDragOverIndex(index);
  }, []);

  const handleDragEnd = useCallback(() => {
    if (dragIndex !== null && dragOverIndex !== null && dragIndex !== dragOverIndex) {
      const newIds = [...selectedPlayerIds];
      const [removed] = newIds.splice(dragIndex, 1);
      newIds.splice(dragOverIndex, 0, removed);
      onChange(newIds);
    }
    setDragIndex(null);
    setDragOverIndex(null);
  }, [dragIndex, dragOverIndex, selectedPlayerIds, onChange]);

  // Loading state
  if (loading) {
    return (
      <div className="space-y-4">
        <h2 className="text-xl font-semibold text-white">Select Players</h2>
        <div className="flex flex-col items-center justify-center p-8 bg-gray-800/50 rounded-xl">
          <div className="w-8 h-8 border-2 border-blue-500 border-t-transparent rounded-full animate-spin" />
          <p className="mt-4 text-gray-400">Loading players...</p>
        </div>
      </div>
    );
  }

  // Error state
  if (error) {
    return (
      <div className="space-y-4">
        <h2 className="text-xl font-semibold text-white">Select Players</h2>
        <div className="p-8 bg-red-500/10 border border-red-500/50 rounded-xl text-center">
          <p className="text-red-400">{error}</p>
        </div>
      </div>
    );
  }

  // Empty state
  if (availablePlayers.length === 0) {
    return (
      <div className="space-y-4">
        <h2 className="text-xl font-semibold text-white">Select Players</h2>
        <div className="flex flex-col items-center justify-center p-8 bg-gray-800/50 rounded-xl">
          <FontAwesomeIcon icon={faUserPlus} className="text-4xl text-gray-500 mb-4" />
          <p className="text-gray-400">No players available.</p>
          <p className="text-sm text-gray-500">Create players in the Edit Players page first.</p>
        </div>
      </div>
    );
  }

  const canAddMore = selectedPlayerIds.length < max;
  const needsMore = selectedPlayerIds.length < min;

  return (
    <div className="space-y-6">
      {/* Header */}
      <div className="flex items-start justify-between gap-4">
        <div>
          <h2 className="text-xl font-semibold text-white">Select Players</h2>
          <p className="text-sm text-gray-400 mt-1">
            {min}-{max} players · Click to select, drag to reorder
          </p>
        </div>
        {hasGuest && onIncludeGuestChange && (
          <label className="flex items-center gap-2 px-3 py-2 bg-white/5 rounded-lg cursor-pointer hover:bg-white/10 transition-colors">
            <input
              type="checkbox"
              checked={includeGuest}
              onChange={(e) => onIncludeGuestChange(e.target.checked)}
              className="w-4 h-4 rounded accent-blue-500"
            />
            <span className="text-sm text-gray-300">Include Guest</span>
          </label>
        )}
      </div>

      {/* Turn Order - Selected Players (horizontal chips) */}
      {selectedPlayers.length > 0 && (
        <div className="space-y-2">
          <h3 className="text-sm font-medium text-gray-400 uppercase tracking-wide">
            Turn Order
          </h3>
          <div className="flex flex-wrap gap-2">
            {selectedPlayers.map((player, index) => (
              <SelectedPlayerChip
                key={player.id}
                index={index}
                player={player}
                onDragStart={handleDragStart}
                onDragOver={handleDragOver}
                onDragEnd={handleDragEnd}
                isDragging={dragIndex === index}
                isDragOver={dragOverIndex === index && dragIndex !== index}
              />
            ))}
          </div>
        </div>
      )}

      {/* All Players Grid */}
      <div className="space-y-2">
        <h3 className="text-sm font-medium text-gray-400 uppercase tracking-wide">
          Available Players ({selectedPlayerIds.length}/{max} selected)
        </h3>
        <div className="grid grid-cols-3 sm:grid-cols-4 md:grid-cols-5 lg:grid-cols-6 gap-3">
          {sortedPlayers.map((player) => (
            <PlayerCard
              key={player.id}
              player={player}
              isSelected={selectedPlayerIds.includes(player.id)}
              onToggle={() => handleTogglePlayer(player.id)}
              disabled={!canAddMore}
            />
          ))}
        </div>
      </div>

      {/* Validation message */}
      {needsMore && (
        <p className="text-center text-amber-400 text-sm">
          Select at least {min} players to start
        </p>
      )}
    </div>
  );
}
