using Xunit;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;
using System.Text;
using System.Text.Json;
using Catan3.GameService.Controllers;
using Catan3.Shared.Models;
using System.Net.Sockets;
using System.Net;
using Catan3.GameService.Services;

namespace Tests.GameService
{
    /// <summary>
    /// Comprehensive tests for the RollForOrder game states
    /// Tests the complete flow from WaitingForRollForOrder to FinishedRollOrder:
    /// 1. WaitingForRollForOrder - Players are expected to roll dice to determine order
    /// 2. FinishedRollOrder - Players can set the final playing order based on roll results
    /// 
    /// These tests focus on player order management and game state transitions that
    /// the companion interface relies on for order determination workflow.
    /// </summary>
    public class RollForOrderTests : IClassFixture<WebApplicationFactory<Program>>
    {
        private readonly WebApplicationFactory<Program> _factory;
        private readonly HttpClient _client;

        public RollForOrderTests(WebApplicationFactory<Program> factory)
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
            _client = _factory.CreateClient();
        }

        // Helper method to create a game in WaitingForRollForOrder state
        private async Task<string> CreateGameInWaitingForRollForOrderState()
        {
            return await GamePhaseHelper.CreateGameInWaitingForRollForOrderState(_client);
        }

        // Helper method to create a game in FinishedRollOrder state
        private async Task<string> CreateGameInFinishedRollOrderState()
        {
            var gameId = await CreateGameInWaitingForRollForOrderState();
            
            // Advance from WaitingForRollForOrder to FinishedRollOrder
            await ExecuteGameAction(gameId, "Next");

            return gameId;
        }

        // Helper method to get game state info
        private async Task<GameStateInfo> GetGameStateInfo(string gameId)
        {
            var gameStateResponse = await _client.GetAsync($"/api/gamestate/{gameId}");
            Assert.True(gameStateResponse.IsSuccessStatusCode, "Should get game state successfully");

            var gameStateBody = await gameStateResponse.Content.ReadAsStringAsync();
            var gameState = JsonSerializer.Deserialize<JsonElement>(gameStateBody);

            return new GameStateInfo
            {
                GameId = gameState.GetProperty("gameId").GetString() ?? "",
                GameState = gameState.GetProperty("gameState").GetString() ?? "",
                Version = gameState.GetProperty("version").GetInt32(),
                CurrentPlayerId = gameState.GetProperty("currentPlayerId").GetString() ?? ""
            };
        }

        // Helper method to get detailed player information from game state
        private async Task<List<PlayerInfo>> GetDetailedPlayerInfo(string gameId)
        {
            var gameStateResponse = await _client.GetAsync($"/api/gamestate/{gameId}");
            Assert.True(gameStateResponse.IsSuccessStatusCode, "Should get game state successfully");

            var gameStateBody = await gameStateResponse.Content.ReadAsStringAsync();
            var gameState = JsonSerializer.Deserialize<JsonElement>(gameStateBody);

            // Extract player information from the GameModel
            var players = gameState.GetProperty("players").EnumerateArray()
                .Select(p => new PlayerInfo
                {
                    Id = p.GetProperty("id").GetString() ?? "",
                    Name = p.GetProperty("id").GetString() ?? "", // Using Id as name
                    IsCurrentPlayer = p.GetProperty("id").GetString() == gameState.GetProperty("currentPlayerId").GetString()
                }).ToList();

            return players;
        }

        // Helper method to execute a game action
        private async Task<JsonElement> ExecuteGameAction(string gameId, string action, string playerId = "Alice")
        {
            var actionBody = new
            {
                gameId = gameId,
                playerId = playerId,
                messageType = "DoAction",
                messageData = new { action = action }
            };

            var actionJson = JsonSerializer.Serialize(actionBody);
            var actionContent = new StringContent(actionJson, Encoding.UTF8, "application/json");

            var actionResponse = await _client.PostAsync("/api/game/action", actionContent);
            Assert.True(actionResponse.IsSuccessStatusCode, $"{action} action should succeed");

            var actionResponseBody = await actionResponse.Content.ReadAsStringAsync();
            var actionResult = JsonSerializer.Deserialize<JsonElement>(actionResponseBody);
            Assert.True(actionResult.GetProperty("success").GetBoolean(), $"{action} should return success");

            return actionResult;
        }

