'use client';

/**
 * SupplementalOverlay - Player selection overlay for PickSupplementalPlayers state.
 *
 * Displays a HexGrid ring with player avatars for multi-select participation.
 * Click a player to toggle their participation in the supplemental build phase.
 * Press Enter or click the center "Next" hex to confirm and proceed.
 *
 * Wrapped in FloatingPanel for draggable/resizable behavior.
 */

import { useMemo, useState, useCallback, useEffect } from 'react';
import { HexGrid, type HexGridItem } from '@/components/hex-grid/HexGrid';
import { HEX_LAYOUTS, cubicCoord } from '@/components/hex-grid/hex-geometry';
import { serviceConfig } from '@/lib/config';
import { buildCssGradient } from '@/lib/utils/playerColors';
import type { PlayerModel } from '@/types/generated/models/player-model';
import type { PlayerProfile } from '@/types/player-profile';
import { DEFAULT_PLAYER_COLORS } from '@/types/player-profile';

/**
 * Build full image URL from profile imageUri.
 * Returns null if no imageUri is provided.
 */
function getPlayerImageUrl(imageUri: string | undefined): string | null {
  if (!imageUri) return null;
  if (imageUri.startsWith('http')) return imageUri;
  return `${serviceConfig.serviceUrl}${imageUri}`;
}

export interface SupplementalOverlayProps {
  /** Players to display (from GameModel) - excludes current player */
  players: PlayerModel[];
  /** Current player ID (excluded from selection) */
  currentPlayerId: string;
  /** Player profiles map (for colors and avatars) */
  playerProfiles: Map<string, PlayerProfile>;
  /** Callback when a player's participation is toggled - sends immediately like Blazor */
  onToggle: (playerId: string, participating: boolean) => void;
  /** Callback when Done/Next is clicked - just advances the game state */
  onDone: () => void;
}

/**
 * Content for the center hex showing the "Next" button.
 * Uses absolute positioning to fill the hex tile.
 */
function NextButtonContent({
  onClick,
  isHovered,
  setIsHovered,
  isPressed,
  setIsPressed,
}: {
  onClick: () => void;
  isHovered: boolean;
  setIsHovered: (h: boolean) => void;
  isPressed: boolean;
  setIsPressed: (p: boolean) => void;
}): React.ReactElement {
  const innerScale = isPressed ? 0.88 : isHovered ? 0.90 : 0.92;
  const bgColor = isPressed
    ? 'linear-gradient(135deg, #1e40af, #1e3a8a)'
    : isHovered
      ? 'linear-gradient(135deg, #3b82f6, #2563eb)'
      : 'linear-gradient(135deg, #2563eb, #1d4ed8)';

  return (
    <div
      className="absolute inset-0 cursor-pointer"
      onMouseEnter={() => setIsHovered(true)}
      onMouseLeave={() => {
        setIsHovered(false);
        setIsPressed(false);
      }}
      onMouseDown={() => setIsPressed(true)}
      onMouseUp={() => setIsPressed(false)}
      onClick={onClick}
    >
      {/* Border layer */}
      <div
        className="absolute inset-0 hex-clip-flat"
        style={{ background: 'rgba(255, 255, 255, 0.3)' }}
      />
      {/* Content layer */}
      <div
        className="absolute inset-0 hex-clip-flat flex flex-col items-center justify-center text-center transition-transform duration-150"
        style={{
          transform: `scale(${innerScale})`,
          background: bgColor,
        }}
      >
        <span className="text-white text-sm font-bold leading-tight">
          Next
        </span>
        <span className="text-white/70 text-xs">
          (Enter)
        </span>
      </div>
    </div>
  );
}

/**
 * Content for a player hex with toggle selection state.
 * Uses absolute positioning to fill the hex tile.
 */
