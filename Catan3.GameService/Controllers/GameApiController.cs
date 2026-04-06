using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Options;
using System.Text.Json;
using Catan3.Shared.Models;
using Catan3.Shared.Utility;
using Catan3.Shared.Profiles;
using Catan3.Shared.GameLogic;
using Catan3.Shared.Extensions;
using Catan3.Shared.Interfaces;
using Catan3.GameService.Abstractions;
using Catan3.GameService.Services;
using Catan3.GameService.Utility;
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
        private readonly ICatanDb _db;
        private readonly IHubContext<GameHub> _hubContext;
        private readonly IGamePersistence _gamePersistence;
        private readonly RecordingService _recordingService;
        private readonly GameTemplateService _templateService;

        public GameApiController(
            IOptions<GameApiOptions> options,
            IPersistenceService persistenceService,
            ILoggerFactory loggerFactory,
            ILogger<GameApiController> logger,
            ICatanDb db,
            IHubContext<GameHub> hubContext,
            IGamePersistence gamePersistence,
            RecordingService recordingService,
            GameTemplateService templateService)
        {
            _options = options.Value;
            _persistenceService = persistenceService;
            _loggerFactory = loggerFactory;
            _logger = logger;
            _db = db;
            _hubContext = hubContext;
            _gamePersistence = gamePersistence;
            _recordingService = recordingService;
            _templateService = templateService;
        }

        /// <summary>
        /// Records an action if recording is active for the game.
        /// Saves to database immediately for crash recovery.
        /// </summary>
        private async Task TryRecordActionAsync(string gameId, IRecordedMessage message)
        {
            if (_recordingService.IsRecording(gameId))
            {
                await _recordingService.RecordActionAsync(gameId, message);
            }
        }

        /// <summary>
        /// Common post-action processing: save to database and broadcast to clients
        /// </summary>
        // Gameplay persistence is handled exclusively by Log.RequestSave() via
        // GameStateMachine.LogGameModel(). No other gameplay code path should save.
        // Non-gameplay saves (copy, import, rename) use _gamePersistence directly.

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

                // Resolve template: prefer TemplateId, fall back to GameType mapping
                IGameMetadata gameInfo;
                if (!string.IsNullOrEmpty(newGameMessage.TemplateId))
                {
                    var templateData = await _templateService.GetAsync(newGameMessage.TemplateId);
                    if (templateData is null)
                        return BadRequest($"Template '{newGameMessage.TemplateId}' not found");
                    gameInfo = new Catan3.GameService.Factory.BoardInfoJsonAdapter(templateData);
                }
                else
                {
                    // Backward compatibility: map GameType to default template
                    var templateId = newGameMessage.GameType == GameType.Regular
                        ? "regular" : "expansion";
                    var templateData = await _templateService.GetAsync(templateId);
                    if (templateData is null)
                    {
                        // Fallback to hardcoded singletons if templates not seeded
                        gameInfo = newGameMessage.GameType == GameType.Regular
                            ? RegularBoardInfo.Default
                            : ExpansionBoardInfo.Default;
                    }
                    else
                    {
                        gameInfo = new Catan3.GameService.Factory.BoardInfoJsonAdapter(templateData);
                    }
                }

                // Generate a temporary save file path for the game log
                var tempSaveFilePath = $"{newGameMessage.GameName ?? "Untitled Game"}-{Guid.NewGuid()}.catan";

                // Create the Log for this game (no logger needed for basic functionality)
                var gameLog = new Shared.Utility.Log<string>(_persistenceService, tempSaveFilePath);

                // Create GameStateMachine with the Log
                var gameStateMachine = CreateGameStateMachineWithServiceDependencies(gameLog);

                // Use the GameStateMachine to create the fully initialized game
                // Pass client-provided HouseRules if present, otherwise use defaults from gameInfo
                var gameModel = await gameStateMachine.HandleNewGameAsync(gameInfo, newGameMessage.PlayerIds, newGameMessage.GameName ?? "Untitled Game", newGameMessage.HouseRules, newGameMessage.Seed);

                // Set SaveLifetimeStats from request (default is true)
                gameModel.SaveLifetimeStats = newGameMessage.SaveLifetimeStats;

                // Store in registry
                GameStateMachineRegistry.AddGameStateMachine(gameModel.GameId, gameStateMachine);

                // Note: Save happens automatically via GameStateMachine.LogGameModel() -> Log.SaveAsync()

                // Start recording if requested (supplementary — don't fail game creation)
                if (newGameMessage.RecordGame)
                {
                    try
                    {
                        await _recordingService.StartRecordingAsync(gameModel.GameId, newGameMessage.GameName ?? "Untitled Game", gameModel);
                    }
                    catch (Exception recEx)
                    {
                        _logger.LogEvent("Recording", $"Failed to start recording for game {gameModel.GameId}: {recEx.Message}", LogLevel.Warning);
                    }
                }

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

                // Broadcast to all clients (save handled by Log.RequestSave via LogGameModel)
                await _hubContext.Clients.Group(gameModel.GameId).SendAsync("GameStateUpdated", gameModel);
                _logger.LogEvent("Send Client Update", $"GameStateUpdated sent for UpdateHouseRules - GameId={gameModel.GameId}");

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
                var shuffleMessage = new ShuffleMessage();
                var updatedGameModel = await gameStateMachine.HandleShuffleAsync(shuffleMessage);

                // Record action if recording is active
                await TryRecordActionAsync(gameId, new ShuffleRecord(updatedGameModel, shuffleMessage));

                // Broadcast to all clients (save handled by Log.RequestSave via LogGameModel)
                await _hubContext.Clients.Group(gameId).SendAsync("GameStateUpdated", updatedGameModel);
                _logger.LogEvent("Send Client Update", $"GameStateUpdated sent for Shuffle - GameId={gameId}");

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

        /// <summary>
        /// Declares the current player as the winner, updates lifetime stats, and archives the game.
        /// </summary>
        [HttpPost("game/{gameId}/winner")]
        public async Task<IActionResult> DeclareWinner(string gameId, [FromBody] DeclareWinnerRequest request)
        {
            _logger.LogEvent("API Request", $"POST /api/game/{gameId}/winner - Declaring winner");

            try
            {
                // Validate request
                if (string.IsNullOrEmpty(request.WinnerId))
                {
                    return BadRequest(new CommandResponse
                    {
                        Success = false,
                        Error = "Missing winnerId",
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

                var currentState = gameStateMachine.GetCurrentState();

                // Validate game is not already over
                if (currentState.GameState == Shared.Models.GameState.GameOver)
                {
                    return BadRequest(new CommandResponse
                    {
                        Success = false,
                        Error = "Game is already over",
                        ErrorCode = "GAME_ALREADY_OVER",
                        GameId = gameId
                    });
                }

                // Validate winner is a player in this game
                var winner = currentState.Players.FirstOrDefault(p => p.Id == request.WinnerId);
                if (winner == null)
                {
                    return BadRequest(new CommandResponse
                    {
                        Success = false,
                        Error = $"Player {request.WinnerId} is not in this game",
                        ErrorCode = "INVALID_PLAYER",
                        GameId = gameId
                    });
                }

                // Get winner name for response
                var winnerName = winner.Name;

                // Execute declare winner action (includes VP card counts if any)
                var declareWinnerMessage = new DeclareWinnerMessage(request.WinnerId, request.VictoryPoints ?? new Dictionary<string, int>());
                var updatedGameModel = await gameStateMachine.HandleDeclareWinnerAsync(declareWinnerMessage);

                // Record action if recording is active
                await TryRecordActionAsync(gameId, new DeclareWinnerRecord(updatedGameModel, declareWinnerMessage));

                // Broadcast immediately — clients see the winner before any I/O
                await _hubContext.Clients.Group(updatedGameModel.GameId).SendAsync("GameStateUpdated", updatedGameModel);
                _logger.LogEvent("Send Client Update", $"GameStateUpdated sent for DeclareWinner - GameId={gameId}");

                // Post-game I/O: update lifetime stats, archive — all after broadcast
                // Game save handled by Log.RequestSave() via LogGameModel

                if (currentState.SaveLifetimeStats)
                {
                    await UpdatePlayerLifetimeStats(updatedGameModel, request.WinnerId);
                }
                else
                {
                    _logger.LogEvent("Stats Skipped", $"Game {gameId}: SaveLifetimeStats=false, skipping stats update");
                }

                await ArchiveCompletedGame(gameId, gameStateMachine, updatedGameModel, request.WinnerId, winnerName);

                _logger.LogEvent("Winner Declared", $"Game {gameId}: {winnerName} declared winner");

                return Ok(new
                {
                    success = true,
                    message = $"Winner recorded: {winnerName}",
                    gameId = gameId,
                    winnerId = request.WinnerId,
                    winnerName = winnerName
                });
            }
            catch (Exception ex)
            {
                _logger.LogEvent("Declare Winner Error", $"Error declaring winner: {ex.Message}", LogLevel.Error);
                return StatusCode(500, new CommandResponse
                {
                    Success = false,
                    Error = $"Error declaring winner: {ex.Message}",
                    ErrorCode = "INTERNAL_ERROR",
                    GameId = gameId
                });
            }
        }

        /// <summary>
        /// Updates lifetime statistics for all players in the completed game.
        /// </summary>
        private async Task UpdatePlayerLifetimeStats(GameModel gameModel, string winnerId)
        {
            // Load all player profiles for participants
            var profileMap = new Dictionary<string, PlayerProfile>();
            foreach (var player in gameModel.Players)
            {
                var profile = await _db.LoadPlayerAsync(player.Id);
                if (profile != null)
                    profileMap[player.Id] = profile;
            }

            foreach (var player in gameModel.Players)
            {
                if (!profileMap.TryGetValue(player.Id, out var profile))
                {
                    _logger.LogEvent("Stats Update", $"Player {player.Id} not found in database, skipping stats update", LogLevel.Warning);
                    continue;
                }

                // Calculate game stats from player's current game data
                var gameStats = CalculateGameStats(player, gameModel);

                // Use the state-machine-computed score (includes VP cards, longest road, largest army)
                var score = player.Score;

                // Get player-specific stats for records
                var soldiersPlayed = player.SpentEntitlementsThisGame?.Count(e => e == Shared.Models.Entitlement.Soldier) ?? 0;

                // Log captured stats for debugging
                _logger.LogEvent("Stats Capture", $"Player {player.Name}: Stars={player.Stars}, GoodRolls={player.GoodRolls}, BadRolls={player.BadRolls}, " +
                    $"TimesTargeted={player.TimesTargeted}, Robber={player.ResourcesThisGame?.Robber ?? 0}, Soldiers={soldiersPlayed}, " +
                    $"Resources={player.ResourcesThisGame?.Count ?? 0}, LongestRoad={player.LongestRoad}, Score={score}");

                // Update lifetime stats with all available data
                var currentStats = profile.LifetimeStats ?? LifetimeStats.Empty;
                var isWinner = player.Id == winnerId;
                var updatedStats = currentStats.AddGame(
                    gameStats,
                    won: isWinner,
                    hasLongestRoad: player.HasLongestRoad,
                    hasLargestArmy: player.LargestArmy,
                    roadLength: player.LongestRoad,
                    soldiersPlayed: soldiersPlayed,
                    stars: player.Stars,
                    score: score
                );

                // Create updated profile with new stats
                var updatedProfile = new PlayerProfile(
                    profile.Id,
                    profile.Name,
                    profile.Colors,
                    profile.ImageUri,
                    updatedStats
                );

                // Save back to database
                await _db.SavePlayerAsync(updatedProfile);
                _logger.LogEvent("Stats Update", $"Updated stats for {player.Name}: Games={updatedStats.GamesPlayed}, Wins={updatedStats.Wins}, " +
                    $"MostStars={updatedStats.MostStarsRecord}, AveStars={updatedStats.AverageStars:F1}, " +
                    $"Targeted={updatedStats.Totals.TimesTargeted}, Robber={updatedStats.Totals.ResourcesLostToRobber}");
            }
        }

        /// <summary>
        /// Calculates game statistics from player model and game state.
        /// Captures all available stats from PlayerModel and GameModel.
        /// </summary>
        private GameStats CalculateGameStats(PlayerModel player, GameModel gameModel)
        {
            // Count built pieces from GameModel
            var roadsBuilt = gameModel.Roads.Count(r => r.OwnerId == player.Id);
            var settlementsBuilt = gameModel.Buildings.Count(b =>
                b.OwnerId == player.Id && b.BuildingState == Shared.Models.BuildingState.Settlement);
            var citiesBuilt = gameModel.Buildings.Count(b =>
                b.OwnerId == player.Id && b.BuildingState == Shared.Models.BuildingState.City);

            // Get resource stats from PlayerModel
            var resourcesCollected = player.ResourcesThisGame?.Count ?? 0;
            var resourcesLostToRobber = player.ResourcesThisGame?.Robber ?? 0;

            // Count soldiers played from entitlements
            var soldiersPlayed = player.SpentEntitlementsThisGame?.Count(e => e == Shared.Models.Entitlement.Soldier) ?? 0;

            return new GameStats(
                ResourcesCollected: resourcesCollected,
                ResourcesLostToRobber: resourcesLostToRobber,
                RoadsBuilt: roadsBuilt,
                SettlementsBuilt: settlementsBuilt,
                CitiesBuilt: citiesBuilt,
                SoldiersPlayed: soldiersPlayed,
                TimesTargeted: player.TimesTargeted,
                GoodRolls: player.GoodRolls,
                BadRolls: player.BadRolls,
                StarsEarned: player.Stars
            );
        }


        /// <summary>
        /// Archives the completed game to the CompletedGames table.
        /// </summary>
        private async Task ArchiveCompletedGame(string gameId, GameStateMachine gameStateMachine, GameModel gameModel, string winnerId, string winnerName)
        {
            try
            {
                var serializableLog = gameStateMachine.GetSerializableLog();
                var json = JsonHelper.Serialize(serializableLog);
                var compressed = JsonHelper.Compress(json);

                var completedGame = new CompletedGameRecord
                {
                    GameId = gameId,
                    GameName = gameModel.GameName,
                    WinnerId = winnerId,
                    WinnerName = winnerName,
                    CompletedAt = DateTime.UtcNow,
                    StartedAt = gameModel.CreatedTime,
                    PlayerCount = gameModel.Players.Count,
                    PlayerNames = string.Join(", ", gameModel.Players.Select(p => p.Name)),
                    TurnCount = serializableLog.DoneCount,
                    CompressedData = compressed,
                    Size = compressed.Length
                };

                await _db.SaveCompletedGameAsync(completedGame);

                _logger.LogEvent("Game Archived", $"Archived completed game {gameId} ({completedGame.Size} bytes)");
            }
            catch (Exception ex)
            {
                _logger.LogEvent("Archive Error", $"Failed to archive game {gameId}: {ex.Message}", LogLevel.Warning);
                // Don't throw - archival failure shouldn't block winner declaration
            }
        }

        /// <summary>
        /// Loads a game from the database into the registry if not already in memory.
        /// Returns the GameStateMachine, or null if the game is not found.
        /// </summary>
        private async Task<GameStateMachine?> EnsureGameLoadedAsync(string gameId)
        {
            try
            {
                return GameStateMachineRegistry.GetGameStateMachine(gameId);
            }
            catch (GameException)
            {
                // GameException from GetGameStateMachine means the game is not in the registry.
                // This is the only case it throws today (see GameStateMachineRegistry).
                // Load from database below.
            }

            var gameSaveData = await _db.LoadGameAsync(gameId);

            if (gameSaveData == null)
                return null;

            var decompressedJson = JsonHelper.Decompress(gameSaveData.CompressedData);
            var serializableLog = JsonHelper.Deserialize<SerializableLog>(decompressedJson);
            if (serializableLog == null)
                return null;

            var gameLog = Log<string>.FromSerializableLog(serializableLog, _persistenceService, string.Empty);
            var gameStateMachine = CreateGameStateMachineWithServiceDependencies(gameLog);
            GameStateMachineRegistry.AddGameStateMachine(gameId, gameStateMachine);
            return gameStateMachine;
        }

        /// <summary>
        /// Returns the next available auto-name for a copy: "{name} - Copy N"
        /// where N is the lowest positive integer not already used.
        /// </summary>
        private async Task<string> GenerateCopyNameAsync(string originalName)
        {
            var prefix = $"{originalName} - Copy ";
            var allGames = await _db.ListGamesAsync();
            var existingNames = allGames
                .Where(g => g.GameName.StartsWith(prefix))
                .Select(g => g.GameName)
                .ToList();

            var usedNumbers = new HashSet<int>();
            foreach (var name in existingNames)
            {
                var suffix = name.Substring(prefix.Length);
                if (int.TryParse(suffix, out var n))
                    usedNumbers.Add(n);
            }

            var next = 1;
            while (usedNumbers.Contains(next))
                next++;
            return $"{prefix}{next}";
        }

        /// <summary>
        /// Creates a copy of the game reset to the first WaitingForRollForOrder state,
        /// so the same group can play again on the same board without setup.
        /// POST /api/game/{gameId}/replay
        /// </summary>
        [HttpPost("game/{gameId}/replay")]
        public async Task<IActionResult> ReplayGame(string gameId, [FromQuery] string? newName = null)
        {
            _logger.LogEvent("API Request", $"POST /api/game/{gameId}/replay - Creating replay");

            try
            {
                var sourceGameStateMachine = await EnsureGameLoadedAsync(gameId);
                if (sourceGameStateMachine == null)
                    return NotFound(new { success = false, error = $"Game {gameId} not found" });

                var sourceLog = sourceGameStateMachine.GetSerializableLog();
                var originalName = sourceGameStateMachine.GetCurrentState().GameName;

                // Find the first WaitingForRollForOrder state in game history.
                // DoneStack[0] = most recent, DoneStack[Count-1] = oldest.
                // Search from the oldest end to find the first (chronologically) occurrence.
                int replayIndex = -1;
                for (int i = sourceLog.DoneStack.Count - 1; i >= 0; i--)
                {
                    var gm = JsonHelper.Deserialize<GameModel>(sourceLog.DoneStack[i]);
                    if (gm?.GameState == GameState.WaitingForRollForOrder)
                    {
                        replayIndex = i;
                        break;
                    }
                }

                if (replayIndex < 0)
                {
                    return UnprocessableEntity(new
                    {
                        success = false,
                        error = "No WaitingForRollForOrder state found — game has not progressed past board setup"
                    });
                }

                var newGameId = Guid.NewGuid().ToString();
                var gameName = !string.IsNullOrWhiteSpace(newName)
                    ? newName
                    : $"{originalName} (Replay)";

                // Truncate DoneStack to WaitingForRollForOrder (keep oldest states from replayIndex onward)
                // and replace GameId / GameName in every entry.
                var newDoneStack = new List<string>();
                for (int i = replayIndex; i < sourceLog.DoneStack.Count; i++)
                {
                    var updatedJson = sourceLog.DoneStack[i]
                        .Replace($"\"GameId\":\"{gameId}\"", $"\"GameId\":\"{newGameId}\"")
                        .Replace($"\"GameName\":\"{originalName}\"", $"\"GameName\":\"{gameName}\"");
                    newDoneStack.Add(updatedJson);
                }

                var newSerializableLog = new SerializableLog
                {
                    DoneStack = newDoneStack,
                    RedoStack = [],
                    GameType = sourceLog.GameType,
                    DoneCount = newDoneStack.Count,
                    RedoCount = 0
                };

                var newGameLog = Log<string>.FromSerializableLog(newSerializableLog, _persistenceService, string.Empty);
                var newGameStateMachine = CreateGameStateMachineWithServiceDependencies(newGameLog);
                var newGameModel = newGameStateMachine.GetCurrentState();

                GameStateMachineRegistry.AddGameStateMachine(newGameId, newGameStateMachine);

                var json = JsonHelper.Serialize(newSerializableLog);
                var compressed = JsonHelper.Compress(json);

                var metadata = new GameMetadata
                {
                    GameName = gameName,
                    GameState = newGameModel.GameState.ToString(),
                    StartedBy = "Replay",
                    PlayerCount = newGameModel.Players.Count,
                    GameType = newGameModel.Tiles.Count > 19 ? "Expansion" : "Regular",
                    PlayerNames = string.Join(", ", newGameModel.Players.Select(p => p.Name)),
                    TurnCount = newSerializableLog.DoneCount
                };

                await _gamePersistence.SaveAsync(newGameId, compressed, metadata);

                _logger.LogEvent("Game Replayed", $"Replayed {gameId} → {newGameId} at WaitingForRollForOrder - {metadata.PlayerNames}");

                return Ok(new
                {
                    success = true,
                    sourceGameId = gameId,
                    newGameId,
                    gameName = metadata.GameName,
                    playerNames = metadata.PlayerNames,
                    message = "Replay created successfully"
                });
            }
            catch (Exception ex)
            {
                _logger.LogEvent("Replay Game Error", $"Error replaying game {gameId}: {ex.Message}", LogLevel.Error);
                return StatusCode(500, new { success = false, error = $"Error replaying game: {ex.Message}" });
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

                // Load from database
                var gameSaveData = await _db.LoadGameAsync(gameId);

                if (gameSaveData == null)
                {
                    _logger.LogEvent("Game Not Found", $"Game {gameId} not found in database", LogLevel.Warning);
                    return NotFound(new { success = false, error = $"Game {gameId} not found in database" });
                }

                // Decompress and deserialize the log from the saved data
                var decompressedJson = JsonHelper.Decompress(gameSaveData.CompressedData);
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
        /// Removes a game from the in-memory registry without deleting it from the database.
        /// Call this when the player explicitly closes/leaves a game.
        /// POST /api/game/{gameId}/close
        /// </summary>
        [HttpPost("game/{gameId}/close")]
        public IActionResult CloseGame(string gameId)
        {
            _logger.LogEvent("API Request", $"POST /api/game/{gameId}/close - Closing game");
            var removed = GameStateMachineRegistry.DeleteGameStateMachine(gameId);
            _logger.LogEvent("Game Closed", $"Game {gameId} removed from registry: {removed}");
            return Ok(new { success = true, gameId, removed });
        }

        /// <summary>
        /// Gets saved games from database for Load Game page.
        /// Pass playerId="*" to get all games, or a specific playerId to filter.
        /// </summary>
        [HttpGet("games")]
        public async Task<IActionResult> GetSavedGames([FromQuery] string playerId = "*")
        {
            _logger.LogEvent("API Request", $"GET /api/games - Getting saved games (playerId={playerId})");

            try
            {
                // Filter by playerId unless "*" (get all)
                string? startedBy = (playerId != "*" && !string.IsNullOrEmpty(playerId)) ? playerId : null;
                var gameSummaries = await _db.ListGamesAsync(startedBy);

                var games = gameSummaries
                    .OrderByDescending(g => g.SavedAt)
                    .Select(g => new
                    {
                        g.GameId,
                        g.GameName,
                        g.GameState,
                        g.GameType,
                        g.PlayerCount,
                        g.PlayerNames,
                        g.TurnCount,
                        g.Size,
                        g.SavedAt,
                        g.CreatedAt
                    })
                    .ToList();

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
        /// Validates schema versions and returns error if database has outdated schema.
        /// </summary>
        [HttpGet("players")]
        [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
        public async Task<IActionResult> GetPlayers()
        {
            _logger.LogEvent("API Request", $"GET /api/players - Getting available players");

            try
            {
                // Get players from database (no caching - always fresh)
                var allPlayers = await _db.LoadPlayersAsync();
                var players = new List<PlayerProfile>();
                var schemaErrors = new List<string>();

                foreach (var profile in allPlayers)
                {
                    // Validate schema version if player has lifetime stats
                    if (profile.LifetimeStats != null)
                    {
                        if (profile.LifetimeStats.SchemaVersion != LifetimeStats.CurrentSchemaVersion)
                        {
                            schemaErrors.Add($"Player '{profile.Name}' has outdated LifetimeStats schema (v{profile.LifetimeStats.SchemaVersion}, expected v{LifetimeStats.CurrentSchemaVersion})");
                        }
                        if (profile.LifetimeStats.Totals.SchemaVersion != GameStats.CurrentSchemaVersion)
                        {
                            schemaErrors.Add($"Player '{profile.Name}' has outdated GameStats schema (v{profile.LifetimeStats.Totals.SchemaVersion}, expected v{GameStats.CurrentSchemaVersion})");
                        }
                    }

                    players.Add(profile);
                }

                // If schema errors found, return error response
                if (schemaErrors.Count > 0)
                {
                    _logger.LogEvent("Schema Mismatch", $"Database schema mismatch detected: {string.Join("; ", schemaErrors)}", LogLevel.Error);
                    return StatusCode(500, new
                    {
                        success = false,
                        error = "Database schema mismatch detected. Please reset the database with 'pwsh ./catan.ps1 database install'",
                        schemaErrors = schemaErrors,
                        timestamp = DateTime.UtcNow.ToString("O")
                    });
                }

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
            var sanitizedPlayerName = (player.Name ?? string.Empty).Replace("\r", string.Empty).Replace("\n", string.Empty);
            var sanitizedPlayerId = (player.Id ?? string.Empty).Replace("\r", string.Empty).Replace("\n", string.Empty);
            _logger.LogEvent("API Request", $"POST /api/players - Creating player: {sanitizedPlayerName}");

            try
            {
                // Check if player ID already exists
                var existing = await _db.LoadPlayerAsync(sanitizedPlayerId);
                if (existing != null)
                {
                    return BadRequest(new { success = false, error = $"Player with ID '{player.Id}' already exists" });
                }

                // Save player profile directly
                await _db.SavePlayerAsync(player);

                _logger.LogEvent("Player Created", $"Created player: {sanitizedPlayerId} ({sanitizedPlayerName})");

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
            var sanitizedId = id.Replace("\r", string.Empty).Replace("\n", string.Empty);
            var sanitizedPlayerName = (player.Name ?? string.Empty).Replace("\r", string.Empty).Replace("\n", string.Empty);
            _logger.LogEvent("API Request", $"PUT /api/players/{sanitizedId} - Updating player");

            try
            {
                var existing = await _db.LoadPlayerAsync(id);
                if (existing == null)
                {
                    return NotFound(new { success = false, error = $"Player '{id}' not found" });
                }

                // Save the updated player profile
                await _db.SavePlayerAsync(player);

                _logger.LogEvent("Player Updated", $"Updated player: {sanitizedId} ({sanitizedPlayerName})");

                // Notify all games containing this player
                await NotifyPlayersUpdated(new[] { player });

                return Ok(new { success = true, player = player });
            }
            catch (Exception ex)
            {
                _logger.LogEvent("Update Player Error", $"Error updating player {sanitizedId}: {ex.Message}", LogLevel.Error);
                return StatusCode(500, new { success = false, error = $"Error updating player: {ex.Message}" });
            }
        }

        /// <summary>
        /// Deletes a player.
        /// </summary>
        [HttpDelete("players/{id}")]
        public async Task<IActionResult> DeletePlayer(string id)
        {
            var sanitizedId = id.Replace("\r", string.Empty).Replace("\n", string.Empty);
            _logger.LogEvent("API Request", $"DELETE /api/players/{sanitizedId} - Deleting player");

            try
            {
                var existing = await _db.LoadPlayerAsync(id);
                if (existing == null)
                {
                    return NotFound(new { success = false, error = $"Player '{id}' not found" });
                }

                // DeletePlayerAsync handles image deletion internally
                await _db.DeletePlayerAsync(id);

                _logger.LogEvent("Player Deleted", $"Deleted player: {sanitizedId}");

                return Ok(new { success = true, message = $"Player '{id}' deleted" });
            }
            catch (Exception ex)
            {
                _logger.LogEvent("Delete Player Error", $"Error deleting player {sanitizedId}: {ex.Message}", LogLevel.Error);
                return StatusCode(500, new { success = false, error = $"Error deleting player: {ex.Message}" });
            }
        }

        /// <summary>
        /// Uploads an image for a player.
        /// </summary>
        [HttpPost("players/{id}/image")]
        public async Task<IActionResult> UploadPlayerImage(string id, IFormFile file)
        {
            var sanitizedId = id.Replace("\r", string.Empty).Replace("\n", string.Empty);
            _logger.LogEvent("API Request", $"POST /api/players/{sanitizedId}/image - Uploading image");

            try
            {
                // Verify player exists
                var playerProfile = await _db.LoadPlayerAsync(id);
                if (playerProfile == null)
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

                // Save image via ICatanDb
                await _db.SaveImageAsync(id, imageData, file.ContentType);

                // Update player's ImageUri
                var updatedProfile = new PlayerProfile(
                    playerProfile.Id,
                    playerProfile.Name,
                    playerProfile.Colors,
                    $"/api/images/{id}",
                    playerProfile.LifetimeStats
                );
                await _db.SavePlayerAsync(updatedProfile);

                _logger.LogEvent("Image Uploaded", $"Uploaded image for player: {sanitizedId} ({imageData.Length} bytes)");

                // Notify games with this player about the image change
                await NotifyPlayersUpdated(new[] { updatedProfile });

                return Ok(new { success = true, imageUri = $"/api/images/{id}" });
            }
            catch (Exception ex)
            {
                _logger.LogEvent("Upload Image Error", $"Error uploading image for {sanitizedId}: {ex.Message}", LogLevel.Error);
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
            var sanitizedId = id.Replace("\r", string.Empty).Replace("\n", string.Empty);
            _logger.LogEvent("API Request", $"GET /api/images/{sanitizedId} - Getting image");

            try
            {
                var imageResult = await _db.LoadImageAsync(id);
                if (imageResult == null)
                {
                    _logger.LogEvent("Image Not Found", $"Image not found: {sanitizedId}", LogLevel.Warning);
                    return NotFound($"Image {id} not found");
                }

                return File(imageResult.Value.Data, imageResult.Value.ContentType);
            }
            catch (Exception ex)
            {
                _logger.LogEvent("Get Image Error", $"Error getting image {sanitizedId}: {ex.Message}", LogLevel.Error);
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
                var playerCount = (await _db.LoadPlayersAsync()).Count;
                var gameCount = await _db.CountGamesAsync();

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
        /// Troubleshoots Azure SQL connectivity issues and attempts to fix them.
        /// Can fix: Public Network Access disabled, missing AllowAzureServices firewall rule.
        /// Cannot fix: App Service Plan SKU (requires terminal), database paused (auto-resumes).
        /// </summary>
        [HttpPost("troubleshoot")]
        public async Task<IActionResult> Troubleshoot()
        {
            _logger.LogEvent("API Request", "POST /api/troubleshoot - CosmosDB health check");

            try
            {
                var players = await _db.LoadPlayersAsync();
                var gameCount = await _db.CountGamesAsync();
                return Ok(new
                {
                    timestamp = DateTime.UtcNow,
                    connectionSuccessful = true,
                    message = "CosmosDB connected",
                    playerCount = players.Count,
                    gameCount,
                });
            }
            catch (Exception ex)
            {
                return Ok(new
                {
                    timestamp = DateTime.UtcNow,
                    connectionSuccessful = false,
                    message = $"CosmosDB error: {ex.Message}",
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
                    var gameSaveData = await _db.LoadGameAsync(gameId);

                    if (gameSaveData == null)
                    {
                        return NotFound(new { success = false, error = $"Game {gameId} not found" });
                    }

                    var oldName = gameSaveData.GameName;
                    if (oldName == newName)
                    {
                        return Ok(new { success = true, gameId, gameName = newName, message = "Name unchanged" });
                    }

                    // Update the game name in the compressed data
                    var decompressedJson = JsonHelper.Decompress(gameSaveData.CompressedData);
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

                        // Save updated game via ICatanDb
                        var updatedGameSave = new GameSaveData
                        {
                            GameId = gameSaveData.GameId,
                            GameName = newName,
                            GameState = gameSaveData.GameState,
                            GameType = gameSaveData.GameType,
                            StartedBy = gameSaveData.StartedBy,
                            PlayerCount = gameSaveData.PlayerCount,
                            PlayerNames = gameSaveData.PlayerNames,
                            TurnCount = gameSaveData.TurnCount,
                            SavedAt = DateTime.UtcNow,
                            CreatedAt = gameSaveData.CreatedAt,
                            CompressedData = compressed,
                            Size = compressed.Length
                        };

                        await _db.SaveGameAsync(updatedGameSave);
                    }
                    else
                    {
                        // Just update metadata if we can't parse the log
                        var updatedGameSave = new GameSaveData
                        {
                            GameId = gameSaveData.GameId,
                            GameName = newName,
                            GameState = gameSaveData.GameState,
                            GameType = gameSaveData.GameType,
                            StartedBy = gameSaveData.StartedBy,
                            PlayerCount = gameSaveData.PlayerCount,
                            PlayerNames = gameSaveData.PlayerNames,
                            TurnCount = gameSaveData.TurnCount,
                            SavedAt = DateTime.UtcNow,
                            CreatedAt = gameSaveData.CreatedAt,
                            CompressedData = gameSaveData.CompressedData,
                            Size = gameSaveData.Size
                        };

                        await _db.SaveGameAsync(updatedGameSave);
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
        /// Deletes a game from both memory and database.
        /// </summary>
        [HttpDelete("game/{gameId}")]
        public async Task<IActionResult> DeleteGame(string gameId)
        {
            _logger.LogEvent("API Request", $"DELETE /api/game/{gameId} - Deleting game");

            try
            {
                // Remove from in-memory registry if loaded
                try
                {
                    GameStateMachineRegistry.DeleteGameStateMachine(gameId);
                    _logger.LogEvent("Delete Game", $"Removed game {gameId} from memory");
                }
                catch (GameException)
                {
                    // Game not in registry - that's OK
                    _logger.LogEvent("Delete Game", $"Game {gameId} not in memory");
                }

                // Delete from database
                var gameSaveData = await _db.LoadGameAsync(gameId);

                if (gameSaveData == null)
                {
                    return NotFound(new { success = false, error = $"Game {gameId} not found in database" });
                }

                var gameName = gameSaveData.GameName;

                await _db.DeleteGameAsync(gameId);

                _logger.LogEvent("Game Deleted", $"Deleted game {gameId} ({gameName})");

                return Ok(new { success = true, gameId, gameName, message = "Game deleted successfully" });
            }
            catch (Exception ex)
            {
                _logger.LogEvent("Delete Game Error", $"Error deleting game {gameId}: {ex.Message}", LogLevel.Error);
                return StatusCode(500, new { success = false, error = $"Error deleting game: {ex.Message}" });
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
                // Get the game state machine — load from database if not already in memory
                var sourceGameStateMachine = await EnsureGameLoadedAsync(gameId);
                if (sourceGameStateMachine == null)
                    return NotFound(new { success = false, error = $"Game {gameId} not found" });

                // Get the serializable log (contains DoneStack and RedoStack of serialized GameModels)
                var sourceLog = sourceGameStateMachine.GetSerializableLog();

                // Get original game info for name replacement
                var originalGameModel = sourceGameStateMachine.GetCurrentState();
                var originalName = originalGameModel.GameName;

                // Generate a new GameId and determine new name
                var newGameId = Guid.NewGuid().ToString();
                var gameName = !string.IsNullOrWhiteSpace(newName)
                    ? newName
                    : await GenerateCopyNameAsync(originalName);

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
            var sanitizedFileName = (file?.FileName ?? string.Empty).Replace("\r", string.Empty).Replace("\n", string.Empty);
            _logger.LogEvent("API Request", $"POST /api/game/import - Importing game file: {sanitizedFileName}");

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
                var existingGame = await _db.LoadGameAsync(gameId);
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

        /// <summary>
        /// Database schema initialization. With CosmosDB, containers are created via ICatanDb.InitializeAsync().
        /// This endpoint delegates to ICatanDb and seeds system templates.
        /// </summary>
        [HttpPost("database/migrate")]
        public async Task<IActionResult> MigrateDatabase()
        {
            _logger.LogEvent("API Request", "POST /api/database/migrate - Initializing database");

            try
            {
                await _db.InitializeAsync();

                _logger.LogEvent("Database Migrate Complete", "Database initialized");

                return Ok(new
                {
                    timestamp = DateTime.UtcNow,
                    success = true,
                    message = "Database initialized via ICatanDb."
                });
            }
            catch (Exception ex)
            {
                _logger.LogEvent("Database Migrate Error", $"Error during initialization: {ex.Message}", LogLevel.Error);
                return StatusCode(500, new
                {
                    timestamp = DateTime.UtcNow,
                    success = false,
                    message = $"Initialization failed: {ex.Message}"
                });
            }
        }
    }
}