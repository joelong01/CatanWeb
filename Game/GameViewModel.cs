using System.Collections.Generic;
using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using Catan3.Utility;


namespace Catan3.Models
{

    public partial class GameViewModel
     {

        public GameViewModel(GameModel gameModel, List<PlayerViewModel> playingPlayers)
        {
            if (gameModel.BoardSize == BoardSize.Regular)
            {
                BoardInfo = new RegularBoardInfo();
            }
            else
            {
                throw new NotImplementedException();
            }
            foreach (var tile in gameModel.Tiles)
            {
                Tiles.Add(new TileViewModel(tile, BoardInfo.Layout));
            }
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
            RobberTile = gameModel.RobberTile;
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
    }

}
