using System;
using System.Collections.Generic;
using System.Diagnostics;
using Catan3.Shared.Models;
using Catan3.Shared.Utility;
using Microsoft.UI.Xaml.Media;
using Point = Windows.Foundation.Point;
namespace Catan3.DesktopApp.Layout
{
    /// <summary>
    /// This class is responsible for calculating the geometry of an inner hexagon placed within an outer hexagon. 
    /// The dimensions and placement are determined based on the size of the outer hexagon, the desired gap between the 
    /// two hexagons (TileGap), and the stroke thickness of the inner hexagon (InnerHexStrokeThickness).
    /// 
    /// Definitions:
    /// - OuterHexSize: The distance from the center to any vertex of the outer hexagon. This is a constant value in the code.
    /// - InnerHexSize: The calculated size of the inner hexagon which accounts for the desired gap and the stroke thickness.
    ///   It is derived from the OuterHexSize, reduced by the TileGap and InnerHexStrokeThickness, to ensure the inner hexagon's 
    ///   stroke is fully contained within its bounds.
    /// - TileGap: The visual distance desired between the bottom edge of the upper hexagon and the top edge of the lower hexagon.
    /// - InnerHexStrokeThickness: The thickness of the stroke applied to the inner hexagon. This value is used in the calculation
    ///   of InnerHexSize to ensure the stroke does not extend beyond the boundary defined by the TileGap.
    /// - InnerHexPoints and OuterHexPoints: PointCollections that hold the vertices of the inner and outer hexagons, respectively.
    ///   The inner hexagon is centered within the outer hexagon using calculated offsets based on the above properties.
    /// 
    /// The hexagons are flat-topped, and their vertices are calculated starting from the rightmost vertex proceeding clockwise.
    /// The positioning of the inner hexagon is adjusted such that the TileGap is the same on all sides, and the inner hexagon's 
    /// stroke is fully inside the boundary defined by the TileGap around the outer hexagon.
    /// </summary>
    public partial class BoardVisualLayout
    {
        private BoardVisualLayout() { }
        //
        //  this is used in the DependencyProperties so that there is a reasonable non-null default
        public static BoardVisualLayout Default { get; } = new BoardVisualLayout();
        /// <summary>
        ///     return the top based on the geometry of a Regular Flat Topped Hexagon
        ///     see https://www.redblobgames.com/grids/hexagons/
        ///     We also have some base primitives like the BuildingSize, GameMargin, etc. that affect
        ///     exactly where a tile should go.
        /// </summary>
        /// <param name="key"></param>
        /// <returns></returns>
        public double Top(HexCoordinates key)
        {
            var top = (key.Q / 2.0 + key.R) * OuterHexSize * Math.Sqrt(3);
            top += TileYOffset;
            return Math.Round(top, 2);
        }
        public double Left(HexCoordinates key)
        {
            var left = OuterHexSize * 1.5 * key.Q;
            left += TileXOffset;
            return Math.Round(left, 2);
        }
        /// <summary>
        ///     InnerHexSize if a function of the TileGame, InnerHexStroke, and OuterHexSize (which is defined by the TD4 Template)
        ///     The InnerHex splits the TileGap so that the InnerHex and the OuterHex have aligned centers.
        ///     We offset the InnerHexStrokeThickness so that the Size goes to the edge of the Stroke
        /// </summary>
        public double InnerHexSize
        {
            get
            {
                // Adjust the inner hex size to exclude the stroke thickness from the hex itself.
                // The outer hex size is reduced by the TileGap and the 1/2 stroke thickness 
                double adjustedSize = OuterHexSize - TileGap - InnerHexStrokeThickness * 0.5;
                return adjustedSize;
            }
        }
        /// <summary>
        ///     The Points that a Polygon can bind to that has InnerHexSize.  Note that the math here can be strange
        ///     because from a geometry point of view, the InnerHexSize defines exactly the size of the Hex -- but 
        ///     Polygon geometry needs to offset this because the Stroke is 1/2 in the Polygon and 1/2 out of it.
        ///     the size is the "radius" of the hex and the vertical/Horizontal offsets are therefore twice (diameter)
        ///     as much as might be expected
        /// </summary>
        public PointCollection InnerHexPoints
        {
            get
            {
                // Calculate the horizontal difference after accounting for the stroke
                double sizeDiff = (OuterHexSize - InnerHexSize);
                // The inner hexagon needs to be positioned such that the gap is equal on all sides.
                double verticalAdjustment = (sizeDiff) * .86;
                double horizontalAdjustment = (sizeDiff);
                return HexGeometry.FlatTopHexPoints(InnerHexSize, horizontalAdjustment, verticalAdjustment);
            }
        }
        /// <summary>
        ///     A Hex that is not normally visible whose width is the width of the full control
        /// </summary>
        public PointCollection OuterHexPoints
        {
            get
            {
                // OuterHex doesn't need adjustment as it's the reference
                return HexGeometry.FlatTopHexPoints(OuterHexSize, 0, 0);
            }
        }
        public PointCollection PointyHexPoints
        {
            get
            {
                // adjust these points so they are in the center of the flat to pHex
                return HexGeometry.PointyTopHexPoints(OuterHexSize, ControlWidth / 2.0, ControlHeight / 2.0);
            }
        }
        public double ControlWidth => HexGeometry.Width(this.OuterHexSize);
        public double ControlHeight => HexGeometry.Height(this.OuterHexSize);
    }
    public static class LayoutExtensions
    {
        /// <summary>
        ///     This is useful for laying out the Buildings.  Take the OuterHexPoints and get the HexGeometry.
        ///     then call this function
        ///     
        ///     var dict = layout.OuterHexPoints.FlatTopListToDictionary()
        /// </summary>
        /// <param name="points"></param>
        /// <returns></returns>
        public static Dictionary<HexPosition, Point> FlatTopListToDictionary(this PointCollection points)
        {
            Debug.Assert(points.Count == 6);
            var dict = new Dictionary<HexPosition, Point>
            {
                [HexPosition.TopLeft] = points[4],
                [HexPosition.TopRight] = points[5],
                [HexPosition.Right] = points[0],
                [HexPosition.BottomRight] = points[1],
                [HexPosition.BottomLeft] = points[2],
                [HexPosition.Left] = points[3],
            };
            return dict;
        }
        /// <summary>
        ///     This is useful for laying out the Harbors.  Take the OuterHexPoints and get the HexGeometry.
        ///     then call this function
        ///     
        ///     var dict = layout.PointyHexPoints.PointyTopListToDictionary()
        /// </summary>
        /// <param name="points"></param>
        /// <returns></returns>
        public static Dictionary<HexSide, Point> PointyTopListToDictionary(this PointCollection points)
        {
            // Ensure that the points collection has exactly 6 points
            Debug.Assert(points.Count == 6);
            var pointyTopDictionary = new Dictionary<HexSide, Point>
            { // The point indices are adjusted to match the flat-top hexagon's sides.
              // The mapping is done by rotating the pointy-top hexagon's points to align with the flat-top's sides.
                [HexSide.Top] = points[5], // Top-left of pointy-top is the top of flat-top
                [HexSide.TopRight] = points[0], // Top of pointy-top is the top-right of flat-top
                [HexSide.BottomRight] = points[1], // Top-right of pointy-top is the bottom-right of flat-top
                [HexSide.Bottom] = points[2], // Right of pointy-top is the bottom of flat-top
                [HexSide.BottomLeft] = points[3], // Bottom-right of pointy-top is the bottom-left of flat-top
                [HexSide.TopLeft] = points[4], // Bottom of pointy-top is the top-left of flat-top
            };
            return pointyTopDictionary;
        }
    }
}
