using Catan3.Models;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.
namespace Catan3.Player
{
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class PlayerEditorPage : Page
    {
        public static readonly DependencyProperty ViewModelProperty = DependencyProperty.Register("ViewModel", typeof(PlayerSettingsViewModel), typeof(PlayerEditorPage), new PropertyMetadata(null, ViewModelChanged));
        private static void ViewModelChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is PlayerEditorPage page && e.NewValue is PlayerSettingsViewModel viewModel)
            {
                page.DataContext = viewModel;
            }

        }

        public PlayerSettingsViewModel ViewModel
        {
            get => (PlayerSettingsViewModel)GetValue(ViewModelProperty);
            set => SetValue(ViewModelProperty, value);
        }
        public PlayerEditorPage()
        {
            this.InitializeComponent();

        }
        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            PlayerEditorWindow.EditorWindow?.Close();
        }

    }
}
