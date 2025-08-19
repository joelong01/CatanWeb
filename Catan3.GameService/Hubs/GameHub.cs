using Microsoft.AspNetCore.SignalR;
using Catan3.Shared.Models;
using Catan3.Shared.Utility;
using Catan3.GameService.Services;
using System.Text.Json;

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
        private readonly GameStateMachineService _gameService;
        private readonly IClientNotification _clientNotification;

        public GameHub(ILogger<GameHub> logger, GameStateMachineService gameService, IClientNotification clientNotification)
        {
            _logger = logger;
            _gameService = gameService;
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
            
            await Groups.AddToGroupAsync(Context.ConnectionId, gameId);
            LogEvent("JoinGame", $"Player {playerId} added to SignalR group {gameId}");

            // Send current game state to ALL clients in the group (since player joining updates the GameModel)
            var currentGameState = _gameService.GetCurrentGameState(gameId);
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
        /// Executes ExecuteGameActionMessage commands (Shuffle, Undo, Redo, Next) - matches Desktop app exactly
        /// </summary>
        /// <param name="gameId">The game ID</param>
        /// <param name="playerId">The player ID executing the action</param>
        /// <param name="message">The ExecuteGameActionMessage message object</param>
        public async Task ExecuteDoAction(string gameId, string playerId, ExecuteGameActionMessage message)
        {
            var commandId = Guid.NewGuid().ToString();
            try 
            {
                LogEvent("ExecuteGameActionMessage", $"SignalR ExecuteGameActionMessage: {message.Action} for {playerId} in {gameId}");
                
                // Process synchronously for real-time response
                var updatedGameModel = _gameService.ExecuteAction(gameId, gsm => gsm.HandleDoAction(message));
                
                // Notify all clients in game group instantly
                await Clients.Group(gameId).SendAsync("GameStateUpdated", updatedGameModel);
                LogEvent("Send Client Update", $"GameStateUpdated sent for ExecuteGameActionMessage: {message.Action} - PlayerId={playerId}, GameID={gameId}");
                
                // Notify command completion to original client
                await Clients.Caller.SendAsync("CommandCompleted", commandId, true, $"{message.Action} completed");
                
                LogEvent("ExecuteGameActionMessage", $"ExecuteGameActionMessage {message.Action} completed successfully for game {gameId}", LogLevel.Debug);
            }
            catch (Exception ex)
            {
                LogEvent("ExecuteGameActionMessage", $"Failed to execute ExecuteGameActionMessage {message.Action} for {playerId} in {gameId}: {ex.Message}", LogLevel.Error);
                var errorInfo = CreateDetailedErrorInfo(ex, "ExecuteGameActionMessage", $"{message.Action}");
                await Clients.Caller.SendAsync("CommandFailed", commandId, errorInfo);
            }
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
                var updatedGameModel = _gameService.ExecuteAction(gameId, gsm => gsm.HandlePurchaseMessage(message));
                
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
                var updatedGameModel = _gameService.ExecuteAction(gameId, gsm => gsm.HandleRoadPurchase(message));
                
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
                var updatedGameModel = _gameService.ExecuteAction(gameId, gsm => gsm.HandleBuildingUpgrade(message));
                
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
                var updatedGameModel = _gameService.ExecuteAction(gameId, gsm => gsm.HandleMoveRobber(message));
                
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
                var updatedGameModel = _gameService.ExecuteAction(gameId, gsm => gsm.HandleRoll(message));
                
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
                var updatedGameModel = _gameService.ExecuteAction(gameId, gsm => gsm.HandleSetPlayerOrder(message));
                
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
        /// Executes Players Doing Supplemental phase commands - matches Desktop app exactly
        /// </summary>
        /// <param name="gameId">The game ID</param>
        /// <param name="playerId">The player ID</param>
        /// <param name="message">The PlayersDoingSupplemental object</param>
        public async Task ExecutePlayersDoingSupplemental(string gameId, string playerId, PlayersDoingSupplemental message)
        {
            var commandId = Guid.NewGuid().ToString();
            try 
            {
                LogEvent("PlayersDoingSupplemental", $"SignalR Players Doing Supplemental for {playerId} in {gameId}");
                
                // Process synchronously for real-time response
                var updatedGameModel = _gameService.ExecuteAction(gameId, gsm => gsm.HandlePlayersDoingSupplemental(message));
                
                // Notify all clients in game group instantly
                await Clients.Group(gameId).SendAsync("GameStateUpdated", updatedGameModel);
                LogEvent("Send Client Update", $"GameStateUpdated sent for PlayersDoingSupplemental - PlayerId={playerId}, GameID={gameId}");
                
                // Notify command completion to original client
                await Clients.Caller.SendAsync("CommandCompleted", commandId, true, "Supplemental players set");
                
                LogEvent("PlayersDoingSupplemental", $"Players Doing Supplemental completed successfully for game {gameId}", LogLevel.Debug);
            }
            catch (Exception ex)
            {
                LogEvent("PlayersDoingSupplemental", $"Failed to execute Players Doing Supplemental for {playerId} in {gameId}: {ex.Message}", LogLevel.Error);
                var errorInfo = CreateDetailedErrorInfo(ex, "PlayersDoingSupplemental", "");
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
                var updatedGameModel = _gameService.ExecuteAction(gameId, gsm => gsm.HandleBalanceBoard(message));
                
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
                LogEvent("GoFirst", $"SignalR Go First: {message.PlayerId} for {playerId} in {gameId}");
                
                // Process synchronously for real-time response
                var updatedGameModel = _gameService.ExecuteAction(gameId, gsm => gsm.HandleGoFirst(message));
                
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

            // Log the detailed error for server-side debugging
            LogEvent("Error Details", $"Detailed error for {operation} with context {context}: {JsonHelper.Serialize(errorInfo)}", LogLevel.Error);

            return errorInfo;
        }

        #endregion
    }
}