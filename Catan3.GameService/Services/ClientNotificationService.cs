using System.Collections.Concurrent;
using System.Text.Json;
using Catan3.Shared.Models;

namespace Catan3.GameService.Services
{
    /// <summary>
    /// Manages client notifications for real-time game updates.
    /// Maintains separate client managers for each game to handle multiple clients efficiently.
    /// </summary>
    public class ClientNotificationService : IClientNotification
    {
        private readonly ConcurrentDictionary<string, ClientManager> _gameClientManagers = new();
        private readonly ILogger<ClientNotificationService> _logger;

        public ClientNotificationService(ILogger<ClientNotificationService> logger)
        {
            _logger = logger;
        }

        public async Task NotifyAsync(string gameId, GameModel gameModel)
        {
            if (_gameClientManagers.TryGetValue(gameId, out var clientManager))
            {
                await clientManager.NotifyClientsAsync(gameModel);
                _logger.LogDebug("Notified clients for gameId: {GameId}", gameId);
            }
            else
            {
                _logger.LogDebug("No clients waiting for gameId: {GameId}", gameId);
            }
        }

        public async Task<GameModel> WaitForNotificationAsync(string gameId, string clientId, int currentVersion, CancellationToken cancellationToken)
        {
            var clientManager = _gameClientManagers.GetOrAdd(gameId, _ => new ClientManager(_logger, gameId));

            try
            {
                return await clientManager.WaitForUpdateAsync(clientId, currentVersion, cancellationToken);
            }
            catch (OperationCanceledException)
            {
                _logger.LogDebug("Client {ClientId} wait cancelled for gameId: {GameId}", clientId, gameId);
                throw;
            }
        }

        /// <summary>
        /// Sets the current game state for immediate return to clients with older versions.
        /// This allows clients with older versions to get the current state immediately.
        /// </summary>
        /// <param name="gameId">The game ID to set current state for</param>
        /// <param name="gameModel">The current game model</param>
        public void SetCurrentGameState(string gameId, GameModel gameModel)
        {
            var clientManager = _gameClientManagers.GetOrAdd(gameId, _ => new ClientManager(_logger, gameId));
            clientManager.SetCurrentGameState(gameModel);
        }
    }

    /// <summary>
    /// Manages clients waiting for updates for a specific game.
    /// Handles serialization, storage, and notification of game state changes.
    /// </summary>
    internal class ClientManager
    {
        private readonly ConcurrentDictionary<string, TaskCompletionSource<GameModel>> _waitingClients = new();
        private readonly object _notificationLock = new();
        private readonly ILogger _logger;
        private readonly string _gameId;
        private GameModel? _latestGameModel;
        private string? _latestGameModelJson;

        public ClientManager(ILogger logger, string gameId)
        {
            _logger = logger;
            _gameId = gameId;
        }

        public void SetCurrentGameState(GameModel gameModel)
        {
            lock (_notificationLock)
            {
                _latestGameModel = gameModel;
                _latestGameModelJson = JsonSerializer.Serialize(gameModel);
                // Version tracking removed - we don't use version for hanging GET anymore
                _logger.LogDebug("Set current game state for gameId: {GameId}", _gameId);
            }
        }

        public async Task NotifyClientsAsync(GameModel gameModel)
        {
            List<TaskCompletionSource<GameModel>> clientsToNotify;

            lock (_notificationLock)
            {
                // Store the latest game state
                _latestGameModel = gameModel;
                _latestGameModelJson = JsonSerializer.Serialize(gameModel);

                // Get all waiting clients
                clientsToNotify = _waitingClients.Values.ToList();
                _waitingClients.Clear();

                _logger.LogDebug("Notifying {Count} clients for gameId: {GameId}", clientsToNotify.Count, _gameId);
            }

            // Notify all waiting clients outside of lock
            foreach (var tcs in clientsToNotify)
            {
                // Each client gets their own copy of the GameModel
                var clientGameModel = JsonSerializer.Deserialize<GameModel>(_latestGameModelJson!)
                    ?? throw new InvalidOperationException("Failed to deserialize GameModel for client notification");

                tcs.SetResult(clientGameModel);
            }

            await Task.CompletedTask;
        }

        public async Task<GameModel> WaitForUpdateAsync(string clientId, int currentVersion, CancellationToken cancellationToken)
        {
            // Note: We ignore currentVersion since Version is constant (software version = 1)
            // The hanging GET pattern waits for ANY GameModel change, not version comparison
            // This follows the infinite loop pattern where client continuously polls until GameState == GameOver

            // Always wait for notification - no immediate return based on version
            var tcs = new TaskCompletionSource<GameModel>();

            _waitingClients[clientId] = tcs;
            _logger.LogDebug("Client {ClientId} waiting for updates on gameId: {GameId} (version comparison disabled - waiting for any change)", clientId, _gameId);

            // Set up cancellation
            cancellationToken.Register(() =>
            {
                if (_waitingClients.TryRemove(clientId, out var removedTcs))
                {
                    removedTcs.SetCanceled();
                    _logger.LogDebug("Client {ClientId} wait cancelled for gameId: {GameId}", clientId, _gameId);
                }
            });

            try
            {
                return await tcs.Task;
            }
            finally
            {
                // Clean up if still in dictionary
                _waitingClients.TryRemove(clientId, out _);
            }
        }
    }
}