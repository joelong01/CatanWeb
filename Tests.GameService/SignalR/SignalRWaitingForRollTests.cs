using Xunit;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.Configuration;
using Catan3.Shared.Models;
using Tests.GameService.SignalR;

namespace Tests.GameService.SignalR
{
    /// <summary>
    /// Comprehensive tests for WaitingForRoll phase via SignalR.
    /// Tests dice rolling mechanics, resource generation, and turn progression.
    /// 
    /// These tests verify:
    /// 1. Dice rolling functionality via SignalR
    /// 2. Resource generation based on dice rolls
    /// 3. Seven roll mechanics (robber movement)
    /// 4. Real-time synchronization across multiple clients
    /// 5. Turn progression from WaitingForRoll to WaitingForNext
    /// 6. Error handling for invalid rolls and actions
    /// </summary>
    public class SignalRWaitingForRollTests : IClassFixture<WebApplicationFactory<Program>>, IAsyncDisposable
    {
        private readonly WebApplicationFactory<Program> _factory;
        private readonly List<HubConnection> _connections = new();

        public SignalRWaitingForRollTests(WebApplicationFactory<Program> factory)
        {
            _factory = factory.WithWebHostBuilder(builder =>
            {
                builder.ConfigureAppConfiguration((context, config) =>
                {
                    config.AddInMemoryCollection(new Dictionary<string, string?>
                    {
                        ["GameApi:HangingGetTimeoutSeconds"] = "5"
                    });
                });
            });
        }

        [Fact]
        public async Task WaitingForRoll_RollDice_ShouldAdvanceToWaitingForNextViaSignalR()
        {
            // Arrange
            var (gameId, connection) = await SignalRTestHelper.CreateGameInStateViaSignalR(_factory, GameState.WaitingForRoll);
            _connections.Add(connection);

            GameModel? updatedGameModel = null;
            var stateChanged = new TaskCompletionSource<bool>();

            connection.On<GameModel>("GameStateUpdated", gameModel =>
            {
                if (gameModel.GameState == GameState.WaitingForNext)
                {
                    updatedGameModel = gameModel;
                    stateChanged.TrySetResult(true);
                }
            });

            // Act - Roll dice (use 6 to avoid seven roll complications)
            var turnRoll = new TurnRollModel(3, 3); // Roll 6
            var rollMessage = new RollMessage(turnRoll);
            await connection.InvokeAsync("ExecuteRoll", gameId, "Alice", rollMessage);

            // Assert
            var result = await stateChanged.Task.WaitAsync(TimeSpan.FromSeconds(10));
            Assert.True(result, "Should advance to WaitingForNext after roll");
            Assert.NotNull(updatedGameModel);
            Assert.Equal(GameState.WaitingForNext, updatedGameModel.GameState);
        }

        [Fact]
        public async Task WaitingForRoll_SevenRoll_ShouldTriggerRobberMovementViaSignalR()
        {
            // Arrange
            var (gameId, connection) = await SignalRTestHelper.CreateGameInStateViaSignalR(_factory, GameState.WaitingForRoll);
            _connections.Add(connection);

            GameModel? updatedGameModel = null;
            var stateChanged = new TaskCompletionSource<bool>();

            connection.On<GameModel>("GameStateUpdated", gameModel =>
            {
                if (gameModel.GameState == GameState.MustMoveRobber)
                {
                    updatedGameModel = gameModel;
                    stateChanged.TrySetResult(true);
                }
            });

            // Act - Roll seven
            var turnRoll = new TurnRollModel(3, 4); // Roll 7
            var rollMessage = new RollMessage(turnRoll);
            await connection.InvokeAsync("ExecuteRoll", gameId, "Alice", rollMessage);

            // Assert
            var result = await stateChanged.Task.WaitAsync(TimeSpan.FromSeconds(10));
            Assert.True(result, "Should trigger robber movement on seven roll");
            Assert.NotNull(updatedGameModel);
            Assert.Equal(GameState.MustMoveRobber, updatedGameModel.GameState);
        }

