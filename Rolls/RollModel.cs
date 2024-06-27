using System;
using System.Text.Json.Serialization;
using CommunityToolkit.Mvvm.ComponentModel;
namespace Catan3.Models
{
    public partial class TurnRollModel : ObservableRecipient
    {
        [property: JsonIgnore]
        public static TurnRollModel Default => new();
        [ObservableProperty]
        private int _redRoll = -1;
        [ObservableProperty]
        private int _whiteRoll = -1;
        [ObservableProperty]
        private SpecialDice _specialRoll = SpecialDice.None;
        [ObservableProperty]
        private ValidCatanRoll _normalRoll = ValidCatanRoll.None;
        public override int GetHashCode() => HashCode.Combine(RedRoll, WhiteRoll, SpecialRoll, NormalRoll);
    }
    public partial class GameRollModel : ObservableObject
    {
        [property:JsonPropertyName("RollCounts")]
        public int[] RollCounts { get; set; } = new int[11];  // Indexes 0 to 10, corresponding to rolls 2 to 12.  Do not bind here.
        [ObservableProperty]
        private int _totalRolls = 0;
       
    }

    public partial class RollModel : ObservableObject
    {
        [ObservableProperty]
        TurnRollModel? _turnRollModel; // nullable as it gets set to null when the turn is over and the new one is created when the turn is started

        [ObservableProperty]
        GameRollModel _gameRollModel = new();
    }

}
