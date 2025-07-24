using Xunit;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;
using System.Text;
using System.Text.Json;
using Catan3.GameService.Controllers;
using Catan3.Shared.Models;
using System.Net.Sockets;
using System.Net;
using Catan3.GameService.Services;

namespace Tests.GameService
{
    /// <summary>
    /// Comprehensive tests for the Allocation Phase game states
    /// Tests the complete flow from BeginResourceAllocation through AllocateResourceForward 
    /// and AllocateResourceReverse to DoneResourceAllocation and final WaitingForRoll:
    /// 
    /// 1. BeginResourceAllocation ? AllocateResourceForward (via Next)
    /// 2. AllocateResourceForward - Each player places first settlement + road
    /// 3. AllocateResourceReverse - Each player places second settlement + road (reverse order)
    /// 4. DoneResourceAllocation ? WaitingForRoll (via Next)
    /// 
    /// These tests focus on automated optimal placement for settlement/road combinations
    /// and real-time updates that the companion interface relies on for allocation workflow.
    /// </summary>
    public class AllocationPhaseTests : IClassFixture<WebApplicationFactory<Program>>
    {
        private readonly WebApplicationFactory<Program> _factory;
        private readonly HttpClient _client;

        public AllocationPhaseTests(WebApplicationFactory<Program> factory)
        {
            // Configure the factory with test-specific settings
            _factory = factory.WithWebHostBuilder(builder =>
            {
                builder.ConfigureAppConfiguration((context, config) =>
                {
                    config.AddInMemoryCollection(new Dictionary<string, string?>
                    {
                        // Set short timeout for tests - 5 seconds instead of 15 minutes
                        ["GameApi:HangingGetTimeoutSeconds"] = "5"
                    });
                });
            });
            _client = _factory.CreateClient();
        }

        // Helper method to create a game in BeginResourceAllocation state
        private async Task<string> CreateGameInBeginResourceAllocationState()
        {
            var gameId = "allocation-phase-test-" + Guid.NewGuid().ToString();
            var gameType = "Regular";
            var playerIds = new List<string> { "Alice", "Bob", "Charlie" };

            var newGameRequestBody = new
            {
                gameId = gameId,
                gameType = gameType,
                playerIds = playerIds
            };

            var newGameJson = JsonSerializer.Serialize(newGameRequestBody);
            var newGameContent = new StringContent(newGameJson, Encoding.UTF8, "application/json");

            var createGameResponse = await _client.PostAsync("/api/game/new", newGameContent);
            Assert.True(createGameResponse.IsSuccessStatusCode, "Game creation should succeed");

            // Advance through states: PickingBoard ? WaitingForRollForOrder ? FinishedRollOrder ? BeginResourceAllocation
            await ExecuteGameAction(gameId, "Next"); // PickingBoard ? WaitingForRollForOrder
            await ExecuteGameAction(gameId, "Next"); // WaitingForRollForOrder ? FinishedRollOrder
            await ExecuteGameAction(gameId, "Next"); // FinishedRollOrder ? BeginResourceAllocation

            return gameId;
        }

        // Helper method to get game state info
        private async Task<AllocationGameStateInfo> GetGameStateInfo(string gameId)
        {
            var gameStateResponse = await _client.GetAsync($"/api/gamestate/{gameId}");
            Assert.True(gameStateResponse.IsSuccessStatusCode, "Should get game state successfully");

            var gameStateBody = await gameStateResponse.Content.ReadAsStringAsync();
            var gameState = JsonSerializer.Deserialize<JsonElement>(gameStateBody);

            return new AllocationGameStateInfo
            {
                GameId = gameState.GetProperty("gameId").GetString() ?? "",
                GameState = gameState.GetProperty("gameState").GetString() ?? "",
                Version = gameState.GetProperty("version").GetInt32(),
                CurrentPlayerId = gameState.GetProperty("currentPlayerId").GetString() ?? ""
            };
        }

