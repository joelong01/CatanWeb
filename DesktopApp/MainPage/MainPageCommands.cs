using System;
using System.Diagnostics;
using System.Threading.Tasks;
using Catan3.Controller;
using Catan3.Utility;
using Catan3.Shared.Models;
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
                var path = await _fileService.PickSaveFileAsync("");
                if (path != "")
                {
                    Messenger.Send(new PersistGameMessage(LocalPersistActions.SaveAs, path));
                }
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
        private void ToggleShowCommands()
        {
            ShowCommands = !ShowCommands;
        }
    }
}
