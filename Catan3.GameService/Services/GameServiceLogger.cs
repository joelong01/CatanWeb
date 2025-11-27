using System.Runtime.CompilerServices;
using Catan3.Shared.Interfaces;

namespace Catan3.GameService.Services
{
    /// <summary>
    /// GameService implementation of ICatanDebugTrace that wraps Microsoft.Extensions.Logging.ILogger.
    /// Bridges the shared GameStateMachine logging interface to ASP.NET Core logging infrastructure.
    /// </summary>
    public class GameServiceLogger : ICatanDebugTrace
    {
        private readonly ILogger _logger;

        public GameServiceLogger(ILogger logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public void Trace(GameTraceLevel logLevel, string message, int indentLevel = 0, [CallerMemberName] string callerMemberName = "", [CallerLineNumber] int callerLineNumber = 0, [CallerFilePath] string callerFilePath = "")
        {
            // Convert our GameTraceLevel to Microsoft.Extensions.Logging.LogLevel
            var msLogLevel = ConvertLogLevel(logLevel);

            // Format message with indentation if specified
            var formattedMessage = indentLevel > 0
                ? new string(' ', indentLevel * 2) + message
                : message;

            // Add caller information for trace/debug levels
            if (logLevel <= GameTraceLevel.Debug)
            {
                var fileName = System.IO.Path.GetFileName(callerFilePath);
                formattedMessage = $"[{fileName}:{callerLineNumber} {callerMemberName}] {formattedMessage}";
            }

            _logger.Log(msLogLevel, formattedMessage);
        }

        /// <summary>
        /// Converts GameTraceLevel to Microsoft.Extensions.Logging.LogLevel.
        /// </summary>
        private static Microsoft.Extensions.Logging.LogLevel ConvertLogLevel(GameTraceLevel gameLogLevel)
        {
            return gameLogLevel switch
            {
                GameTraceLevel.Trace => Microsoft.Extensions.Logging.LogLevel.Trace,
                GameTraceLevel.Debug => Microsoft.Extensions.Logging.LogLevel.Debug,
                GameTraceLevel.Information => Microsoft.Extensions.Logging.LogLevel.Information,
                GameTraceLevel.Warning => Microsoft.Extensions.Logging.LogLevel.Warning,
                GameTraceLevel.Error => Microsoft.Extensions.Logging.LogLevel.Error,
                GameTraceLevel.Critical => Microsoft.Extensions.Logging.LogLevel.Critical,
                GameTraceLevel.None => Microsoft.Extensions.Logging.LogLevel.None,
                _ => Microsoft.Extensions.Logging.LogLevel.Information
            };
        }
    }
}