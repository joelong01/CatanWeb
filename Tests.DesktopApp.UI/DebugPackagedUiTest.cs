using System;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading;
using FlaUI.Core;
using FlaUI.Core.AutomationElements;
using FlaUI.Core.Definitions;
using FlaUI.Core.Tools;
using FlaUI.UIA3;
using Xunit;
using Xunit.Sdk;
using Tests.DesktopApp.UI.TestInfra;

namespace Tests.DesktopApp.UI
{
    /// <summary>
    /// Debug version of the packaged UI test to isolate the automation issue.
    /// </summary>
    [Collection("UIAutomation")]
    public class DebugPackagedUiTest : IDisposable
    {
        private UIA3Automation? _automation;
        private AutomationElement? _main;

        public void Dispose()
        {
            try
            {
                _main?.AsWindow()?.Close();
            }
            catch { }
            _automation?.Dispose();
        }

        [Fact]
        public void Debug_GridView_Structure()
        {
            Sta.Run(() =>
            {
                // Launch the app
                var pfn = GetPackageFamilyNameOrThrow();
                var aumid = pfn + "!App";

                var psi = new ProcessStartInfo
                {
                    FileName = "explorer.exe",
                    Arguments = $"shell:AppsFolder\\{aumid}",
                    UseShellExecute = true
                };

                using var _ = Process.Start(psi);
                _automation = new UIA3Automation();

                // Find main window
                var win = Retry.WhileNull(
                    () => _automation.GetDesktop().FindFirstDescendant(cf =>
                        cf.ByControlType(ControlType.Window).And(cf.ByClassName("WinUIDesktopWin32WindowClass"))),
                    timeout: TimeSpan.FromSeconds(25),
                    interval: TimeSpan.FromMilliseconds(250),
                    throwOnTimeout: false
                ).Result;

                Assert.NotNull(win);
                _main = win!;

                // Wait for the UI to load
                Thread.Sleep(3000);

                // Find the GridView specifically
                System.Diagnostics.Debug.WriteLine("=== Looking for PlayersGridView ===");
                var gridView = Retry.WhileNull(() => 
                {
                    try
                    {
                        return _main.FindFirstDescendant(cf => cf.ByAutomationId("PlayersGridView"));
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"Error finding GridView: {ex.Message}");
                        return null;
                    }
                }, timeout: TimeSpan.FromSeconds(10), interval: TimeSpan.FromMilliseconds(500)).Result;

                if (gridView == null)
                {
                    System.Diagnostics.Debug.WriteLine("PlayersGridView not found, searching for any GridView elements");
                    var allGridViews = _main.FindAllDescendants(cf => cf.ByControlType(ControlType.List));
                    System.Diagnostics.Debug.WriteLine($"Found {allGridViews.Length} List controls");
                    foreach (var gv in allGridViews)
                    {
                        try
                        {
                            System.Diagnostics.Debug.WriteLine($"  GridView: AutomationId='{gv.AutomationId ?? "(null)"}', Name='{gv.Name ?? "(null)"}'");
                        }
                        catch (Exception ex)
                        {
                            System.Diagnostics.Debug.WriteLine($"  GridView: Error accessing properties: {ex.Message}");
                        }
                    }
                    Assert.Fail("PlayersGridView not found");
                }

                System.Diagnostics.Debug.WriteLine("Found PlayersGridView, examining children...");

                // Wait a bit more for items to populate
                Thread.Sleep(2000);

                // Examine GridView children without accessing problematic properties
                try
                {
                    var children = gridView.FindAllChildren();
                    System.Diagnostics.Debug.WriteLine($"GridView has {children.Length} direct children");

                    foreach (var child in children)
                    {
                        try
                        {
                            System.Diagnostics.Debug.WriteLine($"  Direct child: {child.ControlType}");
                            
                            // Look for grandchildren (the actual items)
                            var grandchildren = child.FindAllChildren();
                            System.Diagnostics.Debug.WriteLine($"    Has {grandchildren.Length} grandchildren");
                            
                            foreach (var gc in grandchildren.Take(3))
                            {
                                System.Diagnostics.Debug.WriteLine($"      Grandchild: {gc.ControlType}");
                            }
                        }
                        catch (Exception ex)
                        {
                            System.Diagnostics.Debug.WriteLine($"  Error examining child: {ex.Message}");
                        }
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Error examining GridView structure: {ex.Message}");
                }

                // Try to find ListItem controls specifically
                try
                {
                    var listItems = gridView.FindAllDescendants(cf => cf.ByControlType(ControlType.ListItem));
                    System.Diagnostics.Debug.WriteLine($"Found {listItems.Length} ListItem descendants");

                    if (listItems.Length > 0)
                    {
                        System.Diagnostics.Debug.WriteLine("Successfully found ListItems in GridView!");
                        Assert.True(listItems.Length >= 5, $"Expected at least 5 players, found {listItems.Length}");
                    }
                    else
                    {
                        System.Diagnostics.Debug.WriteLine("No ListItems found, this could be the issue");
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Error finding ListItems: {ex.Message}");
                    throw;
                }
            });
        }

        private static string GetPackageFamilyNameOrThrow()
        {
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
