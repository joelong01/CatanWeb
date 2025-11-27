using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using System.Text.Json;
using Xunit;
using Catan3.Shared.Models;
using Catan3.Shared.Services;
using Microsoft.AspNetCore.SignalR.Client;
using System.Text;
using Catan3.Shared.Utility;

namespace Tests.GameService.Companion
{
    /// <summary>
    /// Comprehensive test suite for the Companion web interface functionality.
    /// Tests the complete companion.js workflow including:
    /// - Connection to game service
    /// - Loading and displaying available games
    /// - Selecting and joining a game
    /// - Player selection
    /// - Real-time game state updates via SignalR
    /// - Game action execution via SignalR
    /// </summary>
    public class CompanionIntegrationTests : IClassFixture<WebApplicationFactory<Program>>
    {
        private readonly WebApplicationFactory<Program> _factory;

        public CompanionIntegrationTests(WebApplicationFactory<Program> factory)
        {
            _factory = factory.WithWebHostBuilder(builder =>
            {
                builder.ConfigureAppConfiguration((context, config) =>
                {
                    config.AddInMemoryCollection(new Dictionary<string, string?>
                    {
                        ["GameApi:HangingGetTimeoutSeconds"] = "5",
                        ["Logging:LogLevel:Default"] = "Error",
                        ["Logging:LogLevel:Microsoft"] = "Error",
                        ["Logging:LogLevel:Microsoft.AspNetCore"] = "Error"
                    });
                });
            });
        }

        [Fact]
        public async Task CompanionWorkflow_ShouldConnectLoadGamesAndJoin_Successfully()
        {
            // Enable timing for this test
            FunctionTimer.Enabled = true;

            using (new FunctionTimer("CompanionFullWorkflow", enableOverride: true, writeToConsole: true))
            {
                // STEP 1: Create a test game for the companion to discover
                var gameSession = await CreateTestGameSession();

                // STEP 2: Test companion game discovery
                await TestGameDiscovery(gameSession.GameId);

                // STEP 3: Test companion joining game
                await TestGameJoining(gameSession.GameId);

                // STEP 4: Test player selection
                await TestPlayerSelection(gameSession.GameId);

                // STEP 5: Test real-time game state updates
                await TestRealTimeUpdates(gameSession);

                // STEP 6: Test SignalR command execution
                await TestSignalRCommands(gameSession);

                // STEP 7: Cleanup
                await gameSession.DisposeAsync();
            }

            FunctionTimer.Enabled = false;
        }

        [Fact]
        public async Task CompanionGameDiscovery_ShouldLoadAvailableGames_Successfully()
        {
            using (new FunctionTimer("GameDiscovery", enableOverride: true))
            {
                // Create multiple test games
                var game1 = await CreateTestGameSession("game1");
                var game2 = await CreateTestGameSession("game2");

                // Test the companion games API
                var httpClient = _factory.CreateClient();
                var response = await httpClient.GetAsync("/api/companion/games");

                Assert.True(response.IsSuccessStatusCode,
                    $"Games API should succeed. Status: {response.StatusCode}");

                var content = await response.Content.ReadAsStringAsync();
                var gamesResponse = JsonHelper.Deserialize<CompanionGamesResponse>(content);

                Assert.NotNull(gamesResponse);
                Assert.NotNull(gamesResponse.Games);
                Assert.True(gamesResponse.Games.Count >= 2,
                    $"Should have at least 2 games, found {gamesResponse.Games.Count}");

                // Verify game data structure for companion
                var testGame = gamesResponse.Games.FirstOrDefault(g => g.GameId == game1.GameId);
                Assert.NotNull(testGame);
                Assert.Equal("Expansion", testGame.GameType);
                Assert.Equal(5, testGame.PlayerCount);
                Assert.NotEmpty(testGame.PlayerNames);
                Assert.NotEmpty(testGame.DisplayName);
                Assert.True(testGame.IsActive);

                await game1.DisposeAsync();
                await game2.DisposeAsync();
            }
        }

