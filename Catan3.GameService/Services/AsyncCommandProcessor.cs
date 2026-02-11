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
            Console.WriteLine($"[SAVE-ASYNC] SaveGameToDatabaseAsync called for game {gameModel.GameId}");
            try
            {
                // Create a new scope for database operations (since we're a singleton)
                using var scope = _scopeFactory.CreateScope();
                var gamePersistence = scope.ServiceProvider.GetRequiredService<IGamePersistence>();

                // Get the full serializable log (preserves undo/redo stacks)
                var serializableLog = gameStateMachine.GetSerializableLog();
                var json = JsonHelper.Serialize(serializableLog);
                var compressed = JsonHelper.Compress(json);

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
                _logger.LogEvent("Database Save", $"Game saved to database: {gameModel.GameId}");
            }
            catch (Exception ex)
            {
                _logger.LogEvent("Database Save Error", $"Failed to save game to database: {ex.Message}", LogLevel.Error);
                // Don't throw - database save failure shouldn't break the game operation
            }
        }

        /// <summary>
        /// Executes the game logic using the provided GameStateMachine function
        /// </summary>
        private async Task<(GameModel gameModel, GameStateMachine stateMachine)> ExecuteGameLogicAsync(JsonElement request, Func<string, GameStateMachine> getGameStateMachine)
        {
            return await Task.Run(() =>
            {
                var gameId = request.RequireString("gameId", "request");
                var playerId = request.RequireString("playerId", "request");
                var messageType = request.RequireString("messageType", "request");
                var messageData = request.RequireProperty("messageData", "request");

                _logger.LogDebug("Processing message type: {MessageType} for game {GameId}", messageType, gameId);

                var gameStateMachine = getGameStateMachine(gameId);

                GameModel? updatedGameModel = messageType switch
                {
                    "UndoMessage" => ProcessUndoMessage(messageData, gameStateMachine),
                    "RedoMessage" => ProcessRedoMessage(messageData, gameStateMachine),
                    "NextMessage" => ProcessNextMessage(messageData, gameStateMachine),
                    "PurchaseMessage" => ProcessPurchaseMessage(messageData, gameStateMachine),
                    "RoadPurchaseMessage" => ProcessRoadPurchase(messageData, gameStateMachine),
                    "BuildingUpgradeMessage" => ProcessBuildingUpgrade(messageData, gameStateMachine),
                    "MoveRobberMessage" => ProcessMoveRobber(messageData, gameStateMachine),
                    "RollMessage" => ProcessRoll(messageData, gameStateMachine),
                    "SetPlayerOrderMessage" => ProcessSetPlayerOrder(messageData, gameStateMachine),
                    "BalanceBoardMessage" => ProcessBalanceBoard(messageData, gameStateMachine),
                    "GoFirstMessage" => ProcessGoFirst(messageData, gameStateMachine),
                    "ShuffleMessage" => ProcessShuffleMessage(messageData, gameStateMachine),
                    "ParticipatingInSupplementalMessage" => ProcessParticipatingInSupplemental(messageData, gameStateMachine),
                    "SwapTileResourcesMessage" => ProcessSwapTileResources(messageData, gameStateMachine),
                    "DeclareWinnerMessage" => ProcessDeclareWinner(messageData, gameStateMachine),
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

        private GameModel ProcessUndoMessage(JsonElement messageData, GameStateMachine gameStateMachine)
        {
            return gameStateMachine.HandleUndoAsync(new UndoMessage()).Result;
        }

        private GameModel ProcessRedoMessage(JsonElement messageData, GameStateMachine gameStateMachine)
        {
            return gameStateMachine.HandleRedoAsync(new RedoMessage()).Result;
        }

        private GameModel ProcessNextMessage(JsonElement messageData, GameStateMachine gameStateMachine)
        {
            return gameStateMachine.HandleNextAsync(new NextMessage()).Result;
        }

        private GameModel ProcessPurchaseMessage(JsonElement messageData, GameStateMachine gameStateMachine)
        {
            var entitlement = messageData.RequireEnum<Entitlement>("entitlement", "PurchaseMessage");
            return gameStateMachine.HandlePurchaseAsync(new PurchaseMessage(entitlement)).Result;
        }

        private GameModel ProcessRoadPurchase(JsonElement messageData, GameStateMachine gameStateMachine)
        {
            var roadKeyData = messageData.RequireProperty("roadKey", "RoadPurchaseMessage");
            var tileKey = roadKeyData.RequireHexCoordinates("tileKey", "roadKey");
            var hexSide = roadKeyData.RequireEnum<HexSide>("hexSide", "roadKey");

            var roadKey = new RoadKey(tileKey, hexSide);
            return gameStateMachine.HandleRoadPurchaseAsync(new RoadPurchaseMessage(roadKey)).Result;
        }

        private GameModel ProcessBuildingUpgrade(JsonElement messageData, GameStateMachine gameStateMachine)
        {
            var buildingKeyData = messageData.RequireProperty("buildingKey", "BuildingUpgradeMessage");
            var hexCoordinates = buildingKeyData.RequireHexCoordinates("hexCoordinates", "buildingKey");
            var position = buildingKeyData.RequireEnum<HexPosition>("position", "buildingKey");

            var buildingKey = new BuildingKey(hexCoordinates, position);
            return gameStateMachine.HandleBuildingUpgradeAsync(new BuildingUpgradeMessage(buildingKey)).Result;
        }

        private GameModel ProcessMoveRobber(JsonElement messageData, GameStateMachine gameStateMachine)
        {
            var coordinates = messageData.RequireHexCoordinates("coordinates", "MoveRobberMessage");
            var targetPlayerId = messageData.OptionalString("targetPlayerId");

            return gameStateMachine.HandleMoveRobberAsync(new MoveRobberMessage(coordinates, targetPlayerId)).Result;
        }

        private GameModel ProcessRoll(JsonElement messageData, GameStateMachine gameStateMachine)
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
            return gameStateMachine.HandleRollAsync(new RollMessage(roll)).Result;
        }

        private GameModel ProcessSetPlayerOrder(JsonElement messageData, GameStateMachine gameStateMachine)
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

            return gameStateMachine.HandleSetPlayerOrderAsync(new SetPlayerOrderMessage(playerIds)).Result;
        }

        private GameModel ProcessBalanceBoard(JsonElement messageData, GameStateMachine gameStateMachine)
        {
            return gameStateMachine.HandleBalanceBoardAsync(new BalanceBoardMessage()).Result;
        }

        private GameModel ProcessGoFirst(JsonElement messageData, GameStateMachine gameStateMachine)
        {
            var playerId = messageData.RequireString("playerId", "GoFirstMessage");
            return gameStateMachine.HandleGoFirstAsync(new GoFirstMessage(playerId)).Result;
        }

        private GameModel ProcessShuffleMessage(JsonElement messageData, GameStateMachine gameStateMachine)
        {
            return gameStateMachine.HandleShuffleAsync(new ShuffleMessage()).Result;
        }

        private GameModel ProcessParticipatingInSupplemental(JsonElement messageData, GameStateMachine gameStateMachine)
        {
            var playerId = messageData.RequireString("playerId", "ParticipatingInSupplementalMessage");
            var participating = messageData.RequireBool("participating", "ParticipatingInSupplementalMessage");

            return gameStateMachine.HandleParticipatingInSupplementalAsync(
                new ParticipatingInSupplementalMessage(playerId, participating)).Result;
        }

        private GameModel ProcessSwapTileResources(JsonElement messageData, GameStateMachine gameStateMachine)
        {
            var sourceCoords = messageData.RequireHexCoordinates("sourceTileCoordinates", "SwapTileResourcesMessage");
            var destCoords = messageData.RequireHexCoordinates("destinationTileCoordinates", "SwapTileResourcesMessage");
            var sourceResource = messageData.RequireEnum<ResourceType>("sourceCurrentResource", "SwapTileResourcesMessage");
            var destResource = messageData.RequireEnum<ResourceType>("destinationCurrentResource", "SwapTileResourcesMessage");

            return gameStateMachine.HandleSwapResourcesAsync(
                new SwapTileResources(sourceCoords, destCoords, sourceResource, destResource)).Result;
        }

        private GameModel ProcessDeclareWinner(JsonElement messageData, GameStateMachine gameStateMachine)
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

            return gameStateMachine.HandleDeclareWinnerAsync(message).Result;
        }
    }
}
