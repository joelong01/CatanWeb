# End-to-End GameService Test Documentation

## Overview

This document describes the complete flow of the GameService end-to-end tests,
which verify that the multiplayer game service correctly handles game state
synchronization across multiple clients using REST APIs and SignalR real-time
communication.

## Test Architecture

### Key Components

1. **GameServiceProxy** (`Catan3.Shared\Services\GameServiceProxy.cs`) -
   Client-side proxy for both REST and SignalR communication
2. **GameHub** (`Catan3.GameService\Hubs\GameHub.cs`) - SignalR hub for
   real-time game updates
3. **GameApiController** (`Catan3.GameService\Controllers\GameApiController.cs`)
   - REST API endpoints for game lifecycle
4. **GameStateMachineService**
   (`Catan3.GameService\Services\GameStateMachineService.cs`) - Manages
   GameStateMachine instances per game

### Communication Pattern

- **REST API**: Used for game lifecycle operations (create, load, discover)
- **SignalR**: Used for all real-time gameplay commands and state updates
- **Async Pattern**: Clients send commands and wait for SignalR broadcast updates

## Phase 1: Test Data Loading

### 1.1 Finding and Loading the Test File

```csharp
// Load the shared test scenario from embedded resource
var testScenario = await Catan3.Shared.TestData.TestDataLoader.LoadTestScenarioAsync("Expansion.catan_test");
```

### 1.2 Test File Structure

The `.catan_test` file contains:

```json
{
  "gameModel": { /* Complete GameModel object */ },
  "actionStack": [ /* Array of IRecordedMessage objects */ ]
}
```

### 1.3 Data Extraction

```csharp
// Extract player IDs from the initial game model
var playerIds = testScenario.InitialGameModel.Players.Select(p => p.Id).ToArray();
// Example: ["Joe-001", "Dodgy-001", "Doug-001"]

// Set a unique game name for discovery
testScenario.InitialGameModel.GameName = $"Expansion Test {randomSalt}";
```

## Phase 2: Game Setup and Connection

### 2.1 All Players Connect (Real-World Flow)

#### Step 1: All Players Create SignalR Connections

```csharp
// Create connections for ALL players first (like real users would)
var allProxies = new Dictionary<string, GameServiceProxy>();

foreach (var playerId in playerIds)
{
    var playerProxy = new GameServiceProxy(hubUrl, serviceUri, testHandler, playerId);
    await playerProxy.ConnectAsync();
    allProxies[playerId] = playerProxy;
    LogEvent(null, "PlayerConnected", $"Player {playerId} connected to SignalR");
}
```

**SignalR Message**: Connection established to `/gameHub` for each player

**Real-World Scenario**: This mimics how actual users would connect to the game service - all players start the app and connect, waiting for someone to create a game.

### 2.2 One Player Creates/Loads the Game

#### Step 2: First Player Loads GameModel via REST API

```csharp
var firstPlayerProxy = allProxies[firstPlayerId];
var loadResult = await firstPlayerProxy.LoadGameModelAsync(testScenario.InitialGameModel);
var actualGameId = firstPlayerProxy.GameId ?? throw new InvalidOperationException("GameId was not set after loading GameModel");
```

**REST API Call**:

- **Endpoint**: `POST /api/game/loadmodel`
- **Request Body**: `LoadGameModelMessage` containing the complete GameModel (including its original GameId)
- **Response**: `{ success: true, gameId: "original-gameid-from-gamemodel" }`

**Internal Service Flow**:

1. `GameApiController.LoadGameModel()` receives request with GameModel
2. `GameStateMachineService.CreateNewGameAsync()` creates new GameStateMachine instance
3. `GameStateMachine.HandleLoadGameModelAsync()` loads the GameModel with its existing GameId
4. The original GameId from the loaded GameModel is preserved and returned

**Real-World Scenario**: This is like when one friend creates the game and tells everyone else "I created the game, look for 'Expansion Test 1234'"

### 2.3 All Players Discover and Join the Game

#### Step 3: All Players Search for and Join the Game

```csharp
// ALL players (including the one who loaded) now discover and join the game
foreach (var playerId in playerIds)
{
    var playerProxy = allProxies[playerId];
    
    // Each player discovers available games
    var availableGames = await playerProxy.GetAvailableGamesAsync();
    LogEvent(null, "GamesDiscovered", $"Player {playerId} discovered {availableGames.Count} available games");
    
    // Find the test game by DisplayName
    var testGame = availableGames.FirstOrDefault(g => g.DisplayName == testScenario.InitialGameModel.GameName);
    if (testGame == null)
    {
        throw new InvalidOperationException($"Player {playerId} could not find test game '{testScenario.InitialGameModel.GameName}' in available games");
    }
    
    // Join the game via SignalR
    await playerProxy.JoinGameAsync(testGame.GameId);
    LogEvent(null, "PlayerJoined", $"Player {playerId} joined game {testGame.GameId}");
}
```

