using System.Collections.Generic;
using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.UI;
using Microsoft.UI.Xaml.Media;
using Windows.UI;

namespace Catan3.Models
{

    public enum CurrentColor { PrimaryBackground, SecondaryBackground, Foreground }

    public partial class PlayerColorViewModel : ObservableObject
    {
        public PlayerColorViewModel(Color foreground, Color primary, Color secondary)
        {
            _primaryBackground = primary;
            _secondaryBackground = secondary;
            _foreground = foreground;

            OnPrimaryBackgroundChanged(primary);
            OnSecondaryBackgroundChanged(secondary);
            OnForegroundChanged(foreground);
        }

        [ObservableProperty]
        private Color _primaryBackground;

        [ObservableProperty]
        private Color _secondaryBackground;

        [ObservableProperty]
        private Color _foreground;

        [ObservableProperty]
        private Brush _foregroundBrush = BrushCache.GetSolidColorBrush(Colors.White);

        [ObservableProperty]
        private Brush _backgroundBrush = BrushCache.GetSolidColorBrush(Colors.Black);

        partial void OnPrimaryBackgroundChanged(Color value)
        {
            BackgroundBrush = BrushCache.GetGradientBrush(value, SecondaryBackground);
        }

        partial void OnSecondaryBackgroundChanged(Color value)
        {
            BackgroundBrush = BrushCache.GetGradientBrush(PrimaryBackground, value);
        }

        partial void OnForegroundChanged(Color value)
        {
            ForegroundBrush = BrushCache.GetSolidColorBrush(value);
        }
    }


    public partial class EditPlayerViewModel : ObservableObject
    {
       

        [ObservableProperty]
        private ObservableCollection<PlayerViewModel> _players;

        [ObservableProperty]
        private PlayerViewModel _selectedPlayer;

        public EditPlayerViewModel(IList<PlayerViewModel> players)
        {
            Players = [.. players];
            SelectedPlayer = players[0];
        }

        partial void OnSelectedPlayerChanged(PlayerViewModel value)
        {

            this.TraceMessage($"Selected {value.Name}");

        }
    }
}
