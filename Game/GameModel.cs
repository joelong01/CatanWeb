
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Text.Json.Serialization;
using Catan10.Models;
using Catan3.Utility;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Catan3.Models
{
   
    public partial class GameModel : ObservableObject
    {
      

        [ObservableProperty]
        private TurnRollModel? _turnRollModel = null; // nullable as it gets set to null when the turn is over and the new one is created when the turn is started

        [ObservableProperty]
        private GameType _gameType = GameType.Regular;

        [ObservableProperty]
        private GameState _gameState = GameState.WaitingForNewGame;

        [ObservableProperty]
        private bool _hasSupplementalBuildPhase = false;

        [ObservableProperty]
        private List<PlayerModel> _players = [];

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

        [ObservableProperty]
        private HouseRules _houseRules = new();

        [ObservableProperty]
        private string _currentPlayerId = string.Empty;

        [ObservableProperty]
        private GameRollModel _gameRollModel = new();
        //
        //  keep track of the total resources ever generated in the game by everyone
        [ObservableProperty]
        private ResourcesModel _gameResourcesModel = new();

        public override string ToString()
        {
            return $"State={GameState} CurrentPlayer={CurrentPlayerId}";
        }

       

        public GameModel(GameType gametype, bool hassupplementalbuildphase, List<PlayerModel> players)
        {
            GameType = gametype;
            HasSupplementalBuildPhase = hassupplementalbuildphase;
            Players = players;
        }
        [JsonConstructor]
        public GameModel()
        {
            Players = [];
            GameType = GameType.Regular;
            HasSupplementalBuildPhase = false;
        }
        /// <summary>
        ///     Add up all the stars for the given resource top
        /// </summary>
        /// <param name="tileType"></param>
        /// <returns></returns>
        public int StarCount(ResourceType tileType)
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
        /// <summary>
        ///     Given a building key, get the count of stars for that building
        /// </summary>
        /// <param name="key"></param>
        /// <returns></returns>

        public int BuildingStars(BuildingKey key)
        {
            var adjacentTiles = TilesForBuildings(key);
            var stars = adjacentTiles.Stars();
            return stars;
        }

        public string Serialize()
        {
            string gameModelJson = String.Empty;
            FunctionTimer.CallTimedFunction("GameModel.Serialize", () =>
            {
                gameModelJson = JsonSerializer.Serialize(this);
               
            });

            return gameModelJson;

        }


    }

   
}
