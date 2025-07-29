using Xunit;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using System.Text;
using System.Text.Json;
using Catan3.Shared.Models;
using PlayerInfo = Catan3.Shared.Models.PlayerInfo;

namespace Tests.GameService
{
    /// <summary>
    /// Tests for the new game creation API using the proper shared models
    /// This should be the foundation for all game creation in tests
    /// </summary>
    public class NewGameTests : IClassFixture<WebApplicationFactory<Program>>
    {
        private readonly WebApplicationFactory<Program> _factory;
        private readonly HttpClient _client;

        public NewGameTests(WebApplicationFactory<Program> factory)
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
        public async Task CreateNewGame_WithPlayerIds_ShouldSucceed()
        {
            // Arrange
            var request = new NewGameRequest
            {
                GameType = GameType.Regular,
                PlayerIds = new List<string> { "Alice-001", "Bob-002", "Charlie-003" }
            };

            // Act
            var gameId = await CreateNewGameUsingSharedModel(request);

            // Assert
            Assert.NotNull(gameId);
            Assert.False(string.IsNullOrEmpty(gameId), "Server should return a valid gameId");

            // Verify game state
            var gameState = await GetGameState(gameId);
            Assert.Equal("PickingBoard", gameState.GetProperty("gameState").GetString());
            Assert.Equal("Regular", gameState.GetProperty("gameType").GetString());
            
            var players = gameState.GetProperty("players").EnumerateArray().ToList();
            Assert.Equal(3, players.Count);
            
            var playerIds = players.Select(p => p.GetProperty("id").GetString()).ToList();
            Assert.Contains("Alice-001", playerIds);
            Assert.Contains("Bob-002", playerIds);
            Assert.Contains("Charlie-003", playerIds);
        }

        [Fact]
        public async Task CreateNewGame_WithPlayerObjects_ShouldSucceed()
        {
            // Arrange - Using the Desktop app pattern with player objects
            var request = new NewGameRequest
            {
                GameType = GameType.Expansion,
                Players = new List<global::Catan3.Shared.Models.PlayerInfo>
                {
                    new global::Catan3.Shared.Models.PlayerInfo { Id = "Joe-001", Name = "Joe" },
                    new global::Catan3.Shared.Models.PlayerInfo { Id = "Dodgy-001", Name = "Dodgy" },
                    new global::Catan3.Shared.Models.PlayerInfo { Id = "Doug-001", Name = "Doug" },
                    new global::Catan3.Shared.Models.PlayerInfo { Id = "Ryan-001", Name = "Ryan" }
                }
            };

            // Act
            var gameId = await CreateNewGameUsingSharedModel(request);

            // Assert
            Assert.NotNull(gameId);
            Assert.False(string.IsNullOrEmpty(gameId), "Server should return a valid gameId");

            // Verify game state
            var gameState = await GetGameState(gameId);
            Assert.Equal("PickingBoard", gameState.GetProperty("gameState").GetString());
            Assert.Equal("Expansion", gameState.GetProperty("gameType").GetString());
            
            var players = gameState.GetProperty("players").EnumerateArray().ToList();
            Assert.Equal(4, players.Count);
            
            var playerIds = players.Select(p => p.GetProperty("id").GetString()).ToList();
            Assert.Contains("Joe-001", playerIds);
            Assert.Contains("Dodgy-001", playerIds);
            Assert.Contains("Doug-001", playerIds);
            Assert.Contains("Ryan-001", playerIds);
        }

        [Fact]
        public async Task CreateNewGame_WithEmptyPlayerList_ShouldReturnBadRequest()
        {
            // Arrange
            var request = new NewGameRequest
            {
                GameType = GameType.Regular,
                PlayerIds = new List<string>() // Empty player list
            };

            // Act & Assert
            var exception = await Assert.ThrowsAsync<InvalidOperationException>(
                () => CreateNewGameUsingSharedModel(request));
            
            Assert.Contains("At least one valid player is required", exception.Message);
        }

        [Fact]
        public async Task CreateNewGame_WithTooManyPlayers_ShouldReturnServerError()
        {
            // Arrange - 8 players should be too many for regular Catan
            var request = new NewGameRequest
            {
                GameType = GameType.Regular,
                PlayerIds = new List<string> { "P1", "P2", "P3", "P4", "P5", "P6", "P7", "P8" }
            };

            // Act & Assert
            var exception = await Assert.ThrowsAsync<InvalidOperationException>(
                () => CreateNewGameUsingSharedModel(request));
            
            Assert.Contains("Error creating new game", exception.Message);
        }

        [Fact]
        public void GetPlayerNames_ShouldExtractNamesFromIds()
        {
            // Arrange
            var request = new NewGameRequest
            {
                GameId = "test-game-name-extraction",
                GameType = GameType.Regular,
                PlayerIds = new List<string> { "Alice-001", "Bob-002", "Charlie-003" }
            };

            // Act
            var playerNames = request.GetPlayerNames();

            // Assert
            Assert.Equal(3, playerNames.Count);
            Assert.Contains("Alice", playerNames);
            Assert.Contains("Bob", playerNames);
            Assert.Contains("Charlie", playerNames);
        }

