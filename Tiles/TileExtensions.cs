using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using Catan3.Utility;
namespace Catan3.Models
{
    public static class TileExtensions
    {
        public static TileViewModel? TileFromCoords(this IEnumerable<TileViewModel> collection, HexCoordinates coords)
        {
            ArgumentNullException.ThrowIfNull(collection, nameof(collection));
            ArgumentNullException.ThrowIfNull(coords, nameof(coords));
            return collection.FirstOrDefault(item => item.Tile.TileKey == coords);
        }
        public static void AddRange<T>(this ObservableCollection<T> collection, IEnumerable<T> items)
        {
            ArgumentNullException.ThrowIfNull(collection);
            ArgumentNullException.ThrowIfNull(items);
            foreach (var item in items)
            {
                collection.Add(item);
            }
        }
        public static TileModel? TileFromCoords(this IEnumerable<TileModel> collection, HexCoordinates coords)
        {
            ArgumentNullException.ThrowIfNull(collection, nameof(collection));
            ArgumentNullException.ThrowIfNull(coords, nameof(coords));
            return collection.FirstOrDefault(item => item.TileKey == coords);
        }
        /// <summary>
        ///     return the list of all Tiles adjacent to tile in collection
        /// </summary>
        /// <param name="collection"></param>
        /// <param name="tile"></param>
        /// <returns></returns>
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
        public static int Pips(this IEnumerable<TileModel> collection)
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
        ///     returns all tiles with the specified number
        /// </summary>
        /// <param name="collection"></param>
        /// <param name="number"></param>
        /// <returns></returns>
        public static List<TileModel> TilesWithNumber(this IEnumerable<TileModel> collection, int number)
        {
            return collection.Where(t => t.Number == number).ToList();
        }
        /// <summary>
        ///     returns all tiles with the specified number
        /// </summary>
        /// <param name="collection"></param>
        /// <param name="number"></param>
        /// <returns></returns>
        public static List<TileModel> TilesWithResource(this IEnumerable<TileModel> collection, ResourceType resource)
        {
            return collection.Where(t => t.ResourceType == resource).ToList();
        }
        /// <summary>
        ///     returns all tiles with the specified number
        /// </summary>
        /// <param name="collection"></param>
        /// <param name="number"></param>
        /// <returns></returns>
        public static List<TileModel> TilesWithSixOrEight(this IEnumerable<TileModel> collection)
        {
            return collection.Where(t => t.Number == 6 || t.Number == 8).ToList();
        }

        public static List<TileViewModel> FirstColumn(this IEnumerable<TileViewModel> collection)
        {
            if (collection == null || !collection.Any())
            {
                throw new ArgumentException("List cannot be empty");
            }

            var minQ = collection.Min(tile => tile.Tile.TileKey.Q);
            return collection.Where(tile => tile.Tile.TileKey.Q == minQ).ToList();
        }

        public static List<TileViewModel> LastColumn(this IEnumerable<TileViewModel> collection)
        {
            if (collection == null || !collection.Any())
            {
                throw new ArgumentException("List cannot be empty");
            }

            var minQ = collection.Max(tile => tile.Tile.TileKey.Q);
            return collection.Where(tile => tile.Tile.TileKey.Q == minQ).ToList();
        }

        public static TileViewModel TopTile(this IEnumerable<TileViewModel> collection)
        {
            return collection
             .Where(tile => tile.Tile.TileKey.Q == 0)
             .OrderBy(tile => tile.Tile.TileKey.R)
             .First();
        }

        public static TileViewModel BottomTile(this IEnumerable<TileViewModel> collection)
        {

            return collection
                .Where(tile => tile.Tile.TileKey.Q == 0) // Include this line if you're still filtering by Q == 0
                .OrderByDescending(tile => tile.Tile.TileKey.R)
                .First();

        }
    }
}
