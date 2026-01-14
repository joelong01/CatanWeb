using System.Collections.Concurrent;
using Microsoft.AspNetCore.Mvc;
using Catan3.GameService.Services;
using Catan3.Shared.Models;
using Catan3.Shared.GameLogic;
using Catan3.Shared.Utility;
using Catan3.Shared.Interfaces;

namespace Catan3.GameService.Controllers;

/// <summary>
/// Request to start recording with a name.
/// </summary>
public class StartRecordingRequest
{
    public string Name { get; set; } = string.Empty;
}

/// <summary>
/// Request to rename a recording.
/// </summary>
public class RenameRecordingRequest
{
    public string Name { get; set; } = string.Empty;
}

/// <summary>
/// Request to import a recording from another database.
/// </summary>
public class ImportRecordingRequest
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public string GameType { get; set; } = string.Empty;
    public int PlayerCount { get; set; }
    public string PlayerIds { get; set; } = string.Empty;
    public int ActionCount { get; set; }
    public string Data { get; set; } = string.Empty;
}

/// <summary>
/// Summary of a recording for list display.
/// </summary>
public class RecordingSummary
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public string GameType { get; set; } = string.Empty;
    public int PlayerCount { get; set; }
    public int ActionCount { get; set; }
}

/// <summary>
/// Response for recording status queries.
/// </summary>
public class RecordingStatusResponse
{
    public bool IsRecording { get; set; }
    public int ActionCount { get; set; }
}

/// <summary>
/// Result of replaying a recording.
/// </summary>
public class ReplayResult
{
    public bool Success { get; set; }
    public string RecordingName { get; set; } = string.Empty;
    public int ActionsReplayed { get; set; }
    public int TotalActions { get; set; }
    public int? FailedAtAction { get; set; }
    public string? ExpectedHash { get; set; }
    public string? ActualHash { get; set; }
    public string? ErrorMessage { get; set; }
}

/// <summary>
/// Summary of an action for display in the UI.
/// </summary>
public class ActionSummary
{
    public int Index { get; set; }
    public string ActionType { get; set; } = string.Empty;
    public string GameState { get; set; } = string.Empty;
    public string ExpectedHash { get; set; } = string.Empty;
    public string Details { get; set; } = string.Empty;
}

/// <summary>
/// Result of executing a single action step.
/// </summary>
public class StepResult
{
    public bool Success { get; set; }
    public int ActionIndex { get; set; }
    public string ExpectedHash { get; set; } = string.Empty;
    public string ActualHash { get; set; } = string.Empty;
    public bool HashMatch { get; set; }
    public string? ErrorMessage { get; set; }
}

/// <summary>
/// Tracks an active step-by-step replay session.
/// </summary>
public class ReplaySession
{
    public string SessionId { get; } = Guid.NewGuid().ToString();
    public string RecordingId { get; }
    public string RecordingName { get; }
    public GameStateMachine GameStateMachine { get; }
    public List<IRecordedMessage> Actions { get; }
    public int CurrentIndex { get; set; }
    public DateTime CreatedAt { get; } = DateTime.UtcNow;

    public ReplaySession(string recordingId, string recordingName, GameStateMachine gameStateMachine, List<IRecordedMessage> actions)
    {
        RecordingId = recordingId;
        RecordingName = recordingName;
        GameStateMachine = gameStateMachine;
        Actions = actions;
        CurrentIndex = 0;
    }
}

/// <summary>
/// API endpoints for managing test recordings.
/// </summary>
[ApiController]
[Route("api")]
public class RecordingController : ControllerBase
{
    private static readonly ConcurrentDictionary<string, ReplaySession> _replaySessions = new();

    private readonly RecordingService _recordingService;
    private readonly ILogger<RecordingController> _logger;
    private readonly ILoggerFactory _loggerFactory;
    private readonly IPersistenceService _persistenceService;

    public RecordingController(
        RecordingService recordingService,
        ILogger<RecordingController> logger,
        ILoggerFactory loggerFactory,
        IPersistenceService persistenceService)
    {
        _recordingService = recordingService;
        _logger = logger;
        _loggerFactory = loggerFactory;
        _persistenceService = persistenceService;
    }

