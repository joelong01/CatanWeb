using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;
using System.Text.Json;
using Catan3.Shared.Models;
using Catan3.Shared.Utility;

namespace Tests.GameService.SignalR
{
    /// <summary>
    /// Enhanced SignalR client wrapper for testing with comprehensive logging and abstraction.
    /// Provides automatic logging of GameState updates with userID, timestamps, and response times.
    /// Eliminates duplicate code across tests and offers better event handling.
    /// </summary>
    public class TestSignalRClient : IAsyncDisposable
    {
        private readonly HubConnection _connection;
        private readonly string _playerId;
        private readonly string? _gameId;
        private readonly List<GameStateUpdate> _receivedUpdates = new();
        private readonly List<CommandResult> _commandResults = new();
        private readonly Dictionary<string, CommandCompletionTracker> _pendingCommands = new();
        private readonly object _logLock = new();

        public HubConnection Connection => _connection;
        public string PlayerId => _playerId;
        public string? GameId => _gameId;
        public IReadOnlyList<GameStateUpdate> ReceivedUpdates => _receivedUpdates.AsReadOnly();
        public IReadOnlyList<CommandResult> CommandResults => _commandResults.AsReadOnly();
        public GameModel? LastGameState { get; private set; }

        /// <summary>
        /// Creates a TestSignalRClient with comprehensive logging and event handling
        /// </summary>
        /// <param name="factory">The web application factory</param>
        /// <param name="playerId">The player ID for this client</param>
        /// <param name="gameId">Optional game ID to auto-join</param>
        public TestSignalRClient(WebApplicationFactory<Program> factory, string playerId, string? gameId = null)
        {
            _playerId = playerId;
            _gameId = gameId;

            var uri = factory.Server.BaseAddress ?? new Uri("http://localhost");
            var hubUrl = new Uri(uri, "/gameHub").ToString();

            _connection = new HubConnectionBuilder()
                .WithUrl(hubUrl, options =>
                {
                    options.HttpMessageHandlerFactory = _ => factory.Server.CreateHandler();
                })
                .Build();

            SetupEventHandlers();
        }

        /// <summary>
        /// Connects to the SignalR hub and optionally joins a game
        /// </summary>
        public async Task ConnectAsync()
        {
            await _connection.StartAsync();
            LogEvent("Connected", $"SignalR connection established for {_playerId}");

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
            LogEvent("JoinGame", $"{_playerId} joined game {gameId}");
        }

        /// <summary>
        /// Executes a DoAction command with automatic logging and completion tracking
        /// </summary>
        public async Task<GameModel?> ExecuteDoActionAsync(string gameId, GameAction action, TimeSpan? timeout = null)
        {
            timeout ??= TimeSpan.FromSeconds(10);
            var commandId = Guid.NewGuid().ToString();
            var startTime = DateTime.UtcNow;

            LogEvent("ExecuteDoAction", $"{_playerId} executing {action} in game {gameId}");

            var completionTcs = new TaskCompletionSource<bool>();

            // Track this completion source for the permanent handlers to signal
            var completionTracker = new CommandCompletionTracker(completionTcs);
            
            // Store the tracker temporarily (use a static dictionary or instance field)
            lock (_logLock)
            {
                _pendingCommands[commandId] = completionTracker;
            }

            try
            {
                var message = new DoAction(action);
                await _connection.InvokeAsync("ExecuteDoAction", gameId, _playerId, message);

                var completed = await completionTcs.Task.WaitAsync(timeout.Value);
                
                if (!completed)
                {
                    LogEvent("ExecuteDoAction", $"?? {_playerId} DoAction {action} timed out after {timeout.Value.TotalSeconds}s");
                    throw new TimeoutException($"DoAction {action} timed out after {timeout.Value.TotalSeconds} seconds");
                }

                var success = await completionTcs.Task;
                if (!success)
                {
                    throw new InvalidOperationException($"DoAction {action} failed");
                }

                LogEvent("ExecuteDoAction", $"? {_playerId} DoAction {action} completed successfully");
                return LastGameState;
            }
            catch (Exception ex)
            {
                LogEvent("ExecuteDoAction", $"? {_playerId} DoAction {action} failed: {ex.Message}");
                throw;
            }
            finally
            {
                // Clean up the completion tracker
                lock (_logLock)
                {
                    _pendingCommands.Remove(commandId);
                }
            }
        }

