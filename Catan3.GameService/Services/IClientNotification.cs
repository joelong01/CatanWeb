using Catan3.Shared.Models;

namespace Catan3.GameService.Services
{
    /// <summary>
    /// Interface for client notification system.
    /// Handles real-time updates to clients waiting for game state changes.
    /// </summary>
    public interface IClientNotification
    {
        /// <summary>
        /// Notifies all clients waiting for updates on the specified game.
        /// </summary>
        /// <param name="gameId">The game ID to notify clients for</param>
        /// <param name="gameModel">The updated game model to send to clients</param>
        Task NotifyAsync(string gameId, GameModel gameModel);

        /// <summary>
        /// Waits for a notification on the specified game.
        /// Used by hanging GET endpoints to wait for game state changes.
        /// </summary>
        /// <param name="gameId">The game ID to wait for updates on</param>
        /// <param name="clientId">Unique identifier for this client connection</param>
        /// <param name="currentVersion">The client's current version number</param>
        /// <param name="cancellationToken">Cancellation token for timeout handling</param>
        /// <returns>The updated game model when available</returns>
        Task<GameModel> WaitForNotificationAsync(string gameId, string clientId, int currentVersion, CancellationToken cancellationToken);

        /// <summary>
        /// Sets the current game state for new clients connecting to the hanging GET.
        /// This allows the hanging GET endpoint to have the current state available.
        /// </summary>
        /// <param name="gameId">The game ID to set current state for</param>
        /// <param name="gameModel">The current game model</param>
        void SetCurrentGameState(string gameId, GameModel gameModel);
    }
}