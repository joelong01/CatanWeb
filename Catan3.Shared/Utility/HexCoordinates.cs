
using System.Text.Json.Serialization;
using Catan3.Shared.Models;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Catan3.Shared.Utility
{
    /// <summary>
    /// Represents hexagonal coordinates in a cube coordinate system.
    /// Supports both plain object usage (for JSON/API) and MVVM usage (for UI data binding).
    /// </summary>
    public partial class HexCoordinates(int q, int r, int s) : ObservableObject, IComparable<HexCoordinates>
    {
        [ObservableProperty]
        private int _q = q;

        [ObservableProperty]
        private int _r = r;

        [ObservableProperty]
        private int _s = s;

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

        /// <summary>
        /// Returns a string representation of the HexCoordinates.
        /// </summary>
        /// <returns>A string in the format "(Q,R,S)".</returns>
        public override string ToString()
        {
            return $"({Q},{R},{S})";
        }

        /// <summary>
        /// Creates a HexCoordinates instance from a string representation.
        /// </summary>
        /// <param name="str">The string representation in the format "Q,R,S".</param>
        /// <returns>A HexCoordinates instance or null if the string is invalid.</returns>
        public static HexCoordinates? FromString(string str)
        {
            string[] tokens = str.Split(',', StringSplitOptions.RemoveEmptyEntries);
            if (tokens is null || tokens.Length != 3) return null;
            var q = Int32.Parse(tokens[0]);
            var r = Int32.Parse(tokens[1]);
            var s = Int32.Parse(tokens[2]);
            return new HexCoordinates(q, r, s);
        }

        /// <summary>
        /// Adds two HexCoordinates instances.
        /// </summary>
        /// <param name="x">The first HexCoordinates instance.</param>
        /// <param name="y">The second HexCoordinates instance.</param>
        /// <returns>A new HexCoordinates instance representing the sum of the two inputs.</returns>
        public static HexCoordinates operator +(HexCoordinates x, HexCoordinates y)
        {
            return new HexCoordinates(x.Q + y.Q, x.R + y.R, x.S + y.S);
        }

        /// <summary>
        /// Gets the HexCoordinates of the tile to the north.
        /// </summary>
        [JsonIgnore]
        public HexCoordinates North => this + Directions[Direction.North];

        /// <summary>
        /// Gets the HexCoordinates of the tile to the northeast.
        /// </summary>
        [JsonIgnore]
        public HexCoordinates NorthEast => this + Directions[Direction.NorthEast];

        /// <summary>
        /// Gets the HexCoordinates of the tile to the southeast.
        /// </summary>
        [JsonIgnore]
        public HexCoordinates SouthEast => this + Directions[Direction.SouthEast];

        /// <summary>
        /// Gets the HexCoordinates of the tile to the south.
        /// </summary>
        [JsonIgnore]
        public HexCoordinates South => this + Directions[Direction.South];

        /// <summary>
        /// Gets the HexCoordinates of the tile to the southwest.
        /// </summary>
        [JsonIgnore]
        public HexCoordinates SouthWest => this + Directions[Direction.SouthWest];

        /// <summary>
        /// Gets the HexCoordinates of the tile to the northwest.
        /// </summary>
        [JsonIgnore]
        public HexCoordinates NorthWest => this + Directions[Direction.NorthWest];

        /// <summary>
        /// Gets the HexCoordinates of the adjacent tile in the specified direction.
        /// </summary>
        /// <param name="dir">The direction of the adjacent tile.</param>
        /// <returns>The HexCoordinates of the adjacent tile.</returns>
        public HexCoordinates GetAdjacentTile(Direction dir) => this + Directions[dir];

        /// <summary>
        /// Determines whether the specified object is equal to the current HexCoordinates.
        /// </summary>
        /// <param name="obj">The object to compare with the current HexCoordinates.</param>
        /// <returns>True if the specified object is equal to the current HexCoordinates; otherwise, false.</returns>
        public override bool Equals(object? obj)
        {
            if (obj == null) return false;
            return obj is HexCoordinates key &&
                   Q == key.Q &&
                   R == key.R &&
                   S == key.S;
        }

        /// <summary>
        /// Returns a hash code for the current HexCoordinates.
        /// </summary>
        /// <returns>A hash code for the current HexCoordinates.</returns>
        public override int GetHashCode() => HashCode.Combine(Q, R, S);

        /// <summary>
        /// Gets the default HexCoordinates instance.
        /// </summary>
        public static HexCoordinates Default => new(-10, -10, -10);

        /// <summary>
        /// Calculates the midpoint of a hexagon side.
        /// </summary>
        /// <param name="left">The left coordinate of the hexagon.</param>
        /// <param name="top">The top coordinate of the hexagon.</param>
        /// <param name="size">The size of the hexagon.</param>
        /// <param name="side">The side of the hexagon.</param>
        /// <returns>The midpoint of the specified hexagon side.</returns>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when an invalid hex side is specified.</exception>
        public static Point MidPoint(double left, double top, double size, HexSide side)
        {
            double height = Math.Sqrt(3) * size;
            double width = 2 * size; // Full width from left vertex to right vertex
            double sideLength = (Math.Sqrt(3) / 2) * size; // Horizontal length of a side
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
                // Note the order is reversed for S to sort in descending order
                return -S.CompareTo(other.S);
            }
        }

        /// <summary>
        /// Determines whether two HexCoordinates instances are equal.
        /// </summary>
        /// <param name="left">The first HexCoordinates instance.</param>
        /// <param name="right">The second HexCoordinates instance.</param>
        /// <returns>True if the two HexCoordinates instances are equal; otherwise, false.</returns>
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

        /// <summary>
        /// Determines whether two HexCoordinates instances are not equal.
        /// </summary>
        /// <param name="left">The first HexCoordinates instance.</param>
        /// <param name="right">The second HexCoordinates instance.</param>
        /// <returns>True if the two HexCoordinates instances are not equal; otherwise, false.</returns>
        public static bool operator !=(HexCoordinates left, HexCoordinates right)
        {
            return !(left == right);
        }
    }
}