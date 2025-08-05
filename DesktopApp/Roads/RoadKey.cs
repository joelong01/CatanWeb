using System;
using System.Text.Json.Serialization;
using Catan3.Utility;
using CommunityToolkit.Mvvm.ComponentModel;
namespace Catan3.Models
{
    /// <summary>
    /// Represents a key for a road, including the tile coordinates and the side of the hexagon.
    /// Implements IComparable for sorting and comparison purposes.
    /// </summary>
    public partial class RoadKey(HexCoordinates tileKey, HexSide side) : ObservableObject, IComparable<RoadKey>
    {
        /// <summary>
        /// Gets or sets the tile coordinates for the road.
        /// </summary>
        [ObservableProperty]
        public partial HexCoordinates TileKey { get; set; } = tileKey;

        /// <summary>
        /// Gets or sets the side of the hexagon for the road.
        /// </summary>
        [ObservableProperty]
        public partial HexSide HexSide { get; set; } = side;

        /// <summary>
        /// Initializes a new instance of the RoadKey class with default values.
        /// </summary>
        [JsonConstructor]
        public RoadKey() : this(HexCoordinates.Default, HexSide.Bottom)
        {
        }

        /// <summary>
        /// Returns a string representation of the RoadKey.
        /// </summary>
        /// <returns>A string in the format "TileKey-HexSide".</returns>
        public override string ToString()
        {
            return String.Format($"{TileKey}-{HexSide}");
        }

        /// <summary>
        /// Determines whether the specified object is equal to the current RoadKey.
        /// </summary>
        /// <param name="obj">The object to compare with the current RoadKey.</param>
        /// <returns>True if the specified object is equal to the current RoadKey; otherwise, false.</returns>
        public override bool Equals(object? obj)
        {
            return obj is not null && obj is RoadKey key &&
                   key.HexSide == this.HexSide &&
                   key.TileKey == this.TileKey;
        }

        /// <summary>
        /// Returns a hash code for the current RoadKey.
        /// </summary>
        /// <returns>A hash code for the current RoadKey.</returns>
        public override int GetHashCode() => HashCode.Combine(TileKey, HexSide);

        /// <summary>
        /// Compares the current RoadKey with another RoadKey.
        /// </summary>
        /// <param name="other">The RoadKey to compare with the current RoadKey.</param>
        /// <returns>A value that indicates the relative order of the RoadKeys being compared.</returns>
        public int CompareTo(RoadKey? other)
        {
            if (other is null) return 1;
            // First, compare by HexCoordinates
            int hexCompare = TileKey.CompareTo(other.TileKey);
            if (hexCompare != 0)
            {
                return hexCompare;
            }
            return HexSide.CompareTo(other.HexSide);
        }

        /// <summary>
        /// Gets the default instance of the BuildingKey class.
        /// </summary>
        public static BuildingKey Default => new(HexCoordinates.Default, Utility.HexPosition.None);

        /// <summary>
        /// Determines whether two RoadKey instances are equal.
        /// </summary>
        /// <param name="left">The first RoadKey instance.</param>
        /// <param name="right">The second RoadKey instance.</param>
        /// <returns>True if the two RoadKey instances are equal; otherwise, false.</returns>
        public static bool operator ==(RoadKey left, RoadKey right)
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

        /// <summary>
        /// Determines whether two RoadKey instances are not equal.
        /// </summary>
        /// <param name="left">The first RoadKey instance.</param>
        /// <param name="right">The second RoadKey instance.</param>
        /// <returns>True if the two RoadKey instances are not equal; otherwise, false.</returns>
        public static bool operator !=(RoadKey left, RoadKey right) => !( left == right );
    }
}
