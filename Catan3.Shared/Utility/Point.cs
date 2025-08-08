using System;

namespace Catan3.Shared.Utility
{
    /// <summary>
    /// Represents a point with double-precision coordinates.
    /// </summary>
    public readonly struct Point
    {
        public double X { get; }
        public double Y { get; }

        public Point(double x, double y)
        {
            X = x;
            Y = y;
        }

        public override string ToString() => $"({X}, {Y})";

        public override bool Equals(object? obj) => obj is Point other && X == other.X && Y == other.Y;

        public override int GetHashCode() => HashCode.Combine(X, Y);

        public static bool operator ==(Point left, Point right) => left.Equals(right);

        public static bool operator !=(Point left, Point right) => !left.Equals(right);
    }
}