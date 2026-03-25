using Catan3.GameService.Abstractions;
using Catan3.GameService.Utility;
using Catan3.Shared.Interfaces;

namespace Catan3.GameService.Services;

/// <summary>
/// Database persistence implementation for game state.
/// Uses ICatanDb for CosmosDB storage.
/// </summary>
public class GamePersistenceService : IGamePersistence
{
    private readonly ILogger<GamePersistenceService> _logger;
    private readonly IServiceProvider _serviceProvider;

    public GamePersistenceService(ILogger<GamePersistenceService> logger, IServiceProvider serviceProvider)
    {
        _logger = logger;
        _serviceProvider = serviceProvider;
    }

    public async Task<bool> SaveAsync(string gameId, byte[] data, GameMetadata metadata)
    {
        try
        {
            using var scope = _serviceProvider.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<ICatanDb>();

            var gameData = new GameSaveData
            {
                GameId = gameId,
                GameName = metadata.GameName,
                GameState = metadata.GameState,
                GameType = metadata.GameType,
                StartedBy = metadata.StartedBy,
                PlayerCount = metadata.PlayerCount,
                PlayerNames = metadata.PlayerNames,
                TurnCount = metadata.TurnCount,
                SavedAt = DateTime.UtcNow,
                CompressedData = data,
                Size = data.Length,
            };

            await db.SaveGameAsync(gameData);
            _logger.LogEvent("DatabaseOperation", $"Saved game: {gameId}");
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogEvent("DatabaseOperation", $"Error saving game: {ex.Message}", LogLevel.Error);
            return false;
        }
    }

    public async Task<byte[]?> LoadAsync(string gameId)
    {
        try
        {
            using var scope = _serviceProvider.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<ICatanDb>();

            var game = await db.LoadGameAsync(gameId);
            if (game == null)
            {
                _logger.LogEvent("DatabaseOperation", $"Game not found: {gameId}", LogLevel.Warning);
                return null;
            }

            _logger.LogEvent("DatabaseOperation", $"Loaded game: {gameId}");
            return game.CompressedData;
        }
        catch (Exception ex)
        {
            _logger.LogEvent("DatabaseOperation", $"Error loading game: {ex.Message}", LogLevel.Error);
            return null;
        }
    }

    public async Task<List<GameSummary>> GetGameSummariesAsync(string? startedBy = null)
    {
        using var scope = _serviceProvider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ICatanDb>();
        var summaries = await db.ListGamesAsync(startedBy);
        return summaries.ToList();
    }

    public async Task<bool> DeleteAsync(string gameId)
    {
        try
        {
            using var scope = _serviceProvider.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<ICatanDb>();

            await db.DeleteGameAsync(gameId);
            _logger.LogEvent("DatabaseOperation", $"Deleted game: {gameId}");
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogEvent("DatabaseOperation", $"Error deleting game: {ex.Message}", LogLevel.Error);
            return false;
        }
    }
}

/// <summary>
/// IPersistenceService implementation that delegates to IGamePersistence for database saves.
/// The 'location' parameter is the gameId (passed from Log.SaveAsync).
/// </summary>
public class DatabaseBackedPersistenceService : IPersistenceService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<DatabaseBackedPersistenceService> _logger;

    public DatabaseBackedPersistenceService(IServiceScopeFactory scopeFactory, ILogger<DatabaseBackedPersistenceService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    public string? Location => null;
    public string SaveDirectory { get; set; } = string.Empty;

    public Task<byte[]?> OpenAsync(string location) => Task.FromResult<byte[]?>(null);

    public async Task<bool> SaveAsync(string gameId, byte[] data)
    {
        if (string.IsNullOrEmpty(gameId))
        {
            _logger.LogWarning("SaveAsync called with empty gameId, skipping");
            return false;
        }

        _logger.LogDebug("SaveAsync for game {GameId}, data size: {DataSize}", gameId, data.Length);

        try
        {
            using var scope = _scopeFactory.CreateScope();
            var gamePersistence = scope.ServiceProvider.GetRequiredService<IGamePersistence>();

            // Get metadata from the in-memory GameStateMachine
            var gameStateMachine = GameStateMachineRegistry.GetGameStateMachine(gameId);
            var gameModel = gameStateMachine.GetCurrentState();

            var metadata = new GameMetadata
            {
                GameName = gameModel.GameName,
                GameState = gameModel.GameState.ToString(),
                StartedBy = "WebUI",
                PlayerCount = gameModel.Players.Count,
                GameType = gameModel.Tiles.Count > 19 ? "Expansion" : "Regular",
                PlayerNames = string.Join(", ", gameModel.Players.Select(p => p.Name)),
                TurnCount = gameStateMachine.GetSerializableLog().DoneCount
            };

            var result = await gamePersistence.SaveAsync(gameId, data, metadata);
            _logger.LogDebug("SaveAsync result for {GameId}: {Result}", gameId, result);
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "SaveAsync failed for game {GameId}", gameId);
            return false;
        }
    }

    public Task<bool> WriteTextAsync(string location, string content) => Task.FromResult(true);
}
