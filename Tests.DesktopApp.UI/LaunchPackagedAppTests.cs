using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using FlaUI.Core;
using FlaUI.Core.AutomationElements;
using FlaUI.Core.Tools;
using FlaUI.UIA3;
using Xunit;
using Tests.DesktopApp.UI.TestInfra;
using Xunit.Sdk;

namespace Tests.DesktopApp.UI
{
    [Collection("UIAutomation")]
    public class LaunchPackagedAppTests : IDisposable
    {
        private UIA3Automation? _automation;

        public void Dispose()
        {
            _automation?.Dispose();
        }

        [Fact]
        public void Launches_Packaged_App_Shows_MainWindow()
        {
            Sta.Run(() =>
            {
                // Find the installed package family and AppUserModelId
                var pfn = GetPackageFamilyNameOrThrow();
                var aumid = pfn + "!App"; // App Id from Package.appxmanifest

                // Launch via explorer.exe shell:AppsFolder\{AUMID}
                var start = new ProcessStartInfo
                {
                    FileName = "explorer.exe",
                    Arguments = $"shell:AppsFolder\\{aumid}",
                    UseShellExecute = true
                };
                using var proc = Process.Start(start);

                // Attach to the main window by class
                _automation = new UIA3Automation();
                var ok = Retry.WhileNull(
                    () => _automation.GetDesktop().FindFirstDescendant(cf =>
                        cf.ByControlType(FlaUI.Core.Definitions.ControlType.Window)
                          .And(cf.ByClassName("WinUIDesktopWin32WindowClass"))),
                    timeout: TimeSpan.FromSeconds(25),
                    interval: TimeSpan.FromMilliseconds(250),
                    throwOnTimeout: false
                ).Result != null;

                if (!ok)
                {
                    throw new XunitException($"Failed to find 'Catan Desktop' main window after launching AUMID '{aumid}'. Ensure the app is deployed/registered.");
                }
            });
        }

        private static string GetPackageFamilyNameOrThrow()
        {
            // Identity Name from Package.appxmanifest
            const string identityName = "606d7833-a1be-4389-aa5f-fe8dd1dd1da3";

            var psi = new ProcessStartInfo
            {
                FileName = "powershell.exe",
                Arguments = "-NoProfile -NonInteractive -ExecutionPolicy Bypass -Command \"(Get-AppxPackage -Name '" + identityName + "*').PackageFamilyName\"",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
                StandardOutputEncoding = Encoding.UTF8,
                StandardErrorEncoding = Encoding.UTF8
            };
            using var ps = Process.Start(psi);
            ps!.WaitForExit(5000);
            var output = (ps.StandardOutput.ReadToEnd() ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(output))
            {
                throw new XunitException("App package is not installed. Build/deploy the MSIX before running packaged UI tests.");
            }
            return output.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)[0].Trim();
        }
    }
}
