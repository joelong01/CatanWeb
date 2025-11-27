using System;
using Microsoft.UI.Xaml;

namespace Catan3.Utility
{
    /// <summary>
    /// Centralized animation speed configuration for the entire application.
    /// Provides standard durations for animations and allows disabling them for testing.
    /// </summary>
    public static class AnimationSpeed
    {
        private static bool _testMode = false;

        /// <summary>
        /// Fast animations (100ms default)
        /// Used for: Purchase button press/release, quick UI feedback
        /// </summary>
        public static TimeSpan Fast { get; private set; } = TimeSpan.FromSeconds(0.1);

        /// <summary>
        /// Medium animations (250ms default)
        /// Used for: Flipper card turns, robber movement
        /// </summary>
        public static TimeSpan Medium { get; private set; } = TimeSpan.FromSeconds(0.250);

        /// <summary>
        /// Slow animations (500ms default)
        /// Used for: Tile dimming/brightening, longer transitions
        /// </summary>
        public static TimeSpan Slow { get; private set; } = TimeSpan.FromSeconds(0.5);

        /// <summary>
        /// Extra slow animations (2s default)
        /// Used for: Tile dim timer delay
        /// </summary>
        public static TimeSpan ExtraSlow { get; private set; } = TimeSpan.FromSeconds(2.0);

        /// <summary>
        /// Button hover animations (200ms default)
        /// Used for: Button pressed state animations
        /// </summary>
        public static TimeSpan ButtonPress { get; private set; } = TimeSpan.FromSeconds(0.2);

        /// <summary>
        /// Color transition animations (300ms default)
        /// Used for: Button hover color transitions
        /// </summary>
        public static TimeSpan ColorTransition { get; private set; } = TimeSpan.FromSeconds(0.3);

        /// <summary>
        /// Animation delay (1s default)
        /// Used for: Begin time delays in storyboards
        /// </summary>
        public static TimeSpan Delay { get; private set; } = TimeSpan.FromSeconds(1.0);

        /// <summary>
        /// Sets test mode which disables all animations by setting their duration to zero.
        /// This should be called from App.xaml.cs when IsTestMode is detected.
        /// Also updates the Application Resources with the new durations.
        /// </summary>
        /// <param name="testMode">True to disable animations, false to use default durations</param>
        public static void SetTestMode(bool testMode)
        {
            _testMode = testMode;

            if (testMode)
            {
                // In test mode, all animations are instant
                Fast = TimeSpan.Zero;
                Medium = TimeSpan.Zero;
                Slow = TimeSpan.Zero;
                ExtraSlow = TimeSpan.Zero;
                ButtonPress = TimeSpan.Zero;
                ColorTransition = TimeSpan.Zero;
                Delay = TimeSpan.Zero;
            }
            else
            {
                // Restore default animation durations
                Fast = TimeSpan.FromSeconds(0.1);
                Medium = TimeSpan.FromSeconds(0.250);
                Slow = TimeSpan.FromSeconds(0.5);
                ExtraSlow = TimeSpan.FromSeconds(2.0);
                ButtonPress = TimeSpan.FromSeconds(0.2);
                ColorTransition = TimeSpan.FromSeconds(0.3);
                Delay = TimeSpan.FromSeconds(1.0);
            }

            // Update Application Resources
            UpdateApplicationResources();
        }

        /// <summary>
        /// Updates the Application.Resources with the current animation durations.
        /// This allows XAML bindings to use {StaticResource} for animation durations.
        /// </summary>
        private static void UpdateApplicationResources()
        {
            if (Application.Current?.Resources == null)
                return;

            var resources = Application.Current.Resources;

            // Update the duration resources
            resources["AnimationFast"] = new Duration(Fast);
            resources["AnimationMedium"] = new Duration(Medium);
            resources["AnimationSlow"] = new Duration(Slow);
            resources["AnimationExtraSlow"] = new Duration(ExtraSlow);
            resources["AnimationButtonPress"] = new Duration(ButtonPress);
            resources["AnimationColorTransition"] = new Duration(ColorTransition);
            resources["AnimationDelay"] = new Duration(Delay);
        }

        /// <summary>
        /// Gets whether the animation system is in test mode (animations disabled)
        /// </summary>
        public static bool IsTestMode => _testMode;
    }
}