using System;
using System.Diagnostics;
using System.IO;
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
    /// End-to-end UI test against the packaged app (MSIX). Launches via AUMID and
    /// validates the core flow similar to the CLI parity test.
    /// </summary>
    [Collection("UIAutomation")]
    public class FullCyclePackagedUiTests : IDisposable
    {
        private UIA3Automation? _automation;
        private AutomationElement? _main;
        private AutomationElement Main => _main ?? throw new InvalidOperationException("Main window not initialized");

        public void Dispose()
        {
            try
            {
                // Attempt to close the window cleanly after test
                _main?.AsWindow()?.Close();
            }
            catch { }
            _automation?.Dispose();
        }

        [Fact]
        public void Full_Stateful_Flow_PackagedApp_Expansion_FivePlayers()
        {
            Sta.Run(() =>
            {
                LaunchPackagedAppAndAttachToMainWindow();

                // Wait for the NewGame page to be fully loaded
                WaitForNewGamePageToLoad();

                // New Game page: choose Expansion, select 5 players, Start
                var startBtn = FindByAutomationId("StartButton").AsButton();
                Assert.NotNull(startBtn);

                var gameTypeCombo = FindByAutomationId("GameTypeCombo").AsComboBox();
                Assert.NotNull(gameTypeCombo);
                gameTypeCombo.Select("Expansion Game");

                // Select the first 5 players in the GridView
                var playersGridView = FindByAutomationId("PlayersGridView").AsGrid();
                Assert.NotNull(playersGridView);
                
                // Wait a moment for the GridView to populate
                Thread.Sleep(1000);
                
                // Debug: Check what children the GridView has
                var allChildren = playersGridView.FindAllDescendants();
                System.Diagnostics.Debug.WriteLine($"GridView has {allChildren.Length} total descendants");
                foreach (var child in allChildren.Take(10)) // Show first 10
                {
                    System.Diagnostics.Debug.WriteLine($"  Child: {child.ControlType} - {child.Name} - {child.AutomationId}");
                }
                
                // Try different selectors for player items
                var dataItems = playersGridView.FindAllDescendants(cf => cf.ByControlType(ControlType.DataItem));
                var listItems = playersGridView.FindAllDescendants(cf => cf.ByControlType(ControlType.ListItem));
                var gridItems = playersGridView.FindAllDescendants(cf => cf.ByControlType(ControlType.Custom));
                
                System.Diagnostics.Debug.WriteLine($"Found {dataItems.Length} DataItems, {listItems.Length} ListItems, {gridItems.Length} Custom items");
                
                // Use whichever selector finds items
                var playerItems = dataItems.Length > 0 ? dataItems : 
                                 listItems.Length > 0 ? listItems : 
                                 gridItems.Length > 0 ? gridItems : 
                                 new AutomationElement[0];
                
                Assert.True(playerItems.Length >= 5, $"Expected at least 5 players, found {playerItems.Length}. DataItems={dataItems.Length}, ListItems={listItems.Length}, Custom={gridItems.Length}");
                for (int i = 0; i < 5; i++) 
                {
                    playerItems[i].Click();
                }

                startBtn.Invoke();

                // Wait for board to render and PickingBoard state
                Assert.True(WaitForState("PickingBoard", TimeSpan.FromSeconds(10)), "Expected PickingBoard state");

                // Core board interactions on PickingBoard: Shuffle -> Undo -> Redo
                var shuffle = FindByAutomationId("ShuffleButton").AsButton();
                Assert.NotNull(shuffle);

                // Sample two tile numbers to detect changes
                var sampleA = Main.FindFirstDescendant(cf => cf.ByAutomationId("TileNumber-0_0_0"))?.AsLabel();
                var sampleB = Main.FindFirstDescendant(cf => cf.ByAutomationId("TileNumber-1_-1_0"))?.AsLabel();
                var a0 = sampleA?.Text ?? string.Empty;
                var b0 = sampleB?.Text ?? string.Empty;

                shuffle.Invoke();
                Assert.True(WaitForTileSampleChange(sampleA, sampleB, a0, b0, TimeSpan.FromSeconds(4)), "Shuffle should change tile numbers");
                var a1 = sampleA?.Text ?? string.Empty;
                var b1 = sampleB?.Text ?? string.Empty;

                // Undo should restore previous numbers
                var undo = FindByAutomationId("UndoButton").AsButton();
                Assert.NotNull(undo);
                undo.Invoke();
                Assert.True(WaitForTileSampleTo(sampleA, sampleB, a0, b0, TimeSpan.FromSeconds(4)), "Undo should restore previous tile numbers");

                // Redo should return to shuffled numbers
                var redo = FindByAutomationId("RedoButton").AsButton();
                Assert.NotNull(redo);
                redo.Invoke();
                Assert.True(WaitForTileSampleTo(sampleA, sampleB, a1, b1, TimeSpan.FromSeconds(4)), "Redo should restore shuffled tile numbers");

                // Next advances PickingBoard -> WaitingForRollForOrder
                var next = FindByAutomationId("NextButton").AsButton();
                Assert.NotNull(next);
                Assert.True(next.IsEnabled);
                next.Invoke();
                Assert.True(WaitForState("WaitingForRollForOrder", TimeSpan.FromSeconds(6)), "Expected WaitingForRollForOrder state");

                // Next advances FinishedRollOrder
                next = FindByAutomationId("NextButton").AsButton();
                Assert.True(next.IsEnabled);
                next.Invoke();
                Assert.True(WaitForState("FinishedRollOrder", TimeSpan.FromSeconds(6)), "Expected FinishedRollOrder state");

                // Next advances BeginResourceAllocation
                next = FindByAutomationId("NextButton").AsButton();
                Assert.True(next.IsEnabled);
                next.Invoke();
                Assert.True(WaitForState("BeginResourceAllocation", TimeSpan.FromSeconds(6)), "Expected BeginResourceAllocation state");

                // Proceed through allocation phases by advancing
                next = FindByAutomationId("NextButton").AsButton();
                next.Invoke();
                Assert.True(WaitForState("AllocateResourceForward", TimeSpan.FromSeconds(6)), "Expected AllocateResourceForward state");

                next = FindByAutomationId("NextButton").AsButton();
                next.Invoke();
                Assert.True(WaitForState("AllocateResourceReverse", TimeSpan.FromSeconds(6)), "Expected AllocateResourceReverse state");

                next = FindByAutomationId("NextButton").AsButton();
                next.Invoke();
                Assert.True(WaitForState("DoneResourceAllocation", TimeSpan.FromSeconds(6)), "Expected DoneResourceAllocation state");

                next = FindByAutomationId("NextButton").AsButton();
                next.Invoke();
                Assert.True(WaitForState("WaitingForRoll", TimeSpan.FromSeconds(6)), "Expected WaitingForRoll state");
            });
        }

        private void LaunchPackagedAppAndAttachToMainWindow()
        {
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

            var win = Retry.WhileNull(
                () => _automation.GetDesktop().FindFirstDescendant(cf =>
                    cf.ByControlType(ControlType.Window).And(cf.ByClassName("WinUIDesktopWin32WindowClass"))),
                timeout: TimeSpan.FromSeconds(25),
                interval: TimeSpan.FromMilliseconds(250),
                throwOnTimeout: false
            ).Result;

            if (win == null)
            {
                throw new XunitException($"Failed to find main window for AUMID '{aumid}'. Is the app deployed and running?");
            }
            _main = win;
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

        private AutomationElement FindByAutomationId(string automationId)
        {
            var el = Retry.WhileNull(() => Main.FindFirstDescendant(cf => cf.ByAutomationId(automationId)), timeout: TimeSpan.FromSeconds(5)).Result;
            Assert.NotNull(el);
            return el!;
        }

        private void WaitForNewGamePageToLoad()
        {
            // Wait for the StartButton to appear, which indicates the NewGame page is loaded
            var startBtn = Retry.WhileNull(() => Main.FindFirstDescendant(cf => cf.ByAutomationId("StartButton")), 
                timeout: TimeSpan.FromSeconds(10), 
                interval: TimeSpan.FromMilliseconds(500)).Result;
            
            if (startBtn == null)
            {
                throw new XunitException("NewGame page failed to load - StartButton not found within 10 seconds");
            }
        }

        private AutomationElement FindByText(string text)
        {
            var el = Retry.WhileNull(() => Main.FindFirstDescendant(cf => cf.ByText(text)), timeout: TimeSpan.FromSeconds(5)).Result;
            Assert.NotNull(el);
            return el!;
        }

        private bool WaitForState(string expected, TimeSpan timeout)
        {
            var sw = Stopwatch.StartNew();
            while (sw.Elapsed < timeout)
            {
                var stateLabel = Main.FindFirstDescendant(cf => cf.ByAutomationId("StateMessage"))?.AsLabel();
                var text = stateLabel?.Text ?? string.Empty;
                if (!string.IsNullOrWhiteSpace(text) && text.Contains(expected, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
                Thread.Sleep(120);
            }
            return false;
        }

        private static bool WaitForTileSampleChange(Label? a, Label? b, string aBefore, string bBefore, TimeSpan timeout)
        {
            var sw = Stopwatch.StartNew();
            while (sw.Elapsed < timeout)
            {
                var aNow = a?.Text ?? string.Empty;
                var bNow = b?.Text ?? string.Empty;
                if (aNow != aBefore || bNow != bBefore)
                {
                    return true;
                }
                Thread.Sleep(120);
            }
            return false;
        }

        private static bool WaitForTileSampleTo(Label? a, Label? b, string aExpected, string bExpected, TimeSpan timeout)
        {
            var sw = Stopwatch.StartNew();
            while (sw.Elapsed < timeout)
            {
                var aNow = a?.Text ?? string.Empty;
                var bNow = b?.Text ?? string.Empty;
                if (aNow == aExpected && bNow == bExpected)
                {
                    return true;
                }
                Thread.Sleep(120);
            }
            return false;
        }
    }
}
