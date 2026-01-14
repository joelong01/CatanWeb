using System.Collections.Concurrent;
using System.Text.Json;
using Catan3.GameService.Data;
using Catan3.Shared.Models;
using Catan3.Shared.Utility;
using Microsoft.EntityFrameworkCore;

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
    /// <param name="gameId">The game ID to record</param>
    /// <param name="name">The recording name (typically the game name)</param>
    /// <param name="initialGameModel">The initial game state</param>
    /// <returns>The recording ID, or null if already recording this game</returns>
    public async Task<string?> StartRecordingAsync(string gameId, string name, GameModel initialGameModel)
    {
        var recording = new ActiveRecording(gameId, name, initialGameModel);

        if (!_activeRecordings.TryAdd(gameId, recording))
        {
            _logger.LogWarning("Game {GameId} is already being recorded", gameId);
            return null;
        }

        // Save to database immediately
        await SaveRecordingToDatabaseAsync(recording);

        _logger.LogInformation("Started recording '{Name}' for game {GameId}, recordingId: {RecordingId}",
            name, gameId, recording.RecordingId);
        return recording.RecordingId;
    }

    /// <summary>
    /// Checks if a game is currently being recorded.
    /// </summary>
    public bool IsRecording(string gameId)
    {
        return _activeRecordings.ContainsKey(gameId);
    }

    /// <summary>
    /// Gets the number of actions recorded so far for a game.
    /// </summary>
    public int GetActionCount(string gameId)
    {
        if (_activeRecordings.TryGetValue(gameId, out var recording))
        {
            return recording.Actions.Count;
        }
        return 0;
    }

    /// <summary>
    /// Gets the recording name for a game.
    /// </summary>
    public string? GetRecordingName(string gameId)
    {
        if (_activeRecordings.TryGetValue(gameId, out var recording))
        {
            return recording.Name;
        }
        return null;
    }

    /// <summary>
    /// Records an action and saves to database immediately.
    /// </summary>
    public async Task RecordActionAsync(string gameId, IRecordedMessage message)
    {
        if (_activeRecordings.TryGetValue(gameId, out var recording))
        {
            recording.Actions.Add(message);

            // Save to database after each action for crash recovery
            await SaveRecordingToDatabaseAsync(recording);

            _logger.LogDebug("Recorded action {ActionType} for game {GameId}, total actions: {Count}",
                message.RecordType, gameId, recording.Actions.Count);
        }
    }

    /// <summary>
    /// Stops recording. The recording is already saved in database.
    /// </summary>
    /// <returns>The recording entity, or null if not recording</returns>
    public async Task<RecordingEntity?> StopRecordingAsync(string gameId)
    {
        if (!_activeRecordings.TryRemove(gameId, out var recording))
        {
            _logger.LogWarning("No active recording for game {GameId}", gameId);
            return null;
        }

        // Final save to ensure everything is persisted
        var entity = await SaveRecordingToDatabaseAsync(recording);

        _logger.LogInformation("Stopped recording '{Name}' for game {GameId} with {ActionCount} actions",
            recording.Name, gameId, recording.Actions.Count);

        return entity;
    }

    /// <summary>
    /// Cancels an active recording and removes from database.
    /// </summary>
    public async Task<bool> CancelRecordingAsync(string gameId)
    {
        if (_activeRecordings.TryRemove(gameId, out var recording))
        {
            // Remove from database
            await DeleteRecordingAsync(recording.RecordingId);

            _logger.LogInformation("Cancelled recording {RecordingId} for game {GameId}",
                recording.RecordingId, gameId);
            return true;
        }
        return false;
    }

    /// <summary>
    /// Saves or updates the recording in the database.
    /// </summary>
    private async Task<RecordingEntity> SaveRecordingToDatabaseAsync(ActiveRecording recording)
    {
        var recordingData = new RecordingData
        {
            InitialGameModel = recording.InitialGameModel,
            Actions = recording.Actions
        };

        var entity = new RecordingEntity
        {
            Id = recording.RecordingId,
            Name = recording.Name,
            CreatedAt = recording.StartedAt,
            GameType = recording.InitialGameModel.GameType.ToString(),
            PlayerCount = recording.InitialGameModel.Players.Count,
            PlayerIds = string.Join(",", recording.InitialGameModel.Players.Select(p => p.Id)),
            ActionCount = recording.Actions.Count,
            Data = JsonHelper.Serialize(recordingData)
        };

        using var scope = _scopeFactory.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<CatanDbContext>();

        var existing = await dbContext.Recordings.FindAsync(recording.RecordingId);
        if (existing != null)
        {
            // Update existing
            existing.ActionCount = entity.ActionCount;
            existing.Data = entity.Data;
        }
        else
        {
            // Add new
            dbContext.Recordings.Add(entity);
        }

        await dbContext.SaveChangesAsync();
        return entity;
    }

    /// <summary>
    /// Gets all saved recordings from the database.
    /// </summary>
    public async Task<List<RecordingEntity>> GetRecordingsAsync()
    {
        using var scope = _scopeFactory.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<CatanDbContext>();
        return await dbContext.Recordings
            .OrderByDescending(r => r.CreatedAt)
            .ToListAsync();
    }

    /// <summary>
    /// Gets a specific recording by ID.
    /// </summary>
    public async Task<RecordingEntity?> GetRecordingAsync(string recordingId)
    {
        using var scope = _scopeFactory.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<CatanDbContext>();
        return await dbContext.Recordings.FindAsync(recordingId);
    }

    /// <summary>
    /// Deletes a recording from the database.
    /// </summary>
    public async Task<bool> DeleteRecordingAsync(string recordingId)
    {
        using var scope = _scopeFactory.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<CatanDbContext>();

        var recording = await dbContext.Recordings.FindAsync(recordingId);
        if (recording == null)
        {
            return false;
        }

        dbContext.Recordings.Remove(recording);
        await dbContext.SaveChangesAsync();

        _logger.LogInformation("Deleted recording {RecordingId}", recordingId);
        return true;
    }

    /// <summary>
    /// Renames a recording in the database.
    /// </summary>
    public async Task<bool> RenameRecordingAsync(string recordingId, string newName)
    {
        using var scope = _scopeFactory.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<CatanDbContext>();

        var recording = await dbContext.Recordings.FindAsync(recordingId);
        if (recording == null)
        {
            return false;
        }

        recording.Name = newName;
        await dbContext.SaveChangesAsync();

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
    /// Imports a recording from external source (e.g., syncing between local and Azure databases).
    /// Skips if a recording with the same ID already exists.
    /// </summary>
    public async Task<bool> ImportRecordingAsync(
        string id,
        string name,
        DateTime createdAt,
        string gameType,
        int playerCount,
        string playerIds,
        int actionCount,
        string data)
    {
        using var scope = _scopeFactory.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<CatanDbContext>();

        // Check if recording already exists
        var existing = await dbContext.Recordings.FindAsync(id);
        if (existing != null)
        {
            _logger.LogInformation("Recording {RecordingId} already exists, skipping import", id);
            return false;
        }

        var entity = new RecordingEntity
        {
            Id = id,
            Name = name,
            CreatedAt = createdAt,
            GameType = gameType,
            PlayerCount = playerCount,
            PlayerIds = playerIds,
            ActionCount = actionCount,
            Data = data
        };

        dbContext.Recordings.Add(entity);
        await dbContext.SaveChangesAsync();

        _logger.LogInformation("Imported recording {RecordingId} '{Name}'", id, name);
        return true;
    }
}
