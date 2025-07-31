using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Catan3.Shared.Models;
using Catan3.Shared.Utility;

namespace Catan3.Shared.Extensions
{
    public static class GameModelExtensions
    {
        public static bool AllocationPhase(this GameModel gameModel)
        {
            return (gameModel.GameState == GameState.AllocateResourceForward || gameModel.GameState == GameState.AllocateResourceReverse ||
                    gameModel.GameState == GameState.WaitingForRollForOrder || gameModel.GameState == GameState.FinishedRollOrder ||
                    gameModel.GameState == GameState.BeginResourceAllocation || gameModel.GameState == GameState.PickingBoard);
        }

        public static GamePhase Phase(this GameModel gameModel)
        {
            switch (gameModel.GameState)
            {
                case GameState.Uninitialized:
                case GameState.WaitingForNewGame:
                case GameState.BeginResourceAllocation:
                case GameState.WaitingForPlayers:
                    return GamePhase.Starting;
                case GameState.PickingBoard:
                case GameState.WaitingForRollForOrder:
                case GameState.FinishedRollOrder:
                    return GamePhase.PickingBoard;
                case GameState.AllocateResourceForward:
                case GameState.AllocateResourceReverse:
                case GameState.DoneResourceAllocation:
                    return GamePhase.PickingResources;
                case GameState.WaitingForRoll:
                    return GamePhase.Rolling;
                case GameState.WaitingForNext:
                case GameState.Supplemental:
                    return GamePhase.Purchase;
                case GameState.MustMoveRobber:
                case GameState.TooManyCards:
                case GameState.MustDestroyCity:
                    return GamePhase.ActionRequired;
                default:
                    return GamePhase.Unspecified;
            }
        }

        public static EntitlementPurchaseModel PurchaseModel(this GameModel gameModel, Entitlement entitlement)
        {
            var model = gameModel.EntitlementPurchaseModel.First(m => m.Entitlement == entitlement) ?? throw new System.Exception($"{entitlement} not found in Purchasable Entitlements!");
            return model;
        }

        /// <summary>
        /// Computes a prime-based hash representing the current state of the game tiles.
        /// Uses unique prime multipliers for each tile position to ensure mathematical uniqueness.
        /// This method is fast and deterministic - identical game states will always produce the same hash.
        /// Used for fast verification that all clients have identical game states in multi-player testing.
        /// </summary>
        public static string ComputeGameHash(this GameModel gameModel)
        {
            // Prime numbers for unique hash computation
            // Need 2 primes per tile: one for resource type, one for number
            // Regular board: 19 tiles = 38 primes, Expansion: 30 tiles = 60 primes
            var primes = First100Primes;
            
            long hash = 0;
            
            // Include GameState and CurrentPlayerId for state verification
            hash += (int)gameModel.GameState * 541; // Use last prime for game state
            hash += (gameModel.CurrentPlayerId?.GetHashCode() ?? 0) * 523; // Second to last for player
            
            // Process tiles with unique prime multipliers
            for (int tileIndex = 0; tileIndex < gameModel.Tiles.Count; tileIndex++)
            {
                var tile = gameModel.Tiles[tileIndex];
                
                // Each tile gets 2 unique primes: one for resource, one for number
                var resourcePrime = primes[tileIndex * 2 + 1]; // Start at index 1
                var numberPrime = primes[tileIndex * 2 + 2];   // Start at index 2
                
                hash += resourcePrime * (int)tile.ResourceTileType;
                hash += numberPrime * tile.Number;
            }
            
            // Include Harbors (sorted by coordinates for consistency) 
            if (gameModel.Harbors?.Any() == true)
            {
                var sortedHarbors = gameModel.Harbors.OrderBy(h => h.HarborKey.HexCoordinates.Q)
                    .ThenBy(h => h.HarborKey.HexCoordinates.R).ThenBy(h => h.HarborKey.Side);
                
                int harborIndex = 0;
                foreach (var harbor in sortedHarbors)
                {
                    // Use remaining primes for harbors - need 2 primes per harbor
                    var harborIndexPrime = primes[60 + harborIndex * 2]; // Harbor index prime
                    var harborTypePrime = primes[60 + harborIndex * 2 + 1]; // Harbor type prime
                    
                    hash += harborIndexPrime * harborIndex; // Harbor position in sorted order
                    hash += harborTypePrime * (int)harbor.HarborKey.HarborType; // Harbor type value
                    harborIndex++;
                }
            }
            
            // Include Owned Roads (sorted by coordinates for consistency)
            if (gameModel.Roads?.Any() == true)
            {
                var ownedRoads = gameModel.Roads
                    .Where(r => !string.IsNullOrEmpty(r.OwnerId))
                    .OrderBy(r => r.RoadKey.TileKey.Q)
                    .ThenBy(r => r.RoadKey.TileKey.R)
                    .ThenBy(r => r.RoadKey.HexSide);
                
                int roadIndex = 0;
                foreach (var road in ownedRoads)
                {
                    // Use primes starting after harbors - need 2 primes per owned road
                    var roadIndexPrime = primes[80 + roadIndex * 2]; // Road index prime
                    var ownerPrime = primes[80 + roadIndex * 2 + 1]; // Road owner prime
                    
                    hash += roadIndexPrime * roadIndex; // Road position in sorted order
                    hash += ownerPrime * (road.OwnerId?.GetHashCode() ?? 0); // Owner hash
                    roadIndex++;
                }
            }
            
            // Include Robber position if set - fixed nullable reference warning
            if (gameModel.Robber?.Coordinates is not null && 
                !gameModel.Robber.Coordinates.Equals(HexCoordinates.Default))
            {
                hash += 499 * gameModel.Robber.Coordinates.Q; // Use specific primes for robber
                hash += 503 * gameModel.Robber.Coordinates.R;
                hash += 509 * gameModel.Robber.Coordinates.S;
            }
            
            // Convert to hex string for readability
            return hash.ToString("X");
        }

