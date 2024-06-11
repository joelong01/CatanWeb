using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Text.Json.Serialization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Messaging;
using Microsoft.UI;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using Windows.UI;
namespace Catan3.Models
{
    //
    //  this has all the data about the player that the service doesn't care about
    //  e.g. how to display information about the player -- colors, picutre, etc.
    public partial class PlayerViewModel : ObservableRecipient
    {
        [ObservableProperty]
        private string _id = string.Empty;
        [ObservableProperty]
        private string _name = "Nameless";
        [ObservableProperty]
        private PlayerColorViewModel _playerColors;
        [ObservableProperty]
        private string _imageUri = "ms-appx:///Assets/guest.jpg";
        [ObservableProperty]
        private string _croppedImageUri ="ms-appx:///Assets/guest.jpg";




        [property: JsonIgnore]
        [ObservableProperty]
        private bool _selected = false;

        [property: JsonIgnore]
        [ObservableProperty]
        private PlayerModel _player = PlayerModel.Default;

        [property: JsonIgnore]
        [ObservableProperty]
        private ResourcesViewModel _resourcesThisTurn = new(GameViewModelStatics.PlayerTrackResourceList);

        [property: JsonIgnore]
        [ObservableProperty]
        private ResourcesViewModel _resourcesThisGame = new(GameViewModelStatics.PlayerTrackResourceList);

        [property: JsonIgnore]
        [ObservableProperty]
        private ObservableCollection<PlayerStatsViewModel> _playerStats = [];

        [JsonIgnore]
        public Dictionary<StatName, PlayerStatsViewModel> StatDictionary { get; } = [];

        [JsonIgnore]
        public static PlayerViewModel Default { get; } = new("Nameless-001", "Nameless", "ms-appx:///Assets/guest.jpg", "ms-appx:///Assets/guest.jpg", Colors.HotPink);




        public void InitializeAfterDeserialization()
        {

        }
        /// <summary>
        ///     thisis the ctor that the JsonSerializer should use when it deserializes the saved player state.
        /// </summary>
        [JsonConstructor]
        public PlayerViewModel(string id, string name, string imageUri, string croppedImageUri, PlayerColorViewModel playerColors, bool isActive=false)
        {
            Id = id;
            Name = name;
            ImageUri = imageUri;
            CroppedImageUri = croppedImageUri;
            PlayerColors = playerColors;
            IsActive = isActive;
            CreateStats();
        }

        public PlayerViewModel(string id, string name, string imageUri, string croppedImageUri, Color primaryBackground) : 
                this(id, name, imageUri, croppedImageUri, new PlayerColorViewModel(id, Colors.White, primaryBackground, Colors.Black)) { }
       

        private void CreateStats()
        {
            foreach (var stat in StatTemplate.PlayerStats)
            {
                StatDictionary[stat.Name] = new PlayerStatsViewModel(Id, stat, PlayerColors);
            }
            //
            //  the list of stats to bind to
            PlayerStats.AddRange([.. StatDictionary.Values.ToList()]);
        }

        /// <summary>
        ///     the MVVM notification when the model gets updated -- we set the per person data and update the stats
        /// </summary>
        /// <param name="value"></param>
        partial void OnPlayerChanged(PlayerModel value)
        {
            ResourcesThisTurn.ResourceModel = value.ResourcesThisTurn;
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
            StatDictionary[StatName.TotalResources].Count = value.ResourcesThisGame.Count;
            StatDictionary[StatName.GoodRolls].Count = value.GoodRolls;
            StatDictionary[StatName.BadRolls].Count = value.BadRolls;
            StatDictionary[StatName.Stars].Count = value.Stars;
            StatDictionary[StatName.LongestRoad].Count = value.LongestRoad;
        }
        public override string ToString()
        {
            return $"{Name}";
        }
        public Brush GetBrush(Color color, bool foreground)
        {
            if (foreground)
                return BrushCache.GetSolidColorBrush(color);
            else
                return BrushCache.GetGradientBrush(color, Colors.Black);
        }

        
    }
    public enum ColorName { PrimaryBackground, SecondaryBackground, Foreground }

}
