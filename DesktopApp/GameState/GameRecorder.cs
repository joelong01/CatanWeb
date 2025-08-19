using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using Catan3.Shared.Models;
using Catan3.Shared.Utility;
using System.Runtime.InteropServices;
using System.Diagnostics;
public static class NativeMessageBox
{
    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern int MessageBox(IntPtr hWnd, string text, string caption, uint type);

    /// <summary>Shows a blocking system MessageBox on Windows; falls back to stderr elsewhere.</summary>
    public static void Show(string text, string caption = "Message", uint type = 0)
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            // hWnd = IntPtr.Zero (no owner), type=0 = OK
            MessageBox(IntPtr.Zero, text, caption, type);
        }
        else
        {
            Console.Error.WriteLine($"{caption}: {text}");
        }
    }
}


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
        /// <param name="logFilePath">The path of the log file to use for generating the test file path</param>
        public GameRecorder(GameModel initialGameModel, string logFilePath)
        {
            if (initialGameModel is null)
            {
                NativeMessageBox.Show("Initial GameModel is null.", "Game Recorder Error", 0x00000010 /* MB_ICONHAND */);
                Debugger.Break();
                throw new ArgumentNullException(nameof(initialGameModel));
            }

            // Create a deep copy of the GameModel via serialize/deserialize to ensure immutability
            var jsonString = SerializationHelper.JsonSerialize(initialGameModel);
            _initialGameModel = SerializationHelper.JsonDeserialize<GameModel>(jsonString) 
                ?? throw new InvalidOperationException("Failed to deserialize GameModel during deep copy");

            _recordedActions = new List<object>();
            _outputPath = GenerateTestFilePath(logFilePath);
            _isRecording = true;

            this.TraceMessage($"🎬 Recording started from GameState: {_initialGameModel.GameState}");
            this.TraceMessage($"📁 Recording will be saved to: {_outputPath}");
        }

        /// <summary>
        /// Records an action that occurred during the recording session.
        /// </summary>
        /// <param name="message">The message object containing the action information</param>
        public void RecordAction(object message)
        {
            if (!_isRecording)
            {
                this.TraceMessage($"⚠️ Attempted to record action while recording is stopped: {message.GetType().Name}");
                return;
            }

            try
            {
                // Map message to test action format
                var action = MapMessageToAction(message);
                if (action != null)
                {
                    _recordedActions.Add(action);
                    this.TraceMessage($"📝 Recorded: {message.GetType().Name}");
                }
                else
                {
                    this.TraceMessage($"🔇 Ignored: {message.GetType().Name} (not recorded in tests)");
                }
            }
            catch (Exception ex)
            {
                this.TraceMessage($"❌ Error recording action {message.GetType().Name}: {ex.Message}");
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
                "PlayersDoingSupplemental" => MapPlayersDoingSupplementalMessage((dynamic)message),
                "BalanceBoardMessage" => MapBalanceBoardMessage((dynamic)message),
                
                // Message types we explicitly don't record (return null)
                "NewGameMessage" => null,
                "UpdateGameModel" => null,
                "EndGame" => null,
                "ErrorMessage" => null,
                "PersistGameMessage" => null,
                "LoadGameMessage" => null,
                "StartRecordingMessage" => null,
                "StopRecordingMessage" => null,
                
                _ => throw new NotSupportedException($"Unsupported message type for recording: {message.GetType().Name}. Add explicit mapping for this message type.")
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

        private object MapPlayersDoingSupplementalMessage(dynamic message)
        {
            return new
            {
                type = "SelectSupplementalPlayers",
                parameters = new Dictionary<string, object> { { "playerIds", message.PlayerIds.ToArray() } },
                timestamp = DateTime.UtcNow
            };
        }

        private object MapBalanceBoardMessage(dynamic message)
        {
            return new
            {
                type = ActionType.ShuffleBoard,
                parameters = (Dictionary<string, object>?)null,
                timestamp = DateTime.UtcNow
            };
        }

        private static ActionType GetActionTypeFromGameAction(dynamic action)
        {
            return action.ToString() switch
            {
                "Shuffle" => ActionType.ShuffleBoard,
                "Undo" => ActionType.PreviousBoard,
                "Redo" => ActionType.RedoBoard,
                "Next" => ActionType.AdvanceNext,
                _ => ActionType.AdvanceNext // Default fallback
            };
        }

        private static ActionType GetPurchaseActionType(dynamic entitlement)
        {
            return entitlement.ToString() switch
            {
                "Road" => ActionType.PurchaseRoad,
                "Settlement" => ActionType.PurchaseSettlement,
                "City" => ActionType.PurchaseCity,
                "Soldier" => ActionType.PurchaseSoldier,
                _ => ActionType.PurchaseRoad // Default fallback
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

                // Serialize with pretty formatting using shared options
                var jsonContent = JsonSerializer.Serialize(catanTestData, JsonHelper.PrettyOptions);

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

                this.TraceMessage($"🎬 Recording ended - saved {actionCount} actions to {_outputPath}");
                return _outputPath;
            }
            catch (Exception ex)
            {
                this.TraceMessage($"❌ Failed to save recording: {ex.Message}");
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
        /// Generates the test file path based on the log file path, using the same location and name with .catan_test extension.
        /// </summary>
        private static string GenerateTestFilePath(string logFilePath)
        {
            var directory = Path.GetDirectoryName(logFilePath) ?? throw new ArgumentException("Invalid log file path", nameof(logFilePath));
            var fileNameWithoutExtension = Path.GetFileNameWithoutExtension(logFilePath);
            return Path.Combine(directory, $"{fileNameWithoutExtension}.catan_test");
        }

        /// <summary>
        /// Logs a message with recording context.
        /// </summary>
        private void TraceMessage(string message)
        {
            System.Diagnostics.Debug.WriteLine($"GameRecorder: {message}");
        }
    }
}