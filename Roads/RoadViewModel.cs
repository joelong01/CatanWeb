using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using Catan3.Utility;
using Microsoft.UI.Xaml.Media;
using Windows.Devices.Midi;
using Windows.Foundation;
namespace Catan3.Models
{
    public partial class RoadViewModel : INotifyPropertyChanged
    {

        public void Init()
        {
            if (Layout is not null && Layout is BoardLayout rbl)
            {
                rbl.PropertyChanged += Layout_PropertyChanged;

            }


            UpdateLayout();
        }
        private void Layout_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (sender is BoardLayout layout)
            {
                Layout = layout;
                UpdateLayout();
            }
        }
        private void UpdateLayout()
        {

            Top = GetTop();
            Left = GetLeft();
            OnPropertyChanged(nameof(RoadPolygon));

        }


        private double GetLeft()
        {


            var left = Layout.Left(Road.RoadKey.TileKey);

            return left;
        }
        private double GetTop()
        {
            if (Layout is null) return 0.0;
            var top = Layout.Top(Road.RoadKey.TileKey);

            return top;
        }
        public double VisualRoadHeight
        {
            get
            {
                double height = 2 * Layout.TileGap;
                height += Layout.InnerHexStrokeThickness * 2;
                height += Layout.RoadStrokeThickness;
                return height;
            }
        }

        public double RoadWidth
        {
            get
            {
                var outerPointsDictionary = Layout.OuterHexPoints.ListToDictionary();


                var width = outerPointsDictionary[HexPosition.TopRight].X - outerPointsDictionary[HexPosition.TopLeft].X;

                return width;
            }
        }

        public PointCollection InnerHexPointsZeroBorder
        {
            get
            {

                // The inner hexagon needs to be positioned such that the gap is equal on all sides.
                // Therefore, the vertical and horizontal adjustments are half the TileGap, since it will appear on both sides of the hex.
                double verticalAdjustment = Layout.TileGap / 2;
                double horizontalAdjustment = Layout.TileGap / 2;



                // Calculate the horizontal difference after accounting for the stroke
                double sizeDiff = (Layout.OuterHexSize - Layout.InnerHexSize) / 2;

                return HexGeometry.HexPoints(Layout.InnerHexSize, sizeDiff + horizontalAdjustment, sizeDiff + verticalAdjustment);
            }
        }

        /// <summary>
        ///     keep this around if you want to put the road position in the roads for debugging purposes
        /// </summary>
        /// <param name="position"></param>
        /// <returns></returns>
        public string PositionShortName
        {
            get
            {
                string s=$"{Index}:";
                return this.Road.RoadKey.HexSide switch
                {

                    HexSide.BottomRight => s + "BR",
                    HexSide.BottomLeft => s + "BL",
                    HexSide.TopLeft => s + "TL",
                    HexSide.TopRight => s + "TR",
                    HexSide.None => s + "None",
                    HexSide.Top => s + "T",
                    HexSide.Bottom => s + "B",
                    _ => s + "?",
                };
            }
        }
        /// <summary>
        ///                  / 1 -------------------- 2 \
        ///                 /                            \
        ///                0                              3
        ///                 \                            /
        ///                  \ 5 ------------------- 4  /
        ///                  
        public PointCollection RoadPolygon
        {
            get
            {
                PointCollection points = [];
                if (Layout is null) return points;
                var outerHexPoints = Layout.OuterHexPoints.ListToDictionary();
                var innerHexPoints =  Layout.InnerHexPoints.ListToDictionary();
                switch (Road.RoadKey.HexSide)
                {
                    case HexSide.None:
                        break;
                    case HexSide.Top: // this are "half roads"
                        points.Add(outerHexPoints[HexPosition.TopLeft]);
                        points.Add(outerHexPoints[HexPosition.TopRight]);
                        points.Add(innerHexPoints[HexPosition.TopRight]);
                        points.Add(innerHexPoints[HexPosition.TopLeft]);
                        break;
                    case HexSide.TopRight:
                        var gap = GapBetweenTiles(Direction.NorthEast);
                        points.Add(outerHexPoints[HexPosition.TopRight]);
                        points.Add(innerHexPoints[HexPosition.TopRight]);
                        points.Add(innerHexPoints[HexPosition.Right]);
                        points.Add(outerHexPoints[HexPosition.Right]);
                        points.Add(new Point(innerHexPoints[HexPosition.BottomLeft].X + gap.X,
                                            innerHexPoints[HexPosition.BottomRight].Y + gap.Y));
                        points.Add(new Point(innerHexPoints[HexPosition.Left].X + gap.X, innerHexPoints[HexPosition.Left].Y + gap.Y));
                        break;
                    case HexSide.BottomRight:
                        var delta = GapBetweenTiles(Direction.SouthEast);
                        points.Add(innerHexPoints[HexPosition.BottomRight]);
                        points.Add(outerHexPoints[HexPosition.BottomRight]);
                        points.Add(new Point(innerHexPoints[HexPosition.Left].X + delta.X, innerHexPoints[HexPosition.Left].Y + delta.Y));
                        points.Add(new Point(innerHexPoints[HexPosition.TopLeft].X + delta.X, innerHexPoints[HexPosition.TopLeft].Y + delta.Y));
                        points.Add(outerHexPoints[HexPosition.Right]);
                        points.Add(innerHexPoints[HexPosition.Right]);

                        break;
                    case HexSide.Bottom:
                        points.Add(outerHexPoints[HexPosition.BottomLeft]);
                        points.Add(innerHexPoints[HexPosition.BottomLeft]);
                        points.Add(innerHexPoints[HexPosition.BottomRight]);
                        points.Add(outerHexPoints[HexPosition.BottomRight]);
                        points.Add(new Point(innerHexPoints[HexPosition.BottomRight].X, innerHexPoints[HexPosition.TopRight].Y + Layout.ControlHeight));
                        points.Add(new Point(innerHexPoints[HexPosition.BottomLeft].X, innerHexPoints[HexPosition.TopLeft].Y + Layout.ControlHeight));
                        break;
                    case HexSide.BottomLeft:
                        points.Add(innerHexPoints[HexPosition.BottomLeft]);
                        points.Add(outerHexPoints[HexPosition.BottomLeft]);
                        points.Add(outerHexPoints[HexPosition.Left]);
                        points.Add(innerHexPoints[HexPosition.Left]);
                        break;
                    case HexSide.TopLeft:
                        points.Add(innerHexPoints[HexPosition.Left]);
                        points.Add(outerHexPoints[HexPosition.Left]);
                        points.Add(outerHexPoints[HexPosition.TopLeft]);
                        points.Add(innerHexPoints[HexPosition.TopLeft]);
                        break;
                    default:
                        break;
                }
                return points;
            }
        }

        private Point GapBetweenTiles(Direction direction)
        {
            var adjacentKey  = this.Road.RoadKey.TileKey.GetAdjacentTile(direction);
            double  xGap = adjacentKey.Left(this.Layout) - this.Road.RoadKey.TileKey.Left(this.Layout);
            double  yGap = adjacentKey.Top(this.Layout) - this.Road.RoadKey.TileKey.Top(this.Layout);
            return new Point(xGap, yGap);

        }

      
    }
}
