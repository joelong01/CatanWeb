using Catan3.GameService.Utility;
using Catan3.GameService.Data;
using Catan3.Shared.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace Catan3.GameService.Services;

/// <summary>
/// Database persistence implementation for game state.
/// Uses two-table design: GameSaveMetadata (queryable) + GameSaveData (blob storage).
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
            var dbContext = scope.ServiceProvider.GetRequiredService<CatanDbContext>();

            // Check if metadata already exists for this game
            var existingMetadata = await dbContext.GameSaveMetadata
                .Include(m => m.GameData)
                .FirstOrDefaultAsync(m => m.GameId == gameId);

            if (existingMetadata != null)
            {
                // Update existing records
                existingMetadata.GameData.CompressedData = data;
                existingMetadata.GameData.Size = data.Length;

                existingMetadata.SavedAt = DateTime.UtcNow;
                existingMetadata.GameName = metadata.GameName;
                existingMetadata.GameState = metadata.GameState;
                existingMetadata.PlayerCount = metadata.PlayerCount;
                existingMetadata.PlayerNames = metadata.PlayerNames;
                existingMetadata.TurnCount = metadata.TurnCount;
            }
            else
            {
                // Create new data record
                var gameData = new GameSaveDataEntity
                {
                    CompressedData = data,
                    Size = data.Length
                };
                dbContext.GameSaveData.Add(gameData);
                await dbContext.SaveChangesAsync(); // Save to get the ID

                // Create new metadata record with FK
                var gameMetadata = new GameSaveMetadataEntity
                {
                    GameId = gameId,
                    StartedBy = metadata.StartedBy,
                    SavedAt = DateTime.UtcNow,
                    CreatedAt = DateTime.UtcNow,
                    GameState = metadata.GameState,
                    GameType = metadata.GameType,
                    PlayerCount = metadata.PlayerCount,
                    PlayerNames = metadata.PlayerNames,
                    TurnCount = metadata.TurnCount,
                    GameName = metadata.GameName,
                    GameDataId = gameData.Id
                };
                dbContext.GameSaveMetadata.Add(gameMetadata);
            }

            await dbContext.SaveChangesAsync();
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
            var dbContext = scope.ServiceProvider.GetRequiredService<CatanDbContext>();

            var metadata = await dbContext.GameSaveMetadata
                .Include(m => m.GameData)
                .FirstOrDefaultAsync(m => m.GameId == gameId);

            if (metadata == null)
            {
                _logger.LogEvent("DatabaseOperation", $"Game not found: {gameId}", LogLevel.Warning);
                return null;
            }

            _logger.LogEvent("DatabaseOperation", $"Loaded game: {gameId}");
            return metadata.GameData.CompressedData;
        }
        catch (Exception ex)
        {
            _logger.LogEvent("DatabaseOperation", $"Error loading game: {ex.Message}", LogLevel.Error);
            return null;
        }
    }

    public async Task<List<GameSaveMetadataEntity>> GetGamesAsync(string? startedBy = null)
    {
        using var scope = _serviceProvider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<CatanDbContext>();

        var query = dbContext.GameSaveMetadata
            .Include(m => m.GameData)
            .Where(m => m.GameState != "GameOver"); // Exclude completed games

        if (!string.IsNullOrEmpty(startedBy) && startedBy != "*")
            query = query.Where(m => m.StartedBy == startedBy);

        return await query.OrderByDescending(m => m.SavedAt).ToListAsync();
    }

    public async Task<bool> DeleteAsync(string gameId)
    {
        try
        {
            using var scope = _serviceProvider.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<CatanDbContext>();

            var metadata = await dbContext.GameSaveMetadata
                .Include(m => m.GameData)
                .FirstOrDefaultAsync(m => m.GameId == gameId);

            if (metadata == null)
                return false;

            // Remove metadata (cascade delete will remove data)
            dbContext.GameSaveMetadata.Remove(metadata);
            await dbContext.SaveChangesAsync();
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
/// Stub IPersistenceService for Log compatibility. Does nothing - actual persistence via IGamePersistence.
/// </summary>
public class NullPersistenceService : IPersistenceService
{
    public string? Location => null;
    public string SaveDirectory { get; set; } = string.Empty;

    public Task<byte[]?> OpenAsync(string location) => Task.FromResult<byte[]?>(null);
    public Task<bool> SaveAsync(string location, byte[] data) => Task.FromResult(true);
    public Task<bool> WriteTextAsync(string location, string content) => Task.FromResult(true);
}
