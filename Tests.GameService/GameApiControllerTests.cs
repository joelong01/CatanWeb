using Xunit;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using System.Text;
using System.Text.Json;
using Catan3.GameService.Controllers;
using Catan3.Shared.Models;

namespace Tests.GameService
{
    public class GameApiControllerTests : IClassFixture<WebApplicationFactory<Program>>
    {
        private readonly WebApplicationFactory<Program> _factory;
        private readonly HttpClient _client;

        public GameApiControllerTests(WebApplicationFactory<Program> factory)
        {
            _factory = factory;
            _client = _factory.CreateClient();
        }

        [Fact]
        public async Task NewGame_WithValidRegularGame3Players_ShouldCreateGameSuccessfully()
        {
            // Arrange
            var gameId = "test-game-" + Guid.NewGuid().ToString();
            var gameType = "Regular";
            var playerIds = new List<string> { "Alice", "Bob", "Charlie" };

            var requestBody = new
            {
                gameId = gameId,
                gameType = gameType,
                playerIds = playerIds
            };

            var json = JsonSerializer.Serialize(requestBody);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            // Act
            var response = await _client.PostAsync("/api/game/new", content);

            // Assert
            Assert.True(response.IsSuccessStatusCode, $"Expected success but got {response.StatusCode}");
            
            var responseBody = await response.Content.ReadAsStringAsync();
            var result = JsonSerializer.Deserialize<JsonElement>(responseBody);

            Assert.True(result.GetProperty("success").GetBoolean());
            Assert.True(result.GetProperty("gameStateVersion").GetInt32() > 0);
            Assert.Equal("New game created successfully", result.GetProperty("message").GetString());

            // Verify the game was actually created by getting its state
            var gameStateResponse = await _client.GetAsync($"/api/gamestate/{gameId}");
            Assert.True(gameStateResponse.IsSuccessStatusCode);

            var gameStateBody = await gameStateResponse.Content.ReadAsStringAsync();
            var gameState = JsonSerializer.Deserialize<JsonElement>(gameStateBody);

            Assert.Equal(gameId, gameState.GetProperty("gameId").GetString());
            Assert.True(gameState.TryGetProperty("currentPlayerId", out var currentPlayerId));
            Assert.Contains(currentPlayerId.GetString(), playerIds);
        }

        [Fact]
        public async Task NewGame_WithValidExpansionGame4Players_ShouldCreateGameSuccessfully()
        {
            // Arrange
            var gameId = "expansion-game-" + Guid.NewGuid().ToString();
            var gameType = "Expansion";
            var playerIds = new List<string> { "Player1", "Player2", "Player3", "Player4" };

            var requestBody = new
            {
                gameId = gameId,
                gameType = gameType,
                playerIds = playerIds
            };

            var json = JsonSerializer.Serialize(requestBody);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            // Act
            var response = await _client.PostAsync("/api/game/new", content);

            // Assert
            Assert.True(response.IsSuccessStatusCode);
            
            var responseBody = await response.Content.ReadAsStringAsync();
            var result = JsonSerializer.Deserialize<JsonElement>(responseBody);

            Assert.True(result.GetProperty("success").GetBoolean());
            Assert.Equal("New game created successfully", result.GetProperty("message").GetString());

            // Verify game state shows expansion game with 4 players
            var gameStateResponse = await _client.GetAsync($"/api/gamestate/{gameId}");
            Assert.True(gameStateResponse.IsSuccessStatusCode);

            var gameStateBody = await gameStateResponse.Content.ReadAsStringAsync();
            var gameState = JsonSerializer.Deserialize<JsonElement>(gameStateBody);

            Assert.Equal(gameId, gameState.GetProperty("gameId").GetString());
            
            // Verify players endpoint returns all 4 players
            var playersResponse = await _client.GetAsync($"/api/players/{gameId}");
            Assert.True(playersResponse.IsSuccessStatusCode);

            var playersBody = await playersResponse.Content.ReadAsStringAsync();
            var playersResult = JsonSerializer.Deserialize<JsonElement>(playersBody);

            var players = playersResult.GetProperty("players").EnumerateArray().ToList();
            Assert.Equal(4, players.Count);

            // Verify all player IDs are present
            var returnedPlayerIds = players.Select(p => p.GetProperty("id").GetString()).ToList();
            foreach (var playerId in playerIds)
            {
                Assert.Contains(playerId, returnedPlayerIds);
            }

            // Verify one player is marked as current player
            var currentPlayers = players.Where(p => p.GetProperty("isCurrentPlayer").GetBoolean()).ToList();
            Assert.Single(currentPlayers);
        }

        [Fact]
        public async Task NewGame_WithMissingGameId_ShouldReturnBadRequest()
        {
            // Arrange
            var requestBody = new
            {
                // gameId is missing
                gameType = "Regular",
                playerIds = new List<string> { "Player1", "Player2", "Player3" }
            };

            var json = JsonSerializer.Serialize(requestBody);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            // Act
            var response = await _client.PostAsync("/api/game/new", content);

            // Assert
            Assert.Equal(System.Net.HttpStatusCode.BadRequest, response.StatusCode);
            
            var responseBody = await response.Content.ReadAsStringAsync();
            Assert.Contains("Missing required fields", responseBody);
        }

