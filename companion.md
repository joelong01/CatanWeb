# Catan3 Phone Companion Design

## Overview
This document outlines the design for a phone companion app that allows players to control the Catan3 WinUI3 game remotely. 
The companion app will enable players to trigger game actions like "Next", "Undo", "Purchase", etc., from their mobile devices.
The Game is a "Settlers of Catan" style game with a focus on real-time multiplayer gameplay.

**MAJOR ARCHITECTURE UPDATE**: The system is being redesigned to extract the GameController into a dedicated ASP.NET Core service with a web-based mobile companion interface, enabling better separation of concerns, testability, and potential for distributed gameplay.

## Rules 📋

### **Development & Testing Guidelines**
1. **Command Separators**: When running commands in agent mode, always use ";" as a separator instead of "&&" because using "&&" will cause Copilot to hang when executing PowerShell commands.
2. **WinUI3 Desktop App**: The WinUI3 Desktop app is the main project and it works correctly. It can be analyzed for prior art. It cannot be changed without explicit directions to do so.
3. **Test-Driven Documentation**: After we add a test and have verified that the project builds and runs correctly, we will update the companion.md file to reflect the current status of the project and the tests that have been completed.
4. **Current Work Context**: Before starting any new work session or significant task, update the "Current Work" section with enough context to allow the work to continue seamlessly if a new session is created. Include current objectives, recent changes, pending tasks, and any important decisions or findings.
5. **Task Completion Verification**: Before marking any task as complete, you must ask "is this task complete?" If the answer is yes, then follow rule 3 to update documentation. If not, continue enhancing the tests based on feedback. For example, verifying that shuffle was called and clients were updated is not sufficient - we must also verify that the board actually changed after the shuffle (tiles and harbors should be randomized).
6. **GameState Testing**: Some states exist just to give the players a chance to look at the board and the only action is to click "Next".  if we have one of those states, you can simulate the Next action to get us to a state where we can run tests.
7. **Single Source of Truth**: All client state should be encapsulated in the GameModel that the GameStateMachine returns via the hanging GET pattern or by requesting the current game state (`/api/gamestate/{gameId}`). We should not need separate APIs like `/api/players/{gameId}` - all player information, current player, game state, etc. should come from the complete GameModel. The only exception might be for creating a new game.

## Current Work
*This section should be updated at the start of each work session with current context.*

**Current Session Focus**: ✅ **WAITINGFORNEXT PURCHASE TESTING COMPLETE** - Successfully **implemented comprehensive purchase and placement testing** for the core economic gameplay loop.

**WaitingForNext Achievement Summary**:
✅ **Complete Purchase Workflow Testing** - All road, settlement, and city purchase → placement workflows verified
✅ **Real-time Synchronization** - Purchase and placement actions properly notify all connected companion devices
✅ **Resource Management** - Proper handling of resource constraints and insufficient resource scenarios
✅ **Undo/Redo Functionality** - Purchase undo/redo operations tested and working correctly
✅ **Multi-purchase Support** - Multiple purchases in one turn verified and working
✅ **Error Handling** - Invalid purchase types and edge cases handled gracefully
✅ **Placement Validation** - Building and road placement rules enforced correctly

**Test Suite Status - ALL MAJOR PHASES COMPLETE WITH NEW ARCHITECTURE**:

✅ **COMPREHENSIVE TEST COVERAGE ACHIEVED**:

**1. Game Creation & Setup Phase** ✅
- New game creation with player management
- Game state API response structure validation  
- UDP discovery service integration
- Companion interface loading and basic functionality

**2. PickingBoard Phase** ✅  
- Shuffle action (board randomization)
- Balance action (resource balancing)
- Undo/Redo functionality
- Real-time hanging GET notifications
- Multi-client synchronization
- **Updated to use GamePhaseHelper architecture**

**3. RollForOrder Phase** ✅
- WaitingForRollForOrder → FinishedRollOrder transitions
- Custom player order setting via SetPlayerOrderMessage  
- Order preservation through state transitions
- Complete workflow testing from dice rolling simulation to final order
- **Updated to use GamePhaseHelper architecture**

**4. Allocation Phase** ✅
- BeginResourceAllocation → AllocateResourceForward → AllocateResourceReverse → DoneResourceAllocation
- Automated optimal settlement placement (highest star value locations)
- Smart road placement adjacent to settlements
- Player progression in forward then reverse order
- Entitlement consumption tracking (Settlement + Road)
- Resource allocation from second settlement placement
- Final transition to WaitingForRoll state
- **Integrated logic into GamePhaseHelper for reuse**

