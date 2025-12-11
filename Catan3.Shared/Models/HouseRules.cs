namespace Catan3.Shared.Models
{
    /// <summary>
    /// Represents the house rules for the game, including various settings and options.
    /// </summary>
    public class HouseRules
    {
        /// <summary>
        /// Gets or sets the number of gold tiles.
        /// </summary>
        public int GoldTiles { get; set; } = 1;

        /// <summary>
        /// Gets or sets a value indicating whether walls protect cities.
        /// </summary>
        public bool WallsProtectCities { get; set; } = true;

        /// <summary>
        /// Gets or sets a value indicating whether to hide the baron before invasion.
        /// </summary>
        public bool HideBaronBeforeInvasion { get; set; } = false;

        /// <summary>
        /// Gets or sets a value indicating whether a knight moves the baron before the roll.
        /// </summary>
        public bool KnightMovesBaronBeforeRoll { get; set; } = true;

        /// <summary>
        /// Gets or sets a value indicating whether to hide the robber before invasion.
        /// </summary>
        public bool HideRobberBeforeInvasion { get; set; } = false;

        /// <summary>
        /// Gets or sets a value indicating whether a knight moves the robber before the roll.
        /// </summary>
        public bool KnightMovesRobberBeforeRoll { get; set; } = false;

        /// <summary>
        /// Gets or sets the minimum number of players required for supplemental build phase.
        /// Default is 5 (standard Catan expansion rule).
        /// </summary>
        public int SupplementalMinPlayers { get; set; } = 5;

        /// <summary>
        /// Gets or sets a value indicating whether "Grief Dodgy" mode is enabled.
        /// When enabled, adds special animations targeting the player with ID "Dodgy-001"
        /// during robber moves: non-Dodgy buildings fade, celebration when Dodgy is targeted,
        /// and fake-out animation when Dodgy is not targeted.
        /// </summary>
        public bool GriefDodgy { get; set; } = true;

        /// <summary>
        /// Gets the default house rules.
        /// </summary>
        public static HouseRules Default { get; } = new HouseRules();
    }
}