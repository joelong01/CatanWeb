# Catan3 Phone Companion Design

## Overview
This document outlines the design for a phone companion app that allows players to control the Catan3 WinUI3 game remotely. 
The companion app enables players to trigger game actions like "Next", "Undo", "Purchase", etc., from their mobile devices.
The Game is a "Settlers of Catan" style game with a focus on **real-time multiplayer gameplay** powered by **SignalR** for instant communication.

You are an expert at C# and ASP.NET. You always write best practice code that is well structured, maintainable, and follows SOLID 
principles. You are also expert at writing unit tests and integration tests to ensure the code is robust and reliable.
After you make changes, you ensure that the tests run without error or warnings. If there are errors or warnings you fix them.

The system is comprised of 4 parts:

1. A client application that renders the full game.
2. A web client companion that allows players to control the game when it is their turn.
3. A GameService that implements the game logic and provides both REST API and SignalR interfaces for client interaction. The 
   GameService is implemented as an ASP.NET Core service.
4. A Shared module that contains all the data that is shared between the clients and the service.

At the stage we are in DO NOT change the WinUI3 desktop app. That is the "source of truth" for how the game works, even though 
we are evolving it. We can always reference back to the game to see *WHAT* needs to be done, if not necessarily *HOW* it needs 
to be done.

---
## Rules 📋

These rules *MUST* be followed for *ALL* requests and no violations of any of these rules should be tolerated.

### **Development & Testing Guidelines**
1. **Command Separators**: When running commands in agent mode, always use ";" as a separator instead of "&&" because using 
   "&&" will cause Copilot to hang when executing PowerShell commands.
2. **WinUI3 Desktop App**: The WinUI3 Desktop app is the main project and it works correctly. It can be analyzed for prior art. 
   It cannot be changed without explicit directions to do so.
3. **Test-Driven Documentation**: After we add a test and have verified that the project builds and runs correctly, we will 
   update the companion.md file to reflect the current status of the project and the tests that have been completed.
4. **Current Work Context**: Before starting any new work session or significant task, update the "Current Work" section with 
   enough context to allow the work to continue seamlessly if a new session is created. Include current objectives, recent 
   changes, pending tasks, and any important decisions or findings.
5. **Task Completion Verification**: Before marking any task as complete, you must ask "is this task complete?" If the answer 
   is yes, then follow rule 3 to update documentation. If not, continue enhancing the tests based on feedback. For example, 
   verifying that shuffle was called and clients were updated is not sufficient - we must also verify that the board actually 
   changed after the shuffle (tiles and harbors should be randomized).
6. **GameState Testing**: Some states exist just to give the players a chance to look at the board and the only action is to 
   click "Next". If we have one of those states, you can simulate the Next action to get us to a state where we can run tests.
7. **Single Source of Truth**: All client state should be encapsulated in the GameModel that the GameStateMachine returns via 
   the hanging GET pattern or by requesting the current game state (`/api/gamestate/{gameId}`). We should not need separate APIs 
   like `/api/players/{gameId}` - all player information, current player, game state, etc. should come from the complete 
   GameModel. The only exception might be for creating a new game.
8. **Catan Font Usage**: The companion web interface MUST use the official Catan font for all game-related icons and symbols. 
   The font file is located at `Assets/Fonts/Catan.ttf` and should be served as a web font at `/fonts/Catan.ttf`. Use Unicode 
   characters from `Layout/CatanFont.cs` for authentic Catan iconography (Settlement: \uE926, City: \uE900, Road: \uE909, 
   Soldier: \uE90E, Knight: \uE930, etc.). This ensures visual consistency with the desktop app and provides the authentic 
   Catan look and feel.
9. **🚨 SHARED MODELS DOCUMENTATION REQUIREMENT**: **ANY** and **ALL** changes to the `Catan3.Shared` models **MUST** be 
   fully documented in this companion.md file. This is **CRITICAL** because the WinUI3 Desktop App (Catan3 project) will be 
   updated to use the Shared models, and since we started from the Desktop app implementation, all changes require 
   corresponding updates to the Desktop app. Document changes in a dedicated "Shared Models Changes" section with: (a) What 
   was changed, (b) Why it was changed, (c) Impact on Desktop app, (d) Required Desktop app modifications. This ensures 
   seamless integration and prevents breaking changes.
10. **TESTS MUST PASS**: Before marking any task as complete, ensure that all tests pass without errors or warnings. If there 
    are compilation errors or test failures, fix them before proceeding.
11. **RECCOMENDATIONS**: when asked for recommendations, give me the options and wait for me to decide.  DO NOT IMPLEMENT any
    reccomendation without guidance.
12. **RULES**: follow them.  to not update, delete, or add new rules without explicit instructions.
13. **🚨 SHUFFLE ALGORITHM IS CORRECT**: The shuffle algorithm in GameFactory.Shuffle() is working correctly and must NOT be 
    modified. If shuffle appears to not be working, the issue is in the test logic or the GameHash computation, never in the 
    shuffle algorithm itself. Do not attempt to "fix" the shuffle - fix the test or hash instead.

## 🏗️ **Current Architecture: Comprehensive SignalR Implementation**

**🎯 CURRENT STATE**: The system implements a **comprehensive SignalR architecture** with full MVVM message support and extensive test coverage. The hybrid approach maintains REST for game management while using SignalR for all real-time gameplay.

### **Communication Architecture**:

#### **SignalR-First MVVM Messages** *(Primary/Production)*
1. **Client Connection**: Mobile companion establishes WebSocket connection via SignalR hub (`/gameHub`)
2. **Game Group Joining**: Client joins game-specific group: `JoinGame(gameId, playerId)`
3. **Direct Command Execution**: UI interactions send MVVM messages directly to SignalR hub methods
4. **Synchronous Processing**: Commands processed immediately with real-time response
5. **Real-time Updates**: Game state changes pushed instantly to all clients via `GameStateUpdated(gameModel)`
6. **Command Completion**: Original client receives completion notification via `CommandCompleted(commandId, success, message)`