        /// <summary>
        /// Executes a Purchase command with automatic logging and completion tracking
        /// </summary>
        public async Task<GameModel?> ExecutePurchaseAsync(string gameId, Entitlement entitlement, TimeSpan? timeout = null)
        {
            timeout ??= TimeSpan.FromSeconds(10);
            var startTime = DateTime.UtcNow;

            LogEvent("ExecutePurchase", $"{_playerId} purchasing {entitlement} in game {gameId}");

            var completionTcs = new TaskCompletionSource<bool>();
            var completionTracker = new CommandCompletionTracker(completionTcs);
            var commandId = Guid.NewGuid().ToString();
            
            lock (_logLock)
            {
                _pendingCommands[commandId] = completionTracker;
            }

            try
            {
                var message = new PurchaseMessage(entitlement);
                await _connection.InvokeAsync("ExecutePurchase", gameId, _playerId, message);

                var completed = await completionTcs.Task.WaitAsync(timeout.Value);
                
                if (!completed)
                {
                    LogEvent("ExecutePurchase", $"?? {_playerId} Purchase {entitlement} timed out after {timeout.Value.TotalSeconds}s");
                    throw new TimeoutException($"Purchase {entitlement} timed out after {timeout.Value.TotalSeconds} seconds");
                }

                var success = await completionTcs.Task;
                if (!success)
                {
                    throw new InvalidOperationException($"Purchase {entitlement} failed");
                }

                LogEvent("ExecutePurchase", $"? {_playerId} Purchase {entitlement} completed");
                return LastGameState;
            }
            catch (Exception ex)
            {
                LogEvent("ExecutePurchase", $"? {_playerId} Purchase {entitlement} failed: {ex.Message}");
                throw;
            }
            finally
            {
                lock (_logLock)
                {
                    _pendingCommands.Remove(commandId);
                }
            }
        }

        /// <summary>
        /// Executes a Roll command with automatic logging and completion tracking
        /// </summary>
        public async Task<GameModel?> ExecuteRollAsync(string gameId, int die1, int die2, TimeSpan? timeout = null)
        {
            timeout ??= TimeSpan.FromSeconds(10);
            var startTime = DateTime.UtcNow;

            LogEvent("ExecuteRoll", $"{_playerId} rolling dice ({die1},{die2}) in game {gameId}");

            var completionTcs = new TaskCompletionSource<bool>();
            var completionTracker = new CommandCompletionTracker(completionTcs);
            var commandId = Guid.NewGuid().ToString();
            
            lock (_logLock)
            {
                _pendingCommands[commandId] = completionTracker;
            }

            try
            {
                var turnRollModel = new TurnRollModel(die1, die2);
                var message = new RollMessage(turnRollModel);
                await _connection.InvokeAsync("ExecuteRoll", gameId, _playerId, message);

                var completed = await completionTcs.Task.WaitAsync(timeout.Value);
                
                if (!completed)
                {
                    LogEvent("ExecuteRoll", $"?? {_playerId} Roll ({die1},{die2}) timed out after {timeout.Value.TotalSeconds}s");
                    throw new TimeoutException($"Roll ({die1},{die2}) timed out after {timeout.Value.TotalSeconds} seconds");
                }

                var success = await completionTcs.Task;
                if (!success)
                {
                    throw new InvalidOperationException($"Roll ({die1},{die2}) failed");
                }

                LogEvent("ExecuteRoll", $"? {_playerId} Roll ({die1},{die2}) completed");
                return LastGameState;
            }
            catch (Exception ex)
            {
                LogEvent("ExecuteRoll", $"? {_playerId} Roll ({die1},{die2}) failed: {ex.Message}");
                throw;
            }
            finally
            {
                lock (_logLock)
                {
                    _pendingCommands.Remove(commandId);
                }
            }
        }