        // Helper method to set player order
        private async Task<JsonElement> SetPlayerOrder(string gameId, List<string> playerIds, string requestingPlayerId = "Alice")
        {
            var orderBody = new
            {
                gameId = gameId,
                playerId = requestingPlayerId,
                messageType = "SetPlayerOrderMessage",
                messageData = new { playerIds = playerIds }
            };

            var orderJson = JsonSerializer.Serialize(orderBody);
            var orderContent = new StringContent(orderJson, Encoding.UTF8, "application/json");

            var orderResponse = await _client.PostAsync("/api/game/action", orderContent);
            Assert.True(orderResponse.IsSuccessStatusCode, "SetPlayerOrder action should succeed");

            var orderResponseBody = await orderResponse.Content.ReadAsStringAsync();
            var orderResult = JsonSerializer.Deserialize<JsonElement>(orderResponseBody);
            Assert.True(orderResult.GetProperty("success").GetBoolean(), "SetPlayerOrder should return success");

            return orderResult;
        }

        [Fact]
        public async Task RollForOrder_TransitionFromPickingBoard_ShouldAdvanceToWaitingForRollForOrder()
        {
            // This test verifies that Next action from PickingBoard correctly advances to WaitingForRollForOrder

            // Arrange - Create a game in PickingBoard state using server-generated GameId
            var gameType = "Regular";
            var playerIds = new List<string> { "Alice", "Bob", "Charlie" };

            var newGameRequestBody = new
            {
                gameType = gameType,
                playerIds = playerIds
            };

            var newGameJson = JsonSerializer.Serialize(newGameRequestBody);
            var newGameContent = new StringContent(newGameJson, Encoding.UTF8, "application/json");

            var createGameResponse = await _client.PostAsync("/api/game/new", newGameContent);
            Assert.True(createGameResponse.IsSuccessStatusCode, "Game creation should succeed");

            // Get the server-generated gameId
            var responseBody = await createGameResponse.Content.ReadAsStringAsync();
            var result = JsonSerializer.Deserialize<JsonElement>(responseBody);
            var gameId = result.GetProperty("gameId").GetString()!;

            // Verify initial state is PickingBoard
            var initialState = await GetGameStateInfo(gameId);
            Assert.Equal("PickingBoard", initialState.GameState);

            // Act - Execute Next action to advance to WaitingForRollForOrder
            var nextResult = await ExecuteGameAction(gameId, "Next");

            // Get updated game state
            var nextState = await GetGameStateInfo(gameId);

            // Assert - Verify transition to WaitingForRollForOrder
            Assert.True(nextResult.GetProperty("success").GetBoolean(), "Next action should succeed");

            var newVersion = nextResult.GetProperty("gameStateVersion").GetInt32();
            Assert.Equal(1, newVersion); // Version is static (1), not incremented
            Assert.Equal(newVersion, nextState.Version);

            // Verify game state advanced to WaitingForRollForOrder
            Assert.Equal("WaitingForRollForOrder", nextState.GameState);
        }

