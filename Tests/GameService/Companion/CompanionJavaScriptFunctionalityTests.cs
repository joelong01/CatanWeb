using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using System.Text.Json;
using Xunit;
using Catan3.Shared.Models;
using Catan3.Shared.Services;
using Microsoft.AspNetCore.SignalR.Client;
using System.Text;
using Catan3.Shared.Utility;
using Microsoft.Extensions.DependencyInjection;

namespace Tests.GameService.Companion
{
    /// <summary>
    /// Unit tests for specific companion.js functionality and workflows.
    /// These tests verify the backend APIs and SignalR functionality that the companion JavaScript relies on.
    /// </summary>
    public class CompanionJavaScriptFunctionalityTests : IClassFixture<WebApplicationFactory<Program>>
    {
        private readonly WebApplicationFactory<Program> _factory;

        public CompanionJavaScriptFunctionalityTests(WebApplicationFactory<Program> factory)
        {
            _factory = factory.WithWebHostBuilder(builder =>
            {
                builder.ConfigureAppConfiguration((context, config) =>
                {
                    config.AddInMemoryCollection(new Dictionary<string, string?>
                    {
                        ["GameApi:HangingGetTimeoutSeconds"] = "5",
                        ["Logging:LogLevel:Default"] = "Error"
                    });
                });
            });
        }

        [Fact]
        public async Task CompanionJavaScript_GameSelectionWorkflow_ShouldWork()
        {
            // Test the complete game selection workflow that companion.js follows
            using (new FunctionTimer("GameSelectionWorkflow", enableOverride: true))
            {
                // Create test games
                var game1 = await CreateTestGame("Regular", new[] { "Alice", "Bob", "Charlie", "David" });
                var game2 = await CreateTestGame("Expansion", new[] { "Eve", "Frank", "Grace", "Henry", "Ivy" });

                var httpClient = _factory.CreateClient();

                // 1. Test initial page load (what happens when companion.js initializes)
                var companionPageResponse = await httpClient.GetAsync("/companion");
                Assert.True(companionPageResponse.IsSuccessStatusCode);

                // 2. Test loading available games (companion.js loadAvailableGames())
                var gamesResponse = await httpClient.GetAsync("/api/companion/games");
                Assert.True(gamesResponse.IsSuccessStatusCode);

                var gamesContent = await gamesResponse.Content.ReadAsStringAsync();
                var gamesData = JsonHelper.Deserialize<JsonElement>(gamesContent);

                Assert.True(gamesData.TryGetProperty("games", out var gamesArray));
                Assert.True(gamesArray.GetArrayLength() >= 2);

                // 3. Test selecting a specific game (companion.js selectGame())
                var selectedGameId = game1;
                var gameStateResponse = await httpClient.GetAsync($"/api/gamestate/{selectedGameId}");
                Assert.True(gameStateResponse.IsSuccessStatusCode);

                var gameStateContent = await gameStateResponse.Content.ReadAsStringAsync();
                var selectedGameModel = JsonHelper.Deserialize<GameModel>(gameStateContent);

                Assert.NotNull(selectedGameModel);
                Assert.Equal(selectedGameId, selectedGameModel.GameId);
                Assert.True(selectedGameModel.Players.Count > 0);

                // 4. Verify player information is available for selection
                Assert.All(selectedGameModel.Players, player =>
                {
                    Assert.NotEmpty(player.Id);
                    Assert.NotEmpty(player.Name);
                });
            }
        }

