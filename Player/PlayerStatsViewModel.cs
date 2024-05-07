using System.Collections.Generic;
using System.Security;
using Catan3.Utility;
using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;

namespace Catan3.Models
{
    /// <summary>
    ///     These are all the stats tracked in PlayerCtrl.  We defined the enum so we don't misstype strings.
    /// </summary>
    public enum StatName
    {
        Score,
        RoadsPlayed,
        SettlementsPlayed,
        CitiesPlayed,
        SoldierPlayed,
        ResourcesLostToRobber,
        TimesTargetted,
        TotalResources,
        LongestRoad,
        GoodRolls,
        BadRolls,
        Stars
    }


    /// <summary>
    ///     this is the class that we create an observable collection of instances that are then displayed 
    ///     in the PlayerCtrl that show statistics about the player
    /// </summary>
    public partial class PlayerStatsViewModel(StatName name,
                                              string glyph,
                                              HorizontalAlignment horizontalAlignment = HorizontalAlignment.Left,
                                              VerticalAlignment verticalAlignment = VerticalAlignment.Top) : ObservableObject
    {
        public StatName Name { get; } = name;

        [ObservableProperty]
        private int _count = 0;
        [ObservableProperty]
        private string _glyph = glyph;
        [ObservableProperty]
        private bool _highlighted = false;

        [ObservableProperty]
        private Brush _foreground = BrushCache.GetSolidColorBrush(Colors.White);

        [ObservableProperty]
        private Brush _background = BrushCache.GetSolidColorBrush(Colors.Black);

        [ObservableProperty]
        private HorizontalAlignment _horizontalAlignment = horizontalAlignment;
        [ObservableProperty]
        private VerticalAlignment _verticalAlignment = verticalAlignment;

        public PlayerStatsViewModel(PlayerStatsViewModel model) : this(model.Name, model.Glyph, model.HorizontalAlignment, model.VerticalAlignment) { }

        public Brush GetForeground(bool highlighted)
        {
            return highlighted ? Background : Foreground;
        }

        public Brush GetBackground(bool highlighted)
        {
            return highlighted ? Foreground : Background;
        }


        public static List<PlayerStatsViewModel> StatsTemplate { get; } = [

            new PlayerStatsViewModel(StatName.Score, CatanFont.Score, HorizontalAlignment.Center, VerticalAlignment.Center),
            new PlayerStatsViewModel(StatName.RoadsPlayed, CatanFont.Road) ,
            new PlayerStatsViewModel(StatName.CitiesPlayed, CatanFont.City) ,
            new PlayerStatsViewModel(StatName.SettlementsPlayed, CatanFont.Settlement),
            new PlayerStatsViewModel(StatName.SoldierPlayed, CatanFont.Soldier),
            new PlayerStatsViewModel(StatName.ResourcesLostToRobber, CatanFont.Pirate),
            new PlayerStatsViewModel(StatName.TimesTargetted, CatanFont.Target) ,
            new PlayerStatsViewModel(StatName.TotalResources, CatanFont.Sum, HorizontalAlignment.Left, VerticalAlignment.Center),
            new PlayerStatsViewModel(StatName.LongestRoad, CatanFont.LongestRoad) ,
            new PlayerStatsViewModel(StatName.GoodRolls, CatanFont.GoodRoll) ,
            new PlayerStatsViewModel(StatName.BadRolls, CatanFont.BadRoll) ,
              new PlayerStatsViewModel(StatName.Stars, CatanFont.Star) ,
            ];

    }
}
