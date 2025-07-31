using Microsoft.AspNetCore.SignalR.Client;
using Catan3.Shared.Models;
using System.Text.Json;
using Catan3.Shared.Utility;

namespace Catan3.Shared.Services
{
    /// <summary>
    /// SignalR Proxy for Catan3 GameHub - provides a clean, strongly-typed interface for all SignalR operations.
    /// Can be used by both test infrastructure and Desktop client applications.
    /// Handles connection management, command execution, and event subscriptions.
    /// </summary>
    public class SignalRProxy : IAsyncDisposable
    {
        private readonly HubConnection _connection;
        private readonly string _playerId;
        private readonly string? _gameId;
        private readonly Dictionary<string, TaskCompletionSource<CommandResult>> _pendingCommands = new();
        private readonly object _commandLock = new();

        public HubConnection Connection => _connection;
        public string PlayerId => _playerId;
        public string? GameId => _gameId;
        public GameModel? LastGameState { get; private set; }

        // Events for game state updates and command results
        public event Action<GameModel>? GameStateUpdated;
        public event Action<string, bool, string>? CommandCompleted;
        public event Action<string, string>? CommandFailed;
        public event Action<string, bool>? PlayerPresenceChanged;

        /// <summary>
        /// Creates a SignalRProxy for the Catan3 GameHub
        /// </summary>
        /// <param name="hubUrl">The SignalR hub URL (e.g., "https://localhost:7000/gameHub")</param>
        /// <param name="playerId">The player ID for this connection</param>
        /// <param name="gameId">Optional game ID to auto-join</param>
        public SignalRProxy(string hubUrl, string playerId, string? gameId = null)
        {
            _playerId = playerId;
            _gameId = gameId;

            _connection = new HubConnectionBuilder()
                .WithUrl(hubUrl)
                .WithAutomaticReconnect()
                .Build();

            SetupEventHandlers();
        }

        /// <summary>
        /// Creates a SignalRProxy with custom HubConnection configuration
        /// (useful for testing with WebApplicationFactory)
        /// </summary>
        /// <param name="connectionBuilder">Pre-configured HubConnectionBuilder</param>
        /// <param name="playerId">The player ID for this connection</param>
        /// <param name="gameId">Optional game ID to auto-join</param>
        public SignalRProxy(HubConnectionBuilder connectionBuilder, string playerId, string? gameId = null)
        {
            _playerId = playerId;
            _gameId = gameId;

            _connection = connectionBuilder
                .WithAutomaticReconnect()
                .Build();

            SetupEventHandlers();
        }

        /// <summary>
        /// Creates a SignalRProxy for testing with WebApplicationFactory
        /// This constructor makes it easy to use test server handlers
        /// </summary>
        /// <param name="hubUrl">The SignalR hub URL</param>
        /// <param name="testHandler">HttpMessageHandler from test factory (use factory.Server.CreateHandler())</param>
        /// <param name="playerId">The player ID for this connection</param>
        /// <param name="gameId">Optional game ID to auto-join</param>
        public SignalRProxy(string hubUrl, HttpMessageHandler testHandler, string playerId, string? gameId = null)
        {
            _playerId = playerId;
            _gameId = gameId;

            _connection = new HubConnectionBuilder()
                .WithUrl(hubUrl, options =>
                {
                    options.HttpMessageHandlerFactory = _ => testHandler;
                })
                .WithAutomaticReconnect()
                .Build();

            SetupEventHandlers();
        }

        #region Connection Management

        /// <summary>
        /// Connects to the SignalR hub and optionally joins a game
        /// </summary>
        public async Task ConnectAsync()
        {
            await _connection.StartAsync();

            // Auto-join game if gameId provided
            if (!string.IsNullOrEmpty(_gameId))
            {
                await JoinGameAsync(_gameId);
            }
        }

        /// <summary>
        /// Joins a specific game
        /// </summary>
        public async Task JoinGameAsync(string gameId)
        {
            await _connection.InvokeAsync("JoinGame", gameId, _playerId);
        }

        /// <summary>
        /// Leaves a specific game
        /// </summary>
        public async Task LeaveGameAsync(string gameId)
        {
            await _connection.InvokeAsync("LeaveGame", gameId, _playerId);
        }

        #endregion

        #region SignalR Hub Method Wrappers

        /// <summary>
        /// Executes a DoAction command (Shuffle, Undo, Redo, Next, Balance)
        /// </summary>
        public async Task<CommandResult> ExecuteDoActionAsync(string gameId, GameAction action, TimeSpan? timeout = null)
        {
            var message = new DoAction(action);
            return await ExecuteCommandAsync(
                () => _connection.InvokeAsync("ExecuteDoAction", gameId, _playerId, message),
                $"DoAction {action}",
                timeout
            );
        }

