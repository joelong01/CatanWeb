using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text.Json;
using Catan3.Utility;

namespace Catan3.Models
{

    public static class GameFactory
    {
        public static GameModel CreateGame(GameType gameType, List<string> players)
        {
            IBoardInfo boardInfo;
            if (gameType == GameType.Regular)
            {
                boardInfo =  RegularBoardInfo.Default;
            }
            else if (gameType == GameType.Expansion)
            {
                boardInfo =  ExpansionBoardInfo.Default;
            }
            else
            {
                throw new NotImplementedException();
            }

            Debug.Assert(( boardInfo.TileKeys.Count == boardInfo.Numbers.Count ) && ( boardInfo.TileKeys.Count == boardInfo.Resources.Count ));
            List<PlayerModel> playerModels = players.Select(Id => new PlayerModel(Id)).ToList();
            GameModel game = new(gameType, boardInfo.HasSupplemental, playerModels)
            {
                CurrentPlayerId = players[0]
            };

            game.HouseRules = boardInfo.HouseRules;

            for (int i = 0; i < boardInfo.TileKeys.Count; i++)
            {
                var tile = new TileModel()
                {
                    ResourceTileType = boardInfo.Resources[i],
                    Number = boardInfo.Numbers[i],
                    TileKey = boardInfo.TileKeys[i]
                };
                game.Tiles.InsertSorted(tile);
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
                        game.Buildings.InsertSorted(buildingModel);
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

                        game.Roads.InsertSorted(road);
                    }

                }
            }

            foreach (var harbor in boardInfo.Harbors)
            {
                game.Harbors.InsertSorted(harbor);
            }

            game.Shuffle();
         

            return game;
        }
        /// <summary>
        /// 
        ///  can be called any time and returns a random valid board
        /// </summary>
        public static void Shuffle(this GameModel game)
        {

            Random random = new();
            int count = game.Tiles.Count;
            do
            {
                ShuffleList<TileModel, ResourceTileType>(game.Tiles, random,
                     tile => tile.ResourceTileType,
                     (tile, type) => tile.ResourceTileType = type);

                ShuffleList<TileModel, int>(game.Tiles, random,
                    tile => tile.Number,
                    (tile, number) => tile.Number = number);

                ShuffleList<HarborModel, HarborType>(game.Harbors, random,
                   harbor => harbor.HarborType,
                   (harbor, type) => harbor.HarborType = type);

                // Correct the placement of the number 7 on desert tiles
                EnsureDesertSeven(game);
            } while (!ValidateGame(game));

            // Place the robber on the first desert tile found
            game.Robber.Coordinates = game.Tiles.FirstOrDefault(tile => tile.ResourceTileType == ResourceTileType.Desert)?.TileKey ?? throw new Exception("there must be a desert tile for the game to work");
            

        }


        private static void ShuffleList<T, TValue>(IList<T> list, Random random, Func<T, TValue> valueSelector, Action<T, TValue> valueSetter)
        {
            int count = list.Count;
            for (int i = 0; i < count; i++)
            {
                int j = random.Next(i, count); // Correct Fisher-Yates shuffle
                var temp = valueSelector(list[j]);
                valueSetter(list[j], valueSelector(list[i]));
                valueSetter(list[i], temp);
            }
        }

        private static void EnsureDesertSeven(GameModel game)
        {
            var deserts = game.Tiles.Where(t => t.ResourceTileType == ResourceTileType.Desert).ToList();
            var sevens = game.Tiles.Where(t => t.Number == 7).ToList();

            Debug.Assert(deserts.Count == sevens.Count, "Mismatch between deserts and tiles with number 7");

            for (int i = 0; i < deserts.Count; i++)
            {
                if (deserts[i].Number != 7)
                {
                    sevens[i].Number = deserts[i].Number;
                    deserts[i].Number = 7;
                }
            }
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
