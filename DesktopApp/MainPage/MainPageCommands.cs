using System;
using Catan3.Shared.Models;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
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
        private void SaveAs()
        {
            try
            {
                // Send SaveAs request to GameMessageService - it will handle file dialog and persistence
                Messenger.Send(new SaveAsRequestMessage());
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
