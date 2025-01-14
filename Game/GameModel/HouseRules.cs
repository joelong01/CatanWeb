
using CommunityToolkit.Mvvm.ComponentModel;
namespace Catan3.Models
{
    /// <summary>
    /// Represents the house rules for the game, including various settings and options.
    /// </summary>
    public partial class HouseRules : ObservableObject
    {
        /// <summary>
        /// Gets or sets the number of gold tiles.
        /// </summary>
        [ObservableProperty]
        public partial int GoldTiles { get; set; } = 1;

        /// <summary>
        /// Gets or sets a value indicating whether walls protect cities.
        /// </summary>
        [ObservableProperty]
        public partial bool WallsProtectCities { get; set; } = true;

        /// <summary>
        /// Gets or sets a value indicating whether to hide the baron before invasion.
        /// </summary>
        [ObservableProperty]
        public partial bool HideBaronBeforeInvasion { get; set; } = false;

        /// <summary>
        /// Gets or sets a value indicating whether a knight moves the baron before the roll.
        /// </summary>
        [ObservableProperty]
        public partial bool KnightMovesBaronBeforeRoll { get; set; } = true;

        /// <summary>
        /// Gets or sets a value indicating whether to hide the robber before invasion.
        /// </summary>
        [ObservableProperty]
        public partial bool HideRobberBeforeInvasion { get; set; } = false;

        /// <summary>
        /// Gets or sets a value indicating whether a knight moves the robber before the roll.
        /// </summary>
        [ObservableProperty]
        public partial bool KnightMovesRobberBeforeRoll { get; set; } = false;
    }
}