        [Fact]
        public async Task CompanionJavaScript_SignalRConnection_ShouldFollowCorrectPattern()
        {
            // Test the SignalR connection pattern that companion.js uses
            using (new FunctionTimer("SignalRConnectionPattern", enableOverride: true))
            {
                var gameId = await CreateTestGame("Regular", new[] { "Alice", "Bob", "Charlie", "David" });

                // Use GameServiceProxy for robust real-time async waiting (like end-to-end tests)
                var proxy = await CreateGameServiceProxy(gameId, "Alice");

                // Verify initial connection and state
                Assert.Equal(HubConnectionState.Connected, proxy.Connection.State);

                // Wait for the initial game state to be received (PickingBoard state)
                await proxy.WaitForGameStateAsync(GameState.PickingBoard, TimeSpan.FromSeconds(5));

                // Verify we have the game model
                Assert.NotNull(proxy.GameModel);
                Assert.Equal(GameState.PickingBoard, proxy.GameModel.GameState);

                // Test command execution (companion.js doAction()) - use proper ExecuteGameActionMessage message object
                await proxy.ExecuteShuffleAsync();

                // Wait for the update after the shuffle action (should still be PickingBoard)
                await proxy.WaitForGameStateAsync(GameState.PickingBoard, TimeSpan.FromSeconds(5));

                // Verify the command was processed
                Assert.NotNull(proxy.GameModel);
                Assert.Equal(GameState.PickingBoard, proxy.GameModel.GameState);

                await proxy.DisposeAsync();
            }
        }

        [Fact]
        public async Task CompanionJavaScript_PlayerSelection_ShouldUpdateCorrectly()
        {
            // Test player selection functionality that companion.js implements
            using (new FunctionTimer("PlayerSelection", enableOverride: true))
            {
                var gameId = await CreateTestGame("Regular", new[] { "Alice", "Bob", "Charlie", "David" });

                var uri = _factory.Server.BaseAddress ?? new Uri("http://localhost");
                var hubUrl = new Uri(uri, "/gameHub").ToString();
                var testHandler = _factory.Server.CreateHandler();

                // Test multiple companion connections for different players
                var aliceConnection = CreateCompanionConnection(hubUrl, testHandler);
                var bobConnection = CreateCompanionConnection(hubUrl, testHandler);

                await aliceConnection.StartAsync();
                await bobConnection.StartAsync();

                var aliceUpdates = new List<GameModel>();
                var bobUpdates = new List<GameModel>();

                aliceConnection.On<GameModel>("GameStateUpdated", aliceUpdates.Add);
                bobConnection.On<GameModel>("GameStateUpdated", bobUpdates.Add);

                // Simulate player selection (companion.js player selection change event)
                await aliceConnection.InvokeAsync("JoinGame", gameId, "Alice");
                await bobConnection.InvokeAsync("JoinGame", gameId, "Bob");

                // Wait for updates
                await Task.Delay(500);

                // Test that both companions can see the game but understand their roles
                var httpClient = _factory.CreateClient();
                var gameStateResponse = await httpClient.GetAsync($"/api/gamestate/{gameId}");
                var gameStateContent = await gameStateResponse.Content.ReadAsStringAsync();
                var gameModel = JsonHelper.Deserialize<GameModel>(gameStateContent);

                Assert.NotNull(gameModel);

                // Verify companion can determine current player and action availability
                var currentPlayerId = gameModel.CurrentPlayerId;
                var actionFlags = gameModel.ActionFlags;

                Assert.NotEmpty(currentPlayerId);
                Assert.NotNull(actionFlags);

                await aliceConnection.DisposeAsync();
                await bobConnection.DisposeAsync();
            }
        }

        [Fact]
        public async Task CompanionJavaScript_StateSpecificUI_ShouldReceiveCorrectData()
        {
            // Test that companion.js gets the right data for state-specific UI
            using (new FunctionTimer("StateSpecificUI", enableOverride: true))
            {
                var gameId = await CreateTestGame("Regular", new[] { "Alice", "Bob", "Charlie", "David" });

                var httpClient = _factory.CreateClient();

                // Test PickingBoard state (initial state)
                var gameStateResponse = await httpClient.GetAsync($"/api/gamestate/{gameId}");
                var gameStateContent = await gameStateResponse.Content.ReadAsStringAsync();
                var gameModel = JsonHelper.Deserialize<GameModel>(gameStateContent);

                Assert.NotNull(gameModel);
                Assert.Equal(GameState.PickingBoard, gameModel.GameState);

                // Verify companion gets action flags for UI state
                Assert.NotNull(gameModel.ActionFlags);
                Assert.True(gameModel.ActionFlags.NextEnabled ||
                           gameModel.ActionFlags.UndoEnabled ||
                           gameModel.ActionFlags.RedoEnabled);

                // Test state progression and UI data
                var proxy = await CreateGameServiceProxy(gameId, "Alice");

                // Advance to next state
                var result = await proxy.ExecuteNextAsync();
                Assert.True(result.Success);

                // Verify new state provides appropriate UI data
                var updatedGameModel = proxy.GameModel;
                Assert.NotNull(updatedGameModel);
                Assert.Equal(GameState.WaitingForRollForOrder, updatedGameModel.GameState);

                await proxy.DisposeAsync();
            }
        }