        /// <summary>
        /// First 100 prime numbers for unique hash computation
        /// </summary>
        private static readonly int[] First100Primes = new int[]
        {
            2, 3, 5, 7, 11, 13, 17, 19, 23, 29,
            31, 37, 41, 43, 47, 53, 59, 61, 67, 71,
            73, 79, 83, 89, 97, 101, 103, 107, 109, 113,
            127, 131, 137, 139, 149, 151, 157, 163, 167, 173,
            179, 181, 191, 193, 197, 199, 211, 223, 227, 229,
            233, 239, 241, 251, 257, 263, 269, 271, 277, 281,
            283, 293, 307, 311, 313, 317, 331, 337, 347, 349,
            353, 359, 367, 373, 379, 383, 389, 397, 401, 409,
            419, 421, 431, 433, 439, 443, 449, 457, 461, 463,
            467, 479, 487, 491, 499, 503, 509, 521, 523, 541
        };

        /// <summary>
        /// Updates the GameHash by recomputing it from the current game state.
        /// This should be called by the GameStateMachine whenever the game state changes.
        /// </summary>
        public static void UpdateGameHash(this GameModel gameModel)
        {
            // Restore proper hash computation
            gameModel.GameHash = gameModel.ComputeGameHash();
        }

        /// <summary>
        /// Given a building, what roads are next to it?
        /// </summary>
        public static List<RoadModel> AdjacentRoads(this GameModel gameModel, BuildingKey buildingKey)
        {
            List<RoadModel> result = [];
            List<RoadKey> roadKeys = [];
            switch (buildingKey.Position)
            {
                case HexPosition.Right:
                    roadKeys.Add(new RoadKey { TileKey = buildingKey.HexCoordinates, HexSide = HexSide.TopRight });
                    roadKeys.Add(new RoadKey { TileKey = buildingKey.HexCoordinates, HexSide = HexSide.BottomRight });
                    roadKeys.Add(new RoadKey { TileKey = buildingKey.HexCoordinates.NorthEast, HexSide = HexSide.Bottom });
                    break;
                case HexPosition.BottomRight:
                    roadKeys.Add(new RoadKey { TileKey = buildingKey.HexCoordinates, HexSide = HexSide.Bottom });
                    roadKeys.Add(new RoadKey { TileKey = buildingKey.HexCoordinates, HexSide = HexSide.BottomRight });
                    roadKeys.Add(new RoadKey { TileKey = buildingKey.HexCoordinates.South, HexSide = HexSide.TopRight });
                    break;
                case HexPosition.BottomLeft:
                    roadKeys.Add(new RoadKey { TileKey = buildingKey.HexCoordinates, HexSide = HexSide.Bottom });
                    roadKeys.Add(new RoadKey { TileKey = buildingKey.HexCoordinates, HexSide = HexSide.BottomLeft });
                    roadKeys.Add(new RoadKey { TileKey = buildingKey.HexCoordinates.South, HexSide = HexSide.TopLeft });
                    break;
                case HexPosition.Left:
                    roadKeys.Add(new RoadKey { TileKey = buildingKey.HexCoordinates, HexSide = HexSide.TopLeft });
                    roadKeys.Add(new RoadKey { TileKey = buildingKey.HexCoordinates, HexSide = HexSide.BottomLeft });
                    roadKeys.Add(new RoadKey { TileKey = buildingKey.HexCoordinates.NorthWest, HexSide = HexSide.Bottom });
                    break;
                case HexPosition.TopLeft:
                    roadKeys.Add(new RoadKey { TileKey = buildingKey.HexCoordinates, HexSide = HexSide.TopLeft });
                    roadKeys.Add(new RoadKey { TileKey = buildingKey.HexCoordinates, HexSide = HexSide.Top });
                    roadKeys.Add(new RoadKey { TileKey = buildingKey.HexCoordinates.NorthWest, HexSide = HexSide.TopRight });
                    break;
                case HexPosition.TopRight:
                    roadKeys.Add(new RoadKey { TileKey = buildingKey.HexCoordinates, HexSide = HexSide.TopRight });
                    roadKeys.Add(new RoadKey { TileKey = buildingKey.HexCoordinates, HexSide = HexSide.Top });
                    roadKeys.Add(new RoadKey { TileKey = buildingKey.HexCoordinates.North, HexSide = HexSide.BottomRight });
                    break;
                case HexPosition.None:
                    break;
            }
            foreach (var key in roadKeys)
            {
                var road = gameModel.Roads.FindRoad(key);
                if (road is not null)
                {
                    result.Add(road);
                }
            }
            return result;
        }

