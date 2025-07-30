using Microsoft.AspNetCore.SignalR;
using Catan3.Shared.Models;
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
            await Groups.AddToGroupAsync(Context.ConnectionId, gameId);
            _logger.LogInformation("Player {PlayerId} joined game {GameId} via SignalR", playerId, gameId);

            // Send current game state immediately to the new client
            var currentGameState = _gameService.GetCurrentGameState(gameId);
            if (currentGameState != null)
            {
                await Clients.Caller.SendAsync("GameStateUpdated", currentGameState);
                _logger.LogDebug("Sent current game state to newly joined client {PlayerId}", playerId);
            }

            // Notify other players about presence
            await Clients.GroupExcept(gameId, Context.ConnectionId)
                .SendAsync("PlayerPresenceChanged", playerId, true);
        }

        /// <summary>
        /// Removes a client from a game group
        /// </summary>
        /// <param name="gameId">The game ID to leave</param>
        /// <param name="playerId">The player ID leaving</param>
        public async Task LeaveGame(string gameId, string playerId)
        {
            await Groups.RemoveFromGroupAsync(Context.ConnectionId, gameId);
            _logger.LogInformation("Player {PlayerId} left game {GameId}", playerId, gameId);

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
                _logger.LogWarning(exception, "Client {ConnectionId} disconnected with error", Context.ConnectionId);
            }
            else
            {
                _logger.LogInformation("Client {ConnectionId} disconnected gracefully", Context.ConnectionId);
            }

            await base.OnDisconnectedAsync(exception);
        }

        /// <summary>
        /// Handles client connection
        /// </summary>
        public override async Task OnConnectedAsync()
        {
            _logger.LogInformation("Client {ConnectionId} connected to GameHub", Context.ConnectionId);
            await base.OnConnectedAsync();
        }

        #endregion

        #region Direct MVVM Message Handlers - Same as Desktop App

        /// <summary>
        /// Executes DoAction commands (Shuffle, Undo, Redo, Next) - matches Desktop app exactly
        /// </summary>
        /// <param name="gameId">The game ID</param>
        /// <param name="playerId">The player ID executing the action</param>
        /// <param name="message">The DoAction message object</param>
        public async Task ExecuteDoAction(string gameId, string playerId, DoAction message)
        {
            var commandId = Guid.NewGuid().ToString();
            try 
            {
                _logger.LogInformation("SignalR DoAction: {Action} for {PlayerId} in {GameId}", 
                    message.Action, playerId, gameId);
                
                // Process synchronously for real-time response
                var updatedGameModel = _gameService.ExecuteAction(gameId, gsm => gsm.HandleDoAction(message));
                
                // Notify all clients in game group instantly
                await Clients.Group(gameId).SendAsync("GameStateUpdated", updatedGameModel);
                
                // Notify command completion to original client
                await Clients.Caller.SendAsync("CommandCompleted", commandId, true, $"{message.Action} completed");
                
                _logger.LogDebug("DoAction {Action} completed successfully for game {GameId}", message.Action, gameId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to execute DoAction {Action} for {PlayerId} in {GameId}", 
                    message.Action, playerId, gameId);
                await Clients.Caller.SendAsync("CommandFailed", commandId, ex.Message);
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
                _logger.LogInformation("SignalR Purchase: {Entitlement} for {PlayerId} in {GameId}", 
                    message.Entitlement, playerId, gameId);
                
                // Process synchronously for real-time response
                var updatedGameModel = _gameService.ExecuteAction(gameId, gsm => gsm.HandlePurchaseMessage(message));
                
                // Notify all clients in game group instantly
                await Clients.Group(gameId).SendAsync("GameStateUpdated", updatedGameModel);
                
                // Notify command completion to original client
                await Clients.Caller.SendAsync("CommandCompleted", commandId, true, $"Purchased {message.Entitlement}");
                
                _logger.LogDebug("Purchase {Entitlement} completed successfully for game {GameId}", message.Entitlement, gameId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to execute Purchase {Entitlement} for {PlayerId} in {GameId}", 
                    message.Entitlement, playerId, gameId);
                await Clients.Caller.SendAsync("CommandFailed", commandId, ex.Message);
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
                _logger.LogInformation("SignalR Road Purchase: {RoadKey} for {PlayerId} in {GameId}", 
                    message.RoadKey, playerId, gameId);
                
                // Process synchronously for real-time response
                var updatedGameModel = _gameService.ExecuteAction(gameId, gsm => gsm.HandleRoadPurchase(message));
                
                // Notify all clients in game group instantly
                await Clients.Group(gameId).SendAsync("GameStateUpdated", updatedGameModel);
                
                // Notify command completion to original client
                await Clients.Caller.SendAsync("CommandCompleted", commandId, true, "Road placed successfully");
                
                _logger.LogDebug("Road purchase completed successfully for game {GameId}", gameId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to execute Road Purchase for {PlayerId} in {GameId}", playerId, gameId);
                await Clients.Caller.SendAsync("CommandFailed", commandId, ex.Message);
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
                _logger.LogInformation("SignalR Building Upgrade: {BuildingKey} for {PlayerId} in {GameId}", 
                    message.BuildingKey, playerId, gameId);
                
                // Process synchronously for real-time response
                var updatedGameModel = _gameService.ExecuteAction(gameId, gsm => gsm.HandleBuildingUpgrade(message));
                
                // Notify all clients in game group instantly
                await Clients.Group(gameId).SendAsync("GameStateUpdated", updatedGameModel);
                
                // Notify command completion to original client
                await Clients.Caller.SendAsync("CommandCompleted", commandId, true, "Building placed successfully");
                
                _logger.LogDebug("Building upgrade completed successfully for game {GameId}", gameId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to execute Building Upgrade for {PlayerId} in {GameId}", playerId, gameId);
                await Clients.Caller.SendAsync("CommandFailed", commandId, ex.Message);
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
                _logger.LogInformation("SignalR Move Robber: {Coordinates} for {PlayerId} in {GameId}", 
                    message.Coordinates, playerId, gameId);
                
                // Process synchronously for real-time response
                var updatedGameModel = _gameService.ExecuteAction(gameId, gsm => gsm.HandleMoveRobber(message));
                
                // Notify all clients in game group instantly
                await Clients.Group(gameId).SendAsync("GameStateUpdated", updatedGameModel);
                
                // Notify command completion to original client
                await Clients.Caller.SendAsync("CommandCompleted", commandId, true, "Robber moved successfully");
                
                _logger.LogDebug("Move Robber completed successfully for game {GameId}", gameId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to execute Move Robber for {PlayerId} in {GameId}", playerId, gameId);
                await Clients.Caller.SendAsync("CommandFailed", commandId, ex.Message);
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
                _logger.LogInformation("SignalR Roll: {Roll} for {PlayerId} in {GameId}", 
                    message.Roll.NormalRoll, playerId, gameId);
                
                // Process synchronously for real-time response
                var updatedGameModel = _gameService.ExecuteAction(gameId, gsm => gsm.HandleRoll(message));
                
                // Notify all clients in game group instantly
                await Clients.Group(gameId).SendAsync("GameStateUpdated", updatedGameModel);
                
                // Notify command completion to original client
                await Clients.Caller.SendAsync("CommandCompleted", commandId, true, $"Rolled {message.Roll.NormalRoll}");
                
                _logger.LogDebug("Roll completed successfully for game {GameId}", gameId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to execute Roll for {PlayerId} in {GameId}", playerId, gameId);
                await Clients.Caller.SendAsync("CommandFailed", commandId, ex.Message);
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
                _logger.LogInformation("SignalR Set Player Order for {PlayerId} in {GameId}", playerId, gameId);
                
                // Process synchronously for real-time response
                var updatedGameModel = _gameService.ExecuteAction(gameId, gsm => gsm.HandleSetPlayerOrder(message));
                
                // Notify all clients in game group instantly
                await Clients.Group(gameId).SendAsync("GameStateUpdated", updatedGameModel);
                
                // Notify command completion to original client
                await Clients.Caller.SendAsync("CommandCompleted", commandId, true, "Player order set successfully");
                
                _logger.LogDebug("Set Player Order completed successfully for game {GameId}", gameId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to execute Set Player Order for {PlayerId} in {GameId}", playerId, gameId);
                await Clients.Caller.SendAsync("CommandFailed", commandId, ex.Message);
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
                _logger.LogInformation("SignalR Players Doing Supplemental for {PlayerId} in {GameId}", playerId, gameId);
                
                // Process synchronously for real-time response
                var updatedGameModel = _gameService.ExecuteAction(gameId, gsm => gsm.HandlePlayersDoingSupplemental(message));
                
                // Notify all clients in game group instantly
                await Clients.Group(gameId).SendAsync("GameStateUpdated", updatedGameModel);
                
                // Notify command completion to original client
                await Clients.Caller.SendAsync("CommandCompleted", commandId, true, "Supplemental players set");
                
                _logger.LogDebug("Players Doing Supplemental completed successfully for game {GameId}", gameId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to execute Players Doing Supplemental for {PlayerId} in {GameId}", playerId, gameId);
                await Clients.Caller.SendAsync("CommandFailed", commandId, ex.Message);
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
                _logger.LogInformation("SignalR Balance Board for {PlayerId} in {GameId}", playerId, gameId);
                
                // Process synchronously for real-time response
                var updatedGameModel = _gameService.ExecuteAction(gameId, gsm => gsm.HandleBalanceBoard(message));
                
                // Notify all clients in game group instantly
                await Clients.Group(gameId).SendAsync("GameStateUpdated", updatedGameModel);
                
                // Notify command completion to original client
                await Clients.Caller.SendAsync("CommandCompleted", commandId, true, "Board balanced successfully");
                
                _logger.LogDebug("Balance Board completed successfully for game {GameId}", gameId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to execute Balance Board for {PlayerId} in {GameId}", playerId, gameId);
                await Clients.Caller.SendAsync("CommandFailed", commandId, ex.Message);
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
                _logger.LogInformation("SignalR Go First: {FirstPlayerId} for {PlayerId} in {GameId}", 
                    message.PlayerId, playerId, gameId);
                
                // Process synchronously for real-time response
                var updatedGameModel = _gameService.ExecuteAction(gameId, gsm => gsm.HandleGoFirst(message));
                
                // Notify all clients in game group instantly
                await Clients.Group(gameId).SendAsync("GameStateUpdated", updatedGameModel);
                
                // Notify command completion to original client
                await Clients.Caller.SendAsync("CommandCompleted", commandId, true, "Turn order set successfully");
                
                _logger.LogDebug("Go First completed successfully for game {GameId}", gameId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to execute Go First for {PlayerId} in {GameId}", playerId, gameId);
                await Clients.Caller.SendAsync("CommandFailed", commandId, ex.Message);
            }
        }

        #endregion

        #region Helper Methods

        /// <summary>
        /// Broadcasts a message to all clients in a game group
        /// </summary>
        /// <param name="gameId">The game ID</param>
        /// <param name="messageType">The message type</param>
        /// <param name="data">The message data</param>
        public async Task BroadcastToGame(string gameId, string messageType, object data)
        {
            await Clients.Group(gameId).SendAsync(messageType, data);
            _logger.LogDebug("Broadcasted {MessageType} to all clients in game {GameId}", messageType, gameId);
        }

        #endregion
    }
}