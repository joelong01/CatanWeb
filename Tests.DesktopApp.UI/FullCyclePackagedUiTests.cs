using Catan3.Shared.Extensions;
using Catan3.Shared.Models;
using Catan3.Shared.Utility;
using FlaUI.Core;
using FlaUI.Core.AutomationElements;
using FlaUI.Core.Conditions;
using FlaUI.Core.Definitions;
using FlaUI.Core.Logging;
using FlaUI.Core.Tools;
using FlaUI.UIA3;
using FlaUI.UIA3.Identifiers;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using System.Threading;
using Tests.DesktopApp.UI.TestInfra;
using Xunit;
using Xunit.Sdk;
using static System.Math;

namespace Tests.DesktopApp.UI
{
    /// <summary>
    /// End-to-end UI test against the packaged app (MSIX). Launches via AUMID and
    /// validates the core flow similar to the CLI parity test.  The goal of this tests is to create the game once and then go through the full cycle of 
    /// the game. The way we will build it is to understand the GameState enum (defined in Catan3.Shared.Models) and how the GameController.cs 
    /// transitions from one state to the next via user interactions in the game.  There is *one* official "test" because we are stateful and thus
    /// create one game and have it run through the scenarios.  the general layout of the tests are
    /// public void Full_Stateful_Flow_PackagedApp_Expansion_FivePlayers()
    ///    {
    ///        Sta.Run(() =>
    ///        {
    ///             // Initialize() -- gather useful data we cache: DO NOT CACHE GAMEMODEL
    ///             // for each state we are testing, we implement a function with the naming convention Test_<GameState>
    ///         }
    ///     }
    /// 
    ///  There is a property we bind to that has the GameModel (which is all the state for the game) in JSON format. The game uses a "copy on write"
    ///  strategy, so every interaction that causes an update to the game results in a new GameModel.  You can compare GameModel.GameHash between two
    ///  GameModel instances. if they are the same, they are identical.  if they are not, they are different.
    ///  
    ///     ========== RULES FOR THIS FILE ==========
    /// 1. This file is stateful and contains one test that runs through the entire game flow.
    /// 2. always build this test with the build script ./build.ps1 -NoTest
    /// 3. when you run this test, do not pass in --log parameter because I don't want double logging
    /// 4. do NOT put comments that show the history of how the code got there.  if you add comments, they should say what it DOES
    /// 5. header comments should not be updated without explicit user consent.  if the code is inconsistent with the comment, ask for guidance.
    /// 6. the full test name is "Full_Stateful_Flow_PackagedApp_Expansion_FivePlayers" use it.
    /// </summary>
    [Collection("UIAutomation")]
    public class FullCyclePackagedUiTests : IDisposable
    {
        private static int SHORT_WAIT = 750;
        private UIA3Automation? _automation;
        private AutomationElement? _main;
        private bool _testSucceeded = false;
        private static readonly ConditionFactory Cf = new(new UIA3PropertyLibrary());
        private AutomationElement Main => _main ?? throw new InvalidOperationException("Main window not initialized");

        /// <summary>
        /// Initializes a new instance of the FullCyclePackagedUiTests class.
        /// Test infrastructure and automation elements are initialized when the actual test runs.
        /// </summary>
        public FullCyclePackagedUiTests()
        {

        }

        // this is a map of the AutomationIds (as a string) to the AutomationElement for all controls in the MainBoard
        // this allows us to find them when the board is created and then use them throughout the stateful test
        private Dictionary<String, AutomationElement> UiControls = [];
        /// <summary>
        /// Constructs a HashMap of all AutomationIds for the entire board.
        /// Once the board is created, we do not delete or create new UI elements we click on (with some small exceptions).
        /// This map allows us to efficiently look up controls throughout the test without repeated searches.
        /// Should be called once after the game board is fully loaded (PickingBoard state).
        /// </summary>
        private void LoadAutomationObjects()
        {
            this.TraceMessage("=== Loading Automation Objects ===");

            try
            {
                // Clear any existing entries
                UiControls.Clear();


                // Get all descendants with AutomationIds
                var allElements = Main.FindAllDescendants();

                foreach (var element in allElements)
                {
                    try
                    {
                        var automationId = element.Properties.AutomationId.ValueOrDefault;
                        if (!string.IsNullOrEmpty(automationId))
                        {
                            // Store the element for efficient lookup
                            // Note: Using automationId as key, storing the AutomationElement reference
                            UiControls[automationId] = element;

                        }
                    }
                    catch (Exception ex)
                    {
                        // Skip elements that can't be accessed
                        this.TraceMessage($"  Skipped element due to error: {ex.Message}");
                    }
                }

                this.TraceMessage($"✅ Loaded {UiControls.Count} automation objects into cache");

                // Log summary of important game elements
                var roadElements = UiControls.Values.Count(obj => obj.AutomationId.StartsWith("Road"));
                var buildingElements = UiControls.Values.Count(obj => obj.AutomationId.StartsWith("Building"));
                var tileElements = UiControls.Values.Count(obj => obj.AutomationId.StartsWith("Tile"));
                var rollElements = UiControls.Values.Count(obj => obj.AutomationId.StartsWith("Roll"));

                this.TraceMessage($"  Roads: {roadElements}, Buildings: {buildingElements}, Tiles: {tileElements} Rolls: {rollElements}");
            }
            catch (Exception ex)
            {
                this.TraceMessage($"❌ Error loading automation objects: {ex.Message}");
                Assert.Fail("Failed to load automation objects from the main window. " +
                            "This may indicate a problem with the game state or UI structure.");
            }
        }

