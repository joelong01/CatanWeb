using Catan3.Shared.Models;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Messaging;
using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;

namespace Catan3.Settings
{
    /// <summary>
    /// View model for the Settings page that manages settings data,
    /// validation, and change tracking for the dynamic settings UI.
    /// </summary>
    public partial class SettingsViewModel : ObservableObject
    {
        /// <summary>
        /// Gets or sets the collection of setting item view models for UI binding
        /// </summary>
        [ObservableProperty]
        public partial ObservableCollection<SettingItemViewModel> SettingItems { get; set; } = [];

        /// <summary>
        /// Gets whether all settings are currently valid
        /// </summary>
        [ObservableProperty]
        public partial bool IsValid { get; set; } = true;

        /// <summary>
        /// Original settings values for change detection and revert functionality
        /// </summary>
        private SettingsModel _originalSettings = new();

        /// <summary>
        /// Initializes a new instance of SettingsViewModel with current app settings
        /// </summary>
        public SettingsViewModel()
        {
            _ = InitializeAsync();
        }

        /// <summary>
        /// Initializes a new instance of SettingsViewModel with provided settings model
        /// </summary>
        public SettingsViewModel(SettingsModel settingsModel)
        {
            InitializeWithModel(settingsModel);
        }

        /// <summary>
        /// Asynchronously initializes the ViewModel with current settings
        /// </summary>
        private async Task InitializeAsync()
        {
            try
            {
                // Get current settings via messaging
                var currentSettings = await SettingsModel.GetAsync();
                InitializeWithModel(currentSettings);
            }
            catch (Exception ex)
            {
                this.TraceMessage($"Failed to initialize settings: {ex.Message}");
                _originalSettings = new SettingsModel();
            }
        }

        /// <summary>
        /// Initializes the ViewModel with a provided settings model
        /// </summary>
        private void InitializeWithModel(SettingsModel settingsModel)
        {
            try
            {
                // Create a function to get all setting models for cross-setting validation
                Func<IEnumerable<SettingItem>> getAllSettings = () =>
                    SettingItems.Select(vm => vm.Model);

                // Add setting items as view models
                foreach (var settingItem in settingsModel.Settings)
                {
                    if (settingItem != null)
                    {
                        var settingViewModel = new SettingItemViewModel(settingItem, getAllSettings);
                        SettingItems.Add(settingViewModel);

                        // Subscribe to property changes for overall validation state
                        settingViewModel.PropertyChanged += OnSettingViewModelChanged;
                    }
                    else
                    {
                        this.TraceMessage("Warning: Found null setting item in settings");
                    }
                }

                // Initial validation check
                _ = ValidateAllSettingsAsync();
                this.TraceMessage($"Added {SettingItems.Count} settings to ViewModel");

                // Create a deep copy for change tracking
                var json = JsonSerializer.Serialize(settingsModel);
                _originalSettings = JsonSerializer.Deserialize<SettingsModel>(json) ?? new SettingsModel();
            }
            catch (Exception ex)
            {
                this.TraceMessage($"Failed to initialize settings with model: {ex.Message}");
                _originalSettings = new SettingsModel();
            }
        }

        /// <summary>
        /// Handles property changes on setting view models for overall validation state
        /// </summary>
        private void OnSettingViewModelChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(SettingItemViewModel.HasValidationError))
            {
                // Update overall IsValid based on all settings
                IsValid = !SettingItems.Any(s => s.HasValidationError);
            }

