# Catan3 Phone Companion Design

## Overview
This document outlines the design for an application that is used to play Settlers of Catan.  The game is played with friends in the same room, so we don't need to worry about security.  The game is comprised
of the following parts:

**1. a "Desktop App" (in the DesktopApp project): this is currently a full end to end game that we DO NOT CHANGE.  it works as is, so if there is ever an issue as we implement the other parts, we can refer to 
   the desktop app to see how the game should work.  Note that we model the UI and collect statistics, but we do not model Resources, Dice, or Development cards.  These are all managed with physical pieces.

In Development, we have:

**2. a GameService (in the GameService project): this is an ASP.NET Core service that implements the game logic and provides a REST API for the client to interact with the game.  The GameService is implemented 
as an ASP.NET Core service. phone companion app that allows players to control the Catan3 WinUI3 game remotely. The companion app will enable players to trigger game actions like "Next", "Undo", "Purchase", etc., 
from their mobile devices.The Game is a "Settlers of Catan" style game with a focus on real-time multiplayer gameplay.

**3. a Shared module (in the Shared project): this contains all the data that is shared between the clients and the service.  This includes the GameModel, PlayerModel, and other data structures that are used by 
the rest of the project.  The Shared module is a .NET Standard library that can be used by both the GameService and the client applications.

**4: a comprehensive set of tests (in the Tests project): this includes unit tests, integration tests, and end-to-end tests that ensure the game logic works correctly and that the REST API is functioning as 
expected.  The Game tests are designed to verify the functionality of the game. since the game is stateful, the tests are stateful (the exception is the shared project, which makes sure that all JSON serialization
for both .net and javascript works correctly).  The tests are designed to be run in a continuous integration environment, and they are designed to be run in parallel. 

**5: a CLI that will create a game and properly transition to the WaitingForRoll state. The use case is to debug the real service with a client that effeciently gets the game to a debuggable state.


At the stage we are in DO NOT change the winui3 desktop app. that is the "source of truth" for how the game works, eventhough we are evolving it.  We can always reference back to the game to see *WHAT* needs be done, if not necessarily *HOW* it needs to be done.


You are an expert at C# and ASP.Net.  You always write best practice code that is well structured, maintainable, and follows SOLID principles. You are also an expert at writing unit tests and integration tests to ensure the code is robust and reliable.
After you make changes, you ensure that the tests run without error or warnings.  If there are errors or warnings you fix them.

As this is initial implementation, sometimes the code is correct and the tests need to be update to match the code, and sometimes the code needs to be updated to match the tests.  You will always ensure that the code and tests are in sync before you finish a task.  
If you are not sure, you will ask for clarification.

the way the client works is there is a thread that is in an infinite loop until the game is over (e.g. GameState == GameState.GameOver).  in the loop, it calls to the GameService to the hanging GET.  when the GameModel changes, the call returns and the GameModel is then "marshalled" to the UI thread which updates the game.  
Our tests should spawn a thread to make the hanging GET and then continue on their main thread, waiting on a TASK signaled by the spawn thread when the GameModel is updated to simulate the behavior.

the synchronization functionality works this way:
1.	client has a thread that has a hanging GET.  this is in an infinite loop, terminging when the GameState reaches GameOver.  if the GET Timesout, it just loops to call the GET Again
2.	the main UI thread on the client can interact with the service that causes the GameModel to be updated.
3.	on ANY change to the GameModel (including Undo or Redo) the hanging GET for all clients completes and this GameModel is passed back to the client
4.	the thread that executes the hanging GET passes the new GameModel to the main UI thread, which then does whatever it does (in the Desktop app, it forces a UI update)
5.	the worker thread loops to do another hanging GET


this is the pattern our tests should be following.

## Rules 📋

These rules *MUST* be followed for *ALL* requests and no violations of any of these rules should be tolerated.

