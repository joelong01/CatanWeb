using System;
using Catan3.Models;
using ColorCode.Compilation.Languages;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media.Imaging;
using Windows.Graphics;
using WinUIEx;
// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.
namespace Catan3.Player
{
    /// <summary>
    /// An empty window that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class PlayerEditorWindow : WinUIEx.WindowEx
    {
        public static PlayerEditorWindow? EditorWindow;
        public PlayerEditorWindow()
        {
            this.InitializeComponent();
            EditorWindow = this;


            // Set the icon for the title bar
            this.SetIcon("ms-appx:///Assets/AppIcon.ico");

        }

        /// <summary>
        ///     Allow the Page's view model to be set when you have a reference to the Window
        /// </summary>
        public PlayerSettingsViewModel ViewModel
        {
            get => PlayerEditorPage.ViewModel;
            set => PlayerEditorPage.ViewModel = value;
        }
    }
}
