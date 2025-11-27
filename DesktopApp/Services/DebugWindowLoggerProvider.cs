using Microsoft.Extensions.Logging;
using System;
using System.Collections.Concurrent;
using Catan3.Shared.Interfaces;

namespace Catan3.Services
{
    /// <summary>
    /// Custom logger provider for DesktopApp that outputs to both Debug.WriteLine and DebugWindow
    /// </summary>
    public class DebugWindowLoggerProvider : ILoggerProvider
    {
        private readonly ConcurrentDictionary<string, DebugWindowLogger> _loggers = new();
        private readonly GameTraceLevel _minLevel;

        public DebugWindowLoggerProvider(GameTraceLevel minLevel = GameTraceLevel.Trace)
        {
            _minLevel = minLevel;
        }

        public ILogger CreateLogger(string categoryName)
        {
            return _loggers.GetOrAdd(categoryName, name => new DebugWindowLogger(name, _minLevel));
        }

        public void Dispose()
        {
            _loggers.Clear();
        }
    }

    /// <summary>
    /// Custom logger implementation that writes to both Debug output and DebugWindow
    /// </summary>
    internal class DebugWindowLogger : ILogger
    {
        private readonly string _categoryName;
        private readonly GameTraceLevel _minLevel;

        public DebugWindowLogger(string categoryName, GameTraceLevel minLevel)
        {
            _categoryName = categoryName;
            _minLevel = minLevel;
        }

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull
        {
            return null;
        }

        public bool IsEnabled(LogLevel logLevel)
        {
            var gameLogLevel = MapToGameLogLevel(logLevel);
            return gameLogLevel >= _minLevel;
        }

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            if (!IsEnabled(logLevel))
                return;

            var message = formatter(state, exception);

            if (exception != null)
            {
                message += Environment.NewLine + exception.ToString();
            }

            // Write the message as-is to both outputs (it already has file/line info from TraceMessage)
            // Write to Debug output (goes to VS Code Debug Window)
            System.Diagnostics.Debug.WriteLine(message);

            // Write to DebugWindow (goes to Catan Debug Messages)
            DebugWindow.ShowMessage(message);
        }

        /// <summary>
        /// Maps Microsoft.Extensions.Logging.LogLevel to our custom GameTraceLevel
        /// </summary>
        private static GameTraceLevel MapToGameLogLevel(LogLevel logLevel)
        {
            return logLevel switch
            {
                LogLevel.Trace => GameTraceLevel.Trace,
                LogLevel.Debug => GameTraceLevel.Debug,
                LogLevel.Information => GameTraceLevel.Information,
                LogLevel.Warning => GameTraceLevel.Warning,
                LogLevel.Error => GameTraceLevel.Error,
                LogLevel.Critical => GameTraceLevel.Error,
                _ => GameTraceLevel.Trace
            };
        }
    }
}