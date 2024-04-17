using System;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Catan3.Models
{
    public partial class RollModel : ObservableObject, IEquatable<RollModel>
    {

        [ObservableProperty]
        private int _redRoll = -1;
        [ObservableProperty]
        private int _whiteRoll = -1;
        [ObservableProperty]
        private CatanOrientation _orientation = CatanOrientation.FaceDown;
        [ObservableProperty]
        private bool _selected = false;
        [ObservableProperty]
        private SpecialDice _specialRoll = SpecialDice.None;

        [ObservableProperty]
        private Roll _normalRoll = Roll.None;

        public bool Equals(RollModel? other)
        {
            if (other is null) return false;
            return (RedRoll, WhiteRoll, SpecialRoll, NormalRoll) == (other.RedRoll, other.WhiteRoll, other.SpecialRoll, other.NormalRoll);
        }

        public override int GetHashCode() => HashCode.Combine(RedRoll, WhiteRoll, SpecialRoll, NormalRoll);

        public static bool operator ==(RollModel left, RollModel right) => Equals(left, right);

        public static bool operator !=(RollModel left, RollModel right) => !Equals(left, right);

        public override bool Equals(object? obj) => Equals(obj as RollModel);
    }
}
