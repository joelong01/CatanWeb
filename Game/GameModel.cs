
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Text.Json.Serialization;
using Catan3.Utility;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Catan3.Models
{
    public partial class GameModel : ObservableObject
    {
        [ObservableProperty]
        private CatanGame _gameType = CatanGame.Regular;

        [ObservableProperty]
        private bool _hasSupplementalBuildPhase = false;

        [ObservableProperty]
        private List<PlayerModel> _players;

        [ObservableProperty]
        private ObservableCollection<TileModel> _tiles = [];

        [ObservableProperty]
        private ObservableCollection<BuildingModel> _buildings = [];

        [ObservableProperty]
        private ObservableCollection<RoadModel> _roads = [];

        [ObservableProperty]
        private ObservableCollection<HarborModel> _harbors = [];

        [ObservableProperty]
        private RobberModel _robber = new();

        public GameModel(CatanGame gametype, bool hassupplementalbuildphase, List<PlayerModel> players)
        {
            GameType = gametype;
            HasSupplementalBuildPhase = hassupplementalbuildphase;
            Players = players;
        }
        [JsonConstructor]
        public GameModel()
        {
            _players = [];
            _gameType = CatanGame.Regular;
            _hasSupplementalBuildPhase = false;
        }

        public int StarCount(ResourceTileType tileType)
        {
            var total = this.Tiles.Where(tile => tile.ResourceTileType == tileType)
                .Sum(tile => tile.Stars);

            return total;
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
            var tileModel = Tiles.TileFromCoords(key.HexCoordinates);
            Debug.Assert(tileModel is not null, "Bad HexCoordinates");
            tiles.Add(tileModel);
            // get the aliases
            var aliases = key.Aliases();
            foreach ((_, Direction direction) in aliases)
            {
                var neighbor = Tiles.TileFromCoords(tileModel.TileKey.GetAdjacentTile(direction));
                if (neighbor is not null)
                {
                    tiles.Add(neighbor);
                }
            }
            return tiles;
        }


    }
}
