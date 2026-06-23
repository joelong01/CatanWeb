# Implementation Plan: Keyboard Shortcuts Robustness (issue #181)

Branch: `keyboard-shortcuts` (to be created from `staging`).
Design: [.design/keyboard-shortcuts.md](../keyboard-shortcuts.md).

## Goal

Address the dice-roll keystroke regression by removing the stale-closure
window in the current handler, migrate all keyboard handling into one hook
with one discipline (also future-proofing for new piece types like ships),
add Backspace→Undo and Enter→Next bindings, and add the first
keyboard-shortcut test suite — in a single shippable change.

## Final decisions locked in (post-review)

Two rounds of adversarial review (issue #181 comments) finalized the
scope below. Decisions are binding; deviations require re-review.

### Design semantics

1. **Backspace semantics:** "undo the most recent action."
   - In a typing target → browser default (delete character).
   - During `WaitingForRoll` with a pending `1` prefix → clear prefix
     only, no Undo (most recent action is the half-typed roll).
   - Modal open → ignore.
   - Otherwise: if `undoEnabled === true` → fire Undo +
     `preventDefault()`. Else no-op.
2. **Enter during `PickSupplementalPlayers`:** plain Enter→Next via
   `actionFlags.nextEnabled`. Verified against
   [`AllowNext`](../../Catan3.Shared/GameLogic/GameStateMachine.cs#L1068-L1082).
   `SupplementalOverlay`'s separate Enter handler removed.
3. **Full migration:** all game shortcut logic in `useGameKeyboard`.
   New piece types (ships, etc.) wire into the one hook.
4. **Pending `1` prefix indicator:** small visible badge near the dice
   panel. UX clarity, not root-cause work.
5. **Modal registry:** token-set + `resetModalRegistry()`, not a
   counter.
6. **Prefix-clear lifecycle:** on state change leaving `WaitingForRoll`,
   on focusin to a typing target, on Escape. **No timeout** — Undo is
   always available, so a stale prefix self-corrects on the next user
   action.
7. **Repeat-key:** `if (e.repeat) return;` at handler top. Low-impact
   safeguard (held `1` could roll 11; held Backspace = repeated
   undos→redos). Included for minor robustness.
8. **IME composition:** **out of scope.** Single-tenant user base does
   not use IME.
9. **Surgical blur:** after a game action, blur only if
   `activeElement instanceof HTMLButtonElement`. Avoids stealing focus
   from keyboard-only users on custom focusable controls.
10. **Capture-phase listening:** scoped to Enter and Backspace only.
    Alphanumeric branches use bubble.
11. **Typed dispatch:** hook receives the existing `proxy` and calls
    typed methods (`proxy.next()`, `proxy.undo()`,
    `proxy.purchase(Entitlement.Settlement)`, …). No `onAction(string)`
    indirection. Uses the existing
    [`Entitlement`](../../Catan3.Shared/Models/GameEnums.cs#L103) enum
    via its generated TypeScript binding.

### Ownership and precedence — key contract

Single source of truth for every key the hook touches. Order in the
table is evaluation order (invariant).

| Key | Owned by hook | Precedence (checked in order) | Dispatch | Repeat |
|---|---|---|---|---|
| `Escape` | Yes (prefix only) | `WaitingForRoll` + prefix pending → clear prefix. Otherwise: not handled (per-modal handlers still own Escape-to-close). | n/a | ignore |
| `Backspace` | Yes | (1) typing target → return; (2) modal → ignore; (3) `WaitingForRoll` + prefix → clear prefix; (4) `undoEnabled` → `proxy.undo()`; (5) else no-op | `proxy.undo()` | ignore |
| `Enter` | Yes | (1) typing target → ignore; (2) modal → ignore; (3) `nextEnabled` → `proxy.next()`; (4) else no-op | `proxy.next()` | ignore |
| `0`–`9` | Yes | (1) typing target → ignore; (2) `WaitingForRoll` → roll logic (incl. prefix); (3) `1`–`9` only and not in `WaitingForRoll` → road/settlement placement | `proxy.roll()` / `proxy.purchaseRoad()` / `proxy.upgradeBuilding()` | ignore |
| `A`–`Z` | Yes | (1) typing target → ignore; (2) road by `buildIndex` (A=10…Z=35); (3) city upgrade reverse index (Z=settlement[0]); (4) S/C/K/R/D purchase | `proxy.purchaseRoad()` / `proxy.upgradeBuilding()` / `proxy.purchase(Entitlement)` | ignore |
| `Ctrl` (track) | No (FloatingPanel-local) | FloatingPanel.tsx tracks for drag. Not a shortcut. | n/a | n/a |
| All other Escape | No | Per-component handlers on SaveLayoutDialog, ContextMenu, DropdownMenu, TileContextMenu, WaterContextMenu, HarborContextMenu. Untouched. | n/a | n/a |

Capture-phase listening applies only to **Enter** and **Backspace**.
The two top-level early-exits — `if (e.repeat) return;` and
`if (isTypingTarget(...)) return;` (where applicable) — are part of
the invariant and labeled as such in code.

### Non-regression matrix

Behavior that must continue to work bit-for-bit after migration. Every
row maps to an explicit test:

| Existing shortcut | Behavior | Test name |
|---|---|---|
| `2`–`9` in `WaitingForRoll` | Rolls that number | `rolls digit during WaitingForRoll` |
| `1` then `0`/`1`/`2` | Rolls 10/11/12 | `rolls 10-12 via 1-prefix` |
| `1` then digit ≥3 | Clears prefix, rolls that digit | `1-prefix flushed by higher digit` |
| `1` then non-digit | Clears prefix, no roll | `non-digit clears 1-prefix` |
| `1`–`9` outside `WaitingForRoll` with road `buildIndex=n` | Places that road | `digit places road by buildIndex` |
| `1`–`9` outside `WaitingForRoll` with Settlement entitlement, not in allocation | Places settlement at index n−1 | `digit places settlement` |
| `1`–`9` in allocation state | Does NOT place settlement (gated by `inAllocation` flag) | `digit does not place settlement during allocation` |
| `A`–`Z` with matching road `buildIndex` (A=10…) | Places that road | `letter places road by buildIndex` |
| `A`–`Z` with City entitlement, reverse index | Upgrades that settlement | `letter upgrades by reverse-index` |
| `S`/`C`/`K`/`R`/`D` (any case) when `canPurchase*` true | Fires the purchase | `purchase shortcut respects flag` |
| `S`/`C`/`K`/`R`/`D` when `canPurchase*` false | No-op | `purchase shortcut blocked by flag` |
| Roll handler ignores INPUT/TEXTAREA focus | Returns early | `roll respects typing target` |

Plus the new bindings:

| New binding | Behavior | Test name |
|---|---|---|
| Enter when `nextEnabled: true` | Fires Next | `Enter fires Next` |
| Enter when `nextEnabled: false` | No-op | `Enter no-op when Next disabled` |
| Enter in `PickSupplementalPlayers` (no participants) | Fires Next, server transitions | `Enter advances supplemental with no participants` |
| Enter when a `<button>` has focus | Fires Next (not the button) | `focused button does not steal Enter` |
| Backspace when `undoEnabled: true` | Fires Undo + `preventDefault` | `Backspace fires Undo` |
| Backspace inside INPUT | Browser default deletes; no Undo | `Backspace deletes in input` |
| Backspace during `WaitingForRoll` + prefix | Clears prefix; no Undo | `Backspace clears 1-prefix` |
| Escape during `WaitingForRoll` + prefix | Clears prefix | `Escape clears 1-prefix` |
| Repeat key (held) | Ignored | `e.repeat=true is no-op` |
| Modal open | Enter/Backspace ignored | `modal blocks Enter and Backspace` |
| Modal counter recovery | `resetModalRegistry()` clears stuck entries | `resetModalRegistry recovers from leak` |

## File-by-file changes

### 1. New: `react-ui/lib/hooks/useGameKeyboard.ts`

Owns the whole game-keyboard contract.

**Structure:**

- One window-level keydown listener registered **once** on mount
  (`useEffect` with empty deps); state read through a ref synced
  every render.
- Capture-phase for Enter/Backspace only (separate listener with
  `{ capture: true }`); bubble-phase for everything else.
- One `focusin` listener: if new focus target is a typing target,
  clear the prefix.
- `pendingRollPrefixRef` (synchronous) + `pendingRollPrefix` state
  (for indicator), synced via a single setter.
- `clearPrefixOnStateChange` effect: when `gameState` leaves
  `WaitingForRoll`, clear prefix.
- `blurFocusedButton()` helper called after any successful game
  action.

**Public shape:**

```ts
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
}

export interface GameKeyboardResult {
  rollPrefixPending: boolean;
}

export function useGameKeyboard(state: GameKeyboardState): GameKeyboardResult;
```

No `onAction` indirection. Hook dispatches directly via `proxy`:
`proxy.next()`, `proxy.undo()`, `proxy.purchase(Entitlement.Settlement)`,
`proxy.purchaseRoad(roadKey)`, `proxy.upgradeBuilding(buildingKey)`,
`proxy.roll(die1, die2)`. The `Entitlement` enum is the existing one
from Shared, via its TypeGen-emitted TypeScript binding.

**Handler body (invariant order):**

```ts
// INVARIANT — see KEY CONTRACT in implementation plan
if (e.repeat) return;
if (e.isComposing) return;       // Defensive; IME out of scope but cheap

const key = e.key;
const state = stateRef.current;

// Enter — capture-phase listener
if (key === 'Enter') {
  if (state.anyModalOpen) return;
  if (isTypingTarget(e.target)) return;
  if (state.actionFlags?.nextEnabled) {
    e.preventDefault();
    state.proxy.next();
    blurFocusedButton();
  }
  return;
}

// Backspace — capture-phase listener
if (key === 'Backspace') {
  if (isTypingTarget(e.target)) return;   // Browser default deletes
  if (state.anyModalOpen) return;
  if (state.gameState === 'WaitingForRoll' && prefixRef.current) {
    setPrefix(false);
    e.preventDefault();
    return;
  }
  if (state.actionFlags?.undoEnabled) {
    e.preventDefault();
    state.proxy.undo();
    blurFocusedButton();
  }
  return;
}

// Escape — only clears prefix; per-modal handlers still own close-on-Esc
if (key === 'Escape') {
  if (state.gameState === 'WaitingForRoll' && prefixRef.current) {
    setPrefix(false);
  }
  return;
}

// Roll digits, placement digits, letters, purchase shortcuts
// (bubble-phase listener; typing-target guard applied per-branch)
// Logic from page.tsx:592-720, preserved bit-for-bit per the
// non-regression matrix.
```

The `e.isComposing` check is defensive: IME support is out of scope but
the check is free and prevents an accidental future regression if our
audience ever expands.

### 2. New: `react-ui/lib/utils/isTypingTarget.ts`

Refined contract handling readonly/disabled/input-type edge cases:

```ts
const NON_TEXT_INPUT_TYPES = new Set([
  'checkbox', 'radio', 'button', 'submit', 'reset',
  'image', 'range', 'color', 'file',
]);

export function isTypingTarget(target: EventTarget | null): boolean {
  if (!(target instanceof HTMLElement)) return false;
  if (target.isContentEditable) return true;

  if (target instanceof HTMLTextAreaElement) {
    return !target.readOnly && !target.disabled;
  }
  if (target instanceof HTMLInputElement) {
    if (target.readOnly || target.disabled) return false;
    return !NON_TEXT_INPUT_TYPES.has(target.type);
  }
  if (target instanceof HTMLSelectElement) {
    return !target.disabled;
  }
  return false;
}
```

Rationale: Backspace inside a checkbox or readonly input should fire
Undo, not no-op. Without the type/readonly check the old guard was
overly broad.

### 3. New: `react-ui/lib/hooks/__tests__/useGameKeyboard.test.ts`

Organized into two clearly-labeled sections.

**Unit (hook in isolation, fake `proxy`):**

- All rows of the non-regression matrix above (each is one `it()`).
- Repeat-key safeguard (`e.repeat=true` is no-op).
- `isTypingTarget` edge cases: checkbox, readonly, disabled, select.
- Prefix lifecycle: state-change clear, focusin clear, Escape clear,
  no timeout (verify prefix persists across an arbitrary delay).
- Surgical blur: blurs `<button>` but not focusable `<div>`.
- Stale-closure safety: rapid `actionFlags` updates between
  keystrokes; assert every keystroke reads latest state. Validates
  ref-based state access — **no claim about dropped-event gaps.**

**Integration (mounted in a host component, real `layoutStore`):**

- `PickSupplementalPlayers` no-participants → Enter fires Next.
- `PickSupplementalPlayers` with unspent entitlements → Enter no-op.
- `SaveLayoutDialog` open → Enter saves (default form behavior),
  Backspace deletes; neither fires Next/Undo.
- Focused `<button>` → Enter fires Next, not the button's onClick.
- Modal token-set: register/unregister pairs leave empty set;
  multiple modals; `resetModalRegistry()` recovery.

### 4. Modified: `react-ui/lib/stores/layoutStore.ts`

Token-set modal registry:

```ts
openModals: Set<string>;
registerModal: (id: string) => void;
unregisterModal: (id: string) => void;
resetModalRegistry: () => void;
```

Selector:

```ts
export const useAnyModalOpen = () =>
  useLayoutStore((s) => s.openModals.size > 0);
```

### 5. Modified: `react-ui/app/game/[id]/page.tsx`

- Remove inline keydown effect ([page.tsx:583-738](../../react-ui/app/game/[id]/page.tsx#L583-L738)).
- Remove prefix-clear effect ([page.tsx:417-421](../../react-ui/app/game/[id]/page.tsx#L417-L421)) — now in the hook.
- Remove `pendingRollPrefixRef` declaration ([page.tsx:414](../../react-ui/app/game/[id]/page.tsx#L414)).
- **Remove the `handleAction(action: string)` shim** ([page.tsx:318-348](../../react-ui/app/game/[id]/page.tsx#L318-L348)) — the hook calls `proxy` directly. Existing onClick handlers in ActionCluster that called `onAction('next')` etc. need their wiring changed too (see file 6).
- Call `useGameKeyboard({ proxy, gameState, roads, buildings, currentPlayer, actionFlags, canPurchase*, anyModalOpen })`.
- Render the prefix indicator:

  ```tsx
  {rollPrefixPending && (
    <div className="roll-prefix-indicator" aria-live="polite">1_</div>
  )}
  ```

  Positioned absolutely near the `dice` FloatingPanel header. Styling
  via existing CSS custom properties per CLAUDE.md rule 7.

### 6. Modified: `react-ui/components/game/controls/ActionCluster.tsx`

- Remove the Enter handler ([ActionCluster.tsx:314-334](../../react-ui/components/game/controls/ActionCluster.tsx#L314-L334)).
- Update `onAction` prop contract or remove it: ActionCluster's hex
  buttons currently call `props.onAction('next')` etc. on click.
  Either:
  - **(a)** keep `onAction(string)` for *clicks* only (decouples from
    the hook's typed dispatch), or
  - **(b)** pass typed callbacks (`onNext`, `onUndo`, `onPurchase`)
    from page.tsx.
  Recommended: **(a)** — onClick wiring is unrelated to the keyboard
  hook's typed contract; changing it expands the diff for no benefit.

### 7. Modified: `react-ui/components/game/overlays/SupplementalOverlay.tsx`

- Remove the Enter handler ([SupplementalOverlay.tsx:240-250](../../react-ui/components/game/overlays/SupplementalOverlay.tsx#L240-L250)).
- "No participants → Enter → continue" now flows through Enter→Next
  via `nextEnabled`.

### 8. Modified: `react-ui/components/game/panels/SaveLayoutDialog.tsx`

```ts
useEffect(() => {
  registerModal('save-layout-dialog');
  return () => unregisterModal('save-layout-dialog');
}, []);
```

React guarantees cleanup on unmount.

## Files modified — summary table

| File | Type | Net change |
|------|------|------------|
| `react-ui/lib/hooks/useGameKeyboard.ts` | New | ~300 lines (handler + capture/bubble split + focusin + blur + prefix sync) |
| `react-ui/lib/utils/isTypingTarget.ts` | New | ~25 lines (refined contract) |
| `react-ui/lib/hooks/__tests__/useGameKeyboard.test.ts` | New | ~500 lines (unit + integration sections) |
| `react-ui/lib/stores/layoutStore.ts` | Modified | +25 lines (token-set + reset + selector) |
| `react-ui/app/game/[id]/page.tsx` | Modified | -~180 (handlers + handleAction shim), +5 (hook + indicator) |
| `react-ui/components/game/controls/ActionCluster.tsx` | Modified | -20 (Enter useEffect removed) |
| `react-ui/components/game/overlays/SupplementalOverlay.tsx` | Modified | -10 (Enter useEffect removed) |
| `react-ui/components/game/panels/SaveLayoutDialog.tsx` | Modified | +6 (register/unregister modal) |

## Verification

1. `./catan.ps1 build` — clean.
2. `./catan.ps1 test` — all existing tests pass; new keyboard tests pass.
3. **Non-regression test pass:** every row in the matrix above maps to
   a green test. Reviewer audit: open the matrix, click through each
   test name in the test file, confirm coverage.
4. **State-freshness investigation (post-fix):** add lightweight trace
   logging in the hook for the SignalR→render lag described as H2 in
   the design. If post-deployment user reports persist and traces show
   measurable lag, design a grace-window fix as a follow-up.
5. **Manual verification (live app via `./catan.ps1 run`):**
   - 4-player AI game. During and immediately after opponent turns,
     press number keys; verify rolls fire consistently.
   - `1` then `0`/`1`/`2` — verify 10/11/12 rolls and the prefix
     indicator appears then clears.
   - `1` then digit ≥3 — verify prefix clears, digit rolls.
   - `1`, click into a rename input, click back, press a digit —
     verify the prefix was cleared by the focus transition.
   - `1`, then Escape — verify prefix cleared.
   - Hold a number key — verify only one roll fires (repeat ignored).
   - Click a button, press Enter — verify Next fires, not the
     button's onClick.
   - Backspace outside an input → Undo (if enabled).
   - Backspace inside an input → character deletes; no Undo.
   - Backspace inside a checkbox → Undo (per refined typing-target).
   - `PickSupplementalPlayers` with no participants → Enter advances.
   - `PickSupplementalPlayers` after purchasing a Settlement → Enter
     no-op (unspent entitlement blocks `nextEnabled`).
   - SaveLayoutDialog open → Enter saves; Backspace deletes characters.
   - Close SaveLayoutDialog → shortcuts work again. Token-set is
     empty (verify in Zustand devtools).
   - **F6 confounder check:** confirm dice panel is visible during
     test session.
   - **A11y check:** press Tab after each shortcut → focus lands
     somewhere sensible (body, not stuck on consumed button).
6. `./catan.ps1 lint` — markdown lint-free.

## Risks & mitigations

- **Risk:** once-only listener registration may introduce stale-closure
  bugs in future contributions if state isn't routed through the ref.
  **Mitigation:** code comment at top of hook explaining the pattern;
  test suite catches stale-closure regressions.
- **Risk:** `nextEnabled` ordering change for `PickSupplementalPlayers`
  if `AllowNext` semantics aren't what the design read them to be.
  **Mitigation:** verified against source; positive and negative
  integration tests both exist.
- **Risk:** capture-phase listening could conflict with future
  components that install their own capture-phase keydown handlers.
  **Mitigation:** scoped to Enter and Backspace only; audit found no
  current conflicts.
- **Risk:** state-freshness (H2) not directly addressed.
  **Mitigation:** instrumented in verification step 4 to measure
  rather than assume. No pre-emptive grace-window logic.

## Out of scope

- IME composition handling (single-tenant user base does not use IME).
- Pre-emptive grace-window logic for H2.
- Accessibility beyond the verification step (arrow-key navigation,
  focus return on dialog close, screen-reader announcements).
- On-screen cheat sheet / customizable bindings.
- Resurfacing a minimized dice panel — user-confirmed not a failure
  mode.
- Migrating remaining per-component Escape handlers.

## Stop

Per `.claude/CLAUDE.md` Stage 2: plan only. No code until this plan is
approved.
