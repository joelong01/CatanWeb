using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Catan.Utility;
using Catan3.Models;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Messaging;
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
            }

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



        private void OnHitMe(object sender, RoutedEventArgs rea)
        {
            if (MainPageModel.GameViewModel is null) return;
            var rand = new Random((int)DateTime.Now.Ticks);
            var index = rand.Next(MainPageModel.GameViewModel.Tiles.Count);
            var tile = MainPageModel.GameViewModel.Tiles[index] ;
            if (tile is null) return;

            tile.Tile.TemporarilyGold = tile.Tile.TemporarilyGold ? false : true;


        }

        private void OnUpdateLayout(object sender, RoutedEventArgs e)
        {
            if (MainPageModel is null) return;
            if (MainPageModel.GameViewModel is null) return;
            Debug.Assert(MainPageModel.GameViewModel.BoardInfo is not null);
            Debug.Assert(MainPageModel.GameViewModel.BoardInfo.Layout is not null);
            MainPageModel.GameViewModel.BoardInfo.Layout.OuterHexSize++ ;
            MainPageModel.GameViewModel.BoardInfo.Layout.OuterHexSize--;
          //  MainPageModel.GameViewModel.UpdateLayout();
        }
    }
}
