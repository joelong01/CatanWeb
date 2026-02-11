# Implementation Plan: Phone Remote Control

**Design doc:** [phone-control.md](../phone-control.md)

## Overview

Create a minimal phone-friendly page at `/phone-control/{gameId}` with a
Next button and supplemental player picking. Extracts shared utilities from
the game page.

## Step 1 — Extract shared utilities

**Create** `react-ui/lib/utils/getDevPlayerId.ts`

Move `getDevPlayerId()` from `app/game/[id]/page.tsx` (lines 117-128).

**Create** `react-ui/lib/utils/gameStateMessages.ts`

Move `GAME_STATE_MESSAGES` and `getStateMessage()` from
`app/game/[id]/page.tsx` (lines 71-114).

**Modify** `react-ui/app/game/[id]/page.tsx`

- Remove inline `getDevPlayerId` and `GAME_STATE_MESSAGES`/`getStateMessage`
- Add imports from `@/lib/utils/getDevPlayerId` and
  `@/lib/utils/gameStateMessages`

## Step 2 — Create phone-control page

**Create** `react-ui/app/phone-control/[id]/page.tsx`

Uses same hooks as game page:

- `useGameConnection({ playerId, gameId, autoConnect: true })`
- `useGameState()`, `useActionFlags()`, `usePlayers()`,
  `useCurrentPlayer()`, `usePlayerProfiles()`, `useSetPlayerProfiles()`

Loads player profiles on mount (same `gameApi.getPlayers()` call).

Two views:

1. **Default** — game state text + large Next button + back link
2. **PickSupplementalPlayers** — reuses `SupplementalOverlay` component

Handlers:

- `handleNext` → `proxy.next()`
- `handleSupplementalToggle` → `proxy.setParticipatingInSupplemental()`
- `handleSupplementalDone` → `proxy.next()`

## Step 3 — Add NavMenu button

**Modify** `react-ui/components/layout/NavMenu.tsx`

- Add `faMobileScreenButton` to imports
- Add `NavMenuItem` in Game page section (after "Edit Players"):

```tsx
<NavMenuItem
  icon={faMobileScreenButton}
  label="Remote"
  onClick={() => navigateTo(`/phone-control/${activeGameId}`)}
/>
```

## Verification

1. `pwsh ./catan.ps1 build` — no TypeScript errors
2. Game page → hamburger → "Remote" button visible → navigates correctly
3. Phone-control page shows state text and Next button
4. Next enabled/disabled follows `actionFlags.nextEnabled`
5. PickSupplementalPlayers state → hex ring overlay renders
6. "Back to Game" link works
