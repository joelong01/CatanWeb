using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Text.Json.Serialization;
using Catan3.Utility;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using Windows.UI;
using Windows.UI.Core;

namespace Catan3.Models
{




    //
    //  this has all the data about the player that the service doesn't care about
    //  e.g. how to display information about the player -- colors, picutre, etc.
    public partial class PlayerViewModel : ObservableObject
    {


        [ObservableProperty]
        private string _id = string.Empty;

        [ObservableProperty]
        private string _name = "Nameless";

        [ObservableProperty]
        private PlayerColorViewModel _playerColors;

        [ObservableProperty]
        private string _imageUri = "ms-appx:///Assets/guest.jpg";

        [JsonIgnore]
        [ObservableProperty]
        BitmapImage _imageSource;

        [JsonIgnore]
        [ObservableProperty]
        private PlayerModel _player = PlayerModel.Default;

        [ObservableProperty]
        private ResourcesViewModel _resourcesThisTurn = new(GameViewModelStatics.PlayerTrackResourceList);

        [ObservableProperty]
        private ResourcesViewModel _resourcesThisGame = new(GameViewModelStatics.PlayerTrackResourceList);

        [ObservableProperty]
        private ObservableCollection<PlayerStatsViewModel> _playerStats = [];

        public Dictionary<StatName, PlayerStatsViewModel> StatDictionary { get; } = [];

        public PlayerViewModel() : this("Nameless", "ms-appx:///Assets/guest.jpg", new PlayerColorViewModel(Colors.White, Colors.HotPink, Colors.HotPink))
        {
            ImageSource = new BitmapImage(new System.Uri(ImageUri));

        }

        partial void OnImageUriChanging(string? oldValue, string newValue)
        {
            ImageSource = new BitmapImage(new System.Uri(newValue));
        }



        [JsonIgnore]
        public static PlayerViewModel Default { get; } = new();

        public PlayerViewModel(string name, string imageUri, PlayerColorViewModel playerColors)
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
            ImageSource = new BitmapImage(new System.Uri(ImageUri));
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
}
