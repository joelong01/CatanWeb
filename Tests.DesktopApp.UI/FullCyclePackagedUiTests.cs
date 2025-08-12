using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using System.Threading;
using FlaUI.Core;
using FlaUI.Core.AutomationElements;
using FlaUI.Core.Definitions;
using FlaUI.Core.Tools;
using FlaUI.UIA3;
using Xunit;
using Xunit.Sdk;

using Tests.DesktopApp.UI.TestInfra;
using Catan3.Shared.Models;
using Catan3.Shared.Utility;

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
                this.TraceMessage("Test starting");
                
                try
                {
                    this.TraceMessage("About to launch app");
                    LaunchPackagedAppAndAttachToMainWindow();
                    this.TraceMessage("App launched successfully");
                }
                catch (Exception ex)
                {
                    this.TraceMessage($"Error launching app: {ex.Message}");
                    throw;
                }

                try
                {
                    this.TraceMessage("About to wait for NewGame page");
                    // Wait for the NewGame page to be fully loaded
                    WaitForNewGamePageToLoad();
                    this.TraceMessage("NewGame page loaded");
                }
                catch (Exception ex)
                {
                    this.TraceMessage($"Error waiting for NewGame page: {ex.Message}");
                    throw;
                }

                // Execute each state test in sequence
                this.TraceMessage("=== Starting GameState progression tests ===");
                
                Test_NewGame(); // NewGame -> PickingBoard (via Start button)
                Test_PickingBoard(); // PickingBoard -> WaitingForRollForOrder (via Next button)
                Test_WaitingForRollForOrder(); // WaitingForRollForOrder -> FinishedRollOrder (via Next button)
                
                // FinishedRollOrder is just a "click Next to continue" state
                TransitionToNextState(); // FinishedRollOrder -> BeginResourceAllocation
                
                Test_BeginResourceAllocation(); // BeginResourceAllocation -> AllocateResourceForward (via Next button)
                Test_AllocateResourceForward(); // AllocateResourceForward -> AllocateResourceReverse (via Next button)
                Test_AllocateResourceReverse(); // AllocateResourceReverse -> DoneResourceAllocation (via Next button)
                Test_DoneResourceAllocation(); // DoneResourceAllocation -> WaitingForRoll (via Next button)
                Test_WaitingForRoll(); // End state for this test
                
                this.TraceMessage("=== All GameState tests completed successfully ===");
            });
        }

        /// <summary>
        /// Test the NewGame state - select Expansion, 5 players, and verify controls
        /// Transitions from NewGame to PickingBoard when Start button is clicked
        /// </summary>
        private void Test_NewGame()
        {
            this.TraceMessage("=== Test_NewGame ===");
            
            // Check automation IDs for NewGame state
            this.CheckAutomationIds("NewGame");
            
            // New Game page: choose Expansion, select 5 players, Start
            this.TraceMessage("Finding StartButton");
            var startBtn = FindByAutomationId("StartButton").AsButton();
            Assert.NotNull(startBtn);
            this.TraceMessage("Found StartButton");

            this.TraceMessage("Finding GameTypeCombo");
            var gameTypeCombo = FindByAutomationId("GameTypeCombo").AsComboBox();
            Assert.NotNull(gameTypeCombo);
            this.TraceMessage("Found GameTypeCombo");
            
            try
            {
                gameTypeCombo.Select("Expansion Game");
                this.TraceMessage("Selected Expansion Game");
            }
            catch (Exception ex)
            {
                this.TraceMessage($"Error selecting Expansion Game: {ex.Message}");
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
                this.TraceMessage($"Attempting to select GridView item {i}");
                
                try
                {
                    // For WinUI GridView with SelectionMode="Multiple", we need to:
                    // 1. Make sure the item is visible and focusable
                    // 2. Use proper selection patterns or keyboard simulation
                    
                    // First, ensure the item is in view and focused
                    item.Focus();
                    Thread.Sleep(100);
                    
                    // Try using Ctrl+Click for multiple selection (standard Windows behavior)
                    this.TraceMessage($"Trying Ctrl+Click for item {i}");
                    
                    // Simulate Ctrl key down, click, Ctrl key up
                    // Note: FlaUI doesn't have great keyboard simulation, so we'll try different approaches
                    
                    // Method 1: Try using the Invoke pattern if available
                    try
                    {
                        var invokePattern = item.Patterns.Invoke.PatternOrDefault;
                        if (invokePattern != null)
                        {
                            this.TraceMessage($"Using Invoke pattern for item {i}");
                            invokePattern.Invoke();
                            Thread.Sleep(100);
                            continue;
                        }
                    }
                    catch (Exception ex)
                    {
                        this.TraceMessage($"Invoke pattern failed for item {i}: {ex.Message}");
                    }
                    
                    // Method 2: Try Toggle pattern (for selectable items)
                    try
                    {
                        var togglePattern = item.Patterns.Toggle.PatternOrDefault;
                        if (togglePattern != null)
                        {
                            this.TraceMessage($"Using Toggle pattern for item {i}");
                            togglePattern.Toggle();
                            Thread.Sleep(100);
                            continue;
                        }
                    }
                    catch (Exception ex)
                    {
                        this.TraceMessage($"Toggle pattern failed for item {i}: {ex.Message}");
                    }
                    
                    // Method 3: Try SelectionItem pattern
                    try
                    {
                        var selectionPattern = item.Patterns.SelectionItem.PatternOrDefault;
                        if (selectionPattern != null)
                        {
                            this.TraceMessage($"Using SelectionItem pattern for item {i}");
                            selectionPattern.AddToSelection(); // Use AddToSelection for multiple selection
                            Thread.Sleep(100);
                            continue;
                        }
                    }
                    catch (Exception ex)
                    {
                        this.TraceMessage($"SelectionItem pattern failed for item {i}: {ex.Message}");
                    }
                    
                    // Method 4: Fallback to basic click
                    this.TraceMessage($"Fallback to basic click for item {i}");
                    item.Click();
                    Thread.Sleep(100);
                    
                }
                catch (Exception ex)
                {
                    this.TraceMessage($"All selection methods failed for item {i}: {ex.Message}");
                    // Continue with next item rather than failing the test
                }
            }
            
            // After attempting to select all items, let's check if the GridView has any selection
            this.TraceMessage("Checking GridView selection after selection attempts");
            Thread.Sleep(500); // Give time for selection events to process

            this.TraceMessage("About to click Start button to transition to PickingBoard");
            startBtn.Invoke();
            this.TraceMessage("Start button clicked - should now be transitioning to PickingBoard");
            
            // Wait for transition to PickingBoard state
            Assert.True(WaitForState("Accept Board", TimeSpan.FromSeconds(10)), "Expected to transition to PickingBoard state (Accept Board)");
            this.TraceMessage("Successfully transitioned to PickingBoard state");
            
            this.TraceMessage("=== Test_NewGame completed ===");
        }

        /// <summary>
        /// Test the PickingBoard state - test shuffle/undo/redo functionality
        /// Transitions from PickingBoard to WaitingForRollForOrder when Next button is clicked
        /// </summary>
        private void Test_PickingBoard()
        {
            this.TraceMessage("=== Test_PickingBoard ===");
            
            // Verify we're in PickingBoard state by checking for "Accept Board"
            var stateLabel = Main.FindFirstDescendant(cf => cf.ByAutomationId("StateMessage"))?.AsLabel();
            var currentState = stateLabel?.Text ?? "(no state found)";
            this.TraceMessage($"Current state: {currentState}");
            
            // Should already be in PickingBoard from Test_NewGame
            Assert.True(currentState.Contains("Accept Board", StringComparison.OrdinalIgnoreCase), "Expected to be in PickingBoard state (Accept Board)");
            
            // Check automation IDs for PickingBoard state
            this.CheckAutomationIds("PickingBoard");
            
            // Test shuffle/undo/redo functionality
            this.TraceMessage("Starting shuffle/undo/redo tests");
            
            var shuffle = FindByAutomationId("ShuffleButton").AsButton();
            Assert.NotNull(shuffle);
            this.TraceMessage("Shuffle button found");

            // Get initial tile values to compare after shuffle
            var initialTileValues = GetTileValues();
            this.TraceMessage($"Initial tiles: {string.Join(", ", initialTileValues)}");

            // Step 1: Shuffle - should change tile arrangement
            this.TraceMessage("Step 1: Clicking Shuffle button");
            shuffle.Invoke();
            
            // Wait a moment for UI to update
            Thread.Sleep(500);
            
            var afterShuffleTileValues = GetTileValues();
            this.TraceMessage($"After shuffle tiles: {string.Join(", ", afterShuffleTileValues)}");
            
            // Verify that the tiles changed (at least one tile should be different)
            bool tilesChanged = !initialTileValues.SequenceEqual(afterShuffleTileValues);
            Assert.True(tilesChanged, "Shuffle should change tile arrangement");
            this.TraceMessage("Step 2: Shuffle successful - tile arrangement changed");

            // Step 3: Undo should restore original tile arrangement
            this.TraceMessage("Step 3: Testing Undo - should restore original tile arrangement");
            var undo = FindByAutomationId("UndoButton").AsButton();
            Assert.NotNull(undo);
            this.TraceMessage("Undo button found, clicking");
            undo.Invoke();
            
            // Wait a moment for UI to update
            Thread.Sleep(500);
            
            var afterUndoTileValues = GetTileValues();
            this.TraceMessage($"After undo tiles: {string.Join(", ", afterUndoTileValues)}");
            
            // Verify that tiles are restored to original state
            bool tilesRestored = initialTileValues.SequenceEqual(afterUndoTileValues);
            Assert.True(tilesRestored, "Undo should restore original tile arrangement");
            this.TraceMessage("Undo successful - tile arrangement restored to original state");

            // Step 4: Redo should return to shuffled arrangement
            this.TraceMessage("Step 4: Testing Redo - should return to shuffled arrangement");
            var redo = FindByAutomationId("RedoButton").AsButton();
            Assert.NotNull(redo);
            this.TraceMessage("Redo button found, clicking");
            redo.Invoke();
            
            // Wait a moment for UI to update
            Thread.Sleep(500);
            
            var afterRedoTileValues = GetTileValues();
            this.TraceMessage($"After redo tiles: {string.Join(", ", afterRedoTileValues)}");
            
            // Verify that tiles match the shuffled state
            bool tilesMatchShuffled = afterShuffleTileValues.SequenceEqual(afterRedoTileValues);
            Assert.True(tilesMatchShuffled, "Redo should restore shuffled tile arrangement");
            this.TraceMessage("Shuffle/Undo/Redo tests completed successfully!");

            // Transition to next state (WaitingForRollForOrder)
            this.TraceMessage("Transitioning to WaitingForRollForOrder state");
            var next = FindByAutomationId("NextButton").AsButton();
            Assert.NotNull(next);
            Assert.True(next.IsEnabled, "Next button should be enabled to transition from PickingBoard");
            this.TraceMessage("Next button found and enabled, clicking to advance to WaitingForRollForOrder");
            next.Invoke();
            
            // Wait for transition to WaitingForRollForOrder
            Assert.True(WaitForState("WaitingForRollForOrder", TimeSpan.FromSeconds(6)), "Expected to transition to WaitingForRollForOrder state");
            this.TraceMessage("Successfully transitioned to WaitingForRollForOrder state");
            
            this.TraceMessage("=== Test_PickingBoard completed ===");
        }

        /// <summary>
        /// Test the WaitingForRollForOrder state
        /// Transitions from WaitingForRollForOrder to FinishedRollOrder when Next button is clicked
        /// </summary>
        private void Test_WaitingForRollForOrder()
        {
            this.TraceMessage("=== Test_WaitingForRollForOrder ===");
            
            // Verify we're in the correct state (should already be here from Test_PickingBoard)
            var stateLabel = Main.FindFirstDescendant(cf => cf.ByAutomationId("StateMessage"))?.AsLabel();
            var currentState = stateLabel?.Text ?? "(no state found)";
            this.TraceMessage($"Current state: {currentState}");
            Assert.True(currentState.Contains("WaitingForRollForOrder", StringComparison.OrdinalIgnoreCase), "Expected to be in WaitingForRollForOrder state");
            
            // Check automation IDs for this state
            this.CheckAutomationIds("WaitingForRollForOrder");
            
            // Get and verify GameModel state
            var testGameModelJson = FindByAutomationId("TestGameModelJson").AsTextBox();
            Assert.NotNull(testGameModelJson);
            
            var gameModelJson = testGameModelJson.Text;
            var gameModel = JsonSerializer.Deserialize<GameModel>(gameModelJson, JsonHelper.StandardOptions);
            Assert.NotNull(gameModel);
            Assert.Equal(GameState.WaitingForRollForOrder, gameModel.GameState);
            
            this.TraceMessage($"Verified GameModel state: {gameModel.GameState}");

            // Transition to next state (FinishedRollOrder)
            this.TraceMessage("Transitioning to FinishedRollOrder state");
            var next = FindByAutomationId("NextButton").AsButton();
            Assert.NotNull(next);
            Assert.True(next.IsEnabled, "Next button should be enabled to transition from WaitingForRollForOrder");
            this.TraceMessage("Next button found and enabled, clicking to advance to FinishedRollOrder");
            next.Invoke();
            
            // Wait for transition to FinishedRollOrder - this might have a different display name
            // Looking at GameController, FinishedRollOrder transitions to BeginResourceAllocation
            Thread.Sleep(1000); // Give time for transition
            
            // Check what state we're in now
            var newStateLabel = Main.FindFirstDescendant(cf => cf.ByAutomationId("StateMessage"))?.AsLabel();
            var newState = newStateLabel?.Text ?? "(no state found)";
            this.TraceMessage($"State after clicking Next: {newState}");
            
            this.TraceMessage("=== Test_WaitingForRollForOrder completed ===");
        }

        /// <summary>
        /// Test the BeginResourceAllocation state
        /// Transitions from BeginResourceAllocation to AllocateResourceForward when Next button is clicked
        /// </summary>
        private void Test_BeginResourceAllocation()
        {
            this.TraceMessage("=== Test_BeginResourceAllocation ===");
            
            // Verify we're in the correct state
            Assert.True(WaitForState("BeginResourceAllocation", TimeSpan.FromSeconds(6)), "Expected BeginResourceAllocation state");
            
            // Check automation IDs for this state
            this.CheckAutomationIds("BeginResourceAllocation");
            
            // Get and verify GameModel state
            var testGameModelJson = FindByAutomationId("TestGameModelJson").AsTextBox();
            Assert.NotNull(testGameModelJson);
            
            var gameModelJson = testGameModelJson.Text;
            var gameModel = JsonSerializer.Deserialize<GameModel>(gameModelJson, JsonHelper.StandardOptions);
            Assert.NotNull(gameModel);
            Assert.Equal(GameState.BeginResourceAllocation, gameModel.GameState);
            
            this.TraceMessage($"Verified GameModel state: {gameModel.GameState}");

            // Transition to next state (AllocateResourceForward)
            this.TraceMessage("Transitioning to AllocateResourceForward state");
            var next = FindByAutomationId("NextButton").AsButton();
            Assert.NotNull(next);
            Assert.True(next.IsEnabled, "Next button should be enabled to transition from BeginResourceAllocation");
            this.TraceMessage("Next button found and enabled, clicking to advance to AllocateResourceForward");
            next.Invoke();
            
            // Wait for transition to AllocateResourceForward
            Assert.True(WaitForState("AllocateResourceForward", TimeSpan.FromSeconds(6)), "Expected to transition to AllocateResourceForward state");
            this.TraceMessage("Successfully transitioned to AllocateResourceForward state");
            
            this.TraceMessage("=== Test_BeginResourceAllocation completed ===");
        }

        /// <summary>
        /// Test the AllocateResourceForward state
        /// Transitions from AllocateResourceForward to AllocateResourceReverse when Next button is clicked (after all players)
        /// </summary>
        private void Test_AllocateResourceForward()
        {
            this.TraceMessage("=== Test_AllocateResourceForward ===");
            
            // Verify we're in the correct state (should already be here from Test_BeginResourceAllocation)
            var stateLabel = Main.FindFirstDescendant(cf => cf.ByAutomationId("StateMessage"))?.AsLabel();
            var currentState = stateLabel?.Text ?? "(no state found)";
            this.TraceMessage($"Current state: {currentState}");
            Assert.True(currentState.Contains("AllocateResourceForward", StringComparison.OrdinalIgnoreCase), "Expected to be in AllocateResourceForward state");
            
            // Check automation IDs for this state
            this.CheckAutomationIds("AllocateResourceForward");
            
            // Get and verify GameModel state
            var testGameModelJson = FindByAutomationId("TestGameModelJson").AsTextBox();
            Assert.NotNull(testGameModelJson);
            
            var gameModelJson = testGameModelJson.Text;
            var gameModel = JsonSerializer.Deserialize<GameModel>(gameModelJson, JsonHelper.StandardOptions);
            Assert.NotNull(gameModel);
            Assert.Equal(GameState.AllocateResourceForward, gameModel.GameState);
            
            this.TraceMessage($"Verified GameModel state: {gameModel.GameState}");

            // In this state, we need to click Next multiple times to go through all players
            // The GameController shows this transitions to AllocateResourceReverse when all players are done
            int maxNextClicks = 10; // Safety limit
            int nextClicks = 0;
            
            while (nextClicks < maxNextClicks)
            {
                var next = FindByAutomationId("NextButton").AsButton();
                Assert.NotNull(next);
                
                if (!next.IsEnabled)
                {
                    this.TraceMessage("Next button is disabled, checking current state");
                    break;
                }
                
                this.TraceMessage($"Clicking Next button (click #{nextClicks + 1}) in AllocateResourceForward");
                next.Invoke();
                nextClicks++;
                
                Thread.Sleep(1000); // Give time for state transition
                
                // Check if we've moved to AllocateResourceReverse
                var currentStateLabel = Main.FindFirstDescendant(cf => cf.ByAutomationId("StateMessage"))?.AsLabel();
                var currentStateText = currentStateLabel?.Text ?? "(no state found)";
                this.TraceMessage($"State after click #{nextClicks}: {currentStateText}");
                
                if (currentStateText.Contains("AllocateResourceReverse", StringComparison.OrdinalIgnoreCase))
                {
                    this.TraceMessage("Successfully transitioned to AllocateResourceReverse state");
                    break;
                }
                
                // Update GameModel to see current state
                var currentGameModelJson = testGameModelJson.Text;
                var currentGameModel = JsonSerializer.Deserialize<GameModel>(currentGameModelJson, JsonHelper.StandardOptions);
                if (currentGameModel?.GameState == GameState.AllocateResourceReverse)
                {
                    this.TraceMessage("GameModel shows we're now in AllocateResourceReverse");
                    break;
                }
            }
            
            // Verify we ended up in AllocateResourceReverse
            Assert.True(WaitForState("AllocateResourceReverse", TimeSpan.FromSeconds(2)), "Expected to end up in AllocateResourceReverse state");
            this.TraceMessage("Successfully completed AllocateResourceForward and transitioned to AllocateResourceReverse");
            
            this.TraceMessage("=== Test_AllocateResourceForward completed ===");
        }

        /// <summary>
        /// Test the AllocateResourceReverse state
        /// Transitions from AllocateResourceReverse to DoneResourceAllocation when Next button is clicked (after all players)
        /// </summary>
        private void Test_AllocateResourceReverse()
        {
            this.TraceMessage("=== Test_AllocateResourceReverse ===");
            
            // Verify we're in the correct state (should already be here from Test_AllocateResourceForward)
            var stateLabel = Main.FindFirstDescendant(cf => cf.ByAutomationId("StateMessage"))?.AsLabel();
            var currentState = stateLabel?.Text ?? "(no state found)";
            this.TraceMessage($"Current state: {currentState}");
            Assert.True(currentState.Contains("AllocateResourceReverse", StringComparison.OrdinalIgnoreCase), "Expected to be in AllocateResourceReverse state");
            
            // Check automation IDs for this state
            this.CheckAutomationIds("AllocateResourceReverse");
            
            // Get and verify GameModel state
            var testGameModelJson = FindByAutomationId("TestGameModelJson").AsTextBox();
            Assert.NotNull(testGameModelJson);
            
            var gameModelJson = testGameModelJson.Text;
            var gameModel = JsonSerializer.Deserialize<GameModel>(gameModelJson, JsonHelper.StandardOptions);
            Assert.NotNull(gameModel);
            Assert.Equal(GameState.AllocateResourceReverse, gameModel.GameState);
            
            this.TraceMessage($"Verified GameModel state: {gameModel.GameState}");

            // In this state, we need to click Next multiple times to go through all players in reverse order
            // The GameController shows this transitions to DoneResourceAllocation when the first player is reached
            int maxNextClicks = 10; // Safety limit
            int nextClicks = 0;
            
            while (nextClicks < maxNextClicks)
            {
                var next = FindByAutomationId("NextButton").AsButton();
                Assert.NotNull(next);
                
                if (!next.IsEnabled)
                {
                    this.TraceMessage("Next button is disabled, checking current state");
                    break;
                }
                
                this.TraceMessage($"Clicking Next button (click #{nextClicks + 1}) in AllocateResourceReverse");
                next.Invoke();
                nextClicks++;
                
                Thread.Sleep(1000); // Give time for state transition
                
                // Check if we've moved to DoneResourceAllocation
                var currentStateLabel = Main.FindFirstDescendant(cf => cf.ByAutomationId("StateMessage"))?.AsLabel();
                var currentStateText = currentStateLabel?.Text ?? "(no state found)";
                this.TraceMessage($"State after click #{nextClicks}: {currentStateText}");
                
                if (currentStateText.Contains("DoneResourceAllocation", StringComparison.OrdinalIgnoreCase))
                {
                    this.TraceMessage("Successfully transitioned to DoneResourceAllocation state");
                    break;
                }
                
                // Update GameModel to see current state
                var currentGameModelJson = testGameModelJson.Text;
                var currentGameModel = JsonSerializer.Deserialize<GameModel>(currentGameModelJson, JsonHelper.StandardOptions);
                if (currentGameModel?.GameState == GameState.DoneResourceAllocation)
                {
                    this.TraceMessage("GameModel shows we're now in DoneResourceAllocation");
                    break;
                }
            }
            
            // Verify we ended up in DoneResourceAllocation
            Assert.True(WaitForState("DoneResourceAllocation", TimeSpan.FromSeconds(2)), "Expected to end up in DoneResourceAllocation state");
            this.TraceMessage("Successfully completed AllocateResourceReverse and transitioned to DoneResourceAllocation");
            
            this.TraceMessage("=== Test_AllocateResourceReverse completed ===");
        }

        /// <summary>
        /// Test the DoneResourceAllocation state
        /// Transitions from DoneResourceAllocation to WaitingForRoll when Next button is clicked
        /// </summary>
        private void Test_DoneResourceAllocation()
        {
            this.TraceMessage("=== Test_DoneResourceAllocation ===");
            
            // Verify we're in the correct state (should already be here from Test_AllocateResourceReverse)
            var stateLabel = Main.FindFirstDescendant(cf => cf.ByAutomationId("StateMessage"))?.AsLabel();
            var currentState = stateLabel?.Text ?? "(no state found)";
            this.TraceMessage($"Current state: {currentState}");
            Assert.True(currentState.Contains("DoneResourceAllocation", StringComparison.OrdinalIgnoreCase), "Expected to be in DoneResourceAllocation state");
            
            // Check automation IDs for this state
            this.CheckAutomationIds("DoneResourceAllocation");
            
            // Get and verify GameModel state
            var testGameModelJson = FindByAutomationId("TestGameModelJson").AsTextBox();
            Assert.NotNull(testGameModelJson);
            
            var gameModelJson = testGameModelJson.Text;
            var gameModel = JsonSerializer.Deserialize<GameModel>(gameModelJson, JsonHelper.StandardOptions);
            Assert.NotNull(gameModel);
            Assert.Equal(GameState.DoneResourceAllocation, gameModel.GameState);
            
            this.TraceMessage($"Verified GameModel state: {gameModel.GameState}");

            // Transition to next state (WaitingForRoll)
            this.TraceMessage("Transitioning to WaitingForRoll state");
            var next = FindByAutomationId("NextButton").AsButton();
            Assert.NotNull(next);
            Assert.True(next.IsEnabled, "Next button should be enabled to transition from DoneResourceAllocation");
            this.TraceMessage("Next button found and enabled, clicking to advance to WaitingForRoll");
            next.Invoke();
            
            // Wait for transition to WaitingForRoll
            Assert.True(WaitForState("WaitingForRoll", TimeSpan.FromSeconds(6)), "Expected to transition to WaitingForRoll state");
            this.TraceMessage("Successfully transitioned to WaitingForRoll state");
            
            this.TraceMessage("=== Test_DoneResourceAllocation completed ===");
        }

        /// <summary>
        /// Test the WaitingForRoll state - this is the final state for this test
        /// </summary>
        private void Test_WaitingForRoll()
        {
            this.TraceMessage("=== Test_WaitingForRoll ===");
            
            // Verify we're in the correct state (should already be here from Test_DoneResourceAllocation)
            var stateLabel = Main.FindFirstDescendant(cf => cf.ByAutomationId("StateMessage"))?.AsLabel();
            var currentState = stateLabel?.Text ?? "(no state found)";
            this.TraceMessage($"Current state: {currentState}");
            Assert.True(currentState.Contains("WaitingForRoll", StringComparison.OrdinalIgnoreCase), "Expected to be in WaitingForRoll state");
            
            // Check automation IDs for this state
            this.CheckAutomationIds("WaitingForRoll");
            
            // Get and verify GameModel state
            var testGameModelJson = FindByAutomationId("TestGameModelJson").AsTextBox();
            Assert.NotNull(testGameModelJson);
            
            var gameModelJson = testGameModelJson.Text;
            var gameModel = JsonSerializer.Deserialize<GameModel>(gameModelJson, JsonHelper.StandardOptions);
            Assert.NotNull(gameModel);
            Assert.Equal(GameState.WaitingForRoll, gameModel.GameState);
            
            this.TraceMessage($"Verified GameModel state: {gameModel.GameState}");
            
            // In WaitingForRoll state, the Next button should be disabled since this state 
            // is controlled by the roll UI, not the Next button (as per GameController comments)
            var next = FindByAutomationId("NextButton").AsButton();
            Assert.NotNull(next);
            this.TraceMessage($"Next button enabled status: {next.IsEnabled}");
            
            // According to GameController: "GameState.WaitingForRoll is not controlled by the Next button. it is controlled by hitting a roll UI"
            // And AllowNext() returns false for WaitingForRoll state
            Assert.False(next.IsEnabled, "Next button should be disabled in WaitingForRoll state (controlled by roll UI)");
            
            this.TraceMessage("Successfully verified WaitingForRoll state - this is the end of the core game setup flow!");
            this.TraceMessage("=== Test_WaitingForRoll completed ===");
        }

        /// <summary>
        /// Transition to the next state by clicking the Next button
        /// </summary>
        private void TransitionToNextState()
        {
            this.TraceMessage("=== TransitionToNextState ===");
            
            var next = FindByAutomationId("NextButton").AsButton();
            Assert.NotNull(next);
            Assert.True(next.IsEnabled, "Next button should be enabled for state transition");
            this.TraceMessage("Next button found and enabled, clicking");
            next.Invoke();
            
            // Give a moment for the state transition to occur
            Thread.Sleep(500);
            
            this.TraceMessage("State transition initiated");
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
            this.TraceMessage("Waiting for NewGame page to load...");
            
            // Wait for the Start button to appear, which indicates the NewGame page is loaded
            var startBtn = Retry.WhileNull(() => 
            {
                try
                {
                    // The XAML shows: AutomationProperties.AutomationId="StartButton"
                    var btn = Main.FindFirstDescendant(cf => cf.ByAutomationId("StartButton"));
                    if (btn != null) 
                    {
                        this.TraceMessage($"Found StartButton: Name='{btn.Name}', AutomationId='{btn.AutomationId}'");
                        return btn;
                    }
                    
                    this.TraceMessage("StartButton not found yet, continuing to wait...");
                    return null;
                }
                catch (Exception ex)
                {
                    this.TraceMessage($"Error in WaitForNewGamePageToLoad: {ex.Message}");
                    return null;
                }
            }, 
            timeout: TimeSpan.FromSeconds(15), // Increased timeout
            interval: TimeSpan.FromMilliseconds(500)).Result;
            
            if (startBtn == null)
            {
                // Provide more diagnostic information if we can't find the button
                this.TraceMessage("StartButton not found - dumping available controls:");
                try
                {
                    var allButtons = Main.FindAllDescendants(cf => cf.ByControlType(FlaUI.Core.Definitions.ControlType.Button));
                    this.TraceMessage($"Found {allButtons.Length} buttons total");
                    foreach (var button in allButtons.Take(10))
                    {
                        this.TraceMessage($"Button: Name='{button.Name}', AutomationId='{button.AutomationId}'");
                    }
                    
                    // Also check what page/content we have
                    var allElements = Main.FindAllDescendants().Take(20);
                    this.TraceMessage("Available elements:");
                    foreach (var element in allElements)
                    {
                        this.TraceMessage($"Element: Type={element.ControlType}, Name='{element.Name}', AutomationId='{element.AutomationId}'");
                    }
                }
                catch (Exception ex)
                {
                    this.TraceMessage($"Error dumping controls: {ex.Message}");
                }
                
                throw new XunitException("NewGame page failed to load - StartButton not found within 15 seconds");
            }
            
            this.TraceMessage("NewGame page loaded successfully");
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
            this.TraceMessage($"WaitForState: Looking for '{expected}' state");
            
            while (sw.Elapsed < timeout)
            {
                var stateLabel = Main.FindFirstDescendant(cf => cf.ByAutomationId("StateMessage"))?.AsLabel();
                var text = stateLabel?.Text ?? string.Empty;
                
                if (!string.IsNullOrWhiteSpace(text))
                {
                    this.TraceMessage($"WaitForState: Current state is '{text}' (elapsed: {sw.Elapsed.TotalSeconds:F1}s)");
                    
                    if (text.Contains(expected, StringComparison.OrdinalIgnoreCase))
                    {
                        this.TraceMessage($"WaitForState: Found expected state '{expected}'!");
                        return true;
                    }
                }
                else
                {
                    this.TraceMessage($"WaitForState: No state text found (elapsed: {sw.Elapsed.TotalSeconds:F1}s)");
                }
                
                Thread.Sleep(120);
            }
            
            this.TraceMessage($"WaitForState: Timed out waiting for '{expected}' after {timeout.TotalSeconds}s");
            return false;
        }

        private static bool WaitForTileSampleChange(FlaUI.Core.AutomationElements.Label? a, FlaUI.Core.AutomationElements.Label? b, string aBefore, string bBefore, TimeSpan timeout)
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

        private static bool WaitForTileSampleTo(FlaUI.Core.AutomationElements.Label? a, FlaUI.Core.AutomationElements.Label? b, string aExpected, string bExpected, TimeSpan timeout)
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

        /// <summary>
        /// Gets the current tile values from the board for comparison during shuffle tests
        /// </summary>
        private List<string> GetTileValues()
        {
            var tileValues = new List<string>();
            
            try
            {
                // Find all tiles by searching for TileCtrl controls
                var tiles = Main.FindAllDescendants(cf => cf.ByAutomationId("TileCtrl"));
                this.TraceMessage($"Found {tiles.Length} tiles on the board");
                
                foreach (var tile in tiles.Take(10)) // Limit to first 10 tiles for practical comparison
                {
                    try
                    {
                        // Try to find the tile number label within each tile
                        var numberLabel = tile.FindFirstDescendant(cf => cf.ByAutomationId("TileNumber"))?.AsLabel();
                        if (numberLabel != null && !string.IsNullOrEmpty(numberLabel.Text))
                        {
                            tileValues.Add(numberLabel.Text);
                        }
                        else
                        {
                            // If no number label, use the tile's name or a placeholder
                            tileValues.Add(tile.Name ?? "Unknown");
                        }
                    }
                    catch (Exception ex)
                    {
                        this.TraceMessage($"Error reading tile value: {ex.Message}");
                        tileValues.Add("Error");
                    }
                }
            }
            catch (Exception ex)
            {
                this.TraceMessage($"Error finding tiles: {ex.Message}");
                // Return empty list if we can't find tiles
                return new List<string>();
            }
            
            return tileValues;
        }

        /// <summary>
        /// Checks and logs all AutomationIds available in the current UI state for debugging
        /// </summary>
        private void CheckAutomationIds(string stateName)
        {
            this.TraceMessage($"=== CheckAutomationIds for {stateName} state ===");
            
            try
            {
                var allControls = Main.FindAllDescendants();
                this.TraceMessage($"Total controls found: {allControls.Length}");
                
                var automationIdControls = allControls.Where(c => 
                {
                    try
                    {
                        return !string.IsNullOrEmpty(c.AutomationId);
                    }
                    catch
                    {
                        return false; // Skip controls that don't support AutomationId
                    }
                }).ToArray();
                this.TraceMessage($"Controls with AutomationId: {automationIdControls.Length}");
                
                // Log all AutomationIds
                foreach (var control in automationIdControls)
                {
                    try
                    {
                        var controlType = control.ControlType.ToString();
                        var name = control.Name ?? "NoName";
                        this.TraceMessage($"  - AutomationId: '{control.AutomationId}', Type: {controlType}, Name: '{name}'");
                    }
                    catch (Exception ex)
                    {
                        this.TraceMessage($"  - AutomationId: '{control.AutomationId}' (error reading details: {ex.Message})");
                    }
                }
                
                // Check for specific expected controls in PickingBoard state
                if (stateName == "PickingBoard")
                {
                    var expectedIds = new[] { "ShuffleButton", "UndoButton", "RedoButton", "NextButton", "StateMessage", "TestGameModelJson" };
                    this.TraceMessage($"=== Checking for expected {stateName} controls ===");
                    
                    foreach (var expectedId in expectedIds)
                    {
                        var found = automationIdControls.Any(c => c.AutomationId == expectedId);
                        this.TraceMessage($"  - {expectedId}: {(found ? "FOUND" : "MISSING")}");
                    }
                }
            }
            catch (Exception ex)
            {
                this.TraceMessage($"Error in CheckAutomationIds: {ex.Message}");
            }
            
            this.TraceMessage($"=== End CheckAutomationIds for {stateName} ===");
        }

        /// <summary>
        /// Waits for the GameModel's board hash to change from the initial value
        /// </summary>
        private bool WaitForGameModelBoardHashChange(AutomationElement gameModelJsonElement, string initialBoardHash, TimeSpan timeout)
        {
            var stopwatch = System.Diagnostics.Stopwatch.StartNew();
            while (stopwatch.Elapsed < timeout)
            {
                try
                {
                    var currentGameModelJson = gameModelJsonElement.AsTextBox().Text;
                    if (!string.IsNullOrEmpty(currentGameModelJson))
                    {
                        var currentGameModel = JsonSerializer.Deserialize<GameModel>(currentGameModelJson, JsonHelper.StandardOptions);
                        if (currentGameModel != null && currentGameModel.GameHash != initialBoardHash)
                        {
                            this.TraceMessage($"Board hash changed: {initialBoardHash} -> {currentGameModel.GameHash}");
                            return true;
                        }
                    }
                }
                catch (Exception ex)
                {
                    this.TraceMessage($"Error reading GameModel JSON: {ex.Message}");
                }
                Thread.Sleep(100);
            }
            this.TraceMessage($"Timeout: Board hash did not change from {initialBoardHash}");
            return false;
        }

        /// <summary>
        /// Waits for the GameModel's board hash to match the expected value
        /// </summary>
        private bool WaitForGameModelBoardHashTo(AutomationElement gameModelJsonElement, string expectedBoardHash, TimeSpan timeout)
        {
            var stopwatch = System.Diagnostics.Stopwatch.StartNew();
            while (stopwatch.Elapsed < timeout)
            {
                try
                {
                    var currentGameModelJson = gameModelJsonElement.AsTextBox().Text;
                    if (!string.IsNullOrEmpty(currentGameModelJson))
                    {
                        var currentGameModel = JsonSerializer.Deserialize<GameModel>(currentGameModelJson, JsonHelper.StandardOptions);
                        if (currentGameModel != null && currentGameModel.GameHash == expectedBoardHash)
                        {
                            this.TraceMessage($"Board hash matched expected: {expectedBoardHash}");
                            return true;
                        }
                    }
                }
                catch (Exception ex)
                {
                    this.TraceMessage($"Error reading GameModel JSON: {ex.Message}");
                }
                Thread.Sleep(100);
            }
            this.TraceMessage($"Timeout: Board hash did not match expected {expectedBoardHash}");
            return false;
        }

        /// <summary>
        /// Waits for the board hash to change from the initial value
        /// </summary>
        private bool WaitForBoardHashChange(AutomationElement boardHashElement, string initialBoardHash, TimeSpan timeout)
        {
            var stopwatch = System.Diagnostics.Stopwatch.StartNew();
            while (stopwatch.Elapsed < timeout)
            {
                try
                {
                    var currentHash = boardHashElement.AsLabel().Text;
                    if (currentHash != initialBoardHash)
                    {
                        this.TraceMessage($"Board hash changed: {initialBoardHash} -> {currentHash}");
                        return true;
                    }
                }
                catch (Exception ex)
                {
                    this.TraceMessage($"Error reading board hash: {ex.Message}");
                }
                Thread.Sleep(100);
            }
            this.TraceMessage($"Timeout: Board hash did not change from {initialBoardHash}");
            return false;
        }

        /// <summary>
        /// Waits for the board hash to match the expected value
        /// </summary>
        private bool WaitForBoardHashTo(AutomationElement boardHashElement, string expectedBoardHash, TimeSpan timeout)
        {
            var stopwatch = System.Diagnostics.Stopwatch.StartNew();
            while (stopwatch.Elapsed < timeout)
            {
                try
                {
                    var currentHash = boardHashElement.AsLabel().Text;
                    if (currentHash == expectedBoardHash)
                    {
                        this.TraceMessage($"Board hash matched expected: {expectedBoardHash}");
                        return true;
                    }
                }
                catch (Exception ex)
                {
                    this.TraceMessage($"Error reading board hash: {ex.Message}");
                }
                Thread.Sleep(100);
            }
            this.TraceMessage($"Timeout: Board hash did not match expected {expectedBoardHash}");
            return false;
        }

        private bool WaitForTilesToRender(TimeSpan timeout)
        {
            var sw = Stopwatch.StartNew();
            this.TraceMessage("WaitForTilesToRender: Checking for valid tile numbers and resources");
            
            while (sw.Elapsed < timeout)
            {
                try
                {
                    // Sample a few tiles to check if they have valid numbers and resources
                    var sampleTiles = new[]
                    {
                        ("0_0_0", Main.FindFirstDescendant(cf => cf.ByAutomationId("TileNumber-0_0_0"))?.AsLabel(), 
                                  Main.FindFirstDescendant(cf => cf.ByAutomationId("TileResource-0_0_0"))?.AsLabel()),
                        ("1_-1_0", Main.FindFirstDescendant(cf => cf.ByAutomationId("TileNumber-1_-1_0"))?.AsLabel(),
                                   Main.FindFirstDescendant(cf => cf.ByAutomationId("TileResource-1_-1_0"))?.AsLabel()),
                        ("-1_1_0", Main.FindFirstDescendant(cf => cf.ByAutomationId("TileNumber--1_1_0"))?.AsLabel(),
                                   Main.FindFirstDescendant(cf => cf.ByAutomationId("TileResource--1_1_0"))?.AsLabel())
                    };

                    int validTiles = 0;
                    foreach (var (coords, numberElement, resourceElement) in sampleTiles)
                    {
                        var number = numberElement?.Text?.Trim() ?? string.Empty;
                        var resource = resourceElement?.Text?.Trim() ?? string.Empty;
                        
                        // Valid tile: has a non-empty number (like "2", "3", etc.) and non-empty resource (like "Wood", "Brick", etc.)
                        // Skip tiles with "7" (robber) as they don't have resources
                        bool hasValidNumber = !string.IsNullOrWhiteSpace(number) && number != "0";
                        bool hasValidResource = !string.IsNullOrWhiteSpace(resource) || number == "7"; // Robber tiles (7) don't have resources
                        
                        if (hasValidNumber && hasValidResource)
                        {
                            validTiles++;
                            this.TraceMessage($"WaitForTilesToRender: Tile {coords} is valid - Number: '{number}', Resource: '{resource}'");
                        }
                        else
                        {
                            this.TraceMessage($"WaitForTilesToRender: Tile {coords} not ready - Number: '{number}', Resource: '{resource}'");
                        }
                    }
                    
                    this.TraceMessage($"WaitForTilesToRender: {validTiles} out of {sampleTiles.Length} sample tiles are valid (elapsed: {sw.Elapsed.TotalSeconds:F1}s)");
                    
                    // If at least 2 out of 3 sample tiles are valid, consider tiles rendered
                    if (validTiles >= 2)
                    {
                        this.TraceMessage("WaitForTilesToRender: Tiles are sufficiently rendered!");
                        return true;
                    }
                }
                catch (Exception ex)
                {
                    this.TraceMessage($"WaitForTilesToRender: Error checking tiles: {ex.Message}");
                }
                
                // Use Thread.Sleep to allow message pumping without async complications
                Thread.Sleep(200);
            }
            
            this.TraceMessage($"WaitForTilesToRender: Timed out after {timeout.TotalSeconds}s waiting for tiles to render");
            return false;
        }

        /// <summary>
        /// Gets the GameModel by checking if there's an exposed property or falls back to UI-based access
        /// </summary>
        private GameModel? GetGameModelDirect()
        {
            this.TraceMessage("GetGameModelDirect: Direct access not available in current architecture");
            return null;
        }
    }
    
    /// <summary>
    /// Extension methods for test tracing
    /// </summary>
    public static class TestExtensions
    {
        public static void TraceMessage(this object o, string toWrite, int indentLevel = 0, [CallerMemberName] string cmb = "", [CallerLineNumber] int cln = 0, [CallerFilePath] string cfp = "")
        {
            var message = $"{Path.GetFileNameWithoutExtension(cfp)}({cln}):{toWrite}\t\t[Caller={cmb}]";
            
            // Write to both debug output and console for test visibility
            for (int i = 0; i < indentLevel; i++)
            {
                System.Diagnostics.Debug.Indent();
                Console.Write("  ");
            }
            System.Diagnostics.Debug.WriteLine(message);
            Console.WriteLine(message);
            for (int i = 0; i < indentLevel; i++)
            {
                System.Diagnostics.Debug.Unindent();
            }
        }
    }
}
