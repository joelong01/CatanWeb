using System;
using System.Collections.Generic;
using System.Diagnostics;
using Catan3.Utility;
using Microsoft.UI.Xaml.Media;

using Windows.Foundation;
namespace Catan3.Models
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
    public partial class BoardLayout
    {
        //
        //  this is used in the DependencyProperties so that there is a reasonable non-null default
        public static BoardLayout Default { get; } = new BoardLayout();

        /// <summary>
        ///     return the top based on the geometry of a Regular Flat Topped Hexagon
        ///     see https://www.redblobgames.com/grids/hexagons/
        ///     We also have some base primitives like the BuildingSize, GameMargin, etc. that affect
        ///     exactly where a tile should go.
        /// </summary>
        /// <param name="key"></param>
        /// <returns></returns>
        public double Top(TileKey key)
        {
            var outerHeight = HexGeometry.Height(OuterHexSize);
            var top =  ( .5 * key.Q +  key.R)*outerHeight ;
            top += 2 * outerHeight;
            top = Math.Round(top + BuildingSize * .5, 1); // the buildings will go on top of the highest tile, give them room
            top += InnerHexStrokeThickness + GameMargin;
            return top;
        }
        public double Left(TileKey key)
        {
            var left = 2 * OuterHexSize * .75 * key.Q ;
            left += ColumnOffset * 2 * OuterHexSize;
            left += ( BuildingSize * 0.5 );
            left += InnerHexStrokeThickness * 0.5;
            left += GameMargin;
            return left;
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
                // The outer hex size is reduced by the TileGap and the full stroke thickness (once, not doubled)
                double adjustedSize = OuterHexSize - TileGap - InnerHexStrokeThickness;
                return adjustedSize;
            }
        }
        /// <summary>
        ///     The Points that a Polygon can bind to that has InnerHexSize.  Note that the math here can be strange
        ///     because from a geometry point of view, the InnerHexSize defines exactly the size of the Hex -- but 
        ///     Polygon geometry needs to offset this because the Stroke is 1/2 in the Polygon and 1/2 out of it.
        /// </summary>
        public PointCollection InnerHexPoints
        {
            get
            {
               

                // Adjust for the stroke thickness of the inner hex by subtracting half the StrokeThickenss from the height on the InnerHexSize
                // since the stroke is centered on the hexagon's path, we only add half of the stroke thickness to the adjustment
                double innerSizeWithStroke = OuterHexSize - (InnerHexStrokeThickness  / 2.0) - TileGap;

                // Calculate the horizontal difference after accounting for the stroke
                double sizeDiff = (OuterHexSize - innerSizeWithStroke);

                // The inner hexagon needs to be positioned such that the gap is equal on all sides.
    
                double verticalAdjustment = (sizeDiff + (TileGap  + InnerHexStrokeThickness  )  * 0.5) / 2.0;
                double horizontalAdjustment = verticalAdjustment;

                return HexGeometry.HexPoints(innerSizeWithStroke, horizontalAdjustment, verticalAdjustment);
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
                return HexGeometry.HexPoints(OuterHexSize, 0, 0);
            }
        }


        public double ControlWidth => HexGeometry.Width(this.OuterHexSize);

        public double ControlHeight => HexGeometry.Height(this.OuterHexSize);

        public double BoardWidth => OuterHexSize * 7 + BuildingSize + GameMargin;

        public double BoardHeight => OuterHexSize * Math.Sqrt(3) * ColumnCount + BuildingSize + GameMargin;



    }

    public static class LayoutExtensions
    {
        /// <summary>
        ///     This is useful for laying out the Buildings.  Take the OuterHexPoints and get the HexGeometry.
        ///     then call this function
        ///     
        ///     var dict = layout.OuterHexPoints.ListToDictionary()
        /// </summary>
        /// <param name="points"></param>
        /// <returns></returns>
        public static Dictionary<HexPosition, Point> ListToDictionary(this PointCollection points)
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

    }
}
