using System.Collections.Concurrent;
using Catan3.Shared.Models;
using Catan3.Shared.Utility;
using Catan3.GameService.Controllers;
using Catan3.GameService.Services;

namespace Catan3.GameService.Services
{
    /// <summary>
    /// Service for managing individual GameStateMachine instances per gameId
    /// Each game gets its own GameStateMachine with its own log state
    /// Thread-safe implementation for concurrent access
    /// Updated to use proper separation of concerns for client notifications
    /// </summary>
    public class GameStateMachineService
    {
        private readonly ConcurrentDictionary<string, GameStateMachine> _gameStateMachines = new();
        private readonly IPersistanceService _persistanceService;
        private readonly IClientNotification _clientNotification;
        private readonly ILogger<GameStateMachineService> _logger;

        public GameStateMachineService(IPersistanceService persistanceService, IClientNotification clientNotification, ILogger<GameStateMachineService> logger)
        {
            _persistanceService = persistanceService;
            _clientNotification = clientNotification;
            _logger = logger;
        }

        /// <summary>
        /// Gets an existing GameStateMachine for the specified gameId
        /// Throws GameException if the game doesn't exist
        /// </summary>
        public GameStateMachine GetGameStateMachine(string gameId)
        {
            _logger.LogDebug("Getting GameStateMachine for gameId: {GameId}", gameId);
            
            if (_gameStateMachines.TryGetValue(gameId, out var gameStateMachine))
            {
                _logger.LogDebug("Found existing GameStateMachine for gameId: {GameId}", gameId);
                return gameStateMachine;
            }
            
            _logger.LogWarning("GameStateMachine not found for gameId: {GameId}", gameId);
            throw new Catan3.Shared.Utility.GameException($"Game {gameId} not found");
        }

        /// <summary>
        /// Creates a new GameStateMachine for the specified gameId
        /// Only called when creating a new game, not for retrieving existing games
        /// </summary>
        private GameStateMachine CreateGameStateMachine()
        {
            _logger.LogInformation("Creating new GameStateMachine with auto-generated GameId");
            
            var gameStateMachine = new GameStateMachine(_persistanceService, _clientNotification, "");  // Empty saveFile for now
            var gameId = gameStateMachine.GameId;
            
            // Update the save file path with the actual GameId
            var saveFile = Path.Combine(Path.GetTempPath(), "Catan3Games", $"game_{gameId}.catan");
            // TODO: We may need to update the Log's save file path if required
            
            _gameStateMachines[gameId] = gameStateMachine;
            
            _logger.LogInformation("Successfully created GameStateMachine with GameId: {GameId}, saveFile: {SaveFile}", gameId, saveFile);
            return gameStateMachine;
        }

        /// <summary>
        /// Executes an action on the GameStateMachine for the specified gameId
        /// Client notifications are handled automatically by the GameStateMachine
        /// </summary>
        public GameModel ExecuteAction(string gameId, Func<GameStateMachine, GameModel> action)
        {
            _logger.LogDebug("Executing action on GameStateMachine for gameId: {GameId}", gameId);
            
            var gameStateMachine = GetGameStateMachine(gameId);
            var result = action(gameStateMachine);
            
            // Rule 7 Compliance: GameModel contains all truth including version
            // Client notification is handled automatically by GameStateMachine.LogGameModel()
            
            _logger.LogDebug("Action executed, version: {Version}, gameId: {GameId}", result.GameStateMachineVersion, gameId);
            
            return result;
        }

        /// <summary>
        /// Executes an async action on the GameStateMachine for the specified gameId
        /// Client notifications are handled automatically by the GameStateMachine
        /// </summary>
        public async Task<GameModel> ExecuteActionAsync(string gameId, Func<GameStateMachine, Task<GameModel>> action)
        {
            _logger.LogDebug("Executing async action on GameStateMachine for gameId: {GameId}", gameId);
            
            var gameStateMachine = GetGameStateMachine(gameId);
            var result = await action(gameStateMachine);
            
            // Rule 7 Compliance: GameModel contains all truth including version
            // Client notification is handled automatically by GameStateMachine.LogGameModel()
            
            _logger.LogDebug("Async action executed, version: {Version}, gameId: {GameId}", result.GameStateMachineVersion, gameId);
            
            return result;
        }

        /// <summary>
        /// Creates a new game and returns the initial game state
        /// GameStateMachine generates its own GameId automatically
        /// Client notifications are handled automatically by the GameStateMachine
        /// </summary>
        public (string gameId, GameModel gameModel) CreateNewGame(Func<GameStateMachine, GameModel> createGameAction)
        {
            _logger.LogInformation("Creating new game with auto-generated GameId");
            
            // Create the GameStateMachine first - it will generate its own GameId
            var gameStateMachine = CreateGameStateMachine();
            var gameId = gameStateMachine.GameId;
            
            _logger.LogInformation("Created GameStateMachine with GameId: {GameId}", gameId);
            
            // Execute the new game action to initialize the game state
            var result = createGameAction(gameStateMachine);
            
            // Rule 7 Compliance: GameModel should already have GameId and CreatedTime from LogGameModel
            // PlayerModel.Name property handles name extraction automatically
            result.GameId = gameId;
            result.CreatedTime = result.CreatedTime == default ? DateTime.UtcNow : result.CreatedTime;
            
            _logger.LogInformation("New game created successfully, gameId: {GameId}, version: {Version}", gameId, result.GameStateMachineVersion);
            
            // Client notification is handled automatically by GameStateMachine.LogGameModel()
            
            return (gameId, result);
        }