        [Fact]
        public async Task CompanionGameJoining_ShouldConnectToSpecificGame_Successfully()
        {
            using (new FunctionTimer("GameJoining", enableOverride: true))
            {
                var gameSession = await CreateTestGameSession();

                // Test the companion joining a specific game
                var httpClient = _factory.CreateClient();

                // Test getting specific game state
                var response = await httpClient.GetAsync($"/api/gamestate/{gameSession.GameId}");
                Assert.True(response.IsSuccessStatusCode,
                    $"Game state API should succeed. Status: {response.StatusCode}");

                var content = await response.Content.ReadAsStringAsync();
                var gameModel = JsonHelper.Deserialize<GameModel>(content);

                Assert.NotNull(gameModel);
                Assert.Equal(gameSession.GameId, gameModel.GameId);
                Assert.Equal(GameState.PickingBoard, gameModel.GameState);
                Assert.Equal(5, gameModel.Players.Count);

                // Verify companion can access player information
                Assert.All(gameModel.Players, player =>
                {
                    Assert.NotNull(player.Id);
                    Assert.NotNull(player.Name);
                });

                await gameSession.DisposeAsync();
            }
        }

        [Fact]
        public async Task CompanionSignalRConnection_ShouldConnectAndReceiveUpdates_Successfully()
        {
            using (new FunctionTimer("SignalRConnection", enableOverride: true))
            {
                var gameSession = await CreateTestGameSession();

                // Create a companion SignalR connection
                var uri = _factory.Server.BaseAddress ?? new Uri("http://localhost");
                var hubUrl = new Uri(uri, "/gameHub").ToString();
                var testHandler = _factory.Server.CreateHandler();

                var companionProxy = new GameServiceProxy(hubUrl, "http://localhost", testHandler, "Alice", gameSession.GameId);
                await companionProxy.ConnectAsync();

                // Verify connection and initial state
                Assert.NotNull(companionProxy.Connection);
                Assert.Equal(HubConnectionState.Connected, companionProxy.Connection.State);

                // Test that companion receives game state updates
                var gameStateUpdateReceived = new TaskCompletionSource<GameModel>();
                companionProxy.GameStateUpdated += (gameModel) =>
                {
                    gameStateUpdateReceived.TrySetResult(gameModel);
                };

                // Trigger a game state change via the main game session
                var result = await gameSession.GetProxy("Alice").ExecuteShuffleAsync();
                Assert.True(result.Success, $"Shuffle action should succeed: {result.Message}");

                // Verify companion received the update
                var updatedGameModel = await gameStateUpdateReceived.Task.WaitAsync(TimeSpan.FromSeconds(5));
                Assert.NotNull(updatedGameModel);
                Assert.Equal(gameSession.GameId, updatedGameModel.GameId);

                await companionProxy.DisposeAsync();
                await gameSession.DisposeAsync();
            }
        }

        [Fact]
        public async Task CompanionCommands_ShouldExecuteGameActions_Successfully()
        {
            using (new FunctionTimer("CompanionCommands", enableOverride: true))
            {
                var gameSession = await CreateTestGameSession();

                // Create companion connection
                var uri = _factory.Server.BaseAddress ?? new Uri("http://localhost");
                var hubUrl = new Uri(uri, "/gameHub").ToString();
                var testHandler = _factory.Server.CreateHandler();

                var companionProxy = new GameServiceProxy(hubUrl, "http://localhost", testHandler, "Alice", gameSession.GameId);
                await companionProxy.ConnectAsync();

                // Test companion executing commands
                var commandResults = new List<(string Command, bool Success)>();

                // Test Shuffle command
                var shuffleResult = await companionProxy.ExecuteShuffleAsync();
                commandResults.Add(("Shuffle", shuffleResult.Success));

                // Test Undo command
                var undoResult = await companionProxy.ExecuteUndoAsync();
                commandResults.Add(("Undo", undoResult.Success));

                // Test Next command to advance state
                var nextResult = await companionProxy.ExecuteNextAsync();
                commandResults.Add(("Next", nextResult.Success));

                // Verify all commands executed successfully
                Assert.All(commandResults, result =>
                {
                    Assert.True(result.Success, $"Command {result.Command} should succeed");
                });

                // Verify game state changed
                var finalGameModel = companionProxy.GameModel;
                Assert.NotNull(finalGameModel);
                Assert.Equal(GameState.WaitingForRollForOrder, finalGameModel.GameState);

                await companionProxy.DisposeAsync();
                await gameSession.DisposeAsync();
            }
        }

