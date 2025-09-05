using System.Net.Http.Json;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.DependencyInjection;
using Catan3.Shared.Models;
using System.Text.Json;
using Catan3.Shared.Utility;

namespace Catan3.Shared.Services
{
    /// <summary>
    /// Game Service Proxy for Catan3 - provides a unified interface for both SignalR and REST API operations.
    /// Can be used by both test infrastructure and Desktop client applications.
    /// Handles connection management, command execution, event subscriptions, and REST API calls.
    /// </summary>
    public class GameServiceProxy : IAsyncDisposable
    {
        private readonly HubConnection _connection;
        private readonly HttpClient _httpClient;
        private string _playerId;
        private string? _gameId;
        private readonly Dictionary<string, TaskCompletionSource<CommandResult>> _pendingCommands = [];
        private readonly object _commandLock = new();
        
        /// <summary>
        /// The base URI for the game service REST API (e.g., "https://localhost:8080")
        /// </summary>
        public string ServiceUri { get; set; } = string.Empty;

        public HubConnection Connection => _connection;
        public string PlayerId 
        { 
            get => _playerId; 
            set => _playerId = value; 
        }
        public string? GameId => _gameId;
        public GameModel? GameModel { get; private set; }

        // Events for game state updates and command results
        public event Action<GameModel>? GameStateUpdated;
        public event Action<string, bool, string>? CommandCompleted;
        public event Action<string, string>? CommandFailed;
        public event Action<string, bool>? PlayerPresenceChanged;

        /// <summary>
        /// Creates a GameServiceProxy for the Catan3 GameHub with REST API support
        /// </summary>
        /// <param name="hubUrl">The SignalR hub URL (e.g., "https://localhost:7000/gameHub")</param>
        /// <param name="serviceUri">The base URI for REST API calls (e.g., "https://localhost:8080")</param>
        /// <param name="playerId">The player ID for this connection</param>
        /// <param name="gameId">Optional game ID to auto-join</param>
        public GameServiceProxy(string hubUrl, string serviceUri, string playerId, string? gameId = null)
        {
            _playerId = playerId;
            _gameId = gameId;
            ServiceUri = serviceUri;

            _connection = new HubConnectionBuilder()
                .WithUrl(hubUrl)
                .WithAutomaticReconnect()
                .AddJsonProtocol(options =>
                {
                    JsonHelper.ConfigureOptions(options.PayloadSerializerOptions);
                })
                .Build();

            // Create HttpClient with same lifetime as GameServiceProxy for optimal connection reuse
            // This HttpClient will be disposed in DisposeAsync() to ensure proper resource cleanup
            _httpClient = new HttpClient()
            {
                BaseAddress = new Uri(serviceUri)
            };
            SetupEventHandlers();
        }

        /// <summary>
        /// Creates a GameServiceProxy with custom HubConnection configuration
        /// (useful for testing with WebApplicationFactory)
        /// </summary>
        /// <param name="connectionBuilder">Pre-configured HubConnectionBuilder</param>
        /// <param name="serviceUri">The base URI for REST API calls (e.g., "https://localhost:8080")</param>
        /// <param name="playerId">The player ID for this connection</param>
        /// <param name="gameId">Optional game ID to auto-join</param>
        public GameServiceProxy(HubConnectionBuilder connectionBuilder, string serviceUri, string playerId, string? gameId = null)
        {
            _playerId = playerId;
            _gameId = gameId;
            ServiceUri = serviceUri;

            _connection = connectionBuilder
                .WithAutomaticReconnect()
                .AddJsonProtocol(options =>
                {
                    JsonHelper.ConfigureOptions(options.PayloadSerializerOptions);
                })
                .Build();

            _httpClient = new HttpClient()
            {
                BaseAddress = new Uri(serviceUri)
            };
            SetupEventHandlers();
        }

