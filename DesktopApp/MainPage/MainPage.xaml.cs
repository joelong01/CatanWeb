using System;
using System.Diagnostics;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Catan3.Models;
using Catan3.Player;
using Catan3.Tests;
using Catan3.Shared.Models;
using Catan3.Shared.Utility;
using CommunityToolkit.Mvvm.Messaging;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Navigation;
// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.
namespace Catan3
{
    public class GameStateTemplateSelector : DataTemplateSelector
    {
        public DataTemplate? RollOrderTemplate { get; set; } = null;
        public DataTemplate? PlayerStatsTemplate { get; set; } = null;
        public DataTemplate? PickSupplementalPlayersTemplate { get; set; } = null;
        protected override DataTemplate SelectTemplateCore(object item, DependencyObject container)
        {
            Debug.Assert(container is not null);

            if (container is not null && container is MainPage page)
            {
                switch (page.MainPageModel.GameViewModel.GameModel?.GameState)
                {
                    case GameState.FinishedRollOrder:
                        return RollOrderTemplate ?? base.SelectTemplateCore(item, container);
                    case GameState.PickSupplementalPlayers:
                        return PickSupplementalPlayersTemplate ?? base.SelectTemplate(item, container);
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
        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);
            if (e.NavigationMode == NavigationMode.Back)
            {
                Debug.Assert(MainWindow.CurrentGame is not null);
                MainPageModel = MainWindow.CurrentGame;
                return;
            }
            if (e.Parameter is MainPageViewModel mainPageModel)
            {
                MainPageModel = mainPageModel;
                if (MainWindow.Instance is not null)
                {
                    MainWindow.Instance.PresenterKind = AppWindowPresenterKind.FullScreen;
                }
            }

        }

        public DataTemplate? StateToItemTemplate(GameState gameState)
        {
            switch (gameState)
            {
                case GameState.FinishedRollOrder:
                    if (this.Resources.TryGetValue("RollOrderTemplate", out var rollOrderTemplate))
                    {
                        return rollOrderTemplate as DataTemplate;
                    }
                    break;
                case GameState.PickSupplementalPlayers:
                    if (this.Resources.TryGetValue("PickSupplementalPlayersTemplate", out var pickSupplementalPlayers))
                    {
                        return pickSupplementalPlayers as DataTemplate;
                    }
                    break;
                default:
                    if (this.Resources.TryGetValue("PlayerStatsTemplate", out var playerStatsTemplate))
                    {
                        return playerStatsTemplate as DataTemplate;
                    }
                    break;

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
            depPropClass?.SetMainPageModel(( MainPageViewModel )e.OldValue, ( MainPageViewModel )e.NewValue);
        }
        private void SetMainPageModel(MainPageViewModel oldValue, MainPageViewModel newValue)
        {

            if (oldValue is not null)
            {
                oldValue.EndGame();
                oldValue.GameViewModel.PropertyChanged -= GameViewModel_PropertyChanged;
            }


            newValue.GameViewModel.PropertyChanged += GameViewModel_PropertyChanged;
            MainWindow.CurrentGame = newValue;
            this.DataContext = newValue;

            // Set the Tag property for UI test access
            UpdateGameModelTag();
        }
        private void OnRightButtonTapped(object sender, RightTappedRoutedEventArgs e)
        {
            ToggleTitleBar();
            HideMenu();
            e.Handled = true;
        }
        private void OnKeyUp(object sender, KeyRoutedEventArgs e)
        {
            if (e.Key == Windows.System.VirtualKey.F11)
            {
                ToggleTitleBar();
                e.Handled = true;
            }
            if (e.Key == Windows.System.VirtualKey.Escape)
            {
                HideMenu();
            }
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

            // Update the Tag when GameModel changes
            if (e.PropertyName == nameof(GameViewModel.GameModel))
            {
                UpdateGameModelTag();
            }
        }

        /// <summary>
        /// Updates the MainPageViewModel's GameModelJson property with the current GameModel for UI test access
        /// </summary>
        private void UpdateGameModelTag()
        {
            try
            {
                if (MainPageModel?.GameViewModel?.GameModel is not null)
                {
                    // Serialize GameModel to JSON and update the ViewModel property
                    // This will automatically update the AutomationProperties.ItemStatus binding
                    var options = new JsonSerializerOptions { WriteIndented = false };
                    var gameModelJson = JsonSerializer.Serialize(MainPageModel.GameViewModel.GameModel, options);
                    MainPageModel.GameModelJson = gameModelJson;
                }
            }
            catch (Exception ex)
            {
                this.TraceMessage($"Error updating GameModel JSON: {ex.Message}");
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
        private void OnNewGame(object sender, RoutedEventArgs e)
        {

            try
            {

                NewGameViewModel viewModel = new(MainWindow.PlayerDatabase.AllPlayers);
                Frame.Navigate(typeof(NewGamePage), viewModel);
            }
            catch (Exception ex)
            {
                this.TraceMessage($"{ex}");
            }
            finally
            {
                HideMenu();
            }

        }
        private void OnHitMe(object sender, RoutedEventArgs rea)
        {
            if (MainPageModel is null) return;
            MainPageModel.ShowCommands = false;

            var gameRollModel = new GameRollModel();
            gameRollModel.TotalRolls++;
            gameRollModel.RollCounts[0]++;

            var json = JsonSerializer.Serialize(gameRollModel);

            GameRollModel? grm = JsonSerializer.Deserialize<GameRollModel>(json);
            if (grm is not null)
            {
                this.TraceMessage($"{grm.TotalRolls}={grm.RollCounts.ListToCsv()}");
            }


        }

        private void OnEditPlayers(object sender, RoutedEventArgs e)
        {
            PlayerEditorWindow window = new();
            PlayerSettingsViewModel viewModel = new(window,  MainWindow.PlayerDatabase);
            window.ViewModel = viewModel;
            window.Activate();
            HideMenu();
        }


        private void ListView_DragItemsCompleted(ListViewBase sender, DragItemsCompletedEventArgs args)
        {
            MainPageModel?.SetPlayerOrder();
        }
        private async void OnRunTests(object sender, RoutedEventArgs e)
        {
            HideMenu();


            var test = new CatanTests();
            await test.TestScore();
            //await test.RunAll();
            
        }
        private void OnClose(object sender, RoutedEventArgs e)
        {
            App.Current.Exit();
        }
        private void OnToggleTitleBar(object sender, RoutedEventArgs e)
        {
            ToggleTitleBar();
        }
        private void ToggleTitleBar()
        {
            if (MainWindow.Instance is not null)
            {
                if (MainWindow.Instance.PresenterKind != AppWindowPresenterKind.FullScreen)
                {
                    MainWindow.Instance.PresenterKind = AppWindowPresenterKind.FullScreen;
                    MainWindow.Instance.IsTitleBarVisible = false;
                    MainWindow.Instance.ExtendsContentIntoTitleBar = true;


                }
                else
                {
                    MainWindow.Instance.PresenterKind = AppWindowPresenterKind.Overlapped;
                    MainWindow.Instance.IsTitleBarVisible = true;
                    MainWindow.Instance.ExtendsContentIntoTitleBar = false;
                }
            }
            HideMenu();
        }
        /// <summary>
        ///     When you fist startup, there is no MainPageModel, so we bind to a click event
        ///     if their is a MainPageModel, then we set the property so that the menu is shown
        ///     if there isn't we show the menu.  this way setting the flag in the model will
        ///     properly open and close the menu so we'll be able to close it after a command
        /// </summary>
        private void OnShowMenu(object sender, RoutedEventArgs e)
        {
            ShowMenu();
        }

        private void ShowMenu()
        {
            if (MainPageModel is not null)
            {
                MainPageModel.ShowCommands = !MainPageModel.ShowCommands;
            }
            else
            {
                MySplitView.IsPaneOpen = !MySplitView.IsPaneOpen;
            }
        }

        private void HideMenu()
        {
            if (MainPageModel is not null)
            {
                MainPageModel.ShowCommands = false;
            }
            else
            {
                MySplitView.IsPaneOpen = false;
            }
        }
    }
}
