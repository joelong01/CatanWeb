using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Collections.Concurrent;
using Catan3.Shared.Models;
using Catan3.Shared.Utility;

namespace Catan3.GameService.Controllers
{
    public class GameApiOptions
    {
        /// <summary>
        /// Timeout for hanging GET requests. Default is 15 minutes for production, should be much shorter for tests.
        /// </summary>
        public TimeSpan HangingGetTimeout { get; set; } = TimeSpan.FromMinutes(15);
        
        /// <summary>
        /// Timeout in seconds for hanging GET requests. This is used for configuration binding.
        /// </summary>
        public int HangingGetTimeoutSeconds 
        { 
            get => (int)HangingGetTimeout.TotalSeconds;
            set => HangingGetTimeout = TimeSpan.FromSeconds(value);
        }
    }

    /// <summary>
    /// Service for managing individual GameStateMachine instances per gameId
    /// Each game gets its own GameStateMachine with its own log state
    /// Thread-safe implementation for concurrent access
    /// </summary>
    public class GameStateMachineService
    {
        private readonly ConcurrentDictionary<string, GameStateMachine> _gameStateMachines = new();
        private readonly Dictionary<string, TaskCompletionSource<GameModel>> _pendingUpdates = new();
        private readonly object _pendingUpdatesLock = new();
        private int _currentVersion = 0;

        /// <summary>
        /// Gets an existing GameStateMachine for the specified gameId
        /// Throws GameException if the game doesn't exist
        /// </summary>
        public GameStateMachine GetGameStateMachine(string gameId)
        {
            if (_gameStateMachines.TryGetValue(gameId, out var gameStateMachine))
            {
                return gameStateMachine;
            }
            throw new Catan3.Shared.Utility.GameException($"Game {gameId} not found");
        }

        /// <summary>
        /// Creates a new GameStateMachine for the specified gameId
        /// Only called when creating a new game, not for retrieving existing games
        /// </summary>
        private GameStateMachine CreateGameStateMachine(string gameId)
        {
            var saveFile = $"game_{gameId}.json";
            var gameStateMachine = new GameStateMachine(null, saveFile);
            _gameStateMachines[gameId] = gameStateMachine;
            return gameStateMachine;
        }

        /// <summary>
        /// Executes an action on the GameStateMachine for the specified gameId
        /// </summary>
        public GameModel ExecuteAction(string gameId, Func<GameStateMachine, GameModel> action)
        {
            var gameStateMachine = GetGameStateMachine(gameId);
            var result = action(gameStateMachine);
            
            Interlocked.Increment(ref _currentVersion);
            NotifyPendingUpdates(gameId, result);
            
            return result;
        }

        /// <summary>
        /// Executes an async action on the GameStateMachine for the specified gameId
        /// </summary>
        public async Task<GameModel> ExecuteActionAsync(string gameId, Func<GameStateMachine, Task<GameModel>> action)
        {
            var gameStateMachine = GetGameStateMachine(gameId);
            var result = await action(gameStateMachine);
            
            Interlocked.Increment(ref _currentVersion);
            NotifyPendingUpdates(gameId, result);
            
            return result;
        }

        /// <summary>
        /// Creates a new game with the specified gameId and returns the initial game state
        /// </summary>
        public GameModel CreateNewGame(string gameId, Func<GameStateMachine, GameModel> createGameAction)
        {
            // Create the GameStateMachine first
            var gameStateMachine = CreateGameStateMachine(gameId);
            
            // Execute the new game action to initialize the game state
            var result = createGameAction(gameStateMachine);
            
            Interlocked.Increment(ref _currentVersion);
            NotifyPendingUpdates(gameId, result);
            
            return result;
        }

        public int GetCurrentVersion() => _currentVersion;

        public void AddPendingUpdate(string key, TaskCompletionSource<GameModel> tcs)
        {
            lock (_pendingUpdatesLock)
            {
                _pendingUpdates[key] = tcs;
            }
        }

