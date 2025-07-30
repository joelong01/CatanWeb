using Xunit;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.Configuration;
using System.Text.Json;
using System.Text;
using Catan3.Shared.Models;
using Tests.GameService.SignalR;

namespace Tests.GameService.SignalR
{
    /// <summary>
    /// Comprehensive tests for game creation, loading, and discovery via SignalR and REST.
    /// Tests the hybrid architecture where game management uses REST and gameplay uses SignalR.
    /// 
    /// These tests verify:
    /// 1. Game creation via REST API (returning gameId)
    /// 2. Game discovery via REST API (/api/companion/games)
    /// 3. SignalR connection to created games
    /// 4. Game state retrieval and synchronization
    /// 5. Game loading functionality
    /// 6. Error handling for invalid game operations
    /// 7. Multiple games management
    /// </summary>
    public class SignalRNewGameTests : IClassFixture<WebApplicationFactory<Program>>, IAsyncDisposable
    {
        private readonly WebApplicationFactory<Program> _factory;
        private readonly HttpClient _httpClient;
        private readonly List<HubConnection> _connections = new();

        public SignalRNewGameTests(WebApplicationFactory<Program> factory)
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
            _httpClient = _factory.CreateClient();
        }

        [Fact]
        public async Task NewGame_ValidRequest_ShouldCreateGameAndConnectViaSignalR()
        {
            // Arrange
            var gameType = "Regular";
            var playerIds = new List<string> { "Alice", "Bob", "Charlie" };

            var newGameRequestBody = new
            {
                gameType = gameType,
                playerIds = playerIds
            };

            var newGameJson = JsonSerializer.Serialize(newGameRequestBody);
            var newGameContent = new StringContent(newGameJson, Encoding.UTF8, "application/json");

            // Act - Create game via REST
            var createGameResponse = await _httpClient.PostAsync("/api/game/new", newGameContent);

            // Assert game creation
            Assert.True(createGameResponse.IsSuccessStatusCode, "Game creation should succeed");

            var createResponseBody = await createGameResponse.Content.ReadAsStringAsync();
            var createResult = JsonSerializer.Deserialize<JsonElement>(createResponseBody);
            
            Assert.True(createResult.TryGetProperty("success", out var success));
            Assert.True(success.GetBoolean());
            
            Assert.True(createResult.TryGetProperty("gameId", out var gameIdProp));
            var gameId = gameIdProp.GetString()!;
            Assert.False(string.IsNullOrEmpty(gameId));

            // Act - Connect via SignalR
            var connection = await SignalRTestHelper.CreateTestConnection(_factory, gameId, "Alice");
            _connections.Add(connection);

            GameModel? gameModel = null;
            var gameStateReceived = new TaskCompletionSource<bool>();

            connection.On<GameModel>("GameStateUpdated", model =>
            {
                gameModel = model;
                gameStateReceived.TrySetResult(true);
            });

            // Wait for game state
            await Task.Delay(1000);

            // Assert SignalR connection and game state
            Assert.Equal(HubConnectionState.Connected, connection.State);
            
            // Get game state via REST to verify
            var gameStateResponse = await _httpClient.GetAsync($"/api/gamestate/{gameId}");
            Assert.True(gameStateResponse.IsSuccessStatusCode);
            
            var gameStateBody = await gameStateResponse.Content.ReadAsStringAsync();
            var gameState = JsonSerializer.Deserialize<JsonElement>(gameStateBody);
            
            Assert.True(gameState.TryGetProperty("gameId", out var returnedGameId));
            Assert.Equal(gameId, returnedGameId.GetString());
            
            Assert.True(gameState.TryGetProperty("players", out var playersProperty));
            var players = playersProperty.EnumerateArray().ToList();
            Assert.Equal(3, players.Count);
            
            // Verify player IDs are present
            var playerIdsFromState = players.Select(p => p.GetProperty("id").GetString()).ToList();
            foreach (var playerId in playerIds)
            {
                Assert.Contains(playerId, playerIdsFromState);
            }
        }

