using System;
using System.Diagnostics;
using System.Diagnostics.Contracts;
using System.Runtime.CompilerServices;
using Catan3.Models;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Windows.Foundation;
using Windows.Security.Cryptography.Certificates;
namespace Catan3.Controls
{
    public delegate void RoadClicked(RoadViewModel viewModel);
    public delegate void RoadMouseEnter(RoadViewModel viewModel);
    public delegate void RoadMouseLeave(RoadViewModel viewModel);
    public partial class RoadCtrl : UserControl
    {
        public event RoadClicked? RoadClicked;
        public RoadCtrl()
        {
            InitializeComponent();
        }
        public static readonly DependencyProperty ViewModelProperty = DependencyProperty.Register("ViewModel", typeof(RoadViewModel), typeof(RoadCtrl), new PropertyMetadata(new RoadKey(TileKey.Default, RoadPosition.None), ViewModelChanged));
        public RoadViewModel ViewModel
        {
            get => ( RoadViewModel )GetValue(ViewModelProperty);
            set => SetValue(ViewModelProperty, value);
        }
        private static void ViewModelChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var depPropClass = d as RoadCtrl;
            var depPropValue = (RoadViewModel)e.NewValue;
            depPropClass?.SetViewModel(depPropValue);
        }
        private void SetViewModel(RoadViewModel value)
        {
            DataContext = value;
        }
      

        private void Grid_PointerPressed(object sender, Microsoft.UI.Xaml.Input.PointerRoutedEventArgs e)
        {
            RoadClicked?.Invoke(this.ViewModel);
        }

        //private PointCollection RoadPolygonPoints(double hexSize, double tileGap, double hexStrokeThickness)
        //{
        //    PointCollection points = [];
        //    if (ViewModel.Layout is null) return points;

        //  //   var layout = ViewModel.Layout;
        //  //  if (ViewModel.Road.RoadKey.TileKey == new TileKey(-1, -1, 2)) this.TraceMessage($"width={width}");
          
        //    points.Add(new Point(0, ViewModel.RoadHeight / 2.0)); // 0
        //    points.Add(new Point(ViewModel.WidthToPoint, 0)); //1
        //    points.Add(new Point(ViewModel.RoadWidth - 2* ViewModel.WidthToPoint, 0)); //2
        //    points.Add(new Point(ViewModel.RoadWidth, ViewModel.RoadHeight * 0.5)); //3
        //    points.Add(new Point(ViewModel.RoadWidth - 2 * ViewModel.WidthToPoint, ViewModel.RoadHeight)); //4
        //    points.Add(new Point(ViewModel.WidthToPoint, ViewModel.RoadHeight));  //5
        //    return points;
        //}

        private string RoadStateString(RoadModel road)
        {
            if (road is null) return "";
            
            return road.RoadKey.RoadPosition.ToString();
        }
        private double RotateTextAngle(double angle)
        { 
            return -angle;
        }
        private double FinalTranslateX(bool updated)
        {
            // Calculate outward movement based on hexagon radius and road height
            double translateX = (ViewModel.Layout.HexSize - ViewModel.Layout.TileGap - ViewModel.Layout.HexStrokeThickness) * Math.Sqrt(3)  / 2.0; 
        
            switch (ViewModel.Road.RoadKey.RoadPosition)
            {
                case RoadPosition.None:
                    break;
                case RoadPosition.Top:
                    return 0.0;
                case RoadPosition.TopRight:
                    return translateX ;
                case RoadPosition.BottomRight:
                    // Similar calculation to TopRight, but with different sign for adjustment
                    return translateX ;
                case RoadPosition.Bottom:
                    return 0.0;
                case RoadPosition.BottomLeft:
                    // Opposite sign for outward and adjustment
                    return -translateX ;
                case RoadPosition.TopLeft:
                    // Opposite sign for outward and adjustment
                    return -translateX;
                default:
                    Debug.Assert(false, "Bad RoadKey");
                    break;
            }
            return 0.0;
        }

        private double FinalTranslateY(bool updated)
        {
            // Calculate up movement based on half hexagon height
            double up = ViewModel.Layout.HexSize * Math.Sqrt(3) / 4.0;
          
            switch (ViewModel.Road.RoadKey.RoadPosition)
            {
                case RoadPosition.None:
                    break;
                case RoadPosition.Top:
                    return -2 * up;
                case RoadPosition.TopRight:
                 
                    return -up ;
                case RoadPosition.BottomRight:
                    // Add up movement for the y-axis, subtract adjustment
                    return up ;
                case RoadPosition.Bottom:
                    return 2*up ;
                case RoadPosition.BottomLeft:
                    // Add up movement for the y-axis, add adjustment
                    return up ;
                case RoadPosition.TopLeft:
                    // Subtract up movement for the y-axis, add adjustment
                    return -up ;
                default:
                    Debug.Assert(false, "Bad RoadKey");
                    break;
            }
            return 0.0;
        }

    }
}