        public void RemovePendingUpdate(string key)
        {
            lock (_pendingUpdatesLock)
            {
                _pendingUpdates.Remove(key);
            }
        }

        private void NotifyPendingUpdates(string gameId, GameModel gameModel)
        {
            List<string> completedTasks;
            
            lock (_pendingUpdatesLock)
            {
                completedTasks = new List<string>();
                foreach (var kvp in _pendingUpdates)
                {
                    if (kvp.Key.StartsWith(gameId + "_"))
                    {
                        kvp.Value.SetResult(gameModel);
                        completedTasks.Add(kvp.Key);
                    }
                }

                foreach (var key in completedTasks)
                {
                    _pendingUpdates.Remove(key);
                }
            }
        }

        /// <summary>
        /// Gets the current game state for the specified gameId
        /// Returns null if the game doesn't exist
        /// </summary>
        public GameModel? GetCurrentGameState(string gameId)
        {
            if (_gameStateMachines.TryGetValue(gameId, out var gameStateMachine))
            {
                try
                {
                    return gameStateMachine.GetCurrentState();
                }
                catch (InvalidOperationException)
                {
                    // Game exists but has no state yet (empty log)
                    return null;
                }
                catch
                {
                    return null;
                }
            }
            return null;
        }
    }

    [ApiController]
    [Route("api")]
    public class GameApiController : ControllerBase
    {
        private readonly GameApiOptions _options;
        private readonly GameStateMachineService _gameStateMachineService;

        public GameApiController(IOptions<GameApiOptions> options, GameStateMachineService gameStateMachineService)
        {
            _options = options.Value;
            _gameStateMachineService = gameStateMachineService;
        }

