using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using Catan10.Models;
using CommunityToolkit.Mvvm.Messaging;

namespace Catan3.Models
{


    public partial class MainPageViewModel
    {
        /// <summary>
        ///     when a roll comes in 
        ///     . make sure that we are ready for a roll
        ///     . update the game state to reflect the roll
        ///     . change the game state
        ///     . highlight the tiles
        ///     . calculate the resources for each plaery
        /// </summary>
        /// <param name="roll"></param>
        /// <exception cref="ArgumentNullException"></exception>
        /// <exception cref="Exception"></exception>
        private void OnRoll(TurnRollModel roll)
        {
            // you can only roll in the state GameState.WaitingForRoll
            if (GameViewModel.GameModel.GameState != GameState.WaitingForRoll) return;

            // update the global counts for rolls
            GameViewModel.GameModel.GameRollModel.RollCounts[( int )roll.NormalRoll - 2]++;
            GameViewModel.GameModel.GameRollModel.TotalRolls++;

            Debug.Assert(ReferenceEquals(GameViewModel.GameModel.GameRollModel, GameViewModel.GameRollViewModel.GameRollModel), "these should be the same GameRollModels!");

            // update the state
            GameViewModel.GameModel.GameState = GameState.WaitingForNext;

            // highlight the tiles - we also flip them to draw the eye
            List<TileViewModel> highlightedTiles = [];
            foreach (TileViewModel tile in GameViewModel.Tiles)
            {
                if (tile.Tile.Number == ( int )roll.NormalRoll)
                {
                    highlightedTiles.Add(tile);
                    tile.Tile.Highlighted = true;
                    tile.Orientation = CatanOrientation.FaceDown;
                }
                else
                {
                    tile.Tile.Highlighted = false;
                    tile.Orientation = CatanOrientation.FaceUp;
                }
            }
            //
            // calculate resources based on the tiles that are highlighted (which we just set)
            foreach (var tile in highlightedTiles)
            {
                tile.Orientation = CatanOrientation.FaceUp;
                var buildings = GameViewModel.GameModel.Buildings.OwnedBuildings(tile.Tile.TileKey);
                foreach (BuildingModel building in buildings)
                {
                    Debug.Assert(building.OwnerId is not null, "OwnedBuildings should only return Owned buildings...");
                    var effectiveType = tile.Tile.TemporarilyGold ? ResourceTileType.GoldMine : tile.Tile.ResourceTileType;
                    TradeResourcesModel resources = building.Resources(effectiveType);
                    var player = PlayerDatabase.FromId(building.OwnerId) ?? throw new Exception($"bad playerId in allocating resources to owners: {building.OwnerId}");
                    Debug.Assert(player.Player.ResourcesThisTurn is not null);
#if DEBUG
                    var playerModel = GameViewModel.GameModel.Players.Find((p) => p.Id == building.OwnerId);
                    Debug.Assert(ReferenceEquals(player.Player, playerModel), "these should be the same PlayerModel objects!");
#endif

                    player.Player.ResourcesThisTurn.Add(resources); // this is updating the underlying GameModel
                    player.Player.TotalResourcesGenerated.Add(resources);
                }
            }

            foreach (var player in GameViewModel.GameModel.Players)
            {
                this.TraceMessage($"Player {player} got {player.ResourcesThisTurn}");
            }

            // save our changes to the GameModel to the log

            Log.Done(GameViewModel.GameModel);

        }

        private void OnRequestTileOwners(TileViewModel tileViewModel)
        {
            var buildings = GameViewModel.GameModel.Buildings.BuildingsInTile(tileViewModel.Tile.TileKey);
            List<PlayerViewModel> owners = [];
            foreach (var building in buildings)
            {
                if (building.OwnerId is not null)
                {
                    var p = GameViewModel.Players.First( player => player.Id == building.OwnerId );
                    Debug.Assert(p is not null);
                    if (p.Id != GameViewModel.CurrentPlayer.Id)
                    {
                        owners.Add(p);
                    }
                }
            }
            Messenger.Send(new TileOwnersResponse(owners));

        }

        /// <summary>
        ///     if the message takes no parameters, then we can just add enum elements and then add a case statement
        ///     without modifying code inbetween
        /// </summary>
        /// <param name="action"></param>
        private void OnAction(GameAction action)
        {
            switch (action)
            {
                case GameAction.Shuffle:
                    Shuffle();
                    break;
                case GameAction.Undo:
                    Log.Undo(this.GameViewModel);
                    break;
                case GameAction.Redo:
                    Log.Redo(this.GameViewModel);
                    break;
                case GameAction.Next:
                    OnNext();
                    break;
            }
        }
        private void OnNext()
        {
            GameModel gameModel = GameViewModel.GameModel;
            GameState currentState = GameViewModel.GameModel.GameState;
            if (currentState == GameState.PickingBoard)
            {
               
               
                Messenger.Send(new TurnStarting(GameViewModel.CurrentPlayer.Id));
                Debug.Assert(gameModel.GameState == GameState.WaitingForRoll);
                Log.Done(GameViewModel.GameModel);
                return;
            }

            if (currentState == GameState.WaitingForRoll)
            {
                return; // need to roll to update state
            }

            if (currentState == GameState.WaitingForNext)
            {
                NextPlayer();
                return;
            }
        }
        private void OnRoadPurchase(RoadKey roadKey)
        {
            if (GameViewModel.GameModel.GameState != GameState.WaitingForNext) return;
            var roadView = GameViewModel.Roads.FirstOrDefault(r => r.Road.RoadKey == roadKey);
            if (roadView is null) return;
            if (roadView.Road.OwnerId is not null) return;
            //
            //  this will be the state we go back to when we Undo
            if (roadView.Road.RoadState == RoadState.Highlighted) roadView.Road.RoadState = RoadState.Unowned;

            roadView.Road.OwnerId = GameViewModel.CurrentPlayer.Id;
            roadView.Road.RoadState = RoadState.Road;
            Log.Done(GameViewModel.GameModel);

        }

        /// <summary>
        ///     This is a loggable event.  in the case of a Service, this would be a service call.
        /// </summary>
        /// <param name="buildingKey"></param>
        private void OnBuildingUpgrade(BuildingKey buildingKey)
        {

            if (GameViewModel.GameModel.GameState != GameState.WaitingForNext) return;
            var bvm = GameViewModel.Buildings.FindBuildingViewModel(buildingKey);
            if (bvm is null) return;

            switch (bvm.Building.BuildingState)
            {
                case BuildingState.Empty:
                case BuildingState.Highlighted:
                case BuildingState.Stars:

                    bvm.Building.BuildingState = BuildingState.Settlement;
                    bvm.Building.OwnerId = GameViewModel.CurrentPlayer.Id;

                    break;
                case BuildingState.Settlement:


                    Debug.Assert(bvm.Building.OwnerId != null);
                    if (bvm.Building.OwnerId != GameViewModel.CurrentPlayer.Id) return;
                    bvm.Building.BuildingState = BuildingState.City;

                    break;
                case BuildingState.City:


                    Debug.Assert(bvm.Building.OwnerId != null);
                    if (bvm.Building.OwnerId != GameViewModel.CurrentPlayer.Id) return;
                    bvm.Building.BuildingState = BuildingState.Knight;

                    break;
                case BuildingState.Knight:
                    break;
            }


            //
            //  turn off all the Stars after you build a building
            GameViewModel.ShownStars = 14;
            Log.Done(GameViewModel.GameModel);
        }
    }
}
