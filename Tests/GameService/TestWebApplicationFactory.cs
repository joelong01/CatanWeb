using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.DependencyInjection;

namespace Tests.GameService
{
    /// <summary>
    /// Base factory configuration for all tests with comprehensive logging suppression
    /// When running tests normally, logs are suppressed for clean output.
    /// When debugging (Debugger.IsAttached), Information level logging is enabled for the GameService components.
    /// This allows developers to see detailed server-side logging during test debugging without cluttering normal test runs.
    /// </summary>
    public static class TestWebApplicationFactory
    {
        /// <summary>
        /// Creates a properly configured WebApplicationFactory with logging suppressed for tests
        /// When debugging, enables Information level logging for better visibility
        /// </summary>
        public static WebApplicationFactory<Program> Create()
        {
            // Hard-coded verbose logging for debugging test failures
            var enableVerboseLogging = true; // TEMPORARY: Always enable to debug timeout/discovery issues

            var logLevel = enableVerboseLogging ? "Information" : "Error";
            var gameServiceLogLevel = enableVerboseLogging ? "Information" : "Error";

            return new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
            {
                // Configure application settings for tests
                builder.ConfigureAppConfiguration((context, config) =>
                {
                    config.AddInMemoryCollection(new Dictionary<string, string?>
                    {
                        // Comprehensive logging suppression for clean test output (unless debugging)
                        ["Logging:LogLevel:Default"] = logLevel,
                        ["Logging:LogLevel:Microsoft"] = "Error", // Keep Microsoft logs quiet
                        ["Logging:LogLevel:Microsoft.AspNetCore"] = "Error",
                        ["Logging:LogLevel:Microsoft.AspNetCore.Hosting"] = "Error",
                        ["Logging:LogLevel:Microsoft.AspNetCore.Mvc"] = "Error",
                        ["Logging:LogLevel:Microsoft.AspNetCore.Routing"] = "Error",
                        ["Logging:LogLevel:Microsoft.AspNetCore.Server.Kestrel"] = "Error",
                        ["Logging:LogLevel:Microsoft.AspNetCore.HttpsPolicy"] = "Error",
                        ["Logging:LogLevel:Microsoft.Extensions.Hosting"] = "Error",
                        ["Logging:LogLevel:Microsoft.Hosting.Lifetime"] = "Error",

                        // Enable application-specific logging when debugging
                        ["Logging:LogLevel:Catan3"] = gameServiceLogLevel,
                        ["Logging:LogLevel:Catan3.GameService"] = gameServiceLogLevel,
                        ["Logging:LogLevel:Catan3.GameService.Controllers"] = gameServiceLogLevel,
                        ["Logging:LogLevel:Catan3.GameService.Controllers.GameApiController"] = gameServiceLogLevel,
                        ["Logging:LogLevel:Catan3.GameService.Services"] = gameServiceLogLevel,
                        ["Logging:LogLevel:Catan3.GameService.Services.GameStateMachineService"] = gameServiceLogLevel,
                        ["Logging:LogLevel:Catan3.GameService.Services.AsyncCommandProcessor"] = gameServiceLogLevel,
                        ["Logging:LogLevel:Catan3.GameService.Hubs"] = gameServiceLogLevel,
                        ["Logging:LogLevel:Catan3.GameService.Hubs.GameHub"] = gameServiceLogLevel,

                        // Console output configuration
                        ["Logging:Console:LogLevel:Default"] = logLevel,
                        ["Console:LogLevel:Default"] = logLevel
                    });
                });

                // Configure services for testing
                builder.ConfigureServices(services =>
                {
                    // Configure logging services directly through dependency injection
                    var minLogLevel = enableVerboseLogging ? LogLevel.Information : LogLevel.Error;

                    services.Configure<LoggerFilterOptions>(options =>
                    {
                        options.MinLevel = minLogLevel;

                        // Add filters for specific categories
                        options.AddFilter("Microsoft", LogLevel.Error); // Keep Microsoft quiet
                        options.AddFilter("Microsoft.AspNetCore", LogLevel.Error);
                        options.AddFilter("Microsoft.Hosting.Lifetime", LogLevel.Error);

                        // Enable our application logging when verbose mode is active
                        if (enableVerboseLogging)
                        {
                            options.AddFilter("Catan3.GameService", LogLevel.Information);
                            options.AddFilter("Catan3.GameService.Controllers", LogLevel.Information);
                            options.AddFilter("Catan3.GameService.Services", LogLevel.Information);
                            options.AddFilter("Catan3.GameService.Hubs", LogLevel.Information);
                        }
                        else
                        {
                            options.AddFilter("Catan3.GameService", LogLevel.Error);
                            options.AddFilter("Catan3.GameService.Controllers", LogLevel.Error);
                            options.AddFilter("Catan3.GameService.Services", LogLevel.Error);
                            options.AddFilter("Catan3.GameService.Hubs", LogLevel.Error);
                        }
                    });

                    // Override any specific service configurations for testing if needed
                });
            });
        }

        /// <summary>
        /// Creates a factory with verbose logging enabled regardless of environment
        /// Use this for debugging specific failing tests
        /// </summary>
        public static WebApplicationFactory<Program> CreateVerbose()
        {
            return Create(); // All factories are verbose now due to hard-coded setting
        }
    }
}