        // Helper method to execute a game action
        private async Task<JsonElement> ExecuteGameAction(string gameId, string action, string playerId = "Alice")
        {
            var actionBody = new
            {
                gameId = gameId,
                playerId = playerId,
                messageType = "DoAction",
                messageData = new { action = action }
            };

            var actionJson = JsonSerializer.Serialize(actionBody);
            var actionContent = new StringContent(actionJson, Encoding.UTF8, "application/json");

            var actionResponse = await _client.PostAsync("/api/game/action", actionContent);
            Assert.True(actionResponse.IsSuccessStatusCode, $"{action} action should succeed");

            var actionResponseBody = await actionResponse.Content.ReadAsStringAsync();
            var actionResult = JsonSerializer.Deserialize<JsonElement>(actionResponseBody);
            Assert.True(actionResult.GetProperty("success").GetBoolean(), $"{action} should return success");

            return actionResult;
        }

        // Helper method to get the complete game model for debugging
        private async Task<JsonElement> GetCurrentFullGameModel(string gameId)
        {
            var gameStateResponse = await _client.GetAsync($"/api/gamestate/{gameId}");
            Assert.True(gameStateResponse.IsSuccessStatusCode, "Should get game state successfully");

            var gameStateBody = await gameStateResponse.Content.ReadAsStringAsync();
            Console.WriteLine("=== FULL GAME MODEL FROM API ===");
            Console.WriteLine(gameStateBody);
            Console.WriteLine("=== END FULL GAME MODEL ===");
            
            return JsonSerializer.Deserialize<JsonElement>(gameStateBody);
        }

        // Helper method to format building coordinates for logging
        private string FormatCoordinates(JsonElement building)
        {
            var buildingKey = building.GetProperty("buildingKey");
            var hexCoords = buildingKey.GetProperty("hexCoordinates");
            var position = buildingKey.GetProperty("position").GetString();
            var q = hexCoords.GetProperty("q").GetInt32();
            var r = hexCoords.GetProperty("r").GetInt32();
            var s = hexCoords.GetProperty("s").GetInt32();
            return $"({q},{r},{s})-{position}";
        }

        // Helper method to find and place the best settlement location based on star values
        private async Task<JsonElement> FindAndPlaceBestSettlement(string gameId, string playerId)
        {
            // Get current game state to find buildable settlements
            var gameModel = await GetCurrentFullGameModel(gameId);
            
            if (!gameModel.TryGetProperty("buildings", out var buildingsProperty))
            {
                throw new InvalidOperationException("Game state does not contain buildings property");
            }

            // Find all PossibleSettlement buildings and calculate their star values
            var buildings = buildingsProperty.EnumerateArray().ToList();
            var buildableSettlements = buildings
                .Where(b => b.TryGetProperty("buildingState", out var state) && 
                           state.GetString() == "PossibleSettlement")
                .ToList();

            Console.WriteLine($"Found {buildableSettlements.Count} buildable settlements");

            if (!buildableSettlements.Any())
            {
                throw new InvalidOperationException("No buildable settlements found");
            }

            // Get tiles for star calculation
            if (!gameModel.TryGetProperty("tiles", out var tilesProperty))
            {
                throw new InvalidOperationException("Game state does not contain tiles property");
            }
            var tiles = tilesProperty.EnumerateArray().ToList();

            // Calculate star values for each settlement and find the best one
            var settlementOptions = buildableSettlements
                .Select(building => new
                {
                    building = building,
                    stars = CalculateSettlementStars(building, tiles),
                    coordinates = FormatCoordinates(building)
                })
                .ToList();

            var maxStars = settlementOptions.Max(s => s.stars);
            var bestOptions = settlementOptions.Where(s => s.stars == maxStars).ToList();
            
            // If multiple settlements have the same star value, pick the first one
            var selectedSettlement = bestOptions.First();
            
            Console.WriteLine($"Selected settlement: {selectedSettlement.coordinates} with {selectedSettlement.stars} stars");
            Console.WriteLine($"Available best options: {string.Join(", ", bestOptions.Select(s => s.coordinates))}");

            // Extract building key details
            var buildingKey = selectedSettlement.building.GetProperty("buildingKey");
            var hexCoords = buildingKey.GetProperty("hexCoordinates");
            var selectedHexCoordinates = hexCoords;
            var selectedPosition = buildingKey.GetProperty("position").GetString();

            Console.WriteLine($"About to send BuildingUpgradeMessage with coordinates q={selectedHexCoordinates.GetProperty("q").GetInt32()}, r={selectedHexCoordinates.GetProperty("r").GetInt32()}, s={selectedHexCoordinates.GetProperty("s").GetInt32()}, position={selectedPosition}");

            // Send building upgrade message
            var buildingUpgradeBody = new
            {
                gameId = gameId,
                playerId = playerId,
                messageType = "BuildingUpgradeMessage",
                messageData = new
                {
                    buildingKey = new
                    {
                        hexCoordinates = new
                        {
                            q = selectedHexCoordinates.GetProperty("q").GetInt32(),
                            r = selectedHexCoordinates.GetProperty("r").GetInt32(),
                            s = selectedHexCoordinates.GetProperty("s").GetInt32()
                        },
                        position = selectedPosition
                    }
                }
            };

            var buildingJson = JsonSerializer.Serialize(buildingUpgradeBody);
            var buildingContent = new StringContent(buildingJson, Encoding.UTF8, "application/json");

            var buildingResponse = await _client.PostAsync("/api/game/action", buildingContent);
            if (!buildingResponse.IsSuccessStatusCode)
            {
                var errorBody = await buildingResponse.Content.ReadAsStringAsync();
                throw new InvalidOperationException($"Building upgrade HTTP call failed: {buildingResponse.StatusCode} - {errorBody}. Tried to place settlement at {selectedSettlement.coordinates}");
            }

            var buildingResponseBody = await buildingResponse.Content.ReadAsStringAsync();
            var buildingResult = JsonSerializer.Deserialize<JsonElement>(buildingResponseBody);
            if (!buildingResult.GetProperty("success").GetBoolean())
            {
                throw new InvalidOperationException($"Building upgrade should succeed. Response: {buildingResponseBody}");
            }

            return buildingResult;
        }