        [Fact]
        public async Task NewGame_MultipleGames_ShouldCreateUniqueGamesViaSignalR()
        {
            // Arrange
            var game1Request = new { gameType = "Regular", playerIds = new[] { "Alice", "Bob" } };
            var game2Request = new { gameType = "Regular", playerIds = new[] { "Charlie", "Dave" } };

            // Act - Create first game
            var game1Json = JsonSerializer.Serialize(game1Request);
            var game1Content = new StringContent(game1Json, Encoding.UTF8, "application/json");
            var game1Response = await _httpClient.PostAsync("/api/game/new", game1Content);

            Assert.True(game1Response.IsSuccessStatusCode);
            var game1Body = await game1Response.Content.ReadAsStringAsync();
            var game1Result = JsonSerializer.Deserialize<JsonElement>(game1Body);
            var game1Id = game1Result.GetProperty("gameId").GetString()!;

            // Act - Create second game
            var game2Json = JsonSerializer.Serialize(game2Request);
            var game2Content = new StringContent(game2Json, Encoding.UTF8, "application/json");
            var game2Response = await _httpClient.PostAsync("/api/game/new", game2Content);

            Assert.True(game2Response.IsSuccessStatusCode);
            var game2Body = await game2Response.Content.ReadAsStringAsync();
            var game2Result = JsonSerializer.Deserialize<JsonElement>(game2Body);
            var game2Id = game2Result.GetProperty("gameId").GetString()!;

            // Assert
            Assert.NotEqual(game1Id, game2Id);

            // Connect to both games via SignalR
            var connection1 = await SignalRTestHelper.CreateTestConnection(_factory, game1Id, "Alice");
            var connection2 = await SignalRTestHelper.CreateTestConnection(_factory, game2Id, "Charlie");
            _connections.AddRange(new[] { connection1, connection2 });

            Assert.Equal(HubConnectionState.Connected, connection1.State);
            Assert.Equal(HubConnectionState.Connected, connection2.State);

            // Verify each game has correct players
            var game1StateResponse = await _httpClient.GetAsync($"/api/gamestate/{game1Id}");
            var game1StateBody = await game1StateResponse.Content.ReadAsStringAsync();
            var game1State = JsonSerializer.Deserialize<JsonElement>(game1StateBody);
            var game1Players = game1State.GetProperty("players").EnumerateArray()
                .Select(p => p.GetProperty("id").GetString()).ToList();

            Assert.Contains("Alice", game1Players);
            Assert.Contains("Bob", game1Players);
            Assert.DoesNotContain("Charlie", game1Players);

            var game2StateResponse = await _httpClient.GetAsync($"/api/gamestate/{game2Id}");
            var game2StateBody = await game2StateResponse.Content.ReadAsStringAsync();
            var game2State = JsonSerializer.Deserialize<JsonElement>(game2Body);
            var game2Players = game2State.GetProperty("players").EnumerateArray()
                .Select(p => p.GetProperty("id").GetString()).ToList();

            Assert.Contains("Charlie", game2Players);
            Assert.Contains("Dave", game2Players);
            Assert.DoesNotContain("Alice", game2Players);
        }

        [Fact]
        public async Task GameDiscovery_ShouldListAvailableGames()
        {
            // Arrange - Create a few games
            var game1Request = new { gameType = "Regular", playerIds = new[] { "Player1", "Player2" } };
            var game2Request = new { gameType = "Regular", playerIds = new[] { "Player3", "Player4", "Player5" } };

            // Create games
            var game1Response = await _httpClient.PostAsync("/api/game/new", 
                new StringContent(JsonSerializer.Serialize(game1Request), Encoding.UTF8, "application/json"));
            var game2Response = await _httpClient.PostAsync("/api/game/new", 
                new StringContent(JsonSerializer.Serialize(game2Request), Encoding.UTF8, "application/json"));

            Assert.True(game1Response.IsSuccessStatusCode);
            Assert.True(game2Response.IsSuccessStatusCode);

            // Act - Discover games
            var discoveryResponse = await _httpClient.GetAsync("/api/companion/games");

            // Assert
            Assert.True(discoveryResponse.IsSuccessStatusCode);
            
            var discoveryBody = await discoveryResponse.Content.ReadAsStringAsync();
            var discoveryResult = JsonSerializer.Deserialize<JsonElement>(discoveryBody);
            
            Assert.True(discoveryResult.TryGetProperty("games", out var availableGames));
            var games = availableGames.EnumerateArray().ToList();
            
            Assert.True(games.Count >= 2, $"Should have at least 2 games, found {games.Count}");
            
            // Verify game properties
            foreach (var game in games)
            {
                Assert.True(game.TryGetProperty("gameId", out _));
                Assert.True(game.TryGetProperty("displayName", out _));
                Assert.True(game.TryGetProperty("gameState", out _));
                Assert.True(game.TryGetProperty("playerCount", out _));
                Assert.True(game.TryGetProperty("isActive", out _));
            }
        }

        [Fact]
        public async Task NewGame_InvalidGameType_ShouldReturnError()
        {
            // Arrange
            var invalidRequest = new { gameType = "InvalidType", playerIds = new[] { "Player1" } };

            // Act
            var response = await _httpClient.PostAsync("/api/game/new", 
                new StringContent(JsonSerializer.Serialize(invalidRequest), Encoding.UTF8, "application/json"));

            // Assert
            Assert.Equal(System.Net.HttpStatusCode.BadRequest, response.StatusCode);
            
            var responseBody = await response.Content.ReadAsStringAsync();
            Assert.Contains("Invalid game type", responseBody);
        }

        [Fact]
        public async Task NewGame_EmptyPlayerList_ShouldReturnError()
        {
            // Arrange
            var invalidRequest = new { gameType = "Regular", playerIds = new string[0] };

            // Act
            var response = await _httpClient.PostAsync("/api/game/new", 
                new StringContent(JsonSerializer.Serialize(invalidRequest), Encoding.UTF8, "application/json"));

            // Assert
            Assert.Equal(System.Net.HttpStatusCode.BadRequest, response.StatusCode);
            
            var responseBody = await response.Content.ReadAsStringAsync();
            Assert.Contains("At least one valid player is required", responseBody);
        }

