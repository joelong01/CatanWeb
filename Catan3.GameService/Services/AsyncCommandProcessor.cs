using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Catan3.Shared.Models;
using Catan3.Shared.GameLogic;
using Catan3.Shared.Utility;
using Catan3.GameService.Utility;

namespace Catan3.GameService.Services
{
    /// <summary>
    /// Handles asynchronous command processing with SignalR notifications.
    /// Implements fire-and-forget pattern where commands return immediately
    /// and completion is notified via SignalR.
    /// </summary>
    public class AsyncCommandProcessor
    {
        private readonly SignalRNotificationService _signalRNotification;
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly RecordingService _recordingService;
        private readonly ILogger<AsyncCommandProcessor> _logger;

        /// <summary>
        /// Pending save requests per game. Only the latest state is kept — intermediate
        /// states are coalesced. A background task drains this and saves to the database.
        /// </summary>
        private readonly System.Collections.Concurrent.ConcurrentDictionary<string, (GameStateMachine stateMachine, GameModel model)> _pendingSaves = new();
        private int _saveRunning = 0; // 0 = idle, 1 = running

        public AsyncCommandProcessor(
            SignalRNotificationService signalRNotification,
            IServiceScopeFactory scopeFactory,
            RecordingService recordingService,
            ILogger<AsyncCommandProcessor> logger)
        {
            _signalRNotification = signalRNotification;
            _scopeFactory = scopeFactory;
            _recordingService = recordingService;
            _logger = logger;
        }

        /// <summary>
        /// Enqueues a save request. If a save is already in progress, the new state
        /// replaces the pending one (coalescing). A background task processes saves.
        /// </summary>
        private void EnqueueSave(GameStateMachine stateMachine, GameModel gameModel)
        {
            var gameId = gameModel.GameId;
            _pendingSaves[gameId] = (stateMachine, gameModel);

            // If no save task is running, start one
            if (Interlocked.CompareExchange(ref _saveRunning, 1, 0) == 0)
            {
                _ = Task.Run(ProcessPendingSavesAsync);
            }
        }

        /// <summary>
        /// Background task that drains all pending saves. Keeps running as long as
        /// there are pending saves (new ones may arrive while saving).
        /// </summary>
        private async Task ProcessPendingSavesAsync()
        {
            try
            {
                while (!_pendingSaves.IsEmpty)
                {
                    // Snapshot and clear all pending saves
                    var gameIds = _pendingSaves.Keys.ToList();
                    foreach (var gameId in gameIds)
                    {
                        if (_pendingSaves.TryRemove(gameId, out var pending))
                        {
                            await SaveGameToDatabaseAsync(pending.stateMachine, pending.model);
                        }
                    }
                }
            }
            finally
            {
                Interlocked.Exchange(ref _saveRunning, 0);

                // Check if new saves arrived while we were finishing
                if (!_pendingSaves.IsEmpty)
                {
                    if (Interlocked.CompareExchange(ref _saveRunning, 1, 0) == 0)
                    {
                        _ = Task.Run(ProcessPendingSavesAsync);
                    }
                }
            }
        }

        /// <summary>
        /// Processes a game command asynchronously with parallel operations
        /// </summary>
        /// <param name="request">The game command request</param>
        /// <param name="commandId">Unique command identifier for tracking</param>
        /// <param name="getGameStateMachine">Function to get GameStateMachine by gameId</param>
        public async Task ProcessAsync(JsonElement request, Guid commandId, Func<string, GameStateMachine> getGameStateMachine)
        {
            string? gameId = null;
            GameStateMachine? gameStateMachine = null;
            try
            {
                // Extract gameId for error reporting
                gameId = request.OptionalString("gameId");

                _logger.LogEvent("Process Command", $"Processing command {commandId} for game {gameId}");

                // Execute game logic using the provided GameStateMachine function
                var (gameModel, stateMachine, recordedMessage) = await ExecuteGameLogicAsync(request, getGameStateMachine);
                gameStateMachine = stateMachine;
                gameId = gameModel.GameId; // Ensure we have the correct gameId

                // Fast path: notify clients immediately
                var tasks = new List<Task>
                {
                    _signalRNotification.NotifyAsync(gameId, gameModel),
                    _signalRNotification.NotifyCommandCompletedAsync(gameId, commandId, true, "Command executed successfully"),
                    TryRecordActionAsync(gameId, recordedMessage)
                };

                await Task.WhenAll(tasks);

                // Slow path: save to database on background thread (fire-and-forget)
                // The save is O(N) in log depth (serialize + compress entire log).
                // Don't block the action path on it.
                EnqueueSave(gameStateMachine, gameModel);

                _logger.LogEvent("Command Completed", $"Command {commandId} completed successfully for game {gameId}");
            }
            catch (Exception ex)
            {
                _logger.LogEvent("Command Failed", $"Command {commandId} failed for game {gameId}: {ex.Message}", LogLevel.Error);

                // Notify about failure
                if (!string.IsNullOrEmpty(gameId))
                {
                    await _signalRNotification.NotifyCommandFailedAsync(gameId, commandId, ex.Message);
                }
            }
        }

