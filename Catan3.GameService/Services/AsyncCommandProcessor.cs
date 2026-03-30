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
                var messageType = request.OptionalString("messageType") ?? "unknown";
                var totalSw = System.Diagnostics.Stopwatch.StartNew();

                // Execute game logic
                var logicSw = System.Diagnostics.Stopwatch.StartNew();
                var (gameModel, stateMachine, recordedMessage) = await ExecuteGameLogicAsync(request, getGameStateMachine);
                logicSw.Stop();
                gameStateMachine = stateMachine;
                gameId = gameModel.GameId;

                // Fast path: notify clients immediately
                var notifySw = System.Diagnostics.Stopwatch.StartNew();
                var tasks = new List<Task>
                {
                    _signalRNotification.NotifyAsync(gameId, gameModel),
                    _signalRNotification.NotifyCommandCompletedAsync(gameId, commandId, true, "Command executed successfully"),
                    TryRecordActionAsync(gameId, recordedMessage)
                };

                await Task.WhenAll(tasks);
                notifySw.Stop();
                totalSw.Stop();

                // Persistence is handled by Log.SaveAsync() inside GameStateMachine.LogGameModel()
                // The Log owns its own save lifecycle (coalesced, background).

                _logger.LogInformation(
                    "[PERF] {MessageType} game={GameId} logic={LogicMs}ms notify={NotifyMs}ms total={TotalMs}ms state={GameState}",
                    messageType, gameId, logicSw.ElapsedMilliseconds, notifySw.ElapsedMilliseconds, totalSw.ElapsedMilliseconds, gameModel.GameState);
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
