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
        private HexCoordinates _coordinates = HexCoordinates.Default;

        /// <summary>
        /// Gets or sets the ID of the player who moved the robber.
        /// </summary>
        [ObservableProperty]
        private string? _movedBy;

        /// <summary>
        /// Gets or sets the ID of the player who was targeted by the robber.
        /// </summary>
        [ObservableProperty]
        private string? _targetted;

        /// <summary>
        /// Gets or sets the number of resources stolen by the robber.
        /// </summary>
        [ObservableProperty]
        private int _resourcesStolen = 0;

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