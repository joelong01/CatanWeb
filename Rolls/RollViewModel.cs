using System;
using System.Reflection.Metadata.Ecma335;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;

namespace Catan3.Models
{
    public partial class RollViewModel : ObservableRecipient
    {
        [ObservableProperty]
        private RollModel _rollModel = RollModel.Default;



        private bool IsComplete => RollModel.ThisTurnsRoll is not null && RollModel.ThisTurnsRoll.RedRoll != -1 && RollModel.ThisTurnsRoll.WhiteRoll != -1 && RollModel.ThisTurnsRoll.SpecialRoll != SpecialDice.None;


        [RelayCommand]
        private void RedRoll(int roll)
        {
            if (RollModel.ThisTurnsRoll is null) return;
            RollModel.ThisTurnsRoll.RedRoll = roll;
            if (IsComplete) { Messenger.Send(new Rolled(RollModel)); }
        }
        [RelayCommand]
        private void WhiteRoll(int roll)
        {
            if (RollModel.ThisTurnsRoll is null) return;

            RollModel.ThisTurnsRoll.WhiteRoll = roll;
            if (IsComplete) { Messenger.Send(new Rolled(RollModel)); }
        }
        [RelayCommand]
        private void SpecialRoll(SpecialDice roll)
        {
            if (RollModel.ThisTurnsRoll is null) return;
            RollModel.ThisTurnsRoll.SpecialRoll = roll;
            if (IsComplete) { Messenger.Send(new Rolled(RollModel)); }
        }

        [RelayCommand]
        private void NormalRoll(ValidCatanRoll roll)
        {
            if (RollModel.ThisTurnsRoll is null)
            {
                RollModel.ThisTurnsRoll = new();
            }
            if (roll == ValidCatanRoll.None) return;
            RollModel.ThisTurnsRoll.NormalRoll = roll;
            Messenger.Send(new Rolled(RollModel));
        }

        public string GetRollCount(RollModel? _, int? __, ValidCatanRoll roll)
        {
            if (roll == ValidCatanRoll.None) return "Error";
            var r = (int)roll;
            return $"{RollModel.RollCounts[r - 2]}  ";
        }

        public string GetRollPercent(RollModel _, int totalrolls, ValidCatanRoll roll)
        {
            if (roll == ValidCatanRoll.None) return "Error";
            var r = (int) roll;
            var count =  RollModel.RollCounts[r - 2];
            if (totalrolls == 0) { return "0%"; }
            var percent = (double) count / (double)totalrolls * 100;
            var result =  $"{Math.Round(percent, 2)}%";
            return result;
        }

    }
}
