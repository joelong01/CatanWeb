using System.Collections.Generic;
using System.Linq;
namespace Catan3.Models
{
    public static class BuildingModelExtensions
    {
        public static List<(BuildingPosition position, Direction direction)> Aliases(this BuildingKey key)
        {
            List<(BuildingPosition, Direction)> directions = [];
            switch (key.BuildingPosition)
            {
                case BuildingPosition.TopRight:
                    directions.Add((BuildingPosition.BottomRight, Direction.North));
                    directions.Add((BuildingPosition.Left, Direction.NorthEast));
                    break;
                case BuildingPosition.Right:
                    directions.Add((BuildingPosition.BottomLeft, Direction.NorthEast));
                    directions.Add((BuildingPosition.TopLeft, Direction.SouthEast));
                    break;
                case BuildingPosition.BottomRight:
                    directions.Add((BuildingPosition.TopRight, Direction.South));
                    directions.Add((BuildingPosition.Left, Direction.SouthEast));
                    break;
                case BuildingPosition.BottomLeft:
                    directions.Add((BuildingPosition.Right, Direction.SouthWest));
                    directions.Add((BuildingPosition.TopLeft, Direction.South));
                    break;
                case BuildingPosition.Left:
                    directions.Add((BuildingPosition.BottomRight, Direction.NorthWest));
                    directions.Add((BuildingPosition.TopRight, Direction.SouthWest));
                    break;
                case BuildingPosition.TopLeft:
                    directions.Add((BuildingPosition.Right, Direction.NorthWest));
                    directions.Add((BuildingPosition.BottomLeft, Direction.North));
                    break;
            }
            return directions;
        }
        public static BuildingModel? FindBuilding(this IEnumerable<BuildingModel> buildings, BuildingKey key)
        {
            if (buildings is null || !buildings.Any()) return null;
            var building = buildings.FirstOrDefault(b => b.BuildingKey == key);
            if (building is null)
            {
                var aliases = key.Aliases();
                foreach ((BuildingPosition position, Direction direction) in aliases)
                {
                    var aliasCoords = key.TileKey.GetAdjacentTile(direction);
                    var aliasKey = new BuildingKey(aliasCoords, position);
                    building = buildings.FirstOrDefault(b => b.BuildingKey == aliasKey);
                    if (building is not null)
                    {
                        return building;
                    }
                }
            }
            return null;
        }
        public static BuildingModel? FindBuilding(this IEnumerable<BuildingViewModel> viewModels, BuildingKey key)
        {
            if (viewModels is null || !viewModels.Any())
            {
                return null;
            }
            
            var buildings =  viewModels.Select(bvm => bvm.Building);
            return FindBuilding(buildings, key);
        }
    }
}
