using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Catan10.Models;
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
        /// <summary>
        ///     returns the list of all buildings within one position of the BuildingKey
        /// </summary>
        /// <param name="gameModel"></param>
        /// <param name="key"></param>
        /// <returns></returns>
        public static List<BuildingModel> AdjacentBuildings(this IList<BuildingModel> collection, BuildingKey key)
        {
            var result = new List<BuildingModel>();
            var keys = new List<BuildingKey>();
            BuildingModel? building;
            switch (key.Position)
            {
                case HexPosition.Right:
                    keys.Add(new BuildingKey(key.HexCoordinates, HexPosition.TopRight));
                    keys.Add(new BuildingKey(key.HexCoordinates, HexPosition.BottomRight));
                    keys.Add(new BuildingKey(key.HexCoordinates.NorthEast, HexPosition.BottomRight));
                    break;
                case HexPosition.BottomRight:
                    keys.Add(new BuildingKey(key.HexCoordinates, HexPosition.Right));
                    keys.Add(new BuildingKey(key.HexCoordinates, HexPosition.BottomLeft));
                    keys.Add(new BuildingKey(key.HexCoordinates.South, HexPosition.Right));
                    break;
                case HexPosition.BottomLeft:
                    keys.Add(new BuildingKey(key.HexCoordinates, HexPosition.Left));
                    keys.Add(new BuildingKey(key.HexCoordinates, HexPosition.BottomRight));
                    keys.Add(new BuildingKey(key.HexCoordinates.South, HexPosition.Left));
                    break;
                case HexPosition.Left:
                    keys.Add(new BuildingKey(key.HexCoordinates, HexPosition.TopLeft));
                    keys.Add(new BuildingKey(key.HexCoordinates, HexPosition.BottomLeft));
                    keys.Add(new BuildingKey(key.HexCoordinates.NorthWest, HexPosition.BottomLeft));
                    break;
                case HexPosition.TopLeft:
                    keys.Add(new BuildingKey(key.HexCoordinates, HexPosition.TopRight));
                    keys.Add(new BuildingKey(key.HexCoordinates, HexPosition.Left));
                    keys.Add(new BuildingKey(key.HexCoordinates.NorthWest, HexPosition.TopRight));
                    break;
                case HexPosition.TopRight:
                    keys.Add(new BuildingKey(key.HexCoordinates, HexPosition.TopLeft));
                    keys.Add(new BuildingKey(key.HexCoordinates, HexPosition.Right));
                    keys.Add(new BuildingKey(key.HexCoordinates.North, HexPosition.Right));
                    break;
                case HexPosition.None:
                    break;
            }
            foreach (var buildingKey in keys)
            {
                building = collection.FindBuildingModel(buildingKey);
                if (building is not null) result.Add(building);
            }

            return result;
        }

        public static BuildingViewModel? FindBuildingViewModel(this IEnumerable<BuildingViewModel> buildings, BuildingKey key)
        {
            if (buildings is null || !buildings.Any()) return null;
            var building = buildings.FirstOrDefault(b => b.Building.BuildingKey == key);
            if (building is null)
            {
                var aliases = key.Aliases();
                foreach ((HexPosition position, Direction direction) in aliases)
                {
                    var aliasCoords = key.HexCoordinates.GetAdjacentTile(direction);
                    var aliasKey = new BuildingKey(aliasCoords, position);
                    building = buildings.FirstOrDefault(b => b.Building.BuildingKey == aliasKey);
                    if (building is not null)
                    {
                        return building;
                    }
                }

                return null;
            }
            return building;
        }
        public static BuildingModel? FindBuildingModel(this IEnumerable<BuildingModel> buildings, BuildingKey key)
        {
            if (buildings is null || !buildings.Any()) return null;
            var building = buildings.FirstOrDefault(b => b.BuildingKey == key);
            if (building is null)
            {
                var aliases = key.Aliases();
                foreach ((HexPosition position, Direction direction) in aliases)
                {
                    var aliasCoords = key.HexCoordinates.GetAdjacentTile(direction);
                    var aliasKey = new BuildingKey(aliasCoords, position);
                    building = buildings.FirstOrDefault(b => b.BuildingKey == aliasKey);
                    if (building is not null)
                    {
                        return building;
                    }
                }
            }
            return building;
        }

        public static BuildingModel GetBuildingOrThrow(this IEnumerable<BuildingModel> buildings, BuildingKey key)
        {
            return buildings.FindBuildingModel(key) ?? throw new GameException($"Building {key} not found");
        }
        
        /// <summary>
        ///     e.g. GameViewModel.GameModel.Buildings.BuildingsInTile(new HexCoordinates(0,0,0)) returns all the buildings in the center tile
        /// </summary>
        /// <param name="collection"></param>
        /// <param name="coordinates"></param>
        /// <returns></returns>
        public static List<BuildingModel> BuildingsInTile(this IList<BuildingModel> collection, HexCoordinates coordinates)
        {
            List<BuildingModel> result = [];
            foreach (HexPosition pos in Enum.GetValues(typeof(HexPosition)))
            {
                if (pos == HexPosition.None) continue;
                var building = collection.FindBuildingModel(new BuildingKey(coordinates, pos));
                if (building is not null)
                {
                    result.Add(building);
                }

            }

            return result;
        }

        /// <summary>
        ///     returns the list of buildings that are owned in the tile
        /// </summary>
        /// <param name="tile"></param>
        /// <returns></returns>

        public static List<BuildingModel> OwnedBuildings(this IList<BuildingModel> collection, HexCoordinates coordinates)
        {
            List<BuildingModel> result = [];
            foreach (HexPosition pos in Enum.GetValues(typeof(HexPosition)))
            {
                if (pos == HexPosition.None) continue;
                var building = collection.FindBuildingModel(new BuildingKey(coordinates, pos));
                if (building is not null && building.OwnerId is not null)
                {
                    result.Add(building);
                }

            }

            return result;

        }
        ///

        public static ResourcesModel Resources(this BuildingModel model, ResourceType resource)
        {
            ResourcesModel result = new ResourcesModel();
            if (model.BuildingState == BuildingState.City)
            {
                result.AddResource(resource, 2);
            }
            else if (model.BuildingState == BuildingState.Settlement)
            {
                result.AddResource(resource, 1);
            }
            else
            {
                Debug.Assert(false, "haven't implemented something yet...");
            }

            return result;
        }

    }

    public partial class BuildingKey
    {

        public override string ToString()
        {
            return $"[{this.HexCoordinates}-{Position}]";
        }

        public override bool Equals(object? obj)
        {
            return obj is not null && obj is BuildingKey key &&
                   key.Position == this.Position &&
                   key.HexCoordinates == this.HexCoordinates;
        }
        public override int GetHashCode() => HashCode.Combine(HexCoordinates, Position);
        public static BuildingKey Default => new(HexCoordinates.Default, HexPosition.None);
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
