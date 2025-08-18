using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using Catan3.Shared.Models;
using Catan3.Shared.Utility;

namespace Catan3.Utility
{
    /// <summary>
    /// Manages recording of game actions from a specific starting GameModel state.
    /// Creates complete .catan_test files containing both the initial GameModel and ActionStack.
    /// </summary>
    public class GameRecorder
    {
        private readonly GameModel _initialGameModel;
        private readonly List<object> _recordedActions;
        private readonly string _outputPath;
        private bool _isRecording;

        /// <summary>
        /// Creates a new recording session starting from the provided GameModel.
        /// </summary>
        /// <param name="initialGameModel">The GameModel to use as the starting state for the recording</param>
        /// <param name="outputPath">Optional output path for the .catan_test file. If null, generates default path.</param>
        public GameRecorder(GameModel initialGameModel, string? outputPath = null)
        {
            // Create a deep copy of the GameModel via serialize/deserialize to ensure immutability
            var jsonString = SerializationHelper.JsonSerialize(initialGameModel);
            _initialGameModel = SerializationHelper.JsonDeserialize<GameModel>(jsonString)
                ?? throw new InvalidOperationException("Failed to create deep copy of GameModel");

            _recordedActions = new List<object>();
            _outputPath = outputPath ?? GenerateDefaultOutputPath();
            _isRecording = true;

            this.LogMessage($"🎬 Recording started from GameState: {_initialGameModel.GameState}");
            this.LogMessage($"📁 Recording will be saved to: {_outputPath}");
        }

        /// <summary>
        /// Records an action that occurred during the recording session.
        /// </summary>
        /// <param name="message">The message object containing the action information</param>
        public void RecordAction(object message)
        {
            if (!_isRecording)
            {
                this.LogMessage($"⚠️ Attempted to record action while recording is stopped: {message.GetType().Name}");
                return;
            }

            try
            {
                // Map message to test action format
                var action = MapMessageToAction(message);
                if (action != null)
                {
                    _recordedActions.Add(action);
                    this.LogMessage($"📝 Recorded: {message.GetType().Name}");
                }
                else
                {
                    this.LogMessage($"⚠️ Failed to map message type: {message.GetType().Name}");
                }
            }
            catch (Exception ex)
            {
                this.LogMessage($"❌ Error recording action {message.GetType().Name}: {ex.Message}");
                // Don't throw - continue recording other actions
            }
        }

        /// <summary>
        /// Maps different message types to the appropriate test action format.
        /// </summary>
        private object? MapMessageToAction(object message)
        {
            return message.GetType().Name switch
            {
                "DoAction" => MapDoActionMessage((dynamic)message),
                "BuildingUpgradeMessage" => MapBuildingUpgradeMessage((dynamic)message),
                "RoadPurchaseMessage" => MapRoadPurchaseMessage((dynamic)message),
                "MoveRobberMessage" => MapMoveRobberMessage((dynamic)message),
                "RollMessage" => MapRollMessage((dynamic)message),
                "PurchaseMessage" => MapPurchaseMessage((dynamic)message),
                "SetPlayerOrderMessage" => MapSetPlayerOrderMessage((dynamic)message),
                "GoFirstMessage" => MapGoFirstMessage((dynamic)message),
                "PickSupplementalPlayersMessage" => MapPickSupplementalPlayersMessage((dynamic)message),
                _ => new { type = "Unknown", messageType = message.GetType().Name, timestamp = DateTime.UtcNow }
            };
        }

        private object MapDoActionMessage(dynamic message)
        {
            return new
            {
                type = GetActionTypeFromGameAction(message.Action),
                parameters = new Dictionary<string, object> { { "gameAction", message.Action.ToString() } },
                timestamp = DateTime.UtcNow
            };
        }

        private object MapBuildingUpgradeMessage(dynamic message)
        {
            return new
            {
                type = "PlaceBuilding", // or "UpgradeBuilding" based on context
                parameters = new Dictionary<string, object> { { "automationId", message.BuildingKey.GetAutomationId() } },
                timestamp = DateTime.UtcNow
            };
        }

        private object MapRoadPurchaseMessage(dynamic message)
        {
            return new
            {
                type = "PlaceRoad",
                parameters = new Dictionary<string, object> { { "automationId", message.RoadKey.GetAutomationId() } },
                timestamp = DateTime.UtcNow
            };
        }

