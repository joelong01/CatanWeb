# Winner Overlay Design

## Overview

The WinnerOverlay is a single unified component that replaces the previous three-component
winner flow (`WinnerDialog`, `WinnerCelebration`, `VictoryPointsOverlay`). It uses the same
`HexGrid` + `CLUSTER_7` layout as `SupplementalOverlay`, ensuring consistent hex styling and
avoiding the positioning bugs that affected the old polar-coordinate approach.

## Problem Statement

The old winner flow had three issues:

1. **Inconsistent styling** -- `WinnerDialog` was a modal dialog box that didn't match the
   hex-based visual language of the rest of the game
2. **Positioning bugs** -- `WinnerCelebration` used manual polar coordinates
   (`Math.cos(angle) * radius`) with hardcoded pixel offsets, leaving hexes in unexpected
   positions at different screen sizes
3. **Fragmented state** -- Three separate components with three separate render conditions
   made the flow difficult to reason about

## Component Architecture

```text
WinnerOverlay (single component, 3 internal phases)
├── Phase: 'ready' | 'celebrating' | 'scoring'
├── HexGrid (CLUSTER_7 layout, hexSize=50, gap=3, fitToParent)
│   ├── Center Hex (varies by phase)
│   │   ├── 'ready': WinnerCenterHex -- "Winner!" button
│   │   ├── 'celebrating': CelebrationCenterHex -- trophy icon
│   │   └── 'scoring': EndGameCenterHex -- "End Game" button
│   └── Ring Hexes (up to 6 players)
│       ├── 'ready'/'celebrating': PlayerDisplayHex -- avatar + name
│       └── 'scoring': PlayerScoringHex -- avatar + VP + "+" button
└── ConfettiOverlay (during 'celebrating' phase only)
```

## Props Interface

```typescript
/** Player data needed for the winner overlay */
export interface WinnerPlayer {
  id: string;
  name: string;
  score: number;
  colors: PlayerColors;
  avatarUrl?: string;
}

export interface WinnerOverlayProps {
  /** All players in the game */
  players: WinnerPlayer[];
  /** Current player's colors with gradient (for center hex styling) */
  currentPlayerColors: PlayerColorsWithGradient;
  /** Duration of celebration spin in ms (default: 5000) */
  celebrationDurationMs?: number;
  /** Called when "End Game" is clicked with final VP scores */
  onEndGame: (vpScores: Record<string, number>) => void;
}
```

## Phase Flow

### Phase 1: Ready

The initial state. Shows all players in a hex ring with a "Winner!" button in the center.

- **Center hex**: Styled with `currentPlayerColors.cssGradient`. Text reads "Winner!" with
  "(Click)" subtitle. Uses the same hover/press scale pattern as SupplementalOverlay's
  NextButtonContent (scale 0.92 -> 0.90 hover -> 0.88 press).
- **Ring hexes**: Each player shown with avatar circle (40px, rounded) and name below.
  Uses `buildCssGradient(colors)` for background, `hex-clip-flat` for shape. Same visual
  treatment as SupplementalOverlay's PlayerHexContent, but without toggle logic.
- **Transition**: Click the center hex to advance to 'celebrating'.

### Phase 2: Celebrating

Animated celebration with spinning hex ring and confetti.

- **Spin animation**: A wrapper `div` around the HexGrid receives a CSS animation:

  ```css
  animation: winner-spin <duration>ms linear infinite;
  ```

  CSS `transform: rotate()` is purely visual and does not affect layout dimensions.
  HexGrid's `fitToParent` + ResizeObserver continues working correctly because the
  parent's measured dimensions are unaffected by a CSS transform on a child element.

- **Spin speed and alignment**: The total rotation must be an exact multiple of 360 degrees
  so that when the animation ends, all hexes return to their original orientation (text
  upright, avatars correctly positioned). Calculate the number of full rotations that fit
  within `celebrationDurationMs` and use that exact value. Do NOT use `infinite` -- use a
  finite animation with `forwards` fill mode ending at `N * 360deg` where N is a whole
  number (e.g., 3 rotations = 1080deg over 5 seconds).

