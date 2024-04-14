using System;
using System.ComponentModel;
using System.Diagnostics;
using Catan3.Models;
using Catan3.Utility;
using CommunityToolkit.Mvvm.Input;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Animation;
using CatanOrientation = Catan3.Models.CatanOrientation;
namespace Catan3.Controls
{
    /// <summary>
    /// Interaction logic for TileCtrl.xaml
    /// </summary>
    public partial class TileCtrl : UserControl
    {
        public TileCtrl()
        {
            this.InitializeComponent();

        }
        public static readonly DependencyProperty TileViewModelProperty = DependencyProperty.Register("TileViewModel", typeof(TileViewModel), typeof(TileCtrl), new PropertyMetadata(null, TileViewModelChanged));
        public TileViewModel TileViewModel
        {
            get => ( TileViewModel )GetValue(TileViewModelProperty);
            set => SetValue(TileViewModelProperty, value);
        }
        private static void TileViewModelChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var depPropClass = d as TileCtrl;
            var depPropValue = (TileViewModel)e.NewValue;
            depPropClass?.SetTileViewModel(depPropValue, ( TileViewModel )e.OldValue);
        }
        private void SetTileViewModel(TileViewModel newModel, TileViewModel? oldModel)
        {
            if (oldModel is not null)
            {
                oldModel.PropertyChanged -= TileViewModel_PropertyChanged;
            }
            this.DataContext = newModel;
            newModel.PropertyChanged += TileViewModel_PropertyChanged;
            if (oldModel is not null && newModel is TileViewModel)
            {
                if (oldModel.Orientation != newModel.Orientation)
                {
                    SetOrientation();
                }
            }

        }

        private void TileViewModel_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == "Orientation") SetOrientation();
        }

        

        private void SetOrientation()
        {
            if (TileViewModel.Orientation == CatanOrientation.FaceUp)
            {
                AnimationHelpers.FlipToFaceUp(C_Back, C_Front);
            }
            else // Assuming the only other state is FaceDown
            {
                AnimationHelpers.FlipToFaceDown(C_Back, C_Front);
            }
        }




        private Visibility PipsVisibility(int tileNumber, int pipIndex)
        {
            Visibility visibility = Visibility.Collapsed;
            switch (tileNumber)
            {
                case 2:
                case 12:
                    if (pipIndex < 1) visibility = Visibility.Visible;
                    break;
                case 3:
                case 11:
                    if (pipIndex < 2) visibility = Visibility.Visible;
                    break;
                case 4:
                case 10:
                    if (pipIndex < 3) visibility = Visibility.Visible;
                    break;
                case 5:
                case 9:
                    if (pipIndex < 4) visibility = Visibility.Visible;
                    break;
                case 6:
                case 8:
                    if (pipIndex < 5) visibility = Visibility.Visible;
                    break;
                case 7:
                    return Visibility.Collapsed;
            }
            return visibility;
        }

        public SolidColorBrush GetPipsForeground(int number)
        {
            if (number == 6 || number == 8)
            {
                return StaticBrushes.RedBrush;
            }
            return StaticBrushes.WhiteBrush;
        }

        /// <summary>
        ///     DataBinding function to scale the CatanNumber in XAML
        ///     The default size of a tile is 100, so as it gets bigger, the scale to the bigger number
        /// </summary>
        /// <param name="size"></param>
        /// <returns></returns>

        private double NumberScale(double size)
        {
            //if (this.TileViewModel.Tile.HexCoordinates == CenterKey)
            //{
            //    this.TraceMessage($"InnerHexSize={size}");
            //}
            return size / 100.0;
        }
        /// <summary>
        ///     DataBinding function to set the top to the Catan Number
        /// </summary>
        /// <param name="tileGap"></param>
        /// <param name="hexStroke"></param>
        /// <returns></returns>

        private double NumberTop(double tileGap, double hexStroke)
        {
            return tileGap + hexStroke + 5; // 5 is an arbitrary number that just "looks good"
        }
        /// <summary>
        ///    the position above the bottom of the control for the Coordinates
        ///    this should place it right above the border
        /// </summary>
        /// <param name="tileGap"></param>
        /// <param name="hexStroke"></param>
        /// <returns></returns>
        private Thickness CooordinateTextMargin(double tileGap, double hexStroke)
        {
            return new Thickness(0, 0, 0, hexStroke + tileGap);
        }

        private void OnRightClicked(object sender, Microsoft.UI.Xaml.Input.RightTappedRoutedEventArgs e)
        {
            this.TileViewModel.TargetCommand.Execute(null);
     //       Debug.Assert(TileViewModel.Targets.Count != 0);
            var flyout = new MenuFlyout();

            foreach (var target in TileViewModel.Targets)
            {
                var menuItem = new MenuFlyoutItem
                {
                    Text = target.Name,
                    Command = TileViewModel.TargetPickedCommand,
                    CommandParameter = target.Id
                };
                flyout.Items.Add(menuItem);
            }

            // Add a separator
            flyout.Items.Add(new MenuFlyoutSeparator());

            // Add a "Cancel" menu item
            var cancelItem = new MenuFlyoutItem
            {
                Text = "Cancel",
                Command = new RelayCommand(() => {})
            };
            flyout.Items.Add(cancelItem);

            flyout.ShowAt(sender as FrameworkElement, new FlyoutShowOptions
            {
                Position = e.GetPosition(sender as UIElement),
                Placement = FlyoutPlacementMode.RightEdgeAlignedTop,
                ShowMode = FlyoutShowMode.Transient
            });

            e.Handled = true;
        }
    }
}
