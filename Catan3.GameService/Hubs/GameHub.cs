using Microsoft.AspNetCore.SignalR;
using Catan3.Shared.Models;
using Catan3.Shared.Utility;
using Catan3.GameService.Services;

namespace Catan3.GameService.Hubs
{
    /// <summary>
    /// Enhanced SignalR Hub for real-time game communication with direct MVVM message support.
    /// Provides instant push notifications and bi-directional communication using the same
    /// message objects as the Desktop app for perfect architectural consistency.
    /// </summary>
    public class GameHub : Hub
    {
        private readonly ILogger<GameHub> _logger;
        private readonly IClientNotification _clientNotification;

        public GameHub(ILogger<GameHub> logger, IClientNotification clientNotification)
        {
            _logger = logger;
            _clientNotification = clientNotification;
        }

        #region Connection Management

        /// <summary>
        /// Joins a client to a specific game group for real-time updates
        /// </summary>
        /// <param name="gameId">The game ID to join</param>
        /// <param name="playerId">The player ID joining</param>
        public async Task JoinGame(string gameId, string playerId)
        {
            LogEvent("JoinGame", $"Player {playerId} requesting to join game {gameId} via SignalR");
            
            // Validate GameId - fail fast if empty or null
            if (string.IsNullOrEmpty(gameId))
            {
                LogEvent("JoinGame", $"ERROR: Player {playerId} attempted to join with empty/null gameId", LogLevel.Error);
                throw new ArgumentException($"GameId cannot be null or empty. Player {playerId} must provide a valid GameId to join a game.");
            }
            
            await Groups.AddToGroupAsync(Context.ConnectionId, gameId);
            LogEvent("JoinGame", $"Player {playerId} added to SignalR group {gameId}");

            // Send current game state to ALL clients in the group (since player joining updates the GameModel)
            var currentGameState = GameStateMachineRegistry.GetGameStateMachine(gameId).GetCurrentState();
            LogEvent("JoinGame", $"Retrieved game state for {gameId}: {currentGameState?.GameState.ToString() ?? "NULL"}");
            
            if (currentGameState != null)
            {
                await Clients.Group(gameId).SendAsync("GameStateUpdated", currentGameState);
                LogEvent("Send Client Update", $"GameStateUpdated sent to ALL clients in group - PlayerId={playerId}, GameID={gameId}");
            }
            else
            {
                LogEvent("JoinGame", $"WARNING: No current game state found for game {gameId}", LogLevel.Warning);
            }

            // Notify other players about presence
            await Clients.GroupExcept(gameId, Context.ConnectionId)
                .SendAsync("PlayerPresenceChanged", playerId, true);
            
            LogEvent("JoinGame", $"Player {playerId} successfully joined game {gameId} and other players notified");
        }

        /// <summary>
        /// Removes a client from a game group
        /// </summary>
        /// <param name="gameId">The game ID to leave</param>
        /// <param name="playerId">The player ID leaving</param>
        public async Task LeaveGame(string gameId, string playerId)
        {
            await Groups.RemoveFromGroupAsync(Context.ConnectionId, gameId);
            LogEvent("LeaveGame", $"Player {playerId} left game {gameId}");

            // Notify other players about disconnection
            await Clients.Group(gameId).SendAsync("PlayerPresenceChanged", playerId, false);
        }

        /// <summary>
        /// Handles client disconnection
        /// </summary>
        public override async Task OnDisconnectedAsync(Exception? exception)
        {
            if (exception != null)
            {
                LogEvent("OnDisconnected", $"Client {Context.ConnectionId} disconnected with error: {exception.Message}", LogLevel.Warning);
            }
            else
            {
                LogEvent("OnDisconnected", $"Client {Context.ConnectionId} disconnected gracefully");
            }

            await base.OnDisconnectedAsync(exception);
        }

        /// <summary>
        /// Handles client connection
        /// </summary>
        public override async Task OnConnectedAsync()
        {
            LogEvent("OnConnected", $"Client {Context.ConnectionId} connected to GameHub");
            await base.OnConnectedAsync();
        }

        #endregion

        #region Direct MVVM Message Handlers - Same as Desktop App

