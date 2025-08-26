using Catan3.Shared.Interfaces;
using Catan3.Shared.GameLogic;

namespace Catan3.Services
{
    /// <summary>
    /// Factory for creating GameStateMachine instances with Desktop-specific dependencies.
    /// Maintains the same interface as the old GameStateMachine constructor to minimize Desktop code changes.
    /// </summary>
    public static class DesktopGameStateMachineFactory
    {
        /// <summary>
        /// Creates a new GameStateMachine with Desktop dependencies using the same parameters as the old constructor.
        /// </summary>
        /// <param name="persistenceService">The Desktop persistence service.</param>
        /// <param name="localSaveFile">The local save file path.</param>
        /// <returns>A configured GameStateMachine using shared implementation with Desktop services.</returns>
        public static GameStateMachine Create(Catan.Services.IPersistenceService persistenceService, string localSaveFile)
        {
            // Create Desktop-specific implementations of shared interfaces
            var adaptedPersistenceService = new DesktopPersistenceServiceAdapter(persistenceService);
            var gameLog = new DesktopGameLog(persistenceService, localSaveFile);
            var gameLogger = new DesktopGameLogger();
            var fileOperations = new DesktopFileOperationsAdapter(persistenceService);
            var recorderFactory = new DesktopGameRecorderFactory(gameLogger, fileOperations);
            var gameFactory = new DesktopGameFactory();

            // Create and return shared GameStateMachine with Desktop dependencies
            return new GameStateMachine(gameLog, gameLogger, recorderFactory, adaptedPersistenceService, gameFactory);
        }
    }
}