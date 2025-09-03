using Catan3.Shared.Interfaces;
using Catan3.Shared.Models;
using Catan3.Shared.GameLogic;

namespace Catan3.Services
{
    /// <summary>
    /// Desktop implementation of IGameRecorderFactory that creates GameRecorder instances.
    /// </summary>
    public class DesktopGameRecorderFactory : IGameRecorderFactory
    {
        private readonly ICatanDebugTrace _logger;
        private readonly IFileOperations _fileOperations;

        public DesktopGameRecorderFactory(ICatanDebugTrace logger, IFileOperations fileOperations)
        {
            _logger = logger;
            _fileOperations = fileOperations;
        }

        public IGameRecorder CreateRecorder(GameModel gameModel, string filePath)
        {
            return new GameRecorder(gameModel, filePath, _logger, _fileOperations);
        }
    }
}