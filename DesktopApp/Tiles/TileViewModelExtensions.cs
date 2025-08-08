using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using Catan3.Shared.Utility;

namespace Catan3.Models
{
    /// <summary>
    /// Extension methods for TileViewModel collections
    /// </summary>
    public static class TileViewModelExtensions
    {
        public static void AddRange<T>(this ObservableCollection<T> collection, IEnumerable<T> items)
        {
            ArgumentNullException.ThrowIfNull(collection);
            ArgumentNullException.ThrowIfNull(items);
            foreach (var item in items)
            {
                collection.Add(item);
            }
        }

        public static TileViewModel? TileFromCoords(this IEnumerable<TileViewModel> collection, HexCoordinates coords)
        {
            ArgumentNullException.ThrowIfNull(collection, nameof(collection));
            ArgumentNullException.ThrowIfNull(coords, nameof(coords));
            return collection.FirstOrDefault(item => item.Tile.TileKey == coords);
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
            var maxQ = collection.Max(tile => tile.Tile.TileKey.Q);
            return collection.Where(tile => tile.Tile.TileKey.Q == maxQ).ToList();
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
                .Where(tile => tile.Tile.TileKey.Q == 0)
                .OrderByDescending(tile => tile.Tile.TileKey.R)
                .First();
        }
    }
}
