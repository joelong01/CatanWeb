using System.Diagnostics;
using Catan.Services;
using System.Security.AccessControl;
using Catan3.Models;
using Catan3.Player;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using Catan3.Controller;
using CommunityToolkit.Mvvm.Messaging;
using System;
using System.Threading.Tasks;


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
                var mainPageModel = new MainPageViewModel(MainWindow.FileService, MainWindow.PlayerDatabase, ViewModel.SelectedGame, ViewModel.PlayingPlayers);
                Frame.Navigate(typeof(MainPage), mainPageModel);
                Frame.BackStack.Clear();
            }
            catch (Exception ex)
            {
                await ShowErrorDialog(ex.Message);
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
                var compressedBytes = await MainWindow.FileService.OpenFileAsync();
                if (compressedBytes is null)
                {
                    this.TraceMessage("Unable to open file");
                    return;
                }

                MainPageViewModel mpViewModel = new ( compressedBytes, MainWindow.FileService, MainWindow.PlayerDatabase);
                Frame.Navigate(typeof(MainPage), mpViewModel);
                Frame.BackStack.Clear();

            }
            catch (Exception ex)
            {
                await ShowErrorDialog(ex.Message);
            }


        }
    }
}
