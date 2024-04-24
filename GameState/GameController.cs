using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
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
                        this.TraceMessage($"Game Exception: {e}");
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
                    this.TraceMessage($"Game Exception: {e}");
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
                        this.TraceMessage($"Game Exception: {e}");
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
                     this.TraceMessage($"Game Exception: {e}");
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
                    this.TraceMessage($"Game Exception: {e}");
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
                    this.TraceMessage($"Game Exception: {e}");
                }

            });

            Messenger.Register<EndGame>(this, (recipient, message) =>
            {
                Messenger.UnregisterAll(this);
            });

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
                    var effectiveType = tile.TemporarilyGold ? ResourceType.GoldMine : tile.ResourceTileType;
                    ResourcesModel resources = building.Resources(effectiveType);
                    playerResources[building.OwnerId].Add(resources);
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
            gameModel.ActionFlags.RedoEnabled = Log.CanRedo;
            gameModel.ActionFlags.NextEnabled = AllowNext(gameModel);
        }

        private bool AllowNext(GameModel gameModel)
        {
            if (gameModel.GameState == GameState.WaitingForRoll) return false;

            // when you have entitlements, return false if there is an unspent entitlement

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

            ThrowIfWrongState(gameModel.GameState, [GameState.WaitingForRollForOrder]);


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
                    }
                    else
                    {
                        // move to the next player
                        gameModel.ChangePlayer(1);
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
                    gameModel.Players.ForEach(p => p.ResourcesThisTurn = new());
                    SetTempGoldTiles(gameModel);
                    gameModel.GameState = GameState.WaitingForRoll;
                    break;
                case GameState.Supplemental:
                    break;
                case GameState.MustMoveBaron:
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
                case GameState.MustMoveKnight:
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
            var roadKey = message.RoadKey;
            // Retrieve the road model corresponding to the road key.
            var roadModel = gameModel.Roads.FirstOrDefault(r => r.RoadKey == roadKey);

            if (roadModel == null)
            {
                string roadKeyMsg = $"Invalid RoadKey {roadKey}";
                throw new GameException(roadKeyMsg);
            }

            // Ensure the road is not already owned.

            if (roadModel.OwnerId != null)
            {
                string ownerMsg = $"Don't try to buy other people's roads! Owner: {roadModel.OwnerId}";
                throw new GameException(ownerMsg);
            }
            // Update the road state if necessary.
            if (roadModel.RoadState == RoadState.Highlighted)
            {
                roadModel.RoadState = RoadState.Unowned;
            }

            // Set the owner of the road to the current player and update the road state to "Road".
            roadModel.OwnerId = gameModel.CurrentPlayerId;
            roadModel.RoadState = RoadState.Road;
            var currentPlayerModel = gameModel.Players.PlayerFromId(gameModel.CurrentPlayerId) ?? throw new GameException($"Can't find player {gameModel.CurrentPlayerId}");

            currentPlayerModel.RoadsPlayed++;
            UpdateScore(gameModel);
            // Log the completed change.
            LogDone(gameModel);

            return gameModel;
        }

        private void LogDone(GameModel gameModel)
        {
            SetActionFlags(gameModel);
            gameModel.ActionFlags.RedoEnabled = false;
            Log.Done(gameModel);
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

            var currentPlayerModel = gameModel.Players.PlayerFromId(gameModel.CurrentPlayerId) ?? throw new GameException($"Can't find player {gameModel.CurrentPlayerId}");

            // Process the building upgrade based on its current state.
            switch (building.BuildingState)
            {
                case BuildingState.Empty:
                case BuildingState.Highlighted:
                case BuildingState.Stars:
                    building.BuildingState = BuildingState.Settlement;
                    building.OwnerId = gameModel.CurrentPlayerId;
                    currentPlayerModel.SettlementsPlayed++;
                    break;

                case BuildingState.Settlement:
                case BuildingState.City:
                    // Ensure the building is owned by the current player before upgrading.
                    if (building.OwnerId != gameModel.CurrentPlayerId)
                    {
                        throw new GameException($"Don't try to upgrade somebody else's building: {building.OwnerId}");
                    }
                    // Upgrade settlement to city, and city potentially to knight.
                    if (building.BuildingState == BuildingState.Settlement)
                    {
                        building.BuildingState = BuildingState.City;
                        currentPlayerModel.SettlementsPlayed--;
                        currentPlayerModel.CitiesPlayed++;
                    }
                    else
                    {
                        building.BuildingState = BuildingState.Knight;
                        currentPlayerModel.KnightsPlayed++;
                        currentPlayerModel.CitiesPlayed--;
                    }

                    break;

                case BuildingState.Knight:
                    // Knights cannot be upgraded further.
                    throw new GameException("Knights cannot be upgraded further.");
                    // No action needed if knights cannot be upgraded.

            }
            UpdateScore(gameModel);
            LogDone(gameModel);
            return gameModel;
        }


        /// <summary>
        /// Updates the scores of all players in the given game model based on current game state.
        /// </summary>
        /// <param name="gameModel">The game model containing the players whose scores need updating.</param>
        private void UpdateScore(GameModel gameModel)
        {
            // Iterate through all players to update their scores
            foreach (var player in gameModel.Players)
            {
                // Calculate base score from cities and settlements
                int score = player.CitiesPlayed * 2 + player.SettlementsPlayed;

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
            ThrowIfWrongState(gameModel.GameState, [GameState.WaitingForNext, GameState.WaitingForRoll]);
            ThrowIfBadPlayer(gameModel.CurrentPlayerId, gameModel.Players);


            // Update the robber's position and the player who moved it
            gameModel.Robber.Coordinates = moveRobber.Coordinates;
            gameModel.Robber.MovedBy = gameModel.CurrentPlayerId;
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
                throw new GameException($"{currentState} is invalid. Must be one of {validStatesList}");
            }
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
    }
    /// <summary>
    /// Provides helper methods to change the current player in a GameModel.
    /// </summary>
    public static class ChangePlayerHelper
    {
        /// <summary>
        /// Calculates the player ID that is a specified number of positions away from a given start player ID.
        /// </summary>
        /// <param name="gameModel">The game model containing the players.</param>
        /// <param name="startPlayerId">The ID of the player from which to start counting.</param>
        /// <param name="numberOfPositions">The number of positions to move forward in the player list; can be negative.</param>
        /// <returns>The player ID of the player numberOfPositions away from the start player.</returns>
        /// <exception cref="GameException">Thrown if the start player ID is invalid or not in the game.</exception>
        public static string NextPlayerId(this GameModel gameModel, string startPlayerId, int numberOfPositions)
        {
            // Validate and find the starting player
            var startPlayer = gameModel.Players.PlayerFromId(startPlayerId) ??
            throw new GameException($"Invalid id: {startPlayerId}");

            int idx = gameModel.Players.IndexOf(startPlayer);
            if (idx == -1)
                throw new GameException("The player must be in the game!");

            int count = gameModel.Players.Count;

            // Calculate the index of the next player, wrapping around if necessary
            int newPlayerIndex = (idx + numberOfPositions) % count;
            if (newPlayerIndex < 0)
                newPlayerIndex += count;

            // Retrieve the new player's ID
            var newPlayer = gameModel.Players[newPlayerIndex];
            return newPlayer.Id;
        }

        /// <summary>
        /// Changes the current player to the player a specified number of positions forward.
        /// </summary>
        /// <param name="gameModel">The game model where the current player will be changed.</param>
        /// <param name="numberOfPositions">The number of positions to move forward in the player list.</param>
        /// /// <exception cref="GameException">Thrown if the player ID is invalid.</exception>
        public static void ChangePlayer(this GameModel gameModel, int numberOfPositions)
        {
            // Ensure the current player ID is valid
            if (string.IsNullOrEmpty(gameModel.CurrentPlayerId))
                throw new GameException("Current player ID must not be null or empty.");

            // Get the next player ID and change to it
            var id = NextPlayerId(gameModel, gameModel.CurrentPlayerId, numberOfPositions);
            gameModel.ChangePlayerTo(id);
        }

        /// <summary>
        /// Sets the current player to the specified player ID.
        /// </summary>
        /// <param name="gameModel">The game model where the current player will be set.</param>
        /// <param name="playerId">The player ID to set as current.</param>
        /// <exception cref="GameException">Thrown if the player ID is invalid.</exception>
        public static void ChangePlayerTo(this GameModel gameModel, string playerId)
        {
            // Validate and find the new player
            var newPlayer = gameModel.Players.PlayerFromId(playerId) ??
            throw new GameException($"Invalid id: {playerId}");

            // Set the current player ID
            gameModel.CurrentPlayerId = newPlayer.Id;
        }
    }

}