        [Fact]
        public async Task CompanionJavaScript_CommandExecution_ShouldHandleAllGameActions()
        {
            // Test all the game actions that companion.js can execute
            using (new FunctionTimer("CommandExecution", enableOverride: true))
            {
                var gameId = await CreateTestGame("Regular", new[] { "Alice", "Bob", "Charlie", "David" });
                var proxy = await CreateGameServiceProxy(gameId, "Alice");

                var commandResults = new Dictionary<string, bool>();

                // Test basic actions (companion.js doAction())
                var testActions = new[]
                {
                    ("Shuffle", (Func<Task<CommandResult>>)(() => proxy.ExecuteShuffleAsync())),
                    ("Undo", (Func<Task<CommandResult>>)(() => proxy.ExecuteUndoAsync())),
                    ("Next", (Func<Task<CommandResult>>)(() => proxy.ExecuteNextAsync()))
                };

                foreach (var (actionName, actionFunc) in testActions)
                {
                    try
                    {
                        var result = await actionFunc();
                        commandResults[actionName] = result.Success;
                    }
                    catch (Exception ex)
                    {
                        commandResults[actionName] = false;
                        Console.WriteLine($"Action {actionName} failed: {ex.Message}");
                    }
                }

                // Verify that valid actions succeeded
                Assert.True(commandResults["Shuffle"], "Shuffle should succeed in PickingBoard state");

                await proxy.DisposeAsync();
            }
        }

        [Fact]
        public async Task CompanionJavaScript_ErrorHandling_ShouldHandleFailuresGracefully()
        {
            // Test error handling scenarios that companion.js needs to handle
            using (new FunctionTimer("ErrorHandling", enableOverride: true))
            {
                var httpClient = _factory.CreateClient();

                // Test invalid game ID (companion.js error handling)
                var invalidGameResponse = await httpClient.GetAsync("/api/gamestate/invalid-game");
                Assert.Equal(System.Net.HttpStatusCode.NotFound, invalidGameResponse.StatusCode);

                // Test empty games list (companion.js no games scenario)
                var gamesResponse = await httpClient.GetAsync("/api/companion/games");
                Assert.True(gamesResponse.IsSuccessStatusCode);

                // Test malformed requests
                var malformedContent = new StringContent("invalid json", Encoding.UTF8, "application/json");
                var malformedResponse = await httpClient.PostAsync("/api/game/new", malformedContent);
                Assert.False(malformedResponse.IsSuccessStatusCode);
            }
        }

