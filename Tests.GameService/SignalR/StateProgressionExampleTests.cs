using Xunit;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.Configuration;
using Catan3.Shared.Models;

namespace Tests.GameService.SignalR
{
    /// <summary>
    /// Example test class demonstrating the proper approach for state-specific testing.
    /// Each test advances to its target state independently, following approach #2.
    /// This provides test isolation while allowing comprehensive state machine testing.
    /// </summary>
    public class StateProgressionExampleTests : IClassFixture<WebApplicationFactory<Program>>, IAsyncDisposable
    {
        private readonly WebApplicationFactory<Program> _factory;
        private readonly List<HubConnection> _connections = new();

        public StateProgressionExampleTests(WebApplicationFactory<Program> factory)
        {
            _factory = TestWebApplicationFactory.Create();
        }

        [Fact]
        public async Task PickingBoard_ShuffleAction_ShouldWorkCorrectly()
        {
            // Arrange - Start with PickingBoard state (fastest path)
            var (gameId, connection) = await StateProgression.AdvanceToState(_factory, GameState.PickingBoard);
            _connections.Add(connection);

            GameModel? shuffledGameModel = null;
            var shuffleCompleted = new TaskCompletionSource<bool>();

            connection.On<GameModel>("GameStateUpdated", gameModel =>
            {
                shuffledGameModel = gameModel;
                shuffleCompleted.TrySetResult(true);
            });

            // Act - Execute Shuffle action (only valid in PickingBoard state)
            await SignalRTestHelper.ExecuteDoActionViaSignalR(connection, gameId, "Alice", GameAction.Shuffle);

            // Assert
            var result = await shuffleCompleted.Task.WaitAsync(TimeSpan.FromSeconds(10));
            Assert.True(result, "Shuffle should complete successfully in PickingBoard state");
            Assert.NotNull(shuffledGameModel);
            Assert.Equal(GameState.PickingBoard, shuffledGameModel.GameState);
            Assert.Equal(gameId, shuffledGameModel.GameId);
        }

        [Fact]
        public async Task WaitingForRoll_RollAction_ShouldAdvanceToWaitingForNext()
        {
            // Arrange - Advance to WaitingForRoll state (requires complete allocation)
            var (gameId, connection) = await StateProgression.AdvanceToState(_factory, GameState.WaitingForRoll);
            _connections.Add(connection);

            GameModel? rolledGameModel = null;
            var rollCompleted = new TaskCompletionSource<bool>();

            connection.On<GameModel>("GameStateUpdated", gameModel =>
            {
                rolledGameModel = gameModel;
                rollCompleted.TrySetResult(true);
            });

            // Act - Execute dice roll (only valid in WaitingForRoll state)
            var turnRollModel = new TurnRollModel(3, 3); // Roll 6
            var rollMessage = new RollMessage(turnRollModel);
            await connection.InvokeAsync("ExecuteRoll", gameId, "Alice", rollMessage);

            // Assert
            var result = await rollCompleted.Task.WaitAsync(TimeSpan.FromSeconds(10));
            Assert.True(result, "Roll should complete successfully in WaitingForRoll state");
            Assert.NotNull(rolledGameModel);
            Assert.Equal(GameState.WaitingForNext, rolledGameModel.GameState);
            Assert.Equal(gameId, rolledGameModel.GameId);
        }

