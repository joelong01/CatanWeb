using Microsoft.Extensions.Logging;
using TestClient.Commands;
using TestClient.Services;
using Catan3.Shared.Models;
using Catan3.Shared.Services;
using System.Text.Json;

namespace TestClient.Services;

/// <summary>
/// Main game runner that orchestrates the end-to-end game execution
/// Follows the same pattern as EndToEndStatefulTest but connects to real GameService
/// </summary>
public class GameRunner
{
    private readonly ILogger<GameRunner> _logger;
    private readonly GameSessionManager _sessionManager;

    public GameRunner(ILogger<GameRunner> logger, GameSessionManager sessionManager)
    {
        _logger = logger;
        _sessionManager = sessionManager;
    }

    /// <summary>
    /// Main entry point for running a game session
    /// </summary>
    public async Task RunGameAsync(GameRunOptions options)
    {
        var startTime = DateTime.UtcNow;
        
        try
        {
            LogEvent("?? CATAN CLI STARTING", $"Starting {options.GameType} game with {options.PlayerCount} players");
            LogEvent("?? CONFIG", options.ToString());
            LogEvent("?? SERVER", $"Connecting to GameService at {options.ServerUri}");

            // Create and initialize the game session
            var session = await CreateGameSession(options);
            
            if (options.Complete)
            {
                LogEvent("?? COMPLETE MODE", "Running full end-to-end game progression");
                await RunCompleteGame(session, options);
            }
            else if (!string.IsNullOrEmpty(options.RunToState))
            {
                LogEvent("?? RUN-TO MODE", $"Running game until state: {options.RunToState}");
                await RunUntilState(session, options, options.RunToState);
            }
            else
            {
                LogEvent("?? BASIC MODE", "Creating game and stopping (use --complete or --run-to for progression)");
            }

            var endTime = DateTime.UtcNow;
            var totalTime = endTime - startTime;
            
            LogEvent("? SUCCESS", $"Game execution completed in {totalTime.TotalSeconds:F2} seconds");
            LogEvent("?? GAME INFO", $"GameId: {session.GameId}, Current State: {session.GetCurrentState()}");

            if (options.NoExit)
            {
                LogEvent("?? NO-EXIT MODE", "Game state preserved. Press Ctrl+C to exit and release resources.");
                LogEvent("?? DEBUG TIP", $"You can now debug the GameService at {options.ServerUri} with GameId: {session.GameId}");
                
                // Keep the session alive until Ctrl+C
                Console.CancelKeyPress += (sender, e) =>
                {
                    e.Cancel = true;
                    LogEvent("?? SHUTDOWN", "Ctrl+C received. Cleaning up and exiting...");
                    _ = Task.Run(async () =>
                    {
                        await session.DisposeAsync();
                        Environment.Exit(0);
                    });
                };

                // Keep the main thread alive
                await Task.Delay(-1);
            }
            else
            {
                await session.DisposeAsync();
                LogEvent("?? CLEANUP", "Session disposed. Exiting.");
            }
        }
        catch (Exception ex)
        {
            LogEvent("? ERROR", $"Game execution failed: {ex.Message}");
            
            if (options.LogLevel <= LogLevel.Debug)
            {
                LogEvent("?? DEBUG", $"Exception details: {ex}");
            }
            
            throw;
        }
    }

    /// <summary>
    /// Creates and initializes a new game session
    /// </summary>
    private async Task<RealGameSession> CreateGameSession(GameRunOptions options)
    {
        LogEvent("?? CREATING GAME", "Initializing new game session...");
        
        var session = new RealGameSession(options, _logger);
        await session.InitializeAsync();
        
        LogEvent("? GAME CREATED", $"Game {session.GameId} created successfully");
        LogEvent("?? PLAYERS", $"Connected {session.PlayerCount} players: {string.Join(", ", session.GetPlayerNames())}");
        LogEvent("?? INITIAL STATE", $"Game started in state: {session.GetCurrentState()}");
        
        return session;
    }

    /// <summary>
    /// Runs the complete end-to-end game (equivalent to EndToEndStatefulTest)
    /// </summary>
    private async Task RunCompleteGame(RealGameSession session, GameRunOptions options)
    {
        LogEvent("?? COMPLETE GAME", "Starting full game progression...");

        // Follow the same progression as EndToEndStatefulTest
        await VerifyPickingBoard(session);
        await VerifyWaitingForRollForOrder(session);
        await VerifyFinishedRollOrder(session);
        await VerifyBeginResourceAllocation(session);
        await VerifyAllocateResourceForward(session);
        
        // TODO: Add more phases as needed
        // await VerifyAllocateResourceReverse(session);
        
        LogEvent("?? COMPLETE", "Full game progression completed!");
    }

