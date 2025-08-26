using System;
using System.Runtime.CompilerServices;
using Microsoft.Extensions.Logging;
using Catan3.Shared.Interfaces;

namespace Catan3.GameService.Services
{
    /// <summary>
    /// GameService implementation of IGameLogger that wraps Microsoft.Extensions.Logging.ILogger.
    /// Bridges the shared GameStateMachine logging interface to ASP.NET Core logging infrastructure.
    /// </summary>
    public class GameServiceLogger : IGameLogger
    {
        private readonly ILogger _logger;

        public GameServiceLogger(ILogger logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public void Log(GameLogLevel logLevel, string message, int indentLevel = 0, [CallerMemberName] string callerMemberName = "", [CallerLineNumber] int callerLineNumber = 0, [CallerFilePath] string callerFilePath = "")
        {
            // Convert our GameLogLevel to Microsoft.Extensions.Logging.LogLevel
            var msLogLevel = ConvertLogLevel(logLevel);
            
            // Format message with indentation if specified
            var formattedMessage = indentLevel > 0 
                ? new string(' ', indentLevel * 2) + message 
                : message;

            // Add caller information for trace/debug levels
            if (logLevel <= GameLogLevel.Debug)
            {
                var fileName = System.IO.Path.GetFileName(callerFilePath);
                formattedMessage = $"[{fileName}:{callerLineNumber} {callerMemberName}] {formattedMessage}";
            }

            _logger.Log(msLogLevel, formattedMessage);
        }

        /// <summary>
        /// Converts GameLogLevel to Microsoft.Extensions.Logging.LogLevel.
        /// </summary>
        private static Microsoft.Extensions.Logging.LogLevel ConvertLogLevel(GameLogLevel gameLogLevel)
        {
            return gameLogLevel switch
            {
                GameLogLevel.Trace => Microsoft.Extensions.Logging.LogLevel.Trace,
                GameLogLevel.Debug => Microsoft.Extensions.Logging.LogLevel.Debug,
                GameLogLevel.Information => Microsoft.Extensions.Logging.LogLevel.Information,
                GameLogLevel.Warning => Microsoft.Extensions.Logging.LogLevel.Warning,
                GameLogLevel.Error => Microsoft.Extensions.Logging.LogLevel.Error,
                GameLogLevel.Critical => Microsoft.Extensions.Logging.LogLevel.Critical,
                GameLogLevel.None => Microsoft.Extensions.Logging.LogLevel.None,
                _ => Microsoft.Extensions.Logging.LogLevel.Information
            };
        }
    }
}