        /// <summary>
        /// Executes a Shuffle action
        /// </summary>
        public async Task Shuffle(string gameId, string playerId)
        {
            LogEvent("GameCommand", $"SignalR Shuffle: for {playerId} in {gameId}");
            
            var gameStateMachine = GameStateMachineRegistry.GetGameStateMachine(gameId);
            var currentGameState = gameStateMachine.GetCurrentState();
            
            // Verify that the current player is the one sending the request
            if (currentGameState.CurrentPlayerId != playerId)
            {
                throw new GameException($"Player {playerId} cannot act - current player is {currentGameState.CurrentPlayerId}");
            }
            
            var updatedGameModel = await gameStateMachine.HandleShuffleAsync(new ShuffleMessage());
            
            // Notify all clients in game group of the updated GameModel
            await Clients.Group(gameId).SendAsync("GameStateUpdated", updatedGameModel);
            LogEvent("Send Client Update", $"GameStateUpdated sent for Shuffle - PlayerId={playerId}, GameID={gameId}");
            
            LogEvent("GameCommand", $"Shuffle completed successfully for game {gameId}", LogLevel.Debug);
        }

        /// <summary>
        /// Executes an Undo action
        /// </summary>
        public async Task Undo(string gameId, string playerId)
        {
            LogEvent("GameCommand", $"SignalR Undo: for {playerId} in {gameId}");
            
            var gameStateMachine = GameStateMachineRegistry.GetGameStateMachine(gameId);
            var currentGameState = gameStateMachine.GetCurrentState();
            
            // Verify that the current player is the one sending the request
            if (currentGameState.CurrentPlayerId != playerId)
            {
                throw new GameException($"Player {playerId} cannot act - current player is {currentGameState.CurrentPlayerId}");
            }
            
            var updatedGameModel = await gameStateMachine.HandleUndoAsync(new UndoMessage());
            
            // Notify all clients in game group of the updated GameModel
            await Clients.Group(gameId).SendAsync("GameStateUpdated", updatedGameModel);
            LogEvent("Send Client Update", $"GameStateUpdated sent for Undo - PlayerId={playerId}, GameID={gameId}");
            
            LogEvent("GameCommand", $"Undo completed successfully for game {gameId}", LogLevel.Debug);
        }

        /// <summary>
        /// Executes a Redo action
        /// </summary>
        public async Task Redo(string gameId, string playerId)
        {
            LogEvent("GameCommand", $"SignalR Redo: for {playerId} in {gameId}");
            
            var gameStateMachine = GameStateMachineRegistry.GetGameStateMachine(gameId);
            var currentGameState = gameStateMachine.GetCurrentState();
            
            // Verify that the current player is the one sending the request
            if (currentGameState.CurrentPlayerId != playerId)
            {
                throw new GameException($"Player {playerId} cannot act - current player is {currentGameState.CurrentPlayerId}");
            }
            
            var updatedGameModel = await gameStateMachine.HandleRedoAsync(new RedoMessage());
            
            // Notify all clients in game group of the updated GameModel
            await Clients.Group(gameId).SendAsync("GameStateUpdated", updatedGameModel);
            LogEvent("Send Client Update", $"GameStateUpdated sent for Redo - PlayerId={playerId}, GameID={gameId}");
            
            LogEvent("GameCommand", $"Redo completed successfully for game {gameId}", LogLevel.Debug);
        }

        /// <summary>
        /// Executes a Next action
        /// </summary>
        public async Task Next(string gameId, string playerId)
        {
            LogEvent("GameCommand", $"SignalR Next: for {playerId} in {gameId}");
            
            var gameStateMachine = GameStateMachineRegistry.GetGameStateMachine(gameId);
            var currentGameState = gameStateMachine.GetCurrentState();
            
            // Verify that the current player is the one sending the request
            if (currentGameState.CurrentPlayerId != playerId)
            {
                throw new GameException($"Player {playerId} cannot act - current player is {currentGameState.CurrentPlayerId}");
            }
            
            var updatedGameModel = await gameStateMachine.HandleNextAsync(new NextMessage());
            
            // Notify all clients in game group of the updated GameModel
            await Clients.Group(gameId).SendAsync("GameStateUpdated", updatedGameModel);
            LogEvent("Send Client Update", $"GameStateUpdated sent for Next - PlayerId={playerId}, GameID={gameId}");
            
            LogEvent("GameCommand", $"Next completed successfully for game {gameId}", LogLevel.Debug);
        }

