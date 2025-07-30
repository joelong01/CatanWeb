using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.AspNetCore.Mvc.Testing;
using System.Text.Json;
using Catan3.Shared.Models;
using Catan3.Shared.Utility;
using System.Net.Http;
using System.Threading.Tasks;
using Tests.GameService.SignalR;

namespace Tests.GameService.SignalR
{
    /// <summary>
    /// Logging levels for state progression control
    /// </summary>
    public enum LogLevel
    {
        Silent,    // No output except failures
        Summary,   // Key actions and results only  
        Detailed,  // Include timing and state transitions
        Debug      // Full SignalR logging (current behavior)
    }

    /// <summary>
    /// Enhanced state progression helper with multi-client support and controllable logging.
    /// Efficiently advances games to any target state for isolated testing with realistic player counts.
    /// Each method corresponds to a specific state in the GameController.
    /// </summary>
    public static class StateProgression
    {
        /// <summary>
        /// Advances a game through the complete sequence to reach the target state with all players connected.
        /// Uses realistic player counts: 3 for Regular, 5 for Expansion.
        /// </summary>
        public static async Task<MultiPlayerTestSession> AdvanceToStateWithAllPlayers(
            WebApplicationFactory<Program> factory,
            GameState targetState,
            GameType gameType = GameType.Regular,
            LogLevel logLevel = LogLevel.Summary)
        {
            // Use correct player counts
            var playerIds = gameType == GameType.Expansion 
                ? new[] { "Alice", "Bob", "Charlie", "David", "Eve" }
                : new[] { "Alice", "Bob", "Charlie" };

            return await AdvanceToStateInternal(factory, targetState, playerIds, logLevel);
        }

        /// <summary>
        /// Legacy single-client method for backward compatibility
        /// </summary>
        public static async Task<(string gameId, HubConnection connection)> AdvanceToState(
            WebApplicationFactory<Program> factory,
            GameState targetState,
            string[] playerIds = null!)
        {
            playerIds ??= new[] { "Alice", "Bob", "Charlie" }; // Fixed to use 3 players

            if (targetState == GameState.PickingBoard)
            {
                // Create a new game (starts in PickingBoard)
                var (gameId, client) = await SignalRTestHelper.CreateGameWithClientAsync(factory, playerIds[0]);
                return (gameId, client.Connection);
            }

            Log("⚠️ Warning: Using legacy single-client StateProgression. Consider AdvanceToStateWithAllPlayers for better testing.", LogLevel.Summary);
            
            // Use the enhanced multi-client version but return only the first connection
            var session = await AdvanceToStateWithAllPlayers(factory, targetState, GameType.Regular, LogLevel.Silent);
            return (session.GameId, session.GetClient("Alice").Connection);
        }

