using System;
using Catan3.Shared.Interfaces;
using Catan3.Shared.Models;
using Catan3.Shared.GameLogic;

namespace Catan3.GameService.Services
{
    /// <summary>
    /// GameService implementation of IGameRecorderFactory.
    /// Creates GameRecorder instances with GameService-specific logging.
    /// </summary>
    public class GameServiceRecorderFactory : IGameRecorderFactory
    {
        private readonly IGameLogger _logger;
        private readonly IFileOperations _fileOperations;

        public GameServiceRecorderFactory(IGameLogger logger, IFileOperations fileOperations)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _fileOperations = fileOperations ?? throw new ArgumentNullException(nameof(fileOperations));
        }

        public IGameRecorder CreateRecorder(GameModel initialGameModel, string logFilePath)
        {
            return new GameRecorder(initialGameModel, logFilePath, _logger, _fileOperations);
        }
    }
}