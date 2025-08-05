using System.Collections.Generic;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
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
            Messenger.Send(new DoAction(GameAction.Shuffle));
        }
        [RelayCommand]
        public void Undo()
        {
            Messenger.Send(new DoAction(GameAction.Undo));
        }
        [RelayCommand]
        public void Redo()
        {
            Messenger.Send(new DoAction(GameAction.Redo));
        }
        [RelayCommand]
        public void NextAction()
        {
            if (GameModel?.GameState == GameState.PickSupplementalPlayers)
            {
                List<string> supplementalPlayers = [];
                foreach (var player in Players)
                {
                    if (player.PartipatingInSupplemental)
                    {
                      //  this.TraceMessage($"Adding {player} to suppl list");
                        supplementalPlayers.Add(player.Id);
                    }
                }
                Messenger.Send(new PlayersDoingSupplemental(supplementalPlayers));
                
 
            }

            Messenger.Send(new DoAction(GameAction.Next));

        }
        /// <summary>
        ///     this is not undoable, client only ... so we can implement this here instead
        ///     of with a message.
        /// </summary>
        /// <param name="stars"></param>
        [RelayCommand]
        public void ShowStarValues(int stars)
        {
            throw new System.Exception("shouldn't be called");

        }
        /// <summary>
        ///     this has the side effect of broadcasting a UpdateOrientation command
        /// </summary>
        [RelayCommand]
        public void FlipOrientation()
        {
            Orientation = Orientation == CatanOrientation.FaceUp ? CatanOrientation.FaceDown : CatanOrientation.FaceUp;
        }
        [RelayCommand]
        public void Purchase(Entitlement entitlement)
        {
            Messenger.Send(new PurchaseMessage(entitlement));
        }

        [RelayCommand]
        public void Balance()
        {
            Messenger.Send(new BalanceBoardMessage());
        }

    }
}