        /// <summary>
        /// Creates a GameServiceProxy for testing with WebApplicationFactory
        /// This constructor makes it easy to use test server handlers
        /// </summary>
        /// <param name="hubUrl">The SignalR hub URL</param>
        /// <param name="serviceUri">The base URI for REST API calls</param>
        /// <param name="testHandler">HttpMessageHandler from test factory (use factory.Server.CreateHandler())</param>
        /// <param name="playerId">The player ID for this connection</param>
        /// <param name="gameId">Optional game ID to auto-join</param>
        public GameServiceProxy(string hubUrl, string serviceUri, HttpMessageHandler testHandler, string playerId, string? gameId = null)
        {
            _playerId = playerId;
            _gameId = gameId;
            ServiceUri = serviceUri;

            _connection = new HubConnectionBuilder()
                .WithUrl(hubUrl, options =>
                {
                    options.HttpMessageHandlerFactory = _ => testHandler;
                })
                .WithAutomaticReconnect()
                .AddJsonProtocol(options =>
                {
                    JsonHelper.ConfigureOptions(options.PayloadSerializerOptions);
                })
                .Build();

            _httpClient = new HttpClient(testHandler)
            {
                BaseAddress = new Uri(serviceUri)
            };
            SetupEventHandlers();
        }

        #region Connection Management

        /// <summary>
        /// Connects to the SignalR hub and optionally joins a game
        /// </summary>
        public async Task ConnectAsync()
        {
            LogEvent("CONNECTING", "Starting SignalR connection");
            await _connection.StartAsync();
            LogEvent("CONNECTED", $"SignalR connection established, state: {_connection.State}");

            // Auto-join game if gameId provided
            if (!string.IsNullOrEmpty(_gameId))
            {
                LogEvent("AUTO_JOINING", $"Auto-joining game {_gameId}");
                await JoinGameAsync(_gameId);
            }
        }

