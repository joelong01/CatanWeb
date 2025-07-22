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