using System.Collections.Generic;
using System.Linq;
namespace Catan3.Models
{
    public static class RoadExtensions
    {
        public static RoadModel? FindRoad(this IEnumerable<RoadModel> roads, RoadKey key)
        {
            if (roads is null || !roads.Any()) return null;
            var road = roads.FirstOrDefault(b => b.RoadKey == key);
            if (road is null)
            {
                var aliases = key.Aliases();
                foreach ((RoadPosition position, Direction direction) in aliases)
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
            return null;
        }
        public static List<(RoadPosition position, Direction direction)> Aliases(this RoadKey key)
        {
            List<(RoadPosition, Direction)> directions = [];
            switch (key.RoadPosition)
            {
                case RoadPosition.TopRight:
                    directions.Add((RoadPosition.BottomLeft, Direction.NorthEast));
                   
                    break;
              
                case RoadPosition.BottomRight:
                    directions.Add((RoadPosition.TopLeft, Direction.SouthEast));
                    break;
                case RoadPosition.BottomLeft:
                    directions.Add((RoadPosition.TopRight, Direction.SouthWest));
                    break;
                case RoadPosition.Bottom:
                    directions.Add((RoadPosition.Top, Direction.South));
                    break;
                case RoadPosition.Top:
                    directions.Add((RoadPosition.Bottom, Direction.North));
                    break;
                case RoadPosition.TopLeft:
                    directions.Add((RoadPosition.BottomRight, Direction.NorthWest));
                    break;
            }
            return directions;
        }
    }
}