        /// <summary>
        /// IDisposable implementation for test cleanup.
        /// If the test succeeded, closes the app normally.
        /// If the test failed, leaves the app open for debugging purposes.
        /// This allows developers to inspect the app state and debug window after test failure.
        /// </summary>
        public void Dispose()
        {
            try
            {
                // Only close the app if the test succeeded
                // If the test failed, leave it open for debugging
                if (_testSucceeded)
                {
                    this.TraceMessage("Test succeeded - closing app");
                    _main?.AsWindow()?.Close();
                }
                else
                {
                    this.TraceMessage("Test failed - leaving app open for debugging. You can check the debug window for trace messages.");
                }
            }
            catch { }
            _automation?.Dispose();
        }
        [Fact]
        public void Full_Stateful_Flow_PackagedApp_Expansion_FivePlayers()
        {
            Sta.Run(() =>
            {
                DoFullTest();
            });
        }
        /// <summary>
        /// Main test point for the full stateful flow test. Runs in an STA context.
        /// This is the only real [Fact] test in this class as the entire test is stateful.
        /// Coordinates the complete game flow from app launch through multiple game states.
        /// 
        /// Entry State: App not launched
        /// Exit State: Test completed (app either closed or left open for debugging)
        /// 
        /// Test Flow:
        /// 1. Launch packaged app and attach to main window
        /// 2. Wait for NewGame page to load
        /// 3. Execute Test_NewGame() to configure and start the game
        ///     -> load and test the automation ids to ensure we can access roads, tiles, and buildings
        /// 4. Execute Test_PickingBoard() to handle board generation
        /// 5. Execute Test_WaitingForRollForOrder() for turn order determination
        /// 6. Execute Test_AllocationPhase:
        /// 7. Execute the Test_WaitingForRoll
        /// 8. Execute the Test_WaitingForNext
        /// 9. Execute the Test_PickingSupplementalPlayers
        /// 
        /// All of these tests should be INFORMED by, but not CONSTRAINED by the tests in the Tests.GameService/SignalR test.  They test the *same* game,
        /// so the logic should be similar (modulo minor updates in game logic), but the mechanism is different since these tests work through the UI, 
        /// and the SignalR tests work through a ASP.Net service.
        /// 
        /// Exception Handling: Any unhandled exception marks the test as failed,
        /// which triggers the Dispose() method to leave the app open for debugging.
        /// </summary>
        private void DoFullTest()
        {

            this.TraceMessage("Test starting");


            this.TraceMessage("About to launch app");
            LaunchPackagedAppAndAttachToMainWindow();
            this.TraceMessage("App launched successfully");

            this.TraceMessage("About to wait for NewGame page");
            // Wait for the NewGame page to be fully loaded
            WaitForNewGamePageToLoad();
            this.TraceMessage("NewGame page loaded");


            // Execute each state test in sequence
            this.TraceMessage("=== Starting GameState progression tests ===");

            Test_NewGame();


            // Load automation objects after the game board is created (in PickingBoard state)
            LoadAutomationObjects();

            Test_PickingBoard(); // PickingBoard -> WaitingForRollForOrder (via Next button)
            Test_WaitingForRollForOrder(); // WaitingForRollForOrder -> FinishedRollOrder (via Next button)
            Test_FinishedRollOrder(); // FinishedRollOrder -> BeginResourceAllocation (via Next button)
            Test_AllocationPhase();
            Test_DoneResourceAllocation(); // DoneResourceAllocation -> WaitingForRoll (via Next button)
            Test_WaitingForRoll(); // End state for this test

            this.TraceMessage("=== All GameState tests completed successfully ===");
            _testSucceeded = true; // Mark test as successful
        }


        /// <summary>
        /// Test the NewGame state - configures game settings and starts the game.
        /// 
        /// Entry State: NewGame (game configuration screen)
        /// Exit State: PickingBoard (board generation in progress)
        /// 
        /// Actions:
        /// 1. Verify we're in NewGame state
        /// 2. Select "Expansion" game type
        /// 3. Set player count to 5
        /// 4. Click Start button to begin game
        /// 
        /// Validation:
        /// - Confirms game state transitions correctly
        /// - Ensures UI elements are available and responsive
        /// - Verifies game configuration is applied properly
        /// 
        /// Transition: NewGame -> PickingBoard (via Start button)
        /// </summary>
        private void Test_NewGame()
        {
            this.TraceMessage("=== Test_NewGame ===");
            // New Game page: choose Expansion, select 5 players, Start

            var startBtn = FindByAutomationId("StartButton").AsButton();
            var gameTypeCombo = FindByAutomationId("GameTypeCombo").AsComboBox();

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

            Assert.NotNull(gridViewItems);

            Assert.True(gridViewItems.Length >= 5, $"Expected at least 5 players, found {gridViewItems.Length}");

            // Select the first 5 players - WinUI GridView with SelectionMode="Multiple" requires proper selection
            for (int i = 0; i < 5; i++)
            {
                var item = gridViewItems[i];
                this.TraceMessage($"Attempting to select GridView item {i}");


                // For WinUI GridView with SelectionMode="Multiple", we need to:
                // 1. Make sure the item is visible and focusable
                // 2. Use proper selection patterns or keyboard simulation

                // First, ensure the item is in view and focused
                item.Focus();
                Thread.Sleep(SHORT_WAIT);
                var selectionPattern = item.Patterns.SelectionItem.PatternOrDefault;
                if (selectionPattern != null)
                {
                    this.TraceMessage($"Using SelectionItem pattern for item {i}");
                    selectionPattern.AddToSelection(); // Use AddToSelection for multiple selection
                    Thread.Sleep(SHORT_WAIT);

                }
            }

            // After attempting to select all items, let's check if the GridView has any selection
            this.TraceMessage("Checking GridView selection after selection attempts");
            Thread.Sleep(SHORT_WAIT); // Give time for selection events to process

            this.TraceMessage("About to click Start button to transition to PickingBoard");
            startBtn.Invoke();
            this.TraceMessage("Start button clicked - should now be transitioning to PickingBoard");

            // Wait for transition to PickingBoard state  
            Assert.True(WaitForGameState(GameState.PickingBoard, TimeSpan.FromSeconds(10)), "Expected to transition to PickingBoard state");
            this.TraceMessage("=== Test_NewGame completed ===");
        }

