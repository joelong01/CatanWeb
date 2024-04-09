using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Linq.Expressions;
using Catan3.Utility;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using Microsoft.UI;

namespace Catan3.Models
{



    public partial class MainPageViewModel : ObservableRecipient
    {
        [ObservableProperty]
        GameViewModel _gameViewModel = GameViewModel.Default;

        public IMessenger MessageService => this.Messenger;
        public MainPageViewModel(GameType selectedGame, List<PlayerViewModel> playingPlayers)
        {
            FunctionTimer.Enabled = false;
            RegisterMessages();
            // create a new GameModel - this would usually come from the service

            List<string> playerIds = playingPlayers.Select( p => p.Id ).ToList();
            var gameModel = GameFactory.CreateGame(selectedGame, playerIds);
            var gvm = new GameViewModel(gameModel);
            this.GameViewModel = gvm;
            GameViewModel.UpdateLayout();
            GameViewModel.SetStars();


        }
        private void RegisterMessages()
        {

            Debug.Assert(Messenger is not null);
            IsActive = true;
            Messenger.Register<RequestShuffle>(this, (recipient, message) =>
            {
                Shuffle();
            });


            Messenger.Register<BuildingUpgrade>(this, (recipient, message) =>
            {
                Building_Upgrade(message.BuildingViewModel);
            });


        }



        private Stack<GameModel> DoneStack { get; } = [];
        /// <summary>
        ///     This is a loggable event.  in the case of a Service, this would be a service call.
        /// </summary>
        /// <param name="buildingViewModel"></param>
        private void Building_Upgrade(BuildingViewModel buildingViewModel)
        {
            var newGameView = CopyGameViewModel();
            DoneStack.Push(GameViewModel.GameModel);
            var bvm = newGameView.Buildings.FindBuildingViewModel(buildingViewModel.Building.BuildingKey);
            if (bvm is null) return;

            switch (bvm.Building.BuildingState)
            {
                case BuildingState.Empty:
                case BuildingState.Highlighted:
                case BuildingState.Stars:
                    
                    bvm.Building.BuildingState = BuildingState.Settlement;
                    bvm.Building.Owner = GameViewModel.CurrentPlayer.Player;
                 
                    break;
                case BuildingState.Settlement:
                    
                    
                    Debug.Assert(bvm.Building.Owner != null);
                    if (bvm.Building.Owner.Id != GameViewModel.CurrentPlayer.Id) return;
                    bvm.Building.BuildingState = BuildingState.City;
                 
                    break;
                case BuildingState.City:


                    Debug.Assert(bvm.Building.Owner != null);
                    if (bvm.Building.Owner.Id != GameViewModel.CurrentPlayer.Id) return;
                    bvm.Building.BuildingState = BuildingState.Knight;
                 
                    break;
                case BuildingState.Knight:
                    break;
            }
 

            this.GameViewModel = newGameView;
            GameViewModel.ShownStars = 14;
         


        }

        [RelayCommand]
        private void Shuffle()
        {
            var currentStars = GameViewModel.ShownStars;

            var gameViewModel = CopyGameViewModel();
            gameViewModel.GameModel.Shuffle();
            this.GameViewModel = gameViewModel;
            GameViewModel.SetStars();
            GameViewModel.ShownStars = 14;
            GameViewModel.ShownStars = currentStars;
            Debug.Assert(GameViewModel.CurrentPlayer != null);
      

        }
        [RelayCommand]
        private void NextPlayer()
        {
            Debug.Assert(GameViewModel.CurrentPlayer != null);
            int index = GameViewModel.Players.IndexOf(GameViewModel.CurrentPlayer);
            Debug.Assert(index >= 0);
            index++;
            index = index % GameViewModel.Players.Count;
            GameViewModel.CurrentPlayer = GameViewModel.Players[index];
        }

        private GameViewModel CopyGameViewModel()
        {
            using (new FunctionTimer("CopyGameViewModel"))
            {
                DoneStack.Push(GameViewModel.GameModel);
                var newGameModel = GameViewModel.GameModel.Copy();

                var gameViewModel = new GameViewModel(newGameModel);
                gameViewModel.SetCurrentPlayer(newGameModel.CurrentPlayerId);   
                return gameViewModel;

            }

        }


        partial void OnGameViewModelChanged(GameViewModel? oldValue, GameViewModel newValue)
        {
            //  this.TraceMessage($"GameViewModel updated to {GameViewModel.Id} CurrentPlayer={GameViewModel.CurrentPlayer}");

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
