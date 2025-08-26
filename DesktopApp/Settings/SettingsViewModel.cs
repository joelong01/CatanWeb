using Catan3.Shared.Models;
using CommunityToolkit.Mvvm.ComponentModel;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text.Json;

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
        /// Original settings values for change detection and revert functionality
        /// </summary>
        private SettingsModel _originalSettings;

        /// <summary>
        /// Initializes a new instance of SettingsViewModel with current app settings
        /// </summary>
        public SettingsViewModel()
        {
            // Add setting items directly from the app settings
            if (App.Settings?.Settings != null)
            {
                foreach (var settingItem in App.Settings.Settings)
                {
                    if (settingItem != null)
                    {
                        SettingItems.Add(settingItem);
                    }
                    else
                    {
                        this.TraceMessage("Warning: Found null setting item in App.Settings.Settings");
                    }
                }
                this.TraceMessage($"Added {SettingItems.Count} settings to ViewModel");
            }
            else
            {
                this.TraceMessage("Warning: App.Settings or App.Settings.Settings is null");
            }
            
            // Create a deep copy for change tracking
            var json = JsonSerializer.Serialize(App.Settings);
            _originalSettings = JsonSerializer.Deserialize<SettingsModel>(json) ?? new SettingsModel();
        }

        /// <summary>
        /// Validates all settings according to their validation rules
        /// </summary>
        /// <returns>A validation result indicating success or failure with error message</returns>
        public ValidationResult ValidateSettings()
        {
            foreach (var setting in SettingItems)
            {
                var validation = setting.Validation;
                if (validation == null) continue;

                var value = setting.ValueAsString;

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
            }

            return new ValidationResult(true, string.Empty);
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
        /// Saves the current settings to app storage and updates the global App.Settings
        /// </summary>
        public void SaveSettings()
        {
            // Update the global app settings by copying our values to the global instance
            foreach (var setting in SettingItems)
            {
                var globalSetting = App.Settings.GetSetting(setting.SettingName);
                if (globalSetting != null)
                {
                    globalSetting.Value = setting.Value;
                }
            }
            
            // Save to persistent storage
            App.SaveSettings();
            
            // Update our original settings for future change detection
            var json = JsonSerializer.Serialize(App.Settings);
            _originalSettings = JsonSerializer.Deserialize<SettingsModel>(json) ?? new SettingsModel();
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
    }

    /// <summary>
    /// Result of settings validation operation
    /// </summary>
    public class ValidationResult
    {
        /// <summary>
        /// Gets whether the validation passed
        /// </summary>
        public bool IsValid { get; }

        /// <summary>
        /// Gets the error message if validation failed
        /// </summary>
        public string ErrorMessage { get; }

        /// <summary>
        /// Initializes a new ValidationResult
        /// </summary>
        /// <param name="isValid">Whether validation passed</param>
        /// <param name="errorMessage">Error message if validation failed</param>
        public ValidationResult(bool isValid, string errorMessage)
        {
            IsValid = isValid;
            ErrorMessage = errorMessage;
        }
    }
}