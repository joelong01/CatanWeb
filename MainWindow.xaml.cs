using System.ComponentModel;
using Microsoft.UI.Xaml;
using Windows.UI.ViewManagement;
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
