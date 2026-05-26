# Design: Keyboard Shortcuts Robustness (issue #181)

## Problem

From #181:

> this showed up at game night. i tried to reproduce at home, but it wouldn't
> work. it looks like there is a window that can capture input that doesn't
> properly forward (bubble?) the kb message and the shortcut isn't happening.
> we should thoroughly review all the keyboard shortcut code.
>
> also backspace should be a shortcut for Undo and Enter should always be
> "Next" (if Next is available)

Clarified in chat: **the shortcut that failed was the dice roll (the 0-9
keys during `WaitingForRoll`).** That re-centers the analysis — Enter/Next
is the *secondary* enhancement; the *reliability* problem to solve is the
roll keys.

Two problems:

1. **Reliability regression on roll keys.** Game-night intermittent failure
   on number-key rolls. Not reproducible at home.
2. **Missing bindings.** Backspace→Undo and Enter→Next-when-available are
   not consistently wired.

## Current state of keyboard handling

Six independent `keydown` listeners are attached at `window`/`document`
across the React UI. They were added incrementally and don't share a
discipline:

| File:line | Scope | Keys handled | Input-field guard? |
|---|---|---|---|
| [react-ui/app/game/[id]/page.tsx:583-738](react-ui/app/game/[id]/page.tsx#L583-L738) | window | 0-9 (roll), 1-9 (road/settlement), A-Z (road/city), S/C/K/R/D (purchase) | partial — INPUT + TEXTAREA only; **no SELECT, no `isContentEditable`** |
| [react-ui/components/game/controls/ActionCluster.tsx:314-334](react-ui/components/game/controls/ActionCluster.tsx#L314-L334) | window | Enter→Next | ✅ tagName + isContentEditable |
| [react-ui/components/game/overlays/SupplementalOverlay.tsx:240-250](react-ui/components/game/overlays/SupplementalOverlay.tsx#L240-L250) | window | Enter→onDone | ❌ **no guard** |
| [react-ui/components/game/panels/SaveLayoutDialog.tsx:46-52](react-ui/components/game/panels/SaveLayoutDialog.tsx#L46-L52) | window | Escape | n/a (dialog open) |
| [react-ui/components/game/panels/ContextMenu.tsx:52-58](react-ui/components/game/panels/ContextMenu.tsx#L52-L58) | window | Escape | n/a |
| [react-ui/components/ui/DropdownMenu.tsx:71-77](react-ui/components/ui/DropdownMenu.tsx#L71-L77) | window | Escape | n/a |
| [react-ui/components/templates/{Tile,Water,Harbor}ContextMenu.tsx](react-ui/components/templates/) | **document** | Escape | n/a |
| [react-ui/components/game/panels/FloatingPanel.tsx:157-171](react-ui/components/game/panels/FloatingPanel.tsx#L157-L171) | window | Ctrl tracking | n/a (no action) |

Action dispatch is centralized in
[react-ui/app/game/[id]/page.tsx:318-348](react-ui/app/game/[id]/page.tsx#L318-L348)
through `handleAction(action)` → `proxy.{next,undo,redo,purchase,...}()`.
Roll dispatch goes through `handleRollClick` →
[react-ui/app/game/[id]/page.tsx:134](react-ui/app/game/[id]/page.tsx#L134)
(properly `useCallback`-memoized). There is **no shortcut test coverage**
today.

## Competing root-cause hypotheses

The roll-key regression is intermittent and not locally reproducible.
Three plausible mechanisms can produce that symptom; none is yet proven.
The fix proposed below addresses H1 and H2 directly (overlapping fix);
H3 is mitigated as a side effect. Disambiguation evidence is listed under
each.

### H1 — Stale closure on state transition

The big keydown effect at
[page.tsx:583-738](react-ui/app/game/[id]/page.tsx#L583-L738) declares
**12 dependencies** (`roads, buildings, gameState, currentPlayer, proxy,
handleRollClick, handleAction, canPurchaseSettlement, canPurchaseCity,
canPlaySoldier, canPurchaseRoad, canPurchaseDevCard`). Every server push
that updates `actionFlags` or the board mutates one of these. Each change
re-runs the effect: cleanup removes the old listener, setup adds a new
one.

The remove/add itself is synchronous within React's commit phase — no
event-delivery gap exists there (a common misreading the original
revision of this doc made; corrected). The real risk is a **stale
closure window**: between the moment a server push commits new state and
the moment the effect re-registers a fresh closure, the *old* listener
is still attached. If a key arrives in that window, it runs through the
old closure's state — possibly missing a just-changed `gameState ===
'WaitingForRoll'` transition and falling through to a no-op branch.

**Fit with the reported symptom:** plausible. Multiplayer game night =
many server-driven state transitions = many stale-closure windows. Solo
home = stable state, no transitions, no windows.

**How to disambiguate:** the proposed test for register-once + ref-synced
state validates stale-closure safety. Manual repro: instrument the
handler to log `{ closureCapturedAt, eventAt, closureGameState,
currentGameStateAtFireTime }` and compare on a multiplayer session.

### H2 — Client/server state-freshness lag

Roll handling is hard-gated by `gameState === 'WaitingForRoll'` at
[page.tsx:593](react-ui/app/game/[id]/page.tsx#L593). SignalR delivery,
React render commit, and React effect commit aren't instantaneous. If
the server has transitioned to `WaitingForRoll` but the corresponding
client `setState`-then-re-render hasn't yet committed, a key pressed in
that window is silently ignored by the handler — indistinguishable from
"my shortcut didn't work."

This is the same family of bug as H1 — both are "client state hasn't
caught up when the key arrived" — but the lag is in the SignalR/render
path rather than the effect-cleanup path.

**Fit with the reported symptom:** plausible, with the same multiplayer
asymmetry as H1.

**How to disambiguate:** instrument SignalR receipt time, setState
schedule time, render commit time, and key receipt time. If keys arrive
between SignalR receipt and render commit, this is the cause. The fix
proposed below (register-once + ref-synced state) does *not* fully solve
this — it solves the React-internal lag but not the SignalR-to-render
lag. A possible mitigation is a small grace window (~50 ms) where a key
arriving immediately after a server-confirmed transition into
`WaitingForRoll` is accepted, but we should not implement that until we
have evidence it's needed.

### H3 — Focus capture by an unexpected element

The user's verbatim hypothesis: "a window can capture input that doesn't
properly forward the kb message." For number keys this would require a
focused INPUT/TEXTAREA/SELECT/contentEditable that the user didn't
realize was focused. The current handler at
[page.tsx:586-588](react-ui/app/game/[id]/page.tsx#L586-L588) guards
INPUT and TEXTAREA but **not** SELECT or contentEditable; a focused
SELECT (template editor) or contentEditable region would proceed through
the handler with mixed effects, not "nothing happens." A *truly* focused
INPUT would suppress the shortcut as designed but type the key into the
input — leaving a visible artifact.

**Fit with the reported symptom:** medium-low for the specific
roll-key regression (the most plausible focused-element scenarios either
fire the shortcut anyway or leave evidence). Higher for Enter (focused
buttons activate on Enter), which is why the proposed fix still
addresses focus-capture concerns even if it isn't the dice-roll cause.

**How to disambiguate:** instrument `document.activeElement` at keydown
fire time; if it's anything other than `document.body`, log it.

## Secondary defects to fix in the same pass

These are real keyboard-handling issues that aren't the headline bug but
are cheap to fix as part of the cleanup:

1. **Missing `SELECT` and `isContentEditable` from the page.tsx input
   guard** ([page.tsx:586-588](react-ui/app/game/[id]/page.tsx#L586-L588)).
2. **`SupplementalOverlay`'s Enter handler has no input guard at all**
   ([SupplementalOverlay.tsx:240-250](react-ui/components/game/overlays/SupplementalOverlay.tsx#L240-L250)).
3. **Two Enter handlers can race.** When SupplementalOverlay and
   ActionCluster are both mounted, both fire on a single Enter; order
   is registration order, which is render order, which is not stable.
4. **Prefix-prefix-then-focus-switch edge case.** A user can press `1`
   (prefix set), focus into a typing UI, type, return, press a digit
   → unexpected 10/11/12 path. Impact is **low**: per the user's stated
   workflow, Undo is always available and they use it frequently, so the
   user-visible consequence is "press Undo, try again." We still clear
   the prefix on focus transition to a typing target as a small
   correctness improvement, but it is not load-bearing for this issue.
5. **No visual indicator for the pending "1" prefix.** A user mid-prefix
   sees no UI feedback. Treated as UX clarity, not root-cause fix.

## Proposed approach

Single hook, single discipline, focus reset, stable listener, plus the
Enter/Backspace bindings. Also: **future-proofing.** The user plans to
add ships (analogous to roads but across water tiles); under the current
six-handler topology, adding ships means wiring keys in a sixth place
with its own quirks. A unified hook means new piece types pick up the
same shortcut discipline for free — one place to add, one place to test.

### 1. Centralize game shortcuts in one hook with stable listener

Introduce `react-ui/lib/hooks/useGameKeyboard.ts`. **The listener is
registered exactly once** (empty deps) and reads current state through a
ref that's updated every render:

```ts
export function useGameKeyboard(state: GameKeyboardState) {
  const stateRef = useRef(state);
  useEffect(() => { stateRef.current = state; }); // every render

  useEffect(() => {
    const handler = (e: KeyboardEvent) => {
      handleKeyDown(e, stateRef.current);
    };
    window.addEventListener('keydown', handler, { capture: true });
    return () => window.removeEventListener('keydown', handler, { capture: true });
  }, []); // register once, never re-register
}
```

This removes the stale-closure window (H1) and, by virtue of always
reading current state through the ref, narrows H2's surface to only the
SignalR-receipt-to-React-commit gap (whatever's left after this is what
warrants instrumentation, not pre-emptive grace-window logic).

Move the dispatch logic out of `page.tsx:583-738` and out of
`ActionCluster.tsx:314-334`. Mount the hook once in `page.tsx`. Single
registration also incidentally fixes Secondary Defect #3 (Enter race).

### 2. One shared input-field guard

```ts
function isTypingTarget(target: EventTarget | null): boolean {
  if (!(target instanceof HTMLElement)) return false;
  if (target.isContentEditable) return true;
  const tag = target.tagName;
  return tag === 'INPUT' || tag === 'TEXTAREA' || tag === 'SELECT';
}
```

Adds `SELECT` and `isContentEditable` to the page.tsx guard; fixes
Secondary Defect #1.

### 3. Capture phase + focus reset

Listen with `{ capture: true }` so the hook sees keydown before any
focused element's bubble-phase default. After a game action fires, blur
the active element so a subsequent Enter (now wired to Next) falls
through to the window-level handler. This improves Enter reliability in
common cases, but does not guarantee precedence over all capture-phase
handlers or cross-context focus targets:

```ts
if (document.activeElement instanceof HTMLElement &&
    document.activeElement !== document.body) {
  document.activeElement.blur();
}
```

Capture-phase listening is scoped to Enter and Backspace only — the
alphanumeric branches don't need it.

### 4. Clear pending prefix on focus transition

Add a `focusin` listener: if the focus target is a typing target, clear
`pendingRollPrefixRef`. Covers Secondary Defect #4. Small, defensive,
no user-visible effect except in the edge case it fixes.

### 5. Pending-prefix visual indicator (UX clarity, not root-cause)

When `pendingRollPrefix === true`, render a small badge near the dice
panel ("1_") so the user can see they're mid-sequence. Clears when the
sequence resolves, the user presses any non-digit, or `gameState`
leaves `WaitingForRoll`.

Explicitly labeled as UX clarity. Independent of the reliability work.

### 6. Enter→Next contract

Single rule, evaluated in this order, all in the hook:

1. If a modal is open (SaveLayoutDialog, etc.) → ignore.
2. Else if target is a typing element → ignore.
3. Else if `actionFlags.nextEnabled === true` → fire Next.
4. Else → no-op.

Verified against `AllowNext`
([GameStateMachine.cs:1068-1082](Catan3.Shared/GameLogic/GameStateMachine.cs#L1068-L1082)):
`nextEnabled` is `true` during `PickSupplementalPlayers` whenever the
player has no unspent entitlements. The "no participating players →
Enter → continue" workflow named in #181 is handled by this contract —
SupplementalOverlay's separate Enter handler is removed.

### 7. Backspace→Undo

Mirror of Enter→Next:

1. If target is a typing element → return (browser default deletes a
   character).
2. Else if a modal is open → ignore.
3. Else if `gameState === 'WaitingForRoll'` and prefix is pending →
   clear prefix, `preventDefault()`. (Matches the user's "undo the
   keypress" framing.)
4. Else if `actionFlags.undoEnabled === true` → fire Undo,
   `preventDefault()` (otherwise some keyboard layouts treat Backspace
   outside an input as "browser back").
5. Else → no-op.

### 8. Modal-open state

Hook needs to know "is a modal open." Use a **token-set** (`Set<string>`
of modal IDs) in `layoutStore`, not a counter — a counter that loses a
decrement leaves shortcuts disabled indefinitely with no diagnostic
trail. The set makes it trivial to see *which* modal forgot to clean up,
and supports a defensive `resetModalRegistry()` debug helper. Each modal
registers and unregisters via a `useEffect(() => { register(id); return
() => unregister(id); }, [])` block; React guarantees cleanup on
unmount.

### 9. Tests

`react-ui/lib/hooks/__tests__/useGameKeyboard.test.ts` covering:

- **Stale-closure safety:** simulate `actionFlags` updates between
  keystrokes via prop changes; assert every keystroke is handled and
  reads the latest state via the ref. (This validates the H1 fix; it
  does **not** prove the existence of dropped-event gaps and isn't
  framed as such.)
- Roll: 2-9 rolls immediately during `WaitingForRoll`.
- Roll: `1` then `0`/`1`/`2` rolls 10/11/12.
- Roll: `1` then non-digit clears the prefix without rolling.
- Roll: number keys ignored when target is INPUT / TEXTAREA / SELECT /
  contentEditable.
- Roll: prefix is cleared on `focusin` to a typing target.
- Roll: number keys ignored outside `WaitingForRoll`.
- Enter: fires Next when `nextEnabled: true`.
- Enter: no-op when `nextEnabled: false`.
- Enter: ignored when target is a typing element.
- Enter: ignored when a modal is registered.
- Enter: still fires Next when a `<button>` is focused (regression for
  the focus-capture failure mode).
- **Enter integration:** during `PickSupplementalPlayers` with no
  participating players selected, Enter fires Next; verify the server
  contract from `AllowNext` is honored. Also: a negative case where
  unspent entitlements exist and Enter does nothing.
- Backspace: fires Undo when `undoEnabled: true` with `preventDefault`.
- Backspace: lets browser default fire when target is INPUT.
- Backspace: during `WaitingForRoll` with pending prefix, clears prefix
  and does NOT fire Undo.
- Backspace: ignored when a modal is registered.
- S/C/K/R/D respect `canPurchase*` flags.
- Modal registry: register-then-unregister leaves an empty set;
  `resetModalRegistry()` recovers from a missed unregister.

This is the first keyboard-shortcut test suite in the repo. Pattern:
`@testing-library/react` `renderHook` + `fireEvent.keyDown(window, …)`.

### 10. Leave alone

- Per-component Escape on modals/menus — localized, not implicated.
- Template context menus' `document`-vs-`window` inconsistency.
- The Ctrl-tracking in `FloatingPanel`.

## Out of scope

- **Dice panel minimized:** user confirms the dice panel is never
  minimized during real game play; ruled out as a confounder.
- Accessibility (arrow keys in RobberTargetMenu, focus return on dialog
  close, screen-reader announcements). Own design.
- On-screen cheat sheet / customizable bindings.
- Pre-emptive grace-window logic for H2 — implement only if
  instrumentation post-fix shows it's needed.

## In-scope investigation (was previously out-of-scope)

- **Client/server state freshness around `WaitingForRoll` transitions
  (H2).** Once the register-once + ref-synced fix is shipped, add
  lightweight instrumentation in production-trace mode that logs the
  delta between SignalR receipt of a `WaitingForRoll` transition and
  the first subsequent keydown. If the deltas show measurable lag and
  user reports persist, design a grace-window fix in a follow-up.

## Decisions resolved during review

1. **Backspace semantics** — confirmed: input-safe, prefix-clear while a
   pending prefix exists, Undo otherwise. Readonly inputs are handled by
   default browser behavior since we return early on typing targets.
2. **Enter in `PickSupplementalPlayers` via `nextEnabled` only** — confirmed
   against `AllowNext`. Tests cover both the no-participating-players path
   and the unspent-entitlements negative case.
3. **Full migration into one hook** — confirmed. Reinforced by the
   ships-coming-soon argument: a unified hook means new piece types pick
   up shortcuts automatically.
4. **Pending `1`-prefix indicator** — kept in. Labeled as UX clarity,
   not root-cause work.
5. **Dice-panel minimized roll feedback** — out of scope; user
   testimony rules this out as the failure mode.

## Files anticipated to change (final list in the implementation plan)

- New: `react-ui/lib/hooks/useGameKeyboard.ts`
- New: `react-ui/lib/utils/isTypingTarget.ts`
- New: `react-ui/lib/hooks/__tests__/useGameKeyboard.test.ts`
- Modified: `react-ui/lib/stores/layoutStore.ts` (modal token-set)
- Modified: `react-ui/app/game/[id]/page.tsx` (remove inline handler,
  call hook, render prefix indicator)
- Modified: `react-ui/components/game/controls/ActionCluster.tsx`
  (remove inline Enter handler)
- Modified: `react-ui/components/game/overlays/SupplementalOverlay.tsx`
  (remove inline Enter handler)
- Modified: `react-ui/components/game/panels/SaveLayoutDialog.tsx`
  (register/unregister with modal token-set)

## Stop

Per `.claude/CLAUDE.md` Stage 1: design only. No implementation plan or
code until this design is approved.
