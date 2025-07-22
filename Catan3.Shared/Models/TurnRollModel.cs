namespace Catan3.Shared.Models
{
    /// <summary>
    /// Represents the model for a turn roll, including red and white dice rolls, special dice, and normal roll.
    /// </summary>
    public class TurnRollModel
    {
        /// <summary>
        /// Gets or sets the value of the red roll.
        /// </summary>
        public int RedRoll { get; set; } = 0;

        /// <summary>
        /// Gets or sets the value of the white roll.
        /// </summary>
        public int WhiteRoll { get; set; } = 0;

        /// <summary>
        /// Gets or sets the special dice rolled.
        /// </summary>
        public SpecialDice SpecialDice { get; set; } = SpecialDice.None;

        /// <summary>
        /// Gets the normal roll (sum of red and white dice).
        /// </summary>
        public ValidCatanRoll NormalRoll => (ValidCatanRoll)(RedRoll + WhiteRoll);

        public TurnRollModel() { }

        public TurnRollModel(int redRoll, int whiteRoll)
        {
            RedRoll = redRoll;
            WhiteRoll = whiteRoll;
        }

        public override string ToString() => $"Red: {RedRoll}, White: {WhiteRoll}, Total: {(int)NormalRoll}";
    }
}