using System;
using System.ComponentModel;
using Catan3.Utility;
namespace Catan3.Models
{
    public partial class RoadKey (HexCoordinates tileKey, HexSide position) : INotifyPropertyChanged
    {
       
       
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
