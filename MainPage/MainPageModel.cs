using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Catan3.Models;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using Microsoft.UI;
using Windows.Devices.Sms;
using Windows.UI.StartScreen;

namespace Catan3.Models
{
   

    public class RequestUpdateBuildingState(BuildingViewModel building)
    {
        public BuildingViewModel Building { get; set; } = building;
    }
    public class RequestShuffle
    {
       
    }


    public partial class MainPageModel : ObservableRecipient
    {
        [ObservableProperty]
        GameViewModel? _gameViewModel;

        public   IMessenger MessageService => this.Messenger;

        private void RegisterMessages()
        {
            Debug.Assert(Messenger is not null);
            Messenger.Register<RequestUpdateBuildingState>(this, (recipient, message) =>
            {
                UpdateBuildingState(message.Building);
            });

            Messenger.Register<RequestShuffle>(this, (recipient, message) =>
            {
              
                Shuffle();
            });
        }
        //
        // this doesn't get logged as it is just a UI update
        private void HandleShowStars(int starCount)
        {
            if (GameViewModel is null)
            {
                Debug.Assert(false, "Should not be null - state problem.");
                return;
            }
            GameViewModel.ShownStars = starCount;
        }

        public MainPageModel(GameType selectedGame, List<PlayerViewModel> playingPlayers)
        {
            RegisterMessages();
            // create a new GameModel - this would usually come from the service

            List<string> playerIds = playingPlayers.Select( p => p.Id ).ToList();
            var gameModel = GameFactory.CreateGame(selectedGame, playerIds);

            // create a GameViewModel -- this sticks for the lifetime of the game
            GameViewModel = new GameViewModel(selectedGame, playingPlayers);

            // joing the Game Model with the GameViewModel
            GameViewModel.UpdateViewModel(gameModel);
            GameViewModel.CurrentPlayer = GameViewModel.Players[0];
          
        }


        /// <summary>
        ///     Shuffle can be undone, so it 
        ///     1. copies the GameModel
        ///     2. calls Shuffle
        ///     3. Saves the old GameModel
        ///     4. Updates the ViewModel to have the new GameModel
        /// </summary>
        [RelayCommand]
        private void Shuffle()
        {
            if (GameViewModel is null || GameViewModel.GameModel is null)
            {
                Debug.Assert(false, "These should not be null");
                return;
            }

            var newModel = GameViewModel.GameModel.Copy();
            if (newModel == null)
            {
                Debug.Assert(false, "We just serialized it. it should deserialize!");
                return;
            }
            newModel.Shuffle();
            GameViewModel.UpdateViewModel(newModel);
            GameViewModel.ShuffleCount++;
           
            GameViewModel tempViewModel = GameViewModel;
            GameViewModel = null; // Reset to trigger update
            GameViewModel = tempViewModel; // Reassign

            GameViewModel.ShowStarValues(GameViewModel.ShownStars);
        }

        private void UpdateBuildingState(BuildingViewModel building)
        {
            switch (building.Building.BuildingState)
            {
                case BuildingState.Empty:

                    break;
                case BuildingState.Settlement:
                    break;
                case BuildingState.City:
                    break;
                case BuildingState.Stars:
                    break;
                case BuildingState.Knight:
                    break;
            }
        }


    }

    public static class PlayerDatabase
    {
        public static List<PlayerViewModel> AvailablePlayers { get; } =
            [
                new ("Dodgy", Colors.White, Colors.Red),
                new ("Joe", Colors.White, Colors.Blue),
                new ("Doug", Colors.White, Colors.Green),
                new ("Chris", Colors.White, Colors.Black),
                new ("Adrian", Colors.White, Colors.Purple),
                new ("Ryan", Colors.White, Colors.DarkGray)
            ];
    }


}
