using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using static Catan3.Models.TurnRollViewModel;
namespace Catan3.Models
{
    /// <summary>
    /// Represents the view model for rolls, including game rolls and turn rolls.
    /// </summary>
    public partial class RollViewModel : ObservableObject
    {
        /// <summary>
        /// Gets or sets the game roll view model.
        /// </summary>
        [ObservableProperty]
        public partial GameRollViewModel GameRollViewModel { get; set; } = new();

        /// <summary>
        /// Gets or sets the turn roll view model.
        /// </summary>
        [ObservableProperty]
        public partial TurnRollViewModel TurnRollViewModel { get; set; } = new();
    }

    /// <summary>
    /// Represents a UI model for a roll, including the roll view model and the roll value.
    /// </summary>
    /// <remarks>
    /// Initializes a new instance of the UiRollModel class with the specified roll view model and roll value.
    /// </remarks>
    /// <param name="rollViewModel">The roll view model.</param>
    /// <param name="roll">The roll value.</param>
    public partial class UiRollModel(RollViewModel rollViewModel, int roll) : ObservableObject
    {
        /// <summary>
        /// Gets or sets the roll view model.
        /// </summary>
        [ObservableProperty]
        public partial RollViewModel RollViewModel { get; set; } = rollViewModel;

        /// <summary>
        /// Gets or sets the roll value.
        /// </summary>
        [ObservableProperty]
        public partial int Roll { get; set; } = roll;
    }
    /// <summary>
    /// Represents the view model for turn rolls, including commands for rolling dice.
    /// </summary>
    public partial class TurnRollViewModel : ObservableRecipient
    {
        /// <summary>
        /// Gets or sets the turn roll model.
        /// </summary>
        [ObservableProperty]
        public partial TurnRollModel? TurnRollModel { get; set; }

        /// <summary>
        /// Gets a value indicating whether the turn roll is complete.
        /// </summary>
        private bool IsComplete => TurnRollModel is not null && TurnRollModel.RedRoll != -1 && TurnRollModel.WhiteRoll != -1 && TurnRollModel.SpecialRoll != SpecialDice.None;

        /// <summary>
        /// Sets the red roll value and sends a RollMessage if the turn roll is complete.
        /// </summary>
        /// <param name="roll">The red roll value.</param>
        [RelayCommand]
        private void RedRoll(int roll)
        {
            if (TurnRollModel is null) return;
            TurnRollModel.RedRoll = roll;
            if (IsComplete) { Messenger.Send(new RollMessage(TurnRollModel)); }
        }

        /// <summary>
        /// Sets the white roll value and sends a RollMessage if the turn roll is complete.
        /// </summary>
        /// <param name="roll">The white roll value.</param>
        [RelayCommand]
        private void WhiteRoll(int roll)
        {
            if (TurnRollModel is null) return;
            TurnRollModel.WhiteRoll = roll;
            if (IsComplete) { Messenger.Send(new RollMessage(TurnRollModel)); }
        }

        /// <summary>
        /// Sets the special roll value and sends a RollMessage if the turn roll is complete.
        /// </summary>
        /// <param name="roll">The special roll value.</param>
        [RelayCommand]
        private void SpecialRoll(SpecialDice roll)
        {
            if (TurnRollModel is null) return;
            TurnRollModel.SpecialRoll = roll;
            if (IsComplete) { Messenger.Send(new RollMessage(TurnRollModel)); }
        }

        /// <summary>
        /// Sets the normal roll value and sends a RollMessage.
        /// </summary>
        /// <param name="roll">The normal roll value.</param>
        [RelayCommand]
        private void NormalRoll(ValidCatanRoll roll)
        {
            if (roll == ValidCatanRoll.None) return;
            if (TurnRollModel is null) return;
            TurnRollModel.NormalRoll = roll;
            Messenger.Send(new RollMessage(TurnRollModel));
        }

        /// <summary>
        /// Represents the view model for game rolls, including methods for getting roll counts and percentages.
        /// </summary>
        public partial class GameRollViewModel : ObservableObject
        {
            /// <summary>
            /// Gets or sets the game roll model.
            /// </summary>
            [ObservableProperty]
            public partial GameRollModel GameRollModel { get; set; } = new();

            /// <summary>
            /// Gets the roll count for the specified roll.
            /// </summary>
            /// <param name="gameRollModel">The game roll model.</param>
            /// <param name="roll">The roll value.</param>
            /// <returns>The roll count as a string.</returns>
            public string GetRollCount(GameRollModel? gameRollModel, ValidCatanRoll roll)
            {
                Debug.Assert(ReferenceEquals(gameRollModel, GameRollModel));
                if (roll == ValidCatanRoll.None) return "Error";
                var r = (int)roll;
                return $"{gameRollModel.RollCounts[r - 2]}  ";
            }

            /// <summary>
            /// Gets the roll percentage for the specified roll.
            /// </summary>
            /// <param name="gameRollModel">The game roll model.</param>
            /// <param name="roll">The roll value.</param>
            /// <returns>The roll percentage as a string.</returns>
            public string GetRollPercent(GameRollModel gameRollModel, ValidCatanRoll roll)
            {
                Debug.Assert(ReferenceEquals(gameRollModel, GameRollModel));
                if (roll == ValidCatanRoll.None) return "Error";
                var r = (int)roll;
                var count = gameRollModel.RollCounts[r - 2];
                if (gameRollModel.TotalRolls == 0) { return "0%"; }
                var percent = (double)count / (double)gameRollModel.TotalRolls * 100;
                var result = $"{Math.Round(percent, 2)}%";
                return result;
            }
        }
    }
}
