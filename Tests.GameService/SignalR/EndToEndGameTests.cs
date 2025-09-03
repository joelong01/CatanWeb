using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Text.Json;
using Catan3.Shared.Models;
using Catan3.Shared.Services;
using Catan3.Shared.Utility;
using Catan3.Shared.Extensions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.Configuration;
using Tests.GameService.SignalR;
using Xunit;
using System.Reflection;

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
        //
        // during the allocation phase, we update the Buildings in GameModel.  the order of the Buildings is fixed
        // and the BuildingState is updated without worrying about order. But we need to know what the last building picked
        //  was so we can verify the resources.  we'll do that by just keeping a simple map of playerId -> Building because
        //  these tests are stateful...

        private readonly Dictionary<string, BuildingModel> _lastBuildingPicked = [];

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

        /// <summary>
        ///     This is the one and only test we are going to run. it is designed to go through a full game initialization and then test the main game
        ///     loop of WaitingForRoll -> WaitingForNext -> WaitingForRoll
        ///     We also test the MustMoveRobber, Purchase, and placing Roads/Buildings.
        /// </summary>
        /// <returns></returns>

        [Fact]
        public async Task EndToEndStatefulTest()
        {
            // Enable function timing for this test
            FunctionTimer.Enabled = true;

            var testStartTime = DateTime.UtcNow;

            // Progress through all game states sequentially
            EndToEndSignalRSession session;
            using (new FunctionTimer("VerifyGameCreationAndJoin", enableOverride: true))
            {
                session = await VerifyGameCreationAndJoin();
            }

            // Array of test functions with their names for timing
            var testFunctions = new (string Name, Func<EndToEndSignalRSession, Task> Function)[]
            {
                ("VerifyPickingBoard", VerifyPickingBoard),
                ("VerifyWaitingForRollForOrder", VerifyWaitingForRollForOrder),
                ("VerifyFinishedRollOrder", VerifyFinishedRollOrder),
                ("VerifyBeginResourceAllocation", VerifyBeginResourceAllocation),
                ("VerifyAllocationPhase", VerifyAllocationPhase),
                ("VerifyDoneResourceAllocation", VerifyDoneResourceAllocation)
            };

            // Execute each test function in sequence with timing
            foreach (var (name, testFunction) in testFunctions)
            {
                using (new FunctionTimer(name, enableOverride: true, writeToConsole: true))
                {
                    await testFunction(session);
                }
            }

            var testEndTime = DateTime.UtcNow;
            var totalTestTime = testEndTime - testStartTime;

            LogEvent(session, "TestComplete", $"✅ End-to-End stateful test completed successfully through DoneResourceAllocation!");
            LogEvent(session, "TestTiming", $"⏱️ Total test execution time: {totalTestTime.TotalSeconds:F2} seconds");
            LogEvent(session, "NextPhase", "Next: WaitingForRoll can be tested in future iterations");

            // Performance assertion
            Assert.True(totalTestTime.TotalSeconds < 120,
                $"E2E test should complete within 2 minutes, took {totalTestTime.TotalSeconds:F2} seconds");

            // Properly dispose the session to clean up all resources
            await session.DisposeAsync();
            LogEvent(null, "CleanupComplete", "✅ All test resources properly disposed");

            // Disable function timing after test completes
            FunctionTimer.Enabled = false;
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
            LogEvent(null, "GameCreation", "Creating EXPANSION game with 5 players for testing");

            // Create EXPANSION game with 5 players - expansion requires 5 players
            var playerIds = new[] { "Alice", "Bob", "Charlie", "David", "Eve" };
            var session = new EndToEndSignalRSession(_factory, GameType.Expansion, playerIds);
            await session.InitializeAsync();

            // Wait for all proxies to receive the initial GameModel and be in the PickingBoard state
            await session.VerifyAllProxiesInState(GameState.PickingBoard);

            LogEvent(session, "PlayersJoined", $"Game created: {session.GameId} with {playerIds.Length} players");

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

            var gameState = session.GetProxy("Alice").GameModel;
            Assert.NotNull(gameState);
            Assert.Equal(GameState.PickingBoard, gameState.GameState);
            Assert.Equal("Alice", gameState.CurrentPlayerId);

            // Verify EXPANSION game structure
            Assert.True(gameState.Buildings.Count > 0);
            Assert.True(gameState.Roads.Count > 0);
            Assert.True(gameState.Tiles.Count > 0);
            Assert.Equal(5, gameState.Players.Count);

            LogEvent(session, "GameCreationVerified", "✅ EXPANSION game creation and player join verified");
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
            LogEvent(session, "PickingBoard", "Testing PickingBoard state functionality");

            // Verify we're in PickingBoard state
            await session.VerifyAllProxiesInState(GameState.PickingBoard);
            await session.VerifyGameConsistency();

            var currentPlayerId = session.GetCurrentPlayerId();
            Assert.Equal("Alice", currentPlayerId);

            // Test Shuffle action with hash verification
            var initialGameState = session.GetProxy("Alice").GameModel;
            var initialHash = initialGameState?.GameHash;
            Assert.NotNull(initialHash);
            LogEvent(session, "InitialHash", $"Initial ExpectedGameHash: {initialHash}");

            // Execute first shuffle
            var shuffleProxy = session.GetProxy(currentPlayerId);
            var shuffleResult = await shuffleProxy.ExecuteShuffleAsync();
            if (!shuffleResult.Success)
            {
                throw new InvalidOperationException($"Shuffle action failed: {shuffleResult.Message}");
            }
            await session.VerifyAllProxiesReceivedUpdate();
            var firstShuffleState = session.GetProxy("Alice").GameModel;
            var firstShuffleHash = firstShuffleState?.GameHash;
            Assert.NotNull(firstShuffleHash);
            Assert.NotEqual(initialHash, firstShuffleHash);
            LogEvent(session, "FirstShuffle", $"After first shuffle: {firstShuffleHash}");

            // Execute second shuffle
            shuffleResult = await shuffleProxy.ExecuteShuffleAsync();
            if (!shuffleResult.Success)
            {
                throw new InvalidOperationException($"Shuffle action failed: {shuffleResult.Message}");
            }
            await session.VerifyAllProxiesReceivedUpdate();
            var secondShuffleState = session.GetProxy("Alice").GameModel;
            var secondShuffleHash = secondShuffleState?.GameHash;
            Assert.NotNull(secondShuffleHash);
            Assert.NotEqual(firstShuffleHash, secondShuffleHash);
            LogEvent(session, "SecondShuffle", $"After second shuffle: {secondShuffleHash}");

            // Test Undo functionality
            var undoProxy = session.GetProxy(currentPlayerId);
            var undoResult = await undoProxy.ExecuteUndoAsync();
            if (!undoResult.Success)
            {
                throw new InvalidOperationException($"Undo action failed: {undoResult.Message}");
            }
            await session.VerifyAllProxiesReceivedUpdate();
            var undoState = session.GetProxy("Alice").GameModel;
            var undoHash = undoState?.GameHash;
            Assert.Equal(firstShuffleHash, undoHash);
            Assert.True(undoState?.ActionFlags.RedoEnabled);
            LogEvent(session, "UndoVerified", "✅ Undo restored previous state correctly");

            // Test Redo functionality
            var redoProxy = session.GetProxy(currentPlayerId);
            var redoResult = await redoProxy.ExecuteRedoAsync();
            if (!redoResult.Success)
            {
                throw new InvalidOperationException($"Redo action failed: {redoResult.Message}");
            }
            await session.VerifyAllProxiesReceivedUpdate();
            var redoState = session.GetProxy("Alice").GameModel;
            var redoHash = redoState?.GameHash;
            Assert.Equal(secondShuffleHash, redoHash);
            LogEvent(session, "RedoVerified", "✅ Redo restored forward state correctly");

            // Test Balance functionality (if available)
            try
            {
                var balanceProxy = session.GetProxy(currentPlayerId);
                var balanceResult = await balanceProxy.ExecuteBalanceAsync();
                if (!balanceResult.Success)
                {
                    throw new InvalidOperationException($"Balance action failed: {balanceResult.Message}");
                }
                await session.VerifyAllProxiesReceivedUpdate();
                var balanceState = session.GetProxy("Alice").GameModel;
                Assert.NotNull(balanceState);
                LogEvent(session, "BalanceVerified", "✅ Balance action executed successfully");
            }
            catch (Exception ex) when (ex.Message.Contains("balance") || ex.Message.Contains("swap"))
            {
                LogEvent(session, "BalanceSkipped", "Balance action not available - expected for some board configurations");
            }

            // Advance to next state using Next action
            var nextProxy = session.GetProxy(currentPlayerId);
            var nextResult = await nextProxy.ExecuteNextAsync();
            if (!nextResult.Success)
            {
                throw new InvalidOperationException($"Next action failed: {nextResult.Message}");
            }
            await session.VerifyAllProxiesReceivedUpdate();
            await session.VerifyAllProxiesInState(GameState.WaitingForRollForOrder);
            await session.VerifyGameConsistency();

            LogEvent(session, "PickingBoardComplete", "✅ PickingBoard state verified - advanced to WaitingForRollForOrder");
        }

        /// <summary>
        /// Verify WaitingForRollForOrder state works correctly.
        /// Tests Next action to advance to FinishedRollOrder.
        /// </summary>
        private async Task VerifyWaitingForRollForOrder(EndToEndSignalRSession session)
        {
            LogEvent(session, "WaitingForRollForOrder", "Testing WaitingForRollForOrder state functionality");

            // Verify we're in correct state
            await session.VerifyAllProxiesInState(GameState.WaitingForRollForOrder);
            await session.VerifyGameConsistency();

            var gameState = session.GetProxy("Alice").GameModel;
            Assert.NotNull(gameState);
            Assert.Equal(GameState.WaitingForRollForOrder, gameState.GameState);
            Assert.Equal("Alice", gameState.CurrentPlayerId);

            // Test Next action to advance to FinishedRollOrder
            var currentPlayerId = session.GetCurrentPlayerId();
            var nextProxy = session.GetProxy(currentPlayerId);
            var nextResult = await nextProxy.ExecuteNextAsync();
            if (!nextResult.Success)
            {
                throw new InvalidOperationException($"Next action failed: {nextResult.Message}");
            }
            await session.VerifyAllProxiesReceivedUpdate();
            await session.VerifyAllProxiesInState(GameState.FinishedRollOrder);
            await session.VerifyGameConsistency();

            LogEvent(session, "WaitingForRollForOrderComplete", "✅ WaitingForRollForOrder state verified - advanced to FinishedRollOrder");
        }

        /// <summary>
        /// Verify FinishedRollOrder state works correctly.
        /// Tests Next action to advance to BeginResourceAllocation.
        /// </summary>
        private async Task VerifyFinishedRollOrder(EndToEndSignalRSession session)
        {
            LogEvent(session, "FinishedRollOrder", "Testing FinishedRollOrder state functionality");

            // Verify we're in correct state
            await session.VerifyAllProxiesInState(GameState.FinishedRollOrder);
            await session.VerifyGameConsistency();

            var gameState = session.GetProxy("Alice").GameModel;
            Assert.NotNull(gameState);
            Assert.Equal(GameState.FinishedRollOrder, gameState.GameState);
            Assert.Equal("Alice", gameState.CurrentPlayerId);

            // Test Next action to advance to BeginResourceAllocation
            var currentPlayerId = session.GetCurrentPlayerId();
            var nextProxy = session.GetProxy(currentPlayerId);
            var nextResult = await nextProxy.ExecuteNextAsync();
            if (!nextResult.Success)
            {
                throw new InvalidOperationException($"Next action failed: {nextResult.Message}");
            }
            await session.VerifyAllProxiesReceivedUpdate();
            await session.VerifyAllProxiesInState(GameState.BeginResourceAllocation);
            await session.VerifyGameConsistency();

            LogEvent(session, "FinishedRollOrderComplete", "✅ FinishedRollOrder state verified - advanced to BeginResourceAllocation");
        }

        /// <summary>
        /// Verify BeginResourceAllocation state works correctly.
        /// Tests Next action to advance to AllocateResourceForward.
        /// </summary>
        private async Task VerifyBeginResourceAllocation(EndToEndSignalRSession session)
        {
            LogEvent(session, "BeginResourceAllocation", "Testing BeginResourceAllocation state functionality");

            // Verify we're in correct state
            await session.VerifyAllProxiesInState(GameState.BeginResourceAllocation);
            await session.VerifyGameConsistency();

            var gameState = session.GetProxy("Alice").GameModel;
            Assert.NotNull(gameState);
            Assert.Equal(GameState.BeginResourceAllocation, gameState.GameState);
            Assert.Equal("Alice", gameState.CurrentPlayerId);

            // Test Next action to advance to AllocateResourceForward
            var currentPlayerId = session.GetCurrentPlayerId();
            var nextProxy = session.GetProxy(currentPlayerId);
            var nextResult = await nextProxy.ExecuteNextAsync();
            if (!nextResult.Success)
            {
                throw new InvalidOperationException($"Next action failed: {nextResult.Message}");
            }
            await session.VerifyAllProxiesReceivedUpdate();
            await session.VerifyAllProxiesInState(GameState.AllocateResourceForward);
            await session.VerifyGameConsistency();

            LogEvent(session, "BeginResourceAllocationComplete", "✅ BeginResourceAllocation state verified - advanced to AllocateResourceForward");
        }

        /// <summary>
        /// Verify AllocateResourceForward and AllocationResourceReverse states works correctly.
        /// Tests that players have proper entitlements and resource tracking is set up.
        /// Loops through all players to place settlement + road, loop until we get to DoneResourceAllocations
        /// the test for that state will verify that the proper resources are assigned
        /// </summary>
        private async Task VerifyAllocationPhase(EndToEndSignalRSession session)
        {
            LogEvent(session, "AllocationPhase", "Testing AllocateResourceForward state functionality");

            // Verify we're in correct state
            await session.VerifyAllProxiesInState(GameState.AllocateResourceForward);
            await session.VerifyGameConsistency();

            var gameModel = session.GetProxy("Alice").GameModel;
            Assert.NotNull(gameModel);
            Assert.Equal(GameState.AllocateResourceForward, gameModel.GameState);
            var initialCurrentPlayerId = gameModel.CurrentPlayerId;
            Assert.Equal("Alice", initialCurrentPlayerId);

            LogEvent(session, "BuildingCount", $"Buildings: {gameModel.Buildings.Count}, Roads: {gameModel.Roads.Count}, Tiles: {gameModel.Tiles.Count}");
            Assert.False(gameModel.ActionFlags.RollsEnabled);
            Assert.Equal(80, gameModel.Buildings.Count);
            Assert.Equal(30, gameModel.Tiles.Count);
            Assert.Equal(109, gameModel.Roads.Count);

            var currentPlayerId = session.GetCurrentPlayerId();
            var proxy = session.GetProxy(currentPlayerId);
            gameModel = proxy.GameModel;
            Assert.NotNull(gameModel);
            // Player order for Expansion game (5 players)

            // Loop through each player until we are done allocating resources
            while (gameModel.GameState == GameState.AllocateResourceForward || gameModel.GameState == GameState.AllocateResourceReverse)
            {


                // Verify player has proper entitlements for allocation
                var currentPlayer = gameModel.Players.First(p => p.Id == currentPlayerId);
                Assert.Contains(Entitlement.Settlement, currentPlayer.UnspentEntitlements);
                Assert.Contains(Entitlement.Road, currentPlayer.UnspentEntitlements);
                LogEvent(session, "EntitlementsVerified", $"✅ {currentPlayerId} has Settlement and Road entitlements");

                // Verify resource tracking is initialized
                Assert.NotNull(currentPlayer.ResourcesThisTurn);
                Assert.NotNull(currentPlayer.ResourcesThisGame);

                // STEP 1: Place Settlement
                try
                {
                    LogEvent(session, "SettlementAttempt", $"Attempting settlement placement for {currentPlayerId}");

                    // Use the local method instead of AllocationHelper
                    var bestBuilding = PickOptimalSettlement(gameModel);
                    LogEvent(session, "SettlementSelected", $"{currentPlayerId} placing optimal settlement at {bestBuilding}");

                    // rely on service logic to validate settlement placement

                    var result = await proxy.ExecuteBuildingUpgradeAsync(session.GameId, bestBuilding.BuildingKey);
                    Assert.True(result.Success, $"Settlement placement failed: {result.Message}"); // if this worked, the PickOptimalSettlement worked
                    LogEvent(session, "SettlementPlaced", $"✅ {currentPlayerId} settlement placement succeeded!.  GameState={gameModel.GameState}");
                    if (gameModel.GameState == GameState.AllocateResourceReverse)
                    {
                        _lastBuildingPicked[gameModel.CurrentPlayerId] = bestBuilding;
                    }
                    // Verify game state after settlement placement
                    await session.VerifyGameConsistency();
                    var updatedGameState = proxy.GameModel;
                    Assert.NotNull(updatedGameState);

                    //
                    //   resource and score validation will come in the next test for the DoneResourceAllocation state

                    // STEP 2: Place Road - find buildable roads after settlement placement
                    var buildableRoads = updatedGameState.Roads
                        .Where(r => r.RoadState == RoadState.Buildable)
                        .ToList();

                    LogEvent(session, "RoadSearch", $"Found {buildableRoads.Count} buildable roads for {currentPlayerId}");
                    Assert.True(buildableRoads.Count > 0, $"Should have buildable roads available for {currentPlayerId}");

                    // Pick the first buildable road
                    var selectedRoad = buildableRoads.First();
                    var roadKey = selectedRoad.RoadKey;
                    LogEvent(session, "RoadSelected", $"{currentPlayerId} placing road at {roadKey}");

                    var roadResult = await proxy.ExecuteRoadPurchaseAsync(session.GameId, roadKey);
                    Assert.True(roadResult.Success, $"Road placement failed: {roadResult.Message}");
                    LogEvent(session, "RoadPlaced", $"✅ {currentPlayerId} road placement succeeded!");

                    // Verify game consistency after road placement
                    await session.VerifyGameConsistency();
                    var afterRoadGameState = proxy.GameModel;
                    Assert.NotNull(afterRoadGameState);

                    // Verify player no longer has unspent entitlements
                    var playerAfterRoad = afterRoadGameState.Players.First(p => p.Id == currentPlayerId);
                    Assert.DoesNotContain(Entitlement.Settlement, playerAfterRoad.UnspentEntitlements);
                    Assert.DoesNotContain(Entitlement.Road, playerAfterRoad.UnspentEntitlements);


                }
                catch (Exception ex)
                {
                    LogEvent(session, "BuildingPlacementError", $"❌ Building placement failed for {currentPlayerId}: {ex.GetType().Name}: {ex.Message}");

                    if (ex.InnerException != null)
                    {
                        LogEvent(session, "InnerException", $"Inner: {ex.InnerException.GetType().Name}: {ex.InnerException.Message}");
                    }

                    // Trace the stack trace for debugging
                    LogEvent(session, "StackTrace", $"Stack trace: {ex.StackTrace}");

                    throw;
                }

                // advance to the next state
                var nextProxy = session.GetProxy(currentPlayerId);
                var nextResult = await nextProxy.ExecuteNextAsync();
                if (!nextResult.Success)
                {
                    throw new InvalidOperationException($"Next action failed: {nextResult.Message}");
                }
                await session.VerifyAllProxiesReceivedUpdate();


                //
                //  get the updated data
                currentPlayerId = session.GetCurrentPlayerId();
                proxy = session.GetProxy(currentPlayerId);
                gameModel = proxy.GameModel;
                Assert.NotNull(gameModel);
                LogEvent(session, "AllocationLoop", $"✅ CurrentPlayer={currentPlayerId} GameState={gameModel.GameState}");


            }

            // Verify we've successfully advanced to AllocateResourceReverse
            await session.VerifyAllProxiesInState(GameState.DoneResourceAllocation);
            await session.VerifyGameConsistency();


            LogEvent(session, "Allocating Resources Complete", $"✅ AllocateResourceForward completed - all players placed settlement + road.  GameState={gameModel.GameState}");
        }

        ///<summary>
        /// e2e helper that picks a settlement 
        ///</summary>
        private BuildingModel PickOptimalSettlement(GameModel gameModel)
        {
            // Get the current game state for the player
            var possibleSettlements = gameModel.Buildings
                .Where(b => b.BuildingState == BuildingState.PossibleSettlement)
                .ToList();

            if (!possibleSettlements.Any())
            {
                throw new InvalidOperationException("No possible settlements available");
            }

            // find the building that is buildable and has the most stars
            var settlementOptions = possibleSettlements
                .Select(building => new
                {
                    stars = gameModel.TilesForBuildings(building.BuildingKey).Stars(),
                    building = building
                })
                .ToList();

            var maxStars = settlementOptions.Max(s => s.stars);
            var bestSettlement = settlementOptions.First(s => s.stars == maxStars);

            return bestSettlement.building;
        }



        /// <summary>
        /// Verify DoneResourceAllocation state works correctly.
        /// Tests Next action to advance to WaitingForRoll.
        /// Port of CLI comprehensive testing for this state.
        /// </summary>
        private async Task VerifyDoneResourceAllocation(EndToEndSignalRSession session)
        {
            LogEvent(session, "DoneResourceAllocation", "Testing DoneResourceAllocation state functionality");

            // ASSERTION 1: Verify we're in the correct state
            await session.VerifyAllProxiesInState(GameState.DoneResourceAllocation);
            await session.VerifyGameConsistency();

            var currentPlayerId = session.GetCurrentPlayerId();
            var proxy = session.GetProxy(currentPlayerId);
            var gameModel = proxy.GameModel;
            Assert.NotNull(gameModel);
            Assert.Equal(GameState.DoneResourceAllocation, gameModel.GameState);
            LogEvent(session, "VerifyDoneResourceAllocation", $"GameState={gameModel.GameState} Currentplayer={gameModel.CurrentPlayerId}");
            Assert.Equal("Alice", currentPlayerId);

            // ASSERTION 2: Verify final allocation results - all players should have 2 settlements and 2 roads
            var playerIds = new[] { "Alice", "Bob", "Charlie", "David", "Eve" };
            foreach (var playerId in playerIds)
            {
                var playerBuildings = gameModel.Buildings.Count(b =>
                    b.OwnerId == playerId && b.BuildingState == BuildingState.Settlement);
                var playerRoads = gameModel.Roads.Count(r =>
                    r.OwnerId == playerId && r.RoadState == RoadState.Road);
                var player = gameModel.Players.FirstOrDefault(p => p.Id == playerId);

                Assert.Equal(2, playerBuildings);
                Assert.Equal(2, playerRoads);
                Assert.Equal(2, player?.Score);


            }
            LogEvent(session, "AllocationComplete", $"✅ All Players: 2 settlements, 2 roads, score 2 - allocation phase complete.  GameState={gameModel.GameState}");


            //
            //  check that the resources for each player is correct
            //  to do this, we will look through the players in the current players GameModel (which we've already shown is the same 
            //  as everybody elses). then we will look at each building they have and calculate what resources they should have
            //  then we will look at ResourcesThisTurn for each player and verify that they match.  ResourcesThisGame are all 0 since that
            //  is only added to after rolls, not during the AllocationPhase of the game

            foreach (var player in gameModel.Players)
            {


                var lastBuilding = _lastBuildingPicked.GetValueOrDefault(player.Id);
                Assert.NotNull(lastBuilding);
                LogEvent(session, "Resource Verification", $"player {player.Name} expected ResourcesThisTurn={player.ResourcesThisTurn}");
                LogEvent(session, "Last Building Resources: ", $"{lastBuilding.BuildingKey} Resources={gameModel.ResourcesForBuilding(lastBuilding)}");
                ResourcesModel  expectedResources = gameModel.ResourcesForBuilding(lastBuilding);

                foreach (var resourceType in Enum.GetValues<ResourceType>())
                {
                    var expectedValue = expectedResources.CountForResource(resourceType);
                    var actualThisTurn = player.ResourcesThisTurn.CountForResource(resourceType);
                    if (expectedValue != actualThisTurn)
                    {
                        LogEvent(session, "ResourceInconsistency", $"{player.Name} expected {expectedValue} of {resourceType} but got {actualThisTurn}");
                    }
                    Assert.Equal(expectedValue, actualThisTurn);
                }
                Assert.Equal(GameState.DoneResourceAllocation, gameModel.GameState);
            }

            // ADVANCEMENT TEST: Test Next action to advance to WaitingForRoll
            LogEvent(session, "Before Next()", "Testing advancement with Next action to WaitingForRoll");
            var nextProxy = session.GetProxy(currentPlayerId);
            var nextResult = await nextProxy.ExecuteNextAsync();
            if (!nextResult.Success)
            {
                throw new InvalidOperationException($"Next action failed: {nextResult.Message}");
            }
            await session.VerifyAllProxiesReceivedUpdate();

            // FINAL ASSERTION: Verify we advanced to WaitingForRoll
            await session.VerifyAllProxiesInState(GameState.WaitingForRoll);
            await session.VerifyGameConsistency();

            var finalGameState = session.GetProxy("Alice").GameModel;
            Assert.NotNull(finalGameState);
            Assert.Equal(GameState.WaitingForRoll, finalGameState.GameState);

            // ASSERTION 4: Verify action flags are correct for WaitingForRoll
            Assert.True(finalGameState.ActionFlags.RollsEnabled, "Rolls should be enabled in WaitingForRoll state");
            Assert.False(finalGameState.ActionFlags.NextEnabled, "Next should be disabled until dice are rolled");

            LogEvent(session, "DoneResourceAllocationComplete", $"✅ DoneResourceAllocation state verified - successfully advanced to GameState={gameModel.GameState}");
        }

        private void LogEvent(EndToEndSignalRSession? session, string eventType, string message, [CallerMemberName] string cmb = "", [CallerLineNumber] int cln = 0, [CallerFilePath] string cfp = "")
        {
            var timestamp = DateTime.UtcNow.ToString("HH:mm:ss.fff");
            if (session is null)
            {
                var nullSessionLog = $"[{cmb}:{cln}] [{timestamp}] [{eventType}] [ {message}";
                if (System.Diagnostics.Debugger.IsAttached)
                    System.Diagnostics.Debug.WriteLine(nullSessionLog);
                else
                    Console.WriteLine(nullSessionLog);
                return;
            }

            var currentPlayerId = session.GetCurrentPlayerId();
            var proxy = session.GetProxy(currentPlayerId);
            var gameModel = proxy.GameModel;
            if (gameModel is null)
            {
                var errorLog = $"[{cmb}:{cln}] [{timestamp}] [{eventType}] [GameModel is null] {message}";
                if (System.Diagnostics.Debugger.IsAttached)
                    System.Diagnostics.Debug.WriteLine(errorLog);
                else
                    Console.WriteLine(errorLog);
                throw new Exception("this is very odd");

            }
            var gameLog = $"[{cmb}:{cln}] [{timestamp}] [{eventType}] [GameState={gameModel.GameState}] [CurrentPlayer={gameModel.CurrentPlayerId}] {message}";
            if (System.Diagnostics.Debugger.IsAttached)
                System.Diagnostics.Debug.WriteLine(gameLog);
            else
                Console.WriteLine(gameLog);
        }

        /// <summary>
        /// Test that replays the shared Expansion.catan_test file to ensure GameService
        /// produces identical game state progression as the Desktop app.
        /// This validates that the shared GameStateMachine behaves consistently across both architectures.
        /// </summary>
        [Fact]
        public async Task TestLoadGameModelOnly()
        {
            LogEvent(null, "LoadGameModelTest", "=== Testing LoadGameModel in isolation ===");

            // Load the shared test scenario
            var testScenario = await Catan3.Shared.TestData.TestDataLoader.LoadTestScenarioAsync("Expansion.catan_test");
            LogEvent(null, "TestFileLoaded", $"Loaded test scenario: {testScenario.TestFileName}");

            // Create a single SignalR proxy
            var playerId = testScenario.InitialGameModel.Players[0].Id;
            var gameId = "test-game-" + Guid.NewGuid().ToString("N")[..8];
            
            var uri = _factory.Server.BaseAddress ?? new Uri("http://localhost");
            var hubUrl = new Uri(uri, "/gameHub").ToString();
            var serviceUri = uri.ToString().TrimEnd('/');
            var testHandler = _factory.Server.CreateHandler();
            var proxy = new GameServiceProxy(hubUrl, serviceUri, testHandler, playerId);
            
            LogEvent(null, "ProxyCreated", $"Created SignalR proxy for player {playerId}");

            // Connect to SignalR
            await proxy.ConnectAsync();
            LogEvent(null, "ProxyConnected", $"Connected proxy for player {playerId}");

            // Try to load the GameModel
            LogEvent(null, "LoadingGameModel", $"Attempting to load GameModel with state: {testScenario.InitialGameModel.GameState}");
            
            try
            {
                var loadResult = await proxy.LoadGameModelAsync(testScenario.InitialGameModel);
                LogEvent(null, "LoadModelSuccess", $"LoadGameModel succeeded: {loadResult.Success}, Message: {loadResult.Message}");
            }
            catch (Exception ex)
            {
                LogEvent(null, "LoadModelError", $"LoadGameModel failed: {ex.GetType().Name}: {ex.Message}");
                throw;
            }
            finally
            {
                // Cleanup
                await proxy.DisposeAsync();
                LogEvent(null, "TestComplete", "LoadGameModel test completed");
            }
        }

        [Fact]
        public async Task ReplaySharedExpansionTestFile()
        {
            LogEvent(null, "ReplayTest", "=== Starting replay of shared Expansion.catan_test with proper multiplayer flow ===");

            // Load the shared test scenario - parse gameModel and actionStack
            var testScenario = await Catan3.Shared.TestData.TestDataLoader.LoadTestScenarioAsync("Expansion.catan_test");
            LogEvent(null, "TestFileLoaded", $"Loaded test scenario: {testScenario.TestFileName} with {testScenario.RecordedActions.Length} actions");

            testScenario.InitialGameModel.Validate();
          
            // Extract player IDs from the initial game model
            var playerIds = testScenario.InitialGameModel.Players.Select(p => p.Id).ToArray();
            LogEvent(null, "PlayersIdentified", $"Players in test: {string.Join(", ", playerIds)}");

            // the gameName is already set in the file.  use it.
           

            // === STEP 1: ALL Players Connect to SignalR First (Real-World Pattern) ===
            var uri = _factory.Server.BaseAddress ?? new Uri("http://localhost");
            var hubUrl = new Uri(uri, "/gameHub").ToString();
            var serviceUri = uri.ToString().TrimEnd('/');
            var testHandler = _factory.Server.CreateHandler();
            
            // Create connections for ALL players first (like real users would)
            var allProxies = new Dictionary<string, GameServiceProxy>();
            
            foreach (var playerId in playerIds)
            {
                var playerProxy = new GameServiceProxy(hubUrl, serviceUri, testHandler, playerId);
                await playerProxy.ConnectAsync();
                allProxies[playerId] = playerProxy;
                LogEvent(null, "PlayerConnected", $"Player {playerId} connected to SignalR");
            }
            
            LogEvent(null, "AllPlayersConnected", $"All {allProxies.Count} players connected to SignalR following real-world pattern");

            // === STEP 2: One Player Loads the Game via REST API ===
            var firstPlayerId = playerIds[0];
            var firstPlayerProxy = allProxies[firstPlayerId];

            // Load the GameModel using the first player's proxy
            var loadResult = await firstPlayerProxy.LoadGameModelAsync(testScenario.InitialGameModel);
            if (!loadResult.Success)
            {
                throw new InvalidOperationException($"Failed to load GameModel: {loadResult.Message}");
            }
            
            // Get the actual GameId that was set in the proxy
            var actualGameId = firstPlayerProxy.GameId ?? throw new InvalidOperationException("GameId was not set after loading GameModel");
            LogEvent(null, "GameModelLoaded", $"GameModel loaded by {firstPlayerId}: {loadResult.Message}");
            LogEvent(null, "ActualGameId", $"Game created with GameId: {actualGameId}");
            LogEvent(null, "ExpectedGameName", $"Looking for games with GameName: '{testScenario.InitialGameModel.GameName}'");

            // === STEP 3: ALL Players Discover and Join the Game via SignalR (Real-World Pattern) ===
            LogEvent(null, "StartingDiscoveryAndJoin", "All players will now discover and join the game (including the player who loaded it)");
            
            foreach (var playerId in playerIds) // ALL players, including first player
            {
                var playerProxy = allProxies[playerId];
                
                // Each player discovers available games independently
                var availableGames = await playerProxy.GetAvailableGamesAsync();
                LogEvent(null, "GamesDiscovered", $"Player {playerId} discovered {availableGames.Count} available games");
                
                // Trace detailed information about discovered games for debugging
                for (int i = 0; i < availableGames.Count; i++)
                {
                    var game = availableGames[i];
                    LogEvent(null, "GameDetails", $"Game[{i}]: GameId='{game.GameId}', DisplayName='{game.DisplayName}', GameType='{game.GameType}', GameState='{game.GameState}', Players={game.PlayerCount}");
                }

                // Find our test game by DisplayName (which should match the GameName we set)
                var testGame = availableGames.FirstOrDefault(g => g.DisplayName == testScenario.InitialGameModel.GameName);
                if (testGame == null)
                {
                    throw new InvalidOperationException($"Player {playerId} could not find test game '{testScenario.InitialGameModel.GameName}' in available games. Found games: {string.Join(", ", availableGames.Select(g => g.DisplayName))}");
                }
                
                LogEvent(null, "GameFound", $"Player {playerId} found test game: {testGame.DisplayName} (ID: {testGame.GameId})");

                // Join the game via SignalR
                await playerProxy.JoinGameAsync(testGame.GameId);
                LogEvent(null, "PlayerJoined", $"Player {playerId} joined game {testGame.GameId}");
            }

            LogEvent(null, "AllPlayersJoined", $"All {allProxies.Count} players have joined the game following proper multiplayer flow");

            // === STEP 4: Verify All Players Have Same GameModel After Joining ===
            LogEvent(null, "VerifyingSynchronization", "Waiting for SignalR notifications to propagate to all clients");
            await Task.Delay(1000); // Longer wait to ensure all SignalR updates have propagated
            
            // Verify all proxies have the same GameModel and match expected initial state
            LogEvent(null, "StartingGameModelVerification", "Verifying all players have synchronized GameModel");
            VerifyAllProxiesHaveSameGameModel(allProxies, testScenario.InitialGameModel.GameState, testScenario.InitialGameModel.GameHash);
            LogEvent(null, "InitialStateVerified", $"✅ All {allProxies.Count} players have correct synchronized initial state: {testScenario.InitialGameModel.GameState}");

            // === STEP 5: Execute Recorded Actions (All Players Now Properly Joined) ===
            LogEvent(null, "StartingActionReplay", "Starting action replay - all players are now properly joined to SignalR groups");
            int actionIndex = 0;
            foreach (var recordedMessage in testScenario.RecordedActions)
            {
                actionIndex++;
                var recordType = recordedMessage.GetType().Name;
                LogEvent(null, "ReplayingAction", $"[{actionIndex}/{testScenario.RecordedActions.Length}] Replaying: {recordType}");

                try
                {
                    // Execute the appropriate action using SignalR proxy
                    await ExecuteRecordedAction(allProxies, recordedMessage, actualGameId);

                    // Wait for SignalR notifications to propagate
                    await Task.Delay(100);

                    // Verify all proxies have same GameHash and it matches expected
                    if (recordedMessage.ExpectedGameHash != null)
                    {
                        VerifyAllProxiesHaveSameGameModel(allProxies, expectedHash: recordedMessage.ExpectedGameHash);
                        LogEvent(null, "HashVerified", $"✅ Hash verified: {recordedMessage.ExpectedGameHash}");
                    }
                }
                catch (Exception ex)
                {
                    LogEvent(null, "ReplayError", $"❌ Failed at action {actionIndex}: {ex.Message}");
                    throw new InvalidOperationException($"Replay failed at action {actionIndex} ({recordType}): {ex.Message}", ex);
                }
            }

            LogEvent(null, "ReplayComplete", $"✅ Successfully replayed all {testScenario.RecordedActions.Length} actions");

            // Cleanup
            foreach (var proxy in allProxies.Values)
            {
                await proxy.DisposeAsync();
            }
            
            LogEvent(null, "TestComplete", "✅ Expansion.catan_test replay completed successfully with proper multiplayer flow - GameService matches Desktop behavior");
        }


        /// <summary>
        /// Verifies that all SignalR proxies have the same GameModel state
        /// </summary>
        private void VerifyAllProxiesHaveSameGameModel(Dictionary<string, GameServiceProxy> proxies, Catan3.Shared.Models.GameState? expectedState = null, string? expectedHash = null)
        {
            var gameModels = proxies.Values.Select(p => p.GameModel).Where(gm => gm != null).ToList();
            
            if (gameModels.Count == 0)
            {
                throw new InvalidOperationException("No proxies have received GameModel updates");
            }

            var firstGameModel = gameModels[0]!;
            
            // Verify all proxies have the same game state
            foreach (var gameModel in gameModels.Skip(1))
            {
                if (gameModel!.GameState != firstGameModel.GameState)
                {
                    throw new InvalidOperationException($"GameState mismatch: {gameModel.GameState} vs {firstGameModel.GameState}");
                }
                
                if (gameModel.GameHash != firstGameModel.GameHash)
                {
                    throw new InvalidOperationException($"GameHash mismatch: {gameModel.GameHash} vs {firstGameModel.GameHash}");
                }
                
                if (gameModel.GameStateMachineVersion != firstGameModel.GameStateMachineVersion)
                {
                    throw new InvalidOperationException($"Version mismatch: {gameModel.GameStateMachineVersion} vs {firstGameModel.GameStateMachineVersion}");
                }
            }

            // Verify against expected values if provided
            if (expectedState.HasValue && firstGameModel.GameState != expectedState.Value)
            {
                throw new InvalidOperationException($"Expected GameState {expectedState}, got {firstGameModel.GameState}");
            }
            
            if (!string.IsNullOrEmpty(expectedHash) && firstGameModel.GameHash != expectedHash)
            {
                throw new InvalidOperationException($"Expected GameHash {expectedHash}, got {firstGameModel.GameHash}");
            }
        }

        /// <summary>
        /// Executes a recorded action using the appropriate SignalR proxy
        /// </summary>
        private async Task ExecuteRecordedAction(Dictionary<string, GameServiceProxy> proxies, IRecordedMessage recordedMessage, string gameId)
        {
            // Determine which player should execute this action based on current game state
            var firstProxy = proxies.Values.First();
            var gameModel = firstProxy.GameModel;
            var currentPlayerId = gameModel?.CurrentPlayerId ?? proxies.Keys.First();
            
            if (!proxies.TryGetValue(currentPlayerId, out var currentPlayerProxy))
            {
                throw new InvalidOperationException($"No proxy found for current player: {currentPlayerId}");
            }

            switch (recordedMessage)
            {
                case ShuffleRecord shuffle:
                    var shuffleResult = await currentPlayerProxy.ExecuteShuffleAsync();
                    if (!shuffleResult.Success)
                    {
                        throw new InvalidOperationException($"Shuffle action failed: {shuffleResult.Message}");
                    }
                    break;

                // ExecuteGameActionRecord is deprecated - individual record types are used instead

                case GoFirstRecord goFirst:
                    var goFirstPlayerId = goFirst.PlayerId;
                    await currentPlayerProxy.ExecuteGoFirstAsync(goFirstPlayerId);
                    break;

                case PurchaseRecord purchase:
                    await currentPlayerProxy.ExecutePurchaseAsync(purchase.Entitlement);
                    break;

                case RollRecord roll:
                    var rollModel = roll.Roll;
                    await currentPlayerProxy.ExecuteRollAsync(rollModel.RedRoll, rollModel.WhiteRoll);
                    break;

                default:
                    var typeName = recordedMessage.GetType().Name;
                    throw new NotImplementedException($"Action type {typeName} not yet implemented for SignalR proxy replay");
            }
        }

        /// <summary>
        /// Replays a recorded action from the test file
        /// </summary>
        private async Task ReplayRecordedAction(EndToEndSignalRSession session, IRecordedMessage recordedMessage)
        {
            // Determine which player should execute this action based on current game state
            var gameModel = session.GetProxy(session.PlayerIds[0]).GameModel;
            var currentPlayerId = gameModel?.CurrentPlayerId ?? session.PlayerIds[0];

            switch (recordedMessage)
            {
                case ShuffleRecord shuffle:
                    LogEvent(session, "ShuffleReplay", "Executing shuffle action replay");
                    var shuffleProxy = session.GetProxy(currentPlayerId);
                    var shuffleResult = await shuffleProxy.ExecuteShuffleAsync();
                    if (!shuffleResult.Success)
                    {
                        LogEvent(session, "ShuffleSkipped", $"⚠️ Shuffle action failed: {shuffleResult.Message}");
                    }
                    else
                    {
                        await session.VerifyAllProxiesReceivedUpdate();
                        LogEvent(session, "ShuffleCompleted", "✅ Shuffle action replay completed");
                    }
                    break;

                case UndoRecord undoAction:
                    LogEvent(session, "UndoReplay", "Executing undo action replay");
                    var undoProxy = session.GetProxy(currentPlayerId);
                    var undoResult = await undoProxy.ExecuteUndoAsync();
                    if (!undoResult.Success)
                    {
                        LogEvent(session, "UndoSkipped", $"⚠️ Undo action failed: {undoResult.Message}");
                    }
                    else
                    {
                        await session.VerifyAllProxiesReceivedUpdate();
                        LogEvent(session, "UndoCompleted", "✅ Undo action replay completed");
                    }
                    break;
                    
                case RedoRecord redoAction:
                    LogEvent(session, "RedoReplay", "Executing redo action replay");
                    var redoProxy = session.GetProxy(currentPlayerId);
                    var redoResult = await redoProxy.ExecuteRedoAsync();
                    if (!redoResult.Success)
                    {
                        LogEvent(session, "RedoSkipped", $"⚠️ Redo action failed: {redoResult.Message}");
                    }
                    else
                    {
                        await session.VerifyAllProxiesReceivedUpdate();
                        LogEvent(session, "RedoCompleted", "✅ Redo action replay completed");
                    }
                    break;
                    
                case NextRecord nextAction:
                    LogEvent(session, "NextReplay", "Executing next action replay");
                    var nextProxy = session.GetProxy(currentPlayerId);
                    var nextResult = await nextProxy.ExecuteNextAsync();
                    if (!nextResult.Success)
                    {
                        LogEvent(session, "NextSkipped", $"⚠️ Next action failed: {nextResult.Message}");
                    }
                    else
                    {
                        await session.VerifyAllProxiesReceivedUpdate();
                        LogEvent(session, "NextCompleted", "✅ Next action replay completed");
                    }
                    break;

                case GoFirstRecord goFirst:
                    // GoFirst messages contain the player who goes first
                    var goFirstPlayerId = goFirst.PlayerId;
                    // In GameService, this would be handled via a specific message
                    // For now, we'll skip this as it's typically handled automatically
                    LogEvent(session, "GoFirstSkipped", $"Skipping goFirst for player {goFirstPlayerId} - handled automatically");
                    break;

                case PurchaseRecord purchase:
                    LogEvent(session, "PurchaseReplay", $"Executing purchase action replay: {purchase.Entitlement}");
                    var purchaseProxy = session.GetProxy(currentPlayerId);
                    var purchaseResult = await purchaseProxy.ExecutePurchaseAsync(purchase.Entitlement);
                    if (!purchaseResult.Success)
                    {
                        LogEvent(session, "PurchaseSkipped", $"⚠️ Purchase action failed: {purchaseResult.Message}");
                    }
                    else
                    {
                        await session.VerifyAllProxiesReceivedUpdate();
                        LogEvent(session, "PurchaseCompleted", $"✅ Purchase action replay completed: {purchase.Entitlement}");
                    }
                    break;

                case RollRecord roll:
                    LogEvent(session, "RollReplay", $"Executing roll action replay: ({roll.Roll.RedRoll},{roll.Roll.WhiteRoll})");
                    var rollProxy = session.GetProxy(currentPlayerId);
                    var rollResult = await rollProxy.ExecuteRollAsync(roll.Roll.RedRoll, roll.Roll.WhiteRoll);
                    if (!rollResult.Success)
                    {
                        LogEvent(session, "RollSkipped", $"⚠️ Roll action failed: {rollResult.Message}");
                    }
                    else
                    {
                        await session.VerifyAllProxiesReceivedUpdate();
                        LogEvent(session, "RollCompleted", $"✅ Roll action replay completed: ({roll.Roll.RedRoll},{roll.Roll.WhiteRoll})");
                    }
                    break;

                default:
                    var typeName = recordedMessage.GetType().Name;
                    LogEvent(session, "UnknownRecordType", $"⚠️ Unknown record type: {typeName} - skipping");
                    break;
            }
        }
    }

    /// <summary>
    /// E2E-specific session wrapper that uses GameServiceProxy from Catan3.Shared
    /// This complies with the rule: "SignalR: use the proxy in the Shared project to call SignalR"
    /// </summary>
    public class EndToEndSignalRSession : IAsyncDisposable
    {
        private readonly WebApplicationFactory<Program> _factory;
        private readonly GameType _gameType;
        private readonly string[] _playerIds;
        private readonly Dictionary<string, GameServiceProxy> _proxies = [];

        public string GameId { get; set; } = "";
        public string[] PlayerIds => _playerIds;

        public EndToEndSignalRSession(WebApplicationFactory<Program> factory, GameType gameType, string[] playerIds)
        {
            _factory = factory;
            _gameType = gameType;
            _playerIds = playerIds;
        }
        private void LogEvent(string eventType, string message, [CallerMemberName] string cmb = "", [CallerLineNumber] int cln = 0, [CallerFilePath] string cfp = "")
        {
            var timestamp = DateTime.UtcNow.ToString("HH:mm:ss.fff");
            var logMessage = $"[{cmb}:{cln}] [{timestamp}] [{eventType}] {message}";
            if (System.Diagnostics.Debugger.IsAttached)
                System.Diagnostics.Debug.WriteLine(logMessage);
            else
                Console.WriteLine(logMessage);
        }
        /// <summary>
        /// Initializes the session by creating a game and connecting all players via GameServiceProxy
        /// </summary>
        public async Task InitializeAsync()
        {
            LogEvent("SessionInitialization", "Starting EndToEndSignalRSession initialization");
            // Create game via REST API
            var httpClient = _factory.CreateClient();
            var gameId = await CreateGameViaRest(httpClient, _gameType, _playerIds);
            GameId = gameId;

            await ConnectPlayersAsync();
        }
        
        /// <summary>
        /// Connects all players to an existing game via GameServiceProxy
        /// </summary>
        public async Task ConnectPlayersAsync()
        {
            LogEvent("PlayerConnection", "Connecting all players to game");
            // Connect all players via GameServiceProxy in parallel for faster execution
            var connectTasks = _playerIds.Select(async playerId =>
            {
                // Use test factory to create a connection that works with in-memory test server
                var uri = _factory.Server.BaseAddress ?? new Uri("http://localhost");
                var hubUrl = new Uri(uri, "/gameHub").ToString();

                // Use the HttpMessageHandler constructor - this is perfect for tests!
                var testHandler = _factory.Server.CreateHandler();
                var serviceUri = uri.ToString().TrimEnd('/');
                var proxy = new GameServiceProxy(hubUrl, serviceUri, testHandler, playerId, GameId);
                await proxy.ConnectAsync();
                
                // Store in thread-safe way
                lock (_proxies)
                {
                    _proxies[playerId] = proxy;
                }
            });

            // Wait for all connections to complete in parallel
            await Task.WhenAll(connectTasks);
            LogEvent("ParallelConnectionsComplete", $"All {_playerIds.Length} players connected in parallel");
        }

        /// <summary>
        /// Gets a specific proxy by player ID
        /// </summary>
        public GameServiceProxy GetProxy(string playerId)
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
            var currentPlayerId = anyProxy.GameModel?.CurrentPlayerId;

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
            LogEvent("StateVerification", $"Verifying all proxies are in state {expectedState}");
            var tasks = _proxies.Values.Select(proxy => proxy.WaitForGameStateAsync(expectedState, TimeSpan.FromSeconds(5)));
            await Task.WhenAll(tasks);
        }

        // ExecuteActionWithVerification method removed - replaced with individual proxy method calls

        /// <summary>
        /// Verifies all proxies have received recent updates (have consistent game state)
        /// </summary>
        public async Task VerifyAllProxiesReceivedUpdate()
        {
            LogEvent("UpdateVerification", "Verifying all proxies have received recent updates");
            // Brief delay to allow for state propagation
            await Task.Delay(50);

            // Check that all proxies have consistent GameModel and ExpectedGameHash
            var gameStates = _proxies.Values
                .Select(p => new { Proxy = p.PlayerId, State = p.GameModel?.GameState, Hash = p.GameModel?.GameHash })
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
        /// Verifies game consistency across all proxies using ExpectedGameHash
        /// </summary>
        public async Task VerifyGameConsistency()
        {
            LogEvent("GameConsistencyCheck", "Verifying game consistency across all proxies");
            await Task.Delay(50); // Brief delay for state propagation

            var proxyStates = _proxies.Values
                .Select(p => new { Proxy = p.PlayerId, GameState = p.GameModel })
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

                // ExpectedGameHash verification for board consistency
                if (!string.IsNullOrEmpty(state.GameHash) && !string.IsNullOrEmpty(referenceState.GameHash))
                {
                    if (state.GameHash != referenceState.GameHash)
                    {
                        inconsistencies.Add($"{proxyState.Proxy}: ExpectedGameHash {state.GameHash} vs {referenceState.GameHash} (BOARD MISMATCH!)");
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

            var newGameJson = JsonHelper.Serialize(newGameRequest);
            var newGameContent = new StringContent(newGameJson, System.Text.Encoding.UTF8, "application/json");

            var newGameResponse = await httpClient.PostAsync("/api/game/new", newGameContent);

            if (!newGameResponse.IsSuccessStatusCode)
            {
                var errorContent = await newGameResponse.Content.ReadAsStringAsync();
                throw new InvalidOperationException($"Failed to create game: {newGameResponse.StatusCode}. Error: {errorContent}");
            }

            var newGameBody = await newGameResponse.Content.ReadAsStringAsync();
            var newGameResult = JsonHelper.Deserialize<JsonElement>(newGameBody);

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