        /// <summary>
        /// Internal method that implements the enhanced multi-client state progression
        /// </summary>
        private static async Task<MultiPlayerTestSession> AdvanceToStateInternal(
            WebApplicationFactory<Program> factory,
            GameState targetState,
            string[] playerIds,
            LogLevel logLevel)
        {
            var gameType = playerIds.Length == 5 ? GameType.Expansion : GameType.Regular;
            var session = new MultiPlayerTestSession(factory, gameType, playerIds, logLevel);
            
            Log($"🎯 Advancing to {targetState} with {playerIds.Length} players: {string.Join(", ", playerIds)}", logLevel);
            var startTime = DateTime.UtcNow;

            try
            {
                // Create game and connect all players
                await session.InitializeAsync();
                
                // Follow the exact state progression from GameController
                switch (targetState)
                {
                    case GameState.PickingBoard:
                        // Already there after initialization
                        break;

                    case GameState.WaitingForRollForOrder:
                        await AdvancePickingBoard(session, logLevel);
                        break;

                    case GameState.FinishedRollOrder:
                        await AdvancePickingBoard(session, logLevel);
                        await AdvanceWaitingForRollForOrder(session, logLevel);
                        break;

                    case GameState.BeginResourceAllocation:
                        await AdvancePickingBoard(session, logLevel);
                        await AdvanceWaitingForRollForOrder(session, logLevel);
                        await AdvanceFinishedRollOrder(session, logLevel);
                        break;

                    case GameState.AllocateResourceForward:
                        await AdvancePickingBoard(session, logLevel);
                        await AdvanceWaitingForRollForOrder(session, logLevel);
                        await AdvanceFinishedRollOrder(session, logLevel);
                        await AdvanceBeginResourceAllocation(session, logLevel);
                        break;

                    case GameState.AllocateResourceReverse:
                        await AdvanceToAllocateResourceForward(session, logLevel);
                        await CompleteForwardAllocation(session, logLevel);
                        break;

                    case GameState.DoneResourceAllocation:
                        await AdvanceToAllocateResourceForward(session, logLevel);
                        await CompleteForwardAllocation(session, logLevel);
                        await CompleteReverseAllocation(session, logLevel);
                        break;

                    case GameState.WaitingForRoll:
                        await AdvanceToAllocateResourceForward(session, logLevel);
                        await CompleteForwardAllocation(session, logLevel);
                        await CompleteReverseAllocation(session, logLevel);
                        await AdvanceDoneResourceAllocation(session, logLevel);
                        break;

                    case GameState.WaitingForNext:
                        await AdvanceToAllocateResourceForward(session, logLevel);
                        await CompleteForwardAllocation(session, logLevel);
                        await CompleteReverseAllocation(session, logLevel);
                        await AdvanceDoneResourceAllocation(session, logLevel);
                        await AdvanceWaitingForRoll(session, logLevel);
                        break;

                    default:
                        throw new NotSupportedException($"State {targetState} progression not implemented");
                }

                // Verify all clients are in the target state
                await session.VerifyAllClientsInState(targetState);
                
                var elapsed = DateTime.UtcNow - startTime;
                Log($"✅ Successfully reached {targetState} with all {playerIds.Length} clients verified in {elapsed.TotalSeconds:F1}s", logLevel);
                
                return session;
            }
            catch (Exception ex)
            {
                var elapsed = DateTime.UtcNow - startTime;
                Log($"❌ Failed to reach {targetState} after {elapsed.TotalSeconds:F1}s: {ex.Message}", LogLevel.Summary);
                await session.DisposeAsync();
                throw;
            }
        }

        // Individual state advancement methods with LogLevel control

        private static async Task AdvancePickingBoard(MultiPlayerTestSession session, LogLevel logLevel)
        {
            if (logLevel >= LogLevel.Detailed) Log("🔄 PickingBoard → WaitingForRollForOrder", logLevel);
            await session.ExecuteActionWithVerification("Alice", GameAction.Next);
            if (logLevel >= LogLevel.Detailed) Log("✅ Advanced from PickingBoard", logLevel);
        }

        private static async Task AdvanceWaitingForRollForOrder(MultiPlayerTestSession session, LogLevel logLevel)
        {
            if (logLevel >= LogLevel.Detailed) Log("🔄 WaitingForRollForOrder → FinishedRollOrder", logLevel);
            await session.ExecuteActionWithVerification("Alice", GameAction.Next);
            if (logLevel >= LogLevel.Detailed) Log("✅ Advanced from WaitingForRollForOrder", logLevel);
        }

        private static async Task AdvanceFinishedRollOrder(MultiPlayerTestSession session, LogLevel logLevel)
        {
            if (logLevel >= LogLevel.Detailed) Log("🔄 FinishedRollOrder → BeginResourceAllocation", logLevel);
            await session.ExecuteActionWithVerification("Alice", GameAction.Next);
            if (logLevel >= LogLevel.Detailed) Log("✅ Advanced from FinishedRollOrder", logLevel);
        }

        private static async Task AdvanceBeginResourceAllocation(MultiPlayerTestSession session, LogLevel logLevel)
        {
            if (logLevel >= LogLevel.Detailed) Log("🔄 BeginResourceAllocation → AllocateResourceForward", logLevel);
            await session.ExecuteActionWithVerification("Alice", GameAction.Next);
            if (logLevel >= LogLevel.Detailed) Log("✅ Advanced from BeginResourceAllocation", logLevel);
        }

