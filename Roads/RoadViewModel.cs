using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Reflection.Metadata.Ecma335;
using Catan3.Utility;
using Microsoft.UI.Xaml.Input;
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

        public PointCollection RoadPolygon
        {
            get
            {

                if (Layout is null) return [];
                var points =  GetRoadPoints(Road.RoadKey.HexSide, Road.RoadKey.TileKey, Layout);
                if (Road.RoadKey.TileKey == new TileKey(2, 0, -2) && Road.RoadKey.HexSide == HexSide.Bottom)
                {
                    this.TraceMessage($"[cacheHit={cacheHit}][cacheMiss={cacheMiss}][cacheSize={RoadCache.Count}]");
                }
                return points;

            }
        }



        private static int cacheHit;
        private static int cacheMiss;
        private static readonly Dictionary<(double size, double tilegap, double stroke), Dictionary<HexSide, PointCollection>> RoadCache = [];
        /// <summary>
        ///  Every tile has the same PointsCollection.   the collection must be unique per control because of the way x:Bind works...but the 
        ///  values are all the same.  So we cache them in a dictionary so that we only do the calculations once per side for any particular
        ///  layout.  for a regular board, we should see something like this: [cacheHit=66][cacheMiss=6][cacheSize=1]
        ///  
        /// </summary>
        /// <param name="side"></param>
        /// <param name="tileKey"></param>
        /// <param name="layout"></param>
        /// <returns></returns>
        private static PointCollection GetRoadPoints(HexSide side, TileKey tileKey, BoardLayout layout)
        {
            Dictionary<HexSide, PointCollection>? sideDictionary;
            if (RoadCache.TryGetValue((layout.OuterHexSize, layout.TileGap, layout.InnerHexStrokeThickness), out sideDictionary))
            {
                if (sideDictionary is not null)
                {
                    if (sideDictionary.TryGetValue(side, out var cachedPoints))
                        if (cachedPoints is not null)
                        {
                            Debug.Assert(cachedPoints.Count > 0);
                            cacheHit++;
                            return cachedPoints.Clone();
                        }
                }
            }
            else
            {
                sideDictionary = [];
                RoadCache.Clear();
                RoadCache[(layout.OuterHexSize, layout.TileGap, layout.InnerHexStrokeThickness)] = sideDictionary;
            }
            Debug.Assert(sideDictionary is not null);
            var points = PointsForSide(side, tileKey, layout);
            cacheMiss++;

            sideDictionary[side] = points;
            return points;
        }
        /// 
        ///                  / 1 -------------------- 2 \
        ///                 /                            \
        ///                0                              3
        ///                 \                            /
        ///                  \ 5 ------------------- 4  / 
        ///        
        private static PointCollection PointsForSide(HexSide side, TileKey tileKey, BoardLayout layout)
        {
            PointCollection points = [];
            if (layout is null) return points;
            var outerHexPoints = layout.OuterHexPoints.ListToDictionary();
            var innerHexPoints =  layout.InnerHexPoints.ListToDictionary();
            switch (side)
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
                    var gap = GapBetweenTiles(tileKey, Direction.NorthEast, layout);
                    points.Add(outerHexPoints[HexPosition.TopRight]);
                    points.Add(innerHexPoints[HexPosition.TopRight]);
                    points.Add(innerHexPoints[HexPosition.Right]);
                    points.Add(outerHexPoints[HexPosition.Right]);
                    points.Add(new Point(innerHexPoints[HexPosition.BottomLeft].X + gap.X,
                                        innerHexPoints[HexPosition.BottomRight].Y + gap.Y));
                    points.Add(new Point(innerHexPoints[HexPosition.Left].X + gap.X, innerHexPoints[HexPosition.Left].Y + gap.Y));
                    break;
                case HexSide.BottomRight:
                    var delta = GapBetweenTiles(tileKey, Direction.SouthEast, layout);
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
                    points.Add(new Point(innerHexPoints[HexPosition.BottomRight].X, innerHexPoints[HexPosition.TopRight].Y + layout.ControlHeight));
                    points.Add(new Point(innerHexPoints[HexPosition.BottomLeft].X, innerHexPoints[HexPosition.TopLeft].Y + layout.ControlHeight));
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
        private static Point GapBetweenTiles(TileKey key, Direction direction, BoardLayout layout)
        {
            var adjacentKey  = key.GetAdjacentTile(direction);
            double  xGap = adjacentKey.Left(layout) - key.Left(layout);
            double  yGap = adjacentKey.Top(layout) - key.Top(layout);
            return new Point(xGap, yGap);
        }
    }
}
