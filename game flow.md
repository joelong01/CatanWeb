# Catan Game Flow - State Machine Documentation

This document describes the state machine implementation for the Settlers of Catan game, based on the `GameController.cs` class in the DesktopApp project.

## Game States Overview

The game follows a sequential flow through various states, each representing a specific phase of gameplay. The state machine is implemented in `GameController.cs:560` (NextState method) and uses the `GameState` enum defined in `GameEnums.cs:37`.

## State Flow Diagram

```text
[Uninitialized] → [WaitingForNewGame] → [WaitingForPlayers] → [PickingBoard] → 
[WaitingForRollForOrder] → [FinishedRollOrder] → [BeginResourceAllocation] → 
[AllocateResourceForward] → [AllocateResourceReverse] → [DoneResourceAllocation] → 
[WaitingForRoll] → [WaitingForNext] → [PickSupplementalPlayers] → [Supplemental] → 
{Back to WaitingForRoll for next player}
```

## Detailed State Descriptions

### Setup Phase

#### 1. **Uninitialized** → **WaitingForNewGame**

- Initial state when game controller is created
- No automatic transition defined in NextState()
- Requires external action to proceed

#### 2. **WaitingForPlayers** → **PickingBoard**

- Waiting for all players to join
- Transition: Call NextState()
- Location: `GameController.cs:574`

#### 3. **PickingBoard** → **WaitingForRollForOrder**

- Players can shuffle board layout during this phase
- Board balancing can occur here
- Transition: Call NextState()
- Location: `GameController.cs:577`

#### 4. **WaitingForRollForOrder** → **FinishedRollOrder**

- Players roll dice to determine turn order
- Transition: Call NextState()
- Location: `GameController.cs:580`

#### 5. **FinishedRollOrder** → **BeginResourceAllocation**

- Turn order has been established
- Can reorder players using `SetPlayerOrder()` method
- Transition: Call NextState()
- Location: `GameController.cs:583`

### Resource Allocation Phase

#### 6. **BeginResourceAllocation** → **AllocateResourceForward**

- Grants initial resources (Settlement + Road) to each player
- Transition: Call NextState()
- Location: `GameController.cs:571`

#### 7. **AllocateResourceForward** → **AllocateResourceReverse** or continue forward

- Players place initial settlements and roads in forward order
- If last player places their first settlement → **AllocateResourceReverse**
- Otherwise → continue to next player
- Transition logic: `GameController.cs:585-597`

#### 8. **AllocateResourceReverse** → **DoneResourceAllocation** or continue reverse

- Players place second settlement and road in reverse order
- Grants additional resources for second settlement placement
- If first player places second settlement → **DoneResourceAllocation**
- Otherwise → continue to previous player
- Transition logic: `GameController.cs:598-608`

#### 9. **DoneResourceAllocation** → **WaitingForRoll**

- Resource allocation complete, main game begins
- Sets temporary gold tiles if enabled
- Transition: Call NextState()
- Location: `GameController.cs:610-613`

### Main Game Loop

#### 10. **WaitingForRoll** → **WaitingForNext**

- Player must roll dice to proceed
- NOT controlled by NextState() - controlled by roll UI
- Transition: Player rolls dice via `OnRoll()` method
- Location: `GameController.cs:408` in `OnRoll()`

#### 11. **WaitingForNext** → **PickSupplementalPlayers** or next player

- Player can purchase and play development cards
- If supplemental build phase enabled → **PickSupplementalPlayers**
- Otherwise → next player's **WaitingForRoll**
- Transition logic: `GameController.cs:618-638`

### Supplemental Phase (Optional)

#### 12. **PickSupplementalPlayers** → **Supplemental** or skip

- Determines which players participate in supplemental building
- If players selected → **Supplemental**
- If no players → next player's **WaitingForRoll**
- Transition logic: `GameController.cs:639-697`

#### 13. **Supplemental** → continue **Supplemental** or main game

- Additional building phase for selected players
- Cycles through participating players
- When complete → original player's next turn
- Transition logic: `GameController.cs:698-740`

### Special States

#### **MustMoveRobber**

- Triggered by rolling 7 or playing Soldier card
- NOT controlled by NextState() - requires robber movement
- Returns to previous state after robber is moved
- Transition: Via `MoveRobber()` method
- Previous state stored in `gameModel.PreviousGameState`

#### Other Special States (Currently Ignored)

The following states are defined but have minimal or no implementation in NextState():

- **TooManyCards** - Discard phase when rolling 7
- **MustDestroyCity** - City destruction mechanic
- **PickingRandomGoldTiles** - Gold tile selection
- **HandlePirates** - Pirate-related actions
- **MustMoveMerchant** - Merchant piece movement
- **DestroyRoad** - Road destruction
- **SwapNumbers** - Number token swapping
- **PickDeserter** - Deserter knight selection
- **PlaceDeserterKnight** - Deserter placement
- **UpgradeToMetro** - Metropolis upgrades
- **DisplaceVictimKnight** - Knight displacement
- **TestCheckpoint** - Testing state

## Key Transition Rules

### NextState() Availability

The `AllowNext()` method determines when the Next button is enabled:

- Disabled during: **WaitingForRoll**, **MustMoveRobber**
- Disabled when player has unspent entitlements
- Location: `GameController.cs:501-515`

### State Validation

Each state transition validates the current state using `ThrowIfWrongState()`:

- Ensures valid state before proceeding
- Throws `GameException` for invalid transitions
- Location: `GameController.cs:1142-1149`

### Player Turn Management

- `ChangePlayer(1)` - Move to next player
- `ChangePlayer(-1)` - Move to previous player
- `ChangePlayerTo(playerId)` - Jump to specific player
- Turn changes trigger `UpdateStateOnNextPlayer()` which resets turn-specific data

## Action Triggers

### External Actions (Not NextState())

- **Rolling Dice**: `OnRoll()` → triggers state change from WaitingForRoll
- **Moving Robber**: `MoveRobber()` → exits MustMoveRobber state
- **Purchasing**: `OnPurchase()` → can trigger MustMoveRobber state
- **Building**: `BuildingUpgrade()`, `RoadPurchase()` → no state changes

### Entitlement System

- Players receive entitlements (Settlement, Road, City, Soldier, etc.)
- Must spend entitlements before proceeding to next state
- Soldier and RolledSeven entitlements force robber movement

## Summary

The Catan state machine implements a comprehensive turn-based game flow that handles:

1. **Setup Phase**: Player joining, board setup, turn order, initial placement
2. **Main Game Loop**: Roll → Act → Next Player
3. **Special Mechanics**: Robber movement, supplemental building phases
4. **Validation**: State transition validation and turn management

The implementation closely follows traditional Settlers of Catan rules while providing flexibility for expansions and house rules through additional states and mechanisms.
