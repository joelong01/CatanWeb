using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using Catan3.Shared.Models;
using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.UI.Xaml.Controls;
namespace Catan3.Models
{
    public partial class NewGameViewModel : ObservableObject
    {
        [ObservableProperty]
        public partial ObservableCollection<PlayerViewModel> Players { get; set; } = [];
        public List<PlayerViewModel> PlayingPlayers { get; internal set; } = [];
        [ObservableProperty]
        public partial ObservableCollection<GameType> Games { get; private set; } = [GameType.Regular, GameType.Expansion];
        [ObservableProperty]
        public partial GameType SelectedGame { get; set; } = GameType.Expansion;

        [ObservableProperty]
        public partial bool ShowMenu { get; set; } = false;

        /// <summary>
        /// Gets whether the Start button should be enabled based on settings validation
        /// </summary>
        [ObservableProperty]
        public partial bool IsStartEnabled { get; set; } = true;
        public NewGameViewModel(IList<PlayerViewModel> players)
        {
            Players.AddRange(players);
            _ = CheckSettingsValidation();
        }

        /// <summary>
        /// Checks if all settings are valid and updates IsStartEnabled accordingly
        /// </summary>
        public async Task<bool> CheckSettingsValidation()
        {
            try
            {
                var validationResult = await SettingsModel.ValidateAsync();
                IsStartEnabled = validationResult.IsValid;
                return validationResult.IsValid;
            }
            catch (Exception ex)
            {
                this.TraceMessage($"Error checking settings validation: {ex.Message}");
                IsStartEnabled = false;
                return false;
            }
        }
        public void Players_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            foreach (PlayerViewModel player in e.AddedItems)
            {

                PlayingPlayers.Add(player);
            }
            foreach (PlayerViewModel player in e.RemovedItems)
            {
                this.TraceMessage($"removing {player.Name}");
                PlayingPlayers.Remove(player);
            }
        }
        public void Game_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (e.AddedItems.Count == 1 && e.AddedItems[0] is not null)
            {
                SelectedGame = ( GameType )( object )e.AddedItems[0];
            }
        }
    }
}
