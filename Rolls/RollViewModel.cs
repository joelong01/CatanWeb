using System;
using System.Diagnostics;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;

namespace Catan3.Models
{
    public partial class TurnRollViewModel : ObservableRecipient
    {
        [ObservableProperty]
        private TurnRollModel? _turnRollModel;



        private bool IsComplete => TurnRollModel is not null && TurnRollModel.RedRoll != -1 && TurnRollModel.WhiteRoll != -1 && TurnRollModel.SpecialRoll != SpecialDice.None;


        [RelayCommand]
        private void RedRoll(int roll)
        {
            if (TurnRollModel is null) return;
            TurnRollModel.RedRoll = roll;
            if (IsComplete) { Messenger.Send(new Rolled(TurnRollModel)); }
        }
        [RelayCommand]
        private void WhiteRoll(int roll)
        {
            if (TurnRollModel is null) return;

            TurnRollModel.WhiteRoll = roll;
            if (IsComplete) { Messenger.Send(new Rolled(TurnRollModel)); }
        }
        [RelayCommand]
        private void SpecialRoll(SpecialDice roll)
        {
            if (TurnRollModel is null) return;
            TurnRollModel.SpecialRoll = roll;
            if (IsComplete) { Messenger.Send(new Rolled(TurnRollModel)); }
        }

        [RelayCommand]
        private void NormalRoll(ValidCatanRoll roll)
        {
            Debug.Assert(TurnRollModel is not null, "Turn roll model is null.  probably a bug in NextPlayer"); 
            if (roll == ValidCatanRoll.None) return;
            TurnRollModel.NormalRoll = roll;
            Messenger.Send(new Rolled(TurnRollModel));
        }

        public partial class GameRollViewModel : ObservableObject
        {
            [ObservableProperty]
            private GameRollModel _gameRollModel = new();

            public string GetRollCount(GameRollModel? gameRollModel,  ValidCatanRoll roll)
            {
                Debug.Assert(ReferenceEquals(gameRollModel, GameRollModel));
                if (roll == ValidCatanRoll.None) return "Error";
                var r = (int)roll;
                return $"{gameRollModel.RollCounts[r - 2]}  ";
            }

            public string GetRollPercent(GameRollModel gameRollModel ,  ValidCatanRoll roll)
            {
                Debug.Assert(ReferenceEquals(gameRollModel, GameRollModel));
                if (roll == ValidCatanRoll.None) return "Error";
                var r = (int) roll;
                var count =  gameRollModel.RollCounts[r - 2];
                if (gameRollModel.TotalRolls == 0) { return "0%"; }
                var percent = (double) count / (double)gameRollModel.TotalRolls * 100;
                var result =  $"{Math.Round(percent, 2)}%";
                return result;
            }
        }

    }
}
