using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using System.Text.Json;
using Catan3.Shared.Models;
using Catan3.Shared.Utility;
using Catan3.Shared.GameLogic;
using Catan3.Shared.Extensions;
using Catan3.Shared.Interfaces;
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
        private readonly IPersistenceService _persistenceService;
        private readonly ILoggerFactory _loggerFactory;
        private readonly ILogger<GameApiController> _logger;

        public GameApiController(IOptions<GameApiOptions> options, IPersistenceService persistenceService, ILoggerFactory loggerFactory, ILogger<GameApiController> logger)
        {
            _options = options.Value;
            _persistenceService = persistenceService;
            _loggerFactory = loggerFactory;
            _logger = logger;
        }

        /// <summary>
        /// Creates a GameStateMachine with GameService-specific dependencies
        /// </summary>
        private GameStateMachine CreateGameStateMachineWithServiceDependencies(IGameLog gameLog)
        {
            // Create GameService-specific implementations
            var gameServiceLogger = _loggerFactory.CreateLogger<GameStateMachine>();
            var gameLogger = new GameServiceLogger(gameServiceLogger);

            // Create and return GameStateMachine with GameService dependencies
            return new GameStateMachine(gameLog, gameLogger, _persistenceService);
        }

        /// <summary>
        /// Gets an existing GameStateMachine for the specified gameId
        /// </summary>
        public static GameStateMachine GetGameStateMachine(string gameId)
        {
            return GameStateMachineRegistry.GetGameStateMachine(gameId);
        }

        [HttpPost("game/action")]
        public Task<IActionResult> ExecuteGameAction([FromBody] JsonElement request)
        {
            var commandId = Guid.NewGuid();
            
            _logger.LogEvent("API Request", $"POST /api/game/action - Processing async command {commandId}");
            
            try
            {
                var gameId = request.GetProperty("gameId").GetString();
                var playerId = request.GetProperty("playerId").GetString();
                var messageType = request.GetProperty("messageType").GetString();

                _logger.LogEvent("Command Request", $"Async command request - GameId: {gameId}, PlayerId: {playerId}, MessageType: {messageType}, CommandId: {commandId}");

                if (string.IsNullOrEmpty(gameId) || string.IsNullOrEmpty(playerId) || string.IsNullOrEmpty(messageType))
                {
                    _logger.LogEvent("Validation Error", $"Missing required fields: gameId={gameId}, playerId={playerId}, messageType={messageType}", LogLevel.Warning);
                    return Task.FromResult<IActionResult>(BadRequest("Missing required fields: gameId, playerId, messageType"));
                }

                // Verify game exists before processing
                try
                {
                    GameStateMachineRegistry.GetGameStateMachine(gameId);
                }
                catch (GameException)
                {
                    _logger.LogEvent("Game Not Found", $"Game not found: {gameId}", LogLevel.Warning);
                    // NOTE: Including GameId in error response is safe - client provided it in the request
                    return Task.FromResult<IActionResult>(NotFound($"Game {gameId} not found"));
                }

                // Get the async command processor from DI
                var commandProcessor = HttpContext.RequestServices.GetRequiredService<AsyncCommandProcessor>();

                // Fire-and-forget async processing with function to get GameStateMachine
                _ = commandProcessor.ProcessAsync(request, commandId, GameStateMachineRegistry.GetGameStateMachine);

                // Return immediate response
                var response = new
                {
                    success = true,
                    commandId = commandId,
                    message = "Command accepted, processing...",
                    estimatedCompletionMs = 100 // Most commands complete very quickly
                };

                _logger.LogEvent("Command Accepted", $"Command accepted for async processing - CommandId: {commandId}, GameId: {gameId}");

                return Task.FromResult<IActionResult>(Ok(response));
            }
            catch (Exception ex)
            {
                _logger.LogEvent("Command Error", $"Error accepting command for async processing - CommandId: {commandId}: {ex.Message}", LogLevel.Error);
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
            _logger.LogEvent("API Request", $"GET /api/gamestate/{gameId} - Getting game state");
            
            try
            {
                var gameStateMachine = GameStateMachineRegistry.GetGameStateMachine(gameId);
                var gameModel = gameStateMachine.GetCurrentState();

                var result = CreateGameStateResponse(gameId, gameModel);
                
                _logger.LogEvent("Game State Retrieved", $"Game state retrieved successfully - GameId: {gameId}, State: {gameModel.GameState}, Players: {gameModel.Players.Count}, GameStateMachineVersion: {gameModel.GameStateMachineVersion}");
                
                return Ok(result);
            }
            catch (GameException)
            {
                _logger.LogEvent("Game Not Found", $"Game not found: {gameId}", LogLevel.Warning);
                return NotFound($"Game {gameId} not found");
            }
            catch (Exception ex)
            {
                _logger.LogEvent("Get Game State Error", $"Error getting game state for gameId: {gameId}: {ex.Message}", LogLevel.Error);
                return StatusCode(500, $"Error getting game state: {ex.Message}");
            }
        }

        [HttpPost("game/register")]
        public IActionResult RegisterGame([FromBody] JsonElement request)
        {
            _logger.LogEvent("API Request", $"POST /api/game/register - This endpoint is deprecated, use /api/game/new instead");
            
            return BadRequest("This endpoint is deprecated. Use /api/game/new instead, which will return a server-generated gameId.");
        }

        [HttpPost("game/new")]
        public Task<IActionResult> NewGame([FromBody] NewGameMessage newGameMessage)
        {
            _logger.LogEvent("API Request", $"POST /api/game/new - Creating new game");
            
            try
            {
                if (newGameMessage is null || newGameMessage.PlayerIds is null or { Count: 0 })
                {
                    return Task.FromResult<IActionResult>(BadRequest("Invalid game creation request - must specify game type and players"));
                }

                // Get the appropriate game metadata based on game type
                IGameMetadata gameInfo = newGameMessage.GameType == GameType.Regular 
                    ? RegularBoardInfo.Default 
                    : ExpansionBoardInfo.Default;
                
                // Create new game model
                var gameModel = GameModelExtensions.CreateNew(gameInfo, newGameMessage.PlayerIds, newGameMessage.GameName ?? "Untitled Game");
                
                // Create the Log for this game (no logger needed for basic functionality)
                var gameLog = new Shared.Utility.Log<string>(_persistenceService, gameModel, isTest: false);
                
                // Create GameStateMachine with the Log
                var gameStateMachine = CreateGameStateMachineWithServiceDependencies(gameLog);
                
                // Store in registry
                GameStateMachineRegistry.AddGameStateMachine(gameModel.GameId, gameStateMachine);

                // Return minimal response - client must join via SignalR to get GameModel
                return Task.FromResult<IActionResult>(Ok(new { success = true, gameId = gameModel.GameId }));
            }
            catch (Exception ex)
            {
                _logger.LogEvent("New Game Error", $"Error creating new game: {ex.Message}", LogLevel.Error);
                return Task.FromResult<IActionResult>(StatusCode(500, $"Error creating new game: {ex.Message}"));
            }
        }

        [HttpPost("game/load")]
        public Task<IActionResult> LoadGame([FromBody] LoadGameMessage loadGameMessage)
        {
            _logger.LogEvent("API Request", $"POST /api/game/load - Loading game from compressed log");
            
            try
            {
                if (loadGameMessage?.CompressedLog is null or { Length: 0 })
                {
                    return Task.FromResult<IActionResult>(BadRequest("Missing compressed game data"));
                }

                // Create Log from compressed data
                var gameLog = Log<string>.FromCompressedString(loadGameMessage.CompressedLog, _persistenceService);

                // Create GameStateMachine with initialized dependencies
                var gameStateMachine = CreateGameStateMachineWithServiceDependencies(gameLog);

                // Get the current game state to determine GameId
                var gameModel = gameStateMachine.GetCurrentState();
                
                // Store GameStateMachine in registry
                GameStateMachineRegistry.AddGameStateMachine(gameModel.GameId, gameStateMachine);

                // Return minimal response - client must join via SignalR to get GameModel
                return Task.FromResult<IActionResult>(Ok(new { success = true, gameId = gameModel.GameId }));
            }
            catch (Exception ex)
            {
                _logger.LogEvent("Load Game Error", $"Error loading game: {ex.Message}", LogLevel.Error);
                return Task.FromResult<IActionResult>(StatusCode(500, $"Error loading game: {ex.Message}"));
            }
        }

        [HttpPost("game/loadmodel")]
        public Task<IActionResult> LoadGameModel([FromBody] LoadGameModelMessage loadGameModelMessage)
        {
            _logger.LogEvent("API Request", $"POST /api/game/loadmodel - Loading game from GameModel JSON");
            
            try
            {
                if (string.IsNullOrWhiteSpace(loadGameModelMessage?.GameModelJson))
                {
                    return Task.FromResult<IActionResult>(BadRequest("Missing GameModel JSON data"));
                }

                // Deserialize the GameModel from the message
                var gameModel = JsonHelper.Deserialize<GameModel>(loadGameModelMessage.GameModelJson)
                    ?? throw new InvalidOperationException("Failed to deserialize GameModel from LoadGameModelMessage JSON");
                
                // Validate the deserialized GameModel
                gameModel.Validate();
                
                // Create Log WITHOUT the GameModel (matches Desktop pattern)
                // Use empty string for file path when IsTest is true, otherwise use temp path
                var filePath = loadGameModelMessage.IsTest ? string.Empty : Path.Combine(Path.GetTempPath(), "Catan3Games", gameModel.SaveFileName());
                var gameLog = new Shared.Utility.Log<string>(_persistenceService, filePath);
                
                // Create GameStateMachine with initialized dependencies
                var gameStateMachine = CreateGameStateMachineWithServiceDependencies(gameLog);
                
                // Initialize the logging state with the GameModel (matches Desktop pattern)
                gameStateMachine.InitializeLoggingState(gameModel);
                
                // Store GameStateMachine in registry
                GameStateMachineRegistry.AddGameStateMachine(gameModel.GameId, gameStateMachine);

                // Return minimal response - client must join via SignalR to get GameModel
                return Task.FromResult<IActionResult>(Ok(new { success = true, gameId = gameModel.GameId }));
            }
            catch (Exception ex)
            {
                _logger.LogEvent("Load GameModel Error", $"Error loading game from GameModel: {ex.Message}", LogLevel.Error);
                return Task.FromResult<IActionResult>(StatusCode(500, $"Error loading game from GameModel: {ex.Message}"));
            }
        }

        [HttpPost("game/persist")]
        public async Task<IActionResult> PersistGame([FromBody] JsonElement request)
        {
            _logger.LogEvent("API Request", $"POST /api/game/persist - Persisting game");
            
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
                    _logger.LogEvent("Validation Error", $"Missing gameId in persist request", LogLevel.Warning);
                    return BadRequest("Missing gameId");
                }

                if (!Enum.TryParse<LocalPersistActions>(actionStr, out var action))
                {
                    _logger.LogEvent("Validation Error", $"Invalid persist action: {actionStr}", LogLevel.Warning);
                    return BadRequest($"Invalid persist action: {actionStr}");
                }

                _logger.LogEvent("Persist Game Request", $"Persisting game - GameId: {gameId}, Action: {action}, Location: {location}");

                var persistMessage = new PersistGameMessage(action, location);
                var gameStateMachine = GameStateMachineRegistry.GetGameStateMachine(gameId);
                await gameStateMachine.HandlePersistGameAsync(persistMessage);

                _logger.LogEvent("Game Persisted", $"Game persisted successfully - GameId: {gameId}, Action: {action}");

                return Ok(new
                {
                    success = true,
                    message = $"Game {action} completed successfully"
                });
            }
            catch (Exception ex)
            {
                _logger.LogEvent("Persist Game Error", $"Error persisting game: {ex.Message}", LogLevel.Error);
                return StatusCode(500, $"Error persisting game: {ex.Message}");
            }
        }

        [HttpGet("companion/games")]
        public IActionResult GetAvailableGames()
        {
            _logger.LogEvent("API Request", $"GET /api/companion/games - Getting available games");
            
            try
            {
                var availableGames = GameStateMachineRegistry.GetAvailableGames().OrderByDescending(g => g.CreatedTime).ToList();
                
                _logger.LogEvent("Available Games", $"Found {availableGames.Count} available games");
                
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
                _logger.LogEvent("Get Available Games Error", $"Error getting available games: {ex.Message}", LogLevel.Error);
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
            return actionStr switch
            {
                "Undo" => gameStateMachine.HandleUndoAsync(new UndoMessage()).Result,
                "Redo" => gameStateMachine.HandleRedoAsync(new RedoMessage()).Result,
                "Next" => gameStateMachine.HandleNextAsync(new NextMessage()).Result,
                _ => throw new ArgumentException($"Invalid action: {actionStr}")
            };
        }

        private GameModel ProcessPurchaseMessage(JsonElement messageData, GameStateMachine gameStateMachine)
        {
            var entitlementStr = messageData.GetProperty("entitlement").GetString();
            if (Enum.TryParse<Entitlement>(entitlementStr, out var entitlement))
            {
                var message = new PurchaseMessage(entitlement);
                return gameStateMachine.HandlePurchaseAsync(message).Result;
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
            return gameStateMachine.HandleRoadPurchaseAsync(message).Result;
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
            return gameStateMachine.HandleBuildingUpgradeAsync(message).Result;
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
            return gameStateMachine.HandleMoveRobberAsync(message).Result;
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
            return gameStateMachine.HandleRollAsync(message).Result;
        }

        private GameModel ProcessSetPlayerOrder(JsonElement messageData, GameStateMachine gameStateMachine)
        {
            var playerIds = messageData.GetProperty("playerIds").EnumerateArray()
                .Select(element => element.GetString())
                .Where(id => !string.IsNullOrEmpty(id))
                .Cast<string>()
                .ToList();

            var message = new SetPlayerOrderMessage(playerIds);
            return gameStateMachine.HandleSetPlayerOrderAsync(message).Result;
        }


        private GameModel ProcessBalanceBoard(JsonElement messageData, GameStateMachine gameStateMachine)
        {
            var message = new BalanceBoardMessage();
            return gameStateMachine.HandleBalanceBoardAsync(message).Result;
        }

        private GameModel ProcessGoFirst(JsonElement messageData, GameStateMachine gameStateMachine)
        {
            var playerId = messageData.GetProperty("playerId").GetString()
                ?? throw new ArgumentException("Missing playerId");

            var message = new GoFirstMessage(playerId);
            return gameStateMachine.HandleGoFirstAsync(message).Result;
        }
    }
}