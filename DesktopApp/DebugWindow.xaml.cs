using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;
using WinUIEx;

namespace Catan3
{
    public sealed partial class DebugWindow : WindowEx
    {
        private static DebugWindow? s_instance;
        
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
                // Ensure we're on the UI thread
                ((App)Application.Current)?.MainWindow?.DispatcherQueue.TryEnqueue(() =>
                {
                    if (s_instance == null)
                    {
                        s_instance = new DebugWindow();
                        s_instance.Activate();
                    }

                    // Add timestamp and append message
                    var timestampedMessage = $"[{DateTime.Now:HH:mm:ss.fff}] {message}";
                    s_instance.MessagesTextBox.Text += timestampedMessage + Environment.NewLine;
                    
                    // Auto-scroll to bottom
                    s_instance.MessagesTextBox.Select(s_instance.MessagesTextBox.Text.Length, 0);
                });
            }
            catch
            {
                // Fallback to console if window creation fails
                Console.WriteLine($"[DEBUG]: {message}");
            }
        }

        /// <summary>
        /// Shows the debug window if it exists, or creates it if it doesn't
        /// </summary>
        public static void Show()
        {
            ((App)Application.Current)?.MainWindow?.DispatcherQueue.TryEnqueue(() =>
            {
                if (s_instance == null)
                {
                    s_instance = new DebugWindow();
                }
                s_instance.Activate();
            });
        }

        private void OnClearClick(object sender, RoutedEventArgs e)
        {
            MessagesTextBox.Text = "";
        }

        private void Window_Closed(object sender, WindowEventArgs args)
        {
            s_instance = null;
        }
    }
}