        [Fact]
        public async Task WaitingForRollForOrder_NextAction_ShouldAdvanceToFinishedRollOrder()
        {
            // This test verifies that Next action from WaitingForRollForOrder advances to FinishedRollOrder

            // Arrange - Create a game in WaitingForRollForOrder state
            var gameId = await CreateGameInWaitingForRollForOrderState();

            // Verify initial state is WaitingForRollForOrder
            var initialState = await GetGameStateInfo(gameId);
            Assert.Equal("WaitingForRollForOrder", initialState.GameState);

            // Act - Execute Next action to advance to FinishedRollOrder
            var nextResult = await ExecuteGameAction(gameId, "Next");

            // Get updated game state
            var nextState = await GetGameStateInfo(gameId);

            // Assert - Verify transition to FinishedRollOrder
            Assert.True(nextResult.GetProperty("success").GetBoolean(), "Next action should succeed");

            var newVersion = nextResult.GetProperty("gameStateVersion").GetInt32();
            Assert.Equal(1, newVersion); // Version is static (1), not incremented
            Assert.Equal(newVersion, nextState.Version);

            // Verify game state advanced to FinishedRollOrder
            Assert.Equal("FinishedRollOrder", nextState.GameState);
        }

        [Fact]
        public async Task FinishedRollOrder_NoOrderChange_ShouldMaintainOriginalOrder()
        {
            // This test verifies that when no order changes are made in FinishedRollOrder,
            // the order remains as originally set when the game was created

            // Arrange - Create a game in FinishedRollOrder state
            var gameId = await CreateGameInFinishedRollOrderState();

            // Verify we're in FinishedRollOrder state
            var initialState = await GetGameStateInfo(gameId);
            Assert.Equal("FinishedRollOrder", initialState.GameState);

            // Get initial player order
            var initialPlayers = await GetDetailedPlayerInfo(gameId);
            var initialPlayerOrder = initialPlayers.Select(p => p.Id).ToList();
            var initialCurrentPlayer = initialPlayers.FirstOrDefault(p => p.IsCurrentPlayer);

            Assert.NotNull(initialCurrentPlayer);
            Assert.Equal("Alice", initialCurrentPlayer.Id); // First player in creation order should be current
            Assert.Equal(new List<string> { "Alice", "Bob", "Charlie" }, initialPlayerOrder);

            // Act - Advance to next state without changing order
            var nextResult = await ExecuteGameAction(gameId, "Next");

            // Get final state
            var finalState = await GetGameStateInfo(gameId);
            var finalPlayers = await GetDetailedPlayerInfo(gameId);
            var finalPlayerOrder = finalPlayers.Select(p => p.Id).ToList();
            var finalCurrentPlayer = finalPlayers.FirstOrDefault(p => p.IsCurrentPlayer);

            // Assert - Order should remain unchanged
            Assert.True(nextResult.GetProperty("success").GetBoolean(), "Next action should succeed");
            Assert.Equal("BeginResourceAllocation", finalState.GameState); // Should advance to next state
            Assert.Equal(initialPlayerOrder, finalPlayerOrder);
            Assert.NotNull(finalCurrentPlayer);
            Assert.Equal(initialCurrentPlayer.Id, finalCurrentPlayer.Id);
        }

