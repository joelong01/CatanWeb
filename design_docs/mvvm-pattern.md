# MVVM Message Pattern Documentation

## Overview

This document describes the complete pattern for adding a new MVVM message to the Catan3 system. When a user interaction in the UI needs to trigger a game logic operation, a new MVVM message must be created and integrated throughout the entire stack.

The pattern involves:

1. **Message Definition** - Creating the MVVM message class
2. **Message Recording** - Supporting game replay/testing via recorded messages
3. **UI Layer** - Sending the message from a ViewModel
4. **Messaging Service** - Registering and handling the message
5. **Game Logic** - Implementing the actual operation in GameStateMachine
6. **Service Support** - Optional support for remote GameService operations

---

## Step 1: Define the MVVM Message

**File Location:** `Catan3.Shared/Models/MessageObjects.cs`

Create a new record class that inherits from your base message type or is a standalone record. The message should be immutable and contain only the data needed to execute the operation.

### Pattern

```csharp
/// <summary>
/// Sent when the user wants to [describe the action].
/// </summary>
public sealed record SwapTileResources(
    HexCoordinates SourceTileCoordinates,
    ResourceType SourceCurrentResource,
    HexCoordinates DestinationTileCoordinates,
    ResourceType DestinationCurrentResource)
{
    // Optional: Override ToString for debugging
    public override string ToString() => 
        $"SwapTileResources: {SourceTileCoordinates} <-> {DestinationTileCoordinates}";
}
```

### Guidelines

- **Immutability**: Use `sealed record` or sealed class with init properties
- **Validation**: Keep validation in GameStateMachine, not the message
- **Data Capture**: Include all information needed to validate and execute the action
- **Race Condition Prevention**: For drag operations, include "current" state to detect concurrent modifications
- **Documentation**: Add XML comments explaining when/why this message is sent

---

## Step 2: Register the Message for Recording/Replay

**File Location:** `Catan3.Shared/Models/RecordedMessage.cs`

Supporting game replay requires recording all messages during gameplay and replaying them in tests.

### Pattern

#### 2a. Create a Sealed Record Class

```csharp
/// <summary>
/// Snapshot of a <c>SwapTileResources</c> suitable for recording and replay.
/// </summary>
public sealed class SwapTileResourcesRecord : IRecordedMessage
{
    /// <summary>
    /// Discriminator value written to/expected from JSON: <c>"swapTileResources"</c>.
    /// </summary>
    public const string Discriminator = "swapTileResources";

    /// <inheritdoc />
    public string ExpectedGameHash { get; init; } = string.Empty;
    
    /// <inheritdoc />
    public GameState ExpectedGameState { get; init; } = GameState.Uninitialized;

    /// <summary>
    /// The coordinates of the source tile.
    /// </summary>
    public HexCoordinates SourceTileCoordinates { get; init; } = default!;
    
    /// <summary>
    /// The coordinates of the destination tile.
    /// </summary>
    public HexCoordinates DestinationTileCoordinates { get; init; } = default!;

    /// <inheritdoc />
    [JsonIgnore]
    public string RecordType => Discriminator;

    /// <summary>
    /// Constructor used during deserialization and for programmatic creation.
    /// </summary>
    [JsonConstructor]
    public SwapTileResourcesRecord(
        string expectedGameHash, 
        GameState expectedGameState,
        HexCoordinates sourceTileCoordinates,
        HexCoordinates destinationTileCoordinates)
    {
        ExpectedGameHash = expectedGameHash;
        ExpectedGameState = expectedGameState;
        SourceTileCoordinates = sourceTileCoordinates;
        DestinationTileCoordinates = destinationTileCoordinates;
    }

    /// <summary>
    /// Convenience constructor to capture a <see cref="SwapTileResources"/> at runtime.
    /// </summary>
    public SwapTileResourcesRecord(GameModel gameModel, SwapTileResources message)
    {
        ExpectedGameHash = gameModel.GameHash;
        ExpectedGameState = gameModel.GameState;
        SourceTileCoordinates = message.SourceTileCoordinates;
        DestinationTileCoordinates = message.DestinationTileCoordinates;
    }
}
```

