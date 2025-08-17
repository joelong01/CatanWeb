# Catan3 Phone Companion Design

## Overview

This document outlines the design for an application that is used to play Settlers of Catan. The game is played with friends in the same room, so we don't need to worry about security. The game is comprised of the following parts:

**1. a "Desktop App" (in the DesktopApp project):** This is currently a full end-to-end game that we DO NOT CHANGE unless specifically told to do so. It works as is, so if there is ever an issue as we implement the other parts, we can refer to the desktop app to see how the game should work. Note that we model the UI and collect statistics, but we do not model Resources, Dice, or Development cards. These are all managed with physical pieces.

In Development, we have:

**2. a GameService (in the GameService project):** This is an ASP.NET Core service that implements the game logic and provides a REST API for the client to interact with the game. The GameService is implemented as an ASP.NET Core service. The companion app will enable players to trigger game actions like "Next", "Undo", "Purchase", etc., from their mobile devices. The Game is a "Settlers of Catan" style game with a focus on real-time multiplayer gameplay.

**3. a Shared module (in the Shared project):** This contains all the data that is shared between the clients and the service. This includes the GameModel, PlayerModel, and other data structures that are used by the rest of the project. The Shared module is a .NET Standard library that can be used by both the GameService and the client applications.

**4. a comprehensive set of tests (in the Tests project):** This includes unit tests, integration tests, and end-to-end tests that ensure the game logic works correctly and that the REST API is functioning as expected. The Game tests are designed to verify the functionality of the game. Since the game is stateful, the tests are stateful (the exception is the shared project, which makes sure that all JSON serialization for both .NET and JavaScript works correctly). The tests are designed to be run in a continuous integration environment, and they are designed to be run in parallel.

**5. a CLI that will create a game and properly transition to the WaitingForRoll state:** The use case is to debug the real service with a client that efficiently gets the game to a debuggable state.

At the stage we are in, DO NOT change the WinUI3 desktop app. That is the "source of truth" for how the game works, even though we are evolving it. We can always reference back to the game to see *WHAT* needs to be done, if not necessarily *HOW* it needs to be done.

You are an expert at C# and ASP.NET. You always write best practice code that is well structured, maintainable, and follows SOLID principles. You are also an expert at writing unit tests and integration tests to ensure the code is robust and reliable. After you make changes, you ensure that the tests run without error or warnings. If there are errors or warnings, you fix them.

As this is initial implementation, sometimes the code is correct and the tests need to be updated to match the code, and sometimes the code needs to be updated to match the tests. You will always ensure that the code and tests are in sync before you finish a task. If you are not sure, you will ask for clarification.

The way the client works is there is a thread that is in an infinite loop until the game is over (e.g., GameState == GameState.GameOver). In the loop, it calls to the GameService to the hanging GET. When the GameModel changes, the call returns and the GameModel is then "marshalled" to the UI thread which updates the game. Our tests should spawn a thread to make the hanging GET and then continue on their main thread, waiting on a TASK signaled by the spawn thread when the GameModel is updated to simulate the behavior.

The synchronization functionality works this way:

1. Client has a thread that has a hanging GET. This is in an infinite loop, terminating when the GameState reaches GameOver. If the GET times out, it just loops to call the GET again.
2. The main UI thread on the client can interact with the service that causes the GameModel to be updated.
3. On ANY change to the GameModel (including Undo or Redo), the hanging GET for all clients completes and this GameModel is passed back to the client.
4. The thread that executes the hanging GET passes the new GameModel to the main UI thread, which then does whatever it does (in the Desktop app, it forces a UI update).
5. The worker thread loops to do another hanging GET.

This is the pattern our tests should be following.

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
11. **SignalR**: Use the proxy in the Shared project to call SignalR.

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

### **GameStateMachine - Server-Generated GameIds**

The `GameStateMachine` class in `Catan3.GameService/Controllers/GameStateMachine.cs` generates its own GameId and manages client notifications:

```csharp
/// <summary>
/// Server-generated unique identifier for this game instance.
/// Generated in constructor to ensure GameId is always available for all GameModels.
/// </summary>
public string GameId { get; private set; }

public GameStateMachine(IPersistenceService? PersistenceService, IClientNotification clientNotification, string localSaveFile)
{
    // Generate server-side GameId to ensure it's available for all GameModels
    GameId = Guid.NewGuid().ToString();
    
    _clientNotification = clientNotification;
    Log = new Log<string>(PersistenceService, localSaveFile);
    MyPersistenceService = PersistenceService;
}
```

### **Client Notification Architecture**

The system uses a separation of concerns approach for real-time updates:

**IClientNotification Interface**:

```csharp
public interface IClientNotification
{
    Task NotifyAsync(string gameId, GameModel gameModel);
    Task<GameModel> WaitForNotificationAsync(string gameId, string clientId, int currentVersion, CancellationToken cancellationToken);
}
```

**ClientNotificationService**:

- Maintains `ConcurrentDictionary<string, ClientManager>` where key = gameId
- Each `ClientManager` handles multiple clients waiting for updates for that specific game
- When `NotifyAsync()` is called:
  1. Serializes GameModel to JSON
  2. Stores JSON in ClientManager
  3. Signals all waiting threads for that gameId
  4. Each waiting thread copies JSON to response and completes the HTTP request

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

### **Protocol Flow**

1. Client sends request to GameService containing the GameId
2. GameService looks up the GameId in its HashMap (GameStateMachineService)
   - If not found, returns HTTP 404
   - If found, dispatches the request to the correct GameStateMachine
3. GameStateMachine **always** returns a GameModel (focus on game state only)
4. GameService uses the GameModel to trigger the hanging GET notification system to update all clients with the new game model

## Directory Structure

Here is the current layout of the project:

```
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
│   ├── Models/
│   ├── Services/
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
├── Tests.DesktopApp.UI/
│   └── TestInfra/
├── Tests.GameService/
│   ├── Companion/
│   ├── CompanionUI/
│   ├── SignalR/
│   └── TestClient/
│       ├── Commands/
│       └── Services/
├── Tests.Shared/
│   └── Serialization/
├── build_worker.ps1
├── build.ps1
├── Catan.sln
├── GlobalSuppressions.cs
├── publish.ps1
├── readme.md
└── test_shared_reference.cs
```
