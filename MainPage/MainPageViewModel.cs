using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Text.Json;
using Catan3.Utility;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using Microsoft.UI;
using Windows.Security.Cryptography.Core;
using Windows.Storage.Provider;
using Windows.Storage;
using System.Threading.Tasks;
using System.Runtime.InteropServices.WindowsRuntime;

namespace Catan3.Models
{





    public partial class MainPageViewModel : ObservableRecipient
    {

        [ObservableProperty]
        GameViewModel _gameViewModel = GameViewModel.Default;

        [ObservableProperty]
        private Log _log;

        private readonly IFileService _fileService;

        public IMessenger MessageService => this.Messenger;
        public MainPageViewModel(IFileService fileService, GameType selectedGame, List<PlayerViewModel> playingPlayers)
        {
            FunctionTimer.Enabled = false;
            _fileService = fileService;
            RegisterMessages();
            // create a new GameModel - this would usually come from the service

            List<string> playerIds = playingPlayers.Select( p => p.Id ).ToList();
            var gameModel = GameFactory.CreateGame(selectedGame, playerIds);
            var gvm = new GameViewModel(gameModel);
            this.GameViewModel = gvm;
            GameViewModel.UpdateLayout();
            GameViewModel.SetStars();
            Log = new Log(selectedGame);
            Log.Done(GameViewModel.GameModel);


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
                case GameAction.NextPlayer:
                    NextPlayer();
                    break;
            }
        }

        private void Road_Purchase(RoadKey roadKey)
        {

            var roadView = GameViewModel.Roads.FirstOrDefault(r => r.Road.RoadKey == roadKey);
            if (roadView is null) return;
            if (roadView.Road.OwnerId is not null) return;
            //
            //  this will be the state we go back to when we Undo
            if (roadView.Road.RoadState == RoadState.Highlighted) roadView.Road.RoadState = RoadState.Unowned;

            roadView.Road.OwnerId = GameViewModel.CurrentPlayer.Id;
            roadView.Road.RoadState = RoadState.Road;
            Log.Done(GameViewModel.GameModel);

        }

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
        ///     This is a loggable event.  in the case of a Service, this would be a service call.
        /// </summary>
        /// <param name="buildingKey"></param>
        private void Building_Upgrade(BuildingKey buildingKey)
        {


            var bvm = GameViewModel.Buildings.FindBuildingViewModel(buildingKey);
            if (bvm is null) return;

            switch (bvm.Building.BuildingState)
            {
                case BuildingState.Empty:
                case BuildingState.Highlighted:
                case BuildingState.Stars:

                    bvm.Building.BuildingState = BuildingState.Settlement;
                    bvm.Building.OwnerId = GameViewModel.CurrentPlayer.Id;

                    break;
                case BuildingState.Settlement:


                    Debug.Assert(bvm.Building.OwnerId != null);
                    if (bvm.Building.OwnerId != GameViewModel.CurrentPlayer.Id) return;
                    bvm.Building.BuildingState = BuildingState.City;

                    break;
                case BuildingState.City:


                    Debug.Assert(bvm.Building.OwnerId != null);
                    if (bvm.Building.OwnerId != GameViewModel.CurrentPlayer.Id) return;
                    bvm.Building.BuildingState = BuildingState.Knight;

                    break;
                case BuildingState.Knight:
                    break;
            }


            //
            //  turn off all the Stars after you build a building
            GameViewModel.ShownStars = 14;
            Log.Done(GameViewModel.GameModel);
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
            Log.Done(GameViewModel.GameModel);
        }

        [RelayCommand]
        private async Task Save()
        {

            var file = await _fileService.SaveFileAsync("Test Game");
            if (file == null) return;
            using (new FunctionTimer("compression", true))
            {
                var uncompressedLog = Log.GetSerializableLog();
                var json = SerializationHelper.JsonSerialize(uncompressedLog);
                var compressedBytes = SerializationHelper.CompressString(json);
                CachedFileManager.DeferUpdates(file);

                await FileIO.WriteBytesAsync(file, compressedBytes);
            }

          
            
            // Let Windows know that we're finished changing the file so the other app can update the remote version of the file.
            FileUpdateStatus status = await CachedFileManager.CompleteUpdatesAsync(file);
            if (status == FileUpdateStatus.Complete)
            {
                // File saved
                this.TraceMessage($"DoneCount={Log.DoneCount}");
            }
            else
            {
                // Error saving file
                this.TraceMessage("Error saving file.");
            }


        }
        [RelayCommand]
        private async Task Open()
        {
            var file = await _fileService.OpenFileAsync();
            if (file == null) return;  // Exit if no file was selected or there was an error

            // Read the compressed data from the file
            var compressedData = await FileIO.ReadBufferAsync(file);
            var compressedBytes = compressedData.ToArray();
            var decompressedJson = SerializationHelper.DecompressString(compressedBytes);

            // Deserialize the JSON back into your Log or relevant data structure
            try
            {
                var log = SerializationHelper.JsonDeserialize<Log>(decompressedJson);
                if (log == null)
                {
                    this.TraceMessage("Error: Failed to load the game data.");
                    return;
                }

                this.Log = log;
                var gameModel = Log.CurrentState();
                GameViewModel.MergeGameModel(gameModel);
                GameViewModel.SetStars();
                GameViewModel.ShownStars = 14;

            }
            catch (Exception ex)
            {
                this.TraceMessage($"Failed to deserialize or apply the game data: {ex.Message}");
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
