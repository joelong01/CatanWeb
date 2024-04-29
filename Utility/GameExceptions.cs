using System;
using System.Runtime.CompilerServices;

namespace Catan3.Utility
{


    public class GameException : Exception
    {
        public GameException() : base() { }

       
        public GameException(string message) : base(message) { }

        public GameException(string message, Exception innerException) : base(message, innerException) { }
    }

}