**REST API Call for Each Player**:

- **Endpoint**: `GET /api/companion/games`
- **Purpose**: Each player independently discovers the available games
- **Critical Test**: This proves game discovery works for all players

**SignalR Hub Method**: `JoinGame(gameId, playerId)` (called for each player)

- Adds each player's connection to SignalR group for the game
- Broadcasts updated GameModel to all clients in group after each join
- Notifies existing players of each new player joining
- **Result**: All players are connected and have synchronized game state

**Real-World Scenario**: This mimics how actual users would find and join a game - they search the games list, find their friend's game by name, and join it.

### 2.4 All Players Successfully Joined

At this point:

- **SignalR Connections**: All players connected to SignalR hub first
- **Game Loading**: One player loaded the GameModel via REST API  
- **Game Discovery**: All players independently discovered the game via REST API
- **Game Joining**: All players joined the same SignalR game group
- **Game State**: Updated to include all players who have joined

### 2.5 Synchronization Verification

```csharp
// Wait for SignalR notifications to propagate to all clients
await Task.Delay(500);

// Verify all proxies have the same GameModel with all players
VerifyAllProxiesHaveSameGameModel(allProxies, testScenario.InitialGameModel.GameState, testScenario.InitialGameModel.GameHash);
```

**Critical Verification**: All connected clients must have identical GameModel state showing all players have successfully joined the game.

## Phase 3: Action Replay

### 3.1 Recorded Action Types

The test replays these action types from the action stack:

| Action Type | SignalR Method | Message Object |
|------------|----------------|----------------|
| ShuffleRecord | ExecuteDoAction | ShuffleMessage |
| UndoRecord | ExecuteDoAction | UndoMessage |
| RedoRecord | ExecuteDoAction | RedoMessage |
| NextRecord | ExecuteDoAction | NextMessage |
| BalanceRecord | ExecuteDoAction | BalanceBoardMessage |
| GoFirstRecord | ExecuteGoFirst | GoFirstMessage |
| PurchaseRecord | ExecutePurchase | PurchaseMessage |
| RollRecord | ExecuteRoll | RollMessage |
| RoadPurchaseRecord | ExecuteRoadPurchase | RoadPurchaseMessage |
| BuildingUpgradeRecord | ExecuteBuildingUpgrade | BuildingUpgradeMessage |
| MoveRobberRecord | ExecuteMoveRobber | MoveRobberMessage |

### 3.2 Replay Loop

```csharp
foreach (var recordedMessage in testScenario.RecordedActions)
{
    // 1. Determine current player from GameModel
    var currentPlayerId = gameModel?.CurrentPlayerId ?? proxies.Keys.First();
    var currentPlayerProxy = proxies[currentPlayerId];
    
    // 2. Execute action based on type
    await ExecuteRecordedAction(allProxies, recordedMessage, actualGameId);
    
    // 3. Wait for SignalR broadcast
    await Task.Delay(100);
    
    // 4. Verify all clients have same state
    if (recordedMessage.ExpectedGameHash != null)
    {
        VerifyAllProxiesHaveSameGameModel(allProxies, expectedHash: recordedMessage.ExpectedGameHash);
    }
}
```

### 3.3 Example Action Execution

#### Shuffle Action

```csharp
case ShuffleRecord shuffle:
    var shuffleResult = await currentPlayerProxy.ExecuteShuffleAsync();
```

**SignalR Flow**:

1. **Client sends**: `connection.InvokeAsync("ExecuteDoAction", gameId, playerId, ShuffleMessage)`
2. **Hub processes**: `GameHub.ExecuteDoAction()` receives message
3. **Service executes**: `GameStateMachineService.ExecuteActionAsync()` → `GameStateMachine.HandleShuffleAsync()`
4. **Broadcast update**: `Clients.Group(gameId).SendAsync("GameStateUpdated", updatedGameModel)`
5. **Command completion**: `Clients.Caller.SendAsync("CommandCompleted", commandId, true, "ShuffleMessage completed")`

## Phase 4: Message Flow Summary

### REST API Endpoints Used

