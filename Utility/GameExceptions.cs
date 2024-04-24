using System;
using System.Runtime.CompilerServices;

namespace Catan3.Utility
{


    public class GameException : Exception
    {
        public GameException() : base() { }

        public GameException(string message, [CallerMemberName] string cmb = "", [CallerLineNumber] int cln = 0, [CallerFilePath] string cfp = "") : base($"{cfp}({cln}):{message}\t\t[Caller={cmb}]") { }

        public GameException(string message, Exception innerException) : base(message, innerException) { }
    }

}
