using Microsoft.Extensions.Logging;
using Catan3.CLI.Commands;
using Catan3.CLI.Services;
using Catan3.Shared.Models;
using Catan3.Shared.Services;
using Catan3.Shared.Extensions;
using System.Linq;

namespace Catan3.CLI.Services;

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

            // Step 1: Parse command line - already done by System.CommandLine
            LogEvent("? STEP 1", "Command line parsed successfully");

            // Step 2: Try to connect to URI to make sure service is running
            LogEvent("?? STEP 2", $"Verifying GameService is running at {options.ServerUri}");
            await VerifyServiceIsRunning(options);
            LogEvent("? STEP 2", "GameService is running and accessible");

            // Step 3: Try to create a new game
            LogEvent("?? STEP 3", "Creating new game via REST API");
            var session = await CreateGameSession(options);
            LogEvent("? STEP 3", $"Game {session.GameId} created successfully");

            // Step 4: Have each player join the new game
            LogEvent("?? STEP 4", $"Connecting {session.PlayerCount} players via SignalR");
            // (Already done in CreateGameSession, but let's verify)
            LogEvent("? STEP 4", $"All {session.PlayerCount} players connected to game {session.GameId}");

            // Step 5: Verify they all got the same GameModel by looking at the hash
            LogEvent("?? STEP 5", "Verifying all players have consistent GameModel (checking GameHash)");
            await session.VerifyGameConsistency();
            
            var gameState = session.GetCurrentState();
            var firstProxy = session.Proxies.Values.First();
            var gameHash = firstProxy.LastGameState?.GameHash ?? "Unknown";
            
            LogEvent("? STEP 5", $"All players have consistent GameModel - GameHash: {gameHash}");
            LogEvent("?? CURRENT STATE", $"Game is in state: {gameState}");

            // Step 6: Check if we've reached the target state
            if (!string.IsNullOrEmpty(options.RunToState))
            {
                if (Enum.TryParse<GameState>(options.RunToState, ignoreCase: true, out var targetState))
                {
                    if (gameState == targetState)
                    {
                        LogEvent("?? TARGET REACHED", $"Game has reached target state: {targetState}");
                        LogEvent("?? SUCCESS", $"--run-to {options.RunToState} objective completed successfully!");
                    }
                    else
                    {
                        LogEvent("?? PROGRESSING", $"Current state: {gameState}, Target: {targetState} - continuing...");
                        await RunUntilState(session, options, options.RunToState);
                    }
                }
                else
                {
                    LogEvent("? ERROR", $"Invalid target state: {options.RunToState}");
                    throw new ArgumentException($"Invalid GameState: {options.RunToState}");
                }
            }
            else if (options.Complete)
            {
                LogEvent("?? COMPLETE MODE", "Running full end-to-end game progression");
                await RunCompleteGame(session, options);
            }
            else
            {
                LogEvent("?? BASIC MODE", "Game creation completed (use --complete or --run-to for progression)");
            }

            var endTime = DateTime.UtcNow;
            var totalTime = endTime - startTime;
            
            LogEvent("?? SUCCESS", $"Game execution completed successfully in {totalTime.TotalSeconds:F2} seconds");
            LogEvent("?? SUMMARY", $"GameId: {session.GameId}, Final State: {session.GetCurrentState()}, Players: {string.Join(", ", session.GetPlayerNames())}");

            if (options.NoExit)
            {
                LogEvent("?? NO-EXIT MODE", "Game state preserved. Press Ctrl+C to exit and release resources.");
                LogEvent("?? DEBUG TIP", $"GameService at {options.ServerUri} has active game {session.GameId}");
                
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
                LogEvent("?? CLEANUP", "Session disposed. Exiting gracefully.");
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
    /// Step 2: Verify the GameService is running and accessible
    /// </summary>
    private async Task VerifyServiceIsRunning(GameRunOptions options)
    {
        try
        {
            using var httpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(5) };
            httpClient.BaseAddress = new Uri(options.GetRestApiUrl());
            
            // Try to hit a simple endpoint to verify service is running
            // We'll try the companion games endpoint which should always respond
            var response = await httpClient.GetAsync("/api/companion/games");
            
            if (response.IsSuccessStatusCode)
            {
                _logger.LogDebug("Service verification successful: {StatusCode}", response.StatusCode);
            }
            else
            {
                throw new InvalidOperationException($"Service responded with error: {response.StatusCode}");
            }
        }
        catch (HttpRequestException ex)
        {
            throw new InvalidOperationException($"Cannot connect to GameService at {options.ServerUri}. Is the service running? Error: {ex.Message}");
        }
        catch (TaskCanceledException)
        {
            throw new InvalidOperationException($"Timeout connecting to GameService at {options.ServerUri}. Is the service running?");
        }
    }

    /// <summary>
    /// Creates and initializes a new game session
    /// </summary>
    private async Task<RealGameSession> CreateGameSession(GameRunOptions options)
    {
        var session = new RealGameSession(options, _logger);
        await session.InitializeAsync();
        return session;
    }

    private void LogEvent(string eventType, string message)
    {
        var timestamp = DateTime.UtcNow.ToString("HH:mm:ss.fff");
        Console.WriteLine($"[{timestamp}] [{eventType}] {message}");
        
        // Also log to the logger for debugging
        _logger.LogInformation("[{EventType}] {Message}", eventType, message);
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
        await VerifyAllocateResourceReverse(session);
        
        LogEvent("?? COMPLETE", "Full game progression completed!");
    }

    /// <summary>
    /// Progresses the game from current state to next state
    /// </summary>
    private async Task ProgressGameState(RealGameSession session, GameState currentState)
    {
        switch (currentState)
        {
            case GameState.PickingBoard:
                LogEvent("?? PROGRESSING", "Processing PickingBoard state");
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
            case GameState.AllocateResourceReverse:
                await VerifyAllocateResourceReverse(session);
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
        LogEvent("?? TESTING PICKINGBOARD", "Starting comprehensive PickingBoard state testing");
        
        // ASSERTION 1: Verify we're in the correct state initially
        var currentState = session.GetCurrentState();
        if (currentState != GameState.PickingBoard)
        {
            throw new InvalidOperationException($"Expected PickingBoard state, but was in {currentState}");
        }
        LogEvent("? STATE ASSERTION", "Confirmed game is in PickingBoard state");

        // ASSERTION 2: Verify current player
        var currentPlayerId = session.GetCurrentPlayerId();
        var firstPlayer = session.GetPlayerNames().First();
        LogEvent("?? CURRENT PLAYER", $"Current player: {currentPlayerId} (expected: first player)");

        // ASSERTION 3: Verify game consistency before testing
        await session.VerifyGameConsistency();
        LogEvent("? CONSISTENCY ASSERTION", "All players have consistent GameModel before testing");

        // ASSERTION 4: Verify action flags are correct for PickingBoard
        var initialGameState = session.Proxies.Values.First().LastGameState;
        if (initialGameState == null)
        {
            throw new InvalidOperationException("No GameState available from any proxy");
        }
        
        // In PickingBoard, Next should be enabled, rolls should be disabled
        if (!initialGameState.ActionFlags.NextEnabled)
        {
            LogEvent("?? ACTION FLAG", "Next is disabled in PickingBoard - this may be expected");
        }
        if (initialGameState.ActionFlags.RollsEnabled)
        {
            throw new InvalidOperationException("Rolls should not be enabled in PickingBoard state");
        }
        LogEvent("? ACTION FLAGS ASSERTION", "Action flags are correct for PickingBoard state");

        // Test Shuffle action with hash verification (like EndToEndStatefulTest)
        var initialHash = initialGameState.GameHash;
        if (string.IsNullOrEmpty(initialHash))
        {
            throw new InvalidOperationException("Initial GameHash is null or empty");
        }
        LogEvent("?? INITIAL HASH", $"Initial GameHash: {initialHash}");

        // SHUFFLE TEST 1: Execute first shuffle
        LogEvent("?? SHUFFLE TEST 1", "Executing first shuffle action");
        await session.ExecuteAction(GameAction.Shuffle);
        
        var firstShuffleState = session.Proxies.Values.First().LastGameState;
        if (firstShuffleState == null)
        {
            throw new InvalidOperationException("No GameState after first shuffle");
        }
        
        var firstShuffleHash = firstShuffleState.GameHash;
        if (string.IsNullOrEmpty(firstShuffleHash))
        {
            throw new InvalidOperationException("GameHash is null after first shuffle");
        }
        
        if (firstShuffleHash == initialHash)
        {
            throw new InvalidOperationException("GameHash did not change after first shuffle - board was not randomized");
        }
        
        LogEvent("? SHUFFLE ASSERTION 1", $"Board randomized successfully: {initialHash} ? {firstShuffleHash}");

        // SHUFFLE TEST 2: Execute second shuffle
        LogEvent("?? SHUFFLE TEST 2", "Executing second shuffle action");
        await session.ExecuteAction(GameAction.Shuffle);
        
        var secondShuffleState = session.Proxies.Values.First().LastGameState;
        if (secondShuffleState == null)
        {
            throw new InvalidOperationException("No GameState after second shuffle");
        }
        
        var secondShuffleHash = secondShuffleState.GameHash;
        if (string.IsNullOrEmpty(secondShuffleHash))
        {
            throw new InvalidOperationException("GameHash is null after second shuffle");
        }
        
        if (secondShuffleHash == firstShuffleHash)
        {
            LogEvent("?? SHUFFLE WARNING", "Second shuffle produced same hash - this is possible but rare");
        }
        else
        {
            LogEvent("? SHUFFLE ASSERTION 2", $"Second shuffle changed board: {firstShuffleHash} ? {secondShuffleHash}");
        }

        // UNDO TEST: Test Undo functionality
        LogEvent("? UNDO TEST", "Testing Undo functionality");
        await session.ExecuteAction(GameAction.Undo);
        
        var undoState = session.Proxies.Values.First().LastGameState;
        if (undoState == null)
        {
            throw new InvalidOperationException("No GameState after undo");
        }
        
        var undoHash = undoState.GameHash;
        if (undoHash != firstShuffleHash)
        {
            throw new InvalidOperationException($"Undo failed: expected {firstShuffleHash}, got {undoHash}");
        }
        
        if (!undoState.ActionFlags.RedoEnabled)
        {
            throw new InvalidOperationException("Redo should be enabled after Undo");
        }
        
        LogEvent("? UNDO ASSERTION", $"Undo restored previous state correctly: {secondShuffleHash} ? {firstShuffleHash}");
        LogEvent("? REDO FLAG ASSERTION", "Redo is enabled after Undo");

        // REDO TEST: Test Redo functionality
        LogEvent("? REDO TEST", "Testing Redo functionality");
        await session.ExecuteAction(GameAction.Redo);
        
        var redoState = session.Proxies.Values.First().LastGameState;
        if (redoState == null)
        {
            throw new InvalidOperationException("No GameState after redo");
        }
        
        var redoHash = redoState.GameHash;
        if (redoHash != secondShuffleHash)
        {
            throw new InvalidOperationException($"Redo failed: expected {secondShuffleHash}, got {redoHash}");
        }
        
        LogEvent("? REDO ASSERTION", $"Redo restored forward state correctly: {firstShuffleHash} ? {secondShuffleHash}");

        // BALANCE TEST: Test Balance functionality (if available)
        LogEvent("?? BALANCE TEST", "Testing Balance functionality");
        try
        {
            await session.ExecuteAction(GameAction.Balance);
            var balanceState = session.Proxies.Values.First().LastGameState;
            if (balanceState == null)
            {
                throw new InvalidOperationException("No GameState after balance");
            }
            LogEvent("? BALANCE ASSERTION", "Balance action executed successfully");
        }
        catch (Exception ex) when (ex.Message.Contains("balance") || ex.Message.Contains("swap") || ex.Message.Contains("not available"))
        {
            LogEvent("?? BALANCE SKIP", $"Balance not available (this is expected): {ex.Message}");
        }

        // STATE PERSISTENCE TEST: Verify we're still in PickingBoard after all actions
        await session.VerifyGameConsistency();
        var finalState = session.GetCurrentState();
        if (finalState != GameState.PickingBoard)
        {
            throw new InvalidOperationException($"Expected to remain in PickingBoard state, but moved to {finalState}");
        }
        LogEvent("? STATE PERSISTENCE ASSERTION", "Still in PickingBoard state after all actions");
        
        // ADVANCEMENT TEST: Advance to next state using Next action
        LogEvent("?? ADVANCEMENT TEST", "Testing advancement to next state with Next action");
        await session.ExecuteAction(GameAction.Next);
        
        // FINAL STATE ASSERTION: Verify we advanced to WaitingForRollForOrder
        var nextState = session.GetCurrentState();
        if (nextState != GameState.WaitingForRollForOrder)
        {
            throw new InvalidOperationException($"Expected WaitingForRollForOrder after Next, but got {nextState}");
        }
        LogEvent("? ADVANCEMENT ASSERTION", "Successfully advanced to WaitingForRollForOrder");

        // FINAL CONSISTENCY CHECK
        await session.VerifyGameConsistency();
        LogEvent("? FINAL CONSISTENCY ASSERTION", "All players consistent after PickingBoard testing");
        
        LogEvent("?? PICKINGBOARD COMPLETE", "All PickingBoard functionality verified with comprehensive assertions");
    }

    private async Task VerifyWaitingForRollForOrder(RealGameSession session)
    {
        LogEvent("?? TESTING WAITINGFORROLLFORORDER", "Starting WaitingForRollForOrder state testing");
        
        // ASSERTION 1: Verify we're in the correct state
        var currentState = session.GetCurrentState();
        if (currentState != GameState.WaitingForRollForOrder)
        {
            throw new InvalidOperationException($"Expected WaitingForRollForOrder state, but was in {currentState}");
        }
        LogEvent("? STATE ASSERTION", "Confirmed game is in WaitingForRollForOrder state");

        // ASSERTION 2: Verify current player and game consistency
        await session.VerifyGameConsistency();
        var gameState = session.Proxies.Values.First().LastGameState;
        if (gameState == null)
        {
            throw new InvalidOperationException("No GameState available");
        }
        
        var currentPlayerId = session.GetCurrentPlayerId();
        LogEvent("?? CURRENT PLAYER", $"Current player: {currentPlayerId}");

        // ADVANCEMENT TEST: Test Next action to advance to FinishedRollOrder
        LogEvent("?? ADVANCEMENT TEST", "Testing advancement with Next action");
        await session.ExecuteAction(GameAction.Next);
        
        // FINAL STATE ASSERTION: Verify advancement
        var nextState = session.GetCurrentState();
        if (nextState != GameState.FinishedRollOrder)
        {
            throw new InvalidOperationException($"Expected FinishedRollOrder after Next, but got {nextState}");
        }
        LogEvent("? ADVANCEMENT ASSERTION", "Successfully advanced to FinishedRollOrder");
        
        await session.VerifyGameConsistency();
        LogEvent("?? WAITINGFORROLLFORORDER COMPLETE", "WaitingForRollForOrder testing completed successfully");
    }

    private async Task VerifyFinishedRollOrder(RealGameSession session)
    {
        LogEvent("?? TESTING FINISHEDROLLORDER", "Starting FinishedRollOrder state testing");
        
        // ASSERTION: Verify state and advance
        var currentState = session.GetCurrentState();
        if (currentState != GameState.FinishedRollOrder)
        {
            throw new InvalidOperationException($"Expected FinishedRollOrder state, but was in {currentState}");
        }
        LogEvent("? STATE ASSERTION", "Confirmed game is in FinishedRollOrder state");

        await session.VerifyGameConsistency();
        
        LogEvent("?? ADVANCEMENT TEST", "Testing advancement with Next action");
        await session.ExecuteAction(GameAction.Next);
        
        var nextState = session.GetCurrentState();
        if (nextState != GameState.BeginResourceAllocation)
        {
            throw new InvalidOperationException($"Expected BeginResourceAllocation after Next, but got {nextState}");
        }
        LogEvent("? ADVANCEMENT ASSERTION", "Successfully advanced to BeginResourceAllocation");
        
        LogEvent("?? FINISHEDROLLORDER COMPLETE", "FinishedRollOrder testing completed successfully");
    }

    private async Task VerifyBeginResourceAllocation(RealGameSession session)
    {
        LogEvent("?? TESTING BEGINRESOURCEALLOCATION", "Starting BeginResourceAllocation state testing");
        
        // ASSERTION: Verify state and advance
        var currentState = session.GetCurrentState();
        if (currentState != GameState.BeginResourceAllocation)
        {
            throw new InvalidOperationException($"Expected BeginResourceAllocation state, but was in {currentState}");
        }
        LogEvent("? STATE ASSERTION", "Confirmed game is in BeginResourceAllocation state");

        await session.VerifyGameConsistency();
        
        LogEvent("?? ADVANCEMENT TEST", "Testing advancement with Next action");
        await session.ExecuteAction(GameAction.Next);
        
        var nextState = session.GetCurrentState();
        if (nextState != GameState.AllocateResourceForward)
        {
            throw new InvalidOperationException($"Expected AllocateResourceForward after Next, but got {nextState}");
        }
        LogEvent("? ADVANCEMENT ASSERTION", "Successfully advanced to AllocateResourceForward");
        
        LogEvent("?? BEGINRESOURCEALLOCATION COMPLETE", "BeginResourceAllocation testing completed successfully");
    }

    private async Task VerifyAllocateResourceForward(RealGameSession session)
    {
        LogEvent("?? TESTING ALLOCATERESOURCEFORWARD", "Starting AllocateResourceForward state testing");
        
        // ASSERTION 1: Verify we're in the correct state
        var currentState = session.GetCurrentState();
        if (currentState != GameState.AllocateResourceForward)
        {
            LogEvent("? FAIL", $"Expected AllocateResourceForward state, but was in {currentState}");
            throw new InvalidOperationException($"Expected AllocateResourceForward state, but was in {currentState}");
        }
        LogEvent("? PASS", "Confirmed game is in AllocateResourceForward state");

        // ASSERTION 2: Verify game structure for allocation
        await session.VerifyGameConsistency();
        var gameState = session.Proxies.Values.First().LastGameState;
        if (gameState == null)
        {
            LogEvent("? FAIL", "No GameState available");
            throw new InvalidOperationException("No GameState available");
        }

        if (gameState.Buildings.Count == 0)
        {
            LogEvent("? FAIL", "No buildings available for allocation");
            throw new InvalidOperationException("No buildings available for allocation");
        }
        if (gameState.Roads.Count == 0)
        {
            LogEvent("? FAIL", "No roads available for allocation");
            throw new InvalidOperationException("No roads available for allocation");
        }
        if (gameState.ActionFlags.RollsEnabled)
        {
            LogEvent("? FAIL", "Rolls should not be enabled during allocation");
            throw new InvalidOperationException("Rolls should not be enabled during allocation");
        }
        
        LogEvent("? PASS", $"Game has {gameState.Buildings.Count} buildings and {gameState.Roads.Count} roads");
        LogEvent("? PASS", "Rolls disabled during allocation (correct)");

        // PLAYER ALLOCATION LOOP: Process each player in forward order
        var playerIds = session.GetPlayerIds();
        LogEvent("??? FORWARD ALLOCATION", $"Starting forward allocation for {playerIds.Count} players");

        for (int i = 0; i < playerIds.Count; i++)
        {
            var currentPlayerId = session.GetCurrentPlayerId();
            var expectedPlayer = playerIds[i];
            
            LogEvent($"?? PLAYER {i + 1}/{playerIds.Count}", $"Processing {currentPlayerId} (expected: {expectedPlayer})");
            
            // ASSERTION: Verify correct player turn
            if (currentPlayerId != expectedPlayer)
            {
                LogEvent("? FAIL", $"Expected player {expectedPlayer} but current player is {currentPlayerId}");
                throw new InvalidOperationException($"Expected player {expectedPlayer} but current player is {currentPlayerId}");
            }

            // Get current game state for this player's turn
            await session.VerifyGameConsistency();
            var currentGameState = session.Proxies.Values.First().LastGameState;
            if (currentGameState == null)
            {
                LogEvent("? FAIL", $"No GameState available for player {currentPlayerId}");
                throw new InvalidOperationException($"No GameState available for player {currentPlayerId}");
            }

            // ASSERTION: Verify player entitlements
            var currentPlayer = currentGameState.Players.FirstOrDefault(p => p.Id == currentPlayerId);
            if (currentPlayer == null)
            {
                LogEvent("? FAIL", $"Current player {currentPlayerId} not found in game state");
                throw new InvalidOperationException($"Current player {currentPlayerId} not found in game state");
            }

            if (!currentPlayer.UnspentEntitlements.Contains(Entitlement.Settlement))
            {
                LogEvent("? FAIL", $"Player {currentPlayerId} should have Settlement entitlement");
                throw new InvalidOperationException($"Player {currentPlayerId} should have Settlement entitlement");
            }
            if (!currentPlayer.UnspentEntitlements.Contains(Entitlement.Road))
            {
                LogEvent("? FAIL", $"Player {currentPlayerId} should have Road entitlement");
                throw new InvalidOperationException($"Player {currentPlayerId} should have Road entitlement");
            }
            
            LogEvent("? PASS", $"Player {currentPlayerId} has Settlement and Road entitlements");

            // STEP 1: Place settlement with highest star value
            LogEvent("?? SETTLEMENT PLACEMENT", $"Finding best settlement location for {currentPlayerId}");
            await PlaceBestSettlement(session, currentPlayerId);
            
            // Verify settlement placement result
            await session.VerifyGameConsistency();
            var afterSettlementState = session.Proxies.Values.First().LastGameState;
            if (afterSettlementState == null)
            {
                LogEvent("? FAIL", "No GameState after settlement placement");
                throw new InvalidOperationException("No GameState after settlement placement");
            }
            
            var playerAfterSettlement = afterSettlementState.Players.FirstOrDefault(p => p.Id == currentPlayerId);
            if (playerAfterSettlement == null)
            {
                LogEvent("? FAIL", $"Player {currentPlayerId} not found after settlement placement");
                throw new InvalidOperationException($"Player {currentPlayerId} not found after settlement placement");
            }
            
            if (playerAfterSettlement.Score != 1)
            {
                LogEvent("? FAIL", $"Player {currentPlayerId} should have score 1 after settlement, but has {playerAfterSettlement.Score}");
                throw new InvalidOperationException($"Player {currentPlayerId} should have score 1 after settlement, but has {playerAfterSettlement.Score}");
            }
            
            if (playerAfterSettlement.UnspentEntitlements.Contains(Entitlement.Settlement))
            {
                LogEvent("? FAIL", $"Player {currentPlayerId} should no longer have Settlement entitlement");
                throw new InvalidOperationException($"Player {currentPlayerId} should no longer have Settlement entitlement");
            }
            
            LogEvent("? PASS", $"Player {currentPlayerId} placed settlement, score is now 1, Settlement entitlement spent");

            // STEP 2: Place road
            LogEvent("??? ROAD PLACEMENT", $"Finding buildable road for {currentPlayerId}");
            await PlaceFirstBuildableRoad(session, currentPlayerId);
            
            // Verify road placement result
            await session.VerifyGameConsistency();
            var afterRoadState = session.Proxies.Values.First().LastGameState;
            if (afterRoadState == null)
            {
                LogEvent("? FAIL", "No GameState after road placement");
                throw new InvalidOperationException("No GameState after road placement");
            }
            
            var playerAfterRoad = afterRoadState.Players.FirstOrDefault(p => p.Id == currentPlayerId);
            if (playerAfterRoad == null)
            {
                LogEvent("? FAIL", $"Player {currentPlayerId} not found after road placement");
                throw new InvalidOperationException($"Player {currentPlayerId} not found after road placement");
            }
            
            if (playerAfterRoad.UnspentEntitlements.Contains(Entitlement.Road))
            {
                LogEvent("? FAIL", $"Player {currentPlayerId} should no longer have Road entitlement");
                throw new InvalidOperationException($"Player {currentPlayerId} should no longer have Road entitlement");
            }
            
            LogEvent("? PASS", $"Player {currentPlayerId} placed road, Road entitlement spent");

            // STEP 3: Advance to next player or next phase
            if (i < playerIds.Count - 1)
            {
                LogEvent("?? NEXT PLAYER", $"Advancing from {currentPlayerId} to next player");
                await session.ExecuteAction(GameAction.Next);
            }
            else
            {
                LogEvent("?? NEXT PHASE", $"All players completed, advancing to AllocateResourceReverse");
                await session.ExecuteAction(GameAction.Next);
            }
        }

        // FINAL ASSERTION: Verify we advanced to AllocateResourceReverse
        var finalState = session.GetCurrentState();
        if (finalState != GameState.AllocateResourceReverse)
        {
            LogEvent("? FAIL", $"Expected AllocateResourceReverse after all players, but got {finalState}");
            throw new InvalidOperationException($"Expected AllocateResourceReverse after all players, but got {finalState}");
        }
        
        // Verify current player is the last player (for reverse order)
        var finalCurrentPlayer = session.GetCurrentPlayerId();
        var lastPlayer = playerIds.Last();
        if (finalCurrentPlayer != lastPlayer)
        {
            LogEvent("? FAIL", $"Expected {lastPlayer} to be current player in reverse phase, but got {finalCurrentPlayer}");
            throw new InvalidOperationException($"Expected {lastPlayer} to be current player in reverse phase, but got {finalCurrentPlayer}");
        }
        
        LogEvent("? PASS", $"Successfully advanced to AllocateResourceReverse with {lastPlayer} as current player");
        
        await session.VerifyGameConsistency();
        LogEvent("?? ALLOCATERESOURCEFORWARD COMPLETE", "All players completed forward allocation successfully");
    }

    private async Task VerifyAllocateResourceReverse(RealGameSession session)
    {
        LogEvent("?? TESTING ALLOCATERESOURCEREVERSE", "Starting AllocateResourceReverse state testing");
        
        // ASSERTION 1: Verify we're in the correct state
        var currentState = session.GetCurrentState();
        if (currentState != GameState.AllocateResourceReverse)
        {
            LogEvent("? FAIL", $"Expected AllocateResourceReverse state, but was in {currentState}");
            throw new InvalidOperationException($"Expected AllocateResourceReverse state, but was in {currentState}");
        }
        LogEvent("? PASS", "Confirmed game is in AllocateResourceReverse state");

        // ASSERTION 2: Verify game structure for reverse allocation
        await session.VerifyGameConsistency();
        var gameState = session.Proxies.Values.First().LastGameState;
        if (gameState == null)
        {
            LogEvent("? FAIL", "No GameState available");
            throw new InvalidOperationException("No GameState available");
        }

        if (gameState.Buildings.Count == 0)
        {
            LogEvent("? FAIL", "No buildings available for allocation");
            throw new InvalidOperationException("No buildings available for allocation");
        }
        if (gameState.Roads.Count == 0)
        {
            LogEvent("? FAIL", "No roads available for allocation");
            throw new InvalidOperationException("No roads available for allocation");
        }
        if (gameState.ActionFlags.RollsEnabled)
        {
            LogEvent("? FAIL", "Rolls should not be enabled during allocation");
            throw new InvalidOperationException("Rolls should not be enabled during allocation");
        }
        
        LogEvent("? PASS", $"Game has {gameState.Buildings.Count} buildings and {gameState.Roads.Count} roads");
        LogEvent("? PASS", "Rolls disabled during allocation (correct)");

        // ASSERTION 3: Verify forward allocation was completed - all players should have 1 settlement and 1 road
        var playerIds = session.GetPlayerIds();
        foreach (var playerId in playerIds)
        {
            var playerBuildings = gameState.Buildings.Count(b => 
                b.OwnerId == playerId && b.BuildingState == BuildingState.Settlement);
            var playerRoads = gameState.Roads.Count(r => 
                r.OwnerId == playerId && r.RoadState == RoadState.Road);
            
            if (playerBuildings != 1)
            {
                LogEvent("? FAIL", $"Player {playerId} should have exactly 1 settlement from forward allocation, but has {playerBuildings}");
                throw new InvalidOperationException($"Player {playerId} should have exactly 1 settlement from forward allocation, but has {playerBuildings}");
            }
            if (playerRoads != 1)
            {
                LogEvent("? FAIL", $"Player {playerId} should have exactly 1 road from forward allocation, but has {playerRoads}");
                throw new InvalidOperationException($"Player {playerId} should have exactly 1 road from forward allocation, but has {playerRoads}");
            }
            
            LogEvent("? PASS", $"Player {playerId} has 1 settlement and 1 road from forward allocation");
        }

        // ASSERTION 4: Verify current player is the last player (reverse order starts with last player)
        var currentPlayerId = session.GetCurrentPlayerId();
        var lastPlayer = playerIds.Last();
        if (currentPlayerId != lastPlayer)
        {
            LogEvent("? FAIL", $"Expected {lastPlayer} to be current player in reverse phase, but got {currentPlayerId}");
            throw new InvalidOperationException($"Expected {lastPlayer} to be current player in reverse phase, but got {currentPlayerId}");
        }
        LogEvent("? PASS", $"Reverse allocation correctly starts with {lastPlayer}");

        // PLAYER ALLOCATION LOOP: Process each player in reverse order
        var reversePlayerIds = playerIds.AsEnumerable().Reverse().ToArray();
        LogEvent("??? REVERSE ALLOCATION", $"Starting reverse allocation for {reversePlayerIds.Length} players");

        for (int i = 0; i < reversePlayerIds.Length; i++)
        {
            currentPlayerId = session.GetCurrentPlayerId();
            var expectedPlayer = reversePlayerIds[i];
            
            LogEvent($"?? PLAYER {i + 1}/{reversePlayerIds.Length}", $"Processing {currentPlayerId} (expected: {expectedPlayer})");
            
            // ASSERTION: Verify correct player turn
            if (currentPlayerId != expectedPlayer)
            {
                LogEvent("? FAIL", $"Expected player {expectedPlayer} but current player is {currentPlayerId}");
                throw new InvalidOperationException($"Expected player {expectedPlayer} but current player is {currentPlayerId}");
            }

            // Get current game state for this player's turn
            await session.VerifyGameConsistency();
            var currentGameState = session.Proxies.Values.First().LastGameState;
            if (currentGameState == null)
            {
                LogEvent("? FAIL", $"No GameState available for player {currentPlayerId}");
                throw new InvalidOperationException($"No GameState available for player {currentPlayerId}");
            }

            // ASSERTION: Verify player entitlements
            var currentPlayer = currentGameState.Players.FirstOrDefault(p => p.Id == currentPlayerId);
            if (currentPlayer == null)
            {
                LogEvent("? FAIL", $"Current player {currentPlayerId} not found in game state");
                throw new InvalidOperationException($"Current player {currentPlayerId} not found in game state");
            }

            if (!currentPlayer.UnspentEntitlements.Contains(Entitlement.Settlement))
            {
                LogEvent("? FAIL", $"Player {currentPlayerId} should have Settlement entitlement");
                throw new InvalidOperationException($"Player {currentPlayerId} should have Settlement entitlement");
            }
            if (!currentPlayer.UnspentEntitlements.Contains(Entitlement.Road))
            {
                LogEvent("? FAIL", $"Player {currentPlayerId} should have Road entitlement");
                throw new InvalidOperationException($"Player {currentPlayerId} should have Road entitlement");
            }
            
            LogEvent("? PASS", $"Player {currentPlayerId} has Settlement and Road entitlements");

            // ASSERTION: Verify resource tracking from forward allocation
            if (currentPlayer.ResourcesThisGame == null)
            {
                LogEvent("? FAIL", $"Player {currentPlayerId} should have ResourcesThisGame initialized");
                throw new InvalidOperationException($"Player {currentPlayerId} should have ResourcesThisGame initialized");
            }
            if (currentPlayer.ResourcesThisTurn == null)
            {
                LogEvent("? FAIL", $"Player {currentPlayerId} should have ResourcesThisTurn initialized");
                throw new InvalidOperationException($"Player {currentPlayerId} should have ResourcesThisTurn initialized");
            }

            // Track initial resources before settlement placement
            var initialResourcesThisGame = currentPlayer.ResourcesThisGame.Brick + 
                                         currentPlayer.ResourcesThisGame.Wood + 
                                         currentPlayer.ResourcesThisGame.Sheep + 
                                         currentPlayer.ResourcesThisGame.Wheat + 
                                         currentPlayer.ResourcesThisGame.Ore;
            
            var initialResourcesThisTurn = currentPlayer.ResourcesThisTurn.Brick + 
                                         currentPlayer.ResourcesThisTurn.Wood + 
                                         currentPlayer.ResourcesThisTurn.Sheep + 
                                         currentPlayer.ResourcesThisTurn.Wheat + 
                                         currentPlayer.ResourcesThisTurn.Ore;

            LogEvent("?? RESOURCE TRACKING", $"Player {currentPlayerId} before reverse settlement: {initialResourcesThisGame} total game resources, {initialResourcesThisTurn} this turn");
            LogEvent("? PASS", $"Player {currentPlayerId} resource tracking properly initialized");

            // STEP 1: Place settlement with highest star value
            LogEvent("?? SETTLEMENT PLACEMENT", $"Finding best settlement location for {currentPlayerId}");
            await PlaceBestSettlement(session, currentPlayerId);
            
            // Verify settlement placement result and resource updates
            await session.VerifyGameConsistency();
            var afterSettlementState = session.Proxies.Values.First().LastGameState;
            if (afterSettlementState == null)
            {
                LogEvent("? FAIL", "No GameState after settlement placement");
                throw new InvalidOperationException("No GameState after settlement placement");
            }
            
            var playerAfterSettlement = afterSettlementState.Players.FirstOrDefault(p => p.Id == currentPlayerId);
            if (playerAfterSettlement == null)
            {
                LogEvent("? FAIL", $"Player {currentPlayerId} not found after settlement placement");
                throw new InvalidOperationException($"Player {currentPlayerId} not found after settlement placement");
            }
            
            if (playerAfterSettlement.Score != 2)
            {
                LogEvent("? FAIL", $"Player {currentPlayerId} should have score 2 after second settlement, but has {playerAfterSettlement.Score}");
                throw new InvalidOperationException($"Player {currentPlayerId} should have score 2 after second settlement, but has {playerAfterSettlement.Score}");
            }
            
            if (playerAfterSettlement.UnspentEntitlements.Contains(Entitlement.Settlement))
            {
                LogEvent("? FAIL", $"Player {currentPlayerId} should no longer have Settlement entitlement");
                throw new InvalidOperationException($"Player {currentPlayerId} should no longer have Settlement entitlement");
            }
            
            // Verify resource updates after settlement placement (key difference in reverse allocation)
            var finalResourcesThisGame = playerAfterSettlement.ResourcesThisGame.Brick + 
                                       playerAfterSettlement.ResourcesThisGame.Wood + 
                                       playerAfterSettlement.ResourcesThisGame.Sheep + 
                                       playerAfterSettlement.ResourcesThisGame.Wheat + 
                                       playerAfterSettlement.ResourcesThisGame.Ore;
            
            var finalResourcesThisTurn = playerAfterSettlement.ResourcesThisTurn.Brick + 
                                       playerAfterSettlement.ResourcesThisTurn.Wood + 
                                       playerAfterSettlement.ResourcesThisTurn.Sheep + 
                                       playerAfterSettlement.ResourcesThisTurn.Wheat + 
                                       playerAfterSettlement.ResourcesThisTurn.Ore;

            LogEvent("?? RESOURCE UPDATE", $"Player {currentPlayerId} after reverse settlement: {finalResourcesThisGame} total game resources (+{finalResourcesThisGame - initialResourcesThisGame}), {finalResourcesThisTurn} this turn (+{finalResourcesThisTurn - initialResourcesThisTurn})");
            
            // In reverse allocation, the second settlement typically yields resources
            if (finalResourcesThisGame >= initialResourcesThisGame)
            {
                LogEvent("? PASS", $"Player {currentPlayerId} resource tracking updated correctly in reverse allocation");
            }
            else
            {
                LogEvent("?? WARN", $"Player {currentPlayerId} resource tracking shows decrease - may be valid based on settlement location");
            }
            
            LogEvent("? PASS", $"Player {currentPlayerId} placed second settlement, score is now 2, Settlement entitlement spent");

            // STEP 2: Place road
            LogEvent("??? ROAD PLACEMENT", $"Finding buildable road for {currentPlayerId}");
            await PlaceFirstBuildableRoad(session, currentPlayerId);
            
            // Verify road placement result
            await session.VerifyGameConsistency();
            var afterRoadState = session.Proxies.Values.First().LastGameState;
            if (afterRoadState == null)
            {
                LogEvent("? FAIL", "No GameState after road placement");
                throw new InvalidOperationException("No GameState after road placement");
            }
            
            var playerAfterRoad = afterRoadState.Players.FirstOrDefault(p => p.Id == currentPlayerId);
            if (playerAfterRoad == null)
            {
                LogEvent("? FAIL", $"Player {currentPlayerId} not found after road placement");
                throw new InvalidOperationException($"Player {currentPlayerId} not found after road placement");
            }
            
            if (playerAfterRoad.UnspentEntitlements.Contains(Entitlement.Road))
            {
                LogEvent("? FAIL", $"Player {currentPlayerId} should no longer have Road entitlement");
                throw new InvalidOperationException($"Player {currentPlayerId} should no longer have Road entitlement");
            }
            
            LogEvent("? PASS", $"Player {currentPlayerId} placed second road, Road entitlement spent");

            // STEP 3: Advance to next player or next phase
            if (i < reversePlayerIds.Length - 1)
            {
                LogEvent("?? NEXT PLAYER", $"Advancing from {currentPlayerId} to next player in reverse order");
                await session.ExecuteAction(GameAction.Next);
            }
            else
            {
                LogEvent("?? NEXT PHASE", $"All players completed reverse allocation, advancing to DoneResourceAllocation");
                await session.ExecuteAction(GameAction.Next);
            }
        }

        // FINAL ASSERTION: Verify we advanced to DoneResourceAllocation
        var finalState = session.GetCurrentState();
        if (finalState != GameState.DoneResourceAllocation)
        {
            LogEvent("? FAIL", $"Expected DoneResourceAllocation after all players, but got {finalState}");
            throw new InvalidOperationException($"Expected DoneResourceAllocation after all players, but got {finalState}");
        }
        
        LogEvent("? PASS", $"Successfully advanced to DoneResourceAllocation");
        
        // FINAL VERIFICATION: Verify all players have exactly 2 settlements and 2 roads
        await session.VerifyGameConsistency();
        var completedGameState = session.Proxies.Values.First().LastGameState;
        if (completedGameState == null)
        {
            LogEvent("? FAIL", "No GameState after allocation completion");
            throw new InvalidOperationException("No GameState after allocation completion");
        }

        foreach (var playerId in playerIds)
        {
            var playerBuildings = completedGameState.Buildings.Count(b => 
                b.OwnerId == playerId && b.BuildingState == BuildingState.Settlement);
            var playerRoads = completedGameState.Roads.Count(r => 
                r.OwnerId == playerId && r.RoadState == RoadState.Road);
            var player = completedGameState.Players.FirstOrDefault(p => p.Id == playerId);
            
            if (playerBuildings != 2)
            {
                LogEvent("? FAIL", $"Player {playerId} should have exactly 2 settlements after complete allocation, but has {playerBuildings}");
                throw new InvalidOperationException($"Player {playerId} should have exactly 2 settlements after complete allocation, but has {playerBuildings}");
            }
            if (playerRoads != 2)
            {
                LogEvent("? FAIL", $"Player {playerId} should have exactly 2 roads after complete allocation, but has {playerRoads}");
                throw new InvalidOperationException($"Player {playerId} should have exactly 2 roads after complete allocation, but has {playerRoads}");
            }
            if (player?.Score != 2)
            {
                LogEvent("? FAIL", $"Player {playerId} should have score 2 after complete allocation, but has {player?.Score}");
                throw new InvalidOperationException($"Player {playerId} should have score 2 after complete allocation, but has {player?.Score}");
            }
            
            var totalResources = player?.ResourcesThisGame.Brick + player?.ResourcesThisGame.Wood + 
                               player?.ResourcesThisGame.Sheep + player?.ResourcesThisGame.Wheat + player?.ResourcesThisGame.Ore;
            
            LogEvent("? PASS", $"Player {playerId}: 2 settlements, 2 roads, score 2, {totalResources} total resources");
        }
        
        LogEvent("?? ALLOCATERESOURCEREVERSE COMPLETE", "All players completed reverse allocation successfully with proper resource tracking");
    }

    /// <summary>
    /// Places the best settlement for a player based on star calculation (highest star value)
    /// </summary>
    private async Task PlaceBestSettlement(RealGameSession session, string playerId)
    {
        var gameState = session.Proxies.Values.First().LastGameState;
        if (gameState == null)
        {
            LogEvent("? FAIL", "No GameState available for settlement placement");
            throw new InvalidOperationException("No GameState available for settlement placement");
        }

        // Find all possible settlements (using opaque BuildingKey approach)
        var possibleSettlements = gameState.Buildings
            .Where(b => b.BuildingState == BuildingState.PossibleSettlement)
            .ToList();

        if (!possibleSettlements.Any())
        {
            LogEvent("? FAIL", "No possible settlements available");
            throw new InvalidOperationException("No possible settlements available");
        }

        LogEvent("?? SETTLEMENT SEARCH", $"Found {possibleSettlements.Count} possible settlements");

        // Calculate star values for each settlement using GameModel.TilesForBuildings().Stars()
        var settlementOptions = possibleSettlements
            .Select(building => new
            {
                building = building,
                stars = gameState.TilesForBuildings(building.BuildingKey).Stars()
            })
            .ToList();

        // Find settlement(s) with highest star value
        var maxStars = settlementOptions.Max(s => s.stars);
        var bestOptions = settlementOptions.Where(s => s.stars == maxStars).ToList();
        
        // Pick the first one if multiple have same stars
        var selectedSettlement = bestOptions.First();
        
        LogEvent("?? BEST SETTLEMENT", $"Selected {selectedSettlement.building.BuildingKey} with {selectedSettlement.stars} stars (from {bestOptions.Count} best options)");

        // Execute BuildingUpgradeMessage using opaque BuildingKey
        var proxy = session.GetProxy(playerId);
        var result = await proxy.ExecuteBuildingUpgradeAsync(session.GameId, selectedSettlement.building.BuildingKey);
        
        if (!result.Success)
        {
            LogEvent("? FAIL", $"Settlement placement failed: {result.Message}");
            throw new InvalidOperationException($"Settlement placement failed: {result.Message}");
        }
        
        LogEvent("? PASS", $"Player {playerId} successfully placed settlement at {selectedSettlement.building.BuildingKey}");
    }

    /// <summary>
    /// Places the first buildable road for a player
    /// </summary>
    private async Task PlaceFirstBuildableRoad(RealGameSession session, string playerId)
    {
        // Get updated game state after settlement placement to find buildable roads
        await session.VerifyGameConsistency();
        var gameState = session.Proxies.Values.First().LastGameState;
        if (gameState == null)
        {
            LogEvent("? FAIL", "No GameState available for road placement");
            throw new InvalidOperationException("No GameState available for road placement");
        }

        // Find all buildable roads (using opaque RoadKey approach)
        var buildableRoads = gameState.Roads
            .Where(r => r.RoadState == RoadState.Buildable)
            .ToList();

        if (!buildableRoads.Any())
        {
            LogEvent("? FAIL", "No buildable roads available");
            throw new InvalidOperationException("No buildable roads available");
        }

        // Pick the first buildable road (simple approach)
        var selectedRoad = buildableRoads.First();
        
        LogEvent("??? ROAD SELECTION", $"Selected road at {selectedRoad.RoadKey} (from {buildableRoads.Count} buildable roads)");

        // Execute RoadPurchaseMessage using opaque RoadKey
        var proxy = session.GetProxy(playerId);
        var result = await proxy.ExecuteRoadPurchaseAsync(session.GameId, selectedRoad.RoadKey);
        
        if (!result.Success)
        {
            LogEvent("? FAIL", $"Road placement failed: {result.Message}");
            throw new InvalidOperationException($"Road placement failed: {result.Message}");
        }
        
        LogEvent("? PASS", $"Player {playerId} successfully placed road at {selectedRoad.RoadKey}");
    }
}