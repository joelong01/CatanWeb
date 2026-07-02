# Implementation Plan: Roll-tile visual cues when rolling is disabled (issue #199)

Design: [.design/roll-tile-visual-cues.md](../roll-tile-visual-cues.md) (approved).

## Summary

When rolling is not enabled (`ActionFlags.RollsEnabled === false`, i.e. game
state ≠ `WaitingForRoll`), the RollRing tiles: (1) swap their background to a
muted "resting" fill, (2) show a `not-allowed` cursor on hover with no
hover/press reaction, and (3) do not fire a roll on click. Count / percentage /
number stay fully legible. Keyboard is already gated — no change there.

## Files modified

| File | Change |
|---|---|
| [react-ui/app/globals.css](../../react-ui/app/globals.css) | Add `--hex-content-gradient-disabled` CSS custom property |
| [react-ui/components/game/controls/RollRing.tsx](../../react-ui/components/game/controls/RollRing.tsx) | Add `rollsEnabled` prop; thread to `RollHexContent`; disabled background + `not-allowed` cursor + suppressed hover/press; no-op click when disabled |
| [react-ui/app/game/[id]/page.tsx](../../react-ui/app/game/[id]/page.tsx) | Pass `rollsEnabled`; early-return in `handleRollClick` when disabled |
| [react-ui/components/game/controls/__tests__/RollRing.test.tsx](../../react-ui/components/game/controls/__tests__/RollRing.test.tsx) | New test: disabled click is a no-op; enabled click fires |

No backend, type-generation, or store changes.

## Per-file changes

### 1. `react-ui/app/globals.css`

After `--hex-content-gradient` (line 42-46), add a disabled variant. A muted,
desaturated slate fill that clearly reads as "inactive" while keeping foreground
text high-contrast:

```css
--hex-content-gradient-disabled: linear-gradient(
  160deg,
  rgba(51, 65, 85, 0.55) 0%,
  rgba(30, 41, 59, 0.6) 100%
);
```

(Values are a muted derivation of the existing gradient; final shade tuned
visually during verification.)

### 2. `react-ui/components/game/controls/RollRing.tsx`

__a. `RollRingProps` (lines 26-33)__ — add prop, default `true` so existing
callers (e.g. `controls-test`) are unaffected:

```tsx
/** Whether rolling is currently enabled. Defaults to true. */
rollsEnabled?: boolean;
```

__b. `RollHexContentProps` (lines 39-44)__ — add `disabled: boolean`.

__c. `RollHexContent` (lines 46-111)__ — apply the disabled treatment:

- `const gradient = disabled ? 'var(--hex-content-gradient-disabled)' : (colors?.cssGradient || 'var(--hex-content-gradient)');`
- `const scale = disabled ? 0.96 : (isPressed ? 0.9 : isHovered ? 0.94 : 0.96);`
  (hold at rest scale when disabled).
- `const borderColor = disabled ? 'var(--hex-disabled-border)' : (isHovered ? 'var(--hex-border-hover)' : 'var(--hex-border-idle)');`
  (reuse the existing `--hex-disabled-border`).
- Outer wrapper `className`: `cursor-pointer` → `disabled ? 'cursor-not-allowed' : 'cursor-pointer'`.
- When `disabled`, make the mouse handlers no-ops so hover/press state can't be
  set (guard each `setIsHovered`/`setIsPressed`, or short-circuit: e.g.
  `onMouseEnter={() => !disabled && setIsHovered(true)}`). Touch handlers same.
- Add `transition` on the inner content background so enable ⇄ disable animates
  (`transition-all duration-150` already present on the inner div covers
  background; confirm the 0.3s-style smoothness is acceptable, else bump).
- __Do not__ alter the `count`, `NumberToken`, or `percentage` layers.

__d. `RollRing` (lines 121-167)__ — accept and forward the prop:

- Destructure `rollsEnabled = true` in props.
- Pass `disabled={!rollsEnabled}` into `<RollHexContent … />` (line 144-149).
- Gate the click at the source (line 151):
  `onClick: () => { if (rollsEnabled) onRollClick?.(roll); }`.

### 3. `react-ui/app/game/[id]/page.tsx`

__a.__ At the RollRing usage (line 813), pass the flag:

```tsx
<RollRing
  rollStats={rollStats}
  onRollClick={handleRollClick}
  colors={playerColors}
  rollsEnabled={actionFlags?.rollsEnabled ?? false}
/>
```

`actionFlags` is already in scope (line 102).

__b.__ Belt-and-suspenders gate in `handleRollClick` (lines 135-159) — early
return so no rejected `proxy.roll()` is ever sent:

```tsx
const handleRollClick = useCallback(
  (rollSum: number) => {
    if (!actionFlags?.rollsEnabled) return;
    // …existing body…
  },
  [proxy, setLastRoll, actionFlags?.rollsEnabled]
);
```

(Add `actionFlags?.rollsEnabled` to the dependency array.)

### 4. `react-ui/components/game/controls/__tests__/RollRing.test.tsx` (new)

Using the project's existing RTL/jest setup (mirrors
`lib/hooks/__tests__/useGameKeyboard.test.ts` conventions):

- __disabled: click is a no-op__ — render `<RollRing rollStats={…} onRollClick={spy} rollsEnabled={false} />`, click a roll tile, assert `spy` not called.
- __enabled: click fires__ — same with `rollsEnabled={true}` (and default-omitted), click, assert `spy` called with the roll number.
- __disabled: cursor cue present__ — assert the tile wrapper carries the
  `cursor-not-allowed` class (or absence of `cursor-pointer`).

If HexGrid click wiring makes DOM-clicking a specific tile awkward in jsdom, the
first two assertions may instead target `RollHexContent`/the item `onClick`
contract directly; keep at least the no-op-when-disabled assertion.

## Verification steps

1. `cd react-ui && npm run lint` — clean.
2. `npm test -- RollRing` (and `useGameKeyboard` to confirm no regression) — pass.
3. `npx tsc --noEmit` (or the project's typecheck) — clean.
4. `pwsh ./catan.ps1 build` — solution builds (no C# touched, but confirm).
5. __Manual__ in a running game (`./catan.ps1 run`):
   - Outside `WaitingForRoll`: RollRing backgrounds are muted, hover shows
     `not-allowed`, tiles don't scale on hover/press, clicking does nothing,
     and __count/percentage/number are fully readable__.
   - On entering `WaitingForRoll`: backgrounds return to player-colored
     gradient, hover/press affordances return, click and number-key both roll.
   - Confirm the enable ⇄ disable transition is smooth, not a flicker.

## Out of scope (v2 / non-goals)

- Card-flip / face-down animation (design records this as the deliberate v2;
  the disabled boundary in `RollHexContent` is the seam it will attach to).
- Board-hex `NumberToken`s (display-only, not a roll input).
- Any backend / `ActionFlags` change.