        /// <summary>
        /// Saves the full game log (with undo/redo stacks) to the database
        /// </summary>
        private async Task SaveGameToDatabaseAsync(GameStateMachine gameStateMachine, GameModel gameModel)
        {
            _logger.LogDebug("SaveGameToDatabaseAsync called for game {GameId}", gameModel.GameId);
            var sw = System.Diagnostics.Stopwatch.StartNew();
            try
            {
                // Create a new scope for database operations (since we're a singleton)
                using var scope = _scopeFactory.CreateScope();
                var gamePersistence = scope.ServiceProvider.GetRequiredService<IGamePersistence>();

                // Get the full serializable log (preserves undo/redo stacks)
                var serializableLog = gameStateMachine.GetSerializableLog();
                var json = JsonHelper.Serialize(serializableLog);
                var compressed = JsonHelper.Compress(json);

                var serializeMs = sw.ElapsedMilliseconds;

                // Create metadata for queryability
                var metadata = new GameMetadata
                {
                    GameName = gameModel.GameName,
                    GameState = gameModel.GameState.ToString(),
                    StartedBy = "WebUI", // Placeholder until user auth is implemented
                    PlayerCount = gameModel.Players.Count,
                    GameType = gameModel.Tiles.Count > 19 ? "Expansion" : "Regular",
                    PlayerNames = string.Join(", ", gameModel.Players.Select(p => p.Name)),
                    TurnCount = serializableLog.DoneCount
                };

                // Save to database
                await gamePersistence.SaveAsync(gameModel.GameId, compressed, metadata);
                sw.Stop();
                _logger.LogDebug(
                    "Game {GameId} async save: serialize={SerializeMs}ms total={TotalMs}ms size={Size}bytes turns={Turns}",
                    gameModel.GameId, serializeMs, sw.ElapsedMilliseconds, compressed.Length, serializableLog.DoneCount);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to save game {GameId} to database", gameModel.GameId);
                // Don't throw - database save failure shouldn't break the game operation
            }
        }

        /// <summary>
        /// Executes the game logic using the provided GameStateMachine function
        /// </summary>
        private async Task<(GameModel gameModel, GameStateMachine stateMachine, IRecordedMessage recordedMessage)> ExecuteGameLogicAsync(JsonElement request, Func<string, GameStateMachine> getGameStateMachine)
        {
            return await Task.Run(async () =>
            {
                var gameId = request.RequireString("gameId", "request");
                var playerId = request.RequireString("playerId", "request");
                var messageType = request.RequireString("messageType", "request");
                var messageData = request.RequireProperty("messageData", "request");

                _logger.LogDebug("Processing message type: {MessageType} for game {GameId}", messageType, gameId);

                var gameStateMachine = getGameStateMachine(gameId);

                // Process message and build recorded message for replay
                (GameModel? model, IRecordedMessage record) = messageType switch
                {
                    "UndoMessage" => await ProcessAndRecordAsync<UndoMessage, UndoRecord>(
                        messageData, gameStateMachine, new UndoMessage(),
                        (gm, msg) => new UndoRecord(gm, msg), gs => gs.HandleUndoAsync),
                    "RedoMessage" => await ProcessAndRecordAsync<RedoMessage, RedoRecord>(
                        messageData, gameStateMachine, new RedoMessage(),
                        (gm, msg) => new RedoRecord(gm, msg), gs => gs.HandleRedoAsync),
                    "NextMessage" => await ProcessAndRecordAsync<NextMessage, NextRecord>(
                        messageData, gameStateMachine, new NextMessage(),
                        (gm, msg) => new NextRecord(gm, msg), gs => gs.HandleNextAsync),
                    "PurchaseMessage" => await ProcessPurchaseWithRecordAsync(messageData, gameStateMachine),
                    "RoadPurchaseMessage" => await ProcessRoadPurchaseWithRecordAsync(messageData, gameStateMachine),
                    "BuildingUpgradeMessage" => await ProcessBuildingUpgradeWithRecordAsync(messageData, gameStateMachine),
                    "MoveRobberMessage" => await ProcessMoveRobberWithRecordAsync(messageData, gameStateMachine),
                    "RollMessage" => await ProcessRollWithRecordAsync(messageData, gameStateMachine),
                    "SetPlayerOrderMessage" => await ProcessSetPlayerOrderWithRecordAsync(messageData, gameStateMachine),
                    "BalanceBoardMessage" => await ProcessAndRecordAsync<BalanceBoardMessage, BalanceBoardRecord>(
                        messageData, gameStateMachine, new BalanceBoardMessage(),
                        (gm, msg) => new BalanceBoardRecord(gm, msg), gs => gs.HandleBalanceBoardAsync),
                    "GoFirstMessage" => await ProcessGoFirstWithRecordAsync(messageData, gameStateMachine),
                    "ShuffleMessage" => await ProcessAndRecordAsync<ShuffleMessage, ShuffleRecord>(
                        messageData, gameStateMachine, new ShuffleMessage(),
                        (gm, msg) => new ShuffleRecord(gm, msg), gs => gs.HandleShuffleAsync),
                    "ParticipatingInSupplementalMessage" => await ProcessParticipatingInSupplementalWithRecordAsync(messageData, gameStateMachine),
                    "SwapTileResourcesMessage" => await ProcessSwapTileResourcesWithRecordAsync(messageData, gameStateMachine),
                    "DeclareWinnerMessage" => await ProcessDeclareWinnerWithRecordAsync(messageData, gameStateMachine),
                    _ => throw new JsonException($"Unknown message type: '{messageType}'. " +
                        $"Valid types: UndoMessage, RedoMessage, NextMessage, PurchaseMessage, " +
                        $"RoadPurchaseMessage, BuildingUpgradeMessage, MoveRobberMessage, RollMessage, " +
                        $"SetPlayerOrderMessage, BalanceBoardMessage, GoFirstMessage, ShuffleMessage, " +
                        $"ParticipatingInSupplementalMessage, SwapTileResourcesMessage, DeclareWinnerMessage")
                };

                var result = model ?? throw new InvalidOperationException("Game action failed to return updated model");
                return (result, gameStateMachine, record);
            });
        }