**5. WaitingForRoll Gameplay Phase** ✅
- **Basic Roll Functionality**: Verified dice rolling API works correctly with specified dice values
- **Resource Distribution**: Tested resource assignment to players based on dice roll numbers
- **Tile Highlighting**: Confirmed correct tiles are highlighted based on roll number
- **Statistics Updates**: Verified both player and game statistics are updated correctly
- **State Transition**: Confirmed roll advances from WaitingForRoll → WaitingForNext
- **Seven Roll Special Case**: Tested seven roll triggers MustMoveRobber state correctly
- **Strategic Resource Testing**: Implemented targeted resource-producing rolls for testing
- **Real-time Integration**: Verified multi-client roll updates and synchronization
- **Updated to use GamePhaseHelper architecture for setup**

**6. WaitingForNext Purchase & Placement Phase** ✅
- **Complete Purchase Workflows**: Road, Settlement, and City purchase → placement → verification
- **Resource Constraint Handling**: Proper validation of insufficient resource scenarios
- **Multiple Purchase Support**: Multiple entitlements can be purchased in one turn
- **Placement Validation**: Building and road placement follows Catan rules correctly
- **Undo/Redo Functionality**: Purchase undo/redo operations work and sync across clients
- **Real-time Synchronization**: All purchase/placement actions notify companion devices immediately
- **Error Handling**: Invalid purchase types and edge cases handled gracefully
- **Turn Completion**: Proper transition from WaitingForNext → next player's WaitingForRoll
- **API Integration**: PurchaseMessage, RoadPurchaseMessage, and BuildingUpgradeMessage all verified

**7. Complex Mechanics Testing** ✅
- **Longest Road Calculation**: Multi-player road building and longest road award mechanics
- **Largest Army Tracking**: Multi-turn knight accumulation with one-knight-per-turn restriction
- **Knight Mechanics**: Knight purchase, robber movement, and largest army competition

**8. API & Real-time Integration** ✅
- Hanging GET pattern for real-time updates
- Multi-client synchronization across all game phases
- Single Source of Truth via GameModel API responses
- Proper JSON serialization and API compatibility
- Error handling and edge case coverage

## Current Session
🎯 **WaitingForNext Purchase Testing**

**Next Phase - Expansion Game Testing**:

🎯 **CURRENT OBJECTIVE: Expansion Game Type Testing** 

**Overview**: With all Regular game phases comprehensively tested, we now focus on testing the **Expansion** game type. Expansion games require 5 players and introduce a unique **PickSupplementalPlayers** phase after WaitingForNext, where players can optionally purchase additional buildings/roads during a supplemental building phase.

**Expansion Game Flow Differences**:
- **5 Players Required**: Expansion games must have exactly 5 players (vs 2-4 for Regular)
- **Larger Board**: 30 tiles instead of 19 tiles for Regular games
- **Standard Phases Identical**: PickingBoard, RollForOrder, Allocation phases work the same
- **New Supplemental Phase**: After WaitingForNext → PickSupplementalPlayers → SupplementalBuild → back to WaitingForRoll
- **Supplemental Build Order**: Players who chose supplemental build in natural game order
- **Extended Gameplay**: Additional building opportunities beyond the standard turn

**Expansion Test Requirements**:

**1. Expansion Game Creation** 🎯
- **5-Player Requirement**: Verify Expansion games require exactly 5 players
- **Board Size Validation**: Confirm 30 tiles vs 19 for Regular games
- **HasSupplementalBuildPhase**: Verify expansion flag is set correctly
- **Game Type Persistence**: Ensure GameType.Expansion is maintained throughout game
- **Standard Phase Compatibility**: PickingBoard, RollForOrder, Allocation work identically

**2. PickSupplementalPlayers Phase** 🎯
- **State Transition**: WaitingForNext → PickSupplementalPlayers for Expansion games
- **Player Choice Mechanism**: Each player can choose to participate in supplemental building
- **No Selection Scenario**: If no players choose supplemental → skip to WaitingForRoll
- **Multiple Selection Scenario**: If 1+ players choose → advance to SupplementalBuild phase
- **Selection Order**: Players make choices in natural game order
- **Real-time Updates**: Choice selections sync across all companion devices

