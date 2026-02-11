'use client';

import React, { useMemo } from 'react';
import Image from 'next/image';
import { FontAwesomeIcon } from '@fortawesome/react-fontawesome';
import { faUser, faUserPlus, faTrophy, faCheck } from '@fortawesome/free-solid-svg-icons';
import { HexGrid, HexGridItem, HEX_LAYOUTS, cubicCoord, CenterHex } from '@/components/hex-grid';
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
  return player.lifetimeStats?.wins ?? 0;
}

/**
 * Player card content for hex grid.
 *
 * IMPORTANT: Always shows player gradient colors (matching Blazor rendering).
 * Selection state changes border style, NOT background color.
 */
interface PlayerCardContentProps {
  player: PlayerProfile;
  isSelected: boolean;
}

function PlayerCardContent({ player, isSelected }: PlayerCardContentProps): React.ReactElement {
  const imageUrl = getPlayerImageUrl(player.imageUri);
  const wins = getWinCount(player);
  const [isHovered, setIsHovered] = React.useState(false);

  return (
    <div
      className="w-full h-full"
      onMouseEnter={() => setIsHovered(true)}
      onMouseLeave={() => setIsHovered(false)}
    >
      {/* Outer hex - border (hover always shows blue, selection uses checkmark not border) */}
      <div
        className="absolute inset-0 hex-clip-flat transition-colors duration-200"
        style={{
          background: isHovered ? '#3b82f6' : 'rgba(255,255,255,0.3)',
        }}
      />

      {/* Inner hex - content */}
      <div
        className="absolute inset-0 flex flex-col items-center justify-center hex-clip-flat"
        style={{
          background: `linear-gradient(160deg, ${player.colors.primary} 0%, ${player.colors.secondary} 70%, rgba(0,0,0,0.3) 100%)`,
          transform: 'scale(0.91)',
        }}
      >
        <div className="flex flex-col items-center justify-center gap-2 px-3">
          {/* Avatar - circular with white border (matching Blazor) */}
          <div className="w-14 h-14 rounded-full overflow-hidden flex items-center justify-center bg-black/20 border-2 border-white">
            {imageUrl ? (
              <Image src={imageUrl} alt={player.name} width={56} height={56} unoptimized />
            ) : (
              <FontAwesomeIcon
                icon={faUser}
                className="text-2xl"
                style={{ color: player.colors.foreground }}
              />
            )}
          </div>

          {/* Name - always in foreground color */}
          <span
            className="text-xs font-bold truncate max-w-full text-center"
            style={{ color: player.colors.foreground }}
          >
            {player.name}
          </span>

          {/* Trophy badge - wins count */}
          {wins > 0 && (
            <span className="absolute bottom-2 flex items-center gap-1 text-[10px] px-1.5 py-0.5 rounded bg-black/60 text-amber-400 font-semibold">
              <FontAwesomeIcon icon={faTrophy} className="text-[8px]" />
              {wins}
            </span>
          )}
        </div>
      </div>

      {/* Selection checkmark - upper-right area, inside hex polygon boundary */}
      {isSelected && (
        <div
          className="absolute z-10 w-6 h-6 rounded-full flex items-center justify-center bg-black/50 border border-white/50 -translate-x-1/2 -translate-y-1/2"
          style={{ top: '16%', left: '68%' }}
        >
          <FontAwesomeIcon icon={faCheck} className="text-xs text-white" />
        </div>
      )}
    </div>
  );
}

/**
 * Modern player selection interface with hex grid layout.
 *
 * Uses HexGrid component with proper Red Blob Games formulas.
 * Center hex with "Choose Players" surrounded by up to 6 player hexes.
 * Selected players shown as draggable chips above the grid.
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
  const { min, max } = getPlayerLimits(gameType);

  // Non-guest players sorted alphabetically
  const sortedPlayers = useMemo(() => {
    return [...availablePlayers]
      .filter((p) => p.name.toLowerCase() !== 'guest')
      .sort((a, b) => a.name.localeCompare(b.name));
  }, [availablePlayers]);

  // Find the Guest player (if it exists in the database)
  const guestPlayer = useMemo(
    () => availablePlayers.find((p) => p.name.toLowerCase() === 'guest'),
    [availablePlayers]
  );

  const handleTogglePlayer = (playerId: string): void => {
    if (selectedPlayerIds.includes(playerId)) {
      onChange(selectedPlayerIds.filter((id) => id !== playerId));
    } else if (selectedPlayerIds.length < max) {
      onChange([...selectedPlayerIds, playerId]);
    } else {
      // Circular: remove oldest selected, add new one
      onChange([...selectedPlayerIds.slice(1), playerId]);
    }
  };

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

  const needsMore = selectedPlayerIds.length < min;

  // Hex size: 100 to match GameTypeSelector
  const hexSize = 100;

  // Visible players (up to 6 surrounding the center hex)
  const visiblePlayers = sortedPlayers.slice(0, 6);

  // Build hex grid items
  const items: HexGridItem[] = [
    // Center hex - "Choose Players" label
    {
      id: 'center',
      coord: HEX_LAYOUTS.CLUSTER_7[0], // Center (0, 0)
      content: (
        <CenterHex
          icon={faUser}
          title="Choose"
          subtitle="Players"
          accentColor="text-blue-400"
          background="linear-gradient(160deg, rgba(30, 58, 138, 0.4) 0%, rgba(23, 37, 84, 0.4) 100%)"
        />
      ),
      disabled: true,
    },

    // Surrounding player hexes (up to 6 positions)
    ...visiblePlayers.map((player, idx) => ({
      id: player.id,
      coord: HEX_LAYOUTS.CLUSTER_7[idx + 1], // Positions 1-6 (surrounding)
      content: (
        <PlayerCardContent player={player} isSelected={selectedPlayerIds.includes(player.id)} />
      ),
      onClick: () => handleTogglePlayer(player.id),
    })),

    // Guest hex - shown below the cluster when "Include Guest" is checked
    // Position (0, 2) is directly below the south hex, maintaining horizontal centering
    ...(includeGuest && guestPlayer
      ? [
          {
            id: guestPlayer.id,
            coord: cubicCoord(0, 2), // Below south hex
            content: (
              <PlayerCardContent
                player={guestPlayer}
                isSelected={selectedPlayerIds.includes(guestPlayer.id)}
              />
            ),
            onClick: () => handleTogglePlayer(guestPlayer.id),
          },
        ]
      : []),
  ];

  return (
    <div className="flex flex-col">
      {/* Hex Grid Layout - grows vertically when Guest is added */}
      <HexGrid hexSize={hexSize} items={items} />

      {/* Guest toggle + validation */}
      <div className="flex items-center justify-between mt-2 px-1">
        {guestPlayer && onIncludeGuestChange ? (
          <label className="flex items-center gap-2 px-2 py-1 rounded bg-white/10 cursor-pointer hover:bg-white/15 transition-colors">
            <input
              type="checkbox"
              checked={includeGuest}
              onChange={(e) => {
                const checked = e.target.checked;
                onIncludeGuestChange(checked);
                // Deselect guest when unchecking
                if (!checked && guestPlayer && selectedPlayerIds.includes(guestPlayer.id)) {
                  onChange(selectedPlayerIds.filter((id) => id !== guestPlayer.id));
                }
              }}
              className="w-3 h-3 rounded accent-blue-500"
            />
            <span className="text-xs text-gray-300">Include Guest</span>
          </label>
        ) : (
          <div />
        )}
        {needsMore && <p className="text-amber-400 text-xs">Select at least {min} players</p>}
      </div>
    </div>
  );
}
