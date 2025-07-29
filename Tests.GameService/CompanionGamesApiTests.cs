using Xunit;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using System.Text;
using System.Text.Json;
using Catan3.Shared.Models;
using System.Net.Http.Headers;
using System.Net.Mime;

namespace Tests.GameService
{
    /// <summary>
    /// Tests for the game discovery and companion API endpoints
    /// Verifies the new /api/companion/games endpoint functionality
    /// </summary>
    public class CompanionGamesApiTests : IClassFixture<WebApplicationFactory<Program>>
    {
        private readonly WebApplicationFactory<Program> _factory;
        private readonly HttpClient _client;

        public CompanionGamesApiTests(WebApplicationFactory<Program> factory)
        {
            // Configure the factory with test-specific settings
            _factory = factory.WithWebHostBuilder(builder =>
            {
                builder.ConfigureAppConfiguration((context, config) =>
                {
                    config.AddInMemoryCollection(new Dictionary<string, string?>
                    {
                        // Set short timeout for tests
                        ["GameApi:HangingGetTimeoutSeconds"] = "5"
                    });
                });
            });
            _client = _factory.CreateClient();
        }

        [Fact]
        public async Task GetAvailableGames_WithNoGames_ShouldReturnEmptyList()
        {
            // Act - Call the companion games API
            var response = await _client.GetAsync("/api/companion/games");
            
            // Assert - Should return success with empty list
            Assert.True(response.IsSuccessStatusCode, $"Expected success but got {response.StatusCode}");
            
            var responseBody = await response.Content.ReadAsStringAsync();
            var result = JsonSerializer.Deserialize<JsonElement>(responseBody);
            
            Assert.True(result.GetProperty("success").GetBoolean());
            Assert.Equal(0, result.GetProperty("count").GetInt32());
            Assert.Equal(JsonValueKind.Array, result.GetProperty("games").ValueKind);
            Assert.Equal(0, result.GetProperty("games").GetArrayLength());
        }

        [Fact]
        public async Task GetAvailableGames_WithOneGame_ShouldReturnGameInfo()
        {
            // Arrange - Create a test game with valid player count (3-4 players for Regular)
            var request = new NewGameRequest
            {
                GameType = GameType.Regular,
                PlayerIds = new List<string> { "Alice", "Bob", "Charlie" } // 3 players (valid)
            };

            var json = JsonSerializer.Serialize(request, new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            });
            var content = new StringContent(json, Encoding.UTF8, MediaTypeNames.Application.Json);

            var createGameResponse = await _client.PostAsync("/api/game/new", content);
            Assert.True(createGameResponse.IsSuccessStatusCode, "Game creation should succeed");

            // Get the gameId from the response
            var createResponseBody = await createGameResponse.Content.ReadAsStringAsync();
            var createResult = JsonSerializer.Deserialize<JsonElement>(createResponseBody);
            var gameId = createResult.GetProperty("gameId").GetString();
            Assert.False(string.IsNullOrEmpty(gameId), "Should have a valid gameId");

            // Act - Call the companion games API
            var response = await _client.GetAsync("/api/companion/games");
            
            // Assert - Should return success with the created game
            Assert.True(response.IsSuccessStatusCode, $"Expected success but got {response.StatusCode}");
            
            var responseBody = await response.Content.ReadAsStringAsync();
            var result = JsonSerializer.Deserialize<JsonElement>(responseBody);
            
            Assert.True(result.GetProperty("success").GetBoolean());
            Assert.True(result.GetProperty("count").GetInt32() >= 1); // At least 1 game
            
            var games = result.GetProperty("games").EnumerateArray().ToList();
            Assert.True(games.Count >= 1);
            
            // Find our created game
            var createdGame = games.FirstOrDefault(g => g.GetProperty("gameId").GetString() == gameId);
            Assert.NotEqual(default(JsonElement), createdGame);
            
            // Verify the game structure
            Assert.Equal("Regular", createdGame.GetProperty("gameType").GetString());
            Assert.Equal(3, createdGame.GetProperty("playerCount").GetInt32());
            Assert.Equal("Setting up board", createdGame.GetProperty("gameState").GetString());
        }

