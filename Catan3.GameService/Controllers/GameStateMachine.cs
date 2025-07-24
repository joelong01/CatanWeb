using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;

using Catan3.Shared.Models;
using Catan3.Shared.Extensions;
using Catan3.GameService.Services;
using Catan3.GameService.Factory;
using Catan3.GameService.Utility;
using Catan3.Shared.Utility;

namespace Catan3.GameService.Controllers
{
    /// <summary>
    /// Game State Machine - manages all game state transitions and logic
    /// Renamed from GameController to reflect that this is now a pure state machine
    /// without any MVVM/UI dependencies
    /// </summary>
    public class GameStateMachine
    {
        private Log<string> Log;
        private IPersistanceService? MyPersistanceService { get; set; }

        public GameStateMachine(IPersistanceService? persistanceService, string localSaveFile)
        {
            Log = new Log<string>(persistanceService, localSaveFile);
            MyPersistanceService = persistanceService;
        }

        public int DoneCount => Log.DoneCount;

        /// <summary>
        /// Gets the current game state from the log
        /// </summary>
        public GameModel GetCurrentState()
        {
            return Log.CurrentState();
        }

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
                    case GameAction.Balance:
                        gameModel = BalanceBoardAction();
                        LogGameModel(gameModel);
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
                    throw new Catan3.Shared.Utility.GameException($"Unable to do action {message}");
                }
            }
            catch (Catan3.Shared.Utility.GameException e)
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
            catch (Catan3.Shared.Utility.GameException e)
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
            catch (Catan3.Shared.Utility.GameException e)
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
            catch (Catan3.Shared.Utility.GameException e)
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
            catch (Catan3.Shared.Utility.GameException e)
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
            catch (Catan3.Shared.Utility.GameException e)
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
            catch (Catan3.Shared.Utility.GameException e)
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
            catch (Catan3.Shared.Utility.GameException e)
            {
                TraceMessage($"Exception handling roll. Message: {e}");
                throw;
            }
        }

        public GameModel HandleSetPlayerOrder(SetPlayerOrderMessage message)
        {
            try
            {
                var gameModel = SetPlayerOrder(message.PlayerIds);
                LogGameModel(gameModel);
                return gameModel;
            }
            catch (Catan3.Shared.Utility.GameException e)
            {
                TraceMessage($"Exception setting player order. Message: {e}");
                throw;
            }
        }

        public GameModel HandlePlayersDoingSupplemental(PlayersDoingSupplemental message)
        {
            try
            {
                GameModel gameModel = Log.CopyCurrent();
                if (gameModel.GameState != GameState.PickSupplementalPlayers)
                    throw new Catan3.Shared.Utility.GameException("Cannot set supplemental players in current state");

                foreach (var player in gameModel.Players)
                {
                    player.ParticipatingInSupplemental = message.PlayerIds.Contains(player.Id);
                }

                LogGameModel(gameModel);
                return gameModel;
            }
            catch (Catan3.Shared.Utility.GameException e)
            {
                TraceMessage($"Exception setting supplemental players. Message: {e}");
                throw;
            }
        }

        public GameModel HandleBalanceBoard(BalanceBoardMessage message)
        {
            try
            {
                GameModel gameModel = Log.CopyCurrent();
                if (BalanceBoard(gameModel))
                {
                    LogGameModel(gameModel);
                    return gameModel;
                }
                throw new Catan3.Shared.Utility.GameException("Unable to balance board");
            }
            catch (Catan3.Shared.Utility.GameException e)
            {
                TraceMessage($"Exception balancing board. Message: {e}");
                throw;
            }
        }

        public GameModel HandleGoFirst(GoFirstMessage message)
        {
            try
            {
                GameModel gameModel = Log.CopyCurrent();
                if (gameModel.GameState != GameState.FinishedRollOrder)
                    throw new Catan3.Shared.Utility.GameException("Cannot go first in current state");

                while (gameModel.Players[0].Id != message.PlayerId)
                {
                    var player = gameModel.Players[0];
                    gameModel.Players.RemoveAt(0);
                    gameModel.Players.Add(player);
                }
                gameModel.CurrentPlayerId = gameModel.Players[0].Id;
                LogGameModel(gameModel);
                return gameModel;
            }
            catch (Catan3.Shared.Utility.GameException e)
            {
                TraceMessage($"Exception setting go first. Message: {e}");
                throw;
            }
        }

        public async Task HandlePersistGame(PersistGameMessage message)
        {
            try
            {
                switch (message.Action)
                {
                    case LocalPersistActions.Save:
                        await Log.SaveAsync();
                        break;
                    case LocalPersistActions.SaveAs:
                        await Log.SaveAsAsync(message.Location);
                        break;
                    case LocalPersistActions.Open:
                        break;
                }
            }
            catch (Exception e)
            {
                TraceMessage($"Exception persisting game. Message: {e}");
                throw;
            }
        }

        // Helper method for tracing without MVVM dependencies
        private void TraceMessage(string message, [CallerMemberName] string cmb = "", [CallerLineNumber] int cln = 0, [CallerFilePath] string cfp = "")
        {
            Debug.WriteLine($"[{cmb}:{cln}] {message}");
        }

        // All the game logic methods remain the same but with MVVM dependencies removed
        private GameModel OnPurchase(PurchaseMessage message)
        {
            GameModel gameModel = Log.CopyCurrent();
            Entitlement entitlement = message.Entitlement;
            if (entitlement == Entitlement.Soldier)
            {
                ThrowIfWrongState(gameModel.GameState, [GameState.WaitingForNext, GameState.WaitingForRoll]);
                gameModel.GameState = GameState.MustMoveRobber;
            }
            else
            {
                ThrowIfWrongState(gameModel.GameState, [GameState.WaitingForNext, GameState.Supplemental]);
            }
            if (!ValidatePurchase(gameModel, entitlement))
            {
                throw new Catan3.Shared.Utility.GameException($"cannot buy {entitlement} in state {gameModel.GameState}");
            }
            gameModel.CurrentPlayer().UnspentEntitlements.Add(entitlement);
            return gameModel;
        }

        private bool BalanceBoard(GameModel gameModel)
        {
            ThrowIfWrongState(gameModel.GameState, [GameState.PickingBoard]);

            var resourceToCountDictionary = gameModel.Tiles.GroupBy(tile => tile.ResourceTileType)
                .ToDictionary(group => group.Key, group => group.Sum(tile => tile.Stars));

            resourceToCountDictionary.Remove(ResourceType.Desert);

            var minResourceType = resourceToCountDictionary.Aggregate((l, r) => l.Value < r.Value ? l : r).Key;
            var maxResourceType = resourceToCountDictionary.Aggregate((l, r) => l.Value > r.Value ? l : r).Key;
            var minTile = gameModel.Tiles.Where(tileModel => tileModel.ResourceTileType == minResourceType)
                                        .OrderBy(t => t.Stars)
                                        .First();

            var maxTiles = gameModel.Tiles.Where(tileModel => tileModel.ResourceTileType == maxResourceType)
                                        .OrderByDescending(t => t.Stars)
                                        .ToList();

            TraceMessage($"Min Tile: {minTile}");
            minTile.ResourceTileType = maxResourceType;

            foreach (var tile in maxTiles)
            {
                tile.ResourceTileType = minResourceType;
                if (gameModel.ValidateGame())
                {
                    TraceMessage($"max Tile: {tile}");
                    return true;
                }
            }

            TraceMessage("Unable to swap");
            minTile.ResourceTileType = minResourceType;
            return false;
        }

        private bool ValidatePurchase(GameModel gameModel, Entitlement entitlement)
        {
            switch (entitlement)
            {
                case Entitlement.Soldier:
                    if (gameModel.CurrentPlayer().SpentEntitlementsThisTurn.Contains(entitlement)) return false;
                    if (gameModel.CurrentPlayer().UnspentEntitlements.Contains(entitlement)) return false;
                    return true;
                case Entitlement.City:
                    int unspentCities = gameModel.CurrentPlayer().UnspentEntitlements.Count(e => e == entitlement);
                    if (unspentCities + gameModel.CurrentPlayer().SpentEntitlementsThisGame.Count(e => e == entitlement) >= gameModel.ResourceRules.MaxCities) return false;
                    return true;
                case Entitlement.Settlement:
                    int unspentSettlement = gameModel.CurrentPlayer().UnspentEntitlements.Count(e => e == entitlement);
                    if (unspentSettlement + gameModel.CurrentPlayer().SpentEntitlementsThisGame.Count(e => e == entitlement) >= gameModel.ResourceRules.MaxSettlements) return false;
                    return true;
                case Entitlement.Road:
                    int unspentRoads = gameModel.CurrentPlayer().UnspentEntitlements.Count(e => e == entitlement);
                    int spentroads = gameModel.CurrentPlayer().SpentEntitlementsThisGame.Count(e => e == entitlement);
                    if (unspentRoads + spentroads >= gameModel.ResourceRules.MaxRoads) return false;
                    return true;
                default:
                    return false;
            }
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
            if (MyPersistanceService is null) throw new Catan3.Shared.Utility.GameException("no persistance service was set");

            var compressedBytes = await MyPersistanceService.OpenAsync(filePath) ?? throw new Catan3.Shared.Utility.GameException($"Unable to open file {filePath}");
            var decompressedJson = SerializationHelper.Decompress(compressedBytes);
            var savedLog = SerializationHelper.JsonDeserialize<SerializableLog>(decompressedJson) ?? throw new Catan3.Shared.Utility.GameException("Error: Failed to load the game data.");
            Log<string> log = Log<string>.FromSerializableLog(savedLog, MyPersistanceService, filePath);
            this.Log = log;
            return Log.CurrentState();
        }

        private GameModel OnRoll(RollMessage msg)
        {
            GameModel gameModel = Log.CopyCurrent();
            ThrowIfWrongState(gameModel.GameState, [GameState.WaitingForRoll]);
            gameModel.RollModel.TurnRollModel = msg.Roll;
            gameModel.RollModel.GameRollModel.RollCounts[(int)gameModel.RollModel.TurnRollModel.NormalRoll - 2]++;
            gameModel.RollModel.GameRollModel.TotalRolls++;

            gameModel.GameState = GameState.WaitingForNext;
            List<TileModel> highlightedTiles = [];
            foreach (TileModel tile in gameModel.Tiles)
            {
                if (tile.Number == (int)gameModel.RollModel.TurnRollModel.NormalRoll)
                {
                    highlightedTiles.Add(tile);
                    tile.Highlighted = true;
                }
                else
                {
                    tile.Highlighted = false;
                }
            }

            Dictionary<string, ResourcesModel> playerResources = [];
            foreach (var player in gameModel.Players)
            {
                playerResources[player.Id] = new();
            }

            foreach (var tile in highlightedTiles)
            {
                var buildings = gameModel.Buildings.OwnedBuildings(tile.TileKey);
                foreach (BuildingModel building in buildings)
                {
                    Debug.Assert(building.OwnerId is not null, "OwnedBuildings should only return Owned buildings...");
                    ResourceType effectiveType = tile.ResourceTileType;
                    if (tile.TemporarilyGold) effectiveType = ResourceType.GoldMine;
                    if (tile.TileKey == gameModel.Robber.Coordinates)
                    {
                        effectiveType = ResourceType.Robber;
                    }
                    ResourcesModel resources = building.Resources(effectiveType);
                    playerResources[building.OwnerId].Add(resources);
                    if (effectiveType == ResourceType.Robber)
                    {
                        gameModel.Robber.ResourcesStolen += resources.Count;
                    }
                }
            }

            foreach (var player in gameModel.Players)
            {
                var newResources = playerResources[player.Id];
                player.ResourcesThisTurn = newResources;
                player.ResourcesThisGame.Add(newResources);
                gameModel.GameResourcesModel.Add(newResources);
                if (player.ResourcesThisTurn.Count > player.ResourcesThisTurn.Robber)
                {
                    player.GoodRolls++;
                }
                else
                {
                    player.BadRolls++;
                }
            }

            if (msg.Roll.NormalRoll == ValidCatanRoll.Seven)
            {
                gameModel.CurrentPlayer().UnspentEntitlements.Add(Entitlement.RolledSeven);
                gameModel.GameState = GameState.MustMoveRobber;
            }
            return gameModel;
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

        private GameModel SetPlayerOrder(IList<string> playerIds)
        {
            GameModel gameModel = Log.CopyCurrent();
            ThrowIfWrongState(gameModel.GameState, [GameState.WaitingForRollForOrder, GameState.FinishedRollOrder]);
            var playerLookup = gameModel.Players.ToDictionary(p => p.Id);
            List<PlayerModel> orderedPlayers = playerIds
                .Select(id =>
                {
                    if (!playerLookup.TryGetValue(id, out PlayerModel? player))
                    {
                        throw new Catan3.Shared.Utility.GameException($"Invalid playerId {id} found.");
                    }
                    return player;
                })
                .ToList();
            gameModel.Players = orderedPlayers;
            gameModel.CurrentPlayerId = gameModel.Players[0].Id;
            return gameModel;
        }

        private GameModel NextState()
        {
            GameModel gameModel = Log.CopyCurrent();
            if (!CanTransitionToNext(gameModel)) throw new Catan3.Shared.Utility.GameException("Cannot transition to Next state at this time");

            switch (gameModel.GameState)
            {
                case GameState.Uninitialized:
                case GameState.WaitingForNewGame:
                    break;
                case GameState.BeginResourceAllocation:
                    GrantAllocationResources(gameModel);
                    gameModel.GameState = GameState.AllocateResourceForward;
                    break;
                case GameState.WaitingForPlayers:
                    gameModel.GameState = GameState.PickingBoard;
                    break;
                case GameState.PickingBoard:
                    gameModel.GameState = GameState.WaitingForRollForOrder;
                    break;
                case GameState.WaitingForRollForOrder:
                    gameModel.GameState = GameState.FinishedRollOrder;
                    break;
                case GameState.FinishedRollOrder:
                    gameModel.GameState = GameState.BeginResourceAllocation;
                    break;
                case GameState.AllocateResourceForward:
                    if (gameModel.Players.Last().Score == 1)
                    {
                        gameModel.GameState = GameState.AllocateResourceReverse;
                        GrantAllocationResources(gameModel);
                    }
                    else
                    {
                        gameModel.ChangePlayer(1);
                        GrantAllocationResources(gameModel);
                    }
                    break;
                case GameState.AllocateResourceReverse:
                    if (gameModel.CurrentPlayerId == gameModel.Players[0].Id)
                    {
                        gameModel.GameState = GameState.DoneResourceAllocation;
                    }
                    else
                    {
                        gameModel.ChangePlayer(-1);
                        GrantAllocationResources(gameModel);
                    }
                    break;
                case GameState.DoneResourceAllocation:
                    UpdateStateOnNextPlayer(gameModel);
                    SetTempGoldTiles(gameModel);
                    gameModel.GameState = GameState.WaitingForRoll;
                    break;
                case GameState.WaitingForNext:
                    if (gameModel.HasSupplementalBuildPhase)
                    {
                        gameModel.GameState = GameState.PickSupplementalPlayers;
                        gameModel.Players.ForEach(p => p.ParticipatingInSupplemental = false);
                        gameModel.NextPlayerToRollAfterSupplemental = gameModel.NextPlayerId(gameModel.CurrentPlayerId, 1);
                    }
                    else
                    {
                        gameModel.ChangePlayer(1);
                        UpdateStateOnNextPlayer(gameModel);
                    }
                    break;
                default:
                    throw new Catan3.Shared.Utility.GameException($"NextState not implemented for {gameModel.GameState}");
            }

            return gameModel;
        }

        private void UpdateStateOnNextPlayer(GameModel gameModel)
        {
            gameModel.RollModel.TurnRollModel = new();
            gameModel.Players.ForEach(p =>
            {
                p.ResourcesThisTurn = new();
                p.SpentEntitlementsThisTurn = [];
            });
            SetTempGoldTiles(gameModel);
            gameModel.GameState = GameState.WaitingForRoll;
            ResetBuildableRoads(gameModel);
        }

        private void GrantAllocationResources(GameModel gameModel)
        {
            ThrowIfWrongState(gameModel.GameState, [GameState.BeginResourceAllocation, GameState.AllocateResourceForward, GameState.AllocateResourceReverse]);
            var currentPlayer = gameModel.CurrentPlayer();
            currentPlayer.UnspentEntitlements.Add(Entitlement.Settlement);
            currentPlayer.UnspentEntitlements.Add(Entitlement.Road);
        }

        private GameModel RoadPurchase(RoadPurchaseMessage message)
        {
            GameModel gameModel = Log.CopyCurrent();
            ThrowIfWrongState(gameModel.GameState, [GameState.WaitingForNext, GameState.AllocateResourceForward, GameState.AllocateResourceReverse, GameState.Supplemental]);
            ThrowIfNoEntitlement(gameModel, [Entitlement.Road]);
            var roadKey = message.RoadKey;
            var roadModel = gameModel.Roads.FirstOrDefault(r => r.RoadKey == roadKey);
            if (roadModel == null)
            {
                throw new Catan3.Shared.Utility.GameException($"Invalid RoadKey {roadKey}");
            }
            if (roadModel.RoadState != RoadState.Buildable)
            {
                throw new Catan3.Shared.Utility.GameException($"Road {roadModel} is not buildable!");
            }
            if (roadModel.OwnerId != null)
            {
                throw new Catan3.Shared.Utility.GameException($"Don't try to buy other people's roads! Owner: {roadModel.OwnerId}");
            }
            roadModel.OwnerId = gameModel.CurrentPlayerId;
            roadModel.RoadState = RoadState.Road;
            ConsumeEntitlement(gameModel, Entitlement.Road);
            return gameModel;
        }

        private void LogGameModel(GameModel gameModel)
        {
            UpdateScore(gameModel);
            MarkBuildableRoads(gameModel);
            MarkBuildableBuildings(gameModel);
            SetActionFlags(gameModel);
            gameModel.ActionFlags.RedoEnabled = false;
            UpdatePurchaseUi(gameModel);
            SetPlaySoldierAccess(gameModel);
            Log.Done(gameModel);
        }

        private void UpdatePurchaseUi(GameModel gameModel)
        {
            var currentPlayer = gameModel.CurrentPlayer();
            foreach (var epm in gameModel.EntitlementPurchaseModel)
            {
                var spent = currentPlayer.SpentEntitlementsThisGame.Count(e => e == epm.Entitlement);
                var unspent = currentPlayer.UnspentEntitlements.Count(e => e == epm.Entitlement);
                if (spent + unspent == gameModel.ResourceRules.MaxEntitlementCount(epm.Entitlement))
                {
                    epm.Enabled = false;
                }
            }
        }

        private void SetPlaySoldierAccess(GameModel gameModel)
        {
            var moveRobber = gameModel.PurchaseModel(Entitlement.Soldier);
            if (gameModel.GameState != GameState.WaitingForNext && gameModel.GameState != GameState.WaitingForRoll)
            {
                moveRobber.Enabled = false;
                return;
            }
            PlayerModel currentPlayer = gameModel.CurrentPlayer();
            if (currentPlayer.SpentEntitlementsThisTurn.Contains(Entitlement.Soldier) || currentPlayer.UnspentEntitlements.Contains(Entitlement.Soldier))
            {
                moveRobber.Enabled = false;
                return;
            }
            moveRobber.Enabled = true;
        }

        private void ThrowIfNoEntitlement(GameModel gameModel, Entitlement[] entitlements)
        {
            var currentPlayer = gameModel.CurrentPlayer();
            if (!entitlements.Any(e => currentPlayer.UnspentEntitlements.Contains(e)))
            {
                throw new Catan3.Shared.Utility.GameException($"{currentPlayer.Id} does not have the required entitlement.");
            }
        }

        private GameModel BuildingUpgrade(BuildingUpgradeMessage message)
        {
            Console.WriteLine($"[DEBUG] BuildingUpgrade called with BuildingKey: {message.BuildingKey}");
            
            GameModel gameModel = Log.CopyCurrent();
            Console.WriteLine($"[DEBUG] Current game state: {gameModel.GameState}");
            Console.WriteLine($"[DEBUG] Current player: {gameModel.CurrentPlayerId}");
            
            ThrowIfWrongState(gameModel.GameState, [GameState.WaitingForNext, GameState.AllocateResourceForward, GameState.AllocateResourceReverse, GameState.Supplemental]);
            BuildingKey buildingKey = message.BuildingKey;
            
            var building = gameModel.Buildings.FindBuildingModel(buildingKey);
            if (building == null)
            {
                Console.WriteLine($"[DEBUG] Building not found for key: {buildingKey}");
                throw new Catan3.Shared.Utility.GameException($"Invalid BuildingKey: {buildingKey}");
            }
            
            Console.WriteLine($"[DEBUG] Found building: {building.BuildingKey}, State: {building.BuildingState}");
            
            if (building.BuildingState == BuildingState.NotBuildable)
            {
                throw new Catan3.Shared.Utility.GameException($"{building} is not buildingable.");
            }

            switch (building.BuildingState)
            {
                case BuildingState.PossibleSettlement:
                    Console.WriteLine($"[DEBUG] Upgrading PossibleSettlement to Settlement");
                    ThrowIfNoEntitlement(gameModel, [Entitlement.Settlement]);
                    building.BuildingState = BuildingState.Settlement;
                    building.OwnerId = gameModel.CurrentPlayerId;
                    ConsumeEntitlement(gameModel, Entitlement.Settlement);
                    HarborModel? adjacentHarbor = gameModel.FindAdjacentHarbor(building.BuildingKey);
                    if (adjacentHarbor is not null)
                    {
                        var currentPlayer = gameModel.CurrentPlayer();
                        adjacentHarbor.Owner = currentPlayer;
                        currentPlayer.OwnedHarbors.Add(adjacentHarbor.HarborKey);
                        TraceMessage($"{adjacentHarbor} now owned by {currentPlayer}");
                    }
                    break;
                case BuildingState.Settlement:
                    ThrowIfNoEntitlement(gameModel, [Entitlement.City]);
                    if (building.OwnerId != gameModel.CurrentPlayerId)
                    {
                        throw new Catan3.Shared.Utility.GameException($"Don't try to upgrade somebody else's building: {building.OwnerId}");
                    }
                    building.BuildingState = BuildingState.City;
                    ConsumeEntitlement(gameModel, Entitlement.City);
                    gameModel.CurrentPlayer().SpentEntitlementsThisGame.Remove(Entitlement.Settlement);
                    break;
                case BuildingState.City:
                    ThrowIfNoEntitlement(gameModel, [Entitlement.BuyKnight]);
                    if (building.OwnerId != gameModel.CurrentPlayerId)
                    {
                        throw new Catan3.Shared.Utility.GameException($"Don't try to upgrade somebody else's building: {building.OwnerId}");
                    }
                    building.BuildingState = BuildingState.Knight;
                    ConsumeEntitlement(gameModel, Entitlement.BuyKnight);
                    break;
                case BuildingState.Knight:
                    throw new Catan3.Shared.Utility.GameException("Knights cannot be upgraded further.");
            }

            Console.WriteLine($"[DEBUG] About to check if gameModel.GameState == GameState.AllocateResourceReverse");
            Console.WriteLine($"[DEBUG] gameModel.GameState = {gameModel.GameState}");
            Console.WriteLine($"[DEBUG] GameState.AllocateResourceReverse = {GameState.AllocateResourceReverse}");
            Console.WriteLine($"[DEBUG] Equality check: {gameModel.GameState == GameState.AllocateResourceReverse}");

            if (gameModel.GameState == GameState.AllocateResourceReverse)
            {
                var currentPlayerModel = gameModel.CurrentPlayer();
                Console.WriteLine($"[DEBUG] AllocateResourceReverse: Processing resources for {currentPlayerModel.Id}");
                
                var tilesForBuilding = gameModel.TilesForBuildings(building.BuildingKey);
                Console.WriteLine($"[DEBUG] TilesForBuildings returned {tilesForBuilding.Count} tiles for building {building.BuildingKey}");
                
                foreach (var tile in tilesForBuilding)
                {
                    Console.WriteLine($"[DEBUG] Processing tile {tile.TileKey} with resource type {tile.ResourceTileType}");
                    ResourcesModel resources = building.Resources(tile.ResourceTileType);
                    Console.WriteLine($"[DEBUG] Building.Resources({tile.ResourceTileType}) returned: Wheat={resources.Wheat}, Wood={resources.Wood}, Sheep={resources.Sheep}, Brick={resources.Brick}, Ore={resources.Ore}, GoldMine={resources.GoldMine}, Fish={resources.Fish}");
                    
                    currentPlayerModel.ResourcesThisTurn.Add(resources);
                    Console.WriteLine($"[DEBUG] After adding resources, player {currentPlayerModel.Id} ResourcesThisTurn: Wheat={currentPlayerModel.ResourcesThisTurn.Wheat}, Wood={currentPlayerModel.ResourcesThisTurn.Wood}, Sheep={currentPlayerModel.ResourcesThisTurn.Sheep}, Brick={currentPlayerModel.ResourcesThisTurn.Brick}, Ore={currentPlayerModel.ResourcesThisTurn.Ore}");
                }
                
                Console.WriteLine($"[DEBUG] Final ResourcesThisTurn for {currentPlayerModel.Id}: Wheat={currentPlayerModel.ResourcesThisTurn.Wheat}, Wood={currentPlayerModel.ResourcesThisTurn.Wood}, Sheep={currentPlayerModel.ResourcesThisTurn.Sheep}, Brick={currentPlayerModel.ResourcesThisTurn.Brick}, Ore={currentPlayerModel.ResourcesThisTurn.Ore}");
            }
            else
            {
                Console.WriteLine($"[DEBUG] NOT in AllocateResourceReverse state, skipping resource allocation");
            }

            Console.WriteLine($"[DEBUG] BuildingUpgrade completed successfully");
            return gameModel;
        }

        private void ConsumeEntitlement(GameModel gameModel, Entitlement entitlement)
        {
            var currentPlayer = gameModel.CurrentPlayer();
            currentPlayer.UnspentEntitlements.Remove(entitlement);
            currentPlayer.SpentEntitlementsThisTurn.Add(entitlement);
            currentPlayer.SpentEntitlementsThisGame.Add(entitlement);
        }

        private void UpdateScore(GameModel gameModel)
        {
            int maxScore = 0;
            CalculateLongestRoad(gameModel);

            int maxSoldierCount = gameModel.Players
                .Select(player => player.SpentEntitlementsThisGame.Count(e => e == Entitlement.Soldier))
                .DefaultIfEmpty(0)
                .Max();

            var playerWithLargestArmy = gameModel.Players.FirstOrDefault(player => player.LargestArmy);

            foreach (var player in gameModel.Players)
            {
                player.HighestScore = false;
                int citiesPlayed = player.SpentEntitlementsThisGame.Count(e => e == Entitlement.City);
                int settlementsPlayed = player.SpentEntitlementsThisGame.Count(e => e == Entitlement.Settlement);
                int knightsPlayed = player.SpentEntitlementsThisGame.Count(e => e == Entitlement.Soldier);
                if (maxSoldierCount > 2)
                {
                    if (knightsPlayed == maxSoldierCount && playerWithLargestArmy is null)
                    {
                        TraceMessage($"{player} has largest army");
                        player.LargestArmy = true;
                        playerWithLargestArmy = player;
                    }
                    else if (playerWithLargestArmy is not null && knightsPlayed > playerWithLargestArmy.SpentEntitlementsThisGame.Count(e => e == Entitlement.Soldier))
                    {
                        TraceMessage($"{player} took largest army from {playerWithLargestArmy}");
                        playerWithLargestArmy.LargestArmy = false;
                        player.LargestArmy = true;
                        playerWithLargestArmy = player;
                    }
                    if (playerWithLargestArmy?.Id == player.Id)
                    {
                        player.LargestArmy = true;
                    }
                    else
                    {
                        player.LargestArmy = false;
                    }
                }
                else
                {
                    player.LargestArmy = false;
                }

                int score = citiesPlayed * 2 + settlementsPlayed;
                if (player.HasLongestRoad)
                {
                    score += 2;
                }
                if (player.LargestArmy)
                {
                    score += 2;
                }
                player.Score = score;
                if (maxScore < player.Score) maxScore = player.Score;
            }
            foreach (var player in gameModel.Players)
            {
                player.HighestScore = (player.Score == maxScore);
            }
        }

        private GameModel MoveRobber(MoveRobberMessage moveRobber)
        {
            GameModel gameModel = Log.CopyCurrent();
            ThrowIfWrongState(gameModel.GameState, [GameState.MustMoveRobber]);
            ThrowIfBadPlayer(gameModel.CurrentPlayerId, gameModel.Players);
            ThrowIfNoEntitlement(gameModel, [Entitlement.Soldier, Entitlement.RolledSeven]);

            gameModel.Robber.Coordinates = moveRobber.Coordinates;
            gameModel.Robber.MovedBy = gameModel.CurrentPlayerId;
            if (moveRobber.TargetPlayerId is not null)
            {
                var target = gameModel.Players.PlayerFromId(moveRobber.TargetPlayerId) ?? throw new Catan3.Shared.Utility.GameException($"TargetPlayerId {moveRobber.TargetPlayerId} is invalid");
                target.TimesTargeted++;
            }

            if (gameModel.CurrentPlayer().UnspentEntitlements.Contains(Entitlement.Soldier))
            {
                Debug.Assert(gameModel.CurrentPlayer().UnspentEntitlements.Contains(Entitlement.RolledSeven) == false);
                ConsumeEntitlement(gameModel, Entitlement.Soldier);
                gameModel.GameState = gameModel.PreviousGameState;
            }
            else
            {
                Debug.Assert(gameModel.CurrentPlayer().UnspentEntitlements.Contains(Entitlement.RolledSeven));
                ConsumeEntitlement(gameModel, Entitlement.RolledSeven);
                gameModel.GameState = GameState.WaitingForNext;
            }

            gameModel.Robber.ResourcesStolen = 0;
            return gameModel;
        }

        private static void ThrowIfWrongState(GameState currentState, GameState[] validStates)
        {
            if (!validStates.Contains(currentState))
            {
                string validStatesList = string.Join(", ", validStates.Select(vs => vs.ToString()));
                throw new Catan3.Shared.Utility.GameException($"{currentState} is invalid. Must be in this set: [{validStatesList}]");
            }
        }

        private static void ThrowIfBadPlayer(string playerId, IList<PlayerModel> players)
        {
            if (!players.Any(p => p.Id == playerId))
            {
                throw new Catan3.Shared.Utility.GameException($"Bad CurrentPlayerId: {playerId}");
            }
        }

        private bool CanTransitionToNext(GameModel gameModel)
        {
            // Use the AllowNext method which contains the proper logic for determining
            // if the Next action should be enabled for the current game state
            return AllowNext(gameModel);
        }

        private void SetTempGoldTiles(GameModel gameModel)
        {
            try
            {
                if (gameModel.HouseRules.GoldTiles == 0) return;
                if (gameModel.Tiles is null) throw new Catan3.Shared.Utility.GameException("Tiles is null");

                HashSet<HexCoordinates> previouslyGoldTiles = new();
                foreach (var tile in gameModel.Tiles)
                {
                    if (tile.TemporarilyGold)
                    {
                        previouslyGoldTiles.Add(tile.TileKey);
                        tile.TemporarilyGold = false;
                    }
                }

                var rand = new Random();
                HashSet<int> usedIndices = new();

                while (usedIndices.Count < gameModel.HouseRules.GoldTiles)
                {
                    int index = rand.Next(gameModel.Tiles.Count);
                    var tileModel = gameModel.Tiles[index];

                    if (previouslyGoldTiles.Contains(tileModel.TileKey) ||
                        tileModel.ResourceTileType == ResourceType.Desert ||
                        usedIndices.Contains(index))
                    {
                        continue;
                    }

                    tileModel.TemporarilyGold = true;
                    usedIndices.Add(index);
                }
            }
            finally
            {
#if DEBUG
                int goldCount = gameModel.Tiles.Count(t => t.TemporarilyGold);
                Debug.Assert(goldCount == gameModel.HouseRules.GoldTiles, "The number of gold tiles does not match the expected value.");
#endif
            }
        }

        private GameModel ShuffleCurrentGame()
        {
            GameModel gameModel = Log.CopyCurrent();
            ThrowIfWrongState(gameModel.GameState, [GameState.PickingBoard]);
            gameModel.Shuffle();
            return gameModel;
        }

        private GameModel BalanceBoardAction()
        {
            GameModel gameModel = Log.CopyCurrent();
            if (BalanceBoard(gameModel))
            {
                return gameModel;
            }
            throw new Catan3.Shared.Utility.GameException("Unable to balance board");
        }

        public SerializableLog GetSerializableLog()
        {
            return Log.GetSerializableLog();
        }

        private GameModel? Undo()
        {
            GameModel result = Log.Undo() ?? throw new Catan3.Shared.Utility.GameException("Undo cannot be done");
            SetActionFlags(result);
            result.ActionFlags.RedoEnabled = true;
            return result;
        }

        private GameModel? Redo()
        {
            GameModel result = Log.Redo() ?? throw new Catan3.Shared.Utility.GameException("Redo cannot be done");
            SetActionFlags(result);
            return result;
        }

        private void ResetBuildableRoads(GameModel gameModel)
        {
            foreach (var road in gameModel.Roads)
            {
                if (road.RoadState == RoadState.Buildable)
                {
                    road.RoadState = RoadState.Unowned;
                }
                road.BuildIndex = 0;
            }
        }

        private void MarkBuildableRoads(GameModel gameModel)
        {
            ResetBuildableRoads(gameModel);
            List<RoadModel> buildableRoads = [];
            if (gameModel.Phase() == GamePhase.Purchase)
            {
                foreach (var road in gameModel.Roads)
                {
                    if (road.OwnerId == gameModel.CurrentPlayerId)
                    {
                        var adjacentRoads = gameModel.Roads.AdjacentRoads(road.RoadKey);
                        foreach (var r in adjacentRoads)
                        {
                            if (r.OwnerId is null && !buildableRoads.Contains(r))
                            {
                                buildableRoads.InsertSorted(r);
                            }
                        }
                    }
                }
            }
            foreach (var building in gameModel.Buildings)
            {
                if (building.OwnerId == gameModel.CurrentPlayerId)
                {
                    if (gameModel.Phase() == GamePhase.PickingResources)
                    {
                        var ownedRoads = gameModel.AdjacentRoads(building.BuildingKey).Where(r => r.OwnerId == gameModel.CurrentPlayerId).ToList();
                        if (ownedRoads.Count == 0)
                        {
                            buildableRoads.AddRange(gameModel.AdjacentRoads(building.BuildingKey));
                        }
                        continue;
                    }
                    var roads = gameModel.AdjacentRoads(building.BuildingKey);
                    foreach (var adjacentRoad in roads)
                    {
                        if (adjacentRoad.OwnerId is null && !buildableRoads.Contains(adjacentRoad))
                        {
                            buildableRoads.InsertSorted(adjacentRoad);
                        }
                    }
                }
            }

            if (buildableRoads.Count == 0)
            {
                gameModel.PurchaseModel(Entitlement.Road).Enabled = false;
                return;
            }

            gameModel.PurchaseModel(Entitlement.Road).Enabled = gameModel.Phase() == GamePhase.Purchase;
            if (gameModel.CurrentPlayer().UnspentEntitlements.Contains(Entitlement.Road))
            {
                for (int i = 0; i < buildableRoads.Count; i++)
                {
                    var road = buildableRoads[i];
                    road.RoadState = RoadState.Buildable;
                    road.BuildIndex = i + 1;
                }
            }
        }

        private void MarkBuildableBuildings(GameModel gameModel)
        {
            gameModel.Buildings
                     .Where(b => b.BuildingState == BuildingState.PossibleSettlement)
                     .ToList()
                     .ForEach(b => b.BuildingState = BuildingState.NotBuildable);
            var currentPlayer = gameModel.CurrentPlayer();
            bool hasCity = currentPlayer.UnspentEntitlements.Contains(Entitlement.City);
            bool hasSettlement = currentPlayer.UnspentEntitlements.Contains(Entitlement.Settlement);
            List<BuildingModel> buildableCities = [];
            List<BuildingModel> buildableSettlements = [];

            foreach (var building in gameModel.Buildings)
            {
                if (building.BuildingState == BuildingState.City) continue;

                if (building.BuildingState == BuildingState.Settlement)
                {
                    if (building.OwnerId == currentPlayer.Id)
                    {
                        buildableCities.Add(building);
                    }
                    continue;
                }
                var ownedAdjacentBuildings = gameModel.Buildings.AdjacentBuildings(building.BuildingKey).Where(b => b.OwnerId != null).ToList();
                if (ownedAdjacentBuildings.Count == 0)
                {
                    if (building.OwnerId is null && (gameModel.Phase() == GamePhase.PickingResources || gameModel.Phase() == GamePhase.PickingBoard))
                    {
                        buildableSettlements.Add(building);
                        continue;
                    }
                    var adjacentRoads = gameModel.AdjacentRoads(building.BuildingKey);
                    foreach (var road in adjacentRoads)
                    {
                        if (road.OwnerId == currentPlayer.Id && building.OwnerId == null)
                        {
                            buildableSettlements.Add(building);
                            break;
                        }
                    }
                }
            }
            if (buildableCities.Count > 0 && gameModel.Phase() == GamePhase.Purchase && AllowPurchase(gameModel, Entitlement.City))
            {
                gameModel.PurchaseModel(Entitlement.City).Enabled = true;
            }
            else
            {
                gameModel.PurchaseModel(Entitlement.City).Enabled = false;
            }
            int unspentSettlements = currentPlayer.UnspentEntitlements.Count(e => e == Entitlement.Settlement);
            if (buildableSettlements.Count > unspentSettlements && gameModel.Phase() == GamePhase.Purchase && AllowPurchase(gameModel, Entitlement.Settlement))
            {
                gameModel.PurchaseModel(Entitlement.Settlement).Enabled = true;
            }
            else
            {
                gameModel.PurchaseModel(Entitlement.Settlement).Enabled = false;
            }
            if (hasSettlement || gameModel.Phase() == GamePhase.PickingResources || gameModel.Phase() == GamePhase.PickingBoard)
            {
                buildableSettlements.ForEach(building => { building.BuildingState = BuildingState.PossibleSettlement; });
            }
        }

        private static bool AllowPurchase(GameModel gameModel, Entitlement entitlement)
        {
            var currentPlayer = gameModel.CurrentPlayer();
            int total = currentPlayer.SpentEntitlementsThisGame.Count(e => e == entitlement) +
                        currentPlayer.UnspentEntitlements.Count(e => e == entitlement);
            return entitlement switch
            {
                Entitlement.Road => (total < gameModel.ResourceRules.MaxRoads),
                Entitlement.Settlement => (total < gameModel.ResourceRules.MaxSettlements),
                Entitlement.City => (total < gameModel.ResourceRules.MaxCities),
                Entitlement.Soldier => true,
                _ => throw new Exception($"TODO: add support for {entitlement} to AllowPurchase"),
            };
        }

        public void CalculateLongestRoad(GameModel gameModel)
        {
            var longestRoadAllPlayers = 0;
            foreach (var player in gameModel.Players)
            {
                var playerRoads = gameModel.Roads.Where(r => r.OwnerId == player.Id).ToList();
                int max = 0;
                foreach (var startRoad in playerRoads)
                {
                    int count = CalculateLongestRoad(gameModel, startRoad, [], null);
                    if (count > max)
                    {
                        max = count;
                        if (max == gameModel.Roads.Count)
                        {
                            break;
                        }
                    }
                }
                player.LongestRoad = max;
                if (max > longestRoadAllPlayers)
                {
                    longestRoadAllPlayers = max;
                }
            }
            var playerWithLongestRoad = gameModel.Players.FirstOrDefault(p => p.HasLongestRoad);
            foreach (var player in gameModel.Players)
            {
                if (player.LongestRoad < 5)
                {
                    player.HasLongestRoad = false;
                    continue;
                }
                if (player.LongestRoad < longestRoadAllPlayers)
                {
                    player.HasLongestRoad = false;
                    continue;
                }
                if (player.LongestRoad == longestRoadAllPlayers && playerWithLongestRoad is null)
                {
                    player.HasLongestRoad = true;
                    playerWithLongestRoad = player;
                    continue;
                }
                if (player.LongestRoad == longestRoadAllPlayers && playerWithLongestRoad is not null && playerWithLongestRoad.LongestRoad < player.LongestRoad)
                {
                    player.HasLongestRoad = true;
                    playerWithLongestRoad.HasLongestRoad = false;
                    playerWithLongestRoad = player;
                }
            }
        }

        private int CalculateLongestRoad(GameModel gameModel, RoadModel start, List<RoadModel> counted, RoadModel? blockedFork)
        {
            int count = 1;
            int max = 1;
            counted.Add(start); // it is counted in the "max=1" above
            RoadModel next = start;
            List<RoadModel> ownedAdjacentNotCounted = gameModel.OwnedAdjacentRoadsNotCounted(next, counted, blockedFork, out bool adjacentFork);
            do
            {
                switch (ownedAdjacentNotCounted.Count)
                {
                    case 0:
                        return max;
                    case 1:
                        {
                            count++;
                            next = ownedAdjacentNotCounted[0];
                            counted.Add(next);                  // we counted it, add it to the counted list.
                            if (count > max)
                            {
                                max = count;
                            }
                            ownedAdjacentNotCounted = gameModel.OwnedAdjacentRoadsNotCounted(next, counted, blockedFork, out adjacentFork);
                            if (adjacentFork)
                            {
                                //ah...the loop
                                count++;
                                counted.Add(next); // we shouldn't have to do this more than once
                                if (count > max)
                                {
                                    max = count;
                                }
                                return max;
                            }
                        }
                        //
                        //  loop to the next road to see if it terminates, forks, or just continues...
                        break;
                    default:
                        //
                        //   general strategy:  for each fork in the road, pretend that all but one of the forks are already counted
                        //                      then count the remaining one.  after that, pick another to be counted
                        //                      because we "count" the entered line, there are only ever 2 forks in the road
                        // ownedAdjacentNotCounted.Count > 1
                        //  usually there means there is a fork like this
                        //                           /
                        //                          /    <=== fork1
                        //                         /
                        //                  ------     <=== always counted
                        //                         \
                        //                          \   <=== Fork 2
                        //                           \
                        //  if we ever get this or the equivalent:
                        //
                        //                           /
                        //                          /    <=== fork1
                        //                         /
                        //                  ------     <=== always counted
                        //                /        \
                        //   Fork 3 -->  /          \   <=== Fork 2
                        //              /            \
                        //
                        //  e.g the adjacent count is > 2 then the road with all the forks around it (the horizontal in ascii art) doesn't have to be counted because we'll count all the
                        //  roads coming into that fork
                        List<RoadModel> forks = [.. ownedAdjacentNotCounted];
                        if (forks.Count > 2)
                        {
                            //
                            //  if the fork count is not 2 then that means we are in a middle segment, and we don't need to start there
                            return max;
                        }
                        foreach (RoadModel road in ownedAdjacentNotCounted)
                        {
                            forks.Remove(road);// now the list has everything except this one road...so we've effectively picked a direction
                            int forkCount = CalculateLongestRoad(gameModel, road, counted, forks[0]); // --> only one element in the forks list at this point
                            if (count + forkCount > max)
                            {
                                max = count + forkCount;
                            }
                            forks.Add(road); // put fork back so we can count that fork
                        }
                        return max;
                }
            } while (ownedAdjacentNotCounted.Count != 0);
            return max;
        }
    }
}
