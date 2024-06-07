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
    public partial class HarborViewModel : ObservableRecipient
    {
        [JsonIgnore]
        [ObservableProperty]
        private HarborModel _harbor;
        [ObservableProperty]
        private BoardLayout _layout;
        [ObservableProperty]
        private double _left;
        [ObservableProperty]
        private double _top;
        [ObservableProperty]
        private CatanOrientation _orientation = CatanOrientation.FaceUp;
        public HarborViewModel(HarborModel harbor, BoardLayout layout)
        {
            Harbor = harbor;
            Layout = layout;
            Init();
        }
        public static HarborViewModel Default => new(HarborModel.Default, BoardLayout.Default);
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
            var point = GetLeftTop(Layout, Harbor.HexCoordinates, Harbor.Side);
            Left = point.X;
            Top = point.Y;
            OnPropertyChanged(nameof(HarborPoints));
        }
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
                    throw new ArgumentOutOfRangeException(nameof(Harbor.Side), $"Invalid hex side: {side}");
            }
            return new Point(left, top);
        }
        public PointCollection HarborPoints
        {
            get
            {
                PointCollection points = [];
                double size = Layout.BuildingSize; // Assuming this is the diameter of the harbor circle
                var flatTopDictionary = Layout.OuterHexPoints.FlatTopListToDictionary();
                var tileTop = Layout.Top(Harbor.HexCoordinates);
                var tileLeft = Layout.Left(Harbor.HexCoordinates);
                var yOffset = Math.Abs(tileTop - Top);
                var xOffset = Math.Abs(Left - tileLeft);
                // Calculate the coordinates of the triangle relative to the UserControl's coordinates
                // (0,0) is the top-left corner of the UserControl
                double centerX = size / 2.0; // X coordinate of the center of the harbor within the UserControl
                double centerY = size / 2.0; // Y coordinate of the center of the harbor within the UserControl
                Point topRight, topLeft, bottomRight, bottomLeft, left, right;
                switch (Harbor.Side)
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
                        throw new ArgumentOutOfRangeException(nameof(Harbor.Side), $"Invalid hex side: {Harbor.Side}");
                }
                return points;
            }
        }
        public override string? ToString()
        {
            return Harbor.ToString();
        }
    }
    public static class HarborExtensions
    {
        public static HarborViewModel? FindHarbor(this IEnumerable<HarborViewModel> collection, HexCoordinates coords, HexSide side)
        {
            return collection.FirstOrDefault(h => h.Harbor.HexCoordinates == coords && h.Harbor.Side == side);
        }
        public static List<HarborViewModel>? FindAnyHarbor(this IEnumerable<HarborViewModel> collection, HexCoordinates coords)
        {
            return collection.Where(h => h.Harbor.HexCoordinates == coords).ToList();
        }
    }
}
