using Microsoft.AspNetCore.Mvc;
using System.Text.Json;
using Catan3.Shared.Models;
using Catan3.Shared.Utility;

namespace Catan3.GameService.Controllers
{
    [ApiController]
    [Route("api")]
    public class GameApiController : ControllerBase
    {
        private readonly GameStateMachine _gameStateMachine;
        private static readonly Dictionary<string, GameModel> _gameStates = new();
        private static readonly Dictionary<string, TaskCompletionSource<GameModel>> _pendingUpdates = new();
        private static int _currentVersion = 0;

        public GameApiController(GameStateMachine gameStateMachine)
        {
            _gameStateMachine = gameStateMachine;
        }

        [HttpGet("players/{gameId}")]
        public IActionResult GetPlayers(string gameId)
        {
            try
            {
                if (!_gameStates.TryGetValue(gameId, out var gameModel))
                {
                    return NotFound($"Game {gameId} not found");
                }

                var result = new
                {
                    gameId,
                    players = gameModel.Players.Select(p => new
                    {
                        id = p.Id,
                        name = p.Id, // Using Id as name for now
                        isCurrentPlayer = p.Id == gameModel.CurrentPlayerId
                    }).ToList()
                };

                return Ok(result);
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Error getting players: {ex.Message}");
            }
        }