    /// <summary>
    /// Gets all saved recordings.
    /// </summary>
    [HttpGet("recordings")]
    public async Task<ActionResult<List<RecordingSummary>>> GetRecordings()
    {
        var recordings = await _recordingService.GetRecordingsAsync();
        var summaries = recordings.Select(r => new RecordingSummary
        {
            Id = r.Id,
            Name = r.Name,
            CreatedAt = r.CreatedAt,
            GameType = r.GameType,
            PlayerCount = r.PlayerCount,
            ActionCount = r.ActionCount
        }).ToList();

        return Ok(summaries);
    }

    /// <summary>
    /// Gets a specific recording by ID (includes full data for replay).
    /// </summary>
    [HttpGet("recording/{id}")]
    public async Task<ActionResult> GetRecording(string id)
    {
        var recording = await _recordingService.GetRecordingAsync(id);
        if (recording == null)
        {
            return NotFound(new { message = $"Recording {id} not found" });
        }

        return Ok(new
        {
            id = recording.Id,
            name = recording.Name,
            createdAt = recording.CreatedAt,
            gameType = recording.GameType,
            playerCount = recording.PlayerCount,
            playerIds = recording.PlayerIds,
            actionCount = recording.ActionCount,
            data = recording.Data
        });
    }

    /// <summary>
    /// Starts recording for a game. Captures initial game state and saves to database immediately.
    /// </summary>
    [HttpPost("recording/start/{gameId}")]
    public async Task<ActionResult<object>> StartRecording(string gameId, [FromBody] StartRecordingRequest? request = null)
    {
        // Get the current game state
        GameStateMachine gameStateMachine;
        try
        {
            gameStateMachine = GameStateMachineRegistry.GetGameStateMachine(gameId);
        }
        catch (GameException)
        {
            return NotFound(new { message = $"Game {gameId} not found" });
        }

        var gameModel = gameStateMachine.GetCurrentState();

        // Use provided name or fall back to game name
        var recordingName = !string.IsNullOrWhiteSpace(request?.Name) ? request.Name : gameModel.GameName;

        var recordingId = await _recordingService.StartRecordingAsync(gameId, recordingName, gameModel);

        if (recordingId == null)
        {
            return Conflict(new { message = $"Game {gameId} is already being recorded" });
        }

        _logger.LogInformation("Started recording '{Name}' for game {GameId}, recordingId: {RecordingId}",
            recordingName, gameId, recordingId);

        return Ok(new
        {
            recordingId,
            name = recordingName,
            message = "Recording started"
        });
    }

    /// <summary>
    /// Stops recording. Recording is already saved to database after each action.
    /// </summary>
    [HttpPost("recording/stop/{gameId}")]
    public async Task<ActionResult<object>> StopRecording(string gameId)
    {
        var recording = await _recordingService.StopRecordingAsync(gameId);
        if (recording == null)
        {
            return NotFound(new { message = $"No active recording for game {gameId}" });
        }

        _logger.LogInformation("Stopped recording for game {GameId}, saved as {Name} with {ActionCount} actions",
            gameId, recording.Name, recording.ActionCount);

        return Ok(new
        {
            recordingId = recording.Id,
            name = recording.Name,
            actionCount = recording.ActionCount,
            message = "Recording saved"
        });
    }

    /// <summary>
    /// Gets the recording status for a game.
    /// </summary>
    [HttpGet("recording/status/{gameId}")]
    public ActionResult<RecordingStatusResponse> GetRecordingStatus(string gameId)
    {
        var isRecording = _recordingService.IsRecording(gameId);
        var actionCount = _recordingService.GetActionCount(gameId);

        return Ok(new RecordingStatusResponse
        {
            IsRecording = isRecording,
            ActionCount = actionCount
        });
    }

    /// <summary>
    /// Deletes a recording.
    /// </summary>
    [HttpDelete("recording/{id}")]
    public async Task<ActionResult> DeleteRecording(string id)
    {
        var deleted = await _recordingService.DeleteRecordingAsync(id);
        if (!deleted)
        {
            return NotFound(new { message = $"Recording {id} not found" });
        }

        _logger.LogInformation("Deleted recording {RecordingId}", id);
        return Ok(new { message = "Recording deleted" });
    }

