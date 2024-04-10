using System;
using System.Diagnostics;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using Microsoft.UI;

namespace Catan3.Models
{
    public partial class BuildingViewModel : ObservableRecipient
    {
        /// <summary>
        ///     the user has clicked on a building.  Send this to the MainPageViewModel via a message 
        ///     so that it can be logged and the logic can span roads and buildings.
        /// </summary>
        public BuildingViewModel()
        {
            IsActive = true;
            Messenger.Register<CurrentPlayerChanged>(this, (recipient, message) =>
            {
                HandleCurrentPlayerChanged(message.CurrentPlayer);
            });
        }
        /// <summary>
        ///     We recieve a message from MainPageViewModel that the current player has changed.
        ///     Store this in a non-Observable property!
        /// </summary>
        /// <param name="newCurrentPlayer"></param>
        private void HandleCurrentPlayerChanged(PlayerViewModel newCurrentPlayer)
        {
            CurrentPlayer = newCurrentPlayer;
           

        }
        //
        //  used for MouseEnter/Mouse leave log
        private BuildingState _oldState = BuildingState.Empty;

        [RelayCommand]
        private void Upgrade()
        {
            if (Building.BuildingState == BuildingState.Highlighted) { Building.BuildingState = BuildingState.Empty; }
            MainPage.Messenger.Send(new BuildingUpgrade(Building.BuildingKey));
        }
        [RelayCommand]
        private void MouseEnter()
        {
            // this.TraceMessage($"CurrentPlayer={this.CurrentPlayer}");
            _oldState = Building.BuildingState;
            if (Building.BuildingState == BuildingState.Empty)
            {
               
                Building.BuildingState = BuildingState.Highlighted;

            }
        }

        [RelayCommand]
        private void MouseExit()
        {
            if (_oldState == BuildingState.Empty && Building.OwnerId is null) // it can be empty going in, bu owned coming out...
            {
                Building.BuildingState = BuildingState.Empty;
            }
        }

        internal void SendChangeNotification()
        {

            OnPropertyChanged(nameof(Building));

        }
    }
}

