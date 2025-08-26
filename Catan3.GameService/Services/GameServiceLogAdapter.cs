using System;
using System.Threading.Tasks;
using Catan3.Shared.Interfaces;
using Catan3.Shared.Models;
using Catan3.GameService.Utility;

namespace Catan3.GameService.Services
{
    /// <summary>
    /// GameService implementation of IGameLog that wraps the existing Log&lt;string&gt; class.
    /// Bridges the shared GameStateMachine logging interface to GameService's existing Log infrastructure.
    /// </summary>
    public class GameServiceLogAdapter : IGameLog
    {
        private Log<string> _log;
        private readonly IPersistenceService _persistenceService;

        public GameServiceLogAdapter(Log<string> log, IPersistenceService persistenceService)
        {
            _log = log ?? throw new ArgumentNullException(nameof(log));
            _persistenceService = persistenceService ?? throw new ArgumentNullException(nameof(persistenceService));
        }

        public GameType GameType 
        { 
            get => _log.GameType; 
            set => _log.GameType = value; 
        }

        public int DoneCount => _log.DoneCount;
        public int RedoCount => _log.RedoCount;
        public bool CanUndo => _log.CanUndo;
        public bool CanRedo => _log.CanRedo;
        public bool IsActive { get; set; } = true; // GameService doesn't have this concept, default to true
        public string FilePath => _log.FilePath;

        public Catan3.Shared.Interfaces.SerializableLog GetSerializableLog()
        {
            var serviceLog = _log.GetSerializableLog();
            // Convert from GameService SerializableLog to Shared SerializableLog
            return new Catan3.Shared.Interfaces.SerializableLog
            {
                DoneStack = serviceLog.DoneStack,
                RedoStack = serviceLog.RedoStack,
                GameType = serviceLog.GameType,
                DoneCount = serviceLog.DoneCount,
                RedoCount = serviceLog.RedoCount
            };
        }

        public void LoadFromSerializableLog(Catan3.Shared.Interfaces.SerializableLog serializableLog)
        {
            // Convert shared SerializableLog to GameService SerializableLog format
            var serviceSerializableLog = new Catan3.GameService.Utility.SerializableLog
            {
                DoneStack = serializableLog.DoneStack,
                RedoStack = serializableLog.RedoStack,
                GameType = serializableLog.GameType,
                DoneCount = serializableLog.DoneCount,
                RedoCount = serializableLog.RedoCount
            };

            // Replace our internal log with a new one created from the serializable log
            _log = Log<string>.FromSerializableLog(serviceSerializableLog, _persistenceService, _log.FilePath);
        }

        public void Done(GameModel model)
        {
            _log.Done(model);
        }

        public GameModel CurrentState()
        {
            return _log.CurrentState();
        }

        public GameModel CopyCurrent()
        {
            return _log.CopyCurrent();
        }

        public GameModel? Undo()
        {
            return _log.Undo();
        }

        public GameModel? Redo()
        {
            return _log.Redo();
        }

        public Task SaveAsync()
        {
            return _log.SaveAsync();
        }

        public Task SaveAsAsync(string filePath)
        {
            return _log.SaveAsAsync(filePath);
        }
    }
}