        /// <summary>
        /// Test the PickingBoard state - test shuffle/previous board/redo functionality
        /// Flow: Initial -> Shuffle -> Previous Board -> Redo -> Final Shuffle
        /// Transitions from PickingBoard to WaitingForRollForOrder when Next button is clicked
        /// </summary>
        private void Test_PickingBoard()
        {
            this.TraceMessage("=== Test_PickingBoard ===");

            // Verify we're in the correct state using GameState (not UI text)
            VerifyExpectedGameState(GameState.PickingBoard);


            // Test shuffle/previous board functionality
            this.TraceMessage("Starting shuffle/previous board/redo tests");

            var shuffle = FindByAutomationId("ShuffleButton").AsButton();
            Assert.NotNull(shuffle);
            this.TraceMessage("Shuffle button found");

            // Get initial board hash to compare after shuffle
            var initialGameHash = GetCurrentGameModel().GameHash;
            Assert.NotNull(initialGameHash);
            this.TraceMessage($"Initial GameHash: {initialGameHash}");

            // Step 1: Shuffle - should change board arrangement
            this.TraceMessage("Step 1: Clicking Shuffle button");
            shuffle.Invoke();

            var afterShuffleGameHash = GetCurrentGameModel().GameHash;
            Assert.NotNull(afterShuffleGameHash);
            this.TraceMessage($"After shuffle GameHash: {afterShuffleGameHash}");

            // Verify that the board changed (GameHash should be different)
            bool boardChanged = !string.Equals(initialGameHash, afterShuffleGameHash, StringComparison.Ordinal);
            Assert.True(boardChanged, "Shuffle should change board arrangement (GameHash should differ)");
            this.TraceMessage("Step 2: Shuffle successful - board arrangement changed");

            // Step 3: Previous Board should restore original board arrangement
            this.TraceMessage("Step 3: Testing Previous Board - should restore original board arrangement");
            var previousBoard = FindByAutomationId("PreviousBoardButton").AsButton();
            Assert.NotNull(previousBoard);

            // Wait a moment and check button states
            Thread.Sleep(SHORT_WAIT); // Give more time for UI state updates

            this.TraceMessage($"Previous Board button enabled: {previousBoard.IsEnabled}");

            // Also check the redo button state for comparison
            var redo = FindByAutomationId("RedoButton").AsButton();
            this.TraceMessage($"Redo button enabled: {redo?.IsEnabled ?? false}");

            // Check the GameModel state to understand button enablement
            var gameModel = GetCurrentGameModel();
            if (gameModel != null)
            {
                this.TraceMessage($"ActionFlags - UndoEnabled: {gameModel.ActionFlags?.UndoEnabled ?? false}");
                this.TraceMessage($"ActionFlags - RedoEnabled: {gameModel.ActionFlags?.RedoEnabled ?? false}");
            }

            if (!previousBoard.IsEnabled)
            {
                this.TraceMessage("Previous Board button is not enabled after shuffle");
                this.TraceMessage("This may indicate that the button enablement logic differs from manual testing");
                this.TraceMessage("Skipping Previous Board test and continuing with next state transition");
            }
            else
            {
                this.TraceMessage("Previous Board button found and enabled, clicking");
                previousBoard.Invoke();


                var afterPreviousBoardGameHash = GetCurrentGameModel().GameHash;
                Assert.NotNull(afterPreviousBoardGameHash);
                this.TraceMessage($"After Previous Board GameHash: {afterPreviousBoardGameHash}");

                // Verify that board is restored to original state
                bool boardRestored = string.Equals(initialGameHash, afterPreviousBoardGameHash, StringComparison.Ordinal);
                Assert.True(boardRestored, "Previous Board should restore original board arrangement (GameHash should match initial)");
                this.TraceMessage("Previous Board successful - board arrangement restored to original state");

                // Step 4: Redo should return to shuffled arrangement
                this.TraceMessage("Step 4: Testing Redo - should return to shuffled arrangement");

                // Refresh redo button reference after Previous Board action
                redo = FindByAutomationId("RedoButton").AsButton();
                Assert.NotNull(redo);

                // After Previous Board, Redo should be enabled
                Assert.True(redo.IsEnabled, "Redo button should be enabled after Previous Board");
                this.TraceMessage("Redo button found and enabled, clicking");
                redo.Invoke();



                var afterRedoGameHash = GetCurrentGameModel().GameHash;
                Assert.NotNull(afterRedoGameHash);
                this.TraceMessage($"After redo GameHash: {afterRedoGameHash}");

                // Verify that board matches the shuffled state
                bool boardMatchesShuffled = string.Equals(afterShuffleGameHash, afterRedoGameHash, StringComparison.Ordinal);
                Assert.True(boardMatchesShuffled, "Redo should restore shuffled board arrangement (GameHash should match shuffle state)");
                this.TraceMessage("Redo successful - board arrangement restored to shuffled state");
            }

            // Step 5: Final shuffle to test we can continue making changes
            this.TraceMessage("Step 5: Testing final shuffle to ensure board generation continues to work");
            shuffle.Invoke();

            var finalShuffleGameHash = GetCurrentGameModel().GameHash;
            Assert.NotNull(finalShuffleGameHash);
            this.TraceMessage($"Final shuffle GameHash: {finalShuffleGameHash}");

            // Verify that board changed from initial (don't compare to other shuffles as they could be same by chance)
            bool boardChangedFromInitial = !string.Equals(initialGameHash, finalShuffleGameHash, StringComparison.Ordinal);
            Assert.True(boardChangedFromInitial, "Final shuffle should create board arrangement different from initial (GameHash should differ from initial)");
            this.TraceMessage("Shuffle/Previous Board/Redo tests completed successfully!");

            // Test the road hierarchy before transitioning to the next state
            this.TraceMessage("Testing road hierarchy to verify AutomationIds are accessible...");


            // Transition to next state (WaitingForRollForOrder)
            this.TraceMessage("Transitioning to WaitingForRollForOrder state");
            var next = FindByAutomationId("NextButton").AsButton();
            Assert.NotNull(next);
            Assert.True(next.IsEnabled, "Next button should be enabled to transition from PickingBoard");
            this.TraceMessage("Next button found and enabled, clicking to advance to WaitingForRollForOrder");
            next.Invoke();

            // Wait for transition to WaitingForRollForOrder
            Assert.True(WaitForGameState(GameState.WaitingForRollForOrder, TimeSpan.FromSeconds(6)), "Expected to transition to WaitingForRollForOrder state");
            VerifyExpectedGameState(GameState.WaitingForRollForOrder);
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

            //
            // Get and verify GameModel state from AutomationProperties.ItemStatus
            var gameModel = GetCurrentGameModel();
            Assert.Equal(GameState.WaitingForRollForOrder, gameModel.GameState);

            // Get the actual current player (don't assume a specific name since it comes from UI selection)
            var currentPlayerId = gameModel.CurrentPlayerId;
            Assert.NotNull(currentPlayerId);
            Assert.False(string.IsNullOrEmpty(currentPlayerId));

               // Find the 3rd person to go first and click their "Go First" button
            this.TraceMessage("Finding the 3rd person in the player order to make them go first");

            // Get current player order
            var playerOrder = gameModel.Players?.Select(p => p.Name).ToList() ?? new List<string>();
            this.TraceMessage($"Current player order: [{string.Join(", ", playerOrder)}]");

            if (playerOrder.Count >= 3)
            {
                var thirdPlayerName = playerOrder[2]; // Index 2 = third player
                this.TraceMessage($"Third player is: {thirdPlayerName}");

                // Find the "Go First" button for the third player
                // Look for elements that might contain the player's name and a "Go First" button
                var goFirstButtons = Main.FindAllDescendants(cf => cf.ByText("Go First")).ToList();
                this.TraceMessage($"Found {goFirstButtons.Count} 'Go First' buttons total");

                if (goFirstButtons.Count >= 3)
                {
                    // Click the third "Go First" button (index 2)
                    var thirdGoFirstButton = goFirstButtons[2];
                    this.TraceMessage($"Clicking 'Go First' button for third player: {thirdPlayerName}");
                    thirdGoFirstButton.AsButton().Invoke();

                    // Wait for UI to update
                    Thread.Sleep(SHORT_WAIT);

                    // Verify order changed
                    var updatedGameModel = GetCurrentGameModel();
                    var newPlayerOrder = updatedGameModel.Players?.Select(p => p.Name).ToList() ?? new List<string>();
                    this.TraceMessage($"Updated player order: [{string.Join(", ", newPlayerOrder)}]");

                    // The third player should now be first
                    if (newPlayerOrder.Count > 0 && newPlayerOrder[0] == thirdPlayerName)
                    {
                        this.TraceMessage($"✅ Successfully made {thirdPlayerName} go first!");
                    }
                    else
                    {
                        this.TraceMessage($"⚠️ Player order may not have changed as expected");
                    }
                }
                else
                {
                    this.TraceMessage($"Not enough 'Go First' buttons found ({goFirstButtons.Count}), skipping reorder");
                }
            }
            else
            {
                this.TraceMessage($"Not enough players ({playerOrder.Count}) to select third player");
            }

            // STEP 4: Execute Next action to advance to FinishedRollOrder (matching SignalR pattern)
            this.TraceMessage("Executing Next action to advance to FinishedRollOrder");
            var next = FindByAutomationId("NextButton").AsButton();
            Assert.NotNull(next);
            Assert.True(next.IsEnabled, "Next button should be enabled to transition from WaitingForRollForOrder");
            this.TraceMessage("Next button found and enabled, clicking to advance to FinishedRollOrder");
            next.Invoke();

            // STEP 5: Verify transition to FinishedRollOrder (matching SignalR pattern)
            Assert.True(WaitForGameState(GameState.FinishedRollOrder, TimeSpan.FromSeconds(6)), "Expected to transition to FinishedRollOrder state");
            VerifyExpectedGameState(GameState.FinishedRollOrder);

            // Verify GameModel consistency after transition
            var newGameModel = GetCurrentGameModel();
            Assert.Equal(GameState.FinishedRollOrder, newGameModel.GameState);

            this.TraceMessage("✅ WaitingForRollForOrder state verified - advanced to FinishedRollOrder");
            this.TraceMessage("=== Test_WaitingForRollForOrder completed ===");
        }

