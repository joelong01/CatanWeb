using Catan3.Shared.Models;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Messaging;
using System;
using System.IO;
using System.Text.Json;
using Windows.Storage;

namespace Catan3.Services
{

    /// <summary>
    /// Service that manages settings via MVVM messaging.
    /// Provides async access to current settings for other services.
    /// </summary>
    public class SettingsService : ObservableRecipient
    {
        /// <summary>
        /// Current settings cached by the service
        /// </summary>
        private SettingsModel _currentSettings;

        /// <summary>
        /// Indicates whether we should seed the environment defaults file after user settings are loaded.
        /// </summary>
        private bool _shouldSeedEnvironmentDefaults;

        /// <summary>
        /// Cached path to the environment defaults file when it needs to be created.
        /// </summary>
        private string? _environmentDefaultsPath;

        /// <summary>
        /// Environment variable that points to an optional external settings directory.
        /// </summary>
        private const string EnvironmentDefaultsVariableName = "CATAN_SETTINGS_PATH";

        /// <summary>
        /// File name that stores the serialized default settings within the external directory.
        /// </summary>
        private const string EnvironmentDefaultsFileName = "default_catan_settings.json";

        /// <summary>
        /// Initializes the SettingsService and registers for messages
        /// </summary>
        public SettingsService()
        {
            _currentSettings = new SettingsModel();
            LoadSettings();
            RegisterMessages();
        }

        /// <summary>
        /// Gets the current settings (synchronous access for App.Settings compatibility)
        /// </summary>
        public SettingsModel Settings => _currentSettings;

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

                // Then, optionally hydrate defaults from environment-provided file
                LoadEnvironmentDefaultSettings();
                
                // Then, load user settings from storage and merge
                LoadUserSettings();

                // If no environment defaults existed, seed them with the merged defaults + user preferences.
                SeedEnvironmentDefaultsIfNeeded();
                
                // Apply environment variable overrides
                ApplyEnvironmentOverrides();
                
                // Always save settings after loading to ensure new defaults are persisted
                SaveSettings();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to load settings, using defaults: {ex.Message}");
                _currentSettings = new SettingsModel();
            }
        }

        /// <summary>
        /// Loads defaults from the environment-specific directory or writes current defaults if the file is missing.
        /// </summary>
        private void LoadEnvironmentDefaultSettings()
        {
            try
            {
                var settingsRoot = Environment.GetEnvironmentVariable(EnvironmentDefaultsVariableName);
                if (string.IsNullOrWhiteSpace(settingsRoot))
                {
                    return;
                }

                var directory = settingsRoot.Trim();
                var defaultsPath = Path.Combine(directory, EnvironmentDefaultsFileName);

                if (File.Exists(defaultsPath))
                {
                    // An existing defaults file takes precedence over the packaged defaults.
                    using var stream = File.OpenRead(defaultsPath);
                    var environmentDefaults = JsonSerializer.Deserialize<SettingsModel>(stream);
                    if (environmentDefaults != null)
                    {
                        MergeSettings(_currentSettings, environmentDefaults);
                    }
                }
                else
                {
                    // Record the path so we can create the seed file after user settings are loaded.
                    Directory.CreateDirectory(directory);
                    _shouldSeedEnvironmentDefaults = true;
                    _environmentDefaultsPath = defaultsPath;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to process environment default settings: {ex.Message}");
            }
        }

        /// <summary>
        /// Seeds the environment defaults file with the fully merged settings if it did not exist.
        /// </summary>
        private void SeedEnvironmentDefaultsIfNeeded()
        {
            if (!_shouldSeedEnvironmentDefaults || string.IsNullOrEmpty(_environmentDefaultsPath))
            {
                return;
            }

            try
            {
                var json = JsonSerializer.Serialize(_currentSettings, new JsonSerializerOptions
                {
                    WriteIndented = true
                });
                File.WriteAllText(_environmentDefaultsPath, json);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to seed environment default settings: {ex.Message}");
            }
            finally
            {
                _shouldSeedEnvironmentDefaults = false;
                _environmentDefaultsPath = null;
            }
        }

        /// <summary>
        /// Copies values from an external settings model into the current defaults, creating entries when necessary.
        /// </summary>
        /// <param name="target">The current settings object that will be updated.</param>
        /// <param name="source">The external settings object providing override values.</param>
        private static void MergeSettings(SettingsModel target, SettingsModel source)
        {
            foreach (var sourceSetting in source.Settings)
            {
                var targetSetting = target.GetSetting(sourceSetting.SettingName);
                if (targetSetting != null)
                {
                    // Preserve the external value and adopt any explicit default overrides.
                    targetSetting.Value = sourceSetting.Value;
                    targetSetting.DefaultValue = sourceSetting.DefaultValue ?? targetSetting.DefaultValue;
                }
                else
                {
                    // Unknown settings are appended so they become available to the rest of the app.
                    target.Settings.Add(sourceSetting);
                }
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
                
                var defaultSettings = JsonSerializer.Deserialize<SettingsModel>(json);
                if (defaultSettings != null)
                {
                    _currentSettings = defaultSettings;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to load default settings: {ex.Message}");
                _currentSettings = new SettingsModel();
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
                            var defaultSetting = _currentSettings.GetSetting(userSetting.SettingName);
                            if (defaultSetting != null)
                            {
                                defaultSetting.Value = userSetting.Value;
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to load user settings: {ex.Message}");
            }
        }

        /// <summary>
        /// Applies environment variable overrides to settings that have environmentVariable defined
        /// </summary>
        private void ApplyEnvironmentOverrides()
        {
            try
            {
                foreach (var setting in _currentSettings.Settings)
                {
                    if (!string.IsNullOrEmpty(setting.EnvironmentVariable))
                    {
                        var envValue = Environment.GetEnvironmentVariable(setting.EnvironmentVariable);
                        if (!string.IsNullOrEmpty(envValue))
                        {
                            setting.Value = envValue;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to apply environment overrides: {ex.Message}");
            }
        }

        /// <summary>
        /// Saves the current application settings to ApplicationData.LocalSettings.
        /// </summary>
        private void SaveSettings()
        {
            try
            {
                var localSettings = ApplicationData.Current.LocalSettings;
                
                // Save settings as JSON
                var settingsJson = JsonSerializer.Serialize(_currentSettings);
                localSettings.Values["UserSettingsJson"] = settingsJson;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to save settings: {ex.Message}");
            }
        }

        /// <summary>
        /// Registers message handlers
        /// </summary>
        private void RegisterMessages()
        {
            IsActive = true;
            Messenger.Register<UpdateSettings>(this, HandleUpdateSettings);
            Messenger.Register<GetSettingsMessage>(this, HandleGetSettings);
        }

        /// <summary>
        /// Handles UpdateSettings message to cache current settings and save them
        /// </summary>
        private void HandleUpdateSettings(object recipient, UpdateSettings message)
        {
            _currentSettings = message.Settings;
            
            // Save to persistent storage whenever settings are updated
            SaveSettings();
        }

        /// <summary>
        /// Handles GetSettingsMessage by responding with current settings
        /// </summary>
        private void HandleGetSettings(object recipient, GetSettingsMessage message)
        {
            var updateMessage = new UpdateSettings(_currentSettings);
            Messenger.Send(updateMessage);
        }

    }
}