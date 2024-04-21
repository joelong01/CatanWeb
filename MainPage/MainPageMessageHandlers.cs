using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Resources;
using System.Threading;
using Catan10.Models;
using CommunityToolkit.Mvvm.Messaging;
using Microsoft.UI.Xaml.Data;

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
            Dictionary<string, ResourcesModel> playerResources = [];
            foreach (var player in  GameViewModel.GameModel.Players)
            {
                playerResources[player.Id] = new();
            }
            //
            //  go through and poplulate the ResourceModel with the resources won for the roll
            foreach (var tile in highlightedTiles)
            {
                tile.Orientation = CatanOrientation.FaceUp;
                var buildings = GameViewModel.GameModel.Buildings.OwnedBuildings(tile.Tile.TileKey);
                foreach (BuildingModel building in buildings)
                {
                    Debug.Assert(building.OwnerId is not null, "OwnedBuildings should only return Owned buildings...");
                    var effectiveType = tile.Tile.TemporarilyGold ? ResourceType.GoldMine : tile.Tile.ResourceTileType;
                    ResourcesModel resources = building.Resources(effectiveType);
                    playerResources[building.OwnerId].Add(resources);

                   
                    
                }
            }
            // now fix up the underlying resource models in the same way as if we loading it from disk or got it back from a service
            // -- e.g. create new data objects and stick the full object into the model
            foreach (var player in GameViewModel.Players)
            {
                var newResources =  playerResources[player.Id];
                player.Player.ResourcesThisTurn = newResources; // this is updating the underlying GameModel, triggering the binding updates
                player.ResourcesThisTurn.ResourceModel = newResources;

                var newTotal = new ResourcesModel(player.Player.TotalResourcesGenerated);
                newTotal.Add(newResources);

                player.Player.TotalResourcesGenerated = newTotal;
                // TODO: need a player.TotalResourcesGenerated ResourceViewModel

                var newGameTotal = new ResourcesModel(GameViewModel.GameModel.GameResourcesModel);
                newGameTotal.Add(newResources);

                GameViewModel.GameModel.GameResourcesModel = newGameTotal;
                GameViewModel.GameResourceViewModel.ResourceModel = newGameTotal;
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
