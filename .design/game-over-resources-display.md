# Design: Show ResourcesThisGame on Game Over

## Problem

After a winner is declared (`GameOver` state), the Players panel shows
`ResourcesThisTurn` (empty after the last roll). This wastes the panel —
`ResourcesThisGame` is already available on every `PlayerModel` and
tells the full story of the game.

## Solution

In `PlayersPanel.tsx`, the `PlayerTile` component already reads
`player.resourcesThisTurn?.[type]` per resource card. Change the
source to `player.resourcesThisGame` when `gameState === 'GameOver'`.

```
const isGameOver = gameState === 'GameOver';
const count = isGameOver
  ? player.resourcesThisGame?.[type] ?? 0
  : player.resourcesThisTurn?.[type] ?? 0;
```

Add a label above the resource row ("Resources This Game" vs the normal
unlabelled turn display) so players know what they're looking at.

## Scope

- **One file changed:** `react-ui/components/game/panels/PlayersPanel.tsx`
- Add `useGameState()` hook call inside `PlayerTile`
- Swap resource source when `GameOver`
- Add conditional label text

## What stays the same

- `ResourceCard` component — no changes
- `RESOURCE_CARD_CONFIG` — no changes
- `autoFlip` behaviour — cards still animate in
- All other game states — no change to existing behaviour