function PlayerHexContent({
  player,
  profile,
  isSelected,
  onToggle,
}: {
  player: PlayerModel;
  profile?: PlayerProfile;
  isSelected: boolean;
  onToggle: () => void;
}): React.ReactElement {
  const [isHovered, setIsHovered] = useState(false);
  const [isPressed, setIsPressed] = useState(false);
  const [imageError, setImageError] = useState(false);

  const colors = profile?.colors ?? DEFAULT_PLAYER_COLORS;
  const avatarUrl = getPlayerImageUrl(profile?.imageUri);
  const showImage = avatarUrl && !imageError;

  // Build gradient from player colors
  const gradient = buildCssGradient(colors);

  // Scale based on interaction state
  const innerScale = isPressed ? 0.88 : isHovered ? 0.90 : 0.92;

  // Border color: brighter when selected or hovered
  const borderColor = isSelected
    ? 'rgba(34, 197, 94, 0.9)' // Green for selected
    : isHovered
      ? 'rgba(255, 255, 255, 0.6)'
      : 'rgba(255, 255, 255, 0.3)';

  return (
    <div
      className="absolute inset-0 cursor-pointer"
      onMouseEnter={() => setIsHovered(true)}
      onMouseLeave={() => {
        setIsHovered(false);
        setIsPressed(false);
      }}
      onMouseDown={() => setIsPressed(true)}
      onMouseUp={() => setIsPressed(false)}
      onClick={onToggle}
    >
      {/* Border layer - green when selected */}
      <div
        className="absolute inset-0 hex-clip-flat transition-colors duration-150"
        style={{ background: borderColor }}
      />
      {/* Content layer with player gradient */}
      <div
        className="absolute inset-0 hex-clip-flat flex flex-col items-center justify-center transition-transform duration-150"
        style={{
          transform: `scale(${innerScale})`,
          background: gradient,
        }}
      >
        {/* Avatar circle */}
        <div
          className="w-10 h-10 rounded-full overflow-hidden border-2 transition-transform"
          style={{
            borderColor: isSelected ? '#22c55e' : colors.foreground,
            transform: isHovered ? 'scale(1.1)' : 'scale(1)',
          }}
        >
          {showImage ? (
            <img
              src={avatarUrl}
              alt={player.name}
              className="w-full h-full object-cover"
              onError={() => setImageError(true)}
            />
          ) : (
            <div
              className="w-full h-full flex items-center justify-center text-lg font-bold"
              style={{
                background: gradient,
                color: colors.foreground,
              }}
            >
              {player.name.charAt(0).toUpperCase()}
            </div>
          )}
        </div>
        {/* Player name */}
        <span
          className="text-xs font-medium mt-1 truncate max-w-[90%]"
          style={{ color: colors.foreground }}
        >
          {player.name}
        </span>
      </div>
    </div>
  );
}

/**
 * SupplementalOverlay component - displays player selection hex ring with multi-select.
 */
export function SupplementalOverlay({
  players,
  currentPlayerId,
  playerProfiles,
  onToggle,
  onDone,
}: SupplementalOverlayProps): React.ReactElement {
  // Track Next button hover/press state
  const [nextHovered, setNextHovered] = useState(false);
  const [nextPressed, setNextPressed] = useState(false);

  // Filter out current player
  const eligiblePlayers = useMemo(
    () => players.filter((p) => p.id !== currentPlayerId),
    [players, currentPlayerId]
  );

  // Selected IDs come from player model state (participatingInSupplemental)
  const selectedIds = useMemo(
    () => new Set(eligiblePlayers.filter((p) => p.participatingInSupplemental).map((p) => p.id)),
    [eligiblePlayers]
  );

  // Toggle player selection - sends immediately to server like Blazor
  const togglePlayer = useCallback((playerId: string) => {
    const player = eligiblePlayers.find((p) => p.id === playerId);
    if (player) {
      // Toggle the current state
      onToggle(playerId, !player.participatingInSupplemental);
    }
  }, [eligiblePlayers, onToggle]);

  // Keyboard handler for Enter key
  useEffect(() => {
    const handleKeyDown = (e: KeyboardEvent) => {
      if (e.key === 'Enter') {
        e.preventDefault();
        onDone();
      }
    };

    window.addEventListener('keydown', handleKeyDown);
    return () => window.removeEventListener('keydown', handleKeyDown);
  }, [onDone]);

  // Build hex grid items: center (Next button) + surrounding player hexes
  const hexItems = useMemo((): HexGridItem[] => {
    const items: HexGridItem[] = [];

    // Center hex - Next button
    items.push({
      id: 'next',
      coord: cubicCoord(0, 0),
      content: (
        <NextButtonContent
          onClick={onDone}
          isHovered={nextHovered}
          setIsHovered={setNextHovered}
          isPressed={nextPressed}
          setIsPressed={setNextPressed}
        />
      ),
      disabled: false,
    });

    // Ring 1 positions (from HEX_LAYOUTS.CLUSTER_7, skip index 0 which is center)
    const ringCoords = HEX_LAYOUTS.CLUSTER_7.slice(1);

    // Map eligible players to ring positions
    eligiblePlayers.forEach((player, index) => {
      if (index >= ringCoords.length) return; // Max 6 players in ring

      const coord = ringCoords[index];
      const profile = playerProfiles.get(player.id);
      const isSelected = selectedIds.has(player.id);

      items.push({
        id: player.id,
        coord: cubicCoord(coord.q, coord.r),
        content: (
          <PlayerHexContent
            player={player}
            profile={profile}
            isSelected={isSelected}
            onToggle={() => togglePlayer(player.id)}
          />
        ),
        disabled: false,
      });
    });

    return items;
  }, [eligiblePlayers, playerProfiles, selectedIds, togglePlayer, onDone, nextHovered, nextPressed]);

  return (
    <div className="w-full h-full flex flex-col">
      {/* Title */}
      <div className="text-center text-white/80 text-sm mb-2">
        Select players for supplemental build
      </div>
      {/* Hex grid */}
      <div className="flex-1 flex items-center justify-center p-2">
        <HexGrid
          hexSize={50}
          gap={3}
          items={hexItems}
          fitToParent
        />
      </div>
      {/* Selected count */}
      <div className="text-center text-white/60 text-xs mt-1">
        {selectedIds.size === 0
          ? 'No players selected (tap Next to skip)'
          : `${selectedIds.size} player${selectedIds.size > 1 ? 's' : ''} selected`}
      </div>
    </div>
  );
}

export default SupplementalOverlay;
