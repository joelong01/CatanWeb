using Xunit;
using Microsoft.AspNetCore.Mvc.Testing;
using System.Text;
using System.Text.Json;

namespace Tests.GameService
{
    /// <summary>
    /// Static helper class for game phase transitions in tests.
    /// Provides clean, reusable methods to advance games through various phases
    /// without duplicating setup logic across test files.
    /// 
    /// Each method works on an existing HttpClient and gameId, allowing tests to focus
    /// on the specific functionality they're testing rather than setup boilerplate.
    /// </summary>
    public static class GamePhaseHelper
    {
        /// <summary>
        /// Creates a new game and returns the gameId.
        /// Game will be in PickingBoard state after creation.
        /// </summary>
        /// <param name="client">HttpClient for API calls</param>
        /// <param name="gameId">Optional gameId, will generate one if not provided</param>
        /// <param name="playerIds">Optional player list, defaults to Alice, Bob, Charlie</param>
        /// <returns>The gameId of the created game</returns>
        public static async Task<string> CreateGame(HttpClient client, string? gameId = null, List<string>? playerIds = null)
        {
            gameId ??= "test-game-" + Guid.NewGuid().ToString();
            playerIds ??= new List<string> { "Alice", "Bob", "Charlie" };

            var newGameRequestBody = new
            {
                gameId = gameId,
                gameType = "Regular",
                playerIds = playerIds
            };

            var newGameJson = JsonSerializer.Serialize(newGameRequestBody);
            var newGameContent = new StringContent(newGameJson, Encoding.UTF8, "application/json");

            var createGameResponse = await client.PostAsync("/api/game/new", newGameContent);
            if (!createGameResponse.IsSuccessStatusCode)
            {
                var errorContent = await createGameResponse.Content.ReadAsStringAsync();
                throw new InvalidOperationException($"Game creation failed: {createGameResponse.StatusCode} - {errorContent}");
            }

            return gameId;
        }

        /// <summary>
        /// Executes a game action via the API.
        /// </summary>
        /// <param name="client">HttpClient for API calls</param>
        /// <param name="gameId">The game to execute the action on</param>
        /// <param name="action">The action to execute (e.g., "Next", "Shuffle", "Undo")</param>
        /// <param name="playerId">The player executing the action, defaults to "Alice"</param>
        /// <returns>The JSON response from the action</returns>
        public static async Task<JsonElement> ExecuteGameAction(HttpClient client, string gameId, string action, string playerId = "Alice")
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

            var actionResponse = await client.PostAsync("/api/game/action", actionContent);
            
            if (!actionResponse.IsSuccessStatusCode)
            {
                var errorContent = await actionResponse.Content.ReadAsStringAsync();
                throw new InvalidOperationException($"{action} action HTTP failed: {actionResponse.StatusCode} - {errorContent}");
            }

            var actionResponseBody = await actionResponse.Content.ReadAsStringAsync();
            var actionResult = JsonSerializer.Deserialize<JsonElement>(actionResponseBody);
            
            if (!actionResult.GetProperty("success").GetBoolean())
            {
                var errorMessage = actionResult.TryGetProperty("message", out var msgElement) 
                    ? msgElement.GetString() 
                    : "Unknown error";
                throw new InvalidOperationException($"{action} action failed: {errorMessage}. Full response: {actionResponseBody}");
            }

            return actionResult;
        }

        /// <summary>
        /// Advances a game from PickingBoard state to WaitingForRollForOrder state.
        /// </summary>
        /// <param name="client">HttpClient for API calls</param>
        /// <param name="gameId">The game to advance</param>
        public static async Task HandlePickingBoard(HttpClient client, string gameId)
        {
            // PickingBoard state: Players can shuffle, balance, undo, redo, or proceed with Next
            // For test purposes, we just proceed with Next to advance the game
            await ExecuteGameAction(client, gameId, "Next"); // PickingBoard ? WaitingForRollForOrder
        }

        /// <summary>
        /// Advances a game through the RollForOrder phase.
        /// Transitions: WaitingForRollForOrder ? FinishedRollOrder ? BeginResourceAllocation
        /// </summary>
        /// <param name="client">HttpClient for API calls</param>
        /// <param name="gameId">The game to advance</param>
        /// <param name="customPlayerOrder">Optional custom player order, if null uses default order</param>
        public static async Task HandleRollForOrderPhase(HttpClient client, string gameId, List<string>? customPlayerOrder = null)
        {
            // WaitingForRollForOrder: Simulate rolling for order (just advance with Next)
            await ExecuteGameAction(client, gameId, "Next"); // WaitingForRollForOrder ? FinishedRollOrder

            // FinishedRollOrder: Set player order if custom order provided
            if (customPlayerOrder != null)
            {
                await SetPlayerOrder(client, gameId, customPlayerOrder);
            }

            // Advance to allocation phase
            await ExecuteGameAction(client, gameId, "Next"); // FinishedRollOrder ? BeginResourceAllocation
        }

