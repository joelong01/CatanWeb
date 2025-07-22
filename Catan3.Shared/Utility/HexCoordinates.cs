using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Catan3.Shared.Utility
{
    public enum HexSide { None = -1, Top = 0, TopRight = 1, BottomRight = 2, Bottom = 3, BottomLeft = 4, TopLeft = 5 };
    public enum HexPosition { None = -1, Right = 0, BottomRight = 1, BottomLeft = 2, Left = 3, TopLeft = 4, TopRight = 5 };
    public enum Direction
    {
        North,
        NorthEast,
        SouthEast,
        South,
        SouthWest,
        NorthWest
    }

    public class HexCoordinates : IComparable<HexCoordinates>
    {
        public int Q { get; set; }
        public int R { get; set; }
        public int S { get; set; }

        [JsonIgnore]
        public static Dictionary<Direction, HexCoordinates> Directions { get; } = new()
        {
            { Direction.North, new HexCoordinates(0, -1, 1) },
            { Direction.NorthEast, new HexCoordinates(1, -1, 0) },
            { Direction.SouthEast, new HexCoordinates(1, 0, -1) },
            { Direction.South, new HexCoordinates(0, 1, -1) },
            { Direction.SouthWest, new HexCoordinates(-1, 1, 0) },
            { Direction.NorthWest, new HexCoordinates(-1, 0, 1) }
        };

        public HexCoordinates(int q, int r, int s)
        {
            Q = q;
            R = r;
            S = s;
        }

        [JsonConstructor]
        public HexCoordinates() : this(0, 0, 0) { }

        /// <summary>
        /// Creates a HexCoordinates instance from a string representation.
        /// </summary>
        /// <param name="str">The string representation in the format "Q,R,S".</param>
        /// <returns>A HexCoordinates instance or null if the string is invalid.</returns>
        public static HexCoordinates? FromString(string str)
        {
            string[] tokens = str.Split(',', StringSplitOptions.RemoveEmptyEntries);
            if (tokens is null || tokens.Length != 3) return null;
            var q = int.Parse(tokens[0]);
            var r = int.Parse(tokens[1]);
            var s = int.Parse(tokens[2]);
            return new HexCoordinates(q, r, s);
        }

        public static HexCoordinates operator +(HexCoordinates x, HexCoordinates y)
        {
            return new HexCoordinates(x.Q + y.Q, x.R + y.R, x.S + y.S);
        }

        [JsonIgnore]
        public HexCoordinates North => this + Directions[Direction.North];
        [JsonIgnore]
        public HexCoordinates NorthEast => this + Directions[Direction.NorthEast];
        [JsonIgnore]
        public HexCoordinates SouthEast => this + Directions[Direction.SouthEast];
        [JsonIgnore]
        public HexCoordinates South => this + Directions[Direction.South];
        [JsonIgnore]
        public HexCoordinates SouthWest => this + Directions[Direction.SouthWest];
        [JsonIgnore]
        public HexCoordinates NorthWest => this + Directions[Direction.NorthWest];

        public override string ToString() => $"({Q},{R},{S})";

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

        /// <summary>
        /// Compares the current HexCoordinates with another HexCoordinates.
        /// </summary>
        /// <param name="other">The HexCoordinates to compare with the current HexCoordinates.</param>
        /// <returns>A value that indicates the relative order of the HexCoordinates being compared.</returns>
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
                return -S.CompareTo(other.S);
            }
        }

        public static bool operator ==(HexCoordinates? left, HexCoordinates? right)
        {
            if (ReferenceEquals(left, right)) return true;
            if (left is null || right is null) return false;
            return left.CompareTo(right) == 0;
        }

        public static bool operator !=(HexCoordinates? left, HexCoordinates? right) => !(left == right);
    }
}