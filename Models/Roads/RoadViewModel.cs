using System;
using System.ComponentModel;
using System.Diagnostics;
using Microsoft.UI.Xaml.Media;
using Windows.Foundation;
namespace Catan3.Models
{
    public partial class RoadViewModel : INotifyPropertyChanged
    {

        public void Init()
        {
            if (Layout is not null && Layout is RegularBoardLayout rbl)
            {
                rbl.PropertyChanged += Layout_PropertyChanged;

            }


            UpdateLayout();
        }
        private void Layout_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (sender is RegularBoardLayout layout)
            {
                Layout = layout;
                UpdateLayout();
            }
        }
        private void UpdateLayout()
        {
            Top = GetTop();
            Left = GetLeft();
            OnPropertyChanged(nameof(BorderPoints));
        }
        public double Angle
        {
            get
            {
                double angle = 0.0;
                switch (Road.RoadKey.RoadPosition)
                {
                    case RoadPosition.Top:
                    case RoadPosition.Bottom:
                        angle = 0;
                        break;
                    case RoadPosition.TopRight:
                        angle = -120.0;
                        break;
                    case RoadPosition.BottomRight:
                        angle = 120.0;
                        break;
                    case RoadPosition.BottomLeft:
                        angle = -120;
                        break;
                    case RoadPosition.TopLeft:
                        angle = 120;
                        break;
                    default:
                        Debug.Assert(false, "Bad RoadKey");
                        break;
                }
                return angle;
            }
        }
        public Point TransformOrigin
        {
            get
            {
                switch (Road.RoadKey.RoadPosition)
                {
                    case RoadPosition.Top:
                    case RoadPosition.Bottom:
                        return new Point(0, 0);
                    case RoadPosition.TopRight:
                        return new Point(1.0, 0.5);
                    case RoadPosition.BottomRight:
                        return new Point(1.0, 0.5);
                    case RoadPosition.BottomLeft:
                        return new Point(0, 0.5);
                    case RoadPosition.TopLeft:
                        return new Point(0, 0.5);
                    default:
                        Debug.Assert(false, "Bad RoadKey");
                        break;
                }
                return new Point(0, 0);
            }
        }
        private double GetLeft()
        {
            if (Layout is null) return 0.0;

            var left = Layout.Left(Road.RoadKey.TileKey);
            var buildingPoints =  Layout.BuildingHexPoints;
            left += buildingPoints[( int )BuildingPosition.TopLeft].X;
            return left;
        }
        private double GetTop()
        {
            if (Layout is null) return 0.0;
            var top = Layout.Top(Road.RoadKey.TileKey);

            switch (Road.RoadKey.RoadPosition)
            {
                case RoadPosition.Top:
                case RoadPosition.TopLeft:
                case RoadPosition.TopRight:
                    top -= ( Layout.HexStrokeThickness + Layout.TileGap ) / 2.0;
                    break;
                case RoadPosition.BottomRight:
                case RoadPosition.BottomLeft:
                case RoadPosition.Bottom:
                    top += Layout.ControlHeight - ( Layout.HexStrokeThickness  + Layout.TileGap );
                    break;
                default:
                    break;
            }
            return top;
        }
        /// <summary>
        ///                  / 1 --------_----------- 2 \
        ///                 /                            \
        ///                0                              3
        ///                 \                            /
        ///                  \ 5 ------------------- 4  /
        public PointCollection BorderPoints
        {
            get
            {
                PointCollection points = [];
                if (Layout is null) return points;
                var tilePoints = Layout.ListToDictionary(Layout.TileHexPoints);
                double height = Layout.TileGap + Layout.HexStrokeThickness * 2 ;
                var width = tilePoints[BuildingPosition.TopRight].X - tilePoints[BuildingPosition.TopLeft].X + 4;
                double triangleHeight = height / 2.0 * Math.Cos(Math.PI / 3.0) ;
                points.Add(new Point(0, height * 0.5)); // 0
                points.Add(new Point(triangleHeight, 0)); //1
                points.Add(new Point(width, 0)); //2
                points.Add(new Point(width + triangleHeight, height * 0.5)); //3
                points.Add(new Point(width, height)); //4
                points.Add(new Point(triangleHeight, height));  //5
                return points;
            }
        }
    }
}