        [Fact]
        public async Task CompanionPlayerSelection_ShouldHandlePlayerSwitching_Successfully()
        {
            using (new FunctionTimer("PlayerSelection", enableOverride: true))
            {
                var gameSession = await CreateTestGameSession();

                // Create companion connections for different players
                var uri = _factory.Server.BaseAddress ?? new Uri("http://localhost");
                var hubUrl = new Uri(uri, "/gameHub").ToString();
                var testHandler = _factory.Server.CreateHandler();

                var aliceProxy = new GameServiceProxy(hubUrl, "http://localhost", testHandler, "Alice", gameSession.GameId);
                var bobProxy = new GameServiceProxy(hubUrl, "http://localhost", testHandler, "Bob", gameSession.GameId);

                await aliceProxy.ConnectAsync();
                await bobProxy.ConnectAsync();

                // Verify both companions are connected
                Assert.Equal(HubConnectionState.Connected, aliceProxy.Connection.State);
                Assert.Equal(HubConnectionState.Connected, bobProxy.Connection.State);

                // Wait for both proxies to receive initial game state
                await aliceProxy.WaitForGameStateAsync(GameState.PickingBoard, TimeSpan.FromSeconds(5));
                await bobProxy.WaitForGameStateAsync(GameState.PickingBoard, TimeSpan.FromSeconds(5));

                // Test that each companion can see the same game state
                Assert.NotNull(aliceProxy.GameModel);
                Assert.NotNull(bobProxy.GameModel);
                Assert.Equal(aliceProxy.GameModel.GameId, bobProxy.GameModel.GameId);
                Assert.Equal(aliceProxy.GameModel.GameState, bobProxy.GameModel.GameState);

                // Test player-specific actions
                var aliceCanAct = aliceProxy.GameModel.CurrentPlayerId == "Alice";
                var bobCanAct = bobProxy.GameModel.CurrentPlayerId == "Bob";

                // At least one should be able to act (current player)
                Assert.True(aliceCanAct || bobCanAct, "At least one player should be the current player");

                await aliceProxy.DisposeAsync();
                await bobProxy.DisposeAsync();
                await gameSession.DisposeAsync();
            }
        }

        [Fact]
        public async Task CompanionWebInterface_ShouldServeCorrectly_Successfully()
        {
            using (new FunctionTimer("WebInterface", enableOverride: true))
            {
                var httpClient = _factory.CreateClient();

                // Test companion HTML page
                var response = await httpClient.GetAsync("/companion");
                Assert.True(response.IsSuccessStatusCode, $"Companion page should load. Status: {response.StatusCode}");

                var content = await response.Content.ReadAsStringAsync();
                Assert.Contains("Catan Companion", content);
                Assert.Contains("companion.css", content);
                Assert.Contains("companion.js", content);
                Assert.Contains("signalr.min.js", content);

                // Test companion CSS
                var cssResponse = await httpClient.GetAsync("/companion.css");
                Assert.True(cssResponse.IsSuccessStatusCode, $"Companion CSS should load. Status: {cssResponse.StatusCode}");

                // Test companion JavaScript
                var jsResponse = await httpClient.GetAsync("/companion.js");
                Assert.True(jsResponse.IsSuccessStatusCode, $"Companion JS should load. Status: {jsResponse.StatusCode}");

                var jsContent = await jsResponse.Content.ReadAsStringAsync();
                Assert.Contains("CatanCompanion", jsContent);
                Assert.Contains("SignalR", jsContent);
                Assert.Contains("gameHub", jsContent);
            }
        }

        [Fact]
        public async Task CompanionWithGameId_ShouldConnectDirectly_Successfully()
        {
            using (new FunctionTimer("DirectGameConnection", enableOverride: true))
            {
                var gameSession = await CreateTestGameSession();

                // Test companion with gameId parameter
                var httpClient = _factory.CreateClient();
                var response = await httpClient.GetAsync($"/companion?gameId={gameSession.GameId}");

                Assert.True(response.IsSuccessStatusCode, $"Direct game connection should work. Status: {response.StatusCode}");

                var content = await response.Content.ReadAsStringAsync();
                Assert.Contains($"window.INITIAL_GAME_ID = '{gameSession.GameId}'", content);

                await gameSession.DisposeAsync();
            }
        }

        [Fact]
        public async Task CompanionErrorHandling_ShouldHandleInvalidGameId_Gracefully()
        {
            using (new FunctionTimer("ErrorHandling", enableOverride: true))
            {
                var httpClient = _factory.CreateClient();

                // Test invalid game ID
                var response = await httpClient.GetAsync("/api/gamestate/invalid-game-id");
                Assert.Equal(System.Net.HttpStatusCode.NotFound, response.StatusCode);

                // Test empty games list when no games exist
                var gamesResponse = await httpClient.GetAsync("/api/companion/games");
                Assert.True(gamesResponse.IsSuccessStatusCode);

                var content = await gamesResponse.Content.ReadAsStringAsync();
                var gamesData = JsonHelper.Deserialize<CompanionGamesResponse>(content);

                Assert.NotNull(gamesData);
                Assert.NotNull(gamesData.Games);
            }
        }

