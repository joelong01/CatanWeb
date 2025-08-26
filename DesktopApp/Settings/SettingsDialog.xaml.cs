using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;
using System.Diagnostics;
using Windows.Storage.Pickers;

namespace Catan3.Settings
{
    /// <summary>
    /// Settings dialog that provides a modal interface for configuring application settings
    /// based on metadata from Assets/settings.json.
    /// </summary>
    public sealed partial class SettingsDialog : ContentDialog
    {
        /// <summary>
        /// Gets or sets the view model containing the settings data and logic
        /// </summary>
        public SettingsViewModel ViewModel { get; set; }

        /// <summary>
        /// Initializes a new instance of the SettingsDialog
        /// </summary>
        public SettingsDialog()
        {
            this.InitializeComponent();
            ViewModel = new SettingsViewModel();
            this.DataContext = ViewModel;

            // Wire up the button events
            this.PrimaryButtonClick += OnPrimaryButtonClick;
            this.SecondaryButtonClick += OnSecondaryButtonClick;
        }

        /// <summary>
        /// Handles the Primary (Save) button click. Validates settings and saves them.
        /// </summary>
        /// <param name="sender">The ContentDialog</param>
        /// <param name="args">Event arguments that can be used to defer or cancel the action</param>
        private async void OnPrimaryButtonClick(ContentDialog sender, ContentDialogButtonClickEventArgs args)
        {
            // Defer the close so we can perform validation
            args.Cancel = true;

            try
            {
                // Validate all settings before saving
                var validationResult = ViewModel.ValidateSettings();
                if (!validationResult.IsValid)
                {
                    await ShowValidationErrorDialog(validationResult.ErrorMessage);
                    return;
                }

                // Save settings to storage and environment variables
                ViewModel.SaveSettings();
                
                // Close the dialog
                this.Hide();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error saving settings: {ex.Message}");
                await ShowErrorDialog($"Failed to save settings: {ex.Message}");
            }
        }

        /// <summary>
        /// Handles the Secondary (Defaults) button click. Resets all settings to defaults.
        /// </summary>
        /// <param name="sender">The ContentDialog</param>
        /// <param name="args">Event arguments that can be used to defer or cancel the action</param>
        private async void OnSecondaryButtonClick(ContentDialog sender, ContentDialogButtonClickEventArgs args)
        {
            // Defer the close so we can show confirmation
            args.Cancel = true;

            try
            {
                var result = await ShowDefaultsConfirmationDialog();
                if (result == ContentDialogResult.Primary)
                {
                    // Reset all settings to their defaults
                    ViewModel.ResetToDefaults();
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error resetting to defaults: {ex.Message}");
                await ShowErrorDialog($"Failed to reset settings to defaults: {ex.Message}");
            }
        }

        /// <summary>
        /// Handles the Browse button click for directory picker settings
        /// </summary>
        /// <param name="sender">The Browse button</param>
        /// <param name="e">Event arguments</param>
        private async void OnBrowseDirectory(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.Tag is Catan3.Shared.Models.SettingItem settingItem)
            {
                try
                {
                    var folderPicker = new FolderPicker();
                    folderPicker.SuggestedStartLocation = PickerLocationId.Desktop;
                    folderPicker.FileTypeFilter.Add("*");

                    // Initialize with window handle for WinUI 3
                    var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(MainWindow.Instance);
                    WinRT.Interop.InitializeWithWindow.Initialize(folderPicker, hwnd);

                    var folder = await folderPicker.PickSingleFolderAsync();
                    if (folder != null)
                    {
                        settingItem.TextValue = folder.Path;
                        this.TraceMessage($"Directory selected for {settingItem.SettingName}: {folder.Path}");
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Error selecting directory: {ex.Message}");
                    await ShowErrorDialog($"Failed to open directory picker: {ex.Message}");
                }
            }
        }

        /// <summary>
        /// Shows a validation error dialog with the specified message
        /// </summary>
        /// <param name="errorMessage">The validation error message to display</param>
        private async System.Threading.Tasks.Task ShowValidationErrorDialog(string errorMessage)
        {
            var dialog = new ContentDialog
            {
                Title = "Validation Error",
                Content = errorMessage,
                CloseButtonText = "OK",
                XamlRoot = this.XamlRoot
            };

            await dialog.ShowAsync();
        }

        /// <summary>
        /// Shows a confirmation dialog asking if the user wants to reset to defaults
        /// </summary>
        /// <returns>The user's choice: Primary=Reset, None=Cancel</returns>
        private async System.Threading.Tasks.Task<ContentDialogResult> ShowDefaultsConfirmationDialog()
        {
            var dialog = new ContentDialog
            {
                Title = "Reset to Defaults",
                Content = "This will reset all settings to their default values from the application. Any custom settings will be lost. Are you sure?",
                PrimaryButtonText = "Reset to Defaults",
                CloseButtonText = "Cancel",
                DefaultButton = ContentDialogButton.Close,
                XamlRoot = this.XamlRoot
            };

            return await dialog.ShowAsync();
        }

        /// <summary>
        /// Shows a general error dialog with the specified message
        /// </summary>
        /// <param name="errorMessage">The error message to display</param>
        private async System.Threading.Tasks.Task ShowErrorDialog(string errorMessage)
        {
            var dialog = new ContentDialog
            {
                Title = "Error",
                Content = errorMessage,
                CloseButtonText = "OK",
                XamlRoot = this.XamlRoot
            };

            await dialog.ShowAsync();
        }
    }
}