using Catan3.Shared.Models;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Messaging;
using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
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
                
                // Then, load user settings from storage and merge
                LoadUserSettings();
                
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