        [Fact]
        public async Task NewGame_TooManyPlayers_ShouldReturnError()
        {
            // Arrange - Create request with too many players
            var tooManyPlayers = Enumerable.Range(1, 10).Select(i => $"Player{i}").ToArray();
            var invalidRequest = new { gameType = "Regular", playerIds = tooManyPlayers };

            // Act
            var response = await _httpClient.PostAsync("/api/game/new", 
                new StringContent(JsonSerializer.Serialize(invalidRequest), Encoding.UTF8, "application/json"));

            // Assert
            Assert.Equal(System.Net.HttpStatusCode.InternalServerError, response.StatusCode);
            
            var responseBody = await response.Content.ReadAsStringAsync();
            Assert.Contains("Error creating new game", responseBody);
        }

        [Fact]
        public async Task GameState_NonExistentGame_ShouldReturnNotFound()
        {
            // Arrange
            var nonExistentGameId = "non-existent-game-123";

            // Act
            var response = await _httpClient.GetAsync($"/api/gamestate/{nonExistentGameId}");

            // Assert
            Assert.Equal(System.Net.HttpStatusCode.NotFound, response.StatusCode);
            
            var responseBody = await response.Content.ReadAsStringAsync();
            Assert.Contains($"Game {nonExistentGameId} not found", responseBody);
        }

        [Fact]
        public async Task SignalRConnection_NonExistentGame_ShouldHandleGracefully()
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

            // Act - Try to join non-existent game
            await connection.InvokeAsync("JoinGame", "non-existent-game", "TestPlayer");

            // Wait for potential error
            await Task.Delay(2000);

            // Try to execute action on non-existent game
            var message = new DoAction(GameAction.Shuffle);
            await connection.InvokeAsync("ExecuteDoAction", "non-existent-game", "TestPlayer", message);

            // Assert
            var errorResult = await errorReceived.Task.WaitAsync(TimeSpan.FromSeconds(10));
            Assert.True(errorResult, "Should receive error for non-existent game");
            Assert.NotNull(errorMessage);
        }

        [Fact]
        public async Task NewGame_AndSignalRGameplay_ShouldWorkEndToEnd()
        {
            // Complete end-to-end test: Create game -> Connect -> Play -> Verify
            
            // Arrange & Act - Create game
            var gameRequest = new { gameType = "Regular", playerIds = new[] { "Alice", "Bob", "Charlie" } };
            var gameResponse = await _httpClient.PostAsync("/api/game/new", 
                new StringContent(JsonSerializer.Serialize(gameRequest), Encoding.UTF8, "application/json"));

            Assert.True(gameResponse.IsSuccessStatusCode);
            var gameBody = await gameResponse.Content.ReadAsStringAsync();
            var gameResult = JsonSerializer.Deserialize<JsonElement>(gameBody);
            var gameId = gameResult.GetProperty("gameId").GetString()!;

            // Connect multiple clients
            var connection1 = await SignalRTestHelper.CreateTestConnection(_factory, gameId, "Alice");
            var connection2 = await SignalRTestHelper.CreateTestConnection(_factory, gameId, "Bob");
            _connections.AddRange(new[] { connection1, connection2 });

            var receivedUpdates = new List<string>();
            var updateLock = new object();

            connection1.On<GameModel>("GameStateUpdated", gameModel =>
            {
                lock (updateLock)
                {
                    receivedUpdates.Add($"Alice: {gameModel.GameState}");
                }
            });

            connection2.On<GameModel>("GameStateUpdated", gameModel =>
            {
                lock (updateLock)
                {
                    receivedUpdates.Add($"Bob: {gameModel.GameState}");
                }
            });

            // Execute some gameplay actions
            await SignalRTestHelper.ExecuteDoActionViaSignalR(connection1, gameId, "Alice", GameAction.Shuffle);
            await Task.Delay(1000);

            await SignalRTestHelper.ExecuteDoActionViaSignalR(connection1, gameId, "Alice", GameAction.Next);
            await Task.Delay(1000);

            // Assert
            Assert.True(receivedUpdates.Count >= 2, $"Should receive multiple updates, got {receivedUpdates.Count}");
            Assert.True(receivedUpdates.Any(u => u.Contains("Alice")), "Alice should receive updates");
            Assert.True(receivedUpdates.Any(u => u.Contains("Bob")), "Bob should receive updates");
        }

        [Fact]
        public async Task GameCreation_ResponseFormat_ShouldMatchExpectedSchema()
        {
            // Arrange
            var request = new { gameType = "Regular", playerIds = new[] { "TestPlayer1", "TestPlayer2" } };

            // Act
            var response = await _httpClient.PostAsync("/api/game/new", 
                new StringContent(JsonSerializer.Serialize(request), Encoding.UTF8, "application/json"));

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

            Assert.True(result.TryGetProperty("gameId", out var gameId));
            Assert.False(string.IsNullOrEmpty(gameId.GetString()));
        }

        public async ValueTask DisposeAsync()
        {
            foreach (var connection in _connections)
            {
                await SignalRTestHelper.DisposeConnection(connection);
            }
            _connections.Clear();
            _httpClient.Dispose();
        }
    }
}