using System;
using System.Diagnostics;
using System.Threading.Tasks;
using Catan3.Controller;
using Catan3.Utility;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using Microsoft.UI.Xaml.Controls;
namespace Catan3.Models
{
    public partial class MainPageViewModel : ObservableRecipient
    {
        [RelayCommand]
        private void Save()
        {
            try
            {
                Messenger.Send(new PersistGameMessage(LocalPersistActions.Save, ""));
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
                await _fileService.SaveAsync($"GameModel DoneDepth={GameController.DoneCount}", compressedBytes);
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
                Debug.Assert(MainWindow.Instance is not null);
                var filePath = await MainWindow.FileService.PickFile(MainWindow.Instance, [".catan"]);
                if (filePath is not null && filePath != "")
                {
                    var compressedBytes = await MainWindow.FileService.OpenAsync(filePath);

                    if (compressedBytes is null)
                    {
                        this.TraceMessage("Unable to open file");
                        return;
                    }

                    Messenger.Send(new EndGame());
                    GameController = new GameController(_fileService, filePath);
                    RegisterMessages();
                    throw new NotImplementedException("implement this with message passing");
                    //var gameModel = GameController.OpenSerializableLog(compressedBytes);
                    //this.GameViewModel = new GameViewModel(gameModel, _playerDatabase);
                    //GameViewModel.UpdateLayout();
                    //GameViewModel.SetGameStars();
                }
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
