'use client';

/**
 * GoFirstOverlay - Player selection overlay for FinishedRollOrder state.
 *
 * Displays a HexGrid ring with player avatars. Click a player to select them
 * as the first player in turn order.
 *
 * Wrapped in FloatingPanel for draggable/resizable behavior.
 */

import { useMemo, useState } from 'react';
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

export interface GoFirstOverlayProps {
  /** Players to display (from GameModel) */
  players: PlayerModel[];
  /** Player profiles map (for colors and avatars) */
  playerProfiles: Map<string, PlayerProfile>;
  /** Callback when a player is selected */
  onSelectPlayer: (playerId: string) => void;
}

/**
 * Content for the center hex showing the title.
 * Uses absolute positioning to fill the hex tile.
 */
function CenterContent(): React.ReactElement {
  return (
    <div className="absolute inset-0">
      {/* Border layer */}
      <div
        className="absolute inset-0 hex-clip-flat"
        style={{ background: 'rgba(100, 100, 100, 0.5)' }}
      />
      {/* Content layer */}
      <div
        className="absolute inset-0 hex-clip-flat flex flex-col items-center justify-center text-center"
        style={{
          transform: 'scale(0.92)',
          background: 'linear-gradient(135deg, #374151, #1f2937)',
        }}
      >
        <span className="text-white text-xs font-semibold leading-tight">Goes First</span>
      </div>
    </div>
  );
}

/**
 * Content for a player hex with hover/press states.
 * Uses absolute positioning to fill the hex tile.
 */
function PlayerHexContent({
  player,
  profile,
}: {
  player: PlayerModel;
  profile?: PlayerProfile;
}): React.ReactElement {
  const [isHovered, setIsHovered] = useState(false);
  const [isPressed, setIsPressed] = useState(false);
  const [imageError, setImageError] = useState(false);

  const colors = profile?.colors ?? DEFAULT_PLAYER_COLORS;
  const avatarUrl = getPlayerImageUrl(profile?.imageUri);
  const showImage = avatarUrl && !imageError;

  // Build gradient from player colors (unified API from playerColors.ts)
  const gradient = buildCssGradient(colors);

  // Scale based on interaction state
  const innerScale = isPressed ? 0.88 : isHovered ? 0.9 : 0.92;
  const borderColor = isHovered ? 'rgba(255, 255, 255, 0.6)' : 'rgba(255, 255, 255, 0.3)';

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
    >
      {/* Border layer */}
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
            borderColor: colors.foreground,
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
 * GoFirstOverlay component - displays player selection hex ring.
 */
export function GoFirstOverlay({
  players,
  playerProfiles,
  onSelectPlayer,
}: GoFirstOverlayProps): React.ReactElement {
  console.log(
    '[GoFirstOverlay] render, players:',
    players.length,
    'profiles:',
    playerProfiles.size
  );

  // Build hex grid items: center (title) + surrounding player hexes
  const hexItems = useMemo((): HexGridItem[] => {
    const items: HexGridItem[] = [];

    // Center hex - title
    items.push({
      id: 'center',
      coord: cubicCoord(0, 0),
      content: <CenterContent />,
      disabled: true,
    });

    // Ring 1 positions (from HEX_LAYOUTS.CLUSTER_7, skip index 0 which is center)
    const ringCoords = HEX_LAYOUTS.CLUSTER_7.slice(1);

    // Map players to ring positions
    players.forEach((player, index) => {
      if (index >= ringCoords.length) return; // Max 6 players in ring

      const coord = ringCoords[index];
      const profile = playerProfiles.get(player.id);

      items.push({
        id: player.id,
        coord: cubicCoord(coord.q, coord.r),
        content: <PlayerHexContent player={player} profile={profile} />,
        onClick: () => onSelectPlayer(player.id),
        disabled: false,
      });
    });

    return items;
  }, [players, playerProfiles, onSelectPlayer]);

  return (
    <div className="w-full h-full flex items-center justify-center p-4">
      <HexGrid hexSize={50} gap={3} items={hexItems} fitToParent />
    </div>
  );
}

export default GoFirstOverlay;