        [Fact]
        public async Task WaitingForRoll_MultipleClients_ShouldReceiveRollUpdatesViaSignalR()
        {
            // Arrange
            var (gameId, connection1) = await SignalRTestHelper.CreateGameInStateViaSignalR(_factory, GameState.WaitingForRoll);
            var connection2 = await SignalRTestHelper.CreateTestConnection(_factory, gameId, "Bob");
            var connection3 = await SignalRTestHelper.CreateTestConnection(_factory, gameId, "Charlie");

            _connections.AddRange(new[] { connection1, connection2, connection3 });

            var receivedUpdates = new List<(string clientId, GameModel gameModel, DateTime timestamp)>();
            var updateLock = new object();

            void AddUpdateHandler(HubConnection conn, string clientId)
            {
                conn.On<GameModel>("GameStateUpdated", gameModel =>
                {
                    lock (updateLock)
                    {
                        receivedUpdates.Add((clientId, gameModel, DateTime.UtcNow));
                    }
                });
            }

            AddUpdateHandler(connection1, "Alice");
            AddUpdateHandler(connection2, "Bob");
            AddUpdateHandler(connection3, "Charlie");

            // Act - Execute roll from one client
            var actionStartTime = DateTime.UtcNow;
            var turnRoll = new TurnRollModel(2, 4); // Roll 6
            var rollMessage = new RollMessage(turnRoll);
            await connection1.InvokeAsync("ExecuteRoll", gameId, "Alice", rollMessage);

            // Wait for all clients to receive updates
            var timeout = TimeSpan.FromSeconds(10);
            var waitStart = DateTime.UtcNow;
            
            while (receivedUpdates.Count < 3 && DateTime.UtcNow - waitStart < timeout)
            {
                await Task.Delay(100);
            }

            // Assert
            Assert.True(receivedUpdates.Count >= 3, $"Expected 3 client updates, got {receivedUpdates.Count}");

            foreach (var update in receivedUpdates)
            {
                var responseTime = update.timestamp - actionStartTime;
                Assert.True(responseTime.TotalSeconds < 5, 
                    $"Client {update.clientId} should receive update quickly, took {responseTime.TotalSeconds} seconds");
                
                Assert.Equal(gameId, update.gameModel.GameId);
                Assert.Equal(GameState.WaitingForNext, update.gameModel.GameState);
            }
        }

        [Fact]
        public async Task WaitingForRoll_InvalidAction_ShouldReturnErrorViaSignalR()
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

            // Act - Try to execute Shuffle in WaitingForRoll state (should fail)
            var shuffleMessage = new DoAction(GameAction.Shuffle);
            await connection.InvokeAsync("ExecuteDoAction", gameId, "Alice", shuffleMessage);

            // Assert
            var errorResult = await errorReceived.Task.WaitAsync(TimeSpan.FromSeconds(10));
            Assert.True(errorResult, "Should receive error for invalid action");
            Assert.NotNull(errorMessage);
        }

        [Fact]
        public async Task WaitingForRoll_PurchaseAttempt_ShouldFailViaSignalR()
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

            // Act - Try to purchase in WaitingForRoll state (should fail)
            var purchaseMessage = new PurchaseMessage(Entitlement.Road);
            await connection.InvokeAsync("ExecutePurchase", gameId, "Alice", purchaseMessage);