#### **REST API for Game Management** *(Fallback/Administrative)*
1. **Game Creation**: `POST /api/game/new` with server-generated gameId
2. **Game Discovery**: `GET /api/companion/games` for browser-based discovery
3. **Game Loading**: `POST /api/game/load` for saved games
4. **Legacy Command Processing**: `POST /api/game/action` (deprecated, use SignalR)

### **SignalR Hub Methods (Complete Implementation)**:
- `ExecuteDoAction(gameId, playerId, DoAction)` - Shuffle, Undo, Redo, Next, Balance actions
- `ExecutePurchase(gameId, playerId, PurchaseMessage)` - Entitlement purchases  
- `ExecuteRoadPurchase(gameId, playerId, RoadPurchaseMessage)` - Road placement
- `ExecuteBuildingUpgrade(gameId, playerId, BuildingUpgradeMessage)` - Building placement
- `ExecuteMoveRobber(gameId, playerId, MoveRobberMessage)` - Robber movement
- `ExecuteRoll(gameId, playerId, RollMessage)` - Dice rolls
- `ExecuteSetPlayerOrder(gameId, playerId, SetPlayerOrderMessage)` - Turn order
- `ExecutePlayersDoingSupplemental(gameId, playerId, PlayersDoingSupplemental)` - Supplemental phase
- `ExecuteBalanceBoard(gameId, playerId, BalanceBoardMessage)` - Board balancing (Legacy - use DoAction.Balance)
- `ExecuteGoFirst(gameId, playerId, GoFirstMessage)` - First player selection

### **MVVM Message Objects** *(Shared between all clients)*
All message objects are located in `Catan3.Shared/Models/MessageObjects.cs`:
- `DoAction` - Core game actions (Shuffle, Undo, Redo, Next, Balance)
- `PurchaseMessage` - Entitlement purchases (Road, Settlement, City, Soldier)
- `RoadPurchaseMessage` - Road placement with coordinates
- `BuildingUpgradeMessage` - Building upgrades (Settlement → City)
- `MoveRobberMessage` - Robber movement with target player
- `RollMessage` - Dice roll with TurnRollModel
- `SetPlayerOrderMessage` - Turn order configuration
- `PlayersDoingSupplemental` - Supplemental phase player selection
- `BalanceBoardMessage` - Board balancing commands (Legacy - use DoAction.Balance)
- `GoFirstMessage` - First player selection
- `NewGameMessage` - Game creation
- `LoadGameMessage` - Game loading

### **Key Advantages of SignalR Implementation**:
- **⚡ Ultra-Low Latency**: Direct WebSocket communication (50-90% faster than REST)
- **🔋 Battery Efficient**: Single persistent connection vs multiple HTTP requests
- **🎯 MVVM Consistency**: Same message objects as Desktop app (from Shared project)
- **🔄 Real-time Updates**: Instant bi-directional communication
- **📱 Mobile Optimized**: Connection pooling and automatic reconnection
- **🧪 Comprehensive Testing**: Full test coverage for all game phases

---

## 📡 **Game Discovery and Management**

### **Game Discovery** *(Browser-Based Only)*
- **REST API**: `GET /api/companion/games` - Lists available games
- **Web Interface**: Game selection via companion web interface
- **Browser-Only**: No external discovery protocols - pure web-based architecture

### **Game Lifecycle Management** *(REST Only)*
- `POST /api/game/new` - Create new game (returns gameId)
- `POST /api/game/load` - Load saved game (returns gameId)  
- `GET /api/gamestate/{gameId}` - Get current state (fallback/initial)

---

## 🧪 **Testing Strategy and Constraints**

### **Game State Testing Guidelines**
Due to the complexity of the Desktop app's game state machine, certain constraints apply to testing:

#### **Reliably Testable States**:
- `GameState.PickingBoard` - Default game creation state, supports Shuffle, Balance, Next
- `GameState.WaitingForRollForOrder` - Reachable via single Next from PickingBoard
- `GameState.FinishedRollOrder` - Reachable via two Next actions from PickingBoard

#### **Complex States Requiring Game Progression**:
- `GameState.WaitingForRoll` - Requires completing allocation phases with proper Settlement/Road placement
- `GameState.WaitingForNext` - Requires rolling dice and resource generation
- `GameState.AllocateResourceForward/Reverse` - Requires players to spend unspent entitlements

#### **Key Testing Rules from Desktop App Logic**:
1. **Next Action Blocking**: `AllowNext()` returns false when players have UnspentEntitlements
2. **Allocation Entitlements**: Players receive Settlement + Road entitlements that must be spent
3. **State Machine Complexity**: Many states require specific game progression sequences
4. **Purchase Validation**: Different entitlements have different state requirements (e.g., Soldier only in WaitingForNext/WaitingForRoll)

#### **MVVM Message Architecture Decision**:
**🏗️ Balance Action Integration**: Balance functionality is available via both patterns:
- **Preferred**: `DoAction(GameAction.Balance)` - Consistent with Shuffle, Undo, Redo, Next
- **Legacy**: `BalanceBoardMessage` - Maintained for backward compatibility

**Desktop App Migration Note**: When updating the WinUI3 desktop app, convert the `BalanceBoardMessage` MVVM usage to `DoAction(GameAction.Balance)` to maintain consistency with the SignalR messaging system. The desktop app's `Balance()` command should send `DoAction` instead of `BalanceBoardMessage`.