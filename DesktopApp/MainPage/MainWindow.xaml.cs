using Catan.Services;
using Catan3.Models;
using Catan3.Shared.Models;
using Microsoft.UI.Xaml;
using WinUIEx;
// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.
namespace Catan3
{
    public partial class MainWindow : WindowEx
    {
        public static PlayerDatabase PlayerDatabase { get; private set; } = new();
        public static FileService FileService { get; private set; } = new();
        public static MainPageViewModel? CurrentGame { get; set; } = null;
        public static MainWindow? Instance { get; private set; } = null;
        public MainWindow()
        {
            Instance = this;
            this.InitializeComponent();
            
            // Subscribe to window closing event
            this.Closed += OnMainWindowClosed;
        }

        private async void OnLoaded(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
        {
            await PlayerDatabase.LoadPlayerDatabase();
            
            // Check if we should auto-load a test file
            if (!string.IsNullOrEmpty(App.ActivatedFilePath))
            {
                // Auto-load test file and skip NewGame dialog
                LoadActivatedFile(App.ActivatedFilePath);
            }
            else
            {
                // Normal flow - show NewGame dialog
                NewGameViewModel viewModel = new(PlayerDatabase.AllPlayers);
                MainFrame.Navigate(typeof(NewGamePage), viewModel);
            }
        }
        
        private void LoadActivatedFile(string activatedFilePath)
        {
            try
            {
                // Create the main page view model for a saved game and navigate directly to game
                // This mimics what NewGamePage does when loading a saved game
                var mainPageViewModel = new MainPageViewModel(FileService, PlayerDatabase, GameType.SavedGame, [], activatedFilePath, App.IsTestMode);
                CurrentGame = mainPageViewModel;
                MainFrame.Navigate(typeof(MainPage), mainPageViewModel);
                this.Title = $"Catan ({activatedFilePath})";
            }
            catch
            {
                // Fallback to normal flow if anything goes wrong
                NewGameViewModel viewModel = new(PlayerDatabase.AllPlayers);
                MainFrame.Navigate(typeof(NewGamePage), viewModel);
            }
        }
        
        private void OnMainWindowClosed(object sender, WindowEventArgs args)
        {
            // Close the DebugWindow if it's open
            DebugWindow.CloseInstance();
        }
    }
}
