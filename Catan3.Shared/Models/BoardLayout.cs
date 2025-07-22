using System;
using Catan3.Shared.Utility;

namespace Catan3.Shared.Models
{
    /// <summary>
    /// Simplified board layout model for shared use, containing essential layout properties without UI dependencies.
    /// </summary>
    public class BoardLayout
    {
        public double TileYOffset { get; set; }
        public double TileXOffset { get; set; }
        public double TileGap { get; set; }
        public double GameMargin { get; set; }
        public double RoadStrokeThickness { get; set; }
        public double OuterHexSize { get; set; }
        public double BuildingSize { get; set; }
        public double InnerHexStrokeThickness { get; set; }
        public double BoardWidth { get; set; }
        public double BoardHeight { get; set; }

        private BoardLayout() { }

        /// <summary>
        /// This is used in the DependencyProperties so that there is a reasonable non-null default
        /// </summary>
        public static BoardLayout Default { get; } = new BoardLayout();

        /// <summary>
        /// Return the top based on the geometry of a Regular Flat Topped Hexagon
        /// See https://www.redblobgames.com/grids/hexagons/
        /// </summary>
        /// <param name="key">The hex coordinates</param>
        /// <returns>The top position</returns>
        public double Top(HexCoordinates key)
        {
            var top = (key.Q / 2.0 + key.R) * OuterHexSize * Math.Sqrt(3);
            top += TileYOffset;
            return Math.Round(top, 2);
        }

        /// <summary>
        /// Return the left position based on the geometry of a Regular Flat Topped Hexagon
        /// </summary>
        /// <param name="key">The hex coordinates</param>
        /// <returns>The left position</returns>
        public double Left(HexCoordinates key)
        {
            var left = OuterHexSize * 1.5 * key.Q;
            left += TileXOffset;
            return Math.Round(left, 2);
        }

        /// <summary>
        /// InnerHexSize is a function of the TileGap, InnerHexStroke, and OuterHexSize
        /// </summary>
        public double InnerHexSize
        {
            get
            {
                // Adjust the inner hex size to exclude the stroke thickness from the hex itself.
                double adjustedSize = OuterHexSize - TileGap - InnerHexStrokeThickness * 0.5;
                return adjustedSize;
            }
        }

        public double ControlWidth => 2 * OuterHexSize; // Width of a flat-topped hexagon
        public double ControlHeight => Math.Sqrt(3) * OuterHexSize; // Height of a flat-topped hexagon
    }
}