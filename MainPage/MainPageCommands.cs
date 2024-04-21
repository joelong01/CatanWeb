using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Catan3.Utility;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;

namespace Catan3.Models
{





    public partial class MainPageViewModel : ObservableRecipient
    {



        [RelayCommand]
        private void Shuffle()
        {
            if (GameViewModel.GameModel.GameState != GameState.PickingBoard) return;
            var currentStars = GameViewModel.ShownStars;
            List<string> playerIds = GameViewModel.Players.Select( p => p.Id ).ToList();
            var gameModel = GameFactory.CreateGame(GameViewModel.GameModel.GameType, playerIds);
            gameModel.GameState = GameState.PickingBoard;
            gameModel.Shuffle();
            
            GameViewModel.MergeGameModel(gameModel);

            GameViewModel.SetStars();
            GameViewModel.ShownStars = 14;
            GameViewModel.ShownStars = currentStars;
            Debug.Assert(GameViewModel.CurrentPlayer != null);
            GameViewModel.Id = GameViewModel.GameModel.GetHashCode().ToString();
            Log.Done(GameViewModel.GameModel);

        }



        /// <summary>
        ///     There are a couple of ways to get here:
        ///         - binding to MainPageViewModel.NextPlayerCommand
        ///         - other models can send the DoAction message with the GameAction.
        ///         
        ///  when GameViewModel.CurrentPlayer is set, it registers for the OnCurrentPlayerChanged
        ///  event and update the undelying GameModel and then sends the CurrentPlayerChanged message.
        ///  other controls like Roads and Buildings subscribe to this event to do things like update
        ///  colors.
        /// </summary>

        [RelayCommand]
        private void NextPlayer()
        {
            Debug.Assert(GameViewModel.CurrentPlayer != null);
            Messenger.Send(new TurnEnding(GameViewModel.CurrentPlayer.Id));

           
           
           
            // change player
            int index = GameViewModel.Players.IndexOf(GameViewModel.CurrentPlayer);
            Debug.Assert(index >= 0);
            index++;
            index %= GameViewModel.Players.Count;
            GameViewModel.CurrentPlayer = GameViewModel.Players[index];

           
            Messenger.Send(new TurnStarting(GameViewModel.CurrentPlayer.Id));

            // log the changes
            Log.Done(GameViewModel.GameModel);
        }

      

        [RelayCommand]
        private async Task Save()
        {
            var uncompressedLog = Log.GetSerializableLog(); // this always comes back the same
            var json = SerializationHelper.JsonSerialize(uncompressedLog);
            var compressedBytes = SerializationHelper.Compress(json);
            await _fileService.SaveFileAsync(compressedBytes);


        }
        [RelayCommand]
        private async Task SaveAs()
        {
            var uncompressedLog = Log.GetSerializableLog(); // this always comes back the same
            var json = SerializationHelper.JsonSerialize(uncompressedLog);
            var compressedBytes = SerializationHelper.Compress(json);
            await _fileService.SaveFileAsAsync($"GameModel DoneDepth={Log.DoneCount}", compressedBytes);


        }
        [RelayCommand]
        private async Task Open()
        {
            try
            {

                var compressedBytes = await _fileService.OpenFileAsync();
                if (compressedBytes is null)
                {
                    this.TraceMessage("Unable to open file");
                    return;
                }

                var decompressedJson = SerializationHelper.Decompress(compressedBytes);

                // Deserialize the JSON back into your Log or relevant data structure

                var savedLog = SerializationHelper.JsonDeserialize<SerializableLog>(decompressedJson);
                if (savedLog == null)
                {
                    this.TraceMessage("Error: Failed to load the game data.");
                    return;
                }

                Log<GameModel> log =  Log<GameModel>.FromSerializableLog(savedLog);

                if (log.GameType == GameViewModel.GameModel.GameType)
                {

                    this.Log = log;
                    var gameModel = Log.CurrentState();
                    GameViewModel.MergeGameModel(gameModel);
                    GameViewModel.SetStars();
                    GameViewModel.ShownStars = 14;
                }

                else
                {
                    var gameModel = log.CurrentState();
                    var gvm = new GameViewModel(gameModel);

                    this.GameViewModel = gvm;

                    GameViewModel.UpdateLayout();
                    GameViewModel.SetStars();
                    Log = log;
                }
                GC.Collect();
            }
            catch (Exception ex)
            {
                this.TraceMessage($"Failed to deserialize or apply the game data: {ex.Message}");
            }
        }



    }



}
