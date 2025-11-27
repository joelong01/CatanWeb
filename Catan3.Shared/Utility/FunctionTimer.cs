using System.Diagnostics;

namespace Catan3.Shared.Utility
{

    public class FunctionTimer : IDisposable
    {
        #region Delegates + Fields + Events + Enums
        private string? message;
        private Stopwatch? watch = null;
        private bool _writeToConsole = false;
        #endregion Delegates + Fields + Events + Enums
        #region Properties
        public static bool Enabled { get; set; } = false;
        #endregion Properties
        #region Constructors + Destructors
        // a global flag to turn off all timing
        public FunctionTimer(string msg, bool enableOverride = false, bool writeToConsole = false)
        {
            if (!Enabled && !enableOverride) return;
            _writeToConsole = writeToConsole;
            watch = new Stopwatch();
            message = msg;
            watch.Start();
        }
        #endregion Constructors + Destructors
        #region Methods
        public void Dispose()
        {
            if (watch == null) return;
            watch.Stop();
            double elapsedMs = watch.ElapsedMilliseconds;
            // Use Console.WriteLine for test visibility instead of this.TraceMessage
            Console.WriteLine($"[TIMER] {message}: {elapsedMs}ms");
        }
        public static void CallTimedFunction(string description, Action action, bool enableOverride = false, bool console = false)
        {
            using (new FunctionTimer(description, enableOverride, console))
            {
                action();
            }
        }
        #endregion Methods
    }
}