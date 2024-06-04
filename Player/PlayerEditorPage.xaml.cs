using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Navigation;
using System.Collections.ObjectModel;
using Catan3.Models;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace Catan3.Player
{


    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class PlayerEditorPage : Page
    {
        public static readonly DependencyProperty ViewModelProperty = DependencyProperty.Register("ViewModel", typeof(EditPlayerViewModel), typeof(PlayerEditorPage), new PropertyMetadata(null));
        public EditPlayerViewModel ViewModel
        {
            get => ( EditPlayerViewModel )GetValue(ViewModelProperty);
            set => SetValue(ViewModelProperty, value);
        }


        public PlayerEditorPage()
        {
            this.InitializeComponent();
        }


        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            PlayerEditorWindow.EditorWindow?.Close();
        }

        private void SaveButton_Click(object sender, RoutedEventArgs e)
        {

        }

      
    }
}
