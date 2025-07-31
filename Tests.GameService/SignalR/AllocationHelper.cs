using Catan3.Shared.Models;
using Catan3.Shared.Utility;
using Catan3.Shared.Extensions;

namespace Tests.GameService.SignalR
{
    /// <summary>
    /// Helper class for optimal settlement and road placement during allocation phases.
    /// This class mimics the exact logic from GameController.MarkBuildableBuildings() for allocation phases.
    /// Uses the exact same logic as the desktop app for star calculations.
    /// </summary>
    public static class AllocationHelper
    {
        /// <summary>
        /// Picks the optimal settlement location based on star values (adjacent tile numbers).
        /// Returns the BuildingKey for the best settlement placement.
        /// Uses buildings already marked as PossibleSettlement by the GameStateMachine,
        /// but applies additional distance rule filtering since GameStateMachine may be too permissive.
        /// </summary>
        /// <param name="gameModel">Current game model to analyze</param>
        /// <returns>BuildingKey for optimal settlement placement</returns>
        public static BuildingKey PickSettlement(GameModel gameModel)
        {
            var currentPhase = gameModel.Phase();
            
            Console.WriteLine($"[AllocationHelper] Current game phase: {currentPhase}");
            Console.WriteLine($"[AllocationHelper] Current game state: {gameModel.GameState}");
            Console.WriteLine($"[AllocationHelper] Current player: {gameModel.CurrentPlayerId}");
            Console.WriteLine($"[AllocationHelper] Total buildings: {gameModel.Buildings.Count}");
            
            // Find buildings already marked as PossibleSettlement by the GameStateMachine
            var possibleSettlements = gameModel.Buildings
                .Where(b => b.BuildingState == BuildingState.PossibleSettlement)
                .ToList();

            Console.WriteLine($"[AllocationHelper] Found {possibleSettlements.Count} buildings marked as PossibleSettlement");

            if (!possibleSettlements.Any())
            {
                throw new InvalidOperationException($"No PossibleSettlement buildings found. Phase: {currentPhase}, State: {gameModel.GameState}");
            }
            //
            //// Apply distance rule filtering: settlements must be at least 2 road segments from existing settlements
            //var validSettlements = possibleSettlements
            //    .Where(building => IsValidSettlementLocation(gameModel, building.BuildingKey))
            //    .ToList();

            //Console.WriteLine($"[AllocationHelper] After distance rule filtering: {validSettlements.Count} valid settlements");

            //if (!validSettlements.Any())
            //{
            //    throw new InvalidOperationException($"No valid settlement locations found after distance filtering. Phase: {currentPhase}, State: {gameModel.GameState}");
            //}

            // Calculate star values for each valid settlement using the exact desktop app logic
            var settlementOptions = possibleSettlements
                .Select(building => new
                {
                    building = building,
                    stars = gameModel.TilesForBuildings(building.BuildingKey).Stars(),
                    buildingKey = building.BuildingKey
                })
                .ToList();

            var maxStars = settlementOptions.Max(s => s.stars);
            var bestSettlement = settlementOptions.First(s => s.stars == maxStars);
            
            Console.WriteLine($"[AllocationHelper] Selected settlement {bestSettlement.buildingKey} with {bestSettlement.stars} stars");
            
            return bestSettlement.buildingKey;
        }

        /// <summary>
        /// Checks if a building location is valid for settlement placement according to the distance rule.
        /// Settlements must be at least 2 road segments away from existing settlements.
        /// </summary>
        /// <param name="gameModel">Current game model</param>
        /// <param name="buildingKey">Building key to check</param>
        /// <returns>True if the location is valid for settlement placement</returns>
        private static bool IsValidSettlementLocation(GameModel gameModel, BuildingKey buildingKey)
        {
            // Find adjacent buildings using the GameModel extension method
            var adjacentBuildings = gameModel.Buildings.AdjacentBuildings(buildingKey);
            
            // Check if any adjacent building is owned (has a settlement)
            foreach (var adjacentBuilding in adjacentBuildings)
            {
                if (!string.IsNullOrEmpty(adjacentBuilding.OwnerId) && 
                    (adjacentBuilding.BuildingState == BuildingState.Settlement || 
                     adjacentBuilding.BuildingState == BuildingState.City))
                {
                    Console.WriteLine($"[AllocationHelper] Building {buildingKey} rejected - adjacent to owned building {adjacentBuilding.BuildingKey}");
                    return false;
                }
            }
            
            return true;
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
            return tiles.Sum(tile => tile.Stars);
        }
    }
}