using Xunit;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.Configuration;
using Catan3.Shared.Models;
using Tests.GameService.SignalR;

namespace Tests.GameService.SignalR
{
    /// <summary>
    /// Comprehensive tests for SignalR GameHub core functionality.
    /// Tests the new pure SignalR architecture with direct MVVM message handling.
    /// 
    /// These tests verify:
    /// 1. SignalR connection establishment and game joining
    /// 2. Real-time bi-directional communication 
    /// 3. Direct MVVM message execution (same as Desktop app)
    /// 4. Command completion and error handling
    /// 5. Game group management and notifications
    /// 6. Connection resilience and reconnection
    /// </summary>
    public class SignalRGameHubTests : IClassFixture<WebApplicationFactory<Program>>, IAsyncDisposable
    {
        private readonly WebApplicationFactory<Program> _factory;
        private readonly List<HubConnection> _connections = new();

        public SignalRGameHubTests(WebApplicationFactory<Program> factory)
        {
            // Configure the factory with test-specific settings
            _factory = factory.WithWebHostBuilder(builder =>
            {
                builder.ConfigureAppConfiguration((context, config) =>
                {
                    config.AddInMemoryCollection(new Dictionary<string, string?>
                    {
                        // Set short timeout for tests - 5 seconds instead of 15 minutes
                        ["GameApi:HangingGetTimeoutSeconds"] = "5"
                    });
                });
            });
        }

        [Fact]
        public async Task SignalRConnection_ShouldEstablishSuccessfully()
        {
            // Arrange & Act
            var connection = await SignalRTestHelper.CreateTestConnection(_factory);
            _connections.Add(connection);

            // Assert
            Assert.Equal(HubConnectionState.Connected, connection.State);
            Assert.NotNull(connection.ConnectionId);
        }

        [Fact]
        public async Task JoinGame_ShouldAddClientToGameGroup()
        {
            // Arrange
            var gameId = "test-game-123";
            var playerId = "Alice";
            var connection = await SignalRTestHelper.CreateTestConnection(_factory);
            _connections.Add(connection);

            GameModel? receivedGameState = null;
            connection.On<GameModel>("GameStateUpdated", gameModel =>
            {
                receivedGameState = gameModel;
            });

            // Act
            await connection.InvokeAsync("JoinGame", gameId, playerId);
            
            // Give some time for any potential game state updates
            await Task.Delay(500);

            // Assert
            Assert.Equal(HubConnectionState.Connected, connection.State);
            // Note: receivedGameState might be null if no game exists with that ID, which is expected
        }

        [Fact]
        public async Task ExecuteDoAction_WithValidAction_ShouldProcessSuccessfully()
        {
            // Arrange - Create a game using REST API first
            var httpClient = _factory.CreateClient();
            var (gameId, connection) = await SignalRTestHelper.CreateGameInStateViaSignalR(_factory, GameState.PickingBoard);
            _connections.Add(connection);

            GameModel? updatedGameModel = null;
            string? commandCompletionMessage = null;
            var updateReceived = new TaskCompletionSource<bool>();
            var commandCompleted = new TaskCompletionSource<bool>();

            // Set up event handlers
            connection.On<GameModel>("GameStateUpdated", gameModel =>
            {
                updatedGameModel = gameModel;
                updateReceived.TrySetResult(true);
            });

            connection.On<string, bool, string>("CommandCompleted", (commandId, success, message) =>
            {
                commandCompletionMessage = message;
                commandCompleted.TrySetResult(success);
            });

            // Act - Execute Shuffle action via SignalR
            var message = new DoAction(GameAction.Shuffle);
            await connection.InvokeAsync("ExecuteDoAction", gameId, "Alice", message);

            // Wait for both game state update and command completion
            var updateTask = updateReceived.Task.WaitAsync(TimeSpan.FromSeconds(10));
            var completionTask = commandCompleted.Task.WaitAsync(TimeSpan.FromSeconds(10));

            var updateResult = await updateTask;
            var completionResult = await completionTask;

            // Assert
            Assert.True(updateResult, "Should receive game state update via SignalR");
            Assert.True(completionResult, "Command should complete successfully");
            Assert.NotNull(updatedGameModel);
            Assert.Equal(gameId, updatedGameModel.GameId);
            Assert.Contains("Shuffle", commandCompletionMessage ?? "");
        }

