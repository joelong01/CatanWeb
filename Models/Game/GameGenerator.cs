using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace Catan3.Models
{

    /// <summary>
    ///     Static data about a game Board
    /// </summary>
    public partial class RegularBoardInfo : IBoardInfo
    {
        public RegularBoardLayout Layout { get; } = new RegularBoardLayout();
        public List<TileKey> TileKeys { get; } =
             [
                 new(-2, 0, 2),
                 new(-2, 1, 1),
                 new(-2, 2, 0),
                 new(-1, -1, 2),
                 new(-1, 0, 1),
                 new(-1, 1, 0),
                 new(-1, 2, -1),
                 new(0, -2, 2),
                 new(0, -1, 1),
                 new(0, 0, 0),
                 new(0, 1, -1),
                 new(0, 2, -2),
                 new(1, -2, 1),
                 new(1, -1, 0),
                 new(1, 0, -1),
                 new(1, 1, -2),
                 new(2, -2, 0),
                 new(2, -1, -1),
                 new(2, 0, -2)
             ];
        public List<ResourceType> Resources { get; } = [
            ResourceType.Desert,
            ResourceType.Brick,
            ResourceType.Brick,
            ResourceType.Brick,
            ResourceType.Ore,
            ResourceType.Ore,
            ResourceType.Ore,
            ResourceType.Sheep,
            ResourceType.Sheep,
            ResourceType.Sheep,
            ResourceType.Sheep,
            ResourceType.Wheat,
            ResourceType.Wheat,
            ResourceType.Wheat,
            ResourceType.Wheat,
            ResourceType.Wood,
            ResourceType.Wood,
            ResourceType.Wood,
            ResourceType.Wood
            ];
        public List<int> Numbers { get; } = [7, 2, 3, 3, 4, 4, 5, 5, 6, 6, 8, 8, 9, 9, 10, 10, 11, 11, 12];
    }
    public static class GameGenerator
    {
        public static GameModel CreateGame(BoardSize boardSize)
        {
            IBoardInfo boardInfo;
            if (boardSize == BoardSize.Regular)
            {
                boardInfo = new RegularBoardInfo();
            }
            else
            {
                throw new NotImplementedException();
            }
            GameModel game = new();
            for (int i = 0; i < boardInfo.TileKeys.Count; i++)
            {
                var tile = new TileModel()
                {
                    ResourceType = boardInfo.Resources[i],
                    Number = boardInfo.Numbers[i],
                    TileKey = boardInfo.TileKeys[i]
                };
                game.Tiles.Add(tile);
            }
            foreach (var tile in game.Tiles)
            {
                foreach (BuildingPosition buildingPosition in Enum.GetValues(typeof(BuildingPosition)))
                {
                    
                    if (buildingPosition == BuildingPosition.None) continue;
                    BuildingKey buildingKey = new(tile.TileKey, buildingPosition);
                    var building = game.FindBuilding(buildingKey);
                    if (building is null)
                    {
                        BuildingModel buildingModel = new(buildingKey, BuildingState.Empty);
                        game.Buildings.Add(buildingModel);
                    }
                }
               // if (tile.TileKey == new TileKey(0,0,0))
                foreach (RoadPosition roadPosition in Enum.GetValues(typeof(RoadPosition)))
                {
                    if (roadPosition == RoadPosition.None) continue;
                    var roadKey = new RoadKey(tile.TileKey, roadPosition);
                    var road = game.FindRoad(roadKey);
                    if (road is null )
                    {
                        road = new RoadModel(roadKey);
                        
                        game.Roads.Add(road);
                    }
                   
                }
            }
            
            
            game.Shuffle();
            return game;
        }
        /// <summary>
        /// 
        ///  can be called any time and returns a random valid board
        /// </summary>
        public static void Shuffle(this GameModel Game)
        {
            int count = Game.Tiles.Count;
            // Using DateTime.Now.Ticks to get the current time in ticks and using that as a seed
            Random random = new((int)(DateTime.Now.Ticks & 0x0000FFFF));
            int iters = 0;
            do
            {
                iters++;
                for (int i = 0; i < count; i++)
                {
                    int index = random.Next(0, count);
                    var x = Game.Tiles[index];
                    var y = Game.Tiles[i];
                    var xN = x.Number;
                    var yN = y.Number;
                    var xR = x.ResourceType;
                    var yR = y.ResourceType;
                    x.Number = yN;
                    x.ResourceType = yR;
                    y.ResourceType = xR;
                    y.Number = xN;
                }
                var tilesWithSeven = Game.Tiles.TilesWithNumber(7);
                var desertTiles = Game.Tiles.TilesWithResource(ResourceType.Desert);
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
            } while (!Game.ValidateGame());

            Game.BaronTile = Game.Tiles.TilesWithResource(ResourceType.Desert)[0].TileKey;
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
