# Balance Board Design

**Status:** Implemented
**Last Updated:** 2026-01-13

## Overview

The "Balance" button creates a balanced board for faster gameplay. It is only available during
the `PickingBoard` game state (before the game starts).

## Goals

A "balanced" board has three properties:

1. **No Adjacent 6/8** - High-probability tiles (6 and 8) cannot be neighbors (existing rule)
2. **Resource Star Parity** - Each resource type has approximately the same average stars per tile
3. **No Resource Clumps** - Resources are distributed evenly, with different thresholds for edge vs interior tiles

## Definitions

### Stars

Each tile has a star value based on its dice number (probability of being rolled):

| Number | Stars | Probability |
|--------|-------|-------------|
| 2, 12  | 1     | 2.8%        |
| 3, 11  | 2     | 5.6%        |
| 4, 10  | 3     | 8.3%        |
| 5, 9   | 4     | 11.1%       |
| 6, 8   | 5     | 13.9%       |
| 7      | 0     | (desert)    |

### Resource Star Parity

**Average** stars per resource type. Since different resources have different tile counts (e.g., Sheep
has 4 tiles, Ore has 3 tiles in a standard board), we compare the **average stars per tile** for each
resource, not totals.

**Example (19-tile board):**

- Sheep: 4 tiles -> total 12 stars -> average 3.0 stars/tile
- Ore: 3 tiles -> total 9 stars -> average 3.0 stars/tile
- Balanced: All resources have similar average stars per tile
- Acceptable variance: within 0.5 average star difference between highest and lowest

### No Resource Clumps

A tile is "clumped" if too many adjacent tiles share its resource type. The threshold depends on
whether the tile is on the edge or in the interior:

- **Edge tiles** (< 6 neighbors): max 2 same-resource neighbors allowed
- **Interior tiles** (6 neighbors): max 1 same-resource neighbor allowed

This allows some clustering on edges (where there are fewer neighbors) while keeping the interior
well-distributed.

## UI

### Balance Button (NavMenu)

- **Visible:** Only when `GameState == PickingBoard`
- **Hidden:** All other game states
- **Location:** Burger menu (NavMenu.razor)
- **Action:** Calls `BalanceBoardAsync()` via SignalR

### Balance Indicator (BoardMeasurement)

- **Location:** Lower-right corner of the BoardMeasurement panel
- **Appearance:** Gold scales glyph (unicode 2696) when board is balanced
- **Hidden:** When board is not balanced
- **Calculation:** Client-side using shared `ValidateBalance()` and `ValidateNoClumps()` APIs

### Shuffle Button (BoardMeasurement)

- Bordered button with icon and "Shuffle" label
- Centered in the panel
- Calls regular `Shuffle()` (not balance-aware)

## Algorithm

The `BalancedShuffle` algorithm uses a **best-effort approach**:

1. **Phase 1: Search** - Shuffle up to 2000 times, tracking the best valid board found
2. **Phase 2: Convergence** - Try to improve the best board's variance by swapping numbers
3. **Fallback** - Always returns a valid board (no adjacent 6/8, no clumps), even if variance > 0.5

### Phase 1: Search with Best Board Tracking

```csharp
public static (bool Success, int Attempts, string? FailureReason) BalancedShuffle(this GameModel game)
{
    const int maxAttempts = 2000;
    const double tightVariance = 0.5;

    // Track the best valid board found
    double bestVariance = double.MaxValue;
    List<(ResourceType, int)>? bestTileState = null;

    for (attempt = 1; attempt <= maxAttempts; attempt++)
    {
        // Shuffle resources, numbers, harbors...

        // Only consider boards passing basic rules (no adjacent 6/8, no clumps)
        if (game.ValidateGame() && game.ValidateNoClumps())
        {
            var variance = CalculateVariance(game);

            // Found a perfect board - return immediately
            if (variance <= tightVariance)
                return (true, attempt, null);

            // Track the best board found
            if (variance < bestVariance)
            {
                bestVariance = variance;
                bestTileState = SaveBoardState(game);
            }
        }
    }

    // Restore the best board found and try to converge
    RestoreBoardState(game, bestTileState);
    ConvergeVariance(game, tightVariance);

    // Return success with best board (valid even if variance > 0.5)
    return (true, maxAttempts, $"best variance {finalVariance:F2}");
}
```

### Phase 2: Convergence

The `ConvergeVariance` function iteratively improves variance by swapping numbers:

1. Find the resource with **lowest** average stars
2. Find the resource with **highest** average stars
3. Swap a low-starred tile from the low-avg resource with a high-starred tile from the high-avg resource
4. Verify the swap doesn't create adjacent 6/8 - if it does, try other candidates
5. If variance is good but clumps were created, call `TryFixClumps`
6. Repeat until variance <= target or no beneficial swaps possible