        private async Task TryRecordActionAsync(string gameId, IRecordedMessage message)
        {
            if (_recordingService.IsRecording(gameId))
            {
                await _recordingService.RecordActionAsync(gameId, message);
            }
        }

        /// <summary>
        /// Helper for simple message types that need no parsing from messageData.
        /// </summary>
        private async Task<(GameModel?, IRecordedMessage)> ProcessAndRecordAsync<TMsg, TRecord>(
            JsonElement messageData, GameStateMachine gsm, TMsg message,
            Func<GameModel, TMsg, TRecord> createRecord,
            Func<GameStateMachine, Func<TMsg, Task<GameModel>>> getHandler)
            where TRecord : IRecordedMessage
        {
            var model = await getHandler(gsm)(message);
            return (model, createRecord(model, message));
        }

        // ============================================================================
        // Individual message processing methods — each returns (GameModel, IRecordedMessage)
        // All use RequireXxx extension methods for descriptive error messages
        // ============================================================================

        private async Task<(GameModel?, IRecordedMessage)> ProcessPurchaseWithRecordAsync(JsonElement messageData, GameStateMachine gsm)
        {
            var entitlement = messageData.RequireEnum<Entitlement>("entitlement", "PurchaseMessage");
            var msg = new PurchaseMessage(entitlement);
            var model = await gsm.HandlePurchaseAsync(msg);
            return (model, new PurchaseRecord(model, msg));
        }

        private async Task<(GameModel?, IRecordedMessage)> ProcessRoadPurchaseWithRecordAsync(JsonElement messageData, GameStateMachine gsm)
        {
            var roadKeyData = messageData.RequireProperty("roadKey", "RoadPurchaseMessage");
            var tileKey = roadKeyData.RequireHexCoordinates("tileKey", "roadKey");
            var hexSide = roadKeyData.RequireEnum<HexSide>("hexSide", "roadKey");
            var msg = new RoadPurchaseMessage(new RoadKey(tileKey, hexSide));
            var model = await gsm.HandleRoadPurchaseAsync(msg);
            return (model, new RoadPurchaseRecord(model, msg));
        }

        private async Task<(GameModel?, IRecordedMessage)> ProcessBuildingUpgradeWithRecordAsync(JsonElement messageData, GameStateMachine gsm)
        {
            var buildingKeyData = messageData.RequireProperty("buildingKey", "BuildingUpgradeMessage");
            var hexCoordinates = buildingKeyData.RequireHexCoordinates("hexCoordinates", "buildingKey");
            var position = buildingKeyData.RequireEnum<HexPosition>("position", "buildingKey");
            var msg = new BuildingUpgradeMessage(new BuildingKey(hexCoordinates, position));
            var model = await gsm.HandleBuildingUpgradeAsync(msg);
            return (model, new BuildingUpgradeRecord(model, msg));
        }

