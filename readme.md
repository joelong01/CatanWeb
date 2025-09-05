# Catan3 Game System

## Overview

This document outlines the design for a multi-platform Settlers of Catan game system. The game is played with friends in the same room, supporting both desktop and web-based companion experiences. The system is comprised of the following components:

**1. Desktop App (DesktopApp project):** A full-featured WinUI3 game client that serves as the reference implementation. This works as-is and should NOT be changed unless specifically directed. It models the UI and game state, while Resources, Dice, and Development cards are managed with physical pieces.

**2. GameService (Catan3.GameService project):** An ASP.NET Core service that implements game logic using both REST API and SignalR for real-time communication. The service enables players to trigger game actions like "Next", "Undo", "Purchase", etc., from their devices with immediate real-time updates to all connected clients.

**3. Shared Library (Catan3.Shared project):** Contains all shared data structures, game logic, and communication interfaces. This includes GameModel, PlayerModel, GameStateMachine, and the unified GameServiceProxy for client-server communication. The library is used by both the GameService and client applications.

**4. Comprehensive Test Suite:** Multiple test projects ensure game logic correctness and API functionality:

- **Tests/Desktop:** End-to-end UI automation tests for the WinUI3 desktop application
- **Tests/GameService:** Integration and SignalR communication tests using ReplayTest infrastructure  
- **Tests/Shared:** JSON serialization compatibility tests for .NET and JavaScript
- **Tests/Data:** Test scenario files (.catan_test) containing recorded game sessions for replay testing

**5. CLI Tool (Catan3.CLI project):** Command-line interface for debugging and testing. Provides commands for:
- Running live game sessions against GameService (`expansion`, `regular`)
- Testing MVVM object serialization (`test --mvvm-objects`)
- Extracting GameModel data from .catan files (`extract`)

At the stage we are in, DO NOT change the WinUI3 desktop app. That is the "source of truth" for how the game works, even though we are evolving it. We can always reference back to the game to see *WHAT* needs to be done, if not necessarily *HOW* it needs to be done.

You are an expert at C# and ASP.NET. You always write best practice code that is well structured, maintainable, and follows SOLID principles. You are also an expert at writing unit tests and integration tests to ensure the code is robust and reliable. After you make changes, you ensure that the tests run without error or warnings. If there are errors or warnings, you fix them.

As this is initial implementation, sometimes the code is correct and the tests need to be updated to match the code, and sometimes the code needs to be updated to match the tests. You will always ensure that the code and tests are in sync before you finish a task. If you are not sure, you will ask for clarification.

## Communication Architecture

The system uses **SignalR** for real-time bidirectional communication between clients and the GameService, replacing the previous hanging GET pattern:

### **SignalR Hub Communication**

1. **Client Connection:** Clients connect to `/gameHub` and join game-specific groups
2. **Action Execution:** Clients send game actions (Undo, Redo, Next, Purchase, etc.) via SignalR hub methods
3. **Real-time Updates:** GameService immediately broadcasts `GameStateUpdated` events to all clients in the game group
4. **Command Completion:** Clients receive `CommandCompleted` or `CommandFailed` notifications with results

### **GameServiceProxy - Unified Client Interface**

The `GameServiceProxy` class in `Catan3.Shared.Services` provides a unified interface combining:

- **SignalR Hub Methods:** Real-time game actions (Undo, Redo, Next, Roll, Purchase, etc.)
- **REST API Calls:** Game management (CreateGame, GetAvailableGames, GetGame)
- **Event Handling:** GameStateUpdated, CommandCompleted, PlayerPresenceChanged events
- **Connection Management:** Automatic reconnection, proper resource disposal

### **Message Architecture**

Game actions use specific message types instead of generic commands:

- `UndoMessage` → `HandleUndoAsync()`
- `RedoMessage` → `HandleRedoAsync()`
- `NextMessage` → `HandleNextAsync()`
- `ShuffleMessage` → `HandleShuffleAsync()`
- `PurchaseMessage` → `HandlePurchaseAsync()`

This provides compile-time type safety and eliminates string-based action matching.

## Game Lifecycle API Patterns

The system follows a clean separation between game lifecycle management (REST) and real-time gameplay (SignalR):

### **REST APIs - Game Lifecycle Management**

These endpoints handle game creation, loading, and initial bootstrap operations:

#### **Creating New Games**

```http
POST /api/game/new
Content-Type: application/json

{
  "PlayerIds": ["Alice-001", "Bob-002", "Charlie-003"],
  "GameType": "Regular"
}

Response: { "success": true, "gameId": "generated-uuid" }
```

#### **Loading Existing Games**

```http
POST /api/game/load
Content-Type: application/json

{
  "CompressedLog": "base64-encoded-compressed-log-data"
}

Response: { "success": true, "gameId": "original-game-uuid" }
```

