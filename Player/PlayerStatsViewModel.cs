using System.Collections.Generic;
using Catan3.Utility;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Messaging;
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
    public partial class PlayerStatsViewModel : ObservableRecipient
    {
        public PlayerStatsViewModel(string playerId, StatName name,
                                              string glyph,
                                              HorizontalAlignment horizontalAlignment = HorizontalAlignment.Left,
                                              VerticalAlignment verticalAlignment = VerticalAlignment.Top) : base()
        {
            HorizontalAlignment = horizontalAlignment;
            VerticalAlignment = verticalAlignment;
            Glyph = glyph;
            Name = name;
            PlayerId = playerId;
            RegisterMessages();
        }
        private void RegisterMessages()
        {
            Messenger.Register<PlayerColorChanged>(this, (recipient, message) =>
            {
                if (message.PlayerColors.PlayerId == "Nameless-001") return;
                if (message.PlayerColors.PlayerId == this.PlayerId)
                {
                  //  this.TraceMessage($"updating colors for {message.PlayerColors.PlayerId}");
                    PlayerColors = message.PlayerColors;
                    OnPropertyChanged(nameof(Highlighted)); // this forces the rebinding of the StatsCtrl
                }
            });
        }
        public StatName Name { get; }
        public string PlayerId { get; }
        [ObservableProperty]
        private int _count = 0;
        [ObservableProperty]
        private string _glyph;
        [ObservableProperty]
        private bool _highlighted = false;
        [ObservableProperty]
        private PlayerColorViewModel _playerColors = new("Nameless-001", Colors.White, Colors.HotPink, Colors.HotPink);
        [ObservableProperty]
        private HorizontalAlignment _horizontalAlignment ;
        [ObservableProperty]
        private VerticalAlignment _verticalAlignment;
        public PlayerStatsViewModel(string playerId, StatTemplate template, PlayerColorViewModel playerColors) : 
                                    this(playerId, template.Name, template.Glyph, template.HorizontalAlignment, template.VerticalAlignment)
        {
            PlayerColors = playerColors;
            PlayerId = playerId;
        }
        public Brush GetForeground(bool highlighted)
        {
            return highlighted ? PlayerColors.BackgroundBrush : PlayerColors.ForegroundBrush;
        }
        public Brush GetBackground(bool highlighted)
        {
            return highlighted ? PlayerColors.ForegroundBrush : PlayerColors.BackgroundBrush;
        }
    }
    public class StatTemplate(StatName name, string glyph, HorizontalAlignment horizontalAlignment = HorizontalAlignment.Left,
                                              VerticalAlignment verticalAlignment = VerticalAlignment.Top)
    {
        public StatName Name { get; } = name;
        public string Glyph { get; } = glyph;
        public HorizontalAlignment HorizontalAlignment { get; } = horizontalAlignment;
        public VerticalAlignment VerticalAlignment { get; } = verticalAlignment;
        /// <summary>
        ///     We give the PlayerStatsViewModel a PlayerId so it can recieve the PlaorColorsChanged message and update colors when
        ///     the owner's colors change.  This collection just has the list of stats we track in the UI. if we add a new one, add
        ///     it here.  the PlayerId must be updated when the actual stat is created.
        /// </summary>
        public static List<StatTemplate> PlayerStats { get; } = [
            new StatTemplate(StatName.Score, CatanFont.Score,  HorizontalAlignment.Center, VerticalAlignment.Center),
            new StatTemplate(StatName.RoadsPlayed, CatanFont.Road) ,
            new StatTemplate(StatName.CitiesPlayed, CatanFont.City) ,
            new StatTemplate(StatName.SettlementsPlayed, CatanFont.Settlement),
            new StatTemplate(StatName.SoldierPlayed, CatanFont.Soldier),
            new StatTemplate(StatName.ResourcesLostToRobber, CatanFont.Pirate),
            new StatTemplate(StatName.TimesTargetted, CatanFont.Target) ,
            new StatTemplate(StatName.TotalResources, CatanFont.Sum, HorizontalAlignment.Left, VerticalAlignment.Center),
            new StatTemplate(StatName.LongestRoad, CatanFont.LongestRoad) ,
            new StatTemplate(StatName.GoodRolls, CatanFont.GoodRoll) ,
            new StatTemplate(StatName.BadRolls, CatanFont.BadRoll) ,
            new StatTemplate(StatName.Stars, CatanFont.Star) ,
            ];
    }
}