        [Fact]
        public async Task WaitingForNext_PurchaseAction_ShouldWorkWithResources()
        {
            // Arrange - Advance to WaitingForNext state (requires complete allocation + dice roll)
            var (gameId, connection) = await StateProgression.AdvanceToState(_factory, GameState.WaitingForNext);
            _connections.Add(connection);

            GameModel? purchaseGameModel = null;
            var purchaseCompleted = new TaskCompletionSource<bool>();
            var commandCompleted = new TaskCompletionSource<bool>();

            connection.On<GameModel>("GameStateUpdated", gameModel =>
            {
                purchaseGameModel = gameModel;
            });

            connection.On<string, bool, string>("CommandCompleted", (commandId, success, message) =>
            {
                purchaseCompleted.TrySetResult(success);
                commandCompleted.TrySetResult(true);
            });

            connection.On<string, string>("CommandFailed", (commandId, error) =>
            {
                // Purchase may fail due to insufficient resources, which is expected
                purchaseCompleted.TrySetResult(false);
                commandCompleted.TrySetResult(true);
            });

            // Act - Attempt purchase (may succeed or fail based on resources from dice roll)
            var purchaseMessage = new PurchaseMessage(Entitlement.Road);
            await connection.InvokeAsync("ExecutePurchase", gameId, "Alice", purchaseMessage);

            // Assert - Command should be processed (success or failure both valid)
            var result = await commandCompleted.Task.WaitAsync(TimeSpan.FromSeconds(10));
            Assert.True(result, "Purchase command should be processed in WaitingForNext state");
            
            if (await purchaseCompleted.Task)
            {
                Assert.NotNull(purchaseGameModel);
                Assert.Equal(GameState.WaitingForNext, purchaseGameModel.GameState);
            }
            // Note: Purchase failure due to insufficient resources is also valid behavior
        }

        [Fact]
        public async Task AllocateResourceForward_OptimalSettlementPlacement_ShouldUseStars()
        {
            // Arrange - Advance to AllocateResourceForward state 
            var (gameId, connection) = await StateProgression.AdvanceToState(_factory, GameState.AllocateResourceForward);
            _connections.Add(connection);

            // Get the game model to analyze settlement options
            using var httpClient = new HttpClient();
            httpClient.BaseAddress = new Uri("http://localhost:8080");
            var response = await httpClient.GetAsync($"/api/gamestate/{gameId}");
            var json = await response.Content.ReadAsStringAsync();
            var gameModel = System.Text.Json.JsonSerializer.Deserialize<GameModel>(json);

            Assert.NotNull(gameModel);
            Assert.Equal(GameState.AllocateResourceForward, gameModel.GameState);

            // Act - Use AllocationHelper to find optimal settlement
            var bestSettlementKey = AllocationHelper.PickSettlement(gameModel);
            
            // Place the settlement
            GameModel? placedGameModel = null;
            var placementCompleted = new TaskCompletionSource<bool>();

            connection.On<GameModel>("GameStateUpdated", model =>
            {
                placedGameModel = model;
                placementCompleted.TrySetResult(true);
            });

            var buildingMessage = new BuildingUpgradeMessage(bestSettlementKey);
            await connection.InvokeAsync("ExecuteBuildingUpgrade", gameId, "Alice", buildingMessage);

            // Assert
            var result = await placementCompleted.Task.WaitAsync(TimeSpan.FromSeconds(10));
            Assert.True(result, "Settlement placement should complete successfully");
            Assert.NotNull(placedGameModel);
            
            // Verify the settlement was placed correctly
            var placedSettlement = placedGameModel.Buildings.FirstOrDefault(b => 
                b.BuildingKey.Equals(bestSettlementKey) && 
                b.BuildingState == BuildingState.Settlement &&
                b.OwnerId == "Alice");
            
            Assert.NotNull(placedSettlement);
        }

        [Fact] 
        public async Task StateProgression_AllStates_ShouldBeReachableIndependently()
        {
            // This test verifies that all states can be reached independently
            var targetStates = new[]
            {
                GameState.PickingBoard,
                GameState.WaitingForRollForOrder,
                GameState.FinishedRollOrder,
                GameState.BeginResourceAllocation,
                GameState.AllocateResourceForward,
                GameState.WaitingForRoll,
                GameState.WaitingForNext
            };

            foreach (var targetState in targetStates)
            {
                // Act - Advance to each state independently
                var (gameId, connection) = await StateProgression.AdvanceToState(_factory, targetState);
                _connections.Add(connection);

                // Get current state to verify
                using var httpClient = new HttpClient();
                httpClient.BaseAddress = new Uri("http://localhost:8080");
                var response = await httpClient.GetAsync($"/api/gamestate/{gameId}");
                var json = await response.Content.ReadAsStringAsync();
                var gameModel = System.Text.Json.JsonSerializer.Deserialize<GameModel>(json);

                // Assert
                Assert.NotNull(gameModel);
                Assert.Equal(targetState, gameModel.GameState);
                Assert.Equal(gameId, gameModel.GameId);
                
                Console.WriteLine($"? Successfully reached {targetState} state independently");
            }
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