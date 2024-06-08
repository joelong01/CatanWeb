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
        public static PlayerViewModel Default { get; } = new();

        public PlayerViewModel() : this("Nameless", "ms-appx:///Assets/guest.jpg", "ms-appx:///Assets/guest.jpg", new PlayerColorViewModel(Colors.White, Colors.HotPink, Colors.HotPink))
        {
          
          
        }


        public void InitializeAfterDeserialization()
        {
            PlayerStats.Clear();
            foreach (var stat in PlayerStatsViewModel.StatsTemplate)
            {
                var vm = new PlayerStatsViewModel(stat, PlayerColors);
                StatDictionary[stat.Name] = vm;
                PlayerStats.Add(vm);
            }

        }
       
        
        public PlayerViewModel(string name, string imageUri, string croppedImageUri,  PlayerColorViewModel playerColors)
        {
            Name = name;
            Id = name + "-0001";
            //
            //  each view model needs its own instance of the stats - we put into a dictionary
            //  to make it easy to update them.
            foreach (var stat in PlayerStatsViewModel.StatsTemplate)
            {
                StatDictionary[stat.Name] = new PlayerStatsViewModel(stat, playerColors);
            }
            //
            //  the list of stats to bind to
            PlayerStats.AddRange([.. StatDictionary.Values.ToList()]);
            //
            //  set these last so that the PlayerStats get their colors
            this.PlayerColors = playerColors;
            ImageUri = imageUri;
            CroppedImageUri = croppedImageUri;
          
            //
            // subscribe to color changes to notify the rest of the models
            playerColors.PropertyChanged += PlayerColors_PropertyChanged;
        }
        private void PlayerColors_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            switch (e.PropertyName)
            {
                case "BackgroundBrush":
                case "ForegroundBrush":
                    Messenger.Send(new PlayerColorChanged(this));
                    break;
                default:
                    break;
            }
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

        internal void ReloadCroppedImage()
        {

            var uri = CroppedImageUri;
            CroppedImageUri = "ms-appx:///Assets/guest.jpg";
            CroppedImageUri = uri;
         // OnPropertyChanged(nameof(CroppedImageUri));
        }
    }
    public enum ColorName { PrimaryBackground, SecondaryBackground, Foreground }
    /// <summary>
    ///     this keeps the player's colors/brushes straight.
    ///     This is serializable
    /// </summary>
    public partial class PlayerColorViewModel : ObservableObject
    {
       
        public PlayerColorViewModel(Color foreground, Color primaryBackground, Color secondaryBackground)
        {
            _primaryBackground = primaryBackground;
            _secondaryBackground = secondaryBackground;
            _foreground = foreground;
            OnPrimaryBackgroundChanged(primaryBackground);
            OnSecondaryBackgroundChanged(secondaryBackground);
            OnForegroundChanged(foreground);
        }
        [ObservableProperty]
        private Color _primaryBackground;
        [ObservableProperty]
        private Color _secondaryBackground;
        [ObservableProperty]
        private Color _foreground;
       
       
        [ObservableProperty]
        [property: JsonIgnore]
        private Brush _foregroundBrush = BrushCache.GetSolidColorBrush(Colors.White);
        

        [ObservableProperty]
        [property: JsonIgnore]
        private Brush _backgroundBrush = BrushCache.GetSolidColorBrush(Colors.Black);
        
        partial void OnPrimaryBackgroundChanged(Color value)
        {
            BackgroundBrush = BrushCache.GetGradientBrush(value, SecondaryBackground);
        }
        partial void OnSecondaryBackgroundChanged(Color value)
        {
            BackgroundBrush = BrushCache.GetGradientBrush(PrimaryBackground, value);
        }
        partial void OnForegroundChanged(Color value)
        {
            ForegroundBrush = BrushCache.GetSolidColorBrush(value);
        }

        internal void Initialize()
        {
            OnPropertyChanged(nameof(PrimaryBackground));
            OnPropertyChanged(nameof(SecondaryBackground));
            OnPropertyChanged(nameof(ForegroundBrush));
            OnPrimaryBackgroundChanged(PrimaryBackground);
            OnSecondaryBackgroundChanged(SecondaryBackground);
            OnForegroundChanged(Foreground);
        }
    }
}
