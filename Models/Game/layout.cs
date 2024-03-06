using System;
using System.Collections.Generic;
using System.ComponentModel;
using Microsoft.UI.Xaml.Media;
using PropertyChanged;
using Windows.Foundation;
namespace Catan3.Models
{
    /// <summary>
    ///     Data that defines how layout works. 
    /// </summary>
    public partial class RegularBoardLayout : IBoardLayout, INotifyPropertyChanged
    {
        public static RegularBoardLayout Default { get; } = new RegularBoardLayout();
        public double HexSize { get; set; } = 100.0;
        public double HexStrokeThickness { get; set; } = 8.0;
        public double TileGap { get; set; } = 10.0; // this is *inside* the tile
        public double ColumnOffset { get; set; } = 1.5;
        public int ColumnCount { get; set; } = 5;
        public double GameMargin { get; set; } = 0.0;
        [DependsOn(nameof(HexSize))]
        public double ControlWidth => HexSize * 2.0;
        [DependsOn(nameof(HexSize))]
        public double ControlHeight => HexSize * Math.Sqrt(3);
        public double BuildingSize { get; set; } = 40;
        [DependsOn(nameof(BuildingSize), nameof(ColumnCount), nameof(GameMargin), nameof(HexSize))]
        public double BoardWidth => HexSize * 7 + BuildingSize + GameMargin;
        [DependsOn(nameof(BuildingSize), nameof(ColumnCount), nameof(GameMargin), nameof(HexSize))]
        public double BoardHeight => HexSize * Math.Sqrt(3) * ColumnCount + BuildingSize + GameMargin;
        public double RoadStrokeThickness => 2.0;
        public double Top(TileKey key)
        {
            var top =  ( .5 * key.Q +  key.R)* HexSize * Math.Sqrt(3) ;
            top += 2 * HexSize * Math.Sqrt(3);
            top = Math.Round(top + BuildingSize * .5, 1); // the buildings will go on top of the highest tile, give them room
            top += HexStrokeThickness + GameMargin;
            return top;
        }
        public double Left(TileKey key)
        {
            var left = 2 * HexSize * .75 * key.Q ;
            left += ColumnOffset * 2 * HexSize;
            left += ( BuildingSize * 0.5 );
            left += HexStrokeThickness * 0.5;
            left += GameMargin;
            return left;
        }
        public PointCollection CalculateHexGeometry(double size, double top, double left)
        {
            double height = Math.Sqrt(3) * size;
            double width = size * 2;
            double middle = height / 2.0  ;
            double onequarter =  width * 0.25 ;
            double threequarter =  width * 0.75 ;
            // Create a new PointCollection with adjusted points
            var points = new PointCollection
                {
                    new Point(left + onequarter, top), // TopLeft
                    new Point(left + threequarter, top), // TopRight
                    new Point(left + width, middle + top), //  Right
                    new Point(left + threequarter, height + top ), // BottomRight
                    new Point(left + onequarter, height + top), // BottomLeft 
                    new Point(left,  middle + top), //  Left
                };
            return points;
        }
        [DependsOn(nameof(HexSize), nameof(TileGap), nameof(HexStrokeThickness))]
        public PointCollection TileHexPoints
        {
            get
            {
                double innerSize = (HexSize * Math.Sqrt(3) - (TileGap + HexStrokeThickness)) / Math.Sqrt(3) ;
                return CalculateHexGeometry(innerSize, ( HexStrokeThickness + TileGap ) * .5, ( HexStrokeThickness + TileGap ) * .5);
            }
        }
        public double TileHeight => ( HexSize * Math.Sqrt(3) - ( TileGap + HexStrokeThickness ) ) / Math.Sqrt(3);
        [DependsOn(nameof(HexSize), nameof(TileGap), nameof(HexStrokeThickness))]
        public PointCollection BuildingHexPoints
        {
            get
            {
                return CalculateHexGeometry(HexSize, 0, 0);
            }
        }
        public Dictionary<BuildingPosition, Point> ListToDictionary(PointCollection points)
        {
            var dict = new Dictionary<BuildingPosition, Point>
            {
                [BuildingPosition.TopLeft] = points[0],
                [BuildingPosition.TopRight] = points[1],
                [BuildingPosition.Right] = points[2],
                [BuildingPosition.BottomRight] = points[3],
                [BuildingPosition.BottomLeft] = points[4],
                [BuildingPosition.Left] = points[5],
            };
            return dict;
        }
    }
}
