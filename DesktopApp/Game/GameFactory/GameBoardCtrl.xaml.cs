using System;
using System.ComponentModel;
using System.Diagnostics;
using Catan3.Models;
using Catan3.Shared.Utility;
using Catan3.Shared.Models; // HighlightTile & SwapTileResources
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Animation;
using CommunityToolkit.Mvvm.Messaging;
using Windows.Foundation;
namespace Catan3.Controls
{
    public delegate void TileRightMouseClicked(TileCtrl tileCtrl, RightTappedRoutedEventArgs e);
    public delegate void BuildingClicked(BuildingViewModel viewModel);
    public delegate void BuildingMouseEnter(BuildingViewModel viewModel);
    public delegate void BuildingMouseLeave(BuildingViewModel viewModel);

    public partial class GameBoardCtrl : UserControl
    {
        public event TileRightMouseClicked? TileRightMouseClicked;
        public GameBoardCtrl()
        {
            InitializeComponent();
            VB_Robber.Loaded += VB_Robber_Loaded; // Pointer events wired in XAML only
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
          //  this.TraceMessage($"GameViewModel changed first Tile={newValue.Tiles[0]}");
            if (oldValue is not null)
            {
                oldValue.PropertyChanged -= GameViewModel_PropertyChanged;
                if (oldValue.BoardInfo is not null)
                {
                    oldValue.BoardInfo.Layout.PropertyChanged -= Layout_PropertyChanged;
                }
                if (oldValue.RobberViewModel is not null)
                {
                    oldValue.RobberViewModel.RobberModel.PropertyChanged -= RobberModel_PropertyChanged;
                    oldValue.RobberViewModel.PropertyChanged -= RobberViewModel_PropertyChanged;
                   
                }
            }
            if (newValue is not null)
            {
                newValue.PropertyChanged += GameViewModel_PropertyChanged;
                if (newValue.BoardInfo is not null)
                {
                    newValue.BoardInfo.Layout.PropertyChanged += Layout_PropertyChanged;
                }
                if (newValue.RobberViewModel is not null)
                {
                    newValue.RobberViewModel.RobberModel.PropertyChanged += RobberModel_PropertyChanged;
                    newValue.RobberViewModel.PropertyChanged += RobberViewModel_PropertyChanged;
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
            //  we use Binding in some places where it is convenient and x:Bind in others. Binding needs data context, so set it here
            this.DataContext = newValue;
        }
    
        private void RobberModel_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
           
            if (e.PropertyName == nameof(GameViewModel.RobberViewModel.RobberModel.Coordinates) )
            {
                UpdateRobberTileLocation();
            }
        }
        private void RobberViewModel_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (GameViewModel is null) return;
         //   this.TraceMessage($"robber changed: {e.PropertyName}");
            if (e.PropertyName == nameof(GameViewModel.RobberViewModel.RobberModel))
            {
                GameViewModel.RobberViewModel.RobberModel.PropertyChanged += RobberModel_PropertyChanged;
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
                Debug.Assert(GameViewModel.RobberViewModel.RobberModel is not null);
             //   this.TraceMessage($"Moving Robber to {GameViewModel.RobberViewModel.RobberModel.Coordinates}");
                // Assuming the first two children are the X and Y animations
                if (storyboard.Children[0] is DoubleAnimation animationX && storyboard.Children[1] is DoubleAnimation animationY)
                {
                    if (VB_Robber.ActualWidth > 0 && VB_Robber.ActualHeight > 0 && GameViewModel.BoardInfo is not null && GameViewModel.RobberViewModel.RobberModel is not null)
                    {
                        double x = GameViewModel.BoardInfo.Layout.Left(GameViewModel.RobberViewModel.RobberModel.Coordinates) + GameViewModel.BoardInfo.Layout.OuterHexSize - VB_Robber.ActualWidth / 2.0;
                        double y =  GameViewModel.BoardInfo.Layout.Top(GameViewModel.RobberViewModel.RobberModel.Coordinates) + GameViewModel.BoardInfo.Layout.OuterHexSize - VB_Robber.ActualHeight / 2.0;
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

        private HexCoordinates? _lastHighlighted;
        private bool _isDragging = false;
        private Windows.Foundation.Point _dragStartPoint;
        private HexCoordinates? _dragSourceHex;
        private ResourceType _dragSourceResource;
        private const double DragThreshold = 10.0;

        private bool CanStartDrag()
        {
            var state = GameViewModel?.GameModel?.GameState;
            return state == Shared.Models.GameState.PickingBoard; // only allow during PickingBoard
        }

        private void TraceDrag(string msg)
        {
            this.TraceMessage(msg);
        }

        private void OnBoardPointerMoved(object sender, PointerRoutedEventArgs e)
        {
            if (GameViewModel?.Tiles == null || GameViewModel.BoardInfo?.Layout == null) return;
            if (!CanStartDrag()) return; // only do logic in PickingBoard phase
            var pt = e.GetCurrentPoint(BOARD_RootCanvas).Position;
            var hit = HitTestBoard(pt);

            // No drag in progress - nothing to do
            if (_dragSourceHex is null)
            {
                return;
            }

            // Drag in progress (or threshold not yet crossed)
            if (!_isDragging)
            {
                double dx = pt.X - _dragStartPoint.X;
                double dy = pt.Y - _dragStartPoint.Y;
                double dist = Math.Sqrt(dx * dx + dy * dy);
                if (dist < DragThreshold) return; // still below threshold
                _isDragging = true;
                TraceDrag($"DragStartThresholdReached: Source={_dragSourceHex} Resource={_dragSourceResource} Threshold={DragThreshold} Dist={dist:F1}");
                ShowDragVisual(_dragSourceResource, pt);
            }

            // Update drag visual position
            UpdateDragVisualPosition(pt);

            if (!Equals(hit, _lastHighlighted))
            {
                // leaving previous highlight
                if (_lastHighlighted is not null && !_lastHighlighted.Equals(hit))
                {
                    TraceDrag($"DragLeave: {_lastHighlighted}");
                }
                _lastHighlighted = hit;
                if (hit is not null && !_dragSourceHex!.Equals(hit))
                {
                    WeakReferenceMessenger.Default.Send(new HighlightTile(hit));
                    TraceDrag($"DragHover: {hit}");
                }
                else
                {
                    WeakReferenceMessenger.Default.Send(new HighlightTile(null));
                }
            }
        }

        private void BOARD_RootCanvas_PointerPressed(object sender, PointerRoutedEventArgs e)
        {
            if (!CanStartDrag()) return;
            var pt = e.GetCurrentPoint(BOARD_RootCanvas).Position;
            var hit = HitTestBoard(pt);
            if (hit is null)
            {
                TraceDrag("PointerPressed: No tile under cursor");
                return;
            }
            var sourceTileVM = GameViewModel!.Tiles.TileFromCoords(hit);
            if (sourceTileVM is null)
            {
                TraceDrag($"PointerPressed: Tile VM not found for {hit}");
                return;
            }
            _dragSourceHex = hit;
            _dragSourceResource = sourceTileVM.Tile.ResourceTileType;
            _dragStartPoint = pt;
            _isDragging = false;
            BOARD_RootCanvas.CapturePointer(e.Pointer);
            TraceDrag($"PointerPressed: Source={_dragSourceHex} Resource={_dragSourceResource}");
        }

        private void BOARD_RootCanvas_PointerReleased(object sender, PointerRoutedEventArgs e)
        {
            if (!CanStartDrag()) { BOARD_RootCanvas.ReleasePointerCapture(e.Pointer); return; }
            var pt = e.GetCurrentPoint(BOARD_RootCanvas).Position;
            var destHex = HitTestBoard(pt);
            WeakReferenceMessenger.Default.Send(new HighlightTile(null));

            if (_dragSourceHex is null)
            {
                TraceDrag("PointerReleased: No drag source set");
            }
            else if (!_isDragging)
            {
                TraceDrag($"PointerReleased: Drag threshold not met (treated as click) Source={_dragSourceHex}");
            }
            else if (destHex is null)
            {
                TraceDrag($"PointerReleased: No destination tile under cursor Source={_dragSourceHex}");
            }
            else if (_dragSourceHex.Equals(destHex))
            {
                TraceDrag($"PointerReleased: Destination equals source; no swap Source={_dragSourceHex}");
            }
            else
            {
                var sourceTile = GameViewModel!.Tiles.TileFromCoords(_dragSourceHex);
                var destTile = GameViewModel!.Tiles.TileFromCoords(destHex);
                if (sourceTile is not null && destTile is not null)
                {
                    TraceDrag($"SwapAttempt: {sourceTile.Tile.TileKey}({sourceTile.Tile.ResourceTileType}) -> {destTile.Tile.TileKey}({destTile.Tile.ResourceTileType})");
                    WeakReferenceMessenger.Default.Send(new SwapTileResources(
                        _dragSourceHex!,
                        destHex!,
                        sourceTile.Tile.ResourceTileType,
                        destTile.Tile.ResourceTileType));
                }
                else
                {
                    TraceDrag($"SwapAborted: Missing VM. SourceVM={(sourceTile!=null)} DestVM={(destTile!=null)}");
                }
            }

            // cleanup
            HideDragVisual();
            _dragSourceHex = null;
            _isDragging = false;
            _lastHighlighted = null;
            BOARD_RootCanvas.ReleasePointerCapture(e.Pointer);
        }

        private void BOARD_RootCanvas_PointerCanceled(object sender, PointerRoutedEventArgs e)
        {
            TraceDrag("PointerCanceled: Drag aborted");
            WeakReferenceMessenger.Default.Send(new HighlightTile(null));
            HideDragVisual();
            _dragSourceHex = null;
            _isDragging = false;
            _lastHighlighted = null;
            BOARD_RootCanvas.ReleasePointerCapture(e.Pointer);
        }

        private void ShowDragVisual(ResourceType resourceType, Windows.Foundation.Point position)
        {
            // Get the resource image brush
            string key = $"ResourceCard.{resourceType}";
            if (Application.Current.Resources.TryGetValue(key, out var resource) && resource is ImageBrush brush)
            {
                DragVisualImage.Fill = brush;
            }
            else
            {
                // Fallback to a solid color if image not found
                DragVisualImage.Fill = new SolidColorBrush(Microsoft.UI.Colors.Gray);
            }

            DragVisual.Visibility = Visibility.Visible;
            UpdateDragVisualPosition(position);
        }

        private void UpdateDragVisualPosition(Windows.Foundation.Point position)
        {
            // Offset slightly so cursor isn't hidden by the visual
            Canvas.SetLeft(DragVisual, position.X + 10);
            Canvas.SetTop(DragVisual, position.Y + 10);
        }

        private void HideDragVisual()
        {
            DragVisual.Visibility = Visibility.Collapsed;
        }
        private HexCoordinates? HitTestBoard(Windows.Foundation.Point boardPoint)
        {
            // Quick bounds check
            if (boardPoint.X < 0 || boardPoint.Y < 0 ||
                boardPoint.X > BOARD_RootCanvas.ActualWidth ||
                boardPoint.Y > BOARD_RootCanvas.ActualHeight)
                return null;

            var layout = GameViewModel?.BoardInfo?.Layout;
            if (layout == null || GameViewModel?.Tiles == null) return null;

            double size = layout.OuterHexSize;

            // Calculate the pixel position of hex (0,0,0) center
            // TileXOffset/TileYOffset are the top-left corner of the bounding box,
            // so the center is offset by (size, size*sqrt(3)/2) from that corner
            double centerX = layout.TileXOffset + size;
            double centerY = layout.TileYOffset + size * Math.Sqrt(3) / 2;

            // Use HexCoordinates.FromPixel for the conversion
            var coords = HexCoordinates.FromPixel(boardPoint.X, boardPoint.Y, size, centerX, centerY);

            // Verify tile exists in game model
            var tile = GameViewModel.Tiles.TileFromCoords(coords);
            return tile?.Tile.TileKey;
        }
    }
}
