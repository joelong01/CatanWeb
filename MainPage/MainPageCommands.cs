using System;
using System.Threading.Tasks;
using Catan3.Controller;
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
            try
            {
                var uncompressedLog = GameController.GetSerializableLog(); // this always comes back the same
                var json = SerializationHelper.JsonSerialize(uncompressedLog);
                var compressedBytes = SerializationHelper.Compress(json);
                await _fileService.SaveFileAsync(compressedBytes);
            }
            catch (Exception ex)
            {
                this.TraceMessage($"Failed SaveAs: {ex.Message}");
            }
            finally
            {
                ShowCommands = false;
            }
        }
        [RelayCommand]
        private async Task SaveAs()
        {
            try
            {
                var uncompressedLog = GameController.GetSerializableLog(); // this always comes back the same
                var json = SerializationHelper.JsonSerialize(uncompressedLog);
                var compressedBytes = SerializationHelper.Compress(json);
                await _fileService.SaveFileAsAsync($"GameModel DoneDepth={GameController.DoneCount}", compressedBytes);
            }
            catch (Exception ex)
            {
                this.TraceMessage($"Failed SaveAs: {ex.Message}");
            }
            finally
            {
                ShowCommands = false;
            }
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
                Messenger.Send(new EndGame());
                GameController = new GameController();
                RegisterMessages();
                var gameModel = GameController.OpenSerializableLog(compressedBytes);
                this.GameViewModel = new GameViewModel(gameModel, _playerDatabase);
                GameViewModel.UpdateLayout();
                GameViewModel.SetGameStars();
            }
            catch (Exception ex)
            {
                this.TraceMessage($"Failed to deserialize or apply the game data: {ex.Message}");
            }
            finally
            {
                ShowCommands = false;
            }
        }
        [RelayCommand]
        private void ToggleShowCommands()
        {
            ShowCommands = !ShowCommands;
        }
    }
}
