using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace Tests.GameService.SignalR
{
    /// <summary>
    /// Base class for SignalR tests with logging suppression configured
    /// </summary>
    public abstract class BaseSignalRTests : IClassFixture<WebApplicationFactory<Program>>
    {
        protected readonly WebApplicationFactory<Program> _factory;

        protected BaseSignalRTests(WebApplicationFactory<Program> factory)
        {
            _factory = factory.WithWebHostBuilder(builder =>
            {
                builder.ConfigureAppConfiguration((context, config) =>
                {
                    config.AddInMemoryCollection(new Dictionary<string, string?>
                    {
                        // Test configuration
                        ["GameApi:HangingGetTimeoutSeconds"] = "5",
                        
                        // Suppress logging during tests
                        ["Logging:LogLevel:Default"] = "Warning",
                        ["Logging:LogLevel:Microsoft"] = "Warning", 
                        ["Logging:LogLevel:Microsoft.AspNetCore"] = "Warning",
                        ["Logging:LogLevel:Microsoft.Hosting.Lifetime"] = "Warning",
                        ["Logging:LogLevel:Catan3.GameService"] = "Warning",
                        ["Logging:LogLevel:Catan3.GameService.Controllers"] = "Warning",
                        ["Logging:LogLevel:Catan3.GameService.Services"] = "Warning",
                        ["Logging:LogLevel:Catan3.GameService.Hubs"] = "Warning"
                    });
                });
            });
        }
    }
}