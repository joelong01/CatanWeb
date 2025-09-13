using System.Collections.Generic;
using System.Diagnostics;
using Microsoft.UI.Xaml.Media;
using Windows.Foundation;
using Catan3.Shared.Models;

namespace Catan3.Models
{
    /// <summary>
    /// Extension methods for PointCollection to convert hex points to dictionaries
    /// </summary>
    public static class PointCollectionExtensions
    {
        /// <summary>
        /// This is useful for laying out the Buildings. Take the OuterHexPoints and get the HexGeometry.
        /// then call this function
        /// 
        /// var dict = layout.OuterHexPoints().FlatTopListToDictionary()
        /// </summary>
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
                [HexPosition.Left] = points[3]
            };
            return dict;
        }

        /// <summary>
        /// This is useful for laying out the Harbors. Take the PointyHexPoints and get the HexGeometry.
        /// then call this function
        /// 
        /// var dict = layout.PointyHexPoints().PointyTopListToDictionary()
        /// </summary>
        public static Dictionary<HexSide, Point> PointyTopListToDictionary(this PointCollection points)
        {
            Debug.Assert(points.Count == 6);
            var dict = new Dictionary<HexSide, Point>
            {
                [HexSide.Top] = points[5], // Top-left of pointy-top is the top of flat-top
                [HexSide.TopRight] = points[0], // Top of pointy-top is the top-right of flat-top
                [HexSide.BottomRight] = points[1], // Top-right of pointy-top is the bottom-right of flat-top
                [HexSide.Bottom] = points[2], // Right of pointy-top is the bottom of flat-top
                [HexSide.BottomLeft] = points[3], // Bottom-right of pointy-top is the bottom-left of flat-top
                [HexSide.TopLeft] = points[4] // Bottom of pointy-top is the top-left of flat-top
            };
            return dict;
        }
    }
}
