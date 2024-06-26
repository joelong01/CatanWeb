using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Catan3.Models;
using Catan3.Player;
using Catan3.Utility;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using Windows.Storage;


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
                var myDocuments = KnownFolders.DocumentsLibrary;
                var fileName= $"{ViewModel.SelectedGame}-{UniqueIdGenerator.GenerateUniqueId()}";
                return Path.Join(myDocuments.Path, "Catan Saved Games", fileName, ".catan");
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

            Frame.GoBack();
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
                var filePath = await MainWindow.FileService.PickFile(MainWindow.Instance, [".catan"]);
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
    }
}
