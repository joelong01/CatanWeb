using Catan3.Shared.Models;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Catan3.Shared.Interfaces
{
    /// <summary>
    /// Interface for game state management operations.
    /// Provides a clean abstraction between messaging layer and game logic.
    /// Can be implemented by desktop app (GameStateMachine) or web service proxy.
    /// </summary>
    public interface IGameStateMachine
    {

        /// <summary>
        /// Executes a game action (Next, Undo, Redo, etc.).
        /// </summary>
        Task<GameModel> ExecuteGameActionAsync(ExecuteGameActionMessage message);

        /// <summary>
        /// Handles dice roll operation.
        /// </summary>
        Task<GameModel> HandleRollAsync(RollMessage message);

        /// <summary>
        /// Handles purchase of entitlements (roads, settlements, cities, soldiers).
        /// </summary>
        Task<GameModel> HandlePurchaseAsync(PurchaseMessage message);

        /// <summary>
        /// Handles shuffle operation for deck/board state.
        /// </summary>
        Task<GameModel> HandleShuffleAsync(ShuffleMessage message);

        /// <summary>
        /// Handles building upgrade operation.
        /// </summary>
        Task<GameModel> HandleBuildingUpgradeAsync(BuildingUpgradeMessage message);

        /// <summary>
        /// Handles road purchase operation.
        /// </summary>
        Task<GameModel> HandleRoadPurchaseAsync(RoadPurchaseMessage message);

        /// <summary>
        /// Handles robber movement operation.
        /// </summary>
        Task<GameModel> HandleMoveRobberAsync(MoveRobberMessage message);

        /// <summary>
        /// Handles setting player order operation.
        /// </summary>
        Task<GameModel> HandleSetPlayerOrderAsync(SetPlayerOrderMessage message);

        /// <summary>
        /// Handles individual player participation in supplemental phase.
        /// </summary>
        Task<GameModel> HandleParticipatingInSupplementalAsync(ParticipatingInSupplementalMessage message);

        /// <summary>
        /// Handles go first operation.
        /// </summary>
        Task<GameModel> HandleGoFirstAsync(GoFirstMessage message);

        /// <summary>
        /// Handles board balance operation.
        /// </summary>
        Task<GameModel> HandleBalanceBoardAsync(BalanceBoardMessage message);

        /// <summary>
        /// Creates a new game with specified parameters.
        /// </summary>
        Task<GameModel> HandleNewGameAsync(Catan3.Shared.Models.NewGameMessage message);

        /// <summary>
        /// Loads a game from the specified file path.
        /// </summary>
        Task<GameModel> HandleLoadGameAsync(LoadGameMessage message);

        /// <summary>
        /// Starts recording game actions to the specified output path.
        /// </summary>
        Task HandleStartRecordingAsync(StartRecordingMessage message);

        /// <summary>
        /// Stops recording game actions and saves the recording.
        /// </summary>
        Task HandleStopRecordingAsync(StopRecordingMessage message);

        /// <summary>
        /// Handles game persistence operations (save/load).
        /// </summary>
        Task HandlePersistGameAsync(PersistGameMessage message);

        /// <summary>
        /// Handles end game operation.
        /// </summary>
        Task HandleEndGameAsync(EndGame message);
    }
}