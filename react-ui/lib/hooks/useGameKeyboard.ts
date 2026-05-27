'use client';

/**
 * useGameKeyboard — single owner of game keyboard shortcuts.
 *
 * Replaces a constellation of independent keydown listeners that lived in
 * page.tsx, ActionCluster, and SupplementalOverlay. See issue #181 and
 * .design/keyboard-shortcuts.md for the design and the bug history.
 *
 * Key contract (single source of truth — match the table in the design):
 *
 *   Enter      — modal? typing? else fire Next when nextEnabled
 *   Backspace  — typing? (browser default). else modal? else prefix? (clear)
 *                else fire Undo when undoEnabled
 *   Escape     — clear pending roll prefix. Per-modal Escape-to-close
 *                handlers still own their own behavior elsewhere.
 *   0-9        — typing? WaitingForRoll? → roll logic with `1`-prefix.
 *                Else 1-9 → road placement, then settlement placement.
 *   A-Z        — typing? Road by buildIndex (A=10..Z=35). City upgrade
 *                reverse index. Purchase S/C/K/R/D.
 *
 * Invariants (asserted by tests):
 *
 *  1. The keydown listener is registered ONCE on mount and never
 *     re-registered while the hook is mounted. State is read through
 *     `stateRef` (synced every render). This removes the stale-closure
 *     window described as H1 in the design.
 *  2. `if (e.repeat) return;` and `if (e.isComposing) return;` are
 *     the first two checks. Held keys do nothing. IME composition is
 *     not handled (out of scope) but the check is free.
 *  3. Capture phase is scoped to Enter and Backspace only. The
 *     alphanumeric branches don't need it.
 *  4. When the hook dispatches a game action, the active element is
 *     blurred ONLY if it's an HTMLButtonElement. Other focused
 *     elements are left alone (a11y).
 */

import { useEffect, useRef, useState, useCallback } from 'react';
import { isTypingTarget } from '@/lib/utils/isTypingTarget';
import type { GameServiceProxy } from '@/lib/services/GameServiceProxy';
import type { GameState } from '@/types/generated/models/game-state';
import type { ActionFlags } from '@/types/generated/models/action-flags';
import type { RoadModel } from '@/types/generated/models/road-model';
import type { BuildingModel } from '@/types/generated/models/building-model';
import type { PlayerModel } from '@/types/generated/models/player-model';
import { Entitlement } from '@/types/generated/models/entitlement';

export interface GameKeyboardState {
  proxy: GameServiceProxy;
  gameState: GameState | undefined;
  roads: RoadModel[] | undefined;
  buildings: BuildingModel[] | undefined;
  currentPlayer: PlayerModel | undefined;
  actionFlags: ActionFlags | undefined;
  canPurchaseSettlement: boolean;
  canPurchaseCity: boolean;
  canPurchaseRoad: boolean;
  canPurchaseDevCard: boolean;
  canPlaySoldier: boolean;
  anyModalOpen: boolean;
  /**
   * Dispatch a dice roll by sum (2–12). Delegated to the caller (not
   * called directly via `proxy.roll`) because rolls carry a client-side
   * UX side effect (tile dimming) that the proxy doesn't model.
   */
  onRoll: (rollSum: number) => void;
}

export interface GameKeyboardResult {
  /**
   * True while waiting for the second digit after a `1` prefix during
   * WaitingForRoll. Consumers (e.g. page.tsx) render a small badge so
   * the user can see they're mid-sequence.
   */
  rollPrefixPending: boolean;
}

/** Blur the currently focused element ONLY if it's a button. See invariant 4. */
function blurFocusedButton(): void {
  const active = typeof document !== 'undefined' ? document.activeElement : null;
  if (active instanceof HTMLButtonElement) {
    active.blur();
  }
}

