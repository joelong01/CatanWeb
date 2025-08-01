using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System.CommandLine; // RootCommand, Command
using System.Threading.Tasks; // Task
using Microsoft.AspNetCore.SignalR.Client; // For SignalR HubConnection
using Catan3.Shared.Models; // If you're using GameType directly in this file

using TestClient.Commands;
using TestClient.Services;

namespace TestClient;

/// <summary>
/// Test Client Helper - Real-time game testing and interaction tool
/// Connects to running GameService instances via SignalR for end-to-end testing
/// This is used by the test infrastructure, not as a standalone executable.
/// </summary>
public static class TestClientHelper
{
    /// <summary>
    /// Creates a configured host for dependency injection in tests
    /// </summary>
    public static IHost CreateHost()
    {
        return Host.CreateDefaultBuilder()
            .ConfigureServices((context, services) =>
            {
                services.AddSingleton<GameRunner>();
                services.AddSingleton<GameSessionManager>();
                services.AddLogging(builder =>
                {
                    builder.AddConsole();
                    builder.SetMinimumLevel(LogLevel.Information);
                });
            })
            .Build();
    }

    /// <summary>
    /// Parses log level from string
    /// </summary>
    public static LogLevel ParseLogLevel(string logLevel)
    {
        return logLevel.ToUpper() switch
        {
            "DEBUG" => LogLevel.Debug,
            "TRACE" => LogLevel.Trace,
            "INFO" => LogLevel.Information,
            "WARNING" => LogLevel.Warning,
            "ERROR" => LogLevel.Error,
            _ => LogLevel.Error
        };
    }

    /// <summary>
    /// Creates a GameRunOptions instance for testing
    /// </summary>
    public static GameRunOptions CreateGameRunOptions(
        Catan3.Shared.Models.GameType gameType,
        int? playerCount = null,
        string? runToState = null,
        bool complete = false,
        bool noExit = false,
        string logLevel = "ERROR",
        string serverUri = "http://localhost:8080")
    {
        var options = new GameRunOptions
        {
            GameType = gameType,
            PlayerCount = playerCount ?? (gameType == Catan3.Shared.Models.GameType.Expansion ? 5 : 3),
            RunToState = runToState,
            Complete = complete,
            NoExit = noExit,
            LogLevel = ParseLogLevel(logLevel),
            ServerUri = serverUri
        };

        // Validate options before returning
        options.Validate();

        return options;
    }
}