        /// <summary>
        /// Creates a new game and returns the initial game state (async version)
        /// GameStateMachine generates its own GameId automatically
        /// Client notifications are handled automatically by the GameStateMachine
        /// </summary>
        public async Task<(string gameId, GameModel gameModel)> CreateNewGameAsync(Func<GameStateMachine, Task<GameModel>> createGameAction)
        {
            _logger.LogInformation("Creating new game async with auto-generated GameId");
            
            // Create the GameStateMachine first - it will generate its own GameId
            var gameStateMachine = CreateGameStateMachine();
            var gameId = gameStateMachine.GameId;
            
            _logger.LogInformation("Created GameStateMachine with GameId: {GameId}", gameId);
            
            // Execute the new game action to initialize the game state
            var result = await createGameAction(gameStateMachine);
            
            // Rule 7 Compliance: GameModel should already have GameId and CreatedTime from LogGameModel
            // PlayerModel.Name property handles name extraction automatically
            result.GameId = gameId;
            result.CreatedTime = result.CreatedTime == default ? DateTime.UtcNow : result.CreatedTime;
            
            _logger.LogInformation("New game created successfully async, gameId: {GameId}, version: {Version}", gameId, result.GameStateMachineVersion);
            
            // Client notification is handled automatically by GameStateMachine.LogGameModel()
            
            return (gameId, result);
        }

        /// <summary>
        /// Gets the current game state for the specified gameId
        /// Returns null if the game doesn't exist
        /// </summary>
        public GameModel? GetCurrentGameState(string gameId)
        {
            _logger.LogDebug("Getting current game state for gameId: {GameId}", gameId);
            
            if (_gameStateMachines.TryGetValue(gameId, out var gameStateMachine))
            {
                try
                {
                    var gameModel = gameStateMachine.GetCurrentState();
                    _logger.LogDebug("Retrieved game state for gameId: {GameId}, state: {GameState}", gameId, gameModel?.GameState);
                    return gameModel;
                }
                catch (InvalidOperationException ex)
                {
                    _logger.LogWarning("Game exists but has no state yet for gameId: {GameId}, error: {Error}", gameId, ex.Message);
                    return null;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error getting game state for gameId: {GameId}", gameId);
                    return null;
                }
            }
            
            _logger.LogDebug("No game found for gameId: {GameId}", gameId);
            return null;
        }

        /// <summary>
        /// Gets a list of available games with user-friendly information
        /// Returns game metadata without exposing raw technical gameIds
        /// Rule 7 Compliance: Copies data from GameModel (single source of truth) to GameInfo (summary object)
        /// </summary>
        public List<GameInfo> GetAvailableGames()
        {
            _logger.LogDebug("Getting available games, total games: {Count}", _gameStateMachines.Count);
            
            var availableGames = new List<GameInfo>();
            
            foreach (var kvp in _gameStateMachines)
            {
                try
                {
                    var gameId = kvp.Key;
                    var gameStateMachine = kvp.Value;
                    var gameModel = gameStateMachine.GetCurrentState();
                    
                    if (gameModel != null)
                    {
                        // Rule 7 Compliance: Copy ALL data from GameModel (truth) to GameInfo (summary)
                        // No computed fields in GameInfo - all computation done by GameModel
                        var gameInfo = new GameInfo
                        {
                            // Direct copy from GameModel fields
                            GameId = gameModel.GameId,
                            PlayerCount = gameModel.Players.Count,
                            PlayerNames = gameModel.GetPlayerNames(), // Use helper method that gets names from PlayerModel.Name
                            PlayerIds = gameModel.Players.Select(p => p.Id).ToList(),
                            CreatedTime = gameModel.CreatedTime,
                            GameStateMachineVersion = gameModel.GameStateMachineVersion, // Get version from GameModel, not service
                            
                            // Computed fields using GameModel helper methods (truth computed in GameModel)
                            DisplayName = gameModel.GetDisplayName(),
                            GameType = gameModel.GameType.ToString(),
                            GameState = gameModel.GetFormattedGameState(),
                            CurrentPlayer = gameModel.GetCurrentPlayerName(),
                            CreatedTimeDisplay = gameModel.GetCreatedTimeDisplay(),
                            IsActive = gameModel.GetIsActive(),
                            Summary = gameModel.GetSummary()
                        };
                        
                        availableGames.Add(gameInfo);
                        _logger.LogDebug("Added game to available list: {GameId}, Type: {GameType}, Players: {PlayerCount}, State: {GameState}", 
                            gameId, gameInfo.GameType, gameInfo.PlayerCount, gameInfo.GameState);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Error getting game info for gameId: {GameId}", kvp.Key);
                    // Continue processing other games
                }
            }
            
            // Sort by creation time (newest first) - using GameModel truth, not computed time
            availableGames = availableGames.OrderByDescending(g => g.CreatedTime).ToList();
            
            _logger.LogDebug("Returning {Count} available games", availableGames.Count);
            return availableGames;
        }

        /// <summary>
        /// Gets the current version for the specified gameId from the GameModel
        /// Rule 7 Compliance: Version comes from GameModel, not service-level counter
        /// Version is always 1 (constant software version)
        /// </summary>
        public int GetCurrentVersion(string? gameId = null)
        {
            // Version is always 1 for all games - it represents GameStateMachine software compatibility
            return 1;
        }
    }
}