        [Fact]
        public async Task CompanionDemoMode_ShouldWorkWithoutBackend_Successfully()
        {
            using (new FunctionTimer("DemoMode", enableOverride: true))
            {
                var httpClient = _factory.CreateClient();

                // Test demo mode endpoints
                var demoResponse = await httpClient.GetAsync("/demo");
                Assert.True(demoResponse.IsSuccessStatusCode, $"Demo page should load. Status: {demoResponse.StatusCode}");

                // Test specific game state demos
                var pickingBoardDemo = await httpClient.GetAsync("/companion/demo/PickingBoard");
                Assert.True(pickingBoardDemo.IsSuccessStatusCode);

                var waitingForRollDemo = await httpClient.GetAsync("/companion/demo/WaitingForRoll");
                Assert.True(waitingForRollDemo.IsSuccessStatusCode);

                // Verify demo mode injection
                var content = await pickingBoardDemo.Content.ReadAsStringAsync();
                Assert.Contains("window.DEMO_MODE = true", content);
                Assert.Contains("window.DEMO_STATE = 'PickingBoard'", content);
            }
        }

        #region Helper Methods

        private async Task<CompanionTestSession> CreateTestGameSession(string? gameIdSuffix = null)
        {
            var playerIds = new[] { "Alice", "Bob", "Charlie", "David", "Eve" };
            var session = new CompanionTestSession(_factory, GameType.Expansion, playerIds, gameIdSuffix);
            await session.InitializeAsync();
            return session;
        }

        private async Task TestGameDiscovery(string expectedGameId)
        {
            var httpClient = _factory.CreateClient();
            var response = await httpClient.GetAsync("/api/companion/games");
            Assert.True(response.IsSuccessStatusCode);

            var content = await response.Content.ReadAsStringAsync();
            var gamesResponse = JsonHelper.Deserialize<CompanionGamesResponse>(content);

            Assert.NotNull(gamesResponse);
            Assert.Contains(gamesResponse.Games, g => g.GameId == expectedGameId);
        }

        private async Task TestGameJoining(string gameId)
        {
            var httpClient = _factory.CreateClient();
            var response = await httpClient.GetAsync($"/api/gamestate/{gameId}");
            Assert.True(response.IsSuccessStatusCode);

            var content = await response.Content.ReadAsStringAsync();
            var gameModel = JsonHelper.Deserialize<GameModel>(content);

            Assert.NotNull(gameModel);
            Assert.Equal(gameId, gameModel.GameId);
        }

        private async Task TestPlayerSelection(string gameId)
        {
            var httpClient = _factory.CreateClient();
            var response = await httpClient.GetAsync($"/api/gamestate/{gameId}");
            var content = await response.Content.ReadAsStringAsync();
            var gameModel = JsonHelper.Deserialize<GameModel>(content);

            Assert.NotNull(gameModel);
            Assert.True(gameModel.Players.Count > 0);
            Assert.All(gameModel.Players, player =>
            {
                Assert.NotEmpty(player.Id);
                Assert.NotEmpty(player.Name);
            });
        }

        private async Task TestRealTimeUpdates(CompanionTestSession gameSession)
        {
            var uri = _factory.Server.BaseAddress ?? new Uri("http://localhost");
            var hubUrl = new Uri(uri, "/gameHub").ToString();
            var testHandler = _factory.Server.CreateHandler();

            var companionProxy = new GameServiceProxy(hubUrl, "http://localhost", testHandler, "Alice", gameSession.GameId);
            await companionProxy.ConnectAsync();

            var updateReceived = new TaskCompletionSource<GameModel>();
            companionProxy.GameStateUpdated += updateReceived.SetResult;

            // Trigger update from main session
            await gameSession.GetProxy("Alice").ExecuteShuffleAsync();

            // Verify companion received update
            var updatedGameModel = await updateReceived.Task.WaitAsync(TimeSpan.FromSeconds(5));
            Assert.NotNull(updatedGameModel);

            await companionProxy.DisposeAsync();
        }

