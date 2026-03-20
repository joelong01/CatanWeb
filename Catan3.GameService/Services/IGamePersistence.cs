using Catan3.GameService.Data;

namespace Catan3.GameService.Services;

/// <summary>
/// Database persistence interface for game state.
/// Uses gameId as the primary identifier.
/// </summary>
public interface IGamePersistence
{
    /// <summary>
    /// Save game state to database (two-table upsert: metadata + data)
    /// </summary>
    Task<bool> SaveAsync(string gameId, byte[] data, GameMetadata metadata);

    /// <summary>
    /// Load compressed game data from database
    /// </summary>
    Task<byte[]?> LoadAsync(string gameId);

    /// <summary>
    /// Get list of saved games metadata for display
    /// </summary>
    Task<List<GameSaveMetadataEntity>> GetGamesAsync(string? startedBy = null);

    /// <summary>
    /// Delete a game save (cascades to data table)
    /// </summary>
    Task<bool> DeleteAsync(string gameId);
}

/// <summary>
/// Metadata for saving a game - passed to SaveAsync
/// </summary>
public class GameMetadata
{
    public string GameName { get; set; } = string.Empty;
    public string GameState { get; set; } = string.Empty;
    public string StartedBy { get; set; } = string.Empty;
    public int PlayerCount { get; set; }
    public string GameType { get; set; } = string.Empty;
    public string PlayerNames { get; set; } = string.Empty;
    public int TurnCount { get; set; }
}
