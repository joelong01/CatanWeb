using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Catan3.Shared.Models;
using Catan3.Shared.Utility;
using Catan3.Shared.Extensions;

namespace Catan3.GameService.Factory
{
    public static class GameFactory
    {
        public static GameModel CreateGame(GameType gameType, IList<string> playerIds, string gameName)
        {
            IGameMetadata gameInfo;
            if (gameType == GameType.Regular)
            {
                gameInfo = RegularBoardInfo.Default;
            }
            else if (gameType == GameType.Expansion)
            {
                gameInfo = ExpansionBoardInfo.Default;
            }
            else
            {
                throw new NotImplementedException();
            }
            
            Debug.Assert((gameInfo.TileKeys.Count == gameInfo.Numbers.Count) && (gameInfo.TileKeys.Count == gameInfo.Resources.Count));
            
            List<PlayerModel> playerModels = playerIds.Select(Id => new PlayerModel { Id = Id }).ToList();
            if (playerIds.Count < gameInfo.ResourceRules.MinPlayers || playerIds.Count > gameInfo.ResourceRules.MaxPlayers)
            {
                throw new System.Exception($"{gameInfo.Description} must have players between {gameInfo.ResourceRules.MinPlayers} and {gameInfo.ResourceRules.MaxPlayers}. You gave {playerIds.Count}");
            }
            
            GameModel game = new GameModel
            {
                GameId = Guid.NewGuid().ToString(),
                GameName = gameName,
                CreatedTime = DateTime.UtcNow,
                GameType = gameType,
                Players = playerModels,
                CurrentPlayerId = playerModels.FirstOrDefault()?.Id ?? "",
                HouseRules = gameInfo.HouseRules,
                ResourceRules = gameInfo.ResourceRules,
                HasSupplementalBuildPhase = gameInfo.HasSupplemental,
                EntitlementPurchaseModel = gameInfo.PurchaseableEntitlements.ToList(),
                Tiles = [],
                Buildings = [],
                Roads = [],
                Harbors = [],
                GameResourcesModel = new ResourcesModel(),
                RollModel = new RollModel(),
                ActionFlags = new ActionFlags(),
                Robber = new RobberModel { Coordinates = HexCoordinates.Default }
            };
            
            for (int i = 0; i < gameInfo.TileKeys.Count; i++)
            {
                var tile = new TileModel
                {
                    ResourceTileType = gameInfo.Resources[i],
                    Number = gameInfo.Numbers[i],
                    TileKey = gameInfo.TileKeys[i]
                };
                game.Tiles.Add(tile);
            }
            
            foreach (var tile in game.Tiles)
            {
                foreach (HexPosition buildingPosition in Enum.GetValues(typeof(HexPosition)))
                {
                    if (buildingPosition == HexPosition.None) continue;
                    BuildingKey buildingKey = new BuildingKey(tile.TileKey, buildingPosition);
                    var building = game.Buildings.FindBuildingModel(buildingKey);
                    if (building is null)
                    {
                        BuildingModel buildingModel = new BuildingModel 
                        { 
                            BuildingKey = buildingKey, 
                            BuildingState = BuildingState.NotBuildable 
                        };
                        game.Buildings.Add(buildingModel);
                    }
                }
                
                foreach (HexSide roadPosition in Enum.GetValues(typeof(HexSide)))
                {
                    if (roadPosition == HexSide.None) continue;
                    var roadKey = new RoadKey(tile.TileKey, roadPosition);
                    var road = game.Roads.FindRoad(roadKey);
                    if (road is null)
                    {
                        road = new RoadModel 
                        { 
                            RoadKey = roadKey,
                            RoadState = RoadState.Unowned
                        };
                        game.Roads.Add(road);
                    }
                }
            }
            
            foreach (var harbor in gameInfo.Harbors)
            {
                game.Harbors.Add(harbor);
            }

            game.Shuffle();

            return game;
        }

        /// <summary>
        /// Can be called any time and returns a random valid board.
        /// Uses ReplayableRandom for deterministic behavior, matching Desktop app implementation.
        /// </summary>
        public static void Shuffle(this GameModel game)
        {
            // CRITICAL FIX: Use ReplayableRandom with game's RandomSeed and RandomIterations
            // This matches the Desktop app implementation exactly for deterministic behavior
            var random = new ReplayableRandom(game.RandomSeed, game.RandomIterations);
            int count = game.Tiles.Count;
            
            // NOTE: The validation loop below is INTENTIONALLY deterministic and thread-safe:
            // - Each GameStateMachine instance operates on isolated game data (per-game concurrency isolation)
            // - ReplayableRandom produces the same sequence for the same seed+iterations
            // - The loop will always take the same number of iterations for the same starting conditions
            // - This is NOT a race condition - it's deterministic game logic by design
            do
            {
                ShuffleList<TileModel, ResourceType>(game.Tiles, random,
                     tile => tile.ResourceTileType,
                     (tile, type) => tile.ResourceTileType = type);
                ShuffleList<TileModel, int>(game.Tiles, random,
                    tile => tile.Number,
                    (tile, number) => tile.Number = number);
                ShuffleList<HarborModel, HarborType>(game.Harbors, random,
                   harbor => harbor.HarborKey.HarborType,
                   (harbor, type) => harbor.HarborKey.HarborType = type);
                // Correct the placement of the number 7 on desert tiles
                EnsureDesertSeven(game);
            } while (!ValidateGame(game));

            // CRITICAL: Update RandomIterations after shuffling, matching Desktop app
            // This captures the total iterations needed (including validation loops) for replay consistency
            game.RandomIterations = random.Iterations;
            
            // 1/14/2025: Robber starts off the board so the first move can be to a desert tile, so do NOT put the robber on a desert tile
        }

        private static void ShuffleList<T, TValue>(IList<T> list, ReplayableRandom random, Func<T, TValue> valueSelector, Action<T, TValue> valueSetter)
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
            var deserts = game.Tiles.Where(t => t.ResourceTileType == ResourceType.Desert).ToList();
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

        public static bool ValidateGame(this GameModel Game)
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