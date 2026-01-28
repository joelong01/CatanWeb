'use client';

/**
 * Game Page - Main game view with GameBoard and floating panels.
 *
 * Architecture (per react-game-page.md):
 * - GameBoard with internal pan/zoom (NOT BoardViewport - deferred to later)
 * - Floating draggable/resizable panels overlay the board
 * - GameModel from SignalR drives all rendering
 */

import { useMemo, useCallback, useEffect, useState, useRef } from 'react';
import { useParams } from 'next/navigation';
import { MainLayout } from '@/components/layout';
import { useGameConnection } from '@/lib/hooks/useGameConnection';
import { useGameStore } from '@/lib/stores/gameStore';
import { useLayoutStore } from '@/lib/stores/layoutStore';
import { GameBoard, type BoardPlayer } from '@/components/game/board';
import { FloatingPanel } from '@/components/game/panels/FloatingPanel';
import { GoFirstOverlay } from '@/components/game/overlays/GoFirstOverlay';
import { RobberTargetMenu } from '@/components/game/overlays/RobberTargetMenu';
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
import type { BuildingKey } from '@/types/generated/models/building-key';
import type { RoadKey } from '@/types/generated/models/road-key';
import type { BoardGameData } from '@/components/game/board';
import type { TileModel } from '@/types/generated/models/tile-model';
import type { HexCoordinates } from '@/types/generated/models/hex-coordinates';
import type { PlayerModel } from '@/types/generated/models/player-model';

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
  console.log('[GamePage] render, gameState:', gameModel?.gameState, 'tiles:', gameModel?.tiles?.length, 'players:', gameModel?.players?.length);

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

      // Trigger tile dimming animation (matches Blazor DimTiles)
      // Clear any existing timer
      if (rollDimTimerRef.current) {
        clearTimeout(rollDimTimerRef.current);
      }
      // Set the rolled number to dim non-matching tiles
      setLastRolledNumber(rollSum);
      // Clear after 5 seconds (matches Blazor TileDimDurationSeconds)
      rollDimTimerRef.current = setTimeout(() => {
        setLastRolledNumber(null);
      }, 5000);
    }
  }, [proxy]);

  // Helper to get enabled state from entitlementPurchaseModel (matches Blazor GetIsFaceUp)
  const getPurchaseEnabled = useCallback((entitlement: string): boolean => {
    if (!gameModel?.entitlementPurchaseModel) return false;
    const model = gameModel.entitlementPurchaseModel.find(m => m.entitlement === entitlement);
    return model?.enabled ?? false;
  }, [gameModel?.entitlementPurchaseModel]);

  // Check purchase capabilities - uses entitlementPurchaseModel.enabled (matches Blazor)
  const canPurchaseRoad = useMemo(() => getPurchaseEnabled('Road'), [getPurchaseEnabled]);
  const canPurchaseSettlement = useMemo(() => getPurchaseEnabled('Settlement'), [getPurchaseEnabled]);
  const canPurchaseCity = useMemo(() => getPurchaseEnabled('City'), [getPurchaseEnabled]);
  const canPurchaseDevCard = useMemo(() => getPurchaseEnabled('DevCard'), [getPurchaseEnabled]);
  const canPlaySoldier = useMemo(() => getPurchaseEnabled('Soldier'), [getPurchaseEnabled]);

  // Action cluster enabled buttons
  const actionEnabledButtons = useMemo((): EnabledButtons => ({
    next: gameModel?.actionFlags?.nextEnabled ?? false,
    undo: gameModel?.actionFlags?.undoEnabled ?? false,
    redo: gameModel?.actionFlags?.redoEnabled ?? false,
    soldier: canPlaySoldier,
    road: canPurchaseRoad,
    settlement: canPurchaseSettlement,
    city: canPurchaseCity,
    devCard: canPurchaseDevCard,
  }), [gameModel?.actionFlags, canPlaySoldier, canPurchaseRoad, canPurchaseSettlement, canPurchaseCity, canPurchaseDevCard]);

  // Action cluster purchase stats - computed from current player
  // Shows UNSPENT entitlements (pending placement) as the count badge
  const actionPurchaseStats = useMemo((): PurchaseStats => {
    if (!currentPlayer) {
      return {
        roads: { bought: 0, available: 15 },
        settlements: { bought: 0, available: 5 },
        cities: { bought: 0, available: 4 },
        devCards: { bought: 0, available: 25 },
        soldier: { played: 0, available: 0 },
      };
    }

    // Count UNSPENT entitlements (pending placement) - this is what shows in the badge
    const unspentEntitlements = currentPlayer.unspentEntitlements ?? [];
    const countUnspent = (type: string) => unspentEntitlements.filter(e => e === type).length;

    // Max values from resource rules (with defaults matching Blazor)
    const maxRoads = gameModel?.resourceRules?.maxRoads ?? 15;
    const maxSettlements = gameModel?.resourceRules?.maxSettlements ?? 5;
    const maxCities = gameModel?.resourceRules?.maxCities ?? 4;

    // Count spent Soldier entitlements (soldiers played this game)
    const spentEntitlements = currentPlayer.spentEntitlementsThisGame ?? [];
    const countSpent = (type: string) => spentEntitlements.filter(e => e === type).length;

    return {
      roads: { bought: countUnspent('Road'), available: maxRoads },
      settlements: { bought: countUnspent('Settlement'), available: maxSettlements },
      cities: { bought: countUnspent('City'), available: maxCities },
      devCards: { bought: countUnspent('DevCard'), available: 25 },
      soldier: { played: countSpent('Soldier'), available: countUnspent('Soldier') },
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
      case 'soldier': proxy.purchase('Soldier'); break;
    }
  }, [proxy]);

  // Shuffle handler for MeasurementCluster reset button
  const handleShuffle = useCallback(() => {
    proxy.shuffle();
  }, [proxy]);

  // Building click handler - calls upgradeBuilding for placement
  const handleBuildingClick = useCallback((buildingKey: BuildingKey) => {
    console.log('[GamePage] Building clicked:', buildingKey);
    proxy.upgradeBuilding(buildingKey);
  }, [proxy]);

  // Road click handler - calls purchaseRoad for placement
  const handleRoadClick = useCallback((roadKey: RoadKey) => {
    console.log('[GamePage] Road clicked:', roadKey);
    proxy.purchaseRoad(roadKey);
  }, [proxy]);

  // GoFirst handler - selects which player goes first
  const handleGoFirst = useCallback((playerId: string) => {
    proxy.goFirst(playerId);
  }, [proxy]);

  // Robber state for target selection (when multiple players on tile)
  const [pendingRobberCoords, setPendingRobberCoords] = useState<HexCoordinates | null>(null);
  const [pendingRobberTile, setPendingRobberTile] = useState<TileModel | null>(null);
  const [robberTargetPlayers, setRobberTargetPlayers] = useState<{ id: string; name: string }[]>([]);
  const [robberMenuPosition, setRobberMenuPosition] = useState({ x: 0, y: 0 });

  // Roll dimming: track last roll and timer (5 seconds, matching Blazor TileDimDurationSeconds)
  const [lastRolledNumber, setLastRolledNumber] = useState<number | null>(null);
  const rollDimTimerRef = useRef<NodeJS.Timeout | null>(null);

  // Helper: Get players with buildings adjacent to a tile coordinate
  // Returns simple { id, name } objects matching Blazor's RobberTarget record
  const getPlayersWithBuildingsOnTile = useCallback((tileCoords: HexCoordinates): { id: string; name: string }[] => {
    if (!gameModel?.buildings || !gameModel.players) return [];

    const targetPlayerIds = new Set<string>();

    // Check all buildings to find ones adjacent to this tile
    gameModel.buildings.forEach(building => {
      // Skip unowned buildings
      if (!building.ownerId) return;
      // Skip current player (can't steal from yourself)
      if (building.ownerId === currentPlayer?.id) return;

      const buildingCoord = building.buildingKey.hexCoordinates;
      const position = building.buildingKey.position;

      // Check if this building is on or adjacent to the target tile
      // A building at a vertex touches up to 3 tiles
      const buildingHexCoord = cubicCoord(buildingCoord.q, buildingCoord.r);

      // Map vertex position to which neighbors also touch this vertex
      const neighborDirections: Record<HexPosition, Direction[]> = {
        Right: [Direction.NorthEast, Direction.SouthEast],
        BottomRight: [Direction.SouthEast, Direction.South],
        BottomLeft: [Direction.South, Direction.SouthWest],
        Left: [Direction.SouthWest, Direction.NorthWest],
        TopLeft: [Direction.NorthWest, Direction.North],
        TopRight: [Direction.North, Direction.NorthEast],
        None: [],
      };

      // Get all tiles this building touches
      const touchedTiles: HexCoordinate[] = [buildingHexCoord];
      const directions = neighborDirections[position as HexPosition] ?? [];
      directions.forEach((dir) => {
        touchedTiles.push(getNeighbor(buildingHexCoord, dir));
      });

      // Check if target tile is in the touched tiles
      const targetCoord = cubicCoord(tileCoords.q, tileCoords.r);
      const touchesTarget = touchedTiles.some(
        t => t.q === targetCoord.q && t.r === targetCoord.r
      );

      if (touchesTarget && (building.buildingState === 'Settlement' || building.buildingState === 'City')) {
        targetPlayerIds.add(building.ownerId);
      }
    });

    // Convert IDs to { id, name } objects matching Blazor's RobberTarget
    // Double-filter to ensure current player is excluded (defensive check)
    const currentId = currentPlayer?.id;
    return gameModel.players
      .filter(p => targetPlayerIds.has(p.id) && p.id !== currentId)
      .map(p => ({ id: p.id, name: p.name }));
  }, [gameModel?.buildings, gameModel?.players, currentPlayer?.id]);

  // Tile right-click handler for robber movement (matches Blazor: right-click shows menu)
  const handleTileRightClick = useCallback((tile: TileModel, event: React.MouseEvent) => {
    // Only handle during MustMoveRobber state
    if (gameModel?.gameState !== 'MustMoveRobber') return;

    // Can't place robber on water/sea tiles
    if (tile.resourceTileType === 'Sea' || tile.resourceTileType === 'Back') return;

    // Can't place robber on current position (unless Desert for GriefDodgy)
    const robberCoords = gameModel.robber?.coordinates;
    if (robberCoords &&
        tile.tileKey.q === robberCoords.q &&
        tile.tileKey.r === robberCoords.r &&
        tile.resourceTileType !== 'Desert') {
      console.log('[GamePage] Cannot place robber on current position');
      return;
    }

    const coords: HexCoordinates = { q: tile.tileKey.q, r: tile.tileKey.r, s: -tile.tileKey.q - tile.tileKey.r };
    const targetPlayers = getPlayersWithBuildingsOnTile(coords);

    console.log('[GamePage] Tile right-clicked for robber:', coords, 'targets:', targetPlayers.length);

    // Always show menu (matches Blazor behavior - menu has "Nobody" option)
    setPendingRobberCoords(coords);
    setPendingRobberTile(tile);
    setRobberTargetPlayers(targetPlayers);
    setRobberMenuPosition({ x: event.clientX, y: event.clientY });
  }, [gameModel?.gameState, gameModel?.robber?.coordinates, getPlayersWithBuildingsOnTile]);

  // Handler for selecting a robber target from the menu
  // playerId is undefined when "Nobody. Hatred Deferred." is selected
  const handleRobberTargetSelect = useCallback((playerId: string | undefined) => {
    if (pendingRobberCoords) {
      proxy.moveRobber(pendingRobberCoords, playerId);
      setPendingRobberCoords(null);
      setPendingRobberTile(null);
      setRobberTargetPlayers([]);
    }
  }, [pendingRobberCoords, proxy]);

  // Cancel robber target selection
  const handleRobberTargetCancel = useCallback(() => {
    setPendingRobberCoords(null);
    setPendingRobberTile(null);
    setRobberTargetPlayers([]);
  }, []);

  // Keyboard shortcuts for road building (1-9) and city upgrades (A-Z)
  useEffect(() => {
    const handleKeyDown = (e: KeyboardEvent) => {
      // Ignore if typing in an input field
      if (e.target instanceof HTMLInputElement || e.target instanceof HTMLTextAreaElement) {
        return;
      }

      const key = e.key.toUpperCase();

      // Handle number keys (1-9) for road building or settlement placement
      const num = parseInt(e.key);
      if (!isNaN(num) && num >= 1 && num <= 9) {
        // First try roads (they have buildIndex from server)
        const road = gameModel?.roads?.find(r =>
          r.roadState === 'Buildable' && r.buildIndex === num
        );
        if (road) {
          console.log('[GamePage] Keyboard shortcut: building road', num);
          proxy.purchaseRoad(road.roadKey);
          return;
        }

        // Then try settlements (only during regular gameplay, not allocation)
        const hasSettlementEntitlement = currentPlayer?.unspentEntitlements?.includes('Settlement');
        const inAllocation = gameModel?.gameState === 'AllocateResourceForward' ||
                            gameModel?.gameState === 'AllocateResourceReverse';

        if (hasSettlementEntitlement && !inAllocation && gameModel?.buildings) {
          // Build list of possible settlements (same order as GameBoard)
          const possibleSettlements = gameModel.buildings.filter(b =>
            b.buildingState === 'PossibleSettlement' && b.ownerId === null
          );

          // 1-based index (num=1 -> index 0)
          const settlementIndex = num - 1;

          if (settlementIndex >= 0 && settlementIndex < possibleSettlements.length) {
            const settlement = possibleSettlements[settlementIndex];
            console.log('[GamePage] Keyboard shortcut: placing settlement', num);
            proxy.upgradeBuilding(settlement.buildingKey);
          }
        }
        return;
      }

      // Handle letter keys (A-Z) for city upgrades
      if (key >= 'A' && key <= 'Z') {
        // Check if current player has City entitlement
        const hasCityEntitlement = currentPlayer?.unspentEntitlements?.includes('City');
        if (!hasCityEntitlement || !gameModel?.buildings || !currentPlayer) return;

        // Build list of upgradeable settlements (same logic as GameBoard)
        const upgradeableSettlements = gameModel.buildings.filter(b =>
          b.buildingState === 'Settlement' && b.ownerId === currentPlayer.id
        );

        // Map letter to index (A=0, B=1, etc.)
        const letterIndex = key.charCodeAt(0) - 65; // 'A' = 65

        if (letterIndex >= 0 && letterIndex < upgradeableSettlements.length) {
          const settlement = upgradeableSettlements[letterIndex];
          console.log('[GamePage] Keyboard shortcut: upgrading settlement', key);
          proxy.upgradeBuilding(settlement.buildingKey);
        }
      }
    };

    window.addEventListener('keydown', handleKeyDown);
    return () => window.removeEventListener('keydown', handleKeyDown);
  }, [gameModel?.roads, gameModel?.buildings, gameModel?.gameState, currentPlayer, proxy]);

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

  // Check if we're in allocation phase (PickingResources in Blazor)
  // During allocation, settlement build indexes are NOT shown
  const isAllocationPhase = gameModel?.gameState === 'AllocateResourceForward' ||
                            gameModel?.gameState === 'AllocateResourceReverse';

  // Show settlement indexes only during regular gameplay when player has Settlement entitlement
  const showSettlementIndexes = useMemo(() => {
    if (isAllocationPhase) return false;
    return currentPlayer?.unspentEntitlements?.includes('Settlement') ?? false;
  }, [isAllocationPhase, currentPlayer?.unspentEntitlements]);

  // Create board game data with current player entitlements for GameBoard
  const boardGameData = useMemo((): BoardGameData | null => {
    if (!gameModel) return null;
    return {
      tiles: gameModel.tiles,
      harbors: gameModel.harbors,
      buildings: gameModel.buildings,
      roads: gameModel.roads,
      currentPlayerEntitlements: currentPlayer?.unspentEntitlements ?? [],
      robber: gameModel.robber,
    };
  }, [gameModel, currentPlayer?.unspentEntitlements]);

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
          gameModel={boardGameData}
          hexSize={50}
          gap={1}
          players={boardPlayers}
          selectedPlayerId={currentPlayer?.id}
          onBuildingClick={handleBuildingClick}
          onRoadClick={handleRoadClick}
          onTileRightClick={handleTileRightClick}
          rolledNumber={lastRolledNumber}
          showSettlementIndexes={showSettlementIndexes}
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
            shuffleEnabled={gameModel?.gameState === 'PickingBoard'}
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

        {/* GoFirst Overlay - shown during FinishedRollOrder state */}
        {gameModel?.gameState === 'FinishedRollOrder' && (
          <FloatingPanel panelId="goFirst" title="Go First" icon="🎯">
            <GoFirstOverlay
              players={gameModel.players}
              playerProfiles={playerProfiles}
              onSelectPlayer={handleGoFirst}
            />
          </FloatingPanel>
        )}

        {/* Robber Target Menu - shown when selecting steal target */}
        {pendingRobberTile && (
          <RobberTargetMenu
            targetPlayers={robberTargetPlayers}
            position={robberMenuPosition}
            onSelectTarget={handleRobberTargetSelect}
            onCancel={handleRobberTargetCancel}
          />
        )}

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
