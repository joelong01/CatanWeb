using Catan3.GameService.Abstractions;

namespace Catan3.GameService.Services;

/// <summary>
/// Database persistence interface for game state.
/// Uses gameId as the primary identifier.
/// </summary>
public interface IGamePersistence
{
    /// <summary>
    /// Save game state to database.
    /// </summary>
    Task<bool> SaveAsync(string gameId, byte[] data, GameMetadata metadata);

    /// <summary>
    /// Load compressed game data from database.
    /// </summary>
    Task<byte[]?> LoadAsync(string gameId);

    /// <summary>
    /// Get list of saved games summaries for display.
    /// </summary>
    Task<List<GameSummary>> GetGameSummariesAsync(string? startedBy = null);

    /// <summary>
    /// Delete a game save.
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
    /// <summary>
    /// Display names. Written only on the completed-game path, where the record is a
    /// point-in-time document. Saved (in-progress) games leave this empty and store
    /// <see cref="PlayerIds"/> instead (issue #208).
    /// </summary>
    public string PlayerNames { get; set; } = string.Empty;

    /// <summary>Authoritative player identity.</summary>
    public List<string> PlayerIds { get; set; } = [];

    public int TurnCount { get; set; }
}
