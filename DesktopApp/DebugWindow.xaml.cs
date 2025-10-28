using Microsoft.UI.Xaml;
using System;
using System.Linq;
using WinUIEx;
using Windows.ApplicationModel.DataTransfer;

namespace Catan3
{
    public sealed partial class DebugWindow : WindowEx
    {
        private static DebugWindow? s_instance;
        private static int s_lineNumber = 1; // Track line numbers
        
        public DebugWindow()
        {
            this.InitializeComponent();
            s_instance = this;
        }

        /// <summary>
        /// Shows a message in the debug window. Creates the window if it doesn't exist.
        /// </summary>
        public static void ShowMessage(string message)
        {
            try
            {
                // Try multiple approaches to get a valid dispatcher queue
                Microsoft.UI.Dispatching.DispatcherQueue? dispatcherQueue = null;
                
                // First try current thread dispatcher
                try
                {
                    dispatcherQueue = Microsoft.UI.Dispatching.DispatcherQueue.GetForCurrentThread();
                }
                catch { /* Ignore and try next approach */ }
                
                // If that fails, try MainWindow dispatcher if available
                if (dispatcherQueue == null && Application.Current is App app && app.MainWindow != null)
                {
                    try
                    {
                        dispatcherQueue = app.MainWindow.DispatcherQueue;
                    }
                    catch { /* Ignore and try next approach */ }
                }
                
                if (dispatcherQueue != null)
                {
                    dispatcherQueue.TryEnqueue(() =>
                    {
                        ShowMessageInternal(message);
                    });
                }
                else
                {
                    // If no dispatcher queue available, try direct call (might be on UI thread already)
                    ShowMessageInternal(message);
                }
            }
            catch (Exception ex)
            {
                // Fallback to console if window creation fails
                Console.WriteLine($"[DEBUG]: {message}");
                System.Diagnostics.Debug.WriteLine($"DebugWindow.ShowMessage failed: {ex.Message}");
            }
        }
        
        /// <summary>
        /// Internal method to actually show the message - must be called on UI thread
        /// </summary>
        private static void ShowMessageInternal(string message)
        {
            try
            {
                if (s_instance == null)
                {
                    s_instance = new DebugWindow();
                    s_instance.Activate();
                }

                // Add line number, timestamp, and message
                var timestampedMessage = $"[{s_lineNumber:D5} {DateTime.Now:HH:mm:ss.fff}] {message}";
                s_instance.MessagesTextBox.Text += timestampedMessage + Environment.NewLine;
                s_lineNumber++;

                // Check if truncation is enabled and we should truncate
                if (s_instance.TruncateCheckBox.IsChecked == true)
                {
                    if (int.TryParse(s_instance.TruncateLimitBox.Text, out int truncateAfter) && truncateAfter > 0)
                    {
                        if (s_lineNumber % truncateAfter == 0)
                        {
                            s_instance.MessagesTextBox.Text = "";
                        }

                    }
                }
                
                // Auto-scroll to bottom
                s_instance.MessagesTextBox.Select(s_instance.MessagesTextBox.Text.Length, 0);
            }
            catch (Exception ex)
            {
                // Final fallback
                System.Diagnostics.Debug.WriteLine($"DebugWindow.ShowMessageInternal failed: {ex.Message}");
                Console.WriteLine($"[DEBUG]: {message}");
            }
        }

        /// <summary>
        /// Shows the debug window if it exists, or creates it if it doesn't
        /// </summary>
        public static void Show()
        {
            try
            {
                // Try to get the current dispatcher queue
                var dispatcherQueue = Microsoft.UI.Dispatching.DispatcherQueue.GetForCurrentThread();
                if (dispatcherQueue != null)
                {
                    dispatcherQueue.TryEnqueue(() =>
                    {
                        ShowInternal();
                    });
                }
                else
                {
                    // Fallback if no dispatcher queue available
                    ShowInternal();
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"DebugWindow.Show() failed: {ex.Message}");
                // Try direct creation as last resort
                try
                {
                    ShowInternal();
                }
                catch (Exception ex2)
                {
                    System.Diagnostics.Debug.WriteLine($"DebugWindow fallback failed: {ex2.Message}");
                }
            }
        }

        private static void ShowInternal()
        {
            if (s_instance == null)
            {
                s_instance = new DebugWindow();
            }
            s_instance.Activate();
        }

        private void OnCopyClick(object sender, RoutedEventArgs e)
        {
            try
            {
                var dataPackage = new DataPackage();
                dataPackage.SetText(MessagesTextBox.Text);
                Clipboard.SetContent(dataPackage);
                
                // Show a brief confirmation message
                ShowMessage("📋 Trace content copied to clipboard");
            }
            catch (Exception ex)
            {
                ShowMessage($"❌ Failed to copy to clipboard: {ex.Message}");
            }
        }

        private void OnClearClick(object sender, RoutedEventArgs e)
        {
            MessagesTextBox.Text = "";
            // DO NOT reset s_lineNumber - keep counting for the entire game session
        }
        
        /// <summary>
        /// Handles the Truncate Lines checkbox toggle
        /// </summary>
        private void OnTruncateToggled(object sender, RoutedEventArgs e)
        {
            // Enable/disable the limit text box based on checkbox state
            TruncateLimitBox?.IsEnabled = TruncateCheckBox.IsChecked == true;
        }
        
        /// <summary>
        /// Closes the DebugWindow instance if it exists
        /// </summary>
        public static void CloseInstance()
        {
            if (s_instance != null)
            {
                s_instance.Close();
                s_instance = null;
            }
        }

        private void Window_Closed(object sender, WindowEventArgs args)
        {
            s_instance = null;
        }
    }
}
