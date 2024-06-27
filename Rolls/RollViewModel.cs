using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using static Catan3.Models.TurnRollViewModel;
namespace Catan3.Models
{
    public partial class RollViewModel : ObservableObject
    {
        [ObservableProperty]
        GameRollViewModel _gameRollViewModel = new();

        [ObservableProperty]
        TurnRollViewModel _turnRollViewModel = new();


    }

    public partial class UiRollModel : ObservableObject
    {
        [ObservableProperty]
        RollViewModel _rollViewModel;
        [ObservableProperty]
        int _roll;

        public UiRollModel(RollViewModel rollViewModel, int roll)
        {
            _rollViewModel = rollViewModel;
            _roll = roll;
        }
    }
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
            if (IsComplete) { Messenger.Send(new RollMessage(TurnRollModel)); }
        }
        [RelayCommand]
        private void WhiteRoll(int roll)
        {
            if (TurnRollModel is null) return;
            TurnRollModel.WhiteRoll = roll;
            if (IsComplete) { Messenger.Send(new RollMessage(TurnRollModel)); }
        }
        [RelayCommand]
        private void SpecialRoll(SpecialDice roll)
        {
            if (TurnRollModel is null) return;
            TurnRollModel.SpecialRoll = roll;
            if (IsComplete) { Messenger.Send(new RollMessage(TurnRollModel)); }
        }
        [RelayCommand]
        private void NormalRoll(ValidCatanRoll roll)
        {
            

            if (roll == ValidCatanRoll.None) return;
            if (TurnRollModel is null) return;
      
            TurnRollModel.NormalRoll = roll;
            Messenger.Send(new RollMessage(TurnRollModel));
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
