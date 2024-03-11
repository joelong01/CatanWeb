using System;
using System.Collections.Generic;
using Microsoft.UI.Xaml.Media;
using Windows.Foundation;

namespace Catan3.Utility
{
    /// <summary>
    ///     this order needs to match the CalculateHexGeometry PointCollection order
    /// </summary>
    public enum HexPosition
    {
        TopLeft = 0,
        TopRight = 1,
        Right = 2,
        BottomRight = 3,
        BottomLeft = 4,
        Left = 5,
        None,
    }

    /// <summary>
    ///     this is a static class for the Geometry of a Regular Flat Top Hexagon.  Points assume (0,0) as the origin
    /// </summary>
    public static class HexGeometry
    {

        /// <summary>
        /// Given the size of a regular hexagon, returns a PointCollection that defines the vertices to draw a flat top hexagon.
        /// The size parameter represents the distance from the center of the hexagon to any of its vertices.
        /// The first vertex is at the top center of the hexagon, assuming (0,0) as the origin.
        /// </summary>
        /// <param name="size">The distance from the center of the hexagon to any of its vertices.</param>
        /// <returns>A PointCollection representing the vertices of the hexagon in the order to draw it.</returns>
        public static PointCollection HexPoints(double size, double deltaX, double deltaY)
        {
            PointCollection points = [];

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

                points.Add(new Point(adjustedX, adjustedY));
            }

            return points;
        }

        public static List<PointCollection> OuterHex(double size, double width, double deltaX, double deltaY)
        {
            var outerSegments = new List<PointCollection>();
            double angleRadians = Math.PI / 3; // 60 degrees in radians
            double outerSize = size + width; // Calculate the outer size based on the width offset

            // Calculate the points for the original and outer hexagon
            List<Point> innerPoints = new List<Point>();
            List<Point> outerPoints = new List<Point>();

            for (int i = 0; i < 6; i++)
            {
                double angle = i * angleRadians;

                // Calculate and add points for the inner hexagon
                double innerX = size * Math.Cos(angle) + deltaX + size;
                double innerY = size * Math.Sin(angle) + deltaY + (Math.Sqrt(3) * size / 2);
                innerPoints.Add(new Point(innerX, innerY));

                // Calculate and add points for the outer hexagon
                double outerX = outerSize * Math.Cos(angle) + deltaX + size;
                double outerY = outerSize * Math.Sin(angle) + deltaY + (Math.Sqrt(3) * outerSize / 2);
                outerPoints.Add(new Point(outerX, outerY));
            }

            // Create segments between each pair of points
            for (int i = 0; i < 6; i++)
            {
                var segmentPoints = new PointCollection();
                int nextIndex = (i + 1) % 6;

                // Add points to create the trapezoid segment
                segmentPoints.Add(innerPoints[i]);
                segmentPoints.Add(outerPoints[i]);
                segmentPoints.Add(outerPoints[nextIndex]);
                segmentPoints.Add(innerPoints[nextIndex]);

                outerSegments.Add(segmentPoints);
            }

            return outerSegments;
        }


        /// <summary>
        /// Calculates the height of a regular hexagon given its size.
        /// </summary>
        /// <param name="size">The size of the hexagon, defined as the distance from its center to any vertex.</param>
        /// <returns>The height of the hexagon, which is the vertical distance from one flat side to the opposite flat side.</returns>

        public static double Height(double size)
        {
            return size * Math.Sqrt(3);

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
            return 2 * size;
        }
        /// <summary>
        /// Calculates the coordinates of the midpoint on the upper right side of a regular hexagon.
        /// The size parameter represents the distance from the center of the hexagon to any of its vertices.
        /// </summary>
        /// <param name="size">The size of the hexagon, defined as the length from its center to any vertex.</param>
        /// <returns>The Point representing the midpoint on the upper right segment of the hexagon.</returns>
        public static Point BisectingPoint(double size)
        {
            // The x-coordinate is given by the horizontal distance to the midpoint of the upper right segment,
            // which can be calculated as the cosine of 30 degrees times the size.
            // The y-coordinate is half the height of the equilateral triangle formed by the segment,
            // which is given by the sine of 60 degrees times the size.
            // These calculations assume the hexagon's orientation is such that one side is horizontal at the top.
            return new Point(Math.Sqrt(3) / 2.0 * size, size / 2.0);
        }

        /// <summary>
        /// Calculates the size of a regular hexagon based on its height.
        /// </summary>
        /// <param name="height">The height of the hexagon, which is the distance from one flat side to the opposite flat side.</param>
        /// <returns>The size of the hexagon, defined as the distance from its center to any vertex. This is calculated as the height divided by 
        /// the square root of 3, based on the geometric properties of a regular hexagon.</returns>
        public static double SizeFromHeight(double height)
        {
            return height / Math.Sqrt(3);
        }

    }
}
