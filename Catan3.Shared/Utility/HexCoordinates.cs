using System.Diagnostics;
using System.Text.Json.Serialization;
using Catan3.Shared.Models;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Catan3.Shared.Utility
{
    /// <summary>
    /// Represents hexagonal coordinates in a cube coordinate system.
    /// Supports both plain object usage (for JSON/API) and MVVM usage (for UI data binding).
    /// Cube coordinates must satisfy the constraint Q + R + S = 0.
    /// </summary>
    public partial class HexCoordinates : ObservableObject, IComparable<HexCoordinates>
    {
        /// <summary>
        /// Initializes a new instance of the HexCoordinates class.
        /// </summary>
        /// <param name="q">The Q coordinate.</param>
        /// <param name="r">The R coordinate.</param>
        /// <param name="s">The S coordinate.</param>
        /// <exception cref="ArgumentException">Thrown when Q + R + S does not equal 0.</exception>
        public HexCoordinates(int q, int r, int s)
        {
            Debug.Assert(q + r + s == 0, $"Invalid cube coordinates: {q}+{r}+{s}={q + r + s}, must equal 0");
            if (q + r + s != 0)
            {
                throw new ArgumentException($"Invalid cube coordinates: Q({q}) + R({r}) + S({s}) = {q + r + s}, must equal 0");
            }
            Q = q;
            R = r;
            S = s;
        }

        [ObservableProperty]
        public partial int Q { get; set; }

        [ObservableProperty]
        public partial int R { get; set; }

        [ObservableProperty]
        public partial int S { get; set; }
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
        /// <param name="str">The string representation in the format "Q,R,S" or "(Q,R,S)".</param>
        /// <returns>A HexCoordinates instance or null if the string is invalid.</returns>
        public static HexCoordinates? FromString(string str)
        {
            // Strip parentheses to handle output from ToString()
            str = str.Trim('(', ')', ' ');
            string[] tokens = str.Split(',', StringSplitOptions.RemoveEmptyEntries);
            if (tokens is null || tokens.Length != 3) return null;
            var q = Int32.Parse(tokens[0].Trim());
            var r = Int32.Parse(tokens[1].Trim());
            var s = Int32.Parse(tokens[2].Trim());
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
        /// Subtracts one HexCoordinates instance from another.
        /// </summary>
        /// <param name="x">The first HexCoordinates instance.</param>
        /// <param name="y">The second HexCoordinates instance to subtract.</param>
        /// <returns>A new HexCoordinates instance representing the difference.</returns>
        public static HexCoordinates operator -(HexCoordinates x, HexCoordinates y)
        {
            return new HexCoordinates(x.Q - y.Q, x.R - y.R, x.S - y.S);
        }

        /// <summary>
        /// Calculates the distance between two HexCoordinates instances.
        /// </summary>
        /// <param name="a">The first HexCoordinates instance.</param>
        /// <param name="b">The second HexCoordinates instance.</param>
        /// <returns>The distance between the two HexCoordinates instances.</returns>
        public static double Distance(HexCoordinates a, HexCoordinates b)
        {
            var vec = a - b;
            return (Math.Abs(vec.Q) + Math.Abs(vec.R) + Math.Abs(vec.S)) / 2.0;
        }

        /// <summary>
        /// Calculates the distance from this HexCoordinates to another.
        /// </summary>
        /// <param name="other">The other HexCoordinates instance.</param>
        /// <returns>The distance to the other HexCoordinates instance.</returns>
        public double DistanceTo(HexCoordinates other)
        {
            return Distance(this, other);
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
        /// Uses values far outside any real board but satisfying Q+R+S=0.
        /// </summary>
        public static HexCoordinates Default => new(-99, 99, 0);

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
            return left.Q == right.Q && left.R == right.R && left.S == right.S;
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

        /// <summary>
        /// Determines whether this hex is adjacent to another hex (distance of 1).
        /// </summary>
        /// <param name="other">The other HexCoordinates instance.</param>
        /// <returns>True if the hexes are adjacent; otherwise, false.</returns>
        public bool IsAdjacent(HexCoordinates other)
        {
            return Distance(this, other) == 1;
        }

        /// <summary>
        /// Gets all 6 neighboring hex coordinates.
        /// </summary>
        /// <returns>A list of all adjacent HexCoordinates.</returns>
        public List<HexCoordinates> GetAllNeighbors()
        {
            var neighbors = new List<HexCoordinates>(6);
            foreach (var (_, direction) in Directions)
            {
                neighbors.Add(this + direction);
            }
            return neighbors;
        }

        /// <summary>
        /// Converts this hex coordinate to pixel coordinates (center point) for a flat-top hexagon.
        /// </summary>
        /// <param name="size">The size of the hexagon (distance from center to vertex).</param>
        /// <param name="offsetX">X offset for the board origin.</param>
        /// <param name="offsetY">Y offset for the board origin.</param>
        /// <returns>The pixel coordinates of the hex center as a Point.</returns>
        public Point ToPixelCenter(double size, double offsetX = 0, double offsetY = 0)
        {
            // Flat-top hex formulas from Red Blob Games:
            // x = size * 3/2 * q
            // y = size * sqrt(3) * (r + q/2)
            double x = size * 1.5 * Q + offsetX;
            double y = size * Math.Sqrt(3) * (R + Q / 2.0) + offsetY;
            return new Point(x, y);
        }

        /// <summary>
        /// Converts pixel coordinates to hex coordinates for a flat-top hexagon using cube rounding.
        /// </summary>
        /// <param name="pixelX">The X pixel coordinate.</param>
        /// <param name="pixelY">The Y pixel coordinate.</param>
        /// <param name="size">The size of the hexagon (distance from center to vertex).</param>
        /// <param name="offsetX">X offset for the board origin (pixel coords of hex 0,0,0 center).</param>
        /// <param name="offsetY">Y offset for the board origin (pixel coords of hex 0,0,0 center).</param>
        /// <returns>The nearest HexCoordinates to the given pixel position.</returns>
        public static HexCoordinates FromPixel(double pixelX, double pixelY, double size, double offsetX = 0, double offsetY = 0)
        {
            // Translate to coordinates relative to the center of hex (0,0,0)
            double relX = pixelX - offsetX;
            double relY = pixelY - offsetY;

            // Convert to fractional axial coordinates (inverse of ToPixelCenter formulas)
            double qf = relX / (1.5 * size);
            double rf = relY / (size * Math.Sqrt(3)) - qf / 2.0;

            // Convert axial (q, r) to cube coordinates (x, y, z) where x + y + z = 0
            double x = qf;
            double z = rf;
            double y = -x - z;

            // Cube rounding algorithm
            int rx = (int)Math.Round(x);
            int ry = (int)Math.Round(y);
            int rz = (int)Math.Round(z);

            double xDiff = Math.Abs(rx - x);
            double yDiff = Math.Abs(ry - y);
            double zDiff = Math.Abs(rz - z);

            // Reset the component with the largest rounding error
            if (xDiff > yDiff && xDiff > zDiff)
            {
                rx = -ry - rz;
            }
            else if (yDiff > zDiff)
            {
                ry = -rx - rz;
            }
            else
            {
                rz = -rx - ry;
            }

            return new HexCoordinates(rx, rz, ry);
        }
    }
}