#### 2b. Register with `IRecordedMessage` Interface

Add a `[JsonDerivedType]` attribute at the top of the file:

```csharp
[JsonDerivedType(typeof(SwapTileResourcesRecord), SwapTileResourcesRecord.Discriminator)]
public interface IRecordedMessage
{
    // ...existing interface...
}
```

The discriminator string (e.g., `"swapTileResources"`) must be:

- Unique across all record types
- Lowercase with camelCase
- Match the `Discriminator` constant in your record class

#### 2c. Add ToRecord Extension Method

In the `MessageConverters` static class:

```csharp
/// <summary>
/// Capture a <see cref="SwapTileResources"/> as a <see cref="SwapTileResourcesRecord"/>.
/// </summary>
public static IRecordedMessage ToRecord(this SwapTileResources msg, GameModel gameModel)
    => new SwapTileResourcesRecord(gameModel, msg);
```

---

## Step 3: Add UI Handler (ViewModel)

**File Location:** `DesktopApp/Tiles/TileViewModel.cs` (or appropriate ViewModel file)

### Guidelines for Choosing the Correct ViewModel

- **Tile Interactions**: `DesktopApp/Tiles/TileViewModel.cs`
- **Building Interactions**: `DesktopApp/Buildings/BuildingViewModel.cs`
- **Player Order/Setup**: `DesktopApp/Game/[GamePhase]ViewModel.cs`
- **General Game Actions**: `DesktopApp/Game/GamePageViewModel.cs`
- **Settings**: `DesktopApp/Settings/SettingsViewModel.cs`

### Pattern

```csharp
public partial class TileViewModel : ObservableObject
{
    // ...existing properties and methods...

    /// <summary>
    /// Handles the drag-and-drop tile swap interaction.
    /// Sends SwapTileResources message via MVVM Messenger.
    /// </summary>
    public void OnTileDragDrop(HexCoordinates destinationCoordinates, ResourceType destinationCurrentResource)
    {
        // Validate preconditions
        if (destinationCoordinates == null)
            return;

        // Create the message with all necessary data
        var message = new SwapTileResources(
            SourceTileCoordinates: this.Tile.TileKey,
            SourceCurrentResource: this.Tile.ResourceTileType,
            DestinationTileCoordinates: destinationCoordinates,
            DestinationCurrentResource: destinationCurrentResource
        );

        // Send the message via MVVM Messenger
        Messenger.Send(message);
    }
}
```

### Best Practices

- **Validation**: Only validate UI-level constraints (e.g., "user selected something")
- **Error Handling**: Don't try/catch here; let GameStateMachine handle business logic errors
- **Tracing**: Use `this.TraceMessage()` if needed for debugging user interactions
- **Messenger**: Always use `Messenger.Send(message)`, not direct method calls

---

## Step 4: Register Handler in GameMessageService

**File Locations:**

- Local game: `DesktopApp/GameState/GameMessageService.cs`
- Service game: `DesktopApp/GameState/GameMessageServiceProxy.cs`

### 4a. Register the Message Type (Main File)

In `GameMessageService.cs`, add registration to **both** `RegisterLocalGameMessages()` and `RegisterServiceGameMessages()`:

```csharp
/// <summary>
/// Registers message handlers for local GameStateMachine operations.
/// </summary>
private void RegisterLocalGameMessages()
{
    // ...existing registrations...
    Messenger.Register<SwapTileResources>(this, HandleSwapTileResourcesAsync);
}

/// <summary>
/// Registers message handlers for GameService proxy operations.
/// </summary>
private async Task RegisterServiceGameMessages()
{
    // ...existing registrations...
    Messenger.Register<SwapTileResources>(this, HandleSwapTileResourcesServiceAsync);
}
```

Also add to `UnregisterGameMessages()`:

```csharp
private void UnregisterGameMessages()
{
    // ...existing unregistrations...
    Messenger.Unregister<SwapTileResources>(this);
}
```

### 4b. Implement Local Handler (Main File)