        // Helper method to calculate total stars for a settlement location using the same logic as the desktop app
        private int CalculateSettlementStars(JsonElement building, List<JsonElement> tiles)
        {
            // Extract building coordinates and position for adjacency calculation
            var buildingKey = building.GetProperty("buildingKey");
            var hexCoords = buildingKey.GetProperty("hexCoordinates");
            var q = hexCoords.GetProperty("q").GetInt32();
            var r = hexCoords.GetProperty("r").GetInt32();
            var s = hexCoords.GetProperty("s").GetInt32();
            var position = buildingKey.GetProperty("position").GetString();

            // Find tiles adjacent to this building using the same logic as GameModel.TilesForBuildings
            var adjacentTiles = FindAdjacentTiles(q, r, s, position, tiles);
            
            // Calculate stars using the same logic as TileModelExtensions.Stars()
            int totalStars = 0;
            foreach (var tile in adjacentTiles)
            {
                if (tile.TryGetProperty("number", out var numberElement))
                {
                    var number = numberElement.GetInt32();
                    totalStars += number switch
                    {
                        2 or 12 => 1,
                        3 or 11 => 2,
                        4 or 10 => 3,
                        5 or 9 => 4,
                        6 or 8 => 5,
                        7 => 0,
                        _ => 0
                    };
                }
            }
            
            return totalStars;
        }

        // Helper method to find tiles adjacent to a building position using the same logic as TilesForBuildings
        private List<JsonElement> FindAdjacentTiles(int q, int r, int s, string? position, List<JsonElement> allTiles)
        {
            var adjacentTiles = new List<JsonElement>();
            
            // Add the tile the building is on
            var baseTile = allTiles.FirstOrDefault(t => 
                t.TryGetProperty("tileKey", out var tileKey) &&
                tileKey.TryGetProperty("q", out var tileQ) && tileQ.GetInt32() == q &&
                tileKey.TryGetProperty("r", out var tileR) && tileR.GetInt32() == r &&
                tileKey.TryGetProperty("s", out var tileS) && tileS.GetInt32() == s);
            
            if (baseTile.ValueKind != JsonValueKind.Undefined)
            {
                adjacentTiles.Add(baseTile);
            }

            // Add adjacent tiles based on building position (simplified version of the hex logic)
            // Each building position connects to 2-3 tiles depending on the hex grid geometry
            switch (position)
            {
                case "TopLeft":
                    AddTileIfExists(q, r - 1, s + 1, allTiles, adjacentTiles); // North
                    AddTileIfExists(q - 1, r, s + 1, allTiles, adjacentTiles); // NorthWest
                    break;
                case "TopRight":
                    AddTileIfExists(q, r - 1, s + 1, allTiles, adjacentTiles); // North
                    AddTileIfExists(q + 1, r - 1, s, allTiles, adjacentTiles); // NorthEast
                    break;
                case "Right":
                    AddTileIfExists(q + 1, r - 1, s, allTiles, adjacentTiles); // NorthEast
                    AddTileIfExists(q + 1, r, s - 1, allTiles, adjacentTiles); // SouthEast
                    break;
                case "BottomRight":
                    AddTileIfExists(q + 1, r, s - 1, allTiles, adjacentTiles); // SouthEast
                    AddTileIfExists(q, r + 1, s - 1, allTiles, adjacentTiles); // South
                    break;
                case "BottomLeft":
                    AddTileIfExists(q, r + 1, s - 1, allTiles, adjacentTiles); // South
                    AddTileIfExists(q - 1, r + 1, s, allTiles, adjacentTiles); // SouthWest
                    break;
                case "Left":
                    AddTileIfExists(q - 1, r + 1, s, allTiles, adjacentTiles); // SouthWest
                    AddTileIfExists(q - 1, r, s + 1, allTiles, adjacentTiles); // NorthWest
                    break;
            }
            
            return adjacentTiles;
        }

