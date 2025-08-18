using System;
using System.IO;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media.Animation;
using System.Threading.Tasks;

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
        /// Global flag to enable recording of user actions for test scenario generation.
        /// When true, disables normal trace messages and records actions instead.
        /// Set to true manually when you want to record a game session.
        /// </summary>
        public static bool RecordMode { get; set; } = false;

        /// <summary>
        /// Path to test file to auto-load on startup (bypasses NewGame dialog)
        /// </summary>

        public static string? ActivatedFilePath { get; set; } = null;
        public static bool IsTestMode { get; private set; } = false; // set based on extension
        /// <summary>
        /// Initializes the singleton application object.  This is the first line of authored code
        /// executed, and as such is the logical equivalent of main() or WinMain().
        /// </summary>
#nullable disable
        public App()
        {
            this.InitializeComponent();
        }
#nullable enable
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
            this.TraceMessage($"Command Line arguments: {args?.Arguments}");

            // Check for file activation arguments
            CheckForFileActivation();

            InitializeWindow();
        }

        /// <summary>
        /// Checks if the app was activated to open a file.  We support 2 file types:
        ///     .catan       Normal game file.
        ///     .catan_test  Test scenario file.
        /// </summary>
        private void CheckForFileActivation()
        {
            try
            {
                var activationArgs = Microsoft.Windows.AppLifecycle.AppInstance.GetCurrent().GetActivatedEventArgs();
                this.TraceMessage($"Activation kind: {activationArgs?.Kind}");

                if (activationArgs?.Kind == Microsoft.Windows.AppLifecycle.ExtendedActivationKind.File)
                {
                    if (activationArgs.Data is Windows.ApplicationModel.Activation.FileActivatedEventArgs fileArgs)
                    {
                        this.TraceMessage($"File activation detected with {fileArgs.Files?.Count} files");

                        if (fileArgs.Files?.Count > 0)
                        {
                            var file = fileArgs.Files[0] as Windows.Storage.StorageFile;
                            if (file is null)
                            {
                                this.TraceMessage("No valid file found in activation arguments.");
                                return;
                            }
                            bool isNormalGameFile = file.Path.EndsWith(".catan", StringComparison.OrdinalIgnoreCase);
                            bool isTestFile = file.Path.EndsWith(".catan_test", StringComparison.OrdinalIgnoreCase);
                            this.TraceMessage($"File activation: {file.Path}");

                            if (isTestFile)
                            {
                                // Enable test mode for file activation
                                Timeline.AllowDependentAnimations = false;
                                // Disable recording mode when running tests
                                RecordMode = false;
                                IsTestMode = true;

                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                this.TraceMessage($"Error checking file activation: {ex.Message}");
            }
        }


        /// <summary>
        /// Initializes the main window if not already initialized.
        /// </summary>
        private void InitializeWindow()
        {
            if (m_window == null)
            {
                m_window = new MainWindow();
                // Delay window activation to allow splash screen to show and background tasks to run
                _ = DelayedActivateAsync();
            }
            else
            {
                // If window already exists, just activate it
                m_window.Activate();
            }
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

            // Give the main window time to fully initialize, then show debug window
            await Task.Delay(100);

            Microsoft.UI.Dispatching.DispatcherQueue.GetForCurrentThread().TryEnqueue(() =>
            {
                // Show debug window by default (especially important during testing)
                try
                {
                    System.Diagnostics.Debug.WriteLine("App: About to show DebugWindow");

                    // Show the window first
                    DebugWindow.Show();



                    // Give it a moment to create, then send messages
                    Microsoft.UI.Dispatching.DispatcherQueue.GetForCurrentThread().TryEnqueue(() =>
                    {
                        DebugWindow.ShowMessage($"🪟 DebugWindow auto-opened (TestMode: {IsTestMode})");

                        // For test mode, add additional message
                        if (IsTestMode)
                        {
                            DebugWindow.ShowMessage("🧪 Test Mode: Enhanced debugging enabled");
                        }
                    });
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"App: Failed to show DebugWindow: {ex.Message}");
                }
            });
        }
        public Window m_window;
        public Window MainWindow => m_window;

    }
}
