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
    /// These are all the stats tracked in PlayerCtrl. We defined the enum so we don't mistype strings.
    /// </summary>
    public enum StatName
    {
        Score,
        RoadsPlayed,
        SettlementsPlayed,
        CitiesPlayed,
        SoldierPlayed,
        ResourcesLostToRobber,
        TimesTargeted,
        TotalResources,
        LongestRoad,
        GoodRolls,
        BadRolls,
        Stars
    }
    /// <summary>
    /// This is the class that we create an observable collection of instances that are then displayed 
    /// in the PlayerCtrl that show statistics about the player.
    /// </summary>
    public partial class PlayerStatsViewModel : ObservableRecipient
    {
        /// <summary>
        /// Initializes a new instance of the PlayerStatsViewModel class with the specified player ID, stat name, and glyph.
        /// </summary>
        /// <param name="playerId">The ID of the player.</param>
        /// <param name="name">The name of the stat.</param>
        /// <param name="glyph">The glyph representing the stat.</param>
        public PlayerStatsViewModel(string playerId, StatName name, string glyph)
        {
            Glyph = glyph;
            Name = name;
            PlayerId = playerId;
            RegisterMessages();
        }

        /// <summary>
        /// Registers messages for the view model.
        /// </summary>
        private void RegisterMessages()
        {
            Messenger.Register<PlayerColorChanged>(this, (recipient, message) =>
            {
                if (message.PlayerColors.PlayerId == "Nameless-001") return;
                if (message.PlayerColors.PlayerId == this.PlayerId)
                {
                    PlayerColors = message.PlayerColors;
                    OnPropertyChanged(nameof(Highlighted)); // this forces the rebinding of the StatsCtrl
                }
            });
        }

        /// <summary>
        /// Gets the name of the stat.
        /// </summary>
        public StatName Name { get; }

        /// <summary>
        /// Gets the ID of the player.
        /// </summary>
        public string PlayerId { get; }

        /// <summary>
        /// Gets or sets the count of the stat.
        /// </summary>
        [ObservableProperty]
        public partial int Count { get; set; } = 0;

        /// <summary>
        /// Gets or sets the glyph representing the stat.
        /// </summary>
        [ObservableProperty]
        public partial string Glyph { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether the stat is highlighted.
        /// </summary>
        [ObservableProperty]
        public partial bool Highlighted { get; set; } = false;

        /// <summary>
        /// Gets or sets the player colors.
        /// </summary>
        [ObservableProperty]
        public partial PlayerColorViewModel PlayerColors { get; set; } = new("Nameless-001", Colors.White, Colors.HotPink, Colors.HotPink);

        /// <summary>
        /// Initializes a new instance of the PlayerStatsViewModel class with the specified player ID, stat template, and player colors.
        /// </summary>
        /// <param name="playerId">The ID of the player.</param>
        /// <param name="template">The stat template.</param>
        /// <param name="playerColors">The player colors.</param>
        public PlayerStatsViewModel(string playerId, StatTemplate template, PlayerColorViewModel playerColors)
            : this(playerId, template.Name, template.Glyph)
        {
            PlayerColors = playerColors;
            PlayerId = playerId;
        }

        /// <summary>
        /// Gets the foreground brush based on whether the stat is highlighted.
        /// </summary>
        /// <param name="highlighted">A value indicating whether the stat is highlighted.</param>
        /// <returns>The foreground brush.</returns>
        public Brush GetForeground(bool highlighted)
        {
            return highlighted ? PlayerColors.BackgroundBrush : PlayerColors.ForegroundBrush;
        }

        /// <summary>
        /// Gets the background brush based on whether the stat is highlighted.
        /// </summary>
        /// <param name="highlighted">A value indicating whether the stat is highlighted.</param>
        /// <returns>The background brush.</returns>
        public Brush GetBackground(bool highlighted)
        {
            return highlighted ? PlayerColors.ForegroundBrush : PlayerColors.BackgroundBrush;
        }
    }
    /// <summary>
    /// Represents a template for a player stat, including the stat name and glyph.
    /// </summary>
    /// <remarks>
    /// Initializes a new instance of the StatTemplate class with the specified stat name and glyph.
    /// </remarks>
    /// <param name="name">The name of the stat.</param>
    /// <param name="glyph">The glyph representing the stat.</param>
    public class StatTemplate(StatName name, string glyph)
    {

        /// <summary>
        /// Gets the name of the stat.
        /// </summary>
        public StatName Name { get; } = name;

        /// <summary>
        /// Gets the glyph representing the stat.
        /// </summary>
        public string Glyph { get; } = glyph;

        /// <summary>
        /// Gets the list of player stats templates.
        /// </summary>
        public static List<StatTemplate> PlayerStats { get; } = new()
            {
                new StatTemplate(StatName.Score, CatanFont.Score),
                new StatTemplate(StatName.RoadsPlayed, CatanFont.Road),
                new StatTemplate(StatName.CitiesPlayed, CatanFont.City),
                new StatTemplate(StatName.SettlementsPlayed, CatanFont.Settlement),
                new StatTemplate(StatName.SoldierPlayed, CatanFont.Soldier),
                new StatTemplate(StatName.ResourcesLostToRobber, CatanFont.Pirate),
                new StatTemplate(StatName.TimesTargeted, CatanFont.Target),
                new StatTemplate(StatName.TotalResources, CatanFont.Sum),
                new StatTemplate(StatName.LongestRoad, CatanFont.LongestRoad),
                new StatTemplate(StatName.GoodRolls, CatanFont.GoodRoll),
                new StatTemplate(StatName.BadRolls, CatanFont.BadRoll),
                new StatTemplate(StatName.Stars, CatanFont.Star),
            };
    }
}