1. `POST /api/game/loadmodel` - Load GameModel to create game
2. `GET /api/companion/games` - Discover available games
3. `POST /api/game/new` - Create new game (alternative flow)
4. `POST /api/game/load` - Load from compressed log (alternative flow)

### SignalR Hub Methods Called

1. `JoinGame(gameId, playerId)` - Join game group
2. `ExecuteDoAction(gameId, playerId, message)` - Execute Undo/Redo/Next/Shuffle/Balance
3. `ExecutePurchase(gameId, playerId, message)` - Purchase entitlements
4. `ExecuteRoadPurchase(gameId, playerId, message)` - Place roads
5. `ExecuteBuildingUpgrade(gameId, playerId, message)` - Upgrade buildings
6. `ExecuteMoveRobber(gameId, playerId, message)` - Move robber
7. `ExecuteRoll(gameId, playerId, message)` - Roll dice
8. `ExecuteGoFirst(gameId, playerId, message)` - Set first player
9. `LeaveGame(gameId, playerId)` - Leave game group

### SignalR Client Events Received

1. `GameStateUpdated(GameModel)` - Broadcast after every game state change
2. `CommandCompleted(commandId, success, message)` - Command execution result
3. `CommandFailed(commandId, error)` - Command execution failure
4. `PlayerPresenceChanged(playerId, isOnline)` - Player join/leave notifications

## Phase 5: Verification Process

### 5.1 State Consistency Check

After each action:

```csharp
VerifyAllProxiesHaveSameGameModel(allProxies)
{
    // Check all proxies have:
    // - Same GameState
    // - Same GameHash (board configuration)
    // - Same GameStateMachineVersion
    // - Same CurrentPlayerId
}
```

### 5.2 Hash Verification

Each recorded action includes an expected GameHash:

```csharp
if (recordedMessage.ExpectedGameHash != null)
{
    // Verify the game board configuration matches expected
    Assert.Equal(recordedMessage.ExpectedGameHash, gameModel.GameHash);
}
```

### 5.3 Complete Test Success Criteria

1. All players successfully connect via SignalR
2. Game is discoverable via REST API
3. All players can join the game
4. All recorded actions execute without errors
5. Game state remains synchronized across all clients
6. Final game state matches expected state

## Test Patterns

### Pattern 1: Async Command Pattern

```text
Client → SignalR Command → Server Processing → Broadcast Update → All Clients Updated
```

### Pattern 2: Game Lifecycle Pattern

```text
REST API (Create/Load) → Get GameId → SignalR Join → SignalR Commands → SignalR Updates
```

### Pattern 3: Multi-Client Synchronization

```text
Player A Action → Server Update → Broadcast to Players A, B, C → All Have Same State
```

## Common Failure Points

### 1. Timeout Issues

- **Symptom**: "ShuffleMessage timed out after 10 seconds"
- **Cause**: SignalR command not receiving CommandCompleted event
- **Location**: `GameServiceProxy.ExecuteCommandAsync()` waiting for completion

### 2. Game Discovery Issues

- **Symptom**: Game not found in available games list
- **Cause**: GameId not properly preserved from loaded GameModel
- **Location**: `GameStateMachineService.GetAvailableGames()`
- **Critical**: The GameId from the loaded GameModel MUST be preserved, not regenerated

### 3. Validation Issues

- **Symptom**: "ValidationVisitor exceeded the maximum configured"
- **Cause**: Complex GameModel exceeds ASP.NET Core validation depth limits
- **Location**: `GameApiController.LoadGameModel()` parameter binding

## Test Execution Flow Diagram

```text
1. Load Expansion.catan_test
   ↓
2. Extract GameModel and Players
   ↓
3. First Player: LoadGameModel (REST) → Returns GameId
   ↓
4. First Player: JoinGame (SignalR) → Receives GameStateUpdated
   ↓
5. Other Players: GetAvailableGames (REST) → Find Game by Name
   ↓
6. Other Players: JoinGame (SignalR) → All Receive GameStateUpdated
   ↓
7. For Each Recorded Action:
   a. Current Player Executes Action (SignalR)
   b. Server Processes and Updates State
   c. All Clients Receive GameStateUpdated
   d. Verify All Have Same GameHash
   ↓
8. Test Complete: All Actions Replayed Successfully
```

## Conclusion

The end-to-end tests verify that the GameService correctly handles multiplayer game synchronization using a hybrid REST/SignalR architecture. The tests ensure that game state remains consistent across all connected clients through a complete game replay sequence.