            // When ServiceGame changes, revalidate dependent settings
            if (sender is SettingItemViewModel changedSetting &&
                e.PropertyName == nameof(SettingItemViewModel.Value) &&
                changedSetting.SettingName == "ServiceGame")
            {
                // Trigger validation for SaveFileLocation and GameServiceUrl asynchronously but keep UI updates on main thread
                _ = Task.Run(async () =>
                {
                    var saveFileLocation = SettingItems.FirstOrDefault(s => s.SettingName == "SaveFileLocation");
                    var gameServiceUrl = SettingItems.FirstOrDefault(s => s.SettingName == "GameServiceUrl");

                    if (saveFileLocation != null)
                    {
                        await saveFileLocation.ValidateAsync();
                    }

                    if (gameServiceUrl != null)
                    {
                        await gameServiceUrl.ValidateAsync();
                    }
                });
            }
        }

        /// <summary>
        /// Validates all settings and updates IsValid property
        /// </summary>
        private async Task ValidateAllSettingsAsync()
        {
            var tasks = SettingItems.Select(s => s.ValidateAsync()).ToArray();
            var results = await Task.WhenAll(tasks);
            IsValid = results.All(r => r);
        }

        /// <summary>
        /// Static validation method for a single setting with all validation rules including cross-setting dependencies
        /// </summary>
        /// <param name="setting">The setting to validate</param>
        /// <param name="allSettings">All settings for cross-setting validation</param>
        /// <returns>A validation result indicating success or failure with error message</returns>
        public static async Task<ValidationResult> ValidateSettingAsync(SettingItem setting, IEnumerable<SettingItem> allSettings)
        {
            var validation = setting.Validation;
            if (validation == null) return new ValidationResult(true, string.Empty);

            var value = setting.ValueAsString;

            // Special case: SaveFileLocation is only required when ServiceGame is false
            if (setting.SettingName == "SaveFileLocation")
            {
                var serviceGameSetting = allSettings.FirstOrDefault(s => s.SettingName == "ServiceGame");
                bool usingServiceGame = serviceGameSetting?.ValueAsBool ?? true;

                if (usingServiceGame)
                {
                    // When using service game, SaveFileLocation is not required
                    return new ValidationResult(true, string.Empty);
                }
            }

            // Special case: GameServiceUrl needs reachability check when ServiceGame is true
            if (setting.SettingName == "GameServiceUrl")
            {
                var serviceGameSetting = allSettings.FirstOrDefault(s => s.SettingName == "ServiceGame");
                bool usingServiceGame = serviceGameSetting?.ValueAsBool ?? false;

                if (usingServiceGame && !string.IsNullOrWhiteSpace(value))
                {
                    var reachabilityResult = await CheckGameServiceReachability(value);
                    if (!reachabilityResult.IsValid)
                    {
                        return reachabilityResult;
                    }
                }
            }

            // Required field validation
            if (validation.Required && string.IsNullOrWhiteSpace(value))
            {
                return new ValidationResult(false, $"{setting.Description} is required.");
            }

            // Length validation
            if (validation.MinLength.HasValue && value.Length < validation.MinLength.Value)
            {
                return new ValidationResult(false, $"{setting.Description} must be at least {validation.MinLength} characters long.");
            }

            if (validation.MaxLength.HasValue && value.Length > validation.MaxLength.Value)
            {
                return new ValidationResult(false, $"{setting.Description} must be no more than {validation.MaxLength} characters long.");
            }

            // Directory existence validation
            if (validation.DirectoryMustExist && !string.IsNullOrWhiteSpace(value) && !Directory.Exists(value))
            {
                return new ValidationResult(false, $"Directory '{value}' does not exist. Please choose a valid directory for {setting.Description}.");
            }

            return new ValidationResult(true, string.Empty);
        }

        /// <summary>
        /// Checks if the GameService is reachable at the specified URL
        /// </summary>
        private static async Task<ValidationResult> CheckGameServiceReachability(string serviceUrl)
        {
            try
            {
                using var httpClient = new HttpClient();
                httpClient.Timeout = TimeSpan.FromSeconds(3);

                // Try a simple GET to the base URL or health endpoint
                var response = await httpClient.GetAsync(serviceUrl.TrimEnd('/'));

                if (response.IsSuccessStatusCode)
                {
                    return new ValidationResult(true, string.Empty);
                }
                else
                {
                    return new ValidationResult(false, $"GameService at {serviceUrl} returned {response.StatusCode}.");
                }
            }
            catch (HttpRequestException ex)
            {
                return new ValidationResult(false, $"GameService not reachable at {serviceUrl}: {ex.Message}");
            }
            catch (TaskCanceledException)
            {
                return new ValidationResult(false, $"GameService at {serviceUrl} timed out (3 seconds).");
            }
            catch (Exception ex)
            {
                return new ValidationResult(false, $"Error connecting to GameService at {serviceUrl}: {ex.Message}");
            }
        }

        /// <summary>
        /// Validates all settings according to their validation rules
        /// </summary>
        /// <returns>A validation result indicating success or failure with error message</returns>
        public async Task<ValidationResult> ValidateSettingsAsync()
        {
            await ValidateAllSettingsAsync();
            return IsValid ? new ValidationResult(true, string.Empty) : new ValidationResult(false, "Please fix the validation errors shown.");
        }

        /// <summary>
        /// Checks if any settings have been modified from their original values
        /// </summary>
        /// <returns>True if there are unsaved changes, false otherwise</returns>
        public bool HasUnsavedChanges()
        {
            // Compare current settings with original settings
            foreach (var currentSettingVm in SettingItems)
            {
                var originalSetting = _originalSettings.Settings.FirstOrDefault(s =>
                    s.SettingName == currentSettingVm.SettingName);

                if (originalSetting == null) return true; // New setting

                // Compare values
                var currentValue = currentSettingVm.Value?.ToString() ?? "";
                var originalValue = originalSetting.Value?.ToString() ?? "";

                if (currentValue != originalValue)
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Reverts all settings to their original values, discarding any changes
        /// </summary>
        public void RevertChanges()
        {
            // Restore original values
            foreach (var currentSettingVm in SettingItems)
            {
                var originalSetting = _originalSettings.Settings.FirstOrDefault(s =>
                    s.SettingName == currentSettingVm.SettingName);

                if (originalSetting != null)
                {
                    currentSettingVm.Value = originalSetting.Value;
                }
            }
        }

        /// <summary>
        /// Saves the current settings via SettingsService
        /// </summary>
        public void SaveSettings()
        {
            try
            {
                // Create a new settings model with our current values
                var settingsToSave = new SettingsModel();
                foreach (var settingVm in SettingItems)
                {
                    settingsToSave.Settings.Add(settingVm.Model);
                }

                // Send to SettingsService for saving
                WeakReferenceMessenger.Default.Send(new UpdateSettings(settingsToSave));

                // Update our original settings for future change detection
                var json = JsonSerializer.Serialize(settingsToSave);
                _originalSettings = JsonSerializer.Deserialize<SettingsModel>(json) ?? new SettingsModel();
            }
            catch (Exception ex)
            {
                this.TraceMessage($"Failed to save settings: {ex.Message}");
            }
        }

        /// <summary>
        /// Resets all settings to their default values from Assets/settings.json
        /// </summary>
        public void ResetToDefaults()
        {
            foreach (var settingVm in SettingItems)
            {
                // Reset to default value
                settingVm.Value = settingVm.Model.DefaultValue;
            }

            // Trigger revalidation
            _ = ValidateAllSettingsAsync();
        }

        /// <summary>
        /// Checks if the GameService is unavailable and returns a message
        /// This allows the user to save settings for local-only gameplay
        /// </summary>
        /// <returns>Error message if service is unavailable, empty string if available</returns>
        public string GetServiceUnavailabilityWarning()
        {
            // Check if ServiceGame is enabled and GameServiceUrl is unreachable
            var serviceGameSetting = SettingItems.FirstOrDefault(s => s.SettingName == "ServiceGame");
            var gameServiceUrlSetting = SettingItems.FirstOrDefault(s => s.SettingName == "GameServiceUrl");

            if (serviceGameSetting?.BooleanValue == true && gameServiceUrlSetting?.HasValidationError == true)
            {
                return gameServiceUrlSetting.ValidationErrorMessage;
            }

            return string.Empty;
        }
    }
}