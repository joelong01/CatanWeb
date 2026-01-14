using Catan3.Shared.Models;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Xunit;
using Xunit.Abstractions;

namespace Tests.GameService.SignalR
{
    /// <summary>
    /// End-to-End Game Test: Complete STATEFUL game progression from start to finish using SignalR infrastructure.
    /// This is a single comprehensive test that follows the game through ALL states sequentially.
    /// 
    /// IMPORTANT: This test is STATEFUL and progresses through the game using proper actions.
    /// DO NOT use StateProgression to jump to states - use the correct actions (typically Next()) to advance.
    /// 
    /// We use EXPANSION games because they are a superset of regular games and test all functionality.
    /// The test progresses through: GameCreation → PickingBoard → WaitingForRollForOrder → 
    /// AllocateResourceForward → AllocateResourceReverse → WaitingForRoll → WaitingForNext → 
    /// PickSupplementalPlayers → Supplemental → MustMoveRobber
    /// 
    /// Updated to use GameServiceProxy from Catan3.Shared instead of local test helpers (Rule compliance).
    /// </summary>
    public class EndToEndGameTests : IClassFixture<WebApplicationFactory<Program>>
    {
        private readonly WebApplicationFactory<Program> _factory;
        private readonly ITestOutputHelper _output;
        //
        // during the allocation phase, we update the Buildings in GameModel.  the order of the Buildings is fixed
        // and the BuildingState is updated without worrying about order. But we need to know what the last building picked
        //  was so we can verify the resources.  we'll do that by just keeping a simple map of playerId -> Building because
        //  these tests are stateful...

        private readonly Dictionary<string, BuildingModel> _lastBuildingPicked = [];

        public EndToEndGameTests(WebApplicationFactory<Program> factory, ITestOutputHelper output)
        {
            _output = output;
            // Use the injected factory instead of creating a new one - this prevents multiple games
            _factory = factory.WithWebHostBuilder(builder =>
            {
                builder.ConfigureAppConfiguration((context, config) =>
                {
                    config.AddInMemoryCollection(new Dictionary<string, string?>
                    {
                        // Test configuration with short timeouts for faster tests
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
        /// Replays the Expansion.catan_test file using the new TestClient pattern.
        /// Verifies GameService matches Desktop app behavior for Expansion game scenario.
        /// </summary>
        [Fact(Skip = "Deprecated: Test file hashes no longer match current game logic. Use recording-based tests instead.")]
        public async Task ReplayExpansionTest()
        {
            using var replayTest = new ReplayTest(_factory, _output);
            await replayTest.ReplayTestFileAsync("Expansion.catan_test");
        }

        /// <summary>
        /// Replays the Regular.catan_test file using the new TestClient pattern.
        /// Verifies GameService matches Desktop app behavior for Regular game scenario.
        /// </summary>
        [Fact(Skip = "Deprecated: Test file hashes no longer match current game logic. Use recording-based tests instead.")]
        public async Task ReplayRegularTest()
        {
            using var replayTest = new ReplayTest(_factory, _output);
            await replayTest.ReplayTestFileAsync("Regular.catan_test");
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
}