using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Options;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;
using Catan3.Shared.Models;
using Catan3.Shared.Utility;
using Catan3.Shared.Profiles;
using Catan3.Shared.GameLogic;
using Catan3.Shared.Extensions;
using Catan3.Shared.Interfaces;
using Catan3.GameService.Services;
using Catan3.GameService.Utility;
using Catan3.GameService.Data;
using Catan3.GameService.Hubs;

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
    /// Base request for game commands
    /// </summary>
    public class CommandRequest
    {
        public string PlayerId { get; set; } = string.Empty;
    }

    /// <summary>
    /// Response for game commands
    /// </summary>
    public class CommandResponse
    {
        public bool Success { get; set; }
        public string? Message { get; set; }
        public string? Error { get; set; }
        public string? ErrorCode { get; set; }
        public string? GameId { get; set; }
    }

    [ApiController]
    [Route("api")]
    public class GameApiController : ControllerBase
    {
        private readonly GameApiOptions _options;
        private readonly IPersistenceService _persistenceService;
        private readonly ILoggerFactory _loggerFactory;
        private readonly ILogger<GameApiController> _logger;
        private readonly CatanDbContext _dbContext;
        private readonly IHubContext<GameHub> _hubContext;
        private readonly IGamePersistence _gamePersistence;

        public GameApiController(
            IOptions<GameApiOptions> options,
            IPersistenceService persistenceService,
            ILoggerFactory loggerFactory,
            ILogger<GameApiController> logger,
            CatanDbContext dbContext,
            IHubContext<GameHub> hubContext,
            IGamePersistence gamePersistence)
        {
            _options = options.Value;
            _persistenceService = persistenceService;
            _loggerFactory = loggerFactory;
            _logger = logger;
            _dbContext = dbContext;
            _hubContext = hubContext;
            _gamePersistence = gamePersistence;
        }

        /// <summary>
        /// Common post-action processing: save to database and broadcast to clients
        /// </summary>
        private async Task ProcessGameActionResult(GameStateMachine gameStateMachine, GameModel gameModel, string actionName)
        {
            // Save to database
            await SaveGameToDatabase(gameStateMachine, gameModel);

            // Broadcast to all clients in game group
            await _hubContext.Clients.Group(gameModel.GameId).SendAsync("GameStateUpdated", gameModel);
            _logger.LogEvent("Send Client Update", $"GameStateUpdated sent for {actionName} - GameId={gameModel.GameId}");
        }

        /// <summary>
        /// Saves the full game log (with undo/redo stacks) to the database
        /// </summary>
        private async Task SaveGameToDatabase(GameStateMachine gameStateMachine, GameModel gameModel)
        {
            try
            {
                // Get the full serializable log (preserves undo/redo stacks)
                var serializableLog = gameStateMachine.GetSerializableLog();
                var json = JsonHelper.Serialize(serializableLog);
                var compressed = JsonHelper.Compress(json);

                // Create metadata for queryability
                var metadata = new GameMetadata
                {
                    GameName = gameModel.GameName,
                    GameState = gameModel.GameState.ToString(),
                    StartedBy = "WebUI", // Placeholder until user auth is implemented
                    PlayerCount = gameModel.Players.Count,
                    GameType = gameModel.Tiles.Count > 19 ? "Expansion" : "Regular",
                    PlayerNames = string.Join(", ", gameModel.Players.Select(p => p.Name)),
                    TurnCount = serializableLog.DoneCount
                };

                // Save to database
                await _gamePersistence.SaveAsync(gameModel.GameId, compressed, metadata);
                _logger.LogEvent("Database Save", $"Game saved to database: {gameModel.GameId}");
            }
            catch (Exception ex)
            {
                _logger.LogEvent("Database Save Error", $"Failed to save game to database: {ex.Message}", LogLevel.Error);
                // Don't throw - database save failure shouldn't break the game operation
            }
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
        public async Task<IActionResult> NewGame([FromBody] NewGameMessage newGameMessage)
        {
            _logger.LogEvent("API Request", $"POST /api/game/new - Creating new game");

            try
            {
                if (newGameMessage is null || newGameMessage.PlayerIds is null or { Count: 0 })
                {
                    return BadRequest("Invalid game creation request - must specify game type and players");
                }

                // Get the appropriate game metadata based on game type
                IGameMetadata gameInfo = newGameMessage.GameType == GameType.Regular
                    ? RegularBoardInfo.Default
                    : ExpansionBoardInfo.Default;

                // Generate a temporary save file path for the game log
                var tempSaveFilePath = $"{newGameMessage.GameName ?? "Untitled Game"}-{Guid.NewGuid()}.catan";

                // Create the Log for this game (no logger needed for basic functionality)
                var gameLog = new Shared.Utility.Log<string>(_persistenceService, tempSaveFilePath);

                // Create GameStateMachine with the Log
                var gameStateMachine = CreateGameStateMachineWithServiceDependencies(gameLog);

                // Use the GameStateMachine to create the fully initialized game
                // Pass client-provided HouseRules if present, otherwise use defaults from gameInfo
                var gameModel = await gameStateMachine.HandleNewGameAsync(gameInfo, newGameMessage.PlayerIds, newGameMessage.GameName ?? "Untitled Game", newGameMessage.HouseRules);


                // Store in registry
                GameStateMachineRegistry.AddGameStateMachine(gameModel.GameId, gameStateMachine);

                // Return minimal response - client must join via SignalR to get GameModel
                return Ok(new { success = true, gameId = gameModel.GameId });
            }
            catch (Exception ex)
            {
                _logger.LogEvent("New Game Error", $"Error creating new game: {ex.Message}", LogLevel.Error);
                return StatusCode(500, $"Error creating new game: {ex.Message}");
            }
        }

        /// <summary>
        /// Updates the house rules for an active game. Can be called mid-game.
        /// Routes through GameStateMachine to maintain undo/redo support.
        /// </summary>
        [HttpPut("game/{gameId}/houserules")]
        public async Task<IActionResult> UpdateHouseRules(string gameId, [FromBody] HouseRules houseRules)
        {
            _logger.LogEvent("API Request", $"PUT /api/game/{gameId}/houserules - Received: GoldTiles={houseRules?.GoldTiles}, SupplementalMinPlayers={houseRules?.SupplementalMinPlayers}");

            try
            {
                if (houseRules == null)
                {
                    return BadRequest("HouseRules cannot be null");
                }

                var gameStateMachine = GameStateMachineRegistry.GetGameStateMachine(gameId);
                if (gameStateMachine == null)
                {
                    return NotFound($"Game {gameId} not found in registry");
                }

                // Route through GameStateMachine to maintain undo/redo support
                var message = new UpdateHouseRulesMessage(houseRules);
                var gameModel = await gameStateMachine.HandleUpdateHouseRulesAsync(message);

                // Save to database and broadcast to all clients
                await ProcessGameActionResult(gameStateMachine, gameModel, "UpdateHouseRules");

                return Ok(new { success = true, message = $"House rules updated: GoldTiles={houseRules.GoldTiles}" });
            }
            catch (Exception ex)
            {
                _logger.LogEvent("Update HouseRules Error", $"Error updating house rules: {ex.Message}", LogLevel.Error);
                return StatusCode(500, $"Error updating house rules: {ex.Message}");
            }
        }

        /// <summary>
        /// Shuffles the board tiles during game setup
        /// </summary>
        [HttpPost("game/{gameId}/shuffle")]
        public async Task<IActionResult> Shuffle(string gameId, [FromBody] CommandRequest request)
        {
            _logger.LogEvent("API Request", $"POST /api/game/{gameId}/shuffle - Shuffling board");

            try
            {
                // Validate request
                if (string.IsNullOrEmpty(request.PlayerId))
                {
                    return BadRequest(new CommandResponse
                    {
                        Success = false,
                        Error = "Missing playerId",
                        ErrorCode = "INVALID_PARAMETERS",
                        GameId = gameId
                    });
                }

                // Get game state machine
                GameStateMachine gameStateMachine;
                try
                {
                    gameStateMachine = GameStateMachineRegistry.GetGameStateMachine(gameId);
                }
                catch (GameException)
                {
                    return NotFound(new CommandResponse
                    {
                        Success = false,
                        Error = $"Game {gameId} not found",
                        ErrorCode = "GAME_NOT_FOUND",
                        GameId = gameId
                    });
                }

                // Validate player is current player
                var currentState = gameStateMachine.GetCurrentState();
                if (currentState.CurrentPlayerId != request.PlayerId)
                {
                    return StatusCode(403, new CommandResponse
                    {
                        Success = false,
                        Error = $"Player {request.PlayerId} cannot act - current player is {currentState.CurrentPlayerId}",
                        ErrorCode = "INVALID_PLAYER",
                        GameId = gameId
                    });
                }

                // Execute shuffle
                var updatedGameModel = await gameStateMachine.HandleShuffleAsync(new ShuffleMessage());

                // Save to database and broadcast to clients
                await ProcessGameActionResult(gameStateMachine, updatedGameModel, "Shuffle");

                return Ok(new CommandResponse
                {
                    Success = true,
                    Message = "Board shuffled successfully",
                    GameId = gameId
                });
            }
            catch (GameException ex)
            {
                _logger.LogEvent("Shuffle Error", $"Game error during shuffle: {ex.Message}", LogLevel.Warning);
                return BadRequest(new CommandResponse
                {
                    Success = false,
                    Error = ex.Message,
                    ErrorCode = "INVALID_STATE",
                    GameId = gameId
                });
            }
            catch (Exception ex)
            {
                _logger.LogEvent("Shuffle Error", $"Error shuffling board: {ex.Message}", LogLevel.Error);
                return StatusCode(500, new CommandResponse
                {
                    Success = false,
                    Error = $"Error shuffling board: {ex.Message}",
                    ErrorCode = "INTERNAL_ERROR",
                    GameId = gameId
                });
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

        /// <summary>
        /// Loads a game from database into memory (creates GameStateMachine in registry).
        /// Must be called before joining the game via SignalR.
        /// </summary>
        [HttpPost("game/{gameId}/load")]
        public async Task<IActionResult> LoadGameFromDatabase(string gameId)
        {
            _logger.LogEvent("API Request", $"POST /api/game/{gameId}/load - Loading game from database");

            try
            {
                // Check if already loaded
                try
                {
                    GameStateMachineRegistry.GetGameStateMachine(gameId);
                    _logger.LogEvent("Game Already Loaded", $"Game {gameId} already in memory");
                    return Ok(new { success = true, gameId = gameId, message = "Game already loaded" });
                }
                catch (GameException)
                {
                    // Not loaded, continue to load from database
                }

                // Load from database using two-table structure
                var gameMetadata = await _dbContext.GameSaveMetadata
                    .Include(m => m.GameData)
                    .FirstOrDefaultAsync(m => m.GameId == gameId);

                if (gameMetadata == null)
                {
                    _logger.LogEvent("Game Not Found", $"Game {gameId} not found in database", LogLevel.Warning);
                    return NotFound(new { success = false, error = $"Game {gameId} not found in database" });
                }

                // Decompress and deserialize the log from the data table
                var decompressedJson = JsonHelper.Decompress(gameMetadata.GameData.CompressedData);
                var serializableLog = JsonHelper.Deserialize<Catan3.Shared.Interfaces.SerializableLog>(decompressedJson);
                if (serializableLog == null)
                {
                    return StatusCode(500, new { success = false, error = "Failed to deserialize game log" });
                }

                // Create Log from serializable log
                var gameLog = Catan3.Shared.Utility.Log<string>.FromSerializableLog(serializableLog, _persistenceService, string.Empty);

                // Create GameStateMachine with the log
                var gameStateMachine = CreateGameStateMachineWithServiceDependencies(gameLog);

                // Get the current game state
                var gameModel = gameStateMachine.GetCurrentState();

                // Store in registry
                GameStateMachineRegistry.AddGameStateMachine(gameId, gameStateMachine);

                _logger.LogEvent("Game Loaded", $"Game {gameId} loaded from database - State: {gameModel.GameState}, Players: {gameModel.Players.Count}");

                return Ok(new { success = true, gameId = gameId, message = "Game loaded successfully" });
            }
            catch (Exception ex)
            {
                _logger.LogEvent("Load Game Error", $"Error loading game {gameId} from database: {ex.Message}", LogLevel.Error);
                return StatusCode(500, new { success = false, error = $"Error loading game: {ex.Message}" });
            }
        }

        /// <summary>
        /// Gets saved games from database for Load Game page.
        /// Pass playerId="*" to get all games, or a specific playerId to filter.
        /// Excludes games where GameState == "GameOver".
        /// </summary>
        [HttpGet("games")]
        public async Task<IActionResult> GetSavedGames([FromQuery] string playerId = "*")
        {
            _logger.LogEvent("API Request", $"GET /api/games - Getting saved games (playerId={playerId})");

            try
            {
                var query = _dbContext.GameSaveMetadata
                    .Include(m => m.GameData)
                    .Where(m => m.GameState != "GameOver"); // Exclude completed games

                // Filter by playerId unless "*" (get all)
                if (playerId != "*" && !string.IsNullOrEmpty(playerId))
                {
                    query = query.Where(m => m.StartedBy == playerId);
                }

                var games = await query
                    .OrderByDescending(m => m.SavedAt)
                    .Select(m => new
                    {
                        m.GameId,
                        m.GameName,
                        m.GameState,
                        m.GameType,
                        m.PlayerCount,
                        m.PlayerNames,
                        m.TurnCount,
                        Size = m.GameData.Size,
                        m.SavedAt,
                        m.CreatedAt
                    })
                    .ToListAsync();

                _logger.LogEvent("Saved Games", $"Found {games.Count} saved games");

                return Ok(new
                {
                    success = true,
                    games = games,
                    count = games.Count,
                    timestamp = DateTime.UtcNow.ToString("O")
                });
            }
            catch (Exception ex)
            {
                _logger.LogEvent("Get Saved Games Error", $"Error getting saved games: {ex.Message}", LogLevel.Error);
                return StatusCode(500, $"Error getting saved games: {ex.Message}");
            }
        }

        /// <summary>
        /// Gets all available players for game creation.
        /// Returns player profiles with relative image URIs.
        /// </summary>
        [HttpGet("players")]
        public async Task<IActionResult> GetPlayers()
        {
            _logger.LogEvent("API Request", $"GET /api/players - Getting available players");

            try
            {
                // Get players from database
                var playerEntities = await _dbContext.Players.ToListAsync();
                var players = playerEntities
                    .Select(e => JsonHelper.Deserialize<PlayerProfile>(e.Data))
                    .Where(p => p != null)
                    .ToList();

                _logger.LogEvent("Players Retrieved", $"Returning {players.Count} players from database");

                return Ok(new
                {
                    success = true,
                    players = players,
                    count = players.Count,
                    timestamp = DateTime.UtcNow.ToString("O")
                });
            }
            catch (Exception ex)
            {
                _logger.LogEvent("Get Players Error", $"Error getting players: {ex.Message}", LogLevel.Error);
                return StatusCode(500, $"Error getting players: {ex.Message}");
            }
        }

        /// <summary>
        /// Creates a new player.
        /// </summary>
        [HttpPost("players")]
        public async Task<IActionResult> CreatePlayer([FromBody] PlayerProfile player)
        {
            _logger.LogEvent("API Request", $"POST /api/players - Creating player: {player.Name}");

            try
            {
                // Check if player ID already exists
                var existing = await _dbContext.Players.FindAsync(player.Id);
                if (existing != null)
                {
                    return BadRequest(new { success = false, error = $"Player with ID '{player.Id}' already exists" });
                }

                // Create player entity
                var playerEntity = new PlayerEntity
                {
                    Id = player.Id,
                    Data = JsonHelper.Serialize(player)
                };

                _dbContext.Players.Add(playerEntity);
                await _dbContext.SaveChangesAsync();

                _logger.LogEvent("Player Created", $"Created player: {player.Id} ({player.Name})");

                return Ok(new { success = true, player = player });
            }
            catch (Exception ex)
            {
                _logger.LogEvent("Create Player Error", $"Error creating player: {ex.Message}", LogLevel.Error);
                return StatusCode(500, new { success = false, error = $"Error creating player: {ex.Message}" });
            }
        }

        /// <summary>
        /// Updates an existing player.
        /// Broadcasts PlayersUpdated to all games containing this player.
        /// </summary>
        [HttpPut("players/{id}")]
        public async Task<IActionResult> UpdatePlayer(string id, [FromBody] PlayerProfile player)
        {
            _logger.LogEvent("API Request", $"PUT /api/players/{id} - Updating player");

            try
            {
                var existing = await _dbContext.Players.FindAsync(id);
                if (existing == null)
                {
                    return NotFound(new { success = false, error = $"Player '{id}' not found" });
                }

                // Update the data
                existing.Data = JsonHelper.Serialize(player);
                await _dbContext.SaveChangesAsync();

                _logger.LogEvent("Player Updated", $"Updated player: {id} ({player.Name})");

                // Notify all games containing this player
                await NotifyPlayersUpdated(new[] { player });

                return Ok(new { success = true, player = player });
            }
            catch (Exception ex)
            {
                _logger.LogEvent("Update Player Error", $"Error updating player {id}: {ex.Message}", LogLevel.Error);
                return StatusCode(500, new { success = false, error = $"Error updating player: {ex.Message}" });
            }
        }

        /// <summary>
        /// Deletes a player.
        /// </summary>
        [HttpDelete("players/{id}")]
        public async Task<IActionResult> DeletePlayer(string id)
        {
            _logger.LogEvent("API Request", $"DELETE /api/players/{id} - Deleting player");

            try
            {
                var existing = await _dbContext.Players.FindAsync(id);
                if (existing == null)
                {
                    return NotFound(new { success = false, error = $"Player '{id}' not found" });
                }

                _dbContext.Players.Remove(existing);

                // Also delete associated image if exists
                var image = await _dbContext.Images.FindAsync(id);
                if (image != null)
                {
                    _dbContext.Images.Remove(image);
                }

                await _dbContext.SaveChangesAsync();

                _logger.LogEvent("Player Deleted", $"Deleted player: {id}");

                return Ok(new { success = true, message = $"Player '{id}' deleted" });
            }
            catch (Exception ex)
            {
                _logger.LogEvent("Delete Player Error", $"Error deleting player {id}: {ex.Message}", LogLevel.Error);
                return StatusCode(500, new { success = false, error = $"Error deleting player: {ex.Message}" });
            }
        }

        /// <summary>
        /// Uploads an image for a player.
        /// </summary>
        [HttpPost("players/{id}/image")]
        public async Task<IActionResult> UploadPlayerImage(string id, IFormFile file)
        {
            _logger.LogEvent("API Request", $"POST /api/players/{id}/image - Uploading image");

            try
            {
                // Verify player exists
                var player = await _dbContext.Players.FindAsync(id);
                if (player == null)
                {
                    return NotFound(new { success = false, error = $"Player '{id}' not found" });
                }

                // Validate file
                if (file == null || file.Length == 0)
                {
                    return BadRequest(new { success = false, error = "No file uploaded" });
                }

                var allowedTypes = new[] { "image/jpeg", "image/png", "image/gif" };
                if (!allowedTypes.Contains(file.ContentType.ToLower()))
                {
                    return BadRequest(new { success = false, error = "Invalid file type. Allowed: JPEG, PNG, GIF" });
                }

                // Read file data
                using var memoryStream = new MemoryStream();
                await file.CopyToAsync(memoryStream);
                var imageData = memoryStream.ToArray();

                // Create or update image entity
                var existingImage = await _dbContext.Images.FindAsync(id);
                if (existingImage != null)
                {
                    existingImage.Data = imageData;
                    existingImage.ContentType = file.ContentType;
                }
                else
                {
                    var imageEntity = new ImageEntity
                    {
                        Id = id,
                        Data = imageData,
                        ContentType = file.ContentType
                    };
                    _dbContext.Images.Add(imageEntity);
                }

                // Update player's ImageUri
                var playerProfile = JsonHelper.Deserialize<PlayerProfile>(player.Data);
                if (playerProfile != null)
                {
                    var updatedProfile = new PlayerProfile(
                        playerProfile.Id,
                        playerProfile.Name,
                        playerProfile.Colors,
                        $"/api/images/{id}",
                        playerProfile.LifetimeStats
                    );
                    player.Data = JsonHelper.Serialize(updatedProfile);
                }

                await _dbContext.SaveChangesAsync();

                _logger.LogEvent("Image Uploaded", $"Uploaded image for player: {id} ({imageData.Length} bytes)");

                // Notify games with this player about the image change
                var profileToNotify = JsonHelper.Deserialize<PlayerProfile>(player.Data);
                if (profileToNotify != null)
                {
                    await NotifyPlayersUpdated(new[] { profileToNotify });
                }

                return Ok(new { success = true, imageUri = $"/api/images/{id}" });
            }
            catch (Exception ex)
            {
                _logger.LogEvent("Upload Image Error", $"Error uploading image for {id}: {ex.Message}", LogLevel.Error);
                return StatusCode(500, new { success = false, error = $"Error uploading image: {ex.Message}" });
            }
        }

        /// <summary>
        /// Notifies all games containing the specified players that their profiles have been updated.
        /// Sends a PlayersUpdated SignalR message to each affected game's group.
        /// </summary>
        private async Task NotifyPlayersUpdated(IEnumerable<PlayerProfile> updatedPlayers)
        {
            var playerList = updatedPlayers.ToList();
            if (playerList.Count == 0) return;

            var playerIds = playerList.Select(p => p.Id);
            var affectedGameIds = GameStateMachineRegistry.GetGamesWithPlayers(playerIds);

            _logger.LogEvent("PlayersUpdated", $"Notifying {affectedGameIds.Count} game(s) about {playerList.Count} updated player(s)");

            foreach (var gameId in affectedGameIds)
            {
                await _hubContext.Clients.Group(gameId).SendAsync("PlayersUpdated", playerList);
            }
        }

        /// <summary>
        /// Gets an image by ID from the database.
        /// </summary>
        [HttpGet("images/{id}")]
        public async Task<IActionResult> GetImage(string id)
        {
            _logger.LogEvent("API Request", $"GET /api/images/{id} - Getting image");

            try
            {
                var imageEntity = await _dbContext.Images.FindAsync(id);
                if (imageEntity == null)
                {
                    _logger.LogEvent("Image Not Found", $"Image not found: {id}", LogLevel.Warning);
                    return NotFound($"Image {id} not found");
                }

                return File(imageEntity.Data, imageEntity.ContentType);
            }
            catch (Exception ex)
            {
                _logger.LogEvent("Get Image Error", $"Error getting image {id}: {ex.Message}", LogLevel.Error);
                return StatusCode(500, $"Error getting image: {ex.Message}");
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

        [HttpPost("game/end")]
        public IActionResult EndGame([FromBody] JsonElement request)
        {
            _logger.LogEvent("API Request", $"POST /api/game/end - Ending game");

            try
            {
                var gameId = request.GetProperty("gameId").GetString();

                if (string.IsNullOrEmpty(gameId))
                {
                    _logger.LogEvent("Validation Error", $"Missing gameId in end game request", LogLevel.Warning);
                    return BadRequest("Missing gameId");
                }

                // Remove game from registry and dispose resources
                bool gameRemoved = GameStateMachineRegistry.DeleteGameStateMachine(gameId);

                if (!gameRemoved)
                {
                    _logger.LogEvent("Game Not Found", $"Game not found: {gameId}", LogLevel.Warning);
                    return NotFound($"Game {gameId} not found");
                }

                _logger.LogEvent("Game Ended", $"Game ended successfully - GameId: {gameId}");

                return Ok(new
                {
                    success = true,
                    message = $"Game {gameId} ended successfully"
                });
            }
            catch (GameException ex)
            {
                _logger.LogEvent("Game Not Found", $"Game not found: {ex.Message}", LogLevel.Warning);
                return NotFound(ex.Message);
            }
            catch (Exception ex)
            {
                _logger.LogEvent("End Game Error", $"Error ending game: {ex.Message}", LogLevel.Error);
                return StatusCode(500, $"Error ending game: {ex.Message}");
            }
        }

        [HttpPost("settings/update")]
        public IActionResult UpdateSettings([FromBody] JsonElement request)
        {
            _logger.LogEvent("API Request", $"POST /api/settings/update - Updating service settings");

            try
            {
                // For now, we'll just acknowledge the settings update
                // In the future, this could store settings that affect game logic
                var settingsCount = 0;
                if (request.TryGetProperty("settings", out var settingsElement))
                {
                    settingsCount = settingsElement.EnumerateObject().Count();
                }

                _logger.LogEvent("Settings Updated", $"Service settings updated - {settingsCount} settings received");

                return Ok(new
                {
                    success = true,
                    message = $"Settings updated successfully ({settingsCount} settings)"
                });
            }
            catch (Exception ex)
            {
                _logger.LogEvent("Settings Update Error", $"Error updating settings: {ex.Message}", LogLevel.Error);
                return StatusCode(500, $"Error updating settings: {ex.Message}");
            }
        }

        /// <summary>
        /// Returns database health status for script decisions.
        /// Used by webui.ps1 to determine if games need to be seeded.
        /// </summary>
        [HttpGet("database/health")]
        public async Task<IActionResult> GetDatabaseHealth()
        {
            _logger.LogEvent("API Request", $"GET /api/database/health - Checking database health");

            try
            {
                var playerCount = await _dbContext.Players.CountAsync();
                var gameCount = await _dbContext.GameSaveMetadata.CountAsync();

                var result = new
                {
                    healthy = true,
                    playerCount = playerCount,
                    gameCount = gameCount,
                    needsSeeding = playerCount == 0,
                    needsGames = gameCount == 0,
                    timestamp = DateTime.UtcNow.ToString("O")
                };

                _logger.LogEvent("Database Health", $"Players: {playerCount}, Games: {gameCount}, NeedsSeeding: {playerCount == 0}, NeedsGames: {gameCount == 0}");

                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogEvent("Database Health Error", $"Error checking database health: {ex.Message}", LogLevel.Error);
                return Ok(new
                {
                    healthy = false,
                    error = ex.Message,
                    needsSeeding = true,
                    needsGames = true
                });
            }
        }

        /// <summary>
        /// Renames a game. Updates both the in-memory GameModel (if loaded) and database.
        /// Works for games that are only in the database (not yet loaded into memory).
        /// </summary>
        [HttpPatch("game/{gameId}/rename")]
        public async Task<IActionResult> RenameGame(string gameId, [FromQuery] string newName)
        {
            _logger.LogEvent("API Request", $"PATCH /api/game/{gameId}/rename - Renaming to '{newName}'");

            if (string.IsNullOrWhiteSpace(newName))
            {
                return BadRequest(new { success = false, error = "Name cannot be empty" });
            }

            try
            {
                // Check if game is loaded in memory
                GameStateMachine? gameStateMachine = null;
                try
                {
                    gameStateMachine = GameStateMachineRegistry.GetGameStateMachine(gameId);
                }
                catch (GameException)
                {
                    // Game not in registry - that's OK, we'll update database directly
                    _logger.LogEvent("Rename Game", $"Game {gameId} not in memory, updating database only");
                }

                if (gameStateMachine != null)
                {
                    // Game is loaded - update both in-memory and database
                    var currentState = gameStateMachine.GetCurrentState();
                    var oldName = currentState.GameName;

                    if (oldName == newName)
                    {
                        return Ok(new { success = true, gameId, gameName = newName, message = "Name unchanged" });
                    }

                    // Get the serializable log and update all GameNames
                    var sourceLog = gameStateMachine.GetSerializableLog();

                    var newDoneStack = new List<string>();
                    foreach (var gameModelJson in sourceLog.DoneStack)
                    {
                        var updatedJson = gameModelJson.Replace($"\"GameName\":\"{oldName}\"", $"\"GameName\":\"{newName}\"");
                        newDoneStack.Add(updatedJson);
                    }

                    var newRedoStack = new List<string>();
                    foreach (var gameModelJson in sourceLog.RedoStack)
                    {
                        var updatedJson = gameModelJson.Replace($"\"GameName\":\"{oldName}\"", $"\"GameName\":\"{newName}\"");
                        newRedoStack.Add(updatedJson);
                    }

                    // Create updated SerializableLog
                    var updatedSerializableLog = new SerializableLog
                    {
                        DoneStack = newDoneStack,
                        RedoStack = newRedoStack,
                        GameType = sourceLog.GameType,
                        DoneCount = sourceLog.DoneCount,
                        RedoCount = sourceLog.RedoCount
                    };

                    // Create new Log and GameStateMachine with updated data
                    var newGameLog = Log<string>.FromSerializableLog(updatedSerializableLog, _persistenceService, string.Empty);
                    var newGameStateMachine = CreateGameStateMachineWithServiceDependencies(newGameLog);

                    // Replace in registry
                    GameStateMachineRegistry.DeleteGameStateMachine(gameId);
                    GameStateMachineRegistry.AddGameStateMachine(gameId, newGameStateMachine);

                    // Get updated game model
                    var updatedGameModel = newGameStateMachine.GetCurrentState();

                    // Save to database
                    var json = JsonHelper.Serialize(updatedSerializableLog);
                    var compressed = JsonHelper.Compress(json);

                    var metadata = new GameMetadata
                    {
                        GameName = newName,
                        GameState = updatedGameModel.GameState.ToString(),
                        StartedBy = "Rename",
                        PlayerCount = updatedGameModel.Players.Count,
                        GameType = updatedGameModel.Tiles.Count > 19 ? "Expansion" : "Regular",
                        PlayerNames = string.Join(", ", updatedGameModel.Players.Select(p => p.Name)),
                        TurnCount = updatedSerializableLog.DoneCount
                    };

                    await _gamePersistence.SaveAsync(gameId, compressed, metadata);

                    // Broadcast updated game state to all clients
                    await _hubContext.Clients.Group(gameId).SendAsync("GameStateUpdated", updatedGameModel);

                    _logger.LogEvent("Game Renamed", $"Renamed game {gameId}: '{oldName}' -> '{newName}' (in-memory + database)");
                }
                else
                {
                    // Game not loaded - update database directly
                    var gameMetadata = await _dbContext.GameSaveMetadata
                        .Include(m => m.GameData)
                        .FirstOrDefaultAsync(m => m.GameId == gameId);

                    if (gameMetadata == null)
                    {
                        return NotFound(new { success = false, error = $"Game {gameId} not found" });
                    }

                    var oldName = gameMetadata.GameName;
                    if (oldName == newName)
                    {
                        return Ok(new { success = true, gameId, gameName = newName, message = "Name unchanged" });
                    }

                    // Update the game name in the compressed data
                    var decompressedJson = JsonHelper.Decompress(gameMetadata.GameData.CompressedData);
                    var serializableLog = JsonHelper.Deserialize<SerializableLog>(decompressedJson);

                    if (serializableLog != null)
                    {
                        // Update GameName in all states
                        var newDoneStack = serializableLog.DoneStack
                            .Select(json => json.Replace($"\"GameName\":\"{oldName}\"", $"\"GameName\":\"{newName}\""))
                            .ToList();
                        var newRedoStack = serializableLog.RedoStack
                            .Select(json => json.Replace($"\"GameName\":\"{oldName}\"", $"\"GameName\":\"{newName}\""))
                            .ToList();

                        var updatedLog = new SerializableLog
                        {
                            DoneStack = newDoneStack,
                            RedoStack = newRedoStack,
                            GameType = serializableLog.GameType,
                            DoneCount = serializableLog.DoneCount,
                            RedoCount = serializableLog.RedoCount
                        };

                        var updatedJson = JsonHelper.Serialize(updatedLog);
                        var compressed = JsonHelper.Compress(updatedJson);

                        // Update database
                        gameMetadata.GameName = newName;
                        gameMetadata.GameData.CompressedData = compressed;
                        gameMetadata.GameData.Size = compressed.Length;
                        gameMetadata.SavedAt = DateTime.UtcNow;

                        await _dbContext.SaveChangesAsync();
                    }
                    else
                    {
                        // Just update metadata if we can't parse the log
                        gameMetadata.GameName = newName;
                        gameMetadata.SavedAt = DateTime.UtcNow;
                        await _dbContext.SaveChangesAsync();
                    }

                    _logger.LogEvent("Game Renamed", $"Renamed game {gameId}: '{oldName}' -> '{newName}' (database only)");
                }

                return Ok(new { success = true, gameId, gameName = newName, message = "Game renamed successfully" });
            }
            catch (Exception ex)
            {
                _logger.LogEvent("Rename Game Error", $"Error renaming game {gameId}: {ex.Message}", LogLevel.Error);
                return StatusCode(500, new { success = false, error = $"Error renaming game: {ex.Message}" });
            }
        }

        /// <summary>
        /// Creates a deep copy of a game with a new GameId.
        /// Useful for creating common starting positions for tests.
        /// </summary>
        [HttpPost("game/{gameId}/copy")]
        public async Task<IActionResult> CopyGame(string gameId, [FromQuery] string? newName = null)
        {
            _logger.LogEvent("API Request", $"POST /api/game/{gameId}/copy - Copying game");

            try
            {
                // Get the game state machine from registry
                GameStateMachine sourceGameStateMachine;
                try
                {
                    sourceGameStateMachine = GameStateMachineRegistry.GetGameStateMachine(gameId);
                }
                catch (GameException)
                {
                    _logger.LogEvent("Game Not Found", $"Game {gameId} not found in registry", LogLevel.Warning);
                    return NotFound(new { success = false, error = $"Game {gameId} not found" });
                }

                // Get the serializable log (contains DoneStack and RedoStack of serialized GameModels)
                var sourceLog = sourceGameStateMachine.GetSerializableLog();

                // Get original game info for name replacement
                var originalGameModel = sourceGameStateMachine.GetCurrentState();
                var originalName = originalGameModel.GameName;

                // Generate a new GameId and determine new name
                var newGameId = Guid.NewGuid().ToString();
                var gameName = !string.IsNullOrWhiteSpace(newName)
                    ? newName
                    : $"{originalName} (Copy)";

                // Deep copy by serializing and replacing GameId and GameName in the JSON
                var newDoneStack = new List<string>();
                foreach (var gameModelJson in sourceLog.DoneStack)
                {
                    var updatedJson = gameModelJson
                        .Replace($"\"GameId\":\"{gameId}\"", $"\"GameId\":\"{newGameId}\"")
                        .Replace($"\"GameName\":\"{originalName}\"", $"\"GameName\":\"{gameName}\"");
                    newDoneStack.Add(updatedJson);
                }

                var newRedoStack = new List<string>();
                foreach (var gameModelJson in sourceLog.RedoStack)
                {
                    var updatedJson = gameModelJson
                        .Replace($"\"GameId\":\"{gameId}\"", $"\"GameId\":\"{newGameId}\"")
                        .Replace($"\"GameName\":\"{originalName}\"", $"\"GameName\":\"{gameName}\"");
                    newRedoStack.Add(updatedJson);
                }

                // Create new SerializableLog with updated data
                var newSerializableLog = new SerializableLog
                {
                    DoneStack = newDoneStack,
                    RedoStack = newRedoStack,
                    GameType = sourceLog.GameType,
                    DoneCount = sourceLog.DoneCount,
                    RedoCount = sourceLog.RedoCount
                };

                // Create a new Log from the serializable log
                var newGameLog = Log<string>.FromSerializableLog(newSerializableLog, _persistenceService, string.Empty);

                // Create new GameStateMachine
                var newGameStateMachine = CreateGameStateMachineWithServiceDependencies(newGameLog);

                // Get the current state of the new game
                var newGameModel = newGameStateMachine.GetCurrentState();

                // Register in the registry
                GameStateMachineRegistry.AddGameStateMachine(newGameId, newGameStateMachine);

                // Save to database
                var json = JsonHelper.Serialize(newSerializableLog);
                var compressed = JsonHelper.Compress(json);

                var metadata = new GameMetadata
                {
                    GameName = gameName,
                    GameState = newGameModel.GameState.ToString(),
                    StartedBy = "Copy",
                    PlayerCount = newGameModel.Players.Count,
                    GameType = newGameModel.Tiles.Count > 19 ? "Expansion" : "Regular",
                    PlayerNames = string.Join(", ", newGameModel.Players.Select(p => p.Name)),
                    TurnCount = newSerializableLog.DoneCount
                };

                await _gamePersistence.SaveAsync(newGameId, compressed, metadata);

                _logger.LogEvent("Game Copied", $"Copied game {gameId} to {newGameId} - {metadata.PlayerNames}, {metadata.TurnCount} turns");

                return Ok(new
                {
                    success = true,
                    sourceGameId = gameId,
                    newGameId = newGameId,
                    gameName = metadata.GameName,
                    playerNames = metadata.PlayerNames,
                    turnCount = metadata.TurnCount,
                    message = "Game copied successfully"
                });
            }
            catch (Exception ex)
            {
                _logger.LogEvent("Copy Game Error", $"Error copying game {gameId}: {ex.Message}", LogLevel.Error);
                return StatusCode(500, new { success = false, error = $"Error copying game: {ex.Message}" });
            }
        }

        /// <summary>
        /// Imports a .catan file into the database.
        /// Used for seeding default games and future user uploads.
        /// </summary>
        [HttpPost("game/import")]
        public async Task<IActionResult> ImportGame(IFormFile file)
        {
            _logger.LogEvent("API Request", $"POST /api/game/import - Importing game file: {file?.FileName}");

            try
            {
                if (file == null || file.Length == 0)
                {
                    return BadRequest(new { success = false, error = "No file provided" });
                }

                // Read the compressed .catan file bytes
                byte[] compressedData;
                using (var memoryStream = new MemoryStream())
                {
                    await file.CopyToAsync(memoryStream);
                    compressedData = memoryStream.ToArray();
                }

                // Decompress to get the SerializableLog JSON
                var json = JsonHelper.Decompress(compressedData);
                var serializableLog = JsonHelper.Deserialize<Catan3.Shared.Interfaces.SerializableLog>(json);

                if (serializableLog == null || serializableLog.DoneCount == 0)
                {
                    return BadRequest(new { success = false, error = "Invalid or empty .catan file" });
                }

                // Get the current game state from the top of the done stack
                var currentGameJson = serializableLog.DoneStack.LastOrDefault();
                if (currentGameJson == null)
                {
                    return BadRequest(new { success = false, error = "No game states in file" });
                }

                var gameModel = JsonHelper.Deserialize<GameModel>(currentGameJson);
                if (gameModel == null)
                {
                    return BadRequest(new { success = false, error = "Could not deserialize game model" });
                }

                // Use the GameId from the file
                var gameId = gameModel.GameId;

                // Check if game already exists
                var existingGame = await _dbContext.GameSaveMetadata.FirstOrDefaultAsync(m => m.GameId == gameId);
                if (existingGame != null)
                {
                    _logger.LogEvent("Game Already Exists", $"Game {gameId} already exists in database, skipping import");
                    return Ok(new
                    {
                        success = true,
                        gameId = gameId,
                        gameName = gameModel.GameName,
                        playerNames = string.Join(", ", gameModel.Players.Select(p => p.Name)),
                        turnCount = serializableLog.DoneCount,
                        message = "Game already exists"
                    });
                }

                // Build metadata from GameModel properties
                var metadata = new GameMetadata
                {
                    GameName = gameModel.GameName,
                    GameState = gameModel.GameState.ToString(),
                    StartedBy = "Import",
                    PlayerCount = gameModel.Players.Count,
                    GameType = gameModel.Tiles.Count > 19 ? "Expansion" : "Regular",
                    PlayerNames = string.Join(", ", gameModel.Players.Select(p => p.Name)),
                    TurnCount = serializableLog.DoneCount
                };

                // Save to database
                await _gamePersistence.SaveAsync(gameId, compressedData, metadata);

                _logger.LogEvent("Game Imported", $"Imported game: {gameId} ({metadata.PlayerNames}) - {metadata.TurnCount} turns");

                return Ok(new
                {
                    success = true,
                    gameId = gameId,
                    gameName = gameModel.GameName,
                    playerNames = metadata.PlayerNames,
                    turnCount = metadata.TurnCount,
                    message = "Game imported successfully"
                });
            }
            catch (Exception ex)
            {
                _logger.LogEvent("Import Game Error", $"Error importing game: {ex.Message}", LogLevel.Error);
                return StatusCode(500, new { success = false, error = $"Error importing game: {ex.Message}" });
            }
        }
    }
}