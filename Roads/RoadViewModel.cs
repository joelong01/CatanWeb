using System;
using System.ComponentModel;
using System.Diagnostics;
using Catan3.Utility;
using Microsoft.UI.Composition.Interactions;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
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
            var top = Layout.Top(Road.RoadKey.TileKey)  ;
            
            
            return top;
        }
        public double VisualRoadHeight
        {
            get
            {
                double height = 2 * Layout.TileGap;
                height += Layout.InnerHexStrokeThickness * 2;
                height += Layout.RoadStrokeThickness ;
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
                return this.Road.RoadKey.RoadPosition switch
                {

                    RoadPosition.BottomRight => s + "BR",
                    RoadPosition.BottomLeft => s + "BL",
                    RoadPosition.TopLeft => s + "TL",
                    RoadPosition.TopRight => s + "TR",
                    RoadPosition.None => s + "None",
                    RoadPosition.Top => s + "T",
                    RoadPosition.Bottom => s + "B",
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
        public PointCollection RoadPolygon
        {
            get
            {
                PointCollection points = [];
                if (Layout is null) return points;
                var polygonHeight = VisualRoadHeight - Layout.RoadStrokeThickness  ; 
                var polygonWidth = RoadWidth - Layout.RoadStrokeThickness ;
                var halfStroke = Layout.RoadStrokeThickness * 0.5;
                double pointOneFiveX = polygonHeight / 2.0 * Math.Sqrt(3) / 4.0 ;
                points.Add(new Point(0, polygonHeight * 0.5)); // 0
                points.Add(new Point(pointOneFiveX, 0)); //1
                points.Add(new Point(polygonWidth - pointOneFiveX, 0)); //2
                points.Add(new Point(polygonWidth, polygonHeight * 0.5 )); //3
                points.Add(new Point(polygonWidth - pointOneFiveX, polygonHeight)); //4
                points.Add(new Point(pointOneFiveX, polygonHeight));  //5

                return points;
            }
        }
    }
}
