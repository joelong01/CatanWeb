using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Text.Json.Serialization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Messaging;
using Catan3.Shared.Models;
using Catan3.Shared.Utility;
using Catan3.DesktopApp.Models;
using static Catan3.Models.TurnRollViewModel;
namespace Catan3.Models
{
    /// <summary>
    /// Contains static properties and methods for the GameViewModel.
    /// </summary>
    public static class GameViewModelStatics
    {
        /// <summary>
        /// List of resource types tracked by stars.
        /// </summary>
        public static ResourceType[] StarsTrackResourceList = [ResourceType.Sheep, ResourceType.Wheat, ResourceType.Wood, ResourceType.Brick, ResourceType.Ore];

        /// <summary>
        /// List of resource types tracked by players.
        /// </summary>
        public static ResourceType[] PlayerTrackResourceList = [..StarsTrackResourceList, ResourceType.GoldMine, ResourceType.Robber];
    }

    /// <summary>
    /// Represents the view model for the game, including various properties and collections related to the game state.
    /// </summary>
    public partial class GameViewModel : ObservableRecipient
    {
        /// <summary>
        /// Gets or sets the collection of purchasable entitlements.
        /// </summary>
        [ObservableProperty]
        public partial ObservableCollection<EntitlementPurchaseViewModel> PurchasableEntitlements { get; set; } = new();

        /// <summary>
        /// Gets or sets the ID of the game.
        /// </summary>
        [ObservableProperty]
        public partial string Id { get; set; }

        /// <summary>
        /// Gets or sets the board information.
        /// </summary>
        [ObservableProperty]
        public partial IDesktopGameMetadata? BoardInfo { get; set; }

        /// <summary>
        /// Gets or sets the view model for the robber.
        /// </summary>
        [ObservableProperty]
        public partial RobberViewModel RobberViewModel { get; set; } = new(new());


        /// <summary>
        /// Gets or sets the name of the game.
        /// </summary>
        [ObservableProperty]
        public partial string Name { get; set; } = "Nameless";

        /// <summary>
        /// Gets or sets a value indicating whether the game includes knights and robbers.
        /// </summary>
        [ObservableProperty]
        public partial bool IsKnightsAndRobbers { get; set; } = false;

        /// <summary>
        /// Gets or sets the current player.
        /// </summary>
        [ObservableProperty]
        public partial PlayerViewModel CurrentPlayer { get; set; } = PlayerViewModel.Default;

        /// <summary>
        /// Gets or sets the collection of tiles.
        /// </summary>
        [ObservableProperty]
        public partial ObservableCollection<TileViewModel> Tiles { get; set; } = new();

        /// <summary>
        /// Gets or sets the collection of buildings.
        /// </summary>
        [ObservableProperty]
        public partial ObservableCollection<BuildingViewModel> Buildings { get; set; } = new();

        /// <summary>
        /// Gets or sets the collection of players.
        /// </summary>
        [ObservableProperty]
        public partial ObservableCollection<PlayerViewModel> Players { get; set; } = new();

        /// <summary>
        /// Gets or sets the collection of supplemental players.
        /// </summary>
        [ObservableProperty]
        public partial ObservableCollection<PlayerViewModel> SupplementalPlayers { get; set; } = new();

        /// <summary>
        /// Gets or sets the collection of roads.
        /// </summary>
        [ObservableProperty]
        public partial ObservableCollection<RoadViewModel> Roads { get; set; } = new();

        /// <summary>
        /// Gets or sets the collection of harbors.
        /// </summary>
        [ObservableProperty]
        public partial ObservableCollection<HarborViewModel> Harbors { get; set; } = new();

        /// <summary>
        /// Gets or sets the game model.
        /// </summary>
        [ObservableProperty]
        public partial GameModel? GameModel { get; set; }

        /// <summary>
        /// Gets or sets the number of shown stars.
        /// </summary>
        [ObservableProperty]
        public partial int ShownStars { get; set; } = 13;

        /// <summary>
        /// Gets or sets the shuffle count.
        /// </summary>
        [ObservableProperty]
        public partial int ShuffleCount { get; set; } = 0;

        /// <summary>
        /// Gets or sets the orientation of the game.
        /// </summary>
        [ObservableProperty]
        public partial CatanOrientation Orientation { get; set; } = CatanOrientation.FaceUp;

        /// <summary>
        /// Gets or sets the view model for the rolls.
        /// </summary>
        [ObservableProperty]
        public partial RollViewModel RollViewModel { get; set; } = new();

        /// <summary>
        /// Gets or sets the total resources allocated for the game.
        /// </summary>
        [ObservableProperty]
        public partial ResourcesViewModel GameResources { get; set; } = new(GameViewModelStatics.PlayerTrackResourceList);

        /// <summary>
        /// Gets or sets the total stars for each resource type in the game.
        /// </summary>
        [ObservableProperty]
        public partial ResourcesViewModel StarsResourceViewModel { get; set; } = new(GameViewModelStatics.StarsTrackResourceList);

        /// <summary>
        /// Gets or sets the error message.
        /// </summary>
        [ObservableProperty]
        public partial ErrorMessage? ErrorMessage { get; set; }

        /// <summary>
        /// Gets or sets the query builder model.
        /// </summary>
        [ObservableProperty]
        public partial QueryBuilderModel QueryBuilder { get; set; } = new();

        /// <summary>
        /// Gets or sets the collection of valid rolls.
        /// </summary>
        [ObservableProperty]
        public partial ObservableCollection<UiRollModel> ValidRolls { get; set; } = new();

        /// <summary>
        /// Broadcasts the global orientation change to all listeners.
        /// </summary>
        /// <param name="value">The new orientation value.</param>
        partial void OnOrientationChanged(CatanOrientation value)
        {
            Messenger.Send(new UpdateOrientation(value));
        }

        /// <summary>
        /// Handles changes to the current player.
        /// </summary>
        /// <param name="oldValue">The previous player.</param>
        /// <param name="newValue">The new player.</param>
        partial void OnCurrentPlayerChanged(PlayerViewModel oldValue, PlayerViewModel newValue)
        {
            if (newValue is null) return;

            Messenger.Send(new CurrentPlayerChanged(newValue));
        }

        /// <summary>
        /// Handles changes to the number of shown stars.
        /// </summary>
        /// <param name="value">The new number of shown stars.</param>
        partial void OnShownStarsChanged(int value)
        {
            Debug.Assert(GameModel is not null);
            foreach (var building in Buildings)
            {
                if (building.Building.OwnerId is not null) continue;
                // During PickingBoard we want to show Stars even for NotBuildable locations
                if (building.Building.BuildingState == BuildingState.NotBuildable && GameModel.GameState != Shared.Models.GameState.PickingBoard) continue;
                if (building.VisualState == BuildingVisualState.Highlighted) continue;
                bool hasEntitlement = CurrentPlayer.Player.UnspentEntitlements.Contains(Entitlement.Settlement);
                //
                // I want the Stars to hide after the user builds their settlement in the allocation phase as 
                // showing them makes the game look too busy.  But we also want to do queries/look at Stars
                // when we pick the board.
                if (building.Stars >= value && ( hasEntitlement || GameModel.GameState == Shared.Models.GameState.PickingBoard ))
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