        /// <summary>
        /// Executes Purchase commands for entitlements - matches Desktop app exactly
        /// </summary>
        /// <param name="gameId">The game ID</param>
        /// <param name="playerId">The player ID making the purchase</param>
        /// <param name="message">The PurchaseMessage object</param>
        public async Task ExecutePurchase(string gameId, string playerId, PurchaseMessage message)
        {
            var commandId = Guid.NewGuid().ToString();
            try 
            {
                LogEvent("Purchase", $"SignalR Purchase: {message.Entitlement} for {playerId} in {gameId}");
                
                // Process synchronously for real-time response
                var updatedGameModel = await GameStateMachineRegistry.GetGameStateMachine(gameId).HandlePurchaseAsync(message);
                
                // Notify all clients in game group instantly
                await Clients.Group(gameId).SendAsync("GameStateUpdated", updatedGameModel);
                LogEvent("Send Client Update", $"GameStateUpdated sent for Purchase: {message.Entitlement} - PlayerId={playerId}, GameID={gameId}");
                
                // Notify command completion to original client
                await Clients.Caller.SendAsync("CommandCompleted", commandId, true, $"Purchased {message.Entitlement}");
                
                LogEvent("Purchase", $"Purchase {message.Entitlement} completed successfully for game {gameId}", LogLevel.Debug);
            }
            catch (Exception ex)
            {
                LogEvent("Purchase", $"Failed to execute Purchase {message.Entitlement} for {playerId} in {gameId}: {ex.Message}", LogLevel.Error);
                var errorInfo = CreateDetailedErrorInfo(ex, "Purchase", $"{message.Entitlement}");
                await Clients.Caller.SendAsync("CommandFailed", commandId, errorInfo);
            }
        }

        /// <summary>
        /// Executes a BalanceBoard action
        /// </summary>
        public async Task BalanceBoard(string gameId, string playerId)
        {
            LogEvent("GameCommand", $"SignalR BalanceBoard: for {playerId} in {gameId}");
            
            var gameStateMachine = GameStateMachineRegistry.GetGameStateMachine(gameId);
            var currentGameState = gameStateMachine.GetCurrentState();
            
            // Verify that the current player is the one sending the request
            if (currentGameState.CurrentPlayerId != playerId)
            {
                throw new GameException($"Player {playerId} cannot act - current player is {currentGameState.CurrentPlayerId}");
            }
            
            var updatedGameModel = await gameStateMachine.HandleBalanceBoardAsync(new BalanceBoardMessage());
            
            // Notify all clients in game group of the updated GameModel
            await Clients.Group(gameId).SendAsync("GameStateUpdated", updatedGameModel);
            LogEvent("Send Client Update", $"GameStateUpdated sent for BalanceBoard - PlayerId={playerId}, GameID={gameId}");
            
            LogEvent("GameCommand", $"BalanceBoard completed successfully for game {gameId}", LogLevel.Debug);
        }

