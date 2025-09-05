using System;
using System.Threading;

namespace Tests.DesktopApp.UI.ScriptedTestData
{
    /// <summary>
    /// Provides waiting utilities that keep the UI responsive by pumping Windows messages.
    /// This is crucial for STA threads in WinUI3 applications to prevent blocking UI updates.
    /// </summary>
    public static class WaitHelper
    {
        /// <summary>
        /// Waits until condition() returns true or times out. Pumps messages between polls.
        /// </summary>
        public static bool WaitUntil(
            Func<bool> condition,
            TimeSpan timeout,
            TimeSpan? pollInterval = null,
            CancellationToken cancellationToken = default)
        {
            var end = DateTime.UtcNow + timeout;
            var interval = pollInterval ?? TimeSpan.FromMilliseconds(50);

            while (DateTime.UtcNow < end)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (condition()) return true;
                UiPump.DelayWithPump(interval);
            }
            return condition();
        }

        /// <summary>
        /// Repeatedly evaluates factory(), returning the first non-null value (or default on timeout).
        /// Handy with FlaUI queries that may start as null.
        /// </summary>
        public static T? WaitUntilNotNull<T>(
            Func<T?> factory,
            TimeSpan timeout,
            TimeSpan? pollInterval = null,
            CancellationToken cancellationToken = default) where T : class
        {
            var end = DateTime.UtcNow + timeout;
            var interval = pollInterval ?? TimeSpan.FromMilliseconds(50);

            while (DateTime.UtcNow < end)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var value = factory();
                if (value != null) return value;
                UiPump.DelayWithPump(interval);
            }
            return factory();
        }

        /// <summary>
        /// Waits until predicate(value) is true, where value is produced each poll.
        /// </summary>
        public static bool WaitUntil<T>(
            Func<T> valueFactory,
            Func<T, bool> predicate,
            TimeSpan timeout,
            TimeSpan? pollInterval = null,
            CancellationToken cancellationToken = default)
        {
            var end = DateTime.UtcNow + timeout;
            var interval = pollInterval ?? TimeSpan.FromMilliseconds(50);

            while (DateTime.UtcNow < end)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var value = valueFactory();
                if (predicate(value)) return true;
                UiPump.DelayWithPump(interval);
            }
            return predicate(valueFactory());
        }

        /// <summary>
        /// Waits until a condition becomes true, throwing an exception on timeout.
        /// </summary>
        /// <param name="condition">The condition to wait for</param>
        /// <param name="timeout">Maximum time to wait</param>
        /// <param name="timeoutMessage">Message for timeout exception</param>
        /// <param name="pollInterval">How often to check the condition (default 50ms)</param>
        /// <exception cref="TimeoutException">Thrown when timeout occurs</exception>
        public static void WaitUntilOrThrow(Func<bool> condition, TimeSpan timeout, string timeoutMessage, TimeSpan? pollInterval = null)
        {
            if (!WaitUntil(condition, timeout, pollInterval))
            {
                throw new TimeoutException($"Timeout after {timeout.TotalSeconds}s: {timeoutMessage}");
            }
        }
    }
}