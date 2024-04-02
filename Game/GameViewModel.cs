using System.Collections.Generic;
using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using Catan3.Utility;
using System.Linq;
using Microsoft.UI.Xaml.Controls;
using Windows.Foundation;
using System.Runtime.CompilerServices;
using System.Text.Json.Serialization;


namespace Catan3.Models
{

    public partial class GameViewModel
    {
      
        public GameModel? GameModel { get; set; }

        public GameViewModel(GameModel gameModel, IEnumerable<PlayerViewModel> allPlayers)
        {
            if (gameModel.GameType == CatanGame.Regular)
            {
                BoardInfo = new RegularBoardInfo();
            }
            else if (gameModel.GameType == CatanGame.Expansion)
            {
                BoardInfo = new ExpansionBoardInfo();
            }
            else
            {
                throw new ArgumentException($"invalid boardsize");
            }

            GameModel = gameModel;

            Tiles = CreateAndSortTileViewModelList(gameModel.Tiles);

            foreach (var building in gameModel.Buildings)
            {
                Buildings.Add(new BuildingViewModel(building, BoardInfo.Layout));
            }
            foreach (var player in gameModel.Players)
            {
                var playerViewModel = allPlayers.FirstOrDefault( p => p.Id == player.Id);
                if (playerViewModel is null)
                {
                    throw new Exception($"Player {player.Id} not found");
                }
                playerViewModel.Player = player;
                Players.Add(playerViewModel);
            }

            foreach (var road in gameModel.Roads)
            {
                var roadView = new RoadViewModel(road, BoardInfo.Layout);
                roadView.Index = Roads.Count;
                Roads.Add(roadView);

            }
            foreach (var harbor in gameModel.Harbors)
            {
                Harbors.Add(new HarborViewModel(harbor, BoardInfo.Layout));
            }
            Robber = new RobberViewModel(gameModel.Robber);
            SetPipCount();
            this.BoardInfo.Layout.PropertyChanged += Layout_PropertyChanged;
            UpdateLayout();
           
        }

        private void Layout_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(BoardInfo.Layout.TileXOffset) ||
                e.PropertyName == nameof(BoardInfo.Layout.TileYOffset) ||
                e.PropertyName == nameof(BoardInfo.Layout.BoardWidth) ||
                e.PropertyName == nameof(BoardInfo.Layout.BoardHeight))
            {
                // those are the 4 properties updated in UpdateLayout
                return;
            }

            if (e.PropertyName == nameof(BoardInfo.Layout.OuterHexSize) || e.PropertyName == nameof(BoardInfo.Layout.BuildingSize))
            {
                // these are the properties that the UpdateLayout depends on

                Debug.WriteLine($"Updating Layout because of PropertyChanged {e.PropertyName}");

                UpdateLayout();
                return;
            }

