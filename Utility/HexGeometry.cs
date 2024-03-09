using System;
using Microsoft.UI.Xaml.Media;
using Windows.Foundation;

namespace Catan3.Utility
{
    /// <summary>
    ///     this is a static class for the Geometry of a Regular Flat Top Hexagon.  Points assume (0,0) as the origin
    /// </summary>
    public static class HexGeometry
    {

        /// <summary>
        /// Given the size of a regular hexagon, returns a PointCollection that defines the vertices to draw that hexagon.
        /// The size parameter represents the distance from the center of the hexagon to any of its vertices.
        /// The first vertex (upper left of the hexagon) is calculated based on the hexagon being centered at (size, size).
        /// </summary>
        /// <param name="size">The distance from the center of the hexagon to any of its vertices.</param>
        /// <returns>A PointCollection representing the vertices of the hexagon in the order to draw it.</returns>
        public static PointCollection HexPoints(double size)
        {
            PointCollection points = new PointCollection();

            // The angle between vertices in a hexagon in radians
            double angleRadians = Math.PI / 3;

            // Starting angle for the upper left vertex
            double startAngleRadians = Math.PI / 6;

            // Calculate each vertex position
            for (int i = 0; i < 6; i++)
            {
                double angle = startAngleRadians + i * angleRadians;
                double x = size * Math.Cos(angle) + size;
                double y = size * Math.Sin(angle) + size;
                points.Add(new Point(x, y));
            }

            return points;
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
