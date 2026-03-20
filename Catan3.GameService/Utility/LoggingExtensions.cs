namespace Catan3.GameService.Utility
{
    /// <summary>
    /// Centralized logging extensions for consistent logging across the GameService
    /// Provides a single LogEvent method that all services can use for standardized logging
    /// </summary>
    public static class LoggingExtensions
    {
        /// <summary>
        /// Centralized logging method for consistent event logging across the GameService
        /// All logging should go through this method to ensure consistency and maintainability
        /// </summary>
        /// <param name="logger">The ILogger instance</param>
        /// <param name="eventType">The type/category of the event being logged</param>
        /// <param name="message">The log message</param>
        /// <param name="logLevel">The log level (defaults to Information)</param>
        public static void LogEvent(this ILogger logger, string eventType, string message, LogLevel logLevel = LogLevel.Information)
        {
            logger.Log(logLevel, "[GameService][{EventType}] {Message}", eventType, message);
        }
    }
}
