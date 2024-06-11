using Catan3.Models;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;


namespace Catan3.Controls
{
    public class PlayerSettingsDialog : ContentDialog
    {
        public PlayerSettingsDialog(EditPlayerViewModel model)
        {
            
            this.Title = "Player Settings";
            this.PrimaryButtonText = "Close";

            this.DefaultButton = ContentDialogButton.Primary;
            this.Style = ( Style )Application.Current.Resources["ConfigurableWidthContentDialogStyle"];
            this.DialogWidth = 850; // Set desired width here

            // Load the custom content
            var content = new PlayerSettingsDialogCtrl(model);

            this.Content = content;
            // Handle button clicks
            this.PrimaryButtonClick += PlayerSettingsDialog_PrimaryButtonClick;
          
        }

        public static readonly DependencyProperty DialogWidthProperty =
        DependencyProperty.Register(
            nameof(DialogWidth),
            typeof(double),
            typeof(PlayerSettingsDialog),
            new PropertyMetadata(500)); // Default width

        public double DialogWidth
        {
            get => ( double )GetValue(DialogWidthProperty);
            set => SetValue(DialogWidthProperty, value);
        }

        private void PlayerSettingsDialog_PrimaryButtonClick(ContentDialog sender, ContentDialogButtonClickEventArgs args)
        {
            this.Hide();
        }

      
    }

    public sealed partial class PlayerSettingsDialogCtrl : UserControl
    {
        public PlayerSettingsDialogCtrl(EditPlayerViewModel model)
        {
            ViewModel = model;
            this.InitializeComponent();
        }

        public static readonly DependencyProperty ViewModelProperty = DependencyProperty.Register("ViewModel", typeof(EditPlayerViewModel), typeof(PlayerSettingsDialogCtrl), new PropertyMetadata(null));
        public EditPlayerViewModel ViewModel
        {
            get => ( EditPlayerViewModel )GetValue(ViewModelProperty);
            set => SetValue(ViewModelProperty, value);
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