            Debug.WriteLine($"Skipping because of PropertyChanged {e.PropertyName}");

        }

        private void SetPipCount()
        {
            foreach (var building in Buildings)
            {
                building.Pips = TilesForBuildings(building.Building.BuildingKey).Pips();
            }
        }
        /// <summary>
        ///     Data that joins 2 or more collections is implemented here instead of as extension methods to the collection
        /// </summary>
        /// <param name="key"></param>
        /// <returns></returns>
        public List<TileModel> TilesForBuildings(BuildingKey key)
        {
            List<TileModel> tiles = [];
            // get the tile
            var tileModel = Tiles.TileFromCoords(key.HexCoordinates)?.Tile;
            Debug.Assert(tileModel is not null, "Bad HexCoordinates");
            tiles.Add(tileModel);
            // get the aliases
            var aliases = key.Aliases();
            foreach ((HexPosition position, Direction direction) in aliases)
            {
                var neighbor = Tiles.TileFromCoords(tileModel.TileKey.GetAdjacentTile(direction));
                if (neighbor is not null)
                {
                    tiles.Add(neighbor.Tile);
                }
            }
            return tiles;
        }

        private ObservableCollection<TileViewModel> CreateAndSortTileViewModelList(ObservableCollection<TileModel> tiles)
        {
            Debug.Assert(BoardInfo is not null);
            var sortedTiles = tiles.OrderBy(tvm => tvm.TileKey).ToList();
            ObservableCollection<TileViewModel> result = [];
            for (int i = 0; i < sortedTiles.Count; i++)
            {
                var tvm = new TileViewModel(sortedTiles[i], BoardInfo.Layout)
                {
                    Index = i
                };
                result.Add(tvm);
            }

            return result;
        }

        /// <summary>
        ///     We want to calculate the offset of the tile positions from the harborLeft of the board so that we leave the right space for the buildings 
        ///     and harbors around the board.  when we start calling .Left and .Top, the space to add to harborLeft and top is 0.  this will calculate what
        ///     should be added. 
        ///     
        ///     Left is calculated by looking for harbors off the first column
        ///     Top is calculated by looking at the top of the middle row for Harbors or Buildings
        ///     Height is calculated by looking at the bottom of the middle row for Harbors or Buildings
        ///     Width is calculated by looking at the last column for harbors 
        ///     
        ///     the space will either be a harbor or be a building.
        /// </summary>

        private void UpdateLayout()
        {
            Debug.Assert(BoardInfo is not null && BoardInfo.Layout is not null);
            Debug.Assert(Harbors is not null);
            BoardInfo.Layout.TileXOffset = 0;
            BoardInfo.Layout.TileYOffset = 0;
            var harborSize = BoardInfo.Layout.BuildingSize;
            var hexSize = BoardInfo.Layout.OuterHexSize;

            //
            //  get Y offset
            var topTile = Tiles.TopTile();
            var key = topTile.Tile.TileKey;
            var tileTop = BoardInfo.Layout.Top(key);
            var pointyDictionary = BoardInfo.Layout.PointyHexPoints.PointyTopListToDictionary();
            var top = pointyDictionary[HexSide.Top].Y;
            BoardInfo.Layout.TileYOffset = Math.Round(Math.Abs(tileTop) + Math.Abs(top) + harborSize, 2);

            // get X offset
            var firstTile = Tiles.FirstColumn().First();
            // all of the Harbors will have the same X on the first column, so make one up assuming that one will be there.
            var harborTopLeft = HarborViewModel.GetLeftTop(BoardInfo.Layout, firstTile.Tile.TileKey, HexSide.BottomLeft);
            BoardInfo.Layout.TileXOffset = Math.Abs(Math.Round(harborTopLeft.X));
            this.TraceMessage($"({BoardInfo.Layout.TileXOffset},{BoardInfo.Layout.TileYOffset})");



            // calculate the height

            var bottomTile = Tiles.BottomTile();

            HarborViewModel? bottomHarbor = Harbors.FindHarbor(bottomTile.Tile.TileKey, HexSide.Bottom);
            if (bottomHarbor is not null)
            {
                BoardInfo.Layout.BoardHeight = bottomHarbor.Top + BoardInfo.Layout.BuildingSize; // BuildingSize is also HarborSize
            }
            else
            {
                var b = Buildings.FindBuildingViewModel(new BuildingKey(bottomTile.Tile.TileKey, HexPosition.BottomLeft));
                Debug.Assert(b != null);

                BoardInfo.Layout.BoardHeight = b.Top + BoardInfo.Layout.BuildingSize;
            }

            //
            //  calulate the Width
            var rightTile = Tiles.LastColumn().First();

            double left = BoardInfo.Layout.Left(rightTile.Tile.TileKey) ;

            BoardInfo.Layout.BoardWidth = left + 2 * hexSize + harborSize / 2.0;

            foreach (var tile in Tiles.LastColumn())
            {
                if (tile is null) continue;
                var harbors = Harbors.FindAnyHarbor(tile.Tile.TileKey);
                if (harbors is null) continue;
                foreach (HarborViewModel h in harbors)
                {
                    if (h.Harbor.Side == HexSide.Top || h.Harbor.Side == HexSide.Bottom) continue;

                    var right = h.Left + harborSize;
                    BoardInfo.Layout.BoardWidth = right;
                    return;

                }
            }

        }

    }

}
