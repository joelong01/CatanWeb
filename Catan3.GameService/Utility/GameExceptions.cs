using System;
using Catan3.Shared.Models;

namespace Catan3.GameService.Utility
{
    public class GameException : Exception
    {
        // Property to store the error level of the exception
        public ErrorLevel ErrorLevel { get; private set; }
        
        // Default constructor with Information as default ErrorLevel
        public GameException() : base()
        {
            ErrorLevel = ErrorLevel.Information;
        }
        
        // Constructor that allows setting the message and uses Information as default ErrorLevel
        public GameException(string message) : base(message)
        {
            ErrorLevel = ErrorLevel.Information;
        }
        
        // Constructor that allows setting the message and the error level
        public GameException(string message, ErrorLevel errorLevel) : base(message)
        {
            ErrorLevel = errorLevel;
        }
        
        // Constructor that allows setting the message, inner exception, and uses Information as default ErrorLevel
        public GameException(string message, Exception innerException) : base(message, innerException)
        {
            ErrorLevel = ErrorLevel.Information;
        }
        
        // Constructor that allows setting the message, inner exception, and error level
        public GameException(string message, Exception innerException, ErrorLevel errorLevel) : base(message, innerException)
        {
            ErrorLevel = errorLevel;
        }
    }
}