        [HttpPost("game/action")]
        public IActionResult ExecuteGameAction([FromBody] JsonElement request)
        {
            try
            {
                var gameId = request.GetProperty("gameId").GetString();
                var playerId = request.GetProperty("playerId").GetString();
                var messageType = request.GetProperty("messageType").GetString();
                var messageData = request.GetProperty("messageData");

                if (string.IsNullOrEmpty(gameId) || string.IsNullOrEmpty(playerId) || string.IsNullOrEmpty(messageType))
                {
                    return BadRequest("Missing required fields: gameId, playerId, messageType");
                }

                // Process the action based on message type
                GameModel? updatedGameModel = null;
                string message = "";

                switch (messageType)
                {
                    case "DoAction":
                        updatedGameModel = ProcessDoAction(messageData);
                        message = "Action executed successfully";
                        break;
                    case "PurchaseMessage":
                        updatedGameModel = ProcessPurchaseMessage(messageData);
                        message = "Purchase executed successfully";
                        break;
                    case "RoadPurchaseMessage":
                        updatedGameModel = ProcessRoadPurchase(messageData);
                        message = "Road purchase executed successfully";
                        break;
                    case "BuildingUpgradeMessage":
                        updatedGameModel = ProcessBuildingUpgrade(messageData);
                        message = "Building upgrade executed successfully";
                        break;
                    case "MoveRobberMessage":
                        updatedGameModel = ProcessMoveRobber(messageData);
                        message = "Robber moved successfully";
                        break;
                    case "RollMessage":
                        updatedGameModel = ProcessRoll(messageData);
                        message = "Roll processed successfully";
                        break;
                    case "SetPlayerOrderMessage":
                        updatedGameModel = ProcessSetPlayerOrder(messageData);
                        message = "Player order set successfully";
                        break;
                    case "PlayersDoingSupplemental":
                        updatedGameModel = ProcessPlayersDoingSupplemental(messageData);
                        message = "Supplemental players set successfully";
                        break;
                    case "BalanceBoardMessage":
                        updatedGameModel = ProcessBalanceBoard(messageData);
                        message = "Board balanced successfully";
                        break;
                    case "GoFirstMessage":
                        updatedGameModel = ProcessGoFirst(messageData);
                        message = "Go first set successfully";
                        break;
                    default:
                        return BadRequest($"Unknown message type: {messageType}");
                }

                if (updatedGameModel != null)
                {
                    _gameStates[gameId] = updatedGameModel;
                    _currentVersion++;
                    NotifyPendingUpdates(gameId);
                }

                return Ok(new
                {
                    success = updatedGameModel != null,
                    gameStateVersion = _currentVersion,
                    message
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Error executing action: {ex.Message}");
            }
        }

        [HttpGet("gamestate/{gameId}")]
        public IActionResult GetGameState(string gameId)
        {
            try
            {
                if (!_gameStates.TryGetValue(gameId, out var gameModel))
                {
                    return NotFound($"Game {gameId} not found");
                }

                var result = CreateGameStateResponse(gameId, gameModel);
                return Ok(result);
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Error getting game state: {ex.Message}");
            }
        }

        [HttpGet("gamestate/{gameId}/listen")]
        public async Task<IActionResult> ListenForUpdates(string gameId, [FromQuery] string? playerId = null, [FromQuery] int version = 0)
        {
            try
            {
                // If client version is behind, return immediately
                if (version < _currentVersion && _gameStates.TryGetValue(gameId, out var currentGame))
                {
                    var result = CreateGameStateResponse(gameId, currentGame);
                    return Ok(result);
                }

                // Create a task that will complete when the game state changes
                var tcs = new TaskCompletionSource<GameModel>();
                var key = $"{gameId}_{playerId}_{Guid.NewGuid()}";
                _pendingUpdates[key] = tcs;

                // Set up timeout (15 minutes for local game scenarios where players might think for a while)
                var timeoutTask = Task.Delay(TimeSpan.FromMinutes(15));
                var completedTask = await Task.WhenAny(tcs.Task, timeoutTask);

                _pendingUpdates.Remove(key);

                if (completedTask == timeoutTask)
                {
                    // Timeout - return current state anyway (but this should be very rare with 15-minute timeout)
                    if (_gameStates.TryGetValue(gameId, out var timeoutGame))
                    {
                        var result = CreateGameStateResponse(gameId, timeoutGame);
                        return Ok(result);
                    }
                    return NotFound($"Game {gameId} not found");
                }
                else
                {
                    // Update received
                    var updatedGame = await tcs.Task;
                    var result = CreateGameStateResponse(gameId, updatedGame);
                    return Ok(result);
                }
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Error listening for updates: {ex.Message}");
            }
        }

        [HttpPost("game/register")]
        public IActionResult RegisterGame([FromBody] JsonElement request)
        {
            try
            {
                var gameId = request.GetProperty("gameId").GetString();
                if (string.IsNullOrEmpty(gameId))
                {
                    return BadRequest("Missing gameId");
                }

                // Create a test game using GameStateMachine
                var playerIds = new List<string> { "player1", "player2", "player3", "player4" };
                var newGameMessage = new NewGameMessage(GameType.Regular, playerIds);
                var gameModel = _gameStateMachine.HandleNewGame(newGameMessage);

                _gameStates[gameId] = gameModel;
                _currentVersion++;

                return Ok(new { success = true, message = "Game registered successfully" });
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Error registering game: {ex.Message}");
            }
        }

        [HttpPost("game/new")]
        public IActionResult NewGame([FromBody] JsonElement request)
        {
            try
            {
                var gameId = request.GetProperty("gameId").GetString();
                var gameTypeStr = request.GetProperty("gameType").GetString();
                var playerIdsElement = request.GetProperty("playerIds");

                if (string.IsNullOrEmpty(gameId) || string.IsNullOrEmpty(gameTypeStr))
                {
                    return BadRequest("Missing required fields: gameId, gameType");
                }

                if (!Enum.TryParse<GameType>(gameTypeStr, out var gameType))
                {
                    return BadRequest($"Invalid game type: {gameTypeStr}");
                }

                var playerIds = playerIdsElement.EnumerateArray()
                    .Select(element => element.GetString())
                    .Where(id => !string.IsNullOrEmpty(id))
                    .Cast<string>()
                    .ToList();

                var newGameMessage = new NewGameMessage(gameType, playerIds);
                var gameModel = _gameStateMachine.HandleNewGame(newGameMessage);

                _gameStates[gameId] = gameModel;
                _currentVersion++;
                NotifyPendingUpdates(gameId);

                return Ok(new
                {
                    success = true,
                    gameStateVersion = _currentVersion,
                    message = "New game created successfully"
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Error creating new game: {ex.Message}");
            }
        }

        [HttpPost("game/load")]
        public async Task<IActionResult> LoadGame([FromBody] JsonElement request)
        {
            try
            {
                var gameId = request.GetProperty("gameId").GetString();
                var filePath = request.GetProperty("filePath").GetString();

                if (string.IsNullOrEmpty(gameId) || string.IsNullOrEmpty(filePath))
                {
                    return BadRequest("Missing required fields: gameId, filePath");
                }

                var loadGameMessage = new LoadGameMessage(filePath);
                var gameModel = await _gameStateMachine.HandleLoadGame(loadGameMessage);

                _gameStates[gameId] = gameModel;
                _currentVersion++;
                NotifyPendingUpdates(gameId);

                return Ok(new
                {
                    success = true,
                    gameStateVersion = _currentVersion,
                    message = "Game loaded successfully"
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Error loading game: {ex.Message}");
            }
        }

        [HttpPost("game/persist")]
        public async Task<IActionResult> PersistGame([FromBody] JsonElement request)
        {
            try
            {
                var actionStr = request.GetProperty("action").GetString();
                var location = "";
                
                if (request.TryGetProperty("location", out var locationElement))
                {
                    location = locationElement.GetString() ?? "";
                }

                if (!Enum.TryParse<LocalPersistActions>(actionStr, out var action))
                {
                    return BadRequest($"Invalid persist action: {actionStr}");
                }

                var persistMessage = new PersistGameMessage(action, location);
                await _gameStateMachine.HandlePersistGame(persistMessage);

                return Ok(new
                {
                    success = true,
                    message = $"Game {action} completed successfully"
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Error persisting game: {ex.Message}");
            }
        }

        private GameModel ProcessDoAction(JsonElement messageData)
        {
            var actionStr = messageData.GetProperty("action").GetString();
            if (Enum.TryParse<GameAction>(actionStr, out var action))
            {
                var message = new DoAction(action);
                return _gameStateMachine.HandleDoAction(message);
            }
            throw new ArgumentException($"Invalid action: {actionStr}");
        }

        private GameModel ProcessPurchaseMessage(JsonElement messageData)
        {
            var entitlementStr = messageData.GetProperty("entitlement").GetString();
            if (Enum.TryParse<Entitlement>(entitlementStr, out var entitlement))
            {
                var message = new PurchaseMessage(entitlement);
                return _gameStateMachine.HandlePurchaseMessage(message);
            }
            throw new ArgumentException($"Invalid entitlement: {entitlementStr}");
        }

        private GameModel ProcessRoadPurchase(JsonElement messageData)
        {
            var roadKeyData = messageData.GetProperty("roadKey");
            var tileKeyData = roadKeyData.GetProperty("tileKey");
            var hexSideStr = roadKeyData.GetProperty("hexSide").GetString();

            var q = tileKeyData.GetProperty("q").GetInt32();
            var r = tileKeyData.GetProperty("r").GetInt32();
            var s = tileKeyData.GetProperty("s").GetInt32();

            if (!Enum.TryParse<Catan3.Shared.Models.HexSide>(hexSideStr, out var hexSide))
            {
                throw new ArgumentException($"Invalid hex side: {hexSideStr}");
            }

            var tileKey = new HexCoordinates(q, r, s);
            var roadKey = new RoadKey { TileKey = tileKey, HexSide = hexSide };
            var message = new RoadPurchaseMessage(roadKey);
            return _gameStateMachine.HandleRoadPurchase(message);
        }

        private GameModel ProcessBuildingUpgrade(JsonElement messageData)
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
            var buildingKey = new BuildingKey { HexCoordinates = hexCoordinates, Position = position };
            var message = new BuildingUpgradeMessage(buildingKey);
            return _gameStateMachine.HandleBuildingUpgrade(message);
        }

        private GameModel ProcessMoveRobber(JsonElement messageData)
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
            return _gameStateMachine.HandleMoveRobber(message);
        }

        private GameModel ProcessRoll(JsonElement messageData)
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
            return _gameStateMachine.HandleRoll(message);
        }

        private GameModel ProcessSetPlayerOrder(JsonElement messageData)
        {
            var playerIds = messageData.GetProperty("playerIds").EnumerateArray()
                .Select(element => element.GetString())
                .Where(id => !string.IsNullOrEmpty(id))
                .Cast<string>()
                .ToList();

            var message = new SetPlayerOrderMessage(playerIds);
            return _gameStateMachine.HandleSetPlayerOrder(message);
        }

        private GameModel ProcessPlayersDoingSupplemental(JsonElement messageData)
        {
            var playerIds = messageData.GetProperty("playerIds").EnumerateArray()
                .Select(element => element.GetString())
                .Where(id => !string.IsNullOrEmpty(id))
                .Cast<string>()
                .ToList();

            var message = new PlayersDoingSupplemental(playerIds);
            return _gameStateMachine.HandlePlayersDoingSupplemental(message);
        }

        private GameModel ProcessBalanceBoard(JsonElement messageData)
        {
            var message = new BalanceBoardMessage();
            return _gameStateMachine.HandleBalanceBoard(message);
        }

        private GameModel ProcessGoFirst(JsonElement messageData)
        {
            var playerId = messageData.GetProperty("playerId").GetString()
                ?? throw new ArgumentException("Missing playerId");

            var message = new GoFirstMessage(playerId);
            return _gameStateMachine.HandleGoFirst(message);
        }

        private object CreateGameStateResponse(string gameId, GameModel gameModel)
        {
            return new
            {
                gameId,
                currentPlayerId = gameModel.CurrentPlayerId,
                gameState = gameModel.GameState.ToString(),
                actionFlags = new
                {
                    nextEnabled = gameModel.ActionFlags.NextEnabled,
                    undoEnabled = gameModel.ActionFlags.UndoEnabled,
                    rollsEnabled = gameModel.ActionFlags.RollsEnabled
                },
                availableEntitlements = gameModel.EntitlementPurchaseModel.Select(e => new
                {
                    entitlement = e.Entitlement.ToString(),
                    enabled = e.Enabled
                }).ToArray(),
                version = _currentVersion,
                timestamp = DateTime.UtcNow.ToString("O")
            };
        }

        private void NotifyPendingUpdates(string gameId)
        {
            if (_gameStates.TryGetValue(gameId, out var gameModel))
            {
                var completedTasks = new List<string>();
                foreach (var kvp in _pendingUpdates)
                {
                    if (kvp.Key.StartsWith(gameId + "_"))
                    {
                        kvp.Value.SetResult(gameModel);
                        completedTasks.Add(kvp.Key);
                    }
                }

                foreach (var key in completedTasks)
                {
                    _pendingUpdates.Remove(key);
                }
            }
        }
    }
}