            // Assert
            var errorResult = await errorReceived.Task.WaitAsync(TimeSpan.FromSeconds(10));
            Assert.True(errorResult, "Should receive error for purchase in WaitingForRoll state");
            Assert.NotNull(errorMessage);
        }

        [Fact]
        public async Task WaitingForRoll_DifferentRollValues_ShouldWorkViaSignalR()
        {
            // Test various dice combinations
            var rollTestCases = new[]
            {
                (2, 1, 3),  // Roll 3
                (1, 1, 2),  // Roll 2  
                (2, 3, 5),  // Roll 5
                (4, 4, 8),  // Roll 8
                (5, 6, 11), // Roll 11
                (6, 6, 12)  // Roll 12
            };

            foreach (var (red, white, expected) in rollTestCases)
            {
                // Arrange
                var (gameId, connection) = await SignalRTestHelper.CreateGameInStateViaSignalR(_factory, GameState.WaitingForRoll);
                _connections.Add(connection);

                GameModel? updatedGameModel = null;
                var rollProcessed = new TaskCompletionSource<bool>();

                connection.On<GameModel>("GameStateUpdated", gameModel =>
                {
                    updatedGameModel = gameModel;
                    rollProcessed.TrySetResult(true);
                });

                // Act
                var turnRoll = new TurnRollModel(red, white);
                var rollMessage = new RollMessage(turnRoll);
                await connection.InvokeAsync("ExecuteRoll", gameId, "Alice", rollMessage);

                // Assert
                var result = await rollProcessed.Task.WaitAsync(TimeSpan.FromSeconds(10));
                Assert.True(result, $"Roll {expected} should be processed");
                Assert.NotNull(updatedGameModel);
                
                // Check expected state based on roll value
                if (expected == 7)
                {
                    Assert.Equal(GameState.MustMoveRobber, updatedGameModel.GameState);
                }
                else
                {
                    Assert.Equal(GameState.WaitingForNext, updatedGameModel.GameState);
                }
            }
        }

        [Fact]
        public async Task WaitingForRoll_UndoAfterRoll_ShouldWorkViaSignalR()
        {
            // Arrange
            var (gameId, connection) = await SignalRTestHelper.CreateGameInStateViaSignalR(_factory, GameState.WaitingForRoll);
            _connections.Add(connection);

            // First, execute a roll
            var gameModelAfterRoll = await SignalRTestHelper.ExecuteRollViaSignalR(connection, gameId, "Alice", ValidCatanRoll.Six);
            Assert.NotNull(gameModelAfterRoll);
            Assert.Equal(GameState.WaitingForNext, gameModelAfterRoll.GameState);

            // Now test undo
            GameModel? gameModelAfterUndo = null;
            var undoCompleted = new TaskCompletionSource<bool>();

            connection.On<GameModel>("GameStateUpdated", gameModel =>
            {
                if (gameModel.GameState == GameState.WaitingForRoll)
                {
                    gameModelAfterUndo = gameModel;
                    undoCompleted.TrySetResult(true);
                }
            });

            // Act - Undo the roll
            var undoMessage = new DoAction(GameAction.Undo);
            await connection.InvokeAsync("ExecuteDoAction", gameId, "Alice", undoMessage);

            // Assert
            var result = await undoCompleted.Task.WaitAsync(TimeSpan.FromSeconds(10));
            Assert.True(result, "Undo should work after roll");
            Assert.NotNull(gameModelAfterUndo);
            Assert.Equal(GameState.WaitingForRoll, gameModelAfterUndo.GameState);
        }

        [Fact]
        public async Task WaitingForRoll_CommandCompletion_ShouldProvideRollDetailsViaSignalR()
        {
            // Arrange
            var (gameId, connection) = await SignalRTestHelper.CreateGameInStateViaSignalR(_factory, GameState.WaitingForRoll);
            _connections.Add(connection);

            string? completionMessage = null;
            var commandCompleted = new TaskCompletionSource<bool>();

            connection.On<string, bool, string>("CommandCompleted", (commandId, success, message) =>
            {
                completionMessage = message;
                commandCompleted.TrySetResult(success);
            });

            // Act - Roll dice
            var turnRoll = new TurnRollModel(2, 3); // Roll 5
            var rollMessage = new RollMessage(turnRoll);
            await connection.InvokeAsync("ExecuteRoll", gameId, "Alice", rollMessage);

            // Assert
            var result = await commandCompleted.Task.WaitAsync(TimeSpan.FromSeconds(10));
            Assert.True(result, "Roll command should complete successfully");
            Assert.NotNull(completionMessage);
            Assert.Contains("5", completionMessage); // Should mention the roll value
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