In `GameMessageService.cs`:

```csharp
/// <summary>
/// Handles SwapTileResources message from the UI to swap tile resources during board setup.
/// Delegates to GameStateMachine and sends the result back to the UI.
/// </summary>
/// <param name="recipient">The message recipient (this service).</param>
/// <param name="message">The swap tile resources message from the UI.</param>
private async void HandleSwapTileResourcesAsync(object recipient, SwapTileResources message)
{
    if (_gameStateMachine == null)
    {
        SendErrorMessage("No active game. Please start a new game or load an existing one.", ErrorLevel.Critical);
        return;
    }

    try
    {
        var gameModel = await _gameStateMachine.HandleSwapResourcesAsync(message);
        Messenger.Send(new UpdateGameModel(gameModel));
    }
    catch (GameException e)
    {
        SendErrorMessage(e.Message, e.ErrorLevel);
    }
}
```

### 4c. Implement Service Handler (Proxy File)

In `GameMessageServiceProxy.cs`:

```csharp
/// <summary>
/// Handles SwapTileResources message from the UI for remote GameService operations.
/// </summary>
private async void HandleSwapTileResourcesServiceAsync(object recipient, SwapTileResources message)
{
    if (_gameServiceProxy == null)
    {
        SendErrorMessage("No active GameService connection", ErrorLevel.Critical);
        return;
    }

    try
    {
        await _gameServiceProxy.ExecuteSwapTileResourcesAsync(message);
        // Result comes via GameStateUpdated event
    }
    catch (Exception ex)
    {
        SendErrorMessage($"Failed to swap tile resources via service: {ex.Message}", ErrorLevel.Critical);
    }
}
```

---

## Step 5: Implement GameStateMachine Handler

**File Location:** `Catan3.Shared/GameLogic/GameStateMachine.cs`

### Pattern

```csharp
/// <summary>
/// Handles swapping tile resources between two tiles during board setup.
/// Validates that both tiles exist and swaps their resource types.
/// Only allowed during PickingBoard state.
/// </summary>
/// <param name="message">The swap request with source and destination tile coordinates.</param>
/// <returns>The updated GameModel after swapping tile resources.</returns>
/// <exception cref="GameException">Thrown when swap is not valid or tiles cannot be found.</exception>
public Task<GameModel> HandleSwapResourcesAsync(SwapTileResources message)
{
    var gameModel = _gameLog.CopyCurrent();
    _logger.Trace(GameTraceLevel.Trace, 
        $"[GameState={gameModel.GameState}][ExpectedGameHash={gameModel.GameHash}][Message={message}]");

    // Validate we're in the correct game state
    ThrowIfWrongState(gameModel.GameState, [Shared.Models.GameState.PickingBoard]);

    // Find the tiles
    var sourceTile = gameModel.Tiles.FirstOrDefault(t => t.TileKey == message.SourceTileCoordinates);
    var destTile = gameModel.Tiles.FirstOrDefault(t => t.TileKey == message.DestinationTileCoordinates);

    if (sourceTile == null || destTile == null)
    {
        throw new GameException("One or both tiles not found in game model");
    }

    // Validate current resources match what was sent (prevents race conditions)
    if (sourceTile.ResourceTileType != message.SourceCurrentResource ||
        destTile.ResourceTileType != message.DestinationCurrentResource)
    {
        throw new GameException("Tile resources changed during drag - swap cancelled");
    }

    // Perform the swap
    (sourceTile.ResourceTileType, destTile.ResourceTileType) = 
        (destTile.ResourceTileType, sourceTile.ResourceTileType);

    _logger.Trace(GameTraceLevel.Trace, 
        $"Tiles swapped: {message.SourceTileCoordinates} now has {sourceTile.ResourceTileType}, " +
        $"{message.DestinationTileCoordinates} now has {destTile.ResourceTileType}");

    LogGameModel(gameModel);
    return Task.FromResult(gameModel);
}
```

### Guidelines

