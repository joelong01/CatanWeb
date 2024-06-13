using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using Catan10.Models;
using Catan3.Models;
using Catan3.Utility;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Messaging;
namespace Catan3.Controller
{
    public class GameController : ObservableRecipient
    {
        private Log<string> Log = new();
        private  GameType GameType = GameType.Unset;
        public GameController()
        {
            RegisterMessages();
        }
        public int DoneCount => Log.DoneCount;
        private void RegisterMessages()
        {
            Debug.Assert(Messenger is not null);
            IsActive = true;
            Messenger.Register<DoAction>(this, (recipient, message) =>
                {
                    try
                    {
                        GameModel? gameModel = null;
                        switch (message.Action)
                        {
                            case GameAction.Shuffle:
                                gameModel = ShuffleCurrentGame();
                                break;
                            case GameAction.Undo:
                                gameModel = Undo();
                                break;
                            case GameAction.Redo:
                                gameModel = Redo();
                                break;
                            case GameAction.Next:
                                gameModel = NextState();
                                break;
                        }
                        if (gameModel is not null)
                        {
                            Messenger.Send(new UpdateGameModel(gameModel));
                        }
                        else
                        {
                            throw new GameException($"Unable to do action {message}");
                        }
                    }
                    catch (GameException e)
                    {
                        this.TraceMessage($"Exception doing Action {message.Action}. Message: {e}");
                    }
                });
            Messenger.Register<BuildingUpgradeMessage>(this, (recipient, message) =>
                {
                    try
                    {
                        var model = BuildingUpgrade(message);
                        Messenger.Send(new UpdateGameModel(model));
                    }
                    catch (GameException e)
                    {
                        SendErrorMessage(e.Message, e.ErrorLevel);
                    }
                });
            Messenger.Register<SetPlayerOrderMessage>(this, (recipient, message) =>
            {
                try
                {
                    var model = SetPlayerOrder(message.PlayerIds);
                    Messenger.Send(new UpdateGameModel(model));
                }
                catch (GameException e)
                {
                    SendErrorMessage(e.Message, e.ErrorLevel);
                }
            });
            Messenger.Register<RoadPurchaseMessage>(this, (recipient, message) =>
                {
                    try
                    {
                        var model = RoadPurchase(message);
                        Messenger.Send(new UpdateGameModel(model));
                    }
                    catch (GameException e)
                    {
                        SendErrorMessage(e.Message, e.ErrorLevel);
                    }
                });
            Messenger.Register<MoveRobberMessage>(this, (recipient, message) =>
             {
                 try
                 {
                     var model = MoveRobber(message);
                     Messenger.Send(new UpdateGameModel(model));
                 }
                 catch (GameException e)
                 {
                     SendErrorMessage(e.Message, e.ErrorLevel);
                 }
             });
            Messenger.Register<NewGameMessage>(this, (recipient, message) =>
            {
                try
                {
                    var model = NewGame(message.GameType, message.PlayerIds);
                    Messenger.Send(new UpdateGameModel(model));
                }
                catch (GameException e)
                {
                    SendErrorMessage(e.Message, e.ErrorLevel);
                }
            });
            Messenger.Register<RollMessage>(this, (recipient, message) =>
            {
                try
                {
                    var model = OnRoll(message);
                    Messenger.Send(new UpdateGameModel(model));
                }
                catch (GameException e)
                {
                    SendErrorMessage(e.Message, e.ErrorLevel);
                }
            });
            Messenger.Register<PurchaseMessage>(this, (recipient, message) =>
            {
                try
                {
                    var model = OnPurchase(message);
                    Messenger.Send(new UpdateGameModel(model));
                }
                catch (GameException e)
                {
                    SendErrorMessage(e.Message, e.ErrorLevel);
                }
            });
            Messenger.Register<EndGame>(this, (recipient, message) =>
            {
                Messenger.UnregisterAll(this);
            });
            Messenger.Register<GoFirstMessage>(this, (recipient, message) =>
            {
                GameModel gameModel = Log.CopyCurrent();
                if (gameModel.GameState != GameState.FinishedRollOrder) return;
                while (gameModel.Players[0].Id != message.PlayerId)
                {
                    var player = gameModel.Players[0];
                    gameModel.Players.RemoveAt(0);
                    gameModel.Players.Add(player);
                }
                gameModel.CurrentPlayerId = gameModel.Players[0].Id;
                LogDone(gameModel);
                Messenger.Send(new UpdateGameModel(gameModel));
            });
        }
        private void SendErrorMessage(string message, ErrorLevel errorLevel, int indentLevel = 0, [CallerMemberName] string cmb = "", [CallerLineNumber] int cln = 0, [CallerFilePath] string cfp = "")
        {
            this.TraceMessage(errorLevel.ToString() + ": " + message, indentLevel, cmb, cln, cfp);
            Messenger.Send(new ErrorMessage(message, errorLevel, cmb, cln, cfp));
        }
        private GameModel OnPurchase(PurchaseMessage message)
        {
            GameModel gameModel = Log.CopyCurrent();
            Entitlement entitlement = message.Entitlement;
            if (entitlement == Entitlement.Soldier)
            {
                // the entitlements you can get before rolling -- right now only the right to move the knight
                ThrowIfWrongState(gameModel.GameState, [GameState.WaitingForNext, GameState.WaitingForRoll]);
                gameModel.GameState = GameState.MustMoveRobber;
            }
            else
            {
                ThrowIfWrongState(gameModel.GameState, [GameState.WaitingForNext]);
            }
            if (!ValidatePurchase(gameModel, entitlement))
            {
                throw new GameException($"cannot buy {entitlement} in state {gameModel.GameState}");
            }
            gameModel.CurrentPlayer().UnspentEntitlements.Add(entitlement);
            LogDone(gameModel);
            return gameModel;
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
                    int unspentCities = gameModel.CurrentPlayer().UnspentEntitlements.Count( e => e == entitlement );
                    if (unspentCities + gameModel.CurrentPlayer().SpentEntitlementsThisGame.Count(e => e == entitlement) >= gameModel.ResourceRules.MaxCities) return false;
                    return true;
                case Entitlement.Settlement:
                    int unspentSettlement = gameModel.CurrentPlayer().UnspentEntitlements.Count( e => e == entitlement );
                    if (unspentSettlement + gameModel.CurrentPlayer().SpentEntitlementsThisGame.Count(e => e == entitlement) >= gameModel.ResourceRules.MaxSettlements) return false;
                    return true;
                case Entitlement.Road:
                    int unspentRoads = gameModel.CurrentPlayer().UnspentEntitlements.Count( e => e == entitlement );
                    int spentroads = gameModel.CurrentPlayer().SpentEntitlementsThisGame.Count(e => e == entitlement);
                    if (unspentRoads + spentroads >= gameModel.ResourceRules.MaxRoads) return false;
                    return true;
                default:
                    return false;
            }
        }
        public GameModel NewGame(GameType selectedGame, List<string> playerIds)
        {
            var gameModel = GameFactory.CreateGame(selectedGame, playerIds);
            Log.GameType = selectedGame;
            gameModel.GameType = selectedGame;
            gameModel.GameState = GameState.PickingBoard;
            LogDone(gameModel);
            return gameModel;
        }
        /// <summary>
        ///     when a roll comes in 
        ///     . make sure that we are ready for a roll
        ///     . update the game state to reflect the roll
        ///     . change the game state
        ///     . highlight the tiles
        ///     . calculate the resources for each player
        /// </summary>
        /// <param name="roll"></param>
        /// <exception cref="GameException"></exception>
        private GameModel OnRoll(RollMessage msg)
        {
            GameModel gameModel = Log.CopyCurrent();
            ThrowIfWrongState(gameModel.GameState, [GameState.WaitingForRoll]);
            TurnRollModel roll = msg.Roll;
            // update the global counts for rolls
            gameModel.GameRollModel.RollCounts[( int )roll.NormalRoll - 2]++;
            gameModel.GameRollModel.TotalRolls++;
            // update the state
            gameModel.GameState = GameState.WaitingForNext;
            // highlight the tiles and build a list of tiles that have this number
            List<TileModel> highlightedTiles = [];
            foreach (TileModel tile in gameModel.Tiles)
            {
                if (tile.Number == ( int )roll.NormalRoll)
                {
                    highlightedTiles.Add(tile);
                    tile.Highlighted = true;
                }
                else
                {
                    tile.Highlighted = false;
                }
            }
            //
            // calculate resources based on the tiles that are highlighted (which we just set)
            // i'm doing this in a dictionary to keep the map playerid->ResoruceModel as we 
            // need to collect the total amount of resources for all the building/tiles
            Dictionary<string, ResourcesModel> playerResources = [];
            foreach (var player in gameModel.Players)
            {
                playerResources[player.Id] = new();
            }
            //
            //  go through and poplulate the ResourceModel with the resources won for the roll
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
            // now fix up the underlying resource models in the same way as if we loading it from disk or got it back from a service
            // -- e.g. create new data objects and stick the full object into the model
            foreach (var player in gameModel.Players)
            {
                var newResources =  playerResources[player.Id];
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
            //
            //  if they rolled 7...
            if (msg.Roll.NormalRoll == ValidCatanRoll.Seven)
            {
                gameModel.CurrentPlayer().UnspentEntitlements.Add(Entitlement.RolledSeven);
                gameModel.GameState = GameState.MustMoveRobber;
            }
            // save our changes to the GameModel to the log
            LogDone(gameModel);
            return gameModel;
        }
        /// <summary>
        ///     set the "Enable*" (e.g. EnableUndo, EnableNext, EnableRedo) ...Undo and Redo are functions of the log
        ///     and the ViewModels don't have that information, only the log does.  EnableNext is a function of other 
        ///     state in the GameModel that the State machine has to know anyway (in the Next() call, so here we are
        ///     just caching the answer so that the client won't waste time calling it.
        /// </summary>
        /// <param name="gameModel"></param>
        private void SetActionFlags(GameModel gameModel)
        {
            gameModel.ActionFlags.UndoEnabled = Log.CanUndo;
            gameModel.ActionFlags.NextEnabled = AllowNext(gameModel);
            gameModel.ActionFlags.RollsEnabled = gameModel.GameState == GameState.WaitingForRoll;
        }
        private bool AllowNext(GameModel gameModel)
        {
            if (gameModel.GameState == GameState.WaitingForRoll) return false;
            if (gameModel.CurrentPlayer().UnspentEntitlements.Count > 0) return false;
            return true;
        }
        /// <summary>
        /// Reorders the players in the game model to match a specified order of player IDs.
        /// </summary>
        /// <param name="playerIds">A list of player IDs representing the desired order of players.</param>
        /// <returns>A new instance of GameModel with players reordered according to playerIds,
        /// or throws an GameException if any player ID in playerIds does not exist in the game model.</returns>
        /// <remarks>
        /// This function performs the reordering with the following steps:
        /// 1. Creates a copy of the current game model to ensure that changes do not affect the original state.
        /// 2. Constructs a dictionary from the current list of players for O(1) lookup time, using player IDs as keys.
        /// 3. Iterates over the list of desired player IDs, using the dictionary to quickly map IDs to PlayerModel instances.
        /// 4. Collects these mapped PlayerModel instances into a new list that reflects the desired order.
        /// 5. Assigns this newly ordered list back to the players property of the game model.
        /// 6. sets the CurrentPlayerId to be the first PlayerId in the collection of players
        /// 
        /// The algorithm assumes that each player ID in playerIds uniquely exists in the original player list. The use of a dictionary
        /// for player lookup optimizes the reordering process, making it efficient for large lists by reducing the complexity
        /// to O(n + m), where n is the number of players in the original list and m is the number of IDs in playerIds.
        /// </remarks>
        /// <exception cref="GameException">Thrown if an ID in playerIds does not correspond to any player in the game model.</exception>
        private GameModel SetPlayerOrder(IList<string> playerIds)
        {
            GameModel gameModel = Log.CopyCurrent();
            ThrowIfWrongState(gameModel.GameState, [GameState.WaitingForRollForOrder, GameState.FinishedRollOrder]);
            var playerLookup = gameModel.Players.ToDictionary(p => p.Id);
            // Using LINQ to order players according to playerIds
            List<PlayerModel> orderedPlayers = playerIds
                .Select(id =>
                {
                    if (!playerLookup.TryGetValue(id, out PlayerModel? player))
                    {
                        throw new GameException($"Invalid playerId {id} found.");
                    }
                    return player;
                })
                .ToList();
            gameModel.Players = orderedPlayers;
            gameModel.CurrentPlayerId = gameModel.Players[0].Id;
            LogDone(gameModel);
            return gameModel;
        }
        /// <summary>
        ///     This function should allow the transition to the next valid state based on the current state.  returns null if the player isn't ready to transition.
        /// </summary>
        /// <returns></returns>
        private GameModel NextState()
        {
            GameModel gameModel = Log.CopyCurrent();
            if (!CanTransitionToNext(gameModel)) throw new GameException("Cannot transition to Next state at this time");
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
                        // move to the next player
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
                    SetTempGoldTiles(gameModel);
                    gameModel.GameState = GameState.WaitingForRoll;
                    break;
                case GameState.WaitingForRoll:
                    // GameState.WaitingForRoll is not controlled by the Next button.
                    // it is controlled by hitting a roll UI
                    break;
                case GameState.WaitingForNext:
                    gameModel.ChangePlayer(1);
                    gameModel.TurnRollModel = new();
                    gameModel.Players.ForEach(p =>
                    {
                        p.ResourcesThisTurn = new();
                        p.SpentEntitlementsThisTurn = [];
                    });
                    SetTempGoldTiles(gameModel);
                    gameModel.GameState = GameState.WaitingForRoll;
                    ResetBuildableRoads(gameModel);
                    break;
                case GameState.Supplemental:
                    break;
                case GameState.MustMoveRobber:
                    break;
                case GameState.TooManyCards:
                    break;
                case GameState.MustDestroyCity:
                    break;
                case GameState.PickingRandomGoldTiles:
                    break;
                case GameState.HandlePirates:
                    break;
                case GameState.DoneDestroyingCities:
                    break;
                case GameState.MustMoveMerchant:
                    break;
                case GameState.DestroyRoad:
                    break;
                case GameState.SwapNumbers:
                    break;
                case GameState.PickDeserter:
                    break;
                case GameState.PlaceDeserterKnight:
                    break;
                case GameState.DoneWithDeserter:
                    break;
                case GameState.UpgradeToMetro:
                    break;
                case GameState.TestCheckpoint:
                    break;
                case GameState.DisplaceVictimKnight:
                    break;
                case GameState.DisplaceKnightMoveVictim:
                    break;
                case GameState.ClickOnKnight:
                    break;
                case GameState.PickSupplementalPlayers:
                    break;
            }
            LogDone(gameModel);
            return gameModel;
        }
        /// <summary>
        ///     during the allocation phase in the normal game, we give a Settlement and a Road.  Later on, some expansions give other resources
        ///     (e.g.  a City on the second reverse allocation).
        /// </summary>
        /// <param name="gameModel"></param>
        private void GrantAllocationResources(GameModel gameModel)
        {
            ThrowIfWrongState(gameModel.GameState, [GameState.BeginResourceAllocation, GameState.AllocateResourceForward, GameState.AllocateResourceReverse]);
            var currentPlayer = gameModel.CurrentPlayer();
            currentPlayer.UnspentEntitlements.Add(Entitlement.Settlement);
            currentPlayer.UnspentEntitlements.Add(Entitlement.Road);
        }
        /// <summary>
        /// Attempts to purchase a road for the current player based on the given road key.
        /// </summary>
        /// <param name="roadKey">The key that identifies the specific road to be purchased.</param>
        /// <returns>The updated game model reflecting the road purchase, or null if the purchase is invalid.</returns>
        /// <exception cref="GameException">Thrown when the game state is not appropriate for a road purchase,
        /// the road key is invalid, or the road is already owned.</exception>
        private GameModel RoadPurchase(RoadPurchaseMessage message)
        {
            GameModel gameModel = Log.CopyCurrent();
            ThrowIfWrongState(gameModel.GameState, [GameState.WaitingForNext, GameState.AllocateResourceForward, GameState.AllocateResourceReverse]);
            ThrowIfNoEntitlement(gameModel, [Entitlement.Road]);
            var roadKey = message.RoadKey;
            // Retrieve the road model corresponding to the road key.
            var roadModel = gameModel.Roads.FirstOrDefault(r => r.RoadKey == roadKey);
            if (roadModel == null)
            {
                string roadKeyMsg = $"Invalid RoadKey {roadKey}";
                throw new GameException(roadKeyMsg);
            }
            if (roadModel.RoadState != RoadState.Buildable)
            {
                throw new GameException($"Road {roadModel} is not buildable!");
            }
            // Ensure the road is not already owned.
            if (roadModel.OwnerId != null)
            {
                string ownerMsg = $"Don't try to buy other people's roads! Owner: {roadModel.OwnerId}";
                throw new GameException(ownerMsg);
            }
            // Set the owner of the road to the current player and update the road state to "Road".
            roadModel.OwnerId = gameModel.CurrentPlayerId;
            roadModel.RoadState = RoadState.Road;
            var currentPlayerModel = gameModel.CurrentPlayer();
            ConsumeEntitlement(gameModel, Entitlement.Road);
            // Log the completed change.
            LogDone(gameModel);
            return gameModel;
        }
        /// <summary>
        ///     The last thing that happens prior to returning
        ///     
        /// </summary>
        /// <param name="gameModel"></param>
        private void LogDone(GameModel gameModel)
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
                var spent = currentPlayer.SpentEntitlementsThisGame.Count(e => e == epm.Entitlement );
                var unspent = currentPlayer.UnspentEntitlements.Count( e => e == epm.Entitlement );
                if (spent + unspent == gameModel.ResourceRules.MaxEntitlementCount(epm.Entitlement))
                {
                    epm.Enabled = false;
                }
            }
        }
        private void SetPlaySoldierAccess(GameModel gameModel)
        {
            var moveRobber = gameModel.PurchaseModel(Entitlement.Soldier );
            if (gameModel.GameState != GameState.WaitingForNext && gameModel.GameState != GameState.WaitingForRoll)
            {
                moveRobber.Enabled = false;
                return;
            }
            // can buy it only once
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
            // Check if at least one entitlement is not in the unspent entitlements.
            if (!entitlements.Any(e => currentPlayer.UnspentEntitlements.Contains(e)))
            {
                throw new GameException($"{currentPlayer.Id} does not have the required entitlement.");
            }
        }
        /// <summary>
        /// Upgrades a building based on its current state.
        /// </summary>
        /// <param name="buildingKey">Key identifying the specific building to upgrade.</param>
        /// <returns>The updated game model after the building upgrade.</returns>
        /// <exception cref="GameException">Thrown when the game state is incorrect, the building key is invalid,
        /// or when trying to upgrade a building not owned by the current player.</exception>
        private GameModel BuildingUpgrade(BuildingUpgradeMessage message)
        {
            GameModel gameModel = Log.CopyCurrent();
            ThrowIfWrongState(gameModel.GameState, [GameState.WaitingForNext, GameState.AllocateResourceForward, GameState.AllocateResourceReverse]);
            BuildingKey buildingKey = message.BuildingKey;
            var building = gameModel.Buildings.FindBuildingModel(buildingKey)
                    ?? throw new GameException($"Invalid BuildingKey: {buildingKey}");
            if (building.BuildingState == BuildingState.NotBuildable)
            {
                throw new GameException($"{building} is not buildingable.");
            }
            var currentPlayerModel = gameModel.CurrentPlayer();
            // Process the building upgrade based on its current state.
            switch (building.BuildingState)
            {
                case BuildingState.PossibleSettlement:
                    ThrowIfNoEntitlement(gameModel, [Entitlement.Settlement]);
                    building.BuildingState = BuildingState.Settlement;
                    building.OwnerId = gameModel.CurrentPlayerId;
                    ConsumeEntitlement(gameModel, Entitlement.Settlement);
                    break;
                case BuildingState.Settlement:
                    ThrowIfNoEntitlement(gameModel, [Entitlement.City]);
                    if (building.OwnerId != gameModel.CurrentPlayerId)
                    {
                        throw new GameException($"Don't try to upgrade somebody else's building: {building.OwnerId}");
                    }
                    building.BuildingState = BuildingState.City;
                    ConsumeEntitlement(gameModel, Entitlement.City);
                    break;
                case BuildingState.City:
                    ThrowIfNoEntitlement(gameModel, [Entitlement.BuyKnight]);
                    // Ensure the building is owned by the current player before upgrading.
                    if (building.OwnerId != gameModel.CurrentPlayerId)
                    {
                        throw new GameException($"Don't try to upgrade somebody else's building: {building.OwnerId}");
                    }
                    // Upgrade settlement to city, and city potentially to knight.
                    building.BuildingState = BuildingState.Knight;
                    ConsumeEntitlement(gameModel, Entitlement.BuyKnight);
                    break;
                case BuildingState.Knight:
                    // Knights cannot be upgraded further.
                    throw new GameException("Knights cannot be upgraded further.");
                    // No action needed if knights cannot be upgraded.
            }
            LogDone(gameModel);
            return gameModel;
        }
        /// <summary>
        ///     remove the entitlement from the UnspentEntitlements collection
        ///     add the entitlement to the spent this turn collection
        ///     add the entitlement to the spent this game collection
        /// </summary>
        /// <param name="gameModel"></param>
        /// <param name="buyKnight"></param>
        /// <exception cref="NotImplementedException"></exception>
        private void ConsumeEntitlement(GameModel gameModel, Entitlement entitlement)
        {
            var currentPlayer = gameModel.CurrentPlayer();
            currentPlayer.UnspentEntitlements.Remove(entitlement);
            currentPlayer.SpentEntitlementsThisTurn.Add(entitlement);
            currentPlayer.SpentEntitlementsThisGame.Add(entitlement);
        }
        /// <summary>
        /// Updates the scores of all players in the given game model based on current game state.
        /// </summary>
        /// <param name="gameModel">The game model containing the players whose scores need updating.</param>
        private void UpdateScore(GameModel gameModel)
        {
            int maxScore = 0;
            CalculateLongestRoad(gameModel);
            // Iterate through all players to update their scores
            foreach (var player in gameModel.Players)
            {
                player.HighestScore = false;
                int citiesPlayed = player.SpentEntitlementsThisGame.Count(e => e == Entitlement.City);
                int settlementsPlayed = player.SpentEntitlementsThisGame.Count(e => e == Entitlement.Settlement);
                settlementsPlayed -= citiesPlayed; // you can't play a city unless you played a settlement
                int knightsPlayed = player.SpentEntitlementsThisGame.Count(e=> e== Entitlement.Soldier);
                // Calculate base score from cities and settlements
                int score = citiesPlayed * 2 + settlementsPlayed;
                // Add bonus points for having the longest road
                if (player.HasLongestRoad)
                {
                    score += 2;
                }
                // Add bonus points for having the largest army
                if (player.LargestArmy)
                {
                    score += 2;
                }
                // Update the player's score
                player.Score = score;
                if (maxScore < player.Score) maxScore = player.Score;
            }
            foreach (var player in gameModel.Players)
            {
                player.HighestScore = ( player.Score == maxScore );
            }
        }
        /// <summary>
        /// Moves the robber to a new location based on input from a player.
        /// </summary>
        /// <param name="moveRobber">The move message containing the new coordinates for the robber.</param>
        /// <returns>The updated game model after moving the robber.</returns>
        /// <exception cref="GameException">Thrown if the game state is not correct for moving the robber,
        /// or if the current player ID is invalid.</exception>
        private GameModel MoveRobber(MoveRobberMessage moveRobber)
        {
            GameModel gameModel = Log.CopyCurrent();
            // Validate the current game state
            ThrowIfWrongState(gameModel.GameState, [GameState.MustMoveRobber]);
            ThrowIfBadPlayer(gameModel.CurrentPlayerId, gameModel.Players);
            ThrowIfNoEntitlement(gameModel, [Entitlement.Soldier, Entitlement.RolledSeven]);
            // Update the robber's position and the player who moved it
            gameModel.Robber.Coordinates = moveRobber.Coordinates;
            gameModel.Robber.MovedBy = gameModel.CurrentPlayerId;
            if (moveRobber.TargetPlayerId is not null)
            {
                var target = gameModel.Players.PlayerFromId(moveRobber.TargetPlayerId) ?? throw new GameException($"TargetPlayerId {moveRobber.TargetPlayerId} is invalid");
                target.TimesTargeted++;
            }
            //
            //  remove the entitlement that allowed the user to move the baron.
            //  if there is soldier, there can't be a rolled seven.
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
            LogDone(gameModel);
            return gameModel;
        }
        /// <summary>
        /// Validates the current game state against a list of acceptable states.
        /// </summary>
        /// <param name="currentState">The current game state to validate.</param>
        /// <param name="validStates">An array of states that are considered valid.</param>
        /// <exception cref="GameException">Thrown if the current state is not in the list of valid states.</exception>
        private static void ThrowIfWrongState(GameState currentState, GameState[] validStates)
        {
            if (!validStates.Contains(currentState))
            {
                string validStatesList = string.Join(", ", validStates.Select(vs => vs.ToString()));
                throw new GameException($"{currentState} is invalid. Must be in this set: [{validStatesList}]");
            }
        }
        private static bool WrongStateCheck(GameState currentState, GameState[] validStates)
        {
            return validStates.Contains(currentState);
        }
        /// <summary>
        /// Validates if a given player ID exists in the provided list of players.
        /// </summary>
        /// <param name="playerId">The ID of the player to validate.</param>
        /// <param name="players">The list of players against which to validate the ID.</param>
        /// <exception cref="GameException">Thrown if no player with the given ID is found in the list.</exception>
        private static void ThrowIfBadPlayer(string playerId, IList<PlayerModel> players)
        {
            // Use LINQ to check for player existence to simplify calling extension methods directly
            if (!players.Any(p => p.Id == playerId))
            {
                throw new GameException($"Bad CurrentPlayerId: {playerId}");
            }
        }
        private bool CanTransitionToNext(GameModel gameModel)
        {
            //GameModel gameModel = Log.CurrentState();
            //var currentPlayer = CurrentPlayer(gameModel);
            //
            //  when entitlement are added make sure they don't have any
            //switch (gameModel.GameState)
            //{
            //}
            return true;
        }
        /// <summary>
        /// Marks a random set of tiles as temporarily gold, avoiding desert tiles and duplicates.
        /// </summary>
        /// <param name="gameModel">The game model containing the tiles and house rules.</param>
        private void SetTempGoldTiles(GameModel gameModel)
        {
            try
            {
                // Exit early if no gold tiles need to be set.
                if (gameModel.HouseRules.GoldTiles == 0) return;
                // Reset the TemporarilyGold property for all tiles.
                foreach (var tile in gameModel.Tiles)
                {
                    tile.TemporarilyGold = false;
                }
                // Initialize a random number generator.
                var rand = new Random();
                HashSet<int> usedIndices = [];
                while (usedIndices.Count < gameModel.HouseRules.GoldTiles)
                {
                    int index = rand.Next(gameModel.Tiles.Count);
                    var tileModel = gameModel.Tiles[index];
                    // Ensure the tile is not null and meets the criteria for becoming a gold tile.
                    if (tileModel.ResourceTileType != ResourceType.Desert && !tileModel.TemporarilyGold)
                    {
                        tileModel.TemporarilyGold = true;
                        usedIndices.Add(index);  // Keep track of used indices to avoid duplicates.
                        // Log a trace message if needed.
                        this.TraceMessage($"GoldTile: {gameModel.CurrentPlayerId}={tileModel}");
                    }
                }
            }
            finally
            {
                // Debug check to ensure the correct number of gold tiles is set.
#if DEBUG
                int goldCount = gameModel.Tiles.Count(t => t.TemporarilyGold);
                Debug.Assert(goldCount == gameModel.HouseRules.GoldTiles, "The number of gold tiles does not match the expected value.");
#endif
            }
        }
        private GameModel ShuffleCurrentGame()
        {
            //
            //  you need to get the gameModel prior to checking the state as we don't know the state until then.
            //  CONSIDER: caching the state to do a top level check w/o the GameModel hydration cost
            GameModel gameModel = Log.CopyCurrent();
            ThrowIfWrongState(gameModel.GameState, [GameState.PickingBoard]);
            gameModel.Shuffle();
            LogDone(gameModel);
            return gameModel;
        }
        public SerializableLog GetSerializableLog()
        {
            return Log.GetSerializableLog();
        }
        public GameModel OpenSerializableLog(byte[] compressedBytes)
        {
            var decompressedJson = SerializationHelper.Decompress(compressedBytes);
            // Deserialize the JSON back into your Log or relevant data structure
            var savedLog = SerializationHelper.JsonDeserialize<SerializableLog>(decompressedJson) ?? throw new GameException("Error: Failed to load the game data.");
            Log<string> log =  Log<string>.FromSerializableLog(savedLog);
            this.Log = log;
            this.GameType = savedLog.GameType;
            return Log.CurrentState();
        }
        private GameModel? Undo()
        {
            GameModel result =  ( ( ILog )Log ).Undo() ?? throw new GameException("Undo cannot be done");
            SetActionFlags(result);
            result.ActionFlags.RedoEnabled = true;
            return result;
        }
        private GameModel? Redo()
        {
            GameModel result =  ( ( ILog )Log ).Redo() ?? throw new GameException("Redo cannot be done");
            SetActionFlags(result);
            return result;
        }
        private void ResetBuildableRoads(GameModel gameModel)
        {
            //
            //   mark them not buildable
            foreach (var road in gameModel.Roads)
            {
                if (road.RoadState == RoadState.Buildable)
                {
                    road.RoadState = RoadState.Unowned;
                }
                road.BuildIndex = 0;
            }
        }
        /// <summary>
        ///     A Road is "Buildable" if 
        ///     1. GameState is correct
        ///     2. it is next to another road owned by the CurrentPlayer
        ///     3. it is next to a building owned by the CurrentPlayer
        /// </summary>
        /// <param name="gameModel"></param>
        private void MarkBuildableRoads(GameModel gameModel)
        {
            ResetBuildableRoads(gameModel);
            List<RoadModel> buildableRoads = [];
            if (gameModel.Phase() == GamePhase.Purchase) // during allocation we can only build next to the settlment
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
                        // in allocation phase you have to build next to the building you just built -- whichi is 
                        // the one with no roads next to it.
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
            //
            // can't build any roads, don't allow the user to buy them
            if (buildableRoads.Count == 0)
            {
                gameModel.PurchaseModel(Entitlement.Road).Enabled = false;
                return;
            }
            //
            //  we have at least one road we can build
            gameModel.PurchaseModel(Entitlement.Road).Enabled = gameModel.Phase() == GamePhase.Purchase;
            if (gameModel.CurrentPlayer().UnspentEntitlements.Contains(Entitlement.Road))
            {
                for (int i = 0; i < buildableRoads.Count; i++)
                {
                    var road=buildableRoads[i];
                    road.RoadState = RoadState.Buildable;
                    road.BuildIndex = i + 1;
                }
            }
        }
        /// <summary>
        ///     Go through each building and determine the correct building state for it
        /// </summary>
        /// <param name="gameModel"></param>
        private void MarkBuildableBuildings(GameModel gameModel)
        {
            //
            //  turn off anything that used to be though of as a possible place for a settlement
            //  the rest of the function will turn them back on...
            gameModel.Buildings
                     .Where(b => b.BuildingState == BuildingState.PossibleSettlement)
                     .ToList()
                     .ForEach(b => b.BuildingState = BuildingState.NotBuildable);
            var currentPlayer = gameModel.CurrentPlayer();
            bool hasCity = currentPlayer.UnspentEntitlements.Contains(Entitlement.City);
            bool hasSettlement = currentPlayer.UnspentEntitlements.Contains(Entitlement.Settlement);
            List<BuildingModel> buildableCities = [];
            List<BuildingModel> buildableSettlements = [];
            var test = new BuildingKey(new HexCoordinates(-3, 2, 1), HexPosition.Right);
            //
            // to be buildable, the location has to be adjacent to an owned road and cannot be within one road of another building
            foreach (var building in gameModel.Buildings)
            {
                if (building.BuildingKey.Equals(test))
                {
                    //  Debug.Assert(false);
                }
                // can't build if there is a city
                if (building.BuildingState == BuildingState.City) continue;
                //
                //  all settlements, in theory, are upgradable
                if (building.BuildingState == BuildingState.Settlement)
                {
                    if (building.OwnerId == currentPlayer.Id)
                    {
                        // as long as it is yours...
                        buildableCities.Add(building);
                    }
                    // if the building is already a settlement, then there is no way to build on it
                    // other than a city upgrade
                    continue;
                }
                // for this empty building, look at the buildings next to it
                var ownedAdjacentBuildings = gameModel.Buildings.AdjacentBuildings(building.BuildingKey).Where(b => b.OwnerId != null).ToList();
                if (ownedAdjacentBuildings.Count == 0)
                {
                    if (building.OwnerId is null && ( gameModel.Phase() == GamePhase.PickingResources || gameModel.Phase() == GamePhase.PickingBoard ))
                    {
                        // during picking resources, you can place a building as long as you aren't next to another building
                        buildableSettlements.Add(building);
                        continue;
                    }
                    // so we have no owned adjacent buildings...but is there a road connect
                    // there isn't a building within one of you, but if there is a owned road next
                    // to you, that means there must be two roads and you can build
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
                // if we can build a city, allow it when we allow purchases
                gameModel.PurchaseModel(Entitlement.City).Enabled = true;
            }
            else
            {
                // don't allow purchase
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
            int total = currentPlayer.SpentEntitlementsThisGame.Count(e => e==entitlement) +
                        currentPlayer.UnspentEntitlements.Count(e => e == entitlement);
            switch (entitlement)
            {
                case Entitlement.Road:
                    return ( total < gameModel.ResourceRules.MaxRoads );
                case Entitlement.Settlement:
                    return ( total < gameModel.ResourceRules.MaxSettlements );
                case Entitlement.City:
                    return ( total < gameModel.ResourceRules.MaxCities );
                case Entitlement.Soldier:
                    return true;
                default:
                    throw new Exception($"TODO: add support for {entitlement} to AllowPurchase");
            }
        }
        public void CalculateLongestRoad(GameModel gameModel)
        {
            var longestRoadAllPlayers = 0;
            // calculate the longest road for each player
            foreach (var player in gameModel.Players)
            {
                // get the roads onwned by this player
                var playerRoads = gameModel.Roads.Where(r => r.OwnerId == player.Id).ToList();
                int max = 0;
                foreach (var startRoad in playerRoads)
                {
                    int count = CalculateLongestRoad(gameModel, startRoad, [], null);
                    if (count > max)
                    {
                        max = count;
                        if (max == gameModel.Roads.Count) // the most roads you can have…only count once
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
                    continue; // never enough to get longest road
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
        //
        //  Start is just any old road you want to start counting from
        //  counted are all the roads that have been counted so far -- presumably starts with .Count = 0
        //  blockedFork roads is set when we recurse so that we can pick a direction.  we need it in case of closed loops
        private int CalculateLongestRoad(GameModel gameModel, RoadModel start, List<RoadModel> counted, RoadModel? blockedFork)
        {
            int count = 1;
            int max = 1;
            counted.Add(start); // it is counted in the "max=1" above
            RoadModel next = start;
            List<RoadModel> ownedAdjacentNotCounted = gameModel.OwnedAdjacentRoadsNotCounted(next, counted,  blockedFork, out bool adjacentFork);
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
                        List<RoadModel> forks = new List<RoadModel>();
                        forks.AddRange(ownedAdjacentNotCounted);
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
