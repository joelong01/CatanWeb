using System.ComponentModel;
using Catan3.Models;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media.Animation;
namespace Catan3.Controls
{
    public delegate void BuildingClicked(BuildingViewModel viewModel);
    public delegate void BuildingMouseEnter(BuildingViewModel viewModel);
    public delegate void BuildingMouseLeave(BuildingViewModel viewModel);
    public delegate void TileRightMouseClicked(TileCtrl tileCtrl, RightTappedRoutedEventArgs e);
    /// <summary>
    /// Interaction logic for GameBoardCtrl.xaml
    /// </summary>
    public partial class GameBoardCtrl : UserControl
    {
        public event BuildingClicked? BuildingClicked;
        public event BuildingMouseEnter? BuildingMouseEnter;
        public event BuildingMouseLeave? BuildingMouseLeave;
        public event RoadClicked? RoadClicked;
        public event RoadMouseEnter? RoadMouseEnter;
        public event RoadMouseLeave? RoadMouseLeave;
        public event TileRightMouseClicked? TileRightMouseClicked;


        public GameBoardCtrl()
        {
            InitializeComponent();
            VB_Robber.Loaded += VB_Robber_Loaded;


        }

        private void VB_Robber_Loaded(object sender, RoutedEventArgs e)
        {
            UpdateRobberTileLocation();
            VB_Robber.Loaded -= VB_Robber_Loaded;
        }

        public static readonly DependencyProperty GameViewModelProperty = DependencyProperty.Register("GameViewModel", typeof(GameViewModel), typeof(GameBoardCtrl), new PropertyMetadata(null, GameViewModelChanged));
        public GameViewModel? GameViewModel
        {
            get => ( GameViewModel )GetValue(GameViewModelProperty);
            set => SetValue(GameViewModelProperty, value);
        }
        private static void GameViewModelChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var depPropClass = d as GameBoardCtrl;


            depPropClass?.SetGameViewModel(( GameViewModel )e.OldValue, ( GameViewModel )e.NewValue);
        }
        private void SetGameViewModel(GameViewModel? oldValue, GameViewModel? newValue)
        {
            if (oldValue is not null)
            {
                oldValue.PropertyChanged -= GameViewModel_PropertyChanged;
                if (oldValue.BoardInfo is not null)
                {
                    oldValue.BoardInfo.Layout.PropertyChanged -= Layout_PropertyChanged;
                }
            }
            if (newValue is not null)
            {
                newValue.PropertyChanged += GameViewModel_PropertyChanged;
                if (newValue.BoardInfo is not null)
                {
                    newValue.BoardInfo.Layout.PropertyChanged += Layout_PropertyChanged;
                }
            }
            UpdateRobberTileLocation();
            //
            // WinUI3 does send property changed notifications when the collection changes, only when the contents of the collection change
            // this will force rebind to the new collections
            IC_Roads.ItemsSource = newValue?.Roads;
            IC_Buildings.ItemsSource = newValue?.Buildings;
            IC_Tiles.ItemsSource = newValue?.Tiles;

            //
            //  we use Binding in some places where it is convinient and x:Bind in others. Binding needs data context, so set it here
            this.DataContext = newValue;
        }
        /// <summary>
        ///     When the geometry of the board changes, we have to update the location of the Robber
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void Layout_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            UpdateRobberTileLocation();
        }

        private void GameViewModel_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {

            if (e.PropertyName == nameof(GameViewModel.RobberTile))
            {

                UpdateRobberTileLocation();
            }

        }



        private void UpdateRobberTileLocation()
        {
            if (this.Resources["MoveRobberAnimation"] is Storyboard storyboard && GameViewModel is not null)
            {
                // Assuming the first two children are the X and Y animations
                if (storyboard.Children[0] is DoubleAnimation animationX && storyboard.Children[1] is DoubleAnimation animationY)
                {
                    if (VB_Robber.ActualWidth > 0 && VB_Robber.ActualHeight > 0 && GameViewModel.BoardInfo is not null)
                    {
                        double x = GameViewModel.BoardInfo.Layout.Left(GameViewModel.RobberTile) + GameViewModel.BoardInfo.Layout.OuterHexSize - VB_Robber.ActualWidth / 2.0;
                        double y =  GameViewModel.BoardInfo.Layout.Top(GameViewModel.RobberTile) + GameViewModel.BoardInfo.Layout.OuterHexSize - VB_Robber.ActualHeight / 2.0;
                        animationX.To = x;
                        animationY.To = y;
                        storyboard.Begin();
                    }
                }
            }
        }





        private void Building_MouseEnter(object sender, PointerRoutedEventArgs e)
        {
            if (sender is BuildingCtrl building)
            {
                BuildingMouseEnter?.Invoke(building.BuildingViewModel);
            }
        }
        private void Building_MouseLeave(object sender, PointerRoutedEventArgs e)
        {
            if (sender is BuildingCtrl building)
            {
                BuildingMouseLeave?.Invoke(building.BuildingViewModel);
            }
        }

        private void Road_MouseEnter(object sender, PointerRoutedEventArgs e)
        {
            if (sender is RoadCtrl Road)
            {
                RoadMouseEnter?.Invoke(Road.ViewModel);
            }
        }
        private void Road_MouseLeave(object sender, PointerRoutedEventArgs e)
        {
            if (sender is RoadCtrl Road)
            {
                RoadMouseLeave?.Invoke(Road.ViewModel);
            }
        }
        private void Road_Clicked(RoadViewModel viewModel)
        {
            RoadClicked?.Invoke(viewModel);
        }


        private void Building_MousePressed(object sender, PointerRoutedEventArgs e)
        {
            if (sender is BuildingCtrl building)
            {
                BuildingClicked?.Invoke(building.BuildingViewModel);
            }
        }

        private void Tile_RightMouseDown(object sender, RightTappedRoutedEventArgs e)
        {
            if (sender is TileCtrl Tile)
            {
                TileRightMouseClicked?.Invoke(Tile, e);
            }
        }

        private double RobberHeight(double hexSize)
        {
            if (GameViewModel is null || GameViewModel.BoardInfo is null) return 80;
            return 80 * ( GameViewModel.BoardInfo.Layout.InnerHexSize * 2 - 92.5 ) / 92.5;
        }
    }
}