- **Public Method**: Must be `public Task<GameModel> Handle[Action]Async(MessageType message)`
- **Logging**: Always trace with GameState and GameHash for debugging
- **State Validation**: Use `ThrowIfWrongState()` to validate preconditions
- **Error Messages**: Throw `GameException` with user-friendly messages
- **Tracing**: Trace both before and after the operation
- **Return**: Always call `LogGameModel()` before returning to update game state

---

## Step 6: Add GameServiceProxy Support (Optional)

**File Location:** `Catan3.Shared/Services/GameServiceProxy.cs`

This is only needed if you support remote GameService operations.

### Pattern

```csharp
/// <summary>
/// Executes a Swap Tile Resources command.
/// Uses the stored GameId from JoinGameAsync call.
/// </summary>
public async Task<CommandResult> ExecuteSwapTileResourcesAsync(SwapTileResources message, TimeSpan? timeout = null)
{
    if (string.IsNullOrEmpty(_gameId))
        throw new InvalidOperationException("Must join a game before executing actions. Call JoinGameAsync first.");
        
    await _connection.InvokeAsync("ExecuteSwapTileResources", _gameId, _playerId, message);
    
    return new CommandResult
    {
        CommandId = Guid.NewGuid().ToString(),
        Success = true,
        Message = $"Swap Tile Resources: {message.SourceTileCoordinates} <-> {message.DestinationTileCoordinates} sent",
        Timestamp = DateTime.UtcNow
    };
}
```

This method:

- Validates `_gameId` is set (player has joined a game)
- Invokes the SignalR hub method with `gameId`, `playerId`, and `message`
- Returns a `CommandResult` for tracking
- Does NOT call `_recorder?.RecordAction()` (that happens in the handler)

---

## Step 7: Corresponding GameService Hub Handler (Optional)

**File Location:** `Catan3.GameService/Hubs/GameHub.cs`

If you support remote GameService operations, add the hub method that receives the message:

```csharp
/// <summary>
/// Handles ExecuteSwapTileResources command from a connected client.
/// </summary>
[HubMethodName("ExecuteSwapTileResources")]
public async Task ExecuteSwapTileResources(string gameId, string playerId, SwapTileResources message)
{
    try
    {
        var gameService = GetGameService(gameId);
        var updatedGame = await gameService.SwapResourcesAsync(message);
        
        // Broadcast updated game state to all players
        await Clients.Group(gameId).SendAsync("GameStateUpdated", updatedGame);
    }
    catch (GameException ex)
    {
        await Clients.Caller.SendAsync("CommandFailed", nameof(ExecuteSwapTileResources), ex.Message);
    }
}
```

---

## Checklist: Adding a New MVVM Message

Use this checklist to ensure you've covered all integration points:

### Message Definition

- [ ] Created sealed record in `MessageObjects.cs`
- [ ] Added XML documentation with `<summary>` explaining when it's sent
- [ ] Included all data needed to execute the action
- [ ] Added override `ToString()` if helpful for debugging

### Message Recording

- [ ] Created sealed Record class in `RecordedMessage.cs`
- [ ] Defined `Discriminator` constant (unique, camelCase)
- [ ] Added `[JsonConstructor]` with init properties
- [ ] Added runtime capture constructor `public Record(GameModel, Message)`
- [ ] Registered with `[JsonDerivedType]` on `IRecordedMessage` interface
- [ ] Added `ToRecord()` extension method in `MessageConverters`

### UI Layer

- [ ] Created handler in appropriate ViewModel file
- [ ] Sends message via `Messenger.Send(message)`
- [ ] Validates UI-level preconditions only
- [ ] No try/catch around message send

### GameMessageService (Local)

- [ ] Added registration in `RegisterLocalGameMessages()`
- [ ] Added unregistration in `UnregisterGameMessages()`
- [ ] Implemented `HandleSwapTileResourcesAsync()` handler
- [ ] Handler checks for null `_gameStateMachine`
- [ ] Handler delegates to `_gameStateMachine.HandleSwapResourcesAsync()`
- [ ] Handler sends `UpdateGameModel` with result
- [ ] Handler catches `GameException` and calls `SendErrorMessage()`

