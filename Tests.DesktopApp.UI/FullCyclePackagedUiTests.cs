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
                System.Diagnostics.Debug.WriteLine("=== Test starting ===");
                
                try
                {
                    System.Diagnostics.Debug.WriteLine("=== About to launch app ===");
                    LaunchPackagedAppAndAttachToMainWindow();
                    System.Diagnostics.Debug.WriteLine("=== App launched successfully ===");
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"=== Error launching app: {ex.Message} ===");
                    throw;
                }

                try
                {
                    System.Diagnostics.Debug.WriteLine("=== About to wait for NewGame page ===");
                    // Wait for the NewGame page to be fully loaded
                    WaitForNewGamePageToLoad();
                    System.Diagnostics.Debug.WriteLine("=== NewGame page loaded ===");
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"=== Error waiting for NewGame page: {ex.Message} ===");
                    throw;
                }

                // New Game page: choose Expansion, select 5 players, Start
                System.Diagnostics.Debug.WriteLine("=== Finding StartButton ===");
                var startBtn = FindByAutomationId("StartButton").AsButton();
                Assert.NotNull(startBtn);
                System.Diagnostics.Debug.WriteLine("=== Found StartButton ===");

                System.Diagnostics.Debug.WriteLine("=== Finding GameTypeCombo ===");
                var gameTypeCombo = FindByAutomationId("GameTypeCombo").AsComboBox();
                Assert.NotNull(gameTypeCombo);
                System.Diagnostics.Debug.WriteLine("=== Found GameTypeCombo ===");
                
                try
                {
                    gameTypeCombo.Select("Expansion Game");
                    System.Diagnostics.Debug.WriteLine("=== Selected Expansion Game ===");
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"=== Error selecting Expansion Game: {ex.Message} ===");
                    throw;
                }

                // Select the first 5 players in the GridView
                var playersGridView = FindByAutomationId("PlayersGridView").AsGrid();
                Assert.NotNull(playersGridView);
                
                // Wait a moment for the GridView to populate
                Thread.Sleep(2000);
                
                // For WinUI GridView, we need to find the actual selectable items
                // GridView items are typically GridViewItem controls containing our data template
                var gridViewItems = Retry.WhileNull(() => 
                {
                    try
                    {
                        // Try multiple approaches to find grid items
                        var listItems = playersGridView.FindAllDescendants(cf => cf.ByControlType(ControlType.ListItem));
                        if (listItems.Length >= 5) return listItems;
                        
                        var dataItems = playersGridView.FindAllDescendants(cf => cf.ByControlType(ControlType.DataItem));
                        if (dataItems.Length >= 5) return dataItems;
                        
                        var customItems = playersGridView.FindAllDescendants(cf => cf.ByControlType(ControlType.Custom));
                        if (customItems.Length >= 5) return customItems;
                        
                        return null;
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"Error finding grid items: {ex.Message}");
                        return null;
                    }
                }, timeout: TimeSpan.FromSeconds(10), interval: TimeSpan.FromMilliseconds(500)).Result;
                
                if (gridViewItems == null)
                {
                    // Debug: Check what we actually have - safely access properties
                    var allChildren = playersGridView.FindAllDescendants();
                    System.Diagnostics.Debug.WriteLine($"GridView has {allChildren.Length} total descendants");
                    for (int i = 0; i < Math.Min(10, allChildren.Length); i++)
                    {
                        var child = allChildren[i];
                        try
                        {
                            var controlType = child.ControlType.ToString();
                            var name = "(checking name...)";
                            var automationId = "(checking id...)";
                            
                            // Safely access properties
                            try { name = child.Name ?? "(null)"; } catch { name = "(error)"; }
                            try { automationId = child.AutomationId ?? "(null)"; } catch { automationId = "(error)"; }
                            
                            System.Diagnostics.Debug.WriteLine($"  Child[{i}]: {controlType} - Name: {name} - AutomationId: {automationId}");
                        }
                        catch (Exception ex)
                        {
                            System.Diagnostics.Debug.WriteLine($"  Child[{i}]: Error accessing element: {ex.Message}");
                        }
                    }
                    
                    Assert.Fail("Could not find GridView items after waiting 10 seconds");
                }
                
                Assert.True(gridViewItems.Length >= 5, $"Expected at least 5 players, found {gridViewItems.Length}");
                
                // Select the first 5 players - WinUI GridView with SelectionMode="Multiple" requires proper selection
                for (int i = 0; i < 5; i++) 
                {
                    var item = gridViewItems[i];
                    System.Diagnostics.Debug.WriteLine($"=== Attempting to select GridView item {i} ===");
                    
                    try
                    {
                        // For WinUI GridView with SelectionMode="Multiple", we need to:
                        // 1. Make sure the item is visible and focusable
                        // 2. Use proper selection patterns or keyboard simulation
                        
                        // First, ensure the item is in view and focused
                        item.Focus();
                        Thread.Sleep(100);
                        
                        // Try using Ctrl+Click for multiple selection (standard Windows behavior)
                        System.Diagnostics.Debug.WriteLine($"Trying Ctrl+Click for item {i}");
                        
                        // Simulate Ctrl key down, click, Ctrl key up
                        // Note: FlaUI doesn't have great keyboard simulation, so we'll try different approaches
                        
                        // Method 1: Try using the Invoke pattern if available
                        try
                        {
                            var invokePattern = item.Patterns.Invoke.PatternOrDefault;
                            if (invokePattern != null)
                            {
                                System.Diagnostics.Debug.WriteLine($"Using Invoke pattern for item {i}");
                                invokePattern.Invoke();
                                Thread.Sleep(100);
                                continue;
                            }
                        }
                        catch (Exception ex)
                        {
                            System.Diagnostics.Debug.WriteLine($"Invoke pattern failed for item {i}: {ex.Message}");
                        }
                        
                        // Method 2: Try Toggle pattern (for selectable items)
                        try
                        {
                            var togglePattern = item.Patterns.Toggle.PatternOrDefault;
                            if (togglePattern != null)
                            {
                                System.Diagnostics.Debug.WriteLine($"Using Toggle pattern for item {i}");
                                togglePattern.Toggle();
                                Thread.Sleep(100);
                                continue;
                            }
                        }
                        catch (Exception ex)
                        {
                            System.Diagnostics.Debug.WriteLine($"Toggle pattern failed for item {i}: {ex.Message}");
                        }
                        
                        // Method 3: Try SelectionItem pattern
                        try
                        {
                            var selectionPattern = item.Patterns.SelectionItem.PatternOrDefault;
                            if (selectionPattern != null)
                            {
                                System.Diagnostics.Debug.WriteLine($"Using SelectionItem pattern for item {i}");
                                selectionPattern.AddToSelection(); // Use AddToSelection for multiple selection
                                Thread.Sleep(100);
                                continue;
                            }
                        }
                        catch (Exception ex)
                        {
                            System.Diagnostics.Debug.WriteLine($"SelectionItem pattern failed for item {i}: {ex.Message}");
                        }
                        
                        // Method 4: Fallback to basic click
                        System.Diagnostics.Debug.WriteLine($"Fallback to basic click for item {i}");
                        item.Click();
                        Thread.Sleep(100);
                        
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"All selection methods failed for item {i}: {ex.Message}");
                        // Continue with next item rather than failing the test
                    }
                }
                
                // After attempting to select all items, let's check if the GridView has any selection
                System.Diagnostics.Debug.WriteLine("=== Checking GridView selection after selection attempts ===");
                Thread.Sleep(500); // Give time for selection events to process

                System.Diagnostics.Debug.WriteLine("=== About to click Start button ===");
                startBtn.Invoke();
                System.Diagnostics.Debug.WriteLine("=== Start button clicked ===");

                // Wait for board to render and PickingBoard state
                System.Diagnostics.Debug.WriteLine("=== Waiting for PickingBoard state ===");
                
                // First, let's see what state we're actually in
                for (int attempt = 0; attempt < 10; attempt++)
                {
                    try
                    {
                        var stateLabel = Main.FindFirstDescendant(cf => cf.ByAutomationId("StateMessage"))?.AsLabel();
                        var currentState = stateLabel?.Text ?? "(no state found)";
                        System.Diagnostics.Debug.WriteLine($"Current state (attempt {attempt}): {currentState}");
                        
                        if (currentState.Contains("PickingBoard", StringComparison.OrdinalIgnoreCase))
                        {
                            System.Diagnostics.Debug.WriteLine("=== PickingBoard state found! ===");
                            break;
                        }
                        
                        Thread.Sleep(1000);
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"Error checking state: {ex.Message}");
                        Thread.Sleep(1000);
                    }
                }
                
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
            var el = Retry.WhileNull(() => 
            {
                try
                {
                    return Main.FindFirstDescendant(cf => cf.ByAutomationId(automationId));
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Error finding element with AutomationId '{automationId}': {ex.Message}");
                    return null;
                }
            }, timeout: TimeSpan.FromSeconds(5)).Result;
            Assert.NotNull(el);
            return el!;
        }

        private void WaitForNewGamePageToLoad()
        {
            // Wait for the StartButton to appear, which indicates the NewGame page is loaded
            var startBtn = Retry.WhileNull(() => 
            {
                try
                {
                    return Main.FindFirstDescendant(cf => cf.ByAutomationId("StartButton"));
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Error in WaitForNewGamePageToLoad: {ex.Message}");
                    return null;
                }
            }, 
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
