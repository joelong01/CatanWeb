using Catan3.Shared.Utility;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Catan3.Shared.Models
{
    /// <summary>
    /// Represents the model for a robber, including its coordinates, who moved it, who it targeted, and the resources stolen.
    /// Supports both plain object usage (for JSON/API) and MVVM usage (for UI data binding).
    /// </summary>
    public partial class RobberModel : ObservableObject
    {
        /// <summary>
        /// Gets or sets the coordinates of the robber.
        /// </summary>
        [ObservableProperty]
        public partial HexCoordinates Coordinates { get; set; } = HexCoordinates.Default;

        /// <summary>
        /// Gets or sets the previous coordinates of the robber (for animation).
        /// Null means no animation needed (initial placement or loaded from save).
        /// Non-null means animate from this position to Coordinates.
        /// </summary>
        /// <remarks>
        /// This property is optional for backwards compatibility with saved games.
        /// When deserializing older saves, this will be null and no animation plays.
        /// </remarks>
        [ObservableProperty]
        public partial HexCoordinates? PreviousCoordinates { get; set; }

        /// <summary>
        /// Gets or sets the ID of the player who moved the robber.
        /// </summary>
        [ObservableProperty]
        public partial string? MovedBy { get; set; }

        /// <summary>
        /// Gets or sets the ID of the player who was targeted by the robber.
        /// </summary>
        [ObservableProperty]
        public partial string? Targeted { get; set; }

        /// <summary>
        /// Gets or sets the number of resources stolen by the robber.
        /// </summary>
        [ObservableProperty]
        public partial int ResourcesStolen { get; set; } = 0;

        /// <summary>
        /// Returns a string representation of the RobberModel.
        /// </summary>
        /// <returns>A string representation of the RobberModel.</returns>
        public override string ToString()
        {
            return $"{Coordinates}-{MovedBy}->{Targeted}: {ResourcesStolen}";
        }
    }
}