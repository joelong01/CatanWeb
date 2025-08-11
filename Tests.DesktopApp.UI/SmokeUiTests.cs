using System;
using System.Diagnostics;
using System.IO;
using FlaUI.Core;
using FlaUI.UIA3;
using Xunit;
using Xunit.Sdk;

namespace Tests.DesktopApp.UI
{
    public class SmokeUiTests : IDisposable
    {
        private Application? _app;
        private UIA3Automation? _automation;

        public void Dispose()
        {
            try { _app?.Close(); } catch { }
            _automation?.Dispose();
        }

    [Fact(Skip = "Using packaged-launch smoke test instead")] 
        public void Launches_And_Shows_MainWindow()
        {
            var exePath = ResolveDesktopAppExe();
            var psi = new ProcessStartInfo(exePath, "--test")
            {
                UseShellExecute = false,
                WorkingDirectory = Path.GetDirectoryName(exePath)!
            };

            try
            {
                _app = Application.Launch(psi);
                _automation = new UIA3Automation();
                _app.WaitWhileMainHandleIsMissing(TimeSpan.FromSeconds(10));
                var main = _app.GetMainWindow(_automation, TimeSpan.FromSeconds(10));
                Assert.NotNull(main);
            }
            catch (System.Runtime.InteropServices.COMException ex) when ((uint)ex.HResult == 0x80040154)
            {
                throw new XunitException("Windows App Runtime not registered (REGDB_E_CLASSNOTREG). Install Windows App SDK Runtime 1.7+ for this user.");
            }
        }

        private static string ResolveDesktopAppExe()
        {
            // Walk up to repo root
            var probe = new DirectoryInfo(AppContext.BaseDirectory);
            bool HasSln(DirectoryInfo d) => File.Exists(Path.Combine(d.FullName, "Catan.sln")) || File.Exists(Path.Combine(d.FullName, "Catan3.sln"));
            while (probe != null && !HasSln(probe)) probe = probe.Parent;
            Assert.True(probe != null, "Could not locate solution root.");

            var arch = Environment.Is64BitProcess ? "x64" : "x86";
            var rid = arch == "x64" ? "win-x64" : "win-x86";
            var exePath = Path.Combine(probe!.FullName, $@"DesktopApp\\bin\\{arch}\\Debug\\net9.0-windows10.0.22621.0\\{rid}\\Catan Desktop.exe");
            Assert.True(File.Exists(exePath), $"App not found at '{exePath}'. Build DesktopApp first.");
            return exePath;
        }
    }
}