        /// <summary>
        /// Advances a game through the complete Allocation phase.
        /// Handles both forward and reverse allocation rounds with optimal settlement/road placement.
        /// Transitions: BeginResourceAllocation ? AllocateResourceForward ? AllocateResourceReverse ? DoneResourceAllocation
        /// </summary>
        /// <param name="client">HttpClient for API calls</param>
        /// <param name="gameId">The game to advance</param>
        /// <param name="playerIds">Optional player list, defaults to Alice, Bob, Charlie</param>
        public static async Task HandleAllocationPhase(HttpClient client, string gameId, List<string>? playerIds = null)
        {
            playerIds ??= new List<string> { "Alice", "Bob", "Charlie" };

            // BeginResourceAllocation ? AllocateResourceForward
            await ExecuteGameAction(client, gameId, "Next");

            // AllocateResourceForward phase - each player places settlement + road
            for (int i = 0; i < playerIds.Count; i++)
            {
                await PlaceBestSettlementAndRoad(client, gameId, playerIds[i]);
                if (i < playerIds.Count - 1)
                {
                    await ExecuteGameAction(client, gameId, "Next");
                }
            }

            // AllocateResourceForward ? AllocateResourceReverse
            await ExecuteGameAction(client, gameId, "Next");

            // AllocateResourceReverse phase - each player places second settlement + road (reverse order)
            var reversePlayerOrder = playerIds.AsEnumerable().Reverse().ToList();
            for (int i = 0; i < reversePlayerOrder.Count; i++)
            {
                await PlaceBestSettlementAndRoad(client, gameId, reversePlayerOrder[i]);
                if (i < reversePlayerOrder.Count - 1)
                {
                    await ExecuteGameAction(client, gameId, "Next");
                }
            }

            // AllocateResourceReverse ? DoneResourceAllocation
            await ExecuteGameAction(client, gameId, "Next");
        }

        /// <summary>
        /// Advances a game from DoneResourceAllocation to WaitingForRoll state.
        /// </summary>
        /// <param name="client">HttpClient for API calls</param>
        /// <param name="gameId">The game to advance</param>
        public static async Task HandleResourceAllocationCompletion(HttpClient client, string gameId)
        {
            // DoneResourceAllocation ? WaitingForRoll
            await ExecuteGameAction(client, gameId, "Next");
        }

        /// <summary>
        /// Convenience method: Creates a game and advances it to WaitingForRoll state.
        /// This is the most common setup for gameplay testing.
        /// </summary>
        /// <param name="client">HttpClient for API calls</param>
        /// <param name="gameId">Optional gameId, will generate one if not provided</param>
        /// <param name="playerIds">Optional player list, defaults to Alice, Bob, Charlie</param>
        /// <returns>The gameId of the created game</returns>
        public static async Task<string> CreateGameInWaitingForRollState(HttpClient client, string? gameId = null, List<string>? playerIds = null)
        {
            gameId = await CreateGame(client, gameId, playerIds);
            await HandlePickingBoard(client, gameId);
            await HandleRollForOrderPhase(client, gameId);
            await HandleAllocationPhase(client, gameId, playerIds);
            await HandleResourceAllocationCompletion(client, gameId);
            return gameId;
        }

        /// <summary>
        /// Convenience method: Creates a game and advances it to PickingBoard state (initial state after creation).
        /// </summary>
        /// <param name="client">HttpClient for API calls</param>
        /// <param name="gameId">Optional gameId, will generate one if not provided</param>
        /// <param name="playerIds">Optional player list, defaults to Alice, Bob, Charlie</param>
        /// <returns>The gameId of the created game</returns>
        public static async Task<string> CreateGameInPickingBoardState(HttpClient client, string? gameId = null, List<string>? playerIds = null)
        {
            return await CreateGame(client, gameId, playerIds);
        }

        /// <summary>
        /// Convenience method overload: Creates a game with specific gameId in PickingBoard state.
        /// </summary>
        /// <param name="gameId">The specific gameId to use</param>
        /// <param name="client">HttpClient for API calls</param>
        /// <param name="playerIds">Optional player list, defaults to Alice, Bob, Charlie</param>
        /// <returns>The gameId of the created game</returns>
        public static async Task<string> CreateGameInPickingBoardState(string gameId, HttpClient client, List<string>? playerIds = null)
        {
            return await CreateGame(client, gameId, playerIds);
        }

