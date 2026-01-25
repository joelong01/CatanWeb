'use client';

import { useState, useEffect, useCallback, useMemo } from 'react';
import { useRouter } from 'next/navigation';
import Image from 'next/image';
import { MainLayout } from '@/components/layout';
import {
  GameTypeSelector,
  PlayerSelector,
  GameNameInput,
  GameOptions,
  generateGameName,
  DEFAULT_GAME_OPTIONS,
} from '@/components/new-game';
import type { GameOptionsState } from '@/components/new-game';
import { gameApi } from '@/lib/api';
import { FontAwesomeIcon } from '@fortawesome/react-fontawesome';
import {
  faPlay,
  faArrowLeft,
  faSpinner,
  faExclamationTriangle,
  faDice,
  faGripVertical,
  faUser,
} from '@fortawesome/free-solid-svg-icons';
import type { PlayerProfile } from '@/types/player-profile';
import type { GameType, HouseRules } from '@/types/generated/models';
import Link from 'next/link';
import { serviceConfig } from '@/lib/services/config';

/**
 * Get the full image URL for a player.
 */
function getPlayerImageUrl(imageUri: string | undefined): string | null {
  if (!imageUri) return null;
  if (imageUri.startsWith('http')) return imageUri;
  return `${serviceConfig.serviceUrl}${imageUri}`;
}

/**
 * New Game page - configure and start a new Catan game.
 *
 * Features:
 * - Game type selection (Regular/Expansion/Coming Soon)
 * - Player selection with drag-and-drop reordering
 * - Auto-generated game name
 * - Game options (record, save, stats)
 * - Validation and error handling
 */
