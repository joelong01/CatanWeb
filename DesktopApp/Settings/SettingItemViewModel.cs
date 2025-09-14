using Catan3.Shared.Models;
using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Catan3.Settings
{
    /// <summary>
    /// View model for an individual setting item that provides UI binding properties
    /// and validation logic for a single setting.
    /// </summary>
    public partial class SettingItemViewModel : ObservableObject
    {
        private readonly Func<IEnumerable<SettingItem>>? _getAllSettings;

        /// <summary>
        /// Gets the underlying setting item model
        /// </summary>
        [ObservableProperty]
        public partial SettingItem Model { get; set; }

        /// <summary>
        /// Gets whether this setting currently has a validation error
        /// </summary>
        [ObservableProperty]
        public partial bool HasValidationError { get; set; }

        /// <summary>
        /// Gets the current validation error message if any
        /// </summary>
        [ObservableProperty]
        public partial string ValidationErrorMessage { get; set; } = string.Empty;

        /// <summary>
        /// Initializes a new instance of SettingItemViewModel
        /// </summary>
        /// <param name="model">The setting item model</param>
        /// <param name="getAllSettings">Function to get all settings for cross-setting validation</param>
        public SettingItemViewModel(SettingItem model, Func<IEnumerable<SettingItem>>? getAllSettings = null)
        {
            Model = model;
            _getAllSettings = getAllSettings;

            // Subscribe to model changes
            Model.PropertyChanged += async (s, e) =>
            {
                if (e.PropertyName == nameof(SettingItem.Value) ||
                    e.PropertyName == nameof(SettingItem.TextValue) ||
                    e.PropertyName == nameof(SettingItem.BooleanValue))
                {
                    await ValidateAsync();

                    // Forward the property change notifications to ViewModel properties
                    OnPropertyChanged(nameof(Value));
                    OnPropertyChanged(nameof(TextValue));
                    OnPropertyChanged(nameof(BooleanValue));
                }
            };
        }

        /// <summary>
        /// Validates this setting asynchronously
        /// </summary>
        public async Task<bool> ValidateAsync()
        {
            var allSettings = _getAllSettings?.Invoke() ?? new List<SettingItem> { Model };
            var result = await SettingsViewModel.ValidateSettingAsync(Model, allSettings);

            // Ensure UI updates happen on the UI thread
            if (Microsoft.UI.Dispatching.DispatcherQueue.GetForCurrentThread() != null)
            {
                // Already on UI thread
                UpdateValidationState(result);
            }
            else
            {
                // Need to marshal to UI thread
                var dispatcherQueue = MainWindow.Instance?.DispatcherQueue;
                if (dispatcherQueue != null)
                {
                    dispatcherQueue.TryEnqueue(() => UpdateValidationState(result));
                }
                else
                {
                    // Fallback - update without UI notifications
                    HasValidationError = !result.IsValid;
                    ValidationErrorMessage = result.ErrorMessage;
                    Model.HasValidationError = HasValidationError;
                    Model.ValidationErrorMessage = ValidationErrorMessage;
                }
            }

            return result.IsValid;
        }

        /// <summary>
        /// Updates validation state and notifies UI (must be called from UI thread)
        /// </summary>
        private void UpdateValidationState(ValidationResult result)
        {
            HasValidationError = !result.IsValid;
            ValidationErrorMessage = result.ErrorMessage;

            // Update the model's validation properties too
            Model.HasValidationError = HasValidationError;
            Model.ValidationErrorMessage = ValidationErrorMessage;

            // Notify UI properties that depend on validation state
            OnPropertyChanged(nameof(Tooltip));
            OnPropertyChanged(nameof(BorderBrush));
            OnPropertyChanged(nameof(BorderThickness));
            OnPropertyChanged(nameof(ErrorVisibility));
        }

        /// <summary>
        /// Gets the tooltip text for this setting
        /// Shows error message when invalid, helpful description when valid
        /// </summary>
        public string Tooltip
        {
            get
            {
                // If there's a validation error and we have an error tooltip, use that
                if (HasValidationError && !string.IsNullOrEmpty(Model.ErrorTooltip))
                {
                    return Model.ErrorTooltip;
                }

                // If there's a validation error message from validation, use that
                if (HasValidationError && !string.IsNullOrEmpty(ValidationErrorMessage))
                {
                    return ValidationErrorMessage;
                }

                // Otherwise return the normal tooltip
                return Model.Tooltip ?? Model.Description;
            }
        }

        /// <summary>
        /// Gets the border brush based on validation state
        /// Red when invalid, default when valid
        /// </summary>
        public Brush BorderBrush
        {
            get
            {
                return HasValidationError ?
                    BrushCache.GetSolidColorBrush(Colors.Red) :
                    (Brush)Application.Current.Resources["TextControlBorderBrush"];
            }
        }

        /// <summary>
        /// Gets the border thickness based on validation state
        /// Thicker when invalid to make it more visible
        /// </summary>
        public Thickness BorderThickness
        {
            get
            {
                return HasValidationError ? new Thickness(2) : new Thickness(1);
            }
        }

        /// <summary>
        /// Gets the visibility for validation error text
        /// </summary>
        public Visibility ErrorVisibility
        {
            get
            {
                return HasValidationError ? Visibility.Visible : Visibility.Collapsed;
            }
        }

        /// <summary>
        /// Pass-through properties for binding to the model
        /// </summary>
        public string SettingName => Model.SettingName;
        public string Description => Model.Description;
        public string InputType => Model.InputType;
        public string[]? Options => Model.Options;

        public string TextValue
        {
            get => Model.TextValue;
            set => Model.TextValue = value;
        }

        public bool BooleanValue
        {
            get => Model.BooleanValue;
            set => Model.BooleanValue = value;
        }

        public object? Value
        {
            get => Model.Value;
            set => Model.Value = value;
        }
    }
}