        [Fact]
        public void GetPlayerNames_WithPlayerObjects_ShouldReturnNames()
        {
            // Arrange
            var request = new NewGameRequest
            {
                GameId = "test-game-name-objects",
                GameType = GameType.Regular,
                Players = new List<global::Catan3.Shared.Models.PlayerInfo>
                {
                    new global::Catan3.Shared.Models.PlayerInfo { Id = "Joe-001", Name = "Joe" },
                    new global::Catan3.Shared.Models.PlayerInfo { Id = "Dodgy-001", Name = "Dodgy" },
                    new global::Catan3.Shared.Models.PlayerInfo { Id = "Doug-001", Name = "Doug" }
                }
            };

            // Act
            var playerNames = request.GetPlayerNames();

            // Assert
            Assert.Equal(3, playerNames.Count);
            Assert.Contains("Joe", playerNames);
            Assert.Contains("Dodgy", playerNames);
            Assert.Contains("Doug", playerNames);
        }

        /// <summary>
        /// Helper method for creating games using the shared NewGameRequest model
        /// This should be the standard way to create games in all tests
        /// Updated to handle server-generated GameIds
        /// </summary>
        public async Task<string> CreateNewGameUsingSharedModel(NewGameRequest request)
        {
            // Remove the GameId from the request since server now generates it
            object requestBody;
            
            if (request.Players != null && request.Players.Count > 0)
            {
                // Send player objects with id and name
                requestBody = new
                {
                    gameType = request.GameType.ToString(),
                    players = request.Players.Select(p => new { id = p.Id, name = p.Name }).ToList()
                };
            }
            else
            {
                // Send simple player IDs
                requestBody = new
                {
                    gameType = request.GameType.ToString(),
                    playerIds = request.PlayerIds ?? new List<string>()
                };
            }
            
            var json = JsonSerializer.Serialize(requestBody, new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            });
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            var response = await _client.PostAsync("/api/game/new", content);
            
            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync();
                throw new InvalidOperationException($"Game creation failed: {response.StatusCode} - {errorContent}");
            }

            var responseBody = await response.Content.ReadAsStringAsync();
            var result = JsonSerializer.Deserialize<JsonElement>(responseBody);
            
            if (!result.GetProperty("success").GetBoolean())
            {
                var message = result.TryGetProperty("message", out var msgElement) ? msgElement.GetString() : "Unknown error";
                throw new InvalidOperationException($"Game creation failed: {message}");
            }

            // Return the server-generated gameId from the response
            if (!result.TryGetProperty("gameId", out var gameIdElement) || string.IsNullOrEmpty(gameIdElement.GetString()))
            {
                throw new InvalidOperationException("Server did not return a gameId");
            }

            return gameIdElement.GetString()!;
        }

        /// <summary>
        /// Helper method for getting game state
        /// </summary>
        public async Task<JsonElement> GetGameState(string gameId)
        {
            var response = await _client.GetAsync($"/api/gamestate/{gameId}");
            
            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync();
                throw new InvalidOperationException($"Get game state failed: {response.StatusCode} - {errorContent}");
            }

            var responseBody = await response.Content.ReadAsStringAsync();
            return JsonSerializer.Deserialize<JsonElement>(responseBody);
        }

        /// <summary>
        /// Creates a standard test game with default players following Desktop app patterns
        /// This should be used as the starting point for most tests
        /// </summary>
        public async Task<string> CreateStandardTestGame(string? gameId = null, GameType gameType = GameType.Regular)
        {
            var request = new NewGameRequest
            {
                GameType = gameType,
                Players = new List<global::Catan3.Shared.Models.PlayerInfo>
                {
                    new global::Catan3.Shared.Models.PlayerInfo { Id = "Joe-001", Name = "Joe" },
                    new global::Catan3.Shared.Models.PlayerInfo { Id = "Dodgy-001", Name = "Dodgy" },
                    new global::Catan3.Shared.Models.PlayerInfo { Id = "Doug-001", Name = "Doug" }
                }
            };

            // Add fourth player for Expansion games
            if (gameType == GameType.Expansion)
            {
                request.Players.Add(new global::Catan3.Shared.Models.PlayerInfo { Id = "Ryan-001", Name = "Ryan" });
            }

            return await CreateNewGameUsingSharedModel(request);
        }

        /// <summary>
        /// Creates a test game with specific player names using Desktop app patterns
        /// </summary>
        public async Task<string> CreateTestGameWithPlayers(List<string> playerNames, string? gameId = null, GameType gameType = GameType.Regular)
        {
            var request = new NewGameRequest
            {
                GameType = gameType,
                Players = playerNames.Select((name, index) => new global::Catan3.Shared.Models.PlayerInfo { Id = $"{name}-{index + 1:D3}", Name = name }).ToList()
            };

            return await CreateNewGameUsingSharedModel(request);
        }
    }
}