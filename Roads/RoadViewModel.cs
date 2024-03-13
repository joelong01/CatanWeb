using System;
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
                //
                //  hackity hack -- we need these because we are doing the visual layout from the perspetive of the current tile
                //  the points are on adjacent tiles, so we either reget their layout and do the math, or go back to first principles
                //  to do the math.
                var magicX = (Layout.TileGap * .5 - Layout.RoadStrokeThickness + Layout.InnerHexStrokeThickness * .43);
                var magicY =  (Layout.TileGap * Math.Sqrt(3) / 2.0 + Layout.RoadStrokeThickness / 2.0 + Layout.InnerHexStrokeThickness / 2.0);
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
                        points.Add(outerHexPoints[HexPosition.TopRight]);
                        points.Add(innerHexPoints[HexPosition.TopRight]);
                        points.Add(innerHexPoints[HexPosition.Right]);
                        points.Add(outerHexPoints[HexPosition.Right]);
                        points.Add(new Point(outerHexPoints[HexPosition.Right].X + magicX,
                                            outerHexPoints[HexPosition.Right].Y - magicY));
                        points.Add(new Point(outerHexPoints[HexPosition.TopRight].X + outerHexPoints[HexPosition.Right].X - innerHexPoints[HexPosition.Right].X - Layout.RoadStrokeThickness ,
                                             outerHexPoints[HexPosition.TopRight].Y));
                        break;
                    case HexSide.BottomRight:
                        points.Add(innerHexPoints[HexPosition.BottomRight]);
                        points.Add(outerHexPoints[HexPosition.BottomRight]);
                        double dX = outerHexPoints[HexPosition.Right].X - innerHexPoints[HexPosition.Right].X ;
                        Point p = new (outerHexPoints[HexPosition.BottomRight].X + dX, outerHexPoints[HexPosition.BottomRight].Y);
                        points.Add(p);
                        p = new(outerHexPoints[HexPosition.Right].X + magicX, outerHexPoints[HexPosition.Right].Y + magicY ) ;
                        points.Add(p);
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




        //public PointCollection RoadPolygon
        //{
        //    get
        //    {
        //        PointCollection points = [];
        //        if (Layout is null) return points;
        //        var polygonHeight = VisualRoadHeight - Layout.RoadStrokeThickness  ; 
        //        var polygonWidth = RoadWidth - Layout.RoadStrokeThickness ;
        //        var halfStroke = Layout.RoadStrokeThickness * 0.5;
        //        double pointOneFiveX = polygonHeight / 2.0 * Math.Sqrt(3) / 4.0 ;
        //        points.Add(new Point(0, polygonHeight * 0.5)); // 0
        //        points.Add(new Point(pointOneFiveX, 0)); //1
        //        points.Add(new Point(polygonWidth - pointOneFiveX, 0)); //2
        //        points.Add(new Point(polygonWidth, polygonHeight * 0.5 )); //3
        //        points.Add(new Point(polygonWidth - pointOneFiveX, polygonHeight)); //4
        //        points.Add(new Point(pointOneFiveX, polygonHeight));  //5

        //        return points;
        //    }
        //}
        //  }
    }
}
