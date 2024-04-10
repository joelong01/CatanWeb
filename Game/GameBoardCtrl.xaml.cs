using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using Catan3.Models;
using Catan3.Utility;
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
            if (newValue is null) return; // happens when we force binding in Shuffle
            this.TraceMessage($"GameViewModel changed first Tile={newValue.Tiles[0]}");
            if (oldValue is not null)
            {
                oldValue.PropertyChanged -= GameViewModel_PropertyChanged;
                if (oldValue.BoardInfo is not null)
                {
                    oldValue.BoardInfo.Layout.PropertyChanged -= Layout_PropertyChanged;
                }
                if (oldValue.Robber is not null)
                {
                    oldValue.Robber.PropertyChanged -= RobberModel_PropertyChanged;
                    DesertTile(oldValue).PropertyChanged -= DesertTile_PropertyChanged;
                }
            }
            if (newValue is not null)
            {
                newValue.PropertyChanged += GameViewModel_PropertyChanged;
                if (newValue.BoardInfo is not null)
                {
                    newValue.BoardInfo.Layout.PropertyChanged += Layout_PropertyChanged;
                }
                if (newValue.Robber is not null)
                {
                    newValue.Robber.PropertyChanged += RobberModel_PropertyChanged;
                }
                if (newValue.Tiles is not null)
                {
                    var desert =  DesertTile(newValue);
                    desert.PropertyChanged += DesertTile_PropertyChanged;
                    ShowRobber(desert.Orientation);
                }
            }
            UpdateRobberTileLocation();

            //
            // WinUI3 does send property changed notifications when the collection changes, only when the contents of the collection change
            // this will force rebind to the new collections
            IC_Roads.ItemsSource = newValue?.Roads;
            IC_Buildings.ItemsSource = newValue?.Buildings;
            IC_Tiles.ItemsSource = newValue?.Tiles;
            IC_Harbors.ItemsSource = newValue?.Harbors;



            //
            //  we use Binding in some places where it is convinient and x:Bind in others. Binding needs data context, so set it here
            this.DataContext = newValue;
        }

        private static TileViewModel DesertTile(GameViewModel gameViewModel)
        {
            if (gameViewModel.Tiles.Count == 0) return TileViewModel.Default;
            return gameViewModel.Tiles.Where(t => t.Tile.ResourceTileType == ResourceTileType.Desert).ToList().First();
        }


        private void DesertTile_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == "Orientation" && GameViewModel is not null)
            {
                ShowRobber(DesertTile(GameViewModel).Orientation);
            }
        }

        private void ShowRobber(CatanOrientation orientation)
        {
            double newValue;
            if (orientation == CatanOrientation.FaceUp)
            {
                newValue = 1.0;

            }
            else
            {
                newValue = 0.0;
            }
            SB_AnimateOpacity.SkipToFill();
            DA_AnimateOpacity.From = VB_Robber.Opacity; // Current opacity as starting point
            DA_AnimateOpacity.To = newValue; // The target opacity
            SB_AnimateOpacity.Begin();
        }

        private void RobberModel_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(GameViewModel.Robber.RobberModel))
            {

                UpdateRobberTileLocation();
            }
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



        }



        private void UpdateRobberTileLocation()
        {

            if (this.Resources["MoveRobberAnimation"] is Storyboard storyboard && GameViewModel is not null)
            {
                Debug.Assert(GameViewModel.Robber.RobberModel is not null);
                // this.TraceMessage($"Moving Robber to {GameViewModel.Robber.RobberModel.Coordinates}");

                // Assuming the first two children are the X and Y animations
                if (storyboard.Children[0] is DoubleAnimation animationX && storyboard.Children[1] is DoubleAnimation animationY)
                {
                    if (VB_Robber.ActualWidth > 0 && VB_Robber.ActualHeight > 0 && GameViewModel.BoardInfo is not null && GameViewModel.Robber.RobberModel is not null)
                    {
                        double x = GameViewModel.BoardInfo.Layout.Left(GameViewModel.Robber.RobberModel.Coordinates) + GameViewModel.BoardInfo.Layout.OuterHexSize - VB_Robber.ActualWidth / 2.0;
                        double y =  GameViewModel.BoardInfo.Layout.Top(GameViewModel.Robber.RobberModel.Coordinates) + GameViewModel.BoardInfo.Layout.OuterHexSize - VB_Robber.ActualHeight / 2.0;
                        animationX.To = x;
                        animationY.To = y;
                        storyboard.Begin();
                    }
                }
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
            var result =  hexSize * .8;
            if (result < 10) return 10;

            return result;
        }
    }
}