        private static async Task AdvanceToAllocateResourceForward(MultiPlayerTestSession session, LogLevel logLevel)
        {
            if (logLevel >= LogLevel.Summary) Log("🔄 Progressing through intermediate states to AllocateResourceForward...", logLevel);
            
            await AdvancePickingBoard(session, LogLevel.Silent);
            await AdvanceWaitingForRollForOrder(session, LogLevel.Silent);
            await AdvanceFinishedRollOrder(session, LogLevel.Silent);
            await AdvanceBeginResourceAllocation(session, logLevel); // Full logging for final step
        }

        private static async Task CompleteForwardAllocation(MultiPlayerTestSession session, LogLevel logLevel)
        {
            if (logLevel >= LogLevel.Detailed) Log("🏗️ Completing forward allocation for all players", logLevel);
            
            var playerIds = session.PlayerIds;
            for (int i = 0; i < playerIds.Length; i++)
            {
                string playerId = playerIds[i];
                
                if (logLevel >= LogLevel.Detailed) Log($"🏘️ {playerId} placing settlement and road", logLevel);
                
                // Place settlement and road for current player
                await PlaceOptimalSettlementAndRoad(session, playerId);
                
                // Advance to next player or next phase
                if (i < playerIds.Length - 1)
                {
                    await session.ExecuteActionWithVerification(playerId, GameAction.Next);
                }
            }

            // After last player, advance to reverse phase
            await session.ExecuteActionWithVerification(playerIds[playerIds.Length - 1], GameAction.Next);
            if (logLevel >= LogLevel.Detailed) Log("✅ Forward allocation completed", logLevel);
        }

        private static async Task CompleteReverseAllocation(MultiPlayerTestSession session, LogLevel logLevel)
        {
            if (logLevel >= LogLevel.Detailed) Log("🏗️ Completing reverse allocation for all players", logLevel);
            
            var playerIds = session.PlayerIds;
            for (int i = playerIds.Length - 1; i >= 0; i--)
            {
                string playerId = playerIds[i];
                
                if (logLevel >= LogLevel.Detailed) Log($"🏘️ {playerId} placing second settlement and road", logLevel);
                
                // Place settlement and road for current player
                await PlaceOptimalSettlementAndRoad(session, playerId);
                
                // Advance to next player or next phase
                if (i > 0)
                {
                    await session.ExecuteActionWithVerification(playerId, GameAction.Next);
                }
            }

            // After first player, advance to done allocation
            await session.ExecuteActionWithVerification(playerIds[0], GameAction.Next);
            if (logLevel >= LogLevel.Detailed) Log("✅ Reverse allocation completed", logLevel);
        }

        private static async Task AdvanceDoneResourceAllocation(MultiPlayerTestSession session, LogLevel logLevel)
        {
            if (logLevel >= LogLevel.Detailed) Log("🔄 DoneResourceAllocation → WaitingForRoll", logLevel);
            await session.ExecuteActionWithVerification("Alice", GameAction.Next);
            if (logLevel >= LogLevel.Detailed) Log("✅ Advanced from DoneResourceAllocation", logLevel);
        }

        private static async Task AdvanceWaitingForRoll(MultiPlayerTestSession session, LogLevel logLevel)
        {
            if (logLevel >= LogLevel.Detailed) Log("🎲 WaitingForRoll → WaitingForNext via dice roll", logLevel);
            
            var client = session.GetClient("Alice");
            await client.ExecuteRollAsync(session.GameId, 3, 3); // Roll 6 - avoids 7 which triggers robber
            await session.VerifyAllClientsReceivedUpdate();
            
            if (logLevel >= LogLevel.Detailed) Log("✅ Advanced from WaitingForRoll", logLevel);
        }