### **Development & Testing Guidelines**
1. **Command Separators**: When running commands in agent mode, always use ";" as a separator instead of "&&" because using "&&" will cause Copilot to hang when executing PowerShell commands.
2. **WinUI3 Desktop App**: The WinUI3 Desktop app is the main project and it works correctly. It can be analyzed for prior art. It cannot be changed without explicit directions to do so.
2. **Current Work Context**: Before starting any new work session or significant task, update the "Current Work" section with enough context to allow the work to continue seamlessly if a new session is created. Include current objectives, recent changes, pending tasks, and any important decisions or findings.
4. **Task Completion Verification**: Before marking any task as complete, you must ask "is this task complete?" If the answer is yes, then follow rule 3 to update documentation. If not, continue enhancing the tests based on feedback. For example, verifying that shuffle was called and clients were updated is not sufficient - we must also verify that the board actually changed after the shuffle (tiles and harbors should be randomized).
5. **GameState Testing**: Some states exist just to give the players a chance to look at the board and the only action is to click "Next".  if we have one of those states, you can simulate the Next action to get us to a state where we can run tests.
6. **Single Source of Truth**: All client state should be encapsulated in the GameModel that the GameStateMachine returns via the hanging GET pattern or by requesting the current game state (`/api/gamestate/{gameId}`). We should not need separate APIs like `/api/players/{gameId}` - all player information, current player, game state, etc. should come from the complete GameModel. The only exception might be for creating a new game.
7. **Catan Font Usage**: The companion web interface MUST use the official Catan font for all game-related icons and symbols. The font file is located at `Assets/Fonts/Catan.ttf` and should be served as a web font at `/fonts/Catan.ttf`. Use Unicode characters from `Layout/CatanFont.cs` for authentic Catan iconography (Settlement: \uE926, City: \uE900, Road: \uE909, Soldier: \uE90E, Knight: \uE930, etc.). This ensures visual consistency with the desktop app and provides the authentic Catan look and feel.
8. **TESTS MUST PASS**: Before marking any task as complete, ensure that all tests pass without errors or warnings. If there are compilation errors or test failures, fix them before proceeding.

**Rule Compliance**: All tests must pass without errors or warnings. If there are any issues, they must be resolved before proceeding with any further work. This includes ensuring that all tests are up-to-date and reflect the current state of the codebase.
**SignalR**: use the proxy in the Shared project to call SignalR


## Current Work
*This section should be updated at the start of each work session with current context.*

**Current Session**: ✅ **RULE 10 COMPLIANCE - ALL TESTS MUST PASS.  NO ERRORS NO WARNINGS.

### **GameModel Extensions for Rule 7 Compliance**
**Date**: 2025-01-25  
**Change Type**: ✅ Field Additions + Helper Methods

#### **What was changed**:
Added the following fields to `GameModel` class in `Catan3.Shared/Models/GameModel.cs`:

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
/// Gets or sets the display names of players extracted from their IDs.
/// This field supports Rule 7 (Single Source of Truth) by ensuring GameModel contains all player display information.
/// Follows Desktop app pattern: "Joe-001" → "Joe"
/// </summary>
public List<string> PlayerNames { get; set; } = new();
```

Added helper methods for computed fields:
- `UpdatePlayerNames()` - Extracts display names from player IDs
- `GetDisplayName()` - Creates game display name
- `GetFormattedGameState()` - Formats game state for display
- `GetCurrentPlayerName()` - Gets current player display name
- `GetCreatedTimeDisplay()` - Formats creation time
- `GetIsActive()` - Determines if game is active
- `GetSummary()` - Creates game summary string
- `ExtractNameFromId(string id)` - Static helper for name extraction

#### **Why it was changed**:
**Rule 7 Compliance**: The previous implementation violated the Single Source of Truth principle because:
- GameInfo contained "truth" that didn't exist in GameModel (GameId, CreatedTime, PlayerNames)
- GameStateMachineService had to compute display fields that should be in GameModel
- `/api/companion/games` created data inconsistency by generating computed fields at service level

This change ensures GameModel contains ALL game truth, making GameInfo a pure summary/display object.

#### **Impact on Desktop app**:
**🚨 BREAKING CHANGES**: 
1. **GameModel Constructor**: Desktop app will need to initialize new fields
2. **Player Name Handling**: Desktop app should call `UpdatePlayerNames()` after player changes
3. **Display Logic**: Desktop app can use new helper methods instead of computing display fields
4. **GameId Management**: Desktop app may need to handle server-generated GameIds

#### **Required Desktop app modifications**:
1. **Update GameModel instantiation** to initialize `GameId`, `CreatedTime`, and `PlayerNames`
2. **Call `UpdatePlayerNames()`** whenever the Players collection changes
3. **Replace existing display logic** with new helper methods (`GetDisplayName()`, etc.)
4. **Update save/load logic** to handle the new fields properly
5. **Consider GameId source** - if Desktop app generates its own GameIds, ensure consistency

### **GameStateMachine Architecture Changes**
**Date**: 2025-01-25  
**Change Type**: ✅ Constructor + GameId Generation

#### **What was changed**:
Modified `GameStateMachine` class in `Catan3.GameService/Controllers/GameStateMachine.cs`:

```csharp
/// <summary>
/// Server-generated unique identifier for this game instance.
/// Generated in constructor to ensure GameId is always available for all GameModels.
/// </summary>
public string GameId { get; private set; }

