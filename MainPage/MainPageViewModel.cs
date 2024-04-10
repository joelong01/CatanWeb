using System;
using System.Collections.Generic;
using System.ComponentModel;
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
    public partial class Log : ObservableObject
    {

        private readonly  BindingList<string> DoneStack = new();
        private readonly  BindingList<string> RedoStack = new();

        public Log()
        {
            DoneStack.ListChanged += DoneStack_ListChanged;
            RedoStack.ListChanged += RedoStack_ListChanged;
        }

        private void RedoStack_ListChanged(object? sender, ListChangedEventArgs e)
        {
            if (sender is not null && sender is BindingList<string> list)
            {
                this.CanRedo = list.Count > 0;
            }
        }

        private void DoneStack_ListChanged(object? sender, ListChangedEventArgs e)
        {
            if (sender is not null && sender is BindingList<string> list)
            {
                this.CanUndo = list.Count > 1; // don't undo past the start
            }
        }

        /// <summary>
        ///     Serialize the model
        ///     put it on the DoneStack
        ///     clear the RedoStack
        /// </summary>
        /// <param name="model"></param>
        public void Done(GameModel model)
        {
            DoneStack.Add(model.Serialize());
            RedoStack.Clear();
        }
        /// <summary>
        /// Performs an undo operation by restoring the state from the undo stack.
        /// The current state is pushed onto the redo stack before the undo is applied.
        /// </summary>
        /// <param name="viewModel">The game view model containing the current game state.</param>
        /// <returns>true if the undo operation was successful; false otherwise.</returns>
        public bool Undo(GameViewModel viewModel)
        {
            if (!CanUndo)  
                return false;

            try
            {
                var currentState = viewModel.GameModel.Serialize();
                RedoStack.Add(currentState);  // Save current state to redo stack

                var undoState = DoneStack[^1];  // Using indexers for readability
                DoneStack.RemoveAt(DoneStack.Count - 1);

                var model = GameModel.Deserialize(undoState) ?? throw new InvalidOperationException("Failed to deserialize the undo state.");
                viewModel.MergeGameModel(model);  // Apply the restored state
                return true;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Undo operation failed: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Restores the game state from the redo stack, pushing the current state onto the undo stack.
        /// </summary>
        /// <param name="viewModel">The game view model to which the state will be applied.</param>
        /// <returns>true if the redo operation was successful; false otherwise.</returns>
        public bool Redo(GameViewModel viewModel)
        {
            if (!CanRedo)  // More explicit than CanRedo for understanding
                return false;

            try
            {
                var redoState = RedoStack[^1];  // Retrieve the last state to redo
                RedoStack.RemoveAt(RedoStack.Count - 1);  // Remove the last item

                DoneStack.Add(redoState);  // Save the redo state back to the undo stack

                var model = GameModel.Deserialize(redoState) ?? throw new InvalidOperationException("Failed to deserialize the redo state.");
                viewModel.MergeGameModel(model);  // Apply the restored state
                return true;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Redo operation failed: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        ///     Updating via notifcation when the UndoStack changes
        /// </summary>
        [ObservableProperty]
        private bool _canUndo = false;

        /// <summary>
        ///  Updated via notification when the RedoStack changes
        /// </summary>
        [ObservableProperty]
        private bool _canRedo = false;



    }


    public partial class MainPageViewModel : ObservableRecipient
    {
        [ObservableProperty]
        GameViewModel _gameViewModel = GameViewModel.Default;

        [ObservableProperty]
        private Log _log = new();

        public IMessenger MessageService => this.Messenger;
        public MainPageViewModel(GameType selectedGame, List<PlayerViewModel> playingPlayers)
        {
            FunctionTimer.Enabled = true;
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
           
            Messenger.Register<DoAction>(this, (recipient, message) =>
            {
                DoAction(message.Action);
            });
           

            Messenger.Register<BuildingUpgrade>(this, (recipient, message) =>
            {
                Building_Upgrade(message.BuildingKey);
            });
            Messenger.Register<BuyRoad>(this, (recipient, message) =>
                       {
                           Road_Purchase(message.RoadKey);
                       });


        }
        /// <summary>
        ///     if the message takes no parameters, then we can just add enum elements and then add a case statement
        ///     without modifying code inbetween
        /// </summary>
        /// <param name="action"></param>
        private void DoAction(GameAction action)
        {
            switch (action)
            {
                case GameAction.Shuffle:
                    Shuffle();
                    break;
                case GameAction.Undo:
                    Log.Undo(this.GameViewModel);
                    break;
                case GameAction.Redo:
                    Log.Redo(this.GameViewModel);
                    break;
            }
        }

        private void Road_Purchase(RoadKey roadKey)
        {

            var roadView = GameViewModel.Roads.FirstOrDefault(r => r.Road.RoadKey == roadKey);
            if (roadView is null) return;
            Log.Done(GameViewModel.GameModel);

            if (roadView.Owner == null)
            {
                roadView.Owner = GameViewModel.CurrentPlayer;
                roadView.Road.RoadState = RoadState.Road;
            }
        }

        [RelayCommand]
        private void Shuffle()
        {
            Log.Done(GameViewModel.GameModel);

            var currentStars = GameViewModel.ShownStars;
            List<string> playerIds = GameViewModel.Players.Select( p => p.Id ).ToList();
            var gameModel = GameFactory.CreateGame(GameViewModel.GameModel.GameType, playerIds);
            gameModel.Shuffle();
            GameViewModel.MergeGameModel(gameModel);

            GameViewModel.SetStars();
            GameViewModel.ShownStars = 14;
            GameViewModel.ShownStars = currentStars;
            Debug.Assert(GameViewModel.CurrentPlayer != null);
            GameViewModel.Id = GameViewModel.GameModel.GetHashCode().ToString();
            //OnPropertyChanged(nameof(GameViewModel));

        }




        /// <summary>
        ///     This is a loggable event.  in the case of a Service, this would be a service call.
        /// </summary>
        /// <param name="buildingKey"></param>
        private void Building_Upgrade(BuildingKey buildingKey)
        {


            var bvm = GameViewModel.Buildings.FindBuildingViewModel(buildingKey);
            if (bvm is null) return;
            Log.Done(GameViewModel.GameModel);
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


            //
            //  turn off all the Stars after you build a building
            GameViewModel.ShownStars = 14;

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

        [RelayCommand]
        private void Undo()
        {
            Log.Undo(GameViewModel);
        }


        [RelayCommand]
        private void Redo()
        {
            Log.Redo(GameViewModel);
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
