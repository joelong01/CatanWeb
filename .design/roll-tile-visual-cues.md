# Design: Roll-tile visual cues when rolling is disabled (issue #199)

## Problem

From #199:

> GamePlay: roll tiles should have visual clues when rolls are not enabled.
>
> perhaps have them be facedown with text that has the roll number and the %.
> functionally this would look as if we just changed the background. but we
> could animate a card flip animation to show when we should roll and when we
> should not.
>
> alternatively, we could disable it so nothing happens.
>
> this needs to work for both shortcuts and mouse.

The **RollRing** (the 2–12 hex buttons the player clicks to record a dice
roll) always looks and behaves like it is clickable, regardless of whether
the game is actually accepting a roll. There is no visual signal that tells
the player "you can't roll right now," and the two input paths don't agree on
whether a roll is even allowed.

## Current state

The roll surface is [RollRing.tsx](../react-ui/components/game/controls/RollRing.tsx),
rendered once on the game page at
[page.tsx:813](../react-ui/app/game/[id]/page.tsx#L813):

```tsx
<RollRing rollStats={rollStats} onRollClick={handleRollClick} colors={playerColors} />
```

`RollRing` has **no awareness of whether rolling is enabled**. Every tile
renders `cursor-pointer`, shows hover/press scale affordances
([RollRing.tsx:59-73](../react-ui/components/game/controls/RollRing.tsx#L59-L73)),
and unconditionally fires `onRollClick(roll)`
([RollRing.tsx:151](../react-ui/components/game/controls/RollRing.tsx#L151)).

The two input paths are **inconsistent**:

| Path | Gated today? | Behavior when not `WaitingForRoll` |
|---|---|---|
| Keyboard (2–9, 1+0/1/2) | ✅ Yes | [useGameKeyboard.ts:184](../react-ui/lib/hooks/useGameKeyboard.ts#L184) checks `gameState === 'WaitingForRoll'`; keys do nothing |
| Mouse (click RollRing tile) | ❌ No | `onRollClick` → `handleRollClick` → `proxy.roll()` fires anyway; backend rejects it |

The backend is the true authority. On every state transition
[GameStateMachine.cs:1066](../Catan3.Shared/GameLogic/GameStateMachine.cs#L1066)
sets:

```csharp
gameModel.ActionFlags.RollsEnabled = gameModel.GameState == GameState.WaitingForRoll;
```

and `OnRoll()` throws if a roll arrives in any other state
([GameStateMachine.cs:963](../Catan3.Shared/GameLogic/GameStateMachine.cs#L963)).
So a mouse click outside `WaitingForRoll` currently produces a rejected
command / `GameException` round-trip and no user-visible feedback — the worst
of both worlds.

**The flag we need already exists on the client.** `ActionFlags.RollsEnabled`
is surfaced by `useActionFlags()` and is already read on the game page at
[page.tsx:102](../react-ui/app/game/[id]/page.tsx#L102) (`actionFlags`). Note
`rollsEnabled` is defined as exactly `GameState == WaitingForRoll`, so it is
the same condition the keyboard hook already checks — a single source of truth
for both paths.

## Decision

**Dim/disabled treatment for v1, structured so a card-flip can be layered on
later** (chosen with the developer).

- v1 ships the "disable it so nothing happens" option from the issue, plus a
  clear visual cue: the ring dims and loses its button affordances when
  `rollsEnabled` is false.
- The disabled visual is isolated behind one boundary in the tile component so
  the later "face-down + flip animation" variant can replace it without
  touching the data flow or the click-gating.

### Why dim rather than face-down-first

`RollRing` is dual-purpose: it is both the **roll input** and a live **roll
statistics panel** (count + percentage per number). **The stats are a
first-class feature — the players actively read and argue about them mid-game
(e.g. "we only rolled two 9s all game").** The disabled cue therefore **must
not reduce the legibility of the count or percentage.** A full face-down
treatment hides the numbers, so it is a deliberate v2 — and even then, per the
issue, the card back keeps the number + % visible.

This constraint rules out dimming the whole tile. The cue must target the
*actionability* (the "you can act here" affordance), not the *information*.

## Approach

### 1. Thread `rollsEnabled` into RollRing

- Add an optional `rollsEnabled?: boolean` prop to `RollRingProps` (default
  `true`, so [controls-test](../react-ui/app/controls-test/page.tsx) and any
  other caller keep working unchanged).
- Pass it from the game page: `rollsEnabled={actionFlags?.rollsEnabled ?? false}`.
- Forward it into `RollHexContent`.

### 2. Visual cue (the disabled boundary)

The cue is **a background swap** — exactly the issue author's mental model
("functionally this would look as if we just changed the background"). The
count, percentage, and center `NumberToken` are text/foreground layers stacked
on top of the tile background, so swapping the background leaves all stats fully
legible. Concentrate it in `RollHexContent` behind a single `disabled` branch
so v2 can replace it wholesale:

- **Swap the tile background** from the live player-colored gradient
  (`colors.cssGradient`, [RollRing.tsx:55](../react-ui/components/game/controls/RollRing.tsx#L55))
  to a neutral, clearly-inactive "resting" background when disabled (a muted /
  desaturated fill — candidate: a dedicated `--hex-content-gradient-disabled`
  CSS custom property, or a desaturated derivation of the player gradient).
  The `count`, `percentage`, and `NumberToken` layers are **not** touched.
- **Remove the button affordance:** on hover the cursor becomes
  `not-allowed` (matching the codebase's disabled convention —
  `.menu-button:disabled` / `.nav-menu-item:disabled` in `globals.css`), and
  the hover/press scale changes are suppressed (hold `scale` at rest, ignore
  hover/press state) so the tile no longer reacts to the mouse. The
  `not-allowed` cursor is an explicit "you can't roll right now" signal on
  hover.
- Add a `transition` on the background consistent with the existing tile-dim
  transition (`0.3s ease`) so enable ⇄ disable reads as a smooth state change,
  not a flicker — this is also the natural seam where the v2 flip transition
  attaches.

Net effect: when rolling is disabled the tiles' background goes "quiet /
inactive," but every number, count, and percentage is exactly as readable as
when enabled — and it degrades gracefully into the v2 face-down/flip look,
which is itself just a more elaborate background change.

### 3. Gate the mouse path (parity with keyboard)

Stop firing rolls when disabled, so mouse matches the already-gated keyboard:

- In `RollRing`, make the item `onClick` a no-op when `!rollsEnabled` (guard at
  the source so no rejected command is ever sent).
- Belt-and-suspenders: `handleRollClick` in `page.tsx` early-returns when
  `!actionFlags?.rollsEnabled`, keeping the page the final client-side gate.

This eliminates the current rejected-`proxy.roll()` round-trip entirely.

### 4. Keyboard path

No behavior change — it is already gated on `WaitingForRoll`. Documented here
only to record that both paths now derive from the same condition
(`rollsEnabled` ⇔ `gameState === 'WaitingForRoll'`).

### 5. Structure for "flip later"

- Keep the disabled rendering in one place in `RollHexContent`.
- Reuse the existing 3D-flip toolkit already proven in the codebase —
  `preserve-3d` / `backface-hidden` / `rotate-y-180` utilities in `globals.css`
  and the `ActionCluster` front/back-face pattern
  ([ActionCluster.tsx:208-291](../react-ui/components/game/controls/ActionCluster.tsx#L208-L291)) —
  so v2 is additive, not a rewrite.
- v2 back face would show the number + percentage (per the issue), so the data
  RollRing already has is sufficient; no new props needed later.

## Files to change (design-level)

| File | Change |
|---|---|
| [RollRing.tsx](../react-ui/components/game/controls/RollRing.tsx) | Add `rollsEnabled` prop; thread to `RollHexContent`; no-op click when disabled; disabled visual (opacity/cursor/affordance) |
| [page.tsx](../react-ui/app/game/[id]/page.tsx) | Pass `rollsEnabled={actionFlags?.rollsEnabled ?? false}`; early-return in `handleRollClick` when disabled |

No backend, type-generation, or store changes — the flag already exists and is
already subscribed on the page.

## Testing

- **Unit (React Testing Library / jest):** a `RollRing` test asserting that
  with `rollsEnabled={false}` (a) clicking a tile does **not** call
  `onRollClick`, and (b) the disabled styling/affordance is applied; and with
  `rollsEnabled={true}` the click fires and the tile is interactive.
- **Regression:** confirm existing keyboard tests
  ([useGameKeyboard.test.ts](../react-ui/lib/hooks/__tests__/useGameKeyboard.test.ts))
  still pass — no keyboard logic changes.
- **Manual:** in a running game, verify the ring dims outside `WaitingForRoll`,
  brightens on entering it, mouse clicks are ignored while dimmed, and stats
  remain readable in both states.

## Non-goals

- The card-flip / face-down animation (explicit v2).
- Any change to backend roll validation or `ActionFlags` semantics.
- Changing the board hex `NumberToken`s — those are display-only, not a roll
  input; #199 is about the interactive roll tiles.

## Open questions

1. ~~Dim level~~ **Resolved:** stats (count + percentage + number) stay at full
   opacity in both states; only the interactive chrome (gradient fill,
   hover/press affordance) changes. Roll statistics are a first-class feature
   and must never be harder to read.
2. **Scope of "roll tiles."** This design treats the RollRing as the roll
   tiles. Confirm you did not also mean the board-hex number tokens.
