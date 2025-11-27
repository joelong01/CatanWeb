namespace Catan3.Shared.ViewData
{
    /// <summary>
    /// Player profile data for UI/display concerns.
    /// Separate from PlayerModel (game state) - this is for colors, images, identity.
    /// Used by GameService API and consumed by WebUI/Desktop clients.
    /// Will be stored in CosmosDB as player profiles.
    /// </summary>
    public class PlayerProfile
    {
        /// <summary>
        /// Unique identifier for the player
        /// </summary>
        public string Id { get; set; } = string.Empty;

        /// <summary>
        /// Display name of the player
        /// </summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// Primary background color for gradient (e.g., "#0000FF")
        /// </summary>
        public string PrimaryBackgroundColor { get; set; } = "#FFFFFF";

        /// <summary>
        /// Secondary background color for gradient (e.g., "#000080")
        /// </summary>
        public string SecondaryBackgroundColor { get; set; } = "#CCCCCC";

        /// <summary>
        /// Foreground/text color as hex string (e.g., "#FFFFFF")
        /// </summary>
        public string ForegroundColor { get; set; } = "#000000";

        /// <summary>
        /// Relative URI to player image (e.g., "/images/players/joe.jpg").
        /// Client combines with service base URL.
        /// </summary>
        public string ImageUri { get; set; } = string.Empty;

        public PlayerProfile() { }

        public PlayerProfile(string id, string name, string primaryBackgroundColor, string secondaryBackgroundColor, string foregroundColor, string imageUri)
        {
            Id = id;
            Name = name;
            PrimaryBackgroundColor = primaryBackgroundColor;
            SecondaryBackgroundColor = secondaryBackgroundColor;
            ForegroundColor = foregroundColor;
            ImageUri = imageUri;
        }
    }
}
