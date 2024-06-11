using Microsoft.UI.Xaml;
using Catan3.Models;
using Microsoft.UI.Windowing;
using Windows.Graphics;
using Microsoft.UI;
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
            var appWindowPresenter = this.AppWindow.Presenter as OverlappedPresenter ;
            if (appWindowPresenter is not null)
            {
              //  appWindowPresenter.IsResizable = false;
            }

            var appWindow = GetAppWindowForCurrentWindow();

            // Set the window size
            appWindow.Resize(new SizeInt32(975, 1200));
        //    WindowManager.Get(this).IsMaximizable = false;
        }

        private AppWindow GetAppWindowForCurrentWindow()
        {
            var hWnd = WinRT.Interop.WindowNative.GetWindowHandle(this);
            WindowId windowId = Win32Interop.GetWindowIdFromWindow(hWnd);
            return AppWindow.GetFromWindowId(windowId);
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
