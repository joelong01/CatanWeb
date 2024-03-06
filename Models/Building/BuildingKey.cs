using System;
using System.ComponentModel;
namespace Catan3.Models
{
    public partial class BuildingKey(TileKey coords, BuildingPosition pos) : INotifyPropertyChanged
    {
        public BuildingPosition BuildingPosition { get; set; } = pos;
        public TileKey TileKey { get; set; } = coords;
        public override string ToString()
        {
            return $"[{TileKey}-{BuildingPosition}]";
        }
        public static BuildingKey? FromString(string str)
        {
            string[] tokens = str.Split(["[", "]", "-"], StringSplitOptions.RemoveEmptyEntries);
            if (tokens is null) return null;
            if (tokens.Length != 2) return null;
            var tileCoord = TileKey.FromString(tokens[0]);
            if (tileCoord is null) return null;
            var buildingPos = (BuildingPosition)Enum.Parse(typeof(BuildingPosition), tokens[1]);
            return new BuildingKey(tileCoord, buildingPos);
        }
        public override bool Equals(object? obj)
        {
            return obj is not null && obj is BuildingKey key &&
                   key.BuildingPosition == this.BuildingPosition &&
                   key.TileKey == this.TileKey;
        }
        public override int GetHashCode() => HashCode.Combine(TileKey, BuildingPosition);
        public static BuildingKey Default => new(TileKey.Default, BuildingPosition.None);
        public static bool operator ==(BuildingKey left, BuildingKey right)
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
        public static bool operator !=(BuildingKey left, BuildingKey right) => !( left == right );
    }
}