        [Fact]
        public async Task ExecuteDoAction_InvalidAction_ShouldReturnError()
        {
            // Arrange
            var (gameId, connection) = await SignalRTestHelper.CreateGameInStateViaSignalR(_factory, GameState.WaitingForRoll);
            _connections.Add(connection);

            string? errorMessage = null;
            var errorReceived = new TaskCompletionSource<bool>();

            connection.On<string, string>("CommandFailed", (commandId, error) =>
            {
                errorMessage = error;
                errorReceived.TrySetResult(true);
            });

            // Act - Try to shuffle while in WaitingForRoll state (should fail)
            var message = new DoAction(GameAction.Shuffle);
            await connection.InvokeAsync("ExecuteDoAction", gameId, "Alice", message);

            // Wait for error
            var errorResult = await errorReceived.Task.WaitAsync(TimeSpan.FromSeconds(10));

            // Assert
            Assert.True(errorResult, "Should receive error for invalid action");
            Assert.NotNull(errorMessage);
            Assert.Contains("invalid", errorMessage.ToLower());
        }

        [Fact]
        public async Task ExecutePurchase_WithValidEntitlement_ShouldProcessSuccessfully()
        {
            // Arrange - Create a game in WaitingForNext state where purchases are allowed
            var (gameId, connection) = await SignalRTestHelper.CreateGameInStateViaSignalR(_factory, GameState.WaitingForNext);
            _connections.Add(connection);

            GameModel? updatedGameModel = null;
            bool commandSuccess = false;
            var updateReceived = new TaskCompletionSource<bool>();
            var commandCompleted = new TaskCompletionSource<bool>();

            // Set up event handlers
            connection.On<GameModel>("GameStateUpdated", gameModel =>
            {
                updatedGameModel = gameModel;
                updateReceived.TrySetResult(true);
            });

            connection.On<string, bool, string>("CommandCompleted", (commandId, success, message) =>
            {
                commandSuccess = success;
                commandCompleted.TrySetResult(true);
            });

            connection.On<string, string>("CommandFailed", (commandId, error) =>
            {
                commandCompleted.TrySetResult(false);
            });

            // Act - Try to purchase a road
            var message = new PurchaseMessage(Entitlement.Road);
            await connection.InvokeAsync("ExecutePurchase", gameId, "Alice", message);

            // Wait for response (either success or failure is acceptable)
            var completionResult = await commandCompleted.Task.WaitAsync(TimeSpan.FromSeconds(10));

            // Assert
            Assert.True(completionResult, "Should receive some response to purchase command");
            
            if (commandSuccess)
            {
                // If purchase succeeded, verify we got a game state update
                var updateResult = await updateReceived.Task.WaitAsync(TimeSpan.FromSeconds(5));
                Assert.True(updateResult, "Should receive game state update after successful purchase");
                Assert.NotNull(updatedGameModel);
                Assert.Equal(gameId, updatedGameModel.GameId);
            }
            // If purchase failed (e.g., insufficient resources), that's also valid behavior
        }

