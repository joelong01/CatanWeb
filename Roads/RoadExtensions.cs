using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Catan3.Utility;
namespace Catan3.Models
{
    public static class RoadExtensions
    {
        public static List<RoadModel> OwnedAdjacentRoadsNotCounted(this GameModel gameModel, RoadModel road, List<RoadModel> owned, RoadModel? blockedFork, out bool adjacentFork)
        {
            List<RoadModel> list = [];
            var ownedAdjacentRoads = gameModel.Roads.AdjacentRoads(road.RoadKey).Where(r=> r.OwnerId == road.OwnerId).ToList();
            foreach (RoadModel r in ownedAdjacentRoads)
            {
                Debug.Assert(r.OwnerId == road.OwnerId);
                var buildingBetween = gameModel.BuildingBetweenRoads(road.RoadKey, r.RoadKey);
              
                if (buildingBetween is not null && buildingBetween.OwnerId is not null && buildingBetween.OwnerId != r.OwnerId) continue; // if there is a building we don't own there, we stop looking
                
                // dont' add it twice
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
                    throw new GameException($"Roadkey {key} has HexSide=None");
                case HexSide.Top:
                    adjacentKeys.Add(new RoadKey(key.TileKey, HexSide.TopRight));
                    adjacentKeys.Add(new RoadKey(key.TileKey, HexSide.TopLeft));
                    adjacentKeys.Add(new RoadKey(key.TileKey.North, HexSide.BottomRight));
                    adjacentKeys.Add(new RoadKey(key.TileKey.NorthWest, HexSide.BottomLeft));
                    break;
                case HexSide.TopRight:
                    adjacentKeys.Add(new RoadKey(key.TileKey, HexSide.BottomRight));
                    adjacentKeys.Add(new RoadKey(key.TileKey, HexSide.Top));
                    adjacentKeys.Add(new RoadKey(key.TileKey.NorthEast, HexSide.TopLeft));
                    adjacentKeys.Add(new RoadKey(key.TileKey.NorthEast, HexSide.Bottom));
                    break;
                case HexSide.BottomRight:
                    adjacentKeys.Add(new RoadKey(key.TileKey, HexSide.TopRight));
                    adjacentKeys.Add(new RoadKey(key.TileKey, HexSide.Bottom));
                    adjacentKeys.Add(new RoadKey(key.TileKey.SouthEast, HexSide.Top));
                    adjacentKeys.Add(new RoadKey(key.TileKey.SouthEast, HexSide.BottomLeft));
                    break;
                case HexSide.Bottom:
                    adjacentKeys.Add(new RoadKey(key.TileKey, HexSide.BottomLeft));
                    adjacentKeys.Add(new RoadKey(key.TileKey, HexSide.BottomRight));
                    adjacentKeys.Add(new RoadKey(key.TileKey.South, HexSide.TopLeft));
                    adjacentKeys.Add(new RoadKey(key.TileKey.South, HexSide.TopRight));
                    break;
                case HexSide.BottomLeft:
                    adjacentKeys.Add(new RoadKey(key.TileKey, HexSide.Bottom));
                    adjacentKeys.Add(new RoadKey(key.TileKey, HexSide.TopLeft));
                    adjacentKeys.Add(new RoadKey(key.TileKey.SouthWest, HexSide.Top));
                    adjacentKeys.Add(new RoadKey(key.TileKey.SouthWest, HexSide.BottomRight));
                    break;
                case HexSide.TopLeft:
                    adjacentKeys.Add(new RoadKey(key.TileKey, HexSide.Top));
                    adjacentKeys.Add(new RoadKey(key.TileKey, HexSide.BottomLeft));
                    adjacentKeys.Add(new RoadKey(key.TileKey.NorthWest, HexSide.TopRight));
                    adjacentKeys.Add(new RoadKey(key.TileKey.NorthWest, HexSide.Bottom));
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
                    var aliasKey = new RoadKey(aliasCoords, position);
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
    }
}
