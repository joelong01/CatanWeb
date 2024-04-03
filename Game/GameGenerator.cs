using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Catan3.Utility;

namespace Catan3.Models
{

    public static class GameGenerator
    {
        public static GameModel CreateGame(CatanGame gameType, List<string> players)
        {
            IBoardInfo boardInfo;
            if (gameType == CatanGame.Regular)
            {
                boardInfo = new RegularBoardInfo();
            }
            else if (gameType == CatanGame.Expansion)
            {
                boardInfo = new ExpansionBoardInfo();
            }
            else
            {
                throw new NotImplementedException();
            }

            Debug.Assert(( boardInfo.TileKeys.Count == boardInfo.Numbers.Count ) && ( boardInfo.TileKeys.Count == boardInfo.Resources.Count ));
            List<PlayerModel> playerModels = players.Select(Id => new PlayerModel(Id)).ToList();
            GameModel game = new(gameType, boardInfo.HasSupplemental, playerModels);

          
            for (int i = 0; i < boardInfo.TileKeys.Count; i++)
            {
                var tile = new TileModel()
                {
                    ResourceTileType = boardInfo.Resources[i],
                    Number = boardInfo.Numbers[i],
                    TileKey = boardInfo.TileKeys[i]
                };
                game.Tiles.Add(tile);
            }
            foreach (var tile in game.Tiles)
            {
                foreach (HexPosition buildingPosition in Enum.GetValues(typeof(HexPosition)))
                {

                    if (buildingPosition == HexPosition.None) continue;
                    BuildingKey buildingKey = new(tile.TileKey, buildingPosition);
                    var building = game.FindBuildingModel(buildingKey);
                    if (building is null)
                    {
                        BuildingModel buildingModel = new(buildingKey, BuildingState.Empty);
                        game.Buildings.Add(buildingModel);
                    }
                }
                // if (tile.HexCoordinates == new HexCoordinates(0,0,0))
                foreach (HexSide roadPosition in Enum.GetValues(typeof(HexSide)))
                {
                    if (roadPosition == HexSide.None) continue;
                    var roadKey = new RoadKey(tile.TileKey, roadPosition);
                    var road = game.FindRoad(roadKey);
                    if (road is null)
                    {
                        road = new RoadModel(roadKey);

                        game.Roads.Add(road);
                    }

                }
            }

            foreach (var harbor in boardInfo.Harbors)
            {
                game.Harbors.Add(harbor);
            }

            game.Shuffle();

            game.Robber.Coordinates = game.Tiles.TilesWithResource(ResourceTileType.Desert)[0].TileKey;
            return game;
        }
        /// <summary>
        /// 
        ///  can be called any time and returns a random valid board
        /// </summary>
        public static void Shuffle(this GameModel game)
        {
            int count = game.Tiles.Count;
            // Using DateTime.Now.Ticks to get the current time in ticks and using that as a seed
            Random random = new((int)(DateTime.Now.Ticks & 0x0000FFFF));
            int iters = 0;
            do
            {
                iters++;
                for (int i = 0; i < count; i++)
                {
                    int index = random.Next(0, count);
                    var x = game.Tiles[index];
                    var y = game.Tiles[i];

                    var xR = x.ResourceTileType;
                    var yR = y.ResourceTileType;
            
                    x.ResourceTileType = yR;
                    y.ResourceTileType = xR;
       
                }

                for (int i = 0; i < count; i++)
                {
                    int index = random.Next(0, count);
                    var x = game.Tiles[index];
                    var y = game.Tiles[i];
                    var xN = x.Number;
                    var yN = y.Number;
              
                    x.Number = yN;
          
                    y.Number = xN;
                }
                var tilesWithSeven = game.Tiles.TilesWithNumber(7);
                var desertTiles = game.Tiles.TilesWithResource(ResourceTileType.Desert);
                Debug.Assert(tilesWithSeven.Count == desertTiles.Count);
                // if any of the deserts have a non-7 number, swap with the tile that has a 7
                for (int i = 0; i < tilesWithSeven.Count; i++)
                {
                    int tempNumber = desertTiles[i].Number;
                    desertTiles[i].Number = 7;
                    tilesWithSeven[i].Number = tempNumber;
                }
                if (iters == 500)
                {
                    Debug.Assert(false, "Too many iterations");
                }
            } while (!game.ValidateGame());

            count = game.Harbors.Count;
            for (int i = 0; i < count; i++)
            {
                int index = random.Next(0, count);
                var x = game.Harbors[index].HarborType;
                var y = game.Harbors[i].HarborType;
                game.Harbors[i].HarborType = x;
                game.Harbors[index].HarborType = y;
            }

            // this.TraceMessage($"valid.  iters: {iters}");
        }
        private static bool ValidateGame(this GameModel Game)
        {
            foreach (var tile in Game.Tiles.TilesWithNumber(6))
            {
                var adjacent = Game.Tiles.AdjacentTiles(tile);
                if (adjacent != null && adjacent.TilesWithSixOrEight().Count != 0)
                    return false;
            }
            foreach (var tile in Game.Tiles.TilesWithNumber(8))
            {
                var adjacent = Game.Tiles.AdjacentTiles(tile);
                if (adjacent != null && adjacent.TilesWithSixOrEight().Count != 0)
                    return false;
            }
            return true;
        }
    }
}
