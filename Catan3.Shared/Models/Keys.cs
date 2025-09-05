using System.Text.Json.Serialization;
using Catan3.Shared.Utility;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Catan3.Shared.Models
{
    /// <summary>
    /// Represents a key for a road, including the tile coordinates and the side of the hexagon.
    /// Supports both plain object usage (for JSON/API) and MVVM usage (for UI data binding).
    /// </summary>
    public partial class RoadKey : ObservableObject, IComparable<RoadKey>
    {
        [ObservableProperty]
        public partial HexCoordinates TileKey { get; set; } = HexCoordinates.Default;

        [ObservableProperty]
        public partial HexSide HexSide { get; set; } = HexSide.Bottom;

        /// <summary>
        /// Initializes a new instance of the RoadKey class with the specified tile key and hex side.
        /// </summary>
        /// <param name="tileKey">The hex coordinates of the tile.</param>
        /// <param name="side">The side of the hexagon where the road is located.</param>
        public RoadKey(HexCoordinates tileKey, HexSide side)
        {
            TileKey = tileKey;
            HexSide = side;
        }

        /// <summary>
        /// Initializes a new instance of the RoadKey class with default values.
        /// Used for JSON deserialization.
        /// </summary>
        [JsonConstructor]
        public RoadKey() : this(HexCoordinates.Default, HexSide.Bottom) { }

        public override string ToString() => $"{TileKey}-{HexSide}";

        public override bool Equals(object? obj)
        {
            return obj is not null && obj is RoadKey key &&
                   key.HexSide == this.HexSide &&
                   key.TileKey == this.TileKey;
        }

        public override int GetHashCode() => HashCode.Combine(TileKey, HexSide);

        public int CompareTo(RoadKey? other)
        {
            if (other is null) return 1;
            int hexCompare = TileKey.CompareTo(other.TileKey);
            if (hexCompare != 0) return hexCompare;
            return HexSide.CompareTo(other.HexSide);
        }

        public static bool operator ==(RoadKey? left, RoadKey? right)
        {
            if (left is null || right is null) return false;
            if (ReferenceEquals(left, right)) return true;
            return left.Equals(right);
        }

        public static bool operator !=(RoadKey? left, RoadKey? right) => !(left == right);
    }

    /// <summary>
    /// Represents a key for a building, including the tile coordinates and position.
    /// Supports both plain object usage (for JSON/API) and MVVM usage (for UI data binding).
    /// </summary>
    public partial class BuildingKey : ObservableObject, IComparable<BuildingKey>
    {
        [ObservableProperty]
        public partial HexCoordinates HexCoordinates { get; set; } = HexCoordinates.Default;

        [ObservableProperty]
        public partial HexPosition Position { get; set; } = HexPosition.None;

        /// <summary>
        /// Initializes a new instance of the BuildingKey class with the specified coordinates and position.
        /// </summary>
        /// <param name="hexcoordinates">The hex coordinates of the building.</param>
        /// <param name="position">The position of the building on the hex.</param>
        public BuildingKey(HexCoordinates hexcoordinates, HexPosition position)
        {
            HexCoordinates = hexcoordinates;
            Position = position;
        }

        /// <summary>
        /// Initializes a new instance of the BuildingKey class with default values.
        /// Used for JSON deserialization.
        /// </summary>
        [JsonConstructor]
        public BuildingKey() : this(HexCoordinates.Default, HexPosition.None) { }

        public override string ToString() => $"{HexCoordinates}-{Position}";

        public override bool Equals(object? obj)
        {
            return obj is not null && obj is BuildingKey key &&
                   key.Position == this.Position &&
                   key.HexCoordinates == this.HexCoordinates;
        }

        public override int GetHashCode() => HashCode.Combine(HexCoordinates, Position);

        public int CompareTo(BuildingKey? other)
        {
            if (other is null) return 1;
            int hexCompare = HexCoordinates.CompareTo(other.HexCoordinates);
            if (hexCompare != 0) return hexCompare;
            return Position.CompareTo(other.Position);
        }

        public static bool operator ==(BuildingKey? left, BuildingKey? right)
        {
            if (left is null && right is null) return true;
            if (left is null || right is null) return false;
            if (ReferenceEquals(left, right)) return true;
            return left.Equals(right);
        }

        public static bool operator !=(BuildingKey? left, BuildingKey? right) => !(left == right);

        public static BuildingKey Default => new(HexCoordinates.Default, HexPosition.None);
    }

}