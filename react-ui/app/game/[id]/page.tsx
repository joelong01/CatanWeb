'use client';

/**
 * Game Page - Main game view with GameBoard and floating panels.
 *
 * Architecture (per react-game-page.md):
 * - GameBoard with internal pan/zoom (NOT BoardViewport - deferred to later)
 * - Floating draggable/resizable panels overlay the board
 * - GameModel from SignalR drives all rendering
 */

import { useMemo, useCallback, useEffect } from 'react';
import { useParams } from 'next/navigation';
import { MainLayout } from '@/components/layout';
import { useGameConnection } from '@/lib/hooks/useGameConnection';
import { useGameStore } from '@/lib/stores/gameStore';
import { useLayoutStore } from '@/lib/stores/layoutStore';
import { GameBoard, type BoardPlayer } from '@/components/game/board';
import { FloatingPanel } from '@/components/game/panels/FloatingPanel';
import { PlayersPanel } from '@/components/game/panels/PlayersPanel';
import { GameResourcesHeader } from '@/components/game/panels/GameResourcesHeader';
import { RollRing, type RollStats } from '@/components/game/controls/RollRing';
import { ActionCluster, type EnabledButtons, type PurchaseStats } from '@/components/game/controls/ActionCluster';
import { MeasurementCluster, type StarCounts } from '@/components/game/controls/MeasurementCluster';
import { NUMBER_PIPS } from '@/lib/constants/board-assets';
import { cubicCoord, getNeighbor, Direction, type HexCoordinate } from '@/components/hex-grid/hex-geometry';
import type { HexPosition } from '@/types/generated/models/hex-position';
import { createPlayerColors, type PlayerColorsWithGradient } from '@/lib/utils/playerColors';
import { gameApi } from '@/lib/api/gameApi';
import { DEFAULT_PLAYER_COLORS } from '@/types/player-profile';
import type { GameState } from '@/types/generated/models/game-state';

/** Convert GameState enum to display string */
function getStateMessage(gameState: GameState | null | undefined): string {
  if (!gameState) return '';
  const messages: Partial<Record<GameState, string>> = {
    WaitingForRoll: 'Roll the Dice',
    WaitingForNext: 'Click Next',
    AllocateResourceForward: 'Place Settlement',
    AllocateResourceReverse: 'Place Settlement',
    MustMoveRobber: 'Move the Robber',
    PickingBoard: 'Pick a Board',
    WaitingForRollForOrder: 'Roll for Turn Order',
    FinishedRollOrder: 'Select Who Goes First',
    TooManyCards: 'Discard Cards',
    GameOver: 'Game Over!',
    Supplemental: 'Supplemental Build Phase',
  };
  return messages[gameState] || gameState;
}

/** Get a temporary player ID for development */
function getDevPlayerId(): string {
  if (typeof window === 'undefined') return 'dev-player';

  // Check localStorage for existing ID
  const stored = localStorage.getItem('catan-dev-player-id');
  if (stored) return stored;

  // Generate new ID
  const newId = `player-${Date.now()}-${Math.random().toString(36).slice(2, 8)}`;
  localStorage.setItem('catan-dev-player-id', newId);
  return newId;
}

