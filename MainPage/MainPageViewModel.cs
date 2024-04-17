using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.Contracts;
using System.Linq;
using System.Threading.Tasks;
using Catan.Utility;
using Catan3.Controls;
using Catan3.Utility;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using Microsoft.UI;
using Microsoft.UI.Xaml.Controls;
using Windows.Security.Isolation;
using static System.Collections.Specialized.BitVector32;

namespace Catan3.Models
{





    public partial class MainPageViewModel : ObservableRecipient
    {

        [ObservableProperty]
        GameViewModel _gameViewModel = GameViewModel.Default;

        [ObservableProperty]
        private Log<byte[]> _log;

        private readonly IFileService _fileService;


        public IMessenger MessageService => this.Messenger;
        public MainPageViewModel(IFileService fileService, GameType selectedGame, List<PlayerViewModel> playingPlayers)
        {
            FunctionTimer.Enabled = false;
            _fileService = fileService;
            RegisterMessages();
            // create a new GameModel - this would usually come from the service

            List<string> playerIds = playingPlayers.Select( p => p.Id ).ToList();
            var gameModel = GameFactory.CreateGame(selectedGame, playerIds);
            var gvm = new GameViewModel(gameModel);
            this.GameViewModel = gvm;
            GameViewModel.UpdateLayout();
            GameViewModel.SetStars();
            Log = new Log<byte[]>(selectedGame);
            SetTempGoldTiles();
            Log.Done(GameViewModel.GameModel);


        }
        private void RegisterMessages()
        {

            Debug.Assert(Messenger is not null);
            IsActive = true;

            Messenger.Register<DoAction>(this, (recipient, message) =>
            {
                OnAction(message.Action);
            });


            Messenger.Register<BuildingUpgrade>(this, (recipient, message) =>
            {
                OnBuildingUpgrade(message.BuildingKey);
            });

            Messenger.Register<BuyRoad>(this, (recipient, message) =>
                       {
                           OnRoadPurchase(message.RoadKey);
                       });


            Messenger.Register<RequestTileOwners>(this, (recipient, message) =>
            {
                OnRequestTileOwners(message.TileViewModel);
            });
            Messenger.Register<MoveRobber>(this, (recipient, message) =>
            {
                GameViewModel.RobberViewModel.RobberModel.Coordinates = message.Coordinates;
                GameViewModel.RobberViewModel.RobberModel.MovedBy = GameViewModel.CurrentPlayer.Id;
                Log.Done(GameViewModel.GameModel);

            });
            Messenger.Register<Rolled>(this, (recipient, message) =>
            {
                OnRoll(message.Roll);

            });

        }


        private void SetTempGoldTiles()
        {
            try
            {

                if (GameViewModel.GameModel.HouseRules.GoldTiles == 0) return;

                var gameModel = GameViewModel.GameModel;


                int goldCount = GameViewModel.Tiles.Count( t => t.Tile.TemporarilyGold);
                Debug.Assert(goldCount == 0 || goldCount == GameViewModel.GameModel.HouseRules.GoldTiles);
                Contract.Assert(gameModel.HouseRules.GoldTiles > 0);
                foreach (TileModel tile in gameModel.Tiles)
                {
                    tile.TemporarilyGold = false;
                }
                var rand = new Random((int)DateTime.Now.Ticks);
                int count = 0;
                List<TileViewModel> goldTiles = [];
                while (count < GameViewModel.GameModel.HouseRules.GoldTiles)
                {
                    var index = rand.Next(GameViewModel.Tiles.Count);
                    var tileViewModel =  GameViewModel.Tiles[index] ;
                    Contract.Assert(tileViewModel is not null, "this should *never* happen!");
                    if (tileViewModel.Tile.ResourceTileType != ResourceTileType.Desert && tileViewModel.Tile.TemporarilyGold == false)
                    {
                        tileViewModel.Tile.TemporarilyGold = true;
                        tileViewModel.Orientation = CatanOrientation.FaceDown;
                        goldTiles.Add(tileViewModel);
                        this.TraceMessage($"GoldTile: {GameViewModel.GameModel.CurrentPlayerId}={tileViewModel}");
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


                var goldCount = GameViewModel.Tiles.Count(t => t.Tile.TemporarilyGold);
                Debug.Assert(goldCount == GameViewModel.GameModel.HouseRules.GoldTiles);
#endif
            }

            //
            //   this is *not* logged here -- the caller should log so that they
            //   get undone together.
        }



    }

    public static class PlayerDatabase
    {
        public static List<PlayerViewModel> AvailablePlayers { get; } =
            [
                new ("Dodgy", Colors.White, Colors.Red),
                new ("Joe", Colors.White, Colors.Blue),
                new ("Doug", Colors.White, Colors.Green),
                new ("Chris", Colors.White, Colors.Black),
                new ("Adrian", Colors.White, Colors.Purple),
                new ("Ryan", Colors.White, Colors.DarkGray)
            ];

        public static PlayerViewModel? FromId(string id)
        {
            return AvailablePlayers.FirstOrDefault(x => x.Id == id);
        }
    }


}
