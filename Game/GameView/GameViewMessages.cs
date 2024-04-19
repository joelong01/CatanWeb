

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.Contracts;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Messaging;

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
            GameModel.RollModel.ThisTurnsRoll = null; // wipe the current players roll model - don't need it anymore
            foreach (var playerModel in GameModel.Players)
            {
                playerModel.ResourcesThisTurn = new();
              

            }
            GameModel.RollModel = new();
            RollViewModel.RollModel = GameModel.RollModel;
            GameModel.GameState = GameState.WaitingForRoll;

            foreach (var playerViewModel in Players)
            {
                playerViewModel.ResourcesThisTurn = new();
               
                Debug.Assert(playerViewModel.Player.ResourcesThisTurn is not null); // alocated above
                playerViewModel.ResourcesThisTurn.ResourceModel = playerViewModel.Player.ResourcesThisTurn;
            }

            SetTempGoldTiles();

        }

        private void OnTurnEnding(string playerId)
        {
            this.TraceMessage($"Turn Ending for {playerId}");
            ClearTempGoldTiles();
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
                    if (tileViewModel.Tile.ResourceTileType != ResourceTileType.Desert && tileViewModel.Tile.TemporarilyGold == false)
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
