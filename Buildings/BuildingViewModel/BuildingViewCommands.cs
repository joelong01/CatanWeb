using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using Microsoft.UI;

namespace Catan3.Models
{
    public partial class BuildingViewModel : ObservableRecipient
    {

        public BuildingViewModel()
        {
            IsActive = true;
            Messenger.Register<CurrentPlayerChanged>(this, (recipient, message) =>
            {
                HandleCurrentPlayerChanged(message.CurrentPlayer);
            });
        }

        private void HandleCurrentPlayerChanged(PlayerViewModel newCurrentPlayer)
        {
            CurrentPlayer = newCurrentPlayer;
            if (Building.BuildingState == BuildingState.Stars)
            {
                // this switches all Star color to the current player's colors
                Background = BrushCache.GetGradientBrush(CurrentPlayer.Background, Colors.Black);
                Foreground = BrushCache.GetSolidColorBrush(CurrentPlayer.Foreground);
            }

            
        }

        private BuildingState _oldState = BuildingState.Empty;

        [RelayCommand]
        private void Upgrade()
        {
            MainPage.Messenger.Send(new BuildingUpgrade(this));
        }
        [RelayCommand]
        private void MouseEnter()
        {
           // this.TraceMessage($"CurrentPlayer={this.CurrentPlayer}");
            _oldState = Building.BuildingState;
            if (Building.BuildingState == BuildingState.Empty)
            {
                Background = BrushCache.GetGradientBrush(CurrentPlayer.Background, Colors.Black);
                Foreground = BrushCache.GetSolidColorBrush(CurrentPlayer.Foreground);
                Building.BuildingState = BuildingState.Highlighted;
                
            }
        }

        [RelayCommand]
        private void MouseExit()
        {
            if (_oldState == BuildingState.Empty)
            {
                Background = BrushCache.GetSolidColorBrush(Colors.Transparent);
                Foreground = BrushCache.GetSolidColorBrush(Colors.Transparent);
                Building.BuildingState = BuildingState.Empty;
            }
        }




    }
}