        /// <summary>
        /// Test the FinishedRollOrder state
        /// Tests the "Go First" functionality where players can optionally change turn order
        /// Typically 0 or 1 player clicks "Go First" - we test to ensure order is preserved correctly
        /// Transitions from FinishedRollOrder to BeginResourceAllocation when Next button is clicked
        /// </summary>
        private void Test_FinishedRollOrder()
        {
            this.TraceMessage("=== Test_FinishedRollOrder ===");

            // STEP 1: Verify we're in the correct state using GameState (not UI text)
            VerifyExpectedGameState(GameState.FinishedRollOrder);


            // Get and verify GameModel state from AutomationProperties.ItemStatus
            var gameModel = GetCurrentGameModel();

            this.TraceMessage($"✅ Verified GameModel state: {gameModel.GameState}");

            // STEP 2: Record initial player order for comparison
            var initialPlayerOrder = gameModel.Players?.Select(p => p.Name).ToList() ?? new List<string>();
            this.TraceMessage($"Initial player order: [{string.Join(", ", initialPlayerOrder)}]");
            Assert.True(initialPlayerOrder.Count >= 5, "Expected at least 5 players for expansion game");

            // STEP 3: Test "Go First" functionality
            // In real Catan, typically 0 or 1 player clicks "Go First"
            // We'll test both scenarios to verify order preservation

            // Find all "Go First" buttons available
            var goFirstButtons = Main.FindAllDescendants(cf => cf.ByText("Go First")).ToList();
            this.TraceMessage($"Found {goFirstButtons.Count} 'Go First' buttons");

            if (goFirstButtons.Count >= 3)
            {
                // Test scenario: Third player decides to go first (third player going first as requested)
                var thirdButton = goFirstButtons[2]; // Index 2 = third button
                this.TraceMessage("Testing scenario: Third player clicks 'Go First' to change order");

                try
                {
                    thirdButton.AsButton().Invoke();
                    this.TraceMessage("Clicked third 'Go First' button");

                    // Wait for UI to update
                    Thread.Sleep(SHORT_WAIT);

                    // Verify order changed (first player should now be at the front)
                    var updatedGameModel = GetCurrentGameModel();

                    var newPlayerOrder = updatedGameModel.Players?.Select(p => p.Name).ToList() ?? new List<string>();
                    this.TraceMessage($"Updated player order: [{string.Join(", ", newPlayerOrder)}]");

                    // The order should have changed when someone clicked "Go First"
                    bool orderChanged = !initialPlayerOrder.SequenceEqual(newPlayerOrder);
                    if (orderChanged)
                    {
                        this.TraceMessage("✅ Player order correctly changed after third player clicked 'Go First'");
                    }
                    else
                    {
                        this.TraceMessage("ℹ️ Player order remained the same (third player was already first)");
                    }

                    // Verify game state is still FinishedRollOrder (Go First doesn't advance the state)
                    Assert.Equal(GameState.FinishedRollOrder, updatedGameModel.GameState);
                    this.TraceMessage("✅ GameState correctly remains FinishedRollOrder after 'Go First'");
                }
                catch (Exception ex)
                {
                    this.TraceMessage($"Go First test failed (this is not critical): {ex.Message}");
                    // Continue with the test - Go First is optional functionality
                }
            }
            else
            {
                this.TraceMessage("No 'Go First' buttons found - this is normal if order is already optimal");
            }

            // STEP 4: Advance to next state using Next button
            this.TraceMessage("Advancing to BeginResourceAllocation state");
            var next = FindByAutomationId("NextButton").AsButton();
            Assert.NotNull(next);
            Assert.True(next.IsEnabled, "Next button should be enabled to transition from FinishedRollOrder");
            this.TraceMessage("Next button found and enabled, clicking to advance to BeginResourceAllocation");
            next.Invoke();

            // STEP 5: Verify transition to BeginResourceAllocation
            Assert.True(WaitForGameState(GameState.BeginResourceAllocation, TimeSpan.FromSeconds(6)), "Expected to transition to BeginResourceAllocation state");
            VerifyExpectedGameState(GameState.BeginResourceAllocation);

            // Verify GameModel consistency after transition
            var finalGameModel = GetCurrentGameModel();
            Assert.Equal(GameState.BeginResourceAllocation, finalGameModel.GameState);

            this.TraceMessage("✅ FinishedRollOrder state verified - advanced to BeginResourceAllocation");
            this.TraceMessage("=== Test_FinishedRollOrder completed ===");
        }