```http
POST /api/game/loadmodel  
Content-Type: application/json

{
  "GameModelJson": "{ /* GameModel serialized as JSON string */ }"
}

Response: { "success": true, "gameId": "original-game-uuid" }
```

#### **Key Design Principles**

- **REST returns only `gameId`**: No GameModel data is returned from REST endpoints
- **GameId preservation**: Loading existing games preserves their original GameId
- **No GameId extraction**: Service methods handle GameId internally from game data
- **Bootstrap only**: REST APIs only create/load games, they don't handle gameplay

### **SignalR - Real-time Gameplay**

After bootstrapping with REST, all game interactions use SignalR:

1. **Join Game**: `JoinGame(gameId, playerId)` - Client joins game group
2. **Receive Initial State**: Server sends `GameStateUpdated` with complete GameModel
3. **Send Commands**: `ExecuteGameAction(gameId, playerId, message)` - Typed game actions
4. **Receive Updates**: Server broadcasts `GameStateUpdated` to all players
5. **Command Feedback**: `CommandCompleted` or `CommandFailed` notifications

### **Implementation Pattern**

The clean pattern eliminates complex GameId extraction and tuple returns:

```csharp
// Clean pattern - single method, GameId from returned GameModel
var gameModel = await _gameStateMachineService.CreateNewGameAsync(
    gsm => gsm.HandleNewGameAsync(message));
    
return Ok(new { success = true, gameId = gameModel.GameId });
```

### **Test Pattern**

Tests should follow this flow:

1. Load `.catan_test` file and deserialize JSON
2. Extract GameModel and action stack from test data
3. Use REST API to load game: `POST /api/game/loadmodel`
4. Join game via SignalR using returned `gameId`
5. Execute recorded actions via SignalR
6. Verify game state progression

## Rules 📋

These rules *MUST* be followed for *ALL* requests and no violations of any of these rules should be tolerated.

1. **Command Separators**: When running commands in agent mode, always use ";" as a separator instead of "&&" because using "&&" will cause Copilot to hang when executing PowerShell commands.
2. **WinUI3 Desktop App**: The WinUI3 Desktop app is the main project and it works correctly. It can be analyzed for prior art. It cannot be changed without explicit directions to do so.
3. **Current Work Context**: Before starting any new work session or significant task, update the "Current Work" section with enough context to allow the work to continue seamlessly if a new session is created. Include current objectives, recent changes, pending tasks, and any important decisions or findings.
4. **Task Completion Verification**: Before marking any task as complete, you must ask "is this task complete?" If the answer is yes, then follow rule 3 to update documentation. If not, continue enhancing the tests based on feedback. For example, verifying that shuffle was called and clients were updated is not sufficient - we must also verify that the board actually changed after the shuffle (tiles and harbors should be randomized).
5. **GameState Testing**: Some states exist just to give the players a chance to look at the board and the only action is to click "Next". If we have one of those states, you can simulate the Next action to get us to a state where we can run tests.
6. **Single Source of Truth**: All client state should be encapsulated in the GameModel that the GameStateMachine returns via the hanging GET pattern or by requesting the current game state (`/api/gamestate/{gameId}`). We should not need separate APIs like `/api/players/{gameId}` - all player information, current player, game state, etc. should come from the complete GameModel. The only exception might be for creating a new game.
7. **Catan Font Usage**: The companion web interface MUST use the official Catan font for all game-related icons and symbols. The font file is located at `Assets/Fonts/Catan.ttf` and should be served as a web font at `/fonts/Catan.ttf`. Use Unicode characters from `Layout/CatanFont.cs` for authentic Catan iconography (Settlement: \uE926, City: \uE900, Road: \uE909, Soldier: \uE90E, Knight: \uE930, etc.). This ensures visual consistency with the desktop app and provides the authentic Catan look and feel.
8. **TESTS MUST PASS**: Before marking any task as complete, ensure that all tests pass without errors or warnings. If there are compilation errors or test failures, fix them before proceeding.
9. **COMMENT RULES**: We do not embed comments that give history. Comments are there to tell what it *does*. There can be exceptions when the logic is difficult and we want to explain why something works the way it does - we can add a comment with a date and what the bug was and the fix we are making.
10. **Rule Compliance**: All tests must pass without errors or warnings. If there are any issues, they must be resolved before proceeding with any further work. This includes ensuring that all tests are up-to-date and reflect the current state of the codebase.
11. **SignalR Communication**: Use GameServiceProxy in the Shared project for all client-server communication. It provides unified access to both SignalR hub methods and REST APIs.

### **Commenting Guidelines (for AI-friendly evolution)**