        /// <summary>
        /// Executes a Purchase command for entitlements
        /// </summary>
        public async Task<CommandResult> ExecutePurchaseAsync(string gameId, Entitlement entitlement, TimeSpan? timeout = null)
        {
            var message = new PurchaseMessage(entitlement);
            return await ExecuteCommandAsync(
                () => _connection.InvokeAsync("ExecutePurchase", gameId, _playerId, message),
                $"Purchase {entitlement}",
                timeout
            );
        }

        /// <summary>
        /// Executes a Road Purchase command
        /// </summary>
        public async Task<CommandResult> ExecuteRoadPurchaseAsync(string gameId, RoadKey roadKey, TimeSpan? timeout = null)
        {
            var message = new RoadPurchaseMessage(roadKey);
            return await ExecuteCommandAsync(
                () => _connection.InvokeAsync("ExecuteRoadPurchase", gameId, _playerId, message),
                $"Road Purchase at {roadKey}",
                timeout
            );
        }

        /// <summary>
        /// Executes a Building Upgrade command
        /// </summary>
        public async Task<CommandResult> ExecuteBuildingUpgradeAsync(string gameId, BuildingKey buildingKey, TimeSpan? timeout = null)
        {
            var message = new BuildingUpgradeMessage(buildingKey);
            return await ExecuteCommandAsync(
                () => _connection.InvokeAsync("ExecuteBuildingUpgrade", gameId, _playerId, message),
                $"Building Upgrade at {buildingKey}",
                timeout
            );
        }

        /// <summary>
        /// Executes a Move Robber command
        /// </summary>
        public async Task<CommandResult> ExecuteMoveRobberAsync(string gameId, HexCoordinates coordinates, string? targetPlayerId = null, TimeSpan? timeout = null)
        {
            var message = new MoveRobberMessage(coordinates, targetPlayerId);
            return await ExecuteCommandAsync(
                () => _connection.InvokeAsync("ExecuteMoveRobber", gameId, _playerId, message),
                $"Move Robber to {coordinates}",
                timeout
            );
        }

        /// <summary>
        /// Executes a Roll command
        /// </summary>
        public async Task<CommandResult> ExecuteRollAsync(string gameId, int die1, int die2, TimeSpan? timeout = null)
        {
            var turnRollModel = new TurnRollModel(die1, die2);
            var message = new RollMessage(turnRollModel);
            return await ExecuteCommandAsync(
                () => _connection.InvokeAsync("ExecuteRoll", gameId, _playerId, message),
                $"Roll ({die1},{die2})",
                timeout
            );
        }

        /// <summary>
        /// Executes a Set Player Order command
        /// </summary>
        public async Task<CommandResult> ExecuteSetPlayerOrderAsync(string gameId, IList<string> playerIds, TimeSpan? timeout = null)
        {
            var message = new SetPlayerOrderMessage(playerIds);
            return await ExecuteCommandAsync(
                () => _connection.InvokeAsync("ExecuteSetPlayerOrder", gameId, _playerId, message),
                "Set Player Order",
                timeout
            );
        }

        /// <summary>
        /// Executes a Players Doing Supplemental command
        /// </summary>
        public async Task<CommandResult> ExecutePlayersDoingSupplementalAsync(string gameId, IList<string> playerIds, TimeSpan? timeout = null)
        {
            var message = new PlayersDoingSupplemental(playerIds);
            return await ExecuteCommandAsync(
                () => _connection.InvokeAsync("ExecutePlayersDoingSupplemental", gameId, _playerId, message),
                "Players Doing Supplemental",
                timeout
            );
        }

        /// <summary>
        /// Executes a Balance Board command
        /// </summary>
        public async Task<CommandResult> ExecuteBalanceBoardAsync(string gameId, TimeSpan? timeout = null)
        {
            var message = new BalanceBoardMessage();
            return await ExecuteCommandAsync(
                () => _connection.InvokeAsync("ExecuteBalanceBoard", gameId, _playerId, message),
                "Balance Board",
                timeout
            );
        }

        /// <summary>
        /// Executes a Go First command
        /// </summary>
        public async Task<CommandResult> ExecuteGoFirstAsync(string gameId, string firstPlayerId, TimeSpan? timeout = null)
        {
            var message = new GoFirstMessage(firstPlayerId);
            return await ExecuteCommandAsync(
                () => _connection.InvokeAsync("ExecuteGoFirst", gameId, _playerId, message),
                $"Go First: {firstPlayerId}",
                timeout
            );
        }

        #endregion

        #region Utility Methods