        /// <summary>
        /// Executes Road Purchase and placement - matches Desktop app exactly
        /// </summary>
        /// <param name="gameId">The game ID</param>
        /// <param name="playerId">The player ID placing the road</param>
        /// <param name="message">The RoadPurchaseMessage object</param>
        public async Task ExecuteRoadPurchase(string gameId, string playerId, RoadPurchaseMessage message)
        {
            var commandId = Guid.NewGuid().ToString();
            try 
            {
                LogEvent("RoadPurchase", $"SignalR Road Purchase: {message.RoadKey} for {playerId} in {gameId}");
                
                // Process synchronously for real-time response
                var updatedGameModel = await GameStateMachineRegistry.GetGameStateMachine(gameId).HandleRoadPurchaseAsync(message);
                
                // Notify all clients in game group instantly
                await Clients.Group(gameId).SendAsync("GameStateUpdated", updatedGameModel);
                LogEvent("Send Client Update", $"GameStateUpdated sent for RoadPurchase: {message.RoadKey} - PlayerId={playerId}, GameID={gameId}");
                
                // Notify command completion to original client
                await Clients.Caller.SendAsync("CommandCompleted", commandId, true, "Road placed successfully");
                
                LogEvent("RoadPurchase", $"Road purchase completed successfully for game {gameId}", LogLevel.Debug);
            }
            catch (Exception ex)
            {
                LogEvent("RoadPurchase", $"Failed to execute Road Purchase for {playerId} in {gameId}: {ex.Message}", LogLevel.Error);
                var errorInfo = CreateDetailedErrorInfo(ex, "RoadPurchase", $"{message.RoadKey}");
                await Clients.Caller.SendAsync("CommandFailed", commandId, errorInfo);
            }
        }

        /// <summary>
        /// Executes Building Upgrade (Settlement/City placement) - matches Desktop app exactly
        /// </summary>
        /// <param name="gameId">The game ID</param>
        /// <param name="playerId">The player ID placing the building</param>
        /// <param name="message">The BuildingUpgradeMessage object</param>
        public async Task ExecuteBuildingUpgrade(string gameId, string playerId, BuildingUpgradeMessage message)
        {
            var commandId = Guid.NewGuid().ToString();
            try 
            {
                LogEvent("BuildingUpgrade", $"SignalR Building Upgrade: {message.BuildingKey} for {playerId} in {gameId}");
                
                // Process synchronously for real-time response
                var updatedGameModel = await GameStateMachineRegistry.GetGameStateMachine(gameId).HandleBuildingUpgradeAsync(message);
                
                // Notify all clients in game group instantly
                await Clients.Group(gameId).SendAsync("GameStateUpdated", updatedGameModel);
                LogEvent("Send Client Update", $"GameStateUpdated sent for BuildingUpgrade: {message.BuildingKey} - PlayerId={playerId}, GameID={gameId}");
                
                // Notify command completion to original client
                await Clients.Caller.SendAsync("CommandCompleted", commandId, true, "Building placed successfully");
                
                LogEvent("BuildingUpgrade", $"Building upgrade completed successfully for game {gameId}", LogLevel.Debug);
            }
            catch (Exception ex)
            {
                LogEvent("BuildingUpgrade", $"Failed to execute Building Upgrade for {playerId} in {gameId}: {ex.Message}", LogLevel.Error);
                var errorInfo = CreateDetailedErrorInfo(ex, "BuildingUpgrade", $"{message.BuildingKey}");
                await Clients.Caller.SendAsync("CommandFailed", commandId, errorInfo);
            }
        }

        /// <summary>
        /// Executes Move Robber commands - matches Desktop app exactly
        /// </summary>
        /// <param name="gameId">The game ID</param>
        /// <param name="playerId">The player ID moving the robber</param>
        /// <param name="message">The MoveRobberMessage object</param>
        public async Task ExecuteMoveRobber(string gameId, string playerId, MoveRobberMessage message)
        {
            var commandId = Guid.NewGuid().ToString();
            try 
            {
                LogEvent("MoveRobber", $"SignalR Move Robber: {message.Coordinates} for {playerId} in {gameId}");
                
                // Process synchronously for real-time response
                var updatedGameModel = await GameStateMachineRegistry.GetGameStateMachine(gameId).HandleMoveRobberAsync(message);
                
                // Notify all clients in game group instantly
                await Clients.Group(gameId).SendAsync("GameStateUpdated", updatedGameModel);
                LogEvent("Send Client Update", $"GameStateUpdated sent for MoveRobber: {message.Coordinates} - PlayerId={playerId}, GameID={gameId}");
                
                // Notify command completion to original client
                await Clients.Caller.SendAsync("CommandCompleted", commandId, true, "Robber moved successfully");
                
                LogEvent("MoveRobber", $"Move Robber completed successfully for game {gameId}", LogLevel.Debug);
            }
            catch (Exception ex)
            {
                LogEvent("MoveRobber", $"Failed to execute Move Robber for {playerId} in {gameId}: {ex.Message}", LogLevel.Error);
                var errorInfo = CreateDetailedErrorInfo(ex, "MoveRobber", $"{message.Coordinates}");
                await Clients.Caller.SendAsync("CommandFailed", commandId, errorInfo);
            }
        }

