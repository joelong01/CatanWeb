using System;
using System.Collections.Generic;
using System.Diagnostics;
using Microsoft.UI.Xaml.Media;
using WinPoint = Windows.Foundation.Point;

namespace Catan3.DesktopApp.Layout
{
    /// <summary>
    ///     this is a static class for the Geometry of a Regular Flat top Hexagon.  Points assume (0,0) as the origin
    /// </summary>
    public static class HexGeometry
    {
        /// <summary>
        ///     we have an OuterHex and an InnerHex in the game. we'll cache 2 PointCollections so that we aren't constantly
        ///     recalculating them.  the bool is "IsFlatTop", because we use the same size for the pointer hex to get the 
        ///     points to place harbors and for the size of the Tile
        /// </summary>
        private static readonly Dictionary<(double, bool), PointCollection> HexCache = [];
        public static int CacheHit { get; private set; }
        public static int CacheMiss { get; private set; }
        public static int CacheSize => HexCache.Count;
        /// <summary>
        /// Given the size of a regular hexagon, returns a PointCollection that defines the vertices to draw a flat top hexagon.
        /// The size parameter represents the distance from the center of the hexagon to any of its vertices.
        /// The first vertex is at the top center of the hexagon, assuming (0,0) as the origin.
        /// </summary>
        /// <param name="size">The distance from the center of the hexagon to any of its vertices.</param>
        /// <returns>A PointCollection representing the vertices of the hexagon in the order to draw it.</returns>
        public static PointCollection FlatTopHexPoints(double size, double deltaX, double deltaY)
        {
            if (HexCache.TryGetValue((size, true), out PointCollection? points))
            {
                if (points is not null)
                {
                    if (points is not null)
                    {
                        Debug.Assert(points.Count > 0);
                        CacheHit++;
                        return points.Clone();
                    }
                }
            }
            if (HexCache.Count > 2) HexCache.Clear();
            CacheMiss++;
            points = [];
            // Calculate the width and height for positioning adjustments

            double height = Math.Sqrt(3) * size;
            // The angle between vertices in a hexagon in radians, starting from the horizontal right (for a flat top)
            double angleRadians = Math.PI / 3;
            // Calculate each vertex position, assuming the rightmost point should be at (0, height / 2)
            for (int i = 0; i < 6; i++)
            {
                double angle = i * angleRadians;
                // Original x and y based on the center at (0,0)
                double originalX = Math.Round(size * Math.Cos(angle), 2);
                double originalY = Math.Round(size * Math.Sin(angle), 2);
                // Adjust x to align the rightmost point at x=0 and y to center vertically in the parent control
                double adjustedX = originalX + size; // Translate x to move the hexagon's rightmost point to x=0
                double adjustedY = originalY + (height / 2); // Translate y to center the hexagon vertically
                adjustedX += deltaX;
                adjustedY += deltaY;
                HexCache[(size, true)] = points;
                var newPoint = new WinPoint(Math.Round(adjustedX, 2), Math.Round(adjustedY, 2));
                points.Add(newPoint);
            }
            return points;
        }
        /// <summary>
        ///     same idea as FlatTopHexPoints, except it draws a pointy top Hex
        /// </summary>
        /// <param name="size"></param>
        /// <param name="deltaX"></param>
        /// <param name="deltaY"></param>
        /// <returns></returns>
        public static PointCollection PointyTopHexPoints(double size, double deltaX, double deltaY)
        {
            if (HexCache.TryGetValue((size, false), out PointCollection? points))
            {
                if (points is not null)
                {
                    Debug.Assert(points.Count > 0);
                    CacheHit++;
                    return points.Clone();
                }
            }
            if (HexCache.Count > 2) HexCache.Clear();
            CacheMiss++;
            points = [];
            //      // for future reference
            // double width = 2 * size;
            // double height = Math.Sqrt(3) / 2 * width;
            // The angle between vertices in a hexagon in radians, starting from the top (for a pointy top)
            double angleRadians = Math.PI / 3;
            // Calculate each vertex position, assuming the topmost point should be at (0, 0)
            for (int i = 0; i < 6; i++)
            {
                double angle = i * angleRadians - Math.PI / 6; // Start from the top vertex
                // Original x and y based on the center at (0,0)
                double originalX = Math.Round(size * Math.Cos(angle), 2);
                double originalY = Math.Round(size * Math.Sin(angle), 2);
                // Adjust x and y to align the topmost point at (deltaX, deltaY)
                double adjustedX = originalX + deltaX;
                double adjustedY = originalY + deltaY;
                points.Add(new WinPoint(Math.Round(adjustedX, 2), Math.Round(adjustedY, 2)));
            }
            HexCache[(size, false)] = points;
            return points;
        }

        /// <summary>
        /// Calculates the height of a regular hexagon given its size.
        /// </summary>
        /// <param name="size">The size of the hexagon, defined as the distance from its center to any vertex.</param>
        /// <returns>The height of the hexagon, which is the vertical distance from one flat side to the opposite flat side.</returns>
        public static double Height(double size)
        {
            return Math.Round(size * Math.Sqrt(3), 2);
        }
        /// <summary>
        /// Calculates the width of a regular hexagon given its size.
        /// </summary>
        /// <param name="size">The size of the hexagon, defined as the distance from its center to any vertex.</param>
        /// <returns>The width of the hexagon, which is the distance from one flat side to the opposite flat side.</returns>
        public static double Width(double size)
        {
            // The width of a regular hexagon is equal to twice its side length.
            // This is because the hexagon can be divided into two equilateral triangles along its width.
            return Math.Round(2 * size, 2);
        }
        /// <summary>
        /// Calculates the coordinates of the midpoint on the upper right side of a regular hexagon.
        /// The size parameter represents the distance from the center of the hexagon to any of its vertices.
        /// </summary>
        /// <param name="size">The size of the hexagon, defined as the length from its center to any vertex.</param>
        /// <returns>The Point representing the midpoint on the upper right segment of the hexagon.</returns>
        public static WinPoint BisectingPoint(double size)
        {
            // The x-coordinate is given by the horizontal distance to the midpoint of the upper right segment,
            // which can be calculated as the cosine of 30 degrees times the size.
            // The y-coordinate is half the height of the equilateral triangle formed by the segment,
            // which is given by the sine of 60 degrees times the size.
            // These calculations assume the hexagon's orientation is such that one side is horizontal at the top.
            return new WinPoint(Math.Round(Math.Sqrt(3) / 2.0 * size, 2), Math.Round(size / 2.0, 2));
        }
        /// <summary>
        /// Calculates the size of a regular hexagon based on its height.
        /// </summary>
        /// <param name="height">The height of the hexagon, which is the distance from one flat side to the opposite flat side.</param>
        /// <returns>The size of the hexagon, defined as the distance from its center to any vertex. This is calculated as the height divided by 
        /// the square root of 3, based on the geometric properties of a regular hexagon.</returns>
        public static double SizeFromHeight(double height)
        {
            return Math.Round(height / Math.Sqrt(3), 2);
        }
    }
}