        /// <summary>
        /// Waits for a specific game state to be reached
        /// </summary>
        public async Task<GameModel> WaitForGameStateAsync(GameState expectedState, TimeSpan? timeout = null)
        {
            timeout ??= TimeSpan.FromSeconds(3); // Reduced from 5 seconds!
            var startTime = DateTime.UtcNow;

            // Check if we already have the expected state
            if (LastGameState?.GameState == expectedState)
            {
                // Only log if different from what we already have
                return LastGameState;
            }

            LogEvent("WaitForGameState", $"{_playerId} waiting for game state {expectedState}");

            var stateReachedTcs = new TaskCompletionSource<GameModel>();

            // Set up a temporary handler to wait for the specific state
            void StateHandler(GameModel gameModel)
            {
                if (gameModel.GameState == expectedState)
                {
                    stateReachedTcs.TrySetResult(gameModel);
                }
            }

            // Add temporary handler via the existing GameStateUpdated subscription
            _connection.On<GameModel>("GameStateUpdated", StateHandler);

            try
            {
                var result = await stateReachedTcs.Task.WaitAsync(timeout.Value);
                var elapsed = DateTime.UtcNow - startTime;
                
                // Only log if it took a meaningful amount of time
                if (elapsed.TotalMilliseconds > 50)
                {
                    LogEvent("WaitForGameState", $"? {_playerId} reached state {expectedState} after {elapsed.TotalMilliseconds:F0}ms");
                }
                
                return result;
            }
            catch (TimeoutException)
            {
                var elapsed = DateTime.UtcNow - startTime;
                var currentState = LastGameState?.GameState.ToString() ?? "Unknown";
                LogEvent("WaitForGameState", $"?? {_playerId} timed out waiting for {expectedState} after {elapsed.TotalMilliseconds:F0}ms (current: {currentState})");
                throw new TimeoutException($"Timed out waiting for game state {expectedState}. Current state: {currentState}");
            }
            finally
            {
                // Clean up temporary handler
                _connection.Remove("GameStateUpdated");
            }
        }

        /// <summary>
        /// Sets up comprehensive event handlers with logging
        /// </summary>
        private void SetupEventHandlers()
        {
            // Game state updates with comprehensive logging
            _connection.On<GameModel>("GameStateUpdated", gameModel =>
            {
                var timestamp = DateTime.UtcNow;
                var previousState = LastGameState?.GameState;
                LastGameState = gameModel;

                lock (_logLock)
                {
                    var update = new GameStateUpdate
                    {
                        Timestamp = timestamp,
                        PlayerId = _playerId,
                        GameId = gameModel.GameId,
                        GameState = gameModel.GameState,
                        PreviousGameState = previousState,
                        CurrentPlayerId = gameModel.CurrentPlayerId,
                        Version = gameModel.GameStateMachineVersion
                    };
                    _receivedUpdates.Add(update);

                    // Enhanced logging with state transition info and GameHash
                    var stateTransition = previousState.HasValue && previousState.Value != gameModel.GameState 
                        ? $" (transition: {previousState} ? {gameModel.GameState})"
                        : "";
                    
                    var hashDisplay = string.IsNullOrEmpty(gameModel.GameHash) ? "no-hash" : gameModel.GameHash;
                    
                    LogEvent("GameStateUpdated", 
                        $"?? {_playerId} received update: {gameModel.GameState}{stateTransition} " +
                        $"| CurrentPlayer: {gameModel.CurrentPlayerId} | Hash: {hashDisplay}");
                }
            });

            // Command completion
            _connection.On<string, bool, string>("CommandCompleted", (commandId, success, message) =>
            {
                lock (_logLock)
                {
                    var result = new CommandResult
                    {
                        Timestamp = DateTime.UtcNow,
                        PlayerId = _playerId,
                        CommandId = commandId,
                        Success = success,
                        Message = message
                    };
                    _commandResults.Add(result);

                    var status = success ? "?" : "?";
                    LogEvent("CommandCompleted", $"{status} {_playerId} command {commandId}: {message}");
                    
                    // Signal any pending command completion trackers
                    foreach (var tracker in _pendingCommands.Values)
                    {
                        tracker.CompletionSource.TrySetResult(success);
                    }
                }
            });

            // Command failure
            _connection.On<string, string>("CommandFailed", (commandId, error) =>
            {
                lock (_logLock)
                {
                    var result = new CommandResult
                    {
                        Timestamp = DateTime.UtcNow,
                        PlayerId = _playerId,
                        CommandId = commandId,
                        Success = false,
                        Message = error
                    };
                    _commandResults.Add(result);

                    LogEvent("CommandFailed", $"? {_playerId} command {commandId} failed: {error}");
                    
                    // Signal any pending command completion trackers
                    foreach (var tracker in _pendingCommands.Values)
                    {
                        tracker.CompletionSource.TrySetResult(false);
                    }
                }
            });

            // Connection events - using the correct event handlers
            _connection.Reconnecting += (exception) =>
            {
                LogEvent("Connection", $"?? {_playerId} SignalR reconnecting...");
                return Task.CompletedTask;
            };

            _connection.Reconnected += (connectionId) =>
            {
                LogEvent("Connection", $"? {_playerId} SignalR reconnected");
                return Task.CompletedTask;
            };

            _connection.Closed += (exception) =>
            {
                LogEvent("Connection", $"?? {_playerId} SignalR connection closed");
                return Task.CompletedTask;
            };
        }

