using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System.CommandLine;
using Catan3.CLI.Commands;
using Catan3.CLI.Services;

namespace Catan3.CLI;

/// <summary>
/// Catan CLI Test Client - Real-time game testing and interaction tool
/// Connects to running GameService instances via SignalR for end-to-end testing
/// 
/// Usage: catan_cli <GameType> [options]
/// Example: catan_cli expansion --complete --no-exit --log-level DEBUG --uri http://localhost:8080
/// </summary>
public class Program
{
    public static async Task<int> Main(string[] args)
    {
        // Create the root command
        var rootCommand = new RootCommand("Catan CLI - End-to-end game testing and interaction tool");

        // Create host for dependency injection
        var host = CreateHost();

        // Add game type commands
        var expansionCommand = new Command("expansion", "Start an Expansion game (5 players)");
        var regularCommand = new Command("regular", "Start a Regular game (3-4 players)");
        var testCommand = new Command("test", "Run various CLI tests");
        var extractCommand = new Command("extract", "Extract GameModel from .catan file to JSON");

        // Add common options to both game commands
        AddGameOptions(expansionCommand, host, Catan3.Shared.Models.GameType.Expansion);
        AddGameOptions(regularCommand, host, Catan3.Shared.Models.GameType.Regular);

        // Add test command options
        AddTestOptions(testCommand, host);
        
        // Add extract command options
        AddExtractOptions(extractCommand);

        rootCommand.AddCommand(expansionCommand);
        rootCommand.AddCommand(regularCommand);
        rootCommand.AddCommand(testCommand);
        rootCommand.AddCommand(extractCommand);

        // Execute the command
        return await rootCommand.InvokeAsync(args);
    }

    private static void AddGameOptions(Command command, IHost host, Catan3.Shared.Models.GameType gameType)
    {
        // Add options
        var playerCountOption = new Option<int>(
            "--player-count",
            getDefaultValue: () => gameType == Catan3.Shared.Models.GameType.Expansion ? 5 : 3,
            "Number of players in the game");

        var runToOption = new Option<string?>(
            "--run-to",
            "Stop execution at the first instance of this GameState");

        var completeOption = new Option<bool>(
            "--complete",
            "Run the complete end-to-end test, ignoring --run-to");

        var noExitOption = new Option<bool>(
            "--no-exit",
            "Keep the game state alive after completion (press Ctrl+C to exit)");

        var logLevelOption = new Option<string>(
            "--log-level",
            getDefaultValue: () => "ERROR",
            "Logging level: DEBUG, TRACE, INFO, WARNING, ERROR");

        var uriOption = new Option<string>(
            "--uri",
            getDefaultValue: () => "http://localhost:8080",
            "GameService server URI (for debugging: http://localhost:8080, for production: https://yourserver.com)");

        command.AddOption(playerCountOption);
        command.AddOption(runToOption);
        command.AddOption(completeOption);
        command.AddOption(noExitOption);
        command.AddOption(logLevelOption);
        command.AddOption(uriOption);

        command.SetHandler(async (playerCount, runTo, complete, noExit, logLevel, uri) =>
        {
            try
            {
                var options = new GameRunOptions
                {
                    GameType = gameType,
                    PlayerCount = playerCount,
                    RunToState = runTo,
                    Complete = complete,
                    NoExit = noExit,
                    LogLevel = ParseLogLevel(logLevel),
                    ServerUri = uri
                };

                // Validate options before proceeding
                options.Validate();

                var gameRunner = host.Services.GetRequiredService<GameRunner>();
                await gameRunner.RunGameAsync(options);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"? Error: {ex.Message}");
                Environment.Exit(1);
            }
        }, playerCountOption, runToOption, completeOption, noExitOption, logLevelOption, uriOption);
    }

    private static void AddTestOptions(Command command, IHost host)
    {
        // Add test-specific options
        var mvvmObjectsOption = new Option<bool>(
            "--mvvm-objects",
            "Test serialization/deserialization of all MVVM message objects");

        command.AddOption(mvvmObjectsOption);

        command.SetHandler(async (mvvmObjects) =>
        {
            try
            {
                if (mvvmObjects)
                {
                    var tester = host.Services.GetRequiredService<MvvmObjectTester>();
                    await tester.TestAllMvvmObjectsAsync();
                }
                else
                {
                    Console.WriteLine("No test options specified. Use --mvvm-objects to test MVVM message objects.");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"? Test Error: {ex.Message}");
                Environment.Exit(1);
            }
        }, mvvmObjectsOption);
    }

    private static void AddExtractOptions(Command command)
    {
        // Add extract-specific options
        var inputOption = new Option<string>(
            "--in",
            "Input .catan file path")
        {
            IsRequired = true
        };

        var outputOption = new Option<string>(
            "--out",
            "Output JSON file path")
        {
            IsRequired = true
        };
        
        var actionsOption = new Option<string?>(
            "--actions",
            "Optional: JSON file with actions to create a .catan_test file");
        
        command.AddOption(inputOption);
        command.AddOption(outputOption);
        command.AddOption(actionsOption);

        command.SetHandler(async (input, output, actions) =>
        {
            try
            {
                if (!string.IsNullOrEmpty(actions))
                {
                    // Create a combined .catan_test file
                    await ExtractCommand.CreateTestFileAsync(input, actions, output);
                }
                else
                {
                    // Extract just the GameModel
                    await ExtractCommand.ExtractGameModelAsync(input, output);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Extract Error: {ex.Message}");
                Environment.Exit(1);
            }
        }, inputOption, outputOption, actionsOption);
    }

    private static IHost CreateHost()
    {
        return Host.CreateDefaultBuilder()
            .ConfigureServices((context, services) =>
            {
                services.AddSingleton<GameRunner>();
                services.AddSingleton<GameSessionManager>();
                services.AddSingleton<MvvmObjectTester>();
                services.AddLogging(builder =>
                {
                    builder.AddConsole();
                    builder.SetMinimumLevel(LogLevel.Information);
                });
            })
            .Build();
    }

    private static LogLevel ParseLogLevel(string logLevel)
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
}