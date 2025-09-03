using System.Collections.Generic;
using System.Linq;
using Catan3.Shared.Models;
using Catan3.Shared.Extensions;

namespace Catan3.Models
{
    /// <summary>
    /// UI-specific extensions for building ViewModels
    /// </summary>
    public static class BuildingViewModelExtensions
    {
        /// <summary>
        /// Finds a BuildingViewModel by its BuildingKey, including alias checking
        /// </summary>
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
    }
}
