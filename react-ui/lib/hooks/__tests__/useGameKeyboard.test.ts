/**
 * Tests for useGameKeyboard — see issue #181 and
 * .design/keyboard-shortcuts.md.
 *
 * Two sections:
 *   - Unit: hook in isolation with a fake proxy. Covers the
 *     non-regression matrix, repeat/typing-target guards, prefix
 *     lifecycle, stale-closure safety, and surgical blur.
 *   - Integration: mounted against the real layoutStore. Covers
 *     SaveLayoutDialog ↔ modal token-set interaction and Enter
 *     during PickSupplementalPlayers via nextEnabled.
 */

import { describe, it, expect, beforeEach, vi } from 'vitest';
import { renderHook, act, fireEvent } from '@testing-library/react';
import { useGameKeyboard, type GameKeyboardState } from '../useGameKeyboard';
import { useLayoutStore } from '@/lib/stores/layoutStore';
import type { GameServiceProxy } from '@/lib/services/GameServiceProxy';
import type { GameState } from '@/types/generated/models/game-state';
import type { ActionFlags } from '@/types/generated/models/action-flags';
import type { RoadModel } from '@/types/generated/models/road-model';
import { KeyboardShortcut } from '@/types/generated/models/keyboard-shortcut';
import { KeyboardShortcutDescriptions } from '@/types/generated/models/enum-descriptions';
import type { BuildingModel } from '@/types/generated/models/building-model';
import type { PlayerModel } from '@/types/generated/models/player-model';

// ============================================================================
// Fixtures and helpers
// ============================================================================

interface FakeProxy {
  next: ReturnType<typeof vi.fn>;
  undo: ReturnType<typeof vi.fn>;
  redo: ReturnType<typeof vi.fn>;
  roll: ReturnType<typeof vi.fn>;
  purchase: ReturnType<typeof vi.fn>;
  purchaseRoad: ReturnType<typeof vi.fn>;
  upgradeBuilding: ReturnType<typeof vi.fn>;
}

function makeFakeProxy(): FakeProxy {
  return {
    next: vi.fn().mockResolvedValue({ success: true }),
    undo: vi.fn().mockResolvedValue({ success: true }),
    redo: vi.fn().mockResolvedValue({ success: true }),
    roll: vi.fn().mockResolvedValue({ success: true }),
    purchase: vi.fn().mockResolvedValue({ success: true }),
    purchaseRoad: vi.fn().mockResolvedValue({ success: true }),
    upgradeBuilding: vi.fn().mockResolvedValue({ success: true }),
  };
}

function makeFlags(overrides: Partial<ActionFlags> = {}): ActionFlags {
  return {
    nextEnabled: false,
    undoEnabled: false,
    redoEnabled: false,
    rollsEnabled: false,
    ...overrides,
  } as ActionFlags;
}

interface BuildArgs {
  proxy?: FakeProxy;
  onRoll?: (sum: number) => void;
  gameState?: GameState | undefined;
  roads?: RoadModel[];
  buildings?: BuildingModel[];
  currentPlayer?: PlayerModel | undefined;
  actionFlags?: ActionFlags;
  canPurchaseSettlement?: boolean;
  canPurchaseCity?: boolean;
  canPurchaseRoad?: boolean;
  canPurchaseDevCard?: boolean;
  canPlaySoldier?: boolean;
  anyModalOpen?: boolean;
}

function buildState(args: BuildArgs = {}): GameKeyboardState {
  return {
    proxy: (args.proxy ?? makeFakeProxy()) as unknown as GameServiceProxy,
    gameState: args.gameState,
    roads: args.roads,
    buildings: args.buildings,
    currentPlayer: args.currentPlayer,
    actionFlags: args.actionFlags ?? makeFlags(),
    canPurchaseSettlement: args.canPurchaseSettlement ?? false,
    canPurchaseCity: args.canPurchaseCity ?? false,
    canPurchaseRoad: args.canPurchaseRoad ?? false,
    canPurchaseDevCard: args.canPurchaseDevCard ?? false,
    canPlaySoldier: args.canPlaySoldier ?? false,
    anyModalOpen: args.anyModalOpen ?? false,
    onRoll: args.onRoll ?? vi.fn(),
  };
}

