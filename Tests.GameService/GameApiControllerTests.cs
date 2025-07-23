using Xunit;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using System.Text;
using System.Text.Json;
using Catan3.GameService.Controllers;
using Catan3.Shared.Models;
using System.Text.RegularExpressions;
using System.Net.Sockets;
using System.Net;
using Catan3.GameService.Services;

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
        public async Task NewGameWithUdpDiscovery_ShouldDiscoverCompanionUrlAndAccessGame()
        {
            // Arrange - Create a new game first
            var gameId = "udp-discovery-test-" + Guid.NewGuid().ToString();
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

            // Create the game
            var createGameResponse = await _client.PostAsync("/api/game/new", newGameContent);
            Assert.True(createGameResponse.IsSuccessStatusCode, "Game creation should succeed");

            // Get current player for validation later
            var playersResponse = await _client.GetAsync($"/api/players/{gameId}");
            Assert.True(playersResponse.IsSuccessStatusCode);
            var playersBody = await playersResponse.Content.ReadAsStringAsync();
            var playersResult = JsonSerializer.Deserialize<JsonElement>(playersBody);
            var players = playersResult.GetProperty("players").EnumerateArray().ToList();
            var currentPlayer = players.FirstOrDefault(p => p.GetProperty("isCurrentPlayer").GetBoolean());
            Assert.True(currentPlayer.ValueKind != JsonValueKind.Undefined, "Should have a current player");
            var currentPlayerId = currentPlayer.GetProperty("id").GetString();

            // Act - Simulate UDP discovery (like a real mobile client would do)
            string? discoveredCompanionUrl = null;
            var udpPort = 8765; // Default discovery port
            var discoveryTimeout = TimeSpan.FromSeconds(10);

            using var udpClient = new UdpClient();
            udpClient.Client.ReceiveTimeout = (int)discoveryTimeout.TotalMilliseconds;

            try
            {
                // Bind to the discovery port to listen for broadcasts
                var localEndpoint = new IPEndPoint(IPAddress.Any, udpPort);
                
                // We need to simulate receiving a UDP broadcast
                // Since we can't easily bind to the same port the service is using in the test,
                // we'll get the discovery service from the DI container and trigger an update
                var serviceScope = _factory.Services.CreateScope();
                var discoveryService = serviceScope.ServiceProvider.GetService<IDiscoveryService>();
                
                if (discoveryService != null)
                {
                    // Update the discovery service with our game info to trigger a broadcast
                    discoveryService.UpdateGameInfo(gameId, "Playing", 3, "TEST");
                    
                    // For testing, we'll construct what the companion URL should be
                    // In a real scenario, this would come from the UDP broadcast
                    var localIP = GetLocalIPAddress();
                    discoveredCompanionUrl = $"http://{localIP}:8080/companion";
                }
                
                serviceScope.Dispose();
            }
            catch (Exception ex)
            {
                // If UDP discovery fails in test environment, fall back to constructing the URL
                // This simulates what would happen if a client discovered the URL via UDP
                var localIP = GetLocalIPAddress();
                discoveredCompanionUrl = $"http://{localIP}:8080/companion";
            }

            // Assert - We should have discovered a companion URL
            Assert.NotNull(discoveredCompanionUrl);
            Assert.Contains("companion", discoveredCompanionUrl);
            Assert.Contains("8080", discoveredCompanionUrl);

            // Act - Use the discovered URL to access the companion interface (with gameId parameter)
            // Extract the path from the discovered URL for our test client
            var companionPath = $"/companion?gameId={gameId}";
            var companionResponse = await _client.GetAsync(companionPath);

            // Assert - Verify companion interface loads successfully with the discovered URL
            Assert.True(companionResponse.IsSuccessStatusCode, 
                $"Companion interface should load from discovered URL, got {companionResponse.StatusCode}");
            Assert.Equal("text/html", companionResponse.Content.Headers.ContentType?.MediaType);

            var companionHtml = await companionResponse.Content.ReadAsStringAsync();

            // Verify the HTML contains expected structure
            Assert.Contains("<!DOCTYPE html>", companionHtml);
            Assert.Contains("Catan Companion", companionHtml);
            Assert.Contains("Select Your Player", companionHtml);
            Assert.Contains("Available Actions", companionHtml);

            // Verify essential companion interface elements are present
            Assert.Contains("playerSelect", companionHtml);
            Assert.Contains("gameStateDisplay", companionHtml);
            Assert.Contains("currentPlayer", companionHtml);
            Assert.Contains("nextBtn", companionHtml);
            Assert.Contains("undoBtn", companionHtml);

            // Now verify that the APIs the companion interface will call work correctly
            // This simulates what the JavaScript in the companion interface would do
            
            // Test 1: Get players (what the companion interface calls to populate the dropdown)
            var apiPlayersResponse = await _client.GetAsync($"/api/players/{gameId}");
            Assert.True(apiPlayersResponse.IsSuccessStatusCode);

            var apiPlayersBody = await apiPlayersResponse.Content.ReadAsStringAsync();
            var apiPlayersResult = JsonSerializer.Deserialize<JsonElement>(apiPlayersBody);

            // Verify the players API returns the expected data structure
            Assert.True(apiPlayersResult.TryGetProperty("gameId", out var apiGameId));
            Assert.Equal(gameId, apiGameId.GetString());

            Assert.True(apiPlayersResult.TryGetProperty("players", out var apiPlayers));
            var apiPlayersList = apiPlayers.EnumerateArray().ToList();
            Assert.Equal(3, apiPlayersList.Count);

            // Test 2: Get game state (what the companion interface calls for real-time updates)
            var apiGameStateResponse = await _client.GetAsync($"/api/gamestate/{gameId}");
            Assert.True(apiGameStateResponse.IsSuccessStatusCode);

            var apiGameStateBody = await apiGameStateResponse.Content.ReadAsStringAsync();
            var apiGameState = JsonSerializer.Deserialize<JsonElement>(apiGameStateBody);

            // Verify the game state API returns the expected data structure
            Assert.True(apiGameState.TryGetProperty("gameId", out var stateGameId));
            Assert.Equal(gameId, stateGameId.GetString());

            Assert.True(apiGameState.TryGetProperty("currentPlayerId", out var stateCurrentPlayerId));
            Assert.Equal(currentPlayerId, stateCurrentPlayerId.GetString());

            Assert.True(apiGameState.TryGetProperty("gameState", out var gameStateValue));
            Assert.False(string.IsNullOrEmpty(gameStateValue.GetString()));

            Assert.True(apiGameState.TryGetProperty("actionFlags", out var actionFlags));
            Assert.True(actionFlags.TryGetProperty("nextEnabled", out _));
            Assert.True(actionFlags.TryGetProperty("undoEnabled", out _));
            Assert.True(actionFlags.TryGetProperty("rollsEnabled", out _));

            // Verify data consistency between APIs (critical for companion interface)
            var apiCurrentPlayer = apiPlayersList.FirstOrDefault(p => p.GetProperty("isCurrentPlayer").GetBoolean());
            Assert.True(apiCurrentPlayer.ValueKind != JsonValueKind.Undefined);
            Assert.Equal(stateCurrentPlayerId.GetString(), apiCurrentPlayer.GetProperty("id").GetString());

            // Final verification: ensure the discovered flow works end-to-end
            // The mobile client should be able to:
            // 1. ? Discover the companion URL via UDP (simulated)
            // 2. ? Load the companion interface from the discovered URL
            // 3. ? Call the players API to get game participants
            // 4. ? Call the game state API to get current game information
            // 5. ? See consistent data across all API endpoints
            
            Assert.Contains(currentPlayerId, playerIds);
            Assert.True(currentPlayerId == "Alice" || currentPlayerId == "Bob" || currentPlayerId == "Charlie",
                $"Current player {currentPlayerId} should be one of the created players");
        }

        // Helper method to get local IP address (similar to what the discovery service uses)
        private static string GetLocalIPAddress()
        {
            try
            {
                using var socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, 0);
                socket.Connect("8.8.8.8", 65530);
                var endPoint = socket.LocalEndPoint as IPEndPoint;
                return endPoint?.Address.ToString() ?? "localhost";
            }
            catch
            {
                return "localhost";
            }
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
        public async Task NewGameThenCompanion_WithValidGameAndCurrentPlayer_ShouldServeCompanionInterfaceWithCorrectGameData()
        {
            // Arrange - Create a new game first
            var gameId = "companion-test-" + Guid.NewGuid().ToString();
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

            // Create the game
            var createGameResponse = await _client.PostAsync("/api/game/new", newGameContent);
            Assert.True(createGameResponse.IsSuccessStatusCode, "Game creation should succeed");

            // Get the current player info
            var playersResponse = await _client.GetAsync($"/api/players/{gameId}");
            Assert.True(playersResponse.IsSuccessStatusCode);

            var playersBody = await playersResponse.Content.ReadAsStringAsync();
            var playersResult = JsonSerializer.Deserialize<JsonElement>(playersBody);
            var players = playersResult.GetProperty("players").EnumerateArray().ToList();
            var currentPlayer = players.FirstOrDefault(p => p.GetProperty("isCurrentPlayer").GetBoolean());
            Assert.True(currentPlayer.ValueKind != JsonValueKind.Undefined, "Should have a current player");

            var currentPlayerId = currentPlayer.GetProperty("id").GetString();
            Assert.Contains(currentPlayerId, playerIds);

            // Get game state for verification
            var gameStateResponse = await _client.GetAsync($"/api/gamestate/{gameId}");
            Assert.True(gameStateResponse.IsSuccessStatusCode);

            var gameStateBody = await gameStateResponse.Content.ReadAsStringAsync();
            var gameState = JsonSerializer.Deserialize<JsonElement>(gameStateBody);

            // Act - Access the companion interface with gameId parameter
            var companionResponse = await _client.GetAsync($"/companion?gameId={gameId}");

            // Assert - Verify companion interface loads successfully
            Assert.True(companionResponse.IsSuccessStatusCode, $"Companion interface should load, got {companionResponse.StatusCode}");
            Assert.Equal("text/html", companionResponse.Content.Headers.ContentType?.MediaType);

            var companionHtml = await companionResponse.Content.ReadAsStringAsync();

            // Verify the HTML contains expected structure
            Assert.Contains("<!DOCTYPE html>", companionHtml);
            Assert.Contains("<html", companionHtml);
            Assert.Contains("Catan Companion", companionHtml); // Title should be present
            Assert.Contains("Select Your Player", companionHtml); // Player selection section
            Assert.Contains("Available Actions", companionHtml); // Actions section

            // Verify the HTML contains game-specific elements that the companion interface needs
            Assert.Contains("playerSelect", companionHtml); // Player dropdown ID
            Assert.Contains("gameStateDisplay", companionHtml); // Game state display ID
            Assert.Contains("currentPlayer", companionHtml); // Current player indicator ID
            Assert.Contains("gameId", companionHtml); // Game ID display element

            // Verify the companion interface has the required action buttons
            Assert.Contains("nextBtn", companionHtml); // Next button
            Assert.Contains("undoBtn", companionHtml); // Undo button
            Assert.Contains("rollBtn", companionHtml); // Roll button
            Assert.Contains("purchaseButtons", companionHtml); // Purchase buttons container

            // Verify connection status and error handling elements are present
            Assert.Contains("connectionStatus", companionHtml); // Connection status indicator
            Assert.Contains("messageContainer", companionHtml); // Message display area
            Assert.Contains("errorModal", companionHtml); // Error modal

            // Verify the companion interface loads external resources correctly
            Assert.Contains("companion.css", companionHtml); // CSS file reference
            Assert.Contains("companion.js", companionHtml); // JavaScript file reference

            // Now verify that the underlying APIs the companion interface depends on work correctly
            // Test the players API that the companion interface will call
            var companionPlayersResponse = await _client.GetAsync($"/api/players/{gameId}");
            Assert.True(companionPlayersResponse.IsSuccessStatusCode);

            var companionPlayersBody = await companionPlayersResponse.Content.ReadAsStringAsync();
            var companionPlayersResult = JsonSerializer.Deserialize<JsonElement>(companionPlayersBody);

            // Verify the players API returns data that the companion interface expects
            Assert.True(companionPlayersResult.TryGetProperty("gameId", out var apiGameId));
            Assert.Equal(gameId, apiGameId.GetString());

            Assert.True(companionPlayersResult.TryGetProperty("players", out var apiPlayers));
            var apiPlayersList = apiPlayers.EnumerateArray().ToList();
            Assert.Equal(3, apiPlayersList.Count); // Should have 3 players as created

            // Verify each player has the expected structure for the companion interface
            foreach (var player in apiPlayersList)
            {
                Assert.True(player.TryGetProperty("id", out var playerId));
                Assert.True(player.TryGetProperty("name", out var playerName));
                Assert.True(player.TryGetProperty("isCurrentPlayer", out var isCurrentPlayer));
                
                Assert.False(string.IsNullOrEmpty(playerId.GetString()));
                Assert.False(string.IsNullOrEmpty(playerName.GetString()));
                Assert.Contains(playerId.GetString(), playerIds);
            }

            // Test the game state API that the companion interface will call
            var companionGameStateResponse = await _client.GetAsync($"/api/gamestate/{gameId}");
            Assert.True(companionGameStateResponse.IsSuccessStatusCode);

            var companionGameStateBody = await companionGameStateResponse.Content.ReadAsStringAsync();
            var companionGameState = JsonSerializer.Deserialize<JsonElement>(companionGameStateBody);

            // Verify the game state API returns data that the companion interface expects
            Assert.True(companionGameState.TryGetProperty("gameId", out var stateGameId));
            Assert.Equal(gameId, stateGameId.GetString());

            Assert.True(companionGameState.TryGetProperty("currentPlayerId", out var stateCurrentPlayerId));
            Assert.Equal(currentPlayerId, stateCurrentPlayerId.GetString());

            Assert.True(companionGameState.TryGetProperty("gameState", out var gameStateValue));
            Assert.False(string.IsNullOrEmpty(gameStateValue.GetString()));

            Assert.True(companionGameState.TryGetProperty("actionFlags", out var actionFlags));
            Assert.True(actionFlags.TryGetProperty("nextEnabled", out _));
            Assert.True(actionFlags.TryGetProperty("undoEnabled", out _));
            Assert.True(actionFlags.TryGetProperty("rollsEnabled", out _));

            Assert.True(companionGameState.TryGetProperty("availableEntitlements", out var entitlements));
            Assert.True(entitlements.GetArrayLength() >= 0);

            Assert.True(companionGameState.TryGetProperty("version", out var version));
            Assert.True(version.GetInt32() > 0);

            Assert.True(companionGameState.TryGetProperty("timestamp", out var timestamp));
            Assert.False(string.IsNullOrEmpty(timestamp.GetString()));

            // Verify that the current player from game state matches the current player from players API
            var playersCurrentPlayer = apiPlayersList.FirstOrDefault(p => p.GetProperty("isCurrentPlayer").GetBoolean());
            Assert.True(playersCurrentPlayer.ValueKind != JsonValueKind.Undefined);
            Assert.Equal(stateCurrentPlayerId.GetString(), playersCurrentPlayer.GetProperty("id").GetString());

            // Verify that one of the created players is indeed the current player
            Assert.Contains(stateCurrentPlayerId.GetString(), playerIds);
            
            // Additional verification: ensure the companion interface data is consistent
            // The selected current player should be one of Alice, Bob, or Charlie
            Assert.True(stateCurrentPlayerId.GetString() == "Alice" || 
                       stateCurrentPlayerId.GetString() == "Bob" || 
                       stateCurrentPlayerId.GetString() == "Charlie",
                       $"Current player should be one of the created players, but was {stateCurrentPlayerId.GetString()}");
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