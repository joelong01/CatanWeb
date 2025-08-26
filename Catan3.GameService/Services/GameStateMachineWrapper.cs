using System;
using System.Threading.Tasks;
using Catan3.Shared.Interfaces;
using Catan3.Shared.Models;
using Catan3.Shared.GameLogic;

namespace Catan3.GameService.Services
{
    /// <summary>
    /// Wrapper around the shared GameStateMachine that adds GameService-specific functionality.
    /// Provides GameId tracking and any other GameService-specific concerns.
    /// </summary>
    public class GameStateMachineWrapper : IGameStateMachine
    {
        private readonly GameStateMachine _gameStateMachine;
        
        /// <summary>
        /// Server-generated unique identifier for this game instance.
        /// </summary>
        public string GameId { get; private set; }

        public GameStateMachineWrapper(GameStateMachine gameStateMachine)
        {
            _gameStateMachine = gameStateMachine ?? throw new ArgumentNullException(nameof(gameStateMachine));
            GameId = Guid.NewGuid().ToString();
        }

        // Delegate all IGameStateMachine methods to the wrapped instance
        public Task<GameModel> ExecuteGameActionAsync(ExecuteGameActionMessage message) => 
            _gameStateMachine.ExecuteGameActionAsync(message);

        public Task<GameModel> HandleShuffleAsync(ShuffleMessage message) => 
            _gameStateMachine.HandleShuffleAsync(message);

        public Task<GameModel> HandleBuildingUpgradeAsync(BuildingUpgradeMessage message) => 
            _gameStateMachine.HandleBuildingUpgradeAsync(message);

        public Task<GameModel> HandleSetPlayerOrderAsync(SetPlayerOrderMessage message) => 
            _gameStateMachine.HandleSetPlayerOrderAsync(message);

        public Task<GameModel> HandleRoadPurchaseAsync(RoadPurchaseMessage message) => 
            _gameStateMachine.HandleRoadPurchaseAsync(message);

        public Task<GameModel> HandleMoveRobberAsync(MoveRobberMessage message) => 
            _gameStateMachine.HandleMoveRobberAsync(message);

        public Task<GameModel> HandleNewGameAsync(NewGameMessage message) => 
            _gameStateMachine.HandleNewGameAsync(message);

        public Task<GameModel> HandleLoadGameAsync(LoadGameMessage message) => 
            _gameStateMachine.HandleLoadGameAsync(message);

        public Task HandleStartRecordingAsync(StartRecordingMessage message) => 
            _gameStateMachine.HandleStartRecordingAsync(message);

        public Task HandleStopRecordingAsync(StopRecordingMessage message) => 
            _gameStateMachine.HandleStopRecordingAsync(message);

        public Task<GameModel> HandleRollAsync(RollMessage message) => 
            _gameStateMachine.HandleRollAsync(message);

        public Task<GameModel> HandlePurchaseAsync(PurchaseMessage message) => 
            _gameStateMachine.HandlePurchaseAsync(message);

        public Task<GameModel> HandleParticipatingInSupplementalAsync(ParticipatingInSupplementalMessage message) => 
            _gameStateMachine.HandleParticipatingInSupplementalAsync(message);

        public Task<GameModel> HandleBalanceBoardAsync(BalanceBoardMessage message) => 
            _gameStateMachine.HandleBalanceBoardAsync(message);

        public Task HandleEndGameAsync(EndGame message) => 
            _gameStateMachine.HandleEndGameAsync(message);

        public Task<GameModel> HandleGoFirstAsync(GoFirstMessage message) => 
            _gameStateMachine.HandleGoFirstAsync(message);

        public Task HandlePersistGameAsync(PersistGameMessage message) => 
            _gameStateMachine.HandlePersistGameAsync(message);
    }
}