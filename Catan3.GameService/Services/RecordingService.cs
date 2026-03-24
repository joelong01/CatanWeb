using System.Collections.Concurrent;
using System.Text.Json;
using Catan3.GameService.Abstractions;
using Catan3.Shared.Models;
using Catan3.Shared.Services;
using Catan3.Shared.Utility;

namespace Catan3.GameService.Services;

/// <summary>
/// Tracks active recordings in memory during gameplay.
/// Records are persisted to database incrementally after each action.
/// </summary>
public class ActiveRecording
{
    public string RecordingId { get; } = Guid.NewGuid().ToString();
    public string GameId { get; }
    public string Name { get; }
    public GameModel InitialGameModel { get; }
    public List<IRecordedMessage> Actions { get; } = [];
    public DateTime StartedAt { get; } = DateTime.UtcNow;

    public ActiveRecording(string gameId, string name, GameModel initialGameModel)
    {
        GameId = gameId;
        Name = name;
        InitialGameModel = initialGameModel;
    }
}

/// <summary>
/// Recording data format for JSON serialization.
/// Contains the initial game state and all recorded actions.
/// </summary>
public class RecordingData
{
    public GameModel InitialGameModel { get; set; } = null!;
    public List<IRecordedMessage> Actions { get; set; } = [];
}

/// <summary>
/// Service for recording gameplay actions during a session.
/// Recordings are saved to database incrementally for crash recovery.
/// </summary>
public class RecordingService
{
    private readonly ConcurrentDictionary<string, ActiveRecording> _activeRecordings = new();
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<RecordingService> _logger;

    public RecordingService(IServiceScopeFactory scopeFactory, ILogger<RecordingService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    /// <summary>
    /// Starts recording for a game. Captures the initial game state and saves to database immediately.
    /// </summary>
    public async Task<string?> StartRecordingAsync(string gameId, string name, GameModel initialGameModel)
    {
        var recording = new ActiveRecording(gameId, name, initialGameModel);

        if (!_activeRecordings.TryAdd(gameId, recording))
        {
            _logger.LogWarning("Game {GameId} is already being recorded", gameId);
            return null;
        }

        // Delete any existing recordings for this GameID to prevent duplicates
        await DeleteRecordingsByGameIdInternalAsync(initialGameModel.GameId);

        // Save to database immediately
        await SaveRecordingToDatabaseAsync(recording);

        _logger.LogInformation("Started recording '{Name}' for game {GameId}, recordingId: {RecordingId}",
            name, gameId, recording.RecordingId);
        return recording.RecordingId;
    }

    public bool IsRecording(string gameId) => _activeRecordings.ContainsKey(gameId);

    public int GetActionCount(string gameId) =>
        _activeRecordings.TryGetValue(gameId, out var recording) ? recording.Actions.Count : 0;

    public string? GetRecordingName(string gameId) =>
        _activeRecordings.TryGetValue(gameId, out var recording) ? recording.Name : null;

    /// <summary>
    /// Records an action and saves to database immediately.
    /// </summary>
    public async Task RecordActionAsync(string gameId, IRecordedMessage message)
    {
        if (_activeRecordings.TryGetValue(gameId, out var recording))
        {
            recording.Actions.Add(message);
            await SaveRecordingToDatabaseAsync(recording);

            _logger.LogDebug("Recorded action {ActionType} for game {GameId}, total actions: {Count}",
                message.RecordType, gameId, recording.Actions.Count);
        }
    }

    /// <summary>
    /// Stops recording. Returns summary + data, or null if not recording.
    /// </summary>
    public async Task<(GameServiceProxy.RecordingSummary Summary, string Data)?> StopRecordingAsync(string gameId)
    {
        if (!_activeRecordings.TryRemove(gameId, out var recording))
        {
            _logger.LogWarning("No active recording for game {GameId}", gameId);
            return null;
        }

        // Final save
        var (summary, data) = await SaveRecordingToDatabaseAsync(recording);

        _logger.LogInformation("Stopped recording '{Name}' for game {GameId} with {ActionCount} actions",
            recording.Name, gameId, recording.Actions.Count);

        return (summary, data);
    }

    /// <summary>
    /// Cancels an active recording and removes from database.
    /// </summary>
    public async Task<bool> CancelRecordingAsync(string gameId)
    {
        if (_activeRecordings.TryRemove(gameId, out var recording))
        {
            await DeleteRecordingAsync(recording.RecordingId);
            _logger.LogInformation("Cancelled recording {RecordingId} for game {GameId}",
                recording.RecordingId, gameId);
            return true;
        }
        return false;
    }

    /// <summary>
    /// Saves or updates the recording in the database. Returns summary + data.
    /// </summary>
    private async Task<(GameServiceProxy.RecordingSummary Summary, string Data)> SaveRecordingToDatabaseAsync(ActiveRecording recording)
    {
        var recordingData = new RecordingData
        {
            InitialGameModel = recording.InitialGameModel,
            Actions = recording.Actions
        };
        var jsonData = JsonHelper.Serialize(recordingData);

        var summary = new GameServiceProxy.RecordingSummary
        {
            Id = recording.RecordingId,
            Name = recording.Name,
            CreatedAt = recording.StartedAt,
            GameType = recording.InitialGameModel.GameType.ToString(),
            PlayerCount = recording.InitialGameModel.Players.Count,
            ActionCount = recording.Actions.Count,
            GameId = recording.InitialGameModel.GameId,
        };

        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ICatanDb>();
        await db.SaveRecordingAsync(summary, jsonData);

        return (summary, jsonData);
    }

    /// <summary>
    /// Gets all saved recordings from the database.
    /// </summary>
    public async Task<IReadOnlyList<GameServiceProxy.RecordingSummary>> GetRecordingsAsync()
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ICatanDb>();
        return await db.ListRecordingsAsync();
    }