        private async Task TestSignalRCommands(CompanionTestSession gameSession)
        {
            var uri = _factory.Server.BaseAddress ?? new Uri("http://localhost");
            var hubUrl = new Uri(uri, "/gameHub").ToString();
            var testHandler = _factory.Server.CreateHandler();

            var companionProxy = new GameServiceProxy(hubUrl, "http://localhost", testHandler, "Alice", gameSession.GameId);
            await companionProxy.ConnectAsync();

            // Test basic game actions
            var result = await companionProxy.ExecuteNextAsync();
            Assert.True(result.Success);

            await companionProxy.DisposeAsync();
        }

        #endregion
    }

    /// <summary>
    /// Test session wrapper for companion testing
    /// </summary>
    public class CompanionTestSession : IAsyncDisposable
    {
        private readonly WebApplicationFactory<Program> _factory;
        private readonly GameType _gameType;
        private readonly string[] _playerIds;
        private readonly Dictionary<string, GameServiceProxy> _proxies = [];

        public string GameId { get; private set; } = "";
        public string[] PlayerIds => _playerIds;

        public CompanionTestSession(WebApplicationFactory<Program> factory, GameType gameType, string[] playerIds, string? gameIdSuffix = null)
        {
            _factory = factory;
            _gameType = gameType;
            _playerIds = playerIds;
        }

        public async Task InitializeAsync()
        {
            // Create game via REST API
            var httpClient = _factory.CreateClient();
            var gameId = await CreateGameViaRest(httpClient, _gameType, _playerIds);
            GameId = gameId;

            // Connect all players via GameServiceProxy
            var connectTasks = _playerIds.Select(async playerId =>
            {
                var uri = _factory.Server.BaseAddress ?? new Uri("http://localhost");
                var hubUrl = new Uri(uri, "/gameHub").ToString();
                var testHandler = _factory.Server.CreateHandler();
                var proxy = new GameServiceProxy(hubUrl, "http://localhost", testHandler, playerId, gameId);
                await proxy.ConnectAsync();

                lock (_proxies)
                {
                    _proxies[playerId] = proxy;
                }
            });

            await Task.WhenAll(connectTasks);
        }

        public GameServiceProxy GetProxy(string playerId)
        {
            if (!_proxies.TryGetValue(playerId, out var proxy))
            {
                throw new InvalidOperationException($"Proxy for player {playerId} not found");
            }
            return proxy;
        }

        private static async Task<string> CreateGameViaRest(HttpClient httpClient, GameType gameType, string[] playerIds)
        {
            var newGameRequest = new
            {
                gameType = gameType.ToString(),
                playerIds = playerIds
            };

            var newGameJson = JsonHelper.Serialize(newGameRequest);
            var newGameContent = new StringContent(newGameJson, Encoding.UTF8, "application/json");

            var newGameResponse = await httpClient.PostAsync("/api/game/new", newGameContent);

            if (!newGameResponse.IsSuccessStatusCode)
            {
                var errorContent = await newGameResponse.Content.ReadAsStringAsync();
                throw new InvalidOperationException($"Failed to create game: {newGameResponse.StatusCode}. Error: {errorContent}");
            }

            var newGameBody = await newGameResponse.Content.ReadAsStringAsync();
            var newGameResult = JsonHelper.Deserialize<JsonElement>(newGameBody);

            if (!newGameResult.TryGetProperty("gameId", out var gameIdElement))
            {
                throw new InvalidOperationException("Game creation did not return gameId");
            }

            return gameIdElement.GetString() ??
                throw new InvalidOperationException("Game creation returned null gameId");
        }

        public async ValueTask DisposeAsync()
        {
            foreach (var proxy in _proxies.Values)
            {
                await proxy.DisposeAsync();
            }
            _proxies.Clear();
        }
    }

    /// <summary>
    /// Response model for companion games API
    /// </summary>
    public class CompanionGamesResponse
    {
        public List<CompanionGameInfo> Games { get; set; } = [];
    }

    /// <summary>
    /// Game info model for companion interface
    /// </summary>
    public class CompanionGameInfo
    {
        public string GameId { get; set; } = "";
        public string DisplayName { get; set; } = "";
        public string GameType { get; set; } = "";
        public string GameState { get; set; } = "";
        public int PlayerCount { get; set; }
        public List<string> PlayerNames { get; set; } = [];
        public string CurrentPlayer { get; set; } = "";
        public bool IsActive { get; set; }
        public string CreatedTimeDisplay { get; set; } = "";
    }
}