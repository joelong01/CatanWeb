using System;
using System.Text.Json.Serialization;
using CommunityToolkit.Mvvm.ComponentModel;
namespace Catan3.Models
{
    /// <summary>
    /// Represents the model for a turn roll, including red and white dice rolls, special dice, and normal roll.
    /// </summary>
    public partial class TurnRollModel : ObservableRecipient
    {
        /// <summary>
        /// Gets the default instance of the TurnRollModel class.
        /// </summary>
        [property: JsonIgnore]
        public static TurnRollModel Default => new();

        /// <summary>
        /// Gets or sets the value of the red roll.
        /// </summary>
        [ObservableProperty]
        public partial int RedRoll { get; set; } = -1;

        /// <summary>
        /// Gets or sets the value of the white roll.
        /// </summary>
        [ObservableProperty]
        public partial int WhiteRoll { get; set; } = -1;

        /// <summary>
        /// Gets or sets the value of the special roll.
        /// </summary>
        [ObservableProperty]
        public partial SpecialDice SpecialRoll { get; set; } = SpecialDice.None;

        /// <summary>
        /// Gets or sets the value of the normal roll.
        /// </summary>
        [ObservableProperty]
        public partial ValidCatanRoll NormalRoll { get; set; } = ValidCatanRoll.None;

        /// <summary>
        /// Returns a hash code for the current TurnRollModel.
        /// </summary>
        /// <returns>A hash code for the current TurnRollModel.</returns>
        public override int GetHashCode() => HashCode.Combine(RedRoll, WhiteRoll, SpecialRoll, NormalRoll);
    }
    /// <summary>
    /// Represents the model for game rolls, including roll counts and total rolls.
    /// </summary>
    public partial class GameRollModel : ObservableObject
    {
        /// <summary>
        /// Gets or sets the roll counts for the game. Indexes 0 to 10 correspond to rolls 2 to 12.
        /// </summary>
        [property: JsonPropertyName("RollCounts")]
        public int[] RollCounts { get; set; } = new int[11];  // Indexes 0 to 10, corresponding to rolls 2 to 12. Do not bind here.

        /// <summary>
        /// Gets or sets the total number of rolls.
        /// </summary>
        [ObservableProperty]
        public partial int TotalRolls { get; set; } = 0;
    }

    /// <summary>
    /// Represents the model for rolls, including turn rolls and game rolls.
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