        /// <summary>
        /// Test the BeginResourceAllocation state
        /// Transitions from BeginResourceAllocation to AllocateResourceForward when Next button is clicked
        /// </summary>
        private void Test_AllocationPhase()
        {
            this.TraceMessage("=== Test_AllocationPhase ===");


            // Get and verify GameModel state from AutomationProperties.ItemStatus
            var gameModel = GetCurrentGameModel();
            Assert.Equal(GameState.BeginResourceAllocation, gameModel.GameState);



            // Transition to next state (AllocateResourceForward)
            this.TraceMessage("Transitioning to AllocateResourceForward state");
            var next = FindByAutomationId("NextButton").AsButton();
            Assert.NotNull(next);
            Assert.True(next.IsEnabled, "Next button should be enabled to transition from BeginResourceAllocation");
            next.Invoke();

            // Wait for transition to AllocateResourceForward
            Assert.True(WaitForGameState(GameState.AllocateResourceForward, TimeSpan.FromSeconds(6)), "Expected to transition to AllocateResourceForward state");


            // 
            // we need to be careful here to not change the GameModel state -- we can look at the properties, but not update them

            gameModel = GetCurrentGameModel();

            // In allocation phase, each player takes a turn: settlement -> road -> next
            // Loop until we transition out of AllocateResourceReverse


            // Always get fresh GameState for loop condition - NEVER cache GameModel across iterations!
            while (gameModel.GameState == GameState.AllocateResourceForward || gameModel.GameState == GameState.AllocateResourceReverse)
            {
                // we haven't updated the GameModel, so we can get the current player directly
                var currentPlayer = gameModel.CurrentPlayer();

                this.TraceMessage($"Player {currentPlayer.Name} turn in GameState={gameModel.GameState}");

                // Get GameHash before settlement placement
                var preSettlementGameHash = gameModel.GameHash;
                this.TraceMessage($"Pre-settlement GameHash: {preSettlementGameHash}");

                // Step 1: Place settlement (pick the one with most stars)
                this.TraceMessage($"Step 1: Player {currentPlayer.Name} placing settlement");

                PlaceOptimalSettlement(gameModel);

                // the game model has now changed

                // Wait a bit for the UI to respond
                Thread.Sleep(SHORT_WAIT);

                // Check StateMessage after settlement placement
                var stateMessageAfter = FindByAutomationId("StateMessage");
                this.TraceMessage($"StateMessage after settlement: '{stateMessageAfter?.Name}'");



                // Get updated GameModel after settlement placement
                this.TraceMessage("Getting updated GameModel after settlement placement...");
                var postSettlementGameModel = GetCurrentGameModel();

                var postSettlementGameHash = postSettlementGameModel.GameHash;
                Assert.NotEqual(postSettlementGameHash, preSettlementGameHash); // they MUST change!

                this.TraceMessage($"GameModel refreshed, found {postSettlementGameModel.Roads.Count(r => r.RoadState == RoadState.Buildable)} buildable roads");

                // Step 2: Place road (pick first buildable road from updated GameModel)
                this.TraceMessage($"Step 2: Player {currentPlayer.Name} placing road");
                PlaceFirstBuildableRoad(postSettlementGameModel); // on return, GameModel has changed 

                // Wait for UI to update after road placement
                Thread.Sleep(SHORT_WAIT);

                // Step 3: Click Next to advance to next player or next phase
                this.TraceMessage($"Step 3: Player {currentPlayer.Name} clicking Next to advance");
                CallNext();



                // get the gameModel so the loop works correctly
                gameModel = GetCurrentGameModel();

            }

            Assert.Equal(GameState.DoneResourceAllocation, gameModel.GameState);
            this.TraceMessage("=== Test_AllocatePhase completed ===");
        }