    /// <summary>
    /// Gets a specific recording by ID (summary + data).
    /// </summary>
    public async Task<(GameServiceProxy.RecordingSummary Summary, string Data)?> GetRecordingAsync(string recordingId)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ICatanDb>();
        return await db.LoadRecordingAsync(recordingId);
    }

    /// <summary>
    /// Deletes a recording from the database.
    /// </summary>
    public async Task<bool> DeleteRecordingAsync(string recordingId)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ICatanDb>();

        var existing = await db.LoadRecordingAsync(recordingId);
        if (existing == null) return false;

        await db.DeleteRecordingAsync(recordingId);
        _logger.LogInformation("Deleted recording {RecordingId}", recordingId);
        return true;
    }

    /// <summary>
    /// Deletes all recordings for a given GameID.
    /// </summary>
    private async Task DeleteRecordingsByGameIdInternalAsync(string gameId)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ICatanDb>();
        await db.DeleteRecordingsByGameIdAsync(gameId);
    }

    /// <summary>
    /// Renames a recording in the database.
    /// </summary>
    public async Task<bool> RenameRecordingAsync(string recordingId, string newName)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ICatanDb>();

        var existing = await db.LoadRecordingAsync(recordingId);
        if (existing == null) return false;

        var (summary, data) = existing.Value;
        // Create updated summary with new name
        var updated = new GameServiceProxy.RecordingSummary
        {
            Id = summary.Id,
            Name = newName,
            CreatedAt = summary.CreatedAt,
            GameType = summary.GameType,
            PlayerCount = summary.PlayerCount,
            ActionCount = summary.ActionCount,
            GameId = summary.GameId,
        };
        await db.SaveRecordingAsync(updated, data);

        _logger.LogInformation("Renamed recording {RecordingId} to '{NewName}'", recordingId, newName);
        return true;
    }

    /// <summary>
    /// Parses the recording data from JSON.
    /// </summary>
    public RecordingData? ParseRecordingData(string jsonData)
    {
        try
        {
            return JsonHelper.Deserialize<RecordingData>(jsonData);
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, "Failed to parse recording data");
            return null;
        }
    }

    /// <summary>
    /// Imports a recording from external source.
    /// Skips if a recording with the same ID already exists.
    /// </summary>
    public async Task<bool> ImportRecordingAsync(
        string id, string name, DateTime createdAt,
        string gameType, int playerCount, string playerIds,
        int actionCount, string data)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ICatanDb>();

        var existing = await db.LoadRecordingAsync(id);
        if (existing != null)
        {
            _logger.LogInformation("Recording {RecordingId} already exists, skipping import", id);
            return false;
        }

        var summary = new GameServiceProxy.RecordingSummary
        {
            Id = id,
            Name = name,
            CreatedAt = createdAt,
            GameType = gameType,
            PlayerCount = playerCount,
            ActionCount = actionCount,
            GameId = string.Empty, // imported recordings may not have gameId
        };
        await db.SaveRecordingAsync(summary, data);

        _logger.LogInformation("Imported recording {RecordingId} '{Name}'", id, name);
        return true;
    }
}
