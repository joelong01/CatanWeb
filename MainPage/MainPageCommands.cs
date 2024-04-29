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
        private async Task Save()
        {
            var uncompressedLog = GameController.GetSerializableLog(); // this always comes back the same
            var json = SerializationHelper.JsonSerialize(uncompressedLog);
            var compressedBytes = SerializationHelper.Compress(json);
            await _fileService.SaveFileAsync(compressedBytes);


        }
        [RelayCommand]
        private async Task SaveAs()
        {
            var uncompressedLog = GameController.GetSerializableLog(); // this always comes back the same
            var json = SerializationHelper.JsonSerialize(uncompressedLog);
            var compressedBytes = SerializationHelper.Compress(json);
            await _fileService.SaveFileAsAsync($"GameModel DoneDepth={GameController.DoneCount}", compressedBytes);


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

                var gameModel = GameController.OpenSerializableLog(compressedBytes);

                //
                // if the GameTypes are the same as the current one, I don't need 
                // to create a new board - just merge the gamestate in from the GameModel
                if (gameModel.GameType == GameViewModel.GameModel.GameType)
                {
                    GameViewModel.MergeGameModel(gameModel);
                    GameViewModel.SetStars();
                    GameViewModel.ShownStars = 14;
                }

                else
                {
                    // different game type -- create the ViewModel
                    var gvm = new GameViewModel(gameModel);

                    this.GameViewModel = gvm;

                    GameViewModel.UpdateLayout();
                    GameViewModel.SetStars();

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