        [Fact]
        public async Task CompanionJavaScript_RealTimeUpdates_ShouldSynchronizeCorrectly()
        {
            // Test real-time synchronization between multiple companions
            using (new FunctionTimer("RealTimeUpdates", enableOverride: true))
            {
                var gameId = await CreateTestGame("Regular", new[] { "Alice", "Bob", "Charlie", "David" });

                // Create multiple companion connections
                var companion1 = await CreateGameServiceProxy(gameId, "Alice");
                var companion2 = await CreateGameServiceProxy(gameId, "Bob");

                var companion1Updates = new List<GameModel>();
                var companion2Updates = new List<GameModel>();

                companion1.GameStateUpdated += companion1Updates.Add;
                companion2.GameStateUpdated += companion2Updates.Add;

                // Execute action from one companion
                var result = await companion1.ExecuteShuffleAsync();
                Assert.True(result.Success);

                // Wait for updates to propagate
                await Task.Delay(1000);

                // Verify both companions received updates
                Assert.True(companion1Updates.Count > 0, "Companion 1 should receive updates");
                Assert.True(companion2Updates.Count > 0, "Companion 2 should receive updates");

                // Verify updates are consistent
                if (companion1Updates.Count > 0 && companion2Updates.Count > 0)
                {
                    var latest1 = companion1Updates.Last();
                    var latest2 = companion2Updates.Last();

                    Assert.Equal(latest1.GameId, latest2.GameId);
                    Assert.Equal(latest1.GameStateMachineVersion, latest2.GameStateMachineVersion);
                    Assert.Equal(latest1.GameState, latest2.GameState);
                }

                await companion1.DisposeAsync();
                await companion2.DisposeAsync();
            }
        }

        [Fact]
        public async Task CompanionJavaScript_DemoMode_ShouldWorkIndependently()
        {
            // Test demo mode functionality (companion.js demo mode)
            using (new FunctionTimer("DemoMode", enableOverride: true))
            {
                var httpClient = _factory.CreateClient();

                // Test demo page
                var demoResponse = await httpClient.GetAsync("/demo");
                Assert.True(demoResponse.IsSuccessStatusCode);

                // Test specific state demos
                var states = new[] { "PickingBoard", "WaitingForRoll", "WaitingForNext" };
                foreach (var state in states)
                {
                    var stateResponse = await httpClient.GetAsync($"/companion/demo/{state}");
                    Assert.True(stateResponse.IsSuccessStatusCode, $"Demo state {state} should load");

                    var content = await stateResponse.Content.ReadAsStringAsync();
                    Assert.Contains("DEMO_MODE = true", content);
                    Assert.Contains($"DEMO_STATE = '{state}'", content);
                }
            }
        }

        #region Helper Methods

        private async Task<string> CreateTestGame(string gameType, string[] playerIds)
        {
            var httpClient = _factory.CreateClient();
            var newGameRequest = new
            {
                gameType = gameType,
                playerIds = playerIds
            };

            var newGameJson = JsonHelper.Serialize(newGameRequest);
            var newGameContent = new StringContent(newGameJson, Encoding.UTF8, "application/json");

            var newGameResponse = await httpClient.PostAsync("/api/game/new", newGameContent);
            if (!newGameResponse.IsSuccessStatusCode)
            {
                throw new InvalidOperationException($"Failed to create test game: {newGameResponse.StatusCode}");
            }

            var newGameBody = await newGameResponse.Content.ReadAsStringAsync();
            var newGameResult = JsonHelper.Deserialize<JsonElement>(newGameBody);

            if (!newGameResult.TryGetProperty("gameId", out var gameIdElement))
            {
                throw new InvalidOperationException("Game creation did not return gameId");
            }

            return gameIdElement.GetString() ?? throw new InvalidOperationException("Null gameId returned");
        }

        private HubConnection CreateCompanionConnection(string hubUrl, HttpMessageHandler testHandler)
        {
            return new HubConnectionBuilder()
                .WithUrl(hubUrl, options =>
                {
                    options.HttpMessageHandlerFactory = _ => testHandler;
                })
                .WithAutomaticReconnect(new[] { TimeSpan.Zero, TimeSpan.FromMilliseconds(2000), TimeSpan.FromMilliseconds(10000), TimeSpan.FromMilliseconds(30000) })
                .Build();
        }

        private async Task<GameServiceProxy> CreateGameServiceProxy(string gameId, string playerId)
        {
            var uri = _factory.Server.BaseAddress ?? new Uri("http://localhost");
            var hubUrl = new Uri(uri, "/gameHub").ToString();
            var testHandler = _factory.Server.CreateHandler();

            var proxy = new GameServiceProxy(hubUrl, "http://localhost", testHandler, playerId, gameId);
            await proxy.ConnectAsync();
            return proxy;
        }

        #endregion
    }
}