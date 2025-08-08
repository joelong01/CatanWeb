using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text.Json.Serialization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using Windows.UI;
using Catan3.Shared.Models;
using Catan3.Shared.Utility;

namespace Catan3.Models
{
    /// <summary>
    /// Represents the view model for a player, including player details, colors, images, and resources.
    /// </summary>
    public partial class PlayerViewModel : ObservableRecipient
    {
        /// <summary>
        /// Gets or sets the player ID.
        /// </summary>
        [ObservableProperty]
        public partial string Id { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the player name.
        /// </summary>
        [ObservableProperty]
        public partial string Name { get; set; } = "Nameless";

        /// <summary>
        /// Gets or sets the player colors.
        /// </summary>
        [ObservableProperty]
        public partial PlayerColorViewModel PlayerColors { get; set; }

        /// <summary>
        /// Gets or sets the image URI.
        /// </summary>
        [ObservableProperty]
        public partial string ImageUri { get; set; } = PlayerDatabase.DefaultImageUri;

        /// <summary>
        /// Gets or sets the cropped image URI.
        /// </summary>
        [ObservableProperty]
        public partial string CroppedImageUri { get; set; } = PlayerDatabase.DefaultImageUri;

        /// <summary>
        /// Gets or sets a value indicating whether the player is participating in the supplemental phase.
        /// </summary>
        [ObservableProperty]
        public partial bool PartipatingInSupplemental { get; set; } = false;

        partial void OnPartipatingInSupplementalChanged(bool oldValue, bool newValue)
        {
          // this.TraceMessage($"Player {Name} is {(newValue ? "" : "not ")}participating in the supplemental phase.");
        }

        /// <summary>
        /// Gets or sets a value indicating whether the player is the current player.
        /// </summary>
        [ObservableProperty]
        public partial bool IsCurrentPlayer { get; set; } = false;

        /// <summary>
        /// Gets or sets the cropped bitmap image. This property is not serialized.
        /// </summary>
        [ObservableProperty]
        [property: JsonIgnore]
        public partial BitmapImage CroppedBitmapImage { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether the player is selected. This property is not serialized.
        /// </summary>
        [ObservableProperty]
        [property: JsonIgnore]
        public partial bool Selected { get; set; } = false;

        /// <summary>
        /// Gets or sets the player model. This property is not serialized.
        /// </summary>
        [ObservableProperty]
        [property: JsonIgnore]
        public partial PlayerModel Player { get; set; } = new PlayerModel();

        /// <summary>
        /// Gets or sets the resources for this turn. This property is not serialized.
        /// </summary>
        [ObservableProperty]
        [property: JsonIgnore]
        public partial ResourcesViewModel ResourcesThisTurn { get; set; } = new(GameViewModelStatics.PlayerTrackResourceList);

        /// <summary>
        /// Gets or sets the resources for this game. This property is not serialized.
        /// </summary>
        [ObservableProperty]
        [property: JsonIgnore]
        public partial ResourcesViewModel ResourcesThisGame { get; set; } = new(GameViewModelStatics.PlayerTrackResourceList);

        /// <summary>
        /// Gets or sets the player stats. This property is not serialized.
        /// </summary>
        [ObservableProperty]
        [property: JsonIgnore]
        public partial ObservableCollection<PlayerStatsViewModel> PlayerStats { get; set; } = new();

        /// <summary>
        /// Gets the dictionary of player stats.
        /// </summary>
        [JsonIgnore]
        public Dictionary<StatName, PlayerStatsViewModel> StatDictionary { get; } = new();

        /// <summary>
        /// Gets the default instance of the PlayerViewModel class.
        /// </summary>
        [JsonIgnore]
        public static PlayerViewModel Default { get; } = new("Nameless-001", "Nameless", PlayerDatabase.DefaultImageUri, PlayerDatabase.DefaultImageUri, Colors.HotPink);

        /// <summary>
        /// Handles changes to the cropped image URI.
        /// </summary>
        /// <param name="oldValue">The old cropped image URI.</param>
        /// <param name="newValue">The new cropped image URI.</param>
        partial void OnCroppedImageUriChanged(string oldValue, string newValue)
        {
            if (newValue is not null)
            {
                CroppedBitmapImage = CreateBitmapImage(newValue);
            }
        }

        /// <summary>
        /// Determines whether the player can participate in the supplemental phase.
        /// </summary>
        /// <param name="isCurrentPlayer">A value indicating whether the player is the current player.</param>
        /// <returns>True if the player can participate in the supplemental phase; otherwise, false.</returns>
        public bool CanParticipateInSupplemental(bool isCurrentPlayer)
        {
            return !isCurrentPlayer;
        }

        /// <summary>
        /// Creates a BitmapImage from the specified URI.
        /// </summary>
        /// <param name="uri">The URI of the image.</param>
        /// <returns>A BitmapImage created from the specified URI.</returns>
        private BitmapImage CreateBitmapImage(string uri)
        {
            return new BitmapImage
            {
                UriSource = new Uri(uri),
                DecodePixelHeight = 200,
                DecodePixelWidth = 200,
                CreateOptions = BitmapCreateOptions.IgnoreImageCache
            };
        }

        /// <summary>
        /// Initializes the player view model after deserialization.
        /// </summary>
        public void InitializeAfterDeserialization()
        {
        }

        /// <summary>
        /// Initializes a new instance of the PlayerViewModel class with the specified parameters.
        /// This constructor is used by the JsonSerializer when deserializing the saved player state.
        /// </summary>
        /// <param name="id">The player ID.</param>
        /// <param name="name">The player name.</param>
        /// <param name="imageUri">The image URI.</param>
        /// <param name="croppedImageUri">The cropped image URI.</param>
        /// <param name="playerColors">The player colors.</param>
        /// <param name="isActive">A value indicating whether the player is active.</param>
        [JsonConstructor]
        public PlayerViewModel(string id, string name, string imageUri, string croppedImageUri, PlayerColorViewModel playerColors, bool isActive = false)
        {
            Id = id;
            Name = name;
            ImageUri = imageUri;
            CroppedImageUri = croppedImageUri;
            PlayerColors = playerColors;
            IsActive = isActive;
            CroppedBitmapImage = CreateBitmapImage(CroppedImageUri);
            CreateStats();
        }

        /// <summary>
        /// Initializes a new instance of the PlayerViewModel class with the specified parameters.
        /// </summary>
        /// <param name="id">The player ID.</param>
        /// <param name="name">The player name.</param>
        /// <param name="imageUri">The image URI.</param>
        /// <param name="croppedImageUri">The cropped image URI.</param>
        /// <param name="primaryBackground">The primary background color.</param>
        public PlayerViewModel(string id, string name, string imageUri, string croppedImageUri, Color primaryBackground) :
                this(id, name, imageUri, croppedImageUri, new PlayerColorViewModel(id, Colors.White, primaryBackground, Colors.Black))
        { }

        /// <summary>
        /// Creates the player stats.
        /// </summary>
        private void CreateStats()
        {
            foreach (var stat in StatTemplate.PlayerStats)
            {
                StatDictionary[stat.Name] = new PlayerStatsViewModel(Id, stat, PlayerColors);
            }
            // the list of stats to bind to
            foreach (var stat in StatDictionary.Values)
            {
                PlayerStats.Add(stat);
            }
        }

        /// <summary>
        /// Handles changes to the player model.
        /// </summary>
        /// <param name="value">The new player model value.</param>
        partial void OnPlayerChanged(PlayerModel value)
        {
            ResourcesThisTurn.ResourceModel = value.ResourcesThisTurn;

            foreach (var rcvm in ResourcesThisTurn.ResourceCounters)
            {
                rcvm.HarborVisibility = Visibility.Collapsed;
                rcvm.OwnerPlayerId = value.Id;
            }

            foreach (var harborKey in value.OwnedHarbors)
            {
                var resource = harborKey.HarborType.ToResourceType();
                if (resource != ResourceType.None)
                {
                    var rcvm = ResourcesThisTurn.ResourceCounters.FirstOrDefault(r => r.Resource == resource);
                    if (rcvm != null)
                    {
                        this.TraceMessage($"Setting {resource} harbor to visible for {value}");
                        rcvm.HarborVisibility = Visibility.Visible;
                    }
                }
            }

            ResourcesThisGame.ResourceModel = value.ResourcesThisGame;
            StatDictionary[StatName.Score].Count = value.Score;
            StatDictionary[StatName.Score].Highlighted = value.HighestScore;
            StatDictionary[StatName.RoadsPlayed].Count = value.SpentEntitlementsThisGame.Count(e => e == Entitlement.Road);
            StatDictionary[StatName.CitiesPlayed].Count = value.SpentEntitlementsThisGame.Count(e => e == Entitlement.City);
            StatDictionary[StatName.SettlementsPlayed].Count = value.SpentEntitlementsThisGame.Count(e => e == Entitlement.Settlement);
            StatDictionary[StatName.SoldierPlayed].Count = value.SpentEntitlementsThisGame.Count(e => e == Entitlement.Soldier);
            StatDictionary[StatName.SoldierPlayed].Highlighted = value.LargestArmy;
            StatDictionary[StatName.ResourcesLostToRobber].Count = value.ResourcesThisGame.Robber;
            StatDictionary[StatName.TimesTargetted].Count = value.TimesTargeted;
            // 1/15/2025: do not count resources lost to robber in total resources
            StatDictionary[StatName.TotalResources].Count = value.ResourcesThisGame.Count - value.ResourcesThisGame.Robber;
            StatDictionary[StatName.GoodRolls].Count = value.GoodRolls;
            StatDictionary[StatName.BadRolls].Count = value.BadRolls;
            StatDictionary[StatName.Stars].Count = value.Stars;
            StatDictionary[StatName.LongestRoad].Count = value.LongestRoad;
        }

        /// <summary>
        /// Returns a string representation of the player view model.
        /// </summary>
        /// <returns>A string representing the player view model.</returns>
        public override string ToString()
        {
            return $"{Name}";
        }

        /// <summary>
        /// Gets a brush for the specified color.
        /// </summary>
        /// <param name="color">The color.</param>
        /// <param name="foreground">A value indicating whether the brush is for the foreground.</param>
        /// <returns>A Brush representing the specified color.</returns>
        public Brush GetBrush(Color color, bool foreground)
        {
            if (foreground)
                return BrushCache.GetSolidColorBrush(color);
            else
                return BrushCache.GetGradientBrush(color, Colors.Black);
        }

        /// <summary>
        /// Sends a message that makes this player go first in the game. Preserves the order of players.
        /// </summary>
        [RelayCommand]
        [property: JsonIgnore]
        void GoFirst()
        {
            WeakReferenceMessenger.Default.Send(new GoFirstMessage(this.Id));
        }
    }
    /// <summary>
    /// Represents the names of colors used in the player view model.
    /// </summary>
    public enum ColorName { PrimaryBackground, SecondaryBackground, Foreground }
}