        /// <summary>
        /// Convenience method: Creates a game and advances it to WaitingForRollForOrder state.
        /// </summary>
        /// <param name="client">HttpClient for API calls</param>
        /// <param name="gameId">Optional gameId, will generate one if not provided</param>
        /// <param name="playerIds">Optional player list, defaults to Alice, Bob, Charlie</param>
        /// <returns>The gameId of the created game</returns>
        public static async Task<string> CreateGameInWaitingForRollForOrderState(HttpClient client, string? gameId = null, List<string>? playerIds = null)
        {
            gameId = await CreateGame(client, gameId, playerIds);
            await HandlePickingBoard(client, gameId);
            return gameId;
        }

        /// <summary>
        /// Convenience method: Creates a game and advances it to BeginResourceAllocation state.
        /// </summary>
        /// <param name="client">HttpClient for API calls</param>
        /// <param name="gameId">Optional gameId, will generate one if not provided</param>
        /// <param name="playerIds">Optional player list, defaults to Alice, Bob, Charlie</param>
        /// <returns>The gameId of the created game</returns>
        public static async Task<string> CreateGameInBeginResourceAllocationState(HttpClient client, string? gameId = null, List<string>? playerIds = null)
        {
            gameId = await CreateGame(client, gameId, playerIds);
            await HandlePickingBoard(client, gameId);
            await HandleRollForOrderPhase(client, gameId);
            return gameId;
        }

        /// <summary>
        /// Convenience method: Creates a game and advances it to WaitingForNext state.
        /// This involves reaching WaitingForRoll state and then executing a dice roll.
        /// </summary>
        /// <param name="client">HttpClient for API calls</param>
        /// <param name="gameId">Optional gameId, will generate one if not provided</param>
        /// <param name="playerIds">Optional player list, defaults to Alice, Bob, Charlie</param>
        /// <param name="diceRoll">Optional dice total to roll, defaults to 6</param>
        /// <returns>The gameId of the created game</returns>
        public static async Task<string> CreateGameInWaitingForNextState(HttpClient client, string? gameId = null, List<string>? playerIds = null, int diceRoll = 6)
        {
            gameId = await CreateGameInWaitingForRollState(client, gameId, playerIds);
            await ExecuteRollAction(client, gameId, diceRoll);
            return gameId;
        }

        /// <summary>
        /// Executes a dice roll action to advance from WaitingForRoll to WaitingForNext.
        /// </summary>
        /// <param name="client">HttpClient for API calls</param>
        /// <param name="gameId">The game to roll dice in</param>
        /// <param name="totalRoll">The total dice value to roll (2-12)</param>
        /// <param name="playerId">The player rolling the dice, defaults to "Alice"</param>
        /// <returns>The JSON response from the roll action</returns>
        public static async Task<JsonElement> ExecuteRollAction(HttpClient client, string gameId, int totalRoll, string playerId = "Alice")
        {
            var rollBody = new
            {
                gameId = gameId,
                playerId = playerId,
                messageType = "RollMessage",
                messageData = new
                {
                    roll = new
                    {
                        normalRoll = totalRoll.ToString(),
                        specialDice = "None"
                    }
                }
            };

            var rollJson = JsonSerializer.Serialize(rollBody);
            var rollContent = new StringContent(rollJson, Encoding.UTF8, "application/json");

            var rollResponse = await client.PostAsync("/api/game/action", rollContent);
            
            if (!rollResponse.IsSuccessStatusCode)
            {
                var errorContent = await rollResponse.Content.ReadAsStringAsync();
                throw new InvalidOperationException($"Roll action HTTP failed: {rollResponse.StatusCode} - {errorContent}");
            }

            var rollResponseBody = await rollResponse.Content.ReadAsStringAsync();
            var rollResult = JsonSerializer.Deserialize<JsonElement>(rollResponseBody);
            
            if (!rollResult.GetProperty("success").GetBoolean())
            {
                var errorMessage = rollResult.TryGetProperty("message", out var msgElement) 
                    ? msgElement.GetString() 
                    : "Unknown error";
                throw new InvalidOperationException($"Roll action failed: {errorMessage}. Full response: {rollResponseBody}");
            }

            return rollResult;
        }