        /// <summary>
        /// Places settlement and road optimally during allocation phases using the primary client.
        /// </summary>
        private static async Task PlaceOptimalSettlementAndRoad(MultiPlayerTestSession session, string playerId)
        {
            var client = session.GetClient(playerId);
            
            // Get current game state to find buildable settlements and roads
            var gameModel = await GetGameModelViaRest(session.GameId, session.Factory);
            
            // Use AllocationHelper to pick the best settlement
            var bestSettlementKey = AllocationHelper.PickSettlement(gameModel);
            var settlementMessage = new BuildingUpgradeMessage(bestSettlementKey);
            await client.Connection.InvokeAsync("ExecuteBuildingUpgrade", session.GameId, playerId, settlementMessage);

            // Get updated game state after settlement placement to find buildable roads
            gameModel = await GetGameModelViaRest(session.GameId, session.Factory);
            
            // Use AllocationHelper to pick a buildable road
            var roadKey = AllocationHelper.PickRoad(gameModel);
            var roadMessage = new RoadPurchaseMessage(roadKey);
            await client.Connection.InvokeAsync("ExecuteRoadPurchase", session.GameId, playerId, roadMessage);

            // Verify all clients received the building updates
            await session.VerifyAllClientsReceivedUpdate();
        }

        /// <summary>
        /// Helper method to get the current GameModel using REST API
        /// </summary>
        private static async Task<GameModel> GetGameModelViaRest(string gameId, WebApplicationFactory<Program> factory)
        {
            using var httpClient = factory.CreateClient();
            
            var response = await httpClient.GetAsync($"/api/gamestate/{gameId}");
            if (!response.IsSuccessStatusCode)
            {
                throw new InvalidOperationException($"Failed to get game state: {response.StatusCode}");
            }

            var json = await response.Content.ReadAsStringAsync();
            var gameModel = JsonSerializer.Deserialize<GameModel>(json);
            
            if (gameModel == null)
            {
                throw new InvalidOperationException("Failed to deserialize GameModel");
            }

            return gameModel;
        }

        /// <summary>
        /// Centralized logging with level control
        /// </summary>
        private static void Log(string message, LogLevel requiredLevel, LogLevel currentLevel = LogLevel.Summary)
        {
            if (currentLevel >= requiredLevel)
            {
                var timestamp = DateTime.UtcNow.ToString("HH:mm:ss.fff");
                Console.WriteLine($"[{timestamp}] {message}");
            }
        }
    }

    /// <summary>
    /// Manages multiple TestSignalRClient instances for comprehensive multi-player testing.
    /// Provides automatic state verification, logging control, and coordination across all clients.
    /// </summary>
    public class MultiPlayerTestSession : IAsyncDisposable
    {
        private readonly WebApplicationFactory<Program> _factory;
        private readonly GameType _gameType;
        private readonly string[] _playerIds;
        private readonly LogLevel _logLevel;
        private readonly Dictionary<string, TestSignalRClient> _clients = new();
        
        public string GameId { get; private set; } = "";
        public string[] PlayerIds => _playerIds;
        public WebApplicationFactory<Program> Factory => _factory;

        public MultiPlayerTestSession(WebApplicationFactory<Program> factory, GameType gameType, string[] playerIds, LogLevel logLevel)
        {
            _factory = factory;
            _gameType = gameType;
            _playerIds = playerIds;
            _logLevel = logLevel;
        }

        /// <summary>
        /// Initializes the session by creating a game and connecting all players
        /// </summary>
        public async Task InitializeAsync()
        {
            // Create game via REST API
            var httpClient = _factory.CreateClient();
            var gameId = await CreateGameViaRest(httpClient, _gameType, _playerIds);
            GameId = gameId;
            
            // Connect all players via SignalR
            foreach (var playerId in _playerIds)
            {
                var client = await SignalRTestHelper.CreateTestClientAsync(_factory, playerId, gameId);
                _clients[playerId] = client;
            }
            
            if (_logLevel >= LogLevel.Summary)
                Console.WriteLine($"🎮 Created {_gameType} game with {_playerIds.Length} players: {string.Join(", ", _playerIds)}");
        }

        /// <summary>
        /// Gets a specific client by player ID
        /// </summary>
        public TestSignalRClient GetClient(string playerId)
        {
            if (!_clients.TryGetValue(playerId, out var client))
            {
                throw new InvalidOperationException($"Client for player {playerId} not found");
            }
            return client;
        }

