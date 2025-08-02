using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;

namespace Catan3.Shared.Utility
{
    public static class Extensions
    {
        public static void TraceMessage(this object o, string toWrite, int indentLevel = 0, [CallerMemberName] string cmb = "", [CallerLineNumber] int cln = 0, [CallerFilePath] string cfp = "")
        {
            for (int i = 0; i < indentLevel; i++)
            {
                Debug.Indent();
            }
            Debug.WriteLine($"{cfp}({cln}):{toWrite}\t\t[Caller={cmb}]");
            for (int i = 0; i < indentLevel; i++)
            {
                Debug.Unindent();
            }
        }
        public static void ConsoleTrace(this object o, string toWrite, int indentLevel = 0, [CallerMemberName] string cmb = "", [CallerLineNumber] int cln = 0, [CallerFilePath] string cfp = "")
        {
            for (int i = 0; i < indentLevel; i++)
            {
                Console.WriteLine("   ");
            }
            Console.WriteLine($"{cfp}({cln}):{toWrite}\t\t[Caller={cmb}]");

        }
    }
}
