using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Catan3.Shared.Models;
using Catan3.Shared.Utility;

namespace Catan3.Shared.Extensions
{
    public static class RoadModelExtensions
    {
        public static List<RoadModel> OwnedAdjacentRoadsNotCounted(this GameModel gameModel, RoadModel road, List<RoadModel> owned, RoadModel? blockedFork, out bool adjacentFork)
        {
            List<RoadModel> list = [];
            var ownedAdjacentRoads = gameModel.Roads.AdjacentRoads(road.RoadKey).Where(r => r.OwnerId == road.OwnerId).ToList();
            foreach (RoadModel r in ownedAdjacentRoads)
            {
                Debug.Assert(r.OwnerId == road.OwnerId);
                var buildingBetween = gameModel.BuildingBetweenRoads(road.RoadKey, r.RoadKey);

                if (buildingBetween is not null && buildingBetween.OwnerId is not null && buildingBetween.OwnerId != r.OwnerId) continue; // if there is a building we don't own there, we stop looking

                // don't add it twice
                if (!owned.Contains(r))
                {
                    list.Add(r);
                }
            }
            adjacentFork = false;
            if (blockedFork is not null && list.Contains(blockedFork))
            {
                list.Remove(blockedFork);
                adjacentFork = true;
            }
            return list;
        }

        public static List<RoadModel> AdjacentRoads(this IEnumerable<RoadModel> roads, RoadKey key)
        {
            List<RoadModel> result = [];
            List<RoadKey> adjacentKeys = [];
            switch (key.HexSide)
            {
                case HexSide.None:
                    throw new System.Exception($"Roadkey {key} has HexSide=None");
                case HexSide.Top:
                    adjacentKeys.Add(new RoadKey { TileKey = key.TileKey, HexSide = HexSide.TopRight });
                    adjacentKeys.Add(new RoadKey { TileKey = key.TileKey, HexSide = HexSide.TopLeft });
                    adjacentKeys.Add(new RoadKey { TileKey = key.TileKey.North, HexSide = HexSide.BottomRight });
                    adjacentKeys.Add(new RoadKey { TileKey = key.TileKey.NorthWest, HexSide = HexSide.BottomLeft });
                    break;
                case HexSide.TopRight:
                    adjacentKeys.Add(new RoadKey { TileKey = key.TileKey, HexSide = HexSide.BottomRight });
                    adjacentKeys.Add(new RoadKey { TileKey = key.TileKey, HexSide = HexSide.Top });
                    adjacentKeys.Add(new RoadKey { TileKey = key.TileKey.NorthEast, HexSide = HexSide.TopLeft });
                    adjacentKeys.Add(new RoadKey { TileKey = key.TileKey.NorthEast, HexSide = HexSide.Bottom });
                    break;
                case HexSide.BottomRight:
                    adjacentKeys.Add(new RoadKey { TileKey = key.TileKey, HexSide = HexSide.TopRight });
                    adjacentKeys.Add(new RoadKey { TileKey = key.TileKey, HexSide = HexSide.Bottom });
                    adjacentKeys.Add(new RoadKey { TileKey = key.TileKey.SouthEast, HexSide = HexSide.Top });
                    adjacentKeys.Add(new RoadKey { TileKey = key.TileKey.SouthEast, HexSide = HexSide.BottomLeft });
                    break;
                case HexSide.Bottom:
                    adjacentKeys.Add(new RoadKey { TileKey = key.TileKey, HexSide = HexSide.BottomLeft });
                    adjacentKeys.Add(new RoadKey { TileKey = key.TileKey, HexSide = HexSide.BottomRight });
                    adjacentKeys.Add(new RoadKey { TileKey = key.TileKey.South, HexSide = HexSide.TopLeft });
                    adjacentKeys.Add(new RoadKey { TileKey = key.TileKey.South, HexSide = HexSide.TopRight });
                    break;
                case HexSide.BottomLeft:
                    adjacentKeys.Add(new RoadKey { TileKey = key.TileKey, HexSide = HexSide.Bottom });
                    adjacentKeys.Add(new RoadKey { TileKey = key.TileKey, HexSide = HexSide.TopLeft });
                    adjacentKeys.Add(new RoadKey { TileKey = key.TileKey.SouthWest, HexSide = HexSide.Top });
                    adjacentKeys.Add(new RoadKey { TileKey = key.TileKey.SouthWest, HexSide = HexSide.BottomRight });
                    break;
                case HexSide.TopLeft:
                    adjacentKeys.Add(new RoadKey { TileKey = key.TileKey, HexSide = HexSide.Top });
                    adjacentKeys.Add(new RoadKey { TileKey = key.TileKey, HexSide = HexSide.BottomLeft });
                    adjacentKeys.Add(new RoadKey { TileKey = key.TileKey.NorthWest, HexSide = HexSide.TopRight });
                    adjacentKeys.Add(new RoadKey { TileKey = key.TileKey.NorthWest, HexSide = HexSide.Bottom });
                    break;
            }
            foreach (var roadKey in adjacentKeys)
            {
                RoadModel? road = roads.FindRoad(roadKey);
                if (road is not null)
                {
                    result.Add(road);
                }
            }
            return result;
        }

        public static RoadModel? FindRoad(this IEnumerable<RoadModel> roads, RoadKey key)
        {
            if (roads is null || !roads.Any()) return null;
            var road = roads.FirstOrDefault(b => b.RoadKey == key);
            if (road is null)
            {
                var aliases = key.Aliases();
                foreach ((HexSide position, Direction direction) in aliases)
                {
                    var aliasCoords = key.TileKey.GetAdjacentTile(direction);
                    var aliasKey = new RoadKey { TileKey = aliasCoords, HexSide = position };
                    road = roads.FirstOrDefault(b => b.RoadKey == aliasKey);
                    if (road is not null)
                    {
                        return road;
                    }
                }
            }
            return road;
        }

        public static List<(HexSide position, Direction direction)> Aliases(this RoadKey key)
        {
            List<(HexSide, Direction)> directions = [];
            switch (key.HexSide)
            {
                case HexSide.TopRight:
                    directions.Add((HexSide.BottomLeft, Direction.NorthEast));
                    break;
                case HexSide.BottomRight:
                    directions.Add((HexSide.TopLeft, Direction.SouthEast));
                    break;
                case HexSide.BottomLeft:
                    directions.Add((HexSide.TopRight, Direction.SouthWest));
                    break;
                case HexSide.Bottom:
                    directions.Add((HexSide.Top, Direction.South));
                    break;
                case HexSide.Top:
                    directions.Add((HexSide.Bottom, Direction.North));
                    break;
                case HexSide.TopLeft:
                    directions.Add((HexSide.BottomRight, Direction.NorthWest));
                    break;
            }
            return directions;
        }

        /// <summary>
        /// Inserts an item into a sorted list maintaining sort order
        /// </summary>
        /// <typeparam name="T">Type that implements IComparable</typeparam>
        /// <param name="list">The sorted list to insert into</param>
        /// <param name="item">The item to insert</param>
        public static void InsertSorted<T>(this IList<T> list, T item) where T : IComparable<T>
        {
            if (list.Count == 0)
            {
                list.Add(item);
                return;
            }

            int index = 0;
            while (index < list.Count && list[index].CompareTo(item) < 0)
            {
                index++;
            }
            list.Insert(index, item);
        }
    }
}