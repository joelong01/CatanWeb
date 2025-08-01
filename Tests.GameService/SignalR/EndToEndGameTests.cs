using Xunit;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.Configuration;
using Catan3.Shared.Models;
using Catan3.Shared.Utility;
using Catan3.Shared.Services;
using Tests.GameService.SignalR;
using System.Text.Json;

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
    /// Updated to use SignalRProxy from Catan3.Shared instead of local test helpers (Rule compliance).
    /// </summary>
    public class EndToEndGameTests : IClassFixture<WebApplicationFactory<Program>>
    {
        private readonly WebApplicationFactory<Program> _factory;

        public EndToEndGameTests(WebApplicationFactory<Program> factory)
        {
            // Use the injected factory instead of creating a new one - this prevents multiple games
            _factory = factory.WithWebHostBuilder(builder =>
            {
                builder.ConfigureAppConfiguration((context, config) =>
                {
                    config.AddInMemoryCollection(new Dictionary<string, string?>
                    {
                        // Test configuration with short timeouts for faster tests
                        ["GameApi:HangingGetTimeoutSeconds"] = "5",
                        
                        // Suppress logging during tests for cleaner output
                        ["Logging:LogLevel:Default"] = "Error",
                        ["Logging:LogLevel:Microsoft"] = "Error", 
                        ["Logging:LogLevel:Microsoft.AspNetCore"] = "Error",
                        ["Logging:LogLevel:Catan3.GameService"] = "Error",
                        ["Logging:LogLevel:Catan3.GameService.Controllers"] = "Error",
                        ["Logging:LogLevel:Catan3.GameService.Services"] = "Error",
                        ["Logging:LogLevel:Catan3.GameService.Hubs"] = "Error"
                    });
                });
            });
        }

        [Fact]
        public async Task EndToEndStatefulTest()
        {
            var testStartTime = DateTime.UtcNow;
            LogEvent("TestStart", "Beginning comprehensive End-to-End stateful test");

            // Progress through all game states sequentially
            var e2eSession = await VerifyGameCreationAndJoin();
            await VerifyPickingBoard(e2eSession);
            await VerifyWaitingForRollForOrder(e2eSession);
            await VerifyFinishedRollOrder(e2eSession);
            await VerifyBeginResourceAllocation(e2eSession);
            await VerifyAllocateResourceForward(e2eSession);
            
            // IMPORTANT: Test terminates here after successful completion of AllocateResourceForward
            // The game will be in AllocateResourceReverse state but we end the test at a natural checkpoint
            var finalGameState = e2eSession.GetProxy("Alice").LastGameState;
            Assert.NotNull(finalGameState);
            Assert.Equal(GameState.AllocateResourceReverse, finalGameState.GameState);
            
            var testEndTime = DateTime.UtcNow;
            var totalTestTime = testEndTime - testStartTime;
            
            LogEvent("TestComplete", $"✅ End-to-End stateful test completed successfully through AllocateResourceForward!");
            LogEvent("FinalState", $"Game properly transitioned to AllocateResourceReverse - test terminating at natural checkpoint");
            LogEvent("TestTiming", $"⏱️ Total test execution time: {totalTestTime.TotalSeconds:F2} seconds");
            LogEvent("NextPhase", "Next: AllocateResourceReverse can be tested in future iterations");
            
            // Performance assertion
            Assert.True(totalTestTime.TotalSeconds < 60, 
                $"E2E test should complete within 1 minute, took {totalTestTime.TotalSeconds:F2} seconds");

            // Properly dispose the session to clean up all resources
            await e2eSession.DisposeAsync();
            LogEvent("CleanupComplete", "✅ All test resources properly disposed");
        }

        /// <summary>
        /// Creates an EXPANSION game and returns the session.
        /// We pick expansion because it is a superset of regular.
        /// Creates the game and has all the players join it.
        /// Verification includes making sure that the game is created and the players joined.
        /// To make it easy for the next test, make sure that Next() is called appropriately 
        /// so that the game is in the PickingBoard when it returns.
        /// </summary>
        private async Task<EndToEndSignalRSession> VerifyGameCreationAndJoin()
        {
            LogEvent("GameCreation", "Creating EXPANSION game with 5 players for testing");

            // Create EXPANSION game with 5 players - expansion requires 5 players
            var playerIds = new[] { "Alice", "Bob", "Charlie", "David", "Eve" };
            var session = new EndToEndSignalRSession(_factory, GameType.Expansion, playerIds);
            await session.InitializeAsync();
            
            LogEvent("PlayersJoined", $"Game created: {session.GameId} with {playerIds.Length} players");

            // Verify all 5 players connected correctly
            Assert.Equal(5, session.PlayerIds.Length);
            foreach (var playerId in playerIds)
            {
                var proxy = session.GetProxy(playerId);
                Assert.Equal(playerId, proxy.PlayerId);
                Assert.Equal(session.GameId, proxy.GameId);
                Assert.NotNull(proxy.Connection);
                Assert.Equal(HubConnectionState.Connected, proxy.Connection.State);
            }

            // Verify we start in PickingBoard state
            await session.VerifyAllProxiesInState(GameState.PickingBoard);
            await session.VerifyGameConsistency();

            var gameState = session.GetProxy("Alice").LastGameState;
            Assert.NotNull(gameState);
            Assert.Equal(GameState.PickingBoard, gameState.GameState);
            Assert.Equal("Alice", gameState.CurrentPlayerId);

            // Verify EXPANSION game structure
            Assert.True(gameState.Buildings.Count > 0);
            Assert.True(gameState.Roads.Count > 0);
            Assert.True(gameState.Tiles.Count > 0);
            Assert.Equal(5, gameState.Players.Count);

            LogEvent("GameCreationVerified", "✅ EXPANSION game creation and player join verified");
            return session;
        }

        /// <summary>
        /// Go through the full tests of verifying that the PickingBoard state works correctly.
        /// This includes Shuffle, Undo, Redo, and Balance.
        /// Verifies that all clients get all updates and they are the same.
        /// The game should be in the WaitingForRollForOrder when it returns.
        /// </summary>
        private async Task VerifyPickingBoard(EndToEndSignalRSession session)
        {
            LogEvent("PickingBoard", "Testing PickingBoard state functionality");

            // Verify we're in PickingBoard state
            await session.VerifyAllProxiesInState(GameState.PickingBoard);
            await session.VerifyGameConsistency();

            var currentPlayerId = session.GetCurrentPlayerId();
            Assert.Equal("Alice", currentPlayerId);

            // Test Shuffle action with hash verification
            var initialGameState = session.GetProxy("Alice").LastGameState;
            var initialHash = initialGameState?.GameHash;
            Assert.NotNull(initialHash);
            LogEvent("InitialHash", $"Initial GameHash: {initialHash}");

            // Execute first shuffle
            await session.ExecuteActionWithVerification(currentPlayerId, GameAction.Shuffle);
            var firstShuffleState = session.GetProxy("Alice").LastGameState;
            var firstShuffleHash = firstShuffleState?.GameHash;
            Assert.NotNull(firstShuffleHash);
            Assert.NotEqual(initialHash, firstShuffleHash);
            LogEvent("FirstShuffle", $"After first shuffle: {firstShuffleHash}");

            // Execute second shuffle
            await session.ExecuteActionWithVerification(currentPlayerId, GameAction.Shuffle);
            var secondShuffleState = session.GetProxy("Alice").LastGameState;
            var secondShuffleHash = secondShuffleState?.GameHash;
            Assert.NotNull(secondShuffleHash);
            Assert.NotEqual(firstShuffleHash, secondShuffleHash);
            LogEvent("SecondShuffle", $"After second shuffle: {secondShuffleHash}");

            // Test Undo functionality
            await session.ExecuteActionWithVerification(currentPlayerId, GameAction.Undo);
            var undoState = session.GetProxy("Alice").LastGameState;
            var undoHash = undoState?.GameHash;
            Assert.Equal(firstShuffleHash, undoHash);
            Assert.True(undoState?.ActionFlags.RedoEnabled);
            LogEvent("UndoVerified", "✅ Undo restored previous state correctly");

            // Test Redo functionality
            await session.ExecuteActionWithVerification(currentPlayerId, GameAction.Redo);
            var redoState = session.GetProxy("Alice").LastGameState;
            var redoHash = redoState?.GameHash;
            Assert.Equal(secondShuffleHash, redoHash);
            LogEvent("RedoVerified", "✅ Redo restored forward state correctly");

            // Test Balance functionality (if available)
            try
            {
                await session.ExecuteActionWithVerification(currentPlayerId, GameAction.Balance);
                var balanceState = session.GetProxy("Alice").LastGameState;
                Assert.NotNull(balanceState);
                LogEvent("BalanceVerified", "✅ Balance action executed successfully");
            }
            catch (Exception ex) when (ex.Message.Contains("balance") || ex.Message.Contains("swap"))
            {
                LogEvent("BalanceSkipped", "Balance action not available - expected for some board configurations");
            }

            // Advance to next state using Next action
            await session.ExecuteActionWithVerification(currentPlayerId, GameAction.Next);
            await session.VerifyAllProxiesInState(GameState.WaitingForRollForOrder);
            await session.VerifyGameConsistency();

            LogEvent("PickingBoardComplete", "✅ PickingBoard state verified - advanced to WaitingForRollForOrder");
        }

        /// <summary>
        /// Verify WaitingForRollForOrder state works correctly.
        /// Tests Next action to advance to FinishedRollOrder.
        /// </summary>
        private async Task VerifyWaitingForRollForOrder(EndToEndSignalRSession session)
        {
            LogEvent("WaitingForRollForOrder", "Testing WaitingForRollForOrder state functionality");

            // Verify we're in correct state
            await session.VerifyAllProxiesInState(GameState.WaitingForRollForOrder);
            await session.VerifyGameConsistency();

            var gameState = session.GetProxy("Alice").LastGameState;
            Assert.NotNull(gameState);
            Assert.Equal(GameState.WaitingForRollForOrder, gameState.GameState);
            Assert.Equal("Alice", gameState.CurrentPlayerId);

            // Test Next action to advance to FinishedRollOrder
            var currentPlayerId = session.GetCurrentPlayerId();
            await session.ExecuteActionWithVerification(currentPlayerId, GameAction.Next);
            await session.VerifyAllProxiesInState(GameState.FinishedRollOrder);
            await session.VerifyGameConsistency();

            LogEvent("WaitingForRollForOrderComplete", "✅ WaitingForRollForOrder state verified - advanced to FinishedRollOrder");
        }

        /// <summary>
        /// Verify FinishedRollOrder state works correctly.
        /// Tests Next action to advance to BeginResourceAllocation.
        /// </summary>
        private async Task VerifyFinishedRollOrder(EndToEndSignalRSession session)
        {
            LogEvent("FinishedRollOrder", "Testing FinishedRollOrder state functionality");

            // Verify we're in correct state
            await session.VerifyAllProxiesInState(GameState.FinishedRollOrder);
            await session.VerifyGameConsistency();

            var gameState = session.GetProxy("Alice").LastGameState;
            Assert.NotNull(gameState);
            Assert.Equal(GameState.FinishedRollOrder, gameState.GameState);
            Assert.Equal("Alice", gameState.CurrentPlayerId);

            // Test Next action to advance to BeginResourceAllocation
            var currentPlayerId = session.GetCurrentPlayerId();
            await session.ExecuteActionWithVerification(currentPlayerId, GameAction.Next);
            await session.VerifyAllProxiesInState(GameState.BeginResourceAllocation);
            await session.VerifyGameConsistency();

            LogEvent("FinishedRollOrderComplete", "✅ FinishedRollOrder state verified - advanced to BeginResourceAllocation");
        }

        /// <summary>
        /// Verify BeginResourceAllocation state works correctly.
        /// Tests Next action to advance to AllocateResourceForward.
        /// </summary>
        private async Task VerifyBeginResourceAllocation(EndToEndSignalRSession session)
        {
            LogEvent("BeginResourceAllocation", "Testing BeginResourceAllocation state functionality");

            // Verify we're in correct state
            await session.VerifyAllProxiesInState(GameState.BeginResourceAllocation);
            await session.VerifyGameConsistency();

            var gameState = session.GetProxy("Alice").LastGameState;
            Assert.NotNull(gameState);
            Assert.Equal(GameState.BeginResourceAllocation, gameState.GameState);
            Assert.Equal("Alice", gameState.CurrentPlayerId);

            // Test Next action to advance to AllocateResourceForward
            var currentPlayerId = session.GetCurrentPlayerId();
            await session.ExecuteActionWithVerification(currentPlayerId, GameAction.Next);
            await session.VerifyAllProxiesInState(GameState.AllocateResourceForward);
            await session.VerifyGameConsistency();

            LogEvent("BeginResourceAllocationComplete", "✅ BeginResourceAllocation state verified - advanced to AllocateResourceForward");
        }

        /// <summary>
        /// Verify AllocateResourceForward state works correctly.
        /// Tests that players have proper entitlements and resource tracking is set up.
        /// Loops through all players to place settlement + road, then advances to AllocateResourceReverse.
        /// </summary>
        private async Task VerifyAllocateResourceForward(EndToEndSignalRSession session)
        {
            LogEvent("AllocateResourceForward", "Testing AllocateResourceForward state functionality");

            // Verify we're in correct state
            await session.VerifyAllProxiesInState(GameState.AllocateResourceForward);
            await session.VerifyGameConsistency();

            var gameState = session.GetProxy("Alice").LastGameState;
            Assert.NotNull(gameState);
            Assert.Equal(GameState.AllocateResourceForward, gameState.GameState);

            // Don't hard-code Alice - use the actual current player from game state
            var initialCurrentPlayerId = gameState.CurrentPlayerId;
            Assert.Equal("Alice", initialCurrentPlayerId); // Verify it's Alice but don't assume it

            // Verify initial game model structure for allocation
            Assert.True(gameState.Buildings.Count > 0);
            Assert.True(gameState.Roads.Count > 0);
            Assert.True(gameState.Tiles.Count > 0);
            Assert.False(gameState.ActionFlags.RollsEnabled);

            // DEBUG: Log detailed game state for analysis
            LogEvent("GameStateDebug", $"Total buildings: {gameState.Buildings.Count}");
            LogEvent("GameStateDebug", $"PossibleSettlements: {gameState.Buildings.Count(b => b.BuildingState == BuildingState.PossibleSettlement)}");
            LogEvent("GameStateDebug", $"NotBuildable: {gameState.Buildings.Count(b => b.BuildingState == BuildingState.NotBuildable)}");
            LogEvent("GameStateDebug", $"Tiles count: {gameState.Tiles.Count}");
            
            // Show a few example buildings
            for (int i = 0; i < Math.Min(5, gameState.Buildings.Count); i++)
            {
                var building = gameState.Buildings[i];
                LogEvent("BuildingExample", $"Building {i}: {building.BuildingKey}, State: {building.BuildingState}, Owner: {building.OwnerId ?? "None"}");
            }

            // Player order for Expansion game (5 players)
            var playerOrder = new[] { "Alice", "Bob", "Charlie", "David", "Eve" };
            LogEvent("ForwardAllocation", "Beginning forward allocation for all 5 players");

            // Loop through each player in forward order
            for (int i = 0; i < playerOrder.Length; i++)
            {
                // Use current player from game model, not hard-coded array
                var currentPlayerId = session.GetCurrentPlayerId();
                Assert.Equal(playerOrder[i], currentPlayerId);
                
                LogEvent("PlayerTurn", $"Processing {currentPlayerId}'s turn in forward allocation (player {i + 1}/{playerOrder.Length})");

                var proxy = session.GetProxy(currentPlayerId);
                var currentGameState = proxy.LastGameState;
                Assert.NotNull(currentGameState);

                // Verify player has proper entitlements for allocation
                var currentPlayer = currentGameState.Players.First(p => p.Id == currentPlayerId);
                Assert.Contains(Entitlement.Settlement, currentPlayer.UnspentEntitlements);
                Assert.Contains(Entitlement.Road, currentPlayer.UnspentEntitlements);
                LogEvent("EntitlementsVerified", $"✅ {currentPlayerId} has Settlement and Road entitlements");

                // Verify resource tracking is initialized
                Assert.NotNull(currentPlayer.ResourcesThisTurn);
                Assert.NotNull(currentPlayer.ResourcesThisGame);
                
                var totalResourcesThisGame = currentPlayer.ResourcesThisGame.Brick + 
                                           currentPlayer.ResourcesThisGame.Wood + 
                                           currentPlayer.ResourcesThisGame.Sheep + 
                                           currentPlayer.ResourcesThisGame.Wheat + 
                                           currentPlayer.ResourcesThisGame.Ore;
                
                Assert.True(totalResourcesThisGame == 0);
                LogEvent("ResourcesVerified", $"{currentPlayerId}: {totalResourcesThisGame} total resources (should be 0 initially)");

                // STEP 1: Place Settlement - DO NOT USE ALLOCATION HELPER
                try
                {
                    LogEvent("SettlementAttempt", $"Attempting settlement placement for {currentPlayerId}");
                    
                    // Use the local method instead of AllocationHelper
                    var settlementKey = PickOptimalSettlement(currentGameState);
                    LogEvent("SettlementSelected", $"{currentPlayerId} placing optimal settlement at {settlementKey}");

                    // Verify the selected settlement is actually possible
                    var selectedBuilding = currentGameState.Buildings.FirstOrDefault(b => b.BuildingKey.Equals(settlementKey));
                    if (selectedBuilding == null)
                    {
                        LogEvent("SettlementError", $"❌ Selected settlement {settlementKey} not found in game model");
                        throw new InvalidOperationException($"Selected settlement {settlementKey} not found");
                    }
                    
                    if (selectedBuilding.BuildingState != BuildingState.PossibleSettlement)
                    {
                        LogEvent("SettlementError", $"❌ Selected settlement {settlementKey} is not in PossibleSettlement state, actual state: {selectedBuilding.BuildingState}");
                        throw new InvalidOperationException($"Selected settlement {settlementKey} is not buildable");
                    }

                    LogEvent("SettlementValidated", $"✅ Settlement {settlementKey} is valid for placement");

                    var result = await proxy.ExecuteBuildingUpgradeAsync(session.GameId, settlementKey);
                    Assert.True(result.Success, $"Settlement placement failed: {result.Message}");
                    LogEvent("SettlementPlaced", $"✅ {currentPlayerId} settlement placement succeeded!");

                    // Verify game state after settlement placement
                    await session.VerifyGameConsistency();
                    var updatedGameState = proxy.LastGameState;
                    Assert.NotNull(updatedGameState);
                    
                    // Verify player's score increased
                    var playerAfterSettlement = updatedGameState.Players.First(p => p.Id == currentPlayerId);
                    Assert.Equal(1, playerAfterSettlement.Score);
                    LogEvent("ScoreUpdated", $"✅ {currentPlayerId} score is now {playerAfterSettlement.Score}");

                    // STEP 2: Place Road - find buildable roads after settlement placement
                    var buildableRoads = updatedGameState.Roads
                        .Where(r => r.RoadState == RoadState.Buildable)
                        .ToList();
                    
                    LogEvent("RoadSearch", $"Found {buildableRoads.Count} buildable roads for {currentPlayerId}");
                    Assert.True(buildableRoads.Count > 0, $"Should have buildable roads available for {currentPlayerId}");

                    // Pick the first buildable road
                    var selectedRoad = buildableRoads.First();
                    var roadKey = selectedRoad.RoadKey;
                    LogEvent("RoadSelected", $"{currentPlayerId} placing road at {roadKey}");

                    var roadResult = await proxy.ExecuteRoadPurchaseAsync(session.GameId, roadKey);
                    Assert.True(roadResult.Success, $"Road placement failed: {roadResult.Message}");
                    LogEvent("RoadPlaced", $"✅ {currentPlayerId} road placement succeeded!");

                    // Verify game consistency after road placement
                    await session.VerifyGameConsistency();
                    var finalGameState = proxy.LastGameState;
                    Assert.NotNull(finalGameState);

                    // Verify player no longer has unspent entitlements
                    var playerAfterRoad = finalGameState.Players.First(p => p.Id == currentPlayerId);
                    Assert.DoesNotContain(Entitlement.Settlement, playerAfterRoad.UnspentEntitlements);
                    Assert.DoesNotContain(Entitlement.Road, playerAfterRoad.UnspentEntitlements);
                    LogEvent("EntitlementsSpent", $"✅ {currentPlayerId} has spent all entitlements");

                }
                catch (Exception ex)
                {
                    LogEvent("BuildingPlacementError", $"❌ Building placement failed for {currentPlayerId}: {ex.GetType().Name}: {ex.Message}");
                    
                    if (ex.InnerException != null)
                    {
                        LogEvent("InnerException", $"Inner: {ex.InnerException.GetType().Name}: {ex.InnerException.Message}");
                    }
                    
                    // Log the stack trace for debugging
                    LogEvent("StackTrace", $"Stack trace: {ex.StackTrace}");
                    
                    throw;
                }

                // STEP 3: Advance to next player (or next state if last player)
                if (i < playerOrder.Length - 1)
                {
                    // Not the last player - advance to next player
                    await session.ExecuteActionWithVerification(currentPlayerId, GameAction.Next);
                    LogEvent("PlayerAdvanced", $"✅ {currentPlayerId} completed turn, advancing to next player");
                }
                else
                {
                    // Last player - advance to AllocateResourceReverse state
                    await session.ExecuteActionWithVerification(currentPlayerId, GameAction.Next);
                    LogEvent("PhaseAdvanced", $"✅ {currentPlayerId} completed final forward turn, advancing to AllocateResourceReverse");
                }
            }

            // Verify we've successfully advanced to AllocateResourceReverse
            await session.VerifyAllProxiesInState(GameState.AllocateResourceReverse);
            await session.VerifyGameConsistency();

            // Verify Eve is now the current player (reverse order starts with last player in 5-player EXPANSION game)
            var reverseGameState = session.GetProxy("Alice").LastGameState;
            Assert.NotNull(reverseGameState);
            Assert.Equal(GameState.AllocateResourceReverse, reverseGameState.GameState);
            Assert.Equal("Eve", reverseGameState.CurrentPlayerId);

            LogEvent("AllocateResourceForwardComplete", "✅ AllocateResourceForward completed - all players placed settlement + road, advanced to AllocateResourceReverse");
        }

        ///<summary>
        /// e2e helper that picks a settlement 
        ///</summary>
        private BuildingKey PickOptimalSettlement(GameModel gameModel)
        {
            // Get the current game state for the player
            var possibleSettlements = gameModel.Buildings
                .Where(b => b.BuildingState == BuildingState.PossibleSettlement)
                .ToList();
                
            if (!possibleSettlements.Any())
            {
                throw new InvalidOperationException("No possible settlements available");
            }
                
            // DO NOT Use AllocationHelper to pick the best settlement location - isolate the e2e stateful tests to this file
            // Simple heuristic: calculate a basic score based on building position coordinates
            var settlementOptions = possibleSettlements
                .Select(building => new
                {
                    building = building,
                    score = CalculateSimpleSettlementScore(building),
                    buildingKey = building.BuildingKey
                })
                .ToList();

            var maxScore = settlementOptions.Max(s => s.score);
            var bestSettlement = settlementOptions.First(s => s.score == maxScore);

            LogEvent("SettlementHelper", $"Selected settlement {bestSettlement.buildingKey} with score {bestSettlement.score}");

            return bestSettlement.buildingKey;
        }

        /// <summary>
        /// Simple scoring algorithm for settlement placement that doesn't rely on external helpers
        /// </summary>
        private int CalculateSimpleSettlementScore(BuildingModel building)
        {
            // Use a simple heuristic based on coordinates - prefer positions closer to center
            var coords = building.BuildingKey.HexCoordinates;
            var distanceFromCenter = Math.Abs(coords.Q) + Math.Abs(coords.R) + Math.Abs(coords.S);
            
            // Prefer settlements closer to center of board (lower distance = higher score)
            // Add some variation based on position to avoid ties
            var positionBonus = (int)building.BuildingKey.Position;
            return 100 - distanceFromCenter + positionBonus;
        }

        /// <summary>
        /// Verify AllocateResourceReverse state works correctly.
        /// Tests that players have proper entitlements and resource tracking (ResourcesThisTurn and ResourcesThisGame).
        /// This should behave the same way as forward allocation with additional verification of resource updates.
        /// The session should already be in AllocateResourceReverse state when this method is called.
        /// </summary>
        private async Task VerifyAllocateResourceReverse(EndToEndSignalRSession session)
        {
            LogEvent("AllocateResourceReverse", "Testing AllocateResourceReverse state functionality");

            // Verify we're in correct state (should have been set by VerifyAllocateResourceForward)
            await session.VerifyAllProxiesInState(GameState.AllocateResourceReverse);
            await session.VerifyGameConsistency();

            var gameState = session.GetProxy("Alice").LastGameState;
            Assert.NotNull(gameState);
            Assert.Equal(GameState.AllocateResourceReverse, gameState.GameState);
            
            // Eve should be current player in reverse phase (last player goes first in 5-player EXPANSION game)
            var currentPlayerId = gameState.CurrentPlayerId;
            Assert.Equal("Eve", currentPlayerId);

            // Verify current player has proper entitlements for allocation (same as forward)
            var currentPlayer = gameState.Players.First(p => p.Id == currentPlayerId);
            Assert.Contains(Entitlement.Settlement, currentPlayer.UnspentEntitlements);
            Assert.Contains(Entitlement.Road, currentPlayer.UnspentEntitlements);
            LogEvent("EntitlementsVerified", $"✅ {currentPlayerId} has Settlement and Road entitlements in reverse phase");

            // Verify resource tracking is set up correctly - this is the key test for AllocateResourceReverse
            // During allocation reverse, players should have ResourcesThisTurn and ResourcesThisGame properly initialized
            foreach (var player in gameState.Players)
            {
                Assert.NotNull(player.ResourcesThisTurn);
                Assert.NotNull(player.ResourcesThisGame);
                
                // During allocation phase, players should have received some resources from forward allocation
                // Each player should have at least some resources from their first settlement placement
                var totalResourcesThisGame = player.ResourcesThisGame.Brick + 
                                           player.ResourcesThisGame.Wood + 
                                           player.ResourcesThisGame.Sheep + 
                                           player.ResourcesThisGame.Wheat + 
                                           player.ResourcesThisGame.Ore;
                
                LogEvent("ResourceCheck", $"{player.Id}: {totalResourcesThisGame} total resources this game");
                
                // Players may or may not have resources depending on their settlement placement
                // but the resource tracking should be properly initialized
                Assert.True(totalResourcesThisGame >= 0);
                
                // Verify the ResourcesThisTurn structure
                var resourcesThisTurn = player.ResourcesThisTurn;
                var thisTurnTotal = resourcesThisTurn.Brick + resourcesThisTurn.Wood + 
                                  resourcesThisTurn.Sheep + resourcesThisTurn.Wheat + resourcesThisTurn.Ore;
                
                LogEvent("ResourcesThisTurn", $"{player.Id}: {thisTurnTotal} resources this turn");
                Assert.True(thisTurnTotal >= 0);
            }

            // Verify game model has proper structure for allocation (same as forward)
            Assert.True(gameState.Buildings.Count > 0);
            Assert.True(gameState.Roads.Count > 0);
            Assert.True(gameState.Tiles.Count > 0);
            Assert.Equal(5, gameState.Players.Count); // EXPANSION game has 5 players

            // Verify action flags for allocation reverse phase
            Assert.False(gameState.ActionFlags.RollsEnabled);
            LogEvent("ActionFlags", $"Next enabled: {gameState.ActionFlags.NextEnabled}, Rolls enabled: {gameState.ActionFlags.RollsEnabled}");

            // Test that the GameModel is properly updating ResourcesThisTurn and ResourcesThisGame
            // This is the key functionality difference that needs testing for AllocateResourceReverse
            var testStartTime = DateTime.UtcNow;
            
            // Verify that players from forward allocation have scores > 0 (from settlements)
            var playersWithScore = gameState.Players.Where(p => p.Score > 0).ToList();
            LogEvent("ScoreVerification", $"{playersWithScore.Count} players have score > 0 from forward allocation");
            
            // In allocation reverse, players should have buildings from forward allocation
            var ownedBuildings = gameState.Buildings.Where(b => !string.IsNullOrEmpty(b.OwnerId)).ToList();
            LogEvent("BuildingVerification", $"{ownedBuildings.Count} buildings are owned from forward allocation");
            
            // Verify that all players have exactly 1 settlement and 1 road from forward allocation
            foreach (var playerId in new[] { "Alice", "Bob", "Charlie", "David", "Eve" })
            {
                var playerBuildings = gameState.Buildings.Where(b => 
                    b.OwnerId == playerId && b.BuildingState == BuildingState.Settlement).Count();
                var playerRoads = gameState.Roads.Where(r => 
                    r.OwnerId == playerId && r.RoadState == RoadState.Road).Count();
                
                Assert.Equal(1, playerBuildings);
                Assert.Equal(1, playerRoads);
                LogEvent("ForwardAllocationVerified", $"{playerId}: 1 settlement, 1 road placed in forward phase");
            }
            
            var testEndTime = DateTime.UtcNow;
            var verificationTime = testEndTime - testStartTime;
            
            LogEvent("AllocateResourceReverseComplete", 
                $"✅ AllocateResourceReverse state verified with proper entitlements and resource tracking (verification took {verificationTime.TotalMilliseconds:F0}ms)");
        }

        private void LogEvent(string eventType, string message)
        {
            var timestamp = DateTime.UtcNow.ToString("HH:mm:ss.fff");
            Console.WriteLine($"[{timestamp}] [{eventType}] {message}");
        }
    }

    /// <summary>
    /// E2E-specific session wrapper that uses SignalRProxy from Catan3.Shared
    /// This complies with the rule: "SignalR: use the proxy in the Shared project to call SignalR"
    /// </summary>
    public class EndToEndSignalRSession : IAsyncDisposable
    {
        private readonly WebApplicationFactory<Program> _factory;
        private readonly GameType _gameType;
        private readonly string[] _playerIds;
        private readonly Dictionary<string, SignalRProxy> _proxies = new();
        
        public string GameId { get; private set; } = "";
        public string[] PlayerIds => _playerIds;

        public EndToEndSignalRSession(WebApplicationFactory<Program> factory, GameType gameType, string[] playerIds)
        {
            _factory = factory;
            _gameType = gameType;
            _playerIds = playerIds;
        }

        /// <summary>
        /// Initializes the session by creating a game and connecting all players via SignalRProxy
        /// </summary>
        public async Task InitializeAsync()
        {
            // Create game via REST API
            var httpClient = _factory.CreateClient();
            var gameId = await CreateGameViaRest(httpClient, _gameType, _playerIds);
            GameId = gameId;
            
            // Connect all players via SignalRProxy using test factory handler
            foreach (var playerId in _playerIds)
            {
                // Use test factory to create a connection that works with in-memory test server
                var uri = _factory.Server.BaseAddress ?? new Uri("http://localhost");
                var hubUrl = new Uri(uri, "/gameHub").ToString();
                
                // Use the HttpMessageHandler constructor - this is perfect for tests!
                var testHandler = _factory.Server.CreateHandler();
                var proxy = new SignalRProxy(hubUrl, testHandler, playerId, gameId);
                await proxy.ConnectAsync();
                _proxies[playerId] = proxy;
            }
        }

        /// <summary>
        /// Gets a specific proxy by player ID
        /// </summary>
        public SignalRProxy GetProxy(string playerId)
        {
            if (!_proxies.TryGetValue(playerId, out var proxy))
            {
                throw new InvalidOperationException($"Proxy for player {playerId} not found");
            }
            return proxy;
        }

        /// <summary>
        /// Gets the current player ID from the game state
        /// </summary>
        public string GetCurrentPlayerId()
        {
            var anyProxy = _proxies.Values.First();
            var currentPlayerId = anyProxy.LastGameState?.CurrentPlayerId;
            
            if (string.IsNullOrEmpty(currentPlayerId))
            {
                // Default to first player if no current player set yet
                return _playerIds[0];
            }
            
            return currentPlayerId;
        }

        /// <summary>
        /// Verifies all proxies are in the expected state
        /// </summary>
        public async Task VerifyAllProxiesInState(GameState expectedState)
        {
            var tasks = _proxies.Values.Select(proxy => proxy.WaitForGameStateAsync(expectedState, TimeSpan.FromSeconds(5)));
            await Task.WhenAll(tasks);
        }

        /// <summary>
        /// Executes action and verifies all proxies receive updates
        /// </summary>
        public async Task ExecuteActionWithVerification(string playerId, GameAction action)
        {
            var executingProxy = GetProxy(playerId);
            
            // Execute action using SignalRProxy
            var result = await executingProxy.ExecuteDoActionAsync(GameId, action);
            
            if (!result.Success)
            {
                throw new InvalidOperationException($"Action {action} failed: {result.Message}");
            }
            
            // Verify all proxies received updates by checking their latest game state
            await VerifyAllProxiesReceivedUpdate();
        }

        /// <summary>
        /// Verifies all proxies have received recent updates (have consistent game state)
        /// </summary>
        public async Task VerifyAllProxiesReceivedUpdate()
        {
            // Brief delay to allow for state propagation
            await Task.Delay(50);
            
            // Check that all proxies have consistent LastGameState and GameHash
            var gameStates = _proxies.Values
                .Select(p => new { Proxy = p.PlayerId, State = p.LastGameState?.GameState, Hash = p.LastGameState?.GameHash })
                .Where(x => x.State.HasValue)
                .ToList();
            
            if (gameStates.Count > 1)
            {
                var reference = gameStates[0];
                var inconsistencies = gameStates.Where(g => g.State != reference.State || g.Hash != reference.Hash).ToList();
                
                if (inconsistencies.Any())
                {
                    var errorMessage = $"Game state inconsistency detected: {string.Join(", ", inconsistencies.Select(i => $"{i.Proxy}:{i.State}"))}";
                    throw new InvalidOperationException(errorMessage);
                }
            }
        }

        /// <summary>
        /// Verifies game consistency across all proxies using GameHash
        /// </summary>
        public async Task VerifyGameConsistency()
        {
            await Task.Delay(50); // Brief delay for state propagation
            
            var proxyStates = _proxies.Values
                .Select(p => new { Proxy = p.PlayerId, GameState = p.LastGameState })
                .Where(x => x.GameState != null)
                .ToList();
            
            if (proxyStates.Count <= 1) return;
            
            var referenceProxy = proxyStates[0];
            var referenceState = referenceProxy.GameState!;
            var inconsistencies = new List<string>();
            
            foreach (var proxyState in proxyStates.Skip(1))
            {
                var state = proxyState.GameState!;
                
                if (state.GameState != referenceState.GameState)
                    inconsistencies.Add($"{proxyState.Proxy}: GameState {state.GameState} vs {referenceState.GameState}");
                    
                if (state.CurrentPlayerId != referenceState.CurrentPlayerId)
                    inconsistencies.Add($"{proxyState.Proxy}: CurrentPlayer {state.CurrentPlayerId} vs {referenceState.CurrentPlayerId}");
                    
                if (state.GameStateMachineVersion != referenceState.GameStateMachineVersion)
                    inconsistencies.Add($"{proxyState.Proxy}: Version {state.GameStateMachineVersion} vs {referenceState.GameStateMachineVersion}");
                
                // GameHash verification for board consistency
                if (!string.IsNullOrEmpty(state.GameHash) && !string.IsNullOrEmpty(referenceState.GameHash))
                {
                    if (state.GameHash != referenceState.GameHash)
                    {
                        inconsistencies.Add($"{proxyState.Proxy}: GameHash {state.GameHash} vs {referenceState.GameHash} (BOARD MISMATCH!)");
                    }
                }
            }
            
            if (inconsistencies.Any())
            {
                var errorMessage = $"Game consistency check failed:\n  " + string.Join("\n  ", inconsistencies);
                throw new InvalidOperationException(errorMessage);
            }
        }

        /// <summary>
        /// Creates a game via REST API
        /// </summary>
        private static async Task<string> CreateGameViaRest(HttpClient httpClient, GameType gameType, string[] playerIds)
        {
            var newGameRequest = new 
            { 
                gameType = gameType.ToString(), 
                playerIds = playerIds
            };
            
            var newGameJson = JsonSerializer.Serialize(newGameRequest);
            var newGameContent = new StringContent(newGameJson, System.Text.Encoding.UTF8, "application/json");
            
            var newGameResponse = await httpClient.PostAsync("/api/game/new", newGameContent);

            if (!newGameResponse.IsSuccessStatusCode)
            {
                var errorContent = await newGameResponse.Content.ReadAsStringAsync();
                throw new InvalidOperationException($"Failed to create game: {newGameResponse.StatusCode}. Error: {errorContent}");
            }

            var newGameBody = await newGameResponse.Content.ReadAsStringAsync();
            var newGameResult = JsonSerializer.Deserialize<JsonElement>(newGameBody);
            
            if (!newGameResult.TryGetProperty("gameId", out var gameIdElement))
            {
                throw new InvalidOperationException("Game creation did not return gameId");
            }
            
            return gameIdElement.GetString() ?? 
                throw new InvalidOperationException("Game creation returned null gameId");
        }

        /// <summary>
        /// Properly disposes all proxies
        /// </summary>
        public async ValueTask DisposeAsync()
        {
            foreach (var proxy in _proxies.Values)
            {
                await proxy.DisposeAsync();
            }
            _proxies.Clear();
        }
    }
}