- **Describe what the code does**: Write comments that state the intent and behavior of the code at the point of use. Favor clear names and minimal, high-signal comments.
- **Avoid history/change logs in comments**: Do not record why or how the code changed, past decisions, or historic constraints in code comments. Use PR descriptions and commit messages for history.
- **Avoid constraining future design**: Do not embed architectural choices or preferences as permanent rules in comments unless they are enforced by code/tests. Keep comments free of prescriptive historical rationale that would hinder refactors.
- **Exception for bug fixes**: When fixing surprising or complex logic, add a short, adjacent comment that captures the specific bug symptom/data and the invariant being enforced (what must hold). Keep it concise and factual.
- **Prefer self-documenting code**: Use expressive names, small functions, and types to reduce the need for explanatory comments.

## Current Architecture

### **GameModel - Single Source of Truth**

The `GameModel` class in `Catan3.Shared/Models/GameModel.cs` serves as the single source of truth for all game state:

```csharp
/// <summary>
/// Gets or sets the unique identifier for this game instance.
/// This field supports Rule 7 (Single Source of Truth) by ensuring GameModel contains all game metadata.
/// </summary>
public string GameId { get; set; } = string.Empty;

/// <summary>
/// Gets or sets when this game was created.
/// This field supports Rule 7 (Single Source of Truth) by ensuring GameModel contains all game metadata.
/// </summary>
public DateTime CreatedTime { get; set; } = DateTime.UtcNow;

/// <summary>
/// Gets a list of player names for API compatibility.
/// </summary>
public List<string> GetPlayerNames()
{
    return Players.Select(p => p.Name).ToList();
}
```

### **PlayerModel - Self-Contained Display Logic**

The `PlayerModel` class in `Catan3.Shared/Models/PlayerModel.cs` owns its own display name logic:

```csharp
/// <summary>
/// Gets the display name of the player extracted from the ID.
/// Follows Desktop app pattern: "Joe-001" → "Joe"
/// </summary>
public string Name => ExtractNameFromId(Id);

/// <summary>
/// Extracts display name from player ID following Desktop app pattern.
/// "Joe-001" → "Joe"
/// </summary>
/// <param name="id">The player ID to extract the name from</param>
/// <returns>The extracted display name</returns>
private static string ExtractNameFromId(string id)
{
    if (string.IsNullOrEmpty(id)) return "Unknown";
    
    // Desktop app pattern: "Joe-001" -> "Joe"
    if (id.Contains('-'))
    {
        var parts = id.Split('-');
        if (parts.Length >= 2)
        {
            return parts[0];
        }
    }
    
    return id;
}
```

### **GameStateMachine - Shared Game Logic**

The `GameStateMachine` class in `Catan3.Shared.GameLogic` contains the core game logic and state management. The interface has been refactored for type safety:

```csharp
/// <summary>
/// Interface for game state management operations.
/// Provides clean abstraction between messaging layer and game logic.
/// </summary>
public interface IGameStateMachine
{
    Task<GameModel> HandleUndoAsync(UndoMessage message);
    Task<GameModel> HandleRedoAsync(RedoMessage message);  
    Task<GameModel> HandleNextAsync(NextMessage message);
    Task<GameModel> HandleShuffleAsync(ShuffleMessage message);
    Task<GameModel> HandlePurchaseAsync(PurchaseMessage message);
    // ... additional action handlers
}
```

This replaces the previous generic `ExecuteGameActionAsync(ExecuteGameActionMessage)` with specific typed methods for each action.

### **SignalR Hub Architecture**

The system uses SignalR for real-time client-server communication:

**GameHub** (`Catan3.GameService.Hubs.GameHub`):

- Handles client connections and game group management
- Routes typed messages to appropriate GameStateMachine handlers
- Broadcasts `GameStateUpdated` events to all clients in the game group
- Provides command completion feedback via `CommandCompleted`/`CommandFailed` events

**Message Routing**:

```csharp
GameModel updatedGameModel = message switch
{
    UndoMessage undoMsg => await _gameService.ExecuteActionAsync(gameId, gsm => gsm.HandleUndoAsync(undoMsg)),
    RedoMessage redoMsg => await _gameService.ExecuteActionAsync(gameId, gsm => gsm.HandleRedoAsync(redoMsg)), 
    NextMessage nextMsg => await _gameService.ExecuteActionAsync(gameId, gsm => gsm.HandleNextAsync(nextMsg)),
    // ... other message types
    _ => throw new ArgumentException($"Unknown message type: {messageTypeName}")
};
```

### **API Design**

**Server-Generated GameIds**:

- `/api/game/new` endpoint returns server-generated `gameId` instead of accepting client-provided one
- `/api/game/load` endpoint returns server-generated `gameId` for loaded games
- `/api/game/register` endpoint is deprecated