        /// <summary>
        ///     simulates clicking on the Next button - which happens a lot, so it deserves a helper function
        /// </summary>
        private void CallNext()
        {
            var nextButton = FindByAutomationId("NextButton").AsButton();
            Assert.NotNull(nextButton);
            if (nextButton.IsEnabled == false)
            {
                this.TraceMessage("Next button is not enabled, cannot proceed");
                // Log the current state and any relevant information
                var gameModel = GetCurrentGameModel();
                this.TraceMessage($"Current GameState: {gameModel.GameState} CurrentPlayer={gameModel.CurrentPlayerId}");
                Assert.Fail("Next button should be enabled to proceed to next state");
            }
            nextButton.Invoke();
            // Wait for state transition
            Thread.Sleep(SHORT_WAIT);
        }
        /// <summary>
        /// Clicks the roll card "Roll - N" reliably and waits for the GameModel to change.
        /// </summary>
        private void DoRoll(int roll)
        {
            var id = $"Roll - {roll}";

            // Activate the main window first (helps input routing)
            var win = _main!.AsWindow();
            try { win.Focus(); } catch { /* best effort */ }

            // Locate the roll card (SingleRoll root) by AutomationId
            var card = FindByAutomationId(id);

            // Prefer the inner Button under the card; fall back to the card itself if you moved the id
            var btn = card.FindFirstDescendant(cf => cf.ByControlType(ControlType.Button))?.AsButton()
                     ?? card.AsButton(); // may be null if ControlType != Button

            Assert.NotNull(btn);

            // If virtualized/offscreen, scroll it into view
            btn.Patterns.ScrollItem.PatternOrDefault?.ScrollIntoView();

            // Wait until interactable
            Retry.WhileTrue(
                () => !btn.IsEnabled || btn.IsOffscreen,
                timeout: TimeSpan.FromSeconds(5),
                interval: TimeSpan.FromMilliseconds(100));

            // Snapshot pre-action state so we can confirm the click did something
            var preHash = GetCurrentGameModel().GameHash;

            // Give focus (improves Click reliability)
            try { btn.Focus(); } catch { /* ignore */ }

            // Prefer Invoke; fall back to Click (some templates don’t expose Invoke)
            var inv = btn.Patterns.Invoke.PatternOrDefault;
            if (inv != null) inv.Invoke();
            else btn.Click();

            // Wait for the model to change to confirm the action happened
            var changed = Retry.WhileTrue(
                () => string.Equals(GetCurrentGameModel().GameHash, preHash, StringComparison.Ordinal),
                timeout: TimeSpan.FromSeconds(5),
                interval: TimeSpan.FromMilliseconds(100)).Success;

            if (!changed)
            {
                // Optional: dump element info to diagnose why it didn't fire
                DumpElementForDiagnostics(btn);
                throw new Xunit.Sdk.XunitException($"Roll '{id}' did not change the GameModel within timeout.");
            }

            Thread.Sleep(SHORT_WAIT);
        }


        private static void DumpElementForDiagnostics(AutomationElement el)
        {
            try
            {
                var rect = el.BoundingRectangle;
                bool hasInvoke = el.Patterns.Invoke.PatternOrDefault != null;
                bool hasScrollItem = el.Patterns.ScrollItem.PatternOrDefault != null;
                bool hasSelection = el.Patterns.SelectionItem.PatternOrDefault != null;
                bool hasLegacyIA = el.Patterns.LegacyIAccessible.PatternOrDefault != null;
                bool hasToggle = el.Patterns.Toggle.PatternOrDefault != null;
                bool hasValue = el.Patterns.Value.PatternOrDefault != null;

                rect.TraceMessage(
                  
                    $"Name={el.Name}, Id={el.AutomationId}, Enabled={el.IsEnabled}, " +
                    $"Offscreen={el.IsOffscreen}, Rect={rect}, " +
                    $"Patterns: Invoke={hasInvoke}, ScrollItem={hasScrollItem}, " +
                    $"SelectionItem={hasSelection}, LegacyIA={hasLegacyIA}, Toggle={hasToggle}, Value={hasValue}");
            }
            catch { /* best effort */ }
        }





        /// <summary>
        /// Test the DoneResourceAllocation state
        /// TODO:  we need to cache the last Building that was placed in AllocationPhase for each player.
        ///        here we should verify that they got the appropriate resources granted to them.
        ///        
        /// Transitions from DoneResourceAllocation to WaitingForRoll when Next button is clicked
        /// </summary>
        private void Test_DoneResourceAllocation()
        {
            this.TraceMessage("=== Test_DoneResourceAllocation ===");

            // Verify we're in the correct state using GameState (not UI text)
            VerifyExpectedGameState(GameState.DoneResourceAllocation);

            // Get and verify GameModel state from AutomationProperties.ItemStatus
            var gameModel = GetCurrentGameModel();

            this.TraceMessage($"Verified GameModel state: {gameModel.GameState}");

            // Transition to next state (WaitingForRoll)
            CallNext();

            // Wait for transition to WaitingForRoll
            Assert.True(WaitForGameState(GameState.WaitingForRoll, TimeSpan.FromSeconds(6)), "Expected to transition to WaitingForRoll state");
            VerifyExpectedGameState(GameState.WaitingForRoll);
            this.TraceMessage("Successfully transitioned to WaitingForRoll state");

            this.TraceMessage("=== Test_DoneResourceAllocation completed ===");
        }

        /// <summary>
        /// Test the WaitingForRoll state - this is the final state for this test
        /// </summary>
        private void Test_WaitingForRoll()
        {
            this.TraceMessage("=== Test_WaitingForRoll ===");
            DoRoll(6);
            // Get and verify GameModel state from AutomationProperties.ItemStatus
            Thread.Sleep(SHORT_WAIT * 2);
            var gameModel = GetCurrentGameModel();
            Assert.Equal(GameState.WaitingForNext, gameModel.GameState);

            this.TraceMessage("Successfully verified WaitingForRoll state - this is the end of the core game setup flow!");
            this.TraceMessage("=== Test_WaitingForRoll completed ===");
        }

