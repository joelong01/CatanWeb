using Catan3.Shared.Models;

namespace Catan3.Shared.Interfaces
{
    /// <summary>
    /// Represents a serializable log structure for persistence operations.
    /// </summary>
    public class SerializableLog
    {
        public List<string> DoneStack { get; set; } = [];
        public List<string> RedoStack { get; set; } = [];
        public GameType GameType { get; set; } = GameType.Regular;
        public int DoneCount { get; set; } = 0;
        public int RedoCount { get; set; } = 0;
    }

    /// <summary>
    /// Interface for game logging and persistence operations.
    /// Provides abstraction over different logging implementations for Desktop and Service.
    /// </summary>
    public interface IGameLog
    {
        /// <summary>
        /// Gets or sets the type of game being logged.
        /// </summary>
        GameType GameType { get; set; }

        /// <summary>
        /// Gets the number of completed operations in the log.
        /// </summary>
        int DoneCount { get; }

        /// <summary>
        /// Gets the number of operations available for redo.
        /// </summary>
        int RedoCount { get; }

        /// <summary>
        /// Gets whether undo operations are available.
        /// </summary>
        bool CanUndo { get; }

        /// <summary>
        /// Gets whether redo operations are available.
        /// </summary>
        bool CanRedo { get; }

        /// <summary>
        /// Gets whether the log is currently active for tracking operations.
        /// </summary>
        bool IsActive { get; set; }

        /// <summary>
        /// Gets the file path associated with this log.
        /// </summary>
        string FilePath { get; }

        /// <summary>
        /// Gets a serializable representation of the log for persistence.
        /// </summary>
        SerializableLog GetSerializableLog();

        /// <summary>
        /// Restores the log state from a serializable log structure.
        /// </summary>
        /// <param name="serializableLog">The serializable log to restore from</param>
        /// <returns>The current game model after loading the log</returns>
        GameModel LoadFromSerializableLog(SerializableLog serializableLog);

        /// <summary>
        /// Records a completed game model operation in the log.
        /// </summary>
        void Done(GameModel model);

        /// <summary>
        /// Initializes the log with an existing GameModel without any modifications.
        /// Used during game loading to preserve the original GameModel state.
        /// </summary>
        /// <param name="gameModel">The GameModel to initialize the log with</param>
        void InitializeWithGameModel(GameModel gameModel);

        /// <summary>
        /// Gets the current game model state from the log.
        /// </summary>
        GameModel CurrentState();

        /// <summary>
        /// Creates a copy of the current game model for manipulation.
        /// </summary>
        GameModel CopyCurrent();

        /// <summary>
        /// Undoes the last operation and returns the previous game state.
        /// </summary>
        GameModel? Undo();

        /// <summary>
        /// Redoes a previously undone operation and returns the game state.
        /// </summary>
        GameModel? Redo();

        /// <summary>
        /// Asynchronously saves the current log state to persistent storage.
        /// </summary>
        Task SaveAsync();

        /// <summary>
        /// Requests a coalesced background save. Rapid calls are coalesced so
        /// only the latest state is persisted. This is the primary entry point
        /// for persistence — called by GameStateMachine after each action.
        /// </summary>
        void RequestSave();

        /// <summary>
        /// Asynchronously saves the current log state to a specified file path.
        /// </summary>
        Task SaveAsAsync(string filePath);

        /// <summary>
        /// Sets the logger for diagnostics (e.g., PERF-SAVE timing).
        /// Called after construction when the logger isn't available at creation time.
        /// </summary>
        void SetLogger(ICatanDebugTrace logger);
    }
}