- **Center hex**: Displays a trophy icon (FontAwesome `faTrophy`). Non-interactive during
  the spin.

- **Confetti**: ~30 CSS-animated particles using a `confetti-burst` keyframe animation.
  Each particle has randomized angle, distance, color, size, and delay via CSS custom
  properties (`--burst-x`, `--burst-y`). Particles repeat throughout the celebration.

- **Transition**: Auto-advances to 'scoring' after `celebrationDurationMs` (default 5000ms)
  via a `setTimeout` in a `useEffect`.

### Phase 3: Scoring

Static hex ring with victory point adjustment controls.

- **Center hex**: "End Game" button with green gradient background. Clickable.
- **Ring hexes**: Each player hex now shows:
  - Avatar circle (smaller, ~32px)
  - VP score (large bold number)
  - "+" and "-" buttons (circular). The "-" button enforces a floor at the player's
    initial `score` value (can't go below what the game already tracked).
- **State**: Local `vpScores` state as `Record<string, number>`, initialized from
  `players.map(p => [p.id, p.score])`.
- **End**: Click "End Game" calls `onEndGame(vpScores)` with the final score map.

**Note on scoring model**: In Catan, only the current player can win -- that's a core
rule. The current player declares the game over and adjusts all scores to account for
hidden VP cards revealed at the table. The game determines the winner from the final
scores after submission. No `winnerId` prop is needed because the current player is
always the winner by rule.

## CSS Animations

Two keyframe animations added to `globals.css`:

```css
/* Continuous rotation for the hex ring during celebration.
   Respects prefers-reduced-motion -- animation is only applied
   when the user has no motion preference. */
@keyframes winner-spin {
  from { transform: rotate(0deg); }
  to { transform: rotate(360deg); }
}

/* Burst outward for confetti particles */
@keyframes confetti-burst {
  0% {
    transform: translate(0, 0) scale(0);
    opacity: 1;
  }
  50% {
    opacity: 1;
  }
  100% {
    transform: translate(var(--burst-x), var(--burst-y)) scale(1) rotate(720deg);
    opacity: 0;
  }
}
```

## Internal Sub-components

All defined as private functions within `WinnerOverlay.tsx`:

| Component | Phase | Purpose |
| --- | --- | --- |
| `WinnerCenterHex` | ready | "Winner!" button with player gradient |
| `CelebrationCenterHex` | celebrating | Trophy icon, non-interactive |
| `EndGameCenterHex` | scoring | "End Game" button, green gradient |
| `PlayerDisplayHex` | ready, celebrating | Avatar circle + player name |
| `PlayerScoringHex` | scoring | Avatar + VP count + "+"/"-" buttons |
| `ConfettiOverlay` | celebrating | ~30 CSS-animated particles |

## Layout Pattern

Uses the same `HexGrid` infrastructure as `SupplementalOverlay`:

- **Coordinate system**: `HEX_LAYOUTS.CLUSTER_7` provides 7 positions -- index 0 is center,
  indices 1-6 are the surrounding ring (N, NE, SE, S, SW, NW).
- **Hex sizing**: `hexSize={50}` (circumradius), `gap={3}`.
- **Responsive**: `fitToParent` scales the grid to fit within the container without
  exceeding it. Never scales up, only down.
- **Hex shape**: `hex-clip-flat` CSS class applies a flat-top hexagonal clip-path.
- **Player colors**: `buildCssGradient(colors)` creates a 135-degree gradient from
  `primary` to `secondary` to auto-computed black/white endpoint.

## Integration Points

### Layout Store Registration

The `winner` panel is registered in `layoutStore.ts` as a `PanelId`, giving it all the
standard FloatingPanel capabilities: drag, resize, minimize, persist position to
localStorage, and z-index management. Default position mirrors the `supplemental` panel
(centered over the board, 320x340, z-index 1000 so it renders on top of all other panels).

The existing `resetLayout()` action replaces all panel state with defaults from
`LANDSCAPE_PANELS` / `PORTRAIT_PANELS`, so the `winner` panel resets correctly as long as
it has entries in both default objects.

### Controls-test Page (immediate)

Rendered inside a `FloatingPanel` with `panelId="winner"`. Triggered by a "Winning!"
toggle button. Uses mock player data already defined on the page. The FloatingPanel
handles positioning, sizing, and persistence automatically.

### Game Page (future)

Will replace the three old winner components. The game page will:

1. Show WinnerOverlay when the user selects "Winner!" from the game menu
2. Pass real player data and colors from the game state
3. Handle `onEndGame` by sending an `EndGame` message to the GameService via SignalR
4. The `EndGame` message will include the VP scores for server-side persistence

### Backend (future)

The `EndGame` message in `GameStateMachine.HandleEndGameAsync` will be updated to:

1. Accept a `Record<string, number>` of player VP adjustments
2. Update each player's victory point count in the game model
3. Add the end-game action to the game log
4. Notify all clients via SignalR with the final game state

## Design Decisions

1. **HexGrid over polar positioning**: Eliminates the pixel-offset bugs from the old
   `WinnerCelebration`. HexGrid handles all coordinate math and responsive scaling.

2. **Single component**: Three phases managed internally instead of three separate
   components. Simpler state management, no inter-component coordination needed.

3. **CSS animations over framer-motion for spin**: The spin is a simple continuous rotation.
   CSS `@keyframes` is lighter weight than framer-motion for this use case. Confetti also
   uses CSS animations to avoid mounting 30+ framer-motion instances.

4. **Score adjustment with floor**: Players can increment and decrement scores, but the
   "-" button enforces a minimum at the player's initial score (what the game already
   tracked). This prevents accidental over-clicks from being unrecoverable while still
   ensuring scores reflect revealed hidden VP cards.