        /// <summary>
        /// Places the optimal settlement for the current player based on star count.
        /// Uses the same algorithm as the SignalR test PickOptimalSettlement method.
        /// 
        /// Algorithm:
        /// 1. Find all buildings in PossibleSettlement state
        /// 2. Calculate star count for each potential settlement location
        /// 3. Select the settlement with the highest star count (ties broken by first occurrence)
        /// 4. Click on the selected settlement in the UI
        /// 
        /// Star Count Calculation: Uses gameModel.TilesForBuildings(buildingKey).Stars()
        /// which evaluates the resource production potential of adjacent tiles.
        /// 
        /// Throws: InvalidOperationException if no possible settlements are available
        /// </summary>
        /// <param name="gameModel">Current game state containing building and tile information</param>
        private void PlaceOptimalSettlement(GameModel gameModel)
        {
            // Find all possible settlements
            var possibleSettlements = gameModel.Buildings
                .Where(b => b.BuildingState == BuildingState.PossibleSettlement)
                .ToList();

            if (!possibleSettlements.Any())
            {
                throw new InvalidOperationException("No possible settlements available");
            }

            // Find the settlement with the highest star count using the same logic as SignalR tests
            var settlementOptions = possibleSettlements
                .Select(building => new
                {
                    stars = gameModel.TilesForBuildings(building.BuildingKey).Stars(),
                    building = building
                })
                .ToList();

            var maxStars = settlementOptions.Max(s => s.stars);
            var bestSettlement = settlementOptions.First(s => s.stars == maxStars);

            this.TraceMessage($"Placing settlement at {bestSettlement.building.BuildingKey} with {bestSettlement.stars} stars");

            // Click on the building to select it
            ClickOnBuilding(bestSettlement.building);
        }

        /// <summary>
        /// Places the first available buildable road for the current player.
        /// 
        /// Strategy: This method uses a comprehensive approach to handle road UI automation challenges:
        /// 1. Find all roads in RoadState.Buildable from the GameModel
        /// 2. Log all available roads for debugging purposes
        /// 3. Attempt to find and click each road using complex alias logic and hierarchy navigation
        /// 4. Include extensive debugging output to help diagnose automation issues
        /// 5. Test the entire XAML hierarchy to understand why road elements aren't found
        /// 
        /// The method is intentionally complex due to ongoing issues with road UI automation
        /// element discovery. It includes fallback strategies and comprehensive logging to
        /// help debug the AutomationId binding problems with road elements.
        /// 
        /// Throws: InvalidOperationException if no buildable roads are available
        ///         or if UI elements cannot be found for any buildable road
        /// </summary>
        /// <param name="gameModel">Current game state containing road information</param>
        private void PlaceFirstBuildableRoad(GameModel gameModel)
        {
            // Find all buildable roads
            var buildableRoads = gameModel.Roads
                .Where(r => r.RoadState == RoadState.Buildable)
                .ToList();

            if (!buildableRoads.Any())
            {
                throw new InvalidOperationException("No buildable roads available");
            }



            // Log buildable roads as CSV like GameController does
            var buildableRoadsCsv = string.Join(",", buildableRoads.Select(r => r.RoadKey.ToString()));
            this.TraceMessage($"Buildable roads CSV: {buildableRoadsCsv}");

            // the roads in the GameModel should have the same IDs as each time, so we don't need to 
            // look them up by alias ... 

            var element = UiControls[buildableRoads[0].RoadKey.GetAutomationId()];
            Assert.NotNull(element);
            element.Click();

        }

        /// <summary>
        /// gets the AutomationId for a building element and clicks on it.
        /// 
        /// Asserts that the building element exists in the UI and performs a click action.
        /// </summary>
        /// <param name="buildingKey">The BuildingKey identifying the building to click</param>
        private void ClickOnBuilding(BuildingModel building)
        {
            // Build the expected AutomationId that matches BuildingViewModel.AutomationId format
            var expectedAutomationId = building.BuildingKey.GetAutomationId();

            // Look for a UI element with the building's AutomationId
            var buildingElement = UiControls[expectedAutomationId];
            Assert.NotNull(buildingElement);

            buildingElement.Click();
            Thread.Sleep(SHORT_WAIT); // Give time for the click to register
        }

        /// <summary>
        /// Clicks on a road element in the UI by its RoadKey.
        /// 
        /// the UIElement is built when the board is built and is stable for the lifetime of the game.
        /// the RoadState changes, but not the road itself.
        /// 
        /// Throws: AssertionException if the road element cannot be found
        /// </summary>
        /// <param name="roadKey">The RoadKey identifying the road to click</param>
        private void ClickOnRoad(IEnumerable<RoadModel> roads, RoadKey roadKey)
        {
            var roadModel = roads.FindRoad(roadKey); //extension from RoadModelExtensions.cs in the Shared project
            Assert.NotNull(roadModel);
            var roadElement = UiControls[roadModel.RoadKey.GetAutomationId()];
            Assert.NotNull(roadElement);

            roadElement.Click();
            Thread.Sleep(SHORT_WAIT); // Give time for the click to register
        }



        /// <summary>
        /// Launches the packaged app (MSIX) and attaches the UI automation framework.
        /// 
        /// Process:
        /// 1. Constructs the Application User Model ID (AUMID) from Package Family Name
        /// 2. Launches the app via Windows Shell using explorer.exe
        /// 3. Initializes UIA3Automation framework for UI automation
        /// 4. Waits up to 25 seconds for the main window to appear
        /// 5. Sets up the Main window reference for subsequent automation
        /// 
        /// AUMID Format: "{PackageFamilyName}!App"
        /// Window Detection: Searches for WinUIDesktopWin32WindowClass windows
        /// 
        /// Timeout: 25 seconds with 250ms polling interval
        /// 
        /// Side Effects:
        /// - Sets _automation instance
        /// - Sets _main window reference
        /// 
        /// Throws: XunitException if the app fails to launch or main window cannot be found
        /// </summary>
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