        /// <summary>
        /// Executes Roll commands for dice rolls - matches Desktop app exactly
        /// </summary>
        /// <param name="gameId">The game ID</param>
        /// <param name="playerId">The player ID rolling dice</param>
        /// <param name="message">The RollMessage object</param>
        public async Task ExecuteRoll(string gameId, string playerId, RollMessage message)
        {
            var commandId = Guid.NewGuid().ToString();
            try 
            {
                LogEvent("Roll", $"SignalR Roll: {message.Roll.NormalRoll} for {playerId} in {gameId}");
                
                // Process synchronously for real-time response
                var updatedGameModel = await GameStateMachineRegistry.GetGameStateMachine(gameId).HandleRollAsync(message);
                
                // Notify all clients in game group instantly
                await Clients.Group(gameId).SendAsync("GameStateUpdated", updatedGameModel);
                LogEvent("Send Client Update", $"GameStateUpdated sent for Roll: {message.Roll.NormalRoll} - PlayerId={playerId}, GameID={gameId}");
                
                // Notify command completion to original client
                await Clients.Caller.SendAsync("CommandCompleted", commandId, true, $"Rolled {message.Roll.NormalRoll}");
                
                LogEvent("Roll", $"Roll completed successfully for game {gameId}", LogLevel.Debug);
            }
            catch (Exception ex)
            {
                LogEvent("Roll", $"Failed to execute Roll for {playerId} in {gameId}: {ex.Message}", LogLevel.Error);
                var errorInfo = CreateDetailedErrorInfo(ex, "Roll", $"{message.Roll.NormalRoll}");
                await Clients.Caller.SendAsync("CommandFailed", commandId, errorInfo);
            }
        }

        /// <summary>
        /// Executes Set Player Order commands - matches Desktop app exactly
        /// </summary>
        /// <param name="gameId">The game ID</param>
        /// <param name="playerId">The player ID setting the order</param>
        /// <param name="message">The SetPlayerOrderMessage object</param>
        public async Task ExecuteSetPlayerOrder(string gameId, string playerId, SetPlayerOrderMessage message)
        {
            var commandId = Guid.NewGuid().ToString();
            try 
            {
                LogEvent("SetPlayerOrder", $"SignalR Set Player Order for {playerId} in {gameId}");
                
                // Process synchronously for real-time response
                var updatedGameModel = await GameStateMachineRegistry.GetGameStateMachine(gameId).HandleSetPlayerOrderAsync(message);
                
                // Notify all clients in game group instantly
                await Clients.Group(gameId).SendAsync("GameStateUpdated", updatedGameModel);
                LogEvent("Send Client Update", $"GameStateUpdated sent for SetPlayerOrder - PlayerId={playerId}, GameID={gameId}");
                
                // Notify command completion to original client
                await Clients.Caller.SendAsync("CommandCompleted", commandId, true, "Player order set successfully");
                
                LogEvent("SetPlayerOrder", $"Set Player Order completed successfully for game {gameId}", LogLevel.Debug);
            }
            catch (Exception ex)
            {
                LogEvent("SetPlayerOrder", $"Failed to execute Set Player Order for {playerId} in {gameId}: {ex.Message}", LogLevel.Error);
                var errorInfo = CreateDetailedErrorInfo(ex, "SetPlayerOrder", "");
                await Clients.Caller.SendAsync("CommandFailed", commandId, errorInfo);
            }
        }

