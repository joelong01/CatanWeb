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
            return null;
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
