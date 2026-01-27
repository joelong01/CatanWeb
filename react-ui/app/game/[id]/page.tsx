'use client';

/**
 * Game Page - Main game view with infinite hex viewport and floating panels.
 *
 * This page:
 * 1. Connects to SignalR via useGameConnection
 * 2. Renders an infinite pan/zoom hex grid as background
 * 3. Shows floating control panels for dice, actions, players, etc.
 * 4. Board tiles render at their coordinates (water everywhere else)
 */

import { useEffect, useMemo, useCallback } from 'react';
import { useParams } from 'next/navigation';
import { MainLayout } from '@/components/layout';
import { useGameConnection } from '@/lib/hooks/useGameConnection';
import { useGameStore } from '@/lib/stores/gameStore';
import { useLayoutStore } from '@/lib/stores/layoutStore';
import { BoardViewport } from '@/components/game/viewport/BoardViewport';
import { FloatingPanel } from '@/components/game/panels/FloatingPanel';
import { PlayersPanel } from '@/components/game/panels/PlayersPanel';
import { ResourcesPanel } from '@/components/game/panels/ResourcesPanel';
import { DiceCluster } from '@/components/game/controls/DiceCluster';
import { ActionCluster } from '@/components/game/controls/ActionCluster';
import { MeasurementCluster } from '@/components/game/controls/MeasurementCluster';
import type { HexCoordinate } from '@/components/hex-grid';
import { hexCoordsToString } from '@/lib/utils/reconciliation';

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

  // Get game model from store
  const gameModel = useGameStore((state) => state.gameModel);

  // Build tile lookup for efficient rendering
  const tileLookup = useMemo(() => {
    if (!gameModel) return new Map<string, boolean>();
    const map = new Map<string, boolean>();
    for (const tile of gameModel.tiles) {
      const key = hexCoordsToString(tile.tileKey);
      map.set(key, true);
    }
    return map;
  }, [gameModel]);

  // Render function for viewport hexes
  const renderHex = useCallback((coord: HexCoordinate): React.ReactNode | null => {
    const key = `${coord.q},${coord.r}`;
    const isBoardTile = tileLookup.has(key);

    if (isBoardTile) {
      // TODO: Return GameTile component
      // For now, return a placeholder
      return (
        <div className="w-full h-full bg-gradient-to-br from-green-700 to-green-900 flex items-center justify-center">
          <span className="text-white/50 text-xs">{coord.q},{coord.r}</span>
        </div>
      );
    }

    // Return null to render default water
    return null;
  }, [tileLookup]);

  // Get current player info
  const currentPlayer = useMemo(() => {
    if (!gameModel) return null;
    return gameModel.players.find(p => p.id === gameModel.currentPlayerId);
  }, [gameModel]);

  // Check if dice rolling is enabled
  const isDiceEnabled = gameModel?.actionFlags?.rollsEnabled ?? false;

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

  // Connection status
  const { isConnected, isConnecting, proxy } = connection;

  return (
    <MainLayout>
      <div className="relative w-full h-[calc(100vh-64px)] overflow-hidden">
        {/* Infinite hex viewport */}
        <BoardViewport renderHex={renderHex}>
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

          {/* Floating Panels */}
          {/* Dice Panel */}
          <FloatingPanel panelId="dice" title="Dice" icon="🎲">
            <DiceCluster
              enabled={isDiceEnabled}
              isRolling={false}
              onRoll={(die1, die2) => proxy.roll(die1, die2)}
              playerColor={currentPlayer ? '#f59e0b' : undefined}
            />
          </FloatingPanel>

          {/* Action Controls Panel */}
          <FloatingPanel panelId="actions" title="Actions" icon="⚡">
            <ActionCluster
              actionFlags={gameModel?.actionFlags ?? null}
              gameState={gameModel?.gameState ?? null}
              canPurchaseRoad={canPurchaseRoad}
              canPurchaseSettlement={canPurchaseSettlement}
              canPurchaseCity={canPurchaseCity}
              canPurchaseDevCard={canPurchaseDevCard}
              onNext={() => proxy.next()}
              onUndo={() => proxy.undo()}
              onRedo={() => proxy.redo()}
              onPurchaseRoad={() => proxy.purchase('Road')}
              onPurchaseSettlement={() => proxy.purchase('Settlement')}
              onPurchaseCity={() => proxy.purchase('City')}
              onPurchaseDevCard={() => proxy.purchase('DevCard')}
              playerColor={currentPlayer ? '#f59e0b' : undefined}
            />
          </FloatingPanel>

          {/* Board Measurements Panel */}
          <FloatingPanel panelId="measurements" title="Board" icon="📊">
            <MeasurementCluster gameModel={gameModel} />
          </FloatingPanel>

          {/* Players Panel */}
          <FloatingPanel panelId="players" title="Players" icon="👥" resizable>
            <PlayersPanel gameModel={gameModel} />
          </FloatingPanel>

          {/* Resources Panel */}
          <FloatingPanel panelId="resources" title="Resources" icon="📦">
            <ResourcesPanel resources={gameModel?.gameResourcesModel ?? null} />
          </FloatingPanel>
        </BoardViewport>

        {/* Game info footer */}
        <div className="absolute bottom-0 left-0 right-0 bg-gray-900/80 backdrop-blur-sm px-4 py-2 flex items-center justify-between text-xs text-gray-400">
          <div>
            Game: {gameModel?.gameName || gameId}
          </div>
          <div className="flex items-center gap-4">
            <span>
              State: {gameModel?.gameState || 'Loading...'}
            </span>
            <span>
              {isConnected ? (
                <span className="text-green-400">● Connected</span>
              ) : (
                <span className="text-amber-400">○ {isConnecting ? 'Connecting' : 'Disconnected'}</span>
              )}
            </span>
          </div>
        </div>

        {/* Layout reset button */}
        <button
          onClick={() => useLayoutStore.getState().resetLayout()}
          className="absolute top-4 right-4 bg-gray-800/80 hover:bg-gray-700 text-gray-300 px-3 py-1 rounded text-xs transition-colors z-40"
          title="Reset panel positions"
        >
          Reset Layout
        </button>
      </div>
    </MainLayout>
  );
}