**3. SupplementalBuild Phase** 🎯
- **Build Order**: Players who selected supplemental build in natural game order
- **Purchase Mechanics**: Same as WaitingForNext - roads, settlements, cities available
- **Placement Rules**: Standard Catan placement rules apply
- **Resource Consumption**: Players spend their own resources for supplemental builds
- **Undo/Redo Support**: Supplemental purchases can be undone/redone
- **Turn Progression**: Each supplemental player gets one supplemental turn

**4. Supplemental to Regular Transition** 🎯
- **Completion Logic**: After all supplemental players complete → return to WaitingForRoll
- **Turn Order Restoration**: Return to normal turn order after supplemental phase
- **Game State Consistency**: Proper version incrementing and state management
- **Real-time Sync**: State transitions notify all companion devices

**5. Edge Cases & Error Handling** 🎯
- **Invalid Player Counts**: Verify proper rejection of non-5-player Expansion games
- **Mixed Game Types**: Ensure no interference between Regular and Expansion testing
- **Supplemental Timeouts**: Handle scenarios where supplemental choices are not made
- **Resource Constraints**: Supplemental building still respects resource limitations
- **Network Interruptions**: Robust handling of connection issues during supplemental phases

**Test File Structure Plan**:
```
Tests.GameService\ExpansionGameTests.cs - Main expansion testing
├── Game Creation Tests
│   ├── CreateExpansionGame_With5Players_ShouldSucceed()
│   ├── CreateExpansionGame_WithWrongPlayerCount_ShouldFail()
│   ├── ExpansionGame_ShouldHaveLargerBoard()
│   └── ExpansionGame_StandardPhases_ShouldWorkIdentically()
├── PickSupplementalPlayers Tests
│   ├── PickSupplemental_NoPlayersChoose_ShouldSkipToWaitingForRoll()
│   ├── PickSupplemental_SomePlayersChoose_ShouldAdvanceToSupplementalBuild()
│   ├── PickSupplemental_AllPlayersChoose_ShouldProcessInOrder()
│   └── PickSupplemental_RealTimeUpdates_ShouldNotifyAllClients()
├── SupplementalBuild Tests
│   ├── SupplementalBuild_PurchaseAndPlace_ShouldWorkLikeWaitingForNext()
│   ├── SupplementalBuild_MultiplePlayersInOrder_ShouldProcessCorrectly()
│   ├── SupplementalBuild_UndoRedo_ShouldWork()
│   └── SupplementalBuild_ResourceConstraints_ShouldApply()
└── Integration Tests
    ├── ExpansionGame_CompleteWorkflow_ShouldHandleAllPhases()
    ├── ExpansionGame_RealTimeSync_ShouldWorkAcrossAllPhases()
    └── ExpansionGame_ErrorHandling_ShouldBeRobust()
```

**Success Criteria**:
- Expansion games with 5 players work correctly through all phases
- PickSupplementalPlayers phase properly handles player choices and state transitions
- SupplementalBuild phase provides same purchase/placement functionality as WaitingForNext
- Proper transition back to regular gameplay after supplemental building
- Real-time synchronization works correctly for all new expansion phases
- Edge cases and error conditions are handled gracefully
- GamePhaseHelper can be extended to support Expansion game setup

**Implementation Approach**:
1. Extend GamePhaseHelper to support Expansion game creation with 5 players
2. Create comprehensive test for Expansion game flow through all standard phases
3. Implement PickSupplementalPlayers phase testing with choice scenarios
4. Test SupplementalBuild phase using existing purchase/placement test patterns
5. Verify complete workflow integration and real-time synchronization
6. Test edge cases and error handling specific to Expansion games

This phase will complete testing for both Regular and Expansion game types, ensuring the companion interface handles all Catan3 gameplay scenarios correctly.

---

**Previous Completed Sessions**:

**Session 1**: ✅ Game Creation & Basic API Structure
**Session 2**: ✅ PickingBoard State Testing (Shuffle, Balance, Undo, Redo)  
**Session 3**: ✅ RollForOrder State Testing (Player Order Management)
**Session 4**: ✅ Allocation Phase Testing (Settlement/Road Placement)
**Session 5**: ✅ Test Infrastructure Fixes & API Response Structure
**Session 6**: ✅ WaitingForRoll Gameplay Testing (Dice Rolling, Resource Distribution, Seven Rolls)
**Session 7**: ✅ GamePhaseHelper Architecture Implementation (Code Reduction & Reusability)
**Session 8**: ✅ WaitingForNext Purchase & Placement Testing (Complete Economic Gameplay Loop)