export default function NewGame(): React.ReactElement {
  const router = useRouter();

  // Drag and drop state for turn order
  const [dragIndex, setDragIndex] = useState<number | null>(null);
  const [dragOverIndex, setDragOverIndex] = useState<number | null>(null);

  // Form state
  const [gameType, setGameType] = useState<GameType>('Regular');
  const [selectedPlayerIds, setSelectedPlayerIds] = useState<string[]>([]);
  const [gameName, setGameName] = useState(() => generateGameName('Regular'));
  const [gameOptions, setGameOptions] = useState<GameOptionsState>(DEFAULT_GAME_OPTIONS);
  const [includeGuest, setIncludeGuest] = useState(false);

  // Data loading state
  const [players, setPlayers] = useState<PlayerProfile[]>([]);
  const [playersLoading, setPlayersLoading] = useState(true);
  const [playersError, setPlayersError] = useState<string | null>(null);

  // Submit state
  const [isSubmitting, setIsSubmitting] = useState(false);
  const [submitError, setSubmitError] = useState<string | null>(null);

  // Load players on mount
  useEffect(() => {
    async function loadPlayers(): Promise<void> {
      setPlayersLoading(true);
      setPlayersError(null);

      const result = await gameApi.getPlayers();

      if (result.success && result.data) {
        setPlayers(result.data);
      } else {
        setPlayersError(result.error ?? 'Failed to load players');
      }

      setPlayersLoading(false);
    }

    loadPlayers();
  }, []);

  // Reset player selection when game type changes (different player limits)
  const handleGameTypeChange = useCallback(
    (newType: GameType) => {
      setGameType(newType);

      // If switching to Regular and have more than 4 players, trim
      if (newType === 'Regular' && selectedPlayerIds.length > 4) {
        setSelectedPlayerIds(selectedPlayerIds.slice(0, 4));
      }
    },
    [selectedPlayerIds]
  );

  // Get selected players in order for display
  const selectedPlayers = useMemo(() => {
    return selectedPlayerIds
      .map((id) => players.find((p) => p.id === id))
      .filter((p): p is PlayerProfile => p !== undefined);
  }, [selectedPlayerIds, players]);

  // Drag and drop handlers for turn order
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
      setSelectedPlayerIds(newIds);
    }
    setDragIndex(null);
    setDragOverIndex(null);
  }, [dragIndex, dragOverIndex, selectedPlayerIds]);

  // Validation
  const minPlayers = gameType === 'Expansion' ? 3 : 3;
  const isValid = selectedPlayerIds.length >= minPlayers;

  // Handle game creation
  const handleStartGame = useCallback(async () => {
    if (!isValid || isSubmitting) return;

    setIsSubmitting(true);
    setSubmitError(null);

    // Create house rules object if enabled (matching Blazor defaults)
    const houseRules: Partial<HouseRules> | undefined = gameOptions.useHouseRules
      ? {
          goldTiles: gameType === 'Expansion' ? 2 : 1,
          griefDodgy: false, // OFF by default - must be explicitly enabled in Settings
          wallsProtectCities: true,
          knightMovesBaronBeforeRoll: true,
          hideBaronBeforeInvasion: false,
          knightMovesRobberBeforeRoll: false,
          hideRobberBeforeInvasion: false,
          supplementalMinPlayers: 5,
        }
      : undefined;

    const result = await gameApi.createGame(
      gameType,
      selectedPlayerIds,
      gameName || undefined,
      houseRules,
      gameOptions.updateStats
    );

    if (result.success && result.data) {
      // Navigate to the new game
      router.push(`/game/${result.data}`);
    } else {
      setSubmitError(result.error ?? 'Failed to create game');
      setIsSubmitting(false);
    }
  }, [isValid, isSubmitting, gameType, selectedPlayerIds, gameName, gameOptions.useHouseRules, gameOptions.updateStats, router]);

  return (
    <MainLayout>
      <div className="min-h-screen h-full py-5 pt-[60px] pb-[60px] px-5 max-w-[1400px] mx-auto overflow-y-auto">
        {/* Header */}
        <header className="flex items-center gap-4 mb-6">
          <Link
            href="/"
            className="
              flex items-center gap-2 px-4 py-2.5
              bg-white/5 rounded-lg
              text-gray-400 text-sm font-medium
              transition-all duration-200
              hover:bg-white/10 hover:text-white
            "
          >
            <FontAwesomeIcon icon={faArrowLeft} />
            <span>Back</span>
          </Link>
          <div className="flex items-center gap-3">
            <div className="w-10 h-10 rounded-lg bg-gradient-to-br from-amber-500 to-amber-700 flex items-center justify-center">
              <FontAwesomeIcon icon={faDice} className="text-white text-xl" />
            </div>
            <h1 className="text-3xl font-bold bg-gradient-to-r from-amber-400 to-amber-600 bg-clip-text text-transparent">
              New Game
            </h1>
          </div>
        </header>

        {/* Main content - tight, organized layout */}
        <div className="flex flex-col gap-6">
          {/* Game Type + Player Selection + Sitting Order
              Portrait: stacked vertically
              Landscape: 3-column grid (Game Type | Player Selection | Sitting Order)
          */}
          <div className="grid grid-cols-1 lg:grid-cols-2 lg:landscape:grid-cols-[1fr,1fr,300px] gap-6">
            {/* Game Type Card */}
            <div className="bg-white/5 rounded-xl p-4 border border-white/10">
              <GameTypeSelector value={gameType} onChange={handleGameTypeChange} />
            </div>

            {/* Player Selection Card */}
            <div className="bg-white/5 rounded-xl p-4 border border-white/10">
              <PlayerSelector
                availablePlayers={players}
                selectedPlayerIds={selectedPlayerIds}
                onChange={setSelectedPlayerIds}
                gameType={gameType}
                loading={playersLoading}
                error={playersError ?? undefined}
                includeGuest={includeGuest}
                onIncludeGuestChange={setIncludeGuest}
              />
            </div>

            {/* Sitting Order - horizontal in portrait, vertical in landscape */}
            {selectedPlayers.length > 0 && (
              <div className="bg-white/5 rounded-xl p-4 border border-white/10 col-span-full lg:[grid-column:1/3] lg:landscape:[grid-column:3/4]">
                <h3 className="text-sm font-semibold text-white mb-3">Sitting Order</h3>
                <div className="flex flex-wrap lg:landscape:flex-col gap-2">
                  {selectedPlayers.map((player, index) => {
                    const imageUrl = getPlayerImageUrl(player.imageUri);
                    const isDragging = dragIndex === index;
                    const isDragOver = dragOverIndex === index && dragIndex !== index;

                    return (
                      <div
                        key={player.id}
                        className={`
                          flex items-center gap-2 px-3 py-2 rounded-lg cursor-grab active:cursor-grabbing
                          transition-all duration-200 select-none
                          ${isDragging ? 'opacity-50 scale-95' : ''}
                          ${isDragOver ? 'ring-2 ring-blue-500 ring-offset-2 ring-offset-gray-900' : ''}
                        `}
                        draggable
                        onDragStart={() => handleDragStart(index)}
                        onDragOver={(e) => {
                          e.preventDefault();
                          handleDragOver(index);
                        }}
                        onDragEnd={handleDragEnd}
                        style={{
                          background: `linear-gradient(135deg, ${player.colors.primary}, ${player.colors.secondary})`,
                          color: player.colors.foreground,
                        }}
                      >
                        <FontAwesomeIcon icon={faGripVertical} className="opacity-60 text-xs" />
                        <div className="w-7 h-7 hex-clip-flat overflow-hidden bg-black/20 flex items-center justify-center">
                          {imageUrl ? (
                            <Image src={imageUrl} alt={player.name} width={28} height={28} unoptimized />
                          ) : (
                            <FontAwesomeIcon icon={faUser} className="text-xs" />
                          )}
                        </div>
                        <span className="font-medium text-sm">{player.name}</span>
                      </div>
                    );
                  })}
                </div>
              </div>
            )}
          </div>

          {/* Bottom section: Game Name, Options, Start */}
          <div className="space-y-4">

              <GameNameInput
                value={gameName}
                onChange={setGameName}
                gameType={gameType}
              />

              <GameOptions options={gameOptions} onChange={setGameOptions} />

              {/* Error banner */}
              {submitError && (
                <div
                  className="
                    flex items-center gap-3 px-5 py-4
                    bg-red-500/10 border border-red-500 rounded-xl
                    text-red-500
                  "
                >
                  <FontAwesomeIcon icon={faExclamationTriangle} />
                  <span>{submitError}</span>
                </div>
              )}

              {/* Form actions */}
              <div className="flex flex-col items-center gap-4 pt-5 border-t border-white/10">
                <button
                  type="button"
                  className={`
                    flex items-center justify-center gap-3
                    w-full landscape:w-auto
                    px-12 py-[18px] rounded-xl
                    text-white text-xl font-semibold
                    transition-all duration-300
                    ${isValid && !isSubmitting
                      ? 'bg-gradient-to-br from-green-500 to-green-700 cursor-pointer hover:-translate-y-0.5 hover:shadow-xl hover:shadow-green-500/40'
                      : 'bg-gray-600 cursor-not-allowed'
                    }
                  `}
                  onClick={handleStartGame}
                  disabled={!isValid || isSubmitting}
                >
                  {isSubmitting ? (
                    <>
                      <FontAwesomeIcon icon={faSpinner} spin />
                      <span>Creating Game...</span>
                    </>
                  ) : (
                    <>
                      <FontAwesomeIcon icon={faPlay} />
                      <span>Start Game</span>
                    </>
                  )}
                </button>

                <p className="text-sm text-gray-500">
                  {gameType === 'Expansion' ? 'Expansion' : 'Classic'} with {selectedPlayerIds.length} Player{selectedPlayerIds.length !== 1 ? 's' : ''}
                </p>
              </div>
            </div>
          </div>
      </div>
    </MainLayout>
  );
}
