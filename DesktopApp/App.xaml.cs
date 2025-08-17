using System;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media.Animation;
using System.Threading.Tasks;
#nullable disable
// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.
namespace Catan3
{
    /// <summary>
    /// Provides application-specific behavior to supplement the default Application class.
    /// </summary>
    public partial class App : Application
    {
        /// <summary>
        /// Initializes the singleton application object.  This is the first line of authored code
        /// executed, and as such is the logical equivalent of main() or WinMain().
        /// </summary>
        public App()
        {
            this.InitializeComponent();
        }
        /// <summary>
        /// Invoked when the application is launched.
        /// </summary>
        /// <param name="args">Details about the launch request and process.</param>
        protected override void OnLaunched(Microsoft.UI.Xaml.LaunchActivatedEventArgs args)
        {
#if DEBUG
            DebugSettings.BindingFailed += (sender, e) =>
            {
                System.Diagnostics.Debug.WriteLine(e.Message);
            };
#endif
            ConfigureTestMode(args?.Arguments);
            m_window = new MainWindow();
            
            // Delay window activation to allow splash screen to show and background tasks to run
            _ = DelayedActivateAsync();
        }

        private async Task DelayedActivateAsync()
        {
            // Wait for 1 ms to allow splash screen display and background initialization
            await Task.Delay(1);
            
            // Activate the window on the UI thread
            Microsoft.UI.Dispatching.DispatcherQueue.GetForCurrentThread().TryEnqueue(() =>
            {
                m_window?.Activate();
            });
        }
        public Window m_window;
        public Window MainWindow => m_window;

#nullable enable
        /// <summary>
        /// Enables deterministic, automation-friendly settings when launched with --test or CATAN_TEST=1.
        /// </summary>
        private static void ConfigureTestMode(string? launchArgs)
        {
            try
            {
                bool testMode = (launchArgs?.Contains("--test") ?? false) ||
                                (Environment.GetEnvironmentVariable("CATAN_TEST") == "1");
                if (!testMode) return;
                Timeline.AllowDependentAnimations = false;
            }
            catch { }
        }
#nullable disable
    }
}
