using System;
using System.ComponentModel;
namespace Catan3.Models
{
    public partial class RoadKey (TileKey tileKey, RoadPosition position) : INotifyPropertyChanged
    {
       
       
        public override string ToString()
        {
            return String.Format($"{TileKey}-{RoadPosition}");
        }
        public override bool Equals(object? obj)
        {
            return obj is not null && obj is RoadKey key &&
                   key.RoadPosition == this.RoadPosition &&
                   key.TileKey == this.TileKey;
        }
        public override int GetHashCode() => HashCode.Combine(TileKey, RoadPosition);
        public static BuildingKey Default => new(TileKey.Default, Utility.HexPosition.None);
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
