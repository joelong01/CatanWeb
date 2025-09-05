using Catan3.Shared.Models;
using Catan3.Shared.Services;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Xunit.Abstractions;

namespace Tests.GameService
{
    /// <summary>
    /// Represents a single client in the multi-client test scenario.
    /// Creates its own SignalR connection and provides Tasks that complete when updates arrive.
    /// </summary>
    public class TestClient : IDisposable
    {
        private readonly string _playerId;
        private readonly string _hubUrl;
        private readonly string _serviceUri;
        private readonly string _gameId;
        private readonly HttpMessageHandler _testHandler;
        private readonly ITestOutputHelper _output;

        private GameModel _currentGameModel;
        private GameServiceProxy? _proxy;
        private TaskCompletionSource<GameModel>? _currentUpdateTask;
        private readonly object _gameModelLock = new();
        private volatile bool _disposed = false;

        public TestClient(string playerId, GameModel initialGameModel, string hubUrl, string serviceUri, string gameId, HttpMessageHandler testHandler, ITestOutputHelper output)
        {
            _playerId = playerId;
            _hubUrl = hubUrl;
            _serviceUri = serviceUri;
            _gameId = gameId;
            _testHandler = testHandler;
            _output = output;
            _currentGameModel = initialGameModel;
        }

        public string PlayerId => _playerId;

        public GameModel CurrentGameModel
        {
            get
            {
                lock (_gameModelLock)
                {
                    return _currentGameModel;
                }
            }
        }

        /// <summary>
        /// Starts the TestClient by connecting to SignalR and joining the game
        /// </summary>
        public async Task StartAsync()
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(TestClient));

            _output.WriteLine($"[TestClient-{_playerId}] Starting SignalR connection");

            // Create GameServiceProxy
            _proxy = new GameServiceProxy(_hubUrl, _serviceUri, _testHandler, _playerId, _gameId);
            _proxy.GameStateUpdated += OnGameStateUpdated;

            // Connect and join game
            await _proxy.ConnectAsync();
            _output.WriteLine($"[TestClient-{_playerId}] Connected to SignalR");

            await _proxy.JoinGameAsync(_gameId);
            _output.WriteLine($"[TestClient-{_playerId}] Joined game {_gameId} and listening for updates");
        }

        /// <summary>
        /// Returns a Task that will be completed when the next GameStateUpdated message arrives
        /// </summary>
        public Task<GameModel> GetUpdateTaskAsync()
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(TestClient));

            _currentUpdateTask = new TaskCompletionSource<GameModel>();
            _output.WriteLine($"[TestClient-{_playerId}] Created update task");
            return _currentUpdateTask.Task;
        }

        private void OnGameStateUpdated(GameModel gameModel)
        {
            if (_disposed) return;

            lock (_gameModelLock)
            {
                _currentGameModel = gameModel;
            }

            _output.WriteLine($"[TestClient-{_playerId}] GameStateUpdated received: GameHash={gameModel.GameHash}, GameState={gameModel.GameState}");

            // Complete the current update task
            _currentUpdateTask?.SetResult(gameModel);
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;

            _output.WriteLine($"[TestClient-{_playerId}] Disposing");

            // Cancel any pending task if it's not already completed
            if (_currentUpdateTask != null && !_currentUpdateTask.Task.IsCompleted)
            {
                _currentUpdateTask.SetCanceled();
            }

            // Clean up proxy
            if (_proxy != null)
            {
                try
                {
                    _proxy.GameStateUpdated -= OnGameStateUpdated;
                    _proxy.DisposeAsync().AsTask().Wait(TimeSpan.FromSeconds(5));
                    _output.WriteLine($"[TestClient-{_playerId}] SignalR connection disposed");
                }
                catch (Exception ex)
                {
                    _output.WriteLine($"[TestClient-{_playerId}] Error disposing proxy: {ex.Message}");
                }
            }
        }
    }
}