        /// <summary>
        /// Executes Participating In Supplemental phase commands - matches Desktop app exactly
        /// </summary>
        /// <param name="gameId">The game ID</param>
        /// <param name="playerId">The player ID</param>
        /// <param name="participating">Whether the player is participating in supplemental</param>
        public async Task ExecuteParticipatingInSupplemental(string gameId, string playerId, bool participating)
        {
            var commandId = Guid.NewGuid().ToString();
            try 
            {
                LogEvent("ParticipatingInSupplemental", $"SignalR Participating In Supplemental for {playerId} in {gameId}: {participating}");
                
                var message = new ParticipatingInSupplementalMessage(playerId, participating);
                
                // Process synchronously for real-time response
                var updatedGameModel = await GameStateMachineRegistry.GetGameStateMachine(gameId).HandleParticipatingInSupplementalAsync(message);
                
                // Notify all clients in game group instantly
                await Clients.Group(gameId).SendAsync("GameStateUpdated", updatedGameModel);
                LogEvent("Send Client Update", $"GameStateUpdated sent for ParticipatingInSupplemental - PlayerId={playerId}, GameID={gameId}");
                
                // Notify command completion to original client
                await Clients.Caller.SendAsync("CommandCompleted", commandId, true, "Supplemental participation set");
                
                LogEvent("ParticipatingInSupplemental", $"Participating In Supplemental completed successfully for game {gameId}", LogLevel.Debug);
            }
            catch (Exception ex)
            {
                LogEvent("ParticipatingInSupplemental", $"Failed to execute Participating In Supplemental for {playerId} in {gameId}: {ex.Message}", LogLevel.Error);
                var errorInfo = CreateDetailedErrorInfo(ex, "ParticipatingInSupplemental", "");
                await Clients.Caller.SendAsync("CommandFailed", commandId, errorInfo);
            }
        }

        /// <summary>
        /// Executes Balance Board commands - matches Desktop app exactly
        /// </summary>
        /// <param name="gameId">The game ID</param>
        /// <param name="playerId">The player ID</param>
        /// <param name="message">The BalanceBoardMessage object</param>
        public async Task ExecuteBalanceBoard(string gameId, string playerId, BalanceBoardMessage message)
        {
            var commandId = Guid.NewGuid().ToString();
            try 
            {
                LogEvent("BalanceBoard", $"SignalR Balance Board for {playerId} in {gameId}");
                
                // Process synchronously for real-time response
                var updatedGameModel = await GameStateMachineRegistry.GetGameStateMachine(gameId).HandleBalanceBoardAsync(message);
                
                // Notify all clients in game group instantly
                await Clients.Group(gameId).SendAsync("GameStateUpdated", updatedGameModel);
                LogEvent("Send Client Update", $"GameStateUpdated sent for BalanceBoard - PlayerId={playerId}, GameID={gameId}");
                
                // Notify command completion to original client
                await Clients.Caller.SendAsync("CommandCompleted", commandId, true, "Board balanced successfully");
                
                LogEvent("BalanceBoard", $"Balance Board completed successfully for game {gameId}", LogLevel.Debug);
            }
            catch (Exception ex)
            {
                LogEvent("BalanceBoard", $"Failed to execute Balance Board for {playerId} in {gameId}: {ex.Message}", LogLevel.Error);
                var errorInfo = CreateDetailedErrorInfo(ex, "BalanceBoard", "");
                await Clients.Caller.SendAsync("CommandFailed", commandId, errorInfo);
            }
        }

        /// <summary>
        /// Executes Go First commands - matches Desktop app exactly
        /// </summary>
        /// <param name="gameId">The game ID</param>
        /// <param name="playerId">The player ID</param>
        /// <param name="message">The GoFirstMessage object</param>
        public async Task ExecuteGoFirst(string gameId, string playerId, GoFirstMessage message)
        {
            var commandId = Guid.NewGuid().ToString();
            try 
            {
                // Get GameStateMachine and current state first
                var gameStateMachine = GameStateMachineRegistry.GetGameStateMachine(gameId);
                var currentGameModel = gameStateMachine.GetCurrentState();
                
                // Validate the message request
                ValidateMessage(currentGameModel, playerId, "GoFirst", $"Target First Player={message.PlayerId}");
                
                // Process synchronously for real-time response
                var updatedGameModel = await gameStateMachine.HandleGoFirstAsync(message);
                
                // Notify all clients in game group instantly
                await Clients.Group(gameId).SendAsync("GameStateUpdated", updatedGameModel);
                LogEvent("Send Client Update", $"GameStateUpdated sent for GoFirst: {message.PlayerId} - PlayerId={playerId}, GameID={gameId}");
                
                // Notify command completion to original client
                await Clients.Caller.SendAsync("CommandCompleted", commandId, true, "Turn order set successfully");
                
                LogEvent("GoFirst", $"Go First completed successfully for game {gameId}", LogLevel.Debug);
            }
            catch (Exception ex)
            {
                LogEvent("GoFirst", $"Failed to execute Go First for {playerId} in {gameId}: {ex.Message}", LogLevel.Error);
                var errorInfo = CreateDetailedErrorInfo(ex, "GoFirst", $"{message.PlayerId}");
                await Clients.Caller.SendAsync("CommandFailed", commandId, errorInfo);
            }
        }


