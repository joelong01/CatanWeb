using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;
using System.Threading.Tasks;
using Catan.Services;
using Catan3.Models;

namespace Catan3
{
    public sealed partial class ExtendedSplashScreen : Page
    {
        private MainWindow _parentWindow;

        public ExtendedSplashScreen(MainWindow parentWindow)
        {
            this.InitializeComponent();
            _parentWindow = parentWindow;
            
            // Start the initialization process
            _ = InitializeAppAsync();
        }

        private async Task InitializeAppAsync()
        {
            try
            {
                // Simulate minimum splash screen display time (ensure it's visible for at least 1 second)
                var minimumDisplayTime = Task.Delay(1000);
                
                // Perform actual initialization
                var initializationTask = InitializeServicesAsync();
                
                // Wait for both to complete
                await Task.WhenAll(minimumDisplayTime, initializationTask);
                
                // Navigate to the main application
                NavigateToMainApp();
            }
            catch (Exception ex)
            {
                // Handle initialization errors
                System.Diagnostics.Debug.WriteLine($"Initialization error: {ex.Message}");
                NavigateToMainApp(); // Navigate anyway to prevent app from being stuck
            }
        }

        private async Task InitializeServicesAsync()
        {
            // Load player database
            await MainWindow.PlayerDatabase.LoadPlayerDatabase();
            
            // Add any other initialization tasks here
            // For example: configuration loading, service initialization, etc.
        }

        private void NavigateToMainApp()
        {
            // Ensure we're on the UI thread
            if (!Microsoft.UI.Dispatching.DispatcherQueue.GetForCurrentThread().HasThreadAccess)
            {
                Microsoft.UI.Dispatching.DispatcherQueue.GetForCurrentThread().TryEnqueue(() => NavigateToMainApp());
                return;
            }

            // Create the new game view model and navigate
            var viewModel = new NewGameViewModel(MainWindow.PlayerDatabase.AllPlayers);
            
            // The MainWindow's Content is the Frame directly
            if (_parentWindow.Content is Frame mainFrame)
            {
                mainFrame.Navigate(typeof(NewGamePage), viewModel);
            }
        }
    }
}
