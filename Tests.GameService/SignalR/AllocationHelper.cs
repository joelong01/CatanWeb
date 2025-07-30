using Catan3.Shared.Models;
using Catan3.Shared.Utility;
using Catan3.Shared.Extensions;

namespace Tests.GameService.SignalR
{
    /// <summary>
    /// Helper class for optimal settlement and road placement during allocation phases.
    /// This class does NOT know about the state machine - it only analyzes GameModel for optimal placement.
    /// Uses the exact same logic as the desktop app for star calculations.
    /// </summary>
    public static class AllocationHelper
    {
        /// <summary>
        /// Picks the optimal settlement location based on star values (adjacent tile numbers).
        /// Returns the BuildingKey for the best settlement placement.
        /// Uses the same logic as the desktop app: gameModel.TilesForBuildings(buildingKey).Stars()
        /// </summary>
        /// <param name="gameModel">Current game model to analyze</param>
        /// <returns>BuildingKey for optimal settlement placement</returns>
        public static BuildingKey PickSettlement(GameModel gameModel)
        {
            // Find all buildable settlement locations
            var buildableSettlements = gameModel.Buildings
                .Where(b => b.BuildingState == BuildingState.PossibleSettlement)
                .ToList();

            if (!buildableSettlements.Any())
            {
                throw new InvalidOperationException("No buildable settlements found");
            }

            // Calculate star values for each settlement using the exact desktop app logic
            var settlementOptions = buildableSettlements
                .Select(building => new
                {
                    building = building,
                    stars = gameModel.TilesForBuildings(building.BuildingKey).Stars(),
                    buildingKey = building.BuildingKey
                })
                .ToList();

            var maxStars = settlementOptions.Max(s => s.stars);
            var bestSettlement = settlementOptions.First(s => s.stars == maxStars);
            
            return bestSettlement.buildingKey;
        }

        /// <summary>
        /// Picks the first buildable road location.
        /// Returns the RoadKey for road placement.
        /// </summary>
        /// <param name="gameModel">Current game model to analyze</param>
        /// <returns>RoadKey for road placement</returns>
        public static RoadKey PickRoad(GameModel gameModel)
        {
            // Find first buildable road
            var buildableRoad = gameModel.Roads.FirstOrDefault(r => r.RoadState == RoadState.Buildable);
            
            if (buildableRoad == null)
            {
                throw new InvalidOperationException("No buildable roads found");
            }

            return buildableRoad.RoadKey;
        }
    }

    /// <summary>
    /// Extension methods for calculating stars - mirrors the desktop app's TileModelExtensions.Stars()
    /// </summary>
    public static class TileModelExtensions
    {
        /// <summary>
        /// Calculate total stars for a collection of tiles using the same logic as the desktop app
        /// </summary>
        public static int Stars(this IEnumerable<TileModel> tiles)
        {
            int totalStars = 0;
            foreach (var tile in tiles)
            {
                totalStars += tile.Number switch
                {
                    2 or 12 => 1,
                    3 or 11 => 2,
                    4 or 10 => 3,
                    5 or 9 => 4,
                    6 or 8 => 5,
                    7 => 0,
                    _ => 0
                };
            }
            return totalStars;
        }
    }
}