export default function GamePage(): React.ReactElement {
  const params = useParams();
  const gameId = params.id as string;

  // Get player ID (using dev ID for now)
  const playerId = useMemo(() => getDevPlayerId(), []);

  // Connect to game
  const connection = useGameConnection({
    playerId,
    gameId,
    autoConnect: true,
  });

  // Connection status
  const { isConnected, isConnecting, proxy } = connection;

  // Get game model and player profiles from store
  const gameModel = useGameStore((state) => state.gameModel);
  const playerProfiles = useGameStore((state) => state.playerProfiles);
  const setPlayerProfiles = useGameStore((state) => state.setPlayerProfiles);

  // Debug: log when gameModel changes
  console.log('[GamePage] render, gameModel tiles:', gameModel?.tiles?.length);

  // Load player profiles on mount (like Blazor LoadPlayerProfilesAsync)
  useEffect(() => {
    async function loadProfiles() {
      const result = await gameApi.getPlayers();
      if (result.success && result.data) {
        setPlayerProfiles(result.data);
      }
    }
    loadProfiles();
  }, [setPlayerProfiles]);

  // Get current player info
  const currentPlayer = useMemo(() => {
    if (!gameModel) return null;
    return gameModel.players.find(p => p.id === gameModel.currentPlayerId);
  }, [gameModel]);

  // Build BoardPlayer array for GameBoard using profiles for colors
  const boardPlayers = useMemo((): BoardPlayer[] => {
    if (!gameModel) return [];
    return gameModel.players.map(p => {
      const profile = playerProfiles.get(p.id);
      const colors = profile?.colors ?? DEFAULT_PLAYER_COLORS;
      return {
        id: p.id,
        name: p.name,
        colors: {
          primary: colors.primary,
          secondary: colors.secondary,
          foreground: colors.foreground,
        },
      };
    });
  }, [gameModel, playerProfiles]);

  // Compute roll statistics from game model
  const rollStats = useMemo((): Record<number, RollStats> => {
    const stats: Record<number, RollStats> = {};
    const rollCounts = gameModel?.rollModel?.gameRollModel?.rollCounts ?? [];
    const totalRolls = gameModel?.rollModel?.gameRollModel?.totalRolls ?? 0;

    for (let roll = 2; roll <= 12; roll++) {
      const count = rollCounts[roll] ?? 0;
      const percentage = totalRolls > 0 ? Math.round((count / totalRolls) * 100) : 0;
      stats[roll] = { count, percentage };
    }
    return stats;
  }, [gameModel?.rollModel?.gameRollModel]);

  // Handle roll click - convert roll sum to two dice values
  const handleRollClick = useCallback((rollSum: number) => {
    // Split into two dice values (prefer balanced split)
    const die1 = Math.max(1, Math.min(6, Math.ceil(rollSum / 2)));
    const die2 = rollSum - die1;

    // Ensure die2 is valid (1-6)
    if (die2 >= 1 && die2 <= 6) {
      proxy.roll(die1, die2);
    }
  }, [proxy]);

  // Check purchase capabilities
  const canPurchaseRoad = useMemo(() => {
    if (!gameModel || !currentPlayer) return false;
    return currentPlayer.unspentEntitlements.includes('Road');
  }, [gameModel, currentPlayer]);

  const canPurchaseSettlement = useMemo(() => {
    if (!gameModel || !currentPlayer) return false;
    return currentPlayer.unspentEntitlements.includes('Settlement');
  }, [gameModel, currentPlayer]);

  const canPurchaseCity = useMemo(() => {
    if (!gameModel || !currentPlayer) return false;
    return currentPlayer.unspentEntitlements.includes('City');
  }, [gameModel, currentPlayer]);

  const canPurchaseDevCard = useMemo(() => {
    if (!gameModel || !currentPlayer) return false;
    return currentPlayer.unspentEntitlements.includes('DevCard');
  }, [gameModel, currentPlayer]);

  // Action cluster enabled buttons
  const actionEnabledButtons = useMemo((): EnabledButtons => ({
    next: gameModel?.actionFlags?.nextEnabled ?? false,
    undo: gameModel?.actionFlags?.undoEnabled ?? false,
    redo: gameModel?.actionFlags?.redoEnabled ?? false,
    road: canPurchaseRoad,
    settlement: canPurchaseSettlement,
    city: canPurchaseCity,
    devCard: canPurchaseDevCard,
  }), [gameModel?.actionFlags, canPurchaseRoad, canPurchaseSettlement, canPurchaseCity, canPurchaseDevCard]);

  // Action cluster purchase stats - computed from current player (like Blazor's GetSpentCount)
  const actionPurchaseStats = useMemo((): PurchaseStats => {
    if (!currentPlayer) {
      return {
        roads: { bought: 0, available: 15 },
        settlements: { bought: 0, available: 5 },
        cities: { bought: 0, available: 4 },
        devCards: { bought: 0, available: 25 },
      };
    }

    // Count spent entitlements by type
    const spentEntitlements = currentPlayer.spentEntitlementsThisGame ?? [];
    const countSpent = (type: string) => spentEntitlements.filter(e => e === type).length;

    // Max values from resource rules (with defaults matching Blazor)
    const maxRoads = gameModel?.resourceRules?.maxRoads ?? 15;
    const maxSettlements = gameModel?.resourceRules?.maxSettlements ?? 5;
    const maxCities = gameModel?.resourceRules?.maxCities ?? 4;

    return {
      roads: { bought: countSpent('Road'), available: maxRoads },
      settlements: { bought: countSpent('Settlement'), available: maxSettlements },
      cities: { bought: countSpent('City'), available: maxCities },
      devCards: { bought: countSpent('DevCard'), available: 25 },
    };
  }, [currentPlayer, gameModel?.resourceRules]);

  // Player colors for controls - from player profile
  const playerColors = useMemo((): PlayerColorsWithGradient => {
    if (currentPlayer) {
      const profile = playerProfiles.get(currentPlayer.id);
      const colors = profile?.colors ?? DEFAULT_PLAYER_COLORS;
      return createPlayerColors(colors.primary, colors.secondary, colors.foreground);
    }
    return createPlayerColors(DEFAULT_PLAYER_COLORS.primary, DEFAULT_PLAYER_COLORS.secondary, DEFAULT_PLAYER_COLORS.foreground);
  }, [currentPlayer, playerProfiles]);

  // Compute star counts for MeasurementCluster (how many building spots have each star value)
  // Mirrors Blazor's GetStarCount - counts buildings whose adjacent tiles sum to exactly that star value
  const starCounts = useMemo((): StarCounts => {
    const counts: StarCounts = {};
    if (!gameModel?.buildings || !gameModel?.tiles) return counts;

    // Build a map of tile coords to their pip values
    const tilePipsMap = new Map<string, number>();
    gameModel.tiles.forEach((tile) => {
      const key = `${tile.tileKey.q},${tile.tileKey.r},${-tile.tileKey.q - tile.tileKey.r}`;
      const pips = NUMBER_PIPS[tile.number] ?? 0;
      tilePipsMap.set(key, pips);
    });

    // Map vertex position to neighbor directions (same as GameBoard)
    // Each vertex is shared by 3 hexes: the base hex + 2 neighbors
    const neighborDirections: Record<HexPosition, Direction[]> = {
      Right: [Direction.NorthEast, Direction.SouthEast],
      BottomRight: [Direction.SouthEast, Direction.South],
      BottomLeft: [Direction.South, Direction.SouthWest],
      Left: [Direction.SouthWest, Direction.NorthWest],
      TopLeft: [Direction.NorthWest, Direction.North],
      TopRight: [Direction.North, Direction.NorthEast],
      None: [], // Fallback
    };

    // Helper to compute stars for a building position
    const computeStars = (hexCoords: { q: number; r: number }, position: HexPosition): number => {
      const coord = cubicCoord(hexCoords.q, hexCoords.r);
      const adjacentCoords: HexCoordinate[] = [coord];

      // Add neighbor coords based on vertex position
      const directions = neighborDirections[position] ?? [];
      directions.forEach((dir) => {
        adjacentCoords.push(getNeighbor(coord, dir));
      });

      // Sum pips from all adjacent tiles
      let totalPips = 0;
      adjacentCoords.forEach((c) => {
        const key = `${c.q},${c.r},${c.s}`;
        totalPips += tilePipsMap.get(key) ?? 0;
      });

      return totalPips;
    };

    // Count buildings by their star value
    gameModel.buildings.forEach((building) => {
      const { hexCoordinates, position } = building.buildingKey;
      const stars = computeStars(hexCoordinates, position);

      // Increment count for this star value
      counts[stars] = (counts[stars] ?? 0) + 1;
    });

    return counts;
  }, [gameModel?.buildings, gameModel?.tiles]);

  // Action handler
  const handleAction = useCallback((action: string) => {
    switch (action) {
      case 'next': proxy.next(); break;
      case 'undo': proxy.undo(); break;
      case 'redo': proxy.redo(); break;
      case 'road': proxy.purchase('Road'); break;
      case 'settlement': proxy.purchase('Settlement'); break;
      case 'city': proxy.purchase('City'); break;
      case 'devcard': proxy.purchase('DevCard'); break;
    }
  }, [proxy]);

  // Shuffle handler for MeasurementCluster reset button
  const handleShuffle = useCallback(() => {
    proxy.shuffle();
  }, [proxy]);

  // Star filter changed handler (stores in layoutStore for board filtering)
  const setStarFilter = useLayoutStore((state) => state.setStarFilter);
  const handleStarFilterChange = useCallback((stars: number | null) => {
    setStarFilter(stars);
  }, [setStarFilter]);

  // Resource filter changed handler (stores in layoutStore for board filtering)
  const setResourceFilter = useLayoutStore((state) => state.setResourceFilter);
  const handleResourceSelectionChange = useCallback((resources: string[]) => {
    // Store first selected resource (or null if empty)
    setResourceFilter(resources.length > 0 ? resources[0] : null);
  }, [setResourceFilter]);

  // Game-specific menu action handlers
  const handleBalance = useCallback(() => {
    console.log('[GamePage] Balance board requested');
    proxy.balanceBoard();
  }, [proxy]);

  const handleWinner = useCallback(() => {
    // TODO: Show winner selection dialog
    console.log('[GamePage] Winner dialog requested');
    // For now, just log - need to implement winner dialog
  }, []);

  const handleSaveCopy = useCallback(async () => {
    const newName = window.prompt('Enter name for the copy:', '');
    if (newName === null) return; // User cancelled

    try {
      const result = await gameApi.copyGame(gameId, newName || undefined);
      if (result.success && result.data?.newGameId) {
        // Navigate to the new game copy
        window.location.href = `/game/${result.data.newGameId}`;
      }
    } catch (error) {
      console.error('[GamePage] Save copy failed:', error);
    }
  }, [gameId]);

  // Check if game is in PickingBoard state (for Balance button)
  const isPickingBoard = gameModel?.gameState === 'PickingBoard';

  // Game actions for NavMenu
  const gameActions = useMemo(() => ({
    isPickingBoard,
    onBalance: handleBalance,
    onWinner: handleWinner,
    onSaveCopy: handleSaveCopy,
  }), [isPickingBoard, handleBalance, handleWinner, handleSaveCopy]);

  return (
    <MainLayout activeGameId={gameId} gameActions={gameActions}>
      <div className="relative w-full h-full overflow-hidden">
        {/* GameBoard with internal pan/zoom - fills the viewport */}
        <GameBoard
          gameModel={gameModel}
          hexSize={50}
          gap={1}
          players={boardPlayers}
          selectedPlayerId={currentPlayer?.id}
        />

        {/* Connection status overlay */}
        {!isConnected && (
          <div className="absolute top-4 left-1/2 -translate-x-1/2 bg-gray-900/90 rounded-lg px-4 py-2 text-sm z-50">
            {isConnecting ? (
              <span className="text-amber-400">Connecting to game...</span>
            ) : (
              <span className="text-red-400">Disconnected - Reconnecting...</span>
            )}
          </div>
        )}

        {/* Floating Panels - overlay on top of GameBoard */}
        {/* Dice Panel - Roll statistics and click-to-roll */}
        <FloatingPanel panelId="dice" title="Dice" icon="🎲">
          <RollRing
            rollStats={rollStats}
            onRollClick={handleRollClick}
            colors={playerColors}
          />
        </FloatingPanel>

        {/* Action Controls Panel */}
        <FloatingPanel panelId="actions" title="Actions" icon="⚡">
          <ActionCluster
            colors={playerColors}
            gameState={getStateMessage(gameModel?.gameState)}
            onAction={handleAction}
            purchaseStats={actionPurchaseStats}
            enabledButtons={actionEnabledButtons}
          />
        </FloatingPanel>

        {/* Board Measurements Panel */}
        <FloatingPanel panelId="measurements" title="Board" icon="📊">
          <MeasurementCluster
            gameModel={gameModel}
            colors={playerColors}
            starCounts={starCounts}
            onReset={handleShuffle}
            onStarFilterChange={handleStarFilterChange}
            onResourceSelectionChange={handleResourceSelectionChange}
          />
        </FloatingPanel>

        {/* Players Panel */}
        <FloatingPanel panelId="players" title="Players" icon="👥" resizable>
          <PlayersPanel gameModel={gameModel} />
        </FloatingPanel>

        {/* Resources Panel */}
        <FloatingPanel panelId="resources" title="Resources" icon="📦">
          <GameResourcesHeader resources={gameModel?.gameResourcesModel ?? null} />
        </FloatingPanel>

        {/* Game info badges - minimal floating overlays */}
        <div className="absolute bottom-2 left-2 bg-black/60 rounded px-2 py-1 text-xs text-gray-300 z-30 pointer-events-none">
          {gameModel?.gameName || gameId}
        </div>
        <div className="absolute bottom-2 right-2 flex items-center gap-2 z-30 pointer-events-none">
          <span className="bg-black/60 rounded px-2 py-1 text-xs text-gray-300">
            {gameModel?.gameState || 'Loading...'}
          </span>
          <span className="bg-black/60 rounded px-2 py-1 text-xs">
            {isConnected ? (
              <span className="text-green-400">● Connected</span>
            ) : (
              <span className="text-amber-400">○ {isConnecting ? 'Connecting' : 'Disconnected'}</span>
            )}
          </span>
        </div>

      </div>
    </MainLayout>
  );
}
