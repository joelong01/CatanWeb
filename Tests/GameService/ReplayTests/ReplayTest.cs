using System.Runtime.CompilerServices;
using Catan3.Shared.Extensions;
using Catan3.Shared.Models;
using Catan3.Shared.Services;
using Catan3.Shared.Utility;
using Catan3.Shared.TestData;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Xunit;
using Xunit.Abstractions;

namespace Tests.GameService
{
    /// <summary>
    /// Dedicated class for replaying recorded .catan_test files against the GameService.
    /// Tests that GameService produces identical game state progression as the Desktop app.
    /// Uses TestClient pattern to simulate multiple connected clients receiving updates.
    /// </summary>
    public class ReplayTest : IDisposable
    {
        private readonly WebApplicationFactory<Program> _factory;
        private readonly ITestOutputHelper _output;
        private readonly List<TestClient> _activeTestClients = new();

        public ReplayTest(WebApplicationFactory<Program> factory, ITestOutputHelper output)
        {
            _output = output;
            
            // Configure factory with detailed logging for debugging
            _factory = factory.WithWebHostBuilder(builder =>
            {
                builder.ConfigureAppConfiguration((context, config) =>
                {
                    config.AddInMemoryCollection(new Dictionary<string, string?>
                    {
                        ["GameApi:HangingGetTimeoutSeconds"] = "5",
                        
                        // Enable detailed logging for debugging
                        ["Logging:LogLevel:Default"] = "Information",
                        ["Logging:LogLevel:Microsoft"] = "Warning",
                        ["Logging:LogLevel:Microsoft.AspNetCore"] = "Warning", 
                        ["Logging:LogLevel:Catan3.GameService"] = "Debug",
                        ["Logging:LogLevel:Catan3.GameService.Controllers"] = "Debug",
                        ["Logging:LogLevel:Catan3.GameService.Services"] = "Debug",
                        ["Logging:LogLevel:Catan3.GameService.Hubs"] = "Debug"
                    });
                });
                
                builder.ConfigureServices(services =>
                {
                    // Configure logging to output to test output
                    services.AddLogging(logging =>
                    {
                        logging.ClearProviders();
                        logging.AddProvider(new TestOutputLoggerProvider(_output));
                    });
                });
            });
        }

        /// <summary>
        /// Replays a .catan_test file against the GameService to verify behavioral consistency.
        /// Creates a main proxy for game creation and action execution, plus TestClients for synchronization verification.
        /// </summary>
        /// <param name="testFileName">Name of the .catan_test file to replay</param>
        public async Task ReplayTestFileAsync(string testFileName)
        {
            LogEvent("TestStart", $"🚀 Starting replay test for {testFileName}");

            // Step 1: Load and parse the test file
            var (initialGameModel, recordedActions, playerIds) = await LoadAndParseTestFile(testFileName);

            // Step 2: Setup clients and join game
            var (mainProxy, testClients, gameId) = await SetupClientsAndJoinGame(initialGameModel, playerIds);

            try
            {
                // Step 3: Execute main replay loop
                await ExecuteMainReplayLoop(mainProxy, testClients, recordedActions, gameId, testFileName);
            }
            finally
            {
                await mainProxy.DisposeAsync();
            }
        }

        /// <summary>
        /// Loads and parses the .catan_test file, validates the initial game model
        /// </summary>
        /// <param name="testFileName">Name of the .catan_test file to load</param>
        /// <returns>Tuple containing initial game model, recorded actions, and player IDs</returns>
        private async Task<(GameModel initialGameModel, IRecordedMessage[] recordedActions, List<string> playerIds)> LoadAndParseTestFile(string testFileName)
        {
            // Clear test log directory for debugging output
            ClearTestLogDirectory();

            // Load the test scenario from embedded resource
            var testScenario = await TestDataLoader.LoadTestScenarioAsync(testFileName);
            var initialGameModel = testScenario.InitialGameModel;
            var recordedActions = testScenario.RecordedActions;

            LogEvent("TestDataLoaded", $"Loaded {recordedActions.Length} recorded actions, initial GameHash: {initialGameModel.GameHash}");

            // Validate initial game model
            initialGameModel.Validate();
            var playerIds = initialGameModel.Players.Select(p => p.Id).ToList();
            LogEvent("PlayersIdentified", $"Players: {string.Join(", ", playerIds)}");

            return (initialGameModel, recordedActions, playerIds);
        }