        /// <summary>
        /// Joins a specific game and stores the gameId for future operations
        /// </summary>
        public async Task JoinGameAsync(string gameId)
        {
            LogEvent("JOINING_GAME", $"Calling JoinGame for game {gameId}");
            await _connection.InvokeAsync("JoinGame", gameId, _playerId);
            _gameId = gameId; // Store the gameId for future operations
            LogEvent("JOIN_CALLED", $"JoinGame call completed for game {gameId}");
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
        /// Executes an Undo command on the game service
        /// Uses the stored GameId from JoinGameAsync call.
        /// </summary>
        public async Task<CommandResult> ExecuteUndoAsync(TimeSpan? timeout = null)
        {
            if (string.IsNullOrEmpty(_gameId))
                throw new InvalidOperationException("Must join a game before executing actions. Call JoinGameAsync first.");
            
            if (string.IsNullOrEmpty(_gameId))
                throw new InvalidOperationException("Must join a game before executing actions. Call JoinGameAsync first.");

            await _connection.InvokeAsync("Undo", _gameId, _playerId);
            
            return new CommandResult
            {
                CommandId = Guid.NewGuid().ToString(),
                Success = true,
                Message = "Undo completed",
                Timestamp = DateTime.UtcNow
            };
        }

        /// <summary>
        /// Executes a Redo command on the game service
        /// Uses the stored GameId from JoinGameAsync call.
        /// </summary>
        public async Task<CommandResult> ExecuteRedoAsync(TimeSpan? timeout = null)
        {
            if (string.IsNullOrEmpty(_gameId))
                throw new InvalidOperationException("Must join a game before executing actions. Call JoinGameAsync first.");
            
            if (string.IsNullOrEmpty(_gameId))
                throw new InvalidOperationException("Must join a game before executing actions. Call JoinGameAsync first.");

            await _connection.InvokeAsync("Redo", _gameId, _playerId);
            
            return new CommandResult
            {
                CommandId = Guid.NewGuid().ToString(),
                Success = true,
                Message = "Redo completed",
                Timestamp = DateTime.UtcNow
            };
        }

        /// <summary>
        /// Executes a Next command on the game service
        /// Uses the stored GameId from JoinGameAsync call.
        /// </summary>
        public async Task<CommandResult> ExecuteNextAsync(TimeSpan? timeout = null)
        {
            if (string.IsNullOrEmpty(_gameId))
                throw new InvalidOperationException("Must join a game before executing actions. Call JoinGameAsync first.");
            
            if (string.IsNullOrEmpty(_gameId))
                throw new InvalidOperationException("Must join a game before executing actions. Call JoinGameAsync first.");

            await _connection.InvokeAsync("Next", _gameId, _playerId);
            
            return new CommandResult
            {
                CommandId = Guid.NewGuid().ToString(),
                Success = true,
                Message = "Next completed",
                Timestamp = DateTime.UtcNow
            };
        }

        /// <summary>
        /// Executes a Shuffle command on the game service
        /// Uses the stored GameId from JoinGameAsync call.
        /// </summary>
        public async Task<CommandResult> ExecuteShuffleAsync(TimeSpan? timeout = null)
        {
            if (string.IsNullOrEmpty(_gameId))
                throw new InvalidOperationException("Must join a game before executing actions. Call JoinGameAsync first.");
            
            await _connection.InvokeAsync("Shuffle", _gameId, _playerId);
            
            return new CommandResult
            {
                CommandId = Guid.NewGuid().ToString(),
                Success = true,
                Message = "Shuffle completed",
                Timestamp = DateTime.UtcNow
            };
        }

        /// <summary>
        /// Executes a Balance command on the game service
        /// Uses the stored GameId from JoinGameAsync call.
        /// </summary>
        public async Task<CommandResult> ExecuteBalanceAsync(TimeSpan? timeout = null)
        {
            if (string.IsNullOrEmpty(_gameId))
                throw new InvalidOperationException("Must join a game before executing actions. Call JoinGameAsync first.");
            
            if (string.IsNullOrEmpty(_gameId))
                throw new InvalidOperationException("Must join a game before executing actions. Call JoinGameAsync first.");

            await _connection.InvokeAsync("BalanceBoard", _gameId, _playerId);
            
            return new CommandResult
            {
                CommandId = Guid.NewGuid().ToString(),
                Success = true,
                Message = "BalanceBoard completed",
                Timestamp = DateTime.UtcNow
            };
        }


        /// <summary>
        /// Executes a Purchase command for entitlements
        /// Uses the stored GameId from JoinGameAsync call.
        /// </summary>
        public async Task<CommandResult> ExecutePurchaseAsync(Entitlement entitlement, TimeSpan? timeout = null)
        {
            if (string.IsNullOrEmpty(_gameId))
                throw new InvalidOperationException("Must join a game before executing actions. Call JoinGameAsync first.");
                
            var message = new PurchaseMessage(entitlement);
            await _connection.InvokeAsync("ExecutePurchase", _gameId, _playerId, message);
            
            return new CommandResult
            {
                CommandId = Guid.NewGuid().ToString(),
                Success = true,
                Message = $"Purchase {entitlement} sent",
                Timestamp = DateTime.UtcNow
            };
        }
        

        /// <summary>
        /// Executes a Road Purchase command
        /// </summary>
        public async Task<CommandResult> ExecuteRoadPurchaseAsync(string gameId, RoadKey roadKey, TimeSpan? timeout = null)
        {
            var message = new RoadPurchaseMessage(roadKey);
            await _connection.InvokeAsync("ExecuteRoadPurchase", gameId, _playerId, message);
            
            return new CommandResult
            {
                CommandId = Guid.NewGuid().ToString(),
                Success = true,
                Message = $"Road Purchase at {roadKey} sent",
                Timestamp = DateTime.UtcNow
            };
        }

        /// <summary>
        /// Executes a Building Upgrade command
        /// </summary>
        public async Task<CommandResult> ExecuteBuildingUpgradeAsync(string gameId, BuildingKey buildingKey, TimeSpan? timeout = null)
        {
            var message = new BuildingUpgradeMessage(buildingKey);
            await _connection.InvokeAsync("ExecuteBuildingUpgrade", gameId, _playerId, message);
            
            return new CommandResult
            {
                CommandId = Guid.NewGuid().ToString(),
                Success = true,
                Message = $"Building Upgrade at {buildingKey} sent",
                Timestamp = DateTime.UtcNow
            };
        }

        /// <summary>
        /// Executes a Move Robber command
        /// </summary>
        public async Task<CommandResult> ExecuteMoveRobberAsync(string gameId, HexCoordinates coordinates, string? targetPlayerId = null, TimeSpan? timeout = null)
        {
            var message = new MoveRobberMessage(coordinates, targetPlayerId);
            await _connection.InvokeAsync("ExecuteMoveRobber", gameId, _playerId, message);
            
            return new CommandResult
            {
                CommandId = Guid.NewGuid().ToString(),
                Success = true,
                Message = $"Move Robber to {coordinates} sent",
                Timestamp = DateTime.UtcNow
            };
        }

        /// <summary>
        /// Executes a Roll command
        /// Uses the stored GameId from JoinGameAsync call.
        /// </summary>
        public async Task<CommandResult> ExecuteRollAsync(int die1, int die2, TimeSpan? timeout = null)
        {
            if (string.IsNullOrEmpty(_gameId))
                throw new InvalidOperationException("Must join a game before executing actions. Call JoinGameAsync first.");
                
            var turnRollModel = new TurnRollModel(die1, die2);
            var message = new RollMessage(turnRollModel);
            await _connection.InvokeAsync("ExecuteRoll", _gameId, _playerId, message);
            
            return new CommandResult
            {
                CommandId = Guid.NewGuid().ToString(),
                Success = true,
                Message = $"Roll ({die1},{die2}) sent",
                Timestamp = DateTime.UtcNow
            };
        }
        

        /// <summary>
        /// Executes a Set Player Order command
        /// </summary>
        public async Task<CommandResult> ExecuteSetPlayerOrderAsync(string gameId, IList<string> playerIds, TimeSpan? timeout = null)
        {
            var message = new SetPlayerOrderMessage(playerIds);
            await _connection.InvokeAsync("ExecuteSetPlayerOrder", gameId, _playerId, message);
            
            return new CommandResult
            {
                CommandId = Guid.NewGuid().ToString(),
                Success = true,
                Message = "Set Player Order sent",
                Timestamp = DateTime.UtcNow
            };
        }


        /// <summary>
        /// Executes a Balance Board command
        /// </summary>
        public async Task<CommandResult> ExecuteBalanceBoardAsync(string gameId, TimeSpan? timeout = null)
        {
            var message = new BalanceBoardMessage();
            await _connection.InvokeAsync("ExecuteBalanceBoard", gameId, _playerId, message);
            
            return new CommandResult
            {
                CommandId = Guid.NewGuid().ToString(),
                Success = true,
                Message = "Balance Board sent",
                Timestamp = DateTime.UtcNow
            };
        }

        /// <summary>
        /// Executes a Go First command
        /// Uses the stored GameId from JoinGameAsync call.
        /// </summary>
        public async Task<CommandResult> ExecuteGoFirstAsync(string firstPlayerId, TimeSpan? timeout = null)
        {
            if (string.IsNullOrEmpty(_gameId))
                throw new InvalidOperationException("Must join a game before executing actions. Call JoinGameAsync first.");
                
            var message = new GoFirstMessage(firstPlayerId);
            await _connection.InvokeAsync("ExecuteGoFirst", _gameId, _playerId, message);
            
            return new CommandResult
            {
                CommandId = Guid.NewGuid().ToString(),
                Success = true,
                Message = $"Go First: {firstPlayerId} sent",
                Timestamp = DateTime.UtcNow
            };
        }
        
        /// <summary>
        /// Executes a Participating In Supplemental command
        /// Uses the stored GameId from JoinGameAsync call.
        /// </summary>
        public async Task<CommandResult> ExecuteParticipatingInSupplementalAsync(string playerId, bool participating, TimeSpan? timeout = null)
        {
            if (string.IsNullOrEmpty(_gameId))
                throw new InvalidOperationException("Must join a game before executing actions. Call JoinGameAsync first.");
                
            await _connection.InvokeAsync("ExecuteParticipatingInSupplemental", _gameId, playerId, participating);
            
            return new CommandResult
            {
                CommandId = Guid.NewGuid().ToString(),
                Success = true,
                Message = $"Participating In Supplemental: {playerId} - {participating} sent",
                Timestamp = DateTime.UtcNow
            };
        }
        

        /// <summary>
        /// Loads a GameModel directly via REST API - primarily for testing purposes
        /// </summary>
        public async Task<CommandResult> LoadGameModelAsync(GameModel gameModel, TimeSpan? timeout = null)
        {
            try
            {
                // Serialize GameModel to JSON string to avoid ASP.NET validation issues
                var gameModelJson = JsonSerializer.Serialize(gameModel, JsonHelper.StandardOptions);
                
                // Create LoadGameModelMessage with JSON string
                var loadGameModelMessage = new LoadGameModelMessage(gameModelJson);
                
                // Use REST API to load the game
                var response = await _httpClient.PostAsJsonAsync("/api/game/loadmodel", loadGameModelMessage);
                
                if (!response.IsSuccessStatusCode)
                {
                    var errorContent = await response.Content.ReadAsStringAsync();
                    return new CommandResult 
                    { 
                        Success = false, 
                        Message = $"Failed to load GameModel via REST: {response.StatusCode}. {errorContent}",
                        Timestamp = DateTime.UtcNow
                    };
                }
                
                // Parse response to get gameId
                var responseContent = await response.Content.ReadAsStringAsync();
                var responseJson = JsonDocument.Parse(responseContent);
                
                if (!responseJson.RootElement.TryGetProperty("gameId", out var gameIdElement))
                {
                    return new CommandResult 
                    { 
                        Success = false, 
                        Message = "LoadGameModel response missing gameId",
                        Timestamp = DateTime.UtcNow
                    };
                }
                
                var actualGameId = gameIdElement.GetString();
                if (string.IsNullOrEmpty(actualGameId))
                {
                    return new CommandResult 
                    { 
                        Success = false, 
                        Message = "LoadGameModel returned null/empty gameId",
                        Timestamp = DateTime.UtcNow
                    };
                }
                
                // Update our internal gameId and join the game via SignalR
                _gameId = actualGameId;
                await JoinGameAsync(actualGameId);
                
                return new CommandResult 
                { 
                    Success = true, 
                    Message = $"GameModel loaded successfully with GameId: {actualGameId}",
                    Timestamp = DateTime.UtcNow
                };
            }
            catch (Exception ex)
            {
                return new CommandResult 
                { 
                    Success = false, 
                    Message = $"Error loading GameModel: {ex.Message}",
                    Timestamp = DateTime.UtcNow
                };
            }
        }
        
        private string ExtractGameIdFromMessage(string message)
        {
            // Message format: "GameModel loaded successfully with GameId: a4066571-5ddc-4321-b68a-2920b8e9d53d"
            var prefix = "GameModel loaded successfully with GameId: ";
            if (message.StartsWith(prefix))
            {
                return message.Substring(prefix.Length);
            }
            throw new InvalidOperationException($"Could not extract GameId from message: {message}");
        }

        #endregion

        #region REST API Methods

        /// <summary>
        /// Creates a new game via REST API
        /// </summary>
        /// <param name="gameType">The type of game to create</param>
        /// <param name="hasSupplemental">Whether the game has supplemental build phase</param>
        /// <param name="playerNames">List of player names</param>
        /// <returns>The created game information</returns>
        public async Task<GameInfo> CreateGameAsync(GameType gameType, bool hasSupplemental, List<string> playerNames)
        {
            if (string.IsNullOrEmpty(ServiceUri))
                throw new InvalidOperationException("ServiceUri must be set before calling REST API methods");

            var createGameRequest = new
            {
                GameType = gameType.ToString(),
                HasSupplemental = hasSupplemental,
                PlayerNames = playerNames
            };

            var json = JsonHelper.Serialize(createGameRequest);
            var content = new StringContent(json, System.Text.Encoding.UTF8, "application/json");

            var response = await _httpClient.PostAsync($"{ServiceUri}/api/games", content);
            response.EnsureSuccessStatusCode();

            var responseJson = await response.Content.ReadAsStringAsync();
            return JsonHelper.Deserialize<GameInfo>(responseJson) 
                ?? throw new InvalidOperationException("Failed to deserialize CreateGame response");
        }

        /// <summary>
        /// Gets all available games via REST API
        /// </summary>
        /// <returns>List of available games</returns>
        public async Task<List<GameInfo>> GetAvailableGamesAsync()
        {
            if (string.IsNullOrEmpty(ServiceUri))
                throw new InvalidOperationException("ServiceUri must be set before calling REST API methods");

            var response = await _httpClient.GetAsync($"{ServiceUri}/api/companion/games");
            response.EnsureSuccessStatusCode();

            var responseJson = await response.Content.ReadAsStringAsync();
            
            // The API returns a wrapper object: { success: true, games: [...], count: N, timestamp: "..." }
            var apiResponse = JsonHelper.Deserialize<GetAvailableGamesResponse>(responseJson)
                ?? throw new InvalidOperationException("Failed to deserialize GetAvailableGames response");
                
            return apiResponse.Games;
        }

        /// <summary>
        /// Gets a specific game via REST API
        /// </summary>
        /// <param name="gameId">The game ID to retrieve</param>
        /// <returns>The game information</returns>
        public async Task<GameInfo> GetGameAsync(string gameId)
        {
            if (string.IsNullOrEmpty(ServiceUri))
                throw new InvalidOperationException("ServiceUri must be set before calling REST API methods");

            var response = await _httpClient.GetAsync($"{ServiceUri}/api/games/{gameId}");
            response.EnsureSuccessStatusCode();

            var responseJson = await response.Content.ReadAsStringAsync();
            return JsonHelper.Deserialize<GameInfo>(responseJson) 
                ?? throw new InvalidOperationException("Failed to deserialize GetGame response");
        }

        #endregion

        #region Utility Methods

        /// <summary>
        /// Waits for a specific game state to be reached
        /// </summary>
        public async Task<GameModel> WaitForGameStateAsync(Catan3.Shared.Models.GameState expectedState, TimeSpan? timeout = null)
        {
            timeout ??= System.Diagnostics.Debugger.IsAttached ? TimeSpan.FromHours(1) : TimeSpan.FromSeconds(60);

            // Check if we already have the expected state
            if (GameModel?.GameState == expectedState)
            {
                LogEvent("ALREADY_IN_STATE", $"Already in expected state {expectedState}");
                return GameModel;
            }

            var stateReachedTcs = new TaskCompletionSource<GameModel>();
            var startTime = DateTime.UtcNow;
            var endTime = startTime.Add(timeout.Value);

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
                var currentState = GameModel?.GameState.ToString() ?? "null";
                LogEvent("WAITING_FOR_STATE", $"Waiting for {expectedState}, currently {currentState}");

                while (DateTime.UtcNow < endTime)
                {
                    var remainingTime = endTime - DateTime.UtcNow;
                    var waitTime = remainingTime > TimeSpan.FromSeconds(5) ? TimeSpan.FromSeconds(5) : remainingTime;
                    
                    try
                    {
                        var result = await stateReachedTcs.Task.WaitAsync(waitTime);
                        return result;
                    }
                    catch (TimeoutException)
                    {
                        var elapsed = DateTime.UtcNow - startTime;
                        currentState = GameModel?.GameState.ToString() ?? "null";
                        LogEvent("STILL_WAITING", $"Still waiting for {expectedState}, currently {currentState} after {elapsed.TotalSeconds:F1}s");
                    }
                }

                // Final timeout
                var totalElapsed = DateTime.UtcNow - startTime;
                currentState = GameModel?.GameState.ToString() ?? "null";
                LogEvent("TIMEOUT", $"TIMEOUT after {totalElapsed.TotalSeconds:F1}s waiting for {expectedState}, still {currentState}");

                throw new TimeoutException($"Timed out waiting for game state {expectedState} after {totalElapsed.TotalSeconds:F1} seconds. Current state: {currentState}");
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
            timeout ??= System.Diagnostics.Debugger.IsAttached ? TimeSpan.FromHours(1) : TimeSpan.FromSeconds(10);
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
            // Game state updates - use async handler to prevent deadlocks
            _connection.On<GameModel>("GameStateUpdated", async gameModel =>
            {
                LogEvent("CLIENT_RECEIVED", "Received GameStateUpdated message");
                
                GameModel = gameModel;
                
                // Trace the update with gameId and playerId
                var gameState = gameModel?.GameState.ToString() ?? "null";
                LogEvent("GAME_STATE_UPDATED", $"Received GameState: {gameState}");
                
                if (gameModel != null)
                {
                    LogEvent("CLIENT_INVOKING", "Invoking GameStateUpdated event");
                    // Fire and forget to prevent blocking SignalR receive thread
                    _ = Task.Run(() => GameStateUpdated?.Invoke(gameModel));
                }
                else
                {
                    LogEvent("CLIENT_NULL", "Received null gameModel");
                }
                
                await Task.CompletedTask; // Make it properly async
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

                // Complete the specific pending command that matches this commandId
                TaskCompletionSource<CommandResult>? pendingCommand = null;
                
                lock (_commandLock)
                {
                    if (_pendingCommands.TryGetValue(commandId, out pendingCommand))
                    {
                        _pendingCommands.Remove(commandId);
                    }
                }
                
                // Complete command and invoke events outside of lock to prevent deadlocks
                if (pendingCommand != null)
                {
                    pendingCommand.TrySetResult(result);
                }
                
                try
                {
                    CommandCompleted?.Invoke(commandId, success, message);
                }
                catch (Exception ex)
                {
                    LogEvent("EVENT_HANDLER_ERROR", $"Error in CommandCompleted event handler: {ex.Message}");
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

                // Complete the specific pending command that matches this commandId with failure
                TaskCompletionSource<CommandResult>? pendingCommand = null;
                
                lock (_commandLock)
                {
                    if (_pendingCommands.TryGetValue(commandId, out pendingCommand))
                    {
                        _pendingCommands.Remove(commandId);
                    }
                }
                
                // Complete command and invoke events outside of lock to prevent deadlocks
                if (pendingCommand != null)
                {
                    pendingCommand.TrySetResult(result);
                }
                
                try
                {
                    CommandFailed?.Invoke(commandId, error);
                }
                catch (Exception ex)
                {
                    LogEvent("EVENT_HANDLER_ERROR", $"Error in CommandFailed event handler: {ex.Message}");
                }
            });

            // Player presence changes
            _connection.On<string, bool>("PlayerPresenceChanged", (playerId, isOnline) =>
            {
                LogEvent("PRESENCE_RECEIVED", $"Received PlayerPresenceChanged: {playerId} -> {isOnline}");
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

        #region Helper Methods

        /// <summary>
        /// Centralized logging method that handles debugger vs console output
        /// </summary>
        private void LogEvent(string eventType, string message)
        {
            var logMessage = $"[GameServiceProxy][{eventType}] GameId: {_gameId ?? "null"}, PlayerId: {_playerId} - {message}";
            if (System.Diagnostics.Debugger.IsAttached)
                System.Diagnostics.Debug.WriteLine(logMessage);
            else
                Console.WriteLine(logMessage);
        }

        #endregion

        #region IAsyncDisposable

        /// <summary>
        /// Disposes the SignalR connection and HTTP client properly.
        /// Resource cleanup strategy: HttpClient has same lifetime as GameServiceProxy
        /// to maximize connection pooling and reuse benefits for production scenarios.
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
            
            // Dispose HttpClient to properly close TCP connections and release resources
            _httpClient.Dispose();
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

    /// <summary>
    /// Response format for the /api/companion/games endpoint
    /// </summary>
    public class GetAvailableGamesResponse
    {
        public bool Success { get; set; }
        public List<GameInfo> Games { get; set; } = [];
        public int Count { get; set; }
        public string Timestamp { get; set; } = "";
    }
}
