using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;

using Catan3.Shared.Models;
using Catan3.Shared.Utility;
using Catan3.GameService.Utility;
using Catan3.GameService.Services;
using Catan3.GameService.Factory;

namespace Catan3.GameService.Controllers
{
    public class GameController
    {
        private Log<string> Log;
        private IPersistanceService? MyPersistanceService { get; set; }
        
        public GameController(IPersistanceService? persistanceService, string localSaveFile)
        {
            Log = new Log<string>(persistanceService, localSaveFile);
            MyPersistanceService = persistanceService;
        }
        
        public int DoneCount => Log.DoneCount;

        // Simplified message handling without MVVM
        public GameModel HandleDoAction(DoAction message)
        {
            try
            {
                GameModel? gameModel = null;
                switch (message.Action)
                {
                    case GameAction.Shuffle:
                        gameModel = ShuffleCurrentGame();
                        LogGameModel(gameModel);
                        break;
                    case GameAction.Undo:
                        gameModel = Undo(); // NOTE: Undo does not call LogGameMode!
                        break;
                    case GameAction.Redo:
                        gameModel = Redo();  // NOTE: Redo does not call LogGameMode!
                        break;
                    case GameAction.Next:
                        gameModel = NextState();
                        LogGameModel(gameModel);
                        break;
                }
                
                if (gameModel is not null)
                {
                    return gameModel;
                }
                else
                {
                    throw new GameException($"Unable to do action {message}");
                }
            }
            catch (GameException e)
            {
                TraceMessage($"Exception doing Action {message.Action}. Message: {e}");
                throw;
            }
        }

        public GameModel HandlePurchaseMessage(PurchaseMessage message)
        {
            try
            {
                var gameModel = OnPurchase(message);
                LogGameModel(gameModel);
                return gameModel;
            }
            catch (GameException e)
            {
                TraceMessage($"Exception purchasing {message.Entitlement}. Message: {e}");
                throw;
            }
        }

        public GameModel HandleBuildingUpgrade(BuildingUpgradeMessage message)
        {
            try
            {
                var gameModel = BuildingUpgrade(message);
                LogGameModel(gameModel);
                return gameModel;
            }
            catch (GameException e)
            {
                TraceMessage($"Exception upgrading building. Message: {e}");
                throw;
            }
        }

        public GameModel HandleRoadPurchase(RoadPurchaseMessage message)
        {
            try
            {
                var model = RoadPurchase(message);
                LogGameModel(model);
                return model;
            }
            catch (GameException e)
            {
                TraceMessage($"Exception purchasing road. Message: {e}");
                throw;
            }
        }

        public GameModel HandleMoveRobber(MoveRobberMessage message)
        {
            try
            {
                var model = MoveRobber(message);
                LogGameModel(model);
                return model;
            }
            catch (GameException e)
            {
                TraceMessage($"Exception moving robber. Message: {e}");
                throw;
            }
        }

        public GameModel HandleNewGame(NewGameMessage message)
        {
            try
            {
                var model = NewGame(message.GameType, message.PlayerIds);
                LogGameModel(model);
                return model;
            }
            catch (GameException e)
            {
                TraceMessage($"Exception creating new game. Message: {e}");
                throw;
            }
        }

        public async Task<GameModel> HandleLoadGame(LoadGameMessage message)
        {
            try
            {
                var model = await LoadGame(message.LocalFile);
                LogGameModel(model);
                return model;
            }
            catch (GameException e)
            {
                TraceMessage($"Exception loading game. Message: {e}");
                throw;
            }
        }

        public GameModel HandleRoll(RollMessage message)
        {
            try
            {
                var model = OnRoll(message);
                LogGameModel(model);
                return model;
            }
            catch (GameException e)
            {
                TraceMessage($"Exception handling roll. Message: {e}");
                throw;
            }
        }

        // Helper method for tracing
        private void TraceMessage(string message, [CallerMemberName] string cmb = "", [CallerLineNumber] int cln = 0, [CallerFilePath] string cfp = "")
        {
            Debug.WriteLine($"[{cmb}:{cln}] {message}");
        }

        // Simplified implementations for now - these will be expanded later
        private GameModel ShuffleCurrentGame()
        {
            GameModel gameModel = Log.CopyCurrent();
            ThrowIfWrongState(gameModel.GameState, [GameState.PickingBoard]);
            gameModel.Shuffle();
            return gameModel;
        }

        private GameModel? Undo()
        {
            GameModel result = Log.Undo() ?? throw new GameException("Undo cannot be done");
            SetActionFlags(result);
            result.ActionFlags.RedoEnabled = true;
            return result;
        }

