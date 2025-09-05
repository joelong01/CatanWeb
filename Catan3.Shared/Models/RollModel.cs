using System.Text.Json.Serialization;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Catan3.Shared.Models
{
    /// <summary>
    /// Represents the model for game rolls, including roll counts and total rolls.
    /// Supports both plain object usage (for JSON/API) and MVVM usage (for UI data binding).
    /// </summary>
    public partial class GameRollModel : ObservableObject
    {
        /// <summary>
        /// Gets or sets the roll counts for the game. Indexes 0 to 10 correspond to rolls 2 to 12.
        /// </summary>
        [field: JsonPropertyName("RollCounts")]
        [ObservableProperty]
        public partial int[] RollCounts { get; set; } = new int[11];

        /// <summary>
        /// Gets or sets the total number of rolls.
        /// </summary>
        [ObservableProperty]
        public partial int TotalRolls { get; set; } = 0;
    }

    /// <summary>
    /// Represents the model for rolls, including turn rolls and game rolls.
    /// Supports both plain object usage (for JSON/API) and MVVM usage (for UI data binding).
    /// </summary>
    public partial class RollModel : ObservableObject
    {
        /// <summary>
        /// Gets or sets the turn roll model. Nullable as it gets set to null when the turn is over and the new one is created when the turn is started.
        /// </summary>
        [ObservableProperty]
        public partial TurnRollModel? TurnRollModel { get; set; }

        /// <summary>
        /// Gets or sets the game roll model.
        /// </summary>
        [ObservableProperty]
        public partial GameRollModel GameRollModel { get; set; } = new();
    }
}