        /// <summary>
        /// Sets up the main proxy and test clients, connects them all to the game
        /// </summary>
        /// <param name="initialGameModel">The initial game model to load</param>
        /// <param name="playerIds">List of player IDs from the game model</param>
        /// <returns>Tuple containing main proxy, test clients, and game ID</returns>
        private async Task<(GameServiceProxy mainProxy, List<TestClient> testClients, string gameId)> SetupClientsAndJoinGame(GameModel initialGameModel, List<string> playerIds)
        {
            // Get connection details from test factory
            var uri = _factory.Server.BaseAddress ?? new Uri("http://localhost");
            var hubUrl = new Uri(uri, "/gameHub").ToString();
            var serviceUri = uri.ToString().TrimEnd('/');
            var testHandler = _factory.Server.CreateHandler();

            // Create main proxy - represents the primary player who creates game and executes actions
            var mainProxy = new GameServiceProxy(hubUrl, serviceUri, testHandler, playerIds[0]);
            await mainProxy.ConnectAsync();
            LogEvent("MainProxyConnected", $"Main proxy connected for player {playerIds[0]}");
            
            // Main proxy loads the game (like a real primary player would)
            var loadResult = await mainProxy.LoadGameModelAsync(initialGameModel);
            if (!loadResult.Success)
            {
                throw new InvalidOperationException($"Failed to load GameModel: {loadResult.Message}");
            }
            
            var gameId = mainProxy.GameId ?? throw new InvalidOperationException("GameId not set after loading");
            LogEvent("GameCreated", $"✅ Main proxy created game with GameId: {gameId}");

            // Create TestClients for remaining players to simulate companion clients watching the game
            var companionPlayerIds = playerIds.Skip(1).ToList(); // All players except the main one
            var testClients = companionPlayerIds.Select(playerId => 
                new TestClient(playerId, initialGameModel, hubUrl, serviceUri, gameId, testHandler, _output)).ToList();
            _activeTestClients.AddRange(testClients);

            // Start all TestClients (connect to SignalR and join game)
            if (testClients.Any())
            {
                LogEvent("StartingCompanions", $"Starting {testClients.Count} companion TestClients");
                await Task.WhenAll(testClients.Select(client => client.StartAsync()));
                LogEvent("CompanionsStarted", "✅ All companion TestClients connected and ready");
            }

            return (mainProxy, testClients, gameId);
        }

