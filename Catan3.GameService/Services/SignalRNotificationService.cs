using Microsoft.AspNetCore.SignalR;
using Catan3.Shared.Models;
using Catan3.GameService.Hubs;

namespace Catan3.GameService.Services
{
    /// <summary>
    /// SignalR-based implementation of IClientNotification for real-time game updates.
    /// Provides instant push notifications via WebSockets instead of hanging GET requests.
    /// </summary>
    public class SignalRNotificationService : IClientNotification
    {
        private readonly IHubContext<GameHub> _hubContext;
        private readonly ILogger<SignalRNotificationService> _logger;
        private readonly Dictionary<string, GameModel> _gameStates = new();
        private readonly object _stateLock = new();

        public SignalRNotificationService(IHubContext<GameHub> hubContext, ILogger<SignalRNotificationService> logger)
        {
            _hubContext = hubContext;
            _logger = logger;
        }

        /// <summary>
        /// Notifies all clients in a game group about state changes via SignalR
        /// </summary>
        /// <param name="gameId">The game ID to notify</param>
        /// <param name="gameModel">The updated game model</param>
        public async Task NotifyAsync(string gameId, GameModel gameModel)
        {
            // Store the latest game state
            lock (_stateLock)
            {
                _gameStates[gameId] = gameModel;
            }

            // Push update to all clients in the game group
            await _hubContext.Clients.Group(gameId).SendAsync("GameStateUpdated", gameModel);
            
            _logger.LogDebug("Pushed game state update to all clients in game {GameId} via SignalR", gameId);
        }

        /// <summary>
        /// For SignalR, this method is not used as clients receive updates via push notifications.
        /// Maintained for interface compatibility but throws NotSupportedException.
        /// </summary>
        public Task<GameModel> WaitForNotificationAsync(string gameId, string clientId, int currentVersion, CancellationToken cancellationToken)
        {
            throw new NotSupportedException("SignalR uses push notifications. Use SignalR client connection instead of waiting for notifications.");
        }

        /// <summary>
        /// Sets the current game state for new clients joining the game
        /// </summary>
        /// <param name="gameId">The game ID</param>
        /// <param name="gameModel">The current game model</param>
        public void SetCurrentGameState(string gameId, GameModel gameModel)
        {
            lock (_stateLock)
            {
                _gameStates[gameId] = gameModel;
            }
            
            _logger.LogDebug("Set current game state for game {GameId}", gameId);
        }

        /// <summary>
        /// Gets the current game state for a specific game
        /// </summary>
        /// <param name="gameId">The game ID</param>
        /// <returns>The current game model or null if not found</returns>
        public GameModel? GetCurrentGameState(string gameId)
        {
            lock (_stateLock)
            {
                return _gameStates.TryGetValue(gameId, out var gameModel) ? gameModel : null;
            }
        }

        /// <summary>
        /// Notifies clients about command completion
        /// </summary>
        /// <param name="gameId">The game ID</param>
        /// <param name="commandId">The command ID that completed</param>
        /// <param name="success">Whether the command succeeded</param>
        /// <param name="message">Optional message about the command result</param>
        public async Task NotifyCommandCompletedAsync(string gameId, Guid commandId, bool success, string? message = null)
        {
            await _hubContext.Clients.Group(gameId).SendAsync("CommandCompleted", commandId, success, message);
            _logger.LogDebug("Notified command completion for command {CommandId} in game {GameId}: {Success}", 
                commandId, gameId, success);
        }

        /// <summary>
        /// Notifies clients about command failure
        /// </summary>
        /// <param name="gameId">The game ID</param>
        /// <param name="commandId">The command ID that failed</param>
        /// <param name="error">The error message</param>
        public async Task NotifyCommandFailedAsync(string gameId, Guid commandId, string error)
        {
            await _hubContext.Clients.Group(gameId).SendAsync("CommandFailed", commandId, error);
            _logger.LogDebug("Notified command failure for command {CommandId} in game {GameId}: {Error}", 
                commandId, gameId, error);
        }
    }
}