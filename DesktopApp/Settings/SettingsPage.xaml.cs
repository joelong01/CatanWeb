using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Navigation;
using System;
using System.Diagnostics;

namespace Catan3.Settings
{
    /// <summary>
    /// Settings page that slides out from the left side of the main window.
    /// Provides a dynamic UI for configuring application settings based on
    /// metadata from Assets/settings.json.
    /// </summary>
    public sealed partial class SettingsPage : Page
    {
        /// <summary>
        /// Gets or sets the view model containing the settings data and logic
        /// </summary>
        public SettingsViewModel ViewModel { get; set; }

        /// <summary>
        /// Initializes a new instance of the SettingsPage
        /// </summary>
        public SettingsPage()
        {
            this.InitializeComponent();
            ViewModel = new SettingsViewModel();
            this.DataContext = ViewModel;
        }

        /// <summary>
        /// Called when the page is navigated to. Starts the slide-in animation.
        /// </summary>
        /// <param name="e">Navigation event arguments</param>
        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);
            SlideInStoryboard.Begin();
        }

        /// <summary>
        /// Handles the Save button click. Validates settings, saves them,
        /// and closes the settings page.
        /// </summary>
        /// <param name="sender">The button that was clicked</param>
        /// <param name="e">Event arguments</param>
        private async void OnSave(object sender, RoutedEventArgs e)
        {
            try
            {
                // Check for service unavailability warning
                var serviceWarning = ViewModel.GetServiceUnavailabilityWarning();
                
                if (!string.IsNullOrEmpty(serviceWarning))
                {
                    // Show warning but allow proceeding with local-only gameplay
                    var result = await ShowServiceUnavailableDialog(serviceWarning);
                    if (result != ContentDialogResult.Primary)
                    {
                        // User chose not to proceed
                        return;
                    }
                    // User chose to save anyway - continue with save
                }
                else if (!ViewModel.IsValid)
                {
                    // Only block save if there are actual validation errors (not service unavailability)
                    var validationResult = await ViewModel.ValidateSettingsAsync();
                    if (!validationResult.IsValid)
                    {
                        await ShowValidationErrorDialog(validationResult.ErrorMessage);
                        return;
                    }
                }

                // Save settings to storage and environment variables
                ViewModel.SaveSettings();
                
                // Close the settings page
                await CloseSettingsPage();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error saving settings: {ex.Message}");
                await ShowErrorDialog($"Failed to save settings: {ex.Message}");
            }
        }

        /// <summary>
        /// Handles the Close button click. Checks for unsaved changes and
        /// prompts the user if needed, then closes the settings page.
        /// </summary>
        /// <param name="sender">The button that was clicked</param>
        /// <param name="e">Event arguments</param>
        private async void OnClose(object sender, RoutedEventArgs e)
        {
            if (ViewModel.HasUnsavedChanges())
            {
                var result = await ShowUnsavedChangesDialog();
                if (result == ContentDialogResult.Primary)
                {
                    // User chose to save
                    OnSave(sender, e);
                    return;
                }
                else if (result == ContentDialogResult.Secondary)
                {
                    // User chose to discard changes
                    ViewModel.RevertChanges();
                }
                // If None (Cancel), do nothing and stay on settings page
                else
                {
                    return;
                }
            }
            
            await CloseSettingsPage();
        }

        /// <summary>
        /// Handles the Defaults button click. Resets all settings to their default
        /// values from Assets/settings.json after user confirmation.
        /// </summary>
        /// <param name="sender">The Defaults button</param>
        /// <param name="e">Click event arguments</param>
        private async void OnDefaults(object sender, RoutedEventArgs e)
        {
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
        /// Handles tapping on the overlay area (right side). Closes the settings
        /// page with the same unsaved changes logic as the Close button.
        /// </summary>
        /// <param name="sender">The overlay rectangle</param>
        /// <param name="e">Tap event arguments</param>
        private void OnOverlayTapped(object sender, TappedRoutedEventArgs e)
        {
            OnClose(sender, new RoutedEventArgs());
        }

        /// <summary>
        /// Closes the settings page with slide-out animation and navigates back
        /// </summary>
        private async System.Threading.Tasks.Task CloseSettingsPage()
        {
            SlideOutStoryboard.Begin();
            
            // Wait for animation to complete before navigating
            await System.Threading.Tasks.Task.Delay(300);
            
            if (Frame.CanGoBack)
            {
                Frame.GoBack();
            }
        }

        /// <summary>
        /// Shows a dialog asking the user what to do with unsaved changes
        /// </summary>
        /// <returns>The user's choice: Primary=Save, Secondary=Discard, None=Cancel</returns>
        private async System.Threading.Tasks.Task<ContentDialogResult> ShowUnsavedChangesDialog()
        {
            var dialog = new ContentDialog
            {
                Title = "Unsaved Changes",
                Content = "You have unsaved changes. What would you like to do?",
                PrimaryButtonText = "Save",
                SecondaryButtonText = "Discard",
                CloseButtonText = "Cancel",
                DefaultButton = ContentDialogButton.Primary,
                XamlRoot = this.Content.XamlRoot
            };

            return await dialog.ShowAsync();
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
                XamlRoot = this.Content.XamlRoot
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
                XamlRoot = this.Content.XamlRoot
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
                XamlRoot = this.Content.XamlRoot
            };

            await dialog.ShowAsync();
        }

        /// <summary>
        /// Shows a warning dialog when the GameService is unavailable
        /// Allows the user to save settings for local-only gameplay
        /// </summary>
        /// <param name="warningMessage">The warning message from the service availability check</param>
        /// <returns>ContentDialogResult.Primary to proceed with save, otherwise Cancel</returns>
        private async System.Threading.Tasks.Task<ContentDialogResult> ShowServiceUnavailableDialog(string warningMessage)
        {
            var dialog = new ContentDialog
            {
                Title = "Game Service Unavailable",
                Content = $"{warningMessage}\n\nYou can still save these settings and play a local game. Network features will be disabled.\n\nDo you want to continue?",
                PrimaryButtonText = "Save for Local Play",
                CloseButtonText = "Cancel",
                DefaultButton = ContentDialogButton.Close,
                XamlRoot = this.Content.XamlRoot
            };

            return await dialog.ShowAsync();
        }
    }
}