        #endregion

        #region Helper Methods

        /// <summary>
        /// Centralized logging method using pure ASP.NET Core logging
        /// </summary>
        /// <param name="eventType">The type of event being logged</param>
        /// <param name="message">The message to log</param>
        /// <param name="logLevel">The log level (defaults to Information)</param>
        private void LogEvent(string eventType, string message, LogLevel logLevel = LogLevel.Information)
        {
            _logger.Log(logLevel, "[GameHub][{EventType}] {Message}", eventType, message);
        }

        /// <summary>
        /// Validates message execution permissions and logs request details.
        /// Centralized validation logic for all game actions.
        /// </summary>
        /// <param name="gameModel">The current game model</param>
        /// <param name="requestingPlayerId">The player requesting the action</param>
        /// <param name="actionType">The type of action being requested</param>
        /// <param name="additionalInfo">Additional information about the action</param>
        private void ValidateMessage(GameModel gameModel, string requestingPlayerId, string actionType, string additionalInfo = "")
        {
            var currentPlayerId = gameModel.CurrentPlayerId;
            
            var logMessage = $"{actionType}: Requesting Player={requestingPlayerId}, Current Player={currentPlayerId}, Game State={gameModel.GameState}";
            if (!string.IsNullOrEmpty(additionalInfo))
            {
                logMessage += $", {additionalInfo}";
            }
            
            // Use Warning level to ensure it shows during tests (test config suppresses lower levels)
            LogEvent("Validation", logMessage, LogLevel.Warning);
            
            // Future: Add specific validation rules here based on action type
            // For now, we just log the details for debugging
        }

        /// <summary>
        /// Broadcasts a message to all clients in a game group
        /// </summary>
        /// <param name="gameId">The game ID</param>
        /// <param name="messageType">The message type</param>
        /// <param name="data">The message data</param>
        public async Task BroadcastToGame(string gameId, string messageType, object data)
        {
            await Clients.Group(gameId).SendAsync(messageType, data);
            LogEvent("Broadcast", $"Broadcasted {messageType} to all clients in game {gameId}", LogLevel.Debug);
        }

        /// <summary>
        /// Creates detailed error information from exceptions for better client-side debugging
        /// </summary>
        /// <param name="ex">The exception that occurred</param>
        /// <param name="operation">The operation that failed</param>
        /// <param name="context">Additional context about the operation</param>
        /// <returns>A detailed error object with debugging information</returns>
        private object CreateDetailedErrorInfo(Exception ex, string operation, string context)
        {
            var errorInfo = new
            {
                message = ex.Message,
                operation = operation,
                context = context,
                exceptionType = ex.GetType().Name,
                timestamp = DateTime.UtcNow.ToString("O"),
                // Include GameException-specific information if available
                errorLevel = (ex is GameException gameEx) ? gameEx.ErrorLevel.ToString() : "Unknown",
                // Include inner exception if present
                innerException = ex.InnerException?.Message,
                innerExceptionType = ex.InnerException?.GetType().Name,
                // Include stack trace only in development for security
                #if DEBUG
                stackTrace = ex.StackTrace,
                #endif
            };

            // Trace the detailed error for server-side debugging
            LogEvent("Error Details", $"Detailed error for {operation} with context {context}: {JsonHelper.Serialize(errorInfo)}", LogLevel.Error);

            return errorInfo;
        }

        #endregion
    }
}