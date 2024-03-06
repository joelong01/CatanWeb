using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
namespace Catan3.Models
{
    public partial class HouseRules : INotifyPropertyChanged
    {
        public int GoldTiles { get; set; } = 1;
        public bool WallsProtectCities { get; set; } = true;
        public bool HideBaronBeforeInvasion { get; set; } = false;
        public bool KnightMovesBaronBeforeRoll { get; set; } = true;
    }
    public partial class GameViewModel : INotifyPropertyChanged
    {
        public TileKey BaronTile {  get; set; }
        public string Name { get; set; } = "Regular";
        public bool HasSupplementalBuildPhase { get; set; } = false;
        public bool IsKnightsAndRobbers { get; set; } = false;
        public HouseRules HouseRules { get; set; } = new HouseRules();
        public IBoardInfo BoardInfo { get; set; }
        public PlayerViewModel? CurrentPlayer { get; set; }
        public ObservableCollection<TileViewModel> Tiles { get; set; } = [];
        public ObservableCollection<BuildingViewModel> Buildings { get; set; } = [];
        public ObservableCollection<PlayerViewModel> Players { get; } = [];
        public ObservableCollection<RoadViewModel> Roads { get; } = [];
        private GameViewModel() { throw new NotImplementedException(); }
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
                Roads.Add(roadView) ;
                
            }
            BaronTile = gameModel.BaronTile;
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
            var tileModel = Tiles.TileFromCoords(key.TileKey)?.Tile;
            Debug.Assert(tileModel is not null, "Bad TileKey");
            tiles.Add(tileModel);
            // get the aliases
            var aliases = key.Aliases();
            foreach ((BuildingPosition position, Direction direction) in aliases)
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
