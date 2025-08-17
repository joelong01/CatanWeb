using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Catan3.Shared.Models;
using Catan3.Shared.Utility;

namespace Catan3.Shared.Extensions
{
    public static class TileModelExtensions
    {
        public static string GetTileAutomationId(this TileModel tileModel)
        {
            ArgumentNullException.ThrowIfNull(tileModel, nameof(tileModel));
            return $"Tile-{tileModel.TileKey}";
        }
        public static string GetTileNumberAutomationId(this TileModel tileModel)
        {
            ArgumentNullException.ThrowIfNull(tileModel, nameof(tileModel));
            if (tileModel.TileKey is null)
                throw new ArgumentNullException(nameof(tileModel.TileKey), "TileKey cannot be null.");
            return $"Number-{tileModel.TileKey}";
        }
        public static string GetTileResourceAutomationId(this TileModel tileModel)
        {
            ArgumentNullException.ThrowIfNull(tileModel, nameof(tileModel));
            if (tileModel.TileKey is null)
                throw new ArgumentNullException(nameof(tileModel.TileKey), "TileKey cannot be null.");
            return $"Resource-{tileModel.TileKey}";
        }
        public static TileModel? TileFromCoords(this IEnumerable<TileModel> collection, HexCoordinates coords)
        {
            ArgumentNullException.ThrowIfNull(collection, nameof(collection));
            ArgumentNullException.ThrowIfNull(coords, nameof(coords));
            return collection.FirstOrDefault(item => item.TileKey == coords);
        }

        public static List<TileModel> AdjacentTiles(this IEnumerable<TileModel> collection, TileModel tile)
        {
            var result = new List<TileModel>();
            foreach (var (_, coord) in HexCoordinates.Directions)
            {
                var t = collection.TileFromCoords(tile.TileKey + coord);
                if (t is not null) result.Add(t);
            }
            return result;
        }

        /// <summary>
        /// Add up all the Stars for the buildings in the collection
        /// </summary>
        /// <param name="collection">Collection of tiles</param>
        /// <returns>Total star count</returns>
        public static int Stars(this IEnumerable<TileModel> collection)
        {
            ArgumentNullException.ThrowIfNull(collection);
            return collection.Sum(tile => tile.Number switch
            {
                2 or 12 => 1,
                3 or 11 => 2,
                4 or 10 => 3,
                5 or 9 => 4,
                6 or 8 => 5,
                7 => 0,
                _ => throw new ArgumentOutOfRangeException(nameof(tile), $"Invalid tile number: {tile.Number}.")
            });
        }

        /// <summary>
        /// Returns all tiles with the specified number
        /// </summary>
        /// <param name="collection">Collection of tiles</param>
        /// <param name="number">The number to search for</param>
        /// <returns>List of tiles with the specified number</returns>
        public static List<TileModel> TilesWithNumber(this IEnumerable<TileModel> collection, int number)
        {
            return collection.Where(t => t.Number == number).ToList();
        }

        /// <summary>
        /// Returns all tiles with the specified resource type
        /// </summary>
        /// <param name="collection">Collection of tiles</param>
        /// <param name="resource">The resource type to search for</param>
        /// <returns>List of tiles with the specified resource type</returns>
        public static List<TileModel> TilesWithResource(this IEnumerable<TileModel> collection, ResourceType resource)
        {
            return collection.Where(t => t.ResourceTileType == resource).ToList();
        }

        /// <summary>
        /// Returns all tiles with the number 6 or 8
        /// </summary>
        /// <param name="collection">Collection of tiles</param>
        /// <returns>List of tiles with number 6 or 8</returns>
        public static List<TileModel> TilesWithSixOrEight(this IEnumerable<TileModel> collection)
        {
            return collection.Where(t => t.Number == 6 || t.Number == 8).ToList();
        }

        public static ResourceType ToResourceCardType(this ResourceType tileType)
        {
            switch (tileType)
            {
                case ResourceType.Sheep:
                    return ResourceType.Sheep;
                case ResourceType.Wood:
                    return ResourceType.Wood;
                case ResourceType.Ore:
                    return ResourceType.Ore;
                case ResourceType.Wheat:
                    return ResourceType.Wheat;
                case ResourceType.Brick:
                    return ResourceType.Brick;
                case ResourceType.GoldMine:
                    return ResourceType.GoldMine;
                case ResourceType.Desert:
                    return ResourceType.Desert;
                case ResourceType.Back:
                    return ResourceType.Back;
                case ResourceType.None:
                    return ResourceType.None;
                case ResourceType.Sea:
                    return ResourceType.Sea;
                case ResourceType.Robber:
                    return ResourceType.Robber;
            }
            throw new ArgumentException("Did you forget to update this?", nameof(tileType));
        }
        /// <summary>
        ///     Add up all the Stars for the tiles in the list
        /// </summary>
        /// <param name="list"></param>
        /// <returns></returns>

        public static int Stars(this List<TileModel> list)
        {
            ArgumentNullException.ThrowIfNull(list);
            return list.Sum(tile => tile.Stars);
        }
    }
}