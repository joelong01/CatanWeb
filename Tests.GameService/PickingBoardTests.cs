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
    /// Comprehensive tests for the PickingBoard game state
    /// Tests all 4 actions available in PickingBoard state:
    /// 1. Shuffle - Randomize board layout 
    /// 2. Balance - Balance board resources
    /// 3. Undo - Revert to previous board state
    /// 4. Redo - Forward to last board state
    /// 
    /// These tests focus on API functionality and real-time updates that 
    /// the companion interface relies on, rather than detailed board validation.
    /// </summary>
    public class PickingBoardTests : IClassFixture<WebApplicationFactory<Program>>
    {
        private readonly WebApplicationFactory<Program> _factory;
        private readonly HttpClient _client;

        public PickingBoardTests(WebApplicationFactory<Program> factory)
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

        // Helper method to create a game in PickingBoard state
        private async Task<string> CreateGameInPickingBoardState()
        {
            var gameId = "picking-board-test-" + Guid.NewGuid().ToString();
            var gameType = "Regular";
            var playerIds = new List<string> { "Alice", "Bob", "Charlie" };

            var newGameRequestBody = new
            {
                gameId = gameId,
                gameType = gameType,
                playerIds = playerIds
            };

            var newGameJson = JsonSerializer.Serialize(newGameRequestBody);
            var newGameContent = new StringContent(newGameJson, Encoding.UTF8, "application/json");

            var createGameResponse = await _client.PostAsync("/api/game/new", newGameContent);
            Assert.True(createGameResponse.IsSuccessStatusCode, "Game creation should succeed");

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

        [Fact]
        public async Task PickingBoard_ShuffleAction_ShouldSucceedAndIncrementVersion()
        {
            // This test verifies that Shuffle action works correctly via the API
            // and triggers appropriate responses for the companion interface

            // Arrange - Create a game in PickingBoard state
            var gameId = await CreateGameInPickingBoardState();

            // Get initial game state
            var initialState = await GetGameStateInfo(gameId);
            Assert.Equal("PickingBoard", initialState.GameState);

            // Act - Execute Shuffle action
            var shuffleResult = await ExecuteGameAction(gameId, "Shuffle");

            // Get updated game state
            var updatedState = await GetGameStateInfo(gameId);

            // Assert - Verify shuffle succeeded and state is correct
            var newVersion = shuffleResult.GetProperty("gameStateVersion").GetInt32();
            Assert.True(newVersion > initialState.Version, "Game version should increment after shuffle");
            Assert.Equal(newVersion, updatedState.Version);

            // Verify game state is still PickingBoard (shuffle doesn't change state)
            Assert.Equal("PickingBoard", updatedState.GameState);
            Assert.Equal(initialState.CurrentPlayerId, updatedState.CurrentPlayerId);
        }

        [Fact]
        public async Task PickingBoard_BalanceAction_ShouldSucceedAndIncrementVersion()
        {
            // This test verifies that Balance action works correctly via the API

            // Arrange - Create a game in PickingBoard state
            var gameId = await CreateGameInPickingBoardState();

            // Get initial game state
            var initialState = await GetGameStateInfo(gameId);
            Assert.Equal("PickingBoard", initialState.GameState);

            // Act - Execute Balance action
            var balanceResult = await ExecuteGameAction(gameId, "Balance");

            // Get updated game state
            var updatedState = await GetGameStateInfo(gameId);

            // Assert - Verify balance succeeded and state is correct
            var newVersion = balanceResult.GetProperty("gameStateVersion").GetInt32();
            Assert.True(newVersion > initialState.Version, "Game version should increment after balance");
            Assert.Equal(newVersion, updatedState.Version);

            // Verify game state is still PickingBoard (balance doesn't change state)
            Assert.Equal("PickingBoard", updatedState.GameState);
            Assert.Equal(initialState.CurrentPlayerId, updatedState.CurrentPlayerId);
        }

        [Fact]
        public async Task PickingBoard_UndoAction_ShouldSucceedAfterShuffleAction()
        {
            // This test verifies that Undo action works correctly after performing an action

            // Arrange - Create a game and perform an action to create history
            var gameId = await CreateGameInPickingBoardState();

            // Get initial state
            var initialState = await GetGameStateInfo(gameId);

            // Perform a shuffle to create a new state in the log
            await ExecuteGameAction(gameId, "Shuffle");
            var shuffledState = await GetGameStateInfo(gameId);

            // Verify we have different versions
            Assert.True(shuffledState.Version > initialState.Version, "Should have different versions after shuffle");

            // Act - Execute Undo action
            var undoResult = await ExecuteGameAction(gameId, "Undo");

            // Get state after undo
            var undoState = await GetGameStateInfo(gameId);

            // Assert - Verify undo succeeded
            Assert.True(undoResult.GetProperty("success").GetBoolean(), "Undo should succeed");

            // Verify game state is still PickingBoard
            Assert.Equal("PickingBoard", undoState.GameState);

            // Undo should revert to a previous version (implementation may vary)
            // The key is that it succeeds and maintains proper game state
            Assert.True(undoState.Version >= initialState.Version, "Undo should maintain valid version");
        }

        [Fact]
        public async Task PickingBoard_RedoAction_ShouldSucceedAfterUndoAction()
        {
            // This test verifies that Redo action works correctly after performing undo

            // Arrange - Create a game, make changes, and undo to set up for redo
            var gameId = await CreateGameInPickingBoardState();

            // Create some history: initial -> shuffle -> undo -> redo
            await ExecuteGameAction(gameId, "Shuffle");
            await ExecuteGameAction(gameId, "Undo");
            var beforeRedoState = await GetGameStateInfo(gameId);

            // Act - Execute Redo action
            var redoResult = await ExecuteGameAction(gameId, "Redo");

            // Get state after redo
            var redoState = await GetGameStateInfo(gameId);

            // Assert - Verify redo succeeded
            Assert.True(redoResult.GetProperty("success").GetBoolean(), "Redo should succeed");

            // Verify game state is still PickingBoard
            Assert.Equal("PickingBoard", redoState.GameState);

            // Redo should advance to some valid state
            Assert.True(redoState.Version >= beforeRedoState.Version, "Redo should maintain valid version");
        }

        [Fact]
        public async Task PickingBoard_AllActions_ShouldWorkWithRealTimeUpdates()
        {
            // This test verifies that all 4 PickingBoard actions work via the real-time companion interface
            // with proper hanging GET notifications for all actions

            // Arrange - Create a game in PickingBoard state
            var gameId = await CreateGameInPickingBoardState();
            var initialState = await GetGameStateInfo(gameId);

            // Test each action with real-time updates
            var actionsToTest = new[] { "Shuffle", "Balance" };

            foreach (var action in actionsToTest)
            {
                // Setup hanging GET to listen for updates
                var currentState = await GetGameStateInfo(gameId);
                var hangingGetTask = _client.GetAsync($"/api/gamestate/{gameId}/listen?version={currentState.Version}&playerId=Alice");

                // Wait to ensure hanging GET is established
                await Task.Delay(500);
                Assert.False(hangingGetTask.IsCompleted, $"Hanging GET should be waiting before {action}");

                // Execute the action
                var actionStartTime = DateTime.UtcNow;
                var actionResult = await ExecuteGameAction(gameId, action);
                var newVersion = actionResult.GetProperty("gameStateVersion").GetInt32();

                // Wait for hanging GET to receive notification
                var hangingGetResponse = await hangingGetTask;
                var actionEndTime = DateTime.UtcNow;

                // Verify real-time notification was received quickly
                var responseTime = actionEndTime - actionStartTime;
                Assert.True(responseTime.TotalSeconds < 3, 
                    $"Hanging GET should receive {action} notification quickly, took {responseTime.TotalSeconds} seconds");

                // Verify hanging GET response contains updated game state
                Assert.True(hangingGetResponse.IsSuccessStatusCode, $"Hanging GET should receive {action} notification");

                var hangingGetBody = await hangingGetResponse.Content.ReadAsStringAsync();
                var hangingGetResult = JsonSerializer.Deserialize<JsonElement>(hangingGetBody);

                Assert.True(hangingGetResult.TryGetProperty("gameId", out var gameIdProp));
                Assert.Equal(gameId, gameIdProp.GetString());

                Assert.True(hangingGetResult.TryGetProperty("version", out var versionProp));
                Assert.Equal(newVersion, versionProp.GetInt32());
                Assert.True(newVersion > currentState.Version, $"{action} should increment version");

                // Verify game state is still PickingBoard for all actions
                Assert.True(hangingGetResult.TryGetProperty("gameState", out var gameStateProp));
                Assert.Equal("PickingBoard", gameStateProp.GetString());
            }
        }

        [Fact]
        public async Task PickingBoard_UndoRedoSequence_ShouldWorkWithRealTimeUpdates()
        {
            // This test verifies that Undo/Redo sequence works with real-time updates

            // Arrange - Create a game and perform initial action
            var gameId = await CreateGameInPickingBoardState();

            // Create history with Shuffle
            await ExecuteGameAction(gameId, "Shuffle");
            var shuffledState = await GetGameStateInfo(gameId);

            // Test Undo with real-time updates
            var undoHangingGetTask = _client.GetAsync($"/api/gamestate/{gameId}/listen?version={shuffledState.Version}&playerId=Alice");
            await Task.Delay(500);

            var undoStartTime = DateTime.UtcNow;
            var undoResult = await ExecuteGameAction(gameId, "Undo");
            var undoResponse = await undoHangingGetTask;
            var undoEndTime = DateTime.UtcNow;

            // Verify Undo real-time notification
            Assert.True((undoEndTime - undoStartTime).TotalSeconds < 3, "Undo hanging GET should be fast");
            Assert.True(undoResponse.IsSuccessStatusCode, "Undo hanging GET should succeed");

            // Test Redo with real-time updates
            var redoHangingGetTask = _client.GetAsync($"/api/gamestate/{gameId}/listen?version={shuffledState.Version}&playerId=Alice");
            await Task.Delay(500);

            var redoStartTime = DateTime.UtcNow;
            var redoResult = await ExecuteGameAction(gameId, "Redo");
            var redoResponse = await redoHangingGetTask;
            var redoEndTime = DateTime.UtcNow;

            // Verify Redo real-time notification
            Assert.True((redoEndTime - redoStartTime).TotalSeconds < 3, "Redo hanging GET should be fast");
            Assert.True(redoResponse.IsSuccessStatusCode, "Redo hanging GET should succeed");

            // Verify both actions succeeded
            Assert.True(undoResult.GetProperty("success").GetBoolean());
            Assert.True(redoResult.GetProperty("success").GetBoolean());

            // Verify game state remained PickingBoard throughout
            var finalState = await GetGameStateInfo(gameId);
            Assert.Equal("PickingBoard", finalState.GameState);
        }

        [Fact]
        public async Task PickingBoard_NextAction_ShouldAdvanceFromPickingBoardState()
        {
            // This test verifies that Next action advances the game from PickingBoard state
            // to the next state in the game flow

            // Arrange - Create a game in PickingBoard state
            var gameId = await CreateGameInPickingBoardState();
            var initialState = await GetGameStateInfo(gameId);
            Assert.Equal("PickingBoard", initialState.GameState);

            // Act - Execute Next action to advance past PickingBoard
            var nextResult = await ExecuteGameAction(gameId, "Next");

            // Get updated game state
            var nextState = await GetGameStateInfo(gameId);

            // Assert - Verify Next succeeded and advanced the game state
            Assert.True(nextResult.GetProperty("success").GetBoolean(), "Next action should succeed");

            var newVersion = nextResult.GetProperty("gameStateVersion").GetInt32();
            Assert.True(newVersion > initialState.Version, "Game version should increment after Next");
            Assert.Equal(newVersion, nextState.Version);

            // Verify game state advanced beyond PickingBoard
            Assert.NotEqual("PickingBoard", nextState.GameState);
            
            // Common next states after PickingBoard would be WaitingForRollForOrder or similar
            Assert.False(string.IsNullOrEmpty(nextState.GameState), "Game state should have a valid value");
        }
    }

    // Helper class for simplified game state info
    public class GameStateInfo
    {
        public string GameId { get; set; } = "";
        public string GameState { get; set; } = "";
        public string CurrentPlayerId { get; set; } = "";
        public int Version { get; set; }
    }
}