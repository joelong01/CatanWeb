# Game Rules Summary

**Status:** As-Built
**Source:** `GameStateMachine.cs` + `.design/game-play.md`

## Overview

This is a **Hybrid Catan** implementation designed for in-person play with a digital board.

- **Digital**: Board generation, state tracking (roads/cities), scoring, robber constraints.
- **Physical**: Dice rolling, resource cards, development cards, trading.
- **Trust Model**: The app does *not* track player hands. Players must honestly select "Buy Road" only when they have paid the resources to the bank.

## 1. Setup Phase

1. **New Game**: Host selects Game Type (Regular/Expansion) and Players.
2. **Picking Board**: A board is generated. Players can "Shuffle" or "Balance" (logic ensures fair pip distribution) until satisfied.
3. **Roll for Order**: Players roll dice to determine who goes first.
4. **Resource Allocation**:
   - **Forward Pass**: Players 1->N place 1 Settlement + 1 Road.
   - **Reverse Pass**: Players N->1 place 1 Settlement + 1 Road.
   - *Logic*: `GrantAllocationResources` gives free entitlements during this phase.

## 2. Main Turn Loop

1. **WaitingForRoll**:
   - Active player rolls physical dice.
   - Inputs result into "Roll Grid".
   - *Logic*: If 7, transition to `MustMoveRobber`. Else, distribute resources (visual only) and move to `WaitingForNext`.

2. **WaitingForNext (Main Action Phase)**:
   - **Build**: Purchase Roads, Settlements, Cities, Dev Cards.
     - *Constraint*: Must be connected, proper dist, buildable state.
   - **Trade**: Done verbally with physical cards.
   - **Play Dev Cards**: Soldier (Robber), Year of Plenty, Road Building, Monopoly.
   - **Next**: Ends turn.

## 3. Special Mechanics

### The Robber (7s and Knights)

- **Trigger**: Rolling a 7 or playing a Soldier.
- **Discard**: If >7 cards, app enters `TooManyCards` state (honor system discard).
- **Move**: Player selects new hex for Robber.
- **Steal**: Player selects victim (if valid targets exist).

### 5-6 Player Expansion

- **Supplemental Phase**: Between turns, all *other* players get a "Special Build Phase".
- *State Flow*: `WaitingForNext` -> `PickSupplementalPlayers` -> `Supplemental` (Loop) -> `WaitingForRoll`.

### House Rules (Configurable)

- **Gold Tiles**: Adds gold producing hexes.
- **Supplemental Min Players**: Configurable threshold for special build phase.
- **Grief Dodgy**: Special SFX/Rules for specific player named "Dodgy".

## 4. Winning

- **Victory Points**: Tracked automatically (Buildings, Longest Road, Largest Army).
- **VP Cards**: Players manually add VP cards to their count in the app.
- **Goal**: Reach 10 VPs.
- **Declaration**: `DeclareWinnerMessage` terminates the game session.
