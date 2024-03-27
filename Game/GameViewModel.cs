using System.Collections.Generic;
using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using Catan3.Utility;
using System.Linq;


namespace Catan3.Models
{

    public partial class GameViewModel
    {

        public GameViewModel(GameModel gameModel, List<PlayerViewModel> playingPlayers)
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

            Tiles = CreateAndSortTileViewModelList(gameModel.Tiles);
           
            foreach (var building in gameModel.Buildings)
            {
                Buildings.Add(new BuildingViewModel(building, BoardInfo.Layout));
            }
            foreach (var player in playingPlayers)
            {
                player.Player = new PlayerModel(playingPlayers.IndexOf(player));
                Players.Add(player);
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
            for (int i = 0;i<sortedTiles.Count; i++)
            {
                var tvm = new TileViewModel(sortedTiles[i], BoardInfo.Layout)
                {
                    Index = i
                };
                result.Add(tvm);
            }

            return result;
        }

    }

}