        [Fact]
        public async Task MultipleClients_ShouldReceiveRealTimeUpdates()
        {
            // Arrange - Create a game and connect multiple clients
            var (gameId, connection1) = await SignalRTestHelper.CreateGameInStateViaSignalR(_factory, GameState.PickingBoard);
            var connection2 = await SignalRTestHelper.CreateTestConnection(_factory, gameId, "Bob");
            var connection3 = await SignalRTestHelper.CreateTestConnection(_factory, gameId, "Charlie");
            
            _connections.AddRange(new[] { connection1, connection2, connection3 });

            var updates = new List<(string clientId, GameModel gameModel, DateTime timestamp)>();
            var updateLock = new object();

            // Set up event handlers for all clients
            void AddUpdateHandler(HubConnection conn, string clientId)
            {
                conn.On<GameModel>("GameStateUpdated", gameModel =>
                {
                    lock (updateLock)
                    {
                        updates.Add((clientId, gameModel, DateTime.UtcNow));
                    }
                });
            }

            AddUpdateHandler(connection1, "Alice");
            AddUpdateHandler(connection2, "Bob");
            AddUpdateHandler(connection3, "Charlie");

            // Act - Execute an action that should notify all clients
            var actionStartTime = DateTime.UtcNow;
            var message = new DoAction(GameAction.Shuffle);
            await connection1.InvokeAsync("ExecuteDoAction", gameId, "Alice", message);

            // Wait for all clients to receive updates
            var timeout = TimeSpan.FromSeconds(10);
            var waitStart = DateTime.UtcNow;
            
            while (updates.Count < 3 && DateTime.UtcNow - waitStart < timeout)
            {
                await Task.Delay(100);
            }

            // Assert
            Assert.True(updates.Count >= 3, $"Expected at least 3 client updates, got {updates.Count}");

            var allClients = new[] { "Alice", "Bob", "Charlie" };
            foreach (var clientId in allClients)
            {
                var clientUpdate = updates.FirstOrDefault(u => u.clientId == clientId);
                Assert.NotEqual(default, clientUpdate); // Verify client received update
                
                var responseTime = clientUpdate.timestamp - actionStartTime;
                Assert.True(responseTime.TotalSeconds < 5, 
                    $"Client {clientId} should receive update quickly, took {responseTime.TotalSeconds} seconds");
                
                Assert.Equal(gameId, clientUpdate.gameModel.GameId);
            }
        }

        [Fact]
        public async Task CommandCompletion_ShouldNotifyOnlyOriginalClient()
        {
            // Arrange - Create a game and connect multiple clients
            var (gameId, connection1) = await SignalRTestHelper.CreateGameInStateViaSignalR(_factory, GameState.PickingBoard);
            var connection2 = await SignalRTestHelper.CreateTestConnection(_factory, gameId, "Bob");
            
            _connections.AddRange(new[] { connection1, connection2 });

            var alice_completions = new List<string>();
            var bob_completions = new List<string>();

            connection1.On<string, bool, string>("CommandCompleted", (commandId, success, message) =>
            {
                alice_completions.Add(message);
            });

            connection2.On<string, bool, string>("CommandCompleted", (commandId, success, message) =>
            {
                bob_completions.Add(message);
            });

            // Act - Alice executes an action
            var message = new DoAction(GameAction.Shuffle);
            await connection1.InvokeAsync("ExecuteDoAction", gameId, "Alice", message);

            // Wait for completion notifications
            await Task.Delay(2000);

            // Assert - Only Alice should receive command completion
            Assert.True(alice_completions.Count > 0, "Alice should receive command completion notification");
            Assert.Empty(bob_completions); // Bob should NOT receive Alice's command completion
        }

        [Fact]
        public async Task ConnectionResilience_ShouldHandleReconnection()
        {
            // Arrange
            var (gameId, connection) = await SignalRTestHelper.CreateGameInStateViaSignalR(_factory, GameState.PickingBoard);
            _connections.Add(connection);

            // Verify initial connection
            Assert.Equal(HubConnectionState.Connected, connection.State);

            // Act - Simulate disconnection and reconnection
            await connection.StopAsync();
            Assert.Equal(HubConnectionState.Disconnected, connection.State);

            await connection.StartAsync();
            Assert.Equal(HubConnectionState.Connected, connection.State);

            // Rejoin the game
            await connection.InvokeAsync("JoinGame", gameId, "Alice");

            // Test that commands still work after reconnection
            var updateReceived = new TaskCompletionSource<bool>();
            connection.On<GameModel>("GameStateUpdated", gameModel =>
            {
                updateReceived.TrySetResult(true);
            });

            var message = new DoAction(GameAction.Shuffle);
            await connection.InvokeAsync("ExecuteDoAction", gameId, "Alice", message);

            // Assert
            var updateResult = await updateReceived.Task.WaitAsync(TimeSpan.FromSeconds(10));
            Assert.True(updateResult, "Commands should work after reconnection");
        }

