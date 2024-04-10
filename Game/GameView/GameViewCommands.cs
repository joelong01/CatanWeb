using System.Diagnostics;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using Microsoft.UI;

namespace Catan3.Models
{

    public partial class GameViewModel : ObservableRecipient
    {
      
        /// <summary>
        ///     This method is called as an ICommand by controls that bind to GameViewModel
        ///     It requires coordination across the GameViewModel (e.g. needs Undo/Redo)
        ///     and therefore needs to be send to the Messenger.  If there is a service
        ///     handling game logic, this will send a requrest to the service.
        /// </summary>
        [RelayCommand]
        public void Shuffle()
        {
            MainPage.Messenger.Send(new DoAction(GameAction.Shuffle));
        }

        [RelayCommand]
        public void Undo()
        {
            MainPage.Messenger.Send(new DoAction(GameAction.Undo));
        }
        [RelayCommand]
        public void Redo()
        {
            MainPage.Messenger.Send(new DoAction(GameAction.Redo));
        }



        /// <summary>
        ///     this is not undoable, client only ... so we can implement this here instead
        ///     of with a message.
        /// </summary>
        /// <param name="stars"></param>
        [RelayCommand]
        public void ShowStarValues(int stars)
        {
           

            this.TraceMessage($"stars: {stars}");

            foreach (var building in Buildings)
            {
                if (building.Building.OwnerId is not null) continue;
                int buildingStars = GameModel.BuildingStars(building.Building.BuildingKey);
               
                if (buildingStars >= stars)
                {

                         building.Building.BuildingState = BuildingState.Stars;
        
                }
                else
                {
                     building.Building.BuildingState = BuildingState.Empty;
                }

            }

        }
    }
}