        /// <summary>
        /// Executes the main replay loop: verify state → get update tasks → execute → wait for updates
        /// </summary>
        /// <param name="mainProxy">The main proxy that executes actions</param>
        /// <param name="testClients">List of companion test clients</param>
        /// <param name="recordedActions">Array of recorded actions to replay</param>
        /// <param name="gameId">The game ID</param>
        /// <param name="testFileName">Name of the test file for logging</param>
        private async Task ExecuteMainReplayLoop(GameServiceProxy mainProxy, List<TestClient> testClients, IRecordedMessage[] recordedActions, string gameId, string testFileName)
        {
            // Main replay loop: verify state → get update tasks → execute → wait for updates
            for (int i = 0; i < recordedActions.Length; i++)
            {
                var recordedAction = recordedActions[i];
                LogEvent("ReplayingAction", $"[{i + 1}/{recordedActions.Length}] {recordedAction.GetType().Name}");

                // Step 1: Verify all clients have expected GameHash BEFORE executing action
                if (recordedAction.ExpectedGameHash != null)
                {
                    VerifyAllClientsInSync(mainProxy, testClients, recordedAction.ExpectedGameHash);
                    LogEvent("HashVerified", $"✅ Pre-action hash verified: {recordedAction.ExpectedGameHash}");
                }

                // Step 2: Create update tasks for all companion clients (mainProxy manages its own state)
                var updateTasks = testClients.Select(client => client.GetUpdateTaskAsync()).ToArray();

                // Step 3: Set main proxy to current player and execute the recorded action
                var currentPlayer = mainProxy.GameModel?.CurrentPlayer() ?? throw new InvalidOperationException("CurrentPlayer is null");
                mainProxy.PlayerId = currentPlayer.Id;
                await ExecuteRecordedAction(mainProxy, recordedAction, gameId);

                // Step 4: Wait for all companion clients to receive GameStateUpdated messages
                if (updateTasks.Any())
                {
                    var timeout = TimeSpan.FromSeconds(10);
                    await Task.WhenAll(updateTasks).WaitAsync(timeout);
                    LogEvent("UpdatesReceived", $"✅ All {testClients.Count} companion clients received updates");
                }

                // Log GameModel for debugging (use mainProxy's current state)
                var currentGameModel = mainProxy.GameModel ?? throw new InvalidOperationException("MainProxy GameModel is null");
                LogGameModelToFile(currentGameModel, i + 1);
            }

            LogEvent("TestCompleted", $"✅ Successfully replayed {recordedActions.Length} actions from {testFileName}");
        }

        /// <summary>
        /// Verifies that main proxy and all TestClients have the same GameHash, indicating synchronized game state
        /// </summary>
        private void VerifyAllClientsInSync(GameServiceProxy mainProxy, List<TestClient> testClients, string expectedGameHash)
        {
            // Verify main proxy first
            var mainGameModel = mainProxy.GameModel ?? throw new InvalidOperationException("Main proxy GameModel is null");
            if (mainGameModel.GameHash != expectedGameHash)
            {
                throw new InvalidOperationException(
                    $"Main proxy has GameHash {mainGameModel.GameHash}, expected {expectedGameHash}");
            }

            // Verify all companion clients
            foreach (var client in testClients)
            {
                var actualHash = client.CurrentGameModel.GameHash;
                if (actualHash != expectedGameHash)
                {
                    throw new InvalidOperationException(
                        $"Client {client.PlayerId} has GameHash {actualHash}, expected {expectedGameHash}");
                }
            }
        }

        /// <summary>
        /// Executes a recorded action using the main proxy (represents primary player executing action)
        /// </summary>
        private async Task ExecuteRecordedAction(GameServiceProxy mainProxy, IRecordedMessage recordedAction, string gameId)
        {
            switch (recordedAction)
            {
                case ShuffleRecord:
                    await mainProxy.ExecuteShuffleAsync();
                    break;
                case NextRecord:
                    await mainProxy.ExecuteNextAsync();
                    break;
                case GoFirstRecord goFirst:
                    await mainProxy.ExecuteGoFirstAsync(goFirst.PlayerId);
                    break;
                case BuildingUpgradeRecord buildingUpgrade:
                    await mainProxy.ExecuteBuildingUpgradeAsync(gameId, buildingUpgrade.BuildingKey);
                    break;
                case RoadPurchaseRecord roadPurchase:
                    await mainProxy.ExecuteRoadPurchaseAsync(gameId, roadPurchase.RoadKey);
                    break;
                case MoveRobberRecord moveRobber:
                    await mainProxy.ExecuteMoveRobberAsync(gameId, moveRobber.Coordinates, moveRobber.TargetPlayerId);
                    break;
                case RollRecord roll:
                    await mainProxy.ExecuteRollAsync(roll.Roll.RedRoll, roll.Roll.WhiteRoll);
                    break;
                case SetPlayerOrderRecord setPlayerOrder:
                    await mainProxy.ExecuteSetPlayerOrderAsync(gameId, setPlayerOrder.PlayerIds);
                    break;
                case ParticipatingInSupplementalRecord participatingInSupplemental:
                    await mainProxy.ExecuteParticipatingInSupplementalAsync(participatingInSupplemental.PlayerId, participatingInSupplemental.Participating);
                    break;
                case BalanceBoardRecord:
                    await mainProxy.ExecuteBalanceBoardAsync(gameId);
                    break;
                case PurchaseRecord purchase:
                    await mainProxy.ExecutePurchaseAsync(purchase.Entitlement);
                    break;
                case UndoRecord:
                    await mainProxy.ExecuteUndoAsync();
                    break;
                case RedoRecord:
                    await mainProxy.ExecuteRedoAsync();
                    break;
                default:
                    throw new NotImplementedException($"Action type {recordedAction.GetType().Name} not implemented");
            }
        }

