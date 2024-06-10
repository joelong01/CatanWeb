using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;


namespace Catan3.Controls
{
    public class PlayerSettingsDialog : ContentDialog
    {
        public PlayerSettingsDialog()
        {
            this.Title = "Player Settings";
            this.PrimaryButtonText = "Close";
            this.SecondaryButtonText = "Cancel";
            this.DefaultButton = ContentDialogButton.Primary;

            // Load the custom content
            this.Content = new PlayerSettingsDialogContent();

            // Handle button clicks
            this.PrimaryButtonClick += PlayerSettingsDialog_PrimaryButtonClick;
            this.SecondaryButtonClick += PlayerSettingsDialog_SecondaryButtonClick;
        }

        private void PlayerSettingsDialog_PrimaryButtonClick(ContentDialog sender, ContentDialogButtonClickEventArgs args)
        {
            // Handle Close button click
        }

        private void PlayerSettingsDialog_SecondaryButtonClick(ContentDialog sender, ContentDialogButtonClickEventArgs args)
        {
            // Handle Cancel button click
        }
    }

    public sealed partial class PlayerSettingsDialogContent : UserControl
    {
        public PlayerSettingsDialogContent()
        {
            this.InitializeComponent();
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            // Handle close button click
            // You can add logic here to handle the 'Close' button click event
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            // Handle cancel button click
            // You can add logic here to handle the 'Cancel' button click event
        }
    }
}
