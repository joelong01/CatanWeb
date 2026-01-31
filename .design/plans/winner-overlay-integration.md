# Replace Winner Flow with WinnerOverlay

## Goal

Replace the three old winner components (WinnerDialog, WinnerCelebration,
VictoryPointsOverlay) with the single WinnerOverlay component in the game
page. The prototype is working on controls-test. This is the integration
into the real game page.

## Changes

### 1. `react-ui/app/game/[id]/page.tsx`

**Remove imports (lines 37-39):**

```text
- import { WinnerDialog } from '...WinnerDialog';
- import { WinnerCelebration, type PlayerInfo } from '...WinnerCelebration';
- import { VictoryPointsOverlay, type PlayerVPInfo } from '...VictoryPointsOverlay';
```

**Add import:**

```text
+ import { WinnerOverlay, type WinnerPlayer } from '@/components/game/overlays/WinnerOverlay';
```

**Replace state variables (lines 385-390):**

```text
- const [showWinnerDialog, setShowWinnerDialog] = useState(false);
- const [showWinnerCelebration, setShowWinnerCelebration] = useState(false);
- const [showVictoryPoints, setShowVictoryPoints] = useState(false);
- const [winnerName, setWinnerName] = useState('');
- const [winnerId, setWinnerId] = useState('');
+ const [showWinnerOverlay, setShowWinnerOverlay] = useState(false);
+ const [winnerId, setWinnerId] = useState('');
```

**Replace handler functions (lines 632-677):**

```text
- handleWinner (shows dialog) -> handleWinner (shows overlay directly)
- handleWinnerConfirm (dialog -> celebration)     REMOVE
- handleWinnerCancel (close dialog)               REMOVE
- handleCelebrationComplete (celebration -> VP)   REMOVE
- handleVictoryPointsDone (VP -> proxy call)      REPLACE with handleEndGame
```

New handlers:

```typescript
const handleWinner = useCallback(() => {
  if (!currentPlayer) return;
  setWinnerId(currentPlayer.id);
  setShowWinnerOverlay(true);
}, [currentPlayer]);

const handleEndGame = useCallback(async (vpScores: Record<string, number>) => {
  setShowWinnerOverlay(false);
  try {
    const result = await proxy.declareWinner(winnerId, vpScores);
    if (!result.success) {
      console.error('[GamePage] Failed to declare winner:', result.error);
    }
  } catch (error) {
    console.error('[GamePage] Exception declaring winner:', error);
  }
}, [winnerId, proxy]);
```

**Add winnerPlayers memo** (build WinnerPlayer[] from game state):

```typescript
const winnerPlayers: WinnerPlayer[] = useMemo(() => {
  if (!players) return [];
  return players.map(p => {
    const profile = playerProfiles.get(p.id);
    return {
      id: p.id,
      name: profile?.name || p.name,
      score: p.victoryPoints,
      colors: profile?.colors || DEFAULT_PLAYER_COLORS,
      avatarUrl: profile?.avatarUrl,
    };
  });
}, [players, playerProfiles]);
```

**Update gameActions memo (line 702-707):**

- Remove `handleWinnerConfirm`, `handleWinnerCancel` from deps (they're gone)
- `handleWinner` is the same name, just simpler -- deps update automatically

**Replace JSX rendering (lines 811-858):**

Remove all three `<AnimatePresence>` blocks for WinnerDialog, WinnerCelebration,
VictoryPointsOverlay. Replace with:

```tsx
{showWinnerOverlay && players && (
  <FloatingPanel
    panelId="winner"
    title="Winner!"
    className="bg-white/5 border-white/10"
    minWidth={320}
    minHeight={380}
    alwaysOnTop
  >
    <WinnerOverlay
      players={winnerPlayers}
      currentPlayerColors={playerColors}
      celebrationDurationMs={5000}
      onEndGame={handleEndGame}
    />
  </FloatingPanel>
)}
```

`playerColors` already exists at line 265 as `PlayerColorsWithGradient` (used
by ActionCluster, MeasurementCluster, RollRing). Exact match for the prop type.

**Remove `AnimatePresence` import** (line 40) -- all 3 usages are the
winner components being removed. No other usage in this file.

### 2. `react-ui/lib/stores/uiStore.ts`

**Remove winner dialog state:**

- Remove `isWinnerDialogOpen` property and `setWinnerDialogOpen` action
- Remove from `closeAllOverlays()`
- Remove from `hasAnyOverlay` selector

This state was for the old WinnerDialog and is no longer needed since
WinnerOverlay manages its own phases internally.

### 3. `react-ui/lib/stores/stores.test.ts`

- Remove/update test lines referencing `isWinnerDialogOpen` and
  `setWinnerDialogOpen` to match uiStore changes.

### 4. Delete old components

- `react-ui/components/game/overlays/WinnerDialog.tsx`
- `react-ui/components/game/overlays/WinnerCelebration.tsx`
- `react-ui/components/game/overlays/VictoryPointsOverlay.tsx`

### 5. Update `.design/known-issues.md`

- Move "Winner Flow Refactor" from "Components Being Replaced" to
  "Resolved in Recent Sessions"
- Remove the 3-component replacement table

## Files Modified

| File | Action |
|------|--------|
| `react-ui/app/game/[id]/page.tsx` | Replace 3 components with WinnerOverlay |
| `react-ui/lib/stores/uiStore.ts` | Remove `isWinnerDialogOpen` state |
| `react-ui/lib/stores/stores.test.ts` | Update tests for uiStore change |
| `react-ui/components/game/overlays/WinnerDialog.tsx` | DELETE |
| `react-ui/components/game/overlays/WinnerCelebration.tsx` | DELETE |
| `react-ui/components/game/overlays/VictoryPointsOverlay.tsx` | DELETE |
| `.design/known-issues.md` | Mark winner refactor complete |

## Verification

1. `npx next build` -- confirm no type errors or missing imports
2. Navigate to game page, click "Winner!" in nav menu
3. Phase 1: See "Winner!" center hex with player ring -- click it
4. Phase 2: Spinning celebration with fireworks for 5 seconds
5. Phase 3: VP adjustment with +/- buttons -- click "End Game"
6. Verify `proxy.declareWinner()` is called (check network tab or console)
7. Verify old components are no longer importable
