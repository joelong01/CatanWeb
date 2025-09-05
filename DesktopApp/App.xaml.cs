using System;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media.Animation;
using System.Threading.Tasks;
using Catan3.Shared.Interfaces;
using Catan3.Shared.Models;
using System.Text.Json;
using Windows.Storage;
using Microsoft.Extensions.Logging;
using Catan3.Services;
using CommunityToolkit.Mvvm.Messaging;

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
        /// Global log level setting that controls which messages are displayed.
        /// Only messages at or above this level will be shown.
        /// </summary>
        public static GameTraceLevel LogLevel { get; set; } = GameTraceLevel.Trace;

        /// <summary>
        /// Global application settings accessible from anywhere in the app
        /// </summary>
        public static SettingsModel Settings { get; private set; } = new SettingsModel();

        /// <summary>
        /// Global logger instance accessible from anywhere in the app for TraceMessage extensions
        /// </summary>
        public static ILogger? Logger { get; private set; }
        /// <summary>
        /// Initializes the singleton application object.  This is the first line of authored code
        /// executed, and as such is the logical equivalent of main() or WinMain().
        /// </summary>
#nullable disable
        public App()
        {
          //  System.Diagnostics.Debugger.Launch();
            this.InitializeComponent();
        }
#nullable enable
        /// <summary>
        /// Invoked when the application is launched.
        /// </summary>
        /// <param name="args">Details about the launch request and process.</param>
        protected override void OnLaunched(Microsoft.UI.Xaml.LaunchActivatedEventArgs args)
        {
            try
            {
#if DEBUG
                DebugSettings.BindingFailed += (sender, e) =>
                {
                    System.Diagnostics.Debug.WriteLine(e.Message);
                };
#endif
                // Initialize logger first, before any TraceMessage calls
                InitializeLogger();
                
                this.TraceMessage($"Command Line arguments: {args?.Arguments}");

                // Load application settings before anything else
                LoadSettings();

                // Check for file activation arguments
                CheckForFileActivation();

                InitializeWindow();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"FATAL ERROR in OnLaunched: {ex}");
                this.TraceMessage($"FATAL ERROR in OnLaunched: {ex}");
                throw;
            }
        }

        /// <summary>
        /// Initializes the global logger instance with our custom DebugWindow provider
        /// </summary>
        private void InitializeLogger()
        {
            try
            {
                var loggerFactory = LoggerFactory.Create(builder =>
                {
                    builder.AddProvider(new DebugWindowLoggerProvider(LogLevel));
                });

                Logger = loggerFactory.CreateLogger("Catan3");
                this.TraceMessage("Logger initialized successfully");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to initialize logger: {ex.Message}");
                // Continue without logger - old TraceMessage will still work
            }
        }

        /// <summary>
        /// Loads application settings from default configuration and user storage.
        /// First loads defaults from Assets/settings.json, then merges user preferences,
        /// applies environment variable overrides, and saves the final merged settings.
        /// </summary>
        private void LoadSettings()
        {
            try
            {
                // First, load default settings from Assets/settings.json
                LoadDefaultSettings();
                
                // Then, load user settings from storage and merge
                LoadUserSettings();
                
                // Apply environment variable overrides
                ApplyEnvironmentOverrides();
                
                // Always save settings after loading to ensure new defaults are persisted
                SaveSettings();
                
                this.TraceMessage("Settings loaded and saved successfully");
            }
            catch (Exception ex)
            {
                this.TraceMessage($"Failed to load settings, using defaults: {ex.Message}");
                Settings = new SettingsModel();
            }
        }

        /// <summary>
        /// Loads default settings configuration from the Assets/settings.json file.
        /// This file is checked into source control and defines all available settings
        /// with their metadata, validation rules, and default values.
        /// </summary>
        private void LoadDefaultSettings()
        {
            try
            {
                var uri = new Uri("ms-appx:///Assets/settings.json");
                var file = StorageFile.GetFileFromApplicationUriAsync(uri).AsTask().Result;
                var json = FileIO.ReadTextAsync(file).AsTask().Result;
                
                this.TraceMessage($"Loaded settings JSON: {json}");
                
                var defaultSettings = JsonSerializer.Deserialize<SettingsModel>(json);
                if (defaultSettings != null)
                {
                    Settings = defaultSettings;
                    this.TraceMessage($"Loaded {Settings.Settings.Count} default settings");
                }
            }
            catch (Exception ex)
            {
                this.TraceMessage($"Failed to load default settings: {ex.Message}");
                Settings = new SettingsModel();
            }
        }

        /// <summary>
        /// Loads user-specific settings from ApplicationData.LocalSettings and merges
        /// them with the default settings. User values override defaults where they exist.
        /// If no user settings are found, the current defaults will be saved.
        /// </summary>
        private void LoadUserSettings()
        {
            try
            {
                var localSettings = ApplicationData.Current.LocalSettings;
                
                if (localSettings.Values.TryGetValue("UserSettingsJson", out var userSettingsJson))
                {
                    var userSettings = JsonSerializer.Deserialize<SettingsModel>(userSettingsJson.ToString()!);
                    if (userSettings != null)
                    {
                        // Merge user settings with defaults
                        foreach (var userSetting in userSettings.Settings)
                        {
                            var defaultSetting = Settings.GetSetting(userSetting.SettingName);
                            if (defaultSetting != null)
                            {
                                defaultSetting.Value = userSetting.Value;
                            }
                        }
                        this.TraceMessage("Merged user settings with defaults");
                    }
                }
            }
            catch (Exception ex)
            {
                this.TraceMessage($"Failed to load user settings: {ex.Message}");
            }
        }

        /// <summary>
        /// Applies environment variable overrides to settings that specify an
        /// environmentVariable property. Environment variables take precedence
        /// over both default and user-saved values.
        /// </summary>
        private void ApplyEnvironmentOverrides()
        {
            foreach (var setting in Settings.Settings)
            {
                if (!string.IsNullOrEmpty(setting.EnvironmentVariable))
                {
                    var envValue = Environment.GetEnvironmentVariable(setting.EnvironmentVariable);
                    if (!string.IsNullOrEmpty(envValue))
                    {
                        setting.Value = envValue;
                        this.TraceMessage($"Applied environment override for {setting.SettingName}: {envValue}");
                    }
                }
            }
        }

        /// <summary>
        /// Saves the current application settings to both ApplicationData.LocalSettings
        /// and system environment variables (for settings that specify environmentVariable).
        /// This ensures settings persist across app launches and are accessible to external tools.
        /// </summary>
        public static void SaveSettings()
        {
            try
            {
                var localSettings = ApplicationData.Current.LocalSettings;
                
                // Save settings as JSON
                var settingsJson = JsonSerializer.Serialize(Settings);
                localSettings.Values["UserSettingsJson"] = settingsJson;
                
                // Update environment variables for settings that require it
                foreach (var setting in Settings.Settings)
                {
                    if (!string.IsNullOrEmpty(setting.EnvironmentVariable) && setting.Value != null)
                    {
                        Environment.SetEnvironmentVariable(setting.EnvironmentVariable, setting.Value.ToString(), EnvironmentVariableTarget.User);
                    }
                }

                // Broadcast settings update via MVVM messaging
                WeakReferenceMessenger.Default.Send(new UpdateSettings(Settings));
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to save settings: {ex.Message}");
            }
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

                            if (isTestFile || isNormalGameFile)
                            {
                                // Set the activated file path
                                ActivatedFilePath = file.Path;
                                this.TraceMessage($"Set ActivatedFilePath: {ActivatedFilePath}");
                                
                                if (isTestFile)
                                {
                                    // Disable recording mode when running tests
                                    RecordMode = false;
                                    IsTestMode = true;
                                    this.TraceMessage("Test mode enabled via file activation");
                                }
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
                
                // Apply test mode settings after window creation but before activation
                if (IsTestMode)
                {
                    Timeline.AllowDependentAnimations = false;
                    Catan3.Utility.AnimationSpeed.SetTestMode(true);
                    this.TraceMessage("Applied test mode UI settings (disabled animations)");
                }
                
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

            // Capture values before entering dispatcher context to avoid cross-thread issues
            var isTestModeSnapshot = IsTestMode;
            
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
                        DebugWindow.ShowMessage($"🪟 DebugWindow auto-opened (TestMode: {isTestModeSnapshot})");

                        // For test mode, add additional message
                        if (isTestModeSnapshot)
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