        [HttpPost("game/action")]
        public IActionResult ExecuteGameAction([FromBody] JsonElement request)
        {
            try
            {
                var gameId = request.GetProperty("gameId").GetString();
                var playerId = request.GetProperty("playerId").GetString();
                var messageType = request.GetProperty("messageType").GetString();
                var messageData = request.GetProperty("messageData");

                if (string.IsNullOrEmpty(gameId) || string.IsNullOrEmpty(playerId) || string.IsNullOrEmpty(messageType))
                {
                    return BadRequest("Missing required fields: gameId, playerId, messageType");
                }

                // Process the action based on message type using the correct GameStateMachine
                GameModel? updatedGameModel = null;
                string message = "";

                switch (messageType)
                {
                    case "DoAction":
                        updatedGameModel = _gameStateMachineService.ExecuteAction(gameId, gsm => ProcessDoAction(messageData, gsm));
                        message = "Action executed successfully";
                        break;
                    case "PurchaseMessage":
                        updatedGameModel = _gameStateMachineService.ExecuteAction(gameId, gsm => ProcessPurchaseMessage(messageData, gsm));
                        message = "Purchase executed successfully";
                        break;
                    case "RoadPurchaseMessage":
                        updatedGameModel = _gameStateMachineService.ExecuteAction(gameId, gsm => ProcessRoadPurchase(messageData, gsm));
                        message = "Road purchase executed successfully";
                        break;
                    case "BuildingUpgradeMessage":
                        updatedGameModel = _gameStateMachineService.ExecuteAction(gameId, gsm => ProcessBuildingUpgrade(messageData, gsm));
                        message = "Building upgrade executed successfully";
                        break;
                    case "MoveRobberMessage":
                        updatedGameModel = _gameStateMachineService.ExecuteAction(gameId, gsm => ProcessMoveRobber(messageData, gsm));
                        message = "Robber moved successfully";
                        break;
                    case "RollMessage":
                        updatedGameModel = _gameStateMachineService.ExecuteAction(gameId, gsm => ProcessRoll(messageData, gsm));
                        message = "Roll processed successfully";
                        break;
                    case "SetPlayerOrderMessage":
                        updatedGameModel = _gameStateMachineService.ExecuteAction(gameId, gsm => ProcessSetPlayerOrder(messageData, gsm));
                        message = "Player order set successfully";
                        break;
                    case "PlayersDoingSupplemental":
                        updatedGameModel = _gameStateMachineService.ExecuteAction(gameId, gsm => ProcessPlayersDoingSupplemental(messageData, gsm));
                        message = "Supplemental players set successfully";
                        break;
                    case "BalanceBoardMessage":
                        updatedGameModel = _gameStateMachineService.ExecuteAction(gameId, gsm => ProcessBalanceBoard(messageData, gsm));
                        message = "Board balanced successfully";
                        break;
                    case "GoFirstMessage":
                        updatedGameModel = _gameStateMachineService.ExecuteAction(gameId, gsm => ProcessGoFirst(messageData, gsm));
                        message = "Go first set successfully";
                        break;
                    default:
                        return BadRequest($"Unknown message type: {messageType}");
                }

                return Ok(new
                {
                    success = updatedGameModel != null,
                    gameStateVersion = _gameStateMachineService.GetCurrentVersion(),
                    message
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Error executing action: {ex.Message}");
            }
        }

        [HttpGet("gamestate/{gameId}")]
        public IActionResult GetGameState(string gameId)
        {
            try
            {
                var gameModel = _gameStateMachineService.GetCurrentGameState(gameId);
                if (gameModel == null)
                {
                    return NotFound($"Game {gameId} not found");
                }

                var result = CreateGameStateResponse(gameId, gameModel);
                return Ok(result);
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Error getting game state: {ex.Message}");
            }
        }

        [HttpGet("gamestate/{gameId}/listen")]
        public async Task<IActionResult> ListenForUpdates(string gameId, [FromQuery] string? playerId = null, [FromQuery] int version = 0)
        {
            try
            {
                var currentVersion = _gameStateMachineService.GetCurrentVersion();
                
                // If client version is behind, return immediately
                if (version < currentVersion)
                {
                    var currentGame = _gameStateMachineService.GetCurrentGameState(gameId);
                    if (currentGame != null)
                    {
                        var result = CreateGameStateResponse(gameId, currentGame);
                        return Ok(result);
                    }
                }

                // Create a task that will complete when the game state changes
                var tcs = new TaskCompletionSource<GameModel>();
                var key = $"{gameId}_{playerId}_{Guid.NewGuid()}";
                _gameStateMachineService.AddPendingUpdate(key, tcs);

                // Set up timeout using configurable timeout (15 minutes for production, shorter for tests)
                var timeoutTask = Task.Delay(_options.HangingGetTimeout);
                var completedTask = await Task.WhenAny(tcs.Task, timeoutTask);

                _gameStateMachineService.RemovePendingUpdate(key);

                if (completedTask == timeoutTask)
                {
                    // Timeout - return current state anyway (but this should be very rare with 15-minute timeout)
                    var timeoutGame = _gameStateMachineService.GetCurrentGameState(gameId);
                    if (timeoutGame != null)
                    {
                        var result = CreateGameStateResponse(gameId, timeoutGame);
                        return Ok(result);
                    }
                    return NotFound($"Game {gameId} not found");
                }
                else
                {
                    // Update received
                    var updatedGame = await tcs.Task;
                    var result = CreateGameStateResponse(gameId, updatedGame);
                    return Ok(result);
                }
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Error listening for updates: {ex.Message}");
            }
        }

        [HttpPost("game/register")]
        public IActionResult RegisterGame([FromBody] JsonElement request)
        {
            try
            {
                var gameId = request.GetProperty("gameId").GetString();
                if (string.IsNullOrEmpty(gameId))
                {
                    return BadRequest("Missing gameId");
                }

                // Create a test game using GameStateMachine
                var playerIds = new List<string> { "player1", "player2", "player3", "player4" };
                var newGameMessage = new NewGameMessage(GameType.Regular, playerIds);
                
                var gameModel = _gameStateMachineService.CreateNewGame(gameId, gsm => gsm.HandleNewGame(newGameMessage));

                return Ok(new { success = true, message = "Game registered successfully" });
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Error registering game: {ex.Message}");
            }
        }

        [HttpPost("game/new")]
        public IActionResult NewGame([FromBody] JsonElement request)
        {
            try
            {
                // Check if required properties exist
                if (!request.TryGetProperty("gameId", out var gameIdElement))
                {
                    return BadRequest("Missing required fields: gameId, gameType");
                }

                if (!request.TryGetProperty("gameType", out var gameTypeElement))
                {
                    return BadRequest("Missing required fields: gameId, gameType");
                }

                if (!request.TryGetProperty("playerIds", out var playerIdsElement))
                {
                    return BadRequest("Missing required fields: gameId, gameType, playerIds");
                }

                var gameId = gameIdElement.GetString();
                var gameTypeStr = gameTypeElement.GetString();

                if (string.IsNullOrEmpty(gameId) || string.IsNullOrEmpty(gameTypeStr))
                {
                    return BadRequest("Missing required fields: gameId, gameType");
                }

                if (!Enum.TryParse<GameType>(gameTypeStr, out var gameType))
                {
                    return BadRequest($"Invalid game type: {gameTypeStr}");
                }

                var playerIds = playerIdsElement.EnumerateArray()
                    .Select(element => element.GetString())
                    .Where(id => !string.IsNullOrEmpty(id))
                    .Cast<string>()
                    .ToList();

                var newGameMessage = new NewGameMessage(gameType, playerIds);
                var gameModel = _gameStateMachineService.CreateNewGame(gameId, gsm => gsm.HandleNewGame(newGameMessage));

                return Ok(new
                {
                    success = true,
                    gameStateVersion = _gameStateMachineService.GetCurrentVersion(),
                    message = "New game created successfully"
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Error creating new game: {ex.Message}");
            }
        }

        [HttpPost("game/load")]
        public async Task<IActionResult> LoadGame([FromBody] JsonElement request)
        {
            try
            {
                var gameId = request.GetProperty("gameId").GetString();
                var filePath = request.GetProperty("filePath").GetString();

                if (string.IsNullOrEmpty(gameId) || string.IsNullOrEmpty(filePath))
                {
                    return BadRequest("Missing required fields: gameId, filePath");
                }

                var loadGameMessage = new LoadGameMessage(filePath);
                var gameModel = await _gameStateMachineService.ExecuteActionAsync(gameId, async gsm => await gsm.HandleLoadGame(loadGameMessage));

                return Ok(new
                {
                    success = true,
                    gameStateVersion = _gameStateMachineService.GetCurrentVersion(),
                    message = "Game loaded successfully"
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Error loading game: {ex.Message}");
            }
        }

        [HttpPost("game/persist")]
        public async Task<IActionResult> PersistGame([FromBody] JsonElement request)
        {
            try
            {
                var gameId = request.GetProperty("gameId").GetString();
                var actionStr = request.GetProperty("action").GetString();
                var location = "";
                
                if (request.TryGetProperty("location", out var locationElement))
                {
                    location = locationElement.GetString() ?? "";
                }

                if (string.IsNullOrEmpty(gameId))
                {
                    return BadRequest("Missing gameId");
                }

                if (!Enum.TryParse<LocalPersistActions>(actionStr, out var action))
                {
                    return BadRequest($"Invalid persist action: {actionStr}");
                }

                var persistMessage = new PersistGameMessage(action, location);
                var gameStateMachine = _gameStateMachineService.GetGameStateMachine(gameId);
                await gameStateMachine.HandlePersistGame(persistMessage);

                return Ok(new
                {
                    success = true,
                    message = $"Game {action} completed successfully"
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Error persisting game: {ex.Message}");
            }
        }

        private GameModel ProcessDoAction(JsonElement messageData, GameStateMachine gameStateMachine)
        {
            var actionStr = messageData.GetProperty("action").GetString();
            if (Enum.TryParse<GameAction>(actionStr, out var action))
            {
                var message = new DoAction(action);
                return gameStateMachine.HandleDoAction(message);
            }
            throw new ArgumentException($"Invalid action: {actionStr}");
        }

        private GameModel ProcessPurchaseMessage(JsonElement messageData, GameStateMachine gameStateMachine)
        {
            var entitlementStr = messageData.GetProperty("entitlement").GetString();
            if (Enum.TryParse<Entitlement>(entitlementStr, out var entitlement))
            {
                var message = new PurchaseMessage(entitlement);
                return gameStateMachine.HandlePurchaseMessage(message);
            }
            throw new ArgumentException($"Invalid entitlement: {entitlementStr}");
        }

        private GameModel ProcessRoadPurchase(JsonElement messageData, GameStateMachine gameStateMachine)
        {
            var roadKeyData = messageData.GetProperty("roadKey");
            var tileKeyData = roadKeyData.GetProperty("tileKey");
            var sideStr = roadKeyData.GetProperty("side").GetString();

            var q = tileKeyData.GetProperty("q").GetInt32();
            var r = tileKeyData.GetProperty("r").GetInt32();
            var s = tileKeyData.GetProperty("s").GetInt32();

            if (!Enum.TryParse<Catan3.Shared.Models.HexSide>(sideStr, out var side))
            {
                throw new ArgumentException($"Invalid side: {sideStr}");
            }

            var tileKey = new HexCoordinates(q, r, s);
            var roadKey = new RoadKey { TileKey = tileKey, HexSide = side };
            var message = new RoadPurchaseMessage(roadKey);
            return gameStateMachine.HandleRoadPurchase(message);
        }

        private GameModel ProcessBuildingUpgrade(JsonElement messageData, GameStateMachine gameStateMachine)
        {
            var buildingKeyData = messageData.GetProperty("buildingKey");
            var hexCoordinatesData = buildingKeyData.GetProperty("hexCoordinates");
            var positionStr = buildingKeyData.GetProperty("position").GetString();

            var q = hexCoordinatesData.GetProperty("q").GetInt32();
            var r = hexCoordinatesData.GetProperty("r").GetInt32();
            var s = hexCoordinatesData.GetProperty("s").GetInt32();

            if (!Enum.TryParse<Catan3.Shared.Models.HexPosition>(positionStr, out var position))
            {
                throw new ArgumentException($"Invalid hex position: {positionStr}");
            }

            var hexCoordinates = new HexCoordinates(q, r, s);
            var buildingKey = new BuildingKey(hexCoordinates, position);
            var message = new BuildingUpgradeMessage(buildingKey);
            return gameStateMachine.HandleBuildingUpgrade(message);
        }

        private GameModel ProcessMoveRobber(JsonElement messageData, GameStateMachine gameStateMachine)
        {
            var coordinatesData = messageData.GetProperty("coordinates");
            var q = coordinatesData.GetProperty("q").GetInt32();
            var r = coordinatesData.GetProperty("r").GetInt32();
            var s = coordinatesData.GetProperty("s").GetInt32();

            string? targetPlayerId = null;
            if (messageData.TryGetProperty("targetPlayerId", out var targetElement))
            {
                targetPlayerId = targetElement.GetString();
            }

            var coordinates = new HexCoordinates(q, r, s);
            var message = new MoveRobberMessage(coordinates, targetPlayerId);
            return gameStateMachine.HandleMoveRobber(message);
        }

        private GameModel ProcessRoll(JsonElement messageData, GameStateMachine gameStateMachine)
        {
            var rollData = messageData.GetProperty("roll");
            var normalRollStr = rollData.GetProperty("normalRoll").GetString();
            
            if (!Enum.TryParse<ValidCatanRoll>(normalRollStr, out var normalRoll))
            {
                throw new ArgumentException($"Invalid roll: {normalRollStr}");
            }

            var specialDice = SpecialDice.None;
            if (rollData.TryGetProperty("specialDice", out var specialElement))
            {
                var specialStr = specialElement.GetString();
                if (!string.IsNullOrEmpty(specialStr))
                {
                    Enum.TryParse<SpecialDice>(specialStr, out specialDice);
                }
            }

            // Calculate individual dice rolls that sum to the normal roll
            int totalRoll = (int)normalRoll;
            int redRoll = totalRoll / 2;
            int whiteRoll = totalRoll - redRoll;

            var roll = new TurnRollModel(redRoll, whiteRoll)
            {
                SpecialDice = specialDice
            };

            var message = new RollMessage(roll);
            return gameStateMachine.HandleRoll(message);
        }

        private GameModel ProcessSetPlayerOrder(JsonElement messageData, GameStateMachine gameStateMachine)
        {
            var playerIds = messageData.GetProperty("playerIds").EnumerateArray()
                .Select(element => element.GetString())
                .Where(id => !string.IsNullOrEmpty(id))
                .Cast<string>()
                .ToList();

            var message = new SetPlayerOrderMessage(playerIds);
            return gameStateMachine.HandleSetPlayerOrder(message);
        }

        private GameModel ProcessPlayersDoingSupplemental(JsonElement messageData, GameStateMachine gameStateMachine)
        {
            var playerIds = messageData.GetProperty("playerIds").EnumerateArray()
                .Select(element => element.GetString())
                .Where(id => !string.IsNullOrEmpty(id))
                .Cast<string>()
                .ToList();

            var message = new PlayersDoingSupplemental(playerIds);
            return gameStateMachine.HandlePlayersDoingSupplemental(message);
        }

        private GameModel ProcessBalanceBoard(JsonElement messageData, GameStateMachine gameStateMachine)
        {
            var message = new BalanceBoardMessage();
            return gameStateMachine.HandleBalanceBoard(message);
        }

        private GameModel ProcessGoFirst(JsonElement messageData, GameStateMachine gameStateMachine)
        {
            var playerId = messageData.GetProperty("playerId").GetString()
                ?? throw new ArgumentException("Missing playerId");

            var message = new GoFirstMessage(playerId);
            return gameStateMachine.HandleGoFirst(message);
        }

        private object CreateGameStateResponse(string gameId, GameModel gameModel)
        {
            // According to the design principle, we should return the full GameModel
            // as the single source of truth for all client communication
            // Add the gameId and version as additional metadata
            var gameModelJson = JsonSerializer.Serialize(gameModel, new JsonSerializerOptions 
            { 
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                WriteIndented = false,
                Converters = { new JsonStringEnumConverter() } // Convert enums to strings for better API compatibility
            });
            
            var gameModelObject = JsonSerializer.Deserialize<Dictionary<string, object>>(gameModelJson, new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                Converters = { new JsonStringEnumConverter() }
            });
            
            // Add API-specific metadata
            gameModelObject!["gameId"] = gameId;
            gameModelObject["version"] = _gameStateMachineService.GetCurrentVersion();
            gameModelObject["timestamp"] = DateTime.UtcNow.ToString("O");
            
            // Convert EntitlementPurchaseModel to availableEntitlements array for API compatibility
            if (gameModelObject.ContainsKey("entitlementPurchaseModel"))
            {
                var entitlementPurchaseModel = gameModelObject["entitlementPurchaseModel"];
                if (entitlementPurchaseModel is JsonElement entitlementElement && entitlementElement.ValueKind == JsonValueKind.Array)
                {
                    var availableEntitlements = entitlementElement.EnumerateArray()
                        .Where(e => e.TryGetProperty("enabled", out var enabled) && enabled.GetBoolean())
                        .Select(e => e.GetProperty("entitlement").GetString())
                        .Where(e => !string.IsNullOrEmpty(e))
                        .ToArray();
                    
                    gameModelObject["availableEntitlements"] = availableEntitlements;
                }
                else
                {
                    // Fallback to empty array if conversion fails
                    gameModelObject["availableEntitlements"] = new string[0];
                }
            }
            else
            {
                // Fallback to empty array if property doesn't exist
                gameModelObject["availableEntitlements"] = new string[0];
            }
            
            return gameModelObject;
        }
    }
}