    /// <summary>
    /// Imports a recording (for syncing between local and Azure databases).
    /// </summary>
    [HttpPost("recording/import")]
    public async Task<ActionResult> ImportRecording([FromBody] ImportRecordingRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Id) || string.IsNullOrWhiteSpace(request.Name))
        {
            return BadRequest(new { message = "Id and Name are required" });
        }

        var imported = await _recordingService.ImportRecordingAsync(
            request.Id,
            request.Name,
            request.CreatedAt,
            request.GameType,
            request.PlayerCount,
            request.PlayerIds,
            request.ActionCount,
            request.Data);

        if (!imported)
        {
            return Conflict(new { message = $"Recording {request.Id} already exists" });
        }

        _logger.LogInformation("Imported recording {RecordingId}: {Name}", request.Id, request.Name);
        return Ok(new { message = "Recording imported", id = request.Id });
    }

    /// <summary>
    /// Renames a recording.
    /// </summary>
    [HttpPut("recording/{id}/rename")]
    public async Task<ActionResult> RenameRecording(string id, [FromBody] RenameRecordingRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
        {
            return BadRequest(new { message = "New name is required" });
        }

        var renamed = await _recordingService.RenameRecordingAsync(id, request.Name);
        if (!renamed)
        {
            return NotFound(new { message = $"Recording {id} not found" });
        }

        _logger.LogInformation("Renamed recording {RecordingId} to '{NewName}'", id, request.Name);
        return Ok(new { message = "Recording renamed", name = request.Name });
    }

    /// <summary>
    /// Cancels an active recording and removes from database.
    /// </summary>
    [HttpPost("recording/cancel/{gameId}")]
    public async Task<ActionResult> CancelRecording(string gameId)
    {
        var cancelled = await _recordingService.CancelRecordingAsync(gameId);
        if (!cancelled)
        {
            return NotFound(new { message = $"No active recording for game {gameId}" });
        }

        _logger.LogInformation("Cancelled recording for game {GameId}", gameId);
        return Ok(new { message = "Recording cancelled" });
    }

    /// <summary>
    /// Replays a recording and verifies each action produces the expected GameHash.
    /// </summary>
    [HttpPost("recording/{id}/replay")]
    public async Task<ActionResult<ReplayResult>> ReplayRecording(string id)
    {
        // Load the recording
        var recording = await _recordingService.GetRecordingAsync(id);
        if (recording == null)
        {
            return NotFound(new { message = $"Recording {id} not found" });
        }

        // Parse the recording data
        var recordingData = _recordingService.ParseRecordingData(recording.Data);
        if (recordingData == null)
        {
            return BadRequest(new { message = "Failed to parse recording data" });
        }

        _logger.LogInformation("Starting replay of recording {RecordingId} ({Name}) with {ActionCount} actions",
            id, recording.Name, recordingData.Actions.Count);

        try
        {
            // Create a GameStateMachine from the initial GameModel
            var gameLog = new Log<string>(_persistenceService, string.Empty);
            gameLog.InTestMode = true; // Skip persistence during replay
            var gameStateMachine = CreateGameStateMachine(gameLog);
            gameStateMachine.InitializeLoggingState(recordingData.InitialGameModel);

            // Execute each action and verify hash
            for (int i = 0; i < recordingData.Actions.Count; i++)
            {
                var action = recordingData.Actions[i];

                // Execute the action
                var resultModel = await ExecuteRecordedAction(gameStateMachine, action, recordingData.InitialGameModel.GameId);

                // ExpectedGameHash is the POST-action hash (captured after action executed during recording)
                // Compare actual result with the current action's expected hash
                if (!string.IsNullOrEmpty(action.ExpectedGameHash) &&
                    resultModel.GameHash != action.ExpectedGameHash)
                {
                    _logger.LogWarning("Hash mismatch at action {ActionIndex}: expected {Expected}, got {Actual}",
                        i, action.ExpectedGameHash, resultModel.GameHash);
                    return Ok(new ReplayResult
                    {
                        Success = false,
                        RecordingName = recording.Name,
                        ActionsReplayed = i + 1,
                        TotalActions = recordingData.Actions.Count,
                        FailedAtAction = i,
                        ExpectedHash = action.ExpectedGameHash,
                        ActualHash = resultModel.GameHash,
                        ErrorMessage = $"Hash mismatch after {action.GetType().Name}"
                    });
                }
            }

            _logger.LogInformation("Successfully replayed recording {RecordingId} ({Name})", id, recording.Name);
            return Ok(new ReplayResult
            {
                Success = true,
                RecordingName = recording.Name,
                ActionsReplayed = recordingData.Actions.Count,
                TotalActions = recordingData.Actions.Count
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error replaying recording {RecordingId}", id);
            return Ok(new ReplayResult
            {
                Success = false,
                RecordingName = recording.Name,
                ErrorMessage = ex.Message
            });
        }
    }

    /// <summary>
    /// Creates a GameStateMachine with proper dependencies.
    /// </summary>
    private GameStateMachine CreateGameStateMachine(IGameLog gameLog)
    {
        var gameServiceLogger = _loggerFactory.CreateLogger<GameStateMachine>();
        var gameLogger = new GameServiceLogger(gameServiceLogger);
        return new GameStateMachine(gameLog, gameLogger, _persistenceService);
    }

    /// <summary>
    /// Executes a recorded action on the GameStateMachine.
    /// </summary>
    private async Task<GameModel> ExecuteRecordedAction(GameStateMachine gameStateMachine, IRecordedMessage action, string gameId)
    {
        return action switch
        {
            ShuffleRecord => await gameStateMachine.HandleShuffleAsync(new ShuffleMessage()),
            NextRecord => await gameStateMachine.HandleNextAsync(new NextMessage()),
            GoFirstRecord goFirst => await gameStateMachine.HandleGoFirstAsync(new GoFirstMessage(goFirst.PlayerId)),
            BuildingUpgradeRecord buildingUpgrade => await gameStateMachine.HandleBuildingUpgradeAsync(
                new BuildingUpgradeMessage(buildingUpgrade.BuildingKey)),
            RoadPurchaseRecord roadPurchase => await gameStateMachine.HandleRoadPurchaseAsync(
                new RoadPurchaseMessage(roadPurchase.RoadKey)),
            MoveRobberRecord moveRobber => await gameStateMachine.HandleMoveRobberAsync(
                new MoveRobberMessage(moveRobber.Coordinates, moveRobber.TargetPlayerId)),
            RollRecord roll => await gameStateMachine.HandleRollAsync(
                new RollMessage(roll.Roll)),
            SetPlayerOrderRecord setPlayerOrder => await gameStateMachine.HandleSetPlayerOrderAsync(
                new SetPlayerOrderMessage(setPlayerOrder.PlayerIds)),
            ParticipatingInSupplementalRecord participatingInSupplemental => await gameStateMachine.HandleParticipatingInSupplementalAsync(
                new ParticipatingInSupplementalMessage(participatingInSupplemental.PlayerId, participatingInSupplemental.Participating)),
            BalanceBoardRecord => await gameStateMachine.HandleBalanceBoardAsync(new BalanceBoardMessage()),
            PurchaseRecord purchase => await gameStateMachine.HandlePurchaseAsync(new PurchaseMessage(purchase.Entitlement)),
            UndoRecord => await gameStateMachine.HandleUndoAsync(new UndoMessage()),
            RedoRecord => await gameStateMachine.HandleRedoAsync(new RedoMessage()),
            SwapTileResourcesRecord swap => await gameStateMachine.HandleSwapResourcesAsync(
                new SwapTileResources(
                    swap.SourceTileCoordinates,
                    swap.DestinationTileCoordinates,
                    swap.SourceCurrentResource,
                    swap.DestinationCurrentResource)),
            DeclareWinnerRecord winner => await gameStateMachine.HandleDeclareWinnerAsync(
                new DeclareWinnerMessage(winner.WinnerId)),
            _ => throw new NotImplementedException($"Action type {action.GetType().Name} not implemented for replay")
        };
    }

    /// <summary>
    /// Gets action details from a recording for display in the UI.
    /// </summary>
    [HttpGet("recording/{id}/actions")]
    public async Task<ActionResult<List<ActionSummary>>> GetRecordingActions(string id)
    {
        var recording = await _recordingService.GetRecordingAsync(id);
        if (recording == null)
        {
            return NotFound(new { message = $"Recording {id} not found" });
        }

        var recordingData = _recordingService.ParseRecordingData(recording.Data);
        if (recordingData == null)
        {
            return BadRequest(new { message = "Failed to parse recording data" });
        }

        var summaries = recordingData.Actions.Select((action, index) => new ActionSummary
        {
            Index = index,
            ActionType = action.RecordType,
            GameState = action.ExpectedGameState.ToString(),
            ExpectedHash = action.ExpectedGameHash ?? "",
            Details = GetActionDetails(action)
        }).ToList();

        return Ok(summaries);
    }

    /// <summary>
    /// Starts a step-by-step replay session for a recording.
    /// </summary>
    [HttpPost("recording/{id}/replay/start")]
    public async Task<ActionResult> StartReplaySession(string id)
    {
        var recording = await _recordingService.GetRecordingAsync(id);
        if (recording == null)
        {
            return NotFound(new { message = $"Recording {id} not found" });
        }

        var recordingData = _recordingService.ParseRecordingData(recording.Data);
        if (recordingData == null)
        {
            return BadRequest(new { message = "Failed to parse recording data" });
        }

        // Create game state machine with initial game model
        var gameLog = new Log<string>(_persistenceService, string.Empty);
        gameLog.InTestMode = true; // Skip persistence during replay
        var gameStateMachine = CreateGameStateMachine(gameLog);
        gameStateMachine.InitializeLoggingState(recordingData.InitialGameModel);

        // Create and store session
        var session = new ReplaySession(id, recording.Name, gameStateMachine, recordingData.Actions);
        _replaySessions[session.SessionId] = session;

        _logger.LogInformation("Started replay session {SessionId} for recording {RecordingId} with {ActionCount} actions",
            session.SessionId, id, recordingData.Actions.Count);

        return Ok(new
        {
            sessionId = session.SessionId,
            recordingName = recording.Name,
            totalActions = recordingData.Actions.Count,
            currentIndex = 0
        });
    }

    /// <summary>
    /// Executes the next action in a replay session.
    /// </summary>
    [HttpPost("replay/{sessionId}/step")]
    public async Task<ActionResult<StepResult>> StepReplaySession(string sessionId)
    {
        if (!_replaySessions.TryGetValue(sessionId, out var session))
        {
            return NotFound(new { message = $"Replay session {sessionId} not found" });
        }

        if (session.CurrentIndex >= session.Actions.Count)
        {
            return BadRequest(new { message = "No more actions to replay" });
        }

        var action = session.Actions[session.CurrentIndex];
        var actionIndex = session.CurrentIndex;

        try
        {
            // Execute the action
            var resultModel = await ExecuteRecordedAction(session.GameStateMachine, action, session.RecordingId);
            session.CurrentIndex++;

            // ExpectedGameHash is the POST-action hash (captured after action executed during recording)
            // So we compare the actual result with the current action's expected hash
            string expectedHash = action.ExpectedGameHash ?? "";
            var hashMatch = string.IsNullOrEmpty(expectedHash) || resultModel.GameHash == expectedHash;

            _logger.LogDebug("Replay step {ActionIndex}: {ActionType}, hash match: {HashMatch}",
                actionIndex, action.RecordType, hashMatch);

            return Ok(new StepResult
            {
                Success = true,
                ActionIndex = actionIndex,
                ExpectedHash = expectedHash,
                ActualHash = resultModel.GameHash,
                HashMatch = hashMatch
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error executing replay step {ActionIndex}", actionIndex);
            return Ok(new StepResult
            {
                Success = false,
                ActionIndex = actionIndex,
                ErrorMessage = ex.Message
            });
        }
    }

    /// <summary>
    /// Ends a replay session and cleans up resources.
    /// </summary>
    [HttpDelete("replay/{sessionId}")]
    public ActionResult EndReplaySession(string sessionId)
    {
        if (_replaySessions.TryRemove(sessionId, out _))
        {
            _logger.LogInformation("Ended replay session {SessionId}", sessionId);
            return Ok(new { message = "Replay session ended" });
        }

        return NotFound(new { message = $"Replay session {sessionId} not found" });
    }

    /// <summary>
    /// Gets human-readable details for an action.
    /// </summary>
    private static string GetActionDetails(IRecordedMessage action)
    {
        return action switch
        {
            RollRecord roll => $"Roll: {roll.Roll.RedRoll} + {roll.Roll.WhiteRoll} = {roll.Roll.RedRoll + roll.Roll.WhiteRoll}",
            BuildingUpgradeRecord upgrade => $"Building: {upgrade.BuildingKey}",
            RoadPurchaseRecord road => $"Road: {road.RoadKey}",
            MoveRobberRecord robber => $"To: {robber.Coordinates}",
            GoFirstRecord goFirst => $"Player: {goFirst.PlayerId}",
            PurchaseRecord purchase => $"Entitlement: {purchase.Entitlement}",
            SetPlayerOrderRecord order => $"Order: {string.Join(", ", order.PlayerIds)}",
            ParticipatingInSupplementalRecord supp => $"Player: {supp.PlayerId}, Participating: {supp.Participating}",
            SwapTileResourcesRecord swap => $"Swap: {swap.SourceTileCoordinates} <-> {swap.DestinationTileCoordinates}",
            DeclareWinnerRecord winner => $"Winner: {winner.WinnerId}",
            _ => ""
        };
    }
}
