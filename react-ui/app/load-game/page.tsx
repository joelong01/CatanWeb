'use client';

import { useState, useEffect, useCallback, useRef, useMemo } from 'react';
import { useRouter } from 'next/navigation';
import { MainLayout } from '@/components/layout';
import { gameApi, type SavedGameSummary } from '@/lib/api/gameApi';
import { usePlayerProfiles, useSetPlayerProfiles } from '@/lib/stores/gameStoreHooks';
import { resolveHistoricalName } from '@/lib/utils/playerNames';

/**
 * Open Game page - browse and load saved games.
 * Ported from Blazor LoadGame.razor with desktop table and mobile card layouts.
 * Includes filtering and bulk delete functionality.
 */
export default function LoadGame(): React.ReactElement {
  const router = useRouter();
  const [savedGames, setSavedGames] = useState<SavedGameSummary[]>([]);
  const playerProfiles = usePlayerProfiles();
  const setPlayerProfiles = useSetPlayerProfiles();
  const [isLoading, setIsLoading] = useState(true);
  const [errorMessage, setErrorMessage] = useState<string | null>(null);
  const [selectedGameId, setSelectedGameId] = useState<string | null>(null);

  // Inline editing state
  const [editingGameId, setEditingGameId] = useState<string | null>(null);
  const [editingName, setEditingName] = useState('');
  const [originalName, setOriginalName] = useState('');
  const inputRef = useRef<HTMLInputElement>(null);
  const tableRef = useRef<HTMLTableElement>(null);

  // Action menu state — which row's ⋯ menu is open
  const [openMenuGameId, setOpenMenuGameId] = useState<string | null>(null);

  // Filter state
  const [maxRolls, setMaxRolls] = useState<number | null>(null);
  const [filterGameState, setFilterGameState] = useState<string>('');

  // Multi-select state
  const [checkedGameIds, setCheckedGameIds] = useState<Set<string>>(new Set());
  const [isDeleting, setIsDeleting] = useState(false);

  // Player profiles own display names; the summary rows carry only IDs (issue #208).
  useEffect(() => {
    async function loadProfiles() {
      const result = await gameApi.getPlayers();
      if (result.success && result.data) setPlayerProfiles(result.data);
    }
    loadProfiles();
  }, [setPlayerProfiles]);

  /**
   * Names to show for a saved-game row.
   *
   * Prefers the stored `playerNames` for rows written before `playerIds` existed, and
   * resolves from the profiles otherwise. `resolveHistoricalName` bypasses the stored
   * value only when it is provably an ID-parsing artifact.
   */
  const displayPlayerNames = useCallback(
    (game: SavedGameSummary): string => {
      if (game.playerIds?.length) {
        return game.playerIds
          .map((id) => resolveHistoricalName(playerProfiles, id, undefined))
          .join(', ');
      }
      return game.playerNames;
    },
    [playerProfiles]
  );

  // Load saved games on mount
  useEffect(() => {
    loadSavedGames();
  }, []);

  // Focus input when editing starts
  useEffect(() => {
    if (editingGameId && inputRef.current) {
      inputRef.current.focus();
      inputRef.current.select();
    }
  }, [editingGameId]);

  // Close action menu on click outside (use data attribute to find the open menu)
  useEffect(() => {
    if (!openMenuGameId) return;

    const handleOutsideClick = (e: MouseEvent) => {
      const target = e.target as HTMLElement;
      // Close if the click is not inside any element marked as the open menu
      if (!target.closest('[data-action-menu="open"]')) {
        setOpenMenuGameId(null);
      }
    };

    document.addEventListener('mousedown', handleOutsideClick);
    return () => document.removeEventListener('mousedown', handleOutsideClick);
  }, [openMenuGameId]);

  // Filter games based on criteria
  const filteredGames = useMemo(() => {
    return savedGames.filter((game) => {
      if (maxRolls !== null && game.turnCount > maxRolls) return false;
      if (filterGameState && game.gameState !== filterGameState) return false;
      return true;
    });
  }, [savedGames, maxRolls, filterGameState]);

  // Get unique game states for dropdown
  const gameStates = useMemo(() => {
    const states = new Set(savedGames.map((g) => g.gameState));
    return Array.from(states).sort();
  }, [savedGames]);

  const allFilteredChecked = useMemo(() => {
    if (filteredGames.length === 0) return false;
    return filteredGames.every((g) => checkedGameIds.has(g.gameId));
  }, [filteredGames, checkedGameIds]);

  const someFilteredChecked = useMemo(() => {
    if (filteredGames.length === 0) return false;
    const checkedCount = filteredGames.filter((g) => checkedGameIds.has(g.gameId)).length;
    return checkedCount > 0 && checkedCount < filteredGames.length;
  }, [filteredGames, checkedGameIds]);

  const loadSavedGames = async () => {
    try {
      setIsLoading(true);
      const result = await gameApi.getSavedGames();
      if (result.success && result.data) {
        setSavedGames(result.data);
        if (result.data.length > 0) {
          setSelectedGameId(result.data[0].gameId);
        }
      } else {
        setErrorMessage(result.error ?? 'Failed to load saved games');
      }
    } catch (error) {
      setErrorMessage(
        `Error loading games: ${error instanceof Error ? error.message : 'Unknown error'}`
      );
    } finally {
      setIsLoading(false);
    }
  };

  const selectGame = useCallback(
    (gameId: string) => {
      if (editingGameId) return;
      setSelectedGameId(gameId);
    },
    [editingGameId]
  );

  const openGame = useCallback(
    async (gameId: string) => {
      setOpenMenuGameId(null);
      setErrorMessage(null);
      try {
        const result = await gameApi.loadGame(gameId);
        if (!result.success) {
          setErrorMessage(result.error ?? 'Failed to load game');
          return;
        }
        router.push(`/game/${gameId}`);
      } catch (error) {
        setErrorMessage(
          `Error loading game: ${error instanceof Error ? error.message : 'Unknown error'}`
        );
      }
    },
    [router]
  );

  const startEditingName = useCallback(
    (gameId: string) => {
      setOpenMenuGameId(null);
      const game = savedGames.find((g) => g.gameId === gameId);
      if (!game) return;
      setSelectedGameId(gameId);
      setOriginalName(game.gameName);
      setEditingName(game.gameName);
      setEditingGameId(gameId);
    },
    [savedGames]
  );

  const saveName = useCallback(async () => {
    if (!editingGameId) return;
    const trimmedName = editingName.trim();
    setEditingGameId(null);
    if (!trimmedName || trimmedName === originalName) return;
    try {
      const result = await gameApi.renameGame(editingGameId, trimmedName);
      if (result.success) {
        setSavedGames((prev) =>
          prev.map((g) => (g.gameId === editingGameId ? { ...g, gameName: trimmedName } : g))
        );
      }
    } catch (error) {
      console.error('Error renaming:', error);
    }
  }, [editingGameId, editingName, originalName]);

  const cancelEditing = useCallback(() => {
    setEditingGameId(null);
  }, []);

  const deleteGame = useCallback(
    async (gameId: string) => {
      setOpenMenuGameId(null);
      const game = savedGames.find((g) => g.gameId === gameId);
      if (!game) return;

      const currentGameId =
        typeof window !== 'undefined' ? localStorage.getItem('current_gameId') : null;
      if (currentGameId === gameId && game.gameState !== 'GameOver') {
        alert(
          'Cannot delete the currently active game. Please start or load a different game first.'
        );
        return;
      }

      const confirmed = confirm(`Delete game '${game.gameName}'?`);
      if (!confirmed) return;

      try {
        const result = await gameApi.deleteGame(gameId);
        if (result.success) {
          const currentIndex = savedGames.findIndex((g) => g.gameId === gameId);
          const newGames = savedGames.filter((g) => g.gameId !== gameId);
          setSavedGames(newGames);
          setCheckedGameIds((prev) => {
            const next = new Set(prev);
            next.delete(gameId);
            return next;
          });
          if (newGames.length > 0) {
            const newIndex = Math.min(currentIndex, newGames.length - 1);
            setSelectedGameId(newGames[newIndex].gameId);
          } else {
            setSelectedGameId(null);
          }
        } else {
          setErrorMessage(result.error ?? 'Failed to delete game');
        }
      } catch (error) {
        setErrorMessage(
          `Error deleting game: ${error instanceof Error ? error.message : 'Unknown error'}`
        );
      }
    },
    [savedGames]
  );

  const handleCopyGame = useCallback(async (gameId: string) => {
    setOpenMenuGameId(null);
    setErrorMessage(null);
    try {
      const result = await gameApi.copyGame(gameId);
      if (!result.success || !result.data) {
        setErrorMessage(result.error ?? 'Failed to copy game');
        return;
      }
      const newGameId = result.data.newGameId;
      const listResult = await gameApi.getSavedGames();
      if (listResult.success && listResult.data) {
        setSavedGames(listResult.data);
        setSelectedGameId(newGameId);
        const newGame = listResult.data.find((g) => g.gameId === newGameId);
        if (newGame) {
          setOriginalName(newGame.gameName);
          setEditingName(newGame.gameName);
          setEditingGameId(newGameId);
        }
      }
    } catch (error) {
      setErrorMessage(
        `Error copying game: ${error instanceof Error ? error.message : 'Unknown error'}`
      );
    }
  }, []);

  const handleReplayGame = useCallback(
    async (gameId: string) => {
      setOpenMenuGameId(null);
      setErrorMessage(null);
      try {
        const result = await gameApi.replayGame(gameId);
        if (!result.success || !result.data) {
          setErrorMessage(result.error ?? 'Failed to create replay');
          return;
        }
        // The replay endpoint already registered the new game in the
        // GameStateMachineRegistry, so navigate straight in — the game page
        // joins via SignalR (mirrors the openGame path).
        router.push(`/game/${result.data.newGameId}`);
      } catch (error) {
        setErrorMessage(
          `Error creating replay: ${error instanceof Error ? error.message : 'Unknown error'}`
        );
      }
    },
    [router]
  );

  const deleteCheckedGames = useCallback(async () => {
    if (checkedGameIds.size === 0) return;

    const currentGameId =
      typeof window !== 'undefined' ? localStorage.getItem('current_gameId') : null;
    const activeChecked =
      currentGameId &&
      checkedGameIds.has(currentGameId) &&
      savedGames.find((g) => g.gameId === currentGameId)?.gameState !== 'GameOver';
    if (activeChecked) {
      alert(
        'Cannot delete the currently active game. Please uncheck it or load a different game first.'
      );
      return;
    }

    const confirmed = confirm(`Delete ${checkedGameIds.size} selected game(s)?`);
    if (!confirmed) return;

    setIsDeleting(true);
    setErrorMessage(null);

    const idsToDelete = Array.from(checkedGameIds);
    const failedIds: string[] = [];

    for (const gameId of idsToDelete) {
      try {
        const result = await gameApi.deleteGame(gameId);
        if (!result.success) failedIds.push(gameId);
      } catch {
        failedIds.push(gameId);
      }
    }

    setSavedGames((prev) =>
      prev.filter((g) => !checkedGameIds.has(g.gameId) || failedIds.includes(g.gameId))
    );
    setCheckedGameIds(new Set(failedIds));

    if (failedIds.length > 0) {
      setErrorMessage(`Failed to delete ${failedIds.length} game(s)`);
    }

    if (
      selectedGameId &&
      checkedGameIds.has(selectedGameId) &&
      !failedIds.includes(selectedGameId)
    ) {
      const remaining = savedGames.filter(
        (g) => !checkedGameIds.has(g.gameId) || failedIds.includes(g.gameId)
      );
      setSelectedGameId(remaining.length > 0 ? remaining[0].gameId : null);
    }

    setIsDeleting(false);
  }, [checkedGameIds, savedGames, selectedGameId]);

  const toggleGameChecked = useCallback((gameId: string, e?: React.MouseEvent) => {
    e?.stopPropagation();
    setCheckedGameIds((prev) => {
      const next = new Set(prev);
      if (next.has(gameId)) next.delete(gameId);
      else next.add(gameId);
      return next;
    });
  }, []);

  const toggleAllFiltered = useCallback(() => {
    if (allFilteredChecked) {
      setCheckedGameIds((prev) => {
        const next = new Set(prev);
        filteredGames.forEach((g) => next.delete(g.gameId));
        return next;
      });
    } else {
      setCheckedGameIds((prev) => {
        const next = new Set(prev);
        filteredGames.forEach((g) => next.add(g.gameId));
        return next;
      });
    }
  }, [allFilteredChecked, filteredGames]);

  const selectNextGame = useCallback(
    (direction: number) => {
      if (!selectedGameId || filteredGames.length === 0) return;
      const currentIndex = filteredGames.findIndex((g) => g.gameId === selectedGameId);
      const newIndex = currentIndex + direction;
      if (newIndex >= 0 && newIndex < filteredGames.length) {
        setSelectedGameId(filteredGames[newIndex].gameId);
      }
    },
    [selectedGameId, filteredGames]
  );

  const handleTableKeyDown = useCallback(
    (e: React.KeyboardEvent) => {
      if (editingGameId) return;
      switch (e.key) {
        case 'F2':
          if (selectedGameId) {
            e.preventDefault();
            startEditingName(selectedGameId);
          }
          break;
        case 'Enter':
          if (selectedGameId) {
            e.preventDefault();
            openGame(selectedGameId);
          }
          break;
        case 'Delete':
          if (selectedGameId) {
            e.preventDefault();
            deleteGame(selectedGameId);
          }
          break;
        case 'ArrowDown':
          e.preventDefault();
          selectNextGame(1);
          break;
        case 'ArrowUp':
          e.preventDefault();
          selectNextGame(-1);
          break;
        case ' ':
          if (selectedGameId) {
            e.preventDefault();
            toggleGameChecked(selectedGameId);
          }
          break;
        case 'Escape':
          setOpenMenuGameId(null);
          break;
      }
    },
    [
      editingGameId,
      selectedGameId,
      startEditingName,
      openGame,
      deleteGame,
      selectNextGame,
      toggleGameChecked,
    ]
  );

  const handleNameKeyDown = useCallback(
    (e: React.KeyboardEvent) => {
      if (e.key === 'Enter') {
        e.preventDefault();
        saveName();
      } else if (e.key === 'Escape') {
        e.preventDefault();
        cancelEditing();
      }
    },
    [saveName, cancelEditing]
  );

  const formatDate = (dateString: string) => {
    try {
      return new Date(dateString).toLocaleString(undefined, {
        dateStyle: 'short',
        timeStyle: 'short',
      });
    } catch {
      return dateString;
    }
  };

  const clearFilters = () => {
    setMaxRolls(null);
    setFilterGameState('');
  };

  /** Renders the inline ⋯ button + dropdown for a single game row. */
  const renderActionMenu = (game: SavedGameSummary, isSelected: boolean) => {
    const isOpen = openMenuGameId === game.gameId;
    return (
      <div className="relative" data-action-menu={isOpen ? 'open' : undefined}>
        <button
          className={`px-2 py-1 rounded text-sm font-bold transition-opacity select-none ${
            isSelected
              ? 'text-white opacity-90 hover:opacity-100 hover:bg-blue-500'
              : 'text-gray-600 opacity-70 hover:opacity-100 hover:bg-gray-200'
          }`}
          title="Actions"
          onClick={(e) => {
            e.stopPropagation();
            setOpenMenuGameId(isOpen ? null : game.gameId);
          }}
        >
          ⋯
        </button>

        {isOpen && (
          <div className="absolute right-0 top-full mt-1 z-50 bg-white border border-gray-200 rounded-lg shadow-xl min-w-[140px] py-1 text-black text-sm">
            <button
              className="w-full text-left px-4 py-2 hover:bg-gray-100 flex items-center gap-2"
              onClick={(e) => {
                e.stopPropagation();
                openGame(game.gameId);
              }}
            >
              <span>▶</span> Open
            </button>
            <button
              className="w-full text-left px-4 py-2 hover:bg-gray-100 flex items-center gap-2"
              onClick={(e) => {
                e.stopPropagation();
                handleCopyGame(game.gameId);
              }}
            >
              <span>⧉</span> Copy
            </button>
            <button
              className="w-full text-left px-4 py-2 hover:bg-gray-100 flex items-center gap-2"
              onClick={(e) => {
                e.stopPropagation();
                handleReplayGame(game.gameId);
              }}
            >
              <span>↺</span> Replay
            </button>
            <button
              className="w-full text-left px-4 py-2 hover:bg-gray-100 flex items-center gap-2"
              onClick={(e) => {
                e.stopPropagation();
                startEditingName(game.gameId);
              }}
            >
              <span>✏</span> Rename
            </button>
            <div className="border-t border-gray-100 my-1" />
            <button
              className="w-full text-left px-4 py-2 hover:bg-red-50 text-red-600 flex items-center gap-2"
              onClick={(e) => {
                e.stopPropagation();
                deleteGame(game.gameId);
              }}
            >
              <span>🗑</span> Delete
            </button>
          </div>
        )}
      </div>
    );
  };

  return (
    <MainLayout title="Open Game" onBack={() => router.push('/')}>
      <div className="px-8 pb-8 md:px-12 md:pb-12 max-w-5xl">
        {/* Error message */}
        {errorMessage && (
          <div className="bg-red-600 text-white px-4 py-3 rounded mb-4">{errorMessage}</div>
        )}

        {/* Loading state */}
        {isLoading && <p className="text-gray-400">Loading saved games...</p>}

        {/* Empty state */}
        {!isLoading && savedGames.length === 0 && (
          <p className="text-gray-400">No saved games found.</p>
        )}

        {/* Filter bar and bulk actions */}
        {!isLoading && savedGames.length > 0 && (
          <div className="mb-4 flex flex-wrap items-center gap-4 bg-gray-800 p-4 rounded-lg">
            <div className="flex items-center gap-2">
              <label className="text-gray-300 text-sm">Max rolls:</label>
              <input
                type="number"
                min="0"
                placeholder="Any"
                value={maxRolls ?? ''}
                onChange={(e) => setMaxRolls(e.target.value ? parseInt(e.target.value, 10) : null)}
                className="w-20 px-2 py-1 bg-gray-700 text-white border border-gray-600 rounded text-sm"
              />
            </div>

            <div className="flex items-center gap-2">
              <label className="text-gray-300 text-sm">State:</label>
              <select
                value={filterGameState}
                onChange={(e) => setFilterGameState(e.target.value)}
                className="px-2 py-1 bg-gray-700 text-white border border-gray-600 rounded text-sm"
              >
                <option value="">All</option>
                {gameStates.map((state) => (
                  <option key={state} value={state}>
                    {state}
                  </option>
                ))}
              </select>
            </div>

            {(maxRolls !== null || filterGameState) && (
              <button
                onClick={clearFilters}
                className="px-3 py-1 bg-gray-600 text-white rounded text-sm hover:bg-gray-500"
              >
                Clear filters
              </button>
            )}

            <div className="flex-1" />

            <div className="text-gray-400 text-sm">
              {filteredGames.length} of {savedGames.length} games
              {checkedGameIds.size > 0 && (
                <span className="ml-2 text-amber-400">({checkedGameIds.size} selected)</span>
              )}
            </div>

            {checkedGameIds.size > 0 && (
              <button
                onClick={deleteCheckedGames}
                disabled={isDeleting}
                className="px-4 py-1 bg-red-600 text-white rounded text-sm hover:bg-red-700 disabled:opacity-50 disabled:cursor-not-allowed flex items-center gap-2"
              >
                {isDeleting ? <>Deleting...</> : <>🗑️ Delete {checkedGameIds.size} selected</>}
              </button>
            )}
          </div>
        )}

        {/* Desktop: Table layout */}
        {!isLoading && savedGames.length > 0 && (
          <>
            <table
              ref={tableRef}
              className="w-full border-collapse bg-white text-black text-sm hidden md:table outline-none"
              tabIndex={0}
              onKeyDown={handleTableKeyDown}
            >
              <thead>
                <tr className="bg-gray-100">
                  <th className="w-10 text-center border border-gray-300 px-2 py-2">
                    <input
                      type="checkbox"
                      checked={allFilteredChecked}
                      ref={(el) => {
                        if (el) el.indeterminate = someFilteredChecked;
                      }}
                      onChange={toggleAllFiltered}
                      className="w-4 h-4 cursor-pointer"
                      title="Select all filtered"
                    />
                  </th>
                  <th className="w-12 text-center border border-gray-300 px-3 py-2"></th>
                  <th className="border border-gray-300 border-b-2 border-b-gray-500 px-3 py-2 text-left font-semibold text-gray-700">
                    Name
                  </th>
                  <th className="border border-gray-300 border-b-2 border-b-gray-500 px-3 py-2 text-left font-semibold text-gray-700">
                    Players
                  </th>
                  <th className="border border-gray-300 border-b-2 border-b-gray-500 px-3 py-2 text-center font-semibold text-gray-700">
                    Rolls
                  </th>
                  <th className="border border-gray-300 border-b-2 border-b-gray-500 px-3 py-2 text-left font-semibold text-gray-700">
                    State
                  </th>
                  <th className="border border-gray-300 border-b-2 border-b-gray-500 px-3 py-2 text-left font-semibold text-gray-700">
                    Type
                  </th>
                  <th className="border border-gray-300 border-b-2 border-b-gray-500 px-3 py-2 text-left font-semibold text-gray-700">
                    Saved
                  </th>
                </tr>
              </thead>
              <tbody>
                {filteredGames.map((game, index) => {
                  const isSelected = selectedGameId === game.gameId;
                  const isEditing = editingGameId === game.gameId;
                  const isChecked = checkedGameIds.has(game.gameId);

                  return (
                    <tr
                      key={game.gameId}
                      className={`cursor-pointer transition-colors ${
                        isSelected
                          ? 'bg-blue-600 text-white hover:bg-blue-700'
                          : isChecked
                            ? 'bg-amber-100 hover:bg-amber-200'
                            : index % 2 === 0
                              ? 'bg-white hover:bg-blue-50'
                              : 'bg-gray-50 hover:bg-blue-50'
                      }`}
                      onClick={() => selectGame(game.gameId)}
                      onDoubleClick={() => openGame(game.gameId)}
                    >
                      <td className="border border-gray-300 px-2 py-2 text-center">
                        <input
                          type="checkbox"
                          checked={isChecked}
                          onChange={() => toggleGameChecked(game.gameId)}
                          onClick={(e) => e.stopPropagation()}
                          className="w-4 h-4 cursor-pointer"
                        />
                      </td>

                      <td className="border border-gray-300 px-2 py-1 text-center">
                        {renderActionMenu(game, isSelected)}
                      </td>

                      <td className="border border-gray-300 px-3 py-2 min-w-[150px]">
                        {isEditing ? (
                          <input
                            ref={inputRef}
                            type="text"
                            className="w-full px-2 py-1 border-2 border-blue-500 rounded text-sm text-black bg-white outline-none focus:ring-2 focus:ring-blue-300"
                            value={editingName}
                            onChange={(e) => setEditingName(e.target.value)}
                            onBlur={saveName}
                            onKeyDown={handleNameKeyDown}
                            onClick={(e) => e.stopPropagation()}
                          />
                        ) : (
                          game.gameName
                        )}
                      </td>

                      <td className="border border-gray-300 px-3 py-2">
                        {displayPlayerNames(game)}
                      </td>
                      <td className="border border-gray-300 px-3 py-2 text-center">
                        {game.turnCount}
                      </td>
                      <td className="border border-gray-300 px-3 py-2">{game.gameState}</td>
                      <td className="border border-gray-300 px-3 py-2">{game.gameType}</td>
                      <td className="border border-gray-300 px-3 py-2">
                        {formatDate(game.savedAt)}
                      </td>
                    </tr>
                  );
                })}
              </tbody>
            </table>

            {filteredGames.length === 0 && (
              <div className="hidden md:block bg-white text-gray-500 p-8 text-center">
                No games match the current filters.
              </div>
            )}

            <div className="hidden md:block mt-4 text-xs text-gray-500">
              Double-click to open · F2 to rename · Delete key to remove · Space to toggle checkbox
            </div>

            {/* Mobile: Card layout */}
            <div className="flex flex-col gap-4 md:hidden">
              {filteredGames.map((game) => {
                const isSelected = selectedGameId === game.gameId;
                const isChecked = checkedGameIds.has(game.gameId);

                return (
                  <div
                    key={game.gameId}
                    className={`bg-white rounded-xl p-3 shadow-lg border-3 ${
                      isSelected
                        ? 'border-blue-500 bg-blue-50'
                        : isChecked
                          ? 'border-amber-400 bg-amber-50'
                          : 'border-transparent'
                    }`}
                    onClick={() => selectGame(game.gameId)}
                  >
                    <div className="flex items-center gap-2">
                      <input
                        type="checkbox"
                        checked={isChecked}
                        onChange={() => toggleGameChecked(game.gameId)}
                        onClick={(e) => e.stopPropagation()}
                        className="w-5 h-5 cursor-pointer flex-shrink-0"
                      />

                      <div className="flex-1 min-w-0 overflow-hidden">
                        <div className="text-base font-semibold text-gray-800 truncate">
                          {game.gameName}
                        </div>
                        <div className="text-sm text-gray-600 truncate">
                          {displayPlayerNames(game)}
                        </div>
                        <div className="text-xs text-gray-500 truncate">
                          {game.gameState}
                          <span className="mx-1 text-gray-400">•</span>
                          {game.turnCount} rolls
                          <span className="mx-1 text-gray-400">•</span>
                          {formatDate(game.savedAt)}
                        </div>
                      </div>

                      <div className="flex-shrink-0">{renderActionMenu(game, isSelected)}</div>
                    </div>
                  </div>
                );
              })}

              {filteredGames.length === 0 && (
                <div className="bg-white text-gray-500 p-8 text-center rounded-xl">
                  No games match the current filters.
                </div>
              )}
            </div>
          </>
        )}
      </div>
    </MainLayout>
  );
}
