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

**Current Session Focus**: ✅ **ARCHITECTURE IMPROVEMENT COMPLETE** - Successfully **implemented GamePhaseHelper architecture** to eliminate code duplication and dramatically simplify test creation across all game phases.

**Architecture Achievement Summary**:
✅ **GamePhaseHelper Implementation** - Created comprehensive static helper class that eliminates 50+ lines of duplicate setup code from each test file
✅ **Code Reduction** - Reduced complex game setup from multiple methods to single line calls like `await GamePhaseHelper.CreateGameInWaitingForRollState(_client)`
✅ **Centralized Logic** - All phase transitions, settlement placement, and road placement logic now centralized and reusable
✅ **Test Focus** - Tests can now focus purely on what they're testing rather than setup boilerplate

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

**6. API & Real-time Integration** ✅
- Hanging GET pattern for real-time updates
- Multi-client synchronization across all game phases
- Single Source of Truth via GameModel API responses
- Proper JSON serialization and API compatibility
- Error handling and edge case coverage

**Technical Implementation Complete**:
✅ **Production-Ready Architecture**:
- ASP.NET Core service with proper dependency injection
- Thread-safe GameStateMachineService with concurrent game support
- Configurable timeouts for testing vs production
- Comprehensive error handling and validation
- Real-time notification system via hanging GET pattern
- Mobile companion interface with complete game control
- **GamePhaseHelper static class for clean, reusable test setup**

**GamePhaseHelper Available Methods**:
```csharp
// Game Creation
GamePhaseHelper.CreateGame(client, gameId?, playerIds?)
GamePhaseHelper.CreateGameInPickingBoardState(client)
GamePhaseHelper.CreateGameInWaitingForRollForOrderState(client)
GamePhaseHelper.CreateGameInBeginResourceAllocationState(client)  
GamePhaseHelper.CreateGameInWaitingForRollState(client)

// Phase Transitions
GamePhaseHelper.HandlePickingBoard(client, gameId)
GamePhaseHelper.HandleRollForOrderPhase(client, gameId, customOrder?)
GamePhaseHelper.HandleAllocationPhase(client, gameId, playerIds?)
GamePhaseHelper.HandleResourceAllocationCompletion(client, gameId)

// Actions
GamePhaseHelper.ExecuteGameAction(client, gameId, action, playerId?)
GamePhaseHelper.ExecuteKnightAction(client, gameId, playerId?)
```

**Next Phase - WaitingForNext Purchase Testing**:

🎯 **CURRENT OBJECTIVE: WaitingForNext State Testing** 

**Overview**: With all setup phases complete and the GamePhaseHelper architecture implemented, we now focus on comprehensive testing of the **WaitingForNext** state. This state represents the main purchasing and building phase where players use their accumulated resources to buy roads, settlements, and cities, forming the core economic gameplay loop.

**WaitingForNext Test Requirements**:

**1. Purchase Infrastructure** 🏗️
- **Purchase API Testing**: Verify PurchaseMessage works for all entitlement types
- **Resource Requirements**: Test that purchases properly consume required resources
- **Entitlement Granting**: Verify purchased entitlements are properly granted to players
- **Invalid Purchase Prevention**: Ensure players cannot buy what they cannot afford
- **Purchase Transaction Atomicity**: Verify purchases either fully succeed or fully fail

**2. Road Purchase & Placement** 🛤️
- **Road Purchase Success**: Test buying roads with sufficient resources (wood + brick)
- **Road Placement Validation**: Verify roads can only be placed adjacent to existing roads/settlements
- **Invalid Road Placement**: Test roads cannot be placed in invalid locations
- **Road Connectivity Rules**: Ensure road placement follows connectivity requirements
- **Multiple Road Purchases**: Test buying and placing multiple roads in one turn

**3. Settlement Purchase & Placement** 🏘️
- **Settlement Purchase Success**: Test buying settlements with sufficient resources (wood + brick + sheep + wheat)
- **Settlement Placement Rules**: Verify settlements must be 2+ spaces apart from other settlements
- **Settlement-Road Connectivity**: Ensure settlements can only be placed adjacent to player's roads
- **Invalid Settlement Placement**: Test rejection of settlements in invalid locations
- **Settlement Upgrade Preparation**: Verify settlements can later be upgraded to cities

**4. City Purchase & Placement** 🏙️
- **City Upgrade Success**: Test upgrading settlements to cities with sufficient resources (ore + wheat x3)
- **City Upgrade Rules**: Verify only player's own settlements can be upgraded
- **City Resource Production**: Confirm cities produce 2 resources vs settlement's 1 resource
- **City Point Value**: Verify cities are worth 2 victory points vs settlement's 1 point
- **Invalid City Upgrades**: Test rejection of city upgrades in invalid scenarios

**5. Undo/Redo in Purchase Phase** 🔄
- **Purchase Undo**: Verify purchases can be undone, restoring resources and removing buildings
- **Multi-Purchase Undo**: Test undoing sequences of multiple purchases
- **Purchase Redo**: Verify redoing purchases after undo works correctly
- **Undo/Redo State Consistency**: Ensure game state remains consistent through undo/redo cycles
- **Undo/Redo Real-time Sync**: Verify undo/redo operations sync across all companion devices

