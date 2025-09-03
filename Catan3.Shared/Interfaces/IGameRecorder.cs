using Catan3.Shared.Models;

namespace Catan3.Shared.Interfaces
{
    /// <summary>
    /// Interface for recording game actions for replay functionality.
    /// Allows platform-specific implementations (Desktop vs Service).
    /// </summary>
    public interface IGameRecorder
    {
        /// <summary>
        /// Records a game action for later replay.
        /// </summary>
        /// <param name="recordedMessage">The recorded message containing action details.</param>
        void RecordAction(IRecordedMessage recordedMessage);

        /// <summary>
        /// Ends the recording session and returns the file path where it was saved.
        /// </summary>
        /// <returns>The file path of the saved recording.</returns>
        Task<string> EndRecording();

        /// <summary>
        /// Gets the number of actions recorded so far.
        /// </summary>
        int ActionCount { get; }

        /// <summary>
        /// Gets whether the recording is currently active.
        /// </summary>
        bool IsRecording { get; }

        /// <summary>
        /// Gets the output path where the recording will be saved.
        /// </summary>
        string OutputPath { get; }
    }
}