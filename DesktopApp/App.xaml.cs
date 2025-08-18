using System;
using System.IO;
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
        /// Global flag to enable recording of user actions for test scenario generation.
        /// When true, disables normal trace messages and records actions instead.
        /// </summary>
        public static bool RecordMode { get; set; } = true;
        
        /// <summary>
        /// Path to test file to auto-load on startup (bypasses NewGame dialog)
        /// </summary>
#nullable enable
        public static string? TestFilePath { get; set; }
#nullable disable
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
            this.TraceMessage($"Command Line arguments: {args?.Arguments}");
            
            // Check for file activation arguments
            CheckForFileActivation();
            
            ConfigureTestMode(args?.Arguments);
            InitializeWindow();
        }
        
        /// <summary>
        /// Checks if the app was activated to open a .catan file.
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
                            if (file != null && file.Path.EndsWith(".catan", StringComparison.OrdinalIgnoreCase))
                            {
                                this.TraceMessage($"File activation: {file.Path}");
                                
                                // Use the file path directly - test already handles temp file creation
                                TestFilePath = file.Path;
                                
                                // Enable test mode for file activation
                                Timeline.AllowDependentAnimations = false;
                                this.TraceMessage($"Set TestFilePath to: {TestFilePath}");
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
        }
        public Window m_window;
        public Window MainWindow => m_window;

#nullable enable
        /// <summary>
        /// Enables deterministic, automation-friendly settings when launched with --test or CATAN_TEST=1.
        /// Also supports --load-test-file=path to auto-load a specific game file.
        /// </summary>
        private static void ConfigureTestMode(string? launchArgs)
        {
            try
            {
                bool testMode = (launchArgs?.Contains("--test") ?? false) ||
                                (Environment.GetEnvironmentVariable("CATAN_TEST") == "1");
                
                // Check for test file loading
                if (!string.IsNullOrEmpty(launchArgs))
                {
                    const string loadTestFilePrefix = "--load-test-file=";
                    var loadTestFileIndex = launchArgs.IndexOf(loadTestFilePrefix);
                    if (loadTestFileIndex >= 0)
                    {
                        var startIndex = loadTestFileIndex + loadTestFilePrefix.Length;
                        var endIndex = launchArgs.IndexOf(' ', startIndex);
                        if (endIndex == -1) endIndex = launchArgs.Length;
                        
                        var testFilePath = launchArgs.Substring(startIndex, endIndex - startIndex).Trim('"');
                        if (File.Exists(testFilePath))
                        {
                            // Copy to temp file to preserve original
                            var tempFilePath = Path.Combine(Path.GetTempPath(), $"test-{Guid.NewGuid()}.catan");
                            File.Copy(testFilePath, tempFilePath, overwrite: true);
                            TestFilePath = tempFilePath;
                            testMode = true; // Auto-enable test mode when loading test file
                        }
                    }
                }
                
                if (!testMode) return;
                Timeline.AllowDependentAnimations = false;
            }
            catch { }
        }
#nullable disable
    }
}
