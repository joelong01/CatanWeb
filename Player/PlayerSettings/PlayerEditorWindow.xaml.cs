using Microsoft.UI.Xaml;
using Catan3.Models;
using Microsoft.UI.Windowing;
using Windows.Graphics;
using Microsoft.UI;
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


           
            var windowManger = WindowManager.Get(this);
            windowManger.IsMaximizable = false;
            windowManger.IsResizable = false;
            windowManger.AppWindow.Resize(new SizeInt32(975, 1200));
            windowManger.PersistenceId = "Catan_Player_Editor";
            
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