        [Fact]
        public async Task GetAvailableGames_WithOneGame_ShouldReturnGameInfoWithProperPlayerInformation()
        {
            // Arrange - Create a test game with valid player count (3-4 players for Regular)
            var request = new NewGameRequest
            {
                GameType = GameType.Regular,
                PlayerIds = new List<string> { "Alice-001", "Bob-002", "Charlie-003" } // Using Desktop app ID pattern
            };

            var json = JsonSerializer.Serialize(request, new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            });
            var content = new StringContent(json, Encoding.UTF8, MediaTypeNames.Application.Json);

            var createGameResponse = await _client.PostAsync("/api/game/new", content);
            Assert.True(createGameResponse.IsSuccessStatusCode, "Game creation should succeed");

            // Act - Call the companion games API
            var response = await _client.GetAsync("/api/companion/games");
            
            // Assert - Should return success with one game
            Assert.True(response.IsSuccessStatusCode, $"Expected success but got {response.StatusCode}");
            
            var responseBody = await response.Content.ReadAsStringAsync();
            var result = JsonSerializer.Deserialize<JsonElement>(responseBody);
            
            Assert.True(result.GetProperty("success").GetBoolean());
            Assert.True(result.GetProperty("count").GetInt32() >= 1); // At least one game
            
            var games = result.GetProperty("games").EnumerateArray().ToList();
            Assert.True(games.Count >= 1);
            
            // Find our specific game by matching playerIds since gameId is server-generated
            var game = games.FirstOrDefault(g => 
            {
                var playerIds = g.GetProperty("playerIds").EnumerateArray()
                    .Select(p => p.GetString())
                    .Where(id => !string.IsNullOrEmpty(id))
                    .Cast<string>()
                    .ToList();
                return playerIds.SequenceEqual(request.PlayerIds);
            });
            Assert.NotEqual(default(JsonElement), game);
            
            Assert.Equal("Regular", game.GetProperty("gameType").GetString());
            Assert.Equal(3, game.GetProperty("playerCount").GetInt32());
            Assert.True(game.GetProperty("isActive").GetBoolean());
            
            // Check player IDs (should be full IDs)
            var returnedPlayerIds = game.GetProperty("playerIds").EnumerateArray()
                .Select(p => p.GetString())
                .Where(id => !string.IsNullOrEmpty(id))
                .Cast<string>()
                .ToList();
            Assert.Equal(request.PlayerIds, returnedPlayerIds);
            
            // Check player names (should be display names extracted from IDs)
            var expectedPlayerNames = new List<string> { "Alice", "Bob", "Charlie" };
            var returnedPlayerNames = game.GetProperty("playerNames").EnumerateArray()
                .Select(p => p.GetString())
                .Where(name => !string.IsNullOrEmpty(name))
                .Cast<string>()
                .ToList();
            Assert.Equal(expectedPlayerNames, returnedPlayerNames);
            
            // Check displayName uses player names, not IDs
            var displayName = game.GetProperty("displayName").GetString();
            Assert.Contains("Alice", displayName); // Should contain the display name, not the ID
            Assert.DoesNotContain("Alice-001", displayName); // Should not contain the full ID
            
