using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using Catan3.Controls;
using Catan3.Models;
using Catan3.Utility;
using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Windows.UI.ViewManagement;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace Catan3
{
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class MainPage : Page
    {
        public MainPage()
        {
            this.InitializeComponent();
            InitializeComponent();
            AvailablePlayers.Add(new("Dodgy", Colors.White, Colors.Red));
            AvailablePlayers.Add(new("Joe", Colors.White, Colors.Blue));
            AvailablePlayers.Add(new("Doug", Colors.White, Colors.Green));
            AvailablePlayers.Add(new("Chris", Colors.White, Colors.Black));
            AvailablePlayers.Add(new("Adrian", Colors.White, Colors.Purple));
            AvailablePlayers.Add(new("Ryan", Colors.White, Colors.DarkGray));
            Games.Add(CatanGame.Expansion);
            Games.Add(CatanGame.Regular);
            SelectedGame = CatanGame.Expansion;
            NewGame();
            Debug.Assert(GameViewModel != null);
        }
        public static readonly DependencyProperty SelectedGameProperty = DependencyProperty.Register("SelectedGame", typeof(CatanGame), typeof(MainPage), new PropertyMetadata(CatanGame.Regular));
        public CatanGame SelectedGame
        {
            get => ( CatanGame )GetValue(SelectedGameProperty);
            set 
            {
                if (value != SelectedGame)
                {

                    SetValue(SelectedGameProperty, value);
                }
            }
        }
        public static readonly DependencyProperty GameViewModelProperty = DependencyProperty.Register("GameViewModel", typeof(GameViewModel), typeof(MainPage), new PropertyMetadata(null));
        public GameViewModel? GameViewModel
        {
            get => ( GameViewModel? )GetValue(GameViewModelProperty);
            set => SetValue(GameViewModelProperty, value);
        }
        private static List<PlayerViewModel> AvailablePlayers { get; set; } = [];
        public  ObservableCollection<CatanGame> Games { get; set; } = [];
        private void OnRightButtonTapped(object sender, RightTappedRoutedEventArgs e)
        {

        }
        private void OnKeyUp(object sender, KeyRoutedEventArgs e)
        {

        }
        private void NewGame()
        {
            List<PlayerViewModel> playingPlayers = [];
            playingPlayers.Add(AvailablePlayers[0]);
            playingPlayers.Add(AvailablePlayers[1]);
            playingPlayers.Add(AvailablePlayers[2]);
            GameViewModel = new GameViewModel(GameGenerator.CreateGame(SelectedGame), playingPlayers);
            GameViewModel.CurrentPlayer = GameViewModel.Players[0];
            this.DataContext = GameViewModel;
       
        }
        private void OnRegenerate(object sender, RoutedEventArgs e)
        {
            NewGame();
        }
        private void Building_MouseEnter(BuildingViewModel viewModel)
        {
            // this.TraceMessage($"{viewModel.Building.BuildingKey} {Game?.CurrentPlayer.Name}");
            if (GameViewModel?.CurrentPlayer is not null && GameViewModel?.CurrentPlayer.Background is not null && viewModel.Building.Owner is null)
            {
                viewModel.Background = BrushCache.GetGradientBrush(GameViewModel.CurrentPlayer.Background, Colors.Black);
                viewModel.Foreground = BrushCache.GetSolidColorBrush(GameViewModel.CurrentPlayer.Foreground);
                viewModel.Building.BuildingState = BuildingState.Pips;
            }
        }
        private void Building_Clicked(BuildingViewModel viewModel)
        {
            if (GameViewModel?.CurrentPlayer is not null && GameViewModel?.CurrentPlayer.Background is not null && viewModel.Building.Owner is null)
            {
                viewModel.Building.Owner = GameViewModel.CurrentPlayer.Player;
            }
            if (viewModel.Building.Owner is not null)
            {
                switch (viewModel.Building.BuildingState)
                {
                    case BuildingState.Empty:
                        viewModel.Building.BuildingState = BuildingState.Settlement;
                        viewModel.Building.Metropolis = false;
                        break;
                    case BuildingState.Settlement:
                        viewModel.Building.BuildingState = BuildingState.City;
                        break;
                    case BuildingState.City:
                        if (viewModel.Building.Metropolis)
                        {
                            viewModel.Building.Metropolis = false;
                            viewModel.Building.BuildingState = BuildingState.Knight;
                        }
                        else
                        {
                            viewModel.Building.Metropolis = true;
                        }
                        break;
                    case BuildingState.Pips:
                        viewModel.Building.BuildingState = BuildingState.Settlement;
                        break;
                    case BuildingState.Knight:
                        viewModel.Building.BuildingState = BuildingState.Empty;
                        break;
                    default:
                        break;
                }
            }
        }
        private void Building_MouseLeave(BuildingViewModel viewModel)
        {
            if (GameViewModel?.CurrentPlayer is not null && GameViewModel?.CurrentPlayer.Background is not null && viewModel.Building.Owner is null)
            {
                viewModel.Background = BrushCache.GetSolidColorBrush(Colors.Transparent);
                viewModel.Foreground = BrushCache.GetSolidColorBrush(Colors.Transparent);
                viewModel.Building.BuildingState = BuildingState.Empty;
            }
        }
        private void Road_MouseEnter(RoadViewModel viewModel)
        {
            // this.TraceMessage($"{viewModel.Road.RoadKey} {Game?.CurrentPlayer?.Name} {viewModel.Road.RoadState}");
            if (GameViewModel?.CurrentPlayer is not null && GameViewModel?.CurrentPlayer.Background is not null && viewModel.Road.Owner is null)
            {
                viewModel.Background = BrushCache.GetGradientBrush(GameViewModel.CurrentPlayer.Background, Colors.Black);
                viewModel.Foreground = BrushCache.GetSolidColorBrush(GameViewModel.CurrentPlayer.Foreground);
                viewModel.Road.RoadState = RoadState.Road;
            }
        }
        private void Road_MouseLeave(RoadViewModel viewModel)
        {
            //   this.TraceMessage($"{viewModel.Road.RoadKey} {Game?.CurrentPlayer?.Name} {viewModel.Road.RoadState}");
            if (GameViewModel?.CurrentPlayer is not null && GameViewModel?.CurrentPlayer.Background is not null && viewModel.Road.Owner is null)
            {
                viewModel.Background = BrushCache.GetSolidColorBrush(Colors.Transparent);
                viewModel.Foreground = BrushCache.GetSolidColorBrush(Colors.Transparent);
                viewModel.Road.RoadState = RoadState.Unowned;
            }
        }
        private void Road_Clicked(RoadViewModel viewModel)
        {
            //  this.TraceMessage($"{viewModel.Road.RoadKey} {Game?.CurrentPlayer?.Name} {viewModel.Road.RoadState}");
            if (GameViewModel?.CurrentPlayer is not null && GameViewModel?.CurrentPlayer.Background is not null && viewModel.Road.Owner is null)
            {
                viewModel.Road.Owner = GameViewModel.CurrentPlayer.Player;
                viewModel.Road.RoadState = RoadState.Road;
            }
        }

        private void Tile_RightClicked(TileCtrl tileCtrl, RightTappedRoutedEventArgs e)
        {
            if (GameViewModel is null) return;

            // Create a new context menu (MenuFlyout)
            MenuFlyout contextMenu = new MenuFlyout();

            // Add a menu item for each player in the Players collection
            foreach (var player in GameViewModel.Players)
            {
                if (player == GameViewModel.CurrentPlayer) continue;

                MenuFlyoutItem menuItem = new MenuFlyoutItem
                {
                    Text = "Target " + player.Name,
                    Tag = player,
                };
                menuItem.Click += MenuItem_Click; // Local function for handling clicks
                contextMenu.Items.Add(menuItem);
            }

            // Add a separator
            contextMenu.Items.Add(new MenuFlyoutSeparator());

            // Add a "Cancel" menu item
            MenuFlyoutItem cancelItem = new MenuFlyoutItem
            {
                Text = "Cancel"
            };
            cancelItem.Click += (s, e) => { /* Close the menu without doing anything */ };
            contextMenu.Items.Add(cancelItem);

            // Show the context menu

            contextMenu.ShowAt(tileCtrl, e.GetPosition(tileCtrl));



            // Local function to handle menu item clicks
            void MenuItem_Click(object sender, RoutedEventArgs args)
            {
                if (sender is MenuFlyoutItem clickedItem && clickedItem.Tag is PlayerViewModel player)
                {
                    // Handle the click event, e.g., display information about the selected player
                    // Consider using a dialog or a flyout for displaying messages in WinUI 3, as MessageBox is not available.
                    // E.g., use a ContentDialog for messages.
                    GameViewModel.RobberTile = tileCtrl.TileViewModel.Tile.TileKey;
                }
            }
        }

        private void Test_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {

        }

        private void OnHitMe(object sender, RoutedEventArgs e)
        {
            this.TraceMessage($"[CacheHit={HexGeometry.CacheHit}][cacheMiss={HexGeometry.CacheMiss}][cacheSize={HexGeometry.CacheSize}]");
        }
    }
}
