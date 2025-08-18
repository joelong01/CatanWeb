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
                return; // just ignore if recording has ended
            }

            // Map message to test action format
            var action = MapMessageToAction(message);
            if (action != null)
            {
                _recordedActions.Add(action);
                this.LogMessage($"📝 Recorded: {action.GetType().Name}");
            }
        }

        /// <summary>
        /// Maps different message types to the appropriate test action format.
        /// </summary>
        private object? MapMessageToAction(object message)
        {
            // This will be implemented based on the specific message types used in the application
            // For now, just record the message as-is for basic functionality
            return new
            {
                type = message.GetType().Name,
                message = message,
                timestamp = DateTime.UtcNow
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