        [Fact]
        public async Task FinishedRollOrder_SetLastPlayerFirst_ShouldRotateOrderCorrectly()
        {
            // This test verifies the scenario where the last player (Charlie) is set to go first.
            // Original order: Alice, Bob, Charlie
            // Expected new order: Charlie, Alice, Bob (last player first, others maintain relative order)

            // Arrange - Create a game in FinishedRollOrder state
            var gameId = await CreateGameInFinishedRollOrderState();

            // Verify initial state and order
            var initialState = await GetGameStateInfo(gameId);
            Assert.Equal("FinishedRollOrder", initialState.GameState);

            var initialPlayers = await GetDetailedPlayerInfo(gameId);
            var initialPlayerOrder = initialPlayers.Select(p => p.Id).ToList();
            Assert.Equal(new List<string> { "Alice", "Bob", "Charlie" }, initialPlayerOrder);

            // Act - Set Charlie (last player) to go first
            var newOrder = new List<string> { "Charlie", "Alice", "Bob" };
            var setOrderResult = await SetPlayerOrder(gameId, newOrder);

            // Get updated state after order change
            var updatedState = await GetGameStateInfo(gameId);
            var updatedPlayers = await GetDetailedPlayerInfo(gameId);
            var updatedPlayerOrder = updatedPlayers.Select(p => p.Id).ToList();
            var updatedCurrentPlayer = updatedPlayers.FirstOrDefault(p => p.IsCurrentPlayer);

            // Assert - Verify order changed correctly
            Assert.True(setOrderResult.GetProperty("success").GetBoolean(), "SetPlayerOrder should succeed");

            // Verify the new order is applied correctly
            Assert.Equal(newOrder, updatedPlayerOrder);
            Assert.NotNull(updatedCurrentPlayer);
            Assert.Equal("Charlie", updatedCurrentPlayer.Id); // Charlie should now be the current player

            // Verify we're still in FinishedRollOrder state (setting order doesn't advance state)
            Assert.Equal("FinishedRollOrder", updatedState.GameState);

            // Act - Now advance to next state to confirm order is preserved
            var nextResult = await ExecuteGameAction(gameId, "Next");
            var finalState = await GetGameStateInfo(gameId);
            var finalPlayers = await GetDetailedPlayerInfo(gameId);
            var finalPlayerOrder = finalPlayers.Select(p => p.Id).ToList();
            var finalCurrentPlayer = finalPlayers.FirstOrDefault(p => p.IsCurrentPlayer);

            // Assert - Order should be preserved after state transition
            Assert.True(nextResult.GetProperty("success").GetBoolean(), "Next action should succeed");
            Assert.Equal("BeginResourceAllocation", finalState.GameState);
            Assert.Equal(newOrder, finalPlayerOrder);
            Assert.NotNull(finalCurrentPlayer);
            Assert.Equal("Charlie", finalCurrentPlayer.Id); // Charlie should remain the current player
        }

        [Fact]
        public async Task FinishedRollOrder_SetMiddlePlayerFirst_ShouldRotateOrderCorrectly()
        {
            // This test verifies the scenario where the middle player (Bob) is set to go first.
            // Original order: Alice, Bob, Charlie
            // Expected new order: Bob, Charlie, Alice (middle player first, others maintain relative order)

            // Arrange - Create a game in FinishedRollOrder state
            var gameId = await CreateGameInFinishedRollOrderState();

            // Verify initial state and order
            var initialState = await GetGameStateInfo(gameId);
            Assert.Equal("FinishedRollOrder", initialState.GameState);

            // Act - Set Bob (middle player) to go first
            var newOrder = new List<string> { "Bob", "Charlie", "Alice" };
            var setOrderResult = await SetPlayerOrder(gameId, newOrder);

            // Get updated state after order change
            var updatedState = await GetGameStateInfo(gameId);
            var updatedPlayers = await GetDetailedPlayerInfo(gameId);
            var updatedPlayerOrder = updatedPlayers.Select(p => p.Id).ToList();
            var updatedCurrentPlayer = updatedPlayers.FirstOrDefault(p => p.IsCurrentPlayer);

            // Assert - Verify order changed correctly
            Assert.True(setOrderResult.GetProperty("success").GetBoolean(), "SetPlayerOrder should succeed");

            // Verify the new order is applied correctly
            Assert.Equal(newOrder, updatedPlayerOrder);
            Assert.NotNull(updatedCurrentPlayer);
            Assert.Equal("Bob", updatedCurrentPlayer.Id); // Bob should now be the current player

            // Advance to next state to confirm order is preserved
            var nextResult = await ExecuteGameAction(gameId, "Next");
            var finalState = await GetGameStateInfo(gameId);
            var finalPlayers = await GetDetailedPlayerInfo(gameId);
            var finalPlayerOrder = finalPlayers.Select(p => p.Id).ToList();
            var finalCurrentPlayer = finalPlayers.FirstOrDefault(p => p.IsCurrentPlayer);

            // Assert - Order should be preserved after state transition
            Assert.Equal(newOrder, finalPlayerOrder);
            Assert.NotNull(finalCurrentPlayer);
            Assert.Equal("Bob", finalCurrentPlayer.Id); // Bob should remain the current player
        }

