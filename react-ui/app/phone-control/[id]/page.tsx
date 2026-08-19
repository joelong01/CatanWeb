'use client';

/**
 * Phone Remote Control - Minimal touch-first game control page.
 *
 * Always shows a hex-grid UI with a center Next hex button.
 * When in PickSupplementalPlayers, the player ring appears around it.
 * Next is enabled/disabled per GameModel.actionFlags.
 *
 * Connects to the same game via useGameConnection + Zustand store.
 */

import { useMemo, useCallback, useEffect, useState } from 'react';
import { useParams } from 'next/navigation';
import Link from 'next/link';
import { FontAwesomeIcon } from '@fortawesome/react-fontawesome';
import { faForwardStep, faArrowLeft } from '@fortawesome/free-solid-svg-icons';
import { HexGrid, type HexGridItem } from '@/components/hex-grid/HexGrid';
import { HEX_LAYOUTS, cubicCoord } from '@/components/hex-grid/hex-geometry';
import { serviceConfig } from '@/lib/config';
import { buildCssGradient } from '@/lib/utils/playerColors';
import { useGameConnection } from '@/lib/hooks/useGameConnection';
import {
  useGameState,
  useActionFlags,
  usePlayers,
  useCurrentPlayer,
  usePlayerProfiles,
  usePlayerName,
  useSetPlayerProfiles,
} from '@/lib/stores/gameStoreHooks';
import { getDevPlayerId } from '@/lib/utils/getDevPlayerId';
import { getStateMessage } from '@/lib/utils/gameStateMessages';
import { gameApi } from '@/lib/api/gameApi';
import type { PlayerModel } from '@/types/generated/models/player-model';
import type { PlayerProfile } from '@/types/player-profile';
import { DEFAULT_PLAYER_COLORS } from '@/types/player-profile';

function getPlayerImageUrl(imageUri: string | undefined): string | null {
  if (!imageUri) return null;
  if (imageUri.startsWith('http')) return imageUri;
  return `${serviceConfig.serviceUrl}${imageUri}`;
}

/**
 * Center hex "Next" button with enabled/disabled support.
 */
function NextHexContent({
  enabled,
  onClick,
  isHovered,
  setIsHovered,
  isPressed,
  setIsPressed,
}: {
  enabled: boolean;
  onClick: () => void;
  isHovered: boolean;
  setIsHovered: (h: boolean) => void;
  isPressed: boolean;
  setIsPressed: (p: boolean) => void;
}): React.ReactElement {
  const innerScale = !enabled ? 0.92 : isPressed ? 0.88 : isHovered ? 0.9 : 0.92;

  const bgColor = !enabled
    ? 'linear-gradient(135deg, #1f2937, #111827)'
    : isPressed
      ? 'linear-gradient(135deg, #1e40af, #1e3a8a)'
      : isHovered
        ? 'linear-gradient(135deg, #3b82f6, #2563eb)'
        : 'linear-gradient(135deg, #2563eb, #1d4ed8)';

  const borderBg = enabled ? 'rgba(255, 255, 255, 0.3)' : 'rgba(255, 255, 255, 0.1)';
  const textClass = enabled ? 'text-white' : 'text-gray-600';

  return (
    <div
      className={`absolute inset-0 ${enabled ? 'cursor-pointer' : 'cursor-not-allowed'}`}
      onMouseEnter={() => enabled && setIsHovered(true)}
      onMouseLeave={() => {
        setIsHovered(false);
        setIsPressed(false);
      }}
      onMouseDown={() => enabled && setIsPressed(true)}
      onMouseUp={() => setIsPressed(false)}
      onClick={() => enabled && onClick()}
    >
      <div className="absolute inset-0 hex-clip-flat" style={{ background: borderBg }} />
      <div
        className="absolute inset-0 hex-clip-flat flex flex-col items-center justify-center text-center transition-transform duration-150"
        style={{ transform: `scale(${innerScale})`, background: bgColor }}
      >
        <FontAwesomeIcon icon={faForwardStep} className={`text-2xl mb-1 ${textClass}`} />
        <span className={`text-sm font-bold leading-tight ${textClass}`}>Next</span>
      </div>
    </div>
  );
}

