using System;
using System.Collections.Generic;
using System.Linq;
using Catan3.Utility;
namespace Catan3.Models
{
    /// <summary>
    ///     this order needs to match the CalculateHexGeometry PointCollection order
    /// </summary>
   
    public static class BuildingModelExtensions
    {
        public static List<(HexPosition position, Direction direction)> Aliases(this BuildingKey key)
        {
            List<(HexPosition, Direction)> directions = [];
            switch (key.Position)
            {
                case HexPosition.TopRight:
                    directions.Add((HexPosition.BottomRight, Direction.North));
                    directions.Add((HexPosition.Left, Direction.NorthEast));
                    break;
                case HexPosition.Right:
                    directions.Add((HexPosition.BottomLeft, Direction.NorthEast));
                    directions.Add((HexPosition.TopLeft, Direction.SouthEast));
                    break;
                case HexPosition.BottomRight:
                    directions.Add((HexPosition.TopRight, Direction.South));
                    directions.Add((HexPosition.Left, Direction.SouthEast));
                    break;
                case HexPosition.BottomLeft:
                    directions.Add((HexPosition.Right, Direction.SouthWest));
                    directions.Add((HexPosition.TopLeft, Direction.South));
                    break;
                case HexPosition.Left:
                    directions.Add((HexPosition.BottomRight, Direction.NorthWest));
                    directions.Add((HexPosition.TopRight, Direction.SouthWest));
                    break;
                case HexPosition.TopLeft:
                    directions.Add((HexPosition.Right, Direction.NorthWest));
                    directions.Add((HexPosition.BottomLeft, Direction.North));
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
                foreach ((HexPosition position, Direction direction) in aliases)
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

    public partial class BuildingKey
    {

        public override string ToString()
        {
            return $"[{this.TileKey}-{Position}]";
        }
        public static BuildingKey? FromString(string str)
        {
            string[] tokens = str.Split(["[", "]", "-"], StringSplitOptions.RemoveEmptyEntries);
            if (tokens is null) return null;
            if (tokens.Length != 2) return null;
            var tileCoord = TileKey.FromString(tokens[0]);
            if (tileCoord is null) return null;
            var buildingPos = (HexPosition)Enum.Parse(typeof(HexPosition), tokens[1]);
            return new BuildingKey(tileCoord, buildingPos);
        }
        public override bool Equals(object? obj)
        {
            return obj is not null && obj is BuildingKey key &&
                   key.Position == this.Position &&
                   key.TileKey == this.TileKey;
        }
        public override int GetHashCode() => HashCode.Combine(TileKey, Position);
        public static BuildingKey Default => new(TileKey.Default, HexPosition.None);
        public static bool operator ==(BuildingKey left, BuildingKey right)
        {
            if (left is null || right is null)
            {
                return false;
            }
            if (ReferenceEquals(left, right))
            {
                return true;
            }
            return left.Equals(right);
        }
        public static bool operator !=(BuildingKey left, BuildingKey right) => !( left == right );
    }
}
