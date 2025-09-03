using System.Text.Json;
using Catan3.Shared.Models;
using Catan3.Shared.GameLogic;
using Catan3.GameService.Services;
using Catan3.GameService.Controllers;
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
        private readonly ILogger<AsyncCommandProcessor> _logger;

        public AsyncCommandProcessor(
            SignalRNotificationService signalRNotification,
            ILogger<AsyncCommandProcessor> logger)
        {
            _signalRNotification = signalRNotification;
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
            try
            {
                // Extract gameId for error reporting
                if (request.TryGetProperty("gameId", out var gameIdElement))
                {
                    gameId = gameIdElement.GetString();
                }

                _logger.LogEvent("Process Command", $"Processing command {commandId} for game {gameId}");

                // Execute game logic using the provided GameStateMachine function
                var gameModel = await ExecuteGameLogicAsync(request, getGameStateMachine);
                gameId = gameModel.GameId; // Ensure we have the correct gameId

                // Parallel operations: Notify clients + command completion
                var tasks = new List<Task>
                {
                    _signalRNotification.NotifyAsync(gameId, gameModel),
                    _signalRNotification.NotifyCommandCompletedAsync(gameId, commandId, true, "Command executed successfully")
                };

                // TODO: Add persistence task when implemented
                // tasks.Add(_persistenceService.SaveAsync(gameModel));

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
        /// Executes the game logic using the provided GameStateMachine function
        /// </summary>
        /// <param name="request">The game command request</param>
        /// <param name="getGameStateMachine">Function to get GameStateMachine by gameId</param>
        /// <returns>The updated game model</returns>
        private async Task<GameModel> ExecuteGameLogicAsync(JsonElement request, Func<string, GameStateMachine> getGameStateMachine)
        {
            // Execute synchronously but wrap in Task for async interface
            return await Task.Run(() =>
            {
                var gameId = request.GetProperty("gameId").GetString() ?? 
                    throw new ArgumentException("gameId is required");
                var playerId = request.GetProperty("playerId").GetString() ?? 
                    throw new ArgumentException("playerId is required");
                var messageType = request.GetProperty("messageType").GetString() ?? 
                    throw new ArgumentException("messageType is required");
                var messageData = request.GetProperty("messageData");

                _logger.LogDebug("Processing message type: {MessageType} for game {GameId}", messageType, gameId);

                // Get the GameStateMachine directly from the controller
                var gameStateMachine = getGameStateMachine(gameId);

                // Execute the action directly on the GameStateMachine
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
                    _ => throw new ArgumentException($"Unknown message type: {messageType}")
                };

                return updatedGameModel ?? throw new InvalidOperationException("Game action failed to return updated model");
            });
        }

        // Individual message processing methods
        private GameModel ProcessUndoMessage(JsonElement messageData, GameStateMachine gameStateMachine)
        {
            var message = new UndoMessage();
            return gameStateMachine.HandleUndoAsync(message).Result;
        }

        private GameModel ProcessRedoMessage(JsonElement messageData, GameStateMachine gameStateMachine)
        {
            var message = new RedoMessage();
            return gameStateMachine.HandleRedoAsync(message).Result;
        }

        private GameModel ProcessNextMessage(JsonElement messageData, GameStateMachine gameStateMachine)
        {
            var message = new NextMessage();
            return gameStateMachine.HandleNextAsync(message).Result;
        }

        private GameModel ProcessPurchaseMessage(JsonElement messageData, GameStateMachine gameStateMachine)
        {
            var entitlementStr = messageData.GetProperty("entitlement").GetString();
            if (Enum.TryParse<Entitlement>(entitlementStr, out var entitlement))
            {
                var message = new PurchaseMessage(entitlement);
                return gameStateMachine.HandlePurchaseAsync(message).Result;
            }
            throw new ArgumentException($"Invalid entitlement: {entitlementStr}");
        }

        private GameModel ProcessRoadPurchase(JsonElement messageData, GameStateMachine gameStateMachine)
        {
            var roadKeyData = messageData.GetProperty("roadKey");
            var tileKeyData = roadKeyData.GetProperty("tileKey");
            var sideStr = roadKeyData.GetProperty("side").GetString();

            var q = tileKeyData.GetProperty("q").GetInt32();
            var r = tileKeyData.GetProperty("r").GetInt32();
            var s = tileKeyData.GetProperty("s").GetInt32();

            if (!Enum.TryParse<Catan3.Shared.Models.HexSide>(sideStr, out var side))
            {
                throw new ArgumentException($"Invalid side: {sideStr}");
            }

            var tileKey = new HexCoordinates(q, r, s);
            var roadKey = new RoadKey { TileKey = tileKey, HexSide = side };
            var message = new RoadPurchaseMessage(roadKey);
            return gameStateMachine.HandleRoadPurchaseAsync(message).Result;
        }

        private GameModel ProcessBuildingUpgrade(JsonElement messageData, GameStateMachine gameStateMachine)
        {
            var buildingKeyData = messageData.GetProperty("buildingKey");
            var hexCoordinatesData = buildingKeyData.GetProperty("hexCoordinates");
            var positionStr = buildingKeyData.GetProperty("position").GetString();

            var q = hexCoordinatesData.GetProperty("q").GetInt32();
            var r = hexCoordinatesData.GetProperty("r").GetInt32();
            var s = hexCoordinatesData.GetProperty("s").GetInt32();

            if (!Enum.TryParse<Catan3.Shared.Models.HexPosition>(positionStr, out var position))
            {
                throw new ArgumentException($"Invalid hex position: {positionStr}");
            }

            var hexCoordinates = new HexCoordinates(q, r, s);
            var buildingKey = new BuildingKey(hexCoordinates, position);
            var message = new BuildingUpgradeMessage(buildingKey);
            return gameStateMachine.HandleBuildingUpgradeAsync(message).Result;
        }

        private GameModel ProcessMoveRobber(JsonElement messageData, GameStateMachine gameStateMachine)
        {
            var coordinatesData = messageData.GetProperty("coordinates");
            var q = coordinatesData.GetProperty("q").GetInt32();
            var r = coordinatesData.GetProperty("r").GetInt32();
            var s = coordinatesData.GetProperty("s").GetInt32();

            string? targetPlayerId = null;
            if (messageData.TryGetProperty("targetPlayerId", out var targetElement))
            {
                targetPlayerId = targetElement.GetString();
            }

            var coordinates = new HexCoordinates(q, r, s);
            var message = new MoveRobberMessage(coordinates, targetPlayerId);
            return gameStateMachine.HandleMoveRobberAsync(message).Result;
        }

        private GameModel ProcessRoll(JsonElement messageData, GameStateMachine gameStateMachine)
        {
            var rollData = messageData.GetProperty("roll");
            var normalRollStr = rollData.GetProperty("normalRoll").GetString();
            
            if (!Enum.TryParse<ValidCatanRoll>(normalRollStr, out var normalRoll))
            {
                throw new ArgumentException($"Invalid roll: {normalRollStr}");
            }

            var specialDice = SpecialDice.None;
            if (rollData.TryGetProperty("specialDice", out var specialElement))
            {
                var specialStr = specialElement.GetString();
                if (!string.IsNullOrEmpty(specialStr))
                {
                    Enum.TryParse<SpecialDice>(specialStr, out specialDice);
                }
            }

            // Calculate individual dice rolls that sum to the normal roll
            int totalRoll = (int)normalRoll;
            int redRoll = totalRoll / 2;
            int whiteRoll = totalRoll - redRoll;

            var roll = new TurnRollModel(redRoll, whiteRoll)
            {
                SpecialDice = specialDice
            };

            var message = new RollMessage(roll);
            return gameStateMachine.HandleRollAsync(message).Result;
        }

        private GameModel ProcessSetPlayerOrder(JsonElement messageData, GameStateMachine gameStateMachine)
        {
            var playerIds = messageData.GetProperty("playerIds").EnumerateArray()
                .Select(element => element.GetString())
                .Where(id => !string.IsNullOrEmpty(id))
                .Cast<string>()
                .ToList();

            var message = new SetPlayerOrderMessage(playerIds);
            return gameStateMachine.HandleSetPlayerOrderAsync(message).Result;
        }


        private GameModel ProcessBalanceBoard(JsonElement messageData, GameStateMachine gameStateMachine)
        {
            var message = new BalanceBoardMessage();
            return gameStateMachine.HandleBalanceBoardAsync(message).Result;
        }

        private GameModel ProcessGoFirst(JsonElement messageData, GameStateMachine gameStateMachine)
        {
            var playerId = messageData.GetProperty("playerId").GetString()
                ?? throw new ArgumentException("Missing playerId");

            var message = new GoFirstMessage(playerId);
            return gameStateMachine.HandleGoFirstAsync(message).Result;
        }
    }
}