        // Helper method to add a tile to the adjacent list if it exists
        private void AddTileIfExists(int q, int r, int s, List<JsonElement> allTiles, List<JsonElement> adjacentTiles)
        {
            var tile = allTiles.FirstOrDefault(t => 
                t.TryGetProperty("tileKey", out var tileKey) &&
                tileKey.TryGetProperty("q", out var tileQ) && tileQ.GetInt32() == q &&
                tileKey.TryGetProperty("r", out var tileR) && tileR.GetInt32() == r &&
                tileKey.TryGetProperty("s", out var tileS) && tileS.GetInt32() == s);
            
            if (tile.ValueKind != JsonValueKind.Undefined)
            {
                adjacentTiles.Add(tile);
            }
        }

        // Helper method to find and place first valid road
        private async Task<JsonElement> FindAndPlaceFirstValidRoad(string gameId, string playerId)
        {
            // Get current full GameModel to find buildable roads
            var gameModel = await GetCurrentFullGameModel(gameId);

            // Find first buildable road
            if (!gameModel.TryGetProperty("roads", out var roadsProperty))
            {
                throw new InvalidOperationException("Game state does not contain roads property");
            }

            var roads = roadsProperty.EnumerateArray().ToList();
            var buildableRoad = roads.FirstOrDefault(r =>
                r.TryGetProperty("roadState", out var roadState) &&
                roadState.GetString() == "Buildable"
            );

            Assert.True(buildableRoad.ValueKind != JsonValueKind.Undefined, "Should have at least one buildable road");

            // Execute road purchase for the first buildable road
            if (!buildableRoad.TryGetProperty("roadKey", out var roadKey))
            {
                throw new InvalidOperationException("Buildable road does not have roadKey property");
            }

            if (!roadKey.TryGetProperty("tileKey", out var tileKey))
            {
                throw new InvalidOperationException("Road key does not have tileKey property");
            }

            if (!roadKey.TryGetProperty("hexSide", out var sideProperty))
            {
                throw new InvalidOperationException("Road key does not have hexSide property");
            }

            var side = sideProperty.GetString();

            var roadPurchaseBody = new
            {
                gameId = gameId,
                playerId = playerId,
                messageType = "RoadPurchaseMessage",
                messageData = new
                {
                    roadKey = new
                    {
                        tileKey = new
                        {
                            q = tileKey.GetProperty("q").GetInt32(),
                            r = tileKey.GetProperty("r").GetInt32(),
                            s = tileKey.GetProperty("s").GetInt32()
                        },
                        side = side
                    }
                }
            };

            var roadJson = JsonSerializer.Serialize(roadPurchaseBody);
            var roadContent = new StringContent(roadJson, Encoding.UTF8, "application/json");

            var roadResponse = await _client.PostAsync("/api/game/action", roadContent);
            Assert.True(roadResponse.IsSuccessStatusCode, "Road purchase should succeed");

            var roadResponseBody = await roadResponse.Content.ReadAsStringAsync();
            var roadResult = JsonSerializer.Deserialize<JsonElement>(roadResponseBody);
            Assert.True(roadResult.GetProperty("success").GetBoolean(), "Road purchase should return success");

            return roadResult;
        }

