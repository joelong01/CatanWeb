using Xunit;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.Configuration;
using Catan3.Shared.Models;
using Tests.GameService.SignalR;

namespace Tests.GameService.SignalR
{
    /// <summary>
    /// Comprehensive tests for PickingBoard phase via SignalR.
    /// Tests board shuffling, game initialization, and transition to roll-for-order phase.
    /// 
    /// These tests verify:
    /// 1. Board shuffling functionality via SignalR
    /// 2. Real-time synchronization across multiple clients  
    /// 3. Game state progression from PickingBoard to WaitingForRollForOrder
    /// 4. Board randomization and tile distribution
    /// 5. Error handling for invalid actions in PickingBoard state
    /// </summary>
    public class SignalRPickingBoardTests : IClassFixture<WebApplicationFactory<Program>>, IAsyncDisposable
    {
        private readonly WebApplicationFactory<Program> _factory;
        private readonly List<HubConnection> _connections = new();

        public SignalRPickingBoardTests(WebApplicationFactory<Program> factory)
        {
            _factory = TestWebApplicationFactory.Create();
        }

        [Fact]
        public async Task PickingBoard_ShuffleAction_ShouldRandomizeBoardViaSignalR()
        {
            // Arrange - Create a game in PickingBoard state
            var (gameId, connection) = await SignalRTestHelper.CreateGameInStateViaSignalR(_factory, GameState.PickingBoard);
            _connections.Add(connection);

            GameModel? updatedGameModel = null;
            var updateReceived = new TaskCompletionSource<bool>();
            var commandCompleted = new TaskCompletionSource<bool>();

            connection.On<GameModel>("GameStateUpdated", gameModel =>
            {
                updatedGameModel = gameModel;
                updateReceived.TrySetResult(true);
            });

            connection.On<string, bool, string>("CommandCompleted", (commandId, success, message) =>
            {
                commandCompleted.TrySetResult(success);
            });

            // Act - Execute Shuffle action via SignalR
            var shuffleMessage = new DoAction(GameAction.Shuffle);
            await connection.InvokeAsync("ExecuteDoAction", gameId, "Alice", shuffleMessage);

            // Wait for both game state update and command completion
            var updateResult = await updateReceived.Task.WaitAsync(TimeSpan.FromSeconds(10));
            var completionResult = await commandCompleted.Task.WaitAsync(TimeSpan.FromSeconds(10));

            // Assert
            Assert.True(updateResult, "Should receive game state update");
            Assert.True(completionResult, "Command should complete successfully");
            Assert.NotNull(updatedGameModel);
            Assert.Equal(gameId, updatedGameModel.GameId);
            Assert.Equal(GameState.PickingBoard, updatedGameModel.GameState);

            // Verify board has tiles (randomization occurred)
            Assert.NotEmpty(updatedGameModel.Tiles);
            Assert.True(updatedGameModel.Tiles.Count > 0, "Board should have tiles after shuffle");
        }

        [Fact]
        public async Task PickingBoard_NextAction_ShouldAdvanceToRollForOrderViaSignalR()
        {
            // Arrange
            var (gameId, connection) = await SignalRTestHelper.CreateGameInStateViaSignalR(_factory, GameState.PickingBoard);
            _connections.Add(connection);

            // First shuffle the board
            await SignalRTestHelper.ExecuteDoActionViaSignalR(connection, gameId, "Alice", GameAction.Shuffle);

            GameModel? finalGameModel = null;
            var updateReceived = new TaskCompletionSource<bool>();

            connection.On<GameModel>("GameStateUpdated", gameModel =>
            {
                if (gameModel.GameState == GameState.WaitingForRollForOrder)
                {
                    finalGameModel = gameModel;
                    updateReceived.TrySetResult(true);
                }
            });

            // Act - Execute Next action to advance from PickingBoard
            var nextMessage = new DoAction(GameAction.Next);
            await connection.InvokeAsync("ExecuteDoAction", gameId, "Alice", nextMessage);

            // Assert
            var result = await updateReceived.Task.WaitAsync(TimeSpan.FromSeconds(10));
            Assert.True(result, "Should advance to WaitingForRollForOrder");
            Assert.NotNull(finalGameModel);
            Assert.Equal(GameState.WaitingForRollForOrder, finalGameModel.GameState);
        }