/** Fire a keydown on window. Optionally route via a focused target. */
function press(
  key: string,
  options: { target?: HTMLElement; repeat?: boolean; isComposing?: boolean } = {}
): void {
  const target = options.target ?? document.body;
  // fireEvent.keyDown dispatches a KeyboardEvent that bubbles AND
  // travels through capture phase, so both our listeners see it.
  fireEvent.keyDown(target, {
    key,
    repeat: options.repeat ?? false,
    isComposing: options.isComposing ?? false,
  });
}

// ============================================================================
// SECTION: Unit tests
// ============================================================================

describe('useGameKeyboard — unit', () => {
  beforeEach(() => {
    // Reset modal registry between tests (token-set is module-shared)
    useLayoutStore.getState().resetModalRegistry();
  });

  // ── Non-regression matrix: roll digits ─────────────────────────────────────

  describe('roll digits during WaitingForRoll', () => {
    it('rolls a single digit 2-9 immediately', () => {
      const onRoll = vi.fn();
      renderHook(() => useGameKeyboard(buildState({ gameState: 'WaitingForRoll', onRoll })));
      press('7');
      expect(onRoll).toHaveBeenCalledWith(7);
      expect(onRoll).toHaveBeenCalledTimes(1);
    });

    it('treats "1" as a prefix and waits for the second digit', () => {
      const onRoll = vi.fn();
      const { result } = renderHook(() =>
        useGameKeyboard(buildState({ gameState: 'WaitingForRoll', onRoll }))
      );
      act(() => press('1'));
      expect(onRoll).not.toHaveBeenCalled();
      expect(result.current.rollPrefixPending).toBe(true);
      act(() => press('0'));
      expect(onRoll).toHaveBeenCalledWith(10);
      expect(result.current.rollPrefixPending).toBe(false);
    });

    it('completes 11 and 12 via 1+1 and 1+2', () => {
      const onRoll = vi.fn();
      const { result, rerender } = renderHook(
        ({ state }: { state: GameKeyboardState }) => useGameKeyboard(state),
        { initialProps: { state: buildState({ gameState: 'WaitingForRoll', onRoll }) } }
      );

      act(() => press('1'));
      act(() => press('1'));
      expect(onRoll).toHaveBeenLastCalledWith(11);
      expect(result.current.rollPrefixPending).toBe(false);

      // Reset using rerender with same props (clears any pending state)
      rerender({ state: buildState({ gameState: 'WaitingForRoll', onRoll }) });

      act(() => press('1'));
      act(() => press('2'));
      expect(onRoll).toHaveBeenLastCalledWith(12);
    });

    it('"1" then a digit ≥3 clears the prefix and rolls that digit', () => {
      const onRoll = vi.fn();
      const { result } = renderHook(() =>
        useGameKeyboard(buildState({ gameState: 'WaitingForRoll', onRoll }))
      );
      act(() => press('1'));
      expect(result.current.rollPrefixPending).toBe(true);
      act(() => press('7'));
      // Existing behavior: 1+7 → roll 7 (not 17).
      expect(onRoll).toHaveBeenCalledWith(7);
      expect(result.current.rollPrefixPending).toBe(false);
    });

    it('"1" then a non-digit key clears the prefix without rolling', () => {
      const onRoll = vi.fn();
      const { result } = renderHook(() =>
        useGameKeyboard(buildState({ gameState: 'WaitingForRoll', onRoll }))
      );
      act(() => press('1'));
      expect(result.current.rollPrefixPending).toBe(true);
      act(() => press('x'));
      expect(onRoll).not.toHaveBeenCalled();
      expect(result.current.rollPrefixPending).toBe(false);
    });

    it('Escape clears the pending "1" prefix', () => {
      const onRoll = vi.fn();
      const { result } = renderHook(() =>
        useGameKeyboard(buildState({ gameState: 'WaitingForRoll', onRoll }))
      );
      act(() => press('1'));
      expect(result.current.rollPrefixPending).toBe(true);
      act(() => press('Escape'));
      expect(result.current.rollPrefixPending).toBe(false);
      expect(onRoll).not.toHaveBeenCalled();
    });

    it('focusin to a typing target clears the pending "1" prefix', () => {
      const onRoll = vi.fn();
      const { result } = renderHook(() =>
        useGameKeyboard(buildState({ gameState: 'WaitingForRoll', onRoll }))
      );
      act(() => press('1'));
      expect(result.current.rollPrefixPending).toBe(true);

      const input = document.createElement('input');
      input.type = 'text';
      document.body.appendChild(input);
      act(() => {
        fireEvent.focusIn(input);
      });
      expect(result.current.rollPrefixPending).toBe(false);
      document.body.removeChild(input);
    });

    it('ignores digits when target is a typing input', () => {
      const onRoll = vi.fn();
      renderHook(() => useGameKeyboard(buildState({ gameState: 'WaitingForRoll', onRoll })));
      const input = document.createElement('input');
      input.type = 'text';
      document.body.appendChild(input);
      input.focus();
      press('7', { target: input });
      expect(onRoll).not.toHaveBeenCalled();
      document.body.removeChild(input);
    });

    it('ignores digits when target is a SELECT', () => {
      const onRoll = vi.fn();
      renderHook(() => useGameKeyboard(buildState({ gameState: 'WaitingForRoll', onRoll })));
      const select = document.createElement('select');
      document.body.appendChild(select);
      select.focus();
      press('7', { target: select });
      expect(onRoll).not.toHaveBeenCalled();
      document.body.removeChild(select);
    });

    it('treats checkbox inputs as non-typing (digits still fire)', () => {
      const onRoll = vi.fn();
      renderHook(() => useGameKeyboard(buildState({ gameState: 'WaitingForRoll', onRoll })));
      const cb = document.createElement('input');
      cb.type = 'checkbox';
      document.body.appendChild(cb);
      press('7', { target: cb });
      expect(onRoll).toHaveBeenCalledWith(7);
      document.body.removeChild(cb);
    });

    it('treats readOnly inputs as non-typing (digits still fire)', () => {
      const onRoll = vi.fn();
      renderHook(() => useGameKeyboard(buildState({ gameState: 'WaitingForRoll', onRoll })));
      const input = document.createElement('input');
      input.type = 'text';
      input.readOnly = true;
      document.body.appendChild(input);
      press('7', { target: input });
      expect(onRoll).toHaveBeenCalledWith(7);
      document.body.removeChild(input);
    });

    it('ignores held keys (e.repeat === true)', () => {
      const onRoll = vi.fn();
      renderHook(() => useGameKeyboard(buildState({ gameState: 'WaitingForRoll', onRoll })));
      press('7', { repeat: true });
      expect(onRoll).not.toHaveBeenCalled();
    });
  });

  // ── Non-regression matrix: placement digits & letters ──────────────────────

  describe('placement & letter shortcuts', () => {
    function buildableRoad(buildIndex: number): RoadModel {
      return {
        roadKey: {
          hexCoordinates: { q: 0, r: 0 },
          position: 'Top',
        } as unknown as RoadModel['roadKey'],
        roadState: 'Buildable',
        ownerId: null,
        buildIndex,
      } as unknown as RoadModel;
    }

    function possibleSettlement(): BuildingModel {
      return {
        buildingKey: {
          hexCoordinates: { q: 0, r: 0 },
          position: 'Right',
        } as BuildingModel['buildingKey'],
        buildingState: 'PossibleSettlement',
        ownerId: null,
      } as unknown as BuildingModel;
    }

    function ownedSettlement(playerId: string): BuildingModel {
      return {
        buildingKey: {
          hexCoordinates: { q: 1, r: 1 },
          position: 'Left',
        } as BuildingModel['buildingKey'],
        buildingState: 'Settlement',
        ownerId: playerId,
      } as BuildingModel;
    }

    function player(id: string, unspent: string[] = []): PlayerModel {
      return { id, unspentEntitlements: unspent } as unknown as PlayerModel;
    }

    it('digit places a buildable road by buildIndex', () => {
      const proxy = makeFakeProxy();
      const roads = [buildableRoad(3)];
      renderHook(() => useGameKeyboard(buildState({ gameState: 'WaitingForNext', roads, proxy })));
      press('3');
      expect(proxy.purchaseRoad).toHaveBeenCalledTimes(1);
    });

    it('digit places a settlement at index n−1 during gameplay', () => {
      const proxy = makeFakeProxy();
      const buildings = [possibleSettlement(), possibleSettlement(), possibleSettlement()];
      renderHook(() =>
        useGameKeyboard(
          buildState({
            gameState: 'WaitingForNext',
            buildings,
            currentPlayer: player('me', ['Settlement']),
            proxy,
          })
        )
      );
      press('2');
      expect(proxy.upgradeBuilding).toHaveBeenCalledTimes(1);
    });

    it('digit does NOT place a settlement during allocation states', () => {
      const proxy = makeFakeProxy();
      const buildings = [possibleSettlement()];
      renderHook(() =>
        useGameKeyboard(
          buildState({
            gameState: 'AllocateResourceForward',
            buildings,
            currentPlayer: player('me', ['Settlement']),
            proxy,
          })
        )
      );
      press('1');
      expect(proxy.upgradeBuilding).not.toHaveBeenCalled();
    });

    it('letter A places road buildIndex 10', () => {
      const proxy = makeFakeProxy();
      const roads = [buildableRoad(10)];
      renderHook(() => useGameKeyboard(buildState({ gameState: 'WaitingForNext', roads, proxy })));
      press('A');
      expect(proxy.purchaseRoad).toHaveBeenCalledTimes(1);
    });

    it('letter Z upgrades the first owned settlement (reverse-index 0)', () => {
      const proxy = makeFakeProxy();
      const me = player('me', ['City']);
      const buildings = [ownedSettlement('me')];
      renderHook(() =>
        useGameKeyboard(
          buildState({
            gameState: 'WaitingForNext',
            buildings,
            currentPlayer: me,
            proxy,
          })
        )
      );
      press('Z');
      expect(proxy.upgradeBuilding).toHaveBeenCalledTimes(1);
    });

    // Keys come from the KeyboardShortcut enum's descriptions (the single source of
    // truth) so a description typo fails here instead of silently shipping.
    const settlementKey = KeyboardShortcutDescriptions[KeyboardShortcut.PurchaseSettlement];

    it('S fires Purchase Settlement when canPurchaseSettlement is true', () => {
      const proxy = makeFakeProxy();
      renderHook(() => useGameKeyboard(buildState({ proxy, canPurchaseSettlement: true })));
      press(settlementKey);
      expect(proxy.purchase).toHaveBeenCalledWith('Settlement');
    });

    it('S does NOT fire when canPurchaseSettlement is false', () => {
      const proxy = makeFakeProxy();
      renderHook(() => useGameKeyboard(buildState({ proxy, canPurchaseSettlement: false })));
      press(settlementKey);
      expect(proxy.purchase).not.toHaveBeenCalled();
    });

    it.each([
      [KeyboardShortcut.PurchaseCity, 'canPurchaseCity', 'City'],
      [KeyboardShortcut.PlaySoldier, 'canPlaySoldier', 'Soldier'],
      [KeyboardShortcut.PurchaseRoad, 'canPurchaseRoad', 'Road'],
      [KeyboardShortcut.PurchaseDevCard, 'canPurchaseDevCard', 'DevCard'],
    ] as const)('purchase shortcut %s fires when its flag is true', (shortcut, flag, expected) => {
      const proxy = makeFakeProxy();
      const args: BuildArgs = { proxy };
      args[flag] = true;
      renderHook(() => useGameKeyboard(buildState(args)));
      press(KeyboardShortcutDescriptions[shortcut]);
      expect(proxy.purchase).toHaveBeenCalledWith(expected);
    });
  });

  // ── Enter ──────────────────────────────────────────────────────────────────

  describe('Enter', () => {
    it('fires Next when nextEnabled is true', () => {
      const proxy = makeFakeProxy();
      renderHook(() =>
        useGameKeyboard(buildState({ proxy, actionFlags: makeFlags({ nextEnabled: true }) }))
      );
      press('Enter');
      expect(proxy.next).toHaveBeenCalledTimes(1);
    });

    it('no-op when nextEnabled is false', () => {
      const proxy = makeFakeProxy();
      renderHook(() => useGameKeyboard(buildState({ proxy })));
      press('Enter');
      expect(proxy.next).not.toHaveBeenCalled();
    });

    it('ignored when target is a typing input', () => {
      const proxy = makeFakeProxy();
      renderHook(() =>
        useGameKeyboard(buildState({ proxy, actionFlags: makeFlags({ nextEnabled: true }) }))
      );
      const input = document.createElement('input');
      input.type = 'text';
      document.body.appendChild(input);
      press('Enter', { target: input });
      expect(proxy.next).not.toHaveBeenCalled();
      document.body.removeChild(input);
    });

    it('ignored when a modal is registered', () => {
      const proxy = makeFakeProxy();
      useLayoutStore.getState().registerModal('test-modal');
      renderHook(() =>
        useGameKeyboard(
          buildState({
            proxy,
            actionFlags: makeFlags({ nextEnabled: true }),
            anyModalOpen: true,
          })
        )
      );
      press('Enter');
      expect(proxy.next).not.toHaveBeenCalled();
    });

    it('still fires Next when a button has focus (regression for focus-capture)', () => {
      const proxy = makeFakeProxy();
      renderHook(() =>
        useGameKeyboard(buildState({ proxy, actionFlags: makeFlags({ nextEnabled: true }) }))
      );
      const btn = document.createElement('button');
      btn.textContent = 'Roll';
      document.body.appendChild(btn);
      btn.focus();
      expect(document.activeElement).toBe(btn);

      press('Enter', { target: btn });
      expect(proxy.next).toHaveBeenCalledTimes(1);
      // Button should be blurred (surgical blur)
      expect(document.activeElement).not.toBe(btn);
      document.body.removeChild(btn);
    });

    it('ignores held Enter (e.repeat)', () => {
      const proxy = makeFakeProxy();
      renderHook(() =>
        useGameKeyboard(buildState({ proxy, actionFlags: makeFlags({ nextEnabled: true }) }))
      );
      press('Enter', { repeat: true });
      expect(proxy.next).not.toHaveBeenCalled();
    });
  });

  // ── Backspace ──────────────────────────────────────────────────────────────

  describe('Backspace', () => {
    it('fires Undo when undoEnabled is true', () => {
      const proxy = makeFakeProxy();
      renderHook(() =>
        useGameKeyboard(buildState({ proxy, actionFlags: makeFlags({ undoEnabled: true }) }))
      );
      press('Backspace');
      expect(proxy.undo).toHaveBeenCalledTimes(1);
    });

    it('does NOT fire Undo when target is INPUT (browser default deletes)', () => {
      const proxy = makeFakeProxy();
      renderHook(() =>
        useGameKeyboard(buildState({ proxy, actionFlags: makeFlags({ undoEnabled: true }) }))
      );
      const input = document.createElement('input');
      input.type = 'text';
      document.body.appendChild(input);
      press('Backspace', { target: input });
      expect(proxy.undo).not.toHaveBeenCalled();
      document.body.removeChild(input);
    });

    it('during WaitingForRoll with pending prefix, clears prefix and does NOT undo', () => {
      const proxy = makeFakeProxy();
      const { result } = renderHook(() =>
        useGameKeyboard(
          buildState({
            proxy,
            gameState: 'WaitingForRoll',
            actionFlags: makeFlags({ undoEnabled: true }),
          })
        )
      );
      act(() => press('1'));
      expect(result.current.rollPrefixPending).toBe(true);
      act(() => press('Backspace'));
      expect(result.current.rollPrefixPending).toBe(false);
      expect(proxy.undo).not.toHaveBeenCalled();
    });

    it('ignored when a modal is registered', () => {
      const proxy = makeFakeProxy();
      renderHook(() =>
        useGameKeyboard(
          buildState({
            proxy,
            actionFlags: makeFlags({ undoEnabled: true }),
            anyModalOpen: true,
          })
        )
      );
      press('Backspace');
      expect(proxy.undo).not.toHaveBeenCalled();
    });

    it('ignores held Backspace (e.repeat)', () => {
      const proxy = makeFakeProxy();
      renderHook(() =>
        useGameKeyboard(buildState({ proxy, actionFlags: makeFlags({ undoEnabled: true }) }))
      );
      press('Backspace', { repeat: true });
      expect(proxy.undo).not.toHaveBeenCalled();
    });
  });

  // ── Stale-closure safety ──────────────────────────────────────────────────

  describe('stale-closure safety', () => {
    it('reads latest actionFlags via ref across re-renders', () => {
      const proxy = makeFakeProxy();
      const { rerender } = renderHook(
        ({ state }: { state: GameKeyboardState }) => useGameKeyboard(state),
        {
          initialProps: {
            state: buildState({ proxy, actionFlags: makeFlags({ nextEnabled: false }) }),
          },
        }
      );

      press('Enter');
      expect(proxy.next).not.toHaveBeenCalled();

      // Simulate a server push that enables Next.
      rerender({
        state: buildState({ proxy, actionFlags: makeFlags({ nextEnabled: true }) }),
      });

      press('Enter');
      expect(proxy.next).toHaveBeenCalledTimes(1);
    });

    it('every keystroke is handled across rapid re-renders (no dropped events)', () => {
      const proxy = makeFakeProxy();
      const onRoll = vi.fn();
      const { rerender } = renderHook(
        ({ state }: { state: GameKeyboardState }) => useGameKeyboard(state),
        {
          initialProps: {
            state: buildState({ proxy, onRoll, gameState: 'WaitingForRoll' }),
          },
        }
      );

      // Press digits interleaved with re-renders (each rerender changes
      // a prop reference, simulating actionFlags updates from the server).
      for (let i = 2; i <= 9; i++) {
        rerender({
          state: buildState({
            proxy,
            onRoll,
            gameState: 'WaitingForRoll',
            actionFlags: makeFlags({ nextEnabled: i % 2 === 0 }),
          }),
        });
        press(String(i));
      }
      // Eight rolls (2..9), one per keystroke.
      expect(onRoll).toHaveBeenCalledTimes(8);
    });
  });

  // ── Surgical blur ──────────────────────────────────────────────────────────

  describe('surgical blur', () => {
    it('blurs a focused button after firing a game action', () => {
      const proxy = makeFakeProxy();
      renderHook(() =>
        useGameKeyboard(buildState({ proxy, actionFlags: makeFlags({ undoEnabled: true }) }))
      );
      const btn = document.createElement('button');
      document.body.appendChild(btn);
      btn.focus();
      expect(document.activeElement).toBe(btn);
      press('Backspace', { target: btn });
      expect(proxy.undo).toHaveBeenCalled();
      expect(document.activeElement).not.toBe(btn);
      document.body.removeChild(btn);
    });

    it('does NOT blur a focused non-button focusable element (a11y — only buttons)', () => {
      const proxy = makeFakeProxy();
      renderHook(() =>
        useGameKeyboard(buildState({ proxy, actionFlags: makeFlags({ nextEnabled: true }) }))
      );
      // Non-button focusable elements should KEEP focus after a shortcut
      // fires. A focused <div tabindex=0> isn't a typing target (so the
      // shortcut runs), but blurFocusedButton must not touch it.
      const div = document.createElement('div');
      div.setAttribute('tabindex', '0');
      document.body.appendChild(div);
      div.focus();
      expect(document.activeElement).toBe(div);

      press('Enter', { target: div });
      // The shortcut fires (div is not a typing target)…
      expect(proxy.next).toHaveBeenCalledTimes(1);
      // …but the div retains focus because it's not a <button>.
      expect(document.activeElement).toBe(div);
      document.body.removeChild(div);
    });
  });
});

