using Catan3.Shared.Models;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Messaging;
using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;
using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
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
        /// Gets or sets the collection of setting items for UI binding
        /// </summary>
        [ObservableProperty]
        public partial ObservableCollection<SettingItem> SettingItems { get; set; } = [];

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
                // Add setting items directly from the provided settings
                foreach (var settingItem in settingsModel.Settings)
                {
                    if (settingItem != null)
                    {
                        SettingItems.Add(settingItem);
                        // Subscribe to property changes for real-time validation
                        settingItem.PropertyChanged += OnSettingItemChanged;
                    }
                    else
                    {
                        this.TraceMessage("Warning: Found null setting item in settings");
                    }
                }

                // Initial validation check
                ValidateAllSettings();
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
        /// Handles property changes on individual setting items for real-time validation
        /// </summary>
        private void OnSettingItemChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(SettingItem.Value) ||
                e.PropertyName == nameof(SettingItem.TextValue) ||
                e.PropertyName == nameof(SettingItem.BooleanValue))
            {
                ValidateAllSettings();
            }
        }

        /// <summary>
        /// Validates all settings and updates IsValid property and individual setting validation states
        /// </summary>
        private void ValidateAllSettings()
        {
            bool allValid = true;

            foreach (var setting in SettingItems)
            {
                var validationResult = ValidateIndividualSetting(setting);
                setting.HasValidationError = !validationResult.IsValid;
                setting.ValidationErrorMessage = validationResult.ErrorMessage;

                if (!validationResult.IsValid)
                {
                    allValid = false;
                }
            }

            IsValid = allValid;
        }

        /// <summary>
        /// Validates a single setting according to its validation rules with conditional logic
        /// </summary>
        /// <param name="setting">The setting to validate</param>
        /// <returns>A validation result indicating success or failure with error message</returns>
        private ValidationResult ValidateIndividualSetting(SettingItem setting)
        {
            var validation = setting.Validation;
            if (validation == null) return new ValidationResult(true, string.Empty);

            var value = setting.ValueAsString;

            // Special case: SaveFileLocation is only required when ServiceGame is false
            if (setting.SettingName == "SaveFileLocation")
            {
                var serviceGameSetting = SettingItems.FirstOrDefault(s => s.SettingName == "ServiceGame");
                bool usingServiceGame = serviceGameSetting?.ValueAsBool ?? true;

                if (usingServiceGame)
                {
                    // When using service game, SaveFileLocation is not required
                    return new ValidationResult(true, string.Empty);
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
        /// Validates all settings according to their validation rules
        /// </summary>
        /// <returns>A validation result indicating success or failure with error message</returns>
        public ValidationResult ValidateSettings()
        {
            ValidateAllSettings();
            return IsValid ? new ValidationResult(true, string.Empty) : new ValidationResult(false, "Please fix the validation errors shown.");
        }

        /// <summary>
        /// Checks if any settings have been modified from their original values
        /// </summary>
        /// <returns>True if there are unsaved changes, false otherwise</returns>
        public bool HasUnsavedChanges()
        {
            // Compare current settings with original settings
            foreach (var currentSetting in SettingItems)
            {
                var originalSetting = _originalSettings.Settings.FirstOrDefault(s => 
                    s.SettingName == currentSetting.SettingName);

                if (originalSetting == null) return true; // New setting

                // Compare values
                var currentValue = currentSetting.Value?.ToString() ?? "";
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
            foreach (var currentSetting in SettingItems)
            {
                var originalSetting = _originalSettings.Settings.FirstOrDefault(s => 
                    s.SettingName == currentSetting.SettingName);

                if (originalSetting != null)
                {
                    currentSetting.Value = originalSetting.Value;
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
                foreach (var setting in SettingItems)
                {
                    settingsToSave.Settings.Add(setting);
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
            foreach (var setting in SettingItems)
            {
                // Reset to default value
                setting.Value = setting.DefaultValue;
            }
        }

        /// <summary>
        /// Gets the appropriate border brush for validation state
        /// </summary>
        public Brush GetValidationBorderBrush(bool hasError)
        {
            return hasError ? BrushCache.GetSolidColorBrush(Colors.Red) :
                   (Brush)Application.Current.Resources["TextControlBorderBrush"];
        }

        /// <summary>
        /// Gets the appropriate visibility for validation error messages
        /// </summary>
        public Visibility GetValidationErrorVisibility(bool hasError)
        {
            return hasError ? Visibility.Visible : Visibility.Collapsed;
        }
    }
}