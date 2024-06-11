using System.
    Collections.Generic;
using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.UI.Xaml.Controls;

namespace Catan3.Models
{
    public partial class NewGameViewModel : ObservableObject
    {
        [ObservableProperty]
        private ObservableCollection<PlayerViewModel> _players = [];

        public List<PlayerViewModel> PlayingPlayers { get; internal set; } = [];

        [ObservableProperty]
        private ObservableCollection<GameType> _games  = [GameType.Regular, GameType.Expansion];

        [ObservableProperty]
        private GameType _selectedGame = GameType.Expansion;

        public NewGameViewModel(IList<PlayerViewModel> players)
        {
            _players.AddRange(players);
        }

        public void Players_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            foreach (PlayerViewModel player in e.AddedItems)
            {
                this.TraceMessage($"adding {player.Name}");
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
