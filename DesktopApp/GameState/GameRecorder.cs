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
        private readonly List<IRecordedMessage> _recordedActions;
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

            _recordedActions = [];
            _outputPath = GenerateTestFilePath(logFilePath);
            _isRecording = true;

            this.TraceMessage($"🎬 Recording started from GameState: {_initialGameModel.GameState}");
            this.TraceMessage($"📁 Recording will be saved to: {_outputPath}");
        }

        /// <summary>
        /// Records an action that occurred during the recording session.
        /// </summary>
        /// <param name="recordedMessage">The recorded message containing the action and game hash</param>
        public void RecordAction(IRecordedMessage recordedMessage)
        {
            if (!_isRecording)
            {
                this.TraceMessage($"⚠️ Attempted to record action while recording is stopped: {recordedMessage.RecordType}");
                return;
            }

            try
            {
                _recordedActions.Add(recordedMessage);
                this.TraceMessage($"📝 Recorded: {recordedMessage.RecordType} with hash: {recordedMessage.ExpectedGameHash}");
            }
            catch (Exception ex)
            {
                this.TraceMessage($"❌ Error recording action {recordedMessage.RecordType}: {ex.Message}");
                // Don't throw - continue recording other actions
            }
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