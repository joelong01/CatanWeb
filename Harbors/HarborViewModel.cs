using System;
using System.ComponentModel;
using Catan3.Utility;
using Windows.Foundation;

namespace Catan3.Models
{
    public partial class HarborViewModel
    {

        public static HarborViewModel Default => new HarborViewModel(HarborModel.Default, BoardLayout.Default);

        void Init()
        {
            if (Layout is not null && Layout is BoardLayout rbl)
            {
                rbl.PropertyChanged += Layout_PropertyChanged;
                Layout = Layout;
            }
            else
            {
                Layout = BoardLayout.Default;
            }
            UpdateLayout();
        }

        private void Layout_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (sender is BoardLayout layout)
            {
                this.Layout = layout;
                UpdateLayout();
            }
        }
        private void UpdateLayout()
        {
            double top = Layout.Top(Harbor.TileCoordinates);
            double left = Layout.Left(Harbor.TileCoordinates);
            double size = Layout.BuildingSize;

            var pointDictionary = Layout.PointyHexPoints.PointyTopListToDictionary();

            // Get the point from the dictionary that corresponds to the harbor's position
            Point vertexPoint = pointDictionary[Harbor.Position];

            // Adjust top and left to position the center of the harbor on the vertex
            top += vertexPoint.Y - size / 2.0; // Center vertically
            left += vertexPoint.X - size / 2.0; // Center horizontally

            // Calculate the edge offset for the harbor's new center position
            double edgeOffset = size / 2.0; // Harbor's radius
            double horizontalOffset = edgeOffset * Math.Sqrt(3) / 2;
            double verticalOffset = edgeOffset / 2;

            // Now adjust based on Harbor position so that it is on the "edge" of the pointy tip
            switch (Harbor.Position)
            {
                case HexSide.Top:
                    // No horizontal adjustment needed
                    top -= edgeOffset;
                    break;
                case HexSide.TopRight:
                    left += horizontalOffset; // Move to the right (center of right edge)
                    top -= verticalOffset; // Move up (edge of the circle)
                    break;
                case HexSide.BottomRight:
                    left += horizontalOffset; // Move to the right (center of right edge)
                    top += verticalOffset; // Move down (edge of the circle)
                    break;
                case HexSide.Bottom:
                    // No horizontal adjustment needed
                    top += edgeOffset;
                    break;
                case HexSide.BottomLeft:
                    left -= horizontalOffset; // Move to the left (center of left edge)
                    top += verticalOffset; // Move down (edge of the circle)
                    break;
                case HexSide.TopLeft:
                    left -= horizontalOffset; // Move to the left (center of left edge)
                    top -= verticalOffset; // Move up (edge of the circle)
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(Harbor.Position), $"Invalid hex side: {Harbor.Position}");
            }

            Top = top;
            Left = left;
        }



        public override string? ToString()
        {

            return Harbor.ToString();
        }


    }
}
