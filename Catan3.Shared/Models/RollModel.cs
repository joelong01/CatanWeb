using System;
using System.Text.Json.Serialization;

namespace Catan3.Shared.Models
{
    /// <summary>
    /// Represents the model for game rolls, including roll counts and total rolls.
    /// </summary>
    public class GameRollModel
    {
        /// <summary>
        /// Gets or sets the roll counts for the game. Indexes 0 to 10 correspond to rolls 2 to 12.
        /// </summary>
        [JsonPropertyName("RollCounts")]
        public int[] RollCounts { get; set; } = new int[11];  // Indexes 0 to 10, corresponding to rolls 2 to 12

        /// <summary>
        /// Gets or sets the total number of rolls.
        /// </summary>
        public int TotalRolls { get; set; } = 0;
    }

    /// <summary>
    /// Represents the model for rolls, including turn rolls and game rolls.
    /// </summary>
    public class RollModel
    {
        /// <summary>
        /// Gets or sets the turn roll model. Nullable as it gets set to null when the turn is over and the new one is created when the turn is started.
        /// </summary>
        public TurnRollModel? TurnRollModel { get; set; }

        /// <summary>
        /// Gets or sets the game roll model.
        /// </summary>
        public GameRollModel GameRollModel { get; set; } = new();
    }
}