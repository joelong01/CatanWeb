using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Catan3.Utility
{
    public class FunctionTimer : IDisposable
    {
        #region Delegates + Fields + Events + Enums

        private string? message;
        private Stopwatch? watch = null;

        #endregion Delegates + Fields + Events + Enums

        #region Properties

        public static bool Enabled { get; set; } = false;

        #endregion Properties

        #region Constructors + Destructors

        // a global flag to turn off all timing
        public FunctionTimer(string msg, bool enableOverride = false)
        {
            if (!Enabled && !enableOverride) return;
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
            this.TraceMessage($"{message}: {elapsedMs}ms");
        }

        #endregion Methods
    }
}
