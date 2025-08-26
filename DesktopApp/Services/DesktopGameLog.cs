using Catan3.Shared.Interfaces;
using Catan3.Shared.Models;
using Catan3.Utility;
using System.Threading.Tasks;
using SharedSerializableLog = Catan3.Shared.Interfaces.SerializableLog;

namespace Catan3.Services
{
    /// <summary>
    /// Desktop implementation of IGameLog that wraps the existing Log&lt;string&gt; class.
    /// </summary>
    public class DesktopGameLog : IGameLog
    {
        private Log<string> _log;
        private readonly Catan.Services.IPersistenceService _persistenceService;

        public DesktopGameLog(Catan.Services.IPersistenceService persistenceService, string localSaveFile)
        {
            _persistenceService = persistenceService;
            _log = new Log<string>(persistenceService, localSaveFile);
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
        
        public bool IsActive 
        { 
            get => _log.IsActive;
            set => _log.IsActive = value;
        }

        public string FilePath => _log.FilePath;

        public SharedSerializableLog GetSerializableLog()
        {
            var desktopLog = _log.GetSerializableLog();
            return new SharedSerializableLog
            {
                DoneStack = desktopLog.DoneStack,
                RedoStack = desktopLog.RedoStack,
                GameType = desktopLog.GameType,
                DoneCount = desktopLog.DoneCount,
                RedoCount = desktopLog.RedoCount
            };
        }

        public void LoadFromSerializableLog(SharedSerializableLog serializableLog)
        {
            // Convert shared SerializableLog to Desktop SerializableLog format
            var desktopSerializableLog = new Catan3.Utility.SerializableLog
            {
                DoneStack = serializableLog.DoneStack,
                RedoStack = serializableLog.RedoStack,
                GameType = serializableLog.GameType,
                DoneCount = serializableLog.DoneCount,
                RedoCount = serializableLog.RedoCount
            };

            // Replace our internal log with a new one created from the serializable log
            _log = Log<string>.FromSerializableLog(desktopSerializableLog, _persistenceService, _log.FilePath);
        }

        public void Done(GameModel model) => _log.Done(model);
        public GameModel CurrentState() => _log.CurrentState();
        public GameModel CopyCurrent() => _log.CopyCurrent();
        public GameModel? Undo() => _log.Undo();
        public GameModel? Redo() => _log.Redo();
        public Task SaveAsync() => _log.SaveAsync();
        public Task SaveAsAsync(string filePath) => _log.SaveAsAsync(filePath);
    }
}