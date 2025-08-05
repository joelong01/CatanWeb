using System;
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
        [JsonPropertyName("RollCounts")]
        [ObservableProperty]
        private int[] _rollCounts = new int[11];  // Indexes 0 to 10, corresponding to rolls 2 to 12

        /// <summary>
        /// Gets or sets the total number of rolls.
        /// </summary>
        [ObservableProperty]
        private int _totalRolls = 0;
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
        private TurnRollModel? _turnRollModel;

        /// <summary>
        /// Gets or sets the game roll model.
        /// </summary>
        [ObservableProperty]
        private GameRollModel _gameRollModel = new();
    }
}