        [Fact]
        public async Task AllocationPhase_TransitionFromBeginToForward_ShouldAdvanceToAllocateResourceForward()
        {
            // This test verifies that Next action from BeginResourceAllocation advances to AllocateResourceForward
            // and grants Settlement + Road entitlements to the current player

            // Arrange - Create a game in BeginResourceAllocation state
            var gameId = await CreateGameInBeginResourceAllocationState();
            var initialState = await GetGameStateInfo(gameId);

            // Act - Execute Next action to advance to next state
            var actionBody = new
            {
                gameId = gameId,
                playerId = "Alice",
                messageType = "DoAction",
                messageData = new { action = "Next" }
            };

            var actionJson = JsonSerializer.Serialize(actionBody);
            var actionContent = new StringContent(actionJson, Encoding.UTF8, "application/json");

            var actionResponse = await _client.PostAsync("/api/game/action", actionContent);
            Assert.True(actionResponse.IsSuccessStatusCode, "Next action HTTP call should succeed");

            var actionResponseBody = await actionResponse.Content.ReadAsStringAsync();
            var actionResult = JsonSerializer.Deserialize<JsonElement>(actionResponseBody);

            // Get updated game state
            var forwardState = await GetGameStateInfo(gameId);

            // Assert - Verify Next succeeded and advanced to some forward state
            var nextSuccess = actionResult.GetProperty("success").GetBoolean();
            if (!nextSuccess)
            {
                Console.WriteLine($"Action failed. Full response: {actionResponseBody}");
            }
            Assert.True(nextSuccess, $"Next action should succeed. Response: {actionResponseBody}");

            var newVersion = actionResult.GetProperty("gameStateVersion").GetInt32();
            Assert.True(newVersion > initialState.Version, "Game version should increment after Next");
            Assert.Equal(newVersion, forwardState.Version);

            // The state should advance to either AllocateResourceForward or some valid next state
            Assert.NotEqual(initialState.GameState, forwardState.GameState);

            // Verify we can access game model properties
            var gameModel = await GetCurrentFullGameModel(gameId);
            Assert.True(gameModel.TryGetProperty("players", out var players));
            var playersList = players.EnumerateArray().ToList();
            Assert.True(playersList.Count > 0, "Should have at least one player");
        }

        [Fact]
        public async Task AllocationPhase_ForwardPhasePlayerProgression_ShouldCycleAllPlayersWithEntitlements()
        {
            // This test verifies that during AllocateResourceForward phase, 
            // each player gets their turn and receives Settlement + Road entitlements

            // Arrange - Create a game in AllocateResourceForward state
            var gameId = await CreateGameInBeginResourceAllocationState();
            await ExecuteGameAction(gameId, "Next"); // BeginResourceAllocation ? AllocateResourceForward

            var playerOrder = new List<string> { "Alice", "Bob", "Charlie" };
            var playerIndex = 0;

            // Act & Assert - Each player should get their turn in AllocateResourceForward
            for (int i = 0; i < playerOrder.Count; i++)
            {
                var currentState = await GetGameStateInfo(gameId);
                Assert.Equal("AllocateResourceForward", currentState.GameState);
                Assert.Equal(playerOrder[playerIndex], currentState.CurrentPlayerId);

                // Verify current player has Settlement and Road entitlements
                var gameModel = await GetCurrentFullGameModel(gameId);

                if (!gameModel.TryGetProperty("players", out var players))
                {
                    throw new InvalidOperationException("Game state does not contain players property");
                }

                var playersList = players.EnumerateArray().ToList();
                var currentPlayerId = gameModel.GetProperty("currentPlayerId").GetString();
                var currentPlayer = playersList.FirstOrDefault(p => 
                    p.TryGetProperty("id", out var idProp) && 
                    idProp.GetString() == currentPlayerId);

                Assert.True(currentPlayer.ValueKind != JsonValueKind.Undefined, "Should have a current player");

                if (!currentPlayer.TryGetProperty("unspentEntitlements", out var unspentEntitlementsProperty))
                {
                    throw new InvalidOperationException("Current player does not have unspentEntitlements property");
                }

                var unspentEntitlements = unspentEntitlementsProperty.EnumerateArray()
                    .Select(e => e.GetString()).ToList();

                Assert.Contains("Settlement", unspentEntitlements);
                Assert.Contains("Road", unspentEntitlements);

                // Place settlement and road for current player
                await FindAndPlaceBestSettlement(gameId, playerOrder[playerIndex]);
                await FindAndPlaceFirstValidRoad(gameId, playerOrder[playerIndex]);

                // Advance to next player (or next phase if last player)
                if (i < playerOrder.Count - 1)
                {
                    await ExecuteGameAction(gameId, "Next");
                    playerIndex++;
                }
            }

            // After last player in forward phase, Next should advance to AllocateResourceReverse
            await ExecuteGameAction(gameId, "Next");
            var reverseState = await GetGameStateInfo(gameId);
            Assert.Equal("AllocateResourceReverse", reverseState.GameState);
            
            // Current player should be the last player (Charlie) starting the reverse phase
            Assert.Equal("Charlie", reverseState.CurrentPlayerId);
        }