        [Fact]
        public async Task FinishedRollOrder_SetArbitraryOrder_ShouldApplyExactOrder()
        {
            // This test verifies that any arbitrary order can be set, not just rotations

            // Arrange - Create a game in FinishedRollOrder state
            var gameId = await CreateGameInFinishedRollOrderState();

            // Act - Set an arbitrary order (reverse of original)
            var newOrder = new List<string> { "Charlie", "Bob", "Alice" };
            var setOrderResult = await SetPlayerOrder(gameId, newOrder);

            // Get updated state after order change
            var updatedPlayers = await GetDetailedPlayerInfo(gameId);
            var updatedPlayerOrder = updatedPlayers.Select(p => p.Id).ToList();
            var updatedCurrentPlayer = updatedPlayers.FirstOrDefault(p => p.IsCurrentPlayer);

            // Assert - Verify the exact order is applied
            Assert.True(setOrderResult.GetProperty("success").GetBoolean(), "SetPlayerOrder should succeed");
            Assert.Equal(newOrder, updatedPlayerOrder);
            Assert.NotNull(updatedCurrentPlayer);
            Assert.Equal("Charlie", updatedCurrentPlayer.Id); // First player in new order should be current

            // Verify order is preserved through state transition
            await ExecuteGameAction(gameId, "Next");
            var finalPlayers = await GetDetailedPlayerInfo(gameId);
            var finalPlayerOrder = finalPlayers.Select(p => p.Id).ToList();

            Assert.Equal(newOrder, finalPlayerOrder);
        }

        [Fact]
        public async Task FinishedRollOrder_SetOrderWithRealTimeUpdates_ShouldNotifyAllClients()
        {
            // This test verifies that setting player order works with real-time updates
            // and all connected clients receive the updated order

            // Arrange - Create a game in FinishedRollOrder state
            var gameId = await CreateGameInFinishedRollOrderState();
            var initialState = await GetGameStateInfo(gameId);

            // Set up hanging GET connections for multiple clients
            var client1HangingGetTask = _client.GetAsync($"/api/gamestate/{gameId}/listen?version={initialState.Version}&playerId=Alice");
            var client2HangingGetTask = _client.GetAsync($"/api/gamestate/{gameId}/listen?version={initialState.Version}&playerId=Bob");
            var client3HangingGetTask = _client.GetAsync($"/api/gamestate/{gameId}/listen?version={initialState.Version}&playerId=Charlie");

            // Wait to ensure hanging GET requests are established
            await Task.Delay(500);

            // Verify hanging GETs are waiting
            Assert.False(client1HangingGetTask.IsCompleted, "Client 1 hanging GET should be waiting");
            Assert.False(client2HangingGetTask.IsCompleted, "Client 2 hanging GET should be waiting");
            Assert.False(client3HangingGetTask.IsCompleted, "Client 3 hanging GET should be waiting");

            // Act - Set new player order
            var newOrder = new List<string> { "Charlie", "Alice", "Bob" };
            var orderStartTime = DateTime.UtcNow;
            var setOrderResult = await SetPlayerOrder(gameId, newOrder);

            // Wait for all hanging GET requests to complete
            var client1Response = await client1HangingGetTask;
            var client2Response = await client2HangingGetTask;
            var client3Response = await client3HangingGetTask;
            var orderEndTime = DateTime.UtcNow;

            // Assert - Verify real-time notification was received quickly
            var responseTime = orderEndTime - orderStartTime;
            Assert.True(responseTime.TotalSeconds < 3, $"All clients should receive order updates quickly, took {responseTime.TotalSeconds} seconds");

            // Verify all clients received successful responses
            Assert.True(client1Response.IsSuccessStatusCode, "Client 1 should receive order notification");
            Assert.True(client2Response.IsSuccessStatusCode, "Client 2 should receive order notification");
            Assert.True(client3Response.IsSuccessStatusCode, "Client 3 should receive order notification");

            // Verify all clients have the same updated version
            var newVersion = setOrderResult.GetProperty("gameStateVersion").GetInt32();
            
            foreach (var response in new[] { client1Response, client2Response, client3Response })
            {
                var responseBody = await response.Content.ReadAsStringAsync();
                var responseData = JsonSerializer.Deserialize<JsonElement>(responseBody);

                Assert.True(responseData.TryGetProperty("gameId", out var gameIdProp));
                Assert.Equal(gameId, gameIdProp.GetString());

                Assert.True(responseData.TryGetProperty("version", out var versionProp));
                Assert.Equal(newVersion, versionProp.GetInt32());

                Assert.True(responseData.TryGetProperty("gameState", out var gameStateProp));
                Assert.Equal("FinishedRollOrder", gameStateProp.GetString());
            }

            // Verify the player order was actually updated
            var finalPlayers = await GetDetailedPlayerInfo(gameId);
            var finalPlayerOrder = finalPlayers.Select(p => p.Id).ToList();
            Assert.Equal(newOrder, finalPlayerOrder);
        }

