

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.Contracts;
using System.Linq;
using Catan10.Models;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Messaging;
using Windows.Security.Isolation;

namespace Catan3.Models
{




    public partial class GameViewModel : ObservableRecipient
    {
        public GameViewModel()
        {
            IsActive = true;
            Id = GetHashCode().ToString();

            Messenger.Register<TurnStarting>(this, (recipient, message) =>
            {
                OnTurnStarting(message.PlayerId);
            });
            Messenger.Register<TurnEnding>(this, (recipient, message) =>
            {
                OnTurnEnding(message.PlayerId);
            });

            Messenger.Register<EndGame>(this, (recipient, message) =>
            {
                Messenger.UnregisterAll(this);
            });

            Messenger.Register<UpdateGameModel>(this, (recipient, message) => { MergeGameModel(message.GameModel); });

            Messenger.Register<RequestTileOwners>(this, (recipient, message) =>
            {
                OnRequestTileOwners(message.TileViewModel);
            });
         
        }

        private void OnRequestTileOwners(TileViewModel tileViewModel)
        {
            var buildings = GameModel.Buildings.BuildingsInTile(tileViewModel.Tile.TileKey);
            List<PlayerViewModel> owners = [];
            foreach (var building in buildings)
            {
                if (building.OwnerId is not null)
                {
                    var p = Players.First( player => player.Id == building.OwnerId );
                    Debug.Assert(p is not null);
                    if (p.Id != CurrentPlayer.Id)
                    {
                        owners.Add(p);
                    }
                }
            }
            Messenger.Send(new TileOwnersResponse(owners));

        }

        /// <summary>
        ///     MainViewModel.NextPlayer sends this message so each ViewModel can take appropriate action
        /// </summary>
        /// <param name="playerId"></param>
        /// <exception cref="NotImplementedException"></exception>
        private void OnTurnStarting(string playerId)
        {
            this.TraceMessage($"Turn Starting for {playerId}");
            CurrentPlayer = PlayerDatabase.FromId(playerId) ?? throw new Exception($"Bad Player ID in OnTurnStarting {playerId}");

            // lets do GameModel fix up first
            GameModel.CurrentPlayerId = playerId;

            // create a set the place to collect the roll
            GameModel.TurnRollModel = new();
            this.TurnRollViewModel.TurnRollModel = GameModel.TurnRollModel;
            // make sure we don't have any EqualityComparer propblems by ensuring the reference's are the same
            Debug.Assert(ReferenceEquals(GameModel.TurnRollModel, TurnRollViewModel.TurnRollModel));


            // create a place for this turn's resources for each of the players
            foreach (var playerViewModel in Players)
            {
                ResourcesModel resourceModel = new();
                playerViewModel.Player.ResourcesThisTurn = resourceModel;
                playerViewModel.ResourcesThisTurn.ResourceModel = resourceModel;



            }

            SetTempGoldTiles();
            GameModel.GameState = GameState.WaitingForRoll;

            // logging done by the caller
        }

        private void OnTurnEnding(string playerId)
        {
            this.TraceMessage($"Turn Ending for {playerId}");
            ClearTempGoldTiles();
            GameModel.TurnRollModel = null; // wipe the current players roll model - don't need it anymore
            this.TurnRollViewModel.TurnRollModel = null;
        }

        private void ClearTempGoldTiles()
        {
            if (this.GameModel.HouseRules.GoldTiles == 0) return;
            int goldCount = Tiles.Count( t => t.Tile.TemporarilyGold);
            Debug.Assert(goldCount == 0 || goldCount == this.GameModel.HouseRules.GoldTiles);
            Contract.Assert(GameModel.HouseRules.GoldTiles > 0);
            for (int i = 0; i < Tiles.Count - 1; i++)
            {
                Debug.Assert(Tiles[i].Tile.GetHashCode() == GameModel.Tiles[i].GetHashCode());
                Debug.Assert(Tiles[i].Tile == GameModel.Tiles[i]);
                Debug.Assert(Tiles[i].Tile.Equals(GameModel.Tiles[i]));
            }
            foreach (TileModel tile in this.GameModel.Tiles)
            {
                tile.TemporarilyGold = false;
            }
        }

        private void SetTempGoldTiles()
        {
            try
            {

                if (this.GameModel.HouseRules.GoldTiles == 0) return;

                var rand = new Random((int)DateTime.Now.Ticks);
                int count = 0;
                List<TileViewModel> goldTiles = [];
                while (count < this.GameModel.HouseRules.GoldTiles)
                {
                    var index = rand.Next(Tiles.Count);
                    var tileViewModel =  Tiles[index] ;
                    Contract.Assert(tileViewModel is not null, "this should *never* happen!");
                    if (tileViewModel.Tile.ResourceTileType != ResourceType.Desert && tileViewModel.Tile.TemporarilyGold == false)
                    {
                        tileViewModel.Tile.TemporarilyGold = true;
                        tileViewModel.Orientation = CatanOrientation.FaceDown;
                        goldTiles.Add(tileViewModel);
                        this.TraceMessage($"GoldTile: {this.GameModel.CurrentPlayerId}={tileViewModel}");
                        count++;
                    }
                }
                foreach (var t in goldTiles)
                {
                    t.Orientation = CatanOrientation.FaceUp;
                }
            }
            finally
            {
#if DEBUG


                var goldCount = Tiles.Count(t => t.Tile.TemporarilyGold);
                Debug.Assert(goldCount == GameModel.HouseRules.GoldTiles);
#endif
            }

            //
            //   this is *not* logged here -- the caller should log so that they
            //   get undone together.
        }


    }
}
