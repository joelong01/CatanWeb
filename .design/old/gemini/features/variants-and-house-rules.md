# Features: Grief Dodgy & House Rules As-Built

**Status:** As-Built
**Source:** `.design/grief-dodgy.md` & `Catan3.Shared/Models/HouseRules.cs`

## 1. "Grief Dodgy" Mode

A specific "House Rule" enabled by default to torment a player named "Dodgy" (or ID matching the target).

### Mechanics
*   **Robber**: When the Robber is moved to a hex adjacent to Dodgy's buildings -> Play "Cheer" SFX.
*   **7 Rolls**: If Dodgy rolls a 7 -> Global notification "Dodgy rolled a 7... typical."
*   **Tracking**: "Times Targeted" stats are highlighted for this player.
*   **Implementation**: Logic exists in `GameStateMachine` checks and React UI event handlers.

## 2. Board Balance

To mitigate RNG (Random Number Generator) frustration:

*   **Pip Balancing**: The "Balance Board" command swaps number tokens to ensure no two high-probability numbers (6/8) touch.
*   **No Clumping**: Logic prevents resource clustering (e.g., 3 Ore hexes touching).
*   **Voting**: In the "PickingBoard" state, players can vote to "Shuffle" or "Accept".

## 3. Supplemental Build Phase

Implemented for 5-6 player games.
*   **Trigger**: End of turn -> `GameStateMachine` checks player count > 4.
*   **Flow**: Enters `PickSupplementalPlayers` state.
*   **Action**: Eligible players can build Buildings/Roads (but not trade or play cards).