        /// <summary>
        /// Waits for a specific game state to be reached
        /// </summary>
        public async Task<GameModel> WaitForGameStateAsync(GameState expectedState, TimeSpan? timeout = null)
        {
            timeout ??= TimeSpan.FromSeconds(10);

            // Check if we already have the expected state
            if (LastGameState?.GameState == expectedState)
            {
                return LastGameState;
            }

            var stateReachedTcs = new TaskCompletionSource<GameModel>();

            void StateHandler(GameModel gameModel)
            {
                if (gameModel.GameState == expectedState)
                {
                    stateReachedTcs.TrySetResult(gameModel);
                }
            }

            GameStateUpdated += StateHandler;

            try
            {
                var result = await stateReachedTcs.Task.WaitAsync(timeout.Value);
                return result;
            }
            finally
            {
                GameStateUpdated -= StateHandler;
            }
        }

        /// <summary>
        /// Generic command execution with completion tracking
        /// </summary>
        private async Task<CommandResult> ExecuteCommandAsync(Func<Task> hubInvoke, string commandDescription, TimeSpan? timeout = null)
        {
            timeout ??= TimeSpan.FromSeconds(10);
            var commandId = Guid.NewGuid().ToString();
            var completionTcs = new TaskCompletionSource<CommandResult>();

            lock (_commandLock)
            {
                _pendingCommands[commandId] = completionTcs;
            }

            try
            {
                await hubInvoke();

                var result = await completionTcs.Task.WaitAsync(timeout.Value);
                return result;
            }
            catch (TimeoutException)
            {
                throw new TimeoutException($"{commandDescription} timed out after {timeout.Value.TotalSeconds} seconds");
            }
            finally
            {
                lock (_commandLock)
                {
                    _pendingCommands.Remove(commandId);
                }
            }
        }

        #endregion

        #region Event Handlers Setup

        /// <summary>
        /// Sets up SignalR event handlers
        /// </summary>
        private void SetupEventHandlers()
        {
            // Game state updates
            _connection.On<GameModel>("GameStateUpdated", gameModel =>
            {
                LastGameState = gameModel;
                GameStateUpdated?.Invoke(gameModel);
            });

            // Command completion
            _connection.On<string, bool, string>("CommandCompleted", (commandId, success, message) =>
            {
                var result = new CommandResult
                {
                    CommandId = commandId,
                    Success = success,
                    Message = message,
                    Timestamp = DateTime.UtcNow
                };

                CommandCompleted?.Invoke(commandId, success, message);

                // Complete any pending commands (simplified - in production you'd match by commandId)
                lock (_commandLock)
                {
                    foreach (var pending in _pendingCommands.Values)
                    {
                        pending.TrySetResult(result);
                    }
                    _pendingCommands.Clear();
                }
            });

            // Command failure
            _connection.On<string, string>("CommandFailed", (commandId, error) =>
            {
                var result = new CommandResult
                {
                    CommandId = commandId,
                    Success = false,
                    Message = error,
                    Timestamp = DateTime.UtcNow
                };

                CommandFailed?.Invoke(commandId, error);

                // Complete any pending commands with failure
                lock (_commandLock)
                {
                    foreach (var pending in _pendingCommands.Values)
                    {
                        pending.TrySetResult(result);
                    }
                    _pendingCommands.Clear();
                }
            });

            // Player presence changes
            _connection.On<string, bool>("PlayerPresenceChanged", (playerId, isOnline) =>
            {
                PlayerPresenceChanged?.Invoke(playerId, isOnline);
            });

            // Connection events
            _connection.Reconnecting += (exception) =>
            {
                // Handle reconnection logic if needed
                return Task.CompletedTask;
            };

            _connection.Reconnected += (connectionId) =>
            {
                // Handle reconnection completion if needed
                return Task.CompletedTask;
            };

            _connection.Closed += (exception) =>
            {
                // Handle connection closure if needed
                return Task.CompletedTask;
            };
        }

        #endregion

        #region IAsyncDisposable

        /// <summary>
        /// Disposes the SignalR connection properly
        /// </summary>
        public async ValueTask DisposeAsync()
        {
            if (_connection.State == HubConnectionState.Connected)
            {
                if (!string.IsNullOrEmpty(_gameId))
                {
                    await LeaveGameAsync(_gameId);
                }
                await _connection.StopAsync();
            }
            await _connection.DisposeAsync();
        }

        #endregion
    }

    /// <summary>
    /// Represents the result of a SignalR command execution
    /// </summary>
    public class CommandResult
    {
        public string CommandId { get; set; } = "";
        public bool Success { get; set; }
        public string Message { get; set; } = "";
        public DateTime Timestamp { get; set; }
    }
}
