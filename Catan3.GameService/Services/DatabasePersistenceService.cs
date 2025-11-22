using Catan3.GameService.Utility;
using Catan3.GameService.Data;
using Catan3.Shared.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace Catan3.GameService.Services;

/// <summary>
/// Database persistence implementation for game state.
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

            var existingSave = await dbContext.GameSaves.FirstOrDefaultAsync(g => g.GameId == gameId);

            if (existingSave != null)
            {
                existingSave.CompressedData = data;
                existingSave.SavedAt = DateTime.UtcNow;
                existingSave.GameState = metadata.GameState;
                existingSave.PlayerCount = metadata.PlayerCount;
            }
            else
            {
                var newSave = new GameSaveEntity
                {
                    GameId = gameId,
                    CompressedData = data,
                    SavedAt = DateTime.UtcNow,
                    CreatedAt = DateTime.UtcNow,
                    GameName = metadata.GameName,
                    GameState = metadata.GameState,
                    StartedBy = metadata.StartedBy,
                    PlayerCount = metadata.PlayerCount,
                    GameType = metadata.GameType
                };
                dbContext.GameSaves.Add(newSave);
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

            var save = await dbContext.GameSaves.FirstOrDefaultAsync(g => g.GameId == gameId);
            if (save == null)
            {
                _logger.LogEvent("DatabaseOperation", $"Game not found: {gameId}", LogLevel.Warning);
                return null;
            }

            _logger.LogEvent("DatabaseOperation", $"Loaded game: {gameId}");
            return save.CompressedData;
        }
        catch (Exception ex)
        {
            _logger.LogEvent("DatabaseOperation", $"Error loading game: {ex.Message}", LogLevel.Error);
            return null;
        }
    }

    public async Task<List<GameSaveEntity>> GetGamesAsync(string? startedBy = null, string? gameState = null)
    {
        using var scope = _serviceProvider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<CatanDbContext>();

        var query = dbContext.GameSaves.AsQueryable();

        if (!string.IsNullOrEmpty(startedBy))
            query = query.Where(g => g.StartedBy == startedBy);

        if (!string.IsNullOrEmpty(gameState))
            query = query.Where(g => g.GameState == gameState);

        return await query.OrderByDescending(g => g.SavedAt).ToListAsync();
    }

    public async Task<bool> DeleteAsync(string gameId)
    {
        try
        {
            using var scope = _serviceProvider.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<CatanDbContext>();

            var save = await dbContext.GameSaves.FirstOrDefaultAsync(g => g.GameId == gameId);
            if (save == null)
                return false;

            dbContext.GameSaves.Remove(save);
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
