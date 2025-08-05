using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text.Json.Serialization;
using Catan3.Utility;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Messaging;
using Microsoft.UI.Xaml.Media;
using Windows.Foundation;
namespace Catan3.Models
{
    /// <summary>
    /// Represents the view model for a harbor, including its layout, position, and orientation.
    /// </summary>
    public partial class HarborViewModel : ObservableRecipient
    {
        /// <summary>
        /// Gets or sets the harbor model.
        /// </summary>
        [property: JsonIgnore]
        [ObservableProperty]
        public partial HarborModel Harbor { get; set; }

        /// <summary>
        /// Gets or sets the board layout.
        /// </summary>
        [ObservableProperty]
        public partial BoardLayout Layout { get; set; }

        /// <summary>
        /// Gets or sets the left position of the harbor.
        /// </summary>
        [ObservableProperty]
        public partial double Left { get; set; }

        /// <summary>
        /// Gets or sets the top position of the harbor.
        /// </summary>
        [ObservableProperty]
        public partial double Top { get; set; }

        /// <summary>
        /// Gets or sets the orientation of the harbor.
        /// </summary>
        [ObservableProperty]
        public partial CatanOrientation Orientation { get; set; } = CatanOrientation.FaceUp;

        /// <summary>
        /// Initializes a new instance of the HarborViewModel class with the specified harbor model and layout.
        /// </summary>
        /// <param name="harbor">The harbor model.</param>
        /// <param name="layout">The board layout.</param>
        public HarborViewModel(HarborModel harbor, BoardLayout layout)
        {
            Harbor = harbor;
            Layout = layout;
            Init();
        }

        /// <summary>
        /// Gets the default instance of the HarborViewModel class.
        /// </summary>
        public static HarborViewModel Default => new(HarborModel.Default, BoardLayout.Default);

        /// <summary>
        /// Initializes the view model by setting up property change listeners and updating the layout.
        /// </summary>
        void Init()
        {
            if (Layout is not null && Layout is BoardLayout rbl)
            {
                rbl.PropertyChanged += Layout_PropertyChanged;
            }
            else
            {
                Layout = BoardLayout.Default;
                Layout.PropertyChanged += Layout_PropertyChanged;
            }
            UpdateLayout();
            Messenger.Register<UpdateOrientation>(this, (recipient, message) =>
            {
                this.Orientation = message.Orientation;
            });
            Messenger.Register<EndGame>(this, (recipient, message) =>
            {
                Messenger.UnregisterAll(this);
            });
        }

        /// <summary>
        /// Handles property changes in the layout and updates the layout accordingly.
        /// </summary>
        /// <param name="sender">The sender of the event.</param>
        /// <param name="e">The event arguments.</param>
        private void Layout_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (sender is BoardLayout layout)
            {
                this.Layout = layout;
                UpdateLayout();
            }
        }

        /// <summary>
        /// Updates the layout of the harbor based on its coordinates and side.
        /// </summary>
        private void UpdateLayout()
        {
            var point = GetLeftTop(Layout, Harbor.HarborKey.HexCoordinates, Harbor.HarborKey.Side);
            Left = point.X;
            Top = point.Y;
            OnPropertyChanged(nameof(HarborPoints));
        }

