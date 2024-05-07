using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Text.Json.Serialization;
using Catan3.Utility;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.UI;
using Microsoft.UI.Xaml.Media;
using Windows.UI;
using Windows.UI.Core;

namespace Catan3.Models
{
    public enum StatName
    {
        Score,
        RoadsPlayed,
        SettlementsPlayed,
        CitiesPlayed,
        SoldierPlayed,
        ResourcesLostToRobber,
        TimesTargetted,
        TotalResources
    }



    //
    //  this has all the data about the player that the service doesn't care about
    //  e.g. how to display information about the player -- colors, picutre, etc.
    public partial class PlayerViewModel : ObservableObject
    {

        private static List<(StatName statName, string glyph)> Stats = [
            (StatName.Score, CatanFont.Score),
            (StatName.RoadsPlayed, CatanFont.Road) ,
            (StatName.CitiesPlayed, CatanFont.City) ,
            (StatName.SettlementsPlayed, CatanFont.Settlement),
            (StatName.SoldierPlayed, CatanFont.Knight),
            (StatName.ResourcesLostToRobber, CatanFont.Pirate),
            (StatName.TimesTargetted, CatanFont.Target) ,
            (StatName.TotalResources, CatanFont.Sum)

            ];

        [ObservableProperty]
        private string _id = string.Empty;

        [ObservableProperty]
        private string _name = "Nameless";

        [ObservableProperty]
        private Color _foreground = Colors.White;

        [ObservableProperty]
        private Color _background = Colors.HotPink;

        [ObservableProperty]
        private string _imageFileName = "ms-appx:///Assets/guest.jpg";
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

        public PlayerViewModel() : this("Nameless", Colors.White, Colors.HotPink)
        {


        }


        [JsonIgnore]
        public static PlayerViewModel Default { get; } = new();

        public PlayerViewModel(string name, Color foreground, Color background)
        {
            Name = name;
            Foreground = foreground;
            Background = background;
            Id = name + "-0001";

            foreach (var stat in Stats)
            {
                StatDictionary[stat.statName] = new PlayerStatsViewModel(stat.glyph);
            }

           

            PlayerStats.AddRange([.. StatDictionary.Values]);
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

        public Brush ForegroundBrush => GetBrush(this.Foreground, true);
        public Brush BackgroundBrush => GetBrush(this.Background, false);

        [RelayCommand]
        private void PurchaseEntitlement(Entitlement entitlement)
        {
            Debug.Assert(false);
        }


    }
}