        /// <summary>
        /// Clears and recreates the test_log directory for GameModel debugging output
        /// </summary>
        private void ClearTestLogDirectory()
        {
            var testLogDir = Path.Combine(Directory.GetCurrentDirectory(), "test_log");
            if (Directory.Exists(testLogDir))
            {
                Directory.Delete(testLogDir, true);
            }
            Directory.CreateDirectory(testLogDir);
        }

        /// <summary>
        /// Logs a GameModel to a JSON file for debugging purposes
        /// </summary>
        private void LogGameModelToFile(GameModel gameModel, int actionNumber)
        {
            var testLogDir = Path.Combine(Directory.GetCurrentDirectory(), "test_log");
            var fileName = $"GameModel.{actionNumber}.json";
            var filePath = Path.Combine(testLogDir, fileName);
            var gameModelJson = JsonHelper.Serialize(gameModel);
            File.WriteAllText(filePath, gameModelJson);
            LogEvent("GameModelLogged", $"Logged {fileName} with GameHash: {gameModel.GameHash}");
        }

        /// <summary>
        /// Centralized logging that outputs to xUnit test results
        /// </summary>
        private void LogEvent(string eventType, string message, [CallerMemberName] string method = "", [CallerLineNumber] int line = 0)
        {
            var timestamp = DateTime.UtcNow.ToString("HH:mm:ss.fff");
            _output.WriteLine($"[{method}:{line}] [{timestamp}] [REPLAY-{eventType}] {message}");
        }

        /// <summary>
        /// Dispose all active TestClients when test completes
        /// </summary>
        public void Dispose()
        {
            foreach (var client in _activeTestClients)
            {
                client.Dispose();
            }
            _activeTestClients.Clear();
        }
    }

    /// <summary>
    /// Custom logger provider that routes all logs to xUnit test output
    /// </summary>
    public class TestOutputLoggerProvider : ILoggerProvider
    {
        private readonly ITestOutputHelper _output;

        public TestOutputLoggerProvider(ITestOutputHelper output)
        {
            _output = output;
        }

        public ILogger CreateLogger(string categoryName)
        {
            return new TestOutputLogger(_output, categoryName);
        }

        public void Dispose() { }
    }

    /// <summary>
    /// Custom logger that writes to xUnit test output
    /// </summary>
    public class TestOutputLogger : ILogger
    {
        private readonly ITestOutputHelper _output;
        private readonly string _categoryName;

        public TestOutputLogger(ITestOutputHelper output, string categoryName)
        {
            _output = output;
            _categoryName = categoryName;
        }

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => logLevel >= LogLevel.Debug;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            if (!IsEnabled(logLevel))
                return;

            try
            {
                var timestamp = DateTime.UtcNow.ToString("HH:mm:ss.fff");
                var message = formatter(state, exception);
                var logLine = $"[{timestamp}] [{logLevel}] [{_categoryName}] {message}";
                
                if (exception != null)
                {
                    logLine += $"\n{exception}";
                }
                
                _output.WriteLine(logLine);
            }
            catch
            {
                // Ignore logging errors during tests
            }
        }
    }
}