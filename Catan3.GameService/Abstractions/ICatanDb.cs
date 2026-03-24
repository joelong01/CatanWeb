using Catan3.Shared.Models;
using Catan3.Shared.Profiles;
using Catan3.Shared.Services;

namespace Catan3.GameService.Abstractions;

/// <summary>
/// Domain-specific database abstraction. No SQL semantics cross this boundary.
/// Implementations: CosmosCatanDb (CosmosDB), EfCoreCatanDb (future compatibility shim if needed).
/// </summary>
public interface ICatanDb
{
    /// <summary>Creates the database schema (containers/tables) if absent. Does not seed data.</summary>
    Task InitializeAsync();

    // ── Players ──────────────────────────────────────────────────────────────
    Task<IReadOnlyList<PlayerProfile>> LoadPlayersAsync();
    Task<PlayerProfile?> LoadPlayerAsync(string id);
    Task SavePlayerAsync(PlayerProfile player);
    Task DeletePlayerAsync(string id);

    // ── Images ────────────────────────────────────────────────────────────────
    Task<(byte[] Data, string ContentType)?> LoadImageAsync(string playerId);
    Task SaveImageAsync(string playerId, byte[] data, string contentType);
    Task DeleteImageAsync(string playerId);

    // ── Game saves ────────────────────────────────────────────────────────────
    Task<IReadOnlyList<GameSummary>> ListGamesAsync(string? startedBy = null);
    Task<GameSaveData?> LoadGameAsync(string gameId);
    Task SaveGameAsync(GameSaveData game);
    Task DeleteGameAsync(string gameId);
    Task<int> CountGamesAsync();

    // ── Completed games ───────────────────────────────────────────────────────
    Task SaveCompletedGameAsync(CompletedGameRecord game);
    Task<IReadOnlyList<CompletedGameRecord>> ListCompletedGamesAsync();

    // ── Templates ─────────────────────────────────────────────────────────────
    Task<IReadOnlyList<GameTemplateSummary>> ListTemplatesAsync(string? category = null);
    Task<GameTemplateData?> LoadTemplateAsync(string id);
    Task SaveTemplateAsync(
        string id, string name, string category,
        bool isSystemTemplate, GameTemplateData data);
    Task DeleteTemplateAsync(string id);

    // ── Recordings ────────────────────────────────────────────────────────────
    Task<IReadOnlyList<GameServiceProxy.RecordingSummary>> ListRecordingsAsync();
    Task<(GameServiceProxy.RecordingSummary Summary, string Data)?> LoadRecordingAsync(string id);
    Task<(GameServiceProxy.RecordingSummary Summary, string Data)?> FindRecordingByGameIdAsync(string gameId);
    Task SaveRecordingAsync(GameServiceProxy.RecordingSummary summary, string data);
    Task DeleteRecordingAsync(string id);
    Task DeleteRecordingsByGameIdAsync(string gameId);
}
