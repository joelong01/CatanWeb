'use client';

/**
 * ActionCluster - Game action controls in a 7-hex cluster.
 *
 * Center: Next button
 * Around: Undo, Redo, Road, Settlement, City, DevCard
 *
 * State message displayed below the cluster.
 */

import { memo, useCallback, useEffect } from 'react';
import {
  HexGrid,
  HexGridItem,
  HEX_LAYOUTS,
} from '@/components/hex-grid';
import type { ActionFlags } from '@/types/generated/models/action-flags';
import type { GameState } from '@/types/generated/models/game-state';

interface ActionClusterProps {
  /** Action flags from GameModel */
  actionFlags: ActionFlags | null;
  /** Current game state */
  gameState: GameState | null;
  /** Whether player can purchase road */
  canPurchaseRoad: boolean;
  /** Whether player can purchase settlement */
  canPurchaseSettlement: boolean;
  /** Whether player can purchase city */
  canPurchaseCity: boolean;
  /** Whether player can purchase dev card */
  canPurchaseDevCard: boolean;
  /** Callbacks */
  onNext: () => void;
  onUndo: () => void;
  onRedo: () => void;
  onPurchaseRoad: () => void;
  onPurchaseSettlement: () => void;
  onPurchaseCity: () => void;
  onPurchaseDevCard: () => void;
  /** Player color for highlighting */
  playerColor?: string;
}

/** Action button positions */
const ACTION_POSITIONS = {
  next: { q: 0, r: 0 },       // Center
  undo: { q: -1, r: 0 },      // Left
  redo: { q: -1, r: 1 },      // Bottom-left
  road: { q: 1, r: -1 },      // Top-right
  settlement: { q: 1, r: 0 }, // Right
  city: { q: 0, r: 1 },       // Bottom
  devCard: { q: 0, r: -1 },   // Top
} as const;

/** Action icons (simple text/emoji) */
const ACTION_ICONS: Record<string, string> = {
  next: '▶',
  undo: '↶',
  redo: '↷',
  road: '═',
  settlement: '⌂',
  city: '🏰',
  devCard: '🃏',
};

/** Get human-readable state message */
function getStateMessage(gameState: GameState | null): string {
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

/** Main ActionCluster component */
export const ActionCluster = memo(function ActionCluster({
  actionFlags,
  gameState,
  canPurchaseRoad,
  canPurchaseSettlement,
  canPurchaseCity,
  canPurchaseDevCard,
  onNext,
  onUndo,
  onRedo,
  onPurchaseRoad,
  onPurchaseSettlement,
  onPurchaseCity,
  onPurchaseDevCard,
  playerColor = '#f59e0b',
}: ActionClusterProps): React.ReactElement {
  // Keyboard shortcut for Next (Enter key)
  useEffect(() => {
    const handleKeyDown = (e: KeyboardEvent) => {
      if (e.key === 'Enter' && actionFlags?.nextEnabled) {
        e.preventDefault();
        onNext();
      }
    };

    window.addEventListener('keydown', handleKeyDown);
    return () => window.removeEventListener('keydown', handleKeyDown);
  }, [actionFlags?.nextEnabled, onNext]);

  // Build action lookup
  const actions: Record<string, { enabled: boolean; onClick: () => void }> = {
    next: { enabled: actionFlags?.nextEnabled ?? false, onClick: onNext },
    undo: { enabled: actionFlags?.undoEnabled ?? false, onClick: onUndo },
    redo: { enabled: actionFlags?.redoEnabled ?? false, onClick: onRedo },
    road: { enabled: canPurchaseRoad, onClick: onPurchaseRoad },
    settlement: { enabled: canPurchaseSettlement, onClick: onPurchaseSettlement },
    city: { enabled: canPurchaseCity, onClick: onPurchaseCity },
    devCard: { enabled: canPurchaseDevCard, onClick: onPurchaseDevCard },
  };

  // Build hex grid items
  const items: HexGridItem[] = HEX_LAYOUTS.CLUSTER_7.map((coord) => {
    // Find which action this position represents
    const actionEntry = Object.entries(ACTION_POSITIONS).find(
      ([_, pos]) => pos.q === coord.q && pos.r === coord.r
    );

    const actionKey = actionEntry?.[0] || null;
    const action = actionKey ? actions[actionKey] : null;
    const icon = actionKey ? ACTION_ICONS[actionKey] : '';
    const isCenter = actionKey === 'next';

    return {
      id: `${coord.q}-${coord.r}`,
      coord,
      content: (
        <div
          className={`
            w-full h-full flex items-center justify-center
            transition-all duration-200
            ${action?.enabled ? 'cursor-pointer hover:brightness-110' : 'opacity-40 cursor-not-allowed'}
          `}
          style={{
            clipPath: 'polygon(25% 0%, 75% 0%, 100% 50%, 75% 100%, 25% 100%, 0% 50%)',
            backgroundColor: isCenter && action?.enabled ? playerColor : '#4b5563',
          }}
        >
          <span
            className={`
              ${isCenter ? 'text-xl' : 'text-lg'}
              ${action?.enabled ? 'text-white' : 'text-gray-500'}
            `}
          >
            {icon}
          </span>
        </div>
      ),
      onClick: action?.enabled ? action.onClick : undefined,
    };
  });

  const stateMessage = getStateMessage(gameState);

  return (
    <div className="p-3">
      <div className="flex justify-center">
        <HexGrid hexSize={36} items={items} gap={2} />
      </div>

      {/* State message */}
      <div className="mt-4 text-center">
        <div className="text-sm font-medium text-amber-400">
          {stateMessage}
        </div>
      </div>

      {/* Action labels */}
      <div className="mt-3 grid grid-cols-3 gap-1 text-[10px] text-gray-500 text-center">
        <div>Undo</div>
        <div>Next (Enter)</div>
        <div>Redo</div>
      </div>
    </div>
  );
});

export default ActionCluster;
