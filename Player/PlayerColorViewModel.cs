using System.Text.Json.Serialization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Messaging;
using Microsoft.UI;
using Microsoft.UI.Xaml.Media;
using Windows.UI;

namespace Catan3.Models
{
    /// <summary>
    ///     this keeps the player's colors/brushes straight.
    ///     This is serializable.  When a color changes, it Broadcasts a PlayerColorChanged message so anything that depends on colors can do an update.
    /// </summary>
    public partial class PlayerColorViewModel : ObservableRecipient
    {

        public PlayerColorViewModel(string playerId, Color foreground, Color primaryBackground, Color secondaryBackground)
        {
            _primaryBackground = primaryBackground;
            _secondaryBackground = secondaryBackground;
            _foreground = foreground;
            _playerId = playerId;
            OnPrimaryBackgroundChanged(primaryBackground);
            OnSecondaryBackgroundChanged(secondaryBackground);
            OnForegroundChanged(foreground);
        }
        [ObservableProperty]
        private Color _primaryBackground;
        [ObservableProperty]
        private Color _secondaryBackground;
        [ObservableProperty]
        private Color _foreground;
        [ObservableProperty]
        private string _playerId;

        [ObservableProperty]
        [property: JsonIgnore]
        private Brush _foregroundBrush = BrushCache.GetSolidColorBrush(Colors.White);


        [ObservableProperty]
        [property: JsonIgnore]
        private Brush _backgroundBrush = BrushCache.GetSolidColorBrush(Colors.Black);

        partial void OnPrimaryBackgroundChanged(Color value)
        {
            BackgroundBrush = BrushCache.GetGradientBrush(value, SecondaryBackground);
            Messenger.Send(new PlayerColorChanged(this));

        }
        partial void OnSecondaryBackgroundChanged(Color value)
        {
            BackgroundBrush = BrushCache.GetGradientBrush(PrimaryBackground, value);
            Messenger.Send(new PlayerColorChanged(this));
        }
        partial void OnForegroundChanged(Color value)
        {
            ForegroundBrush = BrushCache.GetSolidColorBrush(value);
            Messenger.Send(new PlayerColorChanged(this));
        }

        
    }
}
