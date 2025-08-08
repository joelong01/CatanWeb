using System.Collections.Generic;
using System.Diagnostics;
using Catan3.Shared.Models;
using Catan3.DesktopApp.Layout;
using Microsoft.UI.Xaml.Media;
using Windows.Foundation;

namespace Catan3.DesktopApp.Layout
{
    /// <summary>
    /// UI-specific extensions for BoardVisualLayout to add WinUI geometric calculations
    /// </summary>
    public static class BoardVisualLayoutExtensions
    {
        /// <summary>
        /// A collection of points for the inner hexagon positioned relative to the outer hexagon
        /// </summary>
        public static PointCollection InnerHexPoints(this BoardVisualLayout layout)
        {
            // Calculate the horizontal difference after accounting for the stroke
            double sizeDiff = (layout.OuterHexSize - layout.InnerHexSize);
            // The inner hexagon needs to be positioned such that the gap is equal on all sides.
            double verticalAdjustment = (sizeDiff) * .86;
            double horizontalAdjustment = (sizeDiff);
            return HexGeometry.FlatTopHexPoints(layout.InnerHexSize, horizontalAdjustment, verticalAdjustment);
        }

        /// <summary>
        /// A Hex that is not normally visible whose width is the width of the full control
        /// </summary>
        public static PointCollection OuterHexPoints(this BoardVisualLayout layout)
        {
            // OuterHex doesn't need adjustment as it's the reference
            return HexGeometry.FlatTopHexPoints(layout.OuterHexSize, 0, 0);
        }

        /// <summary>
        /// Points for a pointy-top hexagon positioned in the center
        /// </summary>
        public static PointCollection PointyHexPoints(this BoardVisualLayout layout)
        {
            // adjust these points so they are in the center of the flat to pHex
            return HexGeometry.PointyTopHexPoints(layout.OuterHexSize, layout.ControlWidth() / 2.0, layout.ControlHeight() / 2.0);
        }

        /// <summary>
        /// Width of the hexagon control
        /// </summary>
        public static double ControlWidth(this BoardVisualLayout layout) => HexGeometry.Width(layout.OuterHexSize);

        /// <summary>
        /// Height of the hexagon control
        /// </summary>
        public static double ControlHeight(this BoardVisualLayout layout) => HexGeometry.Height(layout.OuterHexSize);
        
        /// <summary>
        /// Converts a HexSide to the primary HexPosition (vertex) for that side.
        /// Used for positioning harbors which are placed at vertices rather than edges.
        /// </summary>
        public static HexPosition SideToPosition(HexSide side)
        {
            return side switch
            {
                HexSide.Top => HexPosition.TopLeft,
                HexSide.TopRight => HexPosition.TopRight,
                HexSide.BottomRight => HexPosition.Right,
                HexSide.Bottom => HexPosition.BottomRight,
                HexSide.BottomLeft => HexPosition.BottomLeft,
                HexSide.TopLeft => HexPosition.Left,
                _ => HexPosition.TopLeft
            };
        }
    }
}