        [Fact]
        public async Task NewGame_WithMissingGameType_ShouldReturnBadRequest()
        {
            // Arrange
            var requestBody = new
            {
                gameId = "test-game-missing-type",
                // gameType is missing
                playerIds = new List<string> { "Player1", "Player2", "Player3" }
            };

            var json = JsonSerializer.Serialize(requestBody);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            // Act
            var response = await _client.PostAsync("/api/game/new", content);

            // Assert
            Assert.Equal(System.Net.HttpStatusCode.BadRequest, response.StatusCode);
            
            var responseBody = await response.Content.ReadAsStringAsync();
            Assert.Contains("Missing required fields", responseBody);
        }

        [Fact]
        public async Task NewGame_WithInvalidGameType_ShouldReturnBadRequest()
        {
            // Arrange
            var requestBody = new
            {
                gameId = "test-game-invalid-type",
                gameType = "InvalidGameType",
                playerIds = new List<string> { "Player1", "Player2", "Player3" }
            };

            var json = JsonSerializer.Serialize(requestBody);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            // Act
            var response = await _client.PostAsync("/api/game/new", content);

            // Assert
            Assert.Equal(System.Net.HttpStatusCode.BadRequest, response.StatusCode);
            
            var responseBody = await response.Content.ReadAsStringAsync();
            Assert.Contains("Invalid game type", responseBody);
        }

        [Fact]
        public async Task NewGame_WithTooFewPlayers_ShouldReturnServerError()
        {
            // Arrange - Only 1 player, should need at least 2
            var gameId = "test-game-too-few-players";
            var requestBody = new
            {
                gameId = gameId,
                gameType = "Regular",
                playerIds = new List<string> { "LonelyPlayer" }
            };

            var json = JsonSerializer.Serialize(requestBody);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            // Act
            var response = await _client.PostAsync("/api/game/new", content);

            // Assert
            Assert.Equal(System.Net.HttpStatusCode.InternalServerError, response.StatusCode);
            
            var responseBody = await response.Content.ReadAsStringAsync();
            Assert.Contains("Error creating new game", responseBody);
        }

        [Fact]
        public async Task NewGame_WithTooManyPlayers_ShouldReturnServerError()
        {
            // Arrange - 8 players, should be too many for regular Catan
            var gameId = "test-game-too-many-players";
            var requestBody = new
            {
                gameId = gameId,
                gameType = "Regular",
                playerIds = new List<string> { "P1", "P2", "P3", "P4", "P5", "P6", "P7", "P8" }
            };

            var json = JsonSerializer.Serialize(requestBody);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            // Act
            var response = await _client.PostAsync("/api/game/new", content);

            // Assert
            Assert.Equal(System.Net.HttpStatusCode.InternalServerError, response.StatusCode);
            
            var responseBody = await response.Content.ReadAsStringAsync();
            Assert.Contains("Error creating new game", responseBody);
        }

        [Fact]
        public async Task NewGame_WithEmptyPlayerList_ShouldReturnServerError()
        {
            // Arrange
            var gameId = "test-game-no-players";
            var requestBody = new
            {
                gameId = gameId,
                gameType = "Regular",
                playerIds = new List<string>() // Empty list
            };

            var json = JsonSerializer.Serialize(requestBody);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            // Act
            var response = await _client.PostAsync("/api/game/new", content);

            // Assert
            Assert.Equal(System.Net.HttpStatusCode.InternalServerError, response.StatusCode);
        }

        [Fact]
        public async Task NewGame_WithDuplicateGameId_ShouldCreateSecondGameSuccessfully()
        {
            // Arrange - Create first game
            var gameId = "duplicate-test-game";
            var firstRequestBody = new
            {
                gameId = gameId,
                gameType = "Regular",
                playerIds = new List<string> { "Alice", "Bob", "Charlie" }
            };

            var firstJson = JsonSerializer.Serialize(firstRequestBody);
            var firstContent = new StringContent(firstJson, Encoding.UTF8, "application/json");

            // Act - Create first game
            var firstResponse = await _client.PostAsync("/api/game/new", firstContent);
            Assert.True(firstResponse.IsSuccessStatusCode);

            // Arrange - Create second game with same ID but different players
            var secondRequestBody = new
            {
                gameId = gameId,
                gameType = "Regular",
                playerIds = new List<string> { "Dave", "Eve", "Frank" }
            };

            var secondJson = JsonSerializer.Serialize(secondRequestBody);
            var secondContent = new StringContent(secondJson, Encoding.UTF8, "application/json");

            // Act - Create second game (should overwrite first)
            var secondResponse = await _client.PostAsync("/api/game/new", secondContent);

            // Assert
            Assert.True(secondResponse.IsSuccessStatusCode);

            // Verify the game now has the second set of players
            var playersResponse = await _client.GetAsync($"/api/players/{gameId}");
            Assert.True(playersResponse.IsSuccessStatusCode);

            var playersBody = await playersResponse.Content.ReadAsStringAsync();
            var playersResult = JsonSerializer.Deserialize<JsonElement>(playersBody);

            var players = playersResult.GetProperty("players").EnumerateArray().ToList();
            var returnedPlayerIds = players.Select(p => p.GetProperty("id").GetString()).ToList();

            // Should have the second set of players, not the first
            Assert.Contains("Dave", returnedPlayerIds);
            Assert.Contains("Eve", returnedPlayerIds);
            Assert.Contains("Frank", returnedPlayerIds);
            Assert.DoesNotContain("Alice", returnedPlayerIds);
            Assert.DoesNotContain("Bob", returnedPlayerIds);
            Assert.DoesNotContain("Charlie", returnedPlayerIds);
        }

