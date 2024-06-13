using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Catan.Services;
using Catan3.Controls;
using Catan3.Models;
using Catan3.Player;
using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Windows.Security.Cryptography.Core;
using WinUIEx;
using WinUIEx.Messaging;
// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.
namespace Catan3
{
    public class GameStateTemplateSelector : DataTemplateSelector
    {
        public DataTemplate? RollOrderTemplate { get; set; } = null;
        public DataTemplate? PlayerStatsTemplate { get; set; } = null;
        protected override DataTemplate SelectTemplateCore(object item, DependencyObject container)
        {
            Debug.Assert(container is not null);

            if (container is not null && container is MainPage page)
            {
                switch (page.MainPageModel.GameViewModel.GameModel.GameState)
                {
                    case GameState.FinishedRollOrder:
                        return RollOrderTemplate ?? base.SelectTemplateCore(item, container); ;
                    default:
                        return PlayerStatsTemplate ?? base.SelectTemplateCore(item, container); ;
                }
            }
            return base.SelectTemplateCore(item, container);
        }
    }
    public sealed partial class MainPage : Page
    {
        public MainPage()
        {
            this.InitializeComponent();

        }
        public DataTemplate? StateToItemTemplate(GameState gameState)
        {
            if (gameState != GameState.FinishedRollOrder)
            {
                if (this.Resources.TryGetValue("PlayerStatsTemplate", out var playerStatsTemplate))
                {
                    return playerStatsTemplate as DataTemplate;
                }
            }
            if (this.Resources.TryGetValue("RollOrderTemplate", out var rollOrderTemplate))
            {
                return rollOrderTemplate as DataTemplate;
            }
            return null;
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
            if (e.Key == Windows.System.VirtualKey.F11)
            {
                ToggleTitleBar();
                e.Handled = true;
            }
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
            MainPageModel.ShowCommands = false;
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
           
            //  
            //  Uncomment if you want to have the game launched with a game started

            //List<PlayerViewModel> players = [..PlayerDatabase.AvailablePlayers];
            //while (players.Count > 0 && players.Count > 5)
            //{
            //    players.RemoveAt(players.Count - 1);
            //}
     
            //if (players.Count > 0)
            //{
            //    try
            //    {
            //        NewGame(GameType.Expansion, players);
            //    }
            //    catch (Exception ex)
            //    {
            //        this.TraceMessage($"{ex}");
            //    }
            //}
        }
       
        private void ListView_DragItemsCompleted(ListViewBase sender, DragItemsCompletedEventArgs args)
        {
            MainPageModel?.SetPlayerOrder();
        }
        private async void OnRunTests(object sender, RoutedEventArgs e)
        {
            try
            {
                var json = JsonSerializer.Serialize(PlayerDatabase.AvailablePlayers[0]);
                var cpy = JsonSerializer.Deserialize<PlayerViewModel>(json);
                if (cpy is null)
                {
                    this.TraceMessage("FAILED to deserialize PlayerViewModel");
                    return;
                }
                this.TraceMessage($"{cpy.Id}");
            }
            catch (Exception ex)
            {
                this.TraceMessage($"FAILED to deserialize PlayerViewModel: {ex}");
            }
            try
            {
                await PlayerDatabase.LoadPlayerDatabase();
            }
            catch (Exception ex)
            {
                this.TraceMessage($"FAILED ayerDatabase.LoadPlayerDatabase(): {ex}");
            }
        }
        private void OnClose(object sender, RoutedEventArgs e)
        {
            App.Current.Exit();
        }
        private void OnToggleTitleBar(object sender, RoutedEventArgs e)
        {
            ToggleTitleBar();
        }
        private static void ToggleTitleBar()
        {
            if (MainWindow.Instance is not null)
            {
                if (MainWindow.Instance.PresenterKind != AppWindowPresenterKind.FullScreen)
                {
                    MainWindow.Instance.PresenterKind = AppWindowPresenterKind.FullScreen;
                }
                else
                {
                    MainWindow.Instance.PresenterKind = AppWindowPresenterKind.Overlapped;
                }
            }
        }
    }
}