**6. Turn Completion & State Transitions** 🔄
- **Next Action**: Test completing turn with Next action advances to next player's WaitingForRoll
- **Turn Cycling**: Verify turns cycle correctly through all players
- **Score Updates**: Ensure victory point scores update correctly after purchases
- **Action Flag Updates**: Verify action flags properly reflect available actions after purchases
- **Game State Progression**: Confirm proper game state transitions after turn completion

**7. Resource Management** 💰
- **Resource Consumption**: Verify purchases properly deduct required resources
- **Insufficient Resources**: Test purchase rejection when player lacks required resources
- **Resource Display**: Ensure resource counts are properly updated in companion interface
- **Resource Validation**: Verify resource requirements match Catan rule specifications
- **Edge Case Handling**: Test scenarios with exactly sufficient resources

**8. Real-time Integration** 📱
- **Purchase Synchronization**: Verify all purchases are reflected in real-time across companion devices
- **Building Placement Updates**: Ensure building/road placements sync immediately to all clients
- **Resource Updates**: Verify resource changes are synchronized across all companion devices
- **Turn Progression Sync**: Ensure turn advancement is reflected across all connected companions
- **Action Availability Sync**: Verify action button states sync correctly across devices

**Test File Structure Plan**:
```
Tests.GameService\WaitingForNextTests.cs - Main purchase testing
├── Core Purchase Tests
│   ├── Purchase_Road_ShouldConsumeResourcesAndGrantEntitlement()
│   ├── Purchase_Settlement_ShouldConsumeResourcesAndGrantEntitlement()  
│   ├── Purchase_City_ShouldConsumeResourcesAndGrantEntitlement()
│   └── Purchase_InsufficientResources_ShouldFail()
├── Placement Validation Tests
│   ├── RoadPlacement_ValidLocation_ShouldSucceed()
│   ├── RoadPlacement_InvalidLocation_ShouldFail()
│   ├── SettlementPlacement_ValidLocation_ShouldSucceed()
│   ├── SettlementPlacement_InvalidLocation_ShouldFail()
│   ├── CityUpgrade_ValidSettlement_ShouldSucceed()
│   └── CityUpgrade_InvalidLocation_ShouldFail()
├── Undo/Redo Tests  
│   ├── Purchase_UndoSingle_ShouldRestoreResourcesAndRemoveBuilding()
│   ├── Purchase_UndoMultiple_ShouldRestoreAllPurchases()
│   ├── Purchase_RedoAfterUndo_ShouldReapplyPurchases()
│   └── UndoRedo_RealTimeSync_ShouldUpdateAllClients()
├── Turn Management Tests
│   ├── TurnCompletion_NextAction_ShouldAdvanceToNextPlayer()
│   ├── TurnCycling_AllPlayers_ShouldMaintainCorrectOrder()
│   └── ScoreUpdates_AfterPurchases_ShouldReflectCorrectPoints()
└── Real-time Integration Tests
    ├── Purchase_ShouldNotifyAllClients()
    ├── BuildingPlacement_ShouldSyncAcrossCompanions()
    └── ResourceUpdates_ShouldSyncInRealTime()

Tests.GameService\PurchaseValidationTests.cs - Extended validation testing
├── Resource Requirement Tests
├── Building Placement Rule Tests  
├── Edge Case Tests
└── Error Handling Tests
```

**Success Criteria**:
- All WaitingForNext purchase mechanics work correctly via companion interface API
- Resource consumption and building placement follows Catan rules precisely
- Undo/Redo functionality works flawlessly for all purchase scenarios
- Real-time synchronization maintains consistency across multiple companion devices
- Building placement validation prevents invalid game states
- Turn progression works correctly through multiple players
- Performance meets real-time gameplay requirements

**Exclusions for This Phase**:
- **Longest Road Calculation**: Will be tested separately due to complexity requiring multiple player coordination
- **Largest Army Tracking**: Will be tested separately as it requires multiple turns due to one-knight-per-turn restriction
- **Advanced Knight Mechanics**: Covered in WaitingForRoll phase testing
- **Development Card System**: Will be separate test phase if implemented

**Implementation Approach**:
1. Use GamePhaseHelper for all test setup - getting to WaitingForNext should be simple
2. Focus on core purchase-place-undo-redo cycle testing  
3. Verify companion interface responsiveness for all purchase operations
4. Ensure real-time updates work seamlessly for building/resource changes
5. Test edge cases and error conditions thoroughly

This phase will complete the core economic gameplay loop testing, ensuring the companion interface handles the primary building and purchasing mechanics that form the foundation of Catan gameplay.

---

**Previous Completed Sessions**:

**Session 1**: ✅ Game Creation & Basic API Structure
**Session 2**: ✅ PickingBoard State Testing (Shuffle, Balance, Undo, Redo)  
**Session 3**: ✅ RollForOrder State Testing (Player Order Management)
**Session 4**: ✅ Allocation Phase Testing (Settlement/Road Placement)
**Session 5**: ✅ Test Infrastructure Fixes & API Response Structure
**Session 6**: ✅ WaitingForRoll Gameplay Testing (Dice Rolling, Resource Distribution, Seven Rolls)
**Session 7**: ✅ GamePhaseHelper Architecture Implementation (Code Reduction & Reusability)

**Current Session**: 🎯 **WaitingForNext Purchase Testing**