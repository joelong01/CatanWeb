using System;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;

namespace Catan3.Models
{
    public partial class RollViewModel : ObservableRecipient
    {
        [ObservableProperty]
        private RollModel? _rollModel;


        private bool IsComplete => RollModel is not null && RollModel.RedRoll != -1 && RollModel.WhiteRoll != -1 && RollModel.SpecialRoll != SpecialDice.None;


        [RelayCommand]
        private void RedRoll(int roll)
        {
            if (RollModel is null) return;
            RollModel.RedRoll = roll;
            if (IsComplete) { Messenger.Send(new Rolled(RollModel)); }
        }
        [RelayCommand]
        private void WhiteRoll(int roll)
        {
            if (RollModel is null) return;
            RollModel.WhiteRoll = roll;
            if (IsComplete) { Messenger.Send(new Rolled(RollModel)); }
        }
        [RelayCommand]
        private void SpecialRoll(SpecialDice roll)
        {
            if (RollModel is null) return;
            RollModel.SpecialRoll = roll;
            if (IsComplete) { Messenger.Send(new Rolled(RollModel)); }
        }

        [RelayCommand]
        private void NormalRoll(string roll)
        {
            if (RollModel is null)
            {
                RollModel = new();

            };
            var r = Enum.Parse<Roll>(roll);
            RollModel.NormalRoll = r;
            Messenger.Send(new Rolled(RollModel));
        }


    }
}
