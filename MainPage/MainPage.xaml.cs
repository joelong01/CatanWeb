using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Catan.Services;
using Catan3.Controls;
using Catan3.Models;
using Catan3.Player;
using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Windows.Security.Cryptography.Core;
using WinUIEx.Messaging;
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
        public MainPage()
        {
            this.InitializeComponent();




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
        }

        private void OnRightButtonTapped(object sender, RightTappedRoutedEventArgs e)
        {
        }
        private void OnKeyUp(object sender, KeyRoutedEventArgs e)
        {
        }
        private void NewGame(GameType gameType, IList<PlayerViewModel> players)
        {
            if (MainPageModel is not null)
            {
                MainPageModel.EndGame();
                MainPageModel.GameViewModel.PropertyChanged -= GameViewModel_PropertyChanged;
            }


            MainPageModel = new MainPageViewModel(new FileService(), gameType, players);
            MainPageModel.GameViewModel.PropertyChanged += GameViewModel_PropertyChanged;
            this.DataContext = MainPageModel.GameViewModel;
        }
        private async void GameViewModel_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(GameViewModel.ErrorMessage) && MainPageModel.GameViewModel.ErrorMessage is not null)
            {
                // Check if the current thread has access to the UI thread
                if (Microsoft.UI.Dispatching.DispatcherQueue.GetForCurrentThread().HasThreadAccess)
                {
                    if (MainPageModel.GameViewModel.ErrorMessage.ErrorLevel == ErrorLevel.Critical)
                    {
                        // If already on UI thread, show dialog directly
                        await ShowMessageDialog(MainPageModel.GameViewModel.ErrorMessage.Message, "Catan Error");
                    }
                    this.TraceMessage(MainPageModel.GameViewModel.ErrorMessage.Message);
                }
                else
                {
                    // If not on UI thread, use DispatcherQueue to run on the UI thread
                    Microsoft.UI.Dispatching.DispatcherQueue.GetForCurrentThread().TryEnqueue(async () =>
                    {
                        if (MainPageModel.GameViewModel.ErrorMessage.ErrorLevel == ErrorLevel.Critical)
                        {
                            // If already on UI thread, show dialog directly
                            await ShowMessageDialog(MainPageModel.GameViewModel.ErrorMessage.Message, "Catan Error");
                        }
                        this.TraceMessage(MainPageModel.GameViewModel.ErrorMessage.Message);
                    });
                }
            }
        }
        private async Task ShowMessageDialog(string message, string title)
        {
            ContentDialog dialog = new()
            {
                Title = title,
                Content = message,
                CloseButtonText = "Ok",
                XamlRoot = this.XamlRoot
            };
            await dialog.ShowAsync();
        }
        private async void OnNewGame(object sender, RoutedEventArgs e)
        {
            
            NewGameViewModel viewModel = new(PlayerDatabase.AvailablePlayers);
            NewGameContentDialog dialog = new(viewModel)
            {

                XamlRoot = this.XamlRoot
            };
            var result = await dialog.ShowAsync();
            if (result == ContentDialogResult.Primary)
            {
                try
                {
                    NewGame(viewModel.SelectedGame, viewModel.PlayingPlayers);
                }
                catch (Exception ex)
                {
                    this.TraceMessage($"{ex}");
                }
            }
        }
        private void OnHitMe(object sender, RoutedEventArgs rea)
        {
            if (MainPageModel.GameViewModel is null) return;

        }
        private void OnUpdateLayout(object sender, RoutedEventArgs e)
        {
            if (MainPageModel is null) return;
            if (MainPageModel.GameViewModel is null) return;
            Debug.Assert(MainPageModel.GameViewModel.BoardInfo is not null);
            Debug.Assert(MainPageModel.GameViewModel.BoardInfo.Layout is not null);
            MainPageModel.GameViewModel.BoardInfo.Layout.OuterHexSize++;
            MainPageModel.GameViewModel.BoardInfo.Layout.OuterHexSize--;
            //  MainPageModel.GameViewModel.UpdateLayout();
        }
        private void OnEditPlayers(object sender, RoutedEventArgs e)
        {


            PlayerEditorWindow window = new();
            PlayerSettingsViewModel viewModel = new(window, PlayerDatabase.AvailablePlayers);
            window.ViewModel = viewModel;


            window.Activate();
        }

        private async void OnLoaded(object sender, RoutedEventArgs e)
        {
            await PlayerDatabase.LoadPlayerDatabase();
            List<PlayerViewModel> players = [..PlayerDatabase.AvailablePlayers];
            while (players.Count > 0 && players.Count > 5)
            {
                players.RemoveAt(players.Count - 1);
            }
            if (players.Count > 0)
            {
                try
                {
                    NewGame(GameType.Expansion, players);
                }
                catch(Exception ex)
                {
                    this.TraceMessage($"{ex}");
                }
            }
        }
    }
}