            // Check required fields exist
            Assert.True(game.TryGetProperty("displayName", out _));
            Assert.True(game.TryGetProperty("gameState", out _));
            Assert.True(game.TryGetProperty("currentPlayer", out _));
            Assert.True(game.TryGetProperty("createdTime", out _));
            Assert.True(game.TryGetProperty("createdTimeDisplay", out _));
            Assert.True(game.TryGetProperty("summary", out _));
        }

        [Fact]
        public async Task GetAvailableGames_WithMultipleGames_ShouldReturnAllGames()
        {
            // Arrange - Create multiple test games with valid player counts
            var games = new List<NewGameRequest>
            {
                new() { GameType = GameType.Regular, PlayerIds = new List<string> { "Alice-001", "Bob-002", "Charlie-003" } }, // 3 players
                new() { GameType = GameType.Regular, PlayerIds = new List<string> { "Dave-004", "Eve-005", "Frank-006", "Grace-007" } }, // 4 players  
                new() { GameType = GameType.Expansion, PlayerIds = new List<string> { "Grace-007", "Henry-008", "Ivy-009", "Jack-010" } } // 4 players
            };

            var gameIds = new List<string>();
            foreach (var request in games)
            {
                var json = JsonSerializer.Serialize(request, new JsonSerializerOptions
                {
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase
                });
                var content = new StringContent(json, Encoding.UTF8, MediaTypeNames.Application.Json);

                var createGameResponse = await _client.PostAsync("/api/game/new", content);
                Assert.True(createGameResponse.IsSuccessStatusCode, $"Game creation should succeed");
                
                // Get the gameId from the response
                var createResponseBody = await createGameResponse.Content.ReadAsStringAsync();
                var createResult = JsonSerializer.Deserialize<JsonElement>(createResponseBody);
                var gameId = createResult.GetProperty("gameId").GetString();
                Assert.False(string.IsNullOrEmpty(gameId), "Should have a valid gameId");
                gameIds.Add(gameId);
            }

            // Act - Call the companion games API
            var response = await _client.GetAsync("/api/companion/games");
            
            // Assert - Should return success with all games
            Assert.True(response.IsSuccessStatusCode, $"Expected success but got {response.StatusCode}");
            
            var responseBody = await response.Content.ReadAsStringAsync();
            var result = JsonSerializer.Deserialize<JsonElement>(responseBody);
            
            Assert.True(result.GetProperty("success").GetBoolean());
            Assert.True(result.GetProperty("count").GetInt32() >= 3); // At least the 3 we created
            
            var returnedGames = result.GetProperty("games").EnumerateArray().ToList();
            Assert.True(returnedGames.Count >= 3);
            
            // Verify our created games are in the response
            var returnedGameIds = returnedGames
                .Select(g => g.GetProperty("gameId").GetString())
                .Where(id => !string.IsNullOrEmpty(id))
                .Cast<string>()
                .ToList();
            
            foreach (var gameId in gameIds)
            {
                Assert.Contains(gameId, returnedGameIds);
            }
            
            // Verify GameType enum is properly handled - find an Expansion game
            var expansionGame = returnedGames.FirstOrDefault(g => 
                g.GetProperty("gameType").GetString() == "Expansion");
            Assert.NotEqual(default(JsonElement), expansionGame);
            Assert.Equal("Expansion", expansionGame.GetProperty("gameType").GetString());
        }

        [Fact]
        public async Task GetAvailableGames_ResponseFormat_ShouldMatchExpectedStructure()
        {
            // Arrange - Create a test game to ensure we have data with valid player count using Desktop pattern
            var request = new NewGameRequest
            {
                GameType = GameType.Regular,
                PlayerIds = new List<string> { "TestPlayer1-001", "TestPlayer2-002", "TestPlayer3-003" } // Desktop app pattern
            };

            var json = JsonSerializer.Serialize(request, new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            });
            var content = new StringContent(json, Encoding.UTF8, MediaTypeNames.Application.Json);
            var createResponse = await _client.PostAsync("/api/game/new", content);
            Assert.True(createResponse.IsSuccessStatusCode, "Game creation should succeed");

            // Act
            var response = await _client.GetAsync("/api/companion/games");
            
            // Assert response structure
            Assert.True(response.IsSuccessStatusCode);
            Assert.Equal("application/json", response.Content.Headers.ContentType?.MediaType);
            
            var responseBody = await response.Content.ReadAsStringAsync();
            var result = JsonSerializer.Deserialize<JsonElement>(responseBody);
            
            // Verify top-level structure
            Assert.True(result.TryGetProperty("success", out var success) && success.GetBoolean());
            Assert.True(result.TryGetProperty("games", out var gamesArray) && gamesArray.ValueKind == JsonValueKind.Array);
            Assert.True(result.TryGetProperty("count", out var count) && count.ValueKind == JsonValueKind.Number);
            Assert.True(result.TryGetProperty("timestamp", out var timestamp) && timestamp.ValueKind == JsonValueKind.String);
            
            // Verify timestamp format (ISO 8601)
            var timestampStr = timestamp.GetString();
            Assert.True(DateTime.TryParse(timestampStr, out _), "Timestamp should be valid DateTime format");
            
            // Verify game object structure includes both playerIds and playerNames
            if (gamesArray.GetArrayLength() > 0)
            {
                var firstGame = gamesArray[0];
                Assert.True(firstGame.TryGetProperty("playerIds", out var playerIdsProperty) && playerIdsProperty.ValueKind == JsonValueKind.Array);
                Assert.True(firstGame.TryGetProperty("playerNames", out var playerNamesProperty) && playerNamesProperty.ValueKind == JsonValueKind.Array);
                
                // Verify they have the same length but different content
                Assert.Equal(playerIdsProperty.GetArrayLength(), playerNamesProperty.GetArrayLength());
                
                if (playerIdsProperty.GetArrayLength() > 0)
                {
                    var playerId = playerIdsProperty[0].GetString();
                    var playerName = playerNamesProperty[0].GetString();
                    
                    // PlayerName should be derived from PlayerId (e.g., "TestPlayer1-001" -> "TestPlayer1")
                    Assert.NotEqual(playerId, playerName);
                    Assert.True(playerId?.Contains(playerName!) ?? false, "PlayerName should be extracted from PlayerId");
                }
            }
        }

        [Fact]
        public async Task GetAvailableGames_WithComplexPlayerObjects_ShouldHandleDesktopAppFormat()
        {
            // Arrange - Create a test game using the Desktop app format with player objects
            var request = new NewGameRequest
            {
                GameType = GameType.Regular,
                // Test complex player objects (as Desktop app would send)
                Players = new List<global::Catan3.Shared.Models.PlayerInfo>
                {
                    new global::Catan3.Shared.Models.PlayerInfo { Id = "Joe-001", Name = "Joe" },
                    new global::Catan3.Shared.Models.PlayerInfo { Id = "Dodgy-001", Name = "Dodgy" },
                    new global::Catan3.Shared.Models.PlayerInfo { Id = "Doug-001", Name = "Doug" }
                }
            };

            var json = JsonSerializer.Serialize(request, new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            });
            var content = new StringContent(json, Encoding.UTF8, MediaTypeNames.Application.Json);

            var createGameResponse = await _client.PostAsync("/api/game/new", content);
            Assert.True(createGameResponse.IsSuccessStatusCode, "Game creation should succeed with complex player objects");

            // Extract the server-generated gameId from the response
            var createResponseBody = await createGameResponse.Content.ReadAsStringAsync();
            var createResult = JsonSerializer.Deserialize<JsonElement>(createResponseBody);
            var gameId = createResult.GetProperty("gameId").GetString()!;

            // Act - Call the companion games API
            var response = await _client.GetAsync("/api/companion/games");
            
            // Assert - Should return success with properly formatted game info
            Assert.True(response.IsSuccessStatusCode, $"Expected success but got {response.StatusCode}");
            
            var responseBody = await response.Content.ReadAsStringAsync();
            var result = JsonSerializer.Deserialize<JsonElement>(responseBody);
            
            Assert.True(result.GetProperty("success").GetBoolean());
            
            var games = result.GetProperty("games").EnumerateArray().ToList();
            var game = games.FirstOrDefault(g => g.GetProperty("gameId").GetString() == gameId);
            Assert.NotEqual(default(JsonElement), game);
            
            // Verify player IDs are preserved
            var returnedPlayerIds = game.GetProperty("playerIds").EnumerateArray()
                .Select(p => p.GetString())
                .Where(id => !string.IsNullOrEmpty(id))
                .Cast<string>()
                .ToList();
            var expectedPlayerIds = new List<string> { "Joe-001", "Dodgy-001", "Doug-001" };
            Assert.Equal(expectedPlayerIds, returnedPlayerIds);
            
            // Verify display names are extracted properly
            var returnedPlayerNames = game.GetProperty("playerNames").EnumerateArray()
                .Select(p => p.GetString())
                .Where(name => !string.IsNullOrEmpty(name))
                .Cast<string>()
                .ToList();
            var expectedPlayerNames = new List<string> { "Joe", "Dodgy", "Doug" };
            Assert.Equal(expectedPlayerNames, returnedPlayerNames);
            
            // Verify display name uses friendly names
            var displayName = game.GetProperty("displayName").GetString();
            Assert.Contains("Joe", displayName);
            Assert.DoesNotContain("Joe-001", displayName);
        }
    }
}