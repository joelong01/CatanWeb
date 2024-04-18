using System;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;


namespace Catan3.Models
{
    public partial class RollModel : ObservableRecipient
    {
        [ObservableProperty]
        private RollData? _thisTurnsRoll = null;

        public static RollModel Default => new();
        public int[] RollCounts { get; } = new int[11];  // Indexes 0 to 10, corresponding to rolls 2 to 12.  Do not bind here.

        [ObservableProperty]
        private int _totalRolls = 0;

      


    }

    public partial class RollData : ObservableRecipient
    {
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
}