### GameMessageServiceProxy (Service)

- [ ] Added registration in `RegisterServiceGameMessages()`
- [ ] Implemented `HandleSwapTileResourcesServiceAsync()` handler
- [ ] Handler checks for null `_gameServiceProxy`
- [ ] Handler calls `_gameServiceProxy.ExecuteSwapTileResourcesAsync()`
- [ ] Handler sends via `UpdateGameModel` from `GameStateUpdated` event

### GameStateMachine

- [ ] Implemented public `Handle[Action]Async(Message)` method
- [ ] Method signature returns `Task<GameModel>`
- [ ] Traces entry with GameState and GameHash
- [ ] Validates game state with `ThrowIfWrongState()`
- [ ] Throws `GameException` with user-friendly messages
- [ ] Modifies GameModel appropriately
- [ ] Calls `LogGameModel(gameModel)` before returning
- [ ] Traces key state changes

### GameServiceProxy (Optional)

- [ ] Created `ExecuteSwapTileResourcesAsync()` method
- [ ] Validates `_gameId` is set
- [ ] Invokes correct hub method name
- [ ] Returns `CommandResult`

### GameService Hub (Optional)

- [ ] Created `[HubMethodName("ExecuteSwapTileResources")]` method
- [ ] Receives `gameId`, `playerId`, and `message`
- [ ] Delegates to game service logic
- [ ] Broadcasts `GameStateUpdated` to all players
- [ ] Handles errors and sends `CommandFailed`

---

## Common Patterns & Examples

### Pattern: Race Condition Prevention

When the UI drags and drops, send the current resource type to detect concurrent modifications:

```csharp
// In UI ViewModel
var message = new SwapTileResources(
    SourceTileCoordinates: this.Tile.TileKey,
    SourceCurrentResource: this.Tile.ResourceTileType,  // Capture current state
    DestinationTileCoordinates: destinationCoordinates,
    DestinationCurrentResource: destinationCurrentResource  // Capture current state
);

// In GameStateMachine
if (sourceTile.ResourceTileType != message.SourceCurrentResource ||
    destTile.ResourceTileType != message.DestinationCurrentResource)
{
    throw new GameException("Tile resources changed during drag - swap cancelled");
}
```

### Pattern: State Machine Validation

Always validate the game is in the correct state:

```csharp
// Validate we're in PickingBoard state
ThrowIfWrongState(gameModel.GameState, [Shared.Models.GameState.PickingBoard]);
```

### Pattern: Error Messages

Use user-friendly error messages in `GameException`:

```csharp
throw new GameException("Tile resources changed during drag - swap cancelled");
// NOT: throw new GameException("Invalid state transition");
```

### Pattern: Logging

Always log entry point and key decisions:

```csharp
_logger.Trace(GameTraceLevel.Trace, 
    $"[GameState={gameModel.GameState}][ExpectedGameHash={gameModel.GameHash}][Message={message}]");

// Later, after operation:
_logger.Trace(GameTraceLevel.Trace, 
    $"Tiles swapped: {message.SourceTileCoordinates} now has {sourceTile.ResourceTileType}");
```

---

## Testing Considerations

The recording/replay system enables automated UI testing:

```csharp
// In UI tests, you can now:
1. Record a sequence of MVVM messages during gameplay
2. Serialize to JSON
3. Deserialize and replay in a new game instance
4. Verify the game reaches the same state
```

This is critical for preventing regressions when modifying game logic.

---

## Summary

Adding a new MVVM message requires touching 5-7 files across 3 projects:

| Project | Files | Purpose |
|---------|-------|---------|
| Catan3.Shared | MessageObjects.cs, RecordedMessage.cs | Message definition & recording |
| DesktopApp | TileViewModel.cs (or appropriate), GameMessageService.cs | UI handler & routing |
| Catan3.Shared | GameStateMachine.cs | Game logic implementation |
| Optional | GameServiceProxy.cs, GameService/Hubs/GameHub.cs | Remote service support |

Follow the checklist above to ensure all integration points are covered, and your new message will be fully integrated into the system.