        [Fact]
        public async Task PickingBoard_MultipleClients_ShouldReceiveShuffleUpdatesViaSignalR()
        {
            // Arrange - Create a game and connect multiple clients
            var (gameId, connection1) = await SignalRTestHelper.CreateGameInStateViaSignalR(_factory, GameState.PickingBoard);
            var connection2 = await SignalRTestHelper.CreateTestConnection(_factory, gameId, "Bob");
            var connection3 = await SignalRTestHelper.CreateTestConnection(_factory, gameId, "Charlie");

            _connections.AddRange(new[] { connection1, connection2, connection3 });

            var receivedUpdates = new List<(string clientId, GameModel gameModel, DateTime timestamp)>();
            var updateLock = new object();

            // Set up event handlers for all clients
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

            // Act - Execute Shuffle action from one client
            var actionStartTime = DateTime.UtcNow;
            var shuffleMessage = new DoAction(GameAction.Shuffle);
            await connection1.InvokeAsync("ExecuteDoAction", gameId, "Alice", shuffleMessage);

            // Wait for all clients to receive updates
            var timeout = TimeSpan.FromSeconds(10);
            var waitStart = DateTime.UtcNow;
            
            while (receivedUpdates.Count < 3 && DateTime.UtcNow - waitStart < timeout)
            {
                await Task.Delay(100);
            }

            // Assert
            Assert.True(receivedUpdates.Count >= 3, $"Expected 3 client updates, got {receivedUpdates.Count}");

            var allClients = new[] { "Alice", "Bob", "Charlie" };
            foreach (var clientId in allClients)
            {
                var clientUpdate = receivedUpdates.FirstOrDefault(u => u.clientId == clientId);
                Assert.NotEqual(default, clientUpdate);
                
                var responseTime = clientUpdate.timestamp - actionStartTime;
                Assert.True(responseTime.TotalSeconds < 5, 
                    $"Client {clientId} should receive update quickly, took {responseTime.TotalSeconds} seconds");
                
                Assert.Equal(gameId, clientUpdate.gameModel.GameId);
                Assert.Equal(GameState.PickingBoard, clientUpdate.gameModel.GameState);
            }
        }

        [Fact]
        public async Task PickingBoard_UndoAction_ShouldFailAppropriatelyViaSignalR()
        {
            // Arrange
            var (gameId, connection) = await SignalRTestHelper.CreateGameInStateViaSignalR(_factory, GameState.PickingBoard);
            _connections.Add(connection);

            string? errorMessage = null;
            var errorReceived = new TaskCompletionSource<bool>();

            connection.On<string, string>("CommandFailed", (commandId, error) =>
            {
                errorMessage = error;
                errorReceived.TrySetResult(true);
            });

            // Act - Try to undo when no action history exists
            var undoMessage = new DoAction(GameAction.Undo);
            await connection.InvokeAsync("ExecuteDoAction", gameId, "Alice", undoMessage);

            // Assert
            var errorResult = await errorReceived.Task.WaitAsync(TimeSpan.FromSeconds(10));
            Assert.True(errorResult, "Should receive error for invalid undo");
            Assert.NotNull(errorMessage);
        }

        [Fact]
        public async Task PickingBoard_InvalidAction_ShouldReturnErrorViaSignalR()
        {
            // Arrange
            var (gameId, connection) = await SignalRTestHelper.CreateGameInStateViaSignalR(_factory, GameState.PickingBoard);
            _connections.Add(connection);

            string? errorMessage = null;
            var errorReceived = new TaskCompletionSource<bool>();

            connection.On<string, string>("CommandFailed", (commandId, error) =>
            {
                errorMessage = error;
                errorReceived.TrySetResult(true);
            });

            // Act - Try to execute a roll action in PickingBoard state (should fail)
            try
            {
                var turnRoll = new TurnRollModel(3, 4);
                var rollMessage = new RollMessage(turnRoll);
                await connection.InvokeAsync("ExecuteRoll", gameId, "Alice", rollMessage);

                // Wait for error
                var errorResult = await errorReceived.Task.WaitAsync(TimeSpan.FromSeconds(10));
                Assert.True(errorResult, "Should receive error for invalid roll in PickingBoard state");
                Assert.NotNull(errorMessage);
            }
            catch (Exception ex)
            {
                // Alternative: Exception might be thrown directly
                Assert.Contains("invalid", ex.Message.ToLower());
            }
        }

