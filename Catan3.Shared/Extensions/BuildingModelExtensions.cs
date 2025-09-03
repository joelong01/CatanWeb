using System.Diagnostics;
using Catan3.Shared.Models;
using Catan3.Shared.Utility;


namespace Catan3.Shared.Extensions
{
    public static class BuildingModelExtensions
    {
       
        public static string GetAutomationId(this BuildingKey key)
        {
            if (key is null) throw new ArgumentNullException(nameof(key));
            return $"Building-{key}";
        }
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
        /// Returns the list of all buildings within one position of the BuildingKey
        /// </summary>
        /// <param name="collection">Collection of buildings</param>
        /// <param name="key">The building key to find adjacent buildings for</param>
        /// <returns>List of adjacent buildings</returns>
        public static List<BuildingModel> AdjacentBuildings(this IList<BuildingModel> collection, BuildingKey key)
        {
            var result = new List<BuildingModel>();
            var keys = new List<BuildingKey>();
            BuildingModel? building;
            switch (key.Position)
            {
                case HexPosition.Right:
                    keys.Add(new BuildingKey { HexCoordinates = key.HexCoordinates, Position = HexPosition.TopRight });
                    keys.Add(new BuildingKey { HexCoordinates = key.HexCoordinates, Position = HexPosition.BottomRight });
                    keys.Add(new BuildingKey { HexCoordinates = key.HexCoordinates.NorthEast, Position = HexPosition.BottomRight });
                    break;
                case HexPosition.BottomRight:
                    keys.Add(new BuildingKey { HexCoordinates = key.HexCoordinates, Position = HexPosition.Right });
                    keys.Add(new BuildingKey { HexCoordinates = key.HexCoordinates, Position = HexPosition.BottomLeft });
                    keys.Add(new BuildingKey { HexCoordinates = key.HexCoordinates.South, Position = HexPosition.Right });
                    break;
                case HexPosition.BottomLeft:
                    keys.Add(new BuildingKey { HexCoordinates = key.HexCoordinates, Position = HexPosition.Left });
                    keys.Add(new BuildingKey { HexCoordinates = key.HexCoordinates, Position = HexPosition.BottomRight });
                    keys.Add(new BuildingKey { HexCoordinates = key.HexCoordinates.South, Position = HexPosition.Left });
                    break;
                case HexPosition.Left:
                    keys.Add(new BuildingKey { HexCoordinates = key.HexCoordinates, Position = HexPosition.TopLeft });
                    keys.Add(new BuildingKey { HexCoordinates = key.HexCoordinates, Position = HexPosition.BottomLeft });
                    keys.Add(new BuildingKey { HexCoordinates = key.HexCoordinates.NorthWest, Position = HexPosition.BottomLeft });
                    break;
                case HexPosition.TopLeft:
                    keys.Add(new BuildingKey { HexCoordinates = key.HexCoordinates, Position = HexPosition.TopRight });
                    keys.Add(new BuildingKey { HexCoordinates = key.HexCoordinates, Position = HexPosition.Left });
                    keys.Add(new BuildingKey { HexCoordinates = key.HexCoordinates.NorthWest, Position = HexPosition.TopRight });
                    break;
                case HexPosition.TopRight:
                    keys.Add(new BuildingKey { HexCoordinates = key.HexCoordinates, Position = HexPosition.TopLeft });
                    keys.Add(new BuildingKey { HexCoordinates = key.HexCoordinates, Position = HexPosition.Right });
                    keys.Add(new BuildingKey { HexCoordinates = key.HexCoordinates.North, Position = HexPosition.Right });
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
                    var aliasKey = new BuildingKey { HexCoordinates = aliasCoords, Position = position };
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
            return buildings.FindBuildingModel(key) ?? throw new Exception($"Building {key} not found");
        }

        /// <summary>
        /// Returns all buildings in the tile at the given coordinates
        /// </summary>
        /// <param name="collection">Collection of buildings</param>
        /// <param name="coordinates">The hex coordinates</param>
        /// <returns>List of buildings in the tile</returns>
        public static List<BuildingModel> BuildingsInTile(this IList<BuildingModel> collection, HexCoordinates coordinates)
        {
            List<BuildingModel> result = [];
            foreach (HexPosition pos in Enum.GetValues(typeof(HexPosition)))
            {
                if (pos == HexPosition.None) continue;
                var building = collection.FindBuildingModel(new BuildingKey { HexCoordinates = coordinates, Position = pos });
                if (building is not null)
                {
                    result.Add(building);
                }
            }
            return result;
        }

        /// <summary>
        /// Returns the list of buildings that are owned in the tile
        /// </summary>
        /// <param name="collection">Collection of buildings</param>
        /// <param name="coordinates">The hex coordinates</param>
        /// <returns>List of owned buildings in the tile</returns>
        public static List<BuildingModel> OwnedBuildings(this IList<BuildingModel> collection, HexCoordinates coordinates)
        {
            List<BuildingModel> result = [];
            foreach (HexPosition pos in Enum.GetValues(typeof(HexPosition)))
            {
                if (pos == HexPosition.None) continue;
                var building = collection.FindBuildingModel(new BuildingKey { HexCoordinates = coordinates, Position = pos });
                if (building is not null && building.OwnerId is not null)
                {
                    result.Add(building);
                }
            }
            return result;
        }

        /// <summary>
        /// Calculates the resources generated by a building for a given resource type
        /// </summary>
        /// <param name="model">The building model</param>
        /// <param name="resource">The resource type</param>
        /// <returns>A ResourcesModel containing the generated resources</returns>
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
}