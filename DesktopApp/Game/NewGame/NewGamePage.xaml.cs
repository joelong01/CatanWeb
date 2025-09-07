using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Catan3.Models;
using Catan3.Player;
using Catan3.Utility;
using Catan3.Shared.Models;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;



namespace Catan3
{
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class NewGamePage : Page
    {
        public NewGamePage()
        {
            this.InitializeComponent();
        }
        public static readonly DependencyProperty ViewModelProperty = DependencyProperty.Register("ViewModel", typeof(NewGameViewModel), typeof(NewGamePage), new PropertyMetadata(null));
        public NewGameViewModel ViewModel
        {
            get => ( NewGameViewModel )GetValue(ViewModelProperty);
            set => SetValue(ViewModelProperty, value);
        }

        private async void OnStart(object sender, RoutedEventArgs e)
        {
            try
            {
                List<string> playerIds = ViewModel.PlayingPlayers.Select( p => p.Id ).ToList();
                var mainPageModel = new MainPageViewModel(MainWindow.FileService, MainWindow.PlayerDatabase, ViewModel.SelectedGame, playerIds, GameName);
                Frame.Navigate(typeof(MainPage), mainPageModel);
                Frame.BackStack.Clear();
            }
            catch (Exception ex)
            {
                await ShowErrorDialog(ex.Message);
            }

        }

        private string GameName
        {
            get
            {
                // Use corrected Documents path to avoid truncation issues
                var documentsPath = Catan.Services.FileService.GetCorrectDocumentsPath();
                var fileName = $"{ViewModel.SelectedGame}-{UniqueIdGenerator.GenerateUniqueId()}.catan";
                
                return Path.Join(documentsPath, "Catan Saved Games", fileName);
            }
        }


        public async Task ShowErrorDialog(string errorMessage)
        {
            var dialog = new ContentDialog
            {
                Title = "Error",
                Content = errorMessage,
                CloseButtonText = "Ok",
                XamlRoot = this.Content.XamlRoot
            };

            await dialog.ShowAsync();
        }
        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);

            if (e.Parameter is NewGameViewModel viewModel)
            {
                ViewModel = viewModel;
            }
            else
            {
                Debug.Assert(false, "the paramater should be a GameViewModel");
            }
        }

        private void OnCancel(object sender, RoutedEventArgs e)
        {
            if (Frame.CanGoBack)
            {
                Frame.GoBack();
            }
            else
            {
                // Navigate to a fresh NewGamePage if we can't go back
                NewGameViewModel viewModel = new(MainWindow.PlayerDatabase.AllPlayers);
                Frame.Navigate(typeof(NewGamePage), viewModel);
                Frame.BackStack.Clear();
            }
        }

        private void OnManagePlayers(object sender, RoutedEventArgs e)
        {
            PlayerEditorWindow window = new();
            PlayerSettingsViewModel viewModel = new(window,  MainWindow.PlayerDatabase);
            window.ViewModel = viewModel;
            window.Activate();
        }

        private async void OnOpen(object sender, RoutedEventArgs e)
        {
            try
            {
                Debug.Assert(MainWindow.Instance is not null);
                var filePath = await MainWindow.FileService.OpenFileAsync(MainWindow.Instance, [".catan"]);
                if (filePath is not null && filePath != "")
                {
                    
                    MainPageViewModel mpViewModel = new ( MainWindow.FileService, MainWindow.PlayerDatabase, GameType.SavedGame, [], filePath);
                    Frame.Navigate(typeof(MainPage), mpViewModel);
                    Frame.BackStack.Clear();
                }

            }
            catch (Exception ex)
            {
                await ShowErrorDialog(ex.Message);
            }


        }

        private async void OnRunTests(object sender, RoutedEventArgs e)
        {
            try
            {
                Debug.Assert(MainWindow.Instance is not null);
                var filePath = await MainWindow.FileService.OpenFileAsync(MainWindow.Instance, [".catan_test"]);
                if (filePath is not null && filePath != "")
                {
                  // Navigate to MainPage with a minimal ViewModel for test mode
                    MainPageViewModel mpViewModel = new(MainWindow.FileService, MainWindow.PlayerDatabase, GameType.SavedGame, [], filePath);
                    Frame.Navigate(typeof(MainPage), mpViewModel);
                    Frame.BackStack.Clear();
                }
            }
            catch (Exception ex)
            {
                await ShowErrorDialog($"Failed to load test file: {ex.Message}");
            }
        }

        private void OnShowMenu(object sender, RoutedEventArgs e)
        {
            ViewModel.ShowMenu = !ViewModel.ShowMenu;
        }

        private async void OnSettings(object sender, RoutedEventArgs e)
        {
            try
            {
                // Show Settings dialog centered on screen
                var settingsDialog = new Settings.SettingsDialog
                {
                    XamlRoot = this.Content.XamlRoot
                };
                
                await settingsDialog.ShowAsync();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error showing Settings dialog: {ex.Message}");
                await ShowErrorDialog($"Failed to open settings: {ex.Message}");
            }
        }
    }
}