        /// <summary>
        /// Given a road, what buildings are next to it?
        /// </summary>
        public static List<BuildingModel> AdjacentBuildings(this GameModel gameModel, RoadKey roadKey)
        {
            var result = new List<BuildingModel>();
            switch (roadKey.HexSide)
            {
                case HexSide.None:
                    throw new System.Exception($"Invalid Side for road key {roadKey}");
                case HexSide.Top:
                    result.Add(gameModel.Buildings.GetBuildingOrThrow(new BuildingKey { HexCoordinates = roadKey.TileKey, Position = HexPosition.TopLeft }));
                    result.Add(gameModel.Buildings.GetBuildingOrThrow(new BuildingKey { HexCoordinates = roadKey.TileKey, Position = HexPosition.TopRight }));
                    break;
                case HexSide.TopRight:
                    result.Add(gameModel.Buildings.GetBuildingOrThrow(new BuildingKey { HexCoordinates = roadKey.TileKey, Position = HexPosition.TopRight }));
                    result.Add(gameModel.Buildings.GetBuildingOrThrow(new BuildingKey { HexCoordinates = roadKey.TileKey, Position = HexPosition.Right }));
                    break;
                case HexSide.BottomRight:
                    result.Add(gameModel.Buildings.GetBuildingOrThrow(new BuildingKey { HexCoordinates = roadKey.TileKey, Position = HexPosition.Right }));
                    result.Add(gameModel.Buildings.GetBuildingOrThrow(new BuildingKey { HexCoordinates = roadKey.TileKey, Position = HexPosition.BottomRight }));
                    break;
                case HexSide.Bottom:
                    result.Add(gameModel.Buildings.GetBuildingOrThrow(new BuildingKey { HexCoordinates = roadKey.TileKey, Position = HexPosition.BottomRight }));
                    result.Add(gameModel.Buildings.GetBuildingOrThrow(new BuildingKey { HexCoordinates = roadKey.TileKey, Position = HexPosition.BottomLeft }));
                    break;
                case HexSide.BottomLeft:
                    result.Add(gameModel.Buildings.GetBuildingOrThrow(new BuildingKey { HexCoordinates = roadKey.TileKey, Position = HexPosition.BottomLeft }));
                    result.Add(gameModel.Buildings.GetBuildingOrThrow(new BuildingKey { HexCoordinates = roadKey.TileKey, Position = HexPosition.Left }));
                    break;
                case HexSide.TopLeft:
                    result.Add(gameModel.Buildings.GetBuildingOrThrow(new BuildingKey { HexCoordinates = roadKey.TileKey, Position = HexPosition.Left }));
                    result.Add(gameModel.Buildings.GetBuildingOrThrow(new BuildingKey { HexCoordinates = roadKey.TileKey, Position = HexPosition.TopLeft }));
                    break;
            }
            return result;
        }

        /// <summary>
        /// Return the building between the 2 road keys. Works by getting the building adjacent to each of the roads
        /// and then finding what the intersection is. It must be only 0 or 1 roads.
        /// </summary>
        public static BuildingModel? BuildingBetweenRoads(this GameModel gameModel, RoadKey road1, RoadKey road2)
        {
            var buildings1 = gameModel.AdjacentBuildings(road1);
            var buildings2 = gameModel.AdjacentBuildings(road2);
            var result = buildings1.Intersect(buildings2).ToList();
            Debug.Assert(result.Count <= 1);
            if (result.Count == 0) return null;
            return result is null ? null : result[0];
        }

