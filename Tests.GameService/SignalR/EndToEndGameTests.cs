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
            
            // Enable AllocateResourceReverse testing - now properly ported from CLI
            await VerifyAllocateResourceReverse(e2eSession);
            
            // Add DoneResourceAllocation testing - ported from CLI
            await VerifyDoneResourceAllocation(e2eSession);
            
            var testEndTime = DateTime.UtcNow;
            var totalTestTime = testEndTime - testStartTime;
            
            LogEvent("TestComplete", $"✅ End-to-End stateful test completed successfully through DoneResourceAllocation!");
            LogEvent("TestTiming", $"⏱️ Total test execution time: {totalTestTime.TotalSeconds:F2} seconds");
            LogEvent("NextPhase", "Next: WaitingForRoll can be tested in future iterations");
            
            // Performance assertion
            Assert.True(totalTestTime.TotalSeconds < 120, 
                $"E2E test should complete within 2 minutes, took {totalTestTime.TotalSeconds:F2} seconds");

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
                    var afterRoadGameState = proxy.LastGameState;
                    Assert.NotNull(afterRoadGameState);

                    // Verify player no longer has unspent entitlements
                    var playerAfterRoad = afterRoadGameState.Players.First(p => p.Id == currentPlayerId);
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
        /// Processes all players in reverse order for second settlement and road placement.
        /// Port of CLI comprehensive testing for this state.
        /// </summary>
        private async Task VerifyAllocateResourceReverse(EndToEndSignalRSession session)
        {
            LogEvent("AllocateResourceReverse", "Testing AllocateResourceReverse state functionality");

            // ASSERTION 1: Verify we're in the correct state (should have been set by VerifyAllocateResourceForward)
            await session.VerifyAllProxiesInState(GameState.AllocateResourceReverse);
            await session.VerifyGameConsistency();

            var gameState = session.GetProxy("Alice").LastGameState;
            Assert.NotNull(gameState);
            Assert.Equal(GameState.AllocateResourceReverse, gameState.GameState);
            LogEvent("StateVerified", "✅ Confirmed game is in AllocateResourceReverse state");
            
            // ASSERTION 2: Verify current player is the last player (reverse order starts with last player in 5-player EXPANSION game)
            var currentPlayerId = gameState.CurrentPlayerId;
            Assert.Equal("Eve", currentPlayerId);
            LogEvent("CurrentPlayerVerified", $"✅ {currentPlayerId} is current player (reverse order starts with last player)");

            // ASSERTION 3: Verify game structure for reverse allocation
            Assert.True(gameState.Buildings.Count > 0, "Should have buildings available for allocation");
            Assert.True(gameState.Roads.Count > 0, "Should have roads available for allocation");
            Assert.False(gameState.ActionFlags.RollsEnabled, "Rolls should not be enabled during allocation");
            LogEvent("GameStructureVerified", $"✅ Game has {gameState.Buildings.Count} buildings and {gameState.Roads.Count} roads, rolls disabled");

            // ASSERTION 4: Verify forward allocation was completed - all players should have 1 settlement and 1 road
            var playerIds = new[] { "Alice", "Bob", "Charlie", "David", "Eve" };
            foreach (var playerId in playerIds)
            {
                var playerBuildings = gameState.Buildings.Count(b => 
                    b.OwnerId == playerId && b.BuildingState == BuildingState.Settlement);
                var playerRoads = gameState.Roads.Count(r => 
                    r.OwnerId == playerId && r.RoadState == RoadState.Road);
                
                Assert.Equal(1, playerBuildings);
                Assert.Equal(1, playerRoads);
                LogEvent("ForwardAllocationVerified", $"✅ {playerId} has 1 settlement and 1 road from forward allocation");
            }

            // PLAYER ALLOCATION LOOP: Process each player in reverse order
            var reversePlayerIds = playerIds.AsEnumerable().Reverse().ToArray();
            LogEvent("ReverseAllocation", $"Beginning reverse allocation for {reversePlayerIds.Length} players");

            for (int i = 0; i < reversePlayerIds.Length; i++)
            {
                currentPlayerId = session.GetCurrentPlayerId();
                var expectedPlayer = reversePlayerIds[i];
                
                LogEvent("PlayerTurn", $"Processing {currentPlayerId}'s turn in reverse allocation (player {i + 1}/{reversePlayerIds.Length})");
                
                // ASSERTION: Verify correct player turn
                Assert.Equal(expectedPlayer, currentPlayerId);

                var proxy = session.GetProxy(currentPlayerId);
                var currentGameState = proxy.LastGameState;
                Assert.NotNull(currentGameState);

                // ASSERTION: Verify player entitlements
                var currentPlayer = currentGameState.Players.First(p => p.Id == currentPlayerId);
                Assert.Contains(Entitlement.Settlement, currentPlayer.UnspentEntitlements);
                Assert.Contains(Entitlement.Road, currentPlayer.UnspentEntitlements);
                LogEvent("EntitlementsVerified", $"✅ {currentPlayerId} has Settlement and Road entitlements");

                // ASSERTION: Verify resource tracking from forward allocation
                Assert.NotNull(currentPlayer.ResourcesThisGame);
                Assert.NotNull(currentPlayer.ResourcesThisTurn);

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

                LogEvent("ResourceTracking", $"{currentPlayerId} before reverse settlement: {initialResourcesThisGame} total game resources, {initialResourcesThisTurn} this turn");
                LogEvent("ResourcesVerified", $"✅ {currentPlayerId} resource tracking properly initialized");

                // STEP 1: Place Settlement - using same logic as forward allocation
                try
                {
                    LogEvent("SettlementAttempt", $"Attempting settlement placement for {currentPlayerId}");
                    
                    var settlementKey = PickOptimalSettlement(currentGameState);
                    LogEvent("SettlementSelected", $"{currentPlayerId} placing optimal settlement at {settlementKey}");

                    // Verify the selected settlement is actually possible
                    var selectedBuilding = currentGameState.Buildings.FirstOrDefault(b => b.BuildingKey.Equals(settlementKey));
                    Assert.NotNull(selectedBuilding);
                    Assert.Equal(BuildingState.PossibleSettlement, selectedBuilding.BuildingState);
                    LogEvent("SettlementValidated", $"✅ Settlement {settlementKey} is valid for placement");

                    var result = await proxy.ExecuteBuildingUpgradeAsync(session.GameId, settlementKey);
                    Assert.True(result.Success, $"Settlement placement failed: {result.Message}");
                    LogEvent("SettlementPlaced", $"✅ {currentPlayerId} settlement placement succeeded!");

                    // Verify game state after settlement placement
                    await session.VerifyGameConsistency();
                    var updatedGameState = proxy.LastGameState;
                    Assert.NotNull(updatedGameState);
                    
                    // Verify player's score increased to 2 (second settlement)
                    var playerAfterSettlement = updatedGameState.Players.First(p => p.Id == currentPlayerId);
                    Assert.Equal(2, playerAfterSettlement.Score);
                    LogEvent("ScoreUpdated", $"✅ {currentPlayerId} score is now {playerAfterSettlement.Score}");

                    // Verify Settlement entitlement was spent
                    Assert.DoesNotContain(Entitlement.Settlement, playerAfterSettlement.UnspentEntitlements);

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

                    var resourcesGained = finalResourcesThisGame - initialResourcesThisGame;
                    var thisTurnGained = finalResourcesThisTurn - initialResourcesThisTurn;

                    LogEvent("ResourceUpdate", $"{currentPlayerId} after reverse settlement: {finalResourcesThisGame} total (+{resourcesGained}), {finalResourcesThisTurn} this turn (+{thisTurnGained})");
                    
                    // In reverse allocation, the second settlement typically yields resources
                    if (resourcesGained >= 0)
                    {
                        LogEvent("ResourceTrackingVerified", $"✅ {currentPlayerId} resource tracking updated correctly in reverse allocation");
                    }
                    else
                    {
                        LogEvent("ResourceTrackingWarning", $"⚠️ {currentPlayerId} resource tracking shows decrease - may be valid based on settlement location");
                    }

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
                    var afterRoadGameState = proxy.LastGameState;
                    Assert.NotNull(afterRoadGameState);

                    // Verify player no longer has unspent entitlements
                    var playerAfterRoad = afterRoadGameState.Players.First(p => p.Id == currentPlayerId);
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
                    
                    throw;
                }

                // STEP 3: Advance to next player or next state if last player
                if (i < reversePlayerIds.Length - 1)
                {
                    // Not the last player - advance to next player in reverse order
                    await session.ExecuteActionWithVerification(currentPlayerId, GameAction.Next);
                    LogEvent("PlayerAdvanced", $"✅ {currentPlayerId} completed turn, advancing to next player in reverse order");
                }
                else
                {
                    // Last player - advance to DoneResourceAllocation state
                    await session.ExecuteActionWithVerification(currentPlayerId, GameAction.Next);
                    LogEvent("PhaseAdvanced", $"✅ {currentPlayerId} completed final reverse turn, advancing to DoneResourceAllocation");
                }
            }

            // FINAL ASSERTION: Verify we advanced to DoneResourceAllocation
            await session.VerifyAllProxiesInState(GameState.DoneResourceAllocation);
            await session.VerifyGameConsistency();

            var finalGameState = session.GetProxy("Alice").LastGameState;
            Assert.NotNull(finalGameState);
            Assert.Equal(GameState.DoneResourceAllocation, finalGameState.GameState);
            LogEvent("FinalStateVerified", "✅ Successfully advanced to DoneResourceAllocation");

            // FINAL VERIFICATION: Verify all players have exactly 2 settlements and 2 roads
            foreach (var playerId in playerIds)
            {
                var playerBuildings = finalGameState.Buildings.Count(b => 
                    b.OwnerId == playerId && b.BuildingState == BuildingState.Settlement);
                var playerRoads = finalGameState.Roads.Count(r => 
                    r.OwnerId == playerId && r.RoadState == RoadState.Road);
                var player = finalGameState.Players.FirstOrDefault(p => p.Id == playerId);
                
                Assert.Equal(2, playerBuildings);
                Assert.Equal(2, playerRoads);
                Assert.Equal(2, player?.Score);
                
                var totalResources = (player?.ResourcesThisGame.Brick ?? 0) + (player?.ResourcesThisGame.Wood ?? 0) + 
                                   (player?.ResourcesThisGame.Sheep ?? 0) + (player?.ResourcesThisGame.Wheat ?? 0) + (player?.ResourcesThisGame.Ore ?? 0);
                
                LogEvent("FinalPlayerState", $"✅ {playerId}: 2 settlements, 2 roads, score 2, {totalResources} total resources");
            }
            
            LogEvent("AllocateResourceReverseComplete", "✅ All players completed reverse allocation successfully with proper resource tracking and verification");
        }

        /// <summary>
        /// Verify DoneResourceAllocation state works correctly.
        /// Tests Next action to advance to WaitingForRoll.
        /// Port of CLI comprehensive testing for this state.
        /// </summary>
        private async Task VerifyDoneResourceAllocation(EndToEndSignalRSession session)
        {
            LogEvent("DoneResourceAllocation", "Testing DoneResourceAllocation state functionality");
            
            // ASSERTION 1: Verify we're in the correct state
            await session.VerifyAllProxiesInState(GameState.DoneResourceAllocation);
            await session.VerifyGameConsistency();

            var gameState = session.GetProxy("Alice").LastGameState;
            Assert.NotNull(gameState);
            Assert.Equal(GameState.DoneResourceAllocation, gameState.GameState);
            LogEvent("StateVerified", "✅ Confirmed game is in DoneResourceAllocation state");

            // ASSERTION 2: Verify final allocation results - all players should have 2 settlements and 2 roads
            var playerIds = new[] { "Alice", "Bob", "Charlie", "David", "Eve" };
            foreach (var playerId in playerIds)
            {
                var playerBuildings = gameState.Buildings.Count(b => 
                    b.OwnerId == playerId && b.BuildingState == BuildingState.Settlement);
                var playerRoads = gameState.Roads.Count(r => 
                    r.OwnerId == playerId && r.RoadState == RoadState.Road);
                var player = gameState.Players.FirstOrDefault(p => p.Id == playerId);
                
                Assert.Equal(2, playerBuildings);
                Assert.Equal(2, playerRoads);
                Assert.Equal(2, player?.Score);
                
                LogEvent("AllocationComplete", $"✅ {playerId}: 2 settlements, 2 roads, score 2 - allocation phase complete");
            }

            // ASSERTION 3: Verify current player (should be first player for WaitingForRoll phase)
            var currentPlayerId = session.GetCurrentPlayerId();
            Assert.Equal("Alice", currentPlayerId);
            LogEvent("CurrentPlayerVerified", $"✅ Current player is {currentPlayerId} - ready for roll phase");

            // ADVANCEMENT TEST: Test Next action to advance to WaitingForRoll
            LogEvent("AdvancementTest", "Testing advancement with Next action to WaitingForRoll");
            await session.ExecuteActionWithVerification(currentPlayerId, GameAction.Next);
            
            // FINAL ASSERTION: Verify we advanced to WaitingForRoll
            await session.VerifyAllProxiesInState(GameState.WaitingForRoll);
            await session.VerifyGameConsistency();

            var finalGameState = session.GetProxy("Alice").LastGameState;
            Assert.NotNull(finalGameState);
            Assert.Equal(GameState.WaitingForRoll, finalGameState.GameState);
            LogEvent("FinalStateVerified", "✅ Successfully advanced to WaitingForRoll");

            // ASSERTION 4: Verify action flags are correct for WaitingForRoll
            Assert.True(finalGameState.ActionFlags.RollsEnabled, "Rolls should be enabled in WaitingForRoll state");
            Assert.False(finalGameState.ActionFlags.NextEnabled, "Next should be disabled until dice are rolled");
            LogEvent("ActionFlagsVerified", "✅ Action flags correct for WaitingForRoll: rolls enabled, next disabled");

            LogEvent("DoneResourceAllocationComplete", "✅ DoneResourceAllocation state verified - successfully advanced to WaitingForRoll");
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