/**
 * Player hex with toggle selection (same pattern as SupplementalOverlay).
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
  const displayName = usePlayerName(player.id) ?? '';
  const avatarUrl = getPlayerImageUrl(profile?.imageUri);
  const showImage = avatarUrl && !imageError;
  const gradient = buildCssGradient(colors);
  const innerScale = isPressed ? 0.88 : isHovered ? 0.9 : 0.92;

  const borderColor = isSelected
    ? 'rgba(34, 197, 94, 0.9)'
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
      <div
        className="absolute inset-0 hex-clip-flat transition-colors duration-150"
        style={{ background: borderColor }}
      />
      <div
        className="absolute inset-0 hex-clip-flat flex flex-col items-center justify-center transition-transform duration-150"
        style={{ transform: `scale(${innerScale})`, background: gradient }}
      >
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
              alt={displayName}
              className="w-full h-full object-cover"
              onError={() => setImageError(true)}
            />
          ) : (
            <div
              className="w-full h-full flex items-center justify-center text-lg font-bold"
              style={{ background: gradient, color: colors.foreground }}
            >
              {displayName.charAt(0).toUpperCase()}
            </div>
          )}
        </div>
        <span
          className="text-xs font-medium mt-1 truncate max-w-[90%]"
          style={{ color: colors.foreground }}
        >
          {displayName}
        </span>
      </div>
    </div>
  );
}

export default function PhoneControlPage(): React.ReactElement {
  const params = useParams();
  const gameId = params.id as string;
  const playerId = useMemo(() => getDevPlayerId(), []);

  const { isConnected, isConnecting, proxy } = useGameConnection({
    playerId,
    gameId,
    autoConnect: true,
  });

  const gameState = useGameState();
  const actionFlags = useActionFlags();
  const players = usePlayers();
  const currentPlayer = useCurrentPlayer();
  const playerProfiles = usePlayerProfiles();
  const setPlayerProfiles = useSetPlayerProfiles();

  useEffect(() => {
    async function loadProfiles() {
      const result = await gameApi.getPlayers();
      if (result.success && result.data) {
        setPlayerProfiles(result.data);
      }
    }
    loadProfiles();
  }, [setPlayerProfiles]);

  useEffect(() => {
    localStorage.setItem('current_gameId', gameId);
  }, [gameId]);

  const handleNext = useCallback(() => {
    proxy.next();
  }, [proxy]);

  const handleSupplementalToggle = useCallback(
    (targetPlayerId: string, participating: boolean) => {
      proxy.setParticipatingInSupplemental(targetPlayerId, participating);
    },
    [proxy]
  );

  const nextEnabled = actionFlags?.nextEnabled ?? false;
  const stateMessage = getStateMessage(gameState);
  const isSupplemental = gameState === 'PickSupplementalPlayers' && currentPlayer;

  // Next hex hover/press state
  const [nextHovered, setNextHovered] = useState(false);
  const [nextPressed, setNextPressed] = useState(false);

  // Eligible players for supplemental ring
  const eligiblePlayers = useMemo(
    () => (currentPlayer ? players.filter((p) => p.id !== currentPlayer.id) : []),
    [players, currentPlayer]
  );

  const selectedIds = useMemo(
    () => new Set(eligiblePlayers.filter((p) => p.participatingInSupplemental).map((p) => p.id)),
    [eligiblePlayers]
  );

  const togglePlayer = useCallback(
    (pid: string) => {
      const player = eligiblePlayers.find((p) => p.id === pid);
      if (player) {
        handleSupplementalToggle(pid, !player.participatingInSupplemental);
      }
    },
    [eligiblePlayers, handleSupplementalToggle]
  );

  // Build hex items: always center Next, optionally player ring
  const hexItems = useMemo((): HexGridItem[] => {
    const items: HexGridItem[] = [];

    // Center hex — Next button (always present)
    items.push({
      id: 'next',
      coord: cubicCoord(0, 0),
      content: (
        <NextHexContent
          enabled={nextEnabled}
          onClick={handleNext}
          isHovered={nextHovered}
          setIsHovered={setNextHovered}
          isPressed={nextPressed}
          setIsPressed={setNextPressed}
        />
      ),
      disabled: false,
    });

    // Player ring — only during PickSupplementalPlayers
    if (isSupplemental) {
      const ringCoords = HEX_LAYOUTS.CLUSTER_7.slice(1);
      eligiblePlayers.forEach((player, index) => {
        if (index >= ringCoords.length) return;
        const coord = ringCoords[index];
        const profile = playerProfiles.get(player.id);
        items.push({
          id: player.id,
          coord: cubicCoord(coord.q, coord.r),
          content: (
            <PlayerHexContent
              player={player}
              profile={profile}
              isSelected={selectedIds.has(player.id)}
              onToggle={() => togglePlayer(player.id)}
            />
          ),
          disabled: false,
        });
      });
    }

    return items;
  }, [
    nextEnabled,
    handleNext,
    nextHovered,
    nextPressed,
    isSupplemental,
    eligiblePlayers,
    playerProfiles,
    selectedIds,
    togglePlayer,
  ]);

  // Connection status
  if (!isConnected) {
    return (
      <div className="min-h-screen bg-gray-900 flex flex-col items-center justify-center p-6">
        <div className="text-gray-400 text-lg">
          {isConnecting ? 'Connecting...' : 'Disconnected'}
        </div>
        <Link href={`/game/${gameId}`} className="mt-6 text-blue-400 hover:text-blue-300 text-sm">
          <FontAwesomeIcon icon={faArrowLeft} className="mr-2" />
          Back to Game
        </Link>
      </div>
    );
  }

  return (
    <div className="min-h-screen bg-gray-900 flex flex-col items-center justify-center p-6 gap-6">
      {/* Game state */}
      <div className="text-center">
        <div className="text-gray-500 text-xs uppercase tracking-wider mb-1">
          {isSupplemental ? 'Pick Supplemental Players' : 'Game State'}
        </div>
        <div className="text-white text-xl font-medium">{stateMessage || 'Loading...'}</div>
      </div>

      {/* Hex grid — single Next hex, or Next + player ring */}
      <div className="w-full max-w-sm aspect-square flex items-center justify-center">
        <HexGrid hexSize={50} gap={3} items={hexItems} fitToParent />
      </div>

      {/* Selection count during supplemental */}
      {isSupplemental && (
        <div className="text-center text-white/60 text-xs">
          {selectedIds.size === 0
            ? 'No players selected (tap Next to skip)'
            : `${selectedIds.size} player${selectedIds.size > 1 ? 's' : ''} selected`}
        </div>
      )}

      {/* Back link */}
      <Link href={`/game/${gameId}`} className="text-blue-400 hover:text-blue-300 text-sm">
        <FontAwesomeIcon icon={faArrowLeft} className="mr-2" />
        Back to Game
      </Link>
    </div>
  );
}