        /// <summary>
        /// Sets a custom player order during FinishedRollOrder state.
        /// </summary>
        /// <param name="client">HttpClient for API calls</param>
        /// <param name="gameId">The game to set order for</param>
        /// <param name="playerOrder">The desired player order</param>
        private static async Task SetPlayerOrder(HttpClient client, string gameId, List<string> playerOrder)
        {
            var orderBody = new
            {
                gameId = gameId,
                playerId = "Alice", // Arbitrarily use Alice for the order setting
                messageType = "SetPlayerOrderMessage",
                messageData = new { playerIds = playerOrder }
            };

            var orderJson = JsonSerializer.Serialize(orderBody);
            var orderContent = new StringContent(orderJson, Encoding.UTF8, "application/json");

            var orderResponse = await client.PostAsync("/api/game/action", orderContent);
            if (!orderResponse.IsSuccessStatusCode)
            {
                var errorContent = await orderResponse.Content.ReadAsStringAsync();
                throw new InvalidOperationException($"Set player order failed: {orderResponse.StatusCode} - {errorContent}");
            }

            var orderResponseBody = await orderResponse.Content.ReadAsStringAsync();
            var orderResult = JsonSerializer.Deserialize<JsonElement>(orderResponseBody);
            
            if (!orderResult.GetProperty("success").GetBoolean())
            {
                throw new InvalidOperationException($"Set player order should succeed. Response: {orderResponseBody}");
            }
        }