        private object MapMoveRobberMessage(dynamic message)
        {
            return new
            {
                type = "MoveRobber",
                parameters = new Dictionary<string, object> 
                { 
                    { "automationId", $"Tile-{message.Coordinates}" },
                    { "targetPlayerId", message.TargetPlayerId ?? string.Empty }
                },
                timestamp = DateTime.UtcNow
            };
        }

        private object MapRollMessage(dynamic message)
        {
            return new
            {
                type = "RollDice",
                parameters = new Dictionary<string, object> { { "automationId", $"Roll-{(int)message.Roll.NormalRoll}" } },
                timestamp = DateTime.UtcNow
            };
        }

        private object MapPurchaseMessage(dynamic message)
        {
            return new
            {
                type = GetPurchaseActionType(message.Entitlement),
                parameters = (Dictionary<string, object>?)null,
                timestamp = DateTime.UtcNow
            };
        }

        private object MapSetPlayerOrderMessage(dynamic message)
        {
            return new
            {
                type = "SetPlayerOrder",
                parameters = new Dictionary<string, object> { { "playerIds", message.PlayerIds.ToArray() } },
                timestamp = DateTime.UtcNow
            };
        }

        private object MapGoFirstMessage(dynamic message)
        {
            return new
            {
                type = "GoFirst",
                parameters = new Dictionary<string, object> { { "playerId", message.PlayerId } },
                timestamp = DateTime.UtcNow
            };
        }

        private object MapPickSupplementalPlayersMessage(dynamic message)
        {
            return new
            {
                type = "SelectSupplementalPlayers",
                parameters = new Dictionary<string, object> { { "playerIds", message.PlayerIds.ToArray() } },
                timestamp = DateTime.UtcNow
            };
        }

        private static string GetActionTypeFromGameAction(dynamic action)
        {
            return action.ToString() switch
            {
                "Shuffle" => "ShuffleGame",
                "Undo" => "UndoAction",
                "Redo" => "RedoAction", 
                "Next" => "NextState",
                _ => $"GameAction{action}"
            };
        }

        private static string GetPurchaseActionType(dynamic entitlement)
        {
            return entitlement.ToString() switch
            {
                "Road" => "PurchaseRoad",
                "Settlement" => "PurchaseSettlement",
                "City" => "PurchaseCity",
                "DevelopmentCard" => "PurchaseDevelopmentCard",
                _ => $"Purchase{entitlement}"
            };
        }

        /// <summary>
        /// Ends the recording session and saves the complete .catan_test file.
        /// </summary>
        /// <returns>The file path where the .catan_test file was saved</returns>
        public string EndRecording()
        {
            if (!_isRecording)
            {
                throw new InvalidOperationException("Recording has already ended.");
            }

            try
            {
                // Create the .catan_test file structure
                var catanTestData = new
                {
                    GameModel = _initialGameModel,
                    ActionStack = _recordedActions.ToArray()
                };

                // Serialize with pretty formatting
                var jsonContent = JsonSerializer.Serialize(catanTestData, new JsonSerializerOptions 
                { 
                    WriteIndented = true,
                    Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
                });

                // Ensure output directory exists
                var directory = Path.GetDirectoryName(_outputPath);
                if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                // Save the file
                File.WriteAllText(_outputPath, jsonContent);

                var actionCount = _recordedActions.Count;
                _isRecording = false;

                this.LogMessage($"🎬 Recording ended - saved {actionCount} actions to {_outputPath}");
                return _outputPath;
            }
            catch (Exception ex)
            {
                this.LogMessage($"❌ Failed to save recording: {ex.Message}");
                throw new InvalidOperationException($"Failed to save recording to {_outputPath}: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// Gets the number of actions recorded so far.
        /// </summary>
        public int ActionCount => _recordedActions.Count;

        /// <summary>
        /// Gets whether the recording is currently active.
        /// </summary>
        public bool IsRecording => _isRecording;

        /// <summary>
        /// Gets the output path where the recording will be saved.
        /// </summary>
        public string OutputPath => _outputPath;

        /// <summary>
        /// Gets the initial GameModel that was used to start the recording.
        /// </summary>
        public GameModel InitialGameModel => _initialGameModel;

        /// <summary>
        /// Generates a default output path with timestamp for the recording.
        /// </summary>
        private static string GenerateDefaultOutputPath()
        {
            var timestamp = DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss");
            var fileName = $"recorded-session_{timestamp}.catan_test";
            return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Desktop), fileName);
        }

        /// <summary>
        /// Logs a message with recording context.
        /// </summary>
        private void LogMessage(string message)
        {
            System.Diagnostics.Debug.WriteLine($"GameRecorder: {message}");
        }
    }
}