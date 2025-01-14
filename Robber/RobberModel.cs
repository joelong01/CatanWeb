
using Catan3.Utility;
using CommunityToolkit.Mvvm.ComponentModel;
namespace Catan3.Models
{
    /// <summary>
    /// Represents the model for a robber, including its coordinates, who moved it, who it targeted, and the resources stolen.
    /// </summary>
    public partial class RobberModel : ObservableObject
    {
        /// <summary>
        /// Gets or sets the coordinates of the robber.
        /// </summary>
        [ObservableProperty]
        public partial HexCoordinates Coordinates { get; set; } = HexCoordinates.Default;

        /// <summary>
        /// Gets or sets the ID of the player who moved the robber.
        /// </summary>
        [ObservableProperty]
        public partial string? MovedBy { get; set; }

        /// <summary>
        /// Gets or sets the ID of the player who was targeted by the robber.
        /// </summary>
        [ObservableProperty]
        public partial string? Targetted { get; set; }

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
            return $"{Coordinates}-{MovedBy}->{Targetted}: {ResourcesStolen}";
        }
    }
}
