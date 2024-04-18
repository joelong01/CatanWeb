using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using CommunityToolkit.Mvvm.Messaging;

namespace Catan3.Models
{


    public partial class MainPageViewModel
    {
        private void OnRoll(RollModel roll)
        {
            if (GameViewModel.GameModel.GameState != GameState.WaitingForRoll) return;
            if (roll.ThisTurnsRoll is null)
            {
                throw new ArgumentNullException(nameof(roll));
            }

            GameViewModel.GameModel.RollModel.RollCounts[( int )roll.ThisTurnsRoll.NormalRoll - 2]++;
            GameViewModel.GameModel.RollModel.TotalRolls++;


            GameViewModel.GameModel.GameState = GameState.WaitingForNext;

            List<TileViewModel> highlightedTiles = [];
            foreach (TileViewModel tile in GameViewModel.Tiles)
            {
                if (tile.Tile.Number == ( int )roll.ThisTurnsRoll.NormalRoll)
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

            foreach (var tile in highlightedTiles)
            {
                tile.Orientation = CatanOrientation.FaceUp;
            }

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
                GameViewModel.GameModel.GameState = GameState.WaitingForRoll;
                RollData thisTurnsRollData = new();
                gameModel.RollModel.ThisTurnsRoll = thisTurnsRollData;
                GameViewModel.RollViewModel.RollModel.ThisTurnsRoll = thisTurnsRollData;
                SetTempGoldTiles();
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
