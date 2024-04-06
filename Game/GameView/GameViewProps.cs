
using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Catan3.Models
{
    public partial class GameViewModel : ObservableRecipient
    {
        [ObservableProperty]
        private IBoardInfo? _boardInfo;

        [ObservableProperty]
        private RobberViewModel _robber = new(new());

        [ObservableProperty]
        private string _name = "Nameless";

        [ObservableProperty]
        private bool _isKnightsAndRobbers = false;

        [ObservableProperty]
        private HouseRules _houseRules = new();

        [ObservableProperty]
        private PlayerViewModel? _currentPlayer;

        [ObservableProperty]
        private ObservableCollection<TileViewModel> _tiles = [];

        [ObservableProperty]
        private ObservableCollection<BuildingViewModel> _buildings = [];

        [ObservableProperty]
        private ObservableCollection<PlayerViewModel> _players = [];

        [ObservableProperty]
        private ObservableCollection<RoadViewModel> _roads = [];

        [ObservableProperty]
        private ObservableCollection<HarborViewModel> _harbors = [];

        [ObservableProperty]
        public GameModel? _gameModel;

        [ObservableProperty]
        private int _shownStars = 13; // stars are visible above this number

        [ObservableProperty]
        private int _shuffleCount = 0; // stars are visible above this number


        /// <summary>
        ///     NOTE:  this is *partial* method that is implemented by the MVVM Toolkit
        ///     and magically gets called based on the _foo => OnFooChanged() naming
        ///     convention.
        /// </summary>
        /// <param name="value"></param>
        partial void OnShownStarsChanged(int value)
        {
            ShowStarValues(value);
        }

        public void UpdateBindings()
        {
            OnPropertyChanged(nameof(Tiles));
            OnPropertyChanged(nameof(Buildings));
            OnPropertyChanged(nameof(Players));
            OnPropertyChanged(nameof(Roads));
            OnPropertyChanged(nameof(Harbors));
            OnPropertyChanged(nameof(Robber));
        
        }
    }
}
