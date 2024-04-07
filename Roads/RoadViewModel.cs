using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using Catan3.Utility;
using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.UI;
using Microsoft.UI.Xaml.Media;
using Windows.Foundation;
namespace Catan3.Models
{
    public partial class RoadViewModel : ObservableObject
    {
        [ObservableProperty]
        private RoadModel _road;

        [ObservableProperty]
        private BoardLayout _layout;

        [ObservableProperty]
        private PlayerViewModel? _owner;

        [ObservableProperty]
        private Brush _background = BrushCache.GetSolidColorBrush(Colors.Transparent);

        [ObservableProperty]
        private Brush _foreground = BrushCache.GetSolidColorBrush(Colors.Transparent);

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(RoadPolygon))]
        private double _left;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(RoadPolygon))]
        private double _top;

        [ObservableProperty]
        private double _index;

        public RoadViewModel(RoadModel road, BoardLayout layout)
        {
            _road = road;
            _layout = layout;
            Init();
        }
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

            Top = Layout.Top(Road.RoadKey.TileKey);
            Left = Layout.Left(Road.RoadKey.TileKey);

            OnPropertyChanged(nameof(RoadPolygon));
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
                //leaving comment here in case the cache is looked at again.
                //if (Road.RoadKey.HexCoordinates == new HexCoordinates(2, 0, -2) && Road.RoadKey.HexSide == HexSide.Bottom)
                //{
                //    this.TraceMessage($"[CacheHit={cacheHit}][cacheMiss={cacheMiss}][cacheSize={RoadCache.Count}]");
                //}
                return points;

            }
        }



        private static int cacheHit;
        private static int cacheMiss;
        private static readonly Dictionary<(double size, double tilegap, double stroke), Dictionary<HexSide, PointCollection>> RoadCache = [];
        /// <summary>
        ///  Every tile has the same PointsCollection.   the collection must be unique per control because of the way x:Bind works...but the 
        ///  values are all the same.  So we cache them in a dictionary so that we only do the calculations once per side for any particular
        ///  layout.  for a regular board, we should see something like this: [CacheHit=66][cacheMiss=6][cacheSize=1]
        ///  
        /// </summary>
        /// <param name="side"></param>
        /// <param name="tileKey"></param>
        /// <param name="layout"></param>
        /// <returns></returns>
        private static PointCollection GetRoadPoints(HexSide side, HexCoordinates tileKey, BoardLayout layout)
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
        private static PointCollection PointsForSide(HexSide side, HexCoordinates tileKey, BoardLayout layout)
        {
            PointCollection points = [];
            if (layout is null) return points;
            var outerHexPoints = layout.OuterHexPoints.FlatTopListToDictionary();
            var innerHexPoints =  layout.InnerHexPoints.FlatTopListToDictionary();
            Point delta;
            switch (side)
            {
                case HexSide.None:
                    break;
                case HexSide.Top: // this is exactly the same as the bottom, just offset by the height
                    PointCollection bottom = PointsForSide(HexSide.Bottom, tileKey, layout);
                    foreach (var point in bottom)
                    {
                        points.Add(new Point(point.X, point.Y - layout.ControlHeight));
                    }
                    break;
                case HexSide.TopRight:
                    delta = GapBetweenTiles(tileKey, Direction.NorthEast, layout);
                    points.Add(outerHexPoints[HexPosition.TopRight]);
                    points.Add(innerHexPoints[HexPosition.TopRight]);
                    points.Add(innerHexPoints[HexPosition.Right]);
                    points.Add(outerHexPoints[HexPosition.Right]);
                    points.Add(new Point(innerHexPoints[HexPosition.BottomLeft].X + delta.X,
                                        innerHexPoints[HexPosition.BottomRight].Y + delta.Y));
                    points.Add(new Point(innerHexPoints[HexPosition.Left].X + delta.X, innerHexPoints[HexPosition.Left].Y + delta.Y));
                    break;
                case HexSide.BottomRight:
                    delta = GapBetweenTiles(tileKey, Direction.SouthEast, layout);
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
                    delta = GapBetweenTiles(tileKey, Direction.SouthWest, layout);
                    points.Add(innerHexPoints[HexPosition.BottomLeft]);
                    points.Add(outerHexPoints[HexPosition.BottomLeft]);
                   

                    points.Add(new Point(innerHexPoints[HexPosition.Right].X + delta.X,
                                        innerHexPoints[HexPosition.Right].Y + delta.Y));

                    points.Add(new Point(innerHexPoints[HexPosition.TopRight].X + delta.X,
                                         innerHexPoints[HexPosition.TopRight].Y + delta.Y));

                    points.Add(outerHexPoints[HexPosition.Left]);
                    points.Add(innerHexPoints[HexPosition.Left]);
                    break;
                case HexSide.TopLeft:
                    
                    delta = GapBetweenTiles(tileKey, Direction.NorthWest, layout);
                    points.Add(innerHexPoints[HexPosition.TopLeft]);
                    points.Add(outerHexPoints[HexPosition.TopLeft]);
                    points.Add(new Point(innerHexPoints[HexPosition.Right].X + delta.X, innerHexPoints[HexPosition.Right].Y + delta.Y));
                    points.Add(new Point(innerHexPoints[HexPosition.BottomRight].X + delta.X, innerHexPoints[HexPosition.BottomRight].Y + delta.Y));
                    points.Add(outerHexPoints[HexPosition.Left]);
                    points.Add(innerHexPoints[HexPosition.Left]);

                    break;
                default:
                    break;


            }
            return points;
        }
        private static Point GapBetweenTiles(HexCoordinates key, Direction direction, BoardLayout layout)
        {
            var adjacentKey  = key.GetAdjacentTile(direction);
            double  xGap = layout.Left(adjacentKey) - layout.Left(key);
            double  yGap = layout.Top(adjacentKey) - layout.Top(key);
            return new Point(xGap, yGap);
        }

      
    }
}