        /// <summary>
        /// Logs events with consistent formatting and timestamps
        /// </summary>
        private void LogEvent(string eventType, string message)
        {
            var timestamp = DateTime.UtcNow.ToString("HH:mm:ss.fff");
            Console.WriteLine($"[{timestamp}] [{eventType}] {message}");
        }

        /// <summary>
        /// Disposes the SignalR connection properly
        /// </summary>
        public async ValueTask DisposeAsync()
        {
            if (_connection.State == HubConnectionState.Connected)
            {
                LogEvent("Dispose", $"?? {_playerId} disconnecting...");
                await _connection.StopAsync();
            }
            await _connection.DisposeAsync();
            LogEvent("Dispose", $"? {_playerId} disposed");
        }
    }

    /// <summary>
    /// Represents a game state update with comprehensive tracking info
    /// </summary>
    public class GameStateUpdate
    {
        public DateTime Timestamp { get; set; }
        public string PlayerId { get; set; } = "";
        public string GameId { get; set; } = "";
        public GameState GameState { get; set; }
        public GameState? PreviousGameState { get; set; }
        public string? CurrentPlayerId { get; set; }
        public int Version { get; set; }
    }

    /// <summary>
    /// Represents a command result with tracking info
    /// </summary>
    public class CommandResult
    {
        public DateTime Timestamp { get; set; }
        public string PlayerId { get; set; } = "";
        public string CommandId { get; set; } = "";
        public bool Success { get; set; }
        public string Message { get; set; } = "";
    }

    /// <summary>
    /// Internal class for tracking command completion
    /// </summary>
    internal class CommandTracker
    {
        public string CommandId { get; }
        public DateTime StartTime { get; }
        public TaskCompletionSource<bool> CompletionSource { get; }

        public CommandTracker(string commandId, DateTime startTime, TaskCompletionSource<bool> completionSource)
        {
            CommandId = commandId;
            StartTime = startTime;
            CompletionSource = completionSource;
        }
    }

    /// <summary>
    /// Internal class for tracking command completion without command ID matching
    /// </summary>
    internal class CommandCompletionTracker
    {
        public TaskCompletionSource<bool> CompletionSource { get; }

        public CommandCompletionTracker(TaskCompletionSource<bool> completionSource)
        {
            CompletionSource = completionSource;
        }
    }

    /// <summary>
    /// Enhanced static helper methods that work with TestSignalRClient
    /// </summary>
    public static class SignalRTestHelper
    {
        /// <summary>
        /// Creates a TestSignalRClient with automatic connection and optional game joining
        /// </summary>
        public static async Task<TestSignalRClient> CreateTestClientAsync(
            WebApplicationFactory<Program> factory, 
            string playerId,
            string? gameId = null)
        {
            var client = new TestSignalRClient(factory, playerId, gameId);
            await client.ConnectAsync();
            return client;
        }

