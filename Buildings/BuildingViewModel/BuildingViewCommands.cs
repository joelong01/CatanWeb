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
            Messenger.Register<EndGame>(this, (recipient, message) =>
            {
                Messenger.UnregisterAll(this);
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
        private BuildingVisualState _oldState = BuildingVisualState.Hidden;

        [RelayCommand]
        private void Upgrade()
        {
            if (Building.BuildingState == BuildingState.NotBuildable) return;
            Messenger.Send(new BuildingUpgradeMessage(Building.BuildingKey));
        }

        //
        /// <summary>
        ///  this is useful in the beginning of the game when picking board and resources -- if the stars 
        ///  aren't being shown, moving a mouse will show the stars.
        /// </summary>
        [RelayCommand]
        private void MouseEnter()
        {
            if (Building.BuildingState == BuildingState.NotBuildable) return;

            _oldState = VisualState;
            if ( VisualState == BuildingVisualState.Hidden)
            {

                VisualState = BuildingVisualState.Stars;

            }
        }

        [RelayCommand]
        private void MouseExit()
        {
            if (Building.BuildingState == BuildingState.NotBuildable) return;
            if (_oldState == BuildingVisualState.Hidden && Building.OwnerId is null) // it can be empty going in, bu owned coming out...
            {
                VisualState = BuildingVisualState.Hidden;
            }
        }

    }
}

