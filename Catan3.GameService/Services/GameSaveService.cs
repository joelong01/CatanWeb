using Catan3.GameService.Data;
using Catan3.GameService.Utility;
using Catan3.Shared.Models;
using Microsoft.EntityFrameworkCore;

namespace Catan3.GameService.Services;

/// <summary>
/// Service for saving and loading games from database
/// </summary>
public class GameSaveService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<GameSaveService> _logger;

    public GameSaveService(IServiceProvider serviceProvider, ILogger<GameSaveService> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    /// <summary>
    /// Save a game to the database
    /// </summary>
    public async Task<int> SaveGameAsync(GameModel gameModel, byte[] compressedData)
    {
        using var scope = _serviceProvider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<CatanDbContext>();

        var existingSave = await dbContext.GameSaves
            .FirstOrDefaultAsync(g => g.GameId == gameModel.GameId);

        if (existingSave != null)
        {
            // Update existing save
            existingSave.CompressedData = compressedData;
            existingSave.SavedAt = DateTime.UtcNow;
            existingSave.GameState = gameModel.GameState.ToString();
            existingSave.PlayerCount = gameModel.Players.Count;

            await dbContext.SaveChangesAsync();
            _logger.LogEvent("GameSave", $"Updated game save: {gameModel.GameId} (Id: {existingSave.Id})");
            return existingSave.Id;
        }
        else
        {
            // Create new save
            var newSave = new GameSaveEntity
            {
                GameId = gameModel.GameId,
                CompressedData = compressedData,
                SavedAt = DateTime.UtcNow,
                CreatedAt = DateTime.UtcNow,
                GameName = gameModel.GameName,
                GameState = gameModel.GameState.ToString(),
                StartedBy = gameModel.Players.FirstOrDefault()?.Id ?? "",
                PlayerCount = gameModel.Players.Count,
                GameType = gameModel.Tiles.Count > 19 ? "Expansion" : "Regular"
            };

            dbContext.GameSaves.Add(newSave);
            await dbContext.SaveChangesAsync();

            _logger.LogEvent("GameSave", $"Created game save: {gameModel.GameId} (Id: {newSave.Id})");
            return newSave.Id;
        }
    }

    /// <summary>
    /// Load a game from database by persistence ID
    /// </summary>
    public async Task<(GameSaveEntity? save, byte[]? data)> LoadGameAsync(int id)
    {
        using var scope = _serviceProvider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<CatanDbContext>();

        var save = await dbContext.GameSaves.FindAsync(id);
        if (save == null)
        {
            _logger.LogEvent("GameSave", $"Game save not found: {id}", LogLevel.Warning);
            return (null, null);
        }

        return (save, save.CompressedData);
    }

    /// <summary>
    /// Load a game from database by game ID
    /// </summary>
    public async Task<(GameSaveEntity? save, byte[]? data)> LoadGameByGameIdAsync(string gameId)
    {
        using var scope = _serviceProvider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<CatanDbContext>();

        var save = await dbContext.GameSaves
            .FirstOrDefaultAsync(g => g.GameId == gameId);

        if (save == null)
        {
            _logger.LogEvent("GameSave", $"Game save not found for gameId: {gameId}", LogLevel.Warning);
            return (null, null);
        }

        return (save, save.CompressedData);
    }

    /// <summary>
    /// Get list of saved games for "Join Game" page
    /// </summary>
    public async Task<List<GameSaveEntity>> GetSavedGamesAsync(string? startedBy = null, string? gameState = null)
    {
        using var scope = _serviceProvider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<CatanDbContext>();

        var query = dbContext.GameSaves.AsQueryable();

        if (!string.IsNullOrEmpty(startedBy))
        {
            query = query.Where(g => g.StartedBy == startedBy);
        }

        if (!string.IsNullOrEmpty(gameState))
        {
            query = query.Where(g => g.GameState == gameState);
        }

        return await query
            .OrderByDescending(g => g.SavedAt)
            .ToListAsync();
    }

    /// <summary>
    /// Delete a game save
    /// </summary>
    public async Task<bool> DeleteGameAsync(int id)
    {
        using var scope = _serviceProvider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<CatanDbContext>();

        var save = await dbContext.GameSaves.FindAsync(id);
        if (save == null)
        {
            return false;
        }

        dbContext.GameSaves.Remove(save);
        await dbContext.SaveChangesAsync();

        _logger.LogEvent("GameSave", $"Deleted game save: {id}");
        return true;
    }
}