        /// <summary>
        /// Creates multiple TestSignalRClient instances for multi-player testing
        /// </summary>
        public static async Task<List<TestSignalRClient>> CreateMultipleClientsAsync(
            WebApplicationFactory<Program> factory,
            string gameId,
            params string[] playerIds)
        {
            var clients = new List<TestSignalRClient>();

            foreach (var playerId in playerIds)
            {
                var client = await CreateTestClientAsync(factory, playerId, gameId);
                clients.Add(client);
            }

            return clients;
        }

        /// <summary>
        /// Creates a game and returns a connected client (backward compatibility)
        /// </summary>
        public static async Task<(string gameId, TestSignalRClient client)> CreateGameWithClientAsync(
            WebApplicationFactory<Program> factory,
            string playerId = "Alice",
            GameType gameType = GameType.Regular)
        {
            // Create game via REST API
            var httpClient = factory.CreateClient();
            var gameId = await CreateGameViaRest(httpClient, gameType);

            // Create and connect SignalR client
            var client = await CreateTestClientAsync(factory, playerId, gameId);

            return (gameId, client);
        }

        /// <summary>
        /// Creates a game via REST API (helper method)
        /// </summary>
        private static async Task<string> CreateGameViaRest(HttpClient httpClient, GameType gameType)
        {
            // Use realistic player counts: 3 for Regular, 5 for Expansion
            var playerIds = gameType == GameType.Expansion 
                ? new[] { "Alice", "Bob", "Charlie", "David", "Eve" } 
                : new[] { "Alice", "Bob", "Charlie" }; // 3 players for Regular games
            
            var newGameRequest = new 
            { 
                gameType = gameType.ToString(), 
                playerIds = playerIds
            };
            
            var newGameJson = JsonSerializer.Serialize(newGameRequest);
            var newGameContent = new StringContent(newGameJson, System.Text.Encoding.UTF8, "application/json");
            
            var newGameResponse = await httpClient.PostAsync("/api/game/new", newGameContent);

            if (!newGameResponse.IsSuccessStatusCode)
            {
                var errorContent = await newGameResponse.Content.ReadAsStringAsync();
                throw new InvalidOperationException($"Failed to create game: {newGameResponse.StatusCode}. Error: {errorContent}");
            }

            var newGameBody = await newGameResponse.Content.ReadAsStringAsync();
            var newGameResult = JsonSerializer.Deserialize<JsonElement>(newGameBody);
            
            if (!newGameResult.TryGetProperty("gameId", out var gameIdElement))
            {
                throw new InvalidOperationException("Game creation did not return gameId");
            }
            
            return gameIdElement.GetString() ?? 
                throw new InvalidOperationException("Game creation returned null gameId");
        }

        /// <summary>
        /// Backward compatibility method for existing tests
        /// </summary>
        public static async Task<HubConnection> CreateTestConnection(
            WebApplicationFactory<Program> factory, 
            string? gameId = null, 
            string? playerId = null)
        {
            playerId ??= "TestPlayer";
            var client = await CreateTestClientAsync(factory, playerId, gameId);
            return client.Connection;
        }

        /// <summary>
        /// Backward compatibility method for creating games in specific states
        /// </summary>
        public static async Task<(string gameId, HubConnection connection)> CreateGameInStateViaSignalR(
            WebApplicationFactory<Program> factory,
            GameState targetState = GameState.PickingBoard,
            string playerId = "Alice")
        {
            // Use the proper StateProgression.AdvanceToState method which handles ALL states consistently
            return await StateProgression.AdvanceToState(factory, targetState, new[] { playerId, "Bob", "Charlie" });
        }