        /// <summary>
        /// Calculates the player ID that is a specified number of positions away from a given start player ID.
        /// </summary>
        /// <param name="gameModel">The game model containing the players.</param>
        /// <param name="startPlayerId">The ID of the player from which to start counting.</param>
        /// <param name="numberOfPositions">The number of positions to move forward in the player list; can be negative.</param>
        /// <returns>The player ID of the player numberOfPositions away from the start player.</returns>
        /// <exception cref="System.Exception">Thrown if the start player ID is invalid or not in the game.</exception>
        public static string NextPlayerId(this GameModel gameModel, string startPlayerId, int numberOfPositions)
        {
            // Validate and find the starting player
            var startPlayer = gameModel.Players.PlayerFromId(startPlayerId) ??
                throw new System.Exception($"Invalid id: {startPlayerId}");
            int idx = gameModel.Players.IndexOf(startPlayer);
            if (idx == -1)
                throw new System.Exception("The player must be in the game!");
            int count = gameModel.Players.Count;
            // Calculate the index of the next player, wrapping around if necessary
            int newPlayerIndex = (idx + numberOfPositions) % count;
            if (newPlayerIndex < 0)
                newPlayerIndex += count;
            // Retrieve the new player's ID
            var newPlayer = gameModel.Players[newPlayerIndex];
            return newPlayer.Id;
        }

        /// <summary>
        /// Changes the current player to the player a specified number of positions forward.
        /// </summary>
        /// <param name="gameModel">The game model where the current player will be changed.</param>
        /// <param name="numberOfPositions">The number of positions to move forward in the player list.</param>
        /// <exception cref="System.Exception">Thrown if the player ID is invalid.</exception>
        public static void ChangePlayer(this GameModel gameModel, int numberOfPositions)
        {
            // Ensure the current player ID is valid
            if (string.IsNullOrEmpty(gameModel.CurrentPlayerId))
                throw new System.Exception("Current player ID must not be null or empty.");
            // Get the next player ID and change to it
            var id = NextPlayerId(gameModel, gameModel.CurrentPlayerId, numberOfPositions);
            gameModel.ChangePlayerTo(id);
        }

        /// <summary>
        /// Sets the current player to the specified player ID.
        /// </summary>
        /// <param name="gameModel">The game model where the current player will be set.</param>
        /// <param name="playerId">The player ID to set as current.</param>
        /// <exception cref="System.Exception">Thrown if the player ID is invalid.</exception>
        public static void ChangePlayerTo(this GameModel gameModel, string playerId)
        {
            // Validate and find the new player
            var newPlayer = gameModel.Players.PlayerFromId(playerId) ??
                throw new System.Exception($"Invalid id: {playerId}");
            // Set the current player ID
            gameModel.CurrentPlayerId = newPlayer.Id;
        }

        /// <summary>
        /// Given a BuildingKey, return the list of tiles that the Building connects to
        /// </summary>
        /// <param name="gameModel">The game model</param>
        /// <param name="key">The building key</param>
        /// <returns>List of connected tiles</returns>
        public static List<TileModel> TilesForBuildings(this GameModel gameModel, BuildingKey key)
        {
            List<TileModel> tiles = [];
            // get the tile
            var tileModel = gameModel.Tiles.TileFromCoords(key.HexCoordinates);
            Debug.Assert(tileModel is not null, "Bad HexCoordinates");
            tiles.Add(tileModel);
            // get the aliases
            var aliases = key.Aliases();
            foreach ((_, Direction direction) in aliases)
            {
                var neighbor = gameModel.Tiles.TileFromCoords(tileModel.TileKey.GetAdjacentTile(direction));
                if (neighbor is not null)
                {
                    tiles.Add(neighbor);
                }
            }
            return tiles;
        }

        public static PlayerModel CurrentPlayer(this GameModel gameModel)
        {
            return gameModel.Players.PlayerFromId(gameModel.CurrentPlayerId) ?? throw new System.Exception($"Can't find player {gameModel.CurrentPlayerId}");
        }

        /// <summary>
        /// Return the harbor that is adjacent to the given building key.
        /// </summary>
        /// <param name="gameModel">The game model</param>
        /// <param name="buildingKey">The building key</param>
        /// <returns>The adjacent Harbor or null if it has none</returns>
        public static HarborModel? FindAdjacentHarbor(this GameModel gameModel, BuildingKey buildingKey)
        {
            foreach (var (hex, side) in HarborModel.GetAdjacentHarborLocations(buildingKey))
            {
                var harbor = gameModel.Harbors.FirstOrDefault(h => h.HarborKey.HexCoordinates.Equals(hex) && h.HarborKey.Side == side);
                if (harbor is not null)
                    return harbor;
            }
            return null;
        }
    }
}