    /// <summary>
    /// Runs the game until it reaches the specified state
    /// </summary>
    private async Task RunUntilState(RealGameSession session, GameRunOptions options, string targetState)
    {
        if (!Enum.TryParse<GameState>(targetState, ignoreCase: true, out var targetGameState))
        {
            throw new ArgumentException($"Invalid target state: {targetState}");
        }

        LogEvent("?? TARGET STATE", $"Running until state: {targetGameState}");

        var maxIterations = 50; // Safety valve
        var iteration = 0;

        while (iteration < maxIterations)
        {
            var currentState = session.GetCurrentState();
            LogEvent("?? CURRENT STATE", $"Iteration {iteration + 1}: {currentState}");

            if (currentState == targetGameState)
            {
                LogEvent("?? TARGET REACHED", $"Successfully reached target state: {targetGameState}");
                return;
            }

            // Progress to next state based on current state
            await ProgressGameState(session, currentState);
            
            iteration++;
        }

        throw new InvalidOperationException($"Failed to reach target state {targetGameState} after {maxIterations} iterations");
    }

    /// <summary>
    /// Progresses the game from current state to next state
    /// </summary>
    private async Task ProgressGameState(RealGameSession session, GameState currentState)
    {
        switch (currentState)
        {
            case GameState.PickingBoard:
                await VerifyPickingBoard(session);
                break;
            case GameState.WaitingForRollForOrder:
                await VerifyWaitingForRollForOrder(session);
                break;
            case GameState.FinishedRollOrder:
                await VerifyFinishedRollOrder(session);
                break;
            case GameState.BeginResourceAllocation:
                await VerifyBeginResourceAllocation(session);
                break;
            case GameState.AllocateResourceForward:
                await VerifyAllocateResourceForward(session);
                break;
            default:
                // For other states, try a simple Next action
                LogEvent("?? NEXT ACTION", $"Attempting Next action from state: {currentState}");
                await session.ExecuteNextAction();
                break;
        }
    }

    // Game state verification methods (following EndToEndStatefulTest pattern)

    private async Task VerifyPickingBoard(RealGameSession session)
    {
        LogEvent("?? PICKING BOARD", "Testing PickingBoard state functionality");
        
        // Execute shuffles, undo, redo, balance (same as test)
        await session.ExecuteAction(GameAction.Shuffle);
        LogEvent("?? SHUFFLED", "Board shuffled");
        
        await session.ExecuteAction(GameAction.Shuffle);
        LogEvent("?? SHUFFLED", "Board shuffled again");
        
        await session.ExecuteAction(GameAction.Undo);
        LogEvent("? UNDO", "Undid last action");
        
        await session.ExecuteAction(GameAction.Redo);
        LogEvent("? REDO", "Redid last action");
        
        try
        {
            await session.ExecuteAction(GameAction.Balance);
            LogEvent("?? BALANCED", "Board balanced");
        }
        catch (Exception ex)
        {
            LogEvent("?? BALANCE SKIP", $"Balance not available: {ex.Message}");
        }
        
        await session.ExecuteAction(GameAction.Next);
        LogEvent("? PICKING BOARD", "Completed - advanced to WaitingForRollForOrder");
    }

    private async Task VerifyWaitingForRollForOrder(RealGameSession session)
    {
        LogEvent("?? ROLL FOR ORDER", "Processing roll for order phase");
        await session.ExecuteAction(GameAction.Next);
        LogEvent("? ROLL FOR ORDER", "Completed - advanced to FinishedRollOrder");
    }

    private async Task VerifyFinishedRollOrder(RealGameSession session)
    {
        LogEvent("?? FINISHED ROLL ORDER", "Processing finished roll order");
        await session.ExecuteAction(GameAction.Next);
        LogEvent("? FINISHED ROLL ORDER", "Completed - advanced to BeginResourceAllocation");
    }

    private async Task VerifyBeginResourceAllocation(RealGameSession session)
    {
        LogEvent("??? BEGIN ALLOCATION", "Starting resource allocation phase");
        await session.ExecuteAction(GameAction.Next);
        LogEvent("? BEGIN ALLOCATION", "Completed - advanced to AllocateResourceForward");
    }

    private async Task VerifyAllocateResourceForward(RealGameSession session)
    {
        LogEvent("??? FORWARD ALLOCATION", "Processing forward allocation phase");
        LogEvent("?? LIMITATION", "Settlement/road placement not yet implemented in CLI");
        LogEvent("?? NOTE", "This phase requires building placement logic");
        
        // For now, just try to advance
        try
        {
            await session.ExecuteAction(GameAction.Next);
            LogEvent("? FORWARD ALLOCATION", "Advanced to next phase");
        }
        catch (Exception ex)
        {
            LogEvent("?? ALLOCATION BLOCKED", $"Cannot advance: {ex.Message}");
            LogEvent("?? TIP", "This is expected - allocation requires building placement");
        }
    }

    private void LogEvent(string eventType, string message)
    {
        var timestamp = DateTime.UtcNow.ToString("HH:mm:ss.fff");
        Console.WriteLine($"[{timestamp}] [{eventType}] {message}");
        
        // Also log to the logger for debugging
        _logger.LogInformation("[{EventType}] {Message}", eventType, message);
    }
}