        [Fact]
        public async Task FinishedRollOrder_SetOrderInWrongState_ShouldFail()
        {
            // This test verifies that SetPlayerOrder can only be called in the correct states
            // and fails gracefully when called in inappropriate states

            // Arrange - Create a game in PickingBoard state (not valid for SetPlayerOrder)
            var gameType = "Regular";
            var playerIds = new List<string> { "Alice", "Bob", "Charlie" };

            var newGameRequestBody = new
            {
                gameType = gameType,
                playerIds = playerIds
            };

            var newGameJson = JsonSerializer.Serialize(newGameRequestBody);
            var newGameContent = new StringContent(newGameJson, Encoding.UTF8, "application/json");

            var createGameResponse = await _client.PostAsync("/api/game/new", newGameContent);
            Assert.True(createGameResponse.IsSuccessStatusCode, "Game creation should succeed");

            // Get the server-generated gameId
            var responseBody = await createGameResponse.Content.ReadAsStringAsync();
            var result = JsonSerializer.Deserialize<JsonElement>(responseBody);
            var gameId = result.GetProperty("gameId").GetString()!;

            // Verify we're in PickingBoard state (not valid for SetPlayerOrder)
            var currentState = await GetGameStateInfo(gameId);
            Assert.Equal("PickingBoard", currentState.GameState);

            // Act - Try to set player order in PickingBoard state (should fail)
            var newOrder = new List<string> { "Charlie", "Alice", "Bob" };
            
            var orderBody = new
            {
                gameId = gameId,
                playerId = "Alice",
                messageType = "SetPlayerOrderMessage",
                messageData = new { playerIds = newOrder }
            };

            var orderJson = JsonSerializer.Serialize(orderBody);
            var orderContent = new StringContent(orderJson, Encoding.UTF8, "application/json");

            var orderResponse = await _client.PostAsync("/api/game/action", orderContent);

            // Assert - Should fail because we're in the wrong state
            Assert.Equal(System.Net.HttpStatusCode.InternalServerError, orderResponse.StatusCode);
            
            var responseBodyError = await orderResponse.Content.ReadAsStringAsync();
            Assert.Contains("Error executing action", responseBodyError);
        }

