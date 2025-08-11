using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using FlaUI.Core;
using FlaUI.Core.AutomationElements;
using FlaUI.Core.Tools;
using FlaUI.UIA3;
using Xunit;
using Xunit.Sdk;

namespace Tests.DesktopApp.UI
{
    /// <summary>
    /// Stateful UI test that mirrors the proven CLI flow at a UI level.
    /// We validate state transitions and core board interactions (shuffle/undo/redo)
    /// up to WaitingForRoll. We avoid simulating a roll via Next (CLI asserts Next is
    /// disabled until a roll occurs) and instead assert Next remains disabled.
    /// </summary>
    public class FullCycleUiTests : IDisposable
    {
    private Application? _app;
    private UIA3Automation? _automation;
    private Window? _main;
    private readonly string _exePath;
    private Window Main => _main ?? throw new InvalidOperationException("Main window not initialized");

        public FullCycleUiTests()
        {
            // Locate repo root by walking up from the test bin directory looking for a solution file
            var probe = new DirectoryInfo(AppContext.BaseDirectory);
            bool HasSln(DirectoryInfo d) => File.Exists(Path.Combine(d.FullName, "Catan.sln")) || File.Exists(Path.Combine(d.FullName, "Catan3.sln"));
            while (probe != null && !HasSln(probe))
            {
                probe = probe.Parent;
            }
            Assert.True(probe != null && Directory.Exists(Path.Combine(probe.FullName, "DesktopApp")), "Unable to locate solution root containing DesktopApp folder");

            // Determine architecture-specific output
            var arch = Environment.Is64BitProcess ? "x64" : "x86";
            var rid = arch == "x64" ? "win-x64" : "win-x86";
            _exePath = Path.Combine(probe!.FullName, $@"DesktopApp\\bin\\{arch}\\Debug\\net9.0-windows10.0.22621.0\\{rid}\\Catan Desktop.exe");
        }

        [Fact(Skip = "Temporarily disabled while bringing up smoke UI test; re-enable after environment is validated.")]
        public void Full_Stateful_Flow_Expansion_FivePlayers()
        {
            // Skipped
            LaunchAppOrFail();

            // New Game page: choose Expansion, select 5 players, Start
            var startBtn = FindByText("Start").AsButton();
            Assert.NotNull(startBtn);

            var gameTypeCombo = Main.FindFirstDescendant(cf => cf.ByControlType(FlaUI.Core.Definitions.ControlType.ComboBox))?.AsComboBox();
            Assert.NotNull(gameTypeCombo);
            gameTypeCombo.Select("Expansion Game");

            // Select the first 5 players in the GridView
            var playersList = Main.FindAllDescendants(cf => cf.ByClassName("ListViewItem"));
            Assert.True(playersList.Length >= 5);
            for (int i = 0; i < 5; i++) playersList[i].Click();

            startBtn.Invoke();

            // Wait for board to render and PickingBoard state
            Assert.True(WaitForState("PickingBoard", TimeSpan.FromSeconds(5)), "Expected PickingBoard state");

            // Core board interactions on PickingBoard: Shuffle -> Undo -> Redo
            var shuffle = FindByAutomationId("ShuffleButton").AsButton();
            Assert.NotNull(shuffle);

            // Sample two tile numbers to detect changes
            var sampleA = Main.FindFirstDescendant(cf => cf.ByAutomationId("TileNumber-0_0_0"))?.AsLabel();
            var sampleB = Main.FindFirstDescendant(cf => cf.ByAutomationId("TileNumber-1_-1_0"))?.AsLabel();
            var a0 = sampleA?.Text ?? string.Empty;
            var b0 = sampleB?.Text ?? string.Empty;

            shuffle.Invoke();
            Assert.True(WaitForTileSampleChange(sampleA, sampleB, a0, b0, TimeSpan.FromSeconds(2)), "Shuffle should change tile numbers");
            var a1 = sampleA?.Text ?? string.Empty;
            var b1 = sampleB?.Text ?? string.Empty;

            // Undo should restore previous numbers
            var undo = FindByAutomationId("UndoButton").AsButton();
            Assert.NotNull(undo);
            undo.Invoke();
            Assert.True(WaitForTileSampleTo(sampleA, sampleB, a0, b0, TimeSpan.FromSeconds(2)), "Undo should restore previous tile numbers");

            // Redo should return to shuffled numbers
            var redo = FindByAutomationId("RedoButton").AsButton();
            Assert.NotNull(redo);
            redo.Invoke();
            Assert.True(WaitForTileSampleTo(sampleA, sampleB, a1, b1, TimeSpan.FromSeconds(2)), "Redo should restore shuffled tile numbers");

            // Next advances PickingBoard -> WaitingForRollForOrder
            var next = FindByAutomationId("NextButton").AsButton();
            Assert.NotNull(next);
            Assert.True(next.IsEnabled);
            next.Invoke();
            Assert.True(WaitForState("WaitingForRollForOrder", TimeSpan.FromSeconds(3)), "Expected WaitingForRollForOrder state");

            // Next advances FinishedRollOrder
            next = FindByAutomationId("NextButton").AsButton();
            Assert.True(next.IsEnabled);
            next.Invoke();
            Assert.True(WaitForState("FinishedRollOrder", TimeSpan.FromSeconds(3)), "Expected FinishedRollOrder state");

            // Next advances BeginResourceAllocation
            next = FindByAutomationId("NextButton").AsButton();
            Assert.True(next.IsEnabled);
            next.Invoke();
            Assert.True(WaitForState("BeginResourceAllocation", TimeSpan.FromSeconds(3)), "Expected BeginResourceAllocation state");

            // Proceed through allocation phases by advancing; the CLI performs placements,
            // but the UI test focuses on verifying state sequence consistency.
            next = FindByAutomationId("NextButton").AsButton();
            next.Invoke();
            Assert.True(WaitForState("AllocateResourceForward", TimeSpan.FromSeconds(3)), "Expected AllocateResourceForward state");

            next = FindByAutomationId("NextButton").AsButton();
            next.Invoke();
            Assert.True(WaitForState("AllocateResourceReverse", TimeSpan.FromSeconds(3)), "Expected AllocateResourceReverse state");

            next = FindByAutomationId("NextButton").AsButton();
            next.Invoke();
            Assert.True(WaitForState("DoneResourceAllocation", TimeSpan.FromSeconds(3)), "Expected DoneResourceAllocation state");

            next = FindByAutomationId("NextButton").AsButton();
            next.Invoke();
            Assert.True(WaitForState("WaitingForRoll", TimeSpan.FromSeconds(3)), "Expected WaitingForRoll state");

            // Align with CLI: Next should be disabled in WaitingForRoll until a roll occurs
            next = FindByAutomationId("NextButton").AsButton();
            Assert.False(next.IsEnabled);
        }