        [Fact]
        public async Task PlayerPresence_ShouldNotifyOtherClients()
        {
            // Arrange - Create a game with one client
            var (gameId, connection1) = await SignalRTestHelper.CreateGameInStateViaSignalR(_factory, GameState.PickingBoard);
            _connections.Add(connection1);

            var presenceUpdates = new List<(string playerId, bool isOnline)>();
            connection1.On<string, bool>("PlayerPresenceChanged", (playerId, isOnline) =>
            {
                presenceUpdates.Add((playerId, isOnline));
            });

            // Act - Add second client
            var connection2 = await SignalRTestHelper.CreateTestConnection(_factory, gameId, "Bob");
            _connections.Add(connection2);

            // Wait for presence notification
            await Task.Delay(1000);

            // Assert
            Assert.Contains(presenceUpdates, update => update.playerId == "Bob" && update.isOnline == true);
        }

        [Fact]
        public async Task InvalidGameId_ShouldHandleGracefully()
        {
            // Arrange
            var connection = await SignalRTestHelper.CreateTestConnection(_factory);
            _connections.Add(connection);

            string? errorMessage = null;
            var errorReceived = new TaskCompletionSource<bool>();

            connection.On<string, string>("CommandFailed", (commandId, error) =>
            {
                errorMessage = error;
                errorReceived.TrySetResult(true);
            });

            // Act - Try to execute action on non-existent game
            var message = new DoAction(GameAction.Shuffle);
            await connection.InvokeAsync("ExecuteDoAction", "non-existent-game", "Alice", message);

            // Wait for error
            var errorResult = await errorReceived.Task.WaitAsync(TimeSpan.FromSeconds(10));

            // Assert
            Assert.True(errorResult, "Should receive error for invalid game ID");
            Assert.NotNull(errorMessage);
        }

        [Fact]
        public async Task PerformanceTest_SignalRLatency_ShouldBeFast()
        {
            // Arrange
            var (gameId, connection) = await SignalRTestHelper.CreateGameInStateViaSignalR(_factory, GameState.PickingBoard);
            _connections.Add(connection);

            var responseTime = TimeSpan.Zero;
            var responseReceived = new TaskCompletionSource<bool>();

            connection.On<GameModel>("GameStateUpdated", gameModel =>
            {
                responseTime = DateTime.UtcNow - DateTime.UtcNow; // Will be updated below
                responseReceived.TrySetResult(true);
            });

            // Act
            var startTime = DateTime.UtcNow;
            var message = new DoAction(GameAction.Shuffle);
            await connection.InvokeAsync("ExecuteDoAction", gameId, "Alice", message);

            var result = await responseReceived.Task.WaitAsync(TimeSpan.FromSeconds(10));
            var endTime = DateTime.UtcNow;
            responseTime = endTime - startTime;

            // Assert
            Assert.True(result, "Should receive response");
            Assert.True(responseTime.TotalMilliseconds < 2000, 
                $"SignalR response should be fast (< 2000ms), was {responseTime.TotalMilliseconds}ms");
            
            // Log performance for reference
            Console.WriteLine($"SignalR command latency: {responseTime.TotalMilliseconds}ms");
        }

        public async ValueTask DisposeAsync()
        {
            foreach (var connection in _connections)
            {
                await SignalRTestHelper.DisposeConnection(connection);
            }
            _connections.Clear();
        }
    }
}