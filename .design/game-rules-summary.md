# Game Rules & State Machine Flow

**Last verified:** January 30, 2026

## Hybrid Play Model

This is NOT a fully digital Catan implementation. It's a **hybrid companion app**
for in-person play around a shared screen.

- **Physical**: Dice, resource cards, development cards, trading (verbal)
- **Digital**: Board layout, state tracking (roads/settlements/cities/robber),
  scoring, roll statistics, turn management

The app operates on a **high-trust model** -- it doesn't track individual
player hands or verify resource payments. Players pay the physical bank
and click the purchase button in the app.

## Game Phases Mapped to GameState

### Phase 1: Game Setup

| GameState | What Happens |
|-----------|-------------|
| `WaitingForNewGame` | Initial state. Waiting for game creation. |
| `PickingBoard` | Board is displayed. Players can Shuffle, Balance, or Swap tiles. |

**Available commands:** `ShuffleMessage`, `BalanceBoardMessage`,
`SwapTileResources`, `NextMessage` (to advance)

### Phase 2: Turn Order

| GameState | What Happens |
|-----------|-------------|
| `WaitingForRollForOrder` | Players roll physical dice to determine order. |
| `FinishedRollOrder` | Roll order complete. Someone picks who goes first. |

**Available commands:** `SetPlayerOrderMessage`, `GoFirstMessage`,
`NextMessage`

### Phase 3: Resource Allocation (Initial Placement)

| GameState | What Happens |
|-----------|-------------|
| `BeginResourceAllocation` | Transition state entering allocation. |
| `AllocateResourceForward` | Players 1->N each place a settlement + road. |
| `AllocateResourceReverse` | Players N->1 each place a second settlement + road. |
| `DoneResourceAllocation` | All initial placements complete. |

**Available commands:** `BuildingUpgradeMessage` (place settlement),
`RoadPurchaseMessage` (place road), `NextMessage` (advance to next player)

### Phase 4: Main Gameplay Loop

The core loop repeats for each player's turn:

```
WaitingForRoll → (roll dice) → WaitingForNext → (trade/build) → NextMessage
     ↑                                                              |
     └──────────────── next player's turn ──────────────────────────┘
```

| GameState | What Happens |
|-----------|-------------|
| `WaitingForRoll` | Current player must roll (click roll in app). |
| `WaitingForNext` | After roll: player can trade, build, then click Next. |
| `MustMoveRobber` | Rolled 7 or played Soldier. Must relocate robber. |

**Available commands during WaitingForNext:**

- `PurchaseMessage` (Road, Settlement, City, DevCard, Soldier)
- `RoadPurchaseMessage` (place road on board)
- `BuildingUpgradeMessage` (place settlement or upgrade to city)
- `NextMessage` (end turn)

**Soldier special case:** Purchasing `Soldier` transitions to `MustMoveRobber`
before returning to `WaitingForNext`.

### Phase 5: Supplemental Build (5-6 Player Games)

Between turns, other players can build if house rules allow it.

| GameState | What Happens |
|-----------|-------------|
| `PickSupplementalPlayers` | Choose who participates. |
| `Supplemental` | Each participating player can build. |

**Trigger:** `NextMessage` from `WaitingForNext` when supplemental is
enabled and player count >= `SupplementalMinPlayers` (default 5).

### Phase 6: Victory & End Game

| GameState | What Happens |
|-----------|-------------|
| `GameOver` | Winner declared. Scores finalized. |

**Flow:** Current player clicks "Winner" in menu -> confirms -> celebration
animation -> adjust Victory Point cards -> `DeclareWinnerMessage` sent ->
`GameOver` state.

The `DeclareWinnerMessage` includes a `VictoryPoints` dictionary mapping
player IDs to their hidden VP card counts. The server updates scores and
archives the game.

## House Rules

Configurable per-game via `HouseRules` in `GameModel`:

| Rule | Default | Effect |
|------|---------|--------|
| Gold Tiles | 1 (enabled) | Adds gold tiles to board generation |
| Supplemental Build Phase | Min 5 players | Special build phase between turns |
| Walls Protect Cities | Enabled | Cities & Knights protection |
| Grief Dodgy | Enabled | Special effects targeting "Dodgy" |
| Hide Before Invasion | Disabled | Hides robber until first 7 rolled |
| Knight Moves Before Roll | Knight: enabled | Play knight before rolling |

## Roll & Statistics

- Physical dice rolled, result clicked in the app's Roll Ring
- App tracks every roll with count + percentage per number (2-12)
- Tile dimming: after a roll, non-matching tiles dim for 5 seconds
- Lifetime stats tracked per player: games, wins, longest road,
  largest army, soldiers, stars, times targeted, robber losses
