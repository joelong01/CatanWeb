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
            LogEvent(session, "InitialHash", $"Initial GameHash: {initialHash}");

            // Execute first shuffle
            await session.ExecuteActionWithVerification(currentPlayerId, GameAction.Shuffle);
            var firstShuffleState = session.GetProxy("Alice").GameModel;
            var firstShuffleHash = firstShuffleState?.GameHash;
            Assert.NotNull(firstShuffleHash);
            Assert.NotEqual(initialHash, firstShuffleHash);
            LogEvent(session, "FirstShuffle", $"After first shuffle: {firstShuffleHash}");

            // Execute second shuffle
            await session.ExecuteActionWithVerification(currentPlayerId, GameAction.Shuffle);
            var secondShuffleState = session.GetProxy("Alice").GameModel;
            var secondShuffleHash = secondShuffleState?.GameHash;
            Assert.NotNull(secondShuffleHash);
            Assert.NotEqual(firstShuffleHash, secondShuffleHash);
            LogEvent(session, "SecondShuffle", $"After second shuffle: {secondShuffleHash}");

            // Test Undo functionality
            await session.ExecuteActionWithVerification(currentPlayerId, GameAction.Undo);
            var undoState = session.GetProxy("Alice").GameModel;
            var undoHash = undoState?.GameHash;
            Assert.Equal(firstShuffleHash, undoHash);
            Assert.True(undoState?.ActionFlags.RedoEnabled);
            LogEvent(session, "UndoVerified", "✅ Undo restored previous state correctly");

            // Test Redo functionality
            await session.ExecuteActionWithVerification(currentPlayerId, GameAction.Redo);
            var redoState = session.GetProxy("Alice").GameModel;
            var redoHash = redoState?.GameHash;
            Assert.Equal(secondShuffleHash, redoHash);
            LogEvent(session, "RedoVerified", "✅ Redo restored forward state correctly");

            // Test Balance functionality (if available)
            try
            {
                await session.ExecuteActionWithVerification(currentPlayerId, GameAction.Balance);
                var balanceState = session.GetProxy("Alice").GameModel;
                Assert.NotNull(balanceState);
                LogEvent(session, "BalanceVerified", "✅ Balance action executed successfully");
            }
            catch (Exception ex) when (ex.Message.Contains("balance") || ex.Message.Contains("swap"))
            {
                LogEvent(session, "BalanceSkipped", "Balance action not available - expected for some board configurations");
            }

            // Advance to next state using Next action
            await session.ExecuteActionWithVerification(currentPlayerId, GameAction.Next);
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
            await session.ExecuteActionWithVerification(currentPlayerId, GameAction.Next);
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
            await session.ExecuteActionWithVerification(currentPlayerId, GameAction.Next);
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
            await session.ExecuteActionWithVerification(currentPlayerId, GameAction.Next);
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
                    var settlementKey = PickOptimalSettlement(gameModel);
                    LogEvent(session, "SettlementSelected", $"{currentPlayerId} placing optimal settlement at {settlementKey}");

                    // rely on service logic to validate settlement placement

                    var result = await proxy.ExecuteBuildingUpgradeAsync(session.GameId, settlementKey);
                    Assert.True(result.Success, $"Settlement placement failed: {result.Message}"); // if this worked, the PickOmptimalSettlement worked
                    LogEvent(session, "SettlementPlaced", $"✅ {currentPlayerId} settlement placement succeeded!.  GameState={gameModel.GameState}");

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

                    // Log the stack trace for debugging
                    LogEvent(session, "StackTrace", $"Stack trace: {ex.StackTrace}");

                    throw;
                }

                // advance to the next state

                await session.ExecuteActionWithVerification(currentPlayerId, GameAction.Next);


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

            // find the building that is buildable and has the most stars
            var settlementOptions = possibleSettlements
                .Select(building => new
                {
                    stars = gameModel.TilesForBuildings(building.BuildingKey).Stars(),
                    buildingKey = building.BuildingKey
                })
                .ToList();

            var maxStars = settlementOptions.Max(s => s.stars);
            var bestSettlement = settlementOptions.First(s => s.stars == maxStars);


            return bestSettlement.buildingKey;
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
            LogEvent(session, "VerifyDoneResourceAllocation", $"GameState={gameModel.GameState} CurrentPlaye={gameModel.CurrentPlayerId}");
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
                var playerBuildings = gameModel.Buildings.Where(b => b.OwnerId == player.Id && b.BuildingState == BuildingState.Settlement).ToList();
                var playerRoads = gameModel.Roads.Where(r => r.OwnerId == player.Id && r.RoadState == RoadState.Road).ToList();
                LogEvent(session, "Resource Verification", $"ResourcesThisTurn={player.ResourcesThisTurn}");
                // Calculate expected resources based on settlements - for settlement, we have to use the extension methods to find the tiles
                // associated with that settlement and then calculate the resources based on the tiles
                ResourcesModel  expectedResources = new ResourcesModel();
                var lastBuilding =   gameModel.Buildings.Where(b => b.OwnerId == player.Id).ToList().Last();

                var tiles = gameModel.TilesForBuildings (lastBuilding.BuildingKey);
                foreach (var tile in tiles)
                {
                    var resource = tile.ResourceTileType;
                    switch (resource)
                    {
                        case ResourceType.Brick:
                            expectedResources.Brick++;
                            break;
                        case ResourceType.Wood:
                            expectedResources.Wood++;
                            break;
                        case ResourceType.Sheep:
                            expectedResources.Sheep++;
                            break;
                        case ResourceType.Wheat:
                            expectedResources.Wheat++;
                            break;
                        case ResourceType.Ore:
                            expectedResources.Ore++;
                            break;
                        default:
                            Debug.Assert(false, $"Unexpected resource type {resource} for building {lastBuilding.BuildingKey}");
                            break;
                    }
                }


                // now the values for the ResourceTypes in expectedResources should match the ResourcesThisTurn
                foreach (var resourceType in Enum.GetValues<ResourceType>())
                {
                    var expectedValue = expectedResources.CountForResource(resourceType);
                    LogEvent(session, "ResourceVerification", $"{player.Id} expected {expectedValue} for {resourceType}");

                    var actualValueThisTurn = player.ResourcesThisTurn.CountForResource(resourceType);

                    Assert.Equal(expectedValue, actualValueThisTurn);

                }


                LogEvent(session, "ResourceVerification", $"{player.Id} has {player.ResourcesThisTurn} resources this turn)");
                foreach (var resourceType in Enum.GetValues<ResourceType>())
                {
                    Assert.Equal(expectedResources.CountForResource(resourceType), player.ResourcesThisTurn.CountForResource(resourceType));

                }


                Assert.Equal(GameState.DoneResourceAllocation, gameModel.GameState);


            }

            // ADVANCEMENT TEST: Test Next action to advance to WaitingForRoll
            LogEvent(session, "Before Next()", "Testing advancement with Next action to WaitingForRoll");
            await session.ExecuteActionWithVerification(currentPlayerId, GameAction.Next);

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
                Console.WriteLine($"[{cmb}:{cln}] [{timestamp}] [{eventType}] [ {message}");
                return;
            }

            var currentPlayerId = session.GetCurrentPlayerId();
            var proxy = session.GetProxy(currentPlayerId);
            var gameModel = proxy.GameModel;
            if (gameModel is null)
            {
                Console.WriteLine($"[{cmb}:{cln}] [{timestamp}] [{eventType}] [GameModel is null] {message}");
                throw new Exception("this is very odd");

            }
            Console.WriteLine($"[{cmb}:{cln}] [{timestamp}] [{eventType}] [GameState={gameModel.GameState}] [CurrentPlayer={gameModel.CurrentPlayerId}] {message}");
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
        private void LogEvent(string eventType, string message, [CallerMemberName] string cmb = "", [CallerLineNumber] int cln = 0, [CallerFilePath] string cfp = "")
        {
            var timestamp = DateTime.UtcNow.ToString("HH:mm:ss.fff");
            Console.WriteLine($"[{cmb}:{cln}] [{timestamp}] [{eventType}] {message}");
        }
        /// <summary>
        /// Initializes the session by creating a game and connecting all players via SignalRProxy
        /// </summary>
        public async Task InitializeAsync()
        {
            LogEvent("SessionInitialization", "Starting EndToEndSignalRSession initialization");
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

        /// <summary>
        /// Executes action and verifies all proxies receive updates
        /// </summary>
        public async Task ExecuteActionWithVerification(string playerId, GameAction action)
        {
            LogEvent("ActionExecution", $"Executing action {action} for player {playerId}");
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
            LogEvent("UpdateVerification", "Verifying all proxies have received recent updates");
            // Brief delay to allow for state propagation
            await Task.Delay(50);

            // Check that all proxies have consistent GameModel and GameHash
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
        /// Verifies game consistency across all proxies using GameHash
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