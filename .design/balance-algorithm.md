# Board Balance Algorithm

**Last verified:** January 30, 2026

## Overview

The balance algorithm creates fair game boards during the
`PickingBoard` state. It runs when the player clicks the Balance
button and aims to produce boards where no single resource type has
a significant probability advantage over others.

**Entry point:** `GameStateMachine.HandleBalanceBoardAsync()` calls
`gameModel.BalancedShuffle()` in `GameModelExtensions.cs`.

## Star System

Each number token has a probability rating measured in "stars":

| Number | Stars | Probability |
|--------|-------|-------------|
| 2, 12 | 1 | 1/36 each |
| 3, 11 | 2 | 2/36 each |
| 4, 10 | 3 | 3/36 each |
| 5, 9 | 4 | 4/36 each |
| 6, 8 | 5 | 5/36 each |
| 7 | 0 | (robber) |

**Implementation:** `TileModelExtensions.Stars()` and
`NumberToStars()` in `GameModelExtensions.cs`.

## Balance Criteria

A board is considered balanced when all three conditions are met:

### 1. No Adjacent 6/8 Tiles

Tiles numbered 6 or 8 (highest probability) must not be neighbors.
Checked by `ValidateGame()`.

### 2. Resource Star Parity

Each resource type's average stars-per-tile must be within a target
variance:

```
For each resource (Wheat, Wood, Sheep, Brick, Ore):
  avg_stars = sum(stars of tiles with this resource) / count(tiles)

variance = max(avg_stars) - min(avg_stars)
target: variance <= 0.5
```

A variance of 0.5 means the richest resource averages at most half
a star more per tile than the poorest.

### 3. No Resource Clumping

Same-resource tiles must not cluster:

| Tile Position | Max Same-Resource Neighbors |
|---------------|-----------------------------|
| Edge (< 6 neighbors) | 2 |
| Interior (6 neighbors) | 1 |

Checked by `ValidateNoClumps()`. Desert tiles are excluded.

## Algorithm

Two-phase approach implemented in `BalancedShuffle()`:

### Phase 1: Random Search (up to 2000 attempts)

```
bestVariance = infinity
bestBoard = null

for attempt in 1..2000:
    shuffle tiles (resources)
    shuffle numbers
    shuffle harbors

    if not ValidateGame():     # adjacent 6/8 check
        continue
    if not ValidateNoClumps(): # clumping check
        continue

    variance = CalculateVariance()
    if variance < bestVariance:
        bestVariance = variance
        bestBoard = snapshot

    if variance <= 0.5:
        return success
```

### Phase 2: Convergence (up to 50 swaps)

If Phase 1 finds a valid board but with variance > 0.5:

```
restore bestBoard from Phase 1

for swap in 1..50:
    sort resources by average stars

    if variance good but clumps exist:
        TryFixClumps()  # swap resources (not numbers)
        continue

    identify lowest-avg and highest-avg resources
    find candidate tiles from each
    swap their number tokens

    if ValidateGame() and improved:
        keep swap
    else:
        revert swap
```

**`TryFixClumps()`** fixes clumping by swapping resources between
tiles that share the same number but have different resources.

### Return Value

The algorithm always returns a valid board. The return indicates:
- Whether the target variance (0.5) was achieved
- The actual variance of the returned board
- The best board is used even if convergence is incomplete

## Constants

| Constant | Value | Location |
|----------|-------|----------|
| `maxAttempts` | 2000 | `GameModelExtensions.cs:873` |
| `tightVariance` | 0.5 | `GameModelExtensions.cs:874` |
| `maxSwaps` | 50 | `ConvergeVariance` parameter |
| `maxEdgeSameNeighbors` | 2 | `ValidateNoClumps` |
| `maxInteriorSameNeighbors` | 1 | `ValidateNoClumps` |

## Server Logging

After balancing, `LogResourceStarTable()` outputs a per-resource
breakdown showing tile count, total stars, and average stars for
each resource type. This helps verify balance quality during
development.

## UI Integration

- Balance button visible only during `PickingBoard` state
- Located in the MeasurementCluster alongside Shuffle and Swap
- After balancing, the board updates via normal `GameStateUpdated`
  SignalR broadcast

## Unused Code

`GameStateMachine.BalanceBoard()` (lines 802-843) is an older
implementation that swaps individual resource types. It is never
called and is superseded by `BalancedShuffle()`.