// ============================================================================
// SECTION: Integration tests — real layoutStore
// ============================================================================

describe('useGameKeyboard — integration with layoutStore', () => {
  beforeEach(() => {
    useLayoutStore.getState().resetModalRegistry();
  });

  it('modal token-set: register/unregister cycle leaves the set empty', () => {
    const store = useLayoutStore.getState();
    expect(store.openModals.size).toBe(0);

    store.registerModal('save-layout-dialog');
    expect(useLayoutStore.getState().openModals.size).toBe(1);

    store.unregisterModal('save-layout-dialog');
    expect(useLayoutStore.getState().openModals.size).toBe(0);
  });

  it('modal token-set: multiple modals register and unregister independently', () => {
    const store = useLayoutStore.getState();
    store.registerModal('a');
    store.registerModal('b');
    expect(useLayoutStore.getState().openModals.size).toBe(2);
    store.unregisterModal('a');
    expect(useLayoutStore.getState().openModals.size).toBe(1);
    expect(useLayoutStore.getState().openModals.has('b')).toBe(true);
  });

  it('resetModalRegistry recovers from a leaked entry', () => {
    const store = useLayoutStore.getState();
    store.registerModal('orphaned-modal');
    // Simulate that unregisterModal was never called (e.g., component
    // unmount path skipped due to a render error).
    expect(useLayoutStore.getState().openModals.size).toBe(1);
    store.resetModalRegistry();
    expect(useLayoutStore.getState().openModals.size).toBe(0);
  });

  it('Enter no-ops when a modal is registered (via real store + anyModalOpen prop)', () => {
    useLayoutStore.getState().registerModal('save-layout-dialog');
    const proxy = makeFakeProxy();
    renderHook(() =>
      useGameKeyboard(
        buildState({
          proxy,
          actionFlags: makeFlags({ nextEnabled: true }),
          anyModalOpen: true,
        })
      )
    );
    press('Enter');
    expect(proxy.next).not.toHaveBeenCalled();
  });

  // ── PickSupplementalPlayers Enter→Next integration ────────────────────────
  // Server contract (AllowNext in GameStateMachine.cs): nextEnabled is
  // true during PickSupplementalPlayers when the player has no unspent
  // entitlements. The hook treats this state like any other — plain
  // Enter→Next.

  it('Enter advances supplemental when no unspent entitlements (nextEnabled=true)', () => {
    const proxy = makeFakeProxy();
    renderHook(() =>
      useGameKeyboard(
        buildState({
          proxy,
          gameState: 'PickSupplementalPlayers',
          actionFlags: makeFlags({ nextEnabled: true }),
          currentPlayer: {
            id: 'me',
            name: 'me',
            unspentEntitlements: [],
          } as unknown as PlayerModel,
        })
      )
    );
    press('Enter');
    expect(proxy.next).toHaveBeenCalledTimes(1);
  });

  it('Enter no-op during supplemental with unspent entitlements (nextEnabled=false)', () => {
    const proxy = makeFakeProxy();
    renderHook(() =>
      useGameKeyboard(
        buildState({
          proxy,
          gameState: 'PickSupplementalPlayers',
          actionFlags: makeFlags({ nextEnabled: false }),
          currentPlayer: {
            id: 'me',
            unspentEntitlements: ['Settlement'],
          } as unknown as PlayerModel,
        })
      )
    );
    press('Enter');
    expect(proxy.next).not.toHaveBeenCalled();
  });
});