        /// <summary>
        /// Backward compatibility method for executing actions
        /// </summary>
        public static async Task<GameModel?> ExecuteDoActionViaSignalR(
            HubConnection connection, 
            string gameId, 
            string playerId, 
            GameAction action, 
            TimeSpan? timeout = null)
        {
            // This method exists for backward compatibility
            // New tests should use TestSignalRClient directly for better logging
            timeout ??= TimeSpan.FromSeconds(10);
            
            GameModel? updatedGameModel = null;
            var completionTcs = new TaskCompletionSource<bool>();

            connection.On<GameModel>("GameStateUpdated", gameModel =>
            {
                updatedGameModel = gameModel;
            });

            connection.On<string, bool, string>("CommandCompleted", (commandId, success, message) =>
            {
                completionTcs.SetResult(success);
            });

            connection.On<string, string>("CommandFailed", (commandId, error) =>
            {
                completionTcs.SetResult(false);
            });

            var message = new DoAction(action);
            await connection.InvokeAsync("ExecuteDoAction", gameId, playerId, message);

            var completed = await completionTcs.Task.WaitAsync(timeout.Value);
            
            if (!completed)
            {
                throw new TimeoutException($"DoAction {action} timed out after {timeout.Value.TotalSeconds} seconds");
            }

            return updatedGameModel;
        }

        /// <summary>
        /// Backward compatibility method for executing rolls
        /// </summary>
        public static async Task<GameModel?> ExecuteRollViaSignalR(
            HubConnection connection, 
            string gameId, 
            string playerId, 
            ValidCatanRoll roll, 
            TimeSpan? timeout = null)
        {
            timeout ??= TimeSpan.FromSeconds(10);
            
            GameModel? updatedGameModel = null;
            var completionTcs = new TaskCompletionSource<bool>();

            connection.On<GameModel>("GameStateUpdated", gameModel =>
            {
                updatedGameModel = gameModel;
            });

            connection.On<string, bool, string>("CommandCompleted", (commandId, success, message) =>
            {
                completionTcs.SetResult(success);
            });

            connection.On<string, string>("CommandFailed", (commandId, error) =>
            {
                completionTcs.SetResult(false);
            });

            // Calculate individual dice that sum to rollValue
            int rollValue = (int)roll;
            int die1, die2;
            if (rollValue <= 7)
            {
                die1 = Math.Min(rollValue - 1, 6);
                die2 = rollValue - die1;
            }
            else
            {
                die1 = Math.Max(rollValue - 6, 1);
                die2 = rollValue - die1;
            }

            var turnRollModel = new TurnRollModel(die1, die2);
            var message = new RollMessage(turnRollModel);
            await connection.InvokeAsync("ExecuteRoll", gameId, playerId, message);

            var completed = await completionTcs.Task.WaitAsync(timeout.Value);
            
            if (!completed)
            {
                throw new TimeoutException($"Roll {roll} timed out after {timeout.Value.TotalSeconds} seconds");
            }

            return updatedGameModel;
        }

        /// <summary>
        /// Backward compatibility method for executing purchases
        /// </summary>
        public static async Task<GameModel?> ExecutePurchaseViaSignalR(
            HubConnection connection, 
            string gameId, 
            string playerId, 
            Entitlement entitlement, 
            TimeSpan? timeout = null)
        {
            timeout ??= TimeSpan.FromSeconds(10);
            
            GameModel? updatedGameModel = null;
            var completionTcs = new TaskCompletionSource<bool>();

            connection.On<GameModel>("GameStateUpdated", gameModel =>
            {
                updatedGameModel = gameModel;
            });

            connection.On<string, bool, string>("CommandCompleted", (commandId, success, message) =>
            {
                completionTcs.SetResult(success);
            });

            connection.On<string, string>("CommandFailed", (commandId, error) =>
            {
                completionTcs.SetResult(false);
            });

            var message = new PurchaseMessage(entitlement);
            await connection.InvokeAsync("ExecutePurchase", gameId, playerId, message);

            var completed = await completionTcs.Task.WaitAsync(timeout.Value);
            
            if (!completed)
            {
                throw new TimeoutException($"Purchase {entitlement} timed out after {timeout.Value.TotalSeconds} seconds");
            }

            return updatedGameModel;
        }

        /// <summary>
        /// Disposes a connection properly (backward compatibility)
        /// </summary>
        public static async Task DisposeConnection(HubConnection connection)
        {
            if (connection.State == HubConnectionState.Connected)
            {
                await connection.StopAsync();
            }
            await connection.DisposeAsync();
        }
    }
}