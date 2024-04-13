using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Catan.Utility;
using Catan3.Controls;
using Catan3.Models;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Messaging;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Windows.Storage;
using Windows.Storage.Pickers;
using WinRT.Interop;



// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace Catan3
{






    public partial class SelectPlayerModel(string name, string id, bool selected) : ObservableObject
    {
        [ObservableProperty]
        private string _name = name;
        [ObservableProperty]
        private bool _playing = selected;
        [ObservableProperty]
        private string _id = id;
    }



    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class MainPage : Page
    {

        private ObservableCollection<SelectPlayerModel> AvailablePlayers { get; set; }
       
        public MainPage()
        {
            this.InitializeComponent();
            AvailablePlayers = new ObservableCollection<SelectPlayerModel>(
                PlayerDatabase.AvailablePlayers.Select(player => new SelectPlayerModel(player.Name, player.Id, false)));
            AvailablePlayers[0].Playing = true;
            AvailablePlayers[1].Playing = true;
            AvailablePlayers[2].Playing = true;
            Games.Add(GameType.Expansion);
            Games.Add(GameType.Regular);
            SelectedGame = GameType.Expansion;
            NewGame();
            Messenger = MainPageModel.MessageService;
        }
        public static readonly DependencyProperty SelectedGameProperty = DependencyProperty.Register("SelectedGame", typeof(GameType), typeof(MainPage), new PropertyMetadata(GameType.Regular));
        public GameType SelectedGame
        {
            get => ( GameType )GetValue(SelectedGameProperty);
            set
            {
                if (value != SelectedGame)
                {

                    SetValue(SelectedGameProperty, value);
                }
            }
        }
        public static readonly DependencyProperty MainPageModelProperty = DependencyProperty.Register("MainPageModel", typeof(MainPageViewModel), typeof(MainPage), new PropertyMetadata(null, MainPageModelChanged));
        public MainPageViewModel MainPageModel
        {
            get => ( MainPageViewModel )GetValue(MainPageModelProperty);
            set => SetValue(MainPageModelProperty, value);
        }
        private static void MainPageModelChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var depPropClass = d as MainPage;
            var depPropValue = (MainPageViewModel)e.NewValue;
            depPropClass?.SetMainPageModel(depPropValue);
        }
        private void SetMainPageModel(MainPageViewModel value)
        {
            Messenger = value.MessageService;
        }

#pragma warning disable CS8618 // fixing confused compiler
        public static IMessenger Messenger { get; private set; }
#pragma warning restore CS8618 // restoring 

        public ObservableCollection<GameType> Games { get; set; } = [];
        private void OnRightButtonTapped(object sender, RightTappedRoutedEventArgs e)
        {

        }
        private void OnKeyUp(object sender, KeyRoutedEventArgs e)
        {

        }
        private void NewGame()
        {

            var selectedPlayers = new List<PlayerViewModel>(
                            AvailablePlayers
                                .Where(selectModel => selectModel.Playing) // Filter for models where Playing is true
                                .Select(selectModel => PlayerDatabase.AvailablePlayers.FirstOrDefault(pvm => pvm.Id == selectModel.Id)) // Map to CurrentPlayer
                                .OfType<PlayerViewModel>() // Filter out any nulls effectively and ensure all are CurrentPlayer
                        );

            
            MainPageModel = new MainPageViewModel(new FileService(), SelectedGame, selectedPlayers);
            this.DataContext = MainPageModel.GameViewModel;


        }
        private async Task ShowMessageDialog(string message, string title)
        {
            ContentDialog dialog = new()
            {
                Title = title,
                Content = message,
                CloseButtonText = "Ok"
            };

            await dialog.ShowAsync();
        }
        private void OnNewGame(object sender, RoutedEventArgs e)
        {
            NewGame();
        }




        private void Tile_RightClicked(TileCtrl tileCtrl, RightTappedRoutedEventArgs e)
        {
            if (MainPageModel.GameViewModel is null) return;

            // Create a new context menu (MenuFlyout)
            MenuFlyout contextMenu = new();

            // Add a menu item for each player in the Players collection
            foreach (var player in MainPageModel.GameViewModel.Players)
            {
                if (player == MainPageModel.GameViewModel.CurrentPlayer) continue;

                MenuFlyoutItem menuItem = new()
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
            MenuFlyoutItem cancelItem = new()
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
                if (MainPageModel.GameViewModel.Robber.RobberModel is null) return;
                if (sender is MenuFlyoutItem clickedItem && clickedItem.Tag is PlayerViewModel player)
                {
                    // Handle the click event, e.g., display information about the selected player
                    // Consider using a dialog or a flyout for displaying messages in WinUI 3, as MessageBox is not available.
                    // E.g., use a ContentDialog for messages.
                    MainPageModel.GameViewModel.Robber.RobberModel.Coordinates = tileCtrl.TileViewModel.Tile.TileKey;
                }
            }
        }

        private void Test_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {

        }

        private void OnHitMe(object sender, RoutedEventArgs rea)
        {
            if (MainPageModel.GameViewModel is null) return;

            this.TraceMessage($"Current Player: {MainPageModel.GameViewModel.CurrentPlayer}");


        }



        private void OnFlipTiles(object sender, RoutedEventArgs e)
        {
            if (MainPageModel.GameViewModel is null || MainPageModel.GameViewModel.Tiles.Count == 0) return;
            CatanOrientation newOrientaiton = CatanOrientation.FaceUp;
            if (MainPageModel.GameViewModel.Tiles[0].Orientation == CatanOrientation.FaceUp)
            {
                newOrientaiton = CatanOrientation.FaceDown;
            }

            foreach (var tile in MainPageModel.GameViewModel.Tiles)
            {
                tile.Orientation = newOrientaiton;
            }

            foreach (var harbor in MainPageModel.GameViewModel.Harbors)
            {
                harbor.Orientation = newOrientaiton;
            }
        }

        public async Task<StorageFile?> SaveFileAsync(string defaultFileName)
        {
            try
            {
                var savePicker = new FileSavePicker();
                savePicker.SuggestedStartLocation = PickerLocationId.DocumentsLibrary;

                savePicker.FileTypeChoices.Add("Catan File", [".catan"]);

                savePicker.SuggestedFileName = defaultFileName;

                var window = (Application.Current as App)?.MainWindow as MainWindow;
                IntPtr hwnd = WinRT.Interop.WindowNative.GetWindowHandle( window);
                InitializeWithWindow.Initialize(savePicker, hwnd);

                return await savePicker.PickSaveFileAsync();

            }
            catch (Exception ex)
            {
                this.TraceMessage($"{ex}");
                return null;
            }
        }

        public async Task<StorageFile?> OpenFileAsync()
        {
            try
            {
                // Create a new instance of the FileOpenPicker
                var openPicker = new FileOpenPicker();
                openPicker.ViewMode = PickerViewMode.Thumbnail;
                openPicker.SuggestedStartLocation = PickerLocationId.DocumentsLibrary;
                openPicker.FileTypeFilter.Add(".catan");  // Specify the file types that are allowed to be opened

                // Get the MainWindow and its window handle
                var window = (Application.Current as App)?.MainWindow as MainWindow;
                IntPtr hwnd = WindowNative.GetWindowHandle(window);
                InitializeWithWindow.Initialize(openPicker, hwnd);

                // Show the file picker and await the user's file selection
                StorageFile file = await openPicker.PickSingleFileAsync();
                return file;  // Return the selected file
            }
            catch (Exception ex)
            {
                // Log or handle the exception as needed
                Debug.WriteLine($"Error in opening file: {ex}");
                return null;  // Return null in case of an error
            }
        }
    }
}
