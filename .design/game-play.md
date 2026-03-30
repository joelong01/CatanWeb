# How Humans Play This Game

**Last verified:** January 30, 2026

## Overview

This is a **hybrid** Settlers of Catan implementation. Players sit
around a physical table with a shared display (monitor, TV, or tablet)
showing the digital board. Physical components mix with digital
tracking.

## Physical vs. Digital

| Physical | Digital |
|----------|---------|
| Dice (rolled by hand) | Board display and tile layout |
| Resource cards (held in hand) | Road, settlement, city placement |
| Development cards (drawn from deck) | Score tracking |
| Verbal trading | Roll statistics and history |
| Table talk and negotiation | Robber position and targeting |
| | Lifetime player statistics |
| | Game recording and replay |

## Trust Model

The app operates on a **high-trust** model. It does not track
individual card holdings or verify that a player has the resources
to build. Players manage their own resource cards physically and
click purchase buttons when they build.

This means:

- No resource card validation on purchases
- No enforcement of trading rules
- No hand-limit checking (7-card discard is honor-system)
- Players can undo/redo freely during their turn

The system tracks **what was built** (roads, settlements, cities,
soldiers, dev cards) but not **what cards are held**.

## Typical Turn Flow

1. **Roll**: Current player rolls physical dice, enters result via
   the roll ring (click or keyboard 2-12)
2. **Collect resources**: All players collect physical resource cards
   based on the roll (the app dims non-matching tiles to help)
3. **Trade**: Players negotiate verbally and exchange physical cards
4. **Build**: Click purchase buttons and place buildings on the board
   - Settlement: click buildable spot (numbered 1-9 for keyboard)
   - Road: click buildable road segment
   - City: upgrade existing settlement
   - Soldier: play knight card (moves robber)
   - Dev Card: purchase development card
5. **Next**: Click Next/End Turn to pass to the next player

## Game Phases

### Board Setup (`PickingBoard`)

Players collectively approve the board before playing:

- **Shuffle**: randomize tile placement
- **Balance**: run the balance algorithm for fair distribution
- **Swap**: manually swap tile resources or numbers
- **Next**: lock in the board and proceed

### Roll for Order (`WaitingForRollForOrder` / `FinishedRollOrder`)

Each player rolls to determine turn order. The app shows who rolled
what and presents a "Go First" overlay for the winning player.

### Resource Allocation (`AllocateResourceForward` / `AllocateResourceReverse`)

Standard Catan initial placement:

1. Forward pass: each player places one settlement + one road
2. Reverse pass: each player places a second settlement + road
3. Second settlement grants initial resources

### Main Game Loop (`WaitingForRoll` / `WaitingForNext`)

The core gameplay cycle. Each turn alternates between:

- `WaitingForRoll`: only the roll ring is active
- `WaitingForNext`: all purchase buttons and board interaction active

### Robber (`MustMoveRobber`)

When a 7 is rolled or a soldier is played:

1. Click a tile to move the robber
2. If valid targets exist, select which player to steal from
3. Robber animates to new position (1.2s CSS transition)

### Supplemental Build Phase (`PickSupplementalPlayers` / `Supplemental`)

Optional house rule for 5-6 player games. After the active player's
turn, other players can build (but not trade). The active player
selects which players participate via overlay.

### Game End (`GameOver`)

Current player declares victory. If any players have purchased
development cards, a Victory Point entry phase allows manual VP
card count input before final scoring. Winner overlay plays a
three-phase animation (crown, celebration, stats).

## House Rules

Configurable via the Settings page or New Game creation:

| Rule | Default | Effect |
|------|---------|--------|
| Gold Tiles | true | Gold hexes produce chosen resource |
| Walls Protect Cities | false | Cities adjacent to walls immune to robber |
| Supplemental Build Phase | false | Non-active players can build between turns |
| Grief Dodgy | true | Special animations targeting player "Dodgy" |

## Statistics

### In-Game Roll Statistics

The app tracks every dice roll and displays:

- Roll frequency histogram
- Expected vs. actual distribution
- Per-resource production totals

### Lifetime Player Statistics

Across all completed games, the app tracks per player:

- Games played and win count
- Total and average score
- Longest road achievements
- Largest army achievements
- Resources produced and lost to robber
- "Misery index" (times targeted by robber)

Statistics persist in SQLite and can be exported/imported via the
Stats API.

## Board Generation

Boards are dynamically generated with configurable tile counts:

- **Regular**: 19 land tiles + surrounding water/harbor ring
- **Expansion**: 30 land tiles for 5-6 players

The balance algorithm ensures fair resource distribution by
minimizing star variance across resource types and preventing
same-resource clumping. See [balance-algorithm.md](balance-algorithm.md)
for algorithm details.