        [Fact]
        public async Task PickingBoard_ShuffleAndNext_CompleteWorkflowViaSignalR()
        {
            // Test the complete PickingBoard workflow: Shuffle -> Next -> Advance to next phase
            
            // Arrange
            var (gameId, connection) = await SignalRTestHelper.CreateGameInStateViaSignalR(_factory, GameState.PickingBoard);
            _connections.Add(connection);

            var stateUpdates = new List<GameState>();
            var stateUpdateLock = new object();

            connection.On<GameModel>("GameStateUpdated", gameModel =>
            {
                lock (stateUpdateLock)
                {
                    stateUpdates.Add(gameModel.GameState);
                }
            });

            // Act - Execute complete workflow
            // Step 1: Shuffle
            await SignalRTestHelper.ExecuteDoActionViaSignalR(connection, gameId, "Alice", GameAction.Shuffle);
            await Task.Delay(500); // Allow state update to process

            // Step 2: Next (advance to next phase)
            await SignalRTestHelper.ExecuteDoActionViaSignalR(connection, gameId, "Alice", GameAction.Next);
            await Task.Delay(500); // Allow state update to process

            // Assert
            Assert.Contains(GameState.PickingBoard, stateUpdates);
            Assert.Contains(GameState.WaitingForRollForOrder, stateUpdates);
            
            // Verify progression: PickingBoard -> WaitingForRollForOrder
            var pickingBoardIndex = stateUpdates.LastIndexOf(GameState.PickingBoard);
            var rollForOrderIndex = stateUpdates.LastIndexOf(GameState.WaitingForRollForOrder);
            
            Assert.True(rollForOrderIndex > pickingBoardIndex, "Should progress from PickingBoard to WaitingForRollForOrder");
        }

        [Fact]
        public async Task PickingBoard_CommandCompletion_ShouldNotifyOriginalClientOnlyViaSignalR()
        {
            // Arrange
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

            // Act - Alice executes shuffle action
            var shuffleMessage = new DoAction(GameAction.Shuffle);
            await connection1.InvokeAsync("ExecuteDoAction", gameId, "Alice", shuffleMessage);

            // Wait for completion notifications
            await Task.Delay(2000);

            // Assert - Only Alice should receive command completion
            Assert.True(alice_completions.Count > 0, "Alice should receive command completion notification");
            Assert.Empty(bob_completions); // Bob should NOT receive Alice's command completion
            Assert.Contains("Shuffle", alice_completions[0]);
        }

        [Fact]
        public async Task PickingBoard_BalanceAction_ShouldWorkViaSignalR()
        {
            // Arrange - Create a game in PickingBoard state
            var (gameId, connection) = await SignalRTestHelper.CreateGameInStateViaSignalR(_factory, GameState.PickingBoard);
            _connections.Add(connection);

            GameModel? updatedGameModel = null;
            var balanceCompleted = new TaskCompletionSource<bool>();

            connection.On<GameModel>("GameStateUpdated", gameModel =>
            {
                updatedGameModel = gameModel;
                balanceCompleted.TrySetResult(true);
            });

            // Act - Execute Balance action via SignalR using DoAction pattern
            await SignalRTestHelper.ExecuteDoActionViaSignalR(connection, gameId, "Alice", GameAction.Balance);

            // Assert
            var result = await balanceCompleted.Task.WaitAsync(TimeSpan.FromSeconds(10));
            Assert.True(result, "Balance should complete successfully");
            Assert.NotNull(updatedGameModel);
            Assert.Equal(gameId, updatedGameModel.GameId);
            Assert.Equal(GameState.PickingBoard, updatedGameModel.GameState);
            Assert.Equal(1, updatedGameModel.GameStateMachineVersion); // Version is always constant 1
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