using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Catan.Utility;
using Catan3.Controls;
using Catan3.Models;
using Catan3.Player;
using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
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
        private ObservableCollection<SelectPlayerModel> AvailablePlayers { get; set; } = [];
        public MainPage()
        {
            this.InitializeComponent();
        
            Games.Add(GameType.Expansion);
            Games.Add(GameType.Regular);
            SelectedGame = GameType.Expansion;



        }



        public static readonly DependencyProperty SelectedGameProperty = DependencyProperty.Register("SelectedGame", typeof(GameType), typeof(MainPage), new PropertyMetadata(GameType.Regular));
        public GameType SelectedGame
        {
            get => ( GameType )GetValue(SelectedGameProperty);
            set
            {
                if (value == SelectedGame) return;
                SetValue(SelectedGameProperty, value);
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
        }
        public ObservableCollection<GameType> Games { get; set; } = [];
        private void OnRightButtonTapped(object sender, RightTappedRoutedEventArgs e)
        {
        }
        private void OnKeyUp(object sender, KeyRoutedEventArgs e)
        {
        }
        private void NewGame()
        {
            if (MainPageModel is not null)
            {
                MainPageModel.EndGame();
                MainPageModel.GameViewModel.PropertyChanged -= GameViewModel_PropertyChanged;
            }
            foreach (var player in AvailablePlayers)
            {
                player.Playing = false;
            }
            int count = SelectedGame == GameType.Regular ? 3 : 5;
            for (int i = 0; i < count; i++)
            {
                AvailablePlayers[i].Playing = true;
            }
            var selectedPlayers = new List<PlayerViewModel>(
                            AvailablePlayers
                                .Where(selectModel => selectModel.Playing) // Filter for models where Playing is true
                                .Select(selectModel => PlayerDatabase.AvailablePlayers.FirstOrDefault(pvm => pvm.Id == selectModel.Id)) // Map to CurrentPlayer
                                .OfType<PlayerViewModel>() // Filter out any nulls effectively and ensure all are CurrentPlayer
                        );
            MainPageModel = new MainPageViewModel(new FileService(), SelectedGame, selectedPlayers);
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
        private void OnNewGame(object sender, RoutedEventArgs e)
        {
            NewGame();
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
        private async void OnEditPlayers(object sender, RoutedEventArgs e)

        {
            PlayerSettingsDialog playerSettingsDialog = new PlayerSettingsDialog();
            var result = await playerSettingsDialog.ShowAsync();

            if (result == ContentDialogResult.Primary)
            {
                // Handle Close button click
            }
            else if (result == ContentDialogResult.Secondary)
            {
                // Handle Cancel button click
            }

            //PlayerEditorWindow window = new()
            //{
            //    ViewModel = new(PlayerDatabase.AvailablePlayers)
            //};
            //window.Activate();
        }

        private async void OnLoaded(object sender, RoutedEventArgs e)
        {
            await PlayerDatabase.LoadPlayerDatabase();
            AvailablePlayers = new ObservableCollection<SelectPlayerModel>(
            PlayerDatabase.AvailablePlayers.Select(player => new SelectPlayerModel(player.Name, player.Id, false)));
            NewGame();
        }
    }
}