public GameStateMachine(IPersistanceService? persistanceService, string localSaveFile)
{
    // Generate server-side GameId to ensure it's available for all GameModels
    GameId = Guid.NewGuid().ToString();
    
    Log = new Log<string>(persistanceService, localSaveFile);
    MyPersistanceService = persistanceService;
}
```

Updated `LogGameModel()` method to ensure all GameModels have correct metadata:
```csharp
private void LogGameModel(GameModel gameModel)
{
    // Rule 7 Compliance: Ensure GameModel has the GameId from this GameStateMachine
    gameModel.GameId = GameId;
    gameModel.CreatedTime = gameModel.CreatedTime == default ? DateTime.UtcNow : gameModel.CreatedTime;
    
    // Increment version for each state change
    gameModel.Version = Log.DoneCount + 1;
    
    // ...existing code...
}
```

#### **Why it was changed**:
The previous approach required external GameId generation and passing, which created dependency issues and violated the principle that GameStateMachine should be self-contained. Server-side GameId generation ensures:
- GameStateMachine owns its identity from creation
- All GameModels produced have consistent GameId
- No external coordination required for GameId uniqueness

#### **Impact on Desktop app**:
**🚨 BREAKING CHANGES**:
If the Desktop app uses GameStateMachine directly, it will need to:
1. **Remove GameId parameters** from GameStateMachine constructor calls
2. **Get GameId from GameStateMachine.GameId property** instead of passing it
3. **Update any code** that expects to control GameId generation

#### **Required Desktop app modifications**:
1. **Update GameStateMachine instantiation** to not pass gameId parameter
2. **Read GameId from GameStateMachine.GameId property** after creation
3. **Update any persistence logic** that relies on external GameId management
4. **Consider impact on existing save files** - may need migration logic

### **API Changes - Server-Generated GameIds**  
**Date**: 2025-01-25  
**Change Type**: ✅ Breaking API Changes

#### **What was changed**:
1. **`/api/game/new` endpoint** now returns server-generated `gameId` instead of accepting client-provided one
2. **`/api/game/load` endpoint** now returns server-generated `gameId` for loaded games  
3. **`/api/game/register` endpoint** deprecated with error message
4. **GameStateMachineService methods** now return `(string gameId, GameModel gameModel)` tuples

#### **Why it was changed**:
Client-generated GameIds created race conditions and coordination issues. Server-generated GameIds ensure:
- Uniqueness guaranteed by server
- No client coordination required
- Simpler client implementation
- Better security (clients can't guess/collision attack GameIds)

#### **Impact on Desktop app**:
**🚨 BREAKING CHANGES**:
If Desktop app uses these APIs:
1. **Update `/api/game/new` calls** to receive gameId from response instead of sending it
2. **Remove `/api/game/register` usage** - now deprecated
3. **Update game creation flow** to handle server-assigned GameIds

#### **Required Desktop app modifications**:
1. **Modify new game creation** to extract gameId from API response
2. **Update UI flow** to show "Creating game..." → "Game created: {gameId}"
3. **Update error handling** for deprecated `/api/game/register` endpoint
4. **Consider bookmark/favorites logic** if it relies on predictable GameIds

### **NewGameRequest and PlayerInfo Models Added to Shared**
**Date**: 2025-01-25  
**Change Type**: ✅ New Model Classes

#### **What was changed**:
Added two new model classes to `Catan3.Shared/Models/GameInfo.cs`:

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

#### **Why it was changed**:
These models were needed to support the new game creation API and provide a consistent structure for player information that matches the Desktop app patterns. The `NewGameRequest` supports both simple string arrays (backward compatibility) and complex player objects (Desktop app format).

#### **Impact on Desktop app**:
**🔧 NEW CAPABILITIES**:
1. **Standardized Player Structure**: Desktop app can use `PlayerInfo` for consistent player representation
2. **New Game Request Format**: Desktop app can use `NewGameRequest` for API compatibility
3. **Dual Format Support**: Can send either `PlayerIds` (strings) or `Players` (objects with Id/Name)

#### **Required Desktop app modifications**:
1. **Optional Enhancement**: Consider using `PlayerInfo` class for player representation
2. **API Integration**: Use `NewGameRequest` when calling game service APIs
3. **Player Name Extraction**: Can leverage `GetPlayerNames()` helper method

---

### **PlayerModel Name Property Addition - Eliminating PlayerNames Redundancy**
**Date**: 2025-01-25  
**Change Type**: ✅ Property Addition + Architecture Cleanup

#### **What was changed**:
**PlayerModel** - Added `Name` computed property in `Catan3.Shared/Models/PlayerModel.cs`:

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

**GameModel** - Removed redundant `PlayerNames` field and `UpdatePlayerNames()` method, added helper method:

```csharp
/// <summary>
/// Gets a list of player names for API compatibility.
/// </summary>
public List<string> GetPlayerNames()
{
    return Players.Select(p => p.Name).ToList();
}
```

**GameStateMachineService** - Removed all `UpdatePlayerNames()` calls and updated `GetAvailableGames()` to use `gameModel.GetPlayerNames()`.

#### **Why it was changed**:
**Architecture Issue**: The previous implementation violated the Single Responsibility Principle by having duplicate data:
- `List<PlayerModel> Players` contained player information
- `List<string> PlayerNames` duplicated the same name information
- Required manual synchronization with `UpdatePlayerNames()`
- GameStateMachine had to know about display logic (player names)

**Better Design**: PlayerModel now owns its own display name logic:
- `PlayerModel.Name` property automatically extracts name from `Id`
- No data duplication or synchronization needed
- GameStateMachine remains focused on game logic, not display concerns
- Follows Single Source of Truth principle

#### **Impact on Desktop app**:
**🔧 BREAKING CHANGES**: 
1. **PlayerModel.Name Property**: Desktop app can now access `player.Name` directly instead of extracting from ID
2. **GameModel.PlayerNames Removed**: Replace `gameModel.PlayerNames` with `gameModel.GetPlayerNames()` or `gameModel.Players.Select(p => p.Name)`
3. **UpdatePlayerNames() Removed**: No longer need to manually call this method
4. **GameStateMachine Simplification**: GameStateMachine no longer deals with player names

#### **Required Desktop app modifications**:
1. **Replace PlayerNames usage**: 
   - `gameModel.PlayerNames` → `gameModel.GetPlayerNames()` or `gameModel.Players.Select(p => p.Name).ToList()`
2. **Remove UpdatePlayerNames() calls**: No longer needed since `PlayerModel.Name` is computed automatically
3. **Use PlayerModel.Name**: Access `player.Name` directly instead of extracting from `player.Id`
4. **Remove PlayerNames bindings**: Update UI bindings to use `Players.Select(p => p.Name)` collections
5. **Simplify game logic**: Remove any code that manually managed player name synchronization

#### **Benefits**:
- **Eliminates data duplication**: Single source of truth for player names
- **Automatic consistency**: No manual synchronization needed
- **Cleaner architecture**: Each class has single responsibility
- **Better encapsulation**: PlayerModel owns its display logic
- **Reduced complexity**: Less code to maintain and fewer opportunities for bugs

---

## **SEPARATION OF CONCERNS REFACTOR - COMPLETED** 

**Date**: 2025-01-25  
**Change Type**: ✅ Architecture Refactor - Proper Separation of Concerns

### **Separation of Concerns for Real-time Updates - IMPLEMENTED**

Successfully implemented the proper architecture design for hanging GET (real-time update) functionality by moving it out of the GameStateMachine into the GameService layer.

#### **What was implemented**:

**1. Created `IClientNotification` Interface**:
```csharp
public interface IClientNotification
{
    Task NotifyAsync(string gameId, GameModel gameModel);
    Task<GameModel> WaitForNotificationAsync(string gameId, string clientId, int currentVersion, CancellationToken cancellationToken);
}
```

**2. Implemented `ClientNotificationService`**:
- Maintains `ConcurrentDictionary<string, ClientManager>` where key = gameId
- Each `ClientManager` handles multiple clients waiting for updates for that specific game
- When `NotifyAsync()` is called:
  1. Serializes GameModel to JSON
  2. Stores JSON in ClientManager
  3. Signals all waiting threads for that gameId
  4. Each waiting thread copies JSON to response and completes the HTTP request

**3. Updated `GameStateMachine` Constructor**:
```csharp
public GameStateMachine(IPersistanceService persistanceService, IClientNotification clientNotification, string localSaveFile)
{
    _clientNotification = clientNotification;
    // ... existing code
}
```

**4. Modified `GameStateMachine.LogGameModel()` Method**:
```csharp
private void LogGameModel(GameModel gameModel)
{
    // ... existing game state logic ...
    
    // Notify clients of state change asynchronously
    _ = Task.Run(async () =>
    {
        try
        {
            await _clientNotification.NotifyAsync(GameId, gameModel);
        }
        catch (Exception ex)
        {
            TraceMessage($"Error notifying clients: {ex.Message}");
        }
    });
}
```


#### **Protocol Flow Implemented**:
1. ✅ Client sends request to GameService containing the GameId
2. ✅ GameService looks up the GameId in its HashMap (GameStateMachineService)
   - If not found, returns HTTP 404
   - If found, dispatches the request to the correct GameStateMachine
3. ✅ GameStateMachine **always** returns a GameModel (focus on game state only)
4. ✅ GameService uses the GameModel to trigger the hanging GET notification system to update all clients with the new game model

#### **Impact on Desktop app**:
**🔧 BREAKING CHANGES**: If the Desktop app uses GameStateMachine directly:
1. **Update GameStateMachine instantiation** to include `IClientNotification` parameter
2. **Implement IClientNotification** for Desktop app's notification needs
3. **Update any direct GameStateMachine usage** to provide the notification service

#### **Required Desktop app modifications**:
1. **Create Desktop IClientNotification implementation** (likely using MVVM Messenger)
2. **Update GameStateMachine constructor calls** to include notification service
3. **Consider using same separation of concerns** for consistency

Directory structure: here is the layout of the project, it should be kept up to date as new directories are added or moved around.
```
D:\GitHub\Catan3 [Companion ≡ +6 ~6 -253 !]> tree /A
Folder PATH listing for volume Disk 0
Volume serial number is CC93-B862
D:.
+---.github
|   \---workflows
+---.vscode
+---Catan3.CLI
|   +---Commands
|   \---Services
+---Catan3.GameService
|   +---Controllers
|   +---Design Assets
|   +---Extensions
|   +---Factory
|   +---Hubs
|   +---Models
|   +---Properties
|   +---Services
|   +---Utility
|   +---Views
|   |   +---Home
|   |   \---Shared
|   \---wwwroot
|       +---css
|       +---diagrams
|       +---fonts
|       +---js
|       +---lib
|       |   +---bootstrap
|       |   |   \---dist
|       |   |       +---css
|       |   |       \---js
|       |   +---jquery
|       |   |   \---dist
|       |   +---jquery-validation
|       |   |   \---dist
|       |   \---jquery-validation-unobtrusive
|       |       \---dist
|       \---mermaid-source
+---Catan3.Shared
|   +---Extensions
|   +---Models
|   +---Services
|   \---Utility
+---DesktopApp
|   +---Assets
|   |   +---DefaultPlayers
|   |   +---Fonts
|   |   +---Harbors
|   |   +---ResourceCards
|   |   +---SVG
|   |   +---Test Files
|   |   \---Tiles
|   +---Assets (2)
|   |   \---DefaultPlayers
|   +---bin
|   |   \---ARM64
|   |       \---Debug
|   |           \---net9.0-windows10.0.22621.0
|   |               \---win-arm64
|   +---Buildings
|   |   \---BuildingViewModel
|   +---Controls
|   +---Game
|   |   +---Game Control
|   |   +---GameFactory
|   |   +---GameModel
|   |   +---GameView
|   |   \---NewGame
|   +---GameState
|   |   \---GameLog
|   +---Harbors
|   +---Layout
|   +---MainPage
|   +---Models
|   |   \---ModelGeneration
|   +---obj
|   |   \---ARM64
|   |       \---Debug
|   |           \---net9.0-windows10.0.22621.0
|   |               \---win-arm64
|   |                   +---ref
|   |                   \---refint
|   +---Player
|   |   \---PlayerSettings
|   +---Properties
|   |   \---PublishProfiles
|   +---Resources
|   +---Roads
|   +---Robber
|   +---Rolls
|   +---Services
|   |   \---Companion
|   |       \---Models
|   +---Tests
|   +---Themes
|   +---Tiles
|   +---Utility
|   \---ValueConverters
+---Docs
+---Scripts
+---Tests.GameService
|   +---bin
|   |   +---Debug
|   |   |   \---net9.0
|   |   \---Release
|   |       \---net9.0
|   +---Companion
|   +---CompanionUI
|   +---obj
|   |   +---Debug
|   |   |   \---net9.0
|   |   |       +---ref
|   |   |       \---refint
|   |   \---Release
|   |       \---net9.0
|   |           +---ref
|   |           \---refint
|   +---SignalR
|   \---TestClient
|       +---Commands
|       \---Services
\---Tests.Shared
    +---bin
    |   +---Debug
    |   |   \---net9.0
    |   \---Release
    |       \---net9.0
    +---obj
    |   +---Debug
    |   |   \---net9.0
    |   |       +---ref
    |   |       \---refint
    |   \---Release
    |       \---net9.0
    |           +---ref
    |           \---refint
    \---Serialization
