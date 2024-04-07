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

    public class RoadMouseEntered(RoadViewModel roadViewModel)
    {
        public RoadViewModel RoadViewModel { get; } = roadViewModel;
    }

    public class RoadMouseExit(RoadViewModel roadViewModel)
    {
        public RoadViewModel RoadViewModel { get; } = roadViewModel;
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
            Messenger.Register<RoadMouseEntered>(this, (recipient, message) =>
            {

                Road_MouseEnter(message.RoadViewModel);
            });
            Messenger.Register<RoadMouseExit>(this, (recipient, message) =>
            {

                Road_MouseExit(message.RoadViewModel);
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
            PlayerViewModel? currentPlayer = PlayerDatabase.FromId(gameModel.CurrentPlayerId) ?? throw new Exception($"Bad PlayerId: {gameModel.CurrentPlayerId}");
            GameViewModel.CurrentPlayer = GameViewModel.Players[0];
            // joing the Game Model with the GameViewModel
            GameViewModel.UpdateViewModel(gameModel);
           
          
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
        /// <summary>
        ///     just shows current player color when a mouse enters. NOT loggable.
        ///     TODO: should only highlight if it is a buildable road.
        ///     TODO: send a broadcast message when the CurrentPlayer changes so that the RoadViewModel can do this itself.
        ///     TODO: when a Road entitlement is bought, Highlight (.5 Opacity) buildable roads
        /// </summary>
        /// <param name="viewModel"></param>
        private void Road_MouseEnter(RoadViewModel viewModel)
        {
            if (GameViewModel is null)
            {
                Debug.Assert(false, "How can there be a null GameViewModel that has roads?");
                return;
            }
            // this.TraceMessage($"{viewModel.Road.RoadKey} {Game?.CurrentPlayer?.Name} {viewModel.Road.RoadState}");
            if (GameViewModel?.CurrentPlayer is not null && GameViewModel?.CurrentPlayer.Background is not null && viewModel.Road.Owner is null)
            {
                viewModel.Background = BrushCache.GetGradientBrush(GameViewModel.CurrentPlayer.Background, Colors.Black);
                viewModel.Foreground = BrushCache.GetSolidColorBrush(GameViewModel.CurrentPlayer.Foreground);
                viewModel.Road.RoadState = RoadState.Highlighted;
                
               
            }
        }
        private void Road_MouseExit(RoadViewModel viewModel)
        {
            if (GameViewModel is null)
            {
                Debug.Assert(false, "How can there be a null GameViewModel that has roads?");
                return;
            }
            //   this.TraceMessage($"{viewModel.Road.RoadKey} {Game?.CurrentPlayer?.Name} {viewModel.Road.RoadState}");
            if (viewModel.Road.RoadState == RoadState.Highlighted)
            {
                viewModel.Background = BrushCache.GetSolidColorBrush(Colors.Transparent);
                viewModel.Foreground = BrushCache.GetSolidColorBrush(Colors.Transparent);
                viewModel.Road.RoadState = RoadState.Unowned;
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

        public static PlayerViewModel? FromId(string id)
        {
            return AvailablePlayers.FirstOrDefault(x => x.Id == id);
        }
    }


}
