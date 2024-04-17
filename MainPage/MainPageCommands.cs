using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.Contracts;
using System.Linq;
using System.Threading.Tasks;
using Catan.Utility;
using Catan3.Controls;
using Catan3.Utility;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using Microsoft.UI;
using Microsoft.UI.Xaml.Controls;
using Windows.Security.Isolation;
using static System.Collections.Specialized.BitVector32;

namespace Catan3.Models
{





    public partial class MainPageViewModel : ObservableRecipient
    {



        [RelayCommand]
        private void Shuffle()
        {

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
            int index = GameViewModel.Players.IndexOf(GameViewModel.CurrentPlayer);
            Debug.Assert(index >= 0);
            index++;
            index = index % GameViewModel.Players.Count;
            GameViewModel.CurrentPlayer = GameViewModel.Players[index];
            GameViewModel.GameModel.GameState = GameState.WaitingForRoll;

            GameViewModel.GameModel.ThisTurnsRoll = new RollModel();
            GameViewModel.RollViewModel.RollModel = GameViewModel.GameModel.ThisTurnsRoll;
            SetTempGoldTiles();
            foreach (var tile in GameViewModel.Tiles)
            {
                tile.Highlighted = false;
            }
            Log.Done(GameViewModel.GameModel);
        }

        [RelayCommand]
        private void DoRoll(RollModel roll)
        {
            if (GameViewModel.GameModel.GameState != GameState.WaitingForRoll) return;



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

                Log<byte[]> log = Log<byte[]>.FromSerializableLog(savedLog);

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
