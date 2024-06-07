using System;
using System.Text.Json.Serialization;
using Catan3.Utility;
using CommunityToolkit.Mvvm.ComponentModel;
namespace Catan3.Models
{
    public partial class RoadKey(HexCoordinates tileKey, HexSide side) : ObservableObject, IComparable<RoadKey>
    {
        [ObservableProperty]
        private HexCoordinates _tileKey = tileKey;
        [ObservableProperty]
        private HexSide _hexSide = side;
        [JsonConstructor]
        public RoadKey() : this(HexCoordinates.Default, HexSide.Bottom)
        {
            {
            }
        }
        public override string ToString()
        {
            return String.Format($"{TileKey}-{HexSide}");
        }
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
            // First, compare by HexCoordinates
            int hexCompare = TileKey.CompareTo(other.TileKey);
            if (hexCompare != 0)
            {
                return hexCompare;
            }
          
            return HexSide.CompareTo(other.HexSide);
        }
        public static BuildingKey Default => new(HexCoordinates.Default, Utility.HexPosition.None);
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
        public static bool operator !=(RoadKey left, RoadKey right) => !( left == right );
    }
}