        /// <summary>
        /// Places the best settlement and an adjacent road for a player during allocation phase.
        /// Uses the same optimal placement logic as the individual test files.
        /// </summary>
        /// <param name="client">HttpClient for API calls</param>
        /// <param name="gameId">The game to place buildings in</param>
        /// <param name="playerId">The player placing the buildings</param>
        private static async Task PlaceBestSettlementAndRoad(HttpClient client, string gameId, string playerId)
        {
            // Get current game state to find buildable settlements
            var gameModel = await GetCurrentFullGameModel(client, gameId);
            
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

            if (buildableSettlements.Count == 0)
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
                    stars = CalculateSettlementStars(building, tiles)
                })
                .ToList();

            var maxStars = settlementOptions.Max(s => s.stars);
            var bestOptions = settlementOptions.Where(s => s.stars == maxStars).ToList();
            
            // If multiple settlements have the same star value, pick the first one
            var selectedSettlement = bestOptions.First();

            // Place the selected settlement
            await PlaceSettlement(client, gameId, playerId, selectedSettlement.building);

            // Place a road adjacent to the settlement
            await PlaceFirstValidRoad(client, gameId, playerId);
        }

        /// <summary>
        /// Places a settlement for a player.
        /// </summary>
        private static async Task PlaceSettlement(HttpClient client, string gameId, string playerId, JsonElement building)
        {
            var buildingKey = building.GetProperty("buildingKey");
            var hexCoords = buildingKey.GetProperty("hexCoordinates");
            var position = buildingKey.GetProperty("position").GetString();

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
                            q = hexCoords.GetProperty("q").GetInt32(),
                            r = hexCoords.GetProperty("r").GetInt32(),
                            s = hexCoords.GetProperty("s").GetInt32()
                        },
                        position = position
                    }
                }
            };

            var buildingJson = JsonSerializer.Serialize(buildingUpgradeBody);
            var buildingContent = new StringContent(buildingJson, Encoding.UTF8, "application/json");

            var buildingResponse = await client.PostAsync("/api/game/action", buildingContent);
            if (!buildingResponse.IsSuccessStatusCode)
            {
                var errorBody = await buildingResponse.Content.ReadAsStringAsync();
                throw new InvalidOperationException($"Building upgrade HTTP call failed: {buildingResponse.StatusCode} - {errorBody}");
            }

            var buildingResponseBody = await buildingResponse.Content.ReadAsStringAsync();
            var buildingResult = JsonSerializer.Deserialize<JsonElement>(buildingResponseBody);
            if (!buildingResult.GetProperty("success").GetBoolean())
            {
                throw new InvalidOperationException($"Building upgrade should succeed. Response: {buildingResponseBody}");
            }
        }

        /// <summary>
        /// Places the first available road for a player.
        /// </summary>
        private static async Task PlaceFirstValidRoad(HttpClient client, string gameId, string playerId)
        {
            var gameModel = await GetCurrentFullGameModel(client, gameId);

            if (!gameModel.TryGetProperty("roads", out var roadsProperty))
            {
                throw new InvalidOperationException("Game state does not contain roads property");
            }

            var roads = roadsProperty.EnumerateArray().ToList();
            var buildableRoad = roads.FirstOrDefault(r =>
                r.TryGetProperty("roadState", out var roadState) &&
                roadState.GetString() == "Buildable"
            );

            if (buildableRoad.ValueKind == JsonValueKind.Undefined)
            {
                throw new InvalidOperationException("Should have at least one buildable road");
            }

            var roadKey = buildableRoad.GetProperty("roadKey");
            var tileKey = roadKey.GetProperty("tileKey");
            var side = roadKey.GetProperty("hexSide").GetString();

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

            var roadResponse = await client.PostAsync("/api/game/action", roadContent);
            if (!roadResponse.IsSuccessStatusCode)
            {
                var errorContent = await roadResponse.Content.ReadAsStringAsync();
                throw new InvalidOperationException($"Road purchase HTTP failed: {roadResponse.StatusCode} - {errorContent}");
            }

            var roadResponseBody = await roadResponse.Content.ReadAsStringAsync();
            var roadResult = JsonSerializer.Deserialize<JsonElement>(roadResponseBody);
            if (!roadResult.GetProperty("success").GetBoolean())
            {
                throw new InvalidOperationException($"Road purchase should succeed. Response: {roadResponseBody}");
            }
        }

        /// <summary>
        /// Calculates the star value for a settlement location based on adjacent tiles.
        /// </summary>
        private static int CalculateSettlementStars(JsonElement building, List<JsonElement> tiles)
        {
            var buildingKey = building.GetProperty("buildingKey");
            var hexCoords = buildingKey.GetProperty("hexCoordinates");
            var q = hexCoords.GetProperty("q").GetInt32();
            var r = hexCoords.GetProperty("r").GetInt32();
            var s = hexCoords.GetProperty("s").GetInt32();
            var position = buildingKey.GetProperty("position").GetString();

            var adjacentTiles = FindAdjacentTiles(q, r, s, position, tiles);
            
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

        /// <summary>
        /// Finds tiles adjacent to a building position.
        /// </summary>
        private static List<JsonElement> FindAdjacentTiles(int q, int r, int s, string? position, List<JsonElement> allTiles)
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

            // Add adjacent tiles based on building position
            switch (position)
            {
                case "TopLeft":
                    AddTileIfExists(q, r - 1, s + 1, allTiles, adjacentTiles);
                    AddTileIfExists(q - 1, r, s + 1, allTiles, adjacentTiles);
                    break;
                case "TopRight":
                    AddTileIfExists(q, r - 1, s + 1, allTiles, adjacentTiles);
                    AddTileIfExists(q + 1, r - 1, s, allTiles, adjacentTiles);
                    break;
                case "Right":
                    AddTileIfExists(q + 1, r - 1, s, allTiles, adjacentTiles);
                    AddTileIfExists(q + 1, r, s - 1, allTiles, adjacentTiles);
                    break;
                case "BottomRight":
                    AddTileIfExists(q + 1, r, s - 1, allTiles, adjacentTiles);
                    AddTileIfExists(q, r + 1, s - 1, allTiles, adjacentTiles);
                    break;
                case "BottomLeft":
                    AddTileIfExists(q, r + 1, s - 1, allTiles, adjacentTiles);
                    AddTileIfExists(q - 1, r + 1, s, allTiles, adjacentTiles);
                    break;
                case "Left":
                    AddTileIfExists(q - 1, r + 1, s, allTiles, adjacentTiles);
                    AddTileIfExists(q - 1, r, s + 1, allTiles, adjacentTiles);
                    break;
            }
            
            return adjacentTiles;
        }

        /// <summary>
        /// Adds a tile to the adjacent list if it exists.
        /// </summary>
        private static void AddTileIfExists(int q, int r, int s, List<JsonElement> allTiles, List<JsonElement> adjacentTiles)
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

        /// <summary>
        /// Gets the complete game model for analysis.
        /// </summary>
        private static async Task<JsonElement> GetCurrentFullGameModel(HttpClient client, string gameId)
        {
            var gameStateResponse = await client.GetAsync($"/api/gamestate/{gameId}");
            if (!gameStateResponse.IsSuccessStatusCode)
            {
                var errorContent = await gameStateResponse.Content.ReadAsStringAsync();
                throw new InvalidOperationException($"GetGameState failed: {gameStateResponse.StatusCode} - {errorContent}");
            }

            var gameStateBody = await gameStateResponse.Content.ReadAsStringAsync();
            return JsonSerializer.Deserialize<JsonElement>(gameStateBody);
        }
    }
}