using Catan3.Shared.Utility;

namespace Catan3.Shared.Models
{
    /// <summary>
    /// Represents the model for a robber, including its coordinates, who moved it, who it targeted, and the resources stolen.
    /// </summary>
    public class RobberModel
    {
        /// <summary>
        /// Gets or sets the coordinates of the robber.
        /// </summary>
        public HexCoordinates Coordinates { get; set; } = HexCoordinates.Default;

        /// <summary>
        /// Gets or sets the ID of the player who moved the robber.
        /// </summary>
        public string? MovedBy { get; set; }

        /// <summary>
        /// Gets or sets the ID of the player who was targeted by the robber.
        /// </summary>
        public string? Targetted { get; set; }

        /// <summary>
        /// Gets or sets the number of resources stolen by the robber.
        /// </summary>
        public int ResourcesStolen { get; set; } = 0;

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