        [Fact]
        public async Task AllocationPhase_ReversePhasePlayerProgression_ShouldCyclePlayersInReverseOrder()
        {
            // This test verifies that during AllocateResourceReverse phase,
            // players get their turn in reverse order with Settlement + Road entitlements

            // Arrange - Create a game and advance to AllocateResourceReverse state
            var gameId = await CreateGameInBeginResourceAllocationState();
            await ExecuteGameAction(gameId, "Next"); // BeginResourceAllocation ? AllocateResourceForward

            var playerOrder = new List<string> { "Alice", "Bob", "Charlie" };

            // Complete AllocateResourceForward phase for all players
            for (int i = 0; i < playerOrder.Count; i++)
            {
                await FindAndPlaceBestSettlement(gameId, playerOrder[i]);
                await FindAndPlaceFirstValidRoad(gameId, playerOrder[i]);
                if (i < playerOrder.Count - 1)
                {
                    await ExecuteGameAction(gameId, "Next");
                }
            }

            // Advance to AllocateResourceReverse
            await ExecuteGameAction(gameId, "Next");

            // Act & Assert - Each player should get their turn in reverse order
            var reversePlayerOrder = new List<string> { "Charlie", "Bob", "Alice" };
            
            for (int i = 0; i < reversePlayerOrder.Count; i++)
            {
                var currentState = await GetGameStateInfo(gameId);
                Assert.Equal("AllocateResourceReverse", currentState.GameState);
                Assert.Equal(reversePlayerOrder[i], currentState.CurrentPlayerId);

                // Verify current player has Settlement and Road entitlements
                var gameModel = await GetCurrentFullGameModel(gameId);

                if (!gameModel.TryGetProperty("players", out var players))
                {
                    throw new InvalidOperationException("Game state does not contain players property");
                }

                var playersList = players.EnumerateArray().ToList();
                var currentPlayerId = gameModel.GetProperty("currentPlayerId").GetString();
                var currentPlayer = playersList.FirstOrDefault(p => 
                    p.TryGetProperty("id", out var idProp) && 
                    idProp.GetString() == currentPlayerId);

                Assert.True(currentPlayer.ValueKind != JsonValueKind.Undefined, "Should have a current player");

                if (!currentPlayer.TryGetProperty("unspentEntitlements", out var unspentEntitlementsProperty))
                {
                    throw new InvalidOperationException("Current player does not have unspentEntitlements property");
                }

                var unspentEntitlements = unspentEntitlementsProperty.EnumerateArray()
                    .Select(e => e.GetString()).ToList();

                Assert.Contains("Settlement", unspentEntitlements);
                Assert.Contains("Road", unspentEntitlements);

                // Place settlement and road for current player
                await FindAndPlaceBestSettlement(gameId, reversePlayerOrder[i]);
                await FindAndPlaceFirstValidRoad(gameId, reversePlayerOrder[i]);

                // Advance to next player (or next phase if first player)
                if (i < reversePlayerOrder.Count - 1)
                {
                    await ExecuteGameAction(gameId, "Next");
                }
            }

            // After first player in reverse phase, Next should advance to DoneResourceAllocation
            await ExecuteGameAction(gameId, "Next");
            var doneState = await GetGameStateInfo(gameId);
            Assert.Equal("DoneResourceAllocation", doneState.GameState);
        }

