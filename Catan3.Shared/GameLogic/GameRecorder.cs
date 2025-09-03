using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Linq;
using System.Threading.Tasks;
using Catan3.Shared.Models;
using Catan3.Shared.Utility;
using Catan3.Shared.Interfaces;

namespace Catan3.Shared.GameLogic
{
    /// <summary>
    /// Manages recording of game actions from a specific starting GameModel state.
    /// Creates complete .catan_test files containing both the initial GameModel and ActionStack.
    /// Uses dependency injection for platform-specific logging and error handling.
    /// </summary>
    public class GameRecorder : IGameRecorder
    {
        private readonly GameModel _initialGameModel;
        private readonly List<IRecordedMessage> _recordedActions;
        private readonly string _outputPath;
        private readonly ICatanDebugTrace _logger;
        private readonly IFileOperations _fileOperations;
        private bool _isRecording;

        /// <summary>
        /// Creates a new recording session starting from the provided GameModel.
        /// </summary>
        /// <param name="initialGameModel">The GameModel to use as the starting state for the recording</param>
        /// <param name="logFilePath">The path of the log file to use for generating the test file path</param>
        /// <param name="logger">Logger for recording operations</param>
        /// <param name="fileOperations">File operations service for saving recordings</param>
        public GameRecorder(GameModel initialGameModel, string logFilePath, ICatanDebugTrace logger, IFileOperations fileOperations)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _fileOperations = fileOperations ?? throw new ArgumentNullException(nameof(fileOperations));

            if (initialGameModel is null)
            {
                _logger.TraceError("Initial GameModel is null.");
                throw new ArgumentNullException(nameof(initialGameModel));
            }

            // Create a deep copy of the GameModel via serialize/deserialize to ensure immutability
            var jsonString = JsonHelper.Serialize(initialGameModel);
            _initialGameModel = JsonHelper.Deserialize<GameModel>(jsonString) 
                ?? throw new InvalidOperationException("Failed to deserialize GameModel during deep copy");

            _recordedActions = [];
            _outputPath = GenerateTestFilePath(logFilePath);
            _isRecording = true;

            _logger.TraceInfo($"🎬 Recording started from GameState: {_initialGameModel.GameState}");
            _logger.TraceInfo($"📁 Recording will be saved to: {_outputPath}");
        }

        /// <summary>
        /// Records an action that occurred during the recording session.
        /// </summary>
        /// <param name="recordedMessage">The recorded message containing the action and game hash</param>
        public void RecordAction(IRecordedMessage recordedMessage)
        {
            if (!_isRecording)
            {
                _logger.TraceWarning($"⚠️ Attempted to record action while recording is stopped: {recordedMessage.RecordType}");
                return;
            }

            try
            {
                _recordedActions.Add(recordedMessage);
                _logger.TraceMessage($"📝 Recorded: {recordedMessage.RecordType} with hash: {recordedMessage.ExpectedGameHash}");
            }
            catch (Exception ex)
            {
                _logger.TraceError($"❌ Error recording action {recordedMessage.RecordType}: {ex.Message}");
                // Don't throw - continue recording other actions
            }
        }

        /// <summary>
        /// Ends the recording session and saves the complete .catan_test file.
        /// </summary>
        /// <returns>The file path where the .catan_test file was saved</returns>
        public async Task<string> EndRecording()
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

                // Convert full path to relative path for file operations
                var relativePath = GetRelativePathFromFullPath(_outputPath);
                
                // Save the file using file operations service
                var success = await _fileOperations.WriteTextFileAsync(relativePath, jsonContent);
                if (!success)
                {
                    throw new InvalidOperationException($"Failed to save recording file to {_outputPath}");
                }

                var actionCount = _recordedActions.Count;
                _isRecording = false;

                _logger.TraceInfo($"🎬 Recording ended - saved {actionCount} actions to {_outputPath}");
                return _outputPath;
            }
            catch (Exception ex)
            {
                _logger.TraceError($"❌ Failed to save recording: {ex.Message}");
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
        /// Converts a full file path to a relative path for use with file operations.
        /// Assumes the path is under the Documents/Catan Saved Games folder.
        /// </summary>
        private static string GetRelativePathFromFullPath(string fullPath)
        {
            // Find the index of "Catan Saved Games" in the path
            const string catanFolder = "Catan Saved Games";
            var catanIndex = fullPath.IndexOf(catanFolder, StringComparison.OrdinalIgnoreCase);
            
            if (catanIndex >= 0)
            {
                // Return the path starting from "Catan Saved Games"
                return fullPath.Substring(catanIndex);
            }
            
            // Fallback: return just the filename if we can't find the expected structure
            return Path.GetFileName(fullPath);
        }
    }
}