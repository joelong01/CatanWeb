using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using System.Text.Json;
using System.Text.Json.Serialization;
using Catan3.Shared.Models;
using Catan3.Shared.Utility;
using Catan3.GameService.Services;

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

    [ApiController]
    [Route("api")]
    public class GameApiController : ControllerBase
    {
        private readonly GameApiOptions _options;
        private readonly GameStateMachineService _gameStateMachineService;
        private readonly ILogger<GameApiController> _logger;

        public GameApiController(IOptions<GameApiOptions> options, GameStateMachineService gameStateMachineService, ILogger<GameApiController> logger)
        {
            _options = options.Value;
            _gameStateMachineService = gameStateMachineService;
            _logger = logger;
        }

        [HttpPost("game/action")]
        public Task<IActionResult> ExecuteGameAction([FromBody] JsonElement request)
        {
            var requestId = Guid.NewGuid().ToString("N")[..8];
            var commandId = Guid.NewGuid();
            
            _logger.LogInformation("[{RequestId}] POST /api/game/action - Processing async command {CommandId}", requestId, commandId);
            
            try
            {
                var gameId = request.GetProperty("gameId").GetString();
                var playerId = request.GetProperty("playerId").GetString();
                var messageType = request.GetProperty("messageType").GetString();

                _logger.LogInformation("[{RequestId}] Async command request - GameId: {GameId}, PlayerId: {PlayerId}, MessageType: {MessageType}, CommandId: {CommandId}", 
                    requestId, gameId, playerId, messageType, commandId);

                if (string.IsNullOrEmpty(gameId) || string.IsNullOrEmpty(playerId) || string.IsNullOrEmpty(messageType))
                {
                    _logger.LogWarning("[{RequestId}] Missing required fields: gameId={GameId}, playerId={PlayerId}, messageType={MessageType}", 
                        requestId, gameId, playerId, messageType);
                    return Task.FromResult<IActionResult>(BadRequest("Missing required fields: gameId, playerId, messageType"));
                }

                // Verify game exists before processing
                var currentGame = _gameStateMachineService.GetCurrentGameState(gameId);
                if (currentGame == null)
                {
                    _logger.LogWarning("[{RequestId}] Game not found: {GameId}", requestId, gameId);
                    return Task.FromResult<IActionResult>(NotFound($"Game {gameId} not found"));
                }

                // Get the async command processor from DI
                var commandProcessor = HttpContext.RequestServices.GetRequiredService<AsyncCommandProcessor>();

                // Fire-and-forget async processing
                _ = commandProcessor.ProcessAsync(request, commandId);

                // Return immediate response
                var response = new
                {
                    success = true,
                    commandId = commandId,
                    message = "Command accepted, processing...",
                    estimatedCompletionMs = 100 // Most commands complete very quickly
                };

                _logger.LogInformation("[{RequestId}] Command accepted for async processing - CommandId: {CommandId}, GameId: {GameId}", 
                    requestId, commandId, gameId);

                return Task.FromResult<IActionResult>(Ok(response));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[{RequestId}] Error accepting command for async processing - CommandId: {CommandId}", requestId, commandId);
                return Task.FromResult<IActionResult>(StatusCode(500, $"Error accepting command: {ex.Message}"));
            }
        }

        [HttpGet("gamestate/{gameId}")]
        public IActionResult GetGameState(string gameId)
        {
            var requestId = Guid.NewGuid().ToString("N")[..8];
            _logger.LogInformation("[{RequestId}] GET /api/gamestate/{GameId} - Getting game state", requestId, gameId);
            
            try
            {
                var gameModel = _gameStateMachineService.GetCurrentGameState(gameId);
                if (gameModel == null)
                {
                    _logger.LogWarning("[{RequestId}] Game not found: {GameId}", requestId, gameId);
                    return NotFound($"Game {gameId} not found");
                }

                var result = CreateGameStateResponse(gameId, gameModel);
                
                _logger.LogInformation("[{RequestId}] Game state retrieved successfully - GameId: {GameId}, State: {GameState}, Players: {PlayerCount}, GameStateMachineVersion: {GameStateMachineVersion}", 
                    requestId, gameId, gameModel.GameState, gameModel.Players.Count, gameModel.GameStateMachineVersion);
                
                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[{RequestId}] Error getting game state for gameId: {GameId}", requestId, gameId);
                return StatusCode(500, $"Error getting game state: {ex.Message}");
            }
        }

        /// <summary>
        /// API for listening to game state updates via hanging GET
        /// Allows clients to receive live updates for game state changes
        /// </summary>
        [HttpGet("gamestate/{gameId}/listen")]
        public async Task<IActionResult> ListenForUpdates(string gameId, [FromQuery] string? playerId = null, [FromQuery] int version = 0)
        {
            var requestId = Guid.NewGuid().ToString("N")[..8];
            _logger.LogInformation("[{RequestId}] GET /api/gamestate/{GameId}/listen - Listening for updates, PlayerId: {PlayerId}, Version: {Version}", 
                requestId, gameId, playerId ?? "null", version);
            
            try
            {
                // Check if game exists first
                var currentGame = _gameStateMachineService.GetCurrentGameState(gameId);
                if (currentGame == null)
                {
                    _logger.LogWarning("[{RequestId}] Game not found: {GameId}", requestId, gameId);
                    return NotFound($"Game {gameId} not found");
                }

                // Get the ClientNotificationService from DI
                var clientNotificationService = HttpContext.RequestServices.GetRequiredService<IClientNotification>();
                
                // Set the current game state so clients can get immediate current state
                clientNotificationService.SetCurrentGameState(gameId, currentGame);

                // Use the ClientNotificationService to wait for updates
                var clientId = $"{playerId ?? "anonymous"}_{requestId}";
                var cancellationTokenSource = new CancellationTokenSource(_options.HangingGetTimeout);
                
                _logger.LogDebug("[{RequestId}] Starting hanging GET with timeout: {Timeout}ms, ClientId: {ClientId}, ClientVersion: {ClientVersion}", 
                    requestId, _options.HangingGetTimeout.TotalMilliseconds, clientId, version);

                GameModel updatedGameModel;
                try
                {
                    updatedGameModel = await clientNotificationService.WaitForNotificationAsync(
                        gameId, 
                        clientId, 
                        version, 
                        cancellationTokenSource.Token);
                    
                    _logger.LogInformation("[{RequestId}] Received live update - GameId: {GameId}", requestId, gameId);
                }
                catch (OperationCanceledException)
                {
                    _logger.LogDebug("[{RequestId}] Hanging GET timed out after {Timeout}ms", requestId, _options.HangingGetTimeout.TotalMilliseconds);
                    
                    // Timeout - return current state
                    var timeoutGame = _gameStateMachineService.GetCurrentGameState(gameId);
                    if (timeoutGame != null)
                    {
                        var result = CreateGameStateResponse(gameId, timeoutGame);
                        _logger.LogInformation("[{RequestId}] Returned timeout response - GameId: {GameId}", requestId, gameId);
                        return Ok(result);
                    }
                    return NotFound($"Game {gameId} not found");
                }

                // Return the updated game model
                var response = CreateGameStateResponse(gameId, updatedGameModel);
                _logger.LogInformation("[{RequestId}] Returned live update - GameId: {GameId}", requestId, gameId);
                return Ok(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[{RequestId}] Error listening for updates, GameId: {GameId}, PlayerId: {PlayerId}", requestId, gameId, playerId);
                return StatusCode(500, $"Error listening for updates: {ex.Message}");
            }
        }

        [HttpPost("game/register")]
        public IActionResult RegisterGame([FromBody] JsonElement request)
        {
            var requestId = Guid.NewGuid().ToString("N")[..8];
            _logger.LogInformation("[{RequestId}] POST /api/game/register - This endpoint is deprecated, use /api/game/new instead", requestId);
            
            return BadRequest("This endpoint is deprecated. Use /api/game/new instead, which will return a server-generated gameId.");
        }

        [HttpPost("game/new")]
        public IActionResult NewGame([FromBody] JsonElement request)
        {
            var requestId = Guid.NewGuid().ToString("N")[..8];
            _logger.LogInformation("[{RequestId}] POST /api/game/new - Creating new game", requestId);
            
            try
            {
                // Handle both old JSON format and new NewGameRequest format for backward compatibility
                GameType gameType;
                List<string> playerIds = new();

                // Check for required gameType field
                if (!request.TryGetProperty("gameType", out var gameTypeElement))
                {
                    _logger.LogWarning("[{RequestId}] Missing required field: gameType", requestId);
                    return BadRequest("Missing required fields: gameType");
                }

                var gameTypeStr = gameTypeElement.ValueKind switch
                {
                    JsonValueKind.String => gameTypeElement.GetString(),
                    JsonValueKind.Number => ((GameType)gameTypeElement.GetInt32()).ToString(),
                    _ => null
                };
                
                if (string.IsNullOrEmpty(gameTypeStr) || !Enum.TryParse<GameType>(gameTypeStr, out gameType))
                {
                    _logger.LogWarning("[{RequestId}] Invalid game type: {GameType}", requestId, gameTypeStr ?? "null");
                    return BadRequest($"Invalid game type: {gameTypeStr}");
                }

                // Handle player data - can be either simple string array or complex objects with Id/Name
                if (request.TryGetProperty("playerIds", out var playerIdsElement) && playerIdsElement.ValueKind == JsonValueKind.Array)
                {
                    // Simple string array format for backward compatibility
                    playerIds = playerIdsElement.EnumerateArray()
                        .Select(element => {
                            return element.ValueKind switch
                            {
                                JsonValueKind.String => element.GetString(),
                                JsonValueKind.Number => element.GetInt32().ToString(),
                                _ => element.ToString()
                            };
                        })
                        .Where(id => !string.IsNullOrEmpty(id))
                        .Cast<string>()
                        .ToList();
                }
                else if (request.TryGetProperty("players", out var playersElement) && playersElement.ValueKind == JsonValueKind.Array)
                {
                    // Complex object format following Desktop app pattern
                    playerIds = playersElement.EnumerateArray()
                        .Where(p => p.TryGetProperty("id", out var id) && 
                                   (id.ValueKind == JsonValueKind.String && !string.IsNullOrEmpty(id.GetString())))
                        .Select(p => p.GetProperty("id").GetString()!)
                        .ToList();
                }

                if (playerIds.Count == 0)
                {
                    _logger.LogWarning("[{RequestId}] No valid players provided", requestId);
                    return BadRequest("At least one valid player is required");
                }

                _logger.LogInformation("[{RequestId}] Creating new game - GameType: {GameType}, Players: [{Players}]", 
                    requestId, gameType, string.Join(", ", playerIds));

                var newGameMessage = new NewGameMessage(gameType, playerIds);
                var (gameId, gameModel) = _gameStateMachineService.CreateNewGame(gsm => gsm.HandleNewGame(newGameMessage));

                var currentVersion = gameModel.GameStateMachineVersion;

                _logger.LogInformation("[{RequestId}] New game created successfully - GameId: {GameId}, Version: {Version}, State: {GameState}", 
                    requestId, gameId, currentVersion, gameModel.GameState);

                return Ok(new
                {
                    success = true,
                    gameId = gameId,  // Return the server-generated GameId
                    gameStateVersion = currentVersion,
                    message = "New game created successfully"
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[{RequestId}] Error creating new game", requestId);
                return StatusCode(500, $"Error creating new game: {ex.Message}");
            }
        }

        [HttpPost("game/load")]
        public async Task<IActionResult> LoadGame([FromBody] JsonElement request)
        {
            var requestId = Guid.NewGuid().ToString("N")[..8];
            _logger.LogInformation("[{RequestId}] POST /api/game/load - Loading game", requestId);
            
            try
            {
                var filePath = request.GetProperty("filePath").GetString();

                if (string.IsNullOrEmpty(filePath))
                {
                    _logger.LogWarning("[{RequestId}] Missing required fields - FilePath: {FilePath}", requestId, filePath ?? "null");
                    return BadRequest("Missing required fields: filePath");
                }

                _logger.LogInformation("[{RequestId}] Loading game - FilePath: {FilePath}", requestId, filePath);

                var loadGameMessage = new LoadGameMessage(filePath);
                
                // Create a new GameStateMachine and load the game into it
                var (gameId, gameModel) = await _gameStateMachineService.CreateNewGameAsync(async gsm => await gsm.HandleLoadGame(loadGameMessage));

                var currentVersion = gameModel.GameStateMachineVersion;

                _logger.LogInformation("[{RequestId}] Game loaded successfully - GameId: {GameId}, Version: {Version}", requestId, gameId, currentVersion);

                return Ok(new
                {
                    success = true,
                    gameId = gameId,  // Return the server-generated GameId
                    gameStateVersion = currentVersion,
                    message = "Game loaded successfully"
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[{RequestId}] Error loading game", requestId);
                return StatusCode(500, $"Error loading game: {ex.Message}");
            }
        }

        [HttpPost("game/persist")]
        public async Task<IActionResult> PersistGame([FromBody] JsonElement request)
        {
            var requestId = Guid.NewGuid().ToString("N")[..8];
            _logger.LogInformation("[{RequestId}] POST /api/game/persist - Persisting game", requestId);
            
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
                    _logger.LogWarning("[{RequestId}] Missing gameId in persist request", requestId);
                    return BadRequest("Missing gameId");
                }

                if (!Enum.TryParse<LocalPersistActions>(actionStr, out var action))
                {
                    _logger.LogWarning("[{RequestId}] Invalid persist action: {Action}", requestId, actionStr);
                    return BadRequest($"Invalid persist action: {actionStr}");
                }

                _logger.LogInformation("[{RequestId}] Persisting game - GameId: {GameId}, Action: {Action}, Location: {Location}", 
                    requestId, gameId, action, location);

                var persistMessage = new PersistGameMessage(action, location);
                var gameStateMachine = _gameStateMachineService.GetGameStateMachine(gameId);
                await gameStateMachine.HandlePersistGame(persistMessage);

                _logger.LogInformation("[{RequestId}] Game persisted successfully - GameId: {GameId}, Action: {Action}", requestId, gameId, action);

                return Ok(new
                {
                    success = true,
                    message = $"Game {action} completed successfully"
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[{RequestId}] Error persisting game", requestId);
                return StatusCode(500, $"Error persisting game: {ex.Message}");
            }
        }

        [HttpGet("companion/games")]
        public IActionResult GetAvailableGames()
        {
            var requestId = Guid.NewGuid().ToString("N")[..8];
            _logger.LogInformation("[{RequestId}] GET /api/companion/games - Getting available games", requestId);
            
            try
            {
                var availableGames = _gameStateMachineService.GetAvailableGames();
                
                _logger.LogInformation("[{RequestId}] Found {GameCount} available games", requestId, availableGames.Count);
                
                return Ok(new
                {
                    success = true,
                    games = availableGames,
                    count = availableGames.Count,
                    timestamp = DateTime.UtcNow.ToString("O")
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[{RequestId}] Error getting available games", requestId);
                return StatusCode(500, $"Error getting available games: {ex.Message}");
            }
        }

        private object CreateGameStateResponse(string gameId, GameModel gameModel)
        {
            // Rule 7 Compliance: Return GameModel as-is since it contains all necessary data
            // GameModel already has GameId, Version (incrementing with each change), and CreatedTime (as timestamp)
            // No need to add API-specific metadata - GameModel is the single source of truth
            
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
            
            return gameModelObject!;
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
    }
}