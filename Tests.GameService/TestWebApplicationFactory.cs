using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Tests.GameService
{
    /// <summary>
    /// Base factory configuration for all tests with comprehensive logging suppression
    /// </summary>
    public static class TestWebApplicationFactory
    {
        /// <summary>
        /// Creates a properly configured WebApplicationFactory with logging suppressed for tests
        /// </summary>
        public static WebApplicationFactory<Program> Create()
        {
            return new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
            {
                // Configure application settings for tests
                builder.ConfigureAppConfiguration((context, config) =>
                {
                    config.AddInMemoryCollection(new Dictionary<string, string?>
                    {
                        // Comprehensive logging suppression for clean test output
                        ["Logging:LogLevel:Default"] = "Error",
                        ["Logging:LogLevel:Microsoft"] = "Error",
                        ["Logging:LogLevel:Microsoft.AspNetCore"] = "Error",
                        ["Logging:LogLevel:Microsoft.AspNetCore.Hosting"] = "Error",
                        ["Logging:LogLevel:Microsoft.AspNetCore.Mvc"] = "Error",
                        ["Logging:LogLevel:Microsoft.AspNetCore.Routing"] = "Error",
                        ["Logging:LogLevel:Microsoft.AspNetCore.Server.Kestrel"] = "Error",
                        ["Logging:LogLevel:Microsoft.AspNetCore.HttpsPolicy"] = "Error",
                        ["Logging:LogLevel:Microsoft.Extensions.Hosting"] = "Error",
                        ["Logging:LogLevel:Microsoft.Hosting.Lifetime"] = "Error",
                        
                        // Suppress application-specific logging
                        ["Logging:LogLevel:Catan3"] = "Error",
                        ["Logging:LogLevel:Catan3.GameService"] = "Error",
                        ["Logging:LogLevel:Catan3.GameService.Controllers"] = "Error",
                        ["Logging:LogLevel:Catan3.GameService.Controllers.GameApiController"] = "Error",
                        ["Logging:LogLevel:Catan3.GameService.Services"] = "Error",
                        ["Logging:LogLevel:Catan3.GameService.Services.GameStateMachineService"] = "Error",
                        ["Logging:LogLevel:Catan3.GameService.Services.AsyncCommandProcessor"] = "Error",
                        ["Logging:LogLevel:Catan3.GameService.Hubs"] = "Error",
                        ["Logging:LogLevel:Catan3.GameService.Hubs.GameHub"] = "Error",
                        
                        // Console output suppression
                        ["Logging:Console:LogLevel:Default"] = "Error",
                        ["Console:LogLevel:Default"] = "Error"
                    });
                });
                
                // Configure services for testing
                builder.ConfigureServices(services =>
                {
                    // Configure logging services directly through dependency injection
                    services.Configure<LoggerFilterOptions>(options =>
                    {
                        options.MinLevel = LogLevel.Error;
                        
                        // Add filters for specific categories
                        options.AddFilter("Microsoft", LogLevel.Error);
                        options.AddFilter("Microsoft.AspNetCore", LogLevel.Error);
                        options.AddFilter("Microsoft.Hosting.Lifetime", LogLevel.Error);
                        options.AddFilter("Catan3.GameService", LogLevel.Error);
                        options.AddFilter("Catan3.GameService.Controllers", LogLevel.Error);
                        options.AddFilter("Catan3.GameService.Services", LogLevel.Error);
                        options.AddFilter("Catan3.GameService.Hubs", LogLevel.Error);
                    });
                    
                    // Override any specific service configurations for testing if needed
                });
            });
        }
    }
}