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
        private readonly ILogger<AsyncCommandProcessor> _logger;

        public AsyncCommandProcessor(
            SignalRNotificationService signalRNotification,
            IServiceScopeFactory scopeFactory,
            ILogger<AsyncCommandProcessor> logger)
        {
            _signalRNotification = signalRNotification;
            _scopeFactory = scopeFactory;
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

                _logger.LogEvent("Process Command", $"Processing command {commandId} for game {gameId}");

                // Execute game logic using the provided GameStateMachine function
                var (gameModel, stateMachine) = await ExecuteGameLogicAsync(request, getGameStateMachine);
                gameStateMachine = stateMachine;
                gameId = gameModel.GameId; // Ensure we have the correct gameId

                // Parallel operations: Notify clients + command completion + persistence
                var tasks = new List<Task>
                {
                    _signalRNotification.NotifyAsync(gameId, gameModel),
                    _signalRNotification.NotifyCommandCompletedAsync(gameId, commandId, true, "Command executed successfully"),
                    SaveGameToDatabaseAsync(gameStateMachine, gameModel)
                };

                await Task.WhenAll(tasks);

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
        private async Task<(GameModel gameModel, GameStateMachine stateMachine)> ExecuteGameLogicAsync(JsonElement request, Func<string, GameStateMachine> getGameStateMachine)
        {
            return await Task.Run(async () =>
            {
                var gameId = request.RequireString("gameId", "request");
                var playerId = request.RequireString("playerId", "request");
                var messageType = request.RequireString("messageType", "request");
                var messageData = request.RequireProperty("messageData", "request");

                _logger.LogDebug("Processing message type: {MessageType} for game {GameId}", messageType, gameId);

                var gameStateMachine = getGameStateMachine(gameId);

                GameModel? updatedGameModel = messageType switch
                {
                    "UndoMessage" => await ProcessUndoMessageAsync(messageData, gameStateMachine),
                    "RedoMessage" => await ProcessRedoMessageAsync(messageData, gameStateMachine),
                    "NextMessage" => await ProcessNextMessageAsync(messageData, gameStateMachine),
                    "PurchaseMessage" => await ProcessPurchaseMessageAsync(messageData, gameStateMachine),
                    "RoadPurchaseMessage" => await ProcessRoadPurchaseAsync(messageData, gameStateMachine),
                    "BuildingUpgradeMessage" => await ProcessBuildingUpgradeAsync(messageData, gameStateMachine),
                    "MoveRobberMessage" => await ProcessMoveRobberAsync(messageData, gameStateMachine),
                    "RollMessage" => await ProcessRollAsync(messageData, gameStateMachine),
                    "SetPlayerOrderMessage" => await ProcessSetPlayerOrderAsync(messageData, gameStateMachine),
                    "BalanceBoardMessage" => await ProcessBalanceBoardAsync(messageData, gameStateMachine),
                    "GoFirstMessage" => await ProcessGoFirstAsync(messageData, gameStateMachine),
                    "ShuffleMessage" => await ProcessShuffleMessageAsync(messageData, gameStateMachine),
                    "ParticipatingInSupplementalMessage" => await ProcessParticipatingInSupplementalAsync(messageData, gameStateMachine),
                    "SwapTileResourcesMessage" => await ProcessSwapTileResourcesAsync(messageData, gameStateMachine),
                    "DeclareWinnerMessage" => await ProcessDeclareWinnerAsync(messageData, gameStateMachine),
                    _ => throw new JsonException($"Unknown message type: '{messageType}'. " +
                        $"Valid types: UndoMessage, RedoMessage, NextMessage, PurchaseMessage, " +
                        $"RoadPurchaseMessage, BuildingUpgradeMessage, MoveRobberMessage, RollMessage, " +
                        $"SetPlayerOrderMessage, BalanceBoardMessage, GoFirstMessage, ShuffleMessage, " +
                        $"ParticipatingInSupplementalMessage, SwapTileResourcesMessage, DeclareWinnerMessage")
                };

                var result = updatedGameModel ?? throw new InvalidOperationException("Game action failed to return updated model");
                return (result, gameStateMachine);
            });
        }

        // ============================================================================
        // Individual message processing methods
        // All use RequireXxx extension methods for descriptive error messages
        // ============================================================================

        private async Task<GameModel> ProcessUndoMessageAsync(JsonElement messageData, GameStateMachine gameStateMachine)
        {
            return await gameStateMachine.HandleUndoAsync(new UndoMessage());
        }

        private async Task<GameModel> ProcessRedoMessageAsync(JsonElement messageData, GameStateMachine gameStateMachine)
        {
            return await gameStateMachine.HandleRedoAsync(new RedoMessage());
        }

        private async Task<GameModel> ProcessNextMessageAsync(JsonElement messageData, GameStateMachine gameStateMachine)
        {
            return await gameStateMachine.HandleNextAsync(new NextMessage());
        }

        private async Task<GameModel> ProcessPurchaseMessageAsync(JsonElement messageData, GameStateMachine gameStateMachine)
        {
            var entitlement = messageData.RequireEnum<Entitlement>("entitlement", "PurchaseMessage");
            return await gameStateMachine.HandlePurchaseAsync(new PurchaseMessage(entitlement));
        }

        private async Task<GameModel> ProcessRoadPurchaseAsync(JsonElement messageData, GameStateMachine gameStateMachine)
        {
            var roadKeyData = messageData.RequireProperty("roadKey", "RoadPurchaseMessage");
            var tileKey = roadKeyData.RequireHexCoordinates("tileKey", "roadKey");
            var hexSide = roadKeyData.RequireEnum<HexSide>("hexSide", "roadKey");

            var roadKey = new RoadKey(tileKey, hexSide);
            return await gameStateMachine.HandleRoadPurchaseAsync(new RoadPurchaseMessage(roadKey));
        }

        private async Task<GameModel> ProcessBuildingUpgradeAsync(JsonElement messageData, GameStateMachine gameStateMachine)
        {
            var buildingKeyData = messageData.RequireProperty("buildingKey", "BuildingUpgradeMessage");
            var hexCoordinates = buildingKeyData.RequireHexCoordinates("hexCoordinates", "buildingKey");
            var position = buildingKeyData.RequireEnum<HexPosition>("position", "buildingKey");

            var buildingKey = new BuildingKey(hexCoordinates, position);
            return await gameStateMachine.HandleBuildingUpgradeAsync(new BuildingUpgradeMessage(buildingKey));
        }

        private async Task<GameModel> ProcessMoveRobberAsync(JsonElement messageData, GameStateMachine gameStateMachine)
        {
            var coordinates = messageData.RequireHexCoordinates("coordinates", "MoveRobberMessage");
            var targetPlayerId = messageData.OptionalString("targetPlayerId");

            return await gameStateMachine.HandleMoveRobberAsync(new MoveRobberMessage(coordinates, targetPlayerId));
        }

        private async Task<GameModel> ProcessRollAsync(JsonElement messageData, GameStateMachine gameStateMachine)
        {
            var rollData = messageData.RequireProperty("roll", "RollMessage");
            var normalRoll = rollData.RequireEnum<ValidCatanRoll>("normalRoll", "roll");

            var specialDice = SpecialDice.None;
            var specialStr = rollData.OptionalString("specialDice");
            if (!string.IsNullOrEmpty(specialStr))
            {
                Enum.TryParse<SpecialDice>(specialStr, out specialDice);
            }

            // Calculate individual dice rolls that sum to the normal roll
            int totalRoll = (int)normalRoll;
            int redRoll = totalRoll / 2;
            int whiteRoll = totalRoll - redRoll;

            var roll = new TurnRollModel(redRoll, whiteRoll) { SpecialDice = specialDice };
            return await gameStateMachine.HandleRollAsync(new RollMessage(roll));
        }

        private async Task<GameModel> ProcessSetPlayerOrderAsync(JsonElement messageData, GameStateMachine gameStateMachine)
        {
            var playerIdsElement = messageData.RequireProperty("playerIds", "SetPlayerOrderMessage");
            var playerIds = playerIdsElement.EnumerateArray()
                .Select(element => element.GetString())
                .Where(id => !string.IsNullOrEmpty(id))
                .Cast<string>()
                .ToList();

            if (playerIds.Count == 0)
            {
                throw new JsonException("SetPlayerOrderMessage requires at least one player ID in 'playerIds' array");
            }

            return await gameStateMachine.HandleSetPlayerOrderAsync(new SetPlayerOrderMessage(playerIds));
        }

        private async Task<GameModel> ProcessBalanceBoardAsync(JsonElement messageData, GameStateMachine gameStateMachine)
        {
            return await gameStateMachine.HandleBalanceBoardAsync(new BalanceBoardMessage());
        }

        private async Task<GameModel> ProcessGoFirstAsync(JsonElement messageData, GameStateMachine gameStateMachine)
        {
            var playerId = messageData.RequireString("playerId", "GoFirstMessage");
            return await gameStateMachine.HandleGoFirstAsync(new GoFirstMessage(playerId));
        }

        private async Task<GameModel> ProcessShuffleMessageAsync(JsonElement messageData, GameStateMachine gameStateMachine)
        {
            return await gameStateMachine.HandleShuffleAsync(new ShuffleMessage());
        }

        private async Task<GameModel> ProcessParticipatingInSupplementalAsync(JsonElement messageData, GameStateMachine gameStateMachine)
        {
            var playerId = messageData.RequireString("playerId", "ParticipatingInSupplementalMessage");
            var participating = messageData.RequireBool("participating", "ParticipatingInSupplementalMessage");

            return await gameStateMachine.HandleParticipatingInSupplementalAsync(
                new ParticipatingInSupplementalMessage(playerId, participating));
        }

        private async Task<GameModel> ProcessSwapTileResourcesAsync(JsonElement messageData, GameStateMachine gameStateMachine)
        {
            var sourceCoords = messageData.RequireHexCoordinates("sourceTileCoordinates", "SwapTileResourcesMessage");
            var destCoords = messageData.RequireHexCoordinates("destinationTileCoordinates", "SwapTileResourcesMessage");
            var sourceResource = messageData.RequireEnum<ResourceType>("sourceCurrentResource", "SwapTileResourcesMessage");
            var destResource = messageData.RequireEnum<ResourceType>("destinationCurrentResource", "SwapTileResourcesMessage");

            return await gameStateMachine.HandleSwapResourcesAsync(
                new SwapTileResources(sourceCoords, destCoords, sourceResource, destResource));
        }

        private async Task<GameModel> ProcessDeclareWinnerAsync(JsonElement messageData, GameStateMachine gameStateMachine)
        {
            var winnerId = messageData.RequireString("winnerId", "DeclareWinnerMessage");

            var message = new DeclareWinnerMessage { WinnerId = winnerId };

            // Parse optional victory points dictionary
            if (messageData.TryGetProperty("victoryPoints", out var vpElement))
            {
                message.VictoryPoints = new Dictionary<string, int>();
                foreach (var prop in vpElement.EnumerateObject())
                {
                    message.VictoryPoints[prop.Name] = prop.Value.GetInt32();
                }
            }

            return await gameStateMachine.HandleDeclareWinnerAsync(message);
        }
    }
}
