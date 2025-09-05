using System;
using System.Runtime.InteropServices;

namespace Tests.DesktopApp.UI.ScriptedTestData
{
    /// <summary>
    /// Provides proper Windows message pumping for STA threads in WinUI3 applications.
    /// Uses Win32 APIs to pump messages without blocking UI updates.
    /// </summary>
    public static class UiPump
    {
        const uint QS_ALLINPUT = 0x04FF;
        const uint MWMO_INPUTAVAILABLE = 0x0004;

        [StructLayout(LayoutKind.Sequential)]
        struct MSG
        {
            public IntPtr hwnd;
            public uint message;
            public UIntPtr wParam;
            public IntPtr lParam;
            public uint time;
            public int pt_x;
            public int pt_y;
        }

        [DllImport("user32.dll")]
        static extern uint MsgWaitForMultipleObjectsEx(
            uint nCount, IntPtr pHandles, uint dwMilliseconds, uint dwWakeMask, uint dwFlags);

        [DllImport("user32.dll")] static extern bool PeekMessage(out MSG lpMsg, IntPtr hWnd, uint min, uint max, uint remove);
        [DllImport("user32.dll")] static extern bool TranslateMessage(ref MSG lpMsg);
        [DllImport("user32.dll")] static extern IntPtr DispatchMessage(ref MSG lpMsg);

        /// <summary>
        /// Delays for the specified duration while pumping Windows messages.
        /// This prevents STA thread blocking and allows UI updates to occur.
        /// </summary>
        /// <param name="duration">How long to delay</param>
        public static void DelayWithPump(TimeSpan duration)
        {
            var end = DateTime.UtcNow + duration;
            while (DateTime.UtcNow < end)
            {
                // Wait briefly for any input/message, then drain the queue.
                MsgWaitForMultipleObjectsEx(0, IntPtr.Zero, 10, QS_ALLINPUT, MWMO_INPUTAVAILABLE);
                while (PeekMessage(out var msg, IntPtr.Zero, 0, 0, 1))
                {
                    TranslateMessage(ref msg);
                    DispatchMessage(ref msg);
                }
            }
        }

        /// <summary>
        /// Delays for the specified number of milliseconds while pumping messages.
        /// </summary>
        /// <param name="milliseconds">Delay in milliseconds</param>
        public static void DelayWithPump(int milliseconds)
        {
            DelayWithPump(TimeSpan.FromMilliseconds(milliseconds));
        }
    }
}