        [Fact]
        public async Task RollForOrder_CompleteWorkflow_ShouldSupportFullOrderDeterminationFlow()
        {
            // This test verifies the complete roll for order workflow:
            // PickingBoard ? WaitingForRollForOrder ? FinishedRollOrder ? Set Order ? Continue Game

            // Arrange - Start with a game in PickingBoard state using server-generated GameId
            var gameType = "Regular";
            var playerIds = new List<string> { "Alice", "Bob", "Charlie" };

            var newGameRequestBody = new
            {
                gameType = gameType,
                playerIds = playerIds
            };

            var newGameJson = JsonSerializer.Serialize(newGameRequestBody);
            var newGameContent = new StringContent(newGameJson, Encoding.UTF8, "application/json");

            var createGameResponse = await _client.PostAsync("/api/game/new", newGameContent);
            Assert.True(createGameResponse.IsSuccessStatusCode, "Game creation should succeed");

            // Get the server-generated gameId
            var responseBody = await createGameResponse.Content.ReadAsStringAsync();
            var result = JsonSerializer.Deserialize<JsonElement>(responseBody);
            var gameId = result.GetProperty("gameId").GetString()!;

            // Step 1: Verify initial state and player order
            var initialState = await GetGameStateInfo(gameId);
            var initialPlayers = await GetDetailedPlayerInfo(gameId);
            var initialPlayerOrder = initialPlayers.Select(p => p.Id).ToList();
            
            Assert.Equal("PickingBoard", initialState.GameState);
            Assert.Equal(new List<string> { "Alice", "Bob", "Charlie" }, initialPlayerOrder);
            
            // Note: currentPlayerId may not be set until after player order is established
            // In PickingBoard state, this is acceptable

            // Step 2: Advance to WaitingForRollForOrder
            await ExecuteGameAction(gameId, "Next");
            var rollForOrderState = await GetGameStateInfo(gameId);
            Assert.Equal("WaitingForRollForOrder", rollForOrderState.GameState);

            // Step 3: Advance to FinishedRollOrder
            await ExecuteGameAction(gameId, "Next");
            var finishedRollState = await GetGameStateInfo(gameId);
            Assert.Equal("FinishedRollOrder", finishedRollState.GameState);

            // Step 4: Set new player order (Charlie rolls highest and goes first)
            var newOrder = new List<string> { "Charlie", "Alice", "Bob" };
            var setOrderResult = await SetPlayerOrder(gameId, newOrder);
            Assert.True(setOrderResult.GetProperty("success").GetBoolean());

            // Verify order was applied
            var orderedPlayers = await GetDetailedPlayerInfo(gameId);
            var orderedPlayerOrder = orderedPlayers.Select(p => p.Id).ToList();
            var orderedCurrentPlayer = orderedPlayers.FirstOrDefault(p => p.IsCurrentPlayer);
            
            Assert.Equal(newOrder, orderedPlayerOrder);
            Assert.NotNull(orderedCurrentPlayer);
            Assert.Equal("Charlie", orderedCurrentPlayer.Id);

            // Step 5: Advance to next game phase
            await ExecuteGameAction(gameId, "Next");
            var nextPhaseState = await GetGameStateInfo(gameId);
            var nextPhasePlayers = await GetDetailedPlayerInfo(gameId);
            var nextPhasePlayerOrder = nextPhasePlayers.Select(p => p.Id).ToList();
            var nextPhaseCurrentPlayer = nextPhasePlayers.FirstOrDefault(p => p.IsCurrentPlayer);

            // Assert - Verify complete workflow succeeded
            Assert.Equal("BeginResourceAllocation", nextPhaseState.GameState);
            Assert.Equal(newOrder, nextPhasePlayerOrder); // Order preserved
            Assert.NotNull(nextPhaseCurrentPlayer);
            Assert.Equal("Charlie", nextPhaseCurrentPlayer.Id); // Charlie remains current player

            // Final verification: ensure the workflow completed correctly
            // The game should now be in resource allocation with Charlie as the first player
            // and the order set based on the "dice rolls"
            Assert.Equal(1, nextPhaseState.Version); // Version is static (1), not incremented
            Assert.NotEqual(initialPlayerOrder, nextPhasePlayerOrder); // Order changed from initial
        }
    }

    // Helper class for detailed player information
    public class PlayerInfo
    {
        public string Id { get; set; } = "";
        public string Name { get; set; } = "";
        public bool IsCurrentPlayer { get; set; }
    }
}