        private GameModel? Redo()
        {
            GameModel result = Log.Redo() ?? throw new GameException("Redo cannot be done");
            SetActionFlags(result);
            return result;
        }

        private GameModel NextState()
        {
            GameModel gameModel = Log.CopyCurrent();
            if (!CanTransitionToNext(gameModel)) throw new GameException("Cannot transition to Next state at this time");
            
            // Simplified NextState implementation
            switch (gameModel.GameState)
            {
                case GameState.WaitingForNext:
                    gameModel.ChangePlayer(1);
                    gameModel.GameState = GameState.WaitingForRoll;
                    break;
                default:
                    throw new GameException($"NextState not implemented for {gameModel.GameState}");
            }
            
            return gameModel;
        }

        private GameModel OnPurchase(PurchaseMessage message)
        {
            GameModel gameModel = Log.CopyCurrent();
            Entitlement entitlement = message.Entitlement;
            
            ThrowIfWrongState(gameModel.GameState, [GameState.WaitingForNext, GameState.Supplemental]);
            
            gameModel.CurrentPlayer().UnspentEntitlements.Add(entitlement);
            return gameModel;
        }

        private GameModel BuildingUpgrade(BuildingUpgradeMessage message)
        {
            GameModel gameModel = Log.CopyCurrent();
            // Simplified implementation
            return gameModel;
        }

        private GameModel RoadPurchase(RoadPurchaseMessage message)
        {
            GameModel gameModel = Log.CopyCurrent();
            // Simplified implementation
            return gameModel;
        }

        private GameModel MoveRobber(MoveRobberMessage message)
        {
            GameModel gameModel = Log.CopyCurrent();
            gameModel.Robber.Coordinates = message.Coordinates;
            gameModel.Robber.MovedBy = gameModel.CurrentPlayerId;
            return gameModel;
        }

        private GameModel OnRoll(RollMessage message)
        {
            GameModel gameModel = Log.CopyCurrent();
            ThrowIfWrongState(gameModel.GameState, [GameState.WaitingForRoll]);
            gameModel.RollModel.TurnRollModel = message.Roll;
            gameModel.GameState = GameState.WaitingForNext;
            return gameModel;
        }

        public GameModel NewGame(GameType selectedGame, IList<string> playerIds)
        {
            var gameModel = GameFactory.CreateGame(selectedGame, playerIds);
            Log.GameType = selectedGame;
            gameModel.GameType = selectedGame;
            gameModel.GameState = GameState.PickingBoard;
            return gameModel;
        }

        public async Task<GameModel> LoadGame(string filePath)
        {
            if (MyPersistanceService is null) throw new GameException("no persistance service was set");

            var compressedBytes = await MyPersistanceService.OpenAsync(filePath) ?? throw new GameException($"Unable to open file {filePath}");

            var decompressedJson = SerializationHelper.Decompress(compressedBytes);
            var savedLog = SerializationHelper.JsonDeserialize<SerializableLog>(decompressedJson) ?? throw new GameException("Error: Failed to load the game data.");
            Log<string> log = Log<string>.FromSerializableLog(savedLog, MyPersistanceService, filePath);
            this.Log = log;

            return Log.CurrentState();
        }

        private void SetActionFlags(GameModel gameModel)
        {
            gameModel.ActionFlags.UndoEnabled = Log.CanUndo;
            gameModel.ActionFlags.NextEnabled = AllowNext(gameModel);
            gameModel.ActionFlags.RollsEnabled = gameModel.GameState == GameState.WaitingForRoll;
        }

        private bool AllowNext(GameModel gameModel)
        {
            List<GameState> NonNextStates = [GameState.WaitingForRoll, GameState.MustMoveRobber];
            if (NonNextStates.Contains(gameModel.GameState)) return false;
            if (gameModel.CurrentPlayer().UnspentEntitlements.Count > 0) return false;
            return true;
        }

        private bool CanTransitionToNext(GameModel gameModel)
        {
            return true; // Simplified for now
        }

        private void LogGameModel(GameModel gameModel)
        {
            SetActionFlags(gameModel);
            gameModel.ActionFlags.RedoEnabled = false;
            Log.Done(gameModel);
        }

        private static void ThrowIfWrongState(GameState currentState, GameState[] validStates)
        {
            if (!validStates.Contains(currentState))
            {
                string validStatesList = string.Join(", ", validStates.Select(vs => vs.ToString()));
                throw new GameException($"{currentState} is invalid. Must be in this set: [{validStatesList}]");
            }
        }

        public SerializableLog GetSerializableLog()
        {
            return Log.GetSerializableLog();
        }
    }
}