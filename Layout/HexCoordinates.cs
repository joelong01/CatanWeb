using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Text.Json.Serialization;
using Catan3.Models;
using Windows.Foundation;
namespace Catan3.Utility
{

    public enum HexSide { None = -1, Top = 0, TopRight = 1, BottomRight = 2, Bottom = 3, BottomLeft = 4, TopLeft = 5 };
    public enum Direction
    {
        North,
        NorthEast,
        SouthEast,
        South,
        SouthWest,
        NorthWest
    }

    public partial class HexCoordinates(int q, int r, int s) : IComparable<HexCoordinates>
    {
        [JsonIgnore]
        public static
                Dictionary<Direction, HexCoordinates> Directions
        { get; } = new()
                    {
                        { Direction.North, new HexCoordinates(0, -1, 1) },
                        { Direction.NorthEast, new HexCoordinates(1, -1, 0) },
                        { Direction.SouthEast, new HexCoordinates(1, 0, -1) },
                        { Direction.South, new HexCoordinates(0, 1, -1) },
                        { Direction.SouthWest, new HexCoordinates(-1, 1, 0) },
                        { Direction.NorthWest, new HexCoordinates(-1, 0, 1) }
                    };

        public override string ToString()
        {
            return $"({Q},{R},{S})";
        }
        public static HexCoordinates? FromString(string str)
        {
            string[] tokens = str.Split(',', StringSplitOptions.RemoveEmptyEntries);
            if (tokens is null || tokens.Length != 3) return null;
            var q = Int32.Parse(tokens[0]);
            var r = Int32.Parse(tokens[1]);
            var s = Int32.Parse(tokens[2]);
            return new HexCoordinates(q, r, s);
        }
        public static HexCoordinates operator +(HexCoordinates x, HexCoordinates y)
        {
            return new HexCoordinates(x.Q + y.Q, x.R + y.R, x.S + y.S);
        }
        //
        //  this will be the TileKeys of the North tile.  depending on the 
        //  collection, the tile might not exist
        [JsonIgnore]
        public HexCoordinates North => this + Directions[Direction.North];
        [JsonIgnore] public HexCoordinates NorthEast => this + Directions[Direction.NorthEast];
        [JsonIgnore] public HexCoordinates SouthEast => this + Directions[Direction.SouthEast];
        [JsonIgnore] public HexCoordinates South => this + Directions[Direction.South];
        [JsonIgnore] public HexCoordinates SouthWest => this + Directions[Direction.SouthWest];
        [JsonIgnore] public HexCoordinates NorthWest => this + Directions[Direction.NorthWest];
        public HexCoordinates GetAdjacentTile(Direction dir) => this + Directions[dir];
        public override bool Equals(object? obj)
        {
            if (obj == null) return false;
            return obj is HexCoordinates key &&
                   Q == key.Q &&
                   R == key.R &&
                   S == key.S;
        }
        public override int GetHashCode() => HashCode.Combine(Q, R, S);
        public static HexCoordinates Default => new(-10, -10, -10);


        public static Point MidPoint(double left, double top, double size, HexSide side)
        {
            double height = Math.Sqrt(3) * size;
            double width = 2 * size; // Full width from left vertex to right vertex
            double sideLength = (Math.Sqrt(3)/2) * size; // Horizontal length of a side
            double horizontalMargin = (size - sideLength) / 2; // Distance from bounding box to hexagon side

            switch (side)
            {
                case HexSide.Top:
                    return new Point(left + size, top);
                case HexSide.TopRight:
                    return new Point(left + width - horizontalMargin, top + height / 4);
                case HexSide.BottomRight:
                    return new Point(left + width - horizontalMargin, top + 3 * height / 4);
                case HexSide.Bottom:
                    return new Point(left + size, top + height);
                case HexSide.BottomLeft:
                    return new Point(left + horizontalMargin, top + 3 * height / 4);
                case HexSide.TopLeft:
                    return new Point(left + horizontalMargin, top + height / 4);
                default:
                    throw new ArgumentOutOfRangeException(nameof(side), $"Invalid hex side: {side}");
            }
        }

        public int CompareTo(HexCoordinates? other)
        {
            if (other is null) return 1;

            if (Q.CompareTo(other.Q) != 0)
            {
                return Q.CompareTo(other.Q);
            }
            else if (R.CompareTo(other.R) != 0)
            {
                return R.CompareTo(other.R);
            }
            else
            {
                // Note the order is reversed for S to sort in descending order
                return -S.CompareTo(other.S);
            }
        }

        // Override == and != operators to use CompareTo for consistency
        public static bool operator ==(HexCoordinates left, HexCoordinates right)
        {
            if (ReferenceEquals(left, right))
            {
                return true;
            }
            if (left is null || right is null)
            {
                return false;
            }
            return left.CompareTo(right) == 0;
        }

        public static bool operator !=(HexCoordinates left, HexCoordinates right)
        {
            return !( left == right );
        }



    }
}
