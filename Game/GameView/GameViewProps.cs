
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Messaging;
using Windows.ApplicationModel.Activation;
using static Catan3.Models.TurnRollViewModel;

namespace Catan3.Models
{
    public static class GameViewModelStatics
    {
        public static ResourceType[] StarsTrackResourceList =  [ResourceType.Sheep, ResourceType.Wheat, ResourceType.Wood, ResourceType.Brick, ResourceType.Ore];
        public static ResourceType[] PlayerTrackResourceList =  [..StarsTrackResourceList, ResourceType.GoldMine, ResourceType.Robber];
    }
    public partial class GameViewModel : ObservableRecipient
    {
        /// <summary>
        ///     Data that drives the PurchaseUi
        /// </summary>
        [ObservableProperty]
        private ObservableCollection<EntitlementPurchaseViewModel> _purchasableEntitlements = [];

        // id of the game
        [ObservableProperty]
        private string _id;


        [ObservableProperty]
        private IGameMetadata? _boardInfo;

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
        private ResourcesViewModel _gameResources = new(GameViewModelStatics.PlayerTrackResourceList);


        // total stars for each resourcetype in the game
        [ObservableProperty]
        private ResourcesViewModel _starsResourceViewModel = new(GameViewModelStatics.StarsTrackResourceList);

        [ObservableProperty]
        private ErrorMessage? _errorMessage;

        /// <summary>
        ///     this broadcasts to all things that are looking for the global orientation that causes things like Tiles and Harbors to flip
        /// </summary>
        /// <param name="value"></param>
        partial void OnOrientationChanged(CatanOrientation value)
        {
            Messenger.Send(new UpdateOrientation(value));
        }


        partial void OnCurrentPlayerChanged(PlayerViewModel? oldValue, PlayerViewModel newValue)
        {
            if (newValue is null) return;
            //   this.TraceMessage($"Current Player: {oldValue} -> {newValue}");
            this.GameModel.CurrentPlayerId = newValue.Id;
            Messenger.Send(new CurrentPlayerChanged(newValue));
        }

        partial void OnShownStarsChanged(int value)
        {
            foreach (var building in Buildings)
            {
                if (building.Building.OwnerId is not null) continue;
                if (building.Building.BuildingState == BuildingState.NotBuildable) continue;
                if (building.VisualState == BuildingVisualState.Highlighted) continue;

                if (building.Stars >= value)
                {
                    building.VisualState = BuildingVisualState.Stars;
                }
                else
                {
                    building.VisualState = BuildingVisualState.Hidden;
                }
            }
        }

    }
}