        /// <summary>
        /// Calculates the left and top position of the harbor based on the layout, coordinates, and side.
        /// </summary>
        /// <param name="Layout">The board layout.</param>
        /// <param name="coordinates">The coordinates of the harbor.</param>
        /// <param name="side">The side of the hexagon where the harbor is located.</param>
        /// <returns>A Point representing the left and top position of the harbor.</returns>
        public static Point GetLeftTop(BoardLayout Layout, HexCoordinates coordinates, HexSide side)
        {
            double top = Layout.Top(coordinates);
            double left = Layout.Left(coordinates);
            double size = Layout.BuildingSize;
            var pointDictionary = Layout.PointyHexPoints.PointyTopListToDictionary();
            // Get the point from the dictionary that corresponds to the harbor's position
            Point vertexPoint = pointDictionary[side];
            // Adjust top and left to position the center of the harbor on the vertex
            top += vertexPoint.Y - size / 2.0; // Center vertically
            left += vertexPoint.X - size / 2.0; // Center horizontally
                                                // Calculate the edge offset for the harbor's new center position
            double edgeOffset = size / 2.0; // Harbor's radius
            double horizontalOffset = edgeOffset * Math.Sqrt(3) / 2;
            double verticalOffset = edgeOffset / 2;
            // Now adjust based on Harbor position so that it is on the "edge" of the pointy tip
            switch (side)
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
                    throw new ArgumentOutOfRangeException(nameof(Harbor.HarborKey.Side), $"Invalid hex side: {side}");
            }
            return new Point(left, top);
        }

        /// <summary>
        /// Gets the points representing the harbor's shape.
        /// </summary>
        public PointCollection HarborPoints
        {
            get
            {
                PointCollection points = new();
                double size = Layout.BuildingSize; // Assuming this is the diameter of the harbor circle
                var flatTopDictionary = Layout.OuterHexPoints.FlatTopListToDictionary();
                var tileTop = Layout.Top(Harbor.HarborKey.HexCoordinates);
                var tileLeft = Layout.Left(Harbor.HarborKey.HexCoordinates);
                var yOffset = Math.Abs(tileTop - Top);
                var xOffset = Math.Abs(Left - tileLeft);
                // Calculate the coordinates of the triangle relative to the UserControl's coordinates
                // (0,0) is the top-left corner of the UserControl
                double centerX = size / 2.0; // X coordinate of the center of the harbor within the UserControl
                double centerY = size / 2.0; // Y coordinate of the center of the harbor within the UserControl
                Point topRight, topLeft, bottomRight, bottomLeft, left, right;
                switch (Harbor.HarborKey.Side)
                {
                    case HexSide.Top:
                        topLeft = flatTopDictionary[HexPosition.TopLeft];
                        topRight = flatTopDictionary[HexPosition.TopRight];
                        points.Add(new Point(centerX, centerY));
                        points.Add(topLeft.Offset(-xOffset, yOffset));
                        points.Add(topRight.Offset(-xOffset, yOffset));
                        break;
                    case HexSide.TopRight:
                        topRight = flatTopDictionary[HexPosition.TopRight];
                        right = flatTopDictionary[HexPosition.Right];
                        points.Add(new Point(centerX, centerY));
                        points.Add(topRight.Offset(-xOffset, -yOffset));
                        points.Add(right.Offset(-xOffset, -yOffset));
                        break;
                    case HexSide.BottomRight:
                        right = flatTopDictionary[HexPosition.Right];
                        bottomRight = flatTopDictionary[HexPosition.BottomRight];
                        points.Add(new Point(centerX, centerY));
                        points.Add(right.Offset(-xOffset, -yOffset));
                        points.Add(bottomRight.Offset(-xOffset, -yOffset));
                        break;
                    case HexSide.Bottom:
                        bottomLeft = flatTopDictionary[HexPosition.BottomLeft];
                        bottomRight = flatTopDictionary[HexPosition.BottomRight];
                        points.Add(new Point(centerX, centerY));
                        points.Add(bottomLeft.Offset(-xOffset, -yOffset));
                        points.Add(bottomRight.Offset(-xOffset, -yOffset));
                        break;
                    case HexSide.BottomLeft:
                        bottomLeft = flatTopDictionary[HexPosition.BottomLeft];
                        left = flatTopDictionary[HexPosition.Left];
                        points.Add(new Point(centerX, centerY));
                        points.Add(bottomLeft.Offset(xOffset, -yOffset));
                        points.Add(left.Offset(xOffset, -yOffset));
                        break;
                    case HexSide.TopLeft:
                        topLeft = flatTopDictionary[HexPosition.TopLeft];
                        left = flatTopDictionary[HexPosition.Left];
                        points.Add(new Point(centerX, centerY));
                        points.Add(topLeft.Offset(xOffset, -yOffset));
                        points.Add(left.Offset(xOffset, -yOffset));
                        break;
                    default:
                        throw new ArgumentOutOfRangeException(nameof(Harbor.HarborKey.Side), $"Invalid hex side: {Harbor.HarborKey.Side}");
                }
                return points;
            }
        }

        /// <summary>
        /// Returns a string representation of the harbor view model.
        /// </summary>
        /// <returns>A string representation of the harbor view model.</returns>
        public override string? ToString()
        {
            return Harbor.ToString();
        }
    }
    /// <summary>
    /// Provides extension methods for collections of HarborViewModel instances.
    /// </summary>
    public static class HarborExtensions
    {
        /// <summary>
        /// Finds a harbor in the collection with the specified coordinates and side.
        /// </summary>
        /// <param name="collection">The collection of HarborViewModel instances.</param>
        /// <param name="coords">The coordinates of the harbor.</param>
        /// <param name="side">The side of the hexagon where the harbor is located.</param>
        /// <returns>The HarborViewModel instance if found; otherwise, null.</returns>
        public static HarborViewModel? FindHarbor(this IEnumerable<HarborViewModel> collection, HexCoordinates coords, HexSide side)
        {
            return collection.FirstOrDefault(h => h.Harbor.HarborKey.HexCoordinates == coords && h.Harbor.HarborKey.Side == side);
        }

        /// <summary>
        /// Finds any harbors in the collection with the specified coordinates.
        /// </summary>
        /// <param name="collection">The collection of HarborViewModel instances.</param>
        /// <param name="coords">The coordinates of the harbors.</param>
        /// <returns>A list of HarborViewModel instances if found; otherwise, an empty list.</returns>
        public static List<HarborViewModel>? FindAnyHarbor(this IEnumerable<HarborViewModel> collection, HexCoordinates coords)
        {
            return collection.Where(h => h.Harbor.HarborKey.HexCoordinates == coords).ToList();
        }

        public static ResourceType ToResourceType(this HarborType harborType)
        {
            return harborType switch
            {
                HarborType.Brick => ResourceType.Brick,
                HarborType.Ore => ResourceType.Ore,
                HarborType.Sheep => ResourceType.Sheep,
                HarborType.Wheat => ResourceType.Wheat,
                HarborType.Wood => ResourceType.Wood,
                _ => ResourceType.None,
            };
        }
    }
}
