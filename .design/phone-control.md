# Phone Remote Control

**Status:** Draft

## Problem

The full game page has floating panels, a hex board, action cluster, and
overlays — all designed for desktop-class screens. On a phone, the panels
overlap, the board is hard to pan, and the action buttons are small.

Most of the time, the phone user only needs two things:

1. **Next** — advance the game when it's their turn
2. **Pick Supplemental** — select which players participate in supplemental
   build

Everything else (board view, resource tracking, dice stats) is better on
the main screen.

## Solution

A new route `/phone-control/{gameId}` that provides a minimal touch-first
interface. It connects to the same game via the same `useGameConnection`
hook and Zustand store — it's a second view, not a separate session.

## Page Behavior

### Default View (most game states)

Centered layout, full viewport, dark background:

- **Status text** — current game state name (e.g., "Waiting For Next",
  "Roll the Dice", "Must Move Robber")
- **Next button** — large, touch-friendly (min 64px tall). Enabled/disabled
  based on `actionFlags.nextEnabled` from GameModel. Calls `proxy.next()`.
- **Back link** — navigates to `/game/{gameId}` for full view

### PickSupplementalPlayers State

When `gameState === 'PickSupplementalPlayers'`, replace the default view
with the existing `SupplementalOverlay` component. This reuses the hex-ring
UI with player avatars, toggle selection, and center Next button.

Props come from the same hooks: `usePlayers()`, `useCurrentPlayer()`,
`usePlayerProfiles()`. Callbacks use `proxy.setParticipatingInSupplemental()`
and `proxy.next()` — identical to the game page.

## Navigation

A "Remote" button in the NavMenu (Game page section) navigates to
`/phone-control/{activeGameId}`. Uses `faMobileScreenButton` icon.

## Data Flow

The phone-control page uses the same infrastructure as the game page:

- `useGameConnection({ playerId, gameId })` — SignalR + REST proxy
- `getDevPlayerId()` — shared utility (extracted from game page)
- `useGameState()`, `useActionFlags()`, `usePlayers()`,
  `useCurrentPlayer()`, `usePlayerProfiles()` — Zustand store hooks
- `GameServiceProxy` singleton — shared across pages per player ID

No new server communication or store changes needed.

## Scope

**In scope:**

- New page at `app/phone-control/[id]/page.tsx`
- Nav menu button (Game page context only)
- Extract `getDevPlayerId()` to shared utility
- Reuse existing `SupplementalOverlay` component

**Out of scope:**

- Undo/Redo buttons (phone user can use main screen)
- Board view or tile interaction
- Purchase actions (road, settlement, city, dev card)
- Robber placement
- Dice display

## Files

| File | Action |
|------|--------|
| `react-ui/app/phone-control/[id]/page.tsx` | Create — remote control page |
| `react-ui/lib/utils/getDevPlayerId.ts` | Create — extract shared utility |
| `react-ui/app/game/[id]/page.tsx` | Modify — import from shared utility |
| `react-ui/components/layout/NavMenu.tsx` | Modify — add Remote button |
| `.design/README.md` | Modify — add doc reference |
