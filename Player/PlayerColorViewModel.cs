using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Messaging;
using Microsoft.UI;
using Microsoft.UI.Xaml.Media;
using System.Text.Json.Serialization;
using Windows.UI;
namespace Catan3.Models
{
    /// <summary>
    /// Represents the player's color view model, including foreground and background colors and brushes.
    /// This class is serializable and broadcasts a PlayerColorChanged message when a color changes.
    /// </summary>
    public partial class PlayerColorViewModel : ObservableRecipient
    {
        /// <summary>
        /// Initializes a new instance of the PlayerColorViewModel class with the specified player ID and colors.
        /// </summary>
        /// <param name="playerId">The ID of the player.</param>
        /// <param name="foreground">The foreground color.</param>
        /// <param name="primaryBackground">The primary background color.</param>
        /// <param name="secondaryBackground">The secondary background color.</param>
        public PlayerColorViewModel(string playerId, Color foreground, Color primaryBackground, Color secondaryBackground)
        {
            PrimaryBackground = primaryBackground;
            SecondaryBackground = secondaryBackground;
            Foreground = foreground;
            PlayerId = playerId;
            OnPrimaryBackgroundChanged(primaryBackground);
            OnSecondaryBackgroundChanged(secondaryBackground);
            OnForegroundChanged(foreground);
        }

        /// <summary>
        /// Gets or sets the primary background color.
        /// </summary>
        [ObservableProperty]
        public partial Color PrimaryBackground { get; set; }

        /// <summary>
        /// Gets or sets the secondary background color.
        /// </summary>
        [ObservableProperty]
        public partial Color SecondaryBackground { get; set; }

        /// <summary>
        /// Gets or sets the foreground color.
        /// </summary>
        [ObservableProperty]
        public partial Color Foreground { get; set; }

        /// <summary>
        /// Gets or sets the player ID.
        /// </summary>
        [ObservableProperty]
        public partial string PlayerId { get; set; }

        /// <summary>
        /// Gets or sets the foreground brush. This property is not serialized.
        /// </summary>
        [ObservableProperty]
        [property: JsonIgnore]
        public partial Brush ForegroundBrush { get; set; } = BrushCache.GetSolidColorBrush(Colors.White);

        /// <summary>
        /// Gets or sets the background brush. This property is not serialized.
        /// </summary>
        [ObservableProperty]
        [property: JsonIgnore]
        public partial Brush BackgroundBrush { get; set; } = BrushCache.GetSolidColorBrush(Colors.Black);

        /// <summary>
        /// Handles changes to the primary background color.
        /// </summary>
        /// <param name="value">The new primary background color.</param>
        partial void OnPrimaryBackgroundChanged(Color value)
        {
            BackgroundBrush = BrushCache.GetGradientBrush(value, SecondaryBackground);
            Messenger.Send(new PlayerColorChanged(this));
        }

        /// <summary>
        /// Handles changes to the secondary background color.
        /// </summary>
        /// <param name="value">The new secondary background color.</param>
        partial void OnSecondaryBackgroundChanged(Color value)
        {
            BackgroundBrush = BrushCache.GetGradientBrush(PrimaryBackground, value);
            Messenger.Send(new PlayerColorChanged(this));
        }

        /// <summary>
        /// Handles changes to the foreground color.
        /// </summary>
        /// <param name="value">The new foreground color.</param>
        partial void OnForegroundChanged(Color value)
        {
            ForegroundBrush = BrushCache.GetSolidColorBrush(value);
            Messenger.Send(new PlayerColorChanged(this));
        }

        /// <summary>
        /// Converts a Color to a Brush.
        /// </summary>
        /// <param name="color">The color to convert.</param>
        /// <returns>A Brush representing the specified color.</returns>
        public Brush ColorToBrush(Color color)
        {
            return BrushCache.GetSolidColorBrush(color);
        }
    }
}
