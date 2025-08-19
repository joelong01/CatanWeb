using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using System.Text.Json;
using System.Text.Json.Serialization;
using Catan3.Shared.Models;
using Catan3.Shared.Utility;
using Catan3.GameService.Services;
using Catan3.GameService.Utility;

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
            
            _logger.LogEvent("API Request", $"[{requestId}] POST /api/game/action - Processing async command {commandId}");
            
            try
            {
                var gameId = request.GetProperty("gameId").GetString();
                var playerId = request.GetProperty("playerId").GetString();
                var messageType = request.GetProperty("messageType").GetString();

                _logger.LogEvent("Command Request", $"[{requestId}] Async command request - GameId: {gameId}, PlayerId: {playerId}, MessageType: {messageType}, CommandId: {commandId}");

                if (string.IsNullOrEmpty(gameId) || string.IsNullOrEmpty(playerId) || string.IsNullOrEmpty(messageType))
                {
                    _logger.LogEvent("Validation Error", $"[{requestId}] Missing required fields: gameId={gameId}, playerId={playerId}, messageType={messageType}", LogLevel.Warning);
                    return Task.FromResult<IActionResult>(BadRequest("Missing required fields: gameId, playerId, messageType"));
                }

                // Verify game exists before processing
                var currentGame = _gameStateMachineService.GetCurrentGameState(gameId);
                if (currentGame == null)
                {
                    _logger.LogEvent("Game Not Found", $"[{requestId}] Game not found: {gameId}", LogLevel.Warning);
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

                _logger.LogEvent("Command Accepted", $"[{requestId}] Command accepted for async processing - CommandId: {commandId}, GameId: {gameId}");

                return Task.FromResult<IActionResult>(Ok(response));
            }
            catch (Exception ex)
            {
                _logger.LogEvent("Command Error", $"[{requestId}] Error accepting command for async processing - CommandId: {commandId}: {ex.Message}", LogLevel.Error);
                return Task.FromResult<IActionResult>(StatusCode(500, $"Error accepting command: {ex.Message}"));
            }
        }

        /// <summary>
        /// Gets the current game state for a game ID
        /// SignalR should be used for real-time updates instead of hanging GET
        /// </summary>
        [HttpGet("gamestate/{gameId}")]
        public IActionResult GetGameState(string gameId)
        {
            var requestId = Guid.NewGuid().ToString("N")[..8];
            _logger.LogEvent("API Request", $"[{requestId}] GET /api/gamestate/{gameId} - Getting game state");
            
            try
            {
                var gameModel = _gameStateMachineService.GetCurrentGameState(gameId);
                if (gameModel == null)
                {
                    _logger.LogEvent("Game Not Found", $"[{requestId}] Game not found: {gameId}", LogLevel.Warning);
                    return NotFound($"Game {gameId} not found");
                }

                var result = CreateGameStateResponse(gameId, gameModel);
                
                _logger.LogEvent("Game State Retrieved", $"[{requestId}] Game state retrieved successfully - GameId: {gameId}, State: {gameModel.GameState}, Players: {gameModel.Players.Count}, GameStateMachineVersion: {gameModel.GameStateMachineVersion}");
                
                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogEvent("Get Game State Error", $"[{requestId}] Error getting game state for gameId: {gameId}: {ex.Message}", LogLevel.Error);
                return StatusCode(500, $"Error getting game state: {ex.Message}");
            }
        }

        [HttpPost("game/register")]
        public IActionResult RegisterGame([FromBody] JsonElement request)
        {
            var requestId = Guid.NewGuid().ToString("N")[..8];
            _logger.LogEvent("API Request", $"[{requestId}] POST /api/game/register - This endpoint is deprecated, use /api/game/new instead");
            
            return BadRequest("This endpoint is deprecated. Use /api/game/new instead, which will return a server-generated gameId.");
        }

        [HttpPost("game/new")]
        public IActionResult NewGame([FromBody] JsonElement request)
        {
            var requestId = Guid.NewGuid().ToString("N")[..8];
            _logger.LogEvent("API Request", $"[{requestId}] POST /api/game/new - Creating new game");
            
            try
            {
                // Handle both old JSON format and new NewGameRequest format for backward compatibility
                GameType gameType;
                List<string> playerIds = new();

                // Check for required gameType field
                if (!request.TryGetProperty("gameType", out var gameTypeElement))
                {
                    _logger.LogEvent("Validation Error", $"[{requestId}] Missing required field: gameType", LogLevel.Warning);
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
                    _logger.LogEvent("Validation Error", $"[{requestId}] Invalid game type: {gameTypeStr ?? "null"}", LogLevel.Warning);
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
                    _logger.LogEvent("Validation Error", $"[{requestId}] No valid players provided", LogLevel.Warning);
                    return BadRequest("At least one valid player is required");
                }

                _logger.LogEvent("New Game Request", $"[{requestId}] Creating new game - GameType: {gameType}, Players: [{string.Join(", ", playerIds)}]");

                var newGameMessage = new NewGameMessage(gameType, playerIds);
                var (gameId, gameModel) = _gameStateMachineService.CreateNewGame(gsm => gsm.HandleNewGame(newGameMessage));

                var currentVersion = gameModel.GameStateMachineVersion;

                _logger.LogEvent("New Game Created", $"[{requestId}] New game created successfully - GameId: {gameId}, Version: {currentVersion}, State: {gameModel.GameState}");

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
                _logger.LogEvent("New Game Error", $"[{requestId}] Error creating new game: {ex.Message}", LogLevel.Error);
                return StatusCode(500, $"Error creating new game: {ex.Message}");
            }
        }

        [HttpPost("game/load")]
        public async Task<IActionResult> LoadGame([FromBody] JsonElement request)
        {
            var requestId = Guid.NewGuid().ToString("N")[..8];
            _logger.LogEvent("API Request", $"[{requestId}] POST /api/game/load - Loading game");
            
            try
            {
                var filePath = request.GetProperty("filePath").GetString();

                if (string.IsNullOrEmpty(filePath))
                {
                    _logger.LogEvent("Validation Error", $"[{requestId}] Missing required fields - FilePath: {filePath ?? "null"}", LogLevel.Warning);
                    return BadRequest("Missing required fields: filePath");
                }

                _logger.LogEvent("Load Game Request", $"[{requestId}] Loading game - FilePath: {filePath}");

                var loadGameMessage = new LoadGameMessage(filePath);
                
                // Create a new GameStateMachine and load the game into it
                var (gameId, gameModel) = await _gameStateMachineService.CreateNewGameAsync(async gsm => await gsm.HandleLoadGame(loadGameMessage));

                var currentVersion = gameModel.GameStateMachineVersion;

                _logger.LogEvent("Game Loaded", $"[{requestId}] Game loaded successfully - GameId: {gameId}, Version: {currentVersion}");

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
                _logger.LogEvent("Load Game Error", $"[{requestId}] Error loading game: {ex.Message}", LogLevel.Error);
                return StatusCode(500, $"Error loading game: {ex.Message}");
            }
        }

        [HttpPost("game/persist")]
        public async Task<IActionResult> PersistGame([FromBody] JsonElement request)
        {
            var requestId = Guid.NewGuid().ToString("N")[..8];
            _logger.LogEvent("API Request", $"[{requestId}] POST /api/game/persist - Persisting game");
            
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
                    _logger.LogEvent("Validation Error", $"[{requestId}] Missing gameId in persist request", LogLevel.Warning);
                    return BadRequest("Missing gameId");
                }

                if (!Enum.TryParse<LocalPersistActions>(actionStr, out var action))
                {
                    _logger.LogEvent("Validation Error", $"[{requestId}] Invalid persist action: {actionStr}", LogLevel.Warning);
                    return BadRequest($"Invalid persist action: {actionStr}");
                }

                _logger.LogEvent("Persist Game Request", $"[{requestId}] Persisting game - GameId: {gameId}, Action: {action}, Location: {location}");

                var persistMessage = new PersistGameMessage(action, location);
                var gameStateMachine = _gameStateMachineService.GetGameStateMachine(gameId);
                await gameStateMachine.HandlePersistGame(persistMessage);

                _logger.LogEvent("Game Persisted", $"[{requestId}] Game persisted successfully - GameId: {gameId}, Action: {action}");

                return Ok(new
                {
                    success = true,
                    message = $"Game {action} completed successfully"
                });
            }
            catch (Exception ex)
            {
                _logger.LogEvent("Persist Game Error", $"[{requestId}] Error persisting game: {ex.Message}", LogLevel.Error);
                return StatusCode(500, $"Error persisting game: {ex.Message}");
            }
        }

        [HttpGet("companion/games")]
        public IActionResult GetAvailableGames()
        {
            var requestId = Guid.NewGuid().ToString("N")[..8];
            _logger.LogEvent("API Request", $"[{requestId}] GET /api/companion/games - Getting available games");
            
            try
            {
                var availableGames = _gameStateMachineService.GetAvailableGames();
                
                _logger.LogEvent("Available Games", $"[{requestId}] Found {availableGames.Count} available games");
                
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
                _logger.LogEvent("Get Available Games Error", $"[{requestId}] Error getting available games: {ex.Message}", LogLevel.Error);
                return StatusCode(500, $"Error getting available games: {ex.Message}");
            }
        }

        private object CreateGameStateResponse(string gameId, GameModel gameModel)
        {
            // Rule 7 Compliance: Return GameModel as-is since it contains all necessary data
            // GameModel already has GameId, Version (incrementing with each change), and CreatedTime (as timestamp)
            // No need to add API-specific metadata - GameModel is the single source of truth
            
            // Return the GameModel directly - ASP.NET Core will serialize it properly
            // with the configured JsonSerializerOptions in Program.cs
            return gameModel;
        }

        private GameModel ProcessDoAction(JsonElement messageData, GameStateMachine gameStateMachine)
        {
            var actionStr = messageData.GetProperty("action").GetString();
            if (Enum.TryParse<GameAction>(actionStr, out var action))
            {
                var message = new ExecuteGameActionMessage(action);
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