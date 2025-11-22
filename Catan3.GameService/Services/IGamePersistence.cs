using Catan3.GameService.Data;

namespace Catan3.GameService.Services;

/// <summary>
/// Database persistence interface for game state.
/// Uses gameId as the primary identifier.
/// </summary>
public interface IGamePersistence
{
    /// <summary>
    /// Save game state to database
    /// </summary>
    Task<bool> SaveAsync(string gameId, byte[] data, GameMetadata metadata);

    /// <summary>
    /// Load game state from database
    /// </summary>
    Task<byte[]?> LoadAsync(string gameId);

    /// <summary>
    /// Get list of saved games for Join Game page
    /// </summary>
    Task<List<GameSaveEntity>> GetGamesAsync(string? startedBy = null, string? gameState = null);

    /// <summary>
    /// Delete a game save
    /// </summary>
    Task<bool> DeleteAsync(string gameId);
}

/// <summary>
/// Metadata for saving a game
/// </summary>
public class GameMetadata
{
    public string GameName { get; set; } = string.Empty;
    public string GameState { get; set; } = string.Empty;
    public string StartedBy { get; set; } = string.Empty;
    public int PlayerCount { get; set; }
    public string GameType { get; set; } = string.Empty;
}