**New Game Request Models**:

```csharp
/// <summary>
/// Represents a player in a new game request
/// Follows the Desktop app pattern with both Id and Name
/// </summary>
public class PlayerInfo
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    
    public PlayerInfo() { }
    
    public PlayerInfo(string id, string name)
    {
        Id = id;
        Name = name;
    }
}

/// <summary>
/// Request structure for creating a new game
/// Supports both simple string arrays (backward compatibility) and complex player objects
/// </summary>
public class NewGameRequest
{
    public string GameId { get; set; } = "";
    public GameType GameType { get; set; } = GameType.Regular;
    public List<string>? PlayerIds { get; set; }
    public List<PlayerInfo>? Players { get; set; }
    
    public List<string> GetPlayerIds() { /* ... */ }
    public List<string> GetPlayerNames() { /* ... */ }
}
```

### **Communication Flow**

**SignalR Real-time Actions:**

1. Client connects to `/gameHub` and joins game group via `JoinGame(gameId, playerId)`
2. Client sends typed action messages (UndoMessage, NextMessage, etc.) via hub methods
3. GameHub routes message to GameStateMachineService for the specific gameId
4. GameStateMachine processes action and returns updated GameModel
5. GameHub broadcasts `GameStateUpdated(gameModel)` to all clients in the game group
6. GameHub sends `CommandCompleted(commandId, success, message)` to the originating client

**REST API Operations:**

- `POST /api/games` - Create new game
- `GET /api/games/{gameId}` - Get game state  
- `GET /api/companion/games` - List available games
- Game management operations that don't require real-time updates

## Directory Structure

Here is the current layout of the project:

```text
Catan/
├── .github/
│   └── workflows/
├── .vscode/
├── artifacts/
├── bin/
├── Catan3.CLI/
│   ├── Commands/
│   └── Services/
├── Catan3.GameService/
│   ├── Controllers/
│   ├── Design Assets/
│   ├── Factory/
│   ├── Hubs/
│   ├── Models/
│   ├── Properties/
│   ├── Services/
│   ├── Utility/
│   ├── Views/
│   │   ├── Home/
│   │   └── Shared/
│   └── wwwroot/
│       ├── css/
│       ├── diagrams/
│       ├── fonts/
│       ├── js/
│       ├── lib/
│       │   ├── bootstrap/
│       │   ├── jquery/
│       │   ├── jquery-validation/
│       │   └── jquery-validation-unobtrusive/
│       └── mermaid-source/
├── Catan3.Shared/
│   ├── Extensions/
│   ├── GameLogic/          # GameStateMachine moved here  
│   ├── Interfaces/         # IGameStateMachine interface
│   ├── Models/
│   ├── Services/           # GameServiceProxy for client communication
│   ├── TestData/           # TestDataLoader utility class
│   └── Utility/
├── DesktopApp/
│   ├── Assets/
│   │   ├── DefaultPlayers/
│   │   ├── Fonts/
│   │   ├── Harbors/
│   │   ├── ResourceCards/
│   │   ├── SVG/
│   │   ├── Test Files/
│   │   └── Tiles/
│   ├── Buildings/
│   │   └── BuildingViewModel/
│   ├── Controls/
│   ├── Game/
│   │   ├── Game Control/
│   │   ├── GameFactory/
│   │   ├── GameModel/
│   │   ├── GameView/
│   │   └── NewGame/
│   ├── GameState/
│   │   └── GameLog/
│   ├── Harbors/
│   ├── Layout/
│   ├── MainPage/
│   ├── Models/
│   ├── Player/
│   │   └── PlayerSettings/
│   ├── Properties/
│   │   └── PublishProfiles/
│   ├── Resources/
│   ├── Roads/
│   ├── Robber/
│   ├── Rolls/
│   ├── Services/
│   │   └── Companion/
│   │       └── Models/
│   ├── Tests/
│   ├── Themes/
│   ├── Tiles/
│   ├── Utility/
│   └── ValueConverters/
├── Docs/
├── Scripts/
├── Tests/
│   ├── Data/               # Test scenario files (.catan_test)
│   ├── Desktop/            # UI automation tests (Tests.DesktopApp.UI)
│   │   ├── ScriptedTestData/
│   │   └── TestInfra/
│   ├── GameService/        # Integration and SignalR tests
│   │   ├── Companion/
│   │   ├── CompanionUI/
│   │   └── ReplayTests/    # ReplayTest infrastructure
│   └── Shared/            # Serialization compatibility tests
│       └── Serialization/
├── build_worker.ps1
├── build.ps1
├── Catan.sln
├── GlobalSuppressions.cs
├── publish.ps1
├── readme.md
└── test_shared_reference.cs
```
