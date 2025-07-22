using System;
using System.Text.Json.Serialization;
using Catan3.Shared.Utility;

namespace Catan3.Shared.Models
{
    /// <summary>
    /// Represents a key for a road, including the tile coordinates and the side of the hexagon.
    /// </summary>
    public class RoadKey : IComparable<RoadKey>
    {
        public HexCoordinates TileKey { get; set; }
        public HexSide HexSide { get; set; }

        public RoadKey(HexCoordinates tileKey, HexSide side)
        {
            TileKey = tileKey;
            HexSide = side;
        }

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
    /// </summary>
    public class BuildingKey : IComparable<BuildingKey>
    {
        public HexCoordinates HexCoordinates { get; set; }
        public HexPosition Position { get; set; }

        public BuildingKey(HexCoordinates hexcoordinates, HexPosition position)
        {
            HexCoordinates = hexcoordinates;
            Position = position;
        }

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

        public static BuildingKey Default => new(HexCoordinates.Default, HexPosition.None);
    }

    /// <summary>
    /// Represents a key for a harbor.
    /// </summary>
    public class HarborKey : IComparable<HarborKey>
    {
        public HexCoordinates HexCoordinates { get; set; } = HexCoordinates.Default;
        public HexSide HexSide { get; set; } = HexSide.None;

        public HarborKey(HexCoordinates hexCoordinates, HexSide hexSide)
        {
            HexCoordinates = hexCoordinates;
            HexSide = hexSide;
        }

        [JsonConstructor]
        public HarborKey() { }

        public override string ToString() => $"{HexCoordinates}-{HexSide}";

        public override bool Equals(object? obj)
        {
            return obj is not null && obj is HarborKey key &&
                   key.HexSide == this.HexSide &&
                   key.HexCoordinates == this.HexCoordinates;
        }

        public override int GetHashCode() => HashCode.Combine(HexCoordinates, HexSide);

        /// <summary>
        /// Compares the current HarborKey with another HarborKey.
        /// </summary>
        /// <param name="other">The HarborKey to compare with the current HarborKey.</param>
        /// <returns>A value that indicates the relative order of the HarborKeys being compared.</returns>
        public int CompareTo(HarborKey? other)
        {
            if (other is null) return 1;
            int hexCompare = HexCoordinates.CompareTo(other.HexCoordinates);
            if (hexCompare != 0) return hexCompare;
            return HexSide.CompareTo(other.HexSide);
        }
    }
}