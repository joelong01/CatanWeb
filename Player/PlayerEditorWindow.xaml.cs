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
using Catan3.Models;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace Catan3.Player
{

    /// <summary>
    /// An empty window that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class PlayerEditorWindow : Window
    {
        public static PlayerEditorWindow? EditorWindow;
        public PlayerEditorWindow()
        {
            this.InitializeComponent();
            EditorWindow = this;
        }
        /// <summary>
        ///     Allow the Page's view model to be set when you have a reference to the Window
        /// </summary>
        public EditPlayerViewModel ViewModel
        {
            get => PlayerEditorPage.ViewModel;
            set => PlayerEditorPage.ViewModel = value;
        }
    }
}
