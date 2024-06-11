using System;
using Catan3.Models;
using System.Threading.Tasks;
using CommunityToolkit.WinUI.UI.Controls;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Windows.ApplicationModel.DataTransfer;
using Windows.Storage;
using System.IO;
using Windows.Storage.Pickers;
using Windows.Storage.Streams;
using WinRT.Interop;
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
            get => ( PlayerSettingsViewModel )GetValue(ViewModelProperty);
            set => SetValue(ViewModelProperty, value);
        }
        public PlayerEditorPage()
        {
            this.InitializeComponent();
            this.SizeChanged += (object sender, SizeChangedEventArgs e) =>
            {
                this.TraceMessage($"W={e.NewSize.Width} H={e.NewSize.Height}");
            };
        }
        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            PlayerEditorWindow.EditorWindow?.Close();
        }

        
    }
}