        /// <summary>
        /// Gets the current player ID from the game state
        /// </summary>
        public string GetCurrentPlayerId()
        {
            var anyClient = _clients.Values.First();
            var currentPlayerId = anyClient.LastGameState?.CurrentPlayerId;
            
            if (string.IsNullOrEmpty(currentPlayerId))
            {
                // Default to first player if no current player set yet
                return _playerIds[0];
            }
            
            return currentPlayerId;
        }

        /// <summary>
        /// Gets all non-current player IDs
        /// </summary>
        public string[] GetNonCurrentPlayerIds()
        {
            var currentPlayerId = GetCurrentPlayerId();
            return _playerIds.Where(id => id != currentPlayerId).ToArray();
        }

        /// <summary>
        /// Verifies all clients are in the expected state
        /// </summary>
        public async Task VerifyAllClientsInState(GameState expectedState)
        {
            // Use shorter timeout and parallel execution for speed
            var tasks = _clients.Values.Select(client => client.WaitForGameStateAsync(expectedState, TimeSpan.FromSeconds(2)));
            await Task.WhenAll(tasks);
            
            if (_logLevel >= LogLevel.Summary)
                Console.WriteLine($"✅ All {_clients.Count} clients verified in {expectedState}");
        }

        /// <summary>
        /// Executes action and verifies all clients receive updates
        /// </summary>
        public async Task ExecuteActionWithVerification(string playerId, GameAction action)
        {
            var executingClient = GetClient(playerId);
            var otherClients = _clients.Values.Where(c => c.PlayerId != playerId).ToList();
            
            if (_logLevel >= LogLevel.Debug)
                Console.WriteLine($"🎯 {playerId} executing {action}, verifying {otherClients.Count} other clients receive updates");
            
            // Execute action
            var startTime = DateTime.UtcNow;
            await executingClient.ExecuteDoActionAsync(GameId, action);
            
            // Verify all clients received updates by checking their latest game state
            await VerifyAllClientsReceivedUpdate();
            
            var elapsed = DateTime.UtcNow - startTime;
            if (_logLevel >= LogLevel.Detailed)
                Console.WriteLine($"✅ {action} by {playerId} verified across all {_clients.Count} clients in {elapsed.TotalMilliseconds:F0}ms");
        }

        /// <summary>
        /// Verifies all clients have received recent updates (have consistent game state)
        /// </summary>
        public async Task VerifyAllClientsReceivedUpdate()
        {
            // Reduced delay - SignalR is fast
            await Task.Delay(25);
            
            // Check that all clients have consistent LastGameState and GameHash
            var gameStates = _clients.Values
                .Select(c => new { Client = c.PlayerId, State = c.LastGameState?.GameState, Hash = c.LastGameState?.GameHash })
                .Where(x => x.State.HasValue)
                .ToList();
            
            if (gameStates.Count > 1)
            {
                var reference = gameStates[0];
                var stateInconsistencies = gameStates.Where(g => g.State != reference.State).ToList();
                var hashInconsistencies = gameStates.Where(g => !string.IsNullOrEmpty(g.Hash) && !string.IsNullOrEmpty(reference.Hash) && g.Hash != reference.Hash).ToList();
                
                if (stateInconsistencies.Any() && _logLevel >= LogLevel.Debug)
                {
                    Console.WriteLine($"⚠️ Game state inconsistency detected:");
                    foreach (var inconsistency in stateInconsistencies)
                    {
                        Console.WriteLine($"  {inconsistency.Client}: {inconsistency.State} (expected: {reference.State})");
                    }
                }
                
                if (hashInconsistencies.Any() && _logLevel >= LogLevel.Debug)
                {
                    Console.WriteLine($"⚠️ GameHash inconsistency detected:");
                    foreach (var inconsistency in hashInconsistencies)
                    {
                        Console.WriteLine($"  {inconsistency.Client}: {inconsistency.Hash} (expected: {reference.Hash})");
                    }
                }
            }
        }