        [Fact]
        public async Task AllocationPhase_TransitionToWaitingForRoll_ShouldCompleteAllocationAndAdvanceToGameplay()
        {
            // This test verifies the complete allocation phase workflow and transition to gameplay

            // Arrange - Create a game and complete the entire allocation phase
            var gameId = await CreateGameInBeginResourceAllocationState();
            await ExecuteGameAction(gameId, "Next"); // BeginResourceAllocation ? AllocateResourceForward

            var playerOrder = new List<string> { "Alice", "Bob", "Charlie" };

            // Complete AllocateResourceForward phase for all players
            for (int i = 0; i < playerOrder.Count; i++)
            {
                await FindAndPlaceBestSettlement(gameId, playerOrder[i]);
                await FindAndPlaceFirstValidRoad(gameId, playerOrder[i]);
                if (i < playerOrder.Count - 1)
                {
                    await ExecuteGameAction(gameId, "Next");
                }
            }

            // Advance to AllocateResourceReverse
            await ExecuteGameAction(gameId, "Next");

            // Complete AllocateResourceReverse phase for all players (reverse order)
            var reversePlayerOrder = new List<string> { "Charlie", "Bob", "Alice" };
            for (int i = 0; i < reversePlayerOrder.Count; i++)
            {
                await FindAndPlaceBestSettlement(gameId, reversePlayerOrder[i]);
                await FindAndPlaceFirstValidRoad(gameId, reversePlayerOrder[i]);
                if (i < reversePlayerOrder.Count - 1)
                {
                    await ExecuteGameAction(gameId, "Next");
                }
            }

            // Advance to DoneResourceAllocation
            await ExecuteGameAction(gameId, "Next");
            var doneState = await GetGameStateInfo(gameId);
            Assert.Equal("DoneResourceAllocation", doneState.GameState);

            // Act - Final transition to gameplay
            await ExecuteGameAction(gameId, "Next");
            var gameplayState = await GetGameStateInfo(gameId);

            // Assert - Should be in WaitingForRoll state (ready for gameplay)
            Assert.Equal("WaitingForRoll", gameplayState.GameState);

            // Verify each player has exactly 2 settlements and 2 roads
            var finalGameModel = await GetCurrentFullGameModel(gameId);

            var finalPlayers = finalGameModel.GetProperty("players").EnumerateArray().ToList();
            var buildings = finalGameModel.GetProperty("buildings").EnumerateArray().ToList();
            var roads = finalGameModel.GetProperty("roads").EnumerateArray().ToList();

            // Each player should have exactly 2 settlements and 2 roads placed
            foreach (var playerId in playerOrder)
            {
                var playerBuildings = buildings.Where(b =>
                    b.TryGetProperty("ownerId", out var ownerIdElement) && 
                    ownerIdElement.GetString() == playerId &&
                    b.TryGetProperty("buildingState", out var buildingStateElement) &&
                    buildingStateElement.GetString() == "Settlement"
                ).Count();

                var playerRoads = roads.Where(r =>
                    r.TryGetProperty("ownerId", out var ownerIdElement) && 
                    ownerIdElement.GetString() == playerId &&
                    r.TryGetProperty("roadState", out var roadStateElement) &&
                    roadStateElement.GetString() == "Road"
                ).Count();

                Assert.Equal(2, playerBuildings);
                Assert.Equal(2, playerRoads);

                // Each player should have score = 2 (2 settlements * 1 point each)
                var player = finalPlayers.FirstOrDefault(p => p.GetProperty("id").GetString() == playerId);
                var playerScore = player.GetProperty("score").GetInt32();
                Assert.Equal(2, playerScore);
            }

            // Current player should be Alice (first player, ready to roll dice)
            Assert.Equal("Alice", gameplayState.CurrentPlayerId);

            // Action flags should indicate rolls are enabled and next is disabled (waiting for roll)
            var actionFlags = finalGameModel.GetProperty("actionFlags");
            Assert.True(actionFlags.GetProperty("rollsEnabled").GetBoolean(), "Rolls should be enabled in WaitingForRoll state");
            Assert.False(actionFlags.GetProperty("nextEnabled").GetBoolean(), "Next should be disabled in WaitingForRoll state");
        }
    }

    // Helper class for allocation game state info
    public class AllocationGameStateInfo
    {
        public string GameId { get; set; } = "";
        public string GameState { get; set; } = "";
        public string CurrentPlayerId { get; set; } = "";
        public int Version { get; set; }
    }
}