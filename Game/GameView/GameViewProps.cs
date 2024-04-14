
using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Messaging;

namespace Catan3.Models
{
    public partial class GameViewModel : ObservableRecipient
    {

        [ObservableProperty]
        private string _id;

        [ObservableProperty]
        private IBoardInfo? _boardInfo;

        [ObservableProperty]
        private RobberViewModel _robberViewModel = new(new());

        [ObservableProperty]
        private string _name = "Nameless";

        [ObservableProperty]
        private bool _isKnightsAndRobbers = false;

        [ObservableProperty]
        private HouseRules _houseRules = new();

        [ObservableProperty]
        private PlayerViewModel _currentPlayer = PlayerViewModel.Default;

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
        public GameModel _gameModel = new();

        [ObservableProperty]
        private int _shownStars = 13; // stars are visible above this number

        [ObservableProperty]
        private int _shuffleCount = 0;


        /// <summary>
        ///     NOTE:  this is *partial* method that is implemented by the MVVM Toolkit
        ///     and magically gets called based on the _foo => OnFooChanged() naming
        ///     convention.
        /// </summary>
        /// <param name="value"></param>
        partial void OnShownStarsChanged(int value)
        {
            //this.TraceMessage($"New Shown Stars: {value}");
            ShowStarValues(value);
        }

        partial void OnCurrentPlayerChanged(PlayerViewModel? oldValue, PlayerViewModel newValue)
        {
            if (newValue is null) return;
         //   this.TraceMessage($"Current Player: {oldValue} -> {newValue}");
            this.GameModel.CurrentPlayerId = newValue.Id;
            Messenger.Send(new CurrentPlayerChanged(newValue));
        }

       
    }
}