        [Fact]
        public async Task GetPlayers_ForNonExistentGame_ShouldReturnNotFound()
        {
            // Arrange
            var nonExistentGameId = "game-that-does-not-exist";

            // Act
            var response = await _client.GetAsync($"/api/players/{nonExistentGameId}");

            // Assert
            Assert.Equal(System.Net.HttpStatusCode.NotFound, response.StatusCode);
            
            var responseBody = await response.Content.ReadAsStringAsync();
            Assert.Contains($"Game {nonExistentGameId} not found", responseBody);
        }

        [Fact]
        public async Task GetGameState_ForNonExistentGame_ShouldReturnNotFound()
        {
            // Arrange
            var nonExistentGameId = "game-state-does-not-exist";

            // Act
            var response = await _client.GetAsync($"/api/gamestate/{nonExistentGameId}");

            // Assert
            Assert.Equal(System.Net.HttpStatusCode.NotFound, response.StatusCode);
            
            var responseBody = await response.Content.ReadAsStringAsync();
            Assert.Contains($"Game {nonExistentGameId} not found", responseBody);
        }

        [Fact]
        public async Task NewGame_ResponseFormat_ShouldMatchExpectedSchema()
        {
            // Arrange
            var gameId = "schema-test-game";
            var requestBody = new
            {
                gameId = gameId,
                gameType = "Regular",
                playerIds = new List<string> { "Player1", "Player2", "Player3" }
            };

            var json = JsonSerializer.Serialize(requestBody);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            // Act
            var response = await _client.PostAsync("/api/game/new", content);

            // Assert
            Assert.True(response.IsSuccessStatusCode);
            
            var responseBody = await response.Content.ReadAsStringAsync();
            var result = JsonSerializer.Deserialize<JsonElement>(responseBody);

            // Verify response schema
            Assert.True(result.TryGetProperty("success", out var success));
            Assert.True(success.GetBoolean());

            Assert.True(result.TryGetProperty("gameStateVersion", out var version));
            Assert.True(version.GetInt32() > 0);

            Assert.True(result.TryGetProperty("message", out var message));
            Assert.False(string.IsNullOrEmpty(message.GetString()));
        }

        [Fact]
        public async Task GameStateResponse_ShouldContainRequiredFields()
        {
            // Arrange
            var gameId = "state-fields-test";
            var playerIds = new List<string> { "StatePlayer1", "StatePlayer2", "StatePlayer3" };
            
            var requestBody = new
            {
                gameId = gameId,
                gameType = "Regular",
                playerIds = playerIds
            };

            var json = JsonSerializer.Serialize(requestBody);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            // Create the game first
            var createResponse = await _client.PostAsync("/api/game/new", content);
            Assert.True(createResponse.IsSuccessStatusCode);

            // Act
            var response = await _client.GetAsync($"/api/gamestate/{gameId}");

            // Assert
            Assert.True(response.IsSuccessStatusCode);
            
            var responseBody = await response.Content.ReadAsStringAsync();
            var gameState = JsonSerializer.Deserialize<JsonElement>(responseBody);

            // Verify required fields in game state response
            Assert.True(gameState.TryGetProperty("gameId", out var returnedGameId));
            Assert.Equal(gameId, returnedGameId.GetString());

            Assert.True(gameState.TryGetProperty("currentPlayerId", out var currentPlayerId));
            Assert.Contains(currentPlayerId.GetString(), playerIds);

            Assert.True(gameState.TryGetProperty("gameState", out var gameStateValue));
            Assert.False(string.IsNullOrEmpty(gameStateValue.GetString()));

            Assert.True(gameState.TryGetProperty("actionFlags", out var actionFlags));
            Assert.True(actionFlags.TryGetProperty("nextEnabled", out _));
            Assert.True(actionFlags.TryGetProperty("undoEnabled", out _));
            Assert.True(actionFlags.TryGetProperty("rollsEnabled", out _));

            Assert.True(gameState.TryGetProperty("availableEntitlements", out var entitlements));
            Assert.True(entitlements.GetArrayLength() >= 0);

            Assert.True(gameState.TryGetProperty("version", out var version));
            Assert.True(version.GetInt32() > 0);

            Assert.True(gameState.TryGetProperty("timestamp", out var timestamp));
            Assert.False(string.IsNullOrEmpty(timestamp.GetString()));
        }
    }
}