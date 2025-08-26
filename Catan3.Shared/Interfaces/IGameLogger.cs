using System.Runtime.CompilerServices;

namespace Catan3.Shared.Interfaces
{
    /// <summary>
    /// Log level enumeration that matches Microsoft.Extensions.Logging.LogLevel
    /// but without the dependency on the logging framework.
    /// </summary>
    public enum GameLogLevel
    {
        Trace = 0,
        Debug = 1,
        Information = 2,
        Warning = 3,
        Error = 4,
        Critical = 5,
        None = 6
    }

    /// <summary>
    /// Interface for game-specific logging operations.
    /// Allows platform-specific implementations for Desktop (TraceMessage/Debug Window) and Service (ILogger).
    /// Supports LogLevel concept from Microsoft.Extensions.Logging for unified logging semantics.
    /// </summary>
    public interface IGameLogger
    {
        /// <summary>
        /// Logs a message at the specified log level with optional caller information.
        /// </summary>
        /// <param name="logLevel">The severity level of the log message.</param>
        /// <param name="message">The message to log.</param>
        /// <param name="indentLevel">The indentation level for formatting (optional).</param>
        /// <param name="callerMemberName">The calling member name (auto-filled).</param>
        /// <param name="callerLineNumber">The calling line number (auto-filled).</param>
        /// <param name="callerFilePath">The calling file path (auto-filled).</param>
        void Log(GameLogLevel logLevel, string message, int indentLevel = 0, [CallerMemberName] string callerMemberName = "", [CallerLineNumber] int callerLineNumber = 0, [CallerFilePath] string callerFilePath = "");

        /// <summary>
        /// Logs a trace-level message (equivalent to TraceMessage in Desktop implementation).
        /// </summary>
        /// <param name="message">The message to log.</param>
        /// <param name="indentLevel">The indentation level for formatting (optional).</param>
        /// <param name="callerMemberName">The calling member name (auto-filled).</param>
        /// <param name="callerLineNumber">The calling line number (auto-filled).</param>
        /// <param name="callerFilePath">The calling file path (auto-filled).</param>
        void TraceMessage(string message, int indentLevel = 0, [CallerMemberName] string callerMemberName = "", [CallerLineNumber] int callerLineNumber = 0, [CallerFilePath] string callerFilePath = "")
        {
            Log(GameLogLevel.Debug, message, indentLevel, callerMemberName, callerLineNumber, callerFilePath);
        }

        /// <summary>
        /// Logs an informational message.
        /// </summary>
        /// <param name="message">The message to log.</param>
        /// <param name="indentLevel">The indentation level for formatting (optional).</param>
        /// <param name="callerMemberName">The calling member name (auto-filled).</param>
        /// <param name="callerLineNumber">The calling line number (auto-filled).</param>
        /// <param name="callerFilePath">The calling file path (auto-filled).</param>
        void LogInformation(string message, int indentLevel = 0, [CallerMemberName] string callerMemberName = "", [CallerLineNumber] int callerLineNumber = 0, [CallerFilePath] string callerFilePath = "")
        {
            Log(GameLogLevel.Information, message, indentLevel, callerMemberName, callerLineNumber, callerFilePath);
        }

        /// <summary>
        /// Logs a warning message.
        /// </summary>
        /// <param name="message">The message to log.</param>
        /// <param name="indentLevel">The indentation level for formatting (optional).</param>
        /// <param name="callerMemberName">The calling member name (auto-filled).</param>
        /// <param name="callerLineNumber">The calling line number (auto-filled).</param>
        /// <param name="callerFilePath">The calling file path (auto-filled).</param>
        void LogWarning(string message, int indentLevel = 0, [CallerMemberName] string callerMemberName = "", [CallerLineNumber] int callerLineNumber = 0, [CallerFilePath] string callerFilePath = "")
        {
            Log(GameLogLevel.Warning, message, indentLevel, callerMemberName, callerLineNumber, callerFilePath);
        }

        /// <summary>
        /// Logs an error message.
        /// </summary>
        /// <param name="message">The message to log.</param>
        /// <param name="indentLevel">The indentation level for formatting (optional).</param>
        /// <param name="callerMemberName">The calling member name (auto-filled).</param>
        /// <param name="callerLineNumber">The calling line number (auto-filled).</param>
        /// <param name="callerFilePath">The calling file path (auto-filled).</param>
        void LogError(string message, int indentLevel = 0, [CallerMemberName] string callerMemberName = "", [CallerLineNumber] int callerLineNumber = 0, [CallerFilePath] string callerFilePath = "")
        {
            Log(GameLogLevel.Error, message, indentLevel, callerMemberName, callerLineNumber, callerFilePath);
        }
    }
}