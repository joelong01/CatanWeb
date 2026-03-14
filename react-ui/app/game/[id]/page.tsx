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
import { useLayoutStore, selectPanel } from '@/lib/stores/layoutStore';
import {
  useGameState,
  usePlayers,
  useTiles,
  useBuildings,
  useRoads,
  useRobber,
  useActionFlags,
  useCurrentPlayer,
  usePlayerProfiles,
  useSetPlayerProfiles,
  useRollStats,
  useSetLastRoll,
} from '@/lib/stores/gameStoreHooks';
import { GameBoard } from '@/components/game/board';
import { FloatingPanel, MinimizedBar } from '@/components/game/panels';
import { GoFirstOverlay } from '@/components/game/overlays/GoFirstOverlay';
import { SupplementalOverlay } from '@/components/game/overlays/SupplementalOverlay';
import { RobberTargetMenu } from '@/components/game/overlays/RobberTargetMenu';
import { WinnerOverlay, type WinnerPlayer } from '@/components/game/overlays/WinnerOverlay';
import { PlayersPanel } from '@/components/game/panels/PlayersPanel';
import { GameResourcesHeader } from '@/components/game/panels/GameResourcesHeader';
import { RollRing } from '@/components/game/controls/RollRing';
import {
  ActionCluster,
  type EnabledButtons,
  type PurchaseStats,
} from '@/components/game/controls/ActionCluster';
import { MeasurementCluster, type StarCounts } from '@/components/game/controls/MeasurementCluster';
import { NUMBER_PIPS } from '@/lib/constants/board-assets';
import {
  cubicCoord,
  getNeighbor,
  Direction,
  type HexCoordinate,
} from '@/components/hex-grid/hex-geometry';
import type { HexPosition } from '@/types/generated/models/hex-position';
import { createPlayerColors, type PlayerColorsWithGradient } from '@/lib/utils/playerColors';
import { gameApi } from '@/lib/api/gameApi';
import { getServiceUrl } from '@/lib/config';
import { DEFAULT_PLAYER_COLORS } from '@/types/player-profile';
import type { BuildingKey } from '@/types/generated/models/building-key';
import type { RoadKey } from '@/types/generated/models/road-key';
// BoardGameData removed - GameBoard uses internal hooks now
import type { TileModel } from '@/types/generated/models/tile-model';
import type { HexCoordinates } from '@/types/generated/models/hex-coordinates';
import { getDevPlayerId } from '@/lib/utils/getDevPlayerId';
import { getStateMessage } from '@/lib/utils/gameStateMessages';

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

  // Save current game ID to localStorage for "Return to Game" navigation
  useEffect(() => {
    localStorage.setItem('current_gameId', gameId);
  }, [gameId]);

  // Connection status
  const { isConnected, isConnecting, proxy } = connection;

  // Fine-grained store subscriptions (optimized for re-render performance)
  // Each hook uses custom equality functions to prevent unnecessary re-renders
  const gameState = useGameState();
  const players = usePlayers();
  const tiles = useTiles();
  const buildings = useBuildings();
  const roads = useRoads();
  const robber = useRobber();
  const actionFlags = useActionFlags();
  const currentPlayer = useCurrentPlayer();
  const playerProfiles = usePlayerProfiles();
  const setPlayerProfiles = useSetPlayerProfiles();
  const rollStats = useRollStats();
  const setLastRoll = useSetLastRoll();

  // Narrow selectors for less common fields (no dedicated hooks yet)
  const entitlementPurchaseModel = useGameStore(
    (state) => state.gameModel?.entitlementPurchaseModel
  );
  const resourceRules = useGameStore((state) => state.gameModel?.resourceRules);
  const gameResourcesModel = useGameStore((state) => state.gameModel?.gameResourcesModel);
  const gameName = useGameStore((state) => state.gameModel?.gameName);

  // Debug: log when game state changes
  console.log(
    '[GamePage] render, gameState:',
    gameState,
    'tiles:',
    tiles?.length,
    'players:',
    players?.length
  );

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

  // currentPlayer is now obtained from useCurrentPlayer() hook above
  // boardPlayers removed - GameBoard uses useBoardPlayers() hook internally
  // rollStats is now obtained from useRollStats() hook above

  // Handle roll click - convert roll sum to two dice values
  const handleRollClick = useCallback(
    (rollSum: number) => {
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
        // Set the rolled number to dim non-matching tiles (stored in Zustand for GameBoard)
        setLastRoll(rollSum);
        // Clear after 5 seconds (matches Blazor TileDimDurationSeconds)
        rollDimTimerRef.current = setTimeout(() => {
          setLastRoll(null);
        }, 5000);
      }
    },
    [proxy, setLastRoll]
  );

  // Helper to get enabled state from entitlementPurchaseModel (matches Blazor GetIsFaceUp)
  const getPurchaseEnabled = useCallback(
    (entitlement: string): boolean => {
      if (!entitlementPurchaseModel) return false;
      const model = entitlementPurchaseModel.find((m) => m.entitlement === entitlement);
      return model?.enabled ?? false;
    },
    [entitlementPurchaseModel]
  );

  // Check purchase capabilities - uses entitlementPurchaseModel.enabled (matches Blazor)
  const canPurchaseRoad = useMemo(() => getPurchaseEnabled('Road'), [getPurchaseEnabled]);
  const canPurchaseSettlement = useMemo(
    () => getPurchaseEnabled('Settlement'),
    [getPurchaseEnabled]
  );
  const canPurchaseCity = useMemo(() => getPurchaseEnabled('City'), [getPurchaseEnabled]);
  const canPurchaseDevCard = useMemo(() => getPurchaseEnabled('DevCard'), [getPurchaseEnabled]);
  const canPlaySoldier = useMemo(() => getPurchaseEnabled('Soldier'), [getPurchaseEnabled]);

  // Action cluster enabled buttons
  const actionEnabledButtons = useMemo(
    (): EnabledButtons => ({
      next: actionFlags?.nextEnabled ?? false,
      undo: actionFlags?.undoEnabled ?? false,
      redo: actionFlags?.redoEnabled ?? false,
      soldier: canPlaySoldier,
      road: canPurchaseRoad,
      settlement: canPurchaseSettlement,
      city: canPurchaseCity,
      devCard: canPurchaseDevCard,
    }),
    [
      actionFlags,
      canPlaySoldier,
      canPurchaseRoad,
      canPurchaseSettlement,
      canPurchaseCity,
      canPurchaseDevCard,
    ]
  );

  // Action cluster purchase stats - computed from current player
  // Shows UNSPENT entitlements (pending placement) as the count badge
  const actionPurchaseStats = useMemo((): PurchaseStats => {
    if (!currentPlayer) {
      return {
        roads: { unspent: 0, spent: 0, max: 15 },
        settlements: { unspent: 0, spent: 0, max: 5 },
        cities: { unspent: 0, spent: 0, max: 4 },
        devCards: { spent: 0 },
        soldier: { played: 0, unspent: 0 },
      };
    }

    // Count UNSPENT entitlements (pending placement) - this is what shows in the badge
    const unspentEntitlements = currentPlayer.unspentEntitlements ?? [];
    const countUnspent = (type: string) => unspentEntitlements.filter((e) => e === type).length;

    // Max values from resource rules (with defaults matching Blazor)
    const maxRoads = resourceRules?.maxRoads ?? 15;
    const maxSettlements = resourceRules?.maxSettlements ?? 5;
    const maxCities = resourceRules?.maxCities ?? 4;

    // Count spent entitlements (played this game)
    const spentEntitlements = currentPlayer.spentEntitlementsThisGame ?? [];
    const countSpent = (type: string) => spentEntitlements.filter((e) => e === type).length;

    return {
      roads: { unspent: countUnspent('Road'), spent: countSpent('Road'), max: maxRoads },
      settlements: {
        unspent: countUnspent('Settlement'),
        spent: countSpent('Settlement'),
        max: maxSettlements,
      },
      cities: { unspent: countUnspent('City'), spent: countSpent('City'), max: maxCities },
      devCards: { spent: countSpent('DevCard') },
      soldier: { played: countSpent('Soldier'), unspent: countUnspent('Soldier') },
    };
  }, [currentPlayer, resourceRules]);

  // Player colors for controls - from player profile
  const playerColors = useMemo((): PlayerColorsWithGradient => {
    if (currentPlayer) {
      const profile = playerProfiles.get(currentPlayer.id);
      const colors = profile?.colors ?? DEFAULT_PLAYER_COLORS;
      return createPlayerColors(colors.primary, colors.secondary, colors.foreground);
    }
    return createPlayerColors(
      DEFAULT_PLAYER_COLORS.primary,
      DEFAULT_PLAYER_COLORS.secondary,
      DEFAULT_PLAYER_COLORS.foreground
    );
  }, [currentPlayer, playerProfiles]);

  // Compute star counts for MeasurementCluster (how many building spots have each star value)
  // Mirrors Blazor's GetStarCount - counts buildings whose adjacent tiles sum to exactly that star value
  const starCounts = useMemo((): StarCounts => {
    const counts: StarCounts = {};
    if (!buildings || !tiles || buildings.length === 0 || tiles.length === 0) return counts;

    // Build a map of tile coords to their pip values
    const tilePipsMap = new Map<string, number>();
    tiles.forEach((tile) => {
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
    buildings.forEach((building) => {
      const { hexCoordinates, position } = building.buildingKey;
      const stars = computeStars(hexCoordinates, position);

      // Increment count for this star value
      counts[stars] = (counts[stars] ?? 0) + 1;
    });

    // "All" (value 0) = total of all building spots
    counts[0] = Object.values(counts).reduce((sum, c) => sum + c, 0);

    return counts;
  }, [buildings, tiles]);

  // Action handler
  const handleAction = useCallback(
    (action: string) => {
      switch (action) {
        case 'next':
          proxy.next();
          break;
        case 'undo':
          proxy.undo();
          break;
        case 'redo':
          proxy.redo();
          break;
        case 'road':
          proxy.purchase('Road');
          break;
        case 'settlement':
          proxy.purchase('Settlement');
          break;
        case 'city':
          proxy.purchase('City');
          break;
        case 'devcard':
          proxy.purchase('DevCard');
          break;
        case 'soldier':
          proxy.purchase('Soldier');
          break;
      }
    },
    [proxy]
  );

  // Shuffle handler for MeasurementCluster reset button
  const handleShuffle = useCallback(() => {
    proxy.shuffle();
  }, [proxy]);

  // Building click handler - calls upgradeBuilding for placement
  const handleBuildingClick = useCallback(
    (buildingKey: BuildingKey) => {
      console.log('[GamePage] Building clicked:', buildingKey);
      proxy.upgradeBuilding(buildingKey);
    },
    [proxy]
  );

  // Road click handler - calls purchaseRoad for placement
  const handleRoadClick = useCallback(
    (roadKey: RoadKey) => {
      console.log('[GamePage] Road clicked:', roadKey);
      proxy.purchaseRoad(roadKey);
    },
    [proxy]
  );

  // GoFirst handler - selects which player goes first
  const handleGoFirst = useCallback(
    (playerId: string) => {
      proxy.goFirst(playerId);
    },
    [proxy]
  );

  // Layout store actions for supplemental panel visibility
  const toggleMinimize = useLayoutStore((state) => state.toggleMinimize);
  const setPanelVisible = useLayoutStore((state) => state.setPanelVisible);

  // Supplemental toggle handler - sends participation status immediately like Blazor
  const handleSupplementalToggle = useCallback(
    (playerId: string, participating: boolean) => {
      proxy.setParticipatingInSupplemental(playerId, participating);
    },
    [proxy]
  );

  // Supplemental done handler - minimize to preserve position/size, then advance
  const handleSupplementalDone = useCallback(() => {
    toggleMinimize('supplemental');
    proxy.next();
  }, [toggleMinimize, proxy]);

  // Robber state for target selection (when multiple players on tile)
  const [pendingRobberCoords, setPendingRobberCoords] = useState<HexCoordinates | null>(null);
  const [pendingRobberTile, setPendingRobberTile] = useState<TileModel | null>(null);
  const [robberTargetPlayers, setRobberTargetPlayers] = useState<{ id: string; name: string }[]>(
    []
  );
  const [robberMenuPosition, setRobberMenuPosition] = useState({ x: 0, y: 0 });

  // Winner overlay state
  const [showWinnerOverlay, setShowWinnerOverlay] = useState(false);
  const [winnerId, setWinnerId] = useState('');

  // Roll dimming timer ref for clearing after 5 seconds (matching Blazor TileDimDurationSeconds)
  // setLastRoll hook is called earlier with other store hooks
  const rollDimTimerRef = useRef<NodeJS.Timeout | null>(null);

  // Keyboard roll: tracks whether "1" was pressed, waiting for second digit (0, 1, 2 → 10, 11, 12)
  const pendingRollPrefixRef = useRef(false);

  // Clear pending "1" when leaving WaitingForRoll (mouse roll, state change, etc.)
  useEffect(() => {
    if (gameState !== 'WaitingForRoll') {
      pendingRollPrefixRef.current = false;
    }
  }, [gameState]);

  // Helper: Get players with buildings adjacent to a tile coordinate
  // Returns simple { id, name } objects matching Blazor's RobberTarget record
  const getPlayersWithBuildingsOnTile = useCallback(
    (tileCoords: HexCoordinates): { id: string; name: string }[] => {
      if (!buildings || !players || buildings.length === 0) return [];

      const targetPlayerIds = new Set<string>();

      // Check all buildings to find ones adjacent to this tile
      buildings.forEach((building) => {
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
          (t) => t.q === targetCoord.q && t.r === targetCoord.r
        );

        if (
          touchesTarget &&
          (building.buildingState === 'Settlement' || building.buildingState === 'City')
        ) {
          targetPlayerIds.add(building.ownerId);
        }
      });

      // Convert IDs to { id, name } objects matching Blazor's RobberTarget
      // Double-filter to ensure current player is excluded (defensive check)
      const currentId = currentPlayer?.id;
      return players
        .filter((p) => targetPlayerIds.has(p.id) && p.id !== currentId)
        .map((p) => ({ id: p.id, name: p.name }));
    },
    [buildings, players, currentPlayer?.id]
  );

  // Clamp robber menu position so it stays within the viewport bounds
  const clampRobberMenuPosition = (position: { x: number; y: number }) => {
    if (typeof window === 'undefined') {
      return position;
    }

    const margin = 12; // small padding from window edges
    const menuMinWidth = 260; // estimated/minimum robber menu width
    const menuMinHeight = 200; // estimated/minimum robber menu height

    const maxX = Math.max(margin, window.innerWidth - menuMinWidth - margin);
    const maxY = Math.max(margin, window.innerHeight - menuMinHeight - margin);

    return {
      x: Math.min(Math.max(position.x, margin), maxX),
      y: Math.min(Math.max(position.y, margin), maxY),
    };
  };

  // Validate and show robber target menu for a tile click
  const showRobberMenu = useCallback(
    (tile: TileModel, position: { x: number; y: number }) => {
      // Only handle during MustMoveRobber state
      if (gameState !== 'MustMoveRobber') return;

      // Can't place robber on water/sea tiles
      if (tile.resourceTileType === 'Sea' || tile.resourceTileType === 'Back') return;

      // Can't place robber on current position (unless Desert for GriefDodgy)
      const robberCoords = robber?.coordinates;
      if (
        robberCoords &&
        tile.tileKey.q === robberCoords.q &&
        tile.tileKey.r === robberCoords.r &&
        tile.resourceTileType !== 'Desert'
      ) {
        console.log('[GamePage] Cannot place robber on current position');
        return;
      }

      const coords: HexCoordinates = {
        q: tile.tileKey.q,
        r: tile.tileKey.r,
        s: -tile.tileKey.q - tile.tileKey.r,
      };
      const targetPlayers = getPlayersWithBuildingsOnTile(coords);

      console.log(
        '[GamePage] Tile clicked for robber:',
        coords,
        'targets:',
        targetPlayers.length,
        targetPlayers.map((p) => p.name),
        'currentPlayer:',
        currentPlayer?.id,
        currentPlayer?.name
      );

      const clampedPosition = clampRobberMenuPosition(position);

      // Always show menu (matches Blazor behavior - menu has "Nobody" option)
      setPendingRobberCoords(coords);
      setPendingRobberTile(tile);
      setRobberTargetPlayers(targetPlayers);
      setRobberMenuPosition(clampedPosition);
    },
    [
      gameState,
      robber?.coordinates,
      getPlayersWithBuildingsOnTile,
      currentPlayer?.id,
      currentPlayer?.name,
    ]
  );

  // Left-click handler for robber placement (menu at click position)
  const handleTileClick = useCallback(
    (tile: TileModel, clientX: number, clientY: number) => {
      showRobberMenu(tile, { x: clientX, y: clientY });
    },
    [showRobberMenu]
  );

  // Right-click handler for robber placement (menu at click position)
  const handleTileRightClick = useCallback(
    (tile: TileModel, event: React.MouseEvent) => {
      showRobberMenu(tile, { x: event.clientX, y: event.clientY });
    },
    [showRobberMenu]
  );

  // Handler for selecting a robber target from the menu
  // playerId is undefined when "Nobody. Hatred Deferred." is selected
  const handleRobberTargetSelect = useCallback(
    (playerId: string | undefined) => {
      if (pendingRobberCoords) {
        proxy.moveRobber(pendingRobberCoords, playerId);
        setPendingRobberCoords(null);
        setPendingRobberTile(null);
        setRobberTargetPlayers([]);
      }
    },
    [pendingRobberCoords, proxy]
  );

  // Cancel robber target selection
  const handleRobberTargetCancel = useCallback(() => {
    setPendingRobberCoords(null);
    setPendingRobberTile(null);
    setRobberTargetPlayers([]);
  }, []);

  // Keyboard shortcuts for dice roll, road building (1-9), and city upgrades (A-Z)
  useEffect(() => {
    const handleKeyDown = (e: KeyboardEvent) => {
      // Ignore if typing in an input field
      if (e.target instanceof HTMLInputElement || e.target instanceof HTMLTextAreaElement) {
        return;
      }

      // Keyboard roll: when WaitingForRoll, number keys trigger a roll
      // Keys 2-9 roll immediately. Key "1" waits for a second digit:
      //   1+0 → 10, 1+1 → 11, 1+2 → 12, 1+(3-9) → that digit
      if (gameState === 'WaitingForRoll') {
        const num = parseInt(e.key);
        if (!isNaN(num)) {
          if (pendingRollPrefixRef.current) {
            // Second key after "1"
            pendingRollPrefixRef.current = false;
            if (num >= 0 && num <= 2) {
              handleRollClick(10 + num);
            } else {
              handleRollClick(num);
            }
          } else if (num === 1) {
            // "1" pressed — wait for second digit
            pendingRollPrefixRef.current = true;
          } else if (num >= 2 && num <= 9) {
            handleRollClick(num);
          }
          return;
        }
        // Non-digit key clears any pending "1"
        pendingRollPrefixRef.current = false;
      }

      const key = e.key.toUpperCase();

      // Handle number keys (1-9) for road building or settlement placement
      const num = parseInt(e.key);
      if (!isNaN(num) && num >= 1 && num <= 9) {
        // First try roads (they have buildIndex from server)
        const road = roads?.find((r) => r.roadState === 'Buildable' && r.buildIndex === num);
        if (road) {
          console.log('[GamePage] Keyboard shortcut: building road', num);
          proxy.purchaseRoad(road.roadKey);
          return;
        }

        // Then try settlements (only during regular gameplay, not allocation)
        const hasSettlementEntitlement = currentPlayer?.unspentEntitlements?.includes('Settlement');
        const inAllocation =
          gameState === 'AllocateResourceForward' || gameState === 'AllocateResourceReverse';

        if (hasSettlementEntitlement && !inAllocation && buildings) {
          // Build list of possible settlements (same order as GameBoard)
          const possibleSettlements = buildings.filter(
            (b) => b.buildingState === 'PossibleSettlement' && b.ownerId === null
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

      // Handle letter keys (A-Z) for roads or city upgrades
      if (key >= 'A' && key <= 'Z') {
        const letterIndex = key.charCodeAt(0) - 65; // 'A' = 65 -> 0, 'Z' = 90 -> 25
        const buildIndexForLetter = letterIndex + 10; // A=10, B=11, ..., Z=35 (for roads)

        // First try roads (forward alphabet: A=10, B=11, etc.)
        const road = roads?.find(
          (r) => r.roadState === 'Buildable' && r.buildIndex === buildIndexForLetter
        );
        if (road) {
          console.log('[GamePage] Keyboard shortcut: building road', key);
          proxy.purchaseRoad(road.roadKey);
          return;
        }

        // Then try city upgrades (reverse alphabet: Z=0, Y=1, X=2, etc.)
        const hasCityEntitlement = currentPlayer?.unspentEntitlements?.includes('City');
        if (!hasCityEntitlement || !buildings || !currentPlayer) return;

        // Build list of upgradeable settlements (same logic as GameBoard)
        const upgradeableSettlements = buildings.filter(
          (b) => b.buildingState === 'Settlement' && b.ownerId === currentPlayer.id
        );

        // Reverse alphabet mapping: Z=0, Y=1, X=2, etc.
        const cityIndex = 25 - letterIndex; // Z (25) -> 0, Y (24) -> 1, etc.

        if (cityIndex >= 0 && cityIndex < upgradeableSettlements.length) {
          const settlement = upgradeableSettlements[cityIndex];
          console.log('[GamePage] Keyboard shortcut: upgrading settlement', key);
          proxy.upgradeBuilding(settlement.buildingKey);
        }
      }
    };

    window.addEventListener('keydown', handleKeyDown);
    return () => window.removeEventListener('keydown', handleKeyDown);
  }, [roads, buildings, gameState, currentPlayer, proxy, handleRollClick]);

  // Star filter changed handler (stores in layoutStore for board filtering)
  const setStarFilter = useLayoutStore((state) => state.setStarFilter);
  const handleStarFilterChange = useCallback(
    (stars: number | null) => {
      setStarFilter(stars);
    },
    [setStarFilter]
  );

  // Resource filter changed handler (stores in layoutStore for board filtering)
  const setResourceFilters = useLayoutStore((state) => state.setResourceFilters);
  const handleResourceSelectionChange = useCallback(
    (resources: string[]) => {
      setResourceFilters(resources);
    },
    [setResourceFilters]
  );

  // Board panel for sizing overlays; hexCenterRef for hex (0,0,0) screen position
  const boardPanel = useLayoutStore(selectPanel('board'));
  const setPanelPosition = useLayoutStore((state) => state.setPanelPosition);
  const setPanelSize = useLayoutStore((state) => state.setPanelSize);
  const hexCenterRef = useRef<{ x: number; y: number } | null>(null);

  // Show/hide goFirst overlay based on game state.
  // Board-sized, positioned so the overlay center aligns with hex (0,0,0) on the board.
  useEffect(() => {
    if (gameState === 'FinishedRollOrder') {
      const { panels } = useLayoutStore.getState();
      const panel = panels.goFirst;
      if (panel) {
        if (!panel.visible) setPanelVisible('goFirst', true);
        if (panel.minimized) toggleMinimize('goFirst');
      }
      if (boardPanel) {
        const w = boardPanel.width;
        const h = boardPanel.height;
        const center = hexCenterRef.current;
        if (center) {
          setPanelPosition('goFirst', center.x - w / 2, center.y - h / 2);
        } else {
          setPanelPosition('goFirst', boardPanel.left, boardPanel.top);
        }
        setPanelSize('goFirst', w, h);
      }
    } else {
      setPanelVisible('goFirst', false);
    }
  }, [
    gameState,
    boardPanel,
    boardPanel?.left,
    boardPanel?.top,
    boardPanel?.width,
    boardPanel?.height,
    setPanelPosition,
    setPanelSize,
    setPanelVisible,
    toggleMinimize,
  ]);

  // Show/hide supplemental overlay based on game state.
  // Board-sized, positioned so the overlay center aligns with hex (0,0,0) on the board.
  useEffect(() => {
    if (gameState === 'PickSupplementalPlayers') {
      const { panels } = useLayoutStore.getState();
      const panel = panels.supplemental;
      if (panel) {
        if (!panel.visible) setPanelVisible('supplemental', true);
        if (panel.minimized) toggleMinimize('supplemental');
      }
      if (boardPanel) {
        const w = boardPanel.width;
        const h = boardPanel.height;
        const center = hexCenterRef.current;
        if (center) {
          setPanelPosition('supplemental', center.x - w / 2, center.y - h / 2);
        } else {
          setPanelPosition('supplemental', boardPanel.left, boardPanel.top);
        }
        setPanelSize('supplemental', w, h);
      }
    } else {
      setPanelVisible('supplemental', false);
    }
  }, [
    gameState,
    boardPanel,
    boardPanel?.left,
    boardPanel?.top,
    boardPanel?.width,
    boardPanel?.height,
    setPanelPosition,
    setPanelSize,
    setPanelVisible,
    toggleMinimize,
  ]);

  // Game-specific menu action handlers
  const handleBalance = useCallback(() => {
    console.log('[GamePage] Balance board requested');
    proxy.balanceBoard();
  }, [proxy]);

  // Winner overlay handlers
  const handleWinner = useCallback(() => {
    if (!currentPlayer) return;
    setWinnerId(currentPlayer.id);
    setShowWinnerOverlay(true);
  }, [currentPlayer]);

  const handleEndGame = useCallback(
    async (vpScores: Record<string, number>) => {
      setShowWinnerOverlay(false);
      try {
        const result = await proxy.declareWinner(winnerId, vpScores);
        if (!result.success) {
          console.error('[GamePage] Failed to declare winner:', result.message);
        }
      } catch (error) {
        console.error('[GamePage] Exception declaring winner:', error);
      }
    },
    [winnerId, proxy]
  );

  // Build WinnerPlayer[] from game state for the overlay
  const winnerPlayers: WinnerPlayer[] = useMemo(() => {
    if (!players) return [];
    const baseUrl = getServiceUrl();
    return players.map((p) => {
      const profile = playerProfiles.get(p.id);
      const imageUri = profile?.imageUri;
      return {
        id: p.id,
        name: profile?.name || p.name,
        score: p.score,
        colors: profile?.colors || DEFAULT_PLAYER_COLORS,
        avatarUrl: imageUri ? `${baseUrl}${imageUri}` : undefined,
      };
    });
  }, [players, playerProfiles]);

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
  const isPickingBoard = gameState === 'PickingBoard';

  // GameBoard now uses internal hooks for data (useBoardData, useBoardPlayers, etc.)
  // Removed: isAllocationPhase, showSettlementIndexes, boardGameData - all derived internally

  // Game actions for NavMenu
  const gameActions = useMemo(
    () => ({
      isPickingBoard,
      onBalance: handleBalance,
      onWinner: handleWinner,
      onSaveCopy: handleSaveCopy,
    }),
    [isPickingBoard, handleBalance, handleWinner, handleSaveCopy]
  );

  return (
    <MainLayout fullScreen activeGameId={gameId} gameActions={gameActions}>
      <div className="relative w-full h-full overflow-hidden">
        {/* GameBoard with internal pan/zoom - fills the viewport */}
        {/* GameBoard uses internal hooks for data - only pass callbacks and configuration */}
        <GameBoard
          hexSize={50}
          gap={1}
          onBuildingClick={handleBuildingClick}
          onRoadClick={handleRoadClick}
          onTileClick={handleTileClick}
          onTileRightClick={handleTileRightClick}
          hexCenterRef={hexCenterRef}
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
        <FloatingPanel panelId="dice" title="Dice" className="bg-white/5 border-white/10">
          <RollRing rollStats={rollStats} onRollClick={handleRollClick} colors={playerColors} />
        </FloatingPanel>

        {/* Action Controls Panel */}
        <FloatingPanel panelId="actions" title="Actions" className="bg-white/5 border-white/10">
          <ActionCluster
            colors={playerColors}
            gameState={getStateMessage(gameState)}
            onAction={handleAction}
            purchaseStats={actionPurchaseStats}
            enabledButtons={actionEnabledButtons}
          />
        </FloatingPanel>

        {/* Board Measurements Panel */}
        <FloatingPanel panelId="measurements" title="Board" className="bg-white/5 border-white/10">
          <MeasurementCluster
            tiles={tiles ?? []}
            colors={playerColors}
            starCounts={starCounts}
            onReset={handleShuffle}
            onStarFilterChange={handleStarFilterChange}
            onResourceSelectionChange={handleResourceSelectionChange}
            shuffleEnabled={gameState === 'PickingBoard'}
          />
        </FloatingPanel>

        {/* Players Panel */}
        <FloatingPanel
          panelId="players"
          title="Players"
          resizable
          className="bg-white/5 border-white/10"
        >
          <PlayersPanel />
        </FloatingPanel>

        {/* Resources Panel */}
        <FloatingPanel panelId="resources" title="Resources" className="bg-white/5 border-white/10">
          <GameResourcesHeader resources={gameResourcesModel ?? null} />
        </FloatingPanel>

        {/* GoFirst Overlay - visibility controlled by game state effect */}
        <FloatingPanel
          panelId="goFirst"
          title="Go First"
          alwaysOnTop
          className="bg-white/5 border-white/10"
        >
          {gameState === 'FinishedRollOrder' && (
            <GoFirstOverlay
              players={players}
              playerProfiles={playerProfiles}
              onSelectPlayer={handleGoFirst}
            />
          )}
        </FloatingPanel>

        {/* Supplemental Overlay - visibility controlled by game state effect */}
        <FloatingPanel
          panelId="supplemental"
          title="Supplemental Build"
          alwaysOnTop
          className="bg-white/5 border-white/10"
        >
          {gameState === 'PickSupplementalPlayers' && currentPlayer && (
            <SupplementalOverlay
              players={players}
              currentPlayerId={currentPlayer.id}
              playerProfiles={playerProfiles}
              onToggle={handleSupplementalToggle}
              onDone={handleSupplementalDone}
            />
          )}
        </FloatingPanel>

        {/* Robber Target Menu - shown when selecting steal target */}
        {pendingRobberTile && (
          <RobberTargetMenu
            targetPlayers={robberTargetPlayers}
            position={robberMenuPosition}
            onSelectTarget={handleRobberTargetSelect}
            onCancel={handleRobberTargetCancel}
          />
        )}

        {/* Winner Overlay - unified three-phase winner declaration */}
        {showWinnerOverlay && players && (
          <FloatingPanel
            panelId="winner"
            title="Winner!"
            className="bg-white/5 border-white/10"
            minWidth={320}
            minHeight={380}
            alwaysOnTop
          >
            <WinnerOverlay
              players={winnerPlayers}
              currentPlayerColors={playerColors}
              celebrationDurationMs={5000}
              onEndGame={handleEndGame}
              onCancel={() => setShowWinnerOverlay(false)}
            />
          </FloatingPanel>
        )}

        {/* Minimized panels bar - fixed at bottom */}
        <MinimizedBar />

        {/* Game info badges - minimal floating overlays */}
        <div className="absolute bottom-14 left-2 bg-black/60 rounded px-2 py-1 text-xs text-gray-300 z-30 pointer-events-none">
          {gameName || gameId}
        </div>
        <div className="absolute bottom-14 right-2 flex items-center gap-2 z-30 pointer-events-none">
          <span className="bg-black/60 rounded px-2 py-1 text-xs text-gray-300">
            {gameState || 'Loading...'}
          </span>
          <span className="bg-black/60 rounded px-2 py-1 text-xs">
            {isConnected ? (
              <span className="text-green-400">● Connected</span>
            ) : (
              <span className="text-amber-400">
                ○ {isConnecting ? 'Connecting' : 'Disconnected'}
              </span>
            )}
          </span>
        </div>
      </div>
    </MainLayout>
  );
}