### TryFixClumps Helper

If convergence creates resource clumps, `TryFixClumps` attempts to fix them by swapping
**resources** (not numbers) between tiles with the same number. This preserves the star
distribution while breaking up the clump.

## Return Values

`BalancedShuffle` returns `(Success, Attempts, FailureReason)`:

| Success | FailureReason            | Meaning                               |
|---------|--------------------------|---------------------------------------|
| true    | null                     | Perfect board found (variance <= 0.5) |
| true    | "best variance X.XX"     | Valid board, but variance > 0.5       |
| false   | "no valid board found"   | No valid board found (extremely rare) |

**Important:** When `Success == true`, the board is always valid (no adjacent 6/8, no clumps).
The `FailureReason` only indicates whether we achieved the tight variance target.

## Constants

| Constant                   | Value | Description                                                           |
|----------------------------|-------|-----------------------------------------------------------------------|
| `maxAttempts`              | 2000  | Maximum shuffle attempts in Phase 1                                   |
| `tightVariance`            | 0.5   | Target variance (max difference between highest/lowest avg stars)     |
| `maxEdgeSameNeighbors`     | 2     | Max same-resource neighbors for edge tiles (< 6 neighbors)            |
| `maxInteriorSameNeighbors` | 1     | Max same-resource neighbors for interior tiles (6 neighbors)          |
| `maxSwaps`                 | 50    | Max convergence iterations in Phase 2                                 |

## Server-Side Logging

`HandleBalanceBoardAsync` logs one of three outcomes:

```text
# Perfect board found
BalancedShuffle: Found balanced board after {attempts} attempts

# Valid board but variance > 0.5
BalancedShuffle: Using best board after {attempts} attempts (best variance X.XX)

# No valid board found (falls back to regular shuffle)
BalancedShuffle: Failed after {attempts} attempts ({reason}) - using regular shuffle
```

`LogResourceStarTable` logs each line separately for readability:

```text
Resource Star Averages:
  Resource | Tiles | Stars | Avg
  ---------|-------|-------|-----
  Wheat    |     5 |    15 | 3.00
  Ore      |     5 |    14 | 2.80
  Sheep    |     6 |    18 | 3.00
  Wood     |     6 |    17 | 2.83
  Brick    |     6 |    18 | 3.00
  Variance: 0.20 (max 0.5 for balanced)
  Clumps: None
```

## Recording/Replay Support

Uses existing `BalanceBoardRecord` and `BalanceBoardMessage` infrastructure (no new classes needed).

- **Recording:** `GameHub.BalanceBoard()` records `BalanceBoardRecord` after successful operation
- **Replay:** `RecordingController.ExecuteRecordedAction` handles `BalanceBoardRecord` case

## Files Modified

| File                                                | Changes                                                                    |
|-----------------------------------------------------|----------------------------------------------------------------------------|
| `Catan3.Shared/Extensions/GameModelExtensions.cs`   | `ValidateBalance()`, `ValidateNoClumps()`, `BalancedShuffle()`,            |
|                                                     | `ConvergeVariance()`, `TryFixClumps()`, `CalculateVariance()`,             |
|                                                     | `NumberToStars()`                                                          |
| `Catan3.Shared/GameLogic/GameStateMachine.cs`       | `HandleBalanceBoardAsync()`, `LogResourceStarTable()`                      |
| `WebUI/Layout/NavMenu.razor`                        | Balance button visible only during `PickingBoard`, `OnBalance()`           |
| `WebUI/Services/GameConnectionService.cs`           | `BalanceBoardAsync()` wrapper method                                       |
| `WebUI/Components/Board/BoardMeasurement.razor`     | Balance indicator, styled shuffle button                                   |
| `WebUI/Components/Board/BoardMeasurement.razor.css` | Styles for shuffle button and balance indicator                            |

## Edge Cases

1. **Best effort** - Always returns a valid board; variance may exceed 0.5 if perfect board not found
2. **Desert handling** - Desert tiles excluded from clump calculations
3. **Already balanced** - Balance button works regardless; will find a (different) balanced board
4. **Convergence creates clumps** - `TryFixClumps` swaps resources to break clumps
5. **Swap breaks 6/8 rule** - Algorithm tries multiple candidate pairs before giving up

## Architecture Notes

- **GameModel is single source of truth** - Balance indicator uses client-side validation on the same
  GameModel received via SignalR, ensuring consistency
- **Shared validation APIs** - `ValidateBalance()` and `ValidateNoClumps()` are in `Catan3.Shared`,
  usable by both server and client
- **No additional SignalR messages** - Balance result is determined client-side from GameModel state
- **Best board tracking** - Saves/restores tile state (resources + numbers) and harbor types
