using Catan3.Shared.Models;

namespace Catan3.Shared.Interfaces
{
    /// <summary>
    /// Factory interface for creating game recorder instances.
    /// Allows platform-specific recorder creation while keeping GameStateMachine decoupled.
    /// </summary>
    public interface IGameRecorderFactory
    {
        /// <summary>
        /// Creates a new game recorder starting from the provided GameModel.
        /// </summary>
        /// <param name="initialGameModel">The GameModel to use as the starting state for the recording</param>
        /// <param name="logFilePath">The path of the log file to use for generating the test file path</param>
        /// <returns>A new IGameRecorder instance</returns>
        IGameRecorder CreateRecorder(GameModel initialGameModel, string logFilePath);
    }
}