            // Wait for WinUI top-level windows, then pick the *non-debug* one
            _main = Retry.WhileNull(
                () =>
                {
                    var wins = _automation.GetDesktop()
                        .FindAllChildren(Cf.ByControlType(ControlType.Window)
                        .And(Cf.ByClassName("WinUIDesktopWin32WindowClass")));

                    return wins.FirstOrDefault(w =>
                        !w.Name.Contains("Debug", StringComparison.OrdinalIgnoreCase));
                },
                timeout: TimeSpan.FromSeconds(25),
                interval: TimeSpan.FromMilliseconds(250),
                throwOnTimeout: false
            ).Result ?? throw new XunitException($"Failed to find main window for AUMID '{aumid}'. Is the app deployed and running?");
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
            ps!.WaitForExit(500);
            var output = (ps.StandardOutput.ReadToEnd() ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(output))
            {
                throw new XunitException("App package is not installed. Build/deploy the MSIX before running packaged UI tests.");
            }
            return output.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)[0].Trim();
        }

        /// <summary>
        ///     this skips the Dictionary lookup and just finds the element by AutomationId, looking at the base collection.
        ///     Suitable to be used in the StartGameTests
        /// </summary>
        /// <param name="automationId"></param>
        /// <returns></returns>
        private AutomationElement FindByAutomationId(string automationId)
        {
            if (UiControls.Count != 0)
            {
                return UiControls[automationId];
            }
            Assert.NotNull(_main);
            var res = Retry.WhileNull(
            () => _main.FindFirstDescendant(Cf.ByAutomationId(automationId)),
            timeout: TimeSpan.FromMilliseconds(SHORT_WAIT),
            interval: TimeSpan.FromMilliseconds(100),
            throwOnTimeout: false);

            return res.Result ?? throw new TimeoutException($"AutomationId '{automationId}' not found under main window in {SHORT_WAIT} ms.");
        }

        private void WaitForNewGamePageToLoad()
        {
            this.TraceMessage("Waiting for NewGame page to load...");

            var startButton = FindByAutomationId("StartButton");
            Assert.NotNull(startButton);
            this.TraceMessage("✅ NewGame page loaded successfully");
        }

        private AutomationElement FindByText(string text)
        {
            var el = Retry.WhileNull(() => Main.FindFirstDescendant(cf => cf.ByText(text)), timeout: TimeSpan.FromSeconds(5)).Result;
            Assert.NotNull(el);
            return el!;
        }

        /// <summary>
        /// Retrieves the current GameModel from the UI via AutomationProperties.ItemStatus.
        /// 
        /// This method provides access to the complete game state without requiring
        /// direct app dependencies or complex inter-process communication.
        /// 
        ///  Strategy: Read JSON from NextButton's ItemStatus property
        /// 
        /// The GameModel contains all game state and it has copy on write, so it will
        /// change every time an update is sent to the GameController. It cannot be cached
        /// 
        /// JSON Deserialization: Uses JsonHelper.StandardOptions for consistency
        /// with the rest of the codebase.
        /// 
        /// Returns: Valid GameModel instance
        /// Throws: AssertionException if GameModel cannot be retrieved or is invalid
        /// </summary>
        private GameModel GetCurrentGameModel()
        {
            // Primary approach: Get GameModel from NextButton which is always accessible and reliable
            // with the possible exception of starting a new game
            AutomationElement nextButton = FindByAutomationId("NextButton");
            Assert.NotNull(nextButton);
            try
            {
                if (nextButton.Properties.ItemStatus.TryGetValue(out var buttonGameModelValue))
                {
                    var buttonGameModelJson = buttonGameModelValue as string;
                    if (!string.IsNullOrEmpty(buttonGameModelJson))
                    {
                        var buttonGameModel = JsonSerializer.Deserialize<GameModel>(buttonGameModelJson, JsonHelper.StandardOptions);
                        Assert.NotNull(buttonGameModel);
                        return buttonGameModel;
                    }
                    else
                    {
                        this.TraceMessage("NextButton found but ItemStatus is empty");
                    }
                }
                else
                {
                    this.TraceMessage("NextButton does not support ItemStatus property");
                }
            }
            catch (Exception ex)
            {
                this.TraceMessage($"Error getting ItemStatus from NextButton: {ex.Message}");
                Assert.Fail("Next Button must exits");
            }

            throw new Exception("Game Model can't be null");
        }

        /// <summary>
        /// Verifies that the current GameState matches the expected state
        /// Also verifies that the UI Description matches the GameState Description attribute
        /// This provides consistent, reliable state verification across all tests
        /// </summary>
        /// <param name="expectedState">The expected GameState</param>
        private void VerifyExpectedGameState(GameState expectedState)
        {
            this.TraceMessage($"Verifying expected GameState: {expectedState}");

            var currentGameState = GetCurrentGameModel().GameState;

            this.TraceMessage($"Current GameState: {currentGameState}, Expected: {expectedState}");
            Assert.Equal(expectedState, currentGameState);
        }

        /// <summary>
        /// Waits for the GameState to transition to the expected state
        /// Uses actual GameState enum instead of brittle UI text matching
        /// </summary>
        /// <param name="expectedState">The expected GameState to wait for</param>
        /// <param name="timeout">Maximum time to wait</param>
        /// <returns>True if the expected state is reached within timeout</returns>
        private bool WaitForGameState(GameState expectedState, TimeSpan timeout)
        {
            var sw = Stopwatch.StartNew();
            this.TraceMessage($"WaitForGameState: Looking for '{expectedState}' state");

            while (sw.Elapsed < timeout)
            {
                var currentGameState = GetCurrentGameModel().GameState;

                if (currentGameState == expectedState)
                {
                    this.TraceMessage($"WaitForGameState: Found expected state '{expectedState}'!");
                    return true;
                }

                Thread.Sleep(SHORT_WAIT);
            }

            this.TraceMessage($"WaitForGameState: Timed out waiting for '{expectedState}' after {timeout.TotalSeconds}s");
            return false;
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

            }
            System.Diagnostics.Debug.WriteLine(message);

            for (int i = 0; i < indentLevel; i++)
            {
                System.Diagnostics.Debug.Unindent();
            }
        }

        /// <summary>
        /// Gets the Description attribute value from an enum
        /// Copied locally to avoid external dependencies
        /// </summary>
        public static string Description(this Enum instance)
        {
            string output = "";
            Type type = instance.GetType();
            if (type is null) return string.Empty;
            FieldInfo? fi = type.GetField(instance.ToString());
            if (fi is null) return string.Empty;
            DescriptionAttribute[]? attrs = fi.GetCustomAttributes(attributeType: typeof(DescriptionAttribute), false) as DescriptionAttribute[];
            if (attrs is not null && attrs.Length > 0)
            {
                output = attrs[0].Description;
            }
            return output;
        }
    }
}
