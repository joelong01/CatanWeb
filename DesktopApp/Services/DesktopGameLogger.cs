using Catan3.Shared.Interfaces;
using Catan3.Shared.Utility;
using System.Runtime.CompilerServices;

namespace Catan3.Services
{
    /// <summary>
    /// Desktop implementation of IGameLogger that uses the existing TraceMessage extension.
    /// </summary>
    public class DesktopGameLogger : IGameLogger
    {
        public void Log(GameLogLevel logLevel, string message, int indentLevel = 0, 
            [CallerMemberName] string callerMemberName = "", 
            [CallerLineNumber] int callerLineNumber = 0, 
            [CallerFilePath] string callerFilePath = "")
        {
            // Use the extended Desktop extension method with log level support
            this.TraceMessage(logLevel, message, indentLevel, callerMemberName, callerLineNumber, callerFilePath);
        }
    }
}