5. **Configurable celebration duration**: Default 5 seconds. Can be adjusted per-game or
   in settings. Passed as a prop, not hardcoded in timers.

6. **Reduced motion support**: The spin and confetti animations respect
   `prefers-reduced-motion`. When the user prefers reduced motion, the celebrating phase
   skips the spin and shows a static celebration instead.

## Files to Create or Modify

| Action | File | Purpose |
| --- | --- | --- |
| MODIFY | `react-ui/lib/stores/layoutStore.ts` | Add `'winner'` to `PanelId` union, panel metadata, defaults |
| MODIFY | `react-ui/app/globals.css` | Add `winner-spin` and `confetti-burst` keyframe animations |
| CREATE | `react-ui/components/game/overlays/WinnerOverlay.tsx` | New unified winner component |
| MODIFY | `react-ui/components/game/index.ts` | Export `WinnerOverlay` and types |
| MODIFY | `react-ui/app/controls-test/page.tsx` | Add "Winning!" button and `FloatingPanel` |

### Old Files (to be removed in a future step)

| File | Status |
| --- | --- |
| `react-ui/components/game/overlays/WinnerDialog.tsx` | Replaced by WinnerOverlay phase 1 |
| `react-ui/components/game/overlays/WinnerCelebration.tsx` | Replaced by WinnerOverlay phase 2 |
| `react-ui/components/game/overlays/VictoryPointsOverlay.tsx` | Replaced by WinnerOverlay phase 3 |

### Key Reference Files (read-only)

| File | Why |
| --- | --- |
| `react-ui/components/game/overlays/SupplementalOverlay.tsx` | Primary pattern to follow |
| `react-ui/components/hex-grid/HexGrid.tsx` | Layout engine used by the overlay |
| `react-ui/components/hex-grid/hex-geometry.ts` | `CLUSTER_7` layout and coordinate math |
| `react-ui/lib/utils/playerColors.ts` | `buildCssGradient()` utility |
