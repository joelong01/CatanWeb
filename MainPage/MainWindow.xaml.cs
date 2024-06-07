using Microsoft.UI.Xaml;
using Windows.UI.ViewManagement;
using Windows.ApplicationModel;
using Windows.Management.Core;
// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.
namespace Catan3
{
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            this.InitializeComponent();
            this.Content = new MainPage(); // Set MainPage as the content of MainWindow
            this.Activated += MainWindow_Activated;
            this.Closed += MainWindow_Closed;
        }
        private void MainWindow_Activated(object sender, WindowActivatedEventArgs args)
        {
            this.Activated -= MainWindow_Activated;
            RestoreWindowPositionAndState();
        }
        private void MainWindow_Closed(object sender, WindowEventArgs args)
        {
            var window = sender as MainWindow;
            if (window is null) return;
            Microsoft.UI.Windowing.AppWindow appWindow = window.AppWindow;
            // Capture window position
            var position = appWindow.Position;
            var size = appWindow.Size;
            // Save position, size, and window state
            var localSettings = ApplicationDataManager.CreateForPackageFamily(Package.Current.Id.FamilyName).LocalSettings;
            if (localSettings is null) return;
            localSettings.Values["WindowPosition"] = $"{position.X},{position.Y}";
            localSettings.Values["WindowSize"] = $"{size.Width},{size.Height}";
            localSettings.Values["WindowState"] = appWindow.Presenter.Kind.ToString();
        }
        private void RestoreWindowPositionAndState()
        {
            var localSettings = ApplicationDataManager.CreateForPackageFamily(Package.Current.Id.FamilyName).LocalSettings;
            if (localSettings is null) return;
            //  var localSettings = Windows.Storage.ApplicationData.Current.LocalSettings.Values;
            object? position = localSettings.Values["WindowPosition"];
            object? size = localSettings.Values["WindowSize"];
            object? windowState = localSettings.Values["WindowState"];
            if (position is null || position is not string posString) return;
            if (size is null || size is not string sString) return;
            if (windowState is null || windowState.GetType() != typeof(string)) return;
            var positionParts = posString.Split(',');
            var sizeParts = sString.Split(',');
            var x = int.Parse(positionParts[0]);
            var y = int.Parse(positionParts[1]);
            var width = int.Parse(sizeParts[0]);
            var height = int.Parse(sizeParts[1]);
            Microsoft.UI.Windowing.AppWindow appWindow = this.AppWindow;
            // Restore position and size
            appWindow.MoveAndResize(new Windows.Graphics.RectInt32(x, y, width, height));
            //  var view = ApplicationView.GetForCurrentView();
            // Restore window state
            switch (windowState)
            {
                case "Maximized":
                    //  view.TryEnterFullScreenMode();
                    break;
                case "Minimized":
                    // Directly setting a window to minimized state might not be supported. Handle as needed.
                    break;
                case "Default":
                    // if (view.IsFullScreen)
                    {
                        //     view.ExitFullScreenMode();
                    }
                    break;
            }
        }
        public void MaximizeWindow()
        {
            var view = ApplicationView.GetForCurrentView();
            if (!view.IsFullScreenMode)
            {
                // Enters full-screen mode
                view.TryEnterFullScreenMode();
            }
            else
            {
                // Optional: Exit full-screen if already in full-screen
                view.ExitFullScreenMode();
            }
            // Adjust WindowStyle equivalent if needed
        }
    }
}