        public void Dispose()
        {
            try { _app?.Close(); } catch { }
            _automation?.Dispose();
        }

        private static void Wait(int ms) => Thread.Sleep(ms);

        private void LaunchAppOrFail()
        {
            if (!File.Exists(_exePath))
            {
                throw new XunitException($"App not found at '{_exePath}'. Build the DesktopApp project before running UI tests.");
            }
            var psi = new ProcessStartInfo(_exePath, "--test")
            {
                UseShellExecute = false,
                WorkingDirectory = Path.GetDirectoryName(_exePath)!
            };
            try
            {
                _app = Application.Launch(psi);
                _automation = new UIA3Automation();
                _app!.WaitWhileMainHandleIsMissing(TimeSpan.FromSeconds(10));
                _main = _app.GetMainWindow(_automation, TimeSpan.FromSeconds(10));
                if (_main == null)
                {
                    SafeCleanup();
                    throw new XunitException("Failed to obtain main window handle from launched process. If the process exited immediately, a common cause is a missing Windows App Runtime (REGDB_E_CLASSNOTREG). Install Windows App SDK Runtime 1.7+ and re-run.");
                }
            }
            catch (System.Runtime.InteropServices.COMException ex) when ((uint)ex.HResult == 0x80040154)
            {
                // Windows App SDK not registered for this environment/session
                SafeCleanup();
                throw new XunitException($"Failed to launch WinUI 3 app: Windows App Runtime is not registered (REGDB_E_CLASSNOTREG). Ensure Windows App Runtime is installed for the current user/session. Details: 0x{(uint)ex.HResult:X8} {ex.Message}");
            }
            catch (Exception ex)
            {
                // Any other startup failure (e.g., process exited immediately)
                SafeCleanup();
                throw new XunitException($"Failed to launch app: {ex.GetType().Name}: {ex.Message}");
            }
        }

        private void SafeCleanup()
        {
            try { _main = null; } catch { }
            try { _automation?.Dispose(); } catch { }
            try { _app?.Close(); } catch { }
            _automation = null;
            _app = null;
        }

        private AutomationElement FindByAutomationId(string automationId)
        {
            var el = Retry.WhileNull(() => Main.FindFirstDescendant(cf => cf.ByAutomationId(automationId)), timeout: TimeSpan.FromSeconds(2)).Result;
            Assert.NotNull(el);
            return el!;
        }

        private AutomationElement FindByText(string text)
        {
            var el = Retry.WhileNull(() => Main.FindFirstDescendant(cf => cf.ByText(text)), timeout: TimeSpan.FromSeconds(2)).Result;
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
                Thread.Sleep(100);
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
                Thread.Sleep(100);
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
                Thread.Sleep(100);
            }
            return false;
        }
    }
}