        private async Task<(GameModel?, IRecordedMessage)> ProcessMoveRobberWithRecordAsync(JsonElement messageData, GameStateMachine gsm)
        {
            var coordinates = messageData.RequireHexCoordinates("coordinates", "MoveRobberMessage");
            var targetPlayerId = messageData.OptionalString("targetPlayerId");
            var msg = new MoveRobberMessage(coordinates, targetPlayerId);
            var model = await gsm.HandleMoveRobberAsync(msg);
            return (model, new MoveRobberRecord(model, msg));
        }

        private async Task<(GameModel?, IRecordedMessage)> ProcessRollWithRecordAsync(JsonElement messageData, GameStateMachine gsm)
        {
            var rollData = messageData.RequireProperty("roll", "RollMessage");
            var normalRoll = rollData.RequireEnum<ValidCatanRoll>("normalRoll", "roll");

            var specialDice = SpecialDice.None;
            var specialStr = rollData.OptionalString("specialDice");
            if (!string.IsNullOrEmpty(specialStr))
                Enum.TryParse<SpecialDice>(specialStr, out specialDice);

            int totalRoll = (int)normalRoll;
            int redRoll = totalRoll / 2;
            int whiteRoll = totalRoll - redRoll;

            var msg = new RollMessage(new TurnRollModel(redRoll, whiteRoll) { SpecialDice = specialDice });
            var model = await gsm.HandleRollAsync(msg);
            return (model, new RollRecord(model, msg));
        }

        private async Task<(GameModel?, IRecordedMessage)> ProcessSetPlayerOrderWithRecordAsync(JsonElement messageData, GameStateMachine gsm)
        {
            var playerIdsElement = messageData.RequireProperty("playerIds", "SetPlayerOrderMessage");
            var playerIds = playerIdsElement.EnumerateArray()
                .Select(element => element.GetString())
                .Where(id => !string.IsNullOrEmpty(id))
                .Cast<string>()
                .ToList();

            if (playerIds.Count == 0)
                throw new JsonException("SetPlayerOrderMessage requires at least one player ID in 'playerIds' array");

            var msg = new SetPlayerOrderMessage(playerIds);
            var model = await gsm.HandleSetPlayerOrderAsync(msg);
            return (model, new SetPlayerOrderRecord(model, msg));
        }

        private async Task<(GameModel?, IRecordedMessage)> ProcessGoFirstWithRecordAsync(JsonElement messageData, GameStateMachine gsm)
        {
            var playerId = messageData.RequireString("playerId", "GoFirstMessage");
            var msg = new GoFirstMessage(playerId);
            var model = await gsm.HandleGoFirstAsync(msg);
            return (model, new GoFirstRecord(model, msg));
        }

        private async Task<(GameModel?, IRecordedMessage)> ProcessParticipatingInSupplementalWithRecordAsync(JsonElement messageData, GameStateMachine gsm)
        {
            var playerId = messageData.RequireString("playerId", "ParticipatingInSupplementalMessage");
            var participating = messageData.RequireBool("participating", "ParticipatingInSupplementalMessage");
            var msg = new ParticipatingInSupplementalMessage(playerId, participating);
            var model = await gsm.HandleParticipatingInSupplementalAsync(msg);
            return (model, new ParticipatingInSupplementalRecord(model, msg));
        }

        private async Task<(GameModel?, IRecordedMessage)> ProcessSwapTileResourcesWithRecordAsync(JsonElement messageData, GameStateMachine gsm)
        {
            var sourceCoords = messageData.RequireHexCoordinates("sourceTileCoordinates", "SwapTileResourcesMessage");
            var destCoords = messageData.RequireHexCoordinates("destinationTileCoordinates", "SwapTileResourcesMessage");
            var sourceResource = messageData.RequireEnum<ResourceType>("sourceCurrentResource", "SwapTileResourcesMessage");
            var destResource = messageData.RequireEnum<ResourceType>("destinationCurrentResource", "SwapTileResourcesMessage");
            var msg = new SwapTileResources(sourceCoords, destCoords, sourceResource, destResource);
            var model = await gsm.HandleSwapResourcesAsync(msg);
            return (model, new SwapTileResourcesRecord(model, msg));
        }

        private async Task<(GameModel?, IRecordedMessage)> ProcessDeclareWinnerWithRecordAsync(JsonElement messageData, GameStateMachine gsm)
        {
            var winnerId = messageData.RequireString("winnerId", "DeclareWinnerMessage");
            var message = new DeclareWinnerMessage { WinnerId = winnerId };

            if (messageData.TryGetProperty("victoryPoints", out var vpElement))
            {
                message.VictoryPoints = new Dictionary<string, int>();
                foreach (var prop in vpElement.EnumerateObject())
                    message.VictoryPoints[prop.Name] = prop.Value.GetInt32();
            }

            var model = await gsm.HandleDeclareWinnerAsync(message);
            return (model, new DeclareWinnerRecord(model, message));
        }
    }
}