export function useGameKeyboard(state: GameKeyboardState): GameKeyboardResult {
  // Latest state is always reachable through stateRef. The handler
  // closes over the ref, not over `state` directly, so it never goes
  // stale even though it's registered exactly once.
  const stateRef = useRef(state);
  useEffect(() => {
    stateRef.current = state;
  });

  // Roll prefix: ref drives synchronous handler logic; mirrored to
  // React state so consumers can render the `1_` indicator.
  const prefixRef = useRef(false);
  const [prefixPending, setPrefixPending] = useState(false);
  const setPrefix = useCallback((v: boolean) => {
    prefixRef.current = v;
    setPrefixPending(v);
  }, []);

  // When gameState leaves WaitingForRoll, the prefix is no longer
  // meaningful — clear it so re-entering the state starts fresh and
  // the "1_" indicator disappears. The setState-in-effect pattern is
  // intentional here: we're synchronizing React state with an external
  // (server-driven) game-state transition. Same pattern lives in
  // useHealthCheck.ts (warning is precedent in this codebase).
  useEffect(() => {
    if (state.gameState !== 'WaitingForRoll' && prefixRef.current) {
      // eslint-disable-next-line react-hooks/set-state-in-effect
      setPrefix(false);
    }
  }, [state.gameState, setPrefix]);

  // Capture-phase handler — Enter and Backspace only. Runs before any
  // focused element's default action (e.g. focused <button> activating
  // on Enter).
  useEffect(() => {
    const captureHandler = (e: KeyboardEvent): void => {
      if (e.repeat) return;
      if (e.isComposing) return;
      if (e.key !== 'Enter' && e.key !== 'Backspace') return;

      const s = stateRef.current;

      if (e.key === 'Enter') {
        if (s.anyModalOpen) return;
        if (isTypingTarget(e.target)) return;
        if (s.actionFlags?.nextEnabled) {
          e.preventDefault();
          void s.proxy.next();
          blurFocusedButton();
        }
        return;
      }

      // Backspace
      if (isTypingTarget(e.target)) return; // Browser default deletes character
      if (s.anyModalOpen) return;
      if (s.gameState === 'WaitingForRoll' && prefixRef.current) {
        e.preventDefault();
        setPrefix(false);
        return;
      }
      if (s.actionFlags?.undoEnabled) {
        e.preventDefault();
        void s.proxy.undo();
        blurFocusedButton();
      }
    };

    window.addEventListener('keydown', captureHandler, { capture: true });
    return () => window.removeEventListener('keydown', captureHandler, { capture: true });
  }, [setPrefix]); // setPrefix is stable (useCallback []); effect registers once

  // Bubble-phase handler — Escape (prefix-clear), roll digits,
  // placement digits, letter shortcuts, purchase shortcuts.
  useEffect(() => {
    const bubbleHandler = (e: KeyboardEvent): void => {
      if (e.repeat) return;
      if (e.isComposing) return;

      const s = stateRef.current;
      const key = e.key;

      // Escape: clear pending roll prefix. Per-modal close-on-Esc
      // handlers still run independently — they're on separate
      // listeners that we don't touch here.
      if (key === 'Escape') {
        if (s.gameState === 'WaitingForRoll' && prefixRef.current) {
          setPrefix(false);
        }
        return;
      }

      // From here on, typing target suppresses shortcuts.
      if (isTypingTarget(e.target)) return;

      // ── Dice roll branch ───────────────────────────────────────────
      // Keys 2-9 roll immediately. Key "1" waits for a second digit:
      //   1+0 → 10, 1+1 → 11, 1+2 → 12, 1+(3-9) → that digit.
      if (s.gameState === 'WaitingForRoll') {
        const num = parseInt(key, 10);
        if (!isNaN(num)) {
          if (prefixRef.current) {
            // Second key after "1"
            setPrefix(false);
            if (num >= 0 && num <= 2) {
              s.onRoll(10 + num);
            } else {
              s.onRoll(num);
            }
          } else if (num === 1) {
            setPrefix(true);
          } else if (num >= 2 && num <= 9) {
            s.onRoll(num);
          }
          return;
        }
        // Non-digit key clears any pending "1"
        if (prefixRef.current) setPrefix(false);
      }

      const upper = key.toUpperCase();
      const lower = key.toLowerCase();

      // ── Number keys 1-9: road placement, then settlement placement ──
      const numKey = parseInt(key, 10);
      if (!isNaN(numKey) && numKey >= 1 && numKey <= 9) {
        // Roads first (server-assigned buildIndex)
        const road = s.roads?.find(
          (r) => r.roadState === 'Buildable' && r.buildIndex === numKey
        );
        if (road) {
          void s.proxy.purchaseRoad(road.roadKey);
          blurFocusedButton();
          return;
        }

        // Settlements during regular gameplay (not allocation)
        const hasSettlementEntitlement =
          s.currentPlayer?.unspentEntitlements?.includes(Entitlement.Settlement) ?? false;
        const inAllocation =
          s.gameState === 'AllocateResourceForward' ||
          s.gameState === 'AllocateResourceReverse';

        if (hasSettlementEntitlement && !inAllocation && s.buildings) {
          const possibleSettlements = s.buildings.filter(
            (b) => b.buildingState === 'PossibleSettlement' && b.ownerId === null
          );
          const idx = numKey - 1;
          if (idx >= 0 && idx < possibleSettlements.length) {
            void s.proxy.upgradeBuilding(possibleSettlements[idx].buildingKey);
            blurFocusedButton();
          }
        }
        return;
      }

      // ── Letter keys A-Z: road placement, then city upgrade ──
      if (upper >= 'A' && upper <= 'Z') {
        const letterIndex = upper.charCodeAt(0) - 65; // A=0..Z=25
        const buildIndexForLetter = letterIndex + 10; // A=10..Z=35

        // Roads (forward alphabet: A=10, B=11, ...)
        const road = s.roads?.find(
          (r) => r.roadState === 'Buildable' && r.buildIndex === buildIndexForLetter
        );
        if (road) {
          void s.proxy.purchaseRoad(road.roadKey);
          blurFocusedButton();
          return;
        }

        // City upgrades (reverse alphabet: Z=settlement[0], Y=settlement[1], ...)
        const hasCityEntitlement =
          s.currentPlayer?.unspentEntitlements?.includes(Entitlement.City) ?? false;
        if (hasCityEntitlement && s.buildings && s.currentPlayer) {
          const upgradeable = s.buildings.filter(
            (b) =>
              b.buildingState === 'Settlement' && b.ownerId === s.currentPlayer!.id
          );
          const cityIndex = 25 - letterIndex; // Z(25)→0, Y(24)→1, ...
          if (cityIndex >= 0 && cityIndex < upgradeable.length) {
            void s.proxy.upgradeBuilding(upgradeable[cityIndex].buildingKey);
            blurFocusedButton();
            return;
          }
        }
        // Letter didn't match a road or city → fall through to purchase shortcuts.
      }

      // ── Purchase shortcuts (case-insensitive) ──
      // No game-state gating here — the canPurchase* flags already
      // encode whether the purchase is valid in the current state.
      switch (lower) {
        case 's':
          if (s.canPurchaseSettlement) {
            e.preventDefault();
            void s.proxy.purchase(Entitlement.Settlement);
            blurFocusedButton();
          }
          return;
        case 'c':
          if (s.canPurchaseCity) {
            e.preventDefault();
            void s.proxy.purchase(Entitlement.City);
            blurFocusedButton();
          }
          return;
        case 'k':
          if (s.canPlaySoldier) {
            e.preventDefault();
            void s.proxy.purchase(Entitlement.Soldier);
            blurFocusedButton();
          }
          return;
        case 'r':
          if (s.canPurchaseRoad) {
            e.preventDefault();
            void s.proxy.purchase(Entitlement.Road);
            blurFocusedButton();
          }
          return;
        case 'd':
          if (s.canPurchaseDevCard) {
            e.preventDefault();
            void s.proxy.purchase(Entitlement.DevCard);
            blurFocusedButton();
          }
          return;
      }
    };

    window.addEventListener('keydown', bubbleHandler);
    return () => window.removeEventListener('keydown', bubbleHandler);
  }, [setPrefix]);

  // focusin: if focus moves into a typing target, clear the prefix so
  // we don't fire an unexpected 10/11/12 when the user returns and
  // presses a digit. See F4 in the design.
  useEffect(() => {
    const onFocusIn = (e: FocusEvent): void => {
      if (isTypingTarget(e.target) && prefixRef.current) {
        setPrefix(false);
      }
    };
    window.addEventListener('focusin', onFocusIn);
    return () => window.removeEventListener('focusin', onFocusIn);
  }, [setPrefix]);

  return { rollPrefixPending: prefixPending };
}