        /// <summary>
        /// Verifies game consistency across all clients using GameHash
        /// </summary>
        public async Task VerifyGameConsistency()
        {
            await Task.Delay(25); // Reduced from 50ms - just enough for updates to propagate
            
            var clientStates = _clients.Values
                .Select(c => new { Client = c.PlayerId, GameState = c.LastGameState })
                .Where(x => x.GameState != null)
                .ToList();
            
            if (clientStates.Count <= 1) return;
            
            var referenceClient = clientStates[0];
            var referenceState = referenceClient.GameState!;
            var inconsistencies = new List<string>();
            
            foreach (var clientState in clientStates.Skip(1))
            {
                var state = clientState.GameState!;
                
                if (state.GameState != referenceState.GameState)
                    inconsistencies.Add($"{clientState.Client}: GameState {state.GameState} vs {referenceState.GameState}");
                    
                if (state.CurrentPlayerId != referenceState.CurrentPlayerId)
                    inconsistencies.Add($"{clientState.Client}: CurrentPlayer {state.CurrentPlayerId} vs {referenceState.CurrentPlayerId}");
                    
                if (state.GameStateMachineVersion != referenceState.GameStateMachineVersion)
                    inconsistencies.Add($"{clientState.Client}: Version {state.GameStateMachineVersion} vs {referenceState.GameStateMachineVersion}");
                
                // **CRITICAL: GameHash verification for board consistency**
                if (!string.IsNullOrEmpty(state.GameHash) && !string.IsNullOrEmpty(referenceState.GameHash))
                {
                    if (state.GameHash != referenceState.GameHash)
                    {
                        inconsistencies.Add($"{clientState.Client}: GameHash {state.GameHash} vs {referenceState.GameHash} (BOARD MISMATCH!)");
                    }
                }
            }
            
            if (inconsistencies.Any())
            {
                var errorMessage = $"Game consistency check failed:\n  " + string.Join("\n  ", inconsistencies);
                if (_logLevel >= LogLevel.Summary)
                    Console.WriteLine($"❌ {errorMessage}");
                throw new InvalidOperationException(errorMessage);
            }
            
            // Log successful GameHash verification
            var hash = string.IsNullOrEmpty(referenceState.GameHash) ? "no-hash" : referenceState.GameHash;
            
            if (_logLevel >= LogLevel.Summary)
                Console.WriteLine($"✅ Game consistency verified across all {_clients.Count} clients (Hash: {hash})");
        }

        /// <summary>
        /// Verifies only the current player can execute actions (game rule validation)
        /// </summary>
        public async Task VerifyCurrentPlayerRestriction(string attemptingPlayerId, GameAction action)
        {
            var currentPlayerId = GetCurrentPlayerId();
            
            if (attemptingPlayerId != currentPlayerId)
            {
                // Attempt should fail or be ignored
                try
                {
                    await ExecuteActionWithVerification(attemptingPlayerId, action);
                    
                    // If it succeeds, check if the game state allows it (e.g., in PickingBoard anyone can act)
                    var gameState = GetClient(_playerIds[0]).LastGameState?.GameState;
                    if (gameState == GameState.PickingBoard)
                    {
                        if (_logLevel >= LogLevel.Detailed)
                            Console.WriteLine($"✅ {attemptingPlayerId} allowed to act in {gameState} (multiple players can act)");
                    }
                    else
                    {
                        if (_logLevel >= LogLevel.Summary)
                            Console.WriteLine($"⚠️ {attemptingPlayerId} was allowed to act when not current player in {gameState}");
                    }
                }
                catch (Exception ex)
                {
                    if (_logLevel >= LogLevel.Detailed)
                        Console.WriteLine($"✅ Correctly prevented {attemptingPlayerId} from acting: {ex.Message}");
                }
            }
        }

        /// <summary>
        /// Creates a game via REST API
        /// </summary>
        private static async Task<string> CreateGameViaRest(HttpClient httpClient, GameType gameType, string[] playerIds)
        {
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
        /// Properly disposes all clients
        /// </summary>
        public async ValueTask DisposeAsync()
        {
            foreach (var client in _clients.Values)
            {
                await client.DisposeAsync();
            }
            _clients.Clear();
            
            if (_logLevel >= LogLevel.Detailed)
                Console.WriteLine($"🧹 Disposed {_playerIds.Length} clients");
        }
    }
}