D:\GitHub\Catan3 [Companion ≡ +6 ~6 -253 !]> Get-ChildItem -Path . -Include bin,obj -Recurse -Directory |
>>     ForEach-Object {
>>         Write-Host "Deleting $($_.FullName)"
>>         Remove-Item -LiteralPath $_.FullName -Recurse -Force -ErrorAction Stop
>>     }
Deleting D:\GitHub\Catan3\DesktopApp\bin
Deleting D:\GitHub\Catan3\DesktopApp\obj
Deleting D:\GitHub\Catan3\Tests.GameService\bin
Deleting D:\GitHub\Catan3\Tests.GameService\obj
Deleting D:\GitHub\Catan3\Tests.Shared\bin
Deleting D:\GitHub\Catan3\Tests.Shared\obj
D:\GitHub\Catan3 [Companion ≡ +6 ~6 -253 !]> tree /A
Folder PATH listing for volume Disk 0
Volume serial number is CC93-B862
D:.
+---.github
|   \---workflows
+---.vscode
+---Catan3.CLI
|   +---Commands
|   \---Services
+---Catan3.GameService
|   +---Controllers
|   +---Design Assets
|   +---Extensions
|   +---Factory
|   +---Hubs
|   +---Models
|   +---Properties
|   +---Services
|   +---Utility
|   +---Views
|   |   +---Home
|   |   \---Shared
|   \---wwwroot
|       +---css
|       +---diagrams
|       +---fonts
|       +---js
|       +---lib
|       |   +---bootstrap
|       |   |   \---dist
|       |   |       +---css
|       |   |       \---js
|       |   +---jquery
|       |   |   \---dist
|       |   +---jquery-validation
|       |   |   \---dist
|       |   \---jquery-validation-unobtrusive
|       |       \---dist
|       \---mermaid-source
+---Catan3.Shared
|   +---Extensions
|   +---Models
|   +---Services
|   \---Utility
+---DesktopApp
|   +---Assets
|   |   +---DefaultPlayers
|   |   +---Fonts
|   |   +---Harbors
|   |   +---ResourceCards
|   |   +---SVG
|   |   +---Test Files
|   |   \---Tiles
|   +---Assets (2)
|   |   \---DefaultPlayers
|   +---Buildings
|   |   \---BuildingViewModel
|   +---Controls
|   +---Game
|   |   +---Game Control
|   |   +---GameFactory
|   |   +---GameModel
|   |   +---GameView
|   |   \---NewGame
|   +---GameState
|   |   \---GameLog
|   +---Harbors
|   +---Layout
|   +---MainPage
|   +---Models
|   |   \---ModelGeneration
|   +---Player
|   |   \---PlayerSettings
|   +---Properties
|   |   \---PublishProfiles
|   +---Resources
|   +---Roads
|   +---Robber
|   +---Rolls
|   +---Services
|   |   \---Companion
|   |       \---Models
|   +---Tests
|   +---Themes
|   +---Tiles
|   +---Utility
|   \---ValueConverters
+---Docs
+---Scripts
+---Tests.GameService
|   +---Companion
|   +---CompanionUI
|   +---SignalR
|   \---TestClient
|       +---Commands
|       \---Services
\---Tests.Shared
    \---Serialization