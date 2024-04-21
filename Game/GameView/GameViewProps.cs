
using System.Collections.Generic;
using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Messaging;
using static Catan3.Models.TurnRollViewModel;

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

        [ObservableProperty]
        private CatanOrientation _orientation = CatanOrientation.FaceUp;

        [ObservableProperty]
        private GameRollViewModel _gameRollViewModel = new();

        [ObservableProperty]
        private TurnRollViewModel  _turnRollViewModel = new();

        // total resoruces allocated for the game
        [ObservableProperty]
        private ResourcesViewModel _gameResourceViewModel = new();

        // total stars for each resourcetype in the game
        [ObservableProperty]
        private ResourcesViewModel _starsResourceViewModel = new();
        //
        //  resources that show up in the UI as ResourceCardCtrl - not not observable as designed for one time only binding.
        //  when we build the Pirates expansion, we'll have to add to this list.
        public ObservableCollection<ResourceType> TrackedResources { get; } =  [ResourceType.Sheep, ResourceType.Wheat, ResourceType.Wood, ResourceType.Brick, ResourceType.Ore, ResourceType.GoldMine];

        /// <summary>
        ///     this broadcasts to all things that are looking for the global orientation that causes things like Tiles and Harbors to flip
        /// </summary>
        /// <param name="value"></param>
        partial void OnOrientationChanged(CatanOrientation value)